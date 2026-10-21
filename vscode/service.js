'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

function atomic(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temporary = file + '.' + crypto.randomUUID() + '.tmp';
  fs.writeFileSync(temporary, JSON.stringify(value), { encoding: 'utf8', flag: 'wx' });
  fs.renameSync(temporary, file);
}
class Bridge {
  constructor(options) {
    Object.assign(this, options);
    this.busy = false;
  }

  async tick() {
    if (this.busy) return;
    this.busy = true;
    try {
      const terminals = await this.terminals();
      atomic(path.join(this.root, 'hosts', this.instanceId + '.json'), {
        instanceId: this.instanceId,
        process: this.processIdentity,
        updatedAt: new Date(this.now()).toISOString(),
        terminalPids: terminals.map(terminal => terminal.pid)
      });
      const folder = path.join(this.root, 'focus');
      fs.mkdirSync(folder, { recursive: true });
      for (const name of fs.readdirSync(folder)) {
        if (!/^[a-f0-9]{32}\.json$/.test(name)) continue;
        const file = path.join(folder, name);
        let request;
        try {
          if (fs.statSync(file).size > 16384) continue;
          request = JSON.parse(fs.readFileSync(file, 'utf8'));
        } catch {
          continue;
        }
        if (request.instanceId !== this.instanceId || typeof request.id !== 'string' || request.id + '.json' !== name) {
          continue;
        }
        let result = {
          success: false,
          precision: 'unavailable',
          message: 'The focus target is stale or unavailable.'
        };
        const terminal = terminals.find(terminal => terminal.pid === request.shellPid);
        const info = terminal ? await this.processInfo(terminal.pid) : null;
        if (
          request.id + '.json' === name && Date.parse(request.expiresAt) > this.now() && terminal &&
          info && String(info.startedUtcTicks) === request.shellStartedUtcTicks
        ) {
          try {
            const focused = await this.focus(terminal.terminal);
            result = {
              success: focused === true,
              precision: 'exact terminal',
              message: focused === true ? 'The owning VS Code window and registered terminal were focused.' :
                'VS Code did not confirm both window and terminal focus.'
            };
          } catch {
            result = { success: false, precision: 'exact terminal', message: 'VS Code could not focus the owning window and terminal.' };
          }
        }
        atomic(path.join(folder, request.id + '.result'), result);
        fs.unlinkSync(file);
      }
    } finally {
      this.busy = false;
    }
  }
}

module.exports = { Bridge };
