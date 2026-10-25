'use strict';

async function focusTerminal(vscode, terminal, wait = ms => new Promise(resolve => setTimeout(resolve, ms))) {
  terminal.show(false);
  // Revealing a terminal does not foreground its editor window.
  await vscode.commands.executeCommand('workbench.action.focusWindow');
  // Show the exact terminal again after foregrounding, including terminals in editor tabs.
  // The global terminal.focus command can move focus to a different panel terminal.
  terminal.show(false);
  for (let attempt = 0; attempt < 20; attempt++) {
    if (vscode.window.state.focused && vscode.window.activeTerminal === terminal) return true;
    await wait(50);
  }
  return false;
}

// Reveals the exact terminal (panel or editor area) in a window the host has already brought forward.
// activeTerminal records the most recent terminal, so the window must also report focus; neither proves
// keyboard input focus, which the public API cannot observe. The global terminal.focus command is never used.
async function revealTerminal(vscode, terminal, wait = ms => new Promise(resolve => setTimeout(resolve, ms))) {
  terminal.show(false);
  for (let attempt = 0; attempt < 20; attempt++) {
    if (vscode.window.state.focused && vscode.window.activeTerminal === terminal) return true;
    if (attempt === 9) terminal.show(false);
    await wait(50);
  }
  return false;
}

module.exports = { focusTerminal, revealTerminal };
