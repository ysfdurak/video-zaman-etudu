// Video Zaman Etüdü — tarayıcı arayüzü. Desktop/MainWindow.xaml.cs ile aynı akış ve mesajlar.
import {
  newStudy, newObservation, seconds, videoLabel, analyzeSteps, analyzeCycles, studentInv, validateSettings,
  exportPackage, importPackage, suggestedFileName, parseNumber, parsePositiveInt, EXTENSION
} from "./core.js";
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
    (s) => [txt(s.Code), txt(s.Name), txt(videoLabel(s)), txt(fmtDate(s.Updated))],
    {
      isSelected: (s) => s.Id === state.selectedStudyId,
      onSelect: (s) => { state.selectedStudyId = s.Id; reloadStudiesView(); },
      onOpen: (s) => openStudy(s),
      empty: t("list.empty")
    });
}
const reloadStudiesView = () => reloadStudies();
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
  const study = newStudy({ Name: name, Code: code, Video: state.newVideo.file.name });
  attachVideo(study, state.newVideo.file);
  if (state.newVideo.handle) await store.saveHandle(study.Id, state.newVideo.handle);
  await store.save(study);
  requestPersistence();
  state.newVideo = null;
  $("new-name").value = ""; $("new-code").value = ""; $("new-video").value = "";
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
  for (const id of ["observations", "results", "cycles", "groups"]) $(id).tBodies[0].replaceChildren();
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
  setStatus(state.ready ? t("status.ready") : t("status.noDuration"));
});
video.addEventListener("error", () => {
  if (!video.getAttribute("src")) return;
  state.ready = false;
  timeline.disabled = true;
  stopMeasure();
  setStatus(t("status.videoError"));
});
video.addEventListener("ended", () => { pause(); updatePosition(); });
// Uygulama dışından gelen konum değişikliği (ör. medya tuşları) de ardışık ölçümü keser.
video.addEventListener("seeking", () => {
  if (state.internalSeek) return;
  if (state.anchor !== null) { stopMeasure(); setStatus(t("status.positionChanged")); }
});
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
  if (state.anchor !== null) {
    stopMeasure();
    setStatus(t("status.positionChanged"));
  }
  setPosition(ms);
}

/** İnce konumlama (K10): en fazla 1 sn, videoyu duraklatır, ardışık ölçümü kesmez. */
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
  $("measure").textContent = t("btn.start");
  $("measure").classList.remove("on");
}

function requireVideo() {
  if (!state.ready || !state.current) throw new Error(t("msg.needVideo"));
}

async function measure() {
  requireVideo();
  const now = positionMs();
  if (state.anchor === null) {
    state.anchor = now;
    $("measure").textContent = t("btn.measure");
    $("measure").classList.add("on");
    setStatus(t("status.started", { s: fmt3(now / 1000) }));
    return;
  }
  // Kayıt eşzamanlı eklenir, çapa hemen ilerler; diske yazma arkadan gelir.
  // Böylece yazma sürerken yapılan atlama/Durdur, ölçümü yeniden başlatmaz.
  const saving = addObservation(state.anchor, now);
  state.anchor = now;
  await saving;
}

function markStart() {
  requireVideo();
  stopMeasure();
  state.selectedStart = positionMs();
  setStatus(t("status.markStart", { s: fmt3(state.selectedStart / 1000) }));
}

async function addMarked() {
  requireVideo();
  if (state.selectedStart === null) throw new Error(t("msg.markFirst"));
  const saving = addObservation(state.selectedStart, positionMs());
  state.selectedStart = null;
  await saving;
}

function fillRow(row) {
  const name = $("step").value.trim();
  if (name.length === 0) throw new Error(t("msg.stepEmpty"));
  const cycle = parsePositiveInt($("cycle").value, t("name.cycle"));
  row.Step = name;
  row.Cycle = cycle;
  row.Classification = $("classification").value;
  row.Excluded = $("excluded").checked;
}

/** Doğrulama ve ekleme eşzamanlıdır (hata varsa hiçbir şey değişmez); kayıt sözü döner. */
function addObservation(start, end) {
  if (end <= start) throw new Error(t("msg.endAfterStart"));
  const row = newObservation({ StartMs: start, EndMs: end });
  fillRow(row);
  if (state.current.Observations.some((x) => x.StartMs === start && x.EndMs === end && x.Cycle === row.Cycle && x.Step === row.Step && x.Excluded === row.Excluded))
    throw new Error(t("msg.duplicate"));
  state.current.Observations.push(row);
  refreshObservations();
  setStatus(t("status.saved", { step: row.Step, s: fmt3(seconds(row)) }));
  return save();
}

on("measure", measure);
on("stop", stopMeasure);
on("mark-start", markStart);
on("add-marked", addMarked);

on("edit-row", async () => {
  const row = state.selectedRow;
  if (!row) return;
  fillRow(row);
  refreshObservations();
  await save();
});

on("bulk-class", async () => {
  const row = state.selectedRow;
  if (!row || !state.current) return;
  for (const item of state.current.Observations) if (item.Step === row.Step) item.Classification = $("classification").value;
  refreshObservations();
  await save();
});

on("delete-row", async () => {
  const row = state.selectedRow;
  if (!row || !state.current) return;
  if (!(await ask(t("msg.deleteRowConfirm"), t("title.deleteRow")))) return;
  state.current.Observations.splice(state.current.Observations.indexOf(row), 1);
  state.selectedRow = null;
  refreshObservations();
  await save();
});

function refreshObservations() {
  const rows = state.current ? [...state.current.Observations].sort((a, b) => a.StartMs - b.StartMs || a.EndMs - b.EndMs) : [];
  renderTable("observations", rows,
    (r) => [num(r.StartMs), num(r.EndMs), num(fmt3(seconds(r))), txt(r.Step), txt(r.Classification), txt(r.Excluded ? t("yes") : t("no")), num(r.Cycle)],
    {
      isSelected: (r) => r === state.selectedRow,
      onSelect: (r) => {
        state.selectedRow = r;
        $("step").value = r.Step;
        $("cycle").value = String(r.Cycle);
        $("classification").value = r.Classification;
        $("excluded").checked = r.Excluded;
        if (state.ready) seek(r.StartMs);
        refreshObservations();
      },
      empty: t("obs.empty")
    });
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
  if (activeTab !== 1 || !state.current || dialogOpen || e.altKey || e.metaKey) return;
  if (isEditable(document.activeElement)) return;
  const step = e.shiftKey ? 1000 : e.ctrlKey ? 40 : 100;
  let action;
  switch (e.key) {
    case " ": action = () => (video.paused ? play() : pause()); break;
    case "Enter": action = measure; break;
    case "Escape":
      action = () => { if (state.anchor !== null) { stopMeasure(); setStatus(t("status.stopped")); } };
      break;
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
  $("confidence").value = fmtNum(s.Confidence);
  $("target").value = fmtNum(s.TargetError);
  $("pace").value = fmtNum(s.Pace);
  $("allowance").value = fmtNum(s.Allowance);
  $("group-ms").value = String(s.GroupMs);
  $("method").value = s.RepeatedLow ? "1" : "0";
  $("stable").checked = s.Stable;
}

async function applySettings() {
  if (!state.current) return;
  const settings = {
    Confidence: parseNumber($("confidence").value, t("name.confidence")), TargetError: parseNumber($("target").value, t("name.target")),
    Pace: parseNumber($("pace").value, t("name.pace")), Allowance: parseNumber($("allowance").value, t("name.allowance")),
    RepeatedLow: $("method").value === "1", GroupMs: parsePositiveInt($("group-ms").value, t("name.group")),
    Stable: $("stable").checked
  };
  validateSettings(settings);
  state.current.Settings = settings;
  await save();
  refreshAnalysis();
}

function refreshAnalysis() {
  const study = state.current;
  if (!study) return;
  const previous = state.selectedResult;
  const rows = analyzeSteps(study);
  const selected = rows.find((r) => r.Step === previous) ?? rows[0] ?? null;
  state.selectedResult = selected?.Step ?? null;
  renderTable("results", rows,
    (r) => [txt(r.Step), num(r.Count), num(r.Excluded), num(fmt3(r.Mean)), num(fmt3(r.Median)), num(fmt3(r.Min)), num(fmt3(r.Max)),
      num(fmt3(r.Deviation)), num(fmt3(r.CriticalT)), num(fmt3(r.ErrorPercent)), txt(result(r.Sufficiency)), num(fmt3(r.Base)),
      num(fmt3(r.Normal)), num(fmt3(r.Standard)), num(r.Every), num(fmt3(r.Contribution)), txt(result(r.Method))],
    {
      isSelected: (r) => r.Step === state.selectedResult,
      onSelect: (r) => { state.selectedResult = r.Step; $("every").value = String(r.Every); refreshAnalysis(); },
      empty: t("analysis.empty")
    });
  if (selected && previous !== selected.Step) $("every").value = String(selected.Every);
  renderTable("cycles", analyzeCycles(study), (c) => [num(c.Cycle), num(fmt3(c.Elapsed)), num(fmt3(c.Workload))]);
  renderTable("groups", selected?.Groups ?? [], (g) => [num(fmt3(g.Seconds)), num(g.Count)], { empty: t("groups.empty") });
  renderSummary(study, rows);
  $("analysis-status").textContent = `${t(study.Settings.Stable ? "guide.stable" : "guide.general")} ${t("guide.tail")}`;
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
  const withStd = rows.filter((r) => r.Contribution !== null);
  const total = withStd.reduce((s, r) => s + r.Contribution, 0);
  const missing = rows.some((r) => r.Contribution === null && r.Count > 0);
  card(t("summary.total"), withStd.length ? `${fmt3(total)} ${getLang() === "tr" ? "sn" : "s"}` : "—", missing ? [t("summary.incomplete")] : []);
  card(t("summary.rate"), total > 0 ? t("summary.rateUnit", { n: (3600 / total).toLocaleString(locale(), { maximumFractionDigits: 1 }) }) : "—");
  const settings = study.Settings;
  const bad = rows.filter((r) => r.Sufficiency === "Yetersiz");
  const na = rows.filter((r) => r.Sufficiency === "Hesaplanamıyor" && r.Count > 0);
  const details = bad.map((r) => t("summary.needDetail", {
    step: r.Step, c: r.Count,
    n: requiredObservations(r, settings)
  }));
  if (na.length) details.push(t("summary.na", { k: na.length }));
  card(t("summary.sufficiency"), bad.length ? t("summary.need", { k: bad.length }) : t("summary.allOk"), details);
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

on("apply-settings", applySettings);

on("apply-frequency", async () => {
  if (!state.current || state.selectedResult === null) return;
  state.current.Frequency.set(state.selectedResult, parsePositiveInt($("every").value, t("name.frequency")));
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

on("excel", async () => {
  if (!state.current) return;
  await applySettings();
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
  // Kullanıcı adım adını değiştirmediyse varsayılan ad da dile uyar.
  const other = getLang() === "tr" ? "en" : "tr";
  if ($("step").value === t("default.step", {}, other)) $("step").value = t("default.step");
  updateHeader();
  $("measure").textContent = state.anchor === null ? t("btn.start") : t("btn.measure");
  if (state.current) {
    loadSettings();
    refreshObservations();
    if (activeTab === 2) guard(refreshAnalysis);
  }
  const url = new URL(location.href);
  url.searchParams.set("lang", getLang());
  history.replaceState(null, "", url);
}
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
