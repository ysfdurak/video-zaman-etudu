// Shared/ek-alanlar.json üretir: v1.5–v1.7 (masaüstü v2) isteğe bağlı alanları taşıyan paket ve beklenen sonuçlar.
// Kaynak tarayıcı çekirdeğidir (Web/js/core.js); C# tarafı (Checks/V2Checks.cs) aynı dosyayı okuyup aynı sonuçları bulmalıdır.
// Çalıştırma: node Tools/EkAlanlar/uret.mjs   (Web/tests/v2-ortak.test.mjs dosyanın güncel olduğunu denetler)
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import {
  importPackage, exportPackage, analyzeSteps, analyzeCycles, inProcessOrder, cycleSpread, totalStandard,
  compareStudies, requiredObservations, deriveSteps, stepOrder, matchingParts, newStudy
} from "../../Web/js/core.js";
import { outliers, histogram, lineBalance, autoBalance, standardWork, improvements, analyzeSampling, stepFrame, normalInv } from "../../Web/js/advanced.js";

const root = new URL("../../", import.meta.url);
const enc = new TextEncoder();
const demoBytes = () => new Uint8Array(readFileSync(new URL("demo/sentetik-depo-etudu.vzetud", root)));

export function build() {
  const st = importPackage(demoBytes());
  st.Steps = [
    { Name: "Hazırlık", Classification: "SVA", Note: "kutu masaya konunca biter", Type: "Manual", Station: "1", FrameMs: 1500 },
    { Name: "Toplama", Classification: "VA", Type: "Walk", Station: "1", Waste: "Motion", Idea: "rafı tezgâha yaklaştır" },
    { Name: "Tarama", Classification: "SVA", Type: "Machine", Station: "2" },
    { Name: "Paketleme", Classification: "VA", Note: "bant kesilince biter", Station: "2" },
    { Name: "Bekleme", Classification: "NVA", Type: "Wait", Waste: "Waiting" },
    { Name: "Seyrek kontrol", Classification: "SVA", Station: "3" }
  ];
  st.Info = { Operator: "A. Kaya", Station: "Hat 2", Shift: "Gündüz", Date: "2026-11-15" };
  st.StepPace = new Map([["Toplama", 95], ["Paketleme", 120]]);
  st.Takt = 18.5;
  st.Settings = { ...st.Settings, Allowance: 12.5 };
  st.AllowanceParts = { Personal: 5, Fatigue: 4.5, Delay: 3 };
  st.Observations = st.Observations.map((o, i) => (i === 4 ? { ...o, Note: "forklift bekledi" } : i === 7 ? { ...o, Note: "barkod okunmadı" } : i === 1 ? { ...o, Rating: 105 } : i === 6 ? { ...o, Rating: 90 } : o));
  // v1.8: aykırı değer için Toplama'ya üç kayıt daha (biri çok uzun).
  st.Observations.push(
    { StartMs: 36000, EndMs: 41000, Step: "Toplama", Classification: "VA", Excluded: false, Cycle: 4 },
    { StartMs: 41000, EndMs: 46200, Step: "Toplama", Classification: "VA", Excluded: false, Cycle: 5, Rating: 110 },
    { StartMs: 46200, EndMs: 60200, Step: "Toplama", Classification: "VA", Excluded: false, Cycle: 6 });
  const text = exportPackage(st);
  const study = importPackage(enc.encode(text));
  const rows = inProcessOrder(study, analyzeSteps(study));
  const spread = cycleSpread(analyzeCycles(study));
  const plain = importPackage(demoBytes());
  const cmp = compareStudies(plain, study);
  return {
    Note: "Tools/EkAlanlar/uret.mjs üretir. Kaynak: Web/js/core.js. C#: Checks/V2Checks.cs.",
    Package: text,
    Expected: {
      ProcessOrder: rows.map((r) => r.Step),
      Steps: rows.map((r) => ({ Step: r.Step, Pace: r.Pace, Normal: r.Normal, Standard: r.Standard, Contribution: r.Contribution, Sufficiency: r.Sufficiency,
        Required: r.Sufficiency === "Yetersiz" ? requiredObservations(r, study.Settings) : null })),
      Total: totalStandard(rows).Total,
      Incomplete: totalStandard(rows).Incomplete,
      Spread: { Mean: spread.Mean, Deviation: spread.Deviation, Variation: spread.Variation, Outliers: spread.Rows.filter((c) => c.Outlier).map((c) => c.Cycle) },
      Compare: { Before: cmp.Before, After: cmp.After, Diff: cmp.Diff, DiffPercent: cmp.DiffPercent,
        Rows: cmp.Rows.map((r) => ({ Step: r.Step, Before: r.Before, After: r.After })) },
      DerivedSteps: deriveSteps(plain).map((x) => ({ Name: x.Name, Classification: x.Classification })),
      PlainOrder: stepOrder(plain),
      Parts: matchingParts(study.AllowanceParts, study.Settings.Allowance),
      Notes: study.Observations.filter((o) => o.Note).map((o) => ({ StartMs: o.StartMs, Note: o.Note })),
      // v1.8
      Ratings: study.Observations.filter((o) => o.Rating).map((o) => ({ StartMs: o.StartMs, Rating: o.Rating })),
      Outliers: outliers(study).map((x) => ({ Step: x.Step, Q1: x.Q1, Q3: x.Q3, Low: x.Low, High: x.High, StartMs: x.Rows.map((o) => o.StartMs) })),
      Histogram: histogram(study, "Toplama"),
      Balance: (({ Stations, Unassigned, ...rest }) => ({ ...rest, Stations: Stations.map((s) => ({ Station: s.Station, Load: s.Load, Steps: s.Steps.map((x) => x.Step) })), Unassigned: Unassigned.map((x) => x.Step) }))(lineBalance(study)),
      AutoBalance: Object.fromEntries(autoBalance(study, 10)),
      StandardWork: standardWork(study),
      Improvements: improvements(study),
      Frames: Object.fromEntries(stepOrder(study).map((s) => [s, stepFrame(study, s)]))
    },
    Sampling: sampling()
  };
}

/** İş örneklemesi senaryosu: paket ve beklenen sonuçlar. */
function sampling() {
  const st = newStudy({ Name: "Forklift kullanımı", Code: "IO-01", Video: "forklift.mp4", StudyType: "sampling", SampleError: 4,
    Categories: [{ Name: "Yük taşıyor", Classification: "VA" }, { Name: "Boş gidiyor", Classification: "NVA" }, { Name: "Bekliyor", Classification: "NVA" }] });
  st.Settings = { ...st.Settings, Confidence: 90 };
  const cats = ["Yük taşıyor", "Boş gidiyor", "Bekliyor", "Şarjda"];
  st.Samples = Array.from({ length: 57 }, (_, i) => ({ AtMs: i * 7919 % 600000, Category: cats[(i * i + 3 * i) % 7 % 4], ...(i === 5 ? { Note: "rampa dolu" } : {}) }));
  const text = exportPackage(st);
  const study = importPackage(enc.encode(text));
  return { Package: text, Expected: { Z: normalInv(0.95), ...analyzeSampling(study) } };
}

export const serialize = (data) => JSON.stringify(data, null, 1) + "\n";

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  writeFileSync(new URL("Shared/ek-alanlar.json", root), serialize(build()));
  console.log("Shared/ek-alanlar.json yazıldı.");
}
