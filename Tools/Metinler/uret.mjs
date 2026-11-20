// Desktop/Metinler.json üretir: masaüstü (WPF) arayüz metinleri tarayıcı sözlüğünden (Web/js/i18n.js) gelir,
// böylece iki sürüm aynı ifadeleri kullanır. Masaüstüne özgü metinler ve farklılıklar aşağıdaki DESKTOP içindedir.
// Çalıştırma: node Tools/Metinler/uret.mjs   (Web/tests/metinler.test.mjs dosyanın güncel olduğunu denetler)
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { DICT, RESULTS } from "../../Web/js/i18n.js";

/** Masaüstünde farklı olan ya da yalnız masaüstünde kullanılan metinler (iki dilde aynı anahtarlar). */
export const DESKTOP = {
  tr: {
    "edition": "Windows sürümü · Video bilgisayarınızdan çıkmaz",
    "home.note": "Etütler bu bilgisayarda, Windows kullanıcınıza ait yerel kayıtta saklanır. Düzenli olarak <b>Tümünü yedekle</b> ile .zip yedeği alın; yedek tarayıcı sürümünde de açılır.",
    "status.videoError": "Video açılamadı. Dosyayı veya Windows video codec desteğini kontrol edin (H.264 MP4 önerilir).",
    "msg.deleteConfirm": "“{name}” ({code}) etüdü ve tüm ölçümleri bu bilgisayardan silinecek. Video dosyası silinmez. Bu işlem geri alınamaz; gerekirse önce dışa aktarın. Silinsin mi?",
    "x.s.edition": "masaüstü",
    "msg.demoMissing": "Demo dosyaları bulunamadı. Uygulama klasöründeki demo klasörünü kontrol edin: {path}",
    "tour.1.text": "Yeni etüt başlatın ya da kayıtlı bir etüdü açın. Denemek için “Demo etüdü aç” hazır ölçümlerle gelir. Etütler bu bilgisayarda saklanır; ara sıra Tümünü yedekle ile yedek alın.",
    "tour.3.text": "Videoyu oynatın; ilk adım başlarken bir kez Başla, sonra her adım bittiğinde Enter. Sıradaki adım ve çevrim kendiliğinden ilerler. Kayıtları tablodan tek tek ya da Ctrl/Shift ile birden çok seçip toplu düzeltirsiniz.",
    "bulk.hint": "Ctrl veya Shift ile birden çok kayıt seçin. Boş bırakılan alan değişmez.",
    "d.minus": "−", "d.plus": "+",
    "d.filter.package": "Etüt paketi veya yedek (.vzetud, .zip)",
    "d.filter.vzetud": "Video İş Etüdü paketi",
    "d.filter.zip": "Yedek (.zip)",
    "d.filter.excel": "Excel çalışma kitabı",
    "d.filter.all": "Tüm dosyalar"
  },
  en: {
    "edition": "Windows version · Your video never leaves your computer",
    "home.note": "Studies are stored on this computer, in a local file for your Windows user. Take a .zip backup regularly with <b>Back up all</b>; the backup also opens in the browser version.",
    "status.videoError": "The video could not be opened. Check the file or Windows video codec support (H.264 MP4 recommended).",
    "msg.deleteConfirm": "“{name}” ({code}) and all its measurements will be deleted from this computer. The video file is not deleted. This cannot be undone; export it first if needed. Delete?",
    "x.s.edition": "desktop",
    "msg.demoMissing": "Demo files not found. Check the demo folder in the application folder: {path}",
    "tour.1.text": "Start a new study or open a saved one. To try it out, “Open demo study” comes with ready measurements. Studies are stored on this computer; take a backup with Back up all now and then.",
    "tour.3.text": "Play the video; press Start once when the first step begins, then Enter whenever a step ends. The next step and cycle advance automatically. Fix records one by one in the table, or select several with Ctrl/Shift and change them together.",
    "bulk.hint": "Select several records with Ctrl or Shift. Fields left empty stay unchanged.",
    "d.minus": "−", "d.plus": "+",
    "d.filter.package": "Study package or backup (.vzetud, .zip)",
    "d.filter.vzetud": "Video Work Study package",
    "d.filter.zip": "Backup (.zip)",
    "d.filter.excel": "Excel workbook",
    "d.filter.all": "All files"
  }
};

export function build() {
  const out = { note: "Tools/Metinler/uret.mjs üretir; elle düzenlemeyin. Kaynak: Web/js/i18n.js + masaüstü farkları.", tr: {}, en: {}, results: {} };
  for (const lang of ["tr", "en"]) {
    const merged = { ...DICT[lang], ...DESKTOP[lang] };
    for (const key of Object.keys(merged).sort()) out[lang][key] = merged[key];
  }
  for (const key of Object.keys(RESULTS)) out.results[key] = RESULTS[key];
  return out;
}

export const serialize = (data) => JSON.stringify(data, null, 1) + "\n";

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  writeFileSync(new URL("../../Desktop/Metinler.json", import.meta.url), serialize(build()));
  console.log("Desktop/Metinler.json yazıldı.");
}
