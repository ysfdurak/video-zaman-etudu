// Analiz grafikleri: bağımlılıksız SVG. Renkler sınıfa bağlıdır (VA / SVA / NVA / sınıfsız), sıraya değil.
// Palet renk körlüğü denetiminden geçti (dataviz validate_palette: VA–SVA–NVA bitişik çiftler ΔE ≥ 23).
// Açık zeminde VA yeşilinin karşıtlığı 3:1'in altında olduğu için her çubuğun değeri yazıyla da gösterilir.

export const CLASS_COLORS = { VA: "#1baf7a", SVA: "#2a78d6", NVA: "#eb6834", "": "#8a979f" };
const NS = "http://www.w3.org/2000/svg";
const INK = "#17364a", SOFT = "#5b7383", GRID = "#e3eaee";

function el(name, attrs = {}, text = null) {
  const node = document.createElementNS(NS, name);
  for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, String(v));
  if (text !== null) node.textContent = text;
  return node;
}

/** Yatay çubuklar: adım başına standart katkı (sn/çevrim), süreç sırasıyla. data: [{label, value, cls, text}] */
export function stepChart(data, { classLabel = (c) => c, width = 640 } = {}) {
  const row = 26, left = width < 420 ? 96 : 150, right = width < 420 ? 64 : 90, top = 6;
  const height = top + data.length * row + 34;
  const max = Math.max(...data.map((d) => d.value), 1e-9);
  const svg = el("svg", { viewBox: `0 0 ${width} ${height}`, role: "img", class: "chart-svg" });
  const plot = width - left - right;
  svg.append(el("line", { x1: left, x2: left, y1: top, y2: top + data.length * row, stroke: GRID }));
  data.forEach((d, i) => {
    const y = top + i * row;
    const w = Math.max(2, (d.value / max) * plot);
    const g = el("g", { class: "bar" });
    g.append(el("title", {}, `${d.label} · ${d.text}${d.cls ? " · " + classLabel(d.cls) : ""}`));
    g.append(el("text", { x: left - 8, y: y + row / 2 + 4, "text-anchor": "end", fill: INK, "font-size": 12 }, truncate(d.label, left < 120 ? 13 : 22)));
    g.append(el("rect", { x: left, y: y + 5, width: w, height: row - 10, rx: 3, fill: CLASS_COLORS[d.cls] ?? CLASS_COLORS[""] }));
    g.append(el("text", { x: left + w + 6, y: y + row / 2 + 4, fill: INK, "font-size": 12 }, d.text));
    svg.append(g);
  });
  // Açıklama: yalnız kullanılan sınıflar.
  const used = [...new Set(data.map((d) => d.cls ?? ""))].sort((a, b) => "VA SVA NVA ".indexOf(a || "~") - "VA SVA NVA ".indexOf(b || "~"));
  let x = left;
  const ly = top + data.length * row + 20;
  for (const c of used) {
    svg.append(el("rect", { x, y: ly - 9, width: 10, height: 10, rx: 2, fill: CLASS_COLORS[c] ?? CLASS_COLORS[""] }));
    const label = el("text", { x: x + 14, y: ly, fill: SOFT, "font-size": 11.5 }, classLabel(c));
    svg.append(label);
    x += 30 + classLabel(c).length * 6.5;
  }
  svg.setAttribute("aria-label", data.map((d) => `${d.label} ${d.text}`).join(", "));
  return svg;
}

/** Dikey çubuklar: çevrim başına ölçülen geçen süre; ortalama çizgisi; ortalamadan %20+ sapanlar işaretli. */
export function cycleChart(data, { mean, meanLabel, axis, width = 640, format = (v) => String(v) }) {
  const height = 240, left = 44, right = 12, top = 30, bottom = 34;
  const peak = Math.max(...data.map((d) => d.value), mean ?? 0, 1e-9);
  const step = niceStep(peak / 4);
  const max = Math.ceil((peak * 1.08) / step) * step;
  const plotW = width - left - right, plotH = height - top - bottom;
  const svg = el("svg", { viewBox: `0 0 ${width} ${height}`, role: "img", class: "chart-svg" });
  const yOf = (v) => top + plotH - (v / max) * plotH;
  // Y ekseni: yuvarlak değerlerde ince ızgara ve etiket.
  for (let v = 0; v <= max + 1e-9; v += step) {
    svg.append(el("line", { x1: left, x2: width - right, y1: yOf(v), y2: yOf(v), stroke: GRID, "stroke-width": v === 0 ? 1 : 0.6 }));
    svg.append(el("text", { x: left - 6, y: yOf(v) + 4, "text-anchor": "end", fill: SOFT, "font-size": 10.5 }, format(v)));
  }
  const slot = plotW / data.length;
  const bw = Math.max(4, Math.min(36, slot - 4));
  const labelEvery = Math.ceil(data.length / 20);
  const showValues = data.length <= 12 && slot >= 34;
  data.forEach((d, i) => {
    const x = left + i * slot + (slot - bw) / 2;
    const g = el("g", { class: "bar" });
    g.append(el("title", {}, `${axis} ${d.label} · ${d.text}${d.outlier ? " · ⚠" : ""}`));
    g.append(el("rect", { x, y: yOf(d.value), width: bw, height: Math.max(1, top + plotH - yOf(d.value)), rx: 3, fill: d.outlier ? "#eb6834" : "#2a78d6" }));
    if (i % labelEvery === 0) g.append(el("text", { x: x + bw / 2, y: top + plotH + 14, "text-anchor": "middle", fill: SOFT, "font-size": 11 }, d.label));
    const tag = (d.outlier ? "⚠ " : "") + (showValues ? format(d.value) : "");
    if (tag) g.append(el("text", { x: x + bw / 2, y: yOf(d.value) - 4, "text-anchor": "middle", fill: INK, "font-size": 10.5, stroke: "#fff", "stroke-width": 3, "paint-order": "stroke" }, tag));
    svg.append(g);
  });
  if (mean) {
    svg.append(el("line", { x1: left, x2: width - right, y1: yOf(mean), y2: yOf(mean), stroke: INK, "stroke-width": 1.5, "stroke-dasharray": "5 4" }));
    // Ortalama etiketi çizimin üstünde açıklama olarak durur; çubuklarla çakışmaz.
    svg.append(el("line", { x1: width - right - 24, x2: width - right, y1: 12, y2: 12, stroke: INK, "stroke-width": 1.5, "stroke-dasharray": "5 4" }));
    svg.append(el("text", { x: width - right - 30, y: 16, "text-anchor": "end", fill: INK, "font-size": 11.5 }, meanLabel));
  }
  svg.append(el("text", { x: left + plotW / 2, y: height - 4, "text-anchor": "middle", fill: SOFT, "font-size": 11 }, axis));
  svg.setAttribute("aria-label", data.map((d) => `${axis} ${d.label}: ${d.text}`).join(", "));
  return svg;
}

/** 1, 2, 2.5, 5 × 10^k basamaklarından en yakın yuvarlak adım. */
function niceStep(raw) {
  if (!(raw > 0)) return 1;
  const p = 10 ** Math.floor(Math.log10(raw));
  for (const m of [1, 2, 2.5, 5, 10]) if (raw <= m * p) return m * p;
  return 10 * p;
}

const truncate = (s, n) => (s.length > n ? s.slice(0, n - 1) + "…" : s);

// ---------------------------------------------------------------- v1.8

/** Histogram: data [{From, To, Count}] (sn). */
export function histogramChart(data, { width = 640, axis = "", format = (v) => String(v) } = {}) {
  const height = 200, left = 36, right = 12, top = 14, bottom = 38;
  const max = Math.max(...data.map((d) => d.Count), 1);
  const plotW = width - left - right, plotH = height - top - bottom;
  const svg = el("svg", { viewBox: `0 0 ${width} ${height}`, role: "img", class: "chart-svg" });
  const yOf = (v) => top + plotH - (v / max) * plotH;
  for (let v = 0; v <= max; v += Math.max(1, Math.ceil(max / 4))) {
    svg.append(el("line", { x1: left, x2: width - right, y1: yOf(v), y2: yOf(v), stroke: GRID, "stroke-width": v === 0 ? 1 : 0.6 }));
    svg.append(el("text", { x: left - 6, y: yOf(v) + 4, "text-anchor": "end", fill: SOFT, "font-size": 10.5 }, String(v)));
  }
  const bw = plotW / data.length;
  data.forEach((d, i) => {
    const x = left + i * bw;
    const g = el("g", { class: "bar" });
    g.append(el("title", {}, `${format(d.From)}–${format(d.To)}: ${d.Count}`));
    g.append(el("rect", { x: x + 1, y: yOf(d.Count), width: Math.max(1, bw - 2), height: Math.max(0, top + plotH - yOf(d.Count)), rx: 2, fill: "#2a78d6" }));
    if (d.Count) g.append(el("text", { x: x + bw / 2, y: yOf(d.Count) - 4, "text-anchor": "middle", fill: INK, "font-size": 10.5 }, String(d.Count)));
    svg.append(g);
  });
  const ticks = data.length > 6 ? [0, Math.floor(data.length / 2), data.length] : data.map((_, i) => i).concat(data.length);
  for (const i of ticks) {
    const v = i < data.length ? data[i].From : data[data.length - 1].To;
    svg.append(el("text", { x: left + i * bw, y: top + plotH + 14, "text-anchor": "middle", fill: SOFT, "font-size": 10.5 }, format(v)));
  }
  svg.append(el("text", { x: left + plotW / 2, y: height - 4, "text-anchor": "middle", fill: SOFT, "font-size": 11 }, axis));
  svg.setAttribute("aria-label", data.map((d) => `${format(d.From)}–${format(d.To)}: ${d.Count}`).join(", "));
  return svg;
}

/**
 * Yamazumi: istasyon başına yığılmış çubuk (adımlar sınıf renginde, aralarında beyaz boşluk), takt çizgisi.
 * stations: [{label, steps: [{label, value, cls}], load, text}]
 */
export function yamazumiChart(stations, { width = 640, takt = null, taktLabel = "", format = (v) => String(v), classLabel = (c) => c } = {}) {
  const height = 280, left = 44, right = 12, top = 26, bottom = 40;
  const peak = Math.max(...stations.map((s) => s.load), takt ?? 0, 1e-9);
  const step = niceStepOf(peak / 4), max = Math.ceil((peak * 1.08) / step) * step;
  const plotW = width - left - right, plotH = height - top - bottom;
  const yOf = (v) => top + plotH - (v / max) * plotH;
  const svg = el("svg", { viewBox: `0 0 ${width} ${height}`, role: "img", class: "chart-svg" });
  for (let v = 0; v <= max + 1e-9; v += step) {
    svg.append(el("line", { x1: left, x2: width - right, y1: yOf(v), y2: yOf(v), stroke: GRID, "stroke-width": v === 0 ? 1 : 0.6 }));
    svg.append(el("text", { x: left - 6, y: yOf(v) + 4, "text-anchor": "end", fill: SOFT, "font-size": 10.5 }, format(v)));
  }
  const slot = plotW / Math.max(1, stations.length), bw = Math.max(10, Math.min(70, slot - 16));
  stations.forEach((s, i) => {
    const x = left + i * slot + (slot - bw) / 2;
    let acc = 0;
    for (const p of s.steps) {
      const y1 = yOf(acc + p.value), y0 = yOf(acc);
      const g = el("g", { class: "bar" });
      g.append(el("title", {}, `${s.label} · ${p.label} · ${format(p.value)}${p.cls ? " · " + classLabel(p.cls) : ""}`));
      g.append(el("rect", { x, y: y1, width: bw, height: Math.max(1, y0 - y1 - 2), rx: 2, fill: CLASS_COLORS[p.cls] ?? CLASS_COLORS[""] }));
      if (y0 - y1 > 16 && bw > 44) g.append(el("text", { x: x + bw / 2, y: (y0 + y1) / 2 + 3, "text-anchor": "middle", fill: "#fff", "font-size": 10 }, truncate(p.label, Math.floor(bw / 6.5))));
      svg.append(g);
      acc += p.value;
    }
    svg.append(el("text", { x: x + bw / 2, y: yOf(acc) - 5, "text-anchor": "middle", fill: INK, "font-size": 11, stroke: "#fff", "stroke-width": 3, "paint-order": "stroke" }, s.text));
    svg.append(el("text", { x: x + bw / 2, y: top + plotH + 15, "text-anchor": "middle", fill: INK, "font-size": 11.5 }, truncate(s.label, Math.max(6, Math.floor(slot / 7)))));
  });
  if (takt > 0) {
    svg.append(el("line", { x1: left, x2: width - right, y1: yOf(takt), y2: yOf(takt), stroke: "#b42318", "stroke-width": 1.6, "stroke-dasharray": "6 4" }));
    svg.append(el("line", { x1: width - right - 24, x2: width - right, y1: 12, y2: 12, stroke: "#b42318", "stroke-width": 1.6, "stroke-dasharray": "6 4" }));
    svg.append(el("text", { x: width - right - 30, y: 16, "text-anchor": "end", fill: INK, "font-size": 11.5 }, taktLabel));
  }
  svg.setAttribute("aria-label", stations.map((s) => `${s.label}: ${s.text}`).join(", "));
  return svg;
}

/**
 * Standart iş (Gantt): satır başına adım; el işi düz, yürüme dalgalı (kesikli), makine kesikli, bekleme açık.
 * rows: [{label, type, typeLabel, start, end, text}]
 */
export function workChart(rows, { width = 760, takt = null, taktLabel = "", total = 0, format = (v) => String(v) } = {}) {
  const row = 24, left = width < 520 ? 100 : 170, right = 16, top = 22, bottom = 30;
  const height = top + rows.length * row + bottom;
  const peak = Math.max(total, takt ?? 0, ...rows.map((r) => r.end), 1e-9);
  const step = niceStepOf(peak / 5), max = Math.ceil((peak * 1.04) / step) * step;
  const plotW = width - left - right;
  const xOf = (v) => left + (v / max) * plotW;
  const svg = el("svg", { viewBox: `0 0 ${width} ${height}`, role: "img", class: "chart-svg" });
  for (let v = 0; v <= max + 1e-9; v += step) {
    svg.append(el("line", { x1: xOf(v), x2: xOf(v), y1: top - 4, y2: top + rows.length * row, stroke: GRID, "stroke-width": 0.6 }));
    svg.append(el("text", { x: xOf(v), y: top + rows.length * row + 14, "text-anchor": "middle", fill: SOFT, "font-size": 10.5 }, format(v)));
  }
  const style = {
    Manual: { stroke: "#1baf7a", dash: null, w: 7 }, Machine: { stroke: "#2a78d6", dash: "5 3", w: 3 },
    Walk: { stroke: "#eb6834", dash: "2 3", w: 4 }, Wait: { stroke: "#8a979f", dash: "1 4", w: 4 }
  };
  rows.forEach((r, i) => {
    const y = top + i * row + row / 2;
    const s = style[r.type] ?? style.Manual;
    const g = el("g", { class: "bar" });
    g.append(el("title", {}, `${r.label} · ${r.typeLabel} · ${format(r.start)}–${format(r.end)}`));
    g.append(el("text", { x: left - 8, y: y + 4, "text-anchor": "end", fill: INK, "font-size": 11.5 }, truncate(r.label, left < 120 ? 13 : 24)));
    g.append(el("line", { x1: xOf(r.start), x2: Math.max(xOf(r.start) + 2, xOf(r.end)), y1: y, y2: y, stroke: s.stroke, "stroke-width": s.w, "stroke-linecap": "round", ...(s.dash ? { "stroke-dasharray": s.dash } : {}) }));
    g.append(el("text", { x: xOf(r.end) + 5, y: y + 4, fill: SOFT, "font-size": 10.5 }, r.text));
    svg.append(g);
  });
  if (takt > 0) {
    svg.append(el("line", { x1: xOf(takt), x2: xOf(takt), y1: top - 8, y2: top + rows.length * row, stroke: "#b42318", "stroke-width": 1.6, "stroke-dasharray": "6 4" }));
    svg.append(el("text", { x: xOf(takt), y: top - 10, "text-anchor": "middle", fill: "#b42318", "font-size": 11 }, taktLabel));
  }
  svg.setAttribute("aria-label", rows.map((r) => `${r.label} ${r.typeLabel} ${r.text}`).join(", "));
  return svg;
}

/** Örnekleme: kategori başına yatay çubuk (pay) ve güven aralığı çizgisi. rows: [{label, share, low, high, cls, text}] */
export function samplingChart(rows, { width = 640, classLabel = (c) => c } = {}) {
  const row = 28, left = width < 420 ? 96 : 160, right = 70, top = 8;
  const height = top + rows.length * row + 30;
  const plotW = width - left - right;
  const xOf = (v) => left + (v / 100) * plotW;
  const svg = el("svg", { viewBox: `0 0 ${width} ${height}`, role: "img", class: "chart-svg" });
  for (const v of [0, 25, 50, 75, 100]) {
    svg.append(el("line", { x1: xOf(v), x2: xOf(v), y1: top, y2: top + rows.length * row, stroke: GRID, "stroke-width": 0.6 }));
    svg.append(el("text", { x: xOf(v), y: top + rows.length * row + 14, "text-anchor": "middle", fill: SOFT, "font-size": 10.5 }, String(v)));
  }
  rows.forEach((r, i) => {
    const y = top + i * row;
    const g = el("g", { class: "bar" });
    g.append(el("title", {}, `${r.label} · ${r.text}${r.cls ? " · " + classLabel(r.cls) : ""}`));
    g.append(el("text", { x: left - 8, y: y + row / 2 + 4, "text-anchor": "end", fill: INK, "font-size": 12 }, truncate(r.label, left < 120 ? 13 : 22)));
    g.append(el("rect", { x: left, y: y + 6, width: Math.max(1, xOf(r.share) - left), height: row - 12, rx: 3, fill: CLASS_COLORS[r.cls] ?? CLASS_COLORS[""] }));
    g.append(el("line", { x1: xOf(r.low), x2: xOf(r.high), y1: y + row / 2, y2: y + row / 2, stroke: INK, "stroke-width": 1.5 }));
    for (const v of [r.low, r.high]) g.append(el("line", { x1: xOf(v), x2: xOf(v), y1: y + row / 2 - 5, y2: y + row / 2 + 5, stroke: INK, "stroke-width": 1.5 }));
    g.append(el("text", { x: Math.max(xOf(r.high), xOf(r.share)) + 6, y: y + row / 2 + 4, fill: INK, "font-size": 11.5 }, r.text));
    svg.append(g);
  });
  svg.setAttribute("aria-label", rows.map((r) => `${r.label} ${r.text}`).join(", "));
  return svg;
}

function niceStepOf(raw) {
  if (!(raw > 0)) return 1;
  const p = 10 ** Math.floor(Math.log10(raw));
  for (const m of [1, 2, 2.5, 5, 10]) if (raw <= m * p) return m * p;
  return 10 * p;
}
