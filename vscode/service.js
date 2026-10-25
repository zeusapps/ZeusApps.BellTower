'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

// C# VsCodeNavigator treats a registration older than 15 seconds as not live.
const OwnershipTtlMs = 15000;
const LogIntervalMs = 60000;
const Protocol = 2;
const EditorWindowClass = 'Chrome_WidgetWin_1';
const SampleRetryMs = 3000;
const SampleAttempts = 5;

// Writes through a uniquely named temporary file. A failed write or rename removes only that
// temporary path and never touches the previous valid destination.
function atomic(file, value, io = fs) {
  io.mkdirSync(path.dirname(file), { recursive: true });
  const temporary = file + '.' + crypto.randomUUID() + '.tmp';
  try {
    io.writeFileSync(temporary, JSON.stringify(value), { encoding: 'utf8', flag: 'wx' });
    io.renameSync(temporary, file);
  } catch (error) {
    try {
      io.unlinkSync(temporary);
    } catch {
      // The temporary file may never have been created.
    }
    throw error;
  }
}

class Bridge {
  constructor(options) {
    Object.assign(this, options);
    this.fs = this.fs || fs;
    this.busy = false;
    this.lastPublishedAt = 0;
    this.lastLogged = new Map();
    this.suppressed = new Map();
    // Window evidence: the foreground window observed while this instance reported its window focused.
    this.window = null;
    // Only an observed focus-gain event (or the first user interaction in an already focused window) starts
    // a sample. The initial window state is not trusted: a new window can report focused before the OS has
    // made it the foreground window.
    this.windowDirty = false;
    this.windowAttemptAt = 0;
    this.windowAttempts = 0;
    // Incremented by every focus or blur event so a sample that straddles a focus change is discarded.
    this.generation = 0;
    this.sampling = null;
    this.seenHandles = new Set();
    // Why the published window is null when it is more than not yet observed. Permanent for this instance.
    this.windowReason = undefined;
    this.lastTerminals = [];
    this.lastFocused = this.isFocused ? this.isFocused() === true : false;
    this.lastActive = false;
  }

  // Every window-state event feeds this. Blur events matter as much as focus events: they invalidate any
  // sample taken around them.
  noteWindowState(state) {
    this.generation++;
    const gained = state.focused === true && !this.lastFocused;
    const interacted = state.focused === true && state.active === true && !this.lastActive &&
      this.window === null && this.seenHandles.size === 0;
    this.lastFocused = state.focused === true;
    this.lastActive = state.active === true;
    // An interaction event proves input reached this window after load; it is only needed when the window was
    // already focused at load, where no focus-gain event will arrive. The same checks as a focus gain apply.
    return gained || interacted ? this.noteFocus() : Promise.resolve();
  }

  // Called when this editor window gains focus; the mapping is sampled at once, not on the next tick, so the
  // foreground cannot move on before it is observed.
  noteFocus() {
    this.windowDirty = true;
    this.windowAttemptAt = 0;
    this.windowAttempts = 0;
    this.generation++;
    return this.refreshWindow().catch(() => {});
  }

  // Resolves when any in-flight sampling has finished.
  async idle() {
    while (this.sampling) await this.sampling.catch(() => {});
  }

  // Starts the sampling of the window mapping. A request that arrives while a sample is in flight is never
  // dropped: the generation counter makes the running loop discard its sample and sample again.
  refreshWindow() {
    if (this.sampling) return this.sampling;
    if (!this.sampleWindow || !this.isFocused || !this.windowDirty) return Promise.resolve();
    this.sampling = this.sampleLoop().finally(() => { this.sampling = null; });
    return this.sampling;
  }

  async sampleLoop() {
    for (;;) {
      if (!this.windowDirty || !this.isFocused()) return;
      const now = this.now();
      if ((this.windowAttemptAt !== 0 && now - this.windowAttemptAt < SampleRetryMs) || this.windowAttempts >= SampleAttempts) return;
      this.windowAttemptAt = now;
      this.windowAttempts++;
      const generation = this.generation;
      let sample = null;
      try {
        sample = await this.sampleWindow();
      } catch {
        sample = null;
      }
      // Focus moved (any focus or blur event) while sampling, or the window is no longer focused: the sample may
      // belong to another window. Keep the window dirty and sample again instead of recording or dropping it.
      if (generation !== this.generation || !this.isFocused()) {
        this.windowAttemptAt = 0;
        this.windowAttempts = Math.max(0, this.windowAttempts - 1);
        if (!this.isFocused()) return;
        continue;
      }
      if (!sample || sample.unavailable === true) {
        // The sampler could not run (spawn failure, timeout, unstable sample): not evidence either way.
        // Keep any earlier evidence; the tray re-verifies everything at click time. Retry later.
        this.report('window-unavailable', 'The foreground window could not be sampled; the earlier window record is kept and sampling will be retried.');
        return;
      }
      const valid = typeof sample.handle === 'string' && /^[1-9][0-9]{0,18}$/.test(sample.handle) &&
        sample.className === EditorWindowClass && Number.isInteger(sample.ownerPid) &&
        (this.expectedOwnerPid === undefined || sample.ownerPid === this.expectedOwnerPid) &&
        typeof sample.ownerStartedUtcTicks === 'string';
      if (!valid) {
        this.window = null;
        this.report('window', 'The foreground window is not this editor window; native activation stays unavailable until it is observed.');
        this.changed();
        return;
      }
      this.seenHandles.add(sample.handle);
      if (this.seenHandles.size > 1) {
        // More than one top-level window has been seen for this instance (for example a floating editor
        // window). Which one is the bridge own cannot be proven, so none is published.
        this.window = null;
        this.windowReason = 'multiple';
        this.report('window-multiple', 'More than one top-level window was observed for this editor instance; native activation stays unavailable.');
        this.windowDirty = false;
        this.changed();
        return;
      }
      this.window = {
        handle: sample.handle,
        owner: { pid: sample.ownerPid, startedUtcTicks: sample.ownerStartedUtcTicks },
        className: sample.className,
        observedAt: new Date(now).toISOString()
      };
      this.windowDirty = false;
      this.changed();
      return;
    }
  }

  // Publishes a changed window record at once (best effort) so the host does not wait for the next tick.
  changed() {
    if (this.lastPublishedAt) this.publish(this.lastTerminals);
  }

  // A protocol 2 reveal failed although the host reported activating this bridge recorded window, yet the
  // bridge own window is not focused: the record is wrong. Forget it and sample again on the next focus gain.
  selfHeal(request) {
    if (this.isFocused && this.isFocused()) return;
    if (!this.window || request.windowHandle === undefined || request.windowHandle === null) return;
    if (String(request.windowHandle) !== this.window.handle) return;
    this.window = null;
    this.seenHandles.delete(request.windowHandle.toString());
    this.windowDirty = false;
    this.report('window-heal', 'A window activation did not focus this editor window; the window record was cleared.');
    this.changed();
  }

  // Bounded diagnostics: at most one line per key each minute, with a count of suppressed repeats.
  report(key, message) {
    if (!this.log) return;
    const now = this.now();
    const last = this.lastLogged.get(key);
    if (last !== undefined && now - last < LogIntervalMs) {
      this.suppressed.set(key, (this.suppressed.get(key) || 0) + 1);
      return;
    }
    const skipped = this.suppressed.get(key) || 0;
    this.suppressed.delete(key);
    this.lastLogged.set(key, now);
    this.log(skipped ? message + ' (' + skipped + ' similar messages suppressed)' : message);
  }

  // Registration publication is recoverable: a failure leaves the previous registration in place and
  // is retried on the next tick without blocking verified focus processing.
  publish(terminals) {
    try {
      atomic(path.join(this.root, 'hosts', this.instanceId + '.json'), {
        instanceId: this.instanceId,
        process: this.processIdentity,
        updatedAt: new Date(this.now()).toISOString(),
        protocol: Protocol,
        window: this.window,
        windowUnavailable: this.window ? undefined : this.windowReason,
        terminalPids: terminals.map(terminal => terminal.pid)
      }, this.fs);
      this.lastPublishedAt = this.now();
      return true;
    } catch {
      this.report('registration', 'Bridge registration could not be written; it will be retried.');
      return false;
    }
  }

  ownershipLive() {
    return this.lastPublishedAt > 0 && this.now() - this.lastPublishedAt < OwnershipTtlMs;
  }

  async tick() {
    if (this.busy) return;
    this.busy = true;
    try {
      const terminals = await this.terminals();
      this.lastTerminals = terminals;
      this.publish(terminals);
      const folder = path.join(this.root, 'focus');
      let names;
      try {
        this.fs.mkdirSync(folder, { recursive: true });
        names = this.fs.readdirSync(folder);
      } catch {
        this.report('focus-folder', 'Bridge focus folder is unavailable; it will be retried.');
        return;
      }
      for (const name of names) {
        if (!/^[a-f0-9]{32}\.json$/.test(name)) continue;
        try {
          await this.process(folder, name, terminals);
        } catch {
          // A request that cannot be answered stays queued and expires on its own; others still run.
          this.report('request', 'A focus request could not be completed; it will be retried until it expires.');
        }
      }
      // Sampling never delays request processing: it starts after requests were served and republishes itself.
      this.refreshWindow().catch(() => {});
    } finally {
      this.busy = false;
    }
  }

  async process(folder, name, terminals) {
    const file = path.join(folder, name);
    let request;
    try {
      if (this.fs.statSync(file).size > 16384) return;
      request = JSON.parse(this.fs.readFileSync(file, 'utf8'));
    } catch {
      return;
    }
    if (request.instanceId !== this.instanceId || typeof request.id !== 'string' || request.id + '.json' !== name) {
      return;
    }
    let result = {
      success: false,
      precision: 'unavailable',
      message: 'The focus target is stale or unavailable.'
    };
    if (!this.ownershipLive()) {
      // Never act on, or imply, an expired registration; answer honestly and let the host retry.
      result.message = 'The bridge registration is not currently live.';
    } else {
      const terminal = terminals.find(terminal => terminal.pid === request.shellPid);
      const info = terminal ? await this.processInfo(terminal.pid) : null;
      if (
        Date.parse(request.expiresAt) > this.now() && terminal &&
        info && String(info.startedUtcTicks) === request.shellStartedUtcTicks
      ) {
        // Protocol 2 hosts activate the verified native window themselves and ask only for the terminal reveal.
        const revealOnly = request.protocol >= Protocol && request.mode === 'reveal' && typeof this.reveal === 'function';
        try {
          const done = revealOnly ? await this.reveal(terminal.terminal) : await this.focus(terminal.terminal);
          if (revealOnly && done !== true) this.selfHeal(request);
          result = {
            success: done === true,
            precision: 'exact terminal',
            protocol: Protocol,
            message: revealOnly
              ? (done === true ? 'The registered terminal was revealed.' : 'VS Code did not confirm the terminal reveal.')
              : (done === true ? 'The owning VS Code window and registered terminal were focused.' :
                'VS Code did not confirm both window and terminal focus.')
          };
        } catch {
          result = { success: false, precision: 'exact terminal', message: 'VS Code could not focus the owning window and terminal.' };
        }
      }
    }
    atomic(path.join(folder, request.id + '.result'), result, this.fs);
    try {
      this.fs.unlinkSync(file);
    } catch (error) {
      // Already gone (for example the host removed it after reading the receipt): nothing left to do.
      if (!error || error.code !== 'ENOENT') throw error;
    }
  }
}

module.exports = { Bridge, atomic, OwnershipTtlMs };
