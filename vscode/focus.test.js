'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { focusTerminal } = require('./focus');

test('window foreground and terminal focus are requested in the owning bridge', async () => {
  const calls = [];
  const vscode = { window: { state: { focused: false } }, commands: { executeCommand: async name => {
    calls.push(name);
    if (name === 'workbench.action.focusWindow') vscode.window.state.focused = true;
  } } };
  const terminal = { show: preserve => { calls.push(['show', preserve]); vscode.window.activeTerminal = terminal; } };
  assert.equal(await focusTerminal(vscode, terminal), true);
  assert.deepEqual(calls, [['show', false], 'workbench.action.focusWindow', ['show', false]]);
});

test('terminal reveal alone is not considered foreground success', async () => {
  const vscode = { window: { state: { focused: false } }, commands: { executeCommand: async () => {} } };
  assert.equal(await focusTerminal(vscode, { show() {} }, async () => {}), false);
});

const { revealTerminal } = require('./focus');

test('terminal reveal requires the exact terminal and a focused window, and never uses global terminal.focus', async () => {
  const commands = [];
  const vscode = { window: { state: { focused: true } }, commands: { executeCommand: async name => { commands.push(name); } } };
  const terminal = { show: () => { vscode.window.activeTerminal = terminal; } };
  assert.equal(await revealTerminal(vscode, terminal, async () => {}), true);
  assert.deepEqual(commands, []);
  // A different most-recent terminal, as with a duplicate name in another window, is not a match.
  const other = { show() {} };
  vscode.window.activeTerminal = terminal;
  assert.equal(await revealTerminal(vscode, other, async () => {}), false);
  // The right terminal in a window that is not focused is not a match either.
  vscode.window.state.focused = false;
  assert.equal(await revealTerminal(vscode, terminal, async () => {}), false);
});
