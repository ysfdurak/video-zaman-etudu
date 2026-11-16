// Desktop/StudyExport.cs karşılığı: aynı sayfalar, sütunlar ve ayar satırları.
// Türkçe (varsayılan) çıktı masaüstüyle birebir aynıdır; İngilizce seçildiğinde başlıklar ve metin değerleri çevrilir.
import { analyzeSteps, analyzeCycles, seconds, videoLabel, VERSION, INFO_FIELDS, inProcessOrder, totalStandard, cycleSpread, deriveSteps, ALLOWANCE_PARTS, matchingParts, requiredObservations, hasModule } from "./core.js";
import { workbook } from "./xlsx.js";
import { outliers, lineBalance, standardWork, improvements, analyzeSampling } from "./advanced.js";
import { t, result } from "./i18n.js";

const int = (n) => ({ int: n });
const pad = (n) => String(n).padStart(2, "0");
export const formatDate = (d, lang = "tr") => {
  const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  return lang === "tr" ? `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${time}` : `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${time}`;
};

/** v1.9: etüt durumu satırları (taslakta yok; eski çıktılar aynı kalır). */
export function statusRows(study, lang = "tr") {
  if (!study.Status) return [];
  const tx = (key) => t(key, {}, lang);
  return [[tx("x.s.status"), tx("status." + study.Status)],
    ...(study.ApprovedBy ? [[tx("x.s.approvedBy"), study.ApprovedBy]] : []),
    ...(study.ApprovedAt ? [[tx("x.s.approvedAt"), study.ApprovedAt]] : [])];
}

export function reportSheets(study, now = new Date(), lang = "tr") {
  if (study.StudyType === "sampling") return samplingSheets(study, now, lang);
  const tx = (key) => t(key, {}, lang);
  const head = (key) => tx(key).split("|");
  const yesNo = (v) => tx(v ? "yes" : "no");

  // Not sütunu yalnız notlu kayıt varsa eklenir; yoksa sayfa Windows çıktısıyla aynıdır.
  // Not ve tempo puanı sütunları yalnız veri varsa (yoksa ham sayfa Windows v1.2 çıktısıyla aynıdır).
  const notes = study.Observations.some((o) => o.Note);
  const ratings = study.Observations.some((o) => o.Rating);
  const raw = [[...head("x.raw"), ...(notes ? [tx("x.note")] : []), ...(ratings ? [tx("x.rating")] : [])]];
  for (const row of [...study.Observations].sort((a, b) => a.StartMs - b.StartMs || a.EndMs - b.EndMs))
    raw.push([int(row.Cycle), row.Step, int(row.StartMs), int(row.EndMs), row.StartMs / 1000, row.EndMs / 1000,
      seconds(row), row.Classification, yesNo(row.Excluded), ...(notes ? [row.Note ?? ""] : []), ...(ratings ? [row.Rating ?? null] : [])]);

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

  // v1.5: ilk sayfa özet (yalnız tarayıcı sürümü). Diğer sayfalar masaüstüyle aynı kalır.
  const rows = inProcessOrder(study, analyzeSteps(study));
  const { Total: total } = totalStandard(rows);
  const spread = cycleSpread(analyzeCycles(study));
  const defs = new Map(deriveSteps(study).map((x) => [x.Name, x]));
  const byClass = new Map(); let all = 0;
  for (const o of study.Observations) {
    if (o.Excluded || o.EndMs <= o.StartMs) continue;
    const k = o.Classification || tx("x.o.noClass");
    byClass.set(k, (byClass.get(k) ?? 0) + (o.EndMs - o.StartMs)); all += o.EndMs - o.StartMs;
  }
  const outCount = outliers(study).reduce((sum, g) => sum + g.Rows.length, 0);
  const balance = lineBalance(study);
  const summary = [head("x.summary"),
    [tx("x.s.name"), study.Name], [tx("x.s.code"), study.Code], ...statusRows(study, lang),
    ...INFO_FIELDS.filter((k) => study.Info?.[k]).map((k) => [tx("info." + k), study.Info[k]]),
    [tx("x.o.total"), total], [tx("x.o.rate"), total ? 3600 / total : null],
    ...(study.Takt ? [[tx("x.o.takt"), study.Takt], [tx("x.o.load"), total ? (total / study.Takt) * 100 : null], [tx("x.o.operators"), total ? total / study.Takt : null]] : []),
    [tx("x.o.cycles"), int(spread.Rows.length)], [tx("x.o.cv"), spread.Variation],
    [tx("x.o.insufficient"), int(rows.filter((r) => r.Sufficiency === "Yetersiz").length)],
    ...(outCount ? [[tx("x.o.outliers"), int(outCount)]] : []),
    ...(balance.Stations.length && hasModule(study, "Balance") ? [[tx("x.o.eff"), balance.Efficiency], [tx("x.o.bottleneck"), balance.Bottleneck]] : []),
    ...(balance.MinOperators !== null && hasModule(study, "Balance") ? [[tx("x.o.minOps"), int(balance.MinOperators)]] : []),
    [tx("x.o.notComputable"), int(rows.filter((r) => r.Sufficiency === "Hesaplanamıyor" && r.Count > 0).length)],
    ...[...byClass.entries()].sort((a, b) => b[1] - a[1]).map(([k, v]) => [tx("x.o.share") + " " + k, (v / all) * 100]),
    [tx("x.s.pace"), s.Pace], [tx("x.s.allowance"), s.Allowance],
    ...ALLOWANCE_PARTS.filter((k) => matchingParts(study.AllowanceParts, s.Allowance)[k] !== undefined)
      .map((k) => [`${tx("x.o.al")} ${tx("al." + k)}`, matchingParts(study.AllowanceParts, s.Allowance)[k]]),
    [],
    head("x.o.steps")];
  for (const r of rows)
    summary.push([r.Step, defs.get(r.Step)?.Classification ?? "", int(r.Count), r.Pace, r.Standard, int(r.Every), r.Contribution,
      total && r.Contribution !== null ? (r.Contribution / total) * 100 : null, result(r.Sufficiency, lang),
      r.Sufficiency === "Yetersiz" ? int(requiredObservations(r, s)) : null, defs.get(r.Step)?.Note ?? ""]);

  // v1.8 sayfaları (eski sayfaların ardından).
  const extra = [];
  const work = standardWork(study);
  if (work.Rows.length && hasModule(study, "Work")) extra.push({ name: tx("x.sheet.work"), rows: [head("x.work"),
    ...work.Rows.map((r, i) => [int(i + 1), r.Step, tx("type." + r.Type), r.Start, r.End, r.Seconds])] });
  if (balance.Stations.length && hasModule(study, "Balance")) extra.push({ name: tx("x.sheet.balance"), rows: [head("x.balance"),
    ...balance.Stations.flatMap((st) => st.Steps.map((p, i) => [st.Station, p.Step, p.Classification, p.Contribution, i === 0 ? st.Load : null])),
    ...balance.Unassigned.map((p) => [tx("balance.unassigned"), p.Step, p.Classification, p.Contribution, null])] });
  const ideas = improvements(study);
  if (ideas.length && hasModule(study, "Ideas")) extra.push({ name: tx("x.sheet.ideas"), rows: [head("x.ideas"),
    ...ideas.map((x) => [x.Step, x.Classification, x.Waste ? tx("waste." + x.Waste) : "", x.Idea, x.Contribution, x.Share])] });

  return [
    { name: tx("x.sheet.summary"), rows: summary },
    { name: tx("x.sheet.raw"), rows: raw }, { name: tx("x.sheet.steps"), rows: steps }, { name: tx("x.sheet.cycles"), rows: cycles },
    { name: tx("x.sheet.groups"), rows: groups }, { name: tx("x.sheet.settings"), rows: config }, ...extra
  ];
}

/** İş örneklemesi raporu: Özet, kategori sonuçları, gözlemler, ayarlar. */
function samplingSheets(study, now, lang) {
  const tx = (key) => t(key, {}, lang);
  const head = (key) => tx(key).split("|");
  const r = analyzeSampling(study);
  const cls = new Map(study.Categories.map((c) => [c.Name, c.Classification]));
  const summary = [head("x.summary"),
    [tx("x.s.name"), study.Name], [tx("x.s.code"), study.Code], [tx("x.s.studyType"), tx("studyType.sampling")], ...statusRows(study, lang),
    ...INFO_FIELDS.filter((k) => study.Info?.[k]).map((k) => [tx("info." + k), study.Info[k]]),
    [tx("x.o.n"), int(r.N)], [tx("x.o.required"), r.Required === null ? null : int(r.Required)],
    [tx("x.s.confidence"), study.Settings.Confidence], [tx("x.s.sampleError"), r.Error], [tx("x.o.z"), r.Z],
    ...(r.ByClass ? ["VA", "SVA", "NVA", ""].filter((c) => r.ByClass[c] > 0).map((c) => [tx("x.o.share") + " " + (c || tx("x.o.noClass")), r.ByClass[c]]) : [])];
  const results = [head("x.sampling"), ...r.Rows.map((x) => [x.Category, x.Classification, int(x.Count), x.Share, x.Low, x.High, x.Required === null ? null : int(x.Required)])];
  const samples = [head("x.samples"), ...[...study.Samples].sort((a, b) => a.AtMs - b.AtMs).map((x) => [int(x.AtMs), x.AtMs / 1000, x.Category, cls.get(x.Category) ?? "", x.Note ?? ""])];
  const config = [head("x.settings"), [tx("x.s.name"), study.Name], [tx("x.s.code"), study.Code], [tx("x.s.video"), videoLabel(study)],
    [tx("x.s.confidence"), study.Settings.Confidence], [tx("x.s.sampleError"), r.Error],
    [tx("x.s.exported"), formatDate(now, lang)], [tx("x.s.version"), `${VERSION} (${tx("x.s.edition")})`]];
  return [
    { name: tx("x.sheet.summary"), rows: summary }, { name: tx("x.sheet.sampling"), rows: results },
    { name: tx("x.sheet.samples"), rows: samples }, { name: tx("x.sheet.settings"), rows: config }
  ];
}

export const reportBytes = (study, now, lang = "tr") => workbook(reportSheets(study, now, lang));
