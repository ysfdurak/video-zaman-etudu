# Video İş Etüdü

Video üzerinden iş adımlarını ölçen, standart süreyi hesaplayan ve sonuçları Excel'e aktaran Türkçe iş etüdü aracı (zaman etüdü, iş örneklemesi, hat dengeleme). Eski adı: Video Zaman Etüdü; adres ve dosya biçimi değişmedi. **İki sürümü vardır**; ikisi aynı hesap kurallarını ve aynı `.vzetud` paket biçimini kullanır, bir sürümde açılan etüt diğerinde açılır.

| | Windows masaüstü (`Desktop/`) | Tarayıcı (`Web/`) |
|---|---|---|
| Teknoloji | C# / WPF, .NET 8 | HTML + JavaScript, bağımlılıksız |
| Kurulum | ZIP'i çıkar, `VideoZamanEtudu.exe` | Adresi aç; sunucu gerekmez |
| Etütlerin yeri | `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite` | Tarayıcının yerel deposu (IndexedDB) |
| Video | Bilgisayarda kalır, yolu hatırlanır | Bilgisayarda kalır, tarayıcıya yüklenmez; Chrome/Edge'de izinle hatırlanır, diğerlerinde her oturumda yeniden seçilir |
| Arayüz dili | Türkçe ve İngilizce (sağ üstteki düğme; seçim hatırlanır) | Türkçe ve İngilizce |
| Excel / `.vzetud` | Var (ilk sayfa **Özet**) | Var (aynı sayfalar ve biçim) |
| Ölçüm akışı | Adım listesi, 1–9 tuşları, otomatik çevrim, geri al; gezinmek ölçümü kesmez (v2.0) | Aynısı (v1.5) |
| Analiz ekleri | Grafikler, çevrim değişkenliği, etüt bilgileri, önce/sonra, adım açıklaması, kayıt notu, adım temposu, takt, yazdır/PDF, tolerans kalemleri, toplu yedek, kaldığı yerden devam, kayıt süzgeci (v2.0) | Aynısı (v1.5–v1.7) |
| İleri analiz (v2.1 / v1.8) | Kayıt başına tempo, dağılım ve aykırı değer, hat dengeleme (Yamazumi), standart iş kombinasyon tablosu, iyileştirme fikirleri (7 israf), adım kartları (storyboard), iş örneklemesi, yan yana önce/sonra video | Aynısı |
| Sade düzen (v2.2 / v1.9) | Beş sekme: Çalışmalar · Hazırlık · Ölçüm · Analiz · İyileştirme; analizde alt sekmeler, tek "Çıktı al" menüsü; etüdün kapsamı (yalın araçlar seçime göre), adımları başka etütten alma, toplu kayıt düzenleme, etüt durumu ve onay, kısa tur | Aynısı |

Windows **v2.2.3** ile tarayıcı **v1.10.3** aynı özelliklere sahiptir; iki fark var: tarayıcı v1.9.x çevrimdışı çalışmayı ekler (Windows zaten internet gerektirmez), tarayıcı v1.10 **iki gözlemci karşılaştırmasını** ekler (aynı videoyu iki kişi ölçer, fark çıkan adımda başlangıç/bitiş tanımı gösterilir; Windows'ta yok). Bir sürümde kaydedilen etüt ve **Tümünü yedekle** ile alınan .zip yedeği diğerinde açılır. Yeni alanları taşıyan paketler eski Windows v1.2'de de açılır; v1.2 bu alanları görmez ve adım temposu yerine genel tempoyu kullanır.

Eşdeğerlik `Shared/esdeger-ornekler.json` ile sınanır: 61 hesap örneği, 175 t değeri ve 86 paket kabul/ret senaryosu hem C# (`Checks`) hem JavaScript (`Web/tests`) tarafında aynı sonucu vermek zorundadır. v2 alanları (adım listesi, notlar, adım temposu, takt, tolerans kalemleri, karşılaştırma) için `Shared/ek-alanlar.json` aynı işi görür: tarayıcı çekirdeği üretir, C# aynı paketi bayt bayt aynı metinle yazmak ve aynı sonuçları bulmak zorundadır.

Masaüstü program, ilk derlemede NuGet bağımlılıkları yüklendikten sonra internet bağlantısı gerektirmez.


**Ölçüm:** Yalnızca herkese açık demoda (zaman-etudu.ysfdurak.com) hangi ekranların açıldığı anonim sayılır ve geri bildirim düğmesi görünür; video, etüt verisi ve ad gönderilmez, çerez yoktur. Kendi sunucuna ya da şirket içine kurduğun kopya dışarıya hiç istek atmaz (`Web/js/olcum.js`).

**Kurumda kullanacaksan:** Çalışanların kişisel verisi işlenir. KVKK notları, çalışan duyurusu ve (video projelerinde) çekim öncesi onay şablonu: [docs/KVKK_VE_CALISAN_BILGILENDIRME.md](docs/KVKK_VE_CALISAN_BILGILENDIRME.md).


**Projeyi anlatırken:** Mülakatta sorulabilecek 10 soru, kısa cevaplar ve verilen kararlar (sorun → karar → bedel): [docs/MULAKAT.md](docs/MULAKAT.md).

## English

**Video Work Study** (Video İş Etüdü) measures work steps from video, calculates standard time and exports the results to Excel. It comes in two versions that share the same calculation rules and the same `.vzetud` study package: a serverless browser version and a Windows desktop app (C# / WPF, .NET 8). Both are available in English and Turkish.

- **Try it:** https://zaman-etudu.ysfdurak.com/?lang=en (click “Open demo study” for a three-cycle synthetic warehouse video with ready measurements). Windows: download the ZIP from [Releases](https://github.com/ysfdurak/video-zaman-etudu/releases/latest) and run `VideoZamanEtudu.exe`.
- **Measuring:** times come from the video position (Start → Measure → Measure); a step list moves to the next step and cycle automatically, out-of-scope interruptions do not break the sequence, Ctrl+Z undoes the last record.
- **Analysis:** VA / SVA / NVA classes, per-step sample sufficiency with Student-t, pace rating and allowances, normal and standard time, takt, outliers (IQR) and cycle variation.
- **Lean tools:** line balancing (Yamazumi), standard work combination sheet, improvement ideas by the 7 wastes, step cards with video frames, work sampling, before/after comparison with side-by-side video.
- **Privacy:** videos and measurements never leave the computer, and the browser version works fully offline once opened; only the public demo (zaman-etudu.ysfdurak.com) counts screen views anonymously (no cookies, no study data) and shows a feedback button, self-hosted copies send nothing; studies live in the browser (IndexedDB) or in a local SQLite file on Windows. Back up all studies to one `.zip`.
- **Equivalence tests:** 61 calculation cases, 175 t values and 86 package scenarios must give the same result in C# and JavaScript on every push.

The rest of this README (running, release, first study, keyboard shortcuts) is in Turkish; the code guide is in [docs/PROJE_REHBERI.md](docs/PROJE_REHBERI.md).

## Windows’ta çalıştırma

1. Windows’a **.NET 8 SDK** kur. ZIP dosyasını çıkar.
2. PowerShell’de README’nin bulunduğu klasörü aç.
3. Aşağıdaki komutu çalıştır.

```powershell
dotnet run --project .\Desktop\Desktop.csproj
```

Hesaplama kontrolleri:

```powershell
dotnet run --project .\Checks\Checks.csproj
```

Yerel kayıt: `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite`. Uygulama kapalıyken bu dosyayı kopyalayarak kayıtları yedekleyebilirsin.

## Tarayıcı sürümü

`Web/` klasörü doğrudan yayınlanabilen statik bir sitedir (derleme adımı yok). Yerelde denemek için klasörde basit bir sunucu aç; dosyayı çift tıklayarak açmak modüller nedeniyle çalışmaz:

```powershell
cd Web
python -m http.server 8080   # sonra http://localhost:8080
```

- Videolar ve ölçümler hiçbir sunucuya gönderilmez; sayfa yalnızca kendi dosyalarını yükler (içerik güvenliği politikası dış bağlantıyı engeller).
- **Dil:** Arayüz Türkçe ve İngilizce. Adresteki `?lang=tr` / `?lang=en` önceliklidir; yoksa son seçim, o da yoksa tarayıcı dili kullanılır. Metinler `Web/js/i18n.js` sözlüğündedir; yeni metin eklerken iki dile de ekle (test anahtarların eşit olduğunu denetler). Windows sürümü aynı sözlükten üretilen `Desktop/Metinler.json` dosyasını kullanır: sözlüğü değiştirince `node Tools/Metinler/uret.mjs` çalıştır (test dosyanın güncel olduğunu denetler). Windows'ta dil seçimi `%LOCALAPPDATA%\VideoZamanEtudu\dil.txt` dosyasında saklanır.
- **Demo etüdü aç** bağlantısı örnek etüdü ve videoyu sitenin kendi `Web/demo/` kopyasından tek tıkla açar; İngilizce arayüzde İngilizce demo (`synthetic-warehouse-study`) açılır. Demo videoları `Tools/DemoVideo/demo_video.py` ile üretilir. Kökteki `demo/` değişirse bu kopyayı da güncelle; test ikisinin aynı olduğunu denetler.
- Etütler o tarayıcıya ve o bilgisayara özeldir. Tarayıcı verilerini temizlemek etütleri siler; **Tümünü yedekle (.zip)** ile düzenli yedek al, tek etüdü **dışa aktararak** başka bilgisayara taşı. Yedek, **Etüdü içe al…** ile geri yüklenir; aynı etüt ikinci kez eklenmez.
- Video, açık oturumda bellekte tutulur. Chrome ve Edge dosya iznini hatırlayabilir; diğer tarayıcılarda etüdü yeniden açınca **Videoyu yeniden seç** gerekir.
- Önerilen video biçimi H.264 MP4. Arayüz, kısayollar, ölçüm ve analiz masaüstüyle aynıdır.
- Testler: `cd Web` → `npm test` (Node 22). Siteye konacak dosyalar: `index.html`, `app.css`, `js/`. GitHub Actions bunları **VideoZamanEtudu_Web** paketi olarak da üretir.
- Sayfadaki “Windows sürümünü indir” bağlantısı `index.html` içindeki `data-desktop-url` değerinden gelir (GitHub'daki son sürüm).

## Yayın

- **Tarayıcı:** https://zaman-etudu.ysfdurak.com — Cloudflare Workers, depo köküne bağlı. `wrangler.jsonc` yalnız `Web/` klasörünü statik site olarak yayınlar; `Web/.assetsignore` testleri ve `package.json`'ı dışarıda bırakır, `Web/_headers` güvenlik başlıklarını ekler. `main`'e her gönderim siteyi günceller. Yerelde Cloudflare'in aynısıyla denemek için: `npx wrangler dev`.
- **Windows:** `v2.2.0` gibi bir etiket gönderildiğinde Actions Windows ZIP'ini üretir ve `RELEASE_NOTES.md`'deki ilk bölümle GitHub sürümünü (Release) oluşturur:

```powershell
git tag v2.2.0
git push origin v2.2.0
```

Etiket komutu yerine GitHub'da **Actions → Derleme ve kontroller → Run workflow** ile `surum` alanına `v2.2.0` yazarak da aynı sürümü çıkarabilirsin.

## İlk etüt

### Akış (tarayıcı v1.9, Windows v2.2)

Sekmeler işin sırasını izler: **1 Çalışmalar → 2 Hazırlık → 3 Ölçüm → 4 Analiz → 5 İyileştirme**. İlk açılışta kısa bir tur bunu anlatır; sağ üstteki **Kısa tur** ile yeniden açılır.

1. **Çalışmalar:** Ad ve kod yaz, videoyu seç.
   - Standart sürenin yanında kullanacağın araçları işaretle: hat dengeleme, standart iş, israf ve fikirler, adım kartları. Hiçbirini seçmezsen ekranlar sade kalır; sonradan değiştirilebilir.
   - İstersen **Adımlar ve etüt bilgileri** bölümünde adımları yaz ya da **Adımları şu etütten al** ile benzer bir etütten getir.
   - **Yeni çalışma başlat**.
2. **Hazırlık:** Adım tablosunda her adımın adını, sınıfını (VA/SVA/NVA) ve **bitiş anını** (Ölç'e ne zaman basılacağı) yaz. Kapsamda seçtiysen tür, israf ve fikir de buradadır. Sıra ▲▼ ile değişir. **Başka etütten al…** başka bir etüdün adım listesini getirir. Operatör, istasyon ve vardiya gibi etüt bilgileri de burada.
3. **Ölçüm:** Videoyu adımın başına getir ve **Başla**'ya bas. Adım bitince **Ölç** (Enter): kayıt eklenir, sıradaki adım seçilir. Son adımdan sonra çevrim kendiliğinden 1 artar. Zaman çizgisinde gezinmek ölçümü kesmez.
   - Araya giren bir bekleme için o adımı seç (ya da hızlıca ekle), **Sonraki kayıt kapsam dışı**'yı işaretle ve Ölç'e bas. İşaret kalkar, sıra önceki adıma döner.
   - Adım düğmesindeki sayı geçerli ölçüm / gereken ölçümdür (ör. 3/21); "✓" yeterli demektir. Bütün düğmeler ✓ olunca o etüt için yeterince ölçmüşsündür.
   - Yanlış kaydı **Son kaydı geri al** ya da Ctrl+Z ile geri al.
   - Tek kaydı düzeltmek için satıra tıkla. Birden çok kaydı birlikte değiştirmek için tarayıcıda satır başındaki kutuları işaretle, Windows'ta Ctrl/Shift ile seç.
   - Kayıt temposu, paralel aralık ve adım görseli **Gelişmiş** bölümündedir.
4. **Analiz:** Ayarlar üstte katlı durur; başlığa tıklayıp tempo, tolerans ve takt gir.
   - Alt sekmeler: **Özet** (kartlar ve grafikler), **Adım tablosu** (adıma özel sıklık ve tempo; çift tıklayınca adımın kayıtları açılır), **Çevrimler**, **Dağılım ve aykırılar**.
   - İş bitince **Etüt durumu**nu Tamamlandı ya da Onaylandı yap (onaylayan ve tarih).
   - **Çıktı al** menüsünden Excel, Yazdır/PDF, adım kartları ya da t tablosu.
5. **İyileştirme:** Kapsamda seçtiğin yalın araçlar burada: hat dengeleme, standart iş, fikirler, adım kartları.
6. İyileştirmeden sonra aynı işi yeniden ölç. **Çalışmalar → Önce / sonra karşılaştır…** iki etüdün adım ve toplam farkını gösterir.

### Windows sürümü (v2.2)

Akış tarayıcı sürümüyle aynıdır (yukarıdaki adımlar). Farklar:
- Etütler bu bilgisayarda, Windows kullanıcısına ait yerel kayıtta tutulur; video yolu hatırlanır.
- **Demo etüdü aç** uygulama klasöründeki `demo` klasörünü kullanır (ZIP'te hazır gelir).
- **Yazdır / PDF** Windows yazdırma penceresini açar; PDF için "Microsoft Print to PDF" yazıcısını seç.
- Dil düğmesi (sağ üst) pencereyi seçilen dilde yeniden kurar; açık etüt ve bölüm korunur.
- Video oynatma Windows codec desteğine bağlıdır (H.264 MP4 önerilir).

### Klavye kısayolları (Ölçüm sekmesi)

| Tuş | İşlem |
|---|---|
| Boşluk | Oynat / duraklat |
| Enter | Başla, ardından Ölç |
| 1–9 | Adım seç |
| X | Sonraki kaydı kapsam dışı işaretle / kaldır |
| + / − | Çevrimi artır / azalt |
| Ctrl + Z | Son kaydı geri al |
| Esc | Durdur (kayıt eklemez) |
| ← / → | 0,1 sn geri / ileri |
| Shift + ← / → | 1 sn geri / ileri |
| Ctrl + ← / → | 0,04 sn (25 fps videoda yaklaşık 1 kare) |

Kısayollar imleç bir metin kutusundayken çalışmaz; önce videoya ya da tabloya tıkla. İki sürümde de konum değiştirmek ölçümü kesmez: adımın bittiği kareyi bulup Ölç'e basabilirsin. Ölç başlangıcın gerisindeyse kayıt eklenmez ve uyarı çıkar.

### Etüt silme

**Çalışmalar** ekranında etüdü seçip **Seçili etüdü sil…** düğmesine bas. Etüt ve ölçümleri yalnızca bu bilgisayardaki kayıttan silinir; video dosyasına dokunulmaz. İşlem geri alınamaz, gerekirse önce dışa aktar.

Tekrarlı baz süre yönteminde süreler en yakın grup aralığına yuvarlanır. En sık grubun ham süre ortalaması baz alınır. Eşitlikte düşük ortalamalı grup seçilir. Hiç tekrar yoksa baz süre boş gösterilir ve ortalama yöntemi seçilebilir. %95 güven ve ±%5 hata varsayılandır. Toyota esinli 5/10 gözlem sayısı pratik rehberdir. İstatistiksel yeterlilik ayrı hesaplanır. Tempo manuel girilir. Westinghouse seçeneği doğrulanmış puan tablosu sağlandığında uygulanabilir.

## Windows kabul kontrolü

Tam liste için [Windows kontrol listesi](docs/WINDOWS_KONTROL_LISTESI.md); kodun nasıl çalıştığı için [proje rehberi](docs/PROJE_REHBERI.md).

- Videonun ortasından başlayıp üç ardışık adım kaydet. Ortadaki satırı sil ve bağımsız aralık olarak yeniden ekle. Tarih sırasını ve süreyi doğrula.
- Aynı adlı gözlemlere toplu sınıf ata ve birini kapsam dışı işaretle. Kapsam dışı gözlemin ham listede kaldığını ve analize girmediğini doğrula.
- Uygulamayı kapatıp etüdü yeniden aç. Kayıtları ve ayarları karşılaştır.
- Excel dosyasını açıp ham satırları, süreleri ve ayarları ekranla karşılaştır.

## Bir etüdü başka bilgisayara taşıma

1. **Çalışmalar** ekranında bir kayıt seç ve **Seçili etüdü dışa aktar…** düğmesiyle `.vzetud` dosyası oluştur.
2. Alıcı uygulamada **Etüdü içe al…** düğmesiyle paketi seçer. Etüt, kodu mevcut bir kayıtla aynı olsa bile ayrı bir çalışma olarak eklenir.
3. Alıcı ölçümleri ve analizi video olmadan inceleyebilir. Ölçüm yapmak için **Videoyu yeniden seç** düğmesiyle aynı videoyu kendi bilgisayarından bağlar. Video gerekiyorsa ayrıca paylaşılmalıdır.

Paket ölçüm zamanlarını, adımları, sınıfları, dışlamaları, çevrimleri ve analiz ayarlarını taşır. Videonun kendisi veya gönderenin klasör yolu pakete eklenmez. Etüt adı, kodu, iş adımları ve video dosyasının adı dosyada okunabilir. Şirket içinde kurumun onayladığı paylaşım kanalını kullan.

## Örnek video ve etüt

[Sentetik depo demosu](demo/README.md) üç çevrimi, kapsam dışı beklemeyi, paralel işi ve seyrek kontrolü birlikte gösterir. MP4 video ve `.vzetud` paketi `demo/` klasöründedir. Aynı sayfada beklenen analiz sonuçları ve Windows’ta izlenecek deneme adımları var.

Örnek veriyi hesaplama, aktarım ve iki sürümün eşdeğerlik kontrolleriyle birlikte sınamak için:

```powershell
dotnet run --project .\Checks\Checks.csproj -c Release -- .\demo\sentetik-depo-etudu.vzetud .\Shared\esdeger-ornekler.json
```

Hesap veya paket kuralı değişirse ortak örnek dosyasını yeniden üret ve iki tarafın testlerini çalıştır:

```powershell
dotnet run --project .\Tools\EsdegerUretici -c Release -- .\Shared\esdeger-ornekler.json .\demo\sentetik-depo-etudu.vzetud
```

GitHub Actions, Windows üzerinde WPF projesini derler, bu kontrolleri çalıştırır ve ardından **arayüz öz-denemesini** yürütür: `VideoZamanEtudu.exe --self-test demo <klasör>` pencereyi Türkçe ve İngilizce kurar, demo etüdünü açar; süzgeç, adım ekleme, tolerans kalemleri, takt, adım temposu, Excel, yedek ve karşılaştırma yollarını dener ve her bölümün ekran görüntüsünü kaydeder (yapıt: **VideoZamanEtudu_OzDeneme**). Ayrı bir Linux işinde tarayıcı sürümünün testleri yürür. Yazdırma ve gerçek kullanım için örnek etütle elle kabul kontrolünü de uygulayın.

## Windows yayın paketi

```powershell
dotnet publish .\Desktop\Desktop.csproj -c Release -r win-x64 --self-contained true -o .\Yayin\VideoZamanEtudu
```

GitHub Releases için yayın klasörünün tamamını ZIP’leyin. Kullanıcı ZIP’i çıkardıktan sonra `VideoZamanEtudu.exe` dosyasını açar.

### SDK kurmadan GitHub üzerinden Windows paketi alma

1. Bu klasörün içeriğini, `.github` dizini dahil, GitHub deponuzun köküne gönderin.
2. GitHub’da **Actions → Derleme ve kontroller (Windows + tarayıcı)** altında son çalışmayı açın. İş akışı depoya gönderimde otomatik başlar. Gerekirse **Run workflow** ile elle başlatabilirsiniz.
3. Başarılı çalışmanın **Artifacts** bölümünden **VideoZamanEtudu_Windows_x64** paketini indirin. GitHub dış ZIP’in içinde `VideoZamanEtudu_Windows_x64.zip` dosyasını verir.
4. İç ZIP’i çıkarıp `VideoZamanEtudu.exe` dosyasını açın. Yanındaki dosyaları aynı klasörde tutun. Paket 64 bit Intel/AMD Windows içindir.

## Lisans

Bu proje [PolyForm Shield 1.0.0](LICENSE.md) lisansıyla yayınlanır. Kaynak kodu açıktır, ancak resmi anlamda bir "açık kaynak" lisansı değildir.

- **Serbest:** Kişiler, okullar ve şirketler kendi işlerinde ücretsiz kullanabilir, değiştirebilir ve kendi bilgisayarlarına ya da sunucularına kurabilir. İzin almak gerekmez.
- **Yazılı izin gerekir:** Yazılımı satmak, adını değiştirip ürün gibi sunmak ya da bununla (ücretsiz olsa bile) rakip bir ürün veya hizmet sunmak.
- **Garanti yok:** Yazılım olduğu gibi sunulur. Bağlayıcı olan İngilizce lisans metnidir.
- Üçüncü taraf bileşenler kendi lisanslarına tabidir.

**Kurum içinde kullanım:** Tarayıcı sürümü sunucu gerektirmez; `Web/` klasörünü kurumunuzun iç ağındaki herhangi bir web sunucusunda (IIS, nginx, Apache) yayınlamanız yeterlidir. Video ve etütler kullanıcının tarayıcısında kalır. Windows sürümü kurulum gerektirmez.

Kurumunuzda kullanıyorsanız haber verirseniz sevinirim: kurulumda yardımcı olabilir, kurumunuza özel ihtiyaçları birlikte değerlendirebiliriz. İletişim: iletisim@ysfdurak.com

### License (English)

Released under [PolyForm Shield 1.0.0](LICENSE.md). The source is available, but this is not an OSI "open source" licence.

- **Free:** individuals, schools and companies may use it in their own work, change it and run it on their own computers or servers. No permission needed.
- **Needs written permission:** selling it, rebranding it as a product, or providing a product or service that competes with it (even free of charge).
- **No warranty:** provided as is. The English licence text is binding.
- Third-party components keep their own licences.

**Using it inside an organisation:** the browser version needs no server; publishing the `Web/` folder on any web server in your internal network (IIS, nginx, Apache) is enough. Videos and studies stay in each user's browser. The Windows version needs no installation.

Using it in your organisation? I'd be glad to hear from you: I can help with setup and look at your organisation's needs together. Contact: contact@ysfdurak.com
