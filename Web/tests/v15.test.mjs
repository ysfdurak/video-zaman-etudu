// v1.5: adım listesi, etüt bilgileri, süreç sırası, çevrim değişkenliği, önce/sonra karşılaştırması.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  newStudy, newObservation, exportPackage, importPackage, deriveSteps, stepOrder, inProcessOrder, analyzeSteps,
  analyzeCycles, cycleSpread, compareStudies, totalStandard, cleanInfo, cleanSteps
} from "../js/core.js";

const enc = (s) => new TextEncoder().encode(s);
const R = (start, end, Step, Cycle = 1, Classification = "") => newObservation({ StartMs: start, EndMs: end, Step, Cycle, Classification });
const demo = () => importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));

test("adım listesi yoksa ölçümlerden süreç sırasıyla çıkarılır, sınıf en sık kullanılan", () => {
  const st = newStudy({ Name: "E", Code: "K", Observations: [R(5000, 7000, "Paket", 1, "VA"), R(0, 2000, "Hazırlık", 1, "SVA"), R(2000, 5000, "Toplama", 1, "VA"), R(7000, 9000, "Paket", 2, "NVA"), R(9000, 9500, "Paket", 3, "VA")] });
  assert.deepEqual(deriveSteps(st), [{ Name: "Hazırlık", Classification: "SVA" }, { Name: "Toplama", Classification: "VA" }, { Name: "Paket", Classification: "VA" }]);
  st.Steps = [{ Name: "Toplama", Classification: "VA" }];
  assert.deepEqual(stepOrder(st), ["Toplama", "Hazırlık", "Paket"], "listede olmayanlar sona, ilk görülme sırasıyla");
  assert.deepEqual(inProcessOrder(st, analyzeSteps(st)).map((r) => r.Step), ["Toplama", "Hazırlık", "Paket"]);
});

test("paket: adım listesi ve etüt bilgileri gidip gelir; Windows'un zorunlu alanları aynen durur", () => {
  const st = demo();
  st.Steps = [{ Name: "Hazırlık", Classification: "SVA" }, { Name: "Toplama", Classification: "VA" }];
  st.Info = { Operator: "A. Kaya", Station: "Hat 2", Date: "2026-11-15", Bogus: "x" };
  const json = exportPackage(st);
  const root = JSON.parse(json);
  assert.deepEqual(Object.keys(root).slice(0, 8), ["Kind", "Version", "Name", "Code", "VideoName", "Settings", "Observations", "Frequency"]);
  assert.equal(root.Version, 1, "paket sürümü değişmedi: Windows sürümü açabilir");
  assert.deepEqual(root.Info, { Operator: "A. Kaya", Station: "Hat 2", Date: "2026-11-15" });
  const back = importPackage(enc(json));
  assert.deepEqual(back.Steps, st.Steps);
  assert.deepEqual(back.Info, root.Info);
  assert.deepEqual(back.Observations, st.Observations);
});

test("paket: bozuk isteğe bağlı alanlar yok sayılır (Windows da yok sayar), eski paketler açılır", () => {
  const base = JSON.parse(exportPackage(demo()));
  for (const extra of [{ Steps: "x" }, { Steps: [{ Name: 5 }, null, { Name: "  " }] }, { Info: [1, 2] }, { Info: { Operator: 7 } }]) {
    const st = importPackage(enc(JSON.stringify({ ...base, ...extra })));
    assert.deepEqual(st.Steps, []); assert.deepEqual(st.Info, {});
  }
  const old = demo();
  assert.deepEqual(old.Steps, []); assert.deepEqual(old.Info, {});
  assert.equal(JSON.parse(exportPackage(old)).Steps, undefined, "boşsa yazılmaz");
});

test("temizleme: adım adı tekil ve kırpılmış, sınıf geçerli; bilgi alanları en fazla 200 karakter", () => {
  assert.deepEqual(cleanSteps([{ Name: " A " }, { Name: "A" }, { Name: "B", Classification: "XX" }, { Name: "C", Classification: "NVA" }]),
    [{ Name: "A", Classification: "" }, { Name: "B", Classification: "" }, { Name: "C", Classification: "NVA" }]);
  assert.equal(cleanInfo({ Note: "x".repeat(500) }).Note.length, 200);
  assert.deepEqual(cleanInfo({ Operator: "  ", Shift: "Gece" }), { Shift: "Gece" });
});

test("çevrim değişkenliği: ortalama, sapma ve %20'den fazla sapan çevrim", () => {
  const s = cycleSpread([10, 11, 10, 15].map((Elapsed, i) => ({ Cycle: i + 1, Elapsed, Workload: Elapsed })));
  assert.equal(s.Mean, 11.5);
  assert.deepEqual(s.Rows.filter((r) => r.Outlier).map((r) => r.Cycle), [4]);
  assert.ok(Math.abs(s.Rows[3].DiffPercent - 30.4347826) < 1e-6);
  assert.equal(cycleSpread([{ Cycle: 1, Elapsed: 10 }, { Cycle: 2, Elapsed: 20 }]).Rows.some((r) => r.Outlier), false, "iki çevrimde işaretlenmez");
  const d = cycleSpread(analyzeCycles(demo()));
  assert.equal(d.Mean, 11);
});

test("önce / sonra: adımlar ada göre eşleşir, toplam ve yüzde fark", () => {
  const before = demo();
  const after = demo();
  after.Observations = after.Observations.map((o) => (o.Step === "Toplama" ? { ...o, EndMs: o.StartMs + Math.round((o.EndMs - o.StartMs) * 0.5) } : o));
  after.Observations.push(R(40000, 41000, "Yeni adım", 1));
  const c = compareStudies(before, after);
  const toplama = c.Rows.find((r) => r.Step === "Toplama");
  assert.ok(toplama.After < toplama.Before && Math.abs(toplama.DiffPercent + 50) < 6);
  assert.equal(c.Rows.find((r) => r.Step === "Yeni adım").Before, null);
  assert.ok(Math.abs(c.Before - totalStandard(analyzeSteps(before)).Total) < 1e-9);
  assert.ok(c.Diff < 0 && c.DiffPercent < 0);
  const same = compareStudies(before, demo());
  assert.equal(same.Diff, 0);
  // Büyük/küçük harf farkı aynı adım sayılır.
  const upper = demo(); upper.Observations = upper.Observations.map((o) => ({ ...o, Step: o.Step.toLocaleUpperCase("tr") }));
  assert.equal(compareStudies(before, upper).Rows.length, analyzeSteps(before).length);
});
