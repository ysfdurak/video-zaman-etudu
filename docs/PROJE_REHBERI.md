# Proje rehberi — Video Zaman Etüdü

Bu rehber kodu kendin yazmamış olsan bile projeyi anlatabilmen, soruları cevaplayabilmen ve değişiklik isteyebilmen için hazırlandı.

## 1. Tek cümlede
Kullanıcı videodaki iş adımlarının başlangıç/bitişini işaretler; program bu süreleri adım bazında istatistiğe, normal ve standart süreye çevirir, Excel'e yazar ve etüdü `.vzetud` dosyasıyla başka bilgisayara taşır. Sunucu yok; her şey kullanıcının bilgisayarında. İki sürüm var: Windows masaüstü (C#) ve tarayıcı (JavaScript); aynı kurallar, aynı paket biçimi.

## 2. Klasörler ve sorumluluklar

| Klasör / dosya | Ne yapar | Neden ayrı |
|---|---|---|
| `Core/Study.cs` | Veri modeli (`Study`, `Observation`, `Settings`) ve **tüm hesaplar** (`Analysis.Steps`, `Analysis.Cycles`) | Ekran ve Excel aynı hesabı kullansın (K09); Windows olmadan test edilebilsin (K01) |
| `Core/StudyTransfer.cs` | `.vzetud` paketini yazma/okuma, doğrulama, dosya adı önerisi | Taşıma kuralları (K04) tek yerde |
| `Desktop/MainWindow.xaml.cs` | Üç sekmeli WPF arayüzü, video oynatma, ölçüm düğmeleri, kısayollar | Arayüz kodu; hesap içermez |
| `Desktop/StudyStore.cs` | Yerel SQLite kaydı (`%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite`) | Her çalışan kendi profilinde |
| `Desktop/StudyExport.cs` | Excel çıktısı (ClosedXML); ilk sayfa Özet, başlıklar arayüz dilinde | |
| `Core/StudyRules.cs` | v2 kuralları: adım listesi, süreç sırası, temizleme, tolerans kalemleri, çevrim değişkenliği, karşılaştırma, gereken n, devam noktası, ölçüm sırası (`MeasureFlow`) | Tarayıcıdaki `core.js` v1.5–v1.7 işlevlerinin C# karşılığı; arayüzsüz test edilir |
| `Core/StudyBackup.cs` | Toplu yedek (.zip) yazma ve geri yükleme planı | Tarayıcı yedeğiyle aynı biçim |
| `Desktop/MainWindow*.cs` | Arayüz: `MainWindow.xaml.cs` (genel, dil, Çalışmalar), `.Measure.cs` (ölçüm), `.Analysis.cs` (analiz, grafik, karşılaştırma) | Dosya başına bir bölüm |
| `Desktop/Metinler.cs`, `Metinler.json` | TR/EN metinler, sayı/yüzde/saat biçimi; JSON tarayıcı sözlüğünden üretilir | İki sürüm aynı ifadeyi kullansın |
| `Desktop/Charts.cs`, `PrintReport.cs`, `Ui.cs` | WPF grafikleri, yazdırma belgesi, arayüz yapı taşları | |
| `Web/js/advanced.js`, `Core/Advanced.cs` | v1.8/v2.1 ileri analizler (aykırı değer, histogram, hat dengeleme, standart iş, iyileştirme, iş örneklemesi, adım karesi) | Saf işlevler, iki tarafta aynı |
| `Desktop/MainWindow.Sampling.cs`, `MainWindow.Advanced.cs` | Örnekleme paneli ve ileri analiz bölümleri (Windows) | |
| `Desktop/SelfTest.cs` | `--self-test`: CI'da pencereyi iki dilde kurup yolları dener, ekran görüntüsü alır | WPF'i elle açmadan denetlemek için |
| `Tools/Metinler/uret.mjs`, `Tools/EkAlanlar/uret.mjs` | Metin dosyasını ve v2 ortak eşdeğerlik dosyasını üretir | Tek kaynak: tarayıcı |
| `Checks/` | Otomatik kontroller (kabul kriterleri + 58 ek kontrol) | GitHub Actions her gönderimde çalıştırır |
| `.github/workflows/windows.yml` | Windows'ta derle → kontrol et → taşınabilir ZIP üret | Son kullanıcı SDK kurmasın |
| `demo/` | Sentetik video ve etüt (gerçek şirket verisi yok) | |
| `Web/js/core.js` | Tarayıcı çekirdeği: `Core/Study.cs` + `StudyTransfer.cs` karşılığı (hesaplar, t-dağılımı, paket) | Aynı kuralların JavaScript hâli |
| `Web/js/strict-json.js` | Masaüstünün JSON kurallarını izleyen okuyucu | `JSON.parse` `1000` ile `1000.0`'ı ayırt edemez |
| `Web/js/app.js`, `index.html`, `app.css` | Tarayıcı arayüzü (`MainWindow` karşılığı) | |
| `Web/js/store.js` | IndexedDB kaydı (`StudyStore` karşılığı) | |
| `Web/js/report.js`, `xlsx.js` | Excel çıktısı (`StudyExport` karşılığı), kütüphanesiz | Siteye dış bağımlılık girmesin |
| `Web/js/backup.js` | Toplu yedek: bütün etütleri tek .zip'e yazar, .zip'ten geri yükler (v1.7) | Paket kuralları core.js'te kalsın; burası yalnız zip ve karşılaştırma |
| `Web/js/charts.js` | SVG grafikler (adım katkısı, çevrim süresi) | |
| `Shared/esdeger-ornekler.json` | İki sürümün aynı sonucu verdiğini sınayan ortak örnekler | C# üretir, iki taraf da test eder |
| `Tools/EsdegerUretici` | Ortak örnek dosyasını C# çekirdeğinden yeniden üretir | |

## 3. Veri akışı
1. **Yeni etüt** → `Study` nesnesi oluşur → `StudyStore.Save` JSON olarak SQLite'a yazar.
2. **Ölçüm** → `MediaElement.Position` (video zamanı, ms) okunur. Başla = çapa; Ölç = `[çapa, şimdi]` aralığı kaydedilir, çapa = şimdi. Gerçek saat kullanılmadığı için duraklatma ve yavaş oynatma süreyi bozmaz (K02).
3. Her değişiklikte etüt yeniden kaydedilir (`Save`).
4. **Analiz** sekmesine geçince `Analysis.Steps/Cycles` yeniden hesaplanır; Excel de aynı fonksiyonları çağırır.
5. **Dışa aktarım** yalnız adı, kodu, video **adını**, ayarları, ölçümleri ve sıklıkları yazar; yerel kimlik ve klasör yolu yazmaz. İçe alımda her seferinde yeni kimlik verilir.

## 3a. Tarayıcı sürümünde dil (TR/EN)
- Tüm arayüz metinleri `Web/js/i18n.js` sözlüğünde, anahtar → metin olarak durur. HTML'de `data-i18n="anahtar"` işaretli öğeler dil değişince yeniden yazılır; JavaScript mesajları `t("anahtar", {parametre})` ile alınır.
- Hesap çekirdeği (`core.js`) Türkçe sonuç metni üretir (“Yeterli”, “Ortalama”…), çünkü masaüstüyle eşdeğerlik testleri bu metinleri karşılaştırır. Ekranda ve Excel'de `result()` ile çevrilir. Çekirdek hataları Türkçe mesajla birlikte bir çeviri anahtarı taşır.
- Dil sırası: adres parametresi (`?lang=en`) → kaydedilmiş seçim → tarayıcı dili. Sitedeki İngilizce proje sayfası “Try it” bağlantısına `?lang=en` ekler.
- Yeni metin eklerken iki dile de yaz; `npm test` iki sözlüğün anahtarları eşit değilse kırmızı olur.
- Windows sürümü (v2.0) aynı sözlüğü kullanır: `Tools/Metinler/uret.mjs`, `i18n.js`'deki `DICT` ve `RESULTS`'u alır, masaüstüne özgü birkaç metni (`DESKTOP`) üstüne yazar ve `Desktop/Metinler.json`'u üretir. Bu dosya exe'ye gömülür (`L` sınıfı okur). Sözlüğü değiştirip üretmeyi unutursan `npm test` kırmızı olur.
- Windows'ta dil değişince pencere yeni dilde baştan kurulur (arayüz kodla kurulduğu için en sade yol); açık etüt ve bölüm korunur. Çekirdek hataları `Exception.Data["key"]` ile çeviri anahtarı taşır.

## 3b. İki sürüm nasıl aynı kalıyor?
- Hesap kuralları iki yerde yazılı: `Core/Study.cs` (C#) ve `Web/js/core.js` (JavaScript). Tek kaynak yerine iki uygulama seçildi, çünkü tarayıcı sürümü sunucusuz ve hafif olmalı (C#'ı tarayıcıda çalıştırmak 5–10 MB indirme demek).
- Kopukluğu önleyen şey ortak örnek dosyası: C# üretir, her iki taraf her gönderimde ona karşı sınanır. Birinde kural değişip diğerinde değişmezse GitHub Actions kırmızı olur.
- t değeri: masaüstü MathNet kütüphanesini, tarayıcı kendi yazdığımız ters t fonksiyonunu kullanır; tarayıcının değeri daha doğru (scipy ile fark 10⁻¹² mertebesinde); MathNet uç kuyrukta (ör. %99,9 güven, sd 1) ~10⁻⁹ sapar. Test toleransı t'ye bağlı değerlerde 10⁻⁷, diğerlerinde 10⁻⁹; ekranda t 3 ondalıkla gösterilir.
- Bilinçli farklar: tarayıcıda video yolu yok (yalnız dosya adı), etütler tarayıcıya özel, sıralama Türkçe alfabe (masaüstünde Windows bölge ayarı).

## 3c. Tarayıcıda ölçüm akışı (v1.5)

- **Adım listesi** (`study.Steps`, `[{Name, Classification}]`): kayıtların adı ve sınıfı buradan gelir. Eski etütlerde liste yoksa `deriveSteps` ölçümlerden çıkarır (ilk görülme sırası, en sık sınıf).
- **Ölç:** `state.anchor` ölçümün başlangıcıdır. Ölç, başlangıçtan o anki konuma kadar kayıt ekler ve başlangıcı oraya taşır. Konum değiştirmek ölçümü kesmez; konum başlangıcın gerisindeyse kayıt eklenmez (`msg.behindStart`).
- **Sıra:** normal kayıttan sonra sıradaki adıma geçilir, son adımdan sonra çevrim artar (`advance`). Elle başka adıma geçince önceki adım `state.resume`'da tutulur; kapsam dışı kayıttan sonra oraya dönülür (`afterRecord`).
- **Geri al:** bu oturumda eklenen kayıtlar `state.undo` yığınındadır. Geri almak kaydı siler; adım, çevrim ve ölçüm başlangıcını o kaydın öncesine getirir.
- **Süreç sırası:** `analyzeSteps` Windows ile aynı kalsın diye alfabetik döner. Ekranda ve Özet sayfasında `inProcessOrder` ile adım listesinin sırasına dizilir.
- **Windows:** v2.0'dan beri aynı akış. Sıra mantığı `Core/StudyRules.cs` içindeki `MeasureFlow` sınıfındadır ve `Checks/V2Checks.cs` ile arayüzsüz sınanır. Eski Windows v1.2 bu alanları yok sayar; o sürümde zaman çizgisinde atlamak ölçümü keserdi (K02/K10).

## 3d. Etüt bilgileri ve ekler (tarayıcı v1.6, Windows v2.0)

| Alan | Nerede | Ne işe yarar |
|---|---|---|
| `Steps[].Note` | Adım araçları → Bitiş anı / açıklama | Gözlemcinin Ölç'e ne zaman basacağını tanımlar |
| `Observations[].Note` | Seçili kayıt → Not | Kapsam dışı ya da olağan dışı kaydın nedeni |
| `StepPace` (adım → %) | Analiz → Seçili adım | O adımın normal süresi bu tempoyla hesaplanır (`analyzeSteps` içinde) |
| `Takt` (sn) | Analiz ayarları | Özette takt doluluğu ve gereken operatör = standart ÷ takt |
| `Info` | Etüt bilgileri | Operatör, istasyon, vardiya, tarih, gözlemci, not |

Hepsi pakette isteğe bağlıdır; bozuk değerler okunurken atılır (`cleanStepPace`, `cleanTakt`, `cleanSteps`, `cleanInfo`; C#'ta `StudyRules.Clean*`). Windows v2.0 hepsini okur ve yazar. Yalnız eski Windows v1.2 bu alanları yok sayar; adım temposu kullanılmış bir etüt orada genel tempoyla hesaplanır.

## 3e. Tolerans kalemleri, yedek ve devam (tarayıcı v1.7, Windows v2.0)

- **Tolerans kalemleri** (`AllowanceParts`: `Personal`, `Fatigue`, `Delay`, `Other`): Analiz ayarlarında "Toleransı kalemlere ayır" altında girilir. Kalem varsa `Settings.Allowance` = kalemlerin toplamıdır ve toplam alanı kilitlenir. Hesap yalnız toplamı kullanır. Bu yüzden eski Windows v1.2 de aynı sonucu bulur. Orada tolerans değiştirilirse toplam tutmaz; paket açılırken kalemler atılır (`matchingParts` / `StudyRules.MatchingParts`).
- **Toplu yedek:** "Tümünü yedekle (.zip)" her etüdü ayrı `.vzetud` olarak tek zip'e koyar. Geri yükleme için ayrı düğme yok: "Etüdü içe al…" dosyanın zip olduğunu ilk baytlardan anlar (`isZip`). Aynı içerikteki etüt ikinci kez eklenmez: karşılaştırma, iki etüdün paket metninin aynı olmasıdır. Hiçbir mevcut etüt değiştirilmez ya da silinmez. Windows v2.0 aynı yedeği yazar ve okur (`Core/StudyBackup.cs`, .NET'in kendi zip desteği); iki sürüm birbirinin yedeğini açar.
- **Kaldığı yerden devam:** Etüt açılınca video son kaydın bitişine gider. Sıradaki kayıt, son geçerli kaydın ardındaki adım olur; son adımdan sonra çevrim 1 artar (`resumePoint`).
- **Kayıt süzgeci:** Ölçüm tablosu adıma ve çevrime göre daraltılabilir. Yalnız görünümü etkiler.
- **Çıktılar:** Excel özeti "yetersiz" ve "hesaplanamayan" adımları ayrı sayar. Yetersiz adıma "Gereken n (yaklaşık)" yazılır; bu, ekrandaki "yaklaşık N gözlem gerekir" ile aynı hesaptır (`requiredObservations`). Yazdırma başlığında etüt bilgileri ve analizin dayandığı ayarlar var. Başlık Ctrl+P ile de dolar (`beforeprint`).

## 3f. İleri analizler (tarayıcı v1.8, Windows v2.1)

Hesaplar `Web/js/advanced.js` ve `Core/Advanced.cs` içindedir. `Shared/ek-alanlar.json` iki tarafın aynı sonucu verdiğini denetler.

| Özellik | Kural | Nerede girilir |
|---|---|---|
| Kayıt temposu (`Observation.Rating`) | Puan varsa adım temposu = Σ(süre × puan) ÷ Σ süre (ortalama yöntemi) ya da puanların ortalaması (tekrar yöntemi). Puansız kayıt adım/genel tempoyu alır. | Ölçüm: "Tempo puanı %" (sonraki kayıtlara); kayıt düzenleyici |
| Aykırı değer | IQR: Q1 − 1,5·IQR altı ya da Q3 + 1,5·IQR üstü. Yüzdelik doğrusal ara değerli (Excel/R tip 7). En az 4 geçerli kayıt. | Analiz → Dağılım: "Aykırıları kapsam dışı bırak" |
| Histogram | √n sınıf (3–10), eşit genişlik | Analiz, seçili adım |
| Hat dengeleme | Yük = katkılar toplamı. Denge verimi = Σ atanmış yük ÷ (istasyon sayısı × en büyük yük). En az istasyon = ⌈toplam ÷ takt⌉. Otomatik dağıtım: sırayla doldur, yük + adım > takt ise yeni istasyon. | İyileştirme → Hat dengeleme: İstasyon sütunu / otomatik dağıt |
| Standart iş | El işi, yürüme ve bekleme ardışık; makine işi başladığı andan paralel. Süre = katkı. Tür boşsa el işi. | Hazırlık: adım tablosu → Tür |
| İyileştirme | İsraf türü (7 israf) ya da fikri olan adımlar + NVA adımlar | Hazırlık: adım tablosu → İsraf, fikir |
| Adım kartı | Kare = `StepDef.FrameMs`, yoksa ilk geçerli kaydın ortası. Tarayıcı ayrı bir `<video>`'dan canvas'a, Windows `MediaPlayer` + `DrawingVisual` ile çizer. | Ölçüm → Gelişmiş: "Bu kareyi adım görseli yap" |
| İş örneklemesi | p = sayı ÷ n; aralık p ± z·√(p(1−p)/n); gereken n = ⌈z²·p(1−p) ÷ e²⌉, e = hedef mutlak hata (varsayılan 5 puan). z normal dağılımdan (tarayıcı kendi ters normali, Windows MathNet). | Yeni etüt → Etüt türü: İş örneklemesi |

- **Sık sorulabilecek: Neden IQR, ±2σ değil?** IQR, aykırının kendisinden etkilenmez; az kayıtta (4–10) σ'ya dayalı kural aykırıyı gizler.
- **Yamazumi neden katkı kullanıyor?** Seyrek adımlar (her N çevrimde bir) takta karşı çevrim başına yükleriyle karşılaştırılmalı.
- **İş örneklemesinde neden rastgele an?** Gözlem anı önceden bilinmezse davranış değişmez ve sonuç yanlı olmaz. Düzenli aralık (Her X sn) yalnız iş döngüsüyle çakışmıyorsa kullanılmalı.

## 3g. Sade düzen (tarayıcı v1.9, Windows v2.2)

**Neden?** v1.8/v2.1 ile ölçüm ekranında adım tanımlama araçları, analizde dokuz bölüm vardı (yaklaşık üç ekran kaydırma). Kullanıcı neyin nerede olduğunu aramak zorunda kalıyordu. Kural şu oldu: **her sekme işin bir aşaması; bir aşamada yalnız o aşamanın işi**.

| Sekme | Ne yapılır | Kod (tarayıcı / Windows) |
|---|---|---|
| 1 Çalışmalar | Yeni etüt, aç, içe/dışa al, yedek, karşılaştır, durum sütunu | `app.js` başı / `MainWindow.xaml.cs` |
| 2 Hazırlık | Adım tablosu, etüdün kapsamı, etüt bilgileri, başka etütten adım | `renderStepTable` / `MainWindow.Setup.cs` |
| 3 Ölçüm | Video, adım düğmeleri, Başla/Ölç, kayıt tablosu, toplu düzenleme, Gelişmiş | `app.js` ölçüm / `MainWindow.Measure.cs` |
| 4 Analiz | Durum ve onay, Çıktı al, katlı ayarlar, alt sekmeler | `refreshAnalysis` / `MainWindow.Analysis.cs` |
| 5 İyileştirme | Seçili yalın araçlar, her biri bir alt sekme | `renderAdvanced` / `MainWindow.Advanced.cs` |

**Kapsam (modüller):**
- `Study.Modules` dört değerden oluşan bir listedir: `Balance`, `Work`, `Ideas`, `Cards`.
- Alan **yoksa** (`null`, eski etüt) bütün araçlar açık sayılır. Böylece eski kullanıcı hiçbir şey kaybetmez.
- **Boş liste** "yalın araç yok" demektir ve pakete `"Modules": []` olarak yazılır. Bu yüzden `null` ile `[]` ayrı tutulur.
- Kapalı aracın sekmesi, tablo sütunu ve Excel sayfası çıkmaz. Hesaplar değişmez; yalnız görünürlük değişir.
- Tarayıcıda gövdeye `no-mod-X` sınıfı eklenir ve `.mod-X` öğeleri gizlenir. Windows'ta `ApplyModules()` aynı işi yapar.

**Adım kopyalama:** `copySteps` / `StudyRules.CopySteps`. Adım tanımlarını alır, `FrameMs` gelmez: kare öteki videonun zamanıdır, bu videoda anlamsız olur. "Değiştir" seçilirse ölçülmüş ama kaynakta olmayan adımlar sona eklenir; böylece kayıtlar sahipsiz kalmaz.

**Durum:**
- `Status` üç değer alır: `""` (taslak), `done`, `approved`.
- `ApprovedBy` ve `ApprovedAt` yalnız onaylı etütte yazılır. Durum değişince silinir.
- Onaylı etüt kilitlenmez. Ölçüm ekranında not çıkar; değişiklik yapan kişi durumu kendisi günceller. Kilit, sahada küçük bir düzeltmeyi bile engellerdi; bu yüzden tercih edilmedi.

**Toplu düzenleme:** Boş alan "değişmez" demektir. Seçim yalnız görünen kayıtlarda kalır: süzgeç değişince gizlenen kayıt seçimden çıkar, yanlışlıkla değişmez.
- Tarayıcıda satır başında işaret kutusu vardır.
- Windows'ta Ctrl/Shift ile çoklu seçim yapılır; bu, platformun alışılmış yoludur.

**Kısa tur:**
- Tarayıcıda `localStorage` (`vze-tour-seen`) ile bir kez gösterilir.
- Windows'ta `%LOCALAPPDATA%\VideoZamanEtudu\tur-goruldu` dosyasıyla bir kez gösterilir.
- Öz-denemede tur açılmaz. Tarayıcı testleri turu ilk adımda kapatır.

- **Sık sorulabilecek: Neden araçlar varsayılan kapalı?** Çoğu etüt yalnız standart süre içindir. Yeni kullanıcı önce sade ekranı görür; ihtiyaç olunca araç tek tıkla açılır.
- **Neden Hazırlık ayrı sekme?** Adım tanımları ölçümden önce ve bir kez yapılır. Ölçüm sırasında araçlar dikkati dağıtıyor ve yanlışlıkla değiştiriliyordu. Ölçerken beklenmeyen bir adım için hızlı "Adım ekle" kutusu Ölçüm'de kaldı.

## 4. Formüller (sorulursa)
- Süre (sn) = (bitiş ms − başlangıç ms) / 1000
- Bağıl hata % = t(güven, n−1, iki taraflı) × s ÷ (√n × ortalama) × 100 → hedefe eşit/küçükse “Yeterli”; n < 2 ise “Hesaplanamıyor”.
- Tekrar yöntemi: süreleri grup hassasiyetine (varsayılan 100 ms) yuvarla → en kalabalık grup (eşitlikte ortalaması düşük olan) → baz = o grubun **ham** ortalaması. En büyük grup 2'den azsa baz boş.
- Normal = baz × tempo / 100; Standart = normal / (1 − tolerans / 100); seyrek katkı = standart / N.
- Çevrim geçen süresi = aralıkların **birleşimi** (çakışma bir kez, boşluk hiç sayılmaz); iş yükü = sürelerin toplamı.
- Demo örneği: Toplama 5, 6, 5 sn → ortalama 5,333 → normal 5,333 × 1,10 = 5,867 → standart 5,867 / 0,85 = 6,902.

## 5. Sık sorulabilecek sorular
- **Neden video pakete girmiyor?** Boyut ve gizlilik: video şirket içi onaylı kanaldan ayrıca paylaşılır, paket sadece ölçümleri taşır.
- **Kapsam dışı ile NVA farkı?** NVA bir sınıf (analizi değiştirmez); kapsam dışı ise ölçümü istatistikten ve çevrimden çıkarır ama ham listede tutar.
- **Toyota 5/10 gözlem neden “yeterli” demiyor?** Bu pratik bir rehber; istatistiksel yeterlilik ayrı olarak t-dağılımıyla hesaplanır (K06).
- **Westinghouse neden yok?** Doğrulanmış puan tablosu yok; V1 manuel tempo kullanır (K07).
- **Ok tuşları ölçümü neden kesmiyor?** Adım sınırını bulmak için en fazla 1 sn'lik ince düzeltme amaçlı (K10). Zaman çizgisinde atlama ve satır seçimi kesmeye devam eder (K02).
- **Bozuk bir kayıt olursa?** Liste o kaydı atlar ve uyarır; diğer etütler açılır.
- **Tarayıcı sürümü videoyu sunucuya yüklüyor mu?** Hayır. Sayfa yalnızca kendi dosyalarını yükler; güvenlik politikası (CSP) dış bağlantıyı engeller. Video tarayıcı belleğinde açılır.
- **İngilizce demo neden ayrı dosya?** Adım adları veridir, arayüz çevirisiyle değişmez. Bu yüzden aynı zamanlar ve ayarlarla İngilizce adlı ikinci bir paket ve İngilizce yazılı video üretildi (`Tools/DemoVideo`); testler iki demonun aynı sonucu verdiğini denetler.
- **Demo düğmesi videoyu sunucudan mı alıyor?** Sitenin kendi statik dosyasından (`Web/demo/`); dış bir sunucuya gidilmez, ziyaretçinin hiçbir verisi gönderilmez.
- **Tarayıcı sürümünde etütler nerede?** O tarayıcının yerel deposunda (IndexedDB). Başka bilgisayara veya tarayıcıya `.vzetud` ile taşınır. Tarayıcı verisi temizlenirse silinir.
- **Kullanım limiti / hesap neden yok?** Limit ve kullanıcı hesabı sunucu ister; bu V1.2 sunucusuz. Site hesap sistemi VPS'te kurulunca eklenir.
- **Veriler nerede, nasıl yedeklenir?** Windows: `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite`. İki sürümde de "Tümünü yedekle (.zip)" ve geri yüklemek için "Etüdü içe al…".
- **WPF arayüzü Linux'ta nasıl denendi?** Denenmedi; Windows'ta GitHub Actions denedi. `Desktop.exe --self-test` pencereyi iki dilde gerçekten kurar, düğmelerin arkasındaki yolları çalıştırır, sonuçları denetler ve ekran görüntüsü alır. Görüntüler iş günlüğüne de base64 olarak yazılır.
- **Tolerans neden toplanıyor, çarpılmıyor?** Kalemler aynı normal süreye eklenen paylardır; uygulamada yaygın olan toplamdır. Standart = Normal ÷ (1 − toplam tolerans).

## 6. Değişiklik yaparken
- Hesap değişikliği → `Core/Study.cs` + `Checks/` içine kontrol ekle. Ekranda ve Excel'de otomatik yansır.
- Paket alanı eklemek → `StudyTransfer.Package` + `Validate`; eski dosyalar açılmayacaksa `Version` artır.
- Yerelde test: `dotnet run --project .\Checks\Checks.csproj -c Release -- .\demo\sentetik-depo-etudu.vzetud`
- Hesap veya paket kuralı değişikliği → **iki tarafı da** değiştir (`Core/*.cs` ve `Web/js/core.js`), `Tools/EsdegerUretici` ile örnek dosyasını yeniden üret, `Checks` ve `cd Web && npm test` çalıştır.
- Arayüz değişikliği → iki arayüzde de yap. Yeni metni `i18n.js`'ye ekle ve `node Tools/Metinler/uret.mjs` çalıştır. Yeni paket alanı ya da kural → `Tools/EkAlanlar/uret.mjs`'ye ekle, `Shared/ek-alanlar.json`'u yeniden üret ve `Checks/V2Checks.cs`'te C# karşılığını denetle. Son olarak Windows'ta ve tarayıcıda [kontrol listesi](WINDOWS_KONTROL_LISTESI.md) ile elle doğrula.

## 6b. Yayın nasıl işliyor?
- Kod: github.com/ysfdurak/video-zaman-etudu (herkese açık; demo verisi sentetik).
- Tarayıcı sürümü: Cloudflare Workers `video-zaman-etudu` projesi depoya bağlı; `main`'e gönderim → otomatik yayın → zaman-etudu.ysfdurak.com. Sunucu kodu yok, sadece statik dosyalar (`wrangler.jsonc`).
- Windows sürümü: `vX.Y.Z` etiketi → Actions derler, kontrol eder, ZIP'i üretir ve GitHub Release oluşturur. Sitedeki indirme bağlantısı her zaman son sürüme gider.
- Yeni sürüm adımları: `RELEASE_NOTES.md` başına yeni bölüm → `Desktop.csproj` ve `Web/js/core.js` sürüm numarası → gönder → Actions yeşil → etiket.

## 7. Bilinen sınırlar
- Ölçüm hassasiyeti Windows'un video oynatıcısına (MediaElement) bağlıdır; kare hızını program bilmez, Ctrl+ok 0,04 sn sabit adımdır.
- Codec: H.264 MP4 önerilir. Windows “N” sürümlerinde Media Feature Pack gerekir.
- Birlikte düzenleme ve merkezi kayıt V1 kapsamında yok (K01).
- Paket imzasız; şirket dağıtımı için kod imzalama BT sürecine bağlıdır.
