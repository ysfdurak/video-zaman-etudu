// Toplu yedek (v1.7, yalnız tarayıcı): bütün etütler tek bir .zip içinde, her biri ayrı bir .vzetud paketi.
// Paketler Windows sürümüyle aynı biçimdedir; zip açılınca tek tek Windows'a da alınabilir.
// Geri yüklemede aynı içerikteki etüt ikinci kez eklenmez.
import { exportPackage, importPackage, suggestedFileName, EXTENSION, PackageError } from "./core.js";
import { zip } from "./xlsx.js";

const enc = new TextEncoder();
const MAX_ENTRIES = 1000;

/** Etütler → [{name, data}] (dosya adları çakışmasın diye -2, -3 eklenir). Paketlenemeyen etüt atlanır ve sayılır. */
export function backupFiles(studies) {
  const files = [], used = new Set();
  let skipped = 0;
  for (const study of studies) {
    let text;
    try { text = exportPackage(study); } catch { skipped++; continue; }
    let name = suggestedFileName(study, EXTENSION), n = 2;
    while (used.has(name.toLowerCase())) name = suggestedFileName(study, EXTENSION, `-${n++}`);
    used.add(name.toLowerCase());
    files.push({ name, data: enc.encode(text) });
  }
  return { files, skipped };
}

export function makeBackup(studies, date = new Date()) {
  const { files, skipped } = backupFiles(studies);
  return { bytes: zip(files, date), count: files.length, skipped };
}

/** ZIP baytları → [{name, data}]. Sıkıştırmasız (0) ve deflate (8) girdiler okunur. */
export async function unzip(bytes) {
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  let end = -1;
  for (let i = bytes.length - 22; i >= Math.max(0, bytes.length - 22 - 65535); i--)
    if (view.getUint32(i, true) === 0x06054b50) { end = i; break; }
  if (end < 0) throw new PackageError("Bu dosya bir yedek (.zip) değil.", "backup.notZip");
  const count = view.getUint16(end + 10, true);
  let p = view.getUint32(end + 16, true);
  if (count > MAX_ENTRIES) throw new PackageError("Yedekte çok fazla dosya var.", "backup.tooMany");
  const dec = new TextDecoder();
  const out = [];
  for (let i = 0; i < count; i++) {
    if (p + 46 > bytes.length || view.getUint32(p, true) !== 0x02014b50) throw new PackageError("Yedek dosyası bozuk.", "backup.corrupt");
    const method = view.getUint16(p + 10, true), size = view.getUint32(p + 20, true);
    const nameLen = view.getUint16(p + 28, true), extraLen = view.getUint16(p + 30, true), commentLen = view.getUint16(p + 32, true);
    const local = view.getUint32(p + 42, true);
    const name = dec.decode(bytes.subarray(p + 46, p + 46 + nameLen));
    p += 46 + nameLen + extraLen + commentLen;
    if (local + 30 > bytes.length || view.getUint32(local, true) !== 0x04034b50) throw new PackageError("Yedek dosyası bozuk.", "backup.corrupt");
    const start = local + 30 + view.getUint16(local + 26, true) + view.getUint16(local + 28, true);
    if (start + size > bytes.length) throw new PackageError("Yedek dosyası bozuk.", "backup.corrupt");
    const raw = bytes.subarray(start, start + size);
    if (name.endsWith("/")) continue;
    let data;
    if (method === 0) data = raw;
    else if (method === 8 && globalThis.DecompressionStream)
      data = new Uint8Array(await new Response(new Blob([raw]).stream().pipeThrough(new DecompressionStream("deflate-raw"))).arrayBuffer());
    else { out.push({ name, data: null }); continue; }
    out.push({ name, data });
  }
  return out;
}

/** ZIP baytlarının ilk dört baytı "PK\x03\x04" ya da boş arşiv için "PK\x05\x06" olur. */
export const isZip = (bytes) => bytes.length >= 4 && bytes[0] === 0x50 && bytes[1] === 0x4b && (bytes[2] === 3 || bytes[2] === 5);

/**
 * Yedeği okur ve mevcut etütlerle karşılaştırır.
 * Dönen: { add: [study], same: n, failed: [name] } — "same": içerik olarak zaten kayıtlı olan etüt sayısı.
 */
export async function readBackup(bytes, existing = []) {
  const entries = (await unzip(bytes)).filter((e) => e.name.toLowerCase().endsWith(EXTENSION));
  if (!entries.length) throw new PackageError("Yedekte etüt paketi (.vzetud) yok.", "backup.empty");
  const known = new Set();
  for (const s of existing) { try { known.add(exportPackage(s)); } catch { /* bozuk kayıt karşılaştırılmaz */ } }
  const add = [], failed = [];
  let same = 0;
  for (const entry of entries) {
    try {
      if (!entry.data) throw new Error();
      const study = importPackage(entry.data);
      const text = exportPackage(study);
      if (known.has(text)) { same++; continue; }
      known.add(text);
      add.push(study);
    } catch { failed.push(entry.name.split("/").pop()); }
  }
  return { add, same, failed };
}
