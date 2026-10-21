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

function bridge(root, focus) {
  return new Bridge({
    root,
    instanceId: 'window-a',
    processIdentity: { pid: 1, startedUtcTicks: '1' },
    now: Date.now,
    terminals: async () => [{ pid: 42, terminal: 'correct' }],
    processInfo: async () => ({ pid: 42, startedUtcTicks: '639000000000000000' }),
    focus
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
