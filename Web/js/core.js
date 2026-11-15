// Video Zaman Etüdü — tarayıcı çekirdeği.
// Core/Study.cs ve Core/StudyTransfer.cs ile aynı kurallar. İki sürümün eşdeğerliği
// Shared/esdeger-ornekler.json ile hem C# (Checks) hem JavaScript (Web/tests) tarafında sınanır.
import {
  parseStrict, JsonError, JsonObject, readInt32, readInt64, readDouble, readBool, readString, readObject, readArray
} from "./strict-json.js";

export const VERSION = "1.4.0";
export const EXTENSION = ".vzetud";
export const CLASSES = ["", "VA", "SVA", "NVA"];
const FORMAT = "video-zaman-etudu";
const PACKAGE_VERSION = 1;
const MAX_BYTES = 32 * 1024 * 1024;
/** (long)TimeSpan.MaxValue.TotalMilliseconds */
const MAX_MS = 922337203685477;

/** Hata: Türkçe mesaj (masaüstüyle aynı) + arayüz çevirisi için anahtar ve parametreler (i18n.js). */
export class ValidationError extends Error {
  constructor(message, key = null, params = {}) { super(message); this.key = key; this.params = params; }
}
export class PackageError extends Error {
  constructor(message, key = null, params = {}) { super(message); this.key = key; this.params = params; }
}

// ---------------------------------------------------------------- model

export function newSettings() {
  return { Confidence: 95, TargetError: 5, Pace: 100, Allowance: 0, RepeatedLow: false, GroupMs: 100, Stable: false };
}

export function newObservation(fields = {}) {
  return { StartMs: 0, EndMs: 0, Step: "Adım", Classification: "", Excluded: false, Cycle: 1, ...fields };
}

export function newStudy(fields = {}) {
  return {
    Id: crypto.randomUUID(), Name: "", Code: "", Video: "", ExpectedVideoName: "",
    Updated: new Date().toISOString(), Observations: [], Settings: newSettings(), Frequency: new Map(), ...fields
  };
}

export const seconds = (row) => (row.EndMs - row.StartMs) / 1000;
export const videoLabel = (study) => (study.Video.length > 0 ? fileName(study.Video) : study.ExpectedVideoName);

export function validateSettings(s) {
  const finite = Number.isFinite;
  if (!finite(s.Confidence) || s.Confidence <= 0 || s.Confidence >= 100 ||
      !finite(s.TargetError) || s.TargetError <= 0 || !finite(s.Pace) || s.Pace <= 0 ||
      !finite(s.Allowance) || s.Allowance < 0 || s.Allowance >= 100 ||
      !Number.isInteger(s.GroupMs) || s.GroupMs <= 0)
    throw new ValidationError("Güven 0–100 arasında, tempo ve hedef hata pozitif, tolerans %0–%100 aralığında (%100 hariç) ve grup hassasiyeti pozitif olmalı.", "settings.invalid");
}

// ---------------------------------------------------------------- Student-t

function lnGamma(x) {
  const c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
  let y = x, t = x + 5.5;
  t -= (x + 0.5) * Math.log(t);
  let s = 1.000000000190015;
  for (const ci of c) s += ci / ++y;
  return -t + Math.log(2.5066282746310005 * s / x);
}

function betaContinuedFraction(a, b, x) {
  const tiny = 1e-300;
  const qab = a + b, qap = a + 1, qam = a - 1;
  let c = 1, d = 1 - qab * x / qap;
  if (Math.abs(d) < tiny) d = tiny;
  d = 1 / d;
  let h = d;
  for (let m = 1; m < 500; m++) {
    const m2 = 2 * m;
    let aa = m * (b - m) * x / ((qam + m2) * (a + m2));
    d = 1 + aa * d; if (Math.abs(d) < tiny) d = tiny;
    c = 1 + aa / c; if (Math.abs(c) < tiny) c = tiny;
    d = 1 / d; h *= d * c;
    aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
    d = 1 + aa * d; if (Math.abs(d) < tiny) d = tiny;
    c = 1 + aa / c; if (Math.abs(c) < tiny) c = tiny;
    d = 1 / d;
    const del = d * c;
    h *= del;
    if (Math.abs(del - 1) < 1e-15) break;
  }
  return h;
}

function incompleteBeta(a, b, x) {
  if (x <= 0) return 0;
  if (x >= 1) return 1;
  const bt = Math.exp(lnGamma(a + b) - lnGamma(a) - lnGamma(b) + a * Math.log(x) + b * Math.log(1 - x));
  return x < (a + 1) / (a + b + 2) ? bt * betaContinuedFraction(a, b, x) / a : 1 - bt * betaContinuedFraction(b, a, 1 - x) / b;
}

function studentCdf(t, df) {
  const p = 0.5 * incompleteBeta(df / 2, 0.5, df / (df + t * t));
  return t > 0 ? 1 - p : p;
}

const tCache = new Map();
/** MathNet StudentT.InvCDF(0, 1, df, p) karşılığı; ikiye bölme ile 1e-12 göreli hassasiyet. */
export function studentInv(df, p) {
  const key = df + "|" + p;
  if (tCache.has(key)) return tCache.get(key);
  let lo = -1e4, hi = 1e4;
  for (let k = 0; k < 300; k++) {
    const mid = (lo + hi) / 2;
    if (studentCdf(mid, df) < p) lo = mid; else hi = mid;
  }
  const value = (lo + hi) / 2;
  tCache.set(key, value);
  return value;
}

// ---------------------------------------------------------------- analiz

const collator = new Intl.Collator("tr");

export function analyzeSteps(study) {
  const settings = study.Settings;
  validateSettings(settings);
  const byStep = new Map();
  for (const row of study.Observations) {
    if (!byStep.has(row.Step)) byStep.set(row.Step, []);
    byStep.get(row.Step).push(row);
  }
  const keys = [...byStep.keys()].sort(collator.compare);
  return keys.map((key) => {
    const group = byStep.get(key);
    const values = group.filter((x) => !x.Excluded && x.EndMs > x.StartMs).map((x) => x.EndMs - x.StartMs).sort((a, b) => a - b);
    const n = values.length;
    const mean = n === 0 ? null : values.reduce((s, x) => s + x, 0) / n / 1000;
    const median = n === 0 ? null : (values[(n - 1) >> 1] / 2 + values[n >> 1] / 2) / 1000;
    const sd = n < 2 ? null : Math.sqrt(values.reduce((s, x) => s + Math.pow(x / 1000 - mean, 2), 0) / (n - 1));
    const t = n < 2 ? null : studentInv(n - 1, (1 + settings.Confidence / 100) / 2);
    const error = t === null || sd === null || mean === null ? null : t * sd / (Math.sqrt(n) * mean) * 100;

    const buckets = new Map();
    for (const x of values) {
      const k = Math.round(x / settings.GroupMs) * settings.GroupMs; // değerler pozitif: AwayFromZero ile aynı
      if (!buckets.has(k)) buckets.set(k, []);
      buckets.get(k).push(x);
    }
    const groups = [...buckets.values()]
      .map((g) => ({ Seconds: g.reduce((s, x) => s + x, 0) / g.length / 1000, Count: g.length }))
      .sort((a, b) => b.Count - a.Count || a.Seconds - b.Seconds);
    const repeated = groups.find((g) => g.Count >= 2) ?? null;

    const basis = settings.RepeatedLow ? (repeated ? repeated.Seconds : null) : mean;
    const normal = basis === null ? null : basis * settings.Pace / 100;
    const standard = normal === null ? null : normal / (1 - settings.Allowance / 100);
    const every = Math.max(1, study.Frequency.has(key) ? study.Frequency.get(key) : 1);
    return {
      Step: key, Count: n, Excluded: group.length - n, Mean: mean, Median: median,
      Min: n === 0 ? null : values[0] / 1000, Max: n === 0 ? null : values[n - 1] / 1000,
      Deviation: sd, CriticalT: t, ErrorPercent: error,
      Sufficiency: error === null ? "Hesaplanamıyor" : error <= settings.TargetError ? "Yeterli" : "Yetersiz",
      Base: basis, Normal: normal, Standard: standard, Every: every,
      Contribution: standard === null ? null : standard / every,
      Method: settings.RepeatedLow ? (repeated === null ? "Tekrar yok; ortalamayı seçebilirsiniz" : "Tekrarlanan düşük süre") : "Ortalama",
      Groups: groups
    };
  });
}

export function analyzeCycles(study) {
  const byCycle = new Map();
  for (const row of study.Observations) {
    if (row.Excluded || row.EndMs <= row.StartMs) continue;
    if (!byCycle.has(row.Cycle)) byCycle.set(row.Cycle, []);
    byCycle.get(row.Cycle).push(row);
  }
  return [...byCycle.keys()].sort((a, b) => a - b).map((cycle) => {
    const group = byCycle.get(cycle);
    let total = 0, end = 0;
    for (const row of [...group].sort((a, b) => a.StartMs - b.StartMs)) {
      total += Math.max(0, row.EndMs - Math.max(end, row.StartMs));
      end = Math.max(end, row.EndMs);
    }
    return { Cycle: cycle, Elapsed: total / 1000, Workload: group.reduce((s, x) => s + seconds(x), 0) };
  });
}

// ---------------------------------------------------------------- dosya adları

// .NET char.IsWhiteSpace kümesi (JavaScript \s'ten farklı olarak U+FEFF içermez).
const DOTNET_WS = "\u0009\u000A\u000B\u000C\u000D \u0085                 　";
const isBlank = (s) => s === null || [...s].every((c) => DOTNET_WS.includes(c));
const dotnetTrim = (s) => {
  let a = 0, b = s.length;
  while (a < b && DOTNET_WS.includes(s[a])) a++;
  while (b > a && DOTNET_WS.includes(s[b - 1])) b--;
  return s.slice(a, b);
};

export const fileName = (path) => path.replaceAll("\\", "/").split("/").at(-1);

const RESERVED = ["CON", "PRN", "AUX", "NUL", ...Array.from({ length: 9 }, (_, i) => "COM" + (i + 1)), ...Array.from({ length: 9 }, (_, i) => "LPT" + (i + 1))];

export function suggestedFileName(study, extension, suffix = "") {
  let name = [...dotnetTrim(study.Code ?? "")].map((c) => (c.charCodeAt(0) < 32 || "<>:\"/\\|?*".includes(c) ? "_" : c)).join("");
  name = dotnetTrim(name).replace(/[. ]+$/, "");
  if (name.length === 0) name = "VideoZamanEtudu";
  if (RESERVED.includes(name.toUpperCase())) name += "_";
  if (name.length > 80) name = name.slice(0, 80);
  return name + suffix + extension;
}

// ---------------------------------------------------------------- paket

function validatePackage(data) {
  if (isBlank(data.Name) || isBlank(data.Code) || data.VideoName === null || data.Settings === null ||
      data.Observations === null || data.Frequency === null)
    throw new PackageError("Etüt paketinde zorunlu alanlar eksik.", "pkg.required");
  try {
    validateSettings(data.Settings);
  } catch (error) {
    throw new PackageError("Etüt paketindeki analiz ayarları geçersiz. " + error.message, "pkg.settings", { detail: { key: "settings.invalid" } });
  }
  if (data.Observations.some((row) => row === null || typeof row.StartMs === "bigint" || typeof row.EndMs === "bigint" ||
      row.StartMs < 0 || row.EndMs <= row.StartMs || row.EndMs > MAX_MS || row.Cycle < 1 ||
      isBlank(row.Step) || !CLASSES.includes(row.Classification)))
    throw new PackageError("Etüt paketinde geçersiz ölçüm var.", "pkg.observation");
  for (const [key, value] of data.Frequency)
    if (isBlank(key) || value < 1) throw new PackageError("Etüt paketinde geçersiz iş sıklığı var.", "pkg.frequency");
}

/** Etüt → .vzetud metni (masaüstüyle aynı alan adları ve sıra). */
export function exportPackage(study) {
  const data = {
    Kind: FORMAT, Version: PACKAGE_VERSION, Name: study.Name, Code: study.Code,
    VideoName: fileName(study.Video.length > 0 ? study.Video : study.ExpectedVideoName),
    Settings: study.Settings, Observations: study.Observations, Frequency: study.Frequency
  };
  validatePackage(data);
  const json = JSON.stringify({
    Kind: data.Kind, Version: data.Version, Name: data.Name, Code: data.Code, VideoName: data.VideoName,
    Settings: {
      Confidence: data.Settings.Confidence, TargetError: data.Settings.TargetError, Pace: data.Settings.Pace,
      Allowance: data.Settings.Allowance, RepeatedLow: data.Settings.RepeatedLow, GroupMs: data.Settings.GroupMs, Stable: data.Settings.Stable
    },
    Observations: data.Observations.map((o) => ({
      StartMs: o.StartMs, EndMs: o.EndMs, Seconds: seconds(o), Step: o.Step, Classification: o.Classification, Excluded: o.Excluded, Cycle: o.Cycle
    })),
    Frequency: Object.fromEntries(data.Frequency)
  });
  const bytes = new TextEncoder().encode(json);
  if (bytes.length <= 0 || bytes.length > MAX_BYTES) throw new PackageError("Etüt paketi boş olamaz ve 32 MB’ı aşamaz.", "pkg.size");
  return json;
}

function readSettings(node) {
  const obj = readObject(node);
  if (obj === null) return null;
  const s = newSettings();
  const fields = { Confidence: readDouble, TargetError: readDouble, Pace: readDouble, Allowance: readDouble, RepeatedLow: readBool, GroupMs: readInt32, Stable: readBool };
  for (const [name, read] of Object.entries(fields)) if (obj.has(name)) s[name] = read(obj.get(name));
  return s;
}

function readObservation(node) {
  if (node === null) return null;
  if (!(node instanceof JsonObject)) throw new JsonError();
  const o = newObservation();
  const fields = { StartMs: readInt64, EndMs: readInt64, Step: readString, Classification: readString, Excluded: readBool, Cycle: readInt32 };
  for (const [name, read] of Object.entries(fields)) if (node.has(name)) o[name] = read(node.get(name));
  return o;
}

function readFrequency(node) {
  const obj = readObject(node);
  if (obj === null) return null;
  const map = new Map();
  for (const [key, value] of obj.entries()) map.set(key, readInt32(value));
  return map;
}

/** .vzetud baytları → yeni etüt. Geçersiz paket PackageError fırlatır; kayıt oluşturulmaz. */
export function importPackage(bytes) {
  if (bytes.length <= 0 || bytes.length > MAX_BYTES) throw new PackageError("Etüt paketi boş olamaz ve 32 MB’ı aşamaz.", "pkg.size");
  let data;
  try {
    const text = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
    const root = parseStrict(text);
    if (!(root instanceof JsonObject) || typeof root.get("Kind") !== "string" || root.get("Kind") !== FORMAT || !root.has("Version"))
      throw new PackageError("Bu dosya bir Video Zaman Etüdü paketi değil.", "pkg.notPackage");
    let version;
    try { version = readInt32(root.get("Version")); } catch { throw new PackageError("Bu dosya bir Video Zaman Etüdü paketi değil.", "pkg.notPackage"); }
    if (version !== PACKAGE_VERSION) throw new PackageError("Bu etüt paketi sürümü desteklenmiyor.", "pkg.version");
    for (const required of ["Kind", "Version", "Name", "Code", "VideoName", "Settings", "Observations", "Frequency"])
      if (!root.has(required)) throw new JsonError();
    const observations = readArray(root.get("Observations"));
    data = {
      Name: readString(root.get("Name")), Code: readString(root.get("Code")), VideoName: readString(root.get("VideoName")),
      Settings: readSettings(root.get("Settings")),
      Observations: observations === null ? null : observations.map(readObservation),
      Frequency: readFrequency(root.get("Frequency"))
    };
  } catch (error) {
    if (error instanceof PackageError) throw error;
    if (error instanceof JsonError || error instanceof TypeError) throw new PackageError("Etüt paketi eksik veya bozuk.", "pkg.corrupt");
    throw error;
  }
  validatePackage(data);
  return newStudy({
    Name: data.Name, Code: data.Code, ExpectedVideoName: fileName(data.VideoName),
    Settings: data.Settings, Observations: data.Observations, Frequency: data.Frequency
  });
}

// ---------------------------------------------------------------- arayüz yardımcıları

/** "1,5", "1.5", "1e2" kabul; binlik ayırıcı yok. */
export function parseNumber(text, name) {
  const t = String(text).trim();
  if (/^[+-]?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?$/.test(t)) {
    const v = Number(t.replace(",", "."));
    if (Number.isFinite(v)) return v;
  }
  throw new ValidationError(`${name} geçerli bir sayı olmalı.`, "number.invalid", { name });
}

export function parsePositiveInt(text, name) {
  const t = String(text).trim();
  if (/^\+?\d+$/.test(t)) {
    const v = Number(t);
    if (Number.isSafeInteger(v) && v > 0 && v <= 2147483647) return v;
  }
  throw new ValidationError(`${name} pozitif bir tam sayı olmalı.`, "int.invalid", { name });
}

/** IndexedDB kaydı ↔ çalışma nesnesi (Map yapısal kopyada saklanabilir ama açık tutuyoruz). */
export function toRecord(study) {
  return { ...study, Frequency: [...study.Frequency.entries()] };
}

export function fromRecord(record) {
  if (!record || typeof record.Id !== "string" || typeof record.Name !== "string" || typeof record.Code !== "string" ||
      !Array.isArray(record.Observations) || !record.Settings || !Array.isArray(record.Frequency))
    throw new ValidationError("Kayıt okunamadı.");
  return {
    Id: record.Id, Name: record.Name, Code: record.Code, Video: String(record.Video ?? ""),
    ExpectedVideoName: String(record.ExpectedVideoName ?? ""), Updated: String(record.Updated ?? ""),
    Observations: record.Observations.map((o) => newObservation(o)), Settings: { ...newSettings(), ...record.Settings },
    Frequency: new Map(record.Frequency)
  };
}
