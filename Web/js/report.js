// Desktop/StudyExport.cs karşılığı: aynı sayfalar, sütunlar ve ayar satırları.
// Türkçe (varsayılan) çıktı masaüstüyle birebir aynıdır; İngilizce seçildiğinde başlıklar ve metin değerleri çevrilir.
import { analyzeSteps, analyzeCycles, seconds, videoLabel, VERSION } from "./core.js";
import { workbook } from "./xlsx.js";
import { t, result } from "./i18n.js";

const int = (n) => ({ int: n });
const pad = (n) => String(n).padStart(2, "0");
export const formatDate = (d, lang = "tr") => {
  const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  return lang === "tr" ? `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${time}` : `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${time}`;
};

export function reportSheets(study, now = new Date(), lang = "tr") {
  const tx = (key) => t(key, {}, lang);
  const head = (key) => tx(key).split("|");
  const yesNo = (v) => tx(v ? "yes" : "no");

  const raw = [head("x.raw")];
  for (const row of [...study.Observations].sort((a, b) => a.StartMs - b.StartMs || a.EndMs - b.EndMs))
    raw.push([int(row.Cycle), row.Step, int(row.StartMs), int(row.EndMs), row.StartMs / 1000, row.EndMs / 1000,
      seconds(row), row.Classification, yesNo(row.Excluded)]);

  const steps = [head("x.steps")];
  const groups = [head("x.groups")];
  for (const r of analyzeSteps(study)) {
    steps.push([r.Step, int(r.Count), int(r.Excluded), r.Mean, r.Median, r.Min, r.Max, r.Deviation, r.CriticalT, r.ErrorPercent,
      result(r.Sufficiency, lang), r.Base, r.Normal, r.Standard, int(r.Every), r.Contribution, result(r.Method, lang)]);
    for (const g of r.Groups) groups.push([r.Step, g.Seconds, int(g.Count)]);
  }
  const cycles = [head("x.cycles")];
  for (const c of analyzeCycles(study)) cycles.push([int(c.Cycle), c.Elapsed, c.Workload]);

  const s = study.Settings;
  const config = [head("x.settings"),
    [tx("x.s.name"), study.Name], [tx("x.s.code"), study.Code], [tx("x.s.video"), videoLabel(study)],
    [tx("x.s.raw"), int(study.Observations.length)], [tx("x.s.excluded"), int(study.Observations.filter((x) => x.Excluded).length)],
    [tx("x.s.pace"), s.Pace], [tx("x.s.allowance"), s.Allowance], [tx("x.s.confidence"), s.Confidence], [tx("x.s.target"), s.TargetError],
    [tx("x.s.method"), tx(s.RepeatedLow ? "x.s.methodRepeated" : "x.s.methodMean")], [tx("x.s.group"), int(s.GroupMs)],
    [tx("x.s.stable"), yesNo(s.Stable)], [tx("x.s.guide"), int(s.Stable ? 5 : 10)],
    [tx("x.s.exported"), formatDate(now, lang)], [tx("x.s.version"), `${VERSION} (${tx("x.s.edition")})`]];

  return [
    { name: tx("x.sheet.raw"), rows: raw }, { name: tx("x.sheet.steps"), rows: steps }, { name: tx("x.sheet.cycles"), rows: cycles },
    { name: tx("x.sheet.groups"), rows: groups }, { name: tx("x.sheet.settings"), rows: config }
  ];
}

export const reportBytes = (study, now, lang = "tr") => workbook(reportSheets(study, now, lang));
