// Video Zaman Etüdü — tarayıcı arayüzü. Desktop/MainWindow.xaml.cs ile aynı akış ve mesajlar.
import {
  newStudy, newObservation, seconds, videoLabel, analyzeSteps, analyzeCycles, studentInv, validateSettings,
  exportPackage, importPackage, suggestedFileName, parseNumber, parsePositiveInt, EXTENSION,
  INFO_FIELDS, cleanInfo, cleanSteps, deriveSteps, inProcessOrder, cycleSpread, totalStandard, compareStudies, cleanTakt, stepOrder, VERSION, requiredObservations,
  ALLOWANCE_PARTS, cleanAllowanceParts, allowanceSum,
  cleanRating, cleanCategories, cleanSampleError, WORK_TYPES, WASTES,
  MODULES, hasModule, cleanModules, copySteps
} from "./core.js";
import { outliers, histogram, lineBalance, autoBalance, standardWork, improvements, analyzeSampling, randomMoment, stepFrame } from "./advanced.js";
import { stepChart, cycleChart, CLASS_COLORS, histogramChart, yamazumiChart, workChart, samplingChart } from "./charts.js";
import { reportBytes } from "./report.js";
import { makeBackup, readBackup, isZip } from "./backup.js";
import { StudyStore } from "./store.js";
import { t, result, getLang, setLang, locale, applyStatic } from "./i18n.js";

/** Siteye konulduğunda Windows sürümünün indirme adresi (boşsa bağlantı gizlenir). */
const DESKTOP_URL = document.documentElement.dataset.desktopUrl ?? "";
const NUDGE_MAX_MS = 1000;
/** Sitede yayınlanan sentetik demolar (Web/demo/, kökteki demo/ klasörünün kopyası); arayüz diline göre seçilir. */
const DEMOS = {
  tr: { package: "demo/sentetik-depo-etudu.vzetud", video: "demo/sentetik-depo-etudu.mp4", code: "DEMO-01", videoName: "sentetik-depo-etudu.mp4" },
  en: { package: "demo/synthetic-warehouse-study.vzetud", video: "demo/synthetic-warehouse-study.mp4", code: "DEMO-01", videoName: "synthetic-warehouse-study.mp4" },
};

const $ = (id) => document.getElementById(id);
const store = new StudyStore();

const state = {
  studies: [], selectedStudyId: null, current: null,
  anchor: null, selectedStart: null, ready: false,
  selectedRow: null, selectedResult: null, internalSeek: false,
  stepIndex: 0,              // sıradaki kaydın adımı (state.current.Steps içindeki sıra)
  resume: null,              // elle başka adıma geçilmeden önceki adım (kapsam dışı kayıttan sonra dönülür)
  undo: [],                  // bu oturumda eklenen kayıtlar: {row, stepIndex, cycle}
  newVideo: null,            // {file, handle}
  videos: new Map(),         // etüt Id → {file, url} (bu oturumda bağlanan videolar)
  selectedSample: null, sampleUndo: [],   // iş örneklemesi (v1.8)
  checked: new Set(), visibleRows: [],     // v1.9: toplu düzenleme için işaretli kayıtlar
  saving: Promise.resolve()
};

const video = $("video");
const timeline = $("timeline");

// ------------------------------------------------------------ biçimlendirme

const fmt3 = (v) => (v === null || v === undefined ? "" : v.toLocaleString(locale(), { minimumFractionDigits: 3, maximumFractionDigits: 3 }));
const fmtNum = (v) => v.toLocaleString(locale(), { useGrouping: false, maximumFractionDigits: 10 });
const pad = (n, w = 2) => String(n).padStart(w, "0");
const fmtDate = (iso) => {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  return getLang() === "tr" ? `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${time}` : `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${time}`;
};
const fmtClock = (ms) => {
  const t = Math.max(0, Math.round(ms));
  return `${pad(Math.floor(t / 3600000))}:${pad(Math.floor(t / 60000) % 60)}:${pad(Math.floor(t / 1000) % 60)}.${pad(t % 1000, 3)}`;
};

/** Tablodaki konum: d:ss,SSS (saat varsa s:dd:ss,SSS). */
const fmtTime = (ms) => {
  const t = Math.max(0, Math.round(ms));
  const sep = getLang() === "tr" ? "," : ".";
  const h = Math.floor(t / 3600000), m = Math.floor(t / 60000) % 60, sec = Math.floor(t / 1000) % 60;
  return `${h ? h + ":" + pad(m) : m}:${pad(sec)}${sep}${pad(t % 1000, 3)}`;
};
const pct = (v, digits = 0) => {
  if (v === null || v === undefined) return "";
  const n = v.toLocaleString(locale(), { maximumFractionDigits: digits, minimumFractionDigits: digits, signDisplay: "exceptZero" });
  return getLang() === "tr" ? (n.startsWith("+") || n.startsWith("-") || n.startsWith("−") ? n[0] + "%" + n.slice(1) : "%" + n) : n + "%";
};

// ------------------------------------------------------------ iletişim kutusu

const dialog = $("dialog");
let dialogOpen = false;

function showDialog(text, { confirm = false, pre = null, title = t("app.title") } = {}) {
  $("dialog-title").textContent = title;
  $("dialog-text").textContent = text;
  $("dialog-pre").hidden = pre === null;
  $("dialog-pre").textContent = pre ?? "";
  $("dialog-no").hidden = !confirm;
  $("dialog-yes").textContent = confirm ? t("dialog.yes") : t("dialog.ok");
  $("dialog-no").textContent = t("dialog.no");
  dialog.returnValue = "";
  dialogOpen = true;
  dialog.showModal();
  $("dialog-yes").focus();
  return new Promise((resolve) => {
    dialog.addEventListener("close", () => { dialogOpen = false; resolve(dialog.returnValue === "yes"); }, { once: true });
  });
}
const ask = (text, title) => showDialog(text, { confirm: true, title });
const setStatusText = (id, text) => { $(id).textContent = text; $(id).hidden = !text; };

/** MainWindow.Guard: hatayı kullanıcıya gösterir, uygulamayı durdurmaz. */
async function guard(action) {
  try { await action(); } catch (error) { await showDialog(errorText(error)); }
}
/** Çekirdek hataları anahtarla gelir (core.js); anahtarsız hata mesajı olduğu gibi gösterilir. */
function errorText(error) {
  if (!error?.key) return error?.message || String(error);
  const params = Object.fromEntries(Object.entries(error.params ?? {}).map(([k, v]) => [k, v && typeof v === "object" && v.key ? t(v.key) : v]));
  return t(error.key, params);
}
const on = (id, action, event = "click") => $(id).addEventListener(event, () => guard(action));

// ------------------------------------------------------------ tablolar

/**
 * rows: veri; cells(row) → [{text, num}]; isSelected(row); onSelect(row); onOpen(row)
 */
function renderTable(id, rows, cells, { isSelected = () => false, onSelect = null, onOpen = null, empty = "" } = {}) {
  const body = $(id).tBodies[0];
  body.replaceChildren();
  if (rows.length === 0 && empty) {
    const tr = body.insertRow();
    tr.className = "empty";
    const td = tr.insertCell();
    td.colSpan = $(id).tHead.rows[0].cells.length;
    td.textContent = empty;
    return;
  }
  for (const row of rows) {
    const tr = body.insertRow();
    tr.tabIndex = -1;
    tr.setAttribute("aria-selected", String(isSelected(row)));
    for (const cell of cells(row)) {
      const td = tr.insertCell();
      if (cell.check) {
        // Satır seçiminden bağımsız işaret (toplu düzenleme); tıklama satırı seçmez.
        const box = Object.assign(document.createElement("input"), { type: "checkbox", checked: cell.checked });
        box.setAttribute("aria-label", cell.label);
        box.addEventListener("click", (e) => e.stopPropagation());
        box.addEventListener("change", () => guard(() => cell.onCheck(box.checked)));
        td.className = "check-cell";
        td.append(box);
        continue;
      }
      td.textContent = cell.text;
      if (cell.num) td.className = "num";
    }
    if (onSelect) tr.addEventListener("click", () => guard(() => onSelect(row)));
    if (onOpen) tr.addEventListener("dblclick", () => guard(() => onOpen(row)));
  }
}
const txt = (text) => ({ text: String(text ?? "") });
const num = (text) => ({ text: String(text ?? ""), num: true });

// ------------------------------------------------------------ sekmeler

// v1.9: 1 Çalışmalar · 2 Hazırlık · 3 Ölçüm · 4 Analiz · 5 İyileştirme
const TABS = 5, TAB = { studies: 0, setup: 1, measure: 2, analysis: 3, improve: 4 };
let activeTab = 0;
function selectTab(index) {
  activeTab = index;
  for (let i = 0; i < TABS; i++) {
    $(`tab-${i}`).setAttribute("aria-selected", String(i === index));
    $(`tab-${i}`).tabIndex = i === index ? 0 : -1;
    $(`panel-${i}`).hidden = i !== index;
  }
  if (index !== TAB.measure) pause();
  if (index === TAB.setup && state.current) renderStepTable();
  if (index === TAB.measure && state.current) renderSteps();
  if ((index === TAB.analysis || index === TAB.improve) && state.current) guard(refreshAnalysis);
}
for (let i = 0; i < TABS; i++) $(`tab-${i}`).addEventListener("click", () => selectTab(i));
// Sekme listesinde ok tuşlarıyla gezinme (WAI-ARIA sekme deseni).
for (const list of document.querySelectorAll('[role="tablist"]'))
  list.addEventListener("keydown", (e) => {
    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(e.key)) return;
    const tabs = [...list.querySelectorAll('[role="tab"]')].filter((b) => !b.disabled && !b.hidden && b.offsetParent !== null);
    const i = tabs.indexOf(document.activeElement);
    if (i < 0) return;
    const next = e.key === "Home" ? 0 : e.key === "End" ? tabs.length - 1 : (i + (e.key === "ArrowRight" ? 1 : -1) + tabs.length) % tabs.length;
    e.preventDefault(); e.stopPropagation();
    tabs[next].focus(); tabs[next].click();
  });

function setTabsEnabled(enabled) {
  for (let i = 1; i < TABS; i++) $(`tab-${i}`).disabled = !enabled;
  applyModules();
}

/** Alt sekmeler (Analiz ve İyileştirme içinde). */
function selectSub(button) {
  for (const b of document.querySelectorAll(`.subtabs [data-group="${button.dataset.group}"]`)) {
    const on_ = b === button;
    b.setAttribute("aria-selected", String(on_));
    b.tabIndex = on_ ? 0 : -1;
    $(b.getAttribute("aria-controls")).hidden = !on_;
  }
}
for (const b of document.querySelectorAll(".subtabs [role=tab]"))
  b.addEventListener("click", () => { selectSub(b); if (state.current) guard(refreshAnalysis); });

/** Modüller: kapalı araçlar ve sütunlar gövde sınıflarıyla gizlenir; İyileştirme sekmesi araç yoksa görünmez. */
function applyModules() {
  const study = state.current;
  for (const m of MODULES) document.body.classList.toggle(`no-mod-${m}`, !!study && (isSampling() || !hasModule(study, m)));
  const anyTool = !!study && !isSampling() && MODULES.some((m) => hasModule(study, m));
  $("tab-4").hidden = !anyTool;
  if (!anyTool && activeTab === TAB.improve) selectTab(TAB.analysis);
  // Seçili alt sekme gizlendiyse ilk görünene geç.
  const group = [...document.querySelectorAll('.subtabs [data-group="im"]')];
  const visible = group.filter((b) => !study || hasModule(study, MODULE_OF[b.id]));
  if (visible.length && !visible.some((b) => b.getAttribute("aria-selected") === "true")) selectSub(visible[0]);
  if (study) for (const box of $("modules").querySelectorAll("input")) box.checked = hasModule(study, box.value);
}
const MODULE_OF = { "st-balance": "Balance", "st-work": "Work", "st-ideas": "Ideas", "st-story": "Cards" };

function updateHeader() {
  const h = $("header");
  h.replaceChildren(t("header.brand") + " ");
  const sep = document.createElement("span");
  sep.className = "sep";
  sep.textContent = "/";
  h.append(sep, state.current ? `${state.current.Code} · ${state.current.Name}` : t("header.studies"));
  document.title = state.current ? `${state.current.Code} · ${t("app.title")}` : t("app.docTitle");
  document.querySelector('meta[name="description"]')?.setAttribute("content", t("app.docDesc"));
}

// ------------------------------------------------------------ çalışmalar

async function reloadStudies() {
  state.studies = await store.readAll();
  renderTable("studies", state.studies,
    (s) => [txt(s.Code), txt(s.Name), txt([s.Info?.Station, s.Info?.Operator].filter(Boolean).join(" · ")),
      num(s.Observations.length), num(new Set(s.Observations.map((o) => o.Cycle)).size || ""), txt(studyState(s)), txt(statusLabel(s)), txt(videoLabel(s)), txt(fmtDate(s.Updated))],
    {
      isSelected: (s) => s.Id === state.selectedStudyId,
      onSelect: (s) => { state.selectedStudyId = s.Id; reloadStudiesView(); },
      onOpen: (s) => openStudy(s),
      empty: t("list.empty")
    });
  const none = !selectedStudy();
  for (const id of ["open", "export", "delete"]) $(id).disabled = none;
  $("compare").disabled = state.studies.length < 2;
  fillStepSources($("new-steps-from"), null);
  // Son açılan etüde dönüş bağlantısı (etüt açık değilken).
  let lastId = null;
  try { lastId = localStorage.getItem(LAST_KEY); } catch { /* gizli pencere */ }
  const last = state.studies.find((s) => s.Id === lastId);
  $("resume-last").hidden = !last || !!state.current;
  if (last) {
    $("resume-last-btn").textContent = t("resume.last", { code: last.Code, name: last.Name });
    if (!state.selectedStudyId) { state.selectedStudyId = last.Id; for (const id of ["open", "export", "delete"]) $(id).disabled = false; }
  }
}
on("resume-last-btn", async () => {
  let lastId = null;
  try { lastId = localStorage.getItem(LAST_KEY); } catch { /* gizli pencere */ }
  const last = state.studies.find((s) => s.Id === lastId);
  if (last) await openStudy(last);
});
/** Durum: Taslak / Tamamlandı / Onaylandı (onaylayan ve tarih ile). */
function statusLabel(study) {
  const label = t("status." + (study.Status || "draft"));
  return study.Status === "approved" ? [label, study.ApprovedBy, study.ApprovedAt ? fmtDay(study.ApprovedAt) : ""].filter(Boolean).join(" · ") : label;
}
const fmtDay = (iso) => { const [y, m, d] = iso.slice(0, 10).split("-"); return getLang() === "tr" ? `${d}.${m}.${y}` : `${y}-${m}-${d}`; };
/** Adım kaynağı olabilecek zaman etütleri (adım listesi ya da ölçümü olan). */
const stepSources = (exceptId) => state.studies.filter((s) => s.Id !== exceptId && s.StudyType !== "sampling" && copySteps(s).length > 0);
function fillStepSources(select, exceptId, withNone = true) {
  const keep = select.value;
  const list = stepSources(exceptId);
  select.replaceChildren(...(withNone ? [new Option(t("stepsFrom.none"), "")] : []),
    ...list.map((s) => new Option(`${s.Code} · ${s.Name} (${copySteps(s).length})`, s.Id)));
  if (list.some((s) => s.Id === keep)) select.value = keep;
  return list;
}
const reloadStudiesView = () => reloadStudies();
/** Listede kısa durum: ölçüm yok / tüm adımlar yeterli / k adım yetersiz. */
function studyState(study) {
  if (study.Observations.length === 0) return t("state.empty");
  const rows = analyzeSteps(study);
  const bad = rows.filter((r) => r.Sufficiency !== "Yeterli" && r.Count > 0).length;
  return bad ? t("state.need", { k: bad }) : t("state.ok");
}
const selectedStudy = () => state.studies.find((s) => s.Id === state.selectedStudyId) ?? null;

/** Video seçimi: destekleyen tarayıcıda dosya tutamacı da alınır (sonraki oturumda yeniden bağlamak için). */
async function pickVideo() {
  if (window.showOpenFilePicker) {
    try {
      const [handle] = await window.showOpenFilePicker({
        types: [{ description: t("picker.videos"), accept: { "video/*": [".mp4", ".m4v", ".mov", ".webm", ".mkv", ".avi", ".wmv", ".mpg", ".mpeg"] } }],
        excludeAcceptAllOption: false, multiple: false
      });
      return { file: await handle.getFile(), handle };
    } catch (error) {
      if (error?.name === "AbortError") return null;
      // izin veya güvenlik hatasında klasik dosya seçiciye düş
    }
  }
  const file = await chooseFile($("video-file"));
  return file ? { file, handle: null } : null;
}

/** Gizli dosya girişini açar; seçim veya iptal ile sonuçlanır. */
function chooseFile(input) {
  input.value = "";
  return new Promise((resolve) => {
    const finish = () => {
      input.removeEventListener("change", finish);
      input.removeEventListener("cancel", finish);
      resolve(input.files?.[0] ?? null);
    };
    input.addEventListener("change", finish);
    input.addEventListener("cancel", finish);
    input.click();
  });
}

on("pick-new-video", async () => {
  const picked = await pickVideo();
  if (!picked) return;
  state.newVideo = picked;
  $("new-video").value = picked.file.name;
});

on("create", async () => {
  const name = $("new-name").value.trim(), code = $("new-code").value.trim();
  if (!name || !code || !state.newVideo) throw new Error(t("msg.createMissing"));
  const lines = $("new-steps").value.split("\n").map((x) => ({ Name: x, Classification: "" }));
  const info = readInfo("new-info");
  const sampling = $("new-type").value === "sampling";
  // v1.9: adımlar başka etütten alınabilir (yazılan satırlar sona eklenir); kapsam (modüller) seçilir.
  const source = sampling ? null : state.studies.find((x) => x.Id === $("new-steps-from").value);
  const fromSource = source ? copySteps(source) : [];
  const modules = [...document.querySelectorAll("#new-modules input:checked")].map((x) => x.value);
  const study = newStudy({ Name: name, Code: code, Video: state.newVideo.file.name, Info: info,
    ...(sampling ? { StudyType: "sampling", Categories: cleanCategories(lines) }
      : { Steps: cleanSteps([...fromSource, ...lines.filter((l) => !fromSource.some((x) => x.Name === l.Name.trim()))]), Modules: cleanModules(modules) }) });
  attachVideo(study, state.newVideo.file);
  if (state.newVideo.handle) await store.saveHandle(study.Id, state.newVideo.handle);
  await store.save(study);
  requestPersistence();
  state.newVideo = null;
  $("new-name").value = ""; $("new-code").value = ""; $("new-video").value = ""; $("new-steps").value = ""; $("new-type").value = ""; syncNewType();
  $("new-steps-from").value = "";
  for (const box of document.querySelectorAll("#new-modules input")) box.checked = false;
  renderInfoFields("new-info", {});
  await openStudy(study);
});

on("open", async () => { const s = selectedStudy(); if (s) await openStudy(s); });

/** Demo etüdü: zaten varsa onu açar, yoksa içe alır. Video sunucudan değil sitenin kendi dosyasından gelir. */
on("demo", async () => {
  const demo = DEMOS[getLang()];
  let study = state.studies.find((s) => s.Code === demo.code && s.ExpectedVideoName === demo.videoName);
  if (!study) {
    const response = await fetch(demo.package);
    if (!response.ok) throw new Error(t("msg.demoFailed"));
    study = importPackage(new Uint8Array(await response.arrayBuffer()));
    await store.save(study);
    requestPersistence();
  }
  await openStudy(study);
});

/** Demo videosu bağlı değilse sitenin kendi kopyasından bağlar. */
async function attachDemoVideo(study) {
  const demo = Object.values(DEMOS).find((d) => d.videoName === videoLabel(study));
  if (!demo) return false;
  try {
    const response = await fetch(demo.video);
    if (!response.ok) return false;
    attachVideo(study, new File([await response.blob()], demo.videoName, { type: "video/mp4" }));
    return true;
  } catch { return false; }
}

on("import", async () => {
  const file = await chooseFile($("package-file"));
  if (!file) return;
  const bytes = new Uint8Array(await file.arrayBuffer());
  if (isZip(bytes)) return restoreBackup(bytes);
  const study = importPackage(bytes);
  if (!(await ask(t("msg.importConfirm", { name: study.Name, code: study.Code }), t("title.import")))) return;
  await store.save(study);
  requestPersistence();
  await openStudy(study);
});

on("export", () => {
  const s = selectedStudy();
  if (!s) throw new Error(t("msg.selectToExport"));
  download(new Blob([exportPackage(s)], { type: "application/octet-stream" }), suggestedFileName(s, EXTENSION));
});

/** Bütün etütler tek .zip: içinde her etüt ayrı .vzetud paketi. */
on("backup", () => {
  if (!state.studies.length) throw new Error(t("msg.backupNone"));
  const { bytes, count, skipped } = makeBackup(state.studies);
  const d = new Date(), pad2 = (n) => String(n).padStart(2, "0");
  download(new Blob([bytes], { type: "application/zip" }), `${t("backup.file")}-${d.getFullYear()}${pad2(d.getMonth() + 1)}${pad2(d.getDate())}.zip`);
  setStatusText("studies-status", t(skipped ? "msg.backupDoneSkipped" : "msg.backupDone", { count, skipped }));
});

async function restoreBackup(bytes) {
  const { add, same, failed } = await readBackup(bytes, state.studies);
  const parts = [t("msg.restoreFound", { add: add.length, same })];
  if (failed.length) parts.push(t("msg.restoreFailed", { count: failed.length, names: failed.slice(0, 5).join(", ") }));
  if (!add.length) { await showDialog(parts.join(" "), { title: t("title.restore") }); return; }
  if (!(await ask(parts.join(" ") + " " + t("msg.restoreConfirm", { add: add.length }), t("title.restore")))) return;
  for (const study of add) await store.save(study);
  requestPersistence();
  await reloadStudies();
  setStatusText("studies-status", t("msg.restoreDone", { add: add.length }));
}

on("delete", async () => {
  const s = selectedStudy();
  if (!s) throw new Error(t("msg.selectToDelete"));
  if (!(await ask(t("msg.deleteConfirm", { name: s.Name, code: s.Code }), t("title.delete")))) return;
  if (state.current?.Id === s.Id) closeStudy();
  detachVideo(s.Id);
  await store.delete(s.Id);
  state.selectedStudyId = null;
  await reloadStudies();
});

function download(blob, name) {
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = name;
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(a.href), 30000);
}

let persistenceAsked = false;
function requestPersistence() {
  if (persistenceAsked || !navigator.storage?.persist) return;
  persistenceAsked = true;
  navigator.storage.persist().catch(() => {});
}

// ------------------------------------------------------------ etüt açma / kapama

const LAST_KEY = "vze-last-study";
async function openStudy(study) {
  try { localStorage.setItem(LAST_KEY, study.Id); } catch { /* gizli pencere */ }
  state.current = study;
  state.selectedStudyId = study.Id;
  updateHeader();
  stopMeasure();
  state.selectedStart = null;
  state.selectedRow = null;
  state.selectedResult = null;
  state.undo = [];
  // Adım listesi yoksa ölçümlerden çıkarılır; ilk değişiklikte kaydedilir.
  study.Steps = deriveSteps(study).map((x) => ({ ...x }));
  state.resume = null;
  // Kaldığı yerden devam: son kaydın ardındaki adım ve çevrim; video son kaydın bitişine gider (video hazır olunca).
  const point = resumePoint(study);
  state.stepIndex = point.step;
  state.resumeAt = point.at;
  $("cycle").value = String(point.cycle);
  $("excluded").checked = false;
  document.body.classList.toggle("sampling-study", isSampling());
  $("sampling-side").hidden = !isSampling();
  $("time-side").hidden = isSampling();
  renderSteps();
  renderSampling();
  renderInfoFields("info-fields", study.Info);
  state.checked.clear();
  $("settings-box").open = false;
  setTabsEnabled(true);
  loadStatus();
  // Yeni etüt (adım ve ölçüm yok): Hazırlık; aksi halde ölçüm.
  selectTab(!isSampling() && study.Steps.length === 0 && study.Observations.length === 0 ? TAB.setup : TAB.measure);
  $("filter-step").value = ""; $("filter-cycle").value = "";
  refreshObservations();
  loadSettings();
  await loadVideo();
  await reloadStudies();
}

/** Son kayıttan sonraki adım/çevrim ve videoda kaldığı konum. */
function resumePoint(study) {
  const list = study.Steps.map((x) => x.Name);
  const rows = [...study.Observations].sort((a, b) => a.EndMs - b.EndMs || a.StartMs - b.StartMs);
  const at = rows.length ? rows[rows.length - 1].EndMs : null;
  const last = [...rows].reverse().find((o) => !o.Excluded && list.includes(o.Step));
  if (!last) return { step: 0, cycle: Math.max(1, ...study.Observations.map((o) => o.Cycle)), at };
  const i = list.indexOf(last.Step);
  return i + 1 < list.length ? { step: i + 1, cycle: last.Cycle, at } : { step: 0, cycle: last.Cycle + 1, at };
}

function closeStudy() {
  pause();
  stopMeasure();
  state.selectedStart = null;
  unloadVideo();
  state.current = null;
  state.selectedRow = state.selectedResult = null;
  state.undo = [];
  for (const id of ["observations", "results", "cycles", "groups"]) $(id).tBodies[0].replaceChildren();
  $("steps").replaceChildren(); $("segments").replaceChildren(); $("charts").replaceChildren(); $("segment-legend").replaceChildren();
  $("row-editor").hidden = true;
  state.checked.clear(); $("bulk-editor").hidden = true;
  $("step-table").tBodies[0].replaceChildren();
  selectTab(0);
  setTabsEnabled(false);
  updateHeader();
}

function attachVideo(study, file) {
  detachVideo(study.Id);
  state.videos.set(study.Id, { file, url: URL.createObjectURL(file) });
}

function detachVideo(id) {
  const v = state.videos.get(id);
  if (v) { URL.revokeObjectURL(v.url); state.videos.delete(id); }
}

function unloadVideo() {
  pause();
  state.ready = false;
  timeline.disabled = true;
  video.removeAttribute("src");
  video.load();
}

async function loadVideo() {
  unloadVideo();
  $("position").textContent = fmtClock(0);
  timeline.value = "0";
  const study = state.current;
  if (!study) return;
  let attached = state.videos.get(study.Id);
  if (!attached) {
    // Daha önce izin verilmiş dosya tutamacı varsa sormadan bağla.
    const handle = await store.readHandle(study.Id);
    if (handle && (await handle.queryPermission?.({ mode: "read" })) === "granted") {
      try { attachVideo(study, await handle.getFile()); attached = state.videos.get(study.Id); } catch { /* dosya taşınmış olabilir */ }
    }
  }
  if (!attached && (await attachDemoVideo(study))) attached = state.videos.get(study.Id);
  if (!attached) {
    const expected = videoLabel(study);
    setStatus(expected ? t("status.noVideoExpected", { name: expected }) : t("status.noVideo"));
    return;
  }
  setStatus(t("status.opening"));
  video.src = attached.url;
  video.load();
}

on("relink", async () => {
  const study = state.current;
  if (!study) return;
  let picked = null;
  // Kayıtlı tutamaç varsa önce izin iste: dosyayı yeniden aramaya gerek kalmaz.
  const handle = await store.readHandle(study.Id);
  if (handle?.requestPermission && !state.videos.has(study.Id)) {
    try {
      if ((await handle.requestPermission({ mode: "read" })) === "granted") picked = { file: await handle.getFile(), handle };
    } catch { /* dosya bulunamazsa seçiciye geç */ }
  }
  picked ??= await pickVideo();
  if (!picked) return;
  const expected = videoLabel(study);
  const differs = expected && picked.file.name !== expected ? t("relink.differs", { name: picked.file.name }) : "";
  const note = expected ? t("relink.note", { name: expected }) : "";
  if (study.Observations.length > 0 && !(await ask(t("relink.confirm", { note, differs }), t("title.relink")))) return;
  study.Video = picked.file.name;
  attachVideo(study, picked.file);
  if (picked.handle) await store.saveHandle(study.Id, picked.handle);
  await save();
  stopMeasure();
  state.selectedStart = null;
  await loadVideo();
});

// ------------------------------------------------------------ video

video.addEventListener("loadedmetadata", () => {
  state.ready = Number.isFinite(video.duration) && video.duration > 0;
  timeline.disabled = !state.ready;
  if (state.ready) timeline.max = String(Math.max(1, Math.round(video.duration * 1000)));
  setStatus(state.ready ? (isSampling() ? t("s.ready") : state.current?.Steps.length ? t("status.ready") : t("status.addSteps")) : t("status.noDuration"));
  if (state.ready && state.resumeAt !== null && state.resumeAt !== undefined) {
    const at = Math.min(state.resumeAt, Math.round(video.duration * 1000));
    state.resumeAt = null;
    setPosition(at);
    const step = currentStep();
    if (step) setStatus(t("status.resume", { at: fmtClock(at), step: step.Name, cycle: cycleValue() }));
  }
  renderSegments();
});
video.addEventListener("error", () => {
  if (!video.getAttribute("src")) return;
  state.ready = false;
  timeline.disabled = true;
  stopMeasure();
  setStatus(t("status.videoError"));
});
video.addEventListener("ended", () => { pause(); updatePosition(); });
// v1.5: konum değişikliği ardışık ölçümü kesmez. Kullanıcı adım sınırını zaman çizgisinde arayıp Ölç’e basabilir;
// Ölç başlangıcın gerisindeyse kayıt eklenmez ve uyarı verilir.
video.addEventListener("seeked", () => { state.internalSeek = false; });
timeline.addEventListener("input", () => { if (state.ready) seek(Number(timeline.value)); });
$("speed").addEventListener("change", () => { video.playbackRate = selectedSpeed(); });
setInterval(updatePosition, 80);

const positionMs = () => Math.round(video.currentTime * 1000);
const selectedSpeed = () => Number($("speed").value) || 1;

function updatePosition() {
  if (!state.ready) return;
  const ms = positionMs();
  timeline.value = String(ms);
  $("position").textContent = fmtClock(ms);
  updateLiveSegment(ms);
  updateLive(ms);
}

function play() {
  if (!state.ready) return;
  video.playbackRate = selectedSpeed();
  video.play().catch(() => {});
}

function pause() {
  if (state.ready && !video.paused) video.pause();
}

function setPosition(ms) {
  state.internalSeek = true;
  video.currentTime = ms / 1000;
  $("position").textContent = fmtClock(ms);
  timeline.value = String(ms);
}

function seek(ms) {
  setPosition(ms);
  if (state.anchor !== null)
    setStatus(t(ms > state.anchor ? "status.measuring" : "status.nudgeBehind", { s: fmt3(state.anchor / 1000) }));
}

/** İnce konumlama: en fazla 1 sn, videoyu duraklatır. */
function nudge(deltaMs) {
  requireVideo();
  const delta = Math.max(-NUDGE_MAX_MS, Math.min(NUDGE_MAX_MS, deltaMs));
  pause();
  const target = Math.max(0, Math.min(positionMs() + delta, Number(timeline.max)));
  setPosition(target);
  if (state.anchor !== null && target <= state.anchor)
    setStatus(t("status.nudgeBehind", { s: fmt3(state.anchor / 1000) }));
}

on("play", play);
on("pause", pause);
on("back", () => nudge(-100));
on("forward", () => nudge(100));

// ------------------------------------------------------------ ölçüm

function setStatus(text) { $("status").textContent = text; }

function stopMeasure() {
  state.anchor = null;
  if ($("now-live")) $("now-live").hidden = true;
  $("measure").textContent = t("btn.start");
  $("measure").classList.remove("on");
  updateLiveSegment();
}

function requireVideo() {
  if (!state.ready || !state.current) throw new Error(t("msg.needVideo"));
}

// ---- adım listesi

const steps = () => state.current?.Steps ?? [];
const currentStep = () => steps()[state.stepIndex] ?? null;

function renderSteps() {
  const box = $("steps");
  box.replaceChildren();
  const list = steps();
  if (state.stepIndex >= list.length) state.stepIndex = Math.max(0, list.length - 1);
  if (list.length === 0) {
    const p = document.createElement("p");
    p.className = "hint empty-steps";
    p.textContent = t("steps.empty");
    box.append(p);
  }
  // v1.9.1: adım düğmesinde yeterlilik göstergesi (geçerli ölçüm / gereken; yeterliyse ✓)
  const progress = new Map(state.current && !isSampling() ? analyzeSteps(state.current).map((r) => [r.Step, r]) : []);
  list.forEach((st, i) => {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "step-chip";
    b.setAttribute("aria-pressed", String(i === state.stepIndex));
    const k = document.createElement("span"); k.className = "key"; k.textContent = i < 9 ? String(i + 1) : "·";
    const n = document.createElement("span"); n.className = "name"; n.textContent = st.Name;
    b.append(k, n);
    if (st.Classification) {
      const c = document.createElement("span");
      c.className = "cls"; c.textContent = st.Classification;
      c.style.setProperty("--cls", CLASS_COLORS[st.Classification]);
      b.append(c);
    }
    const r = progress.get(st.Name);
    let tip = "";
    if (r && r.Count > 0) {
      const g = document.createElement("span");
      g.className = "prog";
      if (r.Sufficiency === "Yeterli") { g.textContent = "✓ " + r.Count; g.classList.add("ok"); tip = t("steps.progressOk", { n: r.Count }); }
      else if (r.Sufficiency === "Yetersiz") { const req = requiredObservations(r, state.current.Settings); g.textContent = `${r.Count}/${req}`; tip = t("steps.progress", { n: r.Count, req }); }
      else { g.textContent = String(r.Count); tip = t("steps.progressNa", { n: r.Count }); }
      b.append(g);
    }
    b.title = [st.Note, tip].filter(Boolean).join("\n");
    b.addEventListener("click", () => guard(() => selectStep(i)));
    box.append(b);
  });
  $("step-names").replaceChildren(...list.map((x) => Object.assign(document.createElement("option"), { value: x.Name })));
  updateNow();
}

function updateNow() {
  const st = currentStep();
  const cycle = $("cycle").value.trim() || "?";
  $("now-step").textContent = st ? t("now.value", { step: st.Name, cycle }) + ($("excluded").checked ? " · " + t("now.excluded") : "") : t("now.none");
  $("now-note").hidden = !st?.Note;
  $("now-note").textContent = st?.Note ? t("now.note", { note: st.Note }) : "";
}

/** Süren ölçümün geçen süresi (Sıradaki kayıt kutusunda). */
function updateLive(now) {
  const el = $("now-live");
  if (state.anchor === null) { el.hidden = true; return; }
  el.hidden = false;
  el.textContent = t(now > state.anchor ? "now.live" : "now.liveBehind", { s: fmt3(Math.max(0, now - state.anchor) / 1000) });
}

/** Elle adım seçimi. Önceki adım hatırlanır: araya giren kapsam dışı kayıttan sonra sıra oraya döner. */
function selectStep(i) {
  if (i < 0 || i >= steps().length) return;
  if (i !== state.stepIndex) state.resume = state.stepIndex;
  state.stepIndex = i;
  renderSteps();
}

/** table: false iken adım tablosu yeniden çizilmez (alan değişikliğinde odak kaybolmasın). */
async function saveSteps({ table = true } = {}) {
  state.current.Steps = cleanSteps(state.current.Steps);
  renderSteps();
  if (table) renderStepTable();
  await save();
}

async function addStep(inputId, classId, { quiet = false } = {}) {
  if (!state.current) return;
  const name = $(inputId).value.trim();
  // Boş kutuda Enter sessizce yok sayılır (ölçerken Enter alışkanlığı).
  if (!name) { if (quiet) return; throw new Error(t("msg.stepEmpty")); }
  if (steps().some((x) => x.Name === name)) throw new Error(t("msg.stepExists", { step: name }));
  steps().push({ Name: name, Classification: $(classId).value });
  $(inputId).value = "";
  // Ölçüm başladıysa yeni adım seçilir; araya giren bir kayıtsa (kapsam dışı) sonra önceki adıma dönülür.
  // Henüz ölçüm yokken (liste kuruluyor) seçim ilk adımda kalır.
  if (state.current.Observations.length > 0) {
    if (steps().length > 1) state.resume = state.stepIndex;
    state.stepIndex = steps().length - 1;
  }
  await saveSteps();
  if (state.ready && state.anchor === null) setStatus(t("status.ready"));
}
on("add-step", () => addStep("new-step", "new-step-class"));
on("setup-add-step", async () => { await addStep("setup-new-step", "setup-new-step-class"); $("setup-new-step").focus(); });
// Yeni adım kutusunda Enter adımı ekler (kısayollar metin kutusunda çalışmaz).
$("new-step").addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); guard(() => addStep("new-step", "new-step-class", { quiet: true })); } });
$("setup-new-step").addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); guard(() => addStep("setup-new-step", "setup-new-step-class", { quiet: true })); } });
on("edit-steps", () => selectTab(TAB.setup));
on("go-measure-setup", () => selectTab(TAB.measure));

function moveStep(i, delta) {
  const list = steps(), j = i + delta;
  if (j < 0 || j >= list.length) return;
  [list[i], list[j]] = [list[j], list[i]];
  if (state.stepIndex === i) state.stepIndex = j; else if (state.stepIndex === j) state.stepIndex = i;
  return saveSteps();
}

// ---- Hazırlık: adım tablosu (v1.9; ölçüm ekranındaki adım araçlarının yerini alır)

const selectOf = (values, value, label) => {
  const sel = document.createElement("select");
  sel.append(new Option(t("class.none"), ""), ...values.map((v) => new Option(label(v), v)));
  sel.value = value ?? "";
  return sel;
};
function renderStepTable() {
  const body = $("step-table").tBodies[0];
  body.replaceChildren();
  if (!state.current || isSampling()) return;
  const list = steps();
  if (!list.length) {
    const tr = body.insertRow(); tr.className = "empty";
    const td = tr.insertCell(); td.colSpan = $("step-table").tHead.rows[0].cells.length; td.textContent = t("steps.empty");
    return;
  }
  const counts = new Map();
  for (const o of state.current.Observations) counts.set(o.Step, (counts.get(o.Step) ?? 0) + 1);
  list.forEach((st, i) => {
    const tr = body.insertRow();
    const cell = (el, cls = "") => { const td = tr.insertCell(); if (cls) td.className = cls; td.append(el); return el; };
    cell(document.createTextNode(String(i + 1)), "num");
    // Kaydetme adım nesnelerini yeniler (cleanSteps); bu yüzden adım her seferinde sırasından okunur.
    const cur = () => steps()[i];
    const name = cell(Object.assign(document.createElement("input"), { type: "text", value: st.Name, maxLength: 80, autocomplete: "off" }));
    name.setAttribute("aria-label", `${t("th.r.step")} ${i + 1}`);
    name.addEventListener("change", () => guard(() => renameStep(cur(), name)));
    const cls = cell(selectOf(["VA", "SVA", "NVA"], st.Classification, (v) => v));
    cls.setAttribute("aria-label", `${t("field.class")} · ${st.Name}`);
    cls.addEventListener("change", () => guard(() => setStepClass(cur(), cls.value)));
    const field = (el, key, clean, cssClass, label) => {
      cell(el, cssClass);
      el.setAttribute("aria-label", `${label} · ${st.Name}`);
      el.addEventListener("change", () => guard(async () => {
        const v = clean(el.value), target = cur();
        if (v) target[key] = v; else delete target[key];
        await saveSteps({ table: false });
      }));
    };
    field(selectOf(WORK_TYPES, st.Type, (v) => t("type." + v)), "Type", (v) => (WORK_TYPES.includes(v) ? v : ""), "mod-Work", t("field.workType"));
    // Örnek metin yalnız ilk satırda (her satırda tekrar etmesin).
    field(Object.assign(document.createElement("input"), { type: "text", value: st.Note ?? "", maxLength: 200, autocomplete: "off", placeholder: i === 0 ? t("placeholder.stepNote") : "" }),
      "Note", (v) => v.trim().slice(0, 200), "", t("field.stepNote"));
    field(selectOf(WASTES, st.Waste, (v) => t("waste." + v)), "Waste", (v) => (WASTES.includes(v) ? v : ""), "mod-Ideas", t("field.waste"));
    field(Object.assign(document.createElement("input"), { type: "text", value: st.Idea ?? "", maxLength: 200, autocomplete: "off", placeholder: i === 0 ? t("placeholder.idea") : "" }),
      "Idea", (v) => v.trim().slice(0, 200), "mod-Ideas", t("field.idea"));
    cell(document.createTextNode(String(counts.get(st.Name) ?? 0)), "num");
    const actions = document.createElement("span");
    actions.className = "row-actions";
    for (const [text, label, action, disabled, danger] of [
      ["▲", t("setup.up"), () => moveStep(i, -1), i === 0, false],
      ["▼", t("setup.down"), () => moveStep(i, 1), i === list.length - 1, false],
      ["✕", t("setup.remove"), () => removeStep(i), false, true]]) {
      const b = Object.assign(document.createElement("button"), { type: "button", textContent: text, disabled, title: label });
      b.setAttribute("aria-label", `${label} · ${st.Name}`);
      if (danger) b.className = "danger";
      b.addEventListener("click", () => guard(action));
      actions.append(b);
    }
    cell(actions);
  });
}

/** Ad değişikliği kayıtlara, sıklığa ve adım temposuna da yansır. */
async function renameStep(st, input) {
  const next = input.value.trim();
  if (!next || next === st.Name) { input.value = st.Name; return; }
  if (steps().some((x) => x !== st && x.Name === next)) { input.value = st.Name; throw new Error(t("msg.stepExists", { step: next })); }
  const old = st.Name;
  st.Name = next;
  for (const o of state.current.Observations) if (o.Step === old) o.Step = next;
  for (const map of [state.current.Frequency, state.current.StepPace])
    if (map.has(old)) { map.set(next, map.get(old)); map.delete(old); }
  refreshObservations();
  await saveSteps();
}
async function setStepClass(target, value) {
  target.Classification = value;
  const name = target.Name;
  await saveSteps({ table: false });
  const st = steps().find((x) => x.Name === name);
  const others = state.current.Observations.filter((o) => o.Step === st.Name && o.Classification !== st.Classification);
  if (others.length && (await ask(t("msg.stepClassExisting", { n: others.length, step: st.Name, cls: st.Classification || "—" }), t("title.stepClass")))) {
    for (const o of others) o.Classification = st.Classification;
    refreshObservations();
    await save();
  }
}
async function removeStep(i) {
  const st = steps()[i];
  const used = state.current.Observations.filter((o) => o.Step === st.Name).length;
  if (!(await ask(used ? t("msg.stepRemoveUsed", { step: st.Name, n: used }) : t("msg.stepRemove", { step: st.Name }), t("title.stepRemove")))) return;
  steps().splice(i, 1);
  if (state.stepIndex > i) state.stepIndex--;
  await saveSteps();
}

on("step-frame", async () => {
  requireVideo();
  const st = currentStep();
  if (!st) throw new Error(t("msg.noSteps"));
  st.FrameMs = positionMs();
  await saveSteps({ table: false });
  setStatus(t("status.frameSet", { step: st.Name, at: fmtClock(st.FrameMs) }));
});

// ---- Hazırlık: kapsam (modüller)

for (const box of document.querySelectorAll("#modules input"))
  box.addEventListener("change", () => guard(async () => {
    if (!state.current) return;
    state.current.Modules = cleanModules([...document.querySelectorAll("#modules input:checked")].map((x) => x.value));
    applyModules();
    await save();
  }));

// ---- Hazırlık: adımları başka etütten al

function copyPreview() {
  const src = state.studies.find((s) => s.Id === $("copy-source").value);
  const list = src ? copySteps(src) : [];
  $("copy-preview").textContent = list.length ? t("copy.preview", { n: list.length, names: list.map((x) => x.Name).join(" · ") }) : "";
}
$("copy-source").addEventListener("change", copyPreview);
on("copy-steps", async () => {
  if (!state.current) return;
  if (!fillStepSources($("copy-source"), state.current.Id, false).length) { await showDialog(t("copy.none")); return; }
  copyPreview();
  dialogOpen = true;
  const dlg = $("copy-dialog");
  dlg.returnValue = "";
  dlg.showModal();
  await new Promise((resolve) => dlg.addEventListener("close", resolve, { once: true }));
  dialogOpen = false;
  if (dlg.returnValue !== "yes") return;
  const src = state.studies.find((s) => s.Id === $("copy-source").value);
  if (!src) return;
  const copied = copySteps(src);
  const replace = document.querySelector('input[name="copy-mode"]:checked')?.value === "replace";
  const measured = new Set(state.current.Observations.map((o) => o.Step));
  const current = steps();
  state.current.Steps = replace
    ? [...copied, ...current.filter((x) => measured.has(x.Name) && !copied.some((c) => c.Name === x.Name))]
    : [...current, ...copied.filter((c) => !current.some((x) => x.Name === c.Name))];
  state.stepIndex = 0;
  await saveSteps();
  setStatus(t("copy.done", { n: copied.length, source: `${src.Code} · ${src.Name}` }));
});

// ---- çevrim

function setCycle(n) {
  $("cycle").value = String(Math.max(1, n));
  updateNow();
}
const cycleValue = () => parsePositiveInt($("cycle").value, t("name.cycle"));
on("cycle-up", () => setCycle(cycleValue() + 1));
on("cycle-down", () => setCycle(cycleValue() - 1));
$("cycle").addEventListener("input", updateNow);
$("excluded").addEventListener("change", updateNow);

// ---- ölçüm

async function measure() {
  requireVideo();
  if (!currentStep()) throw new Error(t("msg.noSteps"));
  const now = positionMs();
  if (state.anchor === null) {
    state.anchor = now;
    $("measure").textContent = t("btn.measure");
    $("measure").classList.add("on");
    setStatus(t("status.started", { s: fmt3(now / 1000) }));
    updateLiveSegment(now);
    return;
  }
  if (now <= state.anchor) throw new Error(t("msg.behindStart", { s: fmt3(state.anchor / 1000) }));
  // Kayıt eşzamanlı eklenir, çapa hemen ilerler; diske yazma arkadan gelir.
  const excluded = $("excluded").checked;
  const saving = addObservation(state.anchor, now);
  state.anchor = now;
  afterRecord(excluded);
  await saving;
}

/**
 * Kayıttan sonra sıra: kapsam dışı kayıt (ör. araya giren bir bekleme) sırayı ilerletmez; elle başka adıma
 * geçilmişse önceki adıma döner. Normal kayıtta otomatik ilerleme açıksa sıradaki adıma geçilir.
 */
function afterRecord(excluded) {
  if (excluded) {
    if (state.resume !== null && state.resume < steps().length) state.stepIndex = state.resume;
    state.resume = null;
    renderSteps();
    return;
  }
  state.resume = null;
  if ($("auto-advance").checked) advance();
}

/** Sıradaki adıma geçer; son adımdan sonra ilk adıma döner ve çevrimi artırır. */
function advance() {
  if (state.stepIndex + 1 < steps().length) state.stepIndex++;
  else { state.stepIndex = 0; setCycle(cycleValue() + 1); }
  renderSteps();
}

function markStart() {
  requireVideo();
  state.selectedStart = positionMs();
  setStatus(t("status.markStart", { s: fmt3(state.selectedStart / 1000) }));
}

async function addMarked() {
  requireVideo();
  if (state.selectedStart === null) throw new Error(t("msg.markFirst"));
  if (!currentStep()) throw new Error(t("msg.noSteps"));
  const saving = addObservation(state.selectedStart, positionMs());
  state.selectedStart = null;
  await saving;
}

/** Doğrulama ve ekleme eşzamanlıdır (hata varsa hiçbir şey değişmez); kayıt sözü döner. */
function addObservation(start, end) {
  if (end <= start) throw new Error(t("msg.endAfterStart"));
  const st = currentStep();
  const cycle = cycleValue();
  const rating = readRating($("now-rating").value);
  const row = newObservation({ StartMs: start, EndMs: end, Step: st.Name, Classification: st.Classification, Excluded: $("excluded").checked, Cycle: cycle,
    ...(rating !== null ? { Rating: rating } : {}) });
  if (state.current.Observations.some((x) => x.StartMs === start && x.EndMs === end && x.Cycle === row.Cycle && x.Step === row.Step && x.Excluded === row.Excluded))
    throw new Error(t("msg.duplicate"));
  state.current.Observations.push(row);
  state.undo.push({ row, stepIndex: state.stepIndex, cycle });
  // "Kapsam dışı" yalnız bir kayıt içindir.
  $("excluded").checked = false;
  refreshObservations();
  updateNow();
  setStatus(t(row.Excluded ? "status.savedExcluded" : "status.saved", { step: row.Step, s: fmt3(seconds(row)), cycle }));
  return save();
}

/** Son eklenen kaydı siler; adım, çevrim ve ölçüm başlangıcı o kaydın öncesine döner. */
async function undoLast() {
  if (!state.current) return;
  let last;
  while ((last = state.undo.pop()) && !state.current.Observations.includes(last.row)) { /* silinmiş kayıtları atla */ }
  if (!last) { setStatus(t("status.nothingToUndo")); return; }
  state.current.Observations.splice(state.current.Observations.indexOf(last.row), 1);
  if (state.selectedRow === last.row) selectRow(null);
  state.stepIndex = last.stepIndex;
  setCycle(last.cycle);
  if (state.anchor !== null && state.anchor === last.row.EndMs) state.anchor = last.row.StartMs;
  renderSteps();
  refreshObservations();
  setStatus(t("status.undone", { step: last.row.Step, s: fmt3(seconds(last.row)) }));
  await save();
}

on("measure", measure);
on("stop", () => { if (state.anchor !== null) { stopMeasure(); setStatus(t("status.stopped")); } });
on("undo", undoLast);
on("mark-start", markStart);
on("add-marked", addMarked);

// ---- seçili kayıt

function selectRow(row) {
  state.selectedRow = row;
  $("row-editor").hidden = !row;
  if (row) {
    $("row-step").value = row.Step;
    $("row-cycle").value = String(row.Cycle);
    $("row-class").value = row.Classification;
    $("row-excluded").checked = row.Excluded;
    $("row-note").value = row.Note ?? "";
    $("row-rating").value = cleanRating(row.Rating) === null ? "" : fmtNum(row.Rating);
  }
}

on("edit-row", async () => {
  const row = state.selectedRow;
  if (!row) return;
  const name = $("row-step").value.trim();
  if (!name) throw new Error(t("msg.stepEmpty"));
  row.Cycle = parsePositiveInt($("row-cycle").value, t("name.cycle"));
  row.Step = name;
  row.Classification = $("row-class").value;
  row.Excluded = $("row-excluded").checked;
  const note = $("row-note").value.trim().slice(0, 200);
  if (note) row.Note = note; else delete row.Note;
  const rating = readRating($("row-rating").value);
  if (rating !== null) row.Rating = rating; else delete row.Rating;
  if (!steps().some((x) => x.Name === name)) { steps().push({ Name: name, Classification: row.Classification }); renderSteps(); }
  refreshObservations();
  setStatus(t("status.rowUpdated", { step: row.Step }));
  await save();
});

// ---- v1.9: toplu düzenleme (işaretli kayıtlar)

function updateBulk() {
  for (const o of [...state.checked]) if (!state.current?.Observations.includes(o)) state.checked.delete(o);
  const n = state.checked.size;
  $("bulk-editor").hidden = n === 0;
  $("bulk-title").textContent = t("bulk.title", { n });
  const visible = state.visibleRows;
  $("obs-all").checked = visible.length > 0 && visible.every((o) => state.checked.has(o));
  $("obs-all").indeterminate = n > 0 && !$("obs-all").checked;
}
$("obs-all").addEventListener("change", () => {
  for (const o of state.visibleRows) if ($("obs-all").checked) state.checked.add(o); else state.checked.delete(o);
  refreshObservations();
});
function clearBulkFields() {
  $("bulk-step").value = ""; $("bulk-cycle").value = ""; $("bulk-rating").value = "";
  $("bulk-class").value = "keep"; $("bulk-excluded").value = "keep";
}
on("bulk-apply", async () => {
  const rows = [...state.checked];
  if (!rows.length) return;
  const name = $("bulk-step").value.trim(), cycleText = $("bulk-cycle").value.trim(), ratingText = $("bulk-rating").value.trim();
  const cls = $("bulk-class").value, ex = $("bulk-excluded").value;
  const cycle = cycleText ? parsePositiveInt(cycleText, t("name.cycle")) : null;
  const rating = ratingText ? readRating(ratingText) : null;
  if (!name && cycle === null && rating === null && cls === "keep" && ex === "keep") throw new Error(t("bulk.nothing"));
  for (const o of rows) {
    if (name) o.Step = name;
    if (cycle !== null) o.Cycle = cycle;
    if (cls !== "keep") o.Classification = cls;
    if (ex !== "keep") o.Excluded = ex === "yes";
    if (rating !== null) o.Rating = rating;
  }
  if (name && !steps().some((x) => x.Name === name)) { steps().push({ Name: name, Classification: cls === "keep" ? "" : cls }); state.current.Steps = cleanSteps(steps()); renderSteps(); }
  clearBulkFields();
  refreshObservations();
  setStatus(t("bulk.done", { n: rows.length }));
  await save();
});
on("bulk-delete", async () => {
  const rows = [...state.checked];
  if (!rows.length) return;
  if (!(await ask(t("bulk.deleteConfirm", { n: rows.length }), t("title.deleteRow")))) return;
  state.current.Observations = state.current.Observations.filter((o) => !state.checked.has(o));
  if (state.checked.has(state.selectedRow)) selectRow(null);
  state.checked.clear();
  refreshObservations();
  setStatus(t("bulk.deleted", { n: rows.length }));
  await save();
});
on("bulk-clear", () => { state.checked.clear(); refreshObservations(); });

on("delete-row", async () => {
  const row = state.selectedRow;
  if (!row || !state.current) return;
  if (!(await ask(t("msg.deleteRowConfirm"), t("title.deleteRow")))) return;
  state.current.Observations.splice(state.current.Observations.indexOf(row), 1);
  selectRow(null);
  refreshObservations();
  await save();
});
on("row-close", () => { selectRow(null); refreshObservations(); });

/** Kayıt tablosu süzgeci: adım ve çevrim (yalnız görünümü daraltır, hesaplara etkisi yok). */
function fillFilter(id, values, label) {
  const sel = $(id), keep = sel.value;
  sel.replaceChildren(new Option(t("filter.all"), ""), ...values.map((v) => new Option(label(v), String(v))));
  sel.value = values.map(String).includes(keep) ? keep : "";
}
function refreshObservations() {
  const all = state.current ? [...state.current.Observations].sort((a, b) => a.StartMs - b.StartMs || a.EndMs - b.EndMs) : [];
  fillFilter("filter-step", state.current ? stepOrder(state.current).filter((n) => all.some((o) => o.Step === n)) : [], (v) => v);
  fillFilter("filter-cycle", [...new Set(all.map((o) => o.Cycle))].sort((a, b) => a - b), (v) => String(v));
  const fs = $("filter-step").value, fc = $("filter-cycle").value;
  const rows = all.filter((o) => (!fs || o.Step === fs) && (!fc || String(o.Cycle) === fc));
  // İşaret yalnız görünen kayıtlarda kalır (süzgeç değişince gizlenen kayıt yanlışlıkla değişmesin).
  for (const o of [...state.checked]) if (!rows.includes(o)) state.checked.delete(o);
  state.visibleRows = rows;
  $("obs-count").textContent = all.length ? t(rows.length === all.length ? "filter.countAll" : "filter.count", { n: rows.length, total: all.length }) : "";
  renderTable("observations", rows,
    (r) => [{ check: true, checked: state.checked.has(r), label: `${t("bulk.row")} · ${r.Step} · ${fmtTime(r.StartMs)}`,
      onCheck: (v) => { if (v) state.checked.add(r); else state.checked.delete(r); updateBulk(); } },
      num(fmtTime(r.StartMs)), num(fmtTime(r.EndMs)), num(fmt3(seconds(r))), txt(r.Step), txt(r.Classification), txt(r.Excluded ? t("yes") : t("no")), num(r.Cycle), num(cleanRating(r.Rating) === null ? "" : fmtNum(r.Rating)), txt(r.Note ?? "")],
    {
      isSelected: (r) => r === state.selectedRow,
      onSelect: (r) => {
        selectRow(r);
        // Kaydın başına gider; süren ölçüm kesilmez.
        if (state.ready) seek(r.StartMs);
        refreshObservations();
      },
      empty: t("obs.empty")
    });
  updateBulk();
  renderSegments();
  renderSteps();
}

// ---- zaman çizgisindeki ölçülen aralıklar

function renderSegments() {
  const box = $("segments");
  box.replaceChildren();
  const total = Number(timeline.max);
  if (!state.current || !state.ready || !(total > 0)) { updateLiveSegment(); return; }
  // İş örneklemesi: gözlem anları ince işaret.
  for (const x of isSampling() ? state.current.Samples : []) {
    const tick = document.createElement("span");
    tick.className = "seg tick";
    tick.style.left = (x.AtMs / total) * 100 + "%";
    tick.style.setProperty("--cls", CLASS_COLORS[categoryClass(x.Category)] ?? CLASS_COLORS[""]);
    box.append(tick);
  }
  for (const o of state.current.Observations) {
    const seg = document.createElement("span");
    seg.className = "seg" + (o.Excluded ? " excluded" : "") + (o === state.selectedRow ? " selected" : "");
    seg.style.left = (o.StartMs / total) * 100 + "%";
    seg.style.width = Math.max(0.3, ((o.EndMs - o.StartMs) / total) * 100) + "%";
    seg.style.setProperty("--cls", CLASS_COLORS[o.Classification] ?? CLASS_COLORS[""]);
    box.append(seg);
  }
  const legend = $("segment-legend");
  const used = new Set(state.current.Observations.map((o) => (o.Excluded ? "ex" : o.Classification)));
  legend.replaceChildren(...["VA", "SVA", "NVA", "", "ex"].filter((c) => used.has(c)).map((c) => {
    const span = document.createElement("span");
    if (c === "ex") { span.className = "ex"; span.textContent = t("th.excluded"); }
    else { span.style.setProperty("--c", CLASS_COLORS[c]); span.textContent = c || t("summary.noClass"); }
    return span;
  }));
  const live = document.createElement("span");
  live.id = "seg-live";
  live.className = "seg live";
  box.append(live);
  updateLiveSegment();
}

function updateLiveSegment(now = state.ready ? positionMs() : 0) {
  const live = $("seg-live");
  if (!live) return;
  const total = Number(timeline.max);
  if (state.anchor === null || !(total > 0)) { live.hidden = true; return; }
  const a = Math.min(state.anchor, now), b = Math.max(state.anchor, now);
  live.hidden = false;
  live.classList.toggle("behind", now <= state.anchor);
  live.style.left = (a / total) * 100 + "%";
  live.style.width = Math.max(0.3, ((b - a) / total) * 100) + "%";
}

/** Kayıtlar sırayla yazılır; hızlı ardışık Ölç işlemlerinde sıra korunur. */
async function save() {
  if (!state.current) return;
  state.current.Updated = new Date().toISOString();
  const snapshot = state.current;
  state.saving = state.saving.catch(() => {}).then(() => store.save(snapshot));
  await state.saving;
  await reloadStudies();
}

// ------------------------------------------------------------ kısayollar (K10)

const EDITABLE = new Set(["text", "search", "number", "email", "url", "tel", "password", ""]);
function isEditable(el) {
  if (!el) return false;
  if (el.isContentEditable || el.tagName === "TEXTAREA" || el.tagName === "SELECT") return true;
  return el.tagName === "INPUT" && EDITABLE.has((el.getAttribute("type") ?? "").toLowerCase());
}

window.addEventListener("keydown", (e) => {
  if (activeTab !== TAB.measure || !state.current || dialogOpen || e.altKey) return;
  if (e.metaKey && e.key.toLowerCase() !== "z") return;
  if (isEditable(document.activeElement)) return;
  const step = e.shiftKey ? 1000 : e.ctrlKey ? 40 : 100;
  let action;
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") action = isSampling() ? undoSample : undoLast;
  else if (!e.ctrlKey && /^[1-9]$/.test(e.key)) { const i = Number(e.key) - 1; action = isSampling() ? () => recordSample(i) : () => selectStep(i); }
  else if (isSampling() && e.key === "Enter") action = goRandomMoment;
  else switch (e.key) {
    case " ": action = () => (video.paused ? play() : pause()); break;
    case "Enter": action = measure; break;
    case "Escape":
      action = () => { if (state.anchor !== null) { stopMeasure(); setStatus(t("status.stopped")); } };
      break;
    case "x": case "X": action = () => { $("excluded").checked = !$("excluded").checked; updateNow(); }; break;
    case "+": action = () => setCycle(cycleValue() + 1); break;
    case "-": action = () => setCycle(cycleValue() - 1); break;
    case "ArrowLeft": action = () => nudge(-step); break;
    case "ArrowRight": action = () => nudge(step); break;
    default: return;
  }
  e.preventDefault();
  e.stopPropagation();
  // Odaktaki düğmenin aynı tuşla ayrıca tetiklenmesini önle.
  if (document.activeElement instanceof HTMLButtonElement || document.activeElement?.type === "checkbox" || document.activeElement === timeline)
    document.activeElement.blur();
  if (!e.repeat || e.key.startsWith("Arrow")) guard(action);
}, true);

// ------------------------------------------------------------ analiz

function loadSettings() {
  const s = state.current?.Settings;
  if (!s) return;
  $("takt").value = state.current.Takt === null || state.current.Takt === undefined ? "" : fmtNum(state.current.Takt);
  $("sample-error").value = state.current.SampleError === null || state.current.SampleError === undefined ? "" : fmtNum(state.current.SampleError);
  $("confidence").value = fmtNum(s.Confidence);
  $("target").value = fmtNum(s.TargetError);
  $("pace").value = fmtNum(s.Pace);
  $("allowance").value = fmtNum(s.Allowance);
  const parts = state.current.AllowanceParts ?? {};
  for (const k of ALLOWANCE_PARTS) $("al-" + k).value = parts[k] === undefined ? "" : fmtNum(parts[k]);
  $("allowance-parts").open = Object.keys(parts).length > 0;
  syncAllowanceField();
  $("group-ms").value = String(s.GroupMs);
  $("method").value = s.RepeatedLow ? "1" : "0";
  $("stable").checked = s.Stable;
  updateSettingsSummary();
}
/** Katlanmış ayar kutusunun başlığı: geçerli ayarların tek satırlık özeti. */
function updateSettingsSummary() {
  const st = state.current;
  if (!st) return;
  const s = st.Settings;
  $("settings-summary").textContent = isSampling()
    ? t("set.summarySampling", { conf: fmtNum(s.Confidence), err: st.SampleError === null || st.SampleError === undefined ? t("set.auto") : fmtNum(st.SampleError) })
    : t("set.summary", { conf: fmtNum(s.Confidence), target: fmtNum(s.TargetError), pace: fmtNum(s.Pace), al: fmtNum(s.Allowance),
      takt: st.Takt ? t("set.takt", { takt: fmtNum(st.Takt) }) : "" });
}

/** Ayarlar alan değiştikçe uygulanır; geçersiz değer ekranda gösterilir, önceki geçerli ayar kalır. */
async function applySettings() {
  if (!state.current) return;
  $("settings-error").hidden = true;
  const settings = {
    Confidence: parseNumber($("confidence").value, t("name.confidence")), TargetError: parseNumber($("target").value, t("name.target")),
    Pace: parseNumber($("pace").value, t("name.pace")), Allowance: parseNumber($("allowance").value, t("name.allowance")),
    RepeatedLow: $("method").value === "1", GroupMs: parsePositiveInt($("group-ms").value, t("name.group")),
    Stable: $("stable").checked
  };
  // Kalem girildiyse toplam tolerans kalemlerden gelir.
  const parts = {};
  for (const k of ALLOWANCE_PARTS) {
    const text = $("al-" + k).value.trim();
    if (text === "") continue;
    const v = parseNumber(text, t("al." + k));
    if (v < 0 || v >= 100) throw new Error(t("msg.partInvalid", { name: t("al." + k) }));
    if (v > 0) parts[k] = v;
  }
  if (Object.keys(parts).length) settings.Allowance = allowanceSum(parts);
  validateSettings(settings);
  const taktText = $("takt").value.trim();
  const takt = taktText === "" ? null : cleanTakt(parseNumber(taktText, t("name.takt")));
  if (taktText !== "" && takt === null) throw new Error(t("msg.taktInvalid"));
  const errText = $("sample-error").value.trim();
  const sampleError = errText === "" ? null : cleanSampleError(parseNumber(errText, t("field.sampleError")));
  if (errText !== "" && sampleError === null) throw new Error(t("msg.sampleErrorInvalid"));
  state.current.Settings = settings;
  state.current.Takt = takt;
  state.current.SampleError = sampleError;
  state.current.AllowanceParts = cleanAllowanceParts(parts);
  $("allowance").value = fmtNum(settings.Allowance);
  syncAllowanceField();
  updateSettingsSummary();
  await save();
  refreshAnalysis();
}
async function applySettingsInline() {
  try { await applySettings(); }
  catch (error) { $("settings-error").textContent = errorText(error); $("settings-error").hidden = false; $("settings-box").open = true; }
}
for (const id of ["confidence", "target", "pace", "allowance", "group-ms", "method", "stable", "takt", "sample-error", ...ALLOWANCE_PARTS.map((k) => "al-" + k)])
  $(id).addEventListener("change", applySettingsInline);

/** Kalemler doluysa toplam alanı salt okunur olur ve toplamı gösterir. */
function syncAllowanceField() {
  const used = ALLOWANCE_PARTS.some((k) => $("al-" + k).value.trim() !== "");
  $("allowance").readOnly = used;
  $("allowance").title = used ? t("al.sumHint") : "";
  const sum = ALLOWANCE_PARTS.reduce((acc, k) => { const v = Number($("al-" + k).value.trim().replace(",", ".")); return acc + (Number.isFinite(v) ? v : 0); }, 0);
  $("al-sum").textContent = used ? t("al.sum", { sum: fmtNum(Math.round(sum * 1e6) / 1e6) }) : t("al.none");
}
for (const k of ALLOWANCE_PARTS) $("al-" + k).addEventListener("input", syncAllowanceField);
for (const id of ["filter-step", "filter-cycle"]) $(id).addEventListener("change", () => refreshObservations());

function refreshAnalysis() {
  const study = state.current;
  if (!study) return;
  $("sampling-body").hidden = !isSampling();
  if (isSampling()) { $("analysis-body").hidden = true; $("analysis-empty").hidden = true; refreshSampling(); return; }
  const previous = state.selectedResult;
  const rows = inProcessOrder(study, analyzeSteps(study));
  const selected = rows.find((r) => r.Step === previous) ?? rows[0] ?? null;
  state.selectedResult = selected?.Step ?? null;
  const empty = study.Observations.length === 0;
  $("analysis-empty").hidden = !empty;
  $("analysis-body").hidden = empty;
  $("improve-empty").hidden = !empty;
  $("improve-body").hidden = empty;
  $("sel-step").textContent = selected?.Step ?? "—";
  $("apply-step").disabled = !selected;
  renderTable("results", rows,
    (r) => [txt(r.Step), num(r.Count), num(r.Excluded), num(fmt3(r.Mean)), num(fmt3(r.Median)), num(fmt3(r.Min)), num(fmt3(r.Max)),
      num(fmt3(r.Deviation)), num(fmt3(r.CriticalT)), num(fmt3(r.ErrorPercent)), txt(result(r.Sufficiency)), num(fmt3(r.Base)),
      num(fmtNum(r.Pace) + (study.StepPace.has(r.Step) ? " *" : "")), num(fmt3(r.Normal)), num(fmt3(r.Standard)), num(r.Every), num(fmt3(r.Contribution)), txt(result(r.Method))],
    {
      isSelected: (r) => r.Step === state.selectedResult,
      onSelect: (r) => { state.selectedResult = r.Step; fillStepSettings(r); refreshAnalysis(); },
      // Çift tıklama: adımın kayıtları Ölçüm sekmesinde süzülmüş açılır.
      onOpen: (r) => { selectTab(TAB.measure); refreshObservations(); $("filter-step").value = r.Step; state.checked.clear(); refreshObservations(); },
      empty: t("analysis.empty")
    });
  if (selected && previous !== selected.Step) fillStepSettings(selected);
  const spread = cycleSpread(analyzeCycles(study));
  renderTable("cycles", spread.Rows, (c) => [num(c.Cycle), num(fmt3(c.Elapsed)), num(fmt3(c.Workload)), num((c.Outlier ? "⚠ " : "") + pct(c.DiffPercent))]);
  renderCharts(study, rows, spread);
  renderTable("groups", selected?.Groups ?? [], (g) => [num(fmt3(g.Seconds)), num(g.Count)], { empty: t("groups.empty") });
  renderSummary(study, rows);
  renderAdvanced(study, rows, selected);
  const notes = [`${t(study.Settings.Stable ? "guide.stable" : "guide.general")} ${t("guide.tail")}`];
  if (study.Settings.Allowance === 0 && rows.length) notes.push(t("guide.noAllowance"));
  $("analysis-status").textContent = notes.join(" ");
}

function fillStepSettings(r) {
  $("every").value = String(r.Every);
  $("step-pace").value = state.current.StepPace.has(r.Step) ? fmtNum(state.current.StepPace.get(r.Step)) : "";
}

/** Hedef hataya inmek için gereken yaklaşık gözlem sayısı: t değeri serbestlik derecesine göre yinelemeli güncellenir. */

/** Analizin özeti: çevrim başına standart süre, saatlik kapasite, yeterlilik ve sınıf dağılımı. */
function renderSummary(study, rows) {
  const box = $("summary");
  box.replaceChildren();
  if (rows.length === 0) return;
  const card = (label, value, detail = []) => {
    const el = document.createElement("div");
    el.className = "sum-card";
    const l = document.createElement("span"); l.className = "sum-label"; l.textContent = label;
    const v = document.createElement("strong"); v.textContent = value;
    el.append(l, v);
    for (const d of detail) { const p = document.createElement("small"); p.textContent = d; el.append(p); }
    box.append(el);
  };
  const { Total: total, Incomplete: missing } = totalStandard(rows);
  const parts = study.AllowanceParts ?? {};
  const basis = t("summary.basis", { pace: pct(study.Settings.Pace).replace(/^[+]/, ""), al: pct(study.Settings.Allowance, 1).replace(/^[+]/, "") }) +
    (Object.keys(parts).length ? " (" + ALLOWANCE_PARTS.filter((k) => parts[k] !== undefined).map((k) => `${t("al." + k).replace(/ ?%$/, "")} ${fmtNum(parts[k])}`).join(" + ") + ")" : "");
  card(t("summary.total"), total !== null ? `${fmt3(total)} ${getLang() === "tr" ? "sn" : "s"}` : "—", [...(missing ? [t("summary.incomplete")] : []), basis]);
  card(t("summary.rate"), total > 0 ? t("summary.rateUnit", { n: (3600 / total).toLocaleString(locale(), { maximumFractionDigits: 1 }) }) : "—");
  if (study.Takt && total > 0) {
    const load = (total / study.Takt) * 100;
    card(t("summary.takt"), t(load <= 100 ? "summary.taktOk" : "summary.taktOver", { p: pct(load, 0).replace(/^[+]/, "") }),
      [t("summary.taktDetail", { std: fmt3(total), takt: fmt3(study.Takt) }), t("summary.operators", { n: (total / study.Takt).toLocaleString(locale(), { maximumFractionDigits: 2 }) })]);
  }
  const settings = study.Settings;
  const bad = rows.filter((r) => r.Sufficiency === "Yetersiz");
  const na = rows.filter((r) => r.Sufficiency === "Hesaplanamıyor" && r.Count > 0);
  const details = bad.map((r) => t("summary.needDetail", {
    step: r.Step, c: r.Count,
    n: requiredObservations(r, settings)
  }));
  if (na.length) details.push(t("summary.na", { k: na.length }));
  // Hesaplanamayan adım varken "Tüm adımlar yeterli" denmez.
  card(t("summary.sufficiency"), bad.length ? t("summary.need", { k: bad.length }) : na.length ? t("summary.undecided") : t("summary.allOk"), details);
  const spread = cycleSpread(analyzeCycles(study));
  if (spread.Rows.length > 1)
    card(t("summary.cycles"), t("summary.cyclesValue", { n: spread.Rows.length, cv: pct(spread.Variation, 1).replace(/^[+]/, "") }),
      spread.Rows.filter((c) => c.Outlier).map((c) => t("summary.outlier", { c: c.Cycle, d: pct(c.DiffPercent) })));
  const byClass = new Map();
  let all = 0;
  for (const o of study.Observations) {
    if (o.Excluded || o.EndMs <= o.StartMs) continue;
    const k = o.Classification || t("summary.noClass");
    byClass.set(k, (byClass.get(k) ?? 0) + (o.EndMs - o.StartMs));
    all += o.EndMs - o.StartMs;
  }
  const outs = outliers(study).reduce((sum, g) => sum + g.Rows.length, 0);
  if (outs) card(t("summary.outliersLabel"), t("summary.outliers", { n: outs }), [t("summary.outliersDetail")]);
  if (all > 0) card(t("summary.classes"), [...byClass.entries()].sort((a, b) => b[1] - a[1]).map(([k, v]) => (getLang() === "tr" ? `${k} %${Math.round((v / all) * 100)}` : `${k} ${Math.round((v / all) * 100)}%`)).join(" · "));
}

/** Grafikler: adım katkıları (süreç sırası, sınıf rengi) ve çevrim süreleri. Tablolar aynı verinin metin görünümüdür. */
function renderCharts(study, rows, spread) {
  const box = $("charts");
  box.replaceChildren();
  // Grafik genişliği kutuya göre: telefonda yazılar küçülmesin.
  const width = Math.max(300, Math.min(640, (box.clientWidth || window.innerWidth) - 30));
  const unit = getLang() === "tr" ? "sn" : "s";
  const cls = new Map(study.Steps.map((x) => [x.Name, x.Classification]));
  const stepData = rows.filter((r) => r.Contribution !== null).map((r) => ({ label: r.Step, value: r.Contribution, cls: cls.get(r.Step) ?? classOf(study, r.Step), text: fmt3(r.Contribution) + " " + unit }));
  if (stepData.length) box.append(figure(t("chart.steps"), stepChart(stepData, { width, classLabel: (c) => c || t("summary.noClass") })));
  if (spread.Rows.length > 1)
    box.append(figure(t("chart.cycles"), cycleChart(spread.Rows.map((c) => ({ label: String(c.Cycle), value: c.Elapsed, outlier: c.Outlier, text: fmt3(c.Elapsed) + " " + unit })),
      { width, mean: spread.Mean, meanLabel: t("chart.mean", { v: fmt3(spread.Mean) + " " + unit }), axis: t("th.c.cycle"),
        format: (v) => v.toLocaleString(locale(), { maximumFractionDigits: v < 10 ? 1 : 0 }) })));
}
function classOf(study, step) {
  const counts = new Map();
  for (const o of study.Observations) if (o.Step === step && o.Classification) counts.set(o.Classification, (counts.get(o.Classification) ?? 0) + 1);
  return [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? "";
}
function figure(title, svg) {
  const f = document.createElement("figure");
  f.className = "chart";
  const c = document.createElement("figcaption");
  c.textContent = title;
  f.append(c, svg);
  return f;
}

// ------------------------------------------------------------ etüt bilgileri

function renderInfoFields(boxId, info = {}) {
  const box = $(boxId);
  box.replaceChildren();
  for (const k of INFO_FIELDS) {
    const label = document.createElement("label");
    label.className = "field" + (k === "Note" ? " grow" : "");
    const span = document.createElement("span");
    span.textContent = t("info." + k);
    const input = document.createElement("input");
    input.type = k === "Date" ? "date" : "text";
    input.id = `${boxId}-${k}`;
    input.autocomplete = "off";
    input.maxLength = 200;
    input.value = info[k] ?? "";
    if (boxId === "info-fields") input.addEventListener("change", () => guard(saveInfo));
    label.append(span, input);
    box.append(label);
  }
}
const readInfo = (boxId) => cleanInfo(Object.fromEntries(INFO_FIELDS.map((k) => [k, $(`${boxId}-${k}`)?.value ?? ""])));
async function saveInfo() {
  if (!state.current) return;
  state.current.Info = readInfo("info-fields");
  await save();
}

// ------------------------------------------------------------ v1.9: etüt durumu ve onay

function loadStatus() {
  const s = state.current;
  if (!s) return;
  $("study-status").value = s.Status || "";
  $("approved-by").value = s.ApprovedBy || "";
  $("approved-at").value = s.ApprovedAt ? s.ApprovedAt.slice(0, 10) : "";
  const approved = s.Status === "approved";
  for (const el of document.querySelectorAll(".approval")) el.hidden = !approved;
  $("approved-note").hidden = !approved;
  if (approved) $("approved-note").textContent = t("approved.note", { detail: statusLabel(s) });
}
async function saveStatus() {
  const s = state.current;
  if (!s) return;
  s.Status = $("study-status").value;
  if (s.Status === "approved") {
    s.ApprovedBy = $("approved-by").value.trim().slice(0, 80);
    // Tarih boşsa bugün.
    if (!$("approved-at").value) { const d = new Date(); $("approved-at").value = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`; }
    s.ApprovedAt = $("approved-at").value;
  } else { s.ApprovedBy = ""; s.ApprovedAt = ""; }
  loadStatus();
  await save();
  await reloadStudies();
}
for (const id of ["study-status", "approved-by", "approved-at"]) on(id, saveStatus, "change");

// ------------------------------------------------------------ v1.9: Çıktı al menüsü

for (const b of document.querySelectorAll("#output-menu .menu-list button")) b.addEventListener("click", () => { $("output-menu").open = false; });
document.addEventListener("click", (e) => { if (!$("output-menu").contains(e.target)) $("output-menu").open = false; });
$("output-menu").addEventListener("keydown", (e) => { if (e.key === "Escape") { $("output-menu").open = false; $("output-menu").querySelector("summary").focus(); } });

// ------------------------------------------------------------ v1.9: ilk kullanım turu

const TOUR_KEY = "vze-tour-seen";
let tourStep = 0;
function showTourStep() {
  const n = 5, i = tourStep;
  $("tour-count").textContent = t("tour.count", { i: i + 1, n });
  $("tour-title").textContent = t(`tour.${i + 1}.title`);
  $("tour-text").textContent = t(`tour.${i + 1}.text`);
  $("tour-back").disabled = i === 0;
  $("tour-next").textContent = t(i === n - 1 ? "tour.finish" : "tour.next");
  for (let k = 0; k < TABS; k++) $(`tab-${k}`).classList.toggle("tour-focus", k === i);
}
function openTour() {
  tourStep = 0;
  showTourStep();
  dialogOpen = true;
  $("tour").showModal();
}
$("tour").addEventListener("close", () => {
  dialogOpen = false;
  for (let k = 0; k < TABS; k++) $(`tab-${k}`).classList.remove("tour-focus");
  try { localStorage.setItem(TOUR_KEY, "1"); } catch { /* gizli pencere */ }
});
$("tour-back").addEventListener("click", () => { tourStep = Math.max(0, tourStep - 1); showTourStep(); });
$("tour-next").addEventListener("click", () => { if (tourStep >= 4) $("tour").close(); else { tourStep++; showTourStep(); } });
$("tour-open").addEventListener("click", openTour);
const tourSeen = () => { try { return localStorage.getItem(TOUR_KEY) === "1" || new URL(location.href).searchParams.has("notour"); } catch { return true; } };

// ------------------------------------------------------------ önce / sonra

on("compare", async () => {
  if (state.studies.length < 2) throw new Error(t("msg.compareNeedTwo"));
  const before = selectedStudy() ?? state.studies[0];
  const fill = (id, selected) => {
    $(id).replaceChildren(...state.studies.map((s) => Object.assign(document.createElement("option"), { value: s.Id, textContent: `${s.Code} · ${s.Name}`, selected: s.Id === selected })));
  };
  fill("compare-before", before.Id);
  fill("compare-after", state.studies.find((s) => s.Id !== before.Id).Id);
  renderComparison();
  dialogOpen = true;
  $("compare-dialog").showModal();
  $("compare-dialog").addEventListener("close", () => { dialogOpen = false; }, { once: true });
});
$("compare-before").addEventListener("change", () => guard(renderComparison));
$("compare-after").addEventListener("change", () => guard(renderComparison));

function renderComparison() {
  const a = state.studies.find((s) => s.Id === $("compare-before").value), b = state.studies.find((s) => s.Id === $("compare-after").value);
  if (!a || !b) return;
  const c = compareStudies(a, b);
  const unit = getLang() === "tr" ? "sn" : "s";
  renderTable("compare-table", c.Rows, (r) => [txt(r.Step), num(fmt3(r.Before)), num(fmt3(r.After)), num(r.Diff === null ? "" : (r.Diff > 0 ? "+" : "") + fmt3(r.Diff)), num(pct(r.DiffPercent, 1))], { empty: t("analysis.empty") });
  const rate = (x) => (x ? (3600 / x).toLocaleString(locale(), { maximumFractionDigits: 1 }) : "—");
  $("compare-summary").textContent = c.Before !== null && c.After !== null
    ? t("compare.result", { before: fmt3(c.Before) + " " + unit, after: fmt3(c.After) + " " + unit, d: pct(c.DiffPercent, 1), rb: rate(c.Before), ra: rate(c.After) })
    : t("compare.incomplete");
  fillCompareSteps(a, b);
}

on("apply-step", async () => {
  if (!state.current || state.selectedResult === null) return;
  state.current.Frequency.set(state.selectedResult, parsePositiveInt($("every").value, t("name.frequency")));
  const paceText = $("step-pace").value.trim();
  if (paceText === "") state.current.StepPace.delete(state.selectedResult);
  else {
    const pace = parseNumber(paceText, t("name.stepPace"));
    if (!(pace > 0 && pace <= 300)) throw new Error(t("msg.stepPaceInvalid"));
    state.current.StepPace.set(state.selectedResult, pace);
  }
  await save();
  refreshAnalysis();
});

on("t-table", async () => {
  if (!state.current) return;
  const c = state.current.Settings.Confidence;
  const dfs = [...Array.from({ length: 30 }, (_, i) => i + 1), 40, 60, 120];
  const lines = dfs.map((df) => `${String(df).padStart(4)}    ${studentInv(df, (1 + c / 100) / 2).toLocaleString(locale(), { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`);
  await showDialog(t("tTable.formula"), { pre: t("tTable.header") + "\n" + lines.join("\n"), title: t("tTable.title", { c: fmtNum(c) }) });
});

on("go-measure", () => selectTab(TAB.measure));
on("print", async () => {
  if (!state.current) return;
  await applySettings();
  await saveInfo();
  document.body.classList.remove("print-story");
  window.print();
});
/** Yalnız adım kartları (görsel iş talimatı) yazdırılır. */
on("print-story", async () => {
  if (!state.current) return;
  await applySettings();
  await loadStory();
  document.body.classList.add("print-story");
  window.print();
});
window.addEventListener("afterprint", () => document.body.classList.remove("print-story"));
/** Yazdırma başlığı: etüt bilgileri ve analizin dayandığı ayarlar (Ctrl+P ile de dolar). */
function fillPrintHead() {
  const s = state.current;
  if (!s) return;
  const set = s.Settings, parts = s.AllowanceParts ?? {};
  const head = $("print-head");
  head.replaceChildren();
  const line = (text, strong = false) => { const p = document.createElement(strong ? "h2" : "p"); p.textContent = text; head.append(p); };
  line(`${s.Code} · ${s.Name}`, true);
  const info = INFO_FIELDS.filter((k) => s.Info?.[k]).map((k) => `${t("info." + k)}: ${s.Info[k]}`);
  if (info.length) line(info.join(" · "));
  line(t("print.settings", {
    conf: fmtNum(set.Confidence), target: fmtNum(set.TargetError), pace: fmtNum(set.Pace), al: fmtNum(set.Allowance),
    method: t(set.RepeatedLow ? "method.repeated" : "method.mean")
  }) + (Object.keys(parts).length ? " (" + ALLOWANCE_PARTS.filter((k) => parts[k] !== undefined).map((k) => `${t("al." + k).replace(/ ?%$/, "")} ${fmtNum(parts[k])}`).join(" + ") + ")" : "") +
    (s.Takt ? " · " + t("print.takt", { takt: fmtNum(s.Takt) }) : "") + (s.StepPace.size ? " · " + t("print.stepPace") : ""));
  if (s.Status) line(t("print.status", { status: t("status." + s.Status), detail: s.Status === "approved" && (s.ApprovedBy || s.ApprovedAt) ? " · " + [s.ApprovedBy, s.ApprovedAt ? fmtDay(s.ApprovedAt) : ""].filter(Boolean).join(" · ") : "" }));
  $("print-foot").textContent = t("print.foot", { code: s.Code, name: s.Name, date: fmtDate(new Date().toISOString()) }) + ` · ${t("app.title")} ${VERSION}`;
}
window.addEventListener("beforeprint", fillPrintHead);
on("excel", async () => {
  if (!state.current) return;
  await applySettings();
  await saveInfo();
  const bytes = reportBytes(state.current, new Date(), getLang());
  download(new Blob([bytes], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }), suggestedFileName(state.current, ".xlsx", t("file.analysisSuffix")));
});

// ------------------------------------------------------------ açılış

if (DESKTOP_URL) {
  $("desktop-link").href = DESKTOP_URL;
  $("desktop-link-wrap").hidden = false;
}
window.addEventListener("beforeunload", () => { for (const v of state.videos.values()) URL.revokeObjectURL(v.url); });
/** Dil değişince sabit metinler, başlık, tablolar ve analiz yeniden çizilir; seçim hatırlanır ve adrese yazılır. */
function renderLanguage() {
  applyStatic();
  updateHeader();
  $("measure").textContent = state.anchor === null ? t("btn.start") : t("btn.measure");
  renderInfoFields("new-info", readInfoSafe("new-info"));
  syncNewType();
  if (state.current) {
    loadSettings();
    renderSteps();
    renderSampling();
    renderInfoFields("info-fields", state.current.Info);
    refreshObservations();
    renderStepTable();
    loadStatus();
    applyModules();
    if (activeTab === TAB.analysis || activeTab === TAB.improve) guard(refreshAnalysis);
  }
  const url = new URL(location.href);
  url.searchParams.set("lang", getLang());
  history.replaceState(null, "", url);
}

// ------------------------------------------------------------ v1.8: kayıt temposu, örnekleme, ileri analiz

const isSampling = () => state.current?.StudyType === "sampling";

/** Tempo puanı alanı: boş → null; geçersiz → hata. */
function readRating(text) {
  const t_ = String(text).trim();
  if (t_ === "") return null;
  const v = cleanRating(parseNumber(t_, t("name.rating")));
  if (v === null) throw new Error(t("msg.ratingInvalid"));
  return v;
}

/** Yeni etüt formu: örneklemede adımlar yerine kategoriler. */
function syncNewType() {
  const sampling = $("new-type").value === "sampling";
  $("new-steps-label").textContent = t(sampling ? "field.newCategories" : "field.newSteps");
  $("new-steps").placeholder = t(sampling ? "placeholder.newCategories" : "placeholder.newSteps");
}
$("new-type").addEventListener("change", syncNewType);

const categoryClass = (name) => state.current?.Categories.find((c) => c.Name === name)?.Classification ?? "";

function renderSampling() {
  if (!state.current || !isSampling()) return;
  const study = state.current;
  const box = $("categories");
  box.replaceChildren();
  if (!study.Categories.length) {
    const p = document.createElement("p");
    p.className = "hint empty-steps";
    p.textContent = t("s.empty");
    box.append(p);
  }
  const counts = new Map();
  for (const x of study.Samples) counts.set(x.Category, (counts.get(x.Category) ?? 0) + 1);
  study.Categories.forEach((c, i) => {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "step-chip";
    const k = document.createElement("span"); k.className = "key"; k.textContent = i < 9 ? String(i + 1) : "·";
    const n = document.createElement("span"); n.className = "name"; n.textContent = `${c.Name} · ${counts.get(c.Name) ?? 0}`;
    b.append(k, n);
    if (c.Classification) {
      const cl = document.createElement("span");
      cl.className = "cls"; cl.textContent = c.Classification;
      cl.style.setProperty("--cls", CLASS_COLORS[c.Classification]);
      b.append(cl);
    }
    b.addEventListener("click", () => guard(() => recordSample(i)));
    box.append(b);
  });
  const sorted = [...study.Samples].sort((a, b) => a.AtMs - b.AtMs);
  renderTable("samples", sorted, (x) => [num(fmtClock(x.AtMs)), txt(x.Category), txt(categoryClass(x.Category)), txt(x.Note ?? "")], {
    isSelected: (x) => x === state.selectedSample,
    onSelect: (x) => { state.selectedSample = x; $("sample-note").value = x.Note ?? ""; $("sample-editor").hidden = false; if (state.ready) seek(x.AtMs); renderSampling(); },
    empty: t("s.countNone")
  });
  $("sample-editor").hidden = !state.selectedSample || !study.Samples.includes(state.selectedSample);
  const r = analyzeSampling(study);
  $("sample-count").textContent = r.N ? t("s.count", { n: r.N, e: fmtNum(r.Error), req: r.Required }) : t("s.countNone");
  renderSegments();
}

async function recordSample(index) {
  requireVideo();
  const study = state.current;
  const cat = study.Categories[index];
  if (!cat) throw new Error(t("s.noCategory"));
  const at = positionMs();
  if (study.Samples.some((x) => x.AtMs === at)) throw new Error(t("s.duplicate"));
  const sample = { AtMs: at, Category: cat.Name };
  study.Samples.push(sample);
  state.sampleUndo = [...(state.sampleUndo ?? []), sample];
  setStatus(t("s.saved", { cat: cat.Name, at: fmtClock(at) }));
  renderSampling();
  await save();
}

async function undoSample() {
  const study = state.current;
  let last;
  while ((last = state.sampleUndo?.pop()) && !study.Samples.includes(last)) { /* silinmişleri atla */ }
  if (!last) { setStatus(t("s.nothing")); return; }
  study.Samples.splice(study.Samples.indexOf(last), 1);
  setStatus(t("s.undone", { cat: last.Category, at: fmtClock(last.AtMs) }));
  renderSampling();
  await save();
}

function goRandomMoment() {
  requireVideo();
  pause();
  const at = randomMoment(state.current, Number(timeline.max));
  if (at !== null) setPosition(at);
}
on("random-moment", goRandomMoment);
on("next-moment", () => {
  requireVideo();
  pause();
  const stepS = parsePositiveInt($("sample-step").value, t("s.every"));
  setPosition(Math.min(Number(timeline.max), positionMs() + stepS * 1000));
});
on("undo-sample", undoSample);
on("add-category", async () => {
  const study = state.current;
  const name = $("new-category").value.trim();
  if (!name) throw new Error(t("s.emptyName"));
  if (study.Categories.some((c) => c.Name === name)) throw new Error(t("s.exists", { cat: name }));
  study.Categories = cleanCategories([...study.Categories, { Name: name, Classification: $("new-category-class").value }]);
  $("new-category").value = "";
  renderSampling();
  await save();
});
$("new-category").addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); $("add-category").click(); } });
on("sample-save", async () => {
  const x = state.selectedSample;
  if (!x) return;
  const note = $("sample-note").value.trim().slice(0, 200);
  if (note) x.Note = note; else delete x.Note;
  renderSampling();
  await save();
});
on("sample-delete", async () => {
  const x = state.selectedSample;
  if (!x) return;
  state.current.Samples.splice(state.current.Samples.indexOf(x), 1);
  state.selectedSample = null;
  renderSampling();
  await save();
});

/** Örnekleme analizi: kartlar, grafik, tablo. */
function refreshSampling() {
  const study = state.current;
  const r = analyzeSampling(study);
  const box = $("s-summary");
  box.replaceChildren();
  const card = (label, value, detail = []) => {
    const el = document.createElement("div");
    el.className = "sum-card";
    const l = document.createElement("span"); l.className = "sum-label"; l.textContent = label;
    const v = document.createElement("strong"); v.textContent = value;
    el.append(l, v);
    for (const d of detail) { const p = document.createElement("small"); p.textContent = d; el.append(p); }
    box.append(el);
  };
  $("analysis-status").textContent = r.N ? "" : t("s.empty.analysis");
  card(t("s.card.n"), String(r.N));
  if (r.N) {
    card(t("s.card.required"), t("s.card.requiredValue", { n: r.Required }), [r.Required <= r.N ? t("s.card.enough", { e: fmtNum(r.Error) }) : t("s.card.more", { k: r.Required - r.N })]);
    card(t("s.card.classes"), ["VA", "SVA", "NVA", ""].filter((c) => r.ByClass[c] > 0).map((c) => `${c || t("summary.noClass")} ${pct(r.ByClass[c]).replace(/^[+]/, "")}`).join(" · "));
  }
  const fig = $("s-chart");
  fig.replaceChildren();
  if (r.N) {
    const cap = document.createElement("figcaption"); cap.textContent = t("s.chart");
    fig.append(cap, samplingChart(r.Rows.map((x) => ({ label: x.Category, share: x.Share, low: x.Low, high: x.High, cls: x.Classification,
      text: `${pct(x.Share, 1).replace(/^[+]/, "")} ± ${(Math.round(x.Half * 10) / 10).toLocaleString(locale(), { minimumFractionDigits: 1, maximumFractionDigits: 1 })}` })), { width: chartWidth(fig), classLabel: (c) => c || t("summary.noClass") }));
  }
  renderTable("s-results", r.Rows, (x) => [txt(x.Category), txt(x.Classification), num(x.Count), num(x.Share === null ? "" : pct(x.Share, 1).replace(/^[+]/, "")),
    num(x.Low === null ? "" : `${pct(x.Low, 1).replace(/^[+]/, "")} – ${pct(x.High, 1).replace(/^[+]/, "")}`), num(x.Required ?? "")], { empty: t("s.countNone") });
}

const chartWidth = (el) => Math.max(300, Math.min(760, (el.clientWidth || el.parentElement?.clientWidth || window.innerWidth) - 24));
const sec = (v) => `${fmt3(v)} ${getLang() === "tr" ? "sn" : "s"}`;
const stationLabel = (name) => (/^\d+$/.test(name) ? t("lb.station", { n: name }) : name);

/** v1.8 bölümleri: dağılım ve aykırılar, hat dengeleme, standart iş, iyileştirme fikirleri (kartlar istekle yüklenir). */
function renderAdvanced(study, rows, selected) {
  // Dağılım
  const hist = $("hist-box");
  hist.replaceChildren();
  const bins = selected ? histogram(study, selected.Step) : [];
  if (bins.length) {
    const cap = document.createElement("figcaption"); cap.textContent = t("chart.hist", { step: selected.Step });
    hist.append(cap, histogramChart(bins, { width: chartWidth(hist), axis: t("chart.histAxis"), format: (v) => v.toLocaleString(locale(), { maximumFractionDigits: 2 }) }));
  } else { const p = document.createElement("p"); p.className = "hint"; p.textContent = t("hist.empty"); hist.append(p); }
  const outs = outliers(study);
  const flat = outs.flatMap((g) => g.Rows.map((o) => ({ g, o })));
  renderTable("outliers", flat, ({ g, o }) => [txt(g.Step), num(o.Cycle), num(fmtTime(o.StartMs)), num(fmt3(seconds(o))), num(`${fmt3(Math.max(0, g.Low))} – ${fmt3(g.High)}`)],
    { empty: t("outliers.none"), onSelect: ({ o }) => { selectTab(TAB.measure); selectRow(o); if (state.ready) seek(o.StartMs); refreshObservations(); } });
  $("exclude-outliers").disabled = flat.length === 0;

  // Hat dengeleme
  const b = lineBalance(study);
  const defs = new Map(study.Steps.map((x) => [x.Name, x]));
  const withStd = rows.filter((r) => r.Contribution !== null);
  renderTable("balance", withStd, (r) => [txt(r.Step), txt(defs.get(r.Step)?.Classification ?? ""), num(fmt3(r.Contribution)), { text: defs.get(r.Step)?.Station ?? "", station: r.Step }], {});
  // İstasyon hücresi düzenlenebilir.
  for (const td of $("balance").tBodies[0].querySelectorAll("tr td:last-child")) {
    const tr = td.parentElement, step = withStd[tr.sectionRowIndex]?.Step;
    if (!step) continue;
    const input = document.createElement("input");
    input.type = "text"; input.size = 8; input.maxLength = 40; input.value = defs.get(step)?.Station ?? "";
    input.placeholder = t("placeholder.station");
    input.setAttribute("aria-label", `${t("th.station")} · ${step}`);
    input.addEventListener("change", () => guard(async () => { await setStation(step, input.value); }));
    td.replaceChildren(input);
  }
  const summaryEl = $("balance-summary");
  const parts = [];
  if (b.Stations.length) parts.push(t("balance.summary", { k: b.Stations.length, b: stationLabel(b.Bottleneck), max: fmt3(b.Max),
    eff: pct(b.Efficiency, 1).replace(/^[+]/, ""), loss: pct(b.Loss, 1).replace(/^[+]/, "") }));
  else parts.push(t("balance.none"));
  if (b.MinOperators !== null) parts.push(t("balance.minOps", { n: b.MinOperators, takt: fmtNum(study.Takt) }));
  if (b.OverTakt.length) parts.push(t("balance.over", { list: b.OverTakt.map(stationLabel).join(", ") }));
  summaryEl.textContent = parts.join(" ");
  const yb = $("yamazumi-box");
  yb.replaceChildren();
  if (b.Stations.length) {
    const st = [...b.Stations.map((x) => ({ label: stationLabel(x.Station), steps: x.Steps.map((p) => ({ label: p.Step, value: p.Contribution, cls: p.Classification })), load: x.Load, text: fmt3(x.Load) }))];
    if (b.Unassigned.length) {
      const load = b.Unassigned.reduce((sum, p) => sum + p.Contribution, 0);
      st.push({ label: t("balance.unassigned"), steps: b.Unassigned.map((p) => ({ label: p.Step, value: p.Contribution, cls: p.Classification })), load, text: fmt3(load) });
    }
    const cap = document.createElement("figcaption"); cap.textContent = t("chart.yamazumi");
    yb.append(cap, yamazumiChart(st, { width: chartWidth(yb), takt: study.Takt, taktLabel: t("chart.takt", { v: sec(study.Takt ?? 0) }),
      format: (v) => v.toLocaleString(locale(), { maximumFractionDigits: v < 10 ? 1 : 0 }), classLabel: (c) => c || t("summary.noClass") }));
  }

  // Standart iş
  const w = standardWork(study);
  const wb = $("work-box");
  wb.replaceChildren();
  if (w.Rows.length) {
    const cap = document.createElement("figcaption"); cap.textContent = `${t("chart.work")} · ${t("work.summary", { op: sec(w.Operator), total: sec(w.Total) })}`;
    wb.append(cap, workChart(w.Rows.map((r) => ({ label: r.Step, type: r.Type, typeLabel: t("type." + r.Type), start: r.Start, end: r.End, text: fmt3(r.Seconds) })),
      { width: chartWidth(wb), takt: study.Takt, taktLabel: t("chart.takt", { v: sec(study.Takt ?? 0) }), total: w.Total,
        format: (v) => v.toLocaleString(locale(), { maximumFractionDigits: v < 10 ? 1 : 0 }) }));
    const legend = document.createElement("p");
    legend.className = "hint";
    legend.textContent = WORK_TYPES.map((k) => `${{ Manual: "━", Machine: "╍", Walk: "┅", Wait: "┈" }[k]} ${t("type." + k)}`).join("   ");
    wb.append(legend);
  } else { const p = document.createElement("p"); p.className = "hint"; p.textContent = t("work.empty"); wb.append(p); }

  // İyileştirme
  renderTable("ideas", improvements(study), (x) => [txt(x.Step), txt(x.Classification), txt(x.Waste ? t("waste." + x.Waste) : ""), txt(x.Idea), num(fmt3(x.Contribution)),
    num(x.Share === null ? "" : pct(x.Share, 1).replace(/^[+]/, ""))], { empty: t("ideas.empty") });

  // Kartlar: veri hemen, kareler istekle.
  renderStory(false);
}

async function setStation(step, value) {
  const st = state.current.Steps.find((x) => x.Name === step);
  if (!st) { state.current.Steps.push({ Name: step, Classification: "" }); return setStation(step, value); }
  const v = value.trim().slice(0, 40);
  if (v) st.Station = v; else delete st.Station;
  state.current.Steps = cleanSteps(state.current.Steps);
  await save();
  refreshAnalysis();
}

on("auto-balance", async () => {
  const study = state.current;
  if (!(study.Takt > 0)) throw new Error(t("balance.needTakt"));
  study.Steps = deriveSteps(study).map((x) => ({ ...x }));
  for (const [step, station] of autoBalance(study)) {
    let st = study.Steps.find((x) => x.Name === step);
    if (!st) { st = { Name: step, Classification: "" }; study.Steps.push(st); }
    st.Station = station;
  }
  study.Steps = cleanSteps(study.Steps);
  await save();
  refreshAnalysis();
});
on("clear-balance", async () => {
  for (const st of state.current.Steps) delete st.Station;
  await save();
  refreshAnalysis();
});

on("exclude-outliers", async () => {
  const rows = outliers(state.current).flatMap((g) => g.Rows);
  if (!rows.length) return;
  if (!(await ask(t("msg.excludeOutliersConfirm", { n: rows.length }), t("title.excludeOutliers")))) return;
  for (const o of rows) { o.Excluded = true; if (!o.Note) o.Note = t("note.outlier"); }
  refreshObservations();
  await save();
  refreshAnalysis();
  $("analysis-status").textContent = t("status.outliersExcluded", { n: rows.length });
});

/** Adım kartları: kare, ad, sınıf, tür, bitiş anı, standart süre, israf/fikir. withFrames: video karelerini çiz. */
function renderStory(withFrames) {
  const study = state.current;
  const box = $("story");
  box.replaceChildren();
  const rows = new Map(inProcessOrder(study, analyzeSteps(study)).map((r) => [r.Step, r]));
  const list = stepOrderOf(study).filter((n) => rows.get(n)?.Count > 0);
  if (!list.length) { const p = document.createElement("p"); p.className = "hint"; p.textContent = t("story.empty"); box.append(p); return []; }
  const title = document.createElement("h2");
  title.className = "print-only story-title";
  title.textContent = t("story.title", { code: study.Code, name: study.Name });
  box.append(title);
  const canvases = [];
  list.forEach((name, i) => {
    const def = study.Steps.find((x) => x.Name === name) ?? { Name: name, Classification: "" };
    const r = rows.get(name);
    const card = document.createElement("article");
    card.className = "story-card";
    const frame = stepFrame(study, name);
    const canvas = document.createElement("canvas");
    canvas.width = 320; canvas.height = 180;
    canvas.setAttribute("role", "img");
    canvas.setAttribute("aria-label", `${name} · ${t("story.frame", { t: frame === null ? "—" : fmtClock(frame) })}`);
    if (withFrames && frame !== null) canvases.push({ canvas, frame });
    const h = document.createElement("h3"); h.textContent = `${i + 1}. ${name}`;
    const meta = document.createElement("p");
    meta.className = "story-meta";
    meta.textContent = [def.Classification, def.Type ? t("type." + def.Type) : "", r?.Standard !== null && r?.Standard !== undefined ? sec(r.Standard) : ""].filter(Boolean).join(" · ");
    card.append(canvas, h, meta);
    for (const [text, cls] of [[def.Note ? t("now.note", { note: def.Note }) : "", ""], [def.Waste ? `${t("field.waste")}: ${t("waste." + def.Waste)}` : "", "story-waste"], [def.Idea ? `${t("field.idea")}: ${def.Idea}` : "", "story-idea"]]) {
      if (!text) continue;
      const p = document.createElement("p"); p.textContent = text; if (cls) p.className = cls; card.append(p);
    }
    box.append(card);
  });
  return canvases;
}
const stepOrderOf = (study) => { const names = deriveSteps(study).map((x) => x.Name); for (const o of study.Observations) if (!names.includes(o.Step)) names.push(o.Step); return names; };

/** Kareler ayrı bir video öğesinden çizilir (oynatıcının konumu değişmez). */
async function loadStory() {
  const attached = state.videos.get(state.current.Id);
  if (!attached) throw new Error(t("story.noVideo"));
  const canvases = renderStory(true);
  const v = document.createElement("video");
  v.muted = true; v.preload = "auto"; v.src = attached.url;
  await new Promise((resolve, reject) => { v.onloadeddata = resolve; v.onerror = () => reject(new Error(t("status.videoError"))); });
  for (const { canvas, frame } of canvases) {
    await new Promise((resolve) => { v.onseeked = resolve; v.currentTime = Math.min(frame / 1000, Math.max(0, v.duration - 0.05)); });
    const ctx = canvas.getContext("2d");
    const ratio = v.videoWidth && v.videoHeight ? v.videoWidth / v.videoHeight : 16 / 9;
    canvas.height = Math.round(canvas.width / ratio);
    ctx.drawImage(v, 0, 0, canvas.width, canvas.height);
  }
  v.removeAttribute("src"); v.load();
}
on("load-story", loadStory);

// ---- yan yana video (önce / sonra)

const cmpVideos = { before: null, after: null };
function compareStudy(side) { return state.studies.find((s) => s.Id === $(side === "before" ? "compare-before" : "compare-after").value) ?? null; }
function setCompareVideo(side, url) {
  const v = $(side === "before" ? "cmp-video-before" : "cmp-video-after");
  if (url) v.src = url; else v.removeAttribute("src");
  const s = compareStudy(side);
  $(side === "before" ? "cmp-cap-before" : "cmp-cap-after").textContent = s ? `${s.Code} · ${s.Name}${url ? "" : " · " + t("cmp.noVideo")}` : "";
}
function fillCompareSteps(a, b) {
  const sel = $("cmp-step"), keep = sel.value;
  const names = [...new Set([...stepOrderOf(a), ...stepOrderOf(b)])];
  sel.replaceChildren(...names.map((n) => Object.assign(document.createElement("option"), { value: n, textContent: n })));
  if (names.includes(keep)) sel.value = keep;
  for (const side of ["before", "after"]) {
    const s = compareStudy(side);
    const own = s ? state.videos.get(s.Id)?.url : null;
    setCompareVideo(side, own ?? cmpVideos[side]?.url ?? null);
  }
}
for (const side of ["before", "after"])
  on(side === "before" ? "cmp-pick-before" : "cmp-pick-after", async () => {
    const file = await chooseFile($("video-file"));
    if (!file) return;
    if (cmpVideos[side]) URL.revokeObjectURL(cmpVideos[side].url);
    cmpVideos[side] = { url: URL.createObjectURL(file) };
    setCompareVideo(side, cmpVideos[side].url);
  });
function firstStart(study, step) {
  const o = study?.Observations.filter((x) => x.Step === step && !x.Excluded && x.EndMs > x.StartMs).sort((x, y) => x.StartMs - y.StartMs)[0];
  return o ? o.StartMs : null;
}
on("cmp-step", () => {
  const step = $("cmp-step").value;
  for (const side of ["before", "after"]) {
    const v = $(side === "before" ? "cmp-video-before" : "cmp-video-after");
    const at = firstStart(compareStudy(side), step);
    const s = compareStudy(side);
    $(side === "before" ? "cmp-cap-before" : "cmp-cap-after").textContent = s ? `${s.Code} · ${s.Name} · ${step}${at === null ? " · " + t("cmp.missing") : " · " + fmtClock(at)}` : "";
    if (at !== null && v.getAttribute("src")) { v.pause(); v.currentTime = at / 1000; }
  }
}, "change");
on("cmp-play", () => { for (const id of ["cmp-video-before", "cmp-video-after"]) if ($(id).getAttribute("src")) $(id).play().catch(() => {}); });
on("cmp-pause", () => { for (const id of ["cmp-video-before", "cmp-video-after"]) $(id).pause(); });
$("compare-dialog").addEventListener("close", () => { for (const id of ["cmp-video-before", "cmp-video-after"]) $(id).pause(); });

function readInfoSafe(boxId) { return $(`${boxId}-Operator`) ? readInfo(boxId) : {}; }
$("lang-switch").addEventListener("click", () => guard(async () => {
  setLang(getLang() === "tr" ? "en" : "tr");
  $("lang-switch").setAttribute("lang", getLang() === "tr" ? "en" : "tr");
  renderLanguage();
  await reloadStudies();
}));

$("lang-switch").setAttribute("lang", getLang() === "tr" ? "en" : "tr");
renderLanguage();
guard(async () => {
  await reloadStudies();
  if (store.lastSkipped > 0)
    await showDialog(t("msg.skipped", { n: store.lastSkipped }));
  // İlk açılışta kısa tur (bir kez; sonra sağ üstten açılır).
  if (!tourSeen()) openTour();
});
