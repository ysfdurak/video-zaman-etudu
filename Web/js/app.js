// Video Zaman Etüdü — tarayıcı arayüzü. Desktop/MainWindow.xaml.cs ile aynı akış ve mesajlar.
import {
  newStudy, newObservation, seconds, videoLabel, analyzeSteps, analyzeCycles, studentInv, validateSettings,
  exportPackage, importPackage, suggestedFileName, parseNumber, parsePositiveInt, EXTENSION,
  INFO_FIELDS, cleanInfo, cleanSteps, deriveSteps, inProcessOrder, cycleSpread, totalStandard, compareStudies, cleanTakt
} from "./core.js";
import { stepChart, cycleChart, CLASS_COLORS } from "./charts.js";
import { reportBytes } from "./report.js";
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

let activeTab = 0;
function selectTab(index) {
  activeTab = index;
  for (let i = 0; i < 3; i++) {
    $(`tab-${i}`).setAttribute("aria-selected", String(i === index));
    $(`panel-${i}`).hidden = i !== index;
  }
  if (index !== 1) pause();
  if (index === 2 && state.current) guard(refreshAnalysis);
}
for (let i = 0; i < 3; i++) $(`tab-${i}`).addEventListener("click", () => selectTab(i));

function setTabsEnabled(enabled) {
  $("tab-1").disabled = !enabled;
  $("tab-2").disabled = !enabled;
}

function updateHeader() {
  const h = $("header");
  h.replaceChildren(t("header.brand") + " ");
  const sep = document.createElement("span");
  sep.className = "sep";
  sep.textContent = "/";
  h.append(sep, state.current ? `${state.current.Code} · ${state.current.Name}` : t("header.studies"));
  document.title = state.current ? `${state.current.Code} · ${t("app.title")}` : t("app.title");
}

// ------------------------------------------------------------ çalışmalar

async function reloadStudies() {
  state.studies = await store.readAll();
  renderTable("studies", state.studies,
    (s) => [txt(s.Code), txt(s.Name), txt([s.Info?.Station, s.Info?.Operator].filter(Boolean).join(" · ")),
      num(s.Observations.length), num(new Set(s.Observations.map((o) => o.Cycle)).size || ""), txt(studyState(s)), txt(videoLabel(s)), txt(fmtDate(s.Updated))],
    {
      isSelected: (s) => s.Id === state.selectedStudyId,
      onSelect: (s) => { state.selectedStudyId = s.Id; reloadStudiesView(); },
      onOpen: (s) => openStudy(s),
      empty: t("list.empty")
    });
  const none = !selectedStudy();
  for (const id of ["open", "export", "delete"]) $(id).disabled = none;
  $("compare").disabled = state.studies.length < 2;
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
  const steps = cleanSteps($("new-steps").value.split("\n").map((x) => ({ Name: x, Classification: "" })));
  const info = readInfo("new-info");
  const study = newStudy({ Name: name, Code: code, Video: state.newVideo.file.name, Steps: steps, Info: info });
  attachVideo(study, state.newVideo.file);
  if (state.newVideo.handle) await store.saveHandle(study.Id, state.newVideo.handle);
  await store.save(study);
  requestPersistence();
  state.newVideo = null;
  $("new-name").value = ""; $("new-code").value = ""; $("new-video").value = ""; $("new-steps").value = "";
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
  const study = importPackage(new Uint8Array(await file.arrayBuffer()));
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

async function openStudy(study) {
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
  state.stepIndex = 0;
  state.resume = null;
  $("cycle").value = String(Math.max(1, ...study.Observations.map((o) => o.Cycle)));
  $("excluded").checked = false;
  renderSteps();
  renderInfoFields("info-fields", study.Info);
  setTabsEnabled(true);
  selectTab(1);
  refreshObservations();
  loadSettings();
  await loadVideo();
  await reloadStudies();
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
  setStatus(state.ready ? (state.current?.Steps.length ? t("status.ready") : t("status.addSteps")) : t("status.noDuration"));
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
    if (st.Note) b.title = st.Note;
    b.addEventListener("click", () => guard(() => selectStep(i)));
    box.append(b);
  });
  const st = currentStep();
  $("step-tools").hidden = !st;
  if (st) { $("step-class").value = st.Classification; $("step-note").value = st.Note ?? ""; }
  $("step-left").disabled = state.stepIndex <= 0;
  $("step-right").disabled = state.stepIndex >= list.length - 1;
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

async function saveSteps() {
  state.current.Steps = cleanSteps(state.current.Steps);
  renderSteps();
  await save();
}

on("add-step", async () => {
  if (!state.current) return;
  const name = $("new-step").value.trim();
  if (!name) throw new Error(t("msg.stepEmpty"));
  if (steps().some((x) => x.Name === name)) throw new Error(t("msg.stepExists", { step: name }));
  steps().push({ Name: name, Classification: $("new-step-class").value });
  $("new-step").value = "";
  // Yeni adım seçilir; araya giren bir kayıtsa (kapsam dışı) sonra önceki adıma dönülür.
  if (steps().length > 1) state.resume = state.stepIndex;
  state.stepIndex = steps().length - 1;
  await saveSteps();
  if (state.ready && state.anchor === null) setStatus(t("status.ready"));
});
// Yeni adım kutusunda Enter adımı ekler (kısayollar metin kutusunda çalışmaz).
$("new-step").addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); $("add-step").click(); } });

function moveStep(delta) {
  const list = steps(), i = state.stepIndex, j = i + delta;
  if (j < 0 || j >= list.length) return;
  [list[i], list[j]] = [list[j], list[i]];
  state.stepIndex = j;
  return saveSteps();
}
on("step-left", () => moveStep(-1));
on("step-right", () => moveStep(1));
on("step-class", async () => {
  const st = currentStep();
  if (!st) return;
  st.Classification = $("step-class").value;
  await saveSteps();
  const others = state.current.Observations.filter((o) => o.Step === st.Name && o.Classification !== st.Classification);
  if (others.length && (await ask(t("msg.stepClassExisting", { n: others.length, step: st.Name, cls: st.Classification || "—" }), t("title.stepClass")))) {
    for (const o of others) o.Classification = st.Classification;
    refreshObservations();
    await save();
  }
}, "change");
on("step-note", async () => {
  const st = currentStep();
  if (!st) return;
  const note = $("step-note").value.trim().slice(0, 200);
  if (note) st.Note = note; else delete st.Note;
  await saveSteps();
}, "change");
on("step-rename", async () => {
  const st = currentStep();
  if (!st) return;
  const next = window.prompt(t("msg.stepRename", { step: st.Name }), st.Name)?.trim();
  if (!next || next === st.Name) return;
  if (steps().some((x) => x.Name === next)) throw new Error(t("msg.stepExists", { step: next }));
  const old = st.Name;
  st.Name = next;
  for (const o of state.current.Observations) if (o.Step === old) o.Step = next;
  if (state.current.Frequency.has(old)) { state.current.Frequency.set(next, state.current.Frequency.get(old)); state.current.Frequency.delete(old); }
  refreshObservations();
  await saveSteps();
});
on("step-remove", async () => {
  const st = currentStep();
  if (!st) return;
  const used = state.current.Observations.filter((o) => o.Step === st.Name).length;
  if (!(await ask(used ? t("msg.stepRemoveUsed", { step: st.Name, n: used }) : t("msg.stepRemove", { step: st.Name }), t("title.stepRemove")))) return;
  steps().splice(state.stepIndex, 1);
  await saveSteps();
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
  const row = newObservation({ StartMs: start, EndMs: end, Step: st.Name, Classification: st.Classification, Excluded: $("excluded").checked, Cycle: cycle });
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
  if (!steps().some((x) => x.Name === name)) { steps().push({ Name: name, Classification: row.Classification }); renderSteps(); }
  refreshObservations();
  setStatus(t("status.rowUpdated", { step: row.Step }));
  await save();
});

on("bulk-class", async () => {
  const row = state.selectedRow;
  if (!row || !state.current) return;
  const cls = $("row-class").value;
  for (const item of state.current.Observations) if (item.Step === row.Step) item.Classification = cls;
  const st = steps().find((x) => x.Name === row.Step);
  if (st) st.Classification = cls;
  renderSteps();
  refreshObservations();
  await save();
});

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

function refreshObservations() {
  const rows = state.current ? [...state.current.Observations].sort((a, b) => a.StartMs - b.StartMs || a.EndMs - b.EndMs) : [];
  renderTable("observations", rows,
    (r) => [num(fmtTime(r.StartMs)), num(fmtTime(r.EndMs)), num(fmt3(seconds(r))), txt(r.Step), txt(r.Classification), txt(r.Excluded ? t("yes") : t("no")), num(r.Cycle), txt(r.Note ?? "")],
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
  renderSegments();
}

// ---- zaman çizgisindeki ölçülen aralıklar

function renderSegments() {
  const box = $("segments");
  box.replaceChildren();
  const total = Number(timeline.max);
  if (!state.current || !state.ready || !(total > 0)) { updateLiveSegment(); return; }
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
  if (activeTab !== 1 || !state.current || dialogOpen || e.altKey) return;
  if (e.metaKey && e.key.toLowerCase() !== "z") return;
  if (isEditable(document.activeElement)) return;
  const step = e.shiftKey ? 1000 : e.ctrlKey ? 40 : 100;
  let action;
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") action = undoLast;
  else if (!e.ctrlKey && /^[1-9]$/.test(e.key)) { const i = Number(e.key) - 1; action = () => selectStep(i); }
  else switch (e.key) {
    case " ": action = () => (video.paused ? play() : pause()); break;
    case "Enter": action = measure; break;
    case "Escape":
      action = () => { if (state.anchor !== null) { stopMeasure(); setStatus(t("status.stopped")); } };
      break;
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
  $("confidence").value = fmtNum(s.Confidence);
  $("target").value = fmtNum(s.TargetError);
  $("pace").value = fmtNum(s.Pace);
  $("allowance").value = fmtNum(s.Allowance);
  $("group-ms").value = String(s.GroupMs);
  $("method").value = s.RepeatedLow ? "1" : "0";
  $("stable").checked = s.Stable;
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
  validateSettings(settings);
  const taktText = $("takt").value.trim();
  const takt = taktText === "" ? null : cleanTakt(parseNumber(taktText, t("name.takt")));
  if (taktText !== "" && takt === null) throw new Error(t("msg.taktInvalid"));
  state.current.Settings = settings;
  state.current.Takt = takt;
  await save();
  refreshAnalysis();
}
async function applySettingsInline() {
  try { await applySettings(); }
  catch (error) { $("settings-error").textContent = errorText(error); $("settings-error").hidden = false; }
}
for (const id of ["confidence", "target", "pace", "allowance", "group-ms", "method", "stable", "takt"])
  $(id).addEventListener("change", applySettingsInline);

function refreshAnalysis() {
  const study = state.current;
  if (!study) return;
  const previous = state.selectedResult;
  const rows = inProcessOrder(study, analyzeSteps(study));
  const selected = rows.find((r) => r.Step === previous) ?? rows[0] ?? null;
  state.selectedResult = selected?.Step ?? null;
  const empty = study.Observations.length === 0;
  $("analysis-empty").hidden = !empty;
  $("analysis-body").hidden = empty;
  $("sel-step").textContent = selected?.Step ?? "—";
  $("apply-step").disabled = !selected;
  renderTable("results", rows,
    (r) => [txt(r.Step), num(r.Count), num(r.Excluded), num(fmt3(r.Mean)), num(fmt3(r.Median)), num(fmt3(r.Min)), num(fmt3(r.Max)),
      num(fmt3(r.Deviation)), num(fmt3(r.CriticalT)), num(fmt3(r.ErrorPercent)), txt(result(r.Sufficiency)), num(fmt3(r.Base)),
      num(fmtNum(r.Pace) + (study.StepPace.has(r.Step) ? " *" : "")), num(fmt3(r.Normal)), num(fmt3(r.Standard)), num(r.Every), num(fmt3(r.Contribution)), txt(result(r.Method))],
    {
      isSelected: (r) => r.Step === state.selectedResult,
      onSelect: (r) => { state.selectedResult = r.Step; fillStepSettings(r); refreshAnalysis(); },
      empty: t("analysis.empty")
    });
  if (selected && previous !== selected.Step) fillStepSettings(selected);
  const spread = cycleSpread(analyzeCycles(study));
  renderTable("cycles", spread.Rows, (c) => [num(c.Cycle), num(fmt3(c.Elapsed)), num(fmt3(c.Workload)), num((c.Outlier ? "⚠ " : "") + pct(c.DiffPercent))]);
  renderCharts(study, rows, spread);
  renderTable("groups", selected?.Groups ?? [], (g) => [num(fmt3(g.Seconds)), num(g.Count)], { empty: t("groups.empty") });
  renderSummary(study, rows);
  const notes = [`${t(study.Settings.Stable ? "guide.stable" : "guide.general")} ${t("guide.tail")}`];
  if (study.Settings.Allowance === 0 && rows.length) notes.push(t("guide.noAllowance"));
  $("analysis-status").textContent = notes.join(" ");
}

function fillStepSettings(r) {
  $("every").value = String(r.Every);
  $("step-pace").value = state.current.StepPace.has(r.Step) ? fmtNum(state.current.StepPace.get(r.Step)) : "";
}

/** Hedef hataya inmek için gereken yaklaşık gözlem sayısı: t değeri serbestlik derecesine göre yinelemeli güncellenir. */
function requiredObservations(r, settings) {
  const p = (1 + settings.Confidence / 100) / 2;
  const k = (r.Deviation * 100) / (settings.TargetError * r.Mean);
  let n = r.Count;
  for (let i = 0; i < 50; i++) {
    const next = Math.max(r.Count + 1, Math.ceil(Math.pow(studentInv(n - 1, p) * k, 2)));
    if (next === n) break;
    n = next;
  }
  return n;
}

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
  card(t("summary.total"), total !== null ? `${fmt3(total)} ${getLang() === "tr" ? "sn" : "s"}` : "—", missing ? [t("summary.incomplete")] : []);
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
  card(t("summary.sufficiency"), bad.length ? t("summary.need", { k: bad.length }) : t("summary.allOk"), details);
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
      { width, mean: spread.Mean, meanLabel: t("chart.mean", { v: fmt3(spread.Mean) + " " + unit }), axis: t("th.c.cycle") })));
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

on("go-measure", () => selectTab(1));
on("print", async () => {
  if (!state.current) return;
  await applySettings();
  const s = state.current;
  $("print-foot").textContent = t("print.foot", { code: s.Code, name: s.Name, date: fmtDate(new Date().toISOString()) }) +
    INFO_FIELDS.filter((k) => s.Info?.[k]).map((k) => ` · ${t("info." + k)}: ${s.Info[k]}`).join("");
  $("info-box").open = true;
  window.print();
});
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
  if (state.current) {
    loadSettings();
    renderSteps();
    renderInfoFields("info-fields", state.current.Info);
    refreshObservations();
    if (activeTab === 2) guard(refreshAnalysis);
  }
  const url = new URL(location.href);
  url.searchParams.set("lang", getLang());
  history.replaceState(null, "", url);
}
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
});
