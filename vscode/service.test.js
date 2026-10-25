'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { Bridge } = require('./service');

function fixture(t) {
  const parent = path.resolve(__dirname, '../artifacts/bridge-tests');
  const root = path.join(parent, crypto.randomBytes(16).toString('hex'));
  fs.mkdirSync(path.join(root, 'focus'), { recursive: true });
  t.after(() => {
    if (path.dirname(root) !== parent) throw new Error('Unsafe cleanup');
    fs.rmSync(root, { recursive: true });
  });
  return root;
}

function request(root, fields = {}) {
  const id = crypto.randomBytes(16).toString('hex');
  const value = {
    id,
    instanceId: 'window-a',
    attachmentId: 'launch',
    shellPid: 42,
    shellStartedUtcTicks: '639000000000000000',
    expiresAt: new Date(Date.now() + 5000).toISOString(),
    ...fields
  };
  fs.writeFileSync(path.join(root, 'focus', id + '.json'), JSON.stringify(value));
  return value;
}

function bridge(root, focus, extra = {}) {
  return new Bridge({
    root,
    instanceId: 'window-a',
    processIdentity: { pid: 1, startedUtcTicks: '1' },
    now: Date.now,
    terminals: async () => [{ pid: 42, terminal: 'correct' }],
    processInfo: async () => ({ pid: 42, startedUtcTicks: '639000000000000000' }),
    focus,
    ...extra
  });
}
test('focus goes to the registered terminal and acknowledges', async t => {
  const root = fixture(t);
  const r = request(root);
  const seen = [];
  await bridge(root, terminal => { seen.push(terminal); return true; }).tick();
  assert.deepEqual(seen, ['correct']);
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, true);
});
test('another window does not consume the request', async t => {
  const root = fixture(t);
  const r = request(root, { instanceId: 'window-b' });
  await bridge(root, () => assert.fail('Wrong window focused')).tick();
  assert.equal(fs.existsSync(path.join(root, 'focus', r.id + '.json')), true);
});
test('expired and reused PID destinations do not focus', async t => {
  for (const fields of [{ expiresAt: '2020-01-01T00:00:00Z' }, { shellStartedUtcTicks: '2' }, { shellPid: 99 }]) {
    const root = fixture(t);
    const r = request(root, fields);
    await bridge(root, () => assert.fail('Stale terminal focused')).tick();
    assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, false);
  }
});
test('invalid JSON cannot break valid requests', async t => {
  const root = fixture(t);
  fs.writeFileSync(path.join(root, 'focus', 'a'.repeat(32) + '.json'), 'broken');
  const r = request(root);
  await bridge(root, () => true).tick();
  assert.equal(fs.existsSync(path.join(root, 'focus', r.id + '.result')), true);
});

test('foreground failure is acknowledged without claiming success', async t => {
  const root = fixture(t);
  const r = request(root);
  await bridge(root, () => false).tick();
  const result = JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result')));
  assert.equal(result.success, false);
  assert.match(result.message, /window and terminal focus/);
});

test('focus command failures produce a receipt and do not strand requests', async t => {
  const root = fixture(t);
  const r = request(root);
  await bridge(root, () => { throw new Error('Unavailable focus command'); }).tick();
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, false);
  assert.equal(fs.existsSync(path.join(root, 'focus', r.id + '.json')), false);
});

function tmpFiles(root) {
  const found = [];
  const walk = folder => {
    for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
      const full = path.join(folder, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith('.tmp')) found.push(full);
    }
  };
  walk(root);
  return found;
}

// Wraps the real file system so a chosen operation fails a limited number of times.
function failing(operation, times) {
  let remaining = times;
  const io = { ...fs };
  io[operation] = (...args) => {
    if (remaining > 0) {
      remaining--;
      throw new Error('Injected ' + operation + ' failure');
    }
    return fs[operation](...args);
  };
  return io;
}

for (const operation of ['renameSync', 'writeFileSync']) {
  test('injected ' + operation + ' failure leaves no temporary file and the next tick recovers', async t => {
    const root = fixture(t);
    const logs = [];
    const options = { fs: failing(operation, 1), log: line => logs.push(line) };
    const instance = bridge(root, () => true, options);
    await instance.tick();
    assert.deepEqual(tmpFiles(root), []);
    assert.equal(fs.existsSync(path.join(root, 'hosts', 'window-a.json')), false);
    assert.equal(logs.length, 1);
    await instance.tick();
    assert.deepEqual(tmpFiles(root), []);
    assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'hosts', 'window-a.json'))).instanceId, 'window-a');
  });
}

test('a failed refresh never deletes the previous valid registration', async t => {
  const root = fixture(t);
  const options = { fs: failing('renameSync', 0) };
  const instance = bridge(root, () => true, options);
  await instance.tick();
  const before = fs.readFileSync(path.join(root, 'hosts', 'window-a.json'), 'utf8');
  options.fs = failing('renameSync', 1);
  instance.fs = options.fs;
  await instance.tick();
  assert.equal(fs.readFileSync(path.join(root, 'hosts', 'window-a.json'), 'utf8'), before);
  assert.deepEqual(tmpFiles(root), []);
});

test('a transient registration failure does not starve a request while ownership is still live', async t => {
  const root = fixture(t);
  let now = 1000000;
  const options = { now: () => now, fs: failing('renameSync', 0) };
  const instance = bridge(root, () => true, options);
  await instance.tick();
  now += 5000;
  instance.fs = failing('renameSync', 1);
  const r = request(root, { expiresAt: new Date(now + 5000).toISOString() });
  await instance.tick();
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, true);
  assert.deepEqual(tmpFiles(root), []);
});

test('expired ownership is never used to focus and receives an honest failure', async t => {
  const root = fixture(t);
  let now = 1000000;
  const options = { now: () => now, fs: failing('renameSync', 0) };
  const instance = bridge(root, () => assert.fail('Expired ownership focused'), options);
  await instance.tick();
  now += 20000;
  instance.fs = failing('renameSync', 1);
  const r = request(root, { expiresAt: new Date(now + 5000).toISOString() });
  await instance.tick();
  const result = JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result')));
  assert.equal(result.success, false);
  assert.match(result.message, /not currently live/);
  instance.fs = fs;
  await instance.tick();
  const again = request(root, { expiresAt: new Date(now + 5000).toISOString() });
  await instance.tick();
  assert.equal(fs.existsSync(path.join(root, 'focus', again.id + '.result')), true);
});

test('a failing result write leaves the request queued without aborting other requests', async t => {
  const root = fixture(t);
  const first = request(root);
  const instance = bridge(root, () => true, { fs: failing('renameSync', 2) });
  await instance.tick();
  assert.equal(fs.existsSync(path.join(root, 'focus', first.id + '.json')), true);
  assert.deepEqual(tmpFiles(root), []);
  await instance.tick();
  assert.equal(fs.existsSync(path.join(root, 'focus', first.id + '.result')), true);
});

test('repeated failures produce bounded diagnostics', async t => {
  const root = fixture(t);
  let now = 1000000;
  const logs = [];
  const instance = bridge(root, () => true, { now: () => now, fs: failing('renameSync', 100), log: line => logs.push(line) });
  for (let i = 0; i < 30; i++) {
    now += 1500;
    await instance.tick();
  }
  assert.ok(logs.length <= 2, 'logged ' + logs.length);
  assert.deepEqual(tmpFiles(root), []);
});

function windowBridge(root, state, extra = {}) {
  return bridge(root, () => true, {
    sampleWindow: async () => state.sample,
    isFocused: () => state.focused,
    expectedOwnerPid: 500,
    ...extra
  });
}
const goodSample = (handle = '1001') => ({ handle, className: 'Chrome_WidgetWin_1', ownerPid: 500, ownerStartedUtcTicks: '7' });
const published = root => JSON.parse(fs.readFileSync(path.join(root, 'hosts', 'window-a.json'), 'utf8'));

test('registration is versioned and carries no window until a focus gain is observed', async t => {
  const root = fixture(t);
  const state = { focused: true, sample: goodSample() };
  const instance = windowBridge(root, state);
  await instance.tick();
  assert.equal(published(root).protocol, 2);
  assert.equal(published(root).window, null);
});

test('a focus gain publishes instance-bound window evidence', async t => {
  const root = fixture(t);
  const state = { focused: true, sample: goodSample('2002') };
  const instance = windowBridge(root, state);
  instance.noteFocus();
  await instance.tick();
  const window = published(root).window;
  assert.equal(window.handle, '2002');
  assert.deepEqual(window.owner, { pid: 500, startedUtcTicks: '7' });
  assert.equal(window.className, 'Chrome_WidgetWin_1');
});

test('evidence is discarded when focus moved during the sample, and a foreign window never becomes evidence', async t => {
  const root = fixture(t);
  const state = { focused: true, sample: goodSample() };
  const instance = windowBridge(root, state, { sampleWindow: async () => { state.focused = false; return state.sample; } });
  instance.noteFocus();
  await instance.tick();
  assert.equal(published(root).window, null);

  for (const bad of [{ ...goodSample(), ownerPid: 501 }, { ...goodSample(), className: 'Notepad' }, null, { ...goodSample(), handle: '0' }, { ...goodSample(), handle: 'abc' }]) {
    const other = fixture(t);
    const s = { focused: true, sample: bad };
    const b = windowBridge(other, s);
    b.noteFocus();
    await b.tick();
    assert.equal(published(other).window, null);
  }
});

test('a later contradicting sample clears earlier evidence', async t => {
  const root = fixture(t);
  let now = 1000000;
  const state = { focused: true, sample: goodSample() };
  const instance = windowBridge(root, state, { now: () => now });
  instance.noteFocus();
  await instance.tick();
  assert.equal(published(root).window.handle, '1001');
  state.sample = { ...goodSample(), ownerPid: 999 };
  now += 5000;
  instance.noteFocus();
  await instance.tick();
  assert.equal(published(root).window, null);
});

test('window sampling is retried only on a bounded interval', async t => {
  const root = fixture(t);
  let now = 1000000;
  let samples = 0;
  const state = { focused: true, sample: null };
  const instance = windowBridge(root, state, { now: () => now, sampleWindow: async () => { samples++; return null; } });
  instance.noteFocus();
  for (let i = 0; i < 6; i++) {
    now += 500;
    await instance.tick();
  }
  assert.ok(samples <= 2, 'sampled ' + samples);
});

test('protocol 2 reveal requests reveal only and do not use the legacy focus path', async t => {
  const root = fixture(t);
  const r = request(root, { protocol: 2, mode: 'reveal' });
  const seen = [];
  const instance = bridge(root, () => assert.fail('legacy focus used'), { reveal: terminal => { seen.push(terminal); return true; } });
  await instance.tick();
  assert.deepEqual(seen, ['correct']);
  const result = JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result')));
  assert.equal(result.success, true);
  assert.match(result.message, /revealed/);
});

test('an unconfirmed reveal is acknowledged as a failure', async t => {
  const root = fixture(t);
  const r = request(root, { protocol: 2, mode: 'reveal' });
  await bridge(root, () => assert.fail('legacy focus used'), { reveal: () => false }).tick();
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, false);
});

test('requests without a protocol still use the legacy focus path', async t => {
  const root = fixture(t);
  const r = request(root);
  const instance = bridge(root, () => true, { reveal: () => assert.fail('reveal used for a legacy request') });
  await instance.tick();
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, true);
});

test('sampling stops after a bounded number of failed attempts per focus gain', async t => {
  const root = fixture(t);
  let now = 1000000;
  let samples = 0;
  const instance = windowBridge(root, { focused: true }, { now: () => now, sampleWindow: async () => { samples++; return null; } });
  await instance.noteFocus();
  for (let i = 0; i < 40; i++) {
    now += 4000;
    await instance.tick();
  }
  assert.equal(samples, 5);
  await instance.noteFocus();
  assert.equal(samples, 6);
});

// ---- Implementation 002: focus bounce, unavailable samples, self-heal, multiple windows, interaction ----

function deferred() {
  let resolve;
  const promise = new Promise(r => { resolve = r; });
  return { promise, resolve };
}

test('probe A: focus bounce during an in-flight sample never records the other window', async t => {
  const root = fixture(t);
  const state = { focused: true };
  const first = deferred();
  const answers = [first.promise];
  let calls = 0;
  const instance = windowBridge(root, state, {
    sampleWindow: () => {
      calls++;
      return answers.length ? answers.shift() : Promise.resolve(goodSample('1111'));
    }
  });
  const gain = instance.noteWindowState({ focused: true, active: true });
  // A -> B -> A while the first sample (which will see B) is still running.
  instance.noteWindowState({ focused: false, active: true });
  state.focused = false;
  state.focused = true;
  const regain = instance.noteWindowState({ focused: true, active: true });
  first.resolve(goodSample('2222'));
  await gain;
  await regain;
  await instance.idle();
  assert.ok(calls >= 2, 'a second sample is taken instead of dropping the second focus gain');
  assert.equal(instance.window && instance.window.handle, '1111');
  assert.notEqual(instance.window && instance.window.handle, '2222');
});

test('a blur then refocus during a sample discards the sample and samples again', async t => {
  const root = fixture(t);
  const state = { focused: true };
  const first = deferred();
  let calls = 0;
  const instance = windowBridge(root, state, {
    sampleWindow: () => (++calls === 1 ? first.promise : Promise.resolve(goodSample('1111')))
  });
  const gain = instance.noteFocus();
  instance.noteWindowState({ focused: false, active: false });
  instance.noteWindowState({ focused: true, active: false });
  first.resolve(goodSample('2222'));
  await gain;
  await instance.idle();
  assert.equal(instance.window.handle, '1111');
  assert.equal(instance.seenHandles.has('2222'), false);
});

test('an unavailable sampler keeps earlier evidence and logs a distinct message', async t => {
  const root = fixture(t);
  const logs = [];
  let mode = 'ok';
  const instance = windowBridge(root, { focused: true }, {
    log: line => logs.push(line),
    sampleWindow: async () => (mode === 'ok' ? goodSample() : { unavailable: true })
  });
  await instance.noteFocus();
  assert.equal(instance.window.handle, '1001');
  mode = 'down';
  await instance.noteFocus();
  assert.equal(instance.window.handle, '1001');
  assert.ok(logs.some(line => /could not be sampled/.test(line)));
  assert.ok(!logs.some(line => /not this editor window/.test(line)));
  // Real contradictions still clear the evidence.
  mode = 'ok';
  instance.sampleWindow = async () => ({ ...goodSample(), ownerPid: 999 });
  await instance.noteFocus();
  assert.equal(instance.window, null);
});

test('request processing does not wait for a slow window sample', async t => {
  const root = fixture(t);
  const slow = deferred();
  const state = { focused: true };
  const instance = windowBridge(root, state, { sampleWindow: () => slow.promise });
  instance.noteFocus();
  const r = request(root);
  await instance.tick();
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, true);
  slow.resolve(goodSample());
  await instance.idle();
});

test('a failed protocol 2 reveal in an unfocused window clears the window the host activated', async t => {
  const root = fixture(t);
  const state = { focused: true, sample: goodSample() };
  const instance = windowBridge(root, state, { reveal: () => false });
  await instance.noteFocus();
  await instance.tick();
  assert.equal(published(root).window.handle, '1001');
  state.focused = false;
  const r = request(root, { protocol: 2, mode: 'reveal', windowHandle: 1001 });
  await instance.tick();
  assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'focus', r.id + '.result'))).success, false);
  assert.equal(published(root).window, null);
});

test('self-heal does not clear on legacy requests, another handle, a focused window or a missing terminal', async t => {
  const root = fixture(t);
  const state = { focused: true, sample: goodSample() };
  const instance = windowBridge(root, state, { reveal: () => false, focus: () => false });
  await instance.noteFocus();
  await instance.tick();
  state.focused = false;
  request(root); // legacy request, no protocol
  request(root, { protocol: 2, mode: 'reveal', windowHandle: 9999 });
  request(root, { protocol: 2, mode: 'reveal' });
  request(root, { protocol: 2, mode: 'reveal', windowHandle: 1001, shellPid: 77 }); // terminal not found
  await instance.tick();
  assert.equal(instance.window.handle, '1001');
  state.focused = true;
  request(root, { protocol: 2, mode: 'reveal', windowHandle: 1001 });
  await instance.tick();
  assert.equal(instance.window.handle, '1001');
});

test('more than one observed handle publishes no window evidence', async t => {
  const root = fixture(t);
  const state = { focused: true };
  const samples = [goodSample('1001'), goodSample('3003')];
  const logs = [];
  const instance = windowBridge(root, state, { log: line => logs.push(line), sampleWindow: async () => samples.shift() });
  await instance.noteFocus();
  assert.equal(instance.window.handle, '1001');
  await instance.noteFocus();
  assert.equal(instance.window, null);
  await instance.tick();
  assert.equal(published(root).window, null);
  assert.ok(logs.some(line => /More than one top-level window/.test(line)));
  // Later samples of either handle stay unpublished while multiple handles have been seen.
  instance.sampleWindow = async () => goodSample('1001');
  await instance.noteFocus();
  assert.equal(instance.window, null);
});

test('an already focused window becomes verifiable on its first interaction, not from its initial state', async t => {
  const root = fixture(t);
  const state = { focused: true };
  let samples = 0;
  const instance = windowBridge(root, state, { sampleWindow: async () => { samples++; return goodSample(); } });
  await instance.tick();
  await instance.idle();
  assert.equal(samples, 0, 'the initial focused state alone is never sampled');
  // A focus-only event for a window that was already focused is not a gain and not an interaction.
  await instance.noteWindowState({ focused: true, active: false });
  assert.equal(samples, 0);
  await instance.noteWindowState({ focused: true, active: true });
  assert.equal(instance.window.handle, '1001');
  assert.equal(samples, 1);
});

test('an interaction event in an unfocused window never samples', async t => {
  const root = fixture(t);
  let samples = 0;
  const instance = windowBridge(root, { focused: false }, { sampleWindow: async () => { samples++; return goodSample(); } });
  await instance.noteWindowState({ focused: false, active: true });
  assert.equal(samples, 0);
});

test('a foreign foreground at the first interaction is rejected, as in the prototype mismatch case', async t => {
  const root = fixture(t);
  const instance = windowBridge(root, { focused: true }, { sampleWindow: async () => ({ ...goodSample(), ownerPid: 12345, className: 'DAVA_WND_CLASS' }) });
  await instance.noteWindowState({ focused: true, active: true });
  assert.equal(instance.window, null);
});

// ---- Implementation 003 ----

test('the multiple-window guard publishes an additive reason and is never re-enabled', async t => {
  const root = fixture(t);
  const samples = [goodSample('1001'), goodSample('3003')];
  const instance = windowBridge(root, { focused: true }, { sampleWindow: async () => samples.shift() || goodSample('1001') });
  await instance.noteFocus();
  await instance.tick();
  assert.equal(published(root).window.handle, '1001');
  assert.equal(published(root).windowUnavailable, undefined);
  await instance.noteFocus();
  await instance.tick();
  assert.equal(published(root).window, null);
  assert.equal(published(root).windowUnavailable, 'multiple');
  for (let i = 0; i < 3; i++) {
    await instance.noteFocus();
    await instance.tick();
    assert.equal(published(root).window, null);
    assert.equal(published(root).windowUnavailable, 'multiple');
  }
});

test('a registration that has not observed its window carries no reason', async t => {
  const root = fixture(t);
  const instance = windowBridge(root, { focused: true });
  await instance.tick();
  assert.equal(published(root).window, null);
  assert.equal('windowUnavailable' in published(root), false);
});

test('an already removed request is treated as done and not logged as a retry', async t => {
  const root = fixture(t);
  const logs = [];
  const io = { ...fs, unlinkSync: file => {
    if (file.endsWith('.json') && file.includes(path.sep + 'focus' + path.sep)) {
      fs.unlinkSync(file);
      const error = new Error('gone');
      error.code = 'ENOENT';
      throw error;
    }
    return fs.unlinkSync(file);
  } };
  const r = request(root);
  await bridge(root, () => true, { fs: io, log: line => logs.push(line) }).tick();
  assert.equal(fs.existsSync(path.join(root, 'focus', r.id + '.result')), true);
  assert.equal(logs.some(line => /will be retried until it expires/.test(line)), false);
});

test('other unlink failures still leave the request queued and are logged', async t => {
  const root = fixture(t);
  const logs = [];
  const io = { ...fs, unlinkSync: file => {
    if (file.endsWith('.json') && file.includes(path.sep + 'focus' + path.sep)) {
      const error = new Error('busy');
      error.code = 'EPERM';
      throw error;
    }
    return fs.unlinkSync(file);
  } };
  const r = request(root);
  await bridge(root, () => true, { fs: io, log: line => logs.push(line) }).tick();
  assert.equal(fs.existsSync(path.join(root, 'focus', r.id + '.json')), true);
  assert.equal(logs.some(line => /will be retried until it expires/.test(line)), true);
});
