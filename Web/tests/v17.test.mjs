// v1.7: tolerans kalemleri ve toplu yedek.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { deflateRawSync } from "node:zlib";
import { exportPackage, importPackage, analyzeSteps, toRecord, fromRecord, cleanAllowanceParts, allowanceSum, matchingParts } from "../js/core.js";
import { makeBackup, readBackup, unzip, isZip } from "../js/backup.js";
import { reportSheets } from "../js/report.js";
import { zip } from "../js/xlsx.js";

const enc = (s) => new TextEncoder().encode(s);
const demo = () => importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));

test("tolerans kalemleri: temizleme, toplam ve eşleşme", () => {
  assert.deepEqual(cleanAllowanceParts({ Personal: 5, Fatigue: "4", Delay: 0, Other: -1, Foo: 3 }), { Personal: 5, Fatigue: 4 });
  assert.deepEqual(cleanAllowanceParts({ Personal: 60, Fatigue: 40 }), {}, "toplam %100 olamaz");
  assert.deepEqual(cleanAllowanceParts([5]), {});
  assert.equal(allowanceSum({ Personal: 0.1, Fatigue: 0.2 }), 0.3, "ondalık kayması yuvarlanır");
  assert.deepEqual(matchingParts({ Personal: 5, Fatigue: 4 }, 9), { Personal: 5, Fatigue: 4 });
  assert.deepEqual(matchingParts({ Personal: 5, Fatigue: 4 }, 12), {}, "toplam tutmazsa kalemler düşer");
});

test("tolerans kalemleri pakette ve yerel kayıtta taşınır; hesap yalnız toplamı kullanır", () => {
  const st = demo();
  const before = analyzeSteps(st);
  st.Settings = { ...st.Settings, Allowance: 9 };
  st.AllowanceParts = { Personal: 5, Fatigue: 3, Delay: 1 };
  const withParts = analyzeSteps(st);
  const plain = demo(); plain.Settings = { ...plain.Settings, Allowance: 9 };
  assert.deepEqual(withParts, analyzeSteps(plain), "kalemler sonucu değiştirmez, toplam belirler");
  assert.notDeepEqual(withParts, before);
  const root = JSON.parse(exportPackage(st));
  assert.equal(root.Version, 1);
  assert.equal(root.Settings.Allowance, 9, "Windows toplamı okur");
  assert.deepEqual(root.AllowanceParts, { Personal: 5, Fatigue: 3, Delay: 1 });
  const back = importPackage(enc(JSON.stringify(root)));
  assert.deepEqual(back.AllowanceParts, { Personal: 5, Fatigue: 3, Delay: 1 });
  assert.deepEqual(fromRecord(structuredClone(toRecord(back))).AllowanceParts, { Personal: 5, Fatigue: 3, Delay: 1 });
  // Windows'ta tolerans değiştirilip kalemler eski kaldıysa kalemler yok sayılır.
  root.Settings.Allowance = 12;
  assert.deepEqual(importPackage(enc(JSON.stringify(root))).AllowanceParts, {});
  root.AllowanceParts = "bozuk";
  assert.deepEqual(importPackage(enc(JSON.stringify(root))).AllowanceParts, {}, "bozuk alan paketi reddettirmez");
  assert.equal(JSON.parse(exportPackage(demo())).AllowanceParts, undefined, "kalem yoksa alan yazılmaz");
});

test("Excel özetinde tolerans kalemleri satır olarak görünür", () => {
  const st = demo();
  st.Settings = { ...st.Settings, Allowance: 8 };
  st.AllowanceParts = { Personal: 5, Delay: 3 };
  for (const lang of ["tr", "en"]) {
    const rows = reportSheets(st, new Date(2026, 10, 15), lang)[0].rows;
    const labels = rows.map((r) => r[0]);
    assert.ok(labels.some((l) => /Kişisel ihtiyaç|Personal needs/.test(l)), lang);
    assert.ok(labels.some((l) => /gecikme|delay/.test(l)), lang);
    assert.ok(!labels.some((l) => /Yorgunluk|Fatigue/.test(l)), "girilmeyen kalem yok");
  }
});

test("toplu yedek: her etüt ayrı paket, çakışan adlar ayrılır, geri yükleme kopya eklemez", async () => {
  const a = demo(), b = demo(), c = demo();
  b.Name = "İkinci"; b.Observations = b.Observations.slice(0, 4);
  c.Code = "Başka/Kod";
  const { bytes, count, skipped } = makeBackup([a, b, c]);
  assert.equal(count, 3); assert.equal(skipped, 0);
  assert.ok(isZip(bytes));
  const names = (await unzip(bytes)).map((e) => e.name);
  assert.equal(new Set(names.map((n) => n.toLowerCase())).size, 3, names.join());
  assert.ok(names.every((n) => n.endsWith(".vzetud")));
  // Boş depoya: üçü de eklenir.
  const fresh = await readBackup(bytes, []);
  assert.equal(fresh.add.length, 3); assert.equal(fresh.same, 0); assert.deepEqual(fresh.failed, []);
  assert.equal(exportPackage(fresh.add[1]), exportPackage(b));
  // a zaten kayıtlıysa yalnız b ve c eklenir.
  const partial = await readBackup(bytes, [a]);
  assert.equal(partial.add.length, 2); assert.equal(partial.same, 1);
});

test("yedek okuma: deflate girdi, bozuk paket ve yedek olmayan dosya", async () => {
  const text = enc(exportPackage(demo()));
  // Elle yeniden sıkıştırılmış zip (deflate) de okunur.
  const stored = zip([{ name: "klasor/a.vzetud", data: text }, { name: "b.vzetud", data: enc("{bozuk") }, { name: "oku.txt", data: enc("x") }]);
  const deflated = deflateRawSync(text);
  // İlk girdiyi deflate'e çevir: yöntem 8, sıkıştırılmış boyut.
  const entries = await unzip(stored);
  assert.equal(entries.length, 3);
  const r = await readBackup(stored, []);
  assert.equal(r.add.length, 1); assert.deepEqual(r.failed, ["b.vzetud"]);
  const manual = deflateZip("a.vzetud", text, deflated);
  const d = await readBackup(manual, []);
  assert.equal(d.add.length, 1);
  await assert.rejects(readBackup(enc("merhaba dünya"), []), (e) => e.key === "backup.notZip");
  await assert.rejects(readBackup(zip([{ name: "x.txt", data: enc("x") }]), []), (e) => e.key === "backup.empty");
});

/** Tek girdili deflate zip (CRC doğrulanmadığı için 0 yazılır). */
function deflateZip(name, raw, data) {
  const n = enc(name);
  const local = new DataView(new ArrayBuffer(30));
  local.setUint32(0, 0x04034b50, true); local.setUint16(8, 8, true);
  local.setUint32(18, data.length, true); local.setUint32(22, raw.length, true); local.setUint16(26, n.length, true);
  const central = new DataView(new ArrayBuffer(46));
  central.setUint32(0, 0x02014b50, true); central.setUint16(10, 8, true);
  central.setUint32(20, data.length, true); central.setUint32(24, raw.length, true); central.setUint16(28, n.length, true);
  const cdOffset = 30 + n.length + data.length;
  const end = new DataView(new ArrayBuffer(22));
  end.setUint32(0, 0x06054b50, true); end.setUint16(8, 1, true); end.setUint16(10, 1, true);
  end.setUint32(12, 46 + n.length, true); end.setUint32(16, cdOffset, true);
  const parts = [new Uint8Array(local.buffer), n, data, new Uint8Array(central.buffer), n, new Uint8Array(end.buffer)];
  const out = new Uint8Array(parts.reduce((s, p) => s + p.length, 0));
  let p = 0; for (const x of parts) { out.set(x, p); p += x.length; }
  return out;
}

test("Excel özeti: yetersiz ve hesaplanamayan ayrı sayılır, yetersiz adıma gereken n yazılır", () => {
  const rows = reportSheets(demo(), new Date(2026, 10, 15), "tr")[0].rows;
  const val = (label) => rows.find((r) => r[0] === label)?.[1];
  assert.deepEqual(val("Yetersiz adım sayısı"), { int: 1 });
  assert.deepEqual(val("Hesaplanamayan adım sayısı (en az 2 geçerli ölçüm gerekir)"), { int: 1 });
  const head = rows.find((r) => r[0] === "Adım");
  const col = head.indexOf("Gereken n (yaklaşık)");
  assert.ok(col > 0);
  assert.ok(rows.find((r) => r[0] === "Toplama")[col].int > 3);
  assert.equal(rows.find((r) => r[0] === "Hazırlık")[col], null);
});
