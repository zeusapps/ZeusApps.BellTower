'use strict';
const vscode = require('vscode');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { execFile } = require('node:child_process');
const { promisify } = require('node:util');
const execute = promisify(execFile);
const { Bridge } = require('./service');
const { focusTerminal, revealTerminal } = require('./focus');

function activate(context) {
  const settings = vscode.workspace.getConfiguration('bellTower');
  const root = settings.get('dataDirectory') || process.env.BELLTOWER_DATA_DIR ||
    path.join(process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local'), 'ZeusApps.BellTower');
  const executable = settings.get('executablePath') || 'bell-tower';
  const output = vscode.window.createOutputChannel('Bell Tower');
  let timer;
  const instanceId = crypto.randomBytes(16).toString('hex');
  const registration = path.join(root, 'hosts', instanceId + '.json');
  async function processInfo(pid) {
    try {
      const result = await execute(executable, ['process-info', '--pid', String(pid), '--data-dir', root], {
        windowsHide: true,
        timeout: 3000,
        maxBuffer: 16384
      });
      return JSON.parse(result.stdout);
    } catch {
      return null;
    }
  }
  async function sampleWindow() {
    try {
      const result = await execute(executable, ['foreground-window', '--data-dir', root], {
        windowsHide: true,
        timeout: 3000,
        maxBuffer: 4096
      });
      const value = JSON.parse(result.stdout);
      return {
        handle: String(value.handle),
        className: value.className,
        ownerPid: value.ownerPid,
        ownerStartedUtcTicks: String(value.ownerStartedUtcTicks)
      };
    } catch {
      // Spawn failure, timeout, unstable foreground or malformed output: no evidence either way.
      return { unavailable: true };
    }
  }
  async function start() {
    if (process.platform !== 'win32' || vscode.env.remoteName) {
      output.appendLine('This bridge currently supports local Windows terminals (including RDP), not remote SSH/WSL extension contexts.');
      return;
    }
    const processIdentity = await processInfo(process.pid);
    if (!processIdentity) {
      output.appendLine('Bell Tower executable unavailable. Set bellTower.executablePath, then reload this window.');
      return;
    }
    const bridge = new Bridge({
      root,
      instanceId,
      processIdentity,
      processInfo,
      now: Date.now,
      terminals: async () => Promise.all(vscode.window.terminals
        .filter(terminal => !terminal.exitStatus)
        .map(async terminal => ({ pid: await terminal.processId, terminal }))),
      focus: terminal => focusTerminal(vscode, terminal),
      reveal: terminal => revealTerminal(vscode, terminal),
      sampleWindow,
      isFocused: () => vscode.window.state.focused,
      expectedOwnerPid: process.ppid,
      log: line => output.appendLine(line)
    });
    // Both focus and blur events feed the bridge so a sample straddling a focus change is discarded.
    context.subscriptions.push(vscode.window.onDidChangeWindowState(state => {
      bridge.noteWindowState(state).catch(() => {});
    }));
    timer = setInterval(() => bridge.tick().catch(() => bridge.report('tick', 'Local bridge refresh failed.')), 1500);
    await bridge.tick();
  }
  start().catch(() => output.appendLine('Bridge registration failed.'));
  context.subscriptions.push(output, {
    dispose() {
      if (timer) clearInterval(timer);
      try {
        fs.unlinkSync(registration);
      } catch {
        // A failed registration may not have created a file.
      }
    }
  });
}

exports.activate = activate;
