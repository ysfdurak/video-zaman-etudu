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
