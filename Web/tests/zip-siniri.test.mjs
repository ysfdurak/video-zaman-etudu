// Yedek okuma: açılınca 32 MB'ı aşan girdi (zip bombası) belleğe alınmaz, okunamadı sayılır.
import test from 'node:test';
import assert from 'node:assert/strict';
import { deflateRawSync } from 'node:zlib';
import { unzip } from '../js/backup.js';

function zip(name, data) {
  const c = deflateRawSync(data), n = Buffer.from(name);
  const lh = Buffer.alloc(30); lh.writeUInt32LE(0x04034b50, 0); lh.writeUInt16LE(8, 8); lh.writeUInt32LE(c.length, 18); lh.writeUInt32LE(data.length, 22); lh.writeUInt16LE(n.length, 26);
  const ch = Buffer.alloc(46); ch.writeUInt32LE(0x02014b50, 0); ch.writeUInt16LE(8, 10); ch.writeUInt32LE(c.length, 20); ch.writeUInt32LE(data.length, 24); ch.writeUInt16LE(n.length, 28);
  const cd = Buffer.concat([ch, n]);
  const e = Buffer.alloc(22); e.writeUInt32LE(0x06054b50, 0); e.writeUInt16LE(1, 8); e.writeUInt16LE(1, 10); e.writeUInt32LE(cd.length, 12); e.writeUInt32LE(30 + n.length + c.length, 16);
  return new Uint8Array(Buffer.concat([lh, n, c, cd, e]));
}

test('küçük girdi açılır, 200 MB’a açılan girdi reddedilir', async () => {
  const [ok] = await unzip(zip('a.vzetud', Buffer.from('{"x":1}')));
  assert.equal(Buffer.from(ok.data).toString(), '{"x":1}');
  const [bomba] = await unzip(zip('b.vzetud', Buffer.alloc(200 * 1024 * 1024)));
  assert.equal(bomba.data, null);
});
