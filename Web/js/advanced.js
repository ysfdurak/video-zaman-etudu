// v1.8 ileri analizler (masaüstü karşılığı: Core/Advanced.cs). Hepsi saf işlevdir; ekran ve Excel aynı sonucu kullanır.
// Aykırı değer (IQR), dağılım (histogram), hat dengeleme (Yamazumi), standart iş kombinasyon tablosu,
// iyileştirme listesi, iş örneklemesi ve adım karesi.
import { analyzeSteps, inProcessOrder, deriveSteps, studentInv } from "./core.js";

const dur = (o) => o.EndMs - o.StartMs;
const valid = (o) => !o.Excluded && o.EndMs > o.StartMs;

/** Doğrusal ara değerli yüzdelik (Excel YÜZDEBİRLİK / R tip 7). values sıralı olmalı. */
export function quantile(sorted, p) {
  if (!sorted.length) return null;
  const h = (sorted.length - 1) * p, lo = Math.floor(h), hi = Math.ceil(h);
  return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
}

/**
 * Aykırı kayıtlar: adımın geçerli süreleri içinde IQR kuralının (Q1 − 1,5·IQR, Q3 + 1,5·IQR) dışında kalanlar.
 * En az 4 geçerli kayıt gerekir. Kayıtlar silinmez; kullanıcı isterse kapsam dışı bırakır.
 * Dönen: [{ Step, Q1, Q3, Low, High, Rows: [gözlem] }] (yalnız aykırısı olan adımlar, süreç sırasıyla)
 */
export function outliers(study, factor = 1.5) {
  const result = [];
  const order = inProcessOrder(study, analyzeSteps(study)).map((r) => r.Step);
  for (const step of order) {
    const rows = study.Observations.filter((o) => o.Step === step && valid(o));
    if (rows.length < 4) continue;
    const sorted = rows.map(dur).sort((a, b) => a - b);
    const q1 = quantile(sorted, 0.25), q3 = quantile(sorted, 0.75), iqr = q3 - q1;
    const low = q1 - factor * iqr, high = q3 + factor * iqr;
    const out = rows.filter((o) => dur(o) < low - 1e-9 || dur(o) > high + 1e-9).sort((a, b) => a.StartMs - b.StartMs);
    if (out.length) result.push({ Step: step, Q1: q1 / 1000, Q3: q3 / 1000, Low: low / 1000, High: high / 1000, Rows: out });
  }
  return result;
}

/** Histogram: eşit genişlikte sınıflar; sınıf sayısı √n (3–10 arası). Dönen: [{ From, To, Count }] saniye. */
export function histogram(study, step) {
  const values = study.Observations.filter((o) => o.Step === step && valid(o)).map((o) => dur(o) / 1000).sort((a, b) => a - b);
  if (!values.length) return [];
  const min = values[0], max = values[values.length - 1];
  const bins = Math.min(10, Math.max(3, Math.ceil(Math.sqrt(values.length))));
  if (max - min < 1e-9) return [{ From: min, To: max, Count: values.length }];
  const width = (max - min) / bins;
  const out = Array.from({ length: bins }, (_, i) => ({ From: min + i * width, To: min + (i + 1) * width, Count: 0 }));
  for (const v of values) out[Math.min(bins - 1, Math.floor((v - min) / width + 1e-9))].Count++;
  return out;
}

/**
 * Hat dengeleme (Yamazumi): adımlar istasyonlara (StepDef.Station) atanır; yük = katkıların toplamı (sn/çevrim).
 * Dönen: { Stations: [{ Station, Load, Steps: [{Step, Contribution, Classification}] }], Unassigned: [...], Total,
 *          Max, Bottleneck, Efficiency (%), Loss (%), MinOperators (takt varsa), OverTakt: [istasyon] }
 */
export function lineBalance(study, takt = study.Takt) {
  const rows = inProcessOrder(study, analyzeSteps(study)).filter((r) => r.Contribution !== null);
  const defs = new Map(deriveSteps(study).map((x) => [x.Name, x]));
  const stations = [], unassigned = [];
  for (const r of rows) {
    const def = defs.get(r.Step);
    const item = { Step: r.Step, Contribution: r.Contribution, Classification: def?.Classification ?? "" };
    const name = def?.Station ?? "";
    if (!name) { unassigned.push(item); continue; }
    let st = stations.find((s) => s.Station === name);
    if (!st) { st = { Station: name, Load: 0, Steps: [] }; stations.push(st); }
    st.Steps.push(item);
    st.Load += r.Contribution;
  }
  const total = rows.reduce((s, r) => s + r.Contribution, 0);
  const assigned = stations.reduce((s, x) => s + x.Load, 0);
  const max = stations.length ? Math.max(...stations.map((s) => s.Load)) : null;
  const bottleneck = stations.length ? stations.find((s) => s.Load === max).Station : null;
  const efficiency = stations.length && max > 0 ? (assigned / (stations.length * max)) * 100 : null;
  return {
    Stations: stations, Unassigned: unassigned, Total: rows.length ? total : null, Max: max, Bottleneck: bottleneck,
    Efficiency: efficiency, Loss: efficiency === null ? null : 100 - efficiency,
    MinOperators: takt > 0 && rows.length ? Math.ceil(total / takt - 1e-9) : null,
    OverTakt: takt > 0 ? stations.filter((s) => s.Load > takt + 1e-9).map((s) => s.Station) : []
  };
}

/**
 * Otomatik dengeleme: süreç sırası korunur, istasyon dolunca (yük + adım > takt) sonrakine geçilir.
 * Takttan uzun tek adım kendi istasyonunda kalır. Dönen: Map(adım → "1", "2", …).
 */
export function autoBalance(study, takt = study.Takt) {
  if (!(takt > 0)) return new Map();
  const rows = inProcessOrder(study, analyzeSteps(study));
  const out = new Map();
  let station = 1, load = 0;
  for (const r of rows) {
    const c = r.Contribution ?? 0;
    if (load > 0 && load + c > takt + 1e-9) { station++; load = 0; }
    out.set(r.Step, String(station));
    load += c;
  }
  return out;
}

/**
 * Standart iş kombinasyon tablosu: süreç sırasında el / yürüme / bekleme işleri arka arkaya,
 * makine işi başladığı andan itibaren paralel ilerler (operatörü bekletmez). Süre = katkı (sn/çevrim).
 * Türü boş adım el işi sayılır. Dönen: { Rows: [{Step, Type, Start, End, Seconds}], Operator, Total }
 */
export function standardWork(study) {
  const rows = inProcessOrder(study, analyzeSteps(study)).filter((r) => r.Contribution !== null);
  const defs = new Map(deriveSteps(study).map((x) => [x.Name, x]));
  let cursor = 0, end = 0;
  const out = rows.map((r) => {
    const type = defs.get(r.Step)?.Type || "Manual";
    const start = cursor, finish = start + r.Contribution;
    if (type !== "Machine") cursor = finish;
    end = Math.max(end, finish);
    return { Step: r.Step, Type: type, Start: start, End: finish, Seconds: r.Contribution };
  });
  return { Rows: out, Operator: cursor, Total: Math.max(end, cursor) };
}

/**
 * İyileştirme listesi: israf türü ya da fikri girilmiş adımlar ve NVA sınıflı adımlar (süreç sırasıyla).
 * Pay = adım katkısının çevrim başına standart süreye oranı (%).
 */
export function improvements(study) {
  const rows = inProcessOrder(study, analyzeSteps(study));
  const total = rows.reduce((s, r) => s + (r.Contribution ?? 0), 0);
  const defs = new Map(deriveSteps(study).map((x) => [x.Name, x]));
  return rows.filter((r) => { const d = defs.get(r.Step); return d && (d.Waste || d.Idea || d.Classification === "NVA"); })
    .map((r) => {
      const d = defs.get(r.Step);
      return { Step: r.Step, Classification: d.Classification, Waste: d.Waste ?? "", Idea: d.Idea ?? "",
        Contribution: r.Contribution, Share: total > 0 && r.Contribution !== null ? (r.Contribution / total) * 100 : null };
    });
}

/** Adım karesi: tanımlıysa o; yoksa adımın ilk geçerli kaydının ortası (ms). Kayıt yoksa null. */
export function stepFrame(study, step) {
  const def = study.Steps.find((x) => x.Name === step);
  if (def && Number.isFinite(def.FrameMs)) return def.FrameMs;
  const first = study.Observations.filter((o) => o.Step === step && valid(o)).sort((a, b) => a.StartMs - b.StartMs)[0];
  return first ? Math.round((first.StartMs + first.EndMs) / 2) : null;
}

// ---------------------------------------------------------------- iş örneklemesi

function lnGamma(x) {
  const c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
  let y = x, t = x + 5.5;
  t -= (x + 0.5) * Math.log(t);
  let s = 1.000000000190015;
  for (const ci of c) s += ci / ++y;
  return -t + Math.log(2.5066282746310005 * s / x);
}

/** Düzenlenmiş alt eksik gama P(a, x): seri ya da sürekli kesir. */
function gammaP(a, x) {
  if (x <= 0) return 0;
  if (x < a + 1) {
    let sum = 1 / a, term = sum;
    for (let n = 1; n < 1000; n++) { term *= x / (a + n); sum += term; if (Math.abs(term) < Math.abs(sum) * 1e-16) break; }
    return sum * Math.exp(-x + a * Math.log(x) - lnGamma(a));
  }
  const tiny = 1e-300;
  let b = x + 1 - a, c = 1 / tiny, d = 1 / b, h = d;
  for (let i = 1; i < 1000; i++) {
    const an = -i * (i - a);
    b += 2;
    d = an * d + b; if (Math.abs(d) < tiny) d = tiny;
    c = b + an / c; if (Math.abs(c) < tiny) c = tiny;
    d = 1 / d;
    const del = d * c;
    h *= del;
    if (Math.abs(del - 1) < 1e-16) break;
  }
  return 1 - Math.exp(-x + a * Math.log(x) - lnGamma(a)) * h;
}

const normalCdf = (z) => (z >= 0 ? 0.5 + 0.5 * gammaP(0.5, (z * z) / 2) : 0.5 - 0.5 * gammaP(0.5, (z * z) / 2));

/** Standart normal ters dağılım (MathNet Normal.InvCDF karşılığı; ikiye bölme). */
export function normalInv(p) {
  let lo = -40, hi = 40;
  for (let k = 0; k < 200; k++) {
    const mid = (lo + hi) / 2;
    if (normalCdf(mid) < p) lo = mid; else hi = mid;
  }
  return (lo + hi) / 2;
}

/**
 * İş örneklemesi analizi. Her kategori için pay p, güven aralığı p ± z·√(p(1−p)/n) ve
 * hedef mutlak hata e (yüzde puanı) için gereken gözlem sayısı n = z²·p(1−p)/e².
 * Dönen: { N, Z, Error, Rows: [{Category, Classification, Count, Share, Low, High, Half, Required}], Required, ByClass: {VA,SVA,NVA,""} }
 */
export function analyzeSampling(study) {
  const n = study.Samples.length;
  const z = normalInv((1 + study.Settings.Confidence / 100) / 2);
  const e = (study.SampleError ?? 5) / 100;
  const names = study.Categories.map((c) => c.Name);
  for (const s of study.Samples) if (!names.includes(s.Category)) names.push(s.Category);
  const cls = new Map(study.Categories.map((c) => [c.Name, c.Classification]));
  const rows = names.map((name) => {
    const count = study.Samples.filter((s) => s.Category === name).length;
    const p = n ? count / n : null;
    const half = n ? z * Math.sqrt((p * (1 - p)) / n) : null;
    return {
      Category: name, Classification: cls.get(name) ?? "", Count: count,
      Share: p === null ? null : p * 100, Half: half === null ? null : half * 100,
      Low: p === null ? null : Math.max(0, p - half) * 100, High: p === null ? null : Math.min(1, p + half) * 100,
      Required: p === null ? null : Math.ceil((z * z * p * (1 - p)) / (e * e) - 1e-9)
    };
  });
  const byClass = { VA: 0, SVA: 0, NVA: 0, "": 0 };
  for (const r of rows) byClass[r.Classification] += r.Share ?? 0;
  const required = rows.length && n ? Math.max(...rows.map((r) => r.Required)) : null;
  return { N: n, Z: z, Error: e * 100, Rows: rows, Required: required, ByClass: n ? byClass : null };
}

/**
 * Rastgele gözlem anı: [0, süre) aralığında, mevcut gözlemlere en az `gap` ms uzak bir an (en fazla 200 deneme).
 * random() 0–1 arası sayı verir (testte sabitlenebilir).
 */
export function randomMoment(study, durationMs, gap = 1000, random = Math.random) {
  if (!(durationMs > 0)) return null;
  for (let i = 0; i < 200; i++) {
    const at = Math.floor(random() * durationMs);
    if (study.Samples.every((s) => Math.abs(s.AtMs - at) >= gap)) return at;
  }
  return Math.floor(random() * durationMs);
}

export { studentInv };
