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
export function cycleChart(data, { mean, meanLabel, axis, width = 640 }) {
  const height = 220, left = 40, right = 12, top = 18, bottom = 34;
  const max = Math.max(...data.map((d) => d.value), mean ?? 0, 1e-9) * 1.1;
  const plotW = width - left - right, plotH = height - top - bottom;
  const svg = el("svg", { viewBox: `0 0 ${width} ${height}`, role: "img", class: "chart-svg" });
  const yOf = (v) => top + plotH - (v / max) * plotH;
  svg.append(el("line", { x1: left, x2: width - right, y1: top + plotH, y2: top + plotH, stroke: GRID }));
  const slot = plotW / data.length;
  const bw = Math.max(4, Math.min(36, slot - 4));
  const labelEvery = Math.ceil(data.length / 20);
  data.forEach((d, i) => {
    const x = left + i * slot + (slot - bw) / 2;
    const g = el("g", { class: "bar" });
    g.append(el("title", {}, `${axis} ${d.label} · ${d.text}${d.outlier ? " · ⚠" : ""}`));
    g.append(el("rect", { x, y: yOf(d.value), width: bw, height: Math.max(1, top + plotH - yOf(d.value)), rx: 3, fill: d.outlier ? "#eb6834" : "#2a78d6" }));
    if (i % labelEvery === 0) g.append(el("text", { x: x + bw / 2, y: top + plotH + 14, "text-anchor": "middle", fill: SOFT, "font-size": 11 }, d.label));
    if (d.outlier) g.append(el("text", { x: x + bw / 2, y: yOf(d.value) - 4, "text-anchor": "middle", fill: INK, "font-size": 11 }, "⚠"));
    svg.append(g);
  });
  if (mean) {
    svg.append(el("line", { x1: left, x2: width - right, y1: yOf(mean), y2: yOf(mean), stroke: INK, "stroke-width": 1.5, "stroke-dasharray": "5 4" }));
    // Etiket çubukların üstünde okunabilsin diye beyaz kenarlı yazılır.
    svg.append(el("text", { x: width - right, y: yOf(mean) - 5, "text-anchor": "end", fill: INK, "font-size": 11.5, stroke: "#fff", "stroke-width": 3, "paint-order": "stroke" }, meanLabel));
  }
  svg.append(el("text", { x: left + plotW / 2, y: height - 4, "text-anchor": "middle", fill: SOFT, "font-size": 11 }, axis));
  svg.setAttribute("aria-label", data.map((d) => `${axis} ${d.label}: ${d.text}`).join(", "));
  return svg;
}

const truncate = (s, n) => (s.length > n ? s.slice(0, n - 1) + "…" : s);
