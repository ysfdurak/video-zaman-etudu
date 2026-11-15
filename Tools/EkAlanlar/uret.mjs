// Shared/ek-alanlar.json üretir: v1.5–v1.7 (masaüstü v2) isteğe bağlı alanları taşıyan paket ve beklenen sonuçlar.
// Kaynak tarayıcı çekirdeğidir (Web/js/core.js); C# tarafı (Checks/V2Checks.cs) aynı dosyayı okuyup aynı sonuçları bulmalıdır.
// Çalıştırma: node Tools/EkAlanlar/uret.mjs   (Web/tests/v2-ortak.test.mjs dosyanın güncel olduğunu denetler)
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import {
  importPackage, exportPackage, analyzeSteps, analyzeCycles, inProcessOrder, cycleSpread, totalStandard,
  compareStudies, requiredObservations, deriveSteps, stepOrder, matchingParts
} from "../../Web/js/core.js";

const root = new URL("../../", import.meta.url);
const enc = new TextEncoder();
const demoBytes = () => new Uint8Array(readFileSync(new URL("demo/sentetik-depo-etudu.vzetud", root)));

export function build() {
  const st = importPackage(demoBytes());
  st.Steps = [
    { Name: "Hazırlık", Classification: "SVA", Note: "kutu masaya konunca biter" },
    { Name: "Toplama", Classification: "VA" },
    { Name: "Tarama", Classification: "SVA" },
    { Name: "Paketleme", Classification: "VA", Note: "bant kesilince biter" },
    { Name: "Bekleme", Classification: "NVA" },
    { Name: "Seyrek kontrol", Classification: "SVA" }
  ];
  st.Info = { Operator: "A. Kaya", Station: "Hat 2", Shift: "Gündüz", Date: "2026-11-15" };
  st.StepPace = new Map([["Toplama", 95], ["Paketleme", 120]]);
  st.Takt = 18.5;
  st.Settings = { ...st.Settings, Allowance: 12.5 };
  st.AllowanceParts = { Personal: 5, Fatigue: 4.5, Delay: 3 };
  st.Observations = st.Observations.map((o, i) => (i === 4 ? { ...o, Note: "forklift bekledi" } : i === 7 ? { ...o, Note: "barkod okunmadı" } : o));
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
      Notes: study.Observations.filter((o) => o.Note).map((o) => ({ StartMs: o.StartMs, Note: o.Note }))
    }
  };
}

export const serialize = (data) => JSON.stringify(data, null, 1) + "\n";

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  writeFileSync(new URL("Shared/ek-alanlar.json", root), serialize(build()));
  console.log("Shared/ek-alanlar.json yazıldı.");
}
