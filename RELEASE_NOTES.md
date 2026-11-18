# v1.9.3 (tarayıcı) · Video Zaman Etüdü — ilk giriş ve dokunma hedefleri

- **İlk giriş:** Hiç etüdü olmayan kullanıcı iki net seçenekle karşılanır: **Hazır sonucu gör** (sentetik depo etüdü açılır ve doğrudan Analiz sekmesine geçer) ve **Kendin ölç** (yeni etüt formuna götürür). Kısa tur artık yalnızca kayıtlı etüdü olana kendiliğinden açılır; her zaman üstteki "Kısa tur" ile açılabilir.
- **Dokunma hedefleri:** Düğme ve seçim kutuları en az 36 px; dokunmatik ekranda en az 44 px.
- **Windows sürümü:** Değişiklik yok (metin sözlüğü yeni anahtarlarla yeniden üretildi).

**Uyumluluk:** Paket biçimi ve hesaplar değişmedi.

# v1.9.2 (tarayıcı) · Video Zaman Etüdü — çevrimdışı çalışma

Tarayıcı sürümü artık internet olmadan da tamamen çalışıyor. Sayfa bir kez açıldıktan sonra bağlantı kesilse ya da internet hiç olmasa da etüt açılır, ölçülür, analiz edilir ve Excel'e aktarılır. Demo etüdü de çevrimdışı açılır.

- **Nasıl:** Tarayıcı, uygulamanın kendi dosyalarını (sayfa, kod, demolar) kendi önbelleğinde saklar. İnternet varken her açılışta en yeni sürüm alınır; yoksa önbellekteki kullanılır.
- **Gizlilik:** Hiçbir şey değişmedi ve bu, kendi gözünle sınanabilir hale geldi: interneti kapat, uygulama yine çalışır. Video ve etütler zaten bilgisayarda; önbelleğe yalnız sitenin kendi dosyaları girer. Sayfanın güvenlik politikası (`connect-src 'self'`) dış adreslere bağlantıyı engellemeye devam ediyor.
- **Windows sürümü:** Zaten internet gerektirmiyor; değişiklik yok.

**Uyumluluk:** Paket biçimi ve hesaplar değişmedi.

**Testler**
- Node: yeni `sw.test.mjs` (önbellek sürümü uygulama sürümüyle aynı, önbellek listesi bütün dosyaları kapsıyor, dış adres yok).
- Chrome: sayfa açıldıktan sonra ağ kesilip yeniden yüklendiğinde uygulama ve demo açılıyor.

# v2.2.2 (Windows) · v1.9.1 (tarayıcı) · Video Zaman Etüdü — kullanım kolaylıkları

Son gözden geçirmede bulunan sorunlar düzeltildi ve ölçümü hızlandıran küçük eklemeler yapıldı. Hepsi tarayıcıda ve Windows'ta, Türkçe ve İngilizce.

**Düzeltmeler**
- **Yeterlilik kartı:** Hiçbir adımın yeterliliği hesaplanamıyorken (adım başına tek ölçüm) özet kartı "Tüm adımlar yeterli" diyordu. Artık "Henüz karar verilemiyor" diyor.
- **Grafik ölçeği (tarayıcı):** Az adımlı grafikler sayfa genişliğine büyütülüyor, yazılar şişiyordu. Grafik artık çizildiği boyuttan büyümüyor; yazdırmada da öyle.
- **Boş kutuda Enter:** "Yeni adım" kutusu boşken Enter'a basınca hata penceresi çıkıyordu. Artık sessizce yok sayılıyor.
- **Adım ekleme sırası:** Henüz ölçüm yokken arka arkaya adım eklenince sıradaki kayıt son eklenen adıma atlıyordu. Artık ilk adımda kalıyor. Ölçüm başladıktan sonra eklenen adım yine seçilir; araya giren kayıt için davranış aynı.
- **Telefonda düğme çubuğu (tarayıcı):** Alta sabitlenen Ölç / Durdur / Geri al tek satırda ve gölgeli. İçeriğin üstüne binmesi azaldı.

**Eklemeler**
- **Adım düğmesinde yeterlilik göstergesi:** Ölçüm sekmesinde her adım düğmesinde geçerli ölçüm ve gereken ölçüm sayısı görünür (ör. "3/21"); yeterliyse "✓". Ölçmeyi ne zaman bırakacağına Analiz'e gitmeden karar verilir.
- **X kısayolu:** "Sonraki kayıt kapsam dışı" işaretini açar ya da kapatır.
- **Adım tablosundan kayda gitme:** Analiz → Adım tablosunda bir adıma çift tıklayınca o adımın kayıtları Ölçüm sekmesinde süzülmüş açılır.
- **Kaldığın etüde dön:** Program ya da sayfa yeniden açılınca Çalışmalar'da son açılan etüt seçili gelir. Üstte "Kaldığın etüde dön" düğmesi çıkar.

**Uyumluluk:** Paket biçimi ve hesaplar değişmedi.

**Testler**
- Node: 199/199.
- Chrome uçtan uca: 93/93.
- Windows öz-denemesi: iki dilde, yeni denetimler dahil.

# v2.2.1 (Windows) · Video Zaman Etüdü — metin düzeltmeleri

v2.2.0'daki sade düzen aynen geçerli. Bu sürümde yalnız metinler düzeltildi; hesaplar ve paket biçimi değişmedi. Tarayıcı sürümünde aynı düzeltmeler zaten yayında.

- **İngilizce tekil/çoğul:** Sayı 1 olunca bozulan ifadeler ("1 stations", "1 records selected" gibi) sayıdan bağımsız kalıba çevrildi. Etkilenen yerler: toplu düzenleme, adım kopyalama, hat dengeleme özeti, yedek ve geri yükleme, aykırı kayıt iletileri.
- **Eski ekran adı:** Üç iletide kalan "Video ve ölçüm" / "Video & measurement" adı "Ölçüm sekmesi" / "Measure tab" oldu.

Yenilikler için bkz. v2.2.0.

# v2.2.0 (Windows) · v1.9.0 (tarayıcı) · Video Zaman Etüdü — sade düzen ve yeni kolaylıklar

Program büyüdükçe ölçüm ve analiz ekranları kalabalıklaşmıştı. Ekranlar işin sırasına göre yeniden düzenlendi ve dört yeni özellik eklendi. Değişiklikler tarayıcı ve Windows sürümünde, Türkçe ve İngilizce arayüzde aynı.

**Yeni düzen: beş sekme**
1. **Çalışmalar:** Etüt listesi. Yeni sütun: **Durum** (taslak, tamamlandı, onaylandı). Eski "Durum" sütununun adı **Yeterlilik** oldu.
2. **Hazırlık (yeni):** Ölçmeden önce yapılan her şey burada.
   - Adım tablosu: ad, sınıf, tür, bitiş anı, israf, fikir, sıra ve silme tek tabloda.
   - Etüdün kapsamı.
   - Etüt bilgileri.
   - Ölçüm ekranındaki adım araçları kaldırıldı.
3. **Ölçüm:** Yalnız ölçmek için gerekenler kaldı: video, adım düğmeleri, Başla/Ölç, çevrim ve kapsam dışı. Kayıt temposu, paralel aralık ve adım görseli **Gelişmiş** bölümüne taşındı.
4. **Analiz:** Dört alt sekmesi var: **Özet · Adım tablosu · Çevrimler · Dağılım ve aykırılar**.
   - Ayarlar katlı durur. Başlıkta tek satırlık özet görünür (ör. "güven %95 · tempo %110 · tolerans %15"); tıklayınca açılır.
   - Excel, Yazdır/PDF, adım kartları ve t tablosu tek **Çıktı al** menüsünde.
5. **İyileştirme (yeni):** Yalın araçlar analizden ayrıldı ve her biri kendi alt sekmesinde: hat dengeleme, standart iş, fikirler, adım kartları.

**Seçime göre sadelik: etüdün kapsamı**
- Yeni etüt açarken standart sürenin yanında hangi araçların kullanılacağı seçilir: hat dengeleme, standart iş, israf ve fikirler, adım kartları. Varsayılan olarak hiçbiri seçili gelmez; o zaman program yalnız standart süreyi gösterir.
- Seçilmeyen araçların sekmesi, tablo sütunu ve Excel sayfası çıkmaz.
- Seçim sonradan Hazırlık sekmesinden değiştirilebilir. Eski etütlerde bütün araçlar açık kalır.
- İş örneklemesi kendi akışında kalır; İyileştirme sekmesi görünmez.

**Yeni özellikler**
- **Adımları başka etütten al:** Hazırlık'ta "Başka etütten al…" ya da yeni etüt formunda "Adımları şu etütten al".
  - Gelenler: ad, sınıf, tür, bitiş anı, israf, fikir ve istasyon. Ölçümler ve adım görselleri gelmez.
  - Mevcut liste korunup eksikler eklenebilir ya da liste değiştirilebilir. Değiştirmede ölçülmüş adımlar silinmez.
- **Toplu kayıt düzenleme:** Birden çok kayıt seçilip adım, çevrim, sınıf, kapsam dışı ve tempo birlikte değiştirilir ya da kayıtlar birlikte silinir. Boş bırakılan alan değişmez.
  - Tarayıcıda işaret kutularıyla seçilir.
  - Windows'ta Ctrl/Shift ile seçilir.
- **Etüt durumu ve onay:** Taslak, Tamamlandı ya da Onaylandı. Onayda onaylayan ve tarih tutulur; tarih boşsa bugün yazılır.
  - Durum listede, yazdırma başlığında ve Excel özetinde görünür.
  - Onaylı etüt ölçüm ekranında uyarı notu gösterir.
- **İlk kullanım turu:** İlk açılışta beş adımlık kısa tur sekmeleri sırayla tanıtır. Sağ üstteki **Kısa tur** ile her zaman yeniden açılır.

**Düzeltme (Windows):** Etüt açılınca adımın tür, israf, fikir, istasyon ve kare bilgisi kopyalanırken düşüyordu; şimdi korunuyor.

**Erişilebilirlik:** Sekmeler ve alt sekmeler ok tuşlarıyla gezilir (WAI-ARIA sekme deseni). Adım tablosundaki her alanın okunabilir bir adı var.

**Uyumluluk:** Paket sürümü 1 olarak kalır. Etütte dört yeni isteğe bağlı alan var: `Modules` (boş liste "yalın araç yok" demektir; alan yoksa hepsi açık), `Status`, `ApprovedBy`, `ApprovedAt`. Eski sürümler bu alanları yok sayar. Excel'de durum satırları yalnız durum girilmişse yazılır; eski etütlerin çıktısı değişmez.

**Testler**
- Node: 199/199.
- Chrome uçtan uca: 86/86.
- axe: iki dilde, masaüstünde ve telefonda, beş sekme ve alt sekmelerin hepsinde 0 ihlal.
- C# kontrolleri: v2 için 73 denetim (modül, durum ve adım kopyalama `Shared/ek-alanlar.json` ile tarayıcıyla eşdeğer).
- Windows öz-denemesi: iki dilde, Hazırlık, kapsam, onay, toplu düzenleme ve adım kopyalama dahil.

# v2.1.0 (Windows) · v1.8.0 (tarayıcı) · Video Zaman Etüdü — muadil yazılımlardaki eksik özellikler

Timer Pro, AVIX, UMT Plus ve Proplanner gibi zaman etüdü yazılımlarında olup bizde olmayan özellikler eklendi. Hepsi tarayıcı ve Windows sürümünde, Türkçe ve İngilizce.

**Analiz**
- **Kayıt başına tempo puanı:** Klasik etüt formundaki gibi her kayda ayrı tempo verilebilir (ölçerken "Tempo puanı %" alanı ya da kayıt düzenleyici). Adımın temposu kayıtlardan hesaplanır: ortalama yönteminde süreyle ağırlıklı, tekrar yönteminde düz ortalama. Puansız kayıt adım ya da genel tempoyu alır.
- **Dağılım ve aykırı değerler:** Seçili adımın histogramı. Çeyrekler arası açıklığın 1,5 katı dışındaki kayıtlar (en az 4 kayıtlı adımlarda) listelenir. Tek düğmeyle kapsam dışı bırakılır; silinmez, not alanına "aykırı (IQR)" yazılır.
- **Hat dengeleme (Yamazumi):** Adımlar istasyonlara ya da operatörlere atanır. Yığılmış çubuk grafikte takt çizgisi görünür. Darboğaz, denge verimi, denge kaybı ve takta göre en az istasyon sayısı hesaplanır. Takta göre otomatik dağıtım süreç sırasını korur.
- **Standart iş kombinasyon tablosu:** Adım türü seçilir (el işi, makine, yürüme, bekleme). Makine işi paralel ilerler; takt çizgisiyle Gantt görünümü çizilir.
- **İyileştirme fikirleri:** Adıma 7 israftan biri ve iyileştirme fikri girilir. NVA adımlarla birlikte ayrı bir listede, katkı ve payıyla gösterilir.

**Çalışma ve çıktılar**
- **İş örneklemesi:** Yeni etüt türü. Videoda rastgele ya da düzenli anlara gidilir ve o anki kategori 1–9 tuşlarıyla kaydedilir. Sonuçta paylar, güven aralığı ve hedef hata için gereken gözlem sayısı (n = z²·p(1−p)/e²) görünür. Excel raporu da bunlarla çıkar.
- **Adım kartları (storyboard):** Her adımın bir video karesi, sınıfı, türü, bitiş anı, standart süresi, israfı ve fikri. Görsel iş talimatı olarak ayrıca yazdırılabilir. Kare "Bu kareyi adım görseli yap" ile seçilir; seçilmezse adımın ilk kaydının ortası alınır.
- **Yan yana önce / sonra video:** Karşılaştırma penceresinde iki etüdün videosu yan yana izlenir. Adım seçilince ikisi de o adımın başına gider; oynat ve duraklat ikisine birden uygulanır.
- **Excel:** Ham sayfada tempo sütunu (veri varsa). Yeni sayfalar: Standart iş, Hat dengeleme, İyileştirme. Özet sayfasında aykırı kayıt sayısı, denge verimi, darboğaz ve en az istasyon.

**Uyumluluk:** Paket sürümü 1 olarak kalır. Yeni alanlar isteğe bağlıdır: kayıtta `Rating`; adımda `Type`, `Station`, `Waste`, `Idea`, `FrameMs`; etütte `StudyType`, `Categories`, `Samples`, `SampleError`. Eski sürümler bu alanları yok sayar.

**Testler**
- Node 196/196.
- Chrome uçtan uca 64/64.
- axe iki dilde, masaüstünde ve telefonda 0 ihlal.
- C# kontrolleri: v2 için 70 denetim. Yeni hesapların tarayıcıyla eşdeğerliği `Shared/ek-alanlar.json` ile denetlenir.
- Windows öz-denemesi iki dilde 66 denetim (video kareleri dahil).

# v2.0.0 · Video Zaman Etüdü — Windows sürümü tarayıcıyla eşit, Türkçe ve İngilizce

Windows masaüstü sürümü v1.2.0'dan v2.0.0'a geçti. Tarayıcı sürümündeki v1.5–v1.7 kurallarının hepsi artık Windows'ta da var ve arayüz Türkçe ile İngilizce arasında değiştirilebiliyor.

**Dil**
- Sağ üstteki düğme arayüzü Türkçe ↔ İngilizce değiştirir; seçim hatırlanır. Açık etüt ve bölüm korunur.
- Metinler tarayıcı sürümünün sözlüğünden üretilir; iki sürüm aynı ifadeleri kullanır. Excel başlıkları ve sonuç metinleri de seçilen dilde.

**Ölçüm**
- Adım listesi (sınıf ve bitiş anı açıklamasıyla), 1–9 tuşlarıyla adım seçimi, Ölç'ten sonra sıradaki adıma ve çevrime otomatik geçiş.
- Kapsam dışı ara kayıt sırayı bozmaz; önceki adıma dönülür. Ctrl+Z son kaydı geri alır.
- Zaman çizgisinde gezinmek ölçümü kesmez; Ölç başlangıcın gerisindeyse kayıt eklenmez ve uyarı çıkar.
- Zaman çizgisinde ölçülen aralıklar sınıf rengiyle görünür; süren ölçüm canlı gösterilir.
- Etüt kaldığı yerden açılır. Kayıt notu, kayıt düzenleyici, adım ve çevrim süzgeci.

**Analiz ve çıktılar**
- Ayarlar yazıldıkça uygulanır; tolerans kalemleri, takt süresi, adıma özel tempo.
- Özet kartları, adım katkısı ve çevrim süresi grafikleri, çevrim değişkenliği, süreç sırasında adım tablosu.
- Excel'de ilk sayfa **Özet** (gereken n ve tolerans kalemleri dahil). **Yazdır / PDF**.
- Etüt bilgileri (operatör, istasyon, vardiya…), önce / sonra karşılaştırması, **Tümünü yedekle (.zip)** ve yedekten geri yükleme. Yedek tarayıcı sürümüyle aynı biçimde.
- Demo etüdü uygulama içinden açılır.

**Uyumluluk:** Paket sürümü 1 olarak kalır. v1.2 kayıtları ve paketleri v2.0'da açılır. v2.0 paketleri v1.2'de de açılır; v1.2 yeni alanları görmez.

**Testler:** C# kontrolleri v2 için 54 yeni denetim (`Shared/ek-alanlar.json` ile tarayıcıyla bayt düzeyinde eşdeğerlik dahil). Windows'ta arayüz öz-denemesi iki dilde geçti. Node 184/184.

# v1.7.0 · Video Zaman Etüdü — tolerans kalemleri, toplu yedek ve çıktı düzeltmeleri

Yalnız tarayıcı sürümü değişti; v1.5.0 ve v1.6.0 ile aynı PR'da yayınlanır. Bütün yenilikler Türkçe ve İngilizce arayüzde var.

**Yeni**
- **Tolerans kalemleri:** Kişisel ihtiyaç, yorgunluk, kaçınılmaz gecikme ve diğer ayrı girilebilir. Tolerans % bunların toplamı olur. Özet kartında, Excel özetinde ve yazdırmada döküm görünür. Windows yalnız toplamı görür ve aynı sonucu bulur.
- **Toplu yedek:** "Tümünü yedekle (.zip)" bütün etütleri tek dosyaya alır. "Etüdü içe al…" bu yedeği de açar. Zaten kayıtlı olan etüt ikinci kez eklenmez, mevcut etütler değişmez.
- **Kaldığı yerden devam:** Etüt açılınca video son kaydın bitişine gider. Sıradaki adım ve çevrim son kayda göre ayarlanır.
- **Kayıt süzgeci:** Ölçüm tablosu adıma ve çevrime göre daraltılabilir.

**Çıktı düzeltmeleri**
- Excel özeti "1 yetersiz + 1 hesaplanamayan" adımı "2 yetersiz" diye sayıyordu; şimdi ayrı satırlar. Yetersiz adıma "Gereken n (yaklaşık)" sütunu eklendi.
- Yazdırma: adım tablosu sayfadan taşıyordu (Yöntem sütunu kesiliyordu); artık sığıyor. Başlıkta etüt bilgileri ve dayanılan ayarlar (güven, hedef hata, tempo, tolerans, baz süre yöntemi, takt) yazıyor. Altta sürüm var. Ctrl+P ile de aynı çıktı alınıyor.
- Çevrim grafiğinde y ekseni ve çubuk değerleri var; ortalama etiketi artık çubuğun üstüne binmiyor.
- Çalışmalar ekranındaki düğmeler gruplandı: oluştur/aç · içe al/dışa aktar/yedekle · karşılaştır · sil.

**Paket:** Sürüm 1. Yeni isteğe bağlı alan: `AllowanceParts`. Windows okurken yok sayar.

**Testler:** Node 180/180; Chrome uçtan uca 47/47; axe Türkçe ve İngilizce, masaüstü ve telefonda 0 ihlal; telefonda yatay taşma yok.

# v1.6.0 · Video Zaman Etüdü — etüt için eksik kalan bilgiler ve ekran düzeni

Yalnız tarayıcı sürümü değişti; v1.5.0 ile aynı PR'da yayınlanır. Bütün yenilikler Türkçe ve İngilizce arayüzde var.

**Etüt için gereken bilgiler**
- **Adımın bitiş anı / açıklaması:** Her adıma bir kırılma noktası yazılır (ör. "ürün kutuya bırakılınca biter"). Ölçüm sırasında Sıradaki kayıt kutusunda görünür, Excel özetine girer.
- **Kayıt notu:** Seçili kayda not eklenir (ör. "forklift bekledi"). Tabloda ve Excel'in ham sayfasında görünür; not yoksa ham sayfa Windows çıktısıyla aynı kalır.
- **Adım temposu:** Seçili adıma genel tempodan farklı bir tempo verilebilir. Tabloda `*` ile işaretlenir. Windows sürümü bunu tanımaz, genel tempoyu kullanır.
- **Takt süresi:** İsteğe bağlı. Özette takt doluluğu ve gereken operatör sayısı (standart ÷ takt) görünür; Excel özetine de girer.
- **Yazdır / PDF:** Analiz sayfası etüt bilgileriyle birlikte yazdırılır ya da PDF olarak kaydedilir.
- Ölçüm sürerken geçen süre Sıradaki kayıt kutusunda görünür.

**Ekran ve bulunabilirlik**
- Analiz sayfası bölümlere ayrıldı: Analiz ayarları, Sonuç, Adım analizi. Sıklık ve adım temposu "Seçili adım" kutusunda; hangi adıma uygulanacağı yazıyor.
- Ölçüm yokken analizde tek bir boş durum mesajı ve Video ve ölçüme git düğmesi var.
- Çalışmalar ekranında seçim ipucu var; seçim yokken Aç / Dışa aktar / Sil, iki etütten azken Karşılaştır kapalı.
- Adım araçları yalnız bir adım seçiliyken görünüyor (önceden boş listede de görünüyordu).
- Telefonda analiz sayfası yana taşmıyor; grafikler ekran genişliğine göre çiziliyor.
- "Nasıl çalışır" metni yeni ölçüm akışına göre güncellendi.

**Paket:** Sürüm 1. Yeni isteğe bağlı alanlar: `Takt`, `StepPace`, adım `Note`, kayıt `Note`. Windows sürümü okurken yok sayar.

**Testler:** Node 174/174; Chrome uçtan uca 39/39; axe Türkçe ve İngilizce, masaüstü ve telefonda 0 ihlal.

# v1.5.0 · Video Zaman Etüdü — ölçüm akışı, grafikler, özet rapor ve önce/sonra

Yalnız tarayıcı sürümü değişti. Windows sürümü v1.2.0 olarak kalır ve tarayıcıdan dışa aktarılan paketleri açar; yeni adım listesi ve etüt bilgileri Windows'ta görünmez.

**Ölçüm akışı**
- **Zaman çizgisinde gezinmek ölçümü kesmiyor.** Adımın bittiği kare çizgide aranıp Ölç'e basılabilir. Ölç başlangıcın gerisindeyse kayıt eklenmez ve uyarı çıkar.
- **Adım listesi:** Etüt başında ya da ölçüm ekranında süreç adımları sırasıyla tanımlanır. Adım tıklanarak ya da 1–9 tuşlarıyla seçilir. Ölç'ten sonra sıradaki adıma geçilir; son adımdan sonra çevrim kendiliğinden 1 artar.
- **Sınıf adıma bağlı:** VA/SVA/NVA adım tanımında seçilir ve kayıtlara oradan gelir.
- **Kapsam dışı tek kayıtlık:** İşaret kayıttan sonra kalkar. Araya giren kapsam dışı kayıt (ör. bekleme) sırayı ilerletmez; önceki adıma dönülür.
- **Son kaydı geri al** (düğme ve Ctrl+Z): adım, çevrim ve ölçüm başlangıcı o kaydın öncesine döner.

**Ölçüm ekranı**
- Süreler okunur biçimde (0:14,000). Zaman çizgisinin üstünde ölçülen aralıklar sınıf rengiyle görünür; süren ölçüm de işaretlidir.
- Video solda, adımlar ve ölçüm düğmeleri sağda. Seçili kaydın düzenlenmesi ayrı bir kutuda. Bağımsız aralık katlanır bölümde.
- Telefonda Başla/Ölç düğmeleri ekranın altına sabitlenir.

**Analiz**
- Adımlar süreç sırasında. Ayarlar alan değişince uygulanır (Ayarları uygula düğmesi kalktı); geçersiz değer satır içinde gösterilir. Tolerans %0 ise uyarı çıkar.
- Grafikler: adımların çevrim başına standart katkısı (sınıf rengiyle) ve çevrim süreleri (ortalama çizgisi; ortalamadan %20'den fazla sapan çevrim işaretli).
- Çevrimler tablosunda ortalamadan fark; özette çevrim değişkenliği.

**Rapor ve etüt bilgileri**
- Excel'in ilk sayfası **Özet**: etüt bilgileri, çevrim başına standart süre, saatlik kapasite, çevrim değişkenliği, VA/SVA/NVA payı ve süreç sırasında adım tablosu. Diğer sayfalar Windows çıktısıyla aynı.
- **Etüt bilgileri:** operatör, istasyon/hat, vardiya, çekim tarihi, gözlemci, not. Yeni etütte ya da Analiz ekranında girilir.
- Etüt listesinde istasyon · operatör, ölçüm ve çevrim sayısı, yeterlilik durumu.
- **Önce / sonra karşılaştırması:** iki etüdün adım katkıları, toplam standart süre ve saatlik kapasite farkı.

**Paket:** `.vzetud` sürümü 1 olarak kaldı. Adım listesi ve etüt bilgileri isteğe bağlı `Steps` ve `Info` alanlarında taşınır; Windows bunları okurken yok sayar.

**Testler:** Node 170/170 (yeni: adım listesi, paket alanları, değişkenlik, karşılaştırma, özet sayfası). Chrome uçtan uca testi yeni akışa göre yeniden yazıldı: 27/27.

# v1.4.1 · Video Zaman Etüdü — İngilizce demo

Yalnız tarayıcı sürümü ve demo dosyaları değişti; Windows sürümü v1.2.0 olarak kalır (İngilizce demo paketini de açar).

- İngilizce arayüzde **Open demo study** İngilizce adım adlarıyla (Preparation, Picking, Scanning, Packing, Waiting, Periodic check) ve İngilizce yazılı sentetik videoyla açılır. Zamanlar ve sonuçlar Türkçe demoyla birebir aynıdır.
- `Tools/DemoVideo/demo_video.py`: demo videosunu Türkçe veya İngilizce yeniden üretir.
- Testler: İngilizce demonun Türkçe demoyla aynı sonuçları verdiği (Node ve C#), demo kopyalarının eşitliği; e2e 66/66.

# v1.4.0 · Video Zaman Etüdü — tarayıcıda öğretici ekranlar ve analiz özeti

Yalnız tarayıcı sürümü değişti; hesap kuralları, `.vzetud` biçimi ve Excel raporu aynı. Windows sürümü v1.2.0 olarak kalır.

- **Analiz özeti:** Analiz ekranının üstünde çevrim başına standart süre (katkıların toplamı), saatlik kapasite, yetersiz adımlar için gereken yaklaşık gözlem sayısı (t değeri yinelemeli) ve ölçülen sürenin VA/SVA/NVA dağılımı.
- **Öğretici içerik:** Çalışmalar ekranında 3 adımlık "Nasıl çalışır"; ölçüm ekranında VA/SVA/NVA, çevrim ve kapsam dışı açıklaması; analiz ekranında güven, hedef hata, tempo, tolerans, baz → normal → standart ve katkı sütunlarının açıklaması (katlanır, TR/EN).
- Süre grupları tablosu boş kaldığında ne yapılacağını söyler.
- **Telefon ve erişilebilirlik:** Sekmeler tek satırda yatay kayar; sekme listesi gezinme bölgesi içinde; kaydırılabilir tablolar klavyeyle odaklanır; pasif sekme rengi koyulaştı. axe denetimi masaüstü ve telefonda 0 ihlal; Chrome uçtan uca testi 65/65.

# v1.3.0 · Video Zaman Etüdü — tarayıcıda İngilizce arayüz

Yalnız tarayıcı sürümü değişti; Windows sürümü v1.2.0 (Türkçe) olarak kalır. Hesap kuralları ve `.vzetud` biçimi değişmedi.

- **Türkçe / English:** Arayüz, mesajlar, t tablosu ve Excel raporu iki dilde. Dil sırası: adresteki `?lang=tr|en` → son seçim → tarayıcı dili. Sağ üstteki düğmeyle değiştirilir ve hatırlanır.
- İngilizcede sayılar ondalık noktayla, tarihler `yyyy-MM-dd` biçiminde gösterilir; Excel dosyası `_analysis` ekiyle adlandırılır. Türkçe Excel çıktısı masaüstüyle birebir aynı kalır.
- Hesap çekirdeği Türkçe sonuç metinleri üretmeye devam eder (masaüstüyle eşdeğerlik testleri için); ekranda ve raporda çevrilir. Çekirdek hataları çeviri anahtarı taşır.
- Testler: iki dilin sözlük anahtarlarının eşitliği, çekirdek hata anahtarlarının çevirisi, iki dilde Excel; Chrome uçtan uca testine İngilizce ziyaretçi senaryosu (65/65).
- Demo etütteki adım adları ve video etiketleri Türkçedir; İngilizce arayüzde bu belirtilir.

# v1.2.1 · Video Zaman Etüdü — tarayıcıda tek tıkla demo

Yalnız tarayıcı sürümü değişti; Windows sürümü v1.2.0 olarak kalır.

- **Demo etüdü aç:** Çalışmalar ekranındaki bağlantı örnek depo etüdünü ve videosunu tek tıkla açar; dosya indirmek gerekmez. Demo zaten ekliyse yenisi oluşturulmaz.
- Demo videosunu bekleyen etütlere (`sentetik-depo-etudu.mp4`) video kendiliğinden bağlanır.
- Demo dosyaları sitenin kendisinden gelir (`Web/demo/`, kökteki `demo/` ile aynı olduğu testle denetlenir); içerik güvenliği politikası yalnız aynı siteye istek izni verir.

# v1.2.0 · Video Zaman Etüdü — tarayıcı sürümü

**Kullanım**
- **Windows:** aşağıdaki `VideoZamanEtudu_Windows_x64.zip` dosyasını indir, bir klasöre çıkar, `Desktop.exe`'yi aç. .NET kurulumu gerekmez; örnek etüt `demo` klasöründe.
- **Tarayıcı:** https://zaman-etudu.ysfdurak.com — kurulum yok, video bilgisayardan çıkmaz.
- Paket imzasız olduğu için Windows SmartScreen uyarı verebilir: **Ek bilgi → Yine de çalıştır**.

**Yeni: tarayıcı sürümü (`Web/`)**
- Masaüstüyle aynı üç ekran, ölçüm akışı, kısayollar, analiz, Excel ve `.vzetud` aktarımı. Sunucu gerektirmez; video ve ölçümler bilgisayardan çıkmaz.
- Etütler tarayıcının yerel deposunda (IndexedDB) saklanır; okunamayan kayıt atlanır ve uyarılır.
- Chrome/Edge'de video dosyası izni hatırlanır; diğer tarayıcılarda video yeniden seçilir.
- Bağımlılıksız Excel yazıcı (5 sayfa, masaüstüyle aynı sütunlar ve “0.000” biçimi).
- Katı paket okuyucu: masaüstünün kabul/ret kararlarıyla birebir aynı (ör. tam sayı alanında `1000.0` reddedilir).

**İki sürümün eşdeğerliği**
- `Shared/esdeger-ornekler.json`: 61 hesap örneği (sabit + rastgele), 175 t değeri, 86 paket senaryosu. C# Checks ve `Web/tests` aynı dosyaya karşı çalışır.
- `Tools/EsdegerUretici`: kurallar değişirse örnek dosyasını C# çekirdeğinden yeniden üretir.
- GitHub Actions: Windows işi + ayrı tarayıcı test işi; siteye konacak dosyalar **VideoZamanEtudu_Web** paketi olarak üretilir.

# v1.1.0 · Video Zaman Etüdü

**Düzeltmeler**
- Excel sütun genişlikleri artık tüm satırlara göre ayarlanır (önceden yalnız 10–48. satırlar dikkate alınıyordu).
- Analiz ayarları geçersiz olan `.vzetud` paketleri diğer bozuk paketlerle aynı “geçersiz paket” hatasıyla reddedilir.
- Yerel kayıtta okunamayan bir etüt uygulamanın açılmasını engellemez; atlanır ve kullanıcı uyarılır.
- Başlık açık etüdün kodunu ve adını gösterir.

**İyileştirmeler**
- Dışa aktarılan `.vzetud` ve Excel dosyalarına etüt kodundan ad önerilir (ör. `DEMO-01.vzetud`, `DEMO-01_analiz.xlsx`).
- Paket dosyasında Türkçe harfler okunur biçimde yazılır. Paket biçimi ve sürümü (1) değişmedi; eski paketler açılır.
- `.gitignore` demo dışındaki video, Excel ve etüt paketlerini depoya almaz.
- Otomatik kontrollere 56 ek kontrol eklendi (kenar durumları, bozuk paketler, yerel kayıt biçimi, dosya adı).

**Yeni**
- Klavye kısayolları: Boşluk, Enter, Esc, ←/→ (0,1 sn), Shift (1 sn), Ctrl (0,04 sn).
- İnce konumlama düğmeleri ve 0,25×–2× oynatma hızı.
- Etüt silme (onaylı, video dosyasına dokunmaz).
- Excel “Ayarlar” sayfasına video adı, kayıt sayıları, süreç kararlılığı, dışa aktarım zamanı ve program sürümü eklendi.

# v1.0.0 · Video Zaman Etüdü

Video zaman çizelgesinden manuel iş adımı ölçümü alan Windows masaüstü uygulaması. Ölçümler adım ve çevrim bazında saklanabilir, VA/SVA/NVA olarak sınıflandırılabilir ve Excel’e aktarılabilir.

- Adım bazında gözlem yeterliliği, baz süre, tempo ve tolerans hesapları.
- Kapsam dışı aralıkların saklanması, paralel işlerde geçen süre ve iş yükünün ayrı gösterilmesi, seyrek iş katkısı.
- Etütleri `.vzetud` dosyasıyla bilgisayarlar arasında taşıma. Alıcı etüdü ayrı bir kayıt olarak açabilir ve videoyu yerelden bağlayabilir.
- Sentetik depo videosu, örnek etüt ve beklenen sonuçlar.

**GitHub depo açıklaması:** Video üzerinden iş adımlarını ölçen, standart süreyi hesaplayan ve sonuçları Excel’e aktaran Türkçe Windows uygulaması.

**Yayın kontrolü:** Windows derlemesi ve otomatik kontroller geçtikten sonra sentetik video ile manuel kabulü tamamlayın. Ardından yayımlanan Windows klasörünü ZIP olarak sürüme ekleyin.
