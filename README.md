# Video Zaman Etüdü

Video üzerinden iş adımlarını ölçen, standart süreyi hesaplayan ve sonuçları Excel'e aktaran Türkçe zaman etüdü aracı. **İki sürümü vardır**; ikisi aynı hesap kurallarını ve aynı `.vzetud` paket biçimini kullanır, bir sürümde açılan etüt diğerinde açılır.

| | Windows masaüstü (`Desktop/`) | Tarayıcı (`Web/`) |
|---|---|---|
| Teknoloji | C# / WPF, .NET 8 | HTML + JavaScript, bağımlılıksız |
| Kurulum | ZIP'i çıkar, `Desktop.exe` | Adresi aç; sunucu gerekmez |
| Etütlerin yeri | `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite` | Tarayıcının yerel deposu (IndexedDB) |
| Video | Bilgisayarda kalır, yolu hatırlanır | Bilgisayarda kalır, tarayıcıya yüklenmez; Chrome/Edge'de izinle hatırlanır, diğerlerinde her oturumda yeniden seçilir |
| Excel / `.vzetud` | Var | Var (aynı sayfalar ve biçim; tarayıcıda ek **Özet** sayfası) |
| Ölçüm akışı | Adım adı elle, zaman çizgisinde atlama ölçümü keser | Adım listesi, 1–9 tuşları, otomatik çevrim, geri al; gezinmek ölçümü kesmez (v1.5) |
| Analiz ekleri | Tablolar | Grafikler, çevrim değişkenliği, etüt bilgileri, önce/sonra karşılaştırması (v1.5) |

Eşdeğerlik `Shared/esdeger-ornekler.json` ile sınanır: 61 hesap örneği, 175 t değeri ve 86 paket kabul/ret senaryosu hem C# (`Checks`) hem JavaScript (`Web/tests`) tarafında aynı sonucu vermek zorundadır.

Masaüstü program, ilk derlemede NuGet bağımlılıkları yüklendikten sonra internet bağlantısı gerektirmez.

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
- **Dil:** Arayüz Türkçe ve İngilizce. Adresteki `?lang=tr` / `?lang=en` önceliklidir; yoksa son seçim, o da yoksa tarayıcı dili kullanılır. Metinler `Web/js/i18n.js` sözlüğündedir; yeni metin eklerken iki dile de ekle (test anahtarların eşit olduğunu denetler). Windows sürümü şimdilik yalnız Türkçedir.
- **Demo etüdü aç** bağlantısı örnek etüdü ve videoyu sitenin kendi `Web/demo/` kopyasından tek tıkla açar; İngilizce arayüzde İngilizce demo (`synthetic-warehouse-study`) açılır. Demo videoları `Tools/DemoVideo/demo_video.py` ile üretilir. Kökteki `demo/` değişirse bu kopyayı da güncelle; test ikisinin aynı olduğunu denetler.
- Etütler o tarayıcıya ve o bilgisayara özeldir. Tarayıcı verilerini temizlemek etütleri siler; **dışa aktararak** yedekle veya başka bilgisayara taşı.
- Video, açık oturumda bellekte tutulur. Chrome ve Edge dosya iznini hatırlayabilir; diğer tarayıcılarda etüdü yeniden açınca **Videoyu yeniden seç** gerekir.
- Önerilen video biçimi H.264 MP4. Arayüz, kısayollar, ölçüm ve analiz masaüstüyle aynıdır.
- Testler: `cd Web` → `npm test` (Node 22). Siteye konacak dosyalar: `index.html`, `app.css`, `js/`. GitHub Actions bunları **VideoZamanEtudu_Web** paketi olarak da üretir.
- Sayfadaki “Windows sürümünü indir” bağlantısı `index.html` içindeki `data-desktop-url` değerinden gelir (GitHub'daki son sürüm).

## Yayın

- **Tarayıcı:** https://zaman-etudu.ysfdurak.com — Cloudflare Workers, depo köküne bağlı. `wrangler.jsonc` yalnız `Web/` klasörünü statik site olarak yayınlar; `Web/.assetsignore` testleri ve `package.json`'ı dışarıda bırakır, `Web/_headers` güvenlik başlıklarını ekler. `main`'e her gönderim siteyi günceller. Yerelde Cloudflare'in aynısıyla denemek için: `npx wrangler dev`.
- **Windows:** `v1.2.0` gibi bir etiket gönderildiğinde Actions Windows ZIP'ini üretir ve `RELEASE_NOTES.md`'deki ilk bölümle GitHub sürümünü (Release) oluşturur:

```powershell
git tag v1.2.0
git push origin v1.2.0
```

Etiket komutu yerine GitHub'da **Actions → Derleme ve kontroller → Run workflow** ile `surum` alanına `v1.2.0` yazarak da aynı sürümü çıkarabilirsin.

## İlk etüt

### Tarayıcı sürümü (v1.5)

1. **Çalışmalar** ekranında ad ve kod yaz, videoyu seç. İstersen **Adımlar ve etüt bilgileri** bölümüne süreç adımlarını (her satıra bir) ve operatör, istasyon, vardiya gibi bilgileri gir. **Yeni çalışma başlat**.
2. **Video ve ölçüm** ekranında adım listesi sağdadır. Eksik adımı **Yeni adım** kutusuna yazıp Enter'a bas. Seçili adımın sınıfını (VA/SVA/NVA) listedeki **Sınıf** kutusundan seç; kayıtlar sınıfı adımdan alır.
3. Videoyu adımın başına getir ve **Başla**'ya bas. Adım bitince **Ölç**: kayıt eklenir, sıradaki adım seçilir. Son adımdan sonra çevrim kendiliğinden 1 artar. Adım sınırını bulmak için zaman çizgisinde gezinebilirsin; ölçüm kesilmez.
4. Araya giren bir bekleme için o adımı seç (ya da ekle), **Sonraki kayıt kapsam dışı**'yı işaretle ve Ölç'e bas. İşaret kalkar, sıra önceki adıma döner.
5. Yanlış kaydı **Son kaydı geri al** ya da Ctrl+Z ile geri al. Eski bir kaydı düzeltmek için tabloda satıra tıkla; **Seçili kayıt** kutusunda değiştir.
6. **Analiz ve rapor** ekranında tempo ve toleransı gir; değerler alan değişince uygulanır. Grafikleri ve yeterliliği incele, **Excel'e aktar** ile özet ve ham sayfaları indir.
7. İyileştirmeden sonra aynı işi yeniden ölç. **Çalışmalar → Önce / sonra karşılaştır…** iki etüdün adım ve toplam farkını gösterir.

### Windows sürümü (v1.2)

1. Çalışmalar ekranında ad ve kod yaz. MP4 video seç ve yeni çalışmayı başlat. Video oynatma Windows codec desteğine bağlıdır.
2. Videoda başlangıç konumuna git ve **Başla**’ya bas. Adım adı ve çevrim numarasını seç. Adım sonunda **Ölç**’e bas. Her Ölç işlemi sonraki adımın başlangıcını belirler. **Durdur** yeni kayıt eklemeden ölçüm modunu kapatır. Hızlı adımlarda **Hız** listesinden 0,5× veya 0,25× seçebilirsin; ölçüm video zamanından alındığı için süreler değişmez.
3. Bir kayda tıklayınca video o adımın başına gider. Sınıfı, çevrim numarasını, adım adını veya kapsam dışı durumunu düzenleyip **Seçili satırı güncelle**’ye bas. Aynı ada sahip satırlara toplu sınıf verebilirsin.
4. Bağımsız veya çakışan bir aralık için önce **Aralık başlangıcı al**, bitişe gittikten sonra **Aralığı ekle**. Paralel işleri aynı çevrim numarasıyla kaydet. Ölçümlerin dışındaki boşluklar, ölçülen geçen süreye eklenmez.
5. Analizde ayarları uygula. Seyrek bir adım için satırını seçip her kaç çevrimde bir gerçekleştiğini gir. **Excel’e aktar** ile ham ve işlenmiş veriyi kaydet.

### Klavye kısayolları (Video ve ölçüm ekranı)

| Tuş | İşlem |
|---|---|
| Boşluk | Oynat / duraklat |
| Enter | Başla, ardından Ölç |
| 1–9 | Adım seç (tarayıcı) |
| + / − | Çevrimi artır / azalt (tarayıcı) |
| Ctrl + Z | Son kaydı geri al (tarayıcı) |
| Esc | Durdur (kayıt eklemez) |
| ← / → | 0,1 sn geri / ileri |
| Shift + ← / → | 1 sn geri / ileri |
| Ctrl + ← / → | 0,04 sn (25 fps videoda yaklaşık 1 kare) |

Kısayollar imleç bir metin kutusundayken çalışmaz; önce videoya ya da tabloya tıkla. Tarayıcı sürümünde konum değiştirmek ölçümü kesmez. Windows sürümünde ok tuşlarıyla ince konumlama (en fazla 1 sn) ölçümü kesmez; zaman çizgisinde atlamak ya da satır seçmek keser.

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

GitHub Actions, Windows üzerinde WPF projesini derler ve bu kontrolleri çalıştırır; ayrı bir Linux işinde tarayıcı sürümünün testlerini yürütür. Video oynatma, arayüz ve Excel çıktısı için örnek etütle manuel kabul kontrolünü uygulayın.

## Windows yayın paketi

```powershell
dotnet publish .\Desktop\Desktop.csproj -c Release -r win-x64 --self-contained true -o .\Yayin\VideoZamanEtudu
```

GitHub Releases için yayın klasörünün tamamını ZIP’leyin. Kullanıcı ZIP’i çıkardıktan sonra `Desktop.exe` dosyasını açar.

### SDK kurmadan GitHub üzerinden Windows paketi alma

1. Bu klasörün içeriğini, `.github` dizini dahil, GitHub deponuzun köküne gönderin.
2. GitHub’da **Actions → Derleme ve kontroller (Windows + tarayıcı)** altında son çalışmayı açın. İş akışı depoya gönderimde otomatik başlar. Gerekirse **Run workflow** ile elle başlatabilirsiniz.
3. Başarılı çalışmanın **Artifacts** bölümünden **VideoZamanEtudu_Windows_x64** paketini indirin. GitHub dış ZIP’in içinde `VideoZamanEtudu_Windows_x64.zip` dosyasını verir.
4. İç ZIP’i çıkarıp `Desktop.exe` dosyasını açın. Yanındaki dosyaları aynı klasörde tutun. Paket 64 bit Intel/AMD Windows içindir.
