# Kontrol listesi (v1.2.0) — Windows ve tarayıcı

Otomatik kontroller hesaplama ve paket tarafını doğrular. Aşağıdakiler yalnızca Windows'ta, elle doğrulanabilir. Her satırı işaretle; geçmeyen satırı not al.

## 0. Hazırlık
- [ ] GitHub Actions çalışması yeşil: “WPF uygulamasını derle”, “Hesaplama, etüt aktarımı ve eşdeğerlik kontrollerini çalıştır” (çıktıda `Ek kontroller: 56 geçti, 0 kaldı.` ve `Eşdeğerlik kontrolleri: 322/322 geçti.`), “Tarayıcı sürümü” işi yeşil, “Windows x64 taşınabilir paketi oluştur”.
- [ ] Artifacts'tan `VideoZamanEtudu_Windows_x64` indirildi; içteki ZIP temiz bir klasöre çıkarıldı. Klasörde `Desktop.exe`, `BASLAT.txt` ve `demo/` var.
- [ ] (A11) .NET SDK kurulu olmayan, standart (yönetici olmayan) bir Windows hesabında `Desktop.exe` açılıyor. SmartScreen uyarısı çıkarsa not al (imzasız paket; şirket BT süreci).

## 1. Demo etüt (A10, A04)
- [ ] Çalışmalar → **Etüdü içe al…** → `demo/sentetik-depo-etudu.vzetud`. Başlıkta `DEMO-01 · Sentetik depo etüdü` görünüyor.
- [ ] Video bağlı değil mesajı ve beklenen dosya adı görünüyor; Analiz sekmesi açılabiliyor.
- [ ] **Videoyu yeniden seç** → `demo/sentetik-depo-etudu.mp4`. 5. saniyedeki **Tarama** satırına tıklayınca video 00:05.000'e gidiyor.
- [ ] Analiz: Toplama baz/normal/standart 5,333 / 5,867 / 6,902; Seyrek kontrol katkısı 0,863; çevrim geçen süreleri 10/11/12; iş yükleri 12/13/14; Bekleme n=0, dışarıda=1.
- [ ] **Excel'e aktar…** önerilen ad `DEMO-01_analiz.xlsx`. Excel'de 5 sayfa var; ham ölçümlerde 14 satır, Bekleme “Kapsam dışı: Evet”. Sayılar ekranla aynı. Sütunlar başlıkları kesmeden okunuyor. Ayarlar sayfasında video adı, kayıt sayıları ve “Program sürümü 1.2.0” var.

## 2. Ölçüm (A01, A02)
- [ ] Aynı videoyla yeni etüt aç. 2. saniyede **Başla**, oynat, arada 10 sn kadar duraklat, tekrar oynat, 7. saniyede **Ölç** → yaklaşık 5 sn'lik kayıt.
- [ ] Ölçüm açıkken zaman çizgisinde ileri atla → “Konum değişti” mesajı, düğme yeniden **Başla**. **Durdur** yeni satır eklemiyor.
- [ ] Kısayollar: tablo satırına tıkla, sonra Boşluk (oynat/duraklat), Enter (Başla/Ölç), Esc (Durdur). İmleç “İş adımı” kutusundayken Enter ölçüm yapmıyor.
- [ ] Ölçüm açıkken → ve Ctrl+→ ile ince ilerle, sonra Enter: ölçüm kesilmeden kayıt ekleniyor. Başlangıcın gerisine ← ile gidince uyarı çıkıyor.
- [ ] Hız 0,5× ile oynat; 2 sn → 7 sn ölçümü yine ~5 sn.

## 3. Kayıt düzenleme (A03)
- [ ] Ardışık üç adım kaydet, ortadakini sil; diğer iki satırın zamanları aynı. **Aralık başlangıcı al** / **Aralığı ekle** ile yeniden ekle → doğru sıraya geliyor.
- [ ] Aynı adlı iki satıra toplu sınıf uygula; analiz sayıları değişmiyor. Birini kapsam dışı yap → ham listede ve Excel'de duruyor, analizde “Dışarıda” sütununa geçiyor.

## 4. Yerel kayıt ve taşıma (A04, A05)
- [ ] Ayarları değiştir (ör. tempo 105), uygulamayı kapat-aç: ölçümler ve ayarlar geri geliyor.
- [ ] Videoyu başka klasöre taşı, etüdü aç: analiz çalışıyor, video yeniden bağlanabiliyor.
- [ ] **Seçili etüdü dışa aktar…** önerilen ad etüt kodu. Dosyayı Not Defteri'nde aç: Türkçe harfler okunur, bilgisayar klasör yolu yok.
- [ ] Aynı paketi iki kez içe al → iki ayrı satır; eskiler duruyor. İçeriği bozulmuş bir dosya (ör. ilk satırı silinmiş) → hata mesajı, yeni satır yok.

## 5. Silme ve dayanıklılık
- [ ] Açık olan etüdü sil → onay sorusu; sonrasında Çalışmalar ekranına dönülüyor, diğer sekmeler pasif, video dosyası yerinde.
- [ ] (İsteğe bağlı, uygulama kapalıyken) `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite` dosyasını yedekle; bir DB aracıyla bir satırın `data` değerini bozuk metne çevir → uygulama açılıyor, “1 etüt kaydı okunamadı” uyarısı çıkıyor. Sonra yedeği geri koy.

## 6. Tarayıcı sürümü (Chrome veya Edge; isteğe bağlı Firefox/Safari)
Otomatik uçtan uca test Chrome'da 48 adımı kapsadı. Kendi bilgisayarında şunları doğrula:
- [ ] Siteden (veya `Web` klasöründe `python -m http.server`) açılıyor; başlıkta “Tarayıcı sürümü”.
- [ ] Sağ üstteki **English** ile arayüz, analiz tablosu (6.902, Insufficient) ve Excel (“Step analysis”) İngilizceye geçiyor; sayfa yenilenince seçim korunuyor; `?lang=tr` Türkçe açıyor.
- [ ] **Demo etüdü aç** tek tıkla DEMO-01'i ve videoyu açıyor; ikinci tıklama yeni kayıt oluşturmuyor.
- [ ] Demo `.vzetud` içe al → video bağlı değil mesajı → **Videoyu yeniden seç** ile mp4 → 5. saniyedeki Tarama satırı videoyu 00:00:05.000'e götürüyor.
- [ ] Bölüm 2'deki ölçüm adımları (Başla/Ölç, duraklatma, atlama, kısayollar, ince konumlama, 0,5×) aynı sonucu veriyor.
- [ ] Excel indirildi, Excel'de açıldı; ekranla aynı sayılar. Ayarlar sayfasında “1.2.0 (tarayıcı)”.
- [ ] Tarayıcıdan dışa aktarılan `.vzetud` masaüstü sürümünde açılıyor; masaüstünden aktarılan da tarayıcıda.
- [ ] Sayfayı kapatıp aç: etütler ve ayarlar duruyor. Chrome/Edge'de video için yalnız izin soruluyor; Firefox/Safari'de video yeniden seçiliyor.
- [ ] Gizli pencerede açınca kapatıldığında etütlerin silineceği bilinmeli (tarayıcı davranışı).

## Sonuç
| Kriter | Durum (Geçti / Kaldı / Doğrulanmadı) | Not |
|---|---|---|
| A01 | | |
| A02 | | |
| A03 | | |
| A04 | | |
| A05 | | |
| A10 (Excel) | | |
| A11 | | |
| Kısayol / hız / silme | | |
| Tarayıcı sürümü | | |
