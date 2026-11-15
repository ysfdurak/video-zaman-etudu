// v1.8: kayıt temposu, aykırı değer, dağılım, hat dengeleme, standart iş, iyileştirme, iş örneklemesi, adım karesi.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { importPackage, exportPackage, analyzeSteps, newStudy, newObservation, toRecord, fromRecord, cleanSteps, cleanRating, cleanSamples } from "../js/core.js";
import { outliers, histogram, lineBalance, autoBalance, standardWork, improvements, analyzeSampling, normalInv, stepFrame, quantile, randomMoment } from "../js/advanced.js";

const enc = (s) => new TextEncoder().encode(s);
const demo = () => importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));
const near = (a, b, t = 1e-9) => assert.ok(Math.abs(a - b) <= t * Math.max(1, Math.abs(b)), `${a} ≠ ${b}`);
const O = (s, e, step = "A", extra = {}) => newObservation({ StartMs: s, EndMs: e, Step: step, ...extra });

test("kayıt temposu: ortalama yönteminde süreyle ağırlıklı, puansız kayıt genel tempoyu alır", () => {
  const st = newStudy({ Observations: [O(0, 1000, "A", { Rating: 120 }), O(0, 3000, "A")] });
  st.Settings.Pace = 100;
  const r = analyzeSteps(st)[0];
  near(r.Pace, (1000 * 120 + 3000 * 100) / 4000);
  near(r.Normal, (1 * 1.2 + 3 * 1.0) / 2, 1e-12);
  assert.equal(r.Rated, 1);
  st.Settings.RepeatedLow = true;
  near(analyzeSteps(st)[0].Pace, 110);
  assert.equal(cleanRating(0), null); assert.equal(cleanRating(301), null); assert.equal(cleanRating("95"), 95);
});

test("kayıt temposu ve yeni adım alanları pakette ve yerel kayıtta gidip gelir", () => {
  const st = demo();
  st.Observations[0] = { ...st.Observations[0], Rating: 105 };
  st.Steps = [{ Name: "Toplama", Classification: "VA", Type: "Manual", Station: "1", Waste: "Motion", Idea: "rafı yaklaştır", FrameMs: 4000 },
    { Name: "Paketleme", Classification: "VA", Type: "Machine", Waste: "Bogus" }];
  const root = JSON.parse(exportPackage(st));
  assert.equal(root.Observations[0].Rating, 105);
  assert.equal(root.Observations[1].Rating, undefined);
  assert.deepEqual(root.Steps[0], { Name: "Toplama", Classification: "VA", Type: "Manual", Station: "1", Waste: "Motion", Idea: "rafı yaklaştır", FrameMs: 4000 });
  assert.equal(root.Steps[1].Waste, undefined, "geçersiz israf türü atılır");
  const back = importPackage(enc(JSON.stringify(root)));
  assert.equal(back.Observations[0].Rating, 105);
  assert.equal(back.Steps[0].Station, "1");
  const rec = fromRecord(structuredClone(toRecord(back)));
  assert.equal(rec.Observations[0].Rating, 105);
  assert.equal(rec.Steps[0].FrameMs, 4000);
  root.Observations[0].Rating = "yüksek";
  assert.equal(importPackage(enc(JSON.stringify(root))).Observations[0].Rating, undefined, "bozuk puan yok sayılır");
});

test("aykırı değer: IQR dışında kalan kayıt işaretlenir, en az 4 kayıt gerekir", () => {
  const rows = [1000, 1100, 1050, 1080, 1020, 5000].map((d, i) => O(i * 10000, i * 10000 + d));
  const st = newStudy({ Observations: rows });
  const out = outliers(st);
  assert.equal(out.length, 1);
  assert.deepEqual(out[0].Rows.map((o) => o.EndMs - o.StartMs), [5000]);
  assert.equal(outliers(newStudy({ Observations: rows.slice(0, 3).concat(rows[5]) })).length, 1);
  assert.equal(outliers(newStudy({ Observations: rows.slice(0, 2).concat(rows[5]) })).length, 0, "3 kayıtta aranmaz");
  assert.equal(quantile([1, 2, 3, 4], 0.25), 1.75);
});

test("histogram: √n sınıf, toplam sayı korunur", () => {
  const st = newStudy({ Observations: Array.from({ length: 16 }, (_, i) => O(i * 10000, i * 10000 + 1000 + i * 100)) });
  const h = histogram(st, "A");
  assert.equal(h.length, 4);
  assert.equal(h.reduce((s, b) => s + b.Count, 0), 16);
  near(h[0].From, 1); near(h[3].To, 2.5);
  assert.equal(histogram(newStudy({ Observations: [O(0, 1000), O(0, 1000)] }), "A").length, 1);
});

test("hat dengeleme: istasyon yükleri, darboğaz, denge verimi, en az operatör", () => {
  const st = demo();
  st.Steps = [
    { Name: "Hazırlık", Classification: "SVA", Station: "1" }, { Name: "Toplama", Classification: "VA", Station: "1" },
    { Name: "Tarama", Classification: "SVA", Station: "2" }, { Name: "Paketleme", Classification: "VA", Station: "2" },
    { Name: "Bekleme", Classification: "NVA" }, { Name: "Seyrek kontrol", Classification: "SVA" }];
  st.Takt = 8;
  const rows = new Map(analyzeSteps(st).map((r) => [r.Step, r.Contribution]));
  const b = lineBalance(st);
  assert.deepEqual(b.Stations.map((s) => s.Station), ["1", "2"]);
  near(b.Stations[0].Load, rows.get("Hazırlık") + rows.get("Toplama"));
  assert.equal(b.Bottleneck, "1");
  assert.deepEqual(b.Unassigned.map((x) => x.Step), ["Seyrek kontrol"], "standart süresi olmayan Bekleme listede yok");
  near(b.Efficiency, (b.Stations[0].Load + b.Stations[1].Load) / (2 * b.Stations[0].Load) * 100);
  assert.equal(b.MinOperators, Math.ceil(b.Total / 8));
  assert.deepEqual(b.OverTakt, ["1"]);
});

test("otomatik dengeleme: sıra korunur, takt dolunca yeni istasyon", () => {
  const st = demo();
  st.Takt = 10;
  assert.deepEqual([...autoBalance(st).entries()], [["Hazırlık", "1"], ["Toplama", "1"], ["Tarama", "2"], ["Paketleme", "2"], ["Bekleme", "2"], ["Seyrek kontrol", "2"]]);
  st.Takt = 9;
  assert.deepEqual([...autoBalance(st).values()], ["1", "2", "3", "3", "3", "3"], "2,59 + 6,90 > 9: Toplama yeni istasyona");
  assert.equal(autoBalance({ ...st, Takt: null }).size, 0);
});

test("standart iş: makine işi paralel, el işi ardışık", () => {
  const st = demo();
  st.Steps = [{ Name: "Hazırlık", Classification: "SVA" }, { Name: "Toplama", Classification: "VA", Type: "Walk" },
    { Name: "Tarama", Classification: "SVA", Type: "Machine" }, { Name: "Paketleme", Classification: "VA" }];
  const w = standardWork(st);
  const by = new Map(w.Rows.map((r) => [r.Step, r]));
  near(by.get("Tarama").Start, by.get("Toplama").End);
  near(by.get("Paketleme").Start, by.get("Tarama").Start, 1e-12);
  assert.equal(by.get("Hazırlık").Type, "Manual");
  near(w.Operator, w.Rows.filter((r) => r.Type !== "Machine").reduce((s, r) => s + r.Seconds, 0));
});

test("iyileştirme listesi: israf, fikir ya da NVA olan adımlar", () => {
  const st = demo();
  st.Steps = [{ Name: "Toplama", Classification: "VA", Waste: "Motion", Idea: "rafı yaklaştır" }, { Name: "Bekleme", Classification: "NVA" }, { Name: "Tarama", Classification: "SVA" }];
  const list = improvements(st);
  assert.deepEqual(list.map((x) => x.Step), ["Toplama", "Bekleme"]);
  assert.equal(list[0].Waste, "Motion");
  assert.ok(list[0].Share > 0);
});

test("iş örneklemesi: pay, güven aralığı ve gereken gözlem", () => {
  near(normalInv(0.975), 1.959963984540054, 1e-12);
  near(normalInv(0.5), 0, 1e-12);
  const st = newStudy({ StudyType: "sampling", Categories: [{ Name: "Çalışıyor", Classification: "VA" }, { Name: "Bekliyor", Classification: "NVA" }] });
  st.Samples = [...Array(30)].map((_, i) => ({ AtMs: i * 1000, Category: i < 24 ? "Çalışıyor" : "Bekliyor" }));
  const r = analyzeSampling(st);
  assert.equal(r.N, 30);
  near(r.Rows[0].Share, 80);
  near(r.Rows[0].Half, 1.959963984540054 * Math.sqrt(0.8 * 0.2 / 30) * 100, 1e-9);
  assert.equal(r.Rows[0].Required, Math.ceil(1.959963984540054 ** 2 * 0.16 / 0.0025));
  assert.equal(r.Required, r.Rows[0].Required);
  near(r.ByClass.NVA, 20);
  const root = JSON.parse(exportPackage({ ...st, Name: "Ö", Code: "K", Video: "v.mp4", SampleError: 3 }));
  assert.equal(root.StudyType, "sampling");
  assert.equal(root.Samples.length, 30);
  const back = importPackage(enc(JSON.stringify(root)));
  assert.equal(back.StudyType, "sampling"); assert.equal(back.SampleError, 3); assert.equal(back.Categories[1].Classification, "NVA");
  assert.deepEqual(cleanSamples([{ AtMs: -1, Category: "x" }, { AtMs: 5, Category: " " }, { AtMs: 5.5, Category: "x" }]), []);
});

test("rastgele an: mevcut gözlemlere uzak", () => {
  const st = newStudy({ Samples: [{ AtMs: 5000, Category: "A" }] });
  let k = 0;
  const seq = [0.5, 0.501, 0.9];
  assert.equal(randomMoment(st, 10000, 1000, () => seq[k++]), 9000, "5000 ve 5010 mevcut gözleme 1 sn’den yakın");
});

test("adım karesi: tanımlıysa o, yoksa ilk kaydın ortası", () => {
  const st = demo();
  st.Steps = cleanSteps([{ Name: "Toplama", FrameMs: 4500 }, { Name: "Hazırlık" }]);
  assert.equal(stepFrame(st, "Toplama"), 4500);
  assert.equal(stepFrame(st, "Hazırlık"), 1000);
  assert.equal(stepFrame(st, "Yok"), null);
});
