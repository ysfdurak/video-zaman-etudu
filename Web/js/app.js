// Video Zaman Etüdü — tarayıcı arayüzü. Desktop/MainWindow.xaml.cs ile aynı akış ve mesajlar.
import {
  newStudy, newObservation, seconds, videoLabel, analyzeSteps, analyzeCycles, studentInv, validateSettings,
  exportPackage, importPackage, suggestedFileName, parseNumber, parsePositiveInt, EXTENSION
} from "./core.js";
import { reportBytes } from "./report.js";
import { StudyStore } from "./store.js";

/** Siteye konulduğunda Windows sürümünün indirme adresi (boşsa bağlantı gizlenir). */
const DESKTOP_URL = document.documentElement.dataset.desktopUrl ?? "";
const NUDGE_MAX_MS = 1000;
/** Sitede yayınlanan sentetik demo (Web/demo/, kökteki demo/ klasörünün kopyası). */
const DEMO = { package: "demo/sentetik-depo-etudu.vzetud", video: "demo/sentetik-depo-etudu.mp4", code: "DEMO-01", videoName: "sentetik-depo-etudu.mp4" };

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

const fmt3 = (v) => (v === null || v === undefined ? "" : v.toLocaleString("tr-TR", { minimumFractionDigits: 3, maximumFractionDigits: 3 }));
const fmtNum = (v) => v.toLocaleString("tr-TR", { useGrouping: false, maximumFractionDigits: 10 });
const pad = (n, w = 2) => String(n).padStart(w, "0");
const fmtDate = (iso) => {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "" : `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};
const fmtClock = (ms) => {
  const t = Math.max(0, Math.round(ms));
  return `${pad(Math.floor(t / 3600000))}:${pad(Math.floor(t / 60000) % 60)}:${pad(Math.floor(t / 1000) % 60)}.${pad(t % 1000, 3)}`;
};

// ------------------------------------------------------------ iletişim kutusu

const dialog = $("dialog");
let dialogOpen = false;

function showDialog(text, { confirm = false, pre = null, title = "Video Zaman Etüdü" } = {}) {
  $("dialog-title").textContent = title;
  $("dialog-text").textContent = text;
  $("dialog-pre").hidden = pre === null;
  $("dialog-pre").textContent = pre ?? "";
  $("dialog-no").hidden = !confirm;
  $("dialog-yes").textContent = confirm ? "Evet" : "Tamam";
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
  try { await action(); } catch (error) { await showDialog(error?.message || String(error)); }
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
  h.replaceChildren("VIDEO ZAMAN ETÜDÜ ");
  const sep = document.createElement("span");
  sep.className = "sep";
  sep.textContent = "/";
  h.append(sep, state.current ? `${state.current.Code} · ${state.current.Name}` : "Çalışmalar");
  document.title = state.current ? `${state.current.Code} · Video Zaman Etüdü` : "Video Zaman Etüdü";
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
      empty: "Henüz etüt yok. Yeni bir etüt başlatın veya demo etüdü içe alın."
    });
}
const reloadStudiesView = () => reloadStudies();
const selectedStudy = () => state.studies.find((s) => s.Id === state.selectedStudyId) ?? null;

/** Video seçimi: destekleyen tarayıcıda dosya tutamacı da alınır (sonraki oturumda yeniden bağlamak için). */
async function pickVideo() {
  if (window.showOpenFilePicker) {
    try {
      const [handle] = await window.showOpenFilePicker({
        types: [{ description: "Video dosyaları", accept: { "video/*": [".mp4", ".m4v", ".mov", ".webm", ".mkv", ".avi", ".wmv", ".mpg", ".mpeg"] } }],
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
  if (!name || !code || !state.newVideo) throw new Error("Etüt adı, kodu ve geçerli video dosyası seçin.");
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
  let study = state.studies.find((s) => s.Code === DEMO.code && s.ExpectedVideoName === DEMO.videoName);
  if (!study) {
    const response = await fetch(DEMO.package);
    if (!response.ok) throw new Error("Demo etüdü yüklenemedi. Sayfayı yenileyip tekrar deneyin.");
    study = importPackage(new Uint8Array(await response.arrayBuffer()));
    await store.save(study);
    requestPersistence();
  }
  await openStudy(study);
});

/** Demo videosu bağlı değilse sitenin kendi kopyasından bağlar. */
async function attachDemoVideo(study) {
  if (videoLabel(study) !== DEMO.videoName) return false;
  try {
    const response = await fetch(DEMO.video);
    if (!response.ok) return false;
    attachVideo(study, new File([await response.blob()], DEMO.videoName, { type: "video/mp4" }));
    return true;
  } catch { return false; }
}

on("import", async () => {
  const file = await chooseFile($("package-file"));
  if (!file) return;
  const study = importPackage(new Uint8Array(await file.arrayBuffer()));
  if (!(await ask(`“${study.Name}” (${study.Code}) yeni bir etüt olarak eklenecek. Devam edilsin mi?`, "Etüdü içe al"))) return;
  await store.save(study);
  requestPersistence();
  await openStudy(study);
});

on("export", () => {
  const s = selectedStudy();
  if (!s) throw new Error("Dışa aktarmak için listeden bir etüt seçin.");
  download(new Blob([exportPackage(s)], { type: "application/octet-stream" }), suggestedFileName(s, EXTENSION));
});

on("delete", async () => {
  const s = selectedStudy();
  if (!s) throw new Error("Silmek için listeden bir etüt seçin.");
  if (!(await ask(`“${s.Name}” (${s.Code}) etüdü ve tüm ölçümleri bu tarayıcıdan silinecek. Video dosyası silinmez. ` +
      "Bu işlem geri alınamaz; gerekirse önce dışa aktarın. Silinsin mi?", "Etüdü sil"))) return;
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
    setStatus(expected
      ? `Video bağlı değil veya bulunamadı. Beklenen dosya: ${expected}. Videoyu yeniden seçebilirsiniz.`
      : "Video bağlı değil. Ölçümleri inceleyebilir veya bir video seçebilirsiniz.");
    return;
  }
  setStatus("Video açılıyor…");
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
  const differs = expected && picked.file.name !== expected ? ` Seçilen dosyanın adı (${picked.file.name}) beklenenden farklı.` : "";
  const note = expected ? ` Beklenen dosya: ${expected}.` : "";
  if (study.Observations.length > 0 && !(await ask("Mevcut ölçümler seçilen videoya aynı zaman damgalarıyla bağlanacak. Aynı videoyu seçtiğinizden emin olun." +
      note + differs + " Devam edilsin mi?", "Videoyu yeniden bağla"))) return;
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
  setStatus(state.ready ? "Video hazır. İstediğiniz konuma gidip Başla’ya basın." : "Video süresi okunamadı.");
});
video.addEventListener("error", () => {
  if (!video.getAttribute("src")) return;
  state.ready = false;
  timeline.disabled = true;
  stopMeasure();
  setStatus("Video açılamadı. Dosyayı veya tarayıcının video biçimi desteğini kontrol edin (H.264 MP4 önerilir).");
});
video.addEventListener("ended", () => { pause(); updatePosition(); });
// Uygulama dışından gelen konum değişikliği (ör. medya tuşları) de ardışık ölçümü keser.
video.addEventListener("seeking", () => {
  if (state.internalSeek) return;
  if (state.anchor !== null) { stopMeasure(); setStatus("Konum değişti. Yeni konumda Başla’ya basın."); }
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
    setStatus("Konum değişti. Yeni konumda Başla’ya basın.");
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
    setStatus(`Konum ölçüm başlangıcında veya gerisinde (${fmt3(state.anchor / 1000)} sn). Ölç için ileri alın.`);
}

on("play", play);
on("pause", pause);
on("back", () => nudge(-100));
on("forward", () => nudge(100));

// ------------------------------------------------------------ ölçüm

function setStatus(text) { $("status").textContent = text; }

function stopMeasure() {
  state.anchor = null;
  $("measure").textContent = "Başla";
  $("measure").classList.remove("on");
}

function requireVideo() {
  if (!state.ready || !state.current) throw new Error("Ölçüm için önce bir video açın.");
}

async function measure() {
  requireVideo();
  const now = positionMs();
  if (state.anchor === null) {
    state.anchor = now;
    $("measure").textContent = "Ölç";
    $("measure").classList.add("on");
    setStatus(`Ölçüm başladı (${fmt3(now / 1000)} sn).`);
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
  setStatus(`Aralık başlangıcı alındı (${fmt3(state.selectedStart / 1000)} sn).`);
}

async function addMarked() {
  requireVideo();
  if (state.selectedStart === null) throw new Error("Önce aralık başlangıcını alın.");
  const saving = addObservation(state.selectedStart, positionMs());
  state.selectedStart = null;
  await saving;
}

function fillRow(row) {
  const name = $("step").value.trim();
  if (name.length === 0) throw new Error("Adım adı boş olamaz.");
  const cycle = parsePositiveInt($("cycle").value, "Çevrim");
  row.Step = name;
  row.Cycle = cycle;
  row.Classification = $("classification").value;
  row.Excluded = $("excluded").checked;
}

/** Doğrulama ve ekleme eşzamanlıdır (hata varsa hiçbir şey değişmez); kayıt sözü döner. */
function addObservation(start, end) {
  if (end <= start) throw new Error("Bitiş, başlangıçtan sonra olmalı.");
  const row = newObservation({ StartMs: start, EndMs: end });
  fillRow(row);
  if (state.current.Observations.some((x) => x.StartMs === start && x.EndMs === end && x.Cycle === row.Cycle && x.Step === row.Step && x.Excluded === row.Excluded))
    throw new Error("Bu ölçüm aynı adım ve çevrim için zaten kayıtlı.");
  state.current.Observations.push(row);
  refreshObservations();
  setStatus(`Kaydedildi · ${row.Step} · ${fmt3(seconds(row))} sn`);
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
  if (!(await ask("Seçili ölçüm silinsin mi?", "Ölçümü sil"))) return;
  state.current.Observations.splice(state.current.Observations.indexOf(row), 1);
  state.selectedRow = null;
  refreshObservations();
  await save();
});

function refreshObservations() {
  const rows = state.current ? [...state.current.Observations].sort((a, b) => a.StartMs - b.StartMs || a.EndMs - b.EndMs) : [];
  renderTable("observations", rows,
    (r) => [num(r.StartMs), num(r.EndMs), num(fmt3(seconds(r))), txt(r.Step), txt(r.Classification), txt(r.Excluded ? "Evet" : "Hayır"), num(r.Cycle)],
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
      empty: "Henüz ölçüm yok."
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
      action = () => { if (state.anchor !== null) { stopMeasure(); setStatus("Ölçüm durduruldu. Yeni kayıt eklenmedi."); } };
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
    Confidence: parseNumber($("confidence").value, "Güven"), TargetError: parseNumber($("target").value, "Hedef hata"),
    Pace: parseNumber($("pace").value, "Tempo"), Allowance: parseNumber($("allowance").value, "Tolerans"),
    RepeatedLow: $("method").value === "1", GroupMs: parsePositiveInt($("group-ms").value, "Grup hassasiyeti"),
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
      num(fmt3(r.Deviation)), num(fmt3(r.CriticalT)), num(fmt3(r.ErrorPercent)), txt(r.Sufficiency), num(fmt3(r.Base)),
      num(fmt3(r.Normal)), num(fmt3(r.Standard)), num(r.Every), num(fmt3(r.Contribution)), txt(r.Method)],
    {
      isSelected: (r) => r.Step === state.selectedResult,
      onSelect: (r) => { state.selectedResult = r.Step; $("every").value = String(r.Every); refreshAnalysis(); },
      empty: "Analiz için ölçüm yok."
    });
  if (selected && previous !== selected.Step) $("every").value = String(selected.Every);
  renderTable("cycles", analyzeCycles(study), (c) => [num(c.Cycle), num(fmt3(c.Elapsed)), num(fmt3(c.Workload))]);
  renderTable("groups", selected?.Groups ?? [], (g) => [num(fmt3(g.Seconds)), num(g.Count)]);
  $("analysis-status").textContent = `Toyota esinli rehber: ${study.Settings.Stable ? "5 gözlem (kararlı süreç). Tekrar eden düşük süreyi inceleyin." : "10 gözlem (genel). Ortalama ile başlayın."} ` +
    "İstatistiksel yeterlilik ayrı hesaplanır. Westinghouse puan tablosu doğrulanana kadar manuel tempo kullanılır.";
}

on("apply-settings", applySettings);

on("apply-frequency", async () => {
  if (!state.current || state.selectedResult === null) return;
  state.current.Frequency.set(state.selectedResult, parsePositiveInt($("every").value, "Sıklık"));
  await save();
  refreshAnalysis();
});

on("t-table", async () => {
  if (!state.current) return;
  const c = state.current.Settings.Confidence;
  const dfs = [...Array.from({ length: 30 }, (_, i) => i + 1), 40, 60, 120];
  const lines = dfs.map((df) => `${String(df).padStart(4)}    ${studentInv(df, (1 + c / 100) / 2).toLocaleString("tr-TR", { minimumFractionDigits: 4, maximumFractionDigits: 4 })}`);
  await showDialog("Bağıl hata = t × s / (√n × ortalama)", { pre: "  sd    Kritik t\n" + lines.join("\n"), title: `Student-t tablosu · %${fmtNum(c)}` });
});

on("excel", async () => {
  if (!state.current) return;
  await applySettings();
  const bytes = reportBytes(state.current);
  download(new Blob([bytes], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }), suggestedFileName(state.current, ".xlsx", "_analiz"));
});

// ------------------------------------------------------------ açılış

if (DESKTOP_URL) {
  $("desktop-link").href = DESKTOP_URL;
  $("desktop-link-wrap").hidden = false;
}
window.addEventListener("beforeunload", () => { for (const v of state.videos.values()) URL.revokeObjectURL(v.url); });
updateHeader();
guard(async () => {
  await reloadStudies();
  if (store.lastSkipped > 0)
    await showDialog(`${store.lastSkipped} etüt kaydı okunamadığı için listede gösterilmiyor. Diğer kayıtlar kullanılabilir.`);
});
