// v1.6: adım temposu, takt süresi, kayıt notu, adım açıklaması.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { exportPackage, importPackage, analyzeSteps, compareStudies, toRecord, fromRecord } from "../js/core.js";

const enc = (s) => new TextEncoder().encode(s);
const demo = () => importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));

test("adım temposu yalnız o adımın normal ve standart süresini değiştirir", () => {
  const st = demo();
  const before = new Map(analyzeSteps(st).map((r) => [r.Step, r]));
  st.StepPace.set("Toplama", 90);
  const after = new Map(analyzeSteps(st).map((r) => [r.Step, r]));
  assert.equal(after.get("Toplama").Pace, 90);
  assert.ok(Math.abs(after.get("Toplama").Normal - before.get("Toplama").Base * 0.9) < 1e-9);
  assert.deepEqual(after.get("Hazırlık"), before.get("Hazırlık"), "diğer adımlar aynı");
  assert.equal(before.get("Toplama").Pace, 110, "adım temposu yoksa genel tempo");
  assert.ok(compareStudies(demo(), st).Rows.find((r) => r.Step === "Toplama").Diff < 0);
});

test("paket ve yerel kayıt: takt, adım temposu, adım açıklaması ve kayıt notu gidip gelir", () => {
  const st = demo();
  st.Takt = 20; st.StepPace.set("Toplama", 95);
  st.Steps = [{ Name: "Toplama", Classification: "VA", Note: "ürün kutuya bırakılınca" }];
  st.Observations[1] = { ...st.Observations[1], Note: "raf boştu" };
  const root = JSON.parse(exportPackage(st));
  assert.equal(root.Version, 1);
  assert.equal(root.Takt, 20);
  assert.deepEqual(root.StepPace, { Toplama: 95 });
  assert.equal(root.Observations[1].Note, "raf boştu");
  assert.equal(root.Observations[0].Note, undefined, "boş not yazılmaz");
  const back = importPackage(enc(JSON.stringify(root)));
  assert.equal(back.Takt, 20);
  assert.equal(back.StepPace.get("Toplama"), 95);
  assert.equal(back.Steps[0].Note, "ürün kutuya bırakılınca");
  assert.equal(back.Observations[1].Note, "raf boştu");
  const rec = fromRecord(structuredClone(toRecord(back)));
  assert.equal(rec.Takt, 20); assert.equal(rec.StepPace.get("Toplama"), 95); assert.equal(rec.Observations[1].Note, "raf boştu");
});

test("bozuk isteğe bağlı alanlar yok sayılır", () => {
  const base = JSON.parse(exportPackage(demo()));
  for (const extra of [{ Takt: -1 }, { Takt: "x" }, { StepPace: { Toplama: 0, Tarama: 999, "": 50 } }, { StepPace: [1] }]) {
    const st = importPackage(enc(JSON.stringify({ ...base, ...extra })));
    assert.equal(st.Takt, null); assert.equal(st.StepPace.size, 0);
  }
  const o = { ...base, Observations: base.Observations.map((x, i) => (i === 0 ? { ...x, Note: 5 } : x)) };
  assert.equal(importPackage(enc(JSON.stringify(o))).Observations[0].Note, undefined);
});

test("Excel özeti: takt satırları, tempo ve açıklama sütunları; not sütunu yalnız not varsa", async () => {
  const { reportSheets } = await import("../js/report.js");
  const st = demo();
  let sheets = reportSheets(st, new Date(2026, 10, 15), "tr");
  assert.equal(sheets[1].rows[0].length, 9, "not yoksa ham sayfa Windows ile aynı");
  st.Takt = 20; st.Observations[0] = { ...st.Observations[0], Note: "not" };
  sheets = reportSheets(st, new Date(2026, 10, 15), "tr");
  assert.equal(sheets[1].rows[0].at(-1), "Not");
  const sum = new Map(sheets[0].rows.filter((r) => r.length === 2).map((r) => [r[0], r[1]]));
  assert.equal(sum.get("Takt süresi (sn)"), 20);
  assert.ok(Math.abs(sum.get("Gereken operatör (standart ÷ takt)") - 16.8235 / 20) < 0.001);
  const en = reportSheets(st, new Date(2026, 10, 15), "en");
  assert.ok(en[0].rows.some((r) => r[0] === "Takt load (%)"));
});
