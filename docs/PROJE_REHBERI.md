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
| `Desktop/StudyExport.cs` | Excel çıktısı (ClosedXML) | |
| `Checks/` | Otomatik kontroller (kabul kriterleri + 58 ek kontrol) | GitHub Actions her gönderimde çalıştırır |
| `.github/workflows/windows.yml` | Windows'ta derle → kontrol et → taşınabilir ZIP üret | Son kullanıcı SDK kurmasın |
| `demo/` | Sentetik video ve etüt (gerçek şirket verisi yok) | |
| `Web/js/core.js` | Tarayıcı çekirdeği: `Core/Study.cs` + `StudyTransfer.cs` karşılığı (hesaplar, t-dağılımı, paket) | Aynı kuralların JavaScript hâli |
| `Web/js/strict-json.js` | Masaüstünün JSON kurallarını izleyen okuyucu | `JSON.parse` `1000` ile `1000.0`'ı ayırt edemez |
| `Web/js/app.js`, `index.html`, `app.css` | Tarayıcı arayüzü (`MainWindow` karşılığı) | |
| `Web/js/store.js` | IndexedDB kaydı (`StudyStore` karşılığı) | |
| `Web/js/report.js`, `xlsx.js` | Excel çıktısı (`StudyExport` karşılığı), kütüphanesiz | Siteye dış bağımlılık girmesin |
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
- Sık sorulabilecek: “Neden Windows sürümü İngilizce değil?” → Önce ziyaretçinin denediği tarayıcı sürümü çevrildi; masaüstü şirket içi kullanım için Türkçe kaldı, gerekirse aynı sözlük mantığıyla (WPF kaynak dosyaları) çevrilebilir.

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
- **Windows farkı:** Windows sürümünde adım adı serbest metindir ve zaman çizgisinde atlamak ölçümü keser (K02/K10). Tarayıcıda bu kural v1.5'te kaldırıldı. Paketteki `Steps` ve `Info` alanlarını Windows yok sayar; Windows'tan dışa aktarılan pakette bu alanlar olmaz.

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
- **Veriler nerede, nasıl yedeklenir?** `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite`; uygulama kapalıyken kopyala ya da etütleri `.vzetud` olarak dışa aktar.

## 6. Değişiklik yaparken
- Hesap değişikliği → `Core/Study.cs` + `Checks/` içine kontrol ekle. Ekranda ve Excel'de otomatik yansır.
- Paket alanı eklemek → `StudyTransfer.Package` + `Validate`; eski dosyalar açılmayacaksa `Version` artır.
- Yerelde test: `dotnet run --project .\Checks\Checks.csproj -c Release -- .\demo\sentetik-depo-etudu.vzetud`
- Hesap veya paket kuralı değişikliği → **iki tarafı da** değiştir (`Core/*.cs` ve `Web/js/core.js`), `Tools/EsdegerUretici` ile örnek dosyasını yeniden üret, `Checks` ve `cd Web && npm test` çalıştır.
- Arayüz değişikliği → iki arayüzde de yap; Windows'ta ve tarayıcıda [kontrol listesi](WINDOWS_KONTROL_LISTESI.md) ile elle doğrula.

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
