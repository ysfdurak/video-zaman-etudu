// Desktop/StudyExport.cs karşılığı: aynı sayfalar, sütunlar ve ayar satırları.
import { analyzeSteps, analyzeCycles, seconds, videoLabel, VERSION } from "./core.js";
import { workbook } from "./xlsx.js";

const int = (n) => ({ int: n });
const pad = (n) => String(n).padStart(2, "0");
export const formatDate = (d) => `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;

export function reportSheets(study, now = new Date()) {
  const raw = [["Çevrim", "Adım", "Başlangıç (ms)", "Bitiş (ms)", "Başlangıç (sn)", "Bitiş (sn)", "Süre (sn)", "Sınıf", "Kapsam dışı"]];
  for (const row of [...study.Observations].sort((a, b) => a.StartMs - b.StartMs || a.EndMs - b.EndMs))
    raw.push([int(row.Cycle), row.Step, int(row.StartMs), int(row.EndMs), row.StartMs / 1000, row.EndMs / 1000,
      seconds(row), row.Classification, row.Excluded ? "Evet" : "Hayır"]);

  const steps = [["Adım", "Geçerli n", "Dışlanan n", "Ortalama (sn)", "Medyan (sn)", "Min (sn)", "Maks (sn)", "Örneklem std sapması",
    "Kritik t", "Bağıl hata (%)", "Yeterlilik", "Baz (sn)", "Normal (sn)", "Standart (sn)", "Her N çevrimde", "Standart katkı (sn/çevrim)", "Yöntem"]];
  const groups = [["Adım", "Grup ortalaması (sn)", "Tekrar"]];
  for (const r of analyzeSteps(study)) {
    steps.push([r.Step, int(r.Count), int(r.Excluded), r.Mean, r.Median, r.Min, r.Max, r.Deviation, r.CriticalT, r.ErrorPercent,
      r.Sufficiency, r.Base, r.Normal, r.Standard, int(r.Every), r.Contribution, r.Method]);
    for (const g of r.Groups) groups.push([r.Step, g.Seconds, int(g.Count)]);
  }
  const cycles = [["Çevrim", "Ölçülen geçen süre (sn)", "İş yükü (sn)"]];
  for (const c of analyzeCycles(study)) cycles.push([int(c.Cycle), c.Elapsed, c.Workload]);

  const s = study.Settings;
  const config = [["Parametre", "Değer"],
    ["Etüt adı", study.Name], ["Etüt kodu", study.Code], ["Video dosyası", videoLabel(study)],
    ["Ham kayıt sayısı", int(study.Observations.length)], ["Kapsam dışı kayıt sayısı", int(study.Observations.filter((x) => x.Excluded).length)],
    ["Tempo (%)", s.Pace], ["Tolerans (%)", s.Allowance], ["Güven (%)", s.Confidence], ["Hedef bağıl hata (%)", s.TargetError],
    ["Baz süre yöntemi", s.RepeatedLow ? "En çok tekrar eden en küçük süre" : "Ortalama"], ["Grup hassasiyeti (ms)", int(s.GroupMs)],
    ["Süreç kararlı", s.Stable ? "Evet" : "Hayır"], ["Toyota esinli rehber (gözlem)", int(s.Stable ? 5 : 10)],
    ["Dışa aktarım zamanı", formatDate(now)], ["Program sürümü", VERSION + " (tarayıcı)"]];

  return [
    { name: "Ham ölçümler", rows: raw }, { name: "Adım analizi", rows: steps }, { name: "Çevrimler", rows: cycles },
    { name: "Süre grupları", rows: groups }, { name: "Ayarlar", rows: config }
  ];
}

export const reportBytes = (study, now) => workbook(reportSheets(study, now));
