# v1.2.0 · Video Zaman Etüdü — tarayıcı sürümü

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
