// Checks/ExtraChecks.cs ve Checks/Program.cs ile aynı kabul kriterleri (A03–A10, K04–K08).
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  analyzeSteps, analyzeCycles, studentInv, newStudy, newSettings, validateSettings, exportPackage, importPackage,
  suggestedFileName, parseNumber, parsePositiveInt, toRecord, fromRecord, videoLabel, ValidationError, PackageError
} from "../js/core.js";

const near = (a, e, tol = 1e-6) => assert.ok(a !== null && Math.abs(a - e) <= tol, `${a} ≉ ${e}`);
const R = (s, e, Step = "İş", Cycle = 1, Excluded = false) => ({ StartMs: s, EndMs: e, Step, Cycle, Excluded, Classification: "" });
const S = (settings, ...rows) => newStudy({ Name: "Etüt", Code: "K-1", Observations: rows, Settings: { ...newSettings(), ...settings } });
const one = (st) => analyzeSteps(st)[0];
const enc = (s) => new TextEncoder().encode(s);

test("A06 iki baz süre yöntemi", () => {
  near(one(S({}, R(0, 5000), R(0, 6000), R(0, 5000))).Base, 16 / 3);
  near(one(S({ RepeatedLow: true }, R(0, 5000), R(0, 6000), R(0, 5000))).Base, 5);
  const r = one(S({ RepeatedLow: true }, R(0, 5000), R(0, 6000), R(0, 7000)));
  assert.equal(r.Base, null); assert.equal(r.Standard, null); assert.match(r.Method, /Tekrar yok/);
});

test("K06 yuvarlama ve eşitlik", () => {
  near(one(S({ RepeatedLow: true }, R(0, 5049), R(0, 5050), R(0, 5149))).Base, (5.050 + 5.149) / 2);
  near(one(S({ RepeatedLow: true }, R(0, 8000), R(0, 8020), R(0, 6000), R(0, 6040))).Base, 6.02);
});

test("A07 tempo ve tolerans; geçersiz ayarlar reddedilir", () => {
  const r = one(S({ Pace: 110, Allowance: 15 }, R(0, 100000)));
  near(r.Normal, 110); near(r.Standard, 110 / 0.85);
  for (const bad of [{ Allowance: 100 }, { Allowance: -1 }, { Confidence: 0 }, { Confidence: 100 }, { Pace: 0 }, { TargetError: 0 }, { GroupMs: 0 }, { GroupMs: 2.5 }, { Pace: NaN }])
    assert.throws(() => validateSettings({ ...newSettings(), ...bad }), ValidationError);
  validateSettings({ ...newSettings(), Allowance: 99.9 });
});

test("A08 yeterlilik", () => {
  const single = one(S({}, R(0, 5000)));
  assert.equal(single.Sufficiency, "Hesaplanamıyor"); assert.equal(single.CriticalT, null); assert.equal(single.ErrorPercent, null);
  near(studentInv(1, 0.975), 12.706204736, 1e-6);
  const st = S({}, R(0, 10000), R(0, 10500), R(0, 9800), R(0, 10200), R(0, 9900));
  const v = [10, 10.5, 9.8, 10.2, 9.9], m = v.reduce((a, b) => a + b) / 5, sd = Math.sqrt(v.reduce((a, x) => a + (x - m) ** 2, 0) / 4);
  const r = one(st);
  near(r.ErrorPercent, studentInv(4, 0.975) * sd / (Math.sqrt(5) * m) * 100, 1e-9);
  st.Settings.TargetError = r.ErrorPercent;
  assert.equal(one(st).Sufficiency, "Yeterli");
  near(one(S({}, R(0, 1000), R(0, 2000), R(0, 3000), R(0, 4000))).Median, 2.5);
});

test("A03 sınıf analizi değiştirmez; kapsam dışı istatistiğe ve çevrime girmez", () => {
  const st = S({}, R(0, 4000), R(4000, 9000, "İş", 1, true));
  const before = one(st);
  st.Observations[0].Classification = "NVA";
  const after = one(st);
  assert.equal(before.Mean, after.Mean);
  assert.equal(after.Count, 1); assert.equal(after.Excluded, 1);
  near(analyzeCycles(st)[0].Elapsed, 4);
});

test("A09/K08 paralel, boşluk, iç içe ve seyrek iş", () => {
  const gap = analyzeCycles(S({}, R(0, 10000), R(5000, 15000), R(20000, 22000)))[0];
  near(gap.Elapsed, 17); near(gap.Workload, 22);
  const nested = analyzeCycles(S({}, R(0, 10000), R(2000, 3000)))[0];
  near(nested.Elapsed, 10); near(nested.Workload, 11);
  const rare = S({}, R(0, 6000)); rare.Frequency.set("İş", 3);
  near(one(rare).Contribution, 2);
  const c = analyzeCycles(S({}, R(0, 10000), R(0, 12000), R(0, 15000), R(20000, 32000), R(40000, 52000)));
  assert.deepEqual(c.map((x) => x.Elapsed), [39]); assert.deepEqual(c.map((x) => x.Workload), [61]);
});

test("A05 paket: gizlilik, Türkçe, gidiş-dönüş, masaüstü paketiyle uyum", () => {
  const st = S({ Pace: 105 }, R(0, 1000, "Toplama"), R(1000, 2500, "Paketleme", 2));
  st.Name = "Örnek ğüşıöç"; st.Video = "C:\\Users\\ali\\GizliKlasor\\v.mp4"; st.Frequency.set("Toplama", 2);
  const json = exportPackage(st);
  assert.ok(!json.includes("GizliKlasor") && !json.includes(st.Id)); assert.ok(json.includes("Örnek ğüşıöç"));
  const back = importPackage(enc(json));
  assert.equal(back.Video, ""); assert.equal(videoLabel(back), "v.mp4"); assert.notEqual(back.Id, st.Id);
  assert.deepEqual(back.Observations, st.Observations); assert.equal(back.Frequency.get("Toplama"), 2);
  assert.equal(importPackage(enc(exportPackage(back))).ExpectedVideoName, "v.mp4");
  assert.throws(() => exportPackage({ ...st, Code: " " }), PackageError);
  // masaüstünün ürettiği demo paket
  const demo = importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));
  assert.equal(demo.Observations.length, 14);
});

test("A10 demo sonuçları", () => {
  const demo = importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));
  const steps = new Map(analyzeSteps(demo).map((x) => [x.Step, x]));
  const t = steps.get("Toplama");
  assert.deepEqual([t.Base, t.Normal, t.Standard].map((x) => x.toFixed(3)), ["5.333", "5.867", "6.902"]);
  assert.equal(steps.get("Seyrek kontrol").Contribution.toFixed(3), "0.863");
  assert.deepEqual(analyzeCycles(demo).map((c) => [c.Elapsed, c.Workload]), [[10, 12], [11, 13], [12, 14]]);
});

test("sitedeki demo dosyaları kökteki demo/ ile aynı", () => {
  for (const f of ["sentetik-depo-etudu.vzetud", "sentetik-depo-etudu.mp4"])
    assert.ok(readFileSync(new URL(`../demo/${f}`, import.meta.url)).equals(readFileSync(new URL(`../../demo/${f}`, import.meta.url))),
      `Web/demo/${f} kökteki demo/ kopyasından farklı; demo değişince ikisini birlikte güncelleyin`);
});

test("dosya adı önerisi masaüstüyle aynı", () => {
  assert.equal(suggestedFileName(S({}), ".vzetud"), "K-1.vzetud");
  assert.equal(suggestedFileName({ Code: "A/B:C*?" }, ".xlsx", "_analiz"), "A_B_C___analiz.xlsx");
  assert.equal(suggestedFileName({ Code: " " }, ".vzetud"), "VideoZamanEtudu.vzetud");
  assert.equal(suggestedFileName({ Code: "con" }, ".xlsx"), "con_.xlsx");
  assert.equal(suggestedFileName({ Code: "a. . " }, ".x"), "a.x");
});

test("sayı girişi: virgül ve nokta", () => {
  assert.equal(parseNumber("1,5", "x"), 1.5); assert.equal(parseNumber(" 110 ", "x"), 110); assert.equal(parseNumber("1.5", "x"), 1.5);
  for (const bad of ["", "1.000,5", "abc", "1,2,3", "Infinity"]) assert.throws(() => parseNumber(bad, "x"), ValidationError);
  assert.equal(parsePositiveInt("3", "x"), 3);
  for (const bad of ["0", "-1", "2.5", "1e3", "99999999999"]) assert.throws(() => parsePositiveInt(bad, "x"), ValidationError);
});

test("yerel kayıt gidiş-dönüşü ve bozuk kayıt", () => {
  const st = S({ GroupMs: 250, Stable: true }, R(0, 1000, "A"), R(500, 1500, "B", 2, true));
  st.Frequency.set("A", 4); st.Video = "a.mp4";
  const back = fromRecord(structuredClone(toRecord(st)));
  assert.deepEqual({ ...back, Frequency: [...back.Frequency] }, { ...st, Frequency: [...st.Frequency] });
  for (const bad of [null, {}, { Id: "x", Name: "a", Code: "b", Observations: "x", Settings: {}, Frequency: [] }])
    assert.throws(() => fromRecord(bad), ValidationError);
});

test("TR/EN sözlükleri aynı anahtarlara sahip", async () => {
  const { missingKeys } = await import("../js/i18n.js");
  assert.deepEqual(missingKeys(), { onlyTr: [], onlyEn: [] });
});

test("çekirdek hata anahtarları sözlükte var ve İngilizceye çevrilir", async () => {
  const { t } = await import("../js/i18n.js");
  const keys = new Set();
  const grab = (fn) => { try { fn(); } catch (e) { if (e.key) keys.add(e.key); } };
  grab(() => validateSettings({ ...newSettings(), Allowance: 100 }));
  grab(() => parseNumber("x", "Güven"));
  grab(() => parsePositiveInt("0", "Çevrim"));
  for (const content of ["", "x", '{"Kind":"baska","Version":1}', '{"Kind":"video-zaman-etudu","Version":2}'])
    grab(() => importPackage(enc(content)));
  const base = exportPackage(S({}, R(0, 1000)));
  grab(() => importPackage(enc(base.replace('"Allowance":0', '"Allowance":100'))));
  grab(() => importPackage(enc(base.replace('"EndMs":1000', '"EndMs":0'))));
  grab(() => importPackage(enc(base.replace('"Code":"K-1"', '"Code":" "'))));
  grab(() => importPackage(enc(base.replace('"Frequency":{}', '"Frequency":{"İş":0}'))));
  for (const k of ["settings.invalid", "number.invalid", "int.invalid", "pkg.size", "pkg.corrupt", "pkg.notPackage", "pkg.version",
    "pkg.settings", "pkg.observation", "pkg.required", "pkg.frequency"]) {
    assert.ok(keys.has(k), `anahtar üretilmedi: ${k}`);
    assert.notEqual(t(k, {}, "en"), t(k, {}, "tr"), `İngilizce çeviri yok: ${k}`);
  }
});

test("Excel: Türkçe masaüstüyle aynı başlıklar, İngilizce çevrilmiş", async () => {
  const { reportSheets } = await import("../js/report.js");
  const demo = importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));
  const tr = reportSheets(demo, new Date(2026, 10, 15, 9, 5), "tr");
  const en = reportSheets(demo, new Date(2026, 10, 15, 9, 5), "en");
  assert.deepEqual(tr.map((x) => x.name), ["Ham ölçümler", "Adım analizi", "Çevrimler", "Süre grupları", "Ayarlar"]);
  assert.deepEqual(en.map((x) => x.name), ["Raw measurements", "Step analysis", "Cycles", "Duration groups", "Settings"]);
  assert.equal(tr[0].rows[0].length, 9); assert.equal(en[1].rows[0].length, 17);
  const enToplama = en[1].rows.find((r) => r[0] === "Toplama");
  assert.equal(enToplama[10], "Insufficient"); assert.equal(enToplama[16], "Mean");
  assert.equal(tr[1].rows.find((r) => r[0] === "Toplama")[10], "Yetersiz");
  assert.equal(en[4].rows.find((r) => r[0] === "Exported at")[1], "2026-11-15 09:05");
  assert.equal(tr[4].rows.find((r) => r[0] === "Dışa aktarım zamanı")[1], "15.11.2026 09:05");
});
