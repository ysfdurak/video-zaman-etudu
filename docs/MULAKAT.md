# Mülakatta sorulabilecek sorular · Video İş Etüdü

Bu kart, projeyi kendi ağzınla anlatabilmen için. Cevaplar kısa tutuldu; ayrıntısı belirtilen belgede. Kod yapay zekâ (Claude) ile yazıldı; kapsam, öncelik ve aşağıdaki kararlar senin. Bunu saklama, açıkça söyle: "Ürünü ve kararları ben belirledim, kodu yapay zekâyla yazdım, her kararın nedenini anlatabilirim."

**1. Bu proje ne yapıyor?**
İş adımlarının süresini videodan ölçüyor, standart süreyi hesaplıyor ve sonuçları Excel'e aktarıyor. Windows masaüstü ve tarayıcı olmak üzere iki sürümü var.

**2. İki sürüm aynı sonucu veriyor mu?**
Evet, bunu zorunlu kılıyorum: ortak örnek dosyalarda 61 hesap örneği ve 86 paket senaryosu hem C# hem JavaScript tarafında aynı sonucu vermek zorunda.

**3. Standart süre nasıl hesaplanıyor?**
Gözlenen süre tempo puanıyla düzeltilip normal süreye çevriliyor, tolerans eklenerek standart süre bulunuyor. Kayıt başına tempo, dağılım ve aykırı değer de gösteriliyor.

**4. Yalın araçlar?**
Hat dengeleme (Yamazumi), standart iş kombinasyon tablosu, 7 israf üzerinden iyileştirme fikirleri, adım kartları, iş örneklemesi ve yan yana önce/sonra video.

**5. Video nereye gidiyor?**
Hiçbir yere; bilgisayarda kalıyor. Windows'ta yalnız yolu, tarayıcıda yalnız dosya izni hatırlanıyor.

**6. Güvenlik açığı yaşadın mı?**
Eski sürümlerin içindeki SQLite açıklıydı (CVE-2025-6965). Güncel paket düzeltilmiş sürümü kullanıyor; derleme artık paketteki SQLite eskiyse duruyor.

**7. Çalışan tarafı?**
Video çalışanı gösteriyor; çekim öncesi bilgilendirme ve onay gerekiyor, sonuç bireysel performans ya da disiplin için kullanılmamalı. Operatör adı yerine kod öneriyorum.

**8. Teknik yapı?**
Masaüstü C# / WPF (.NET 8) ve SQLite; tarayıcı sürümü sade JavaScript ve IndexedDB. Aynı .vzetud paket biçimi.

**9. Ölçüm güvenilirliği?**
Aynı videoyu iki kişinin ölçmesi arasındaki fark henüz sistematik ölçülmedi.

**10. Neyi farklı yapardın?**
Bağımlılık sürüm kapısını baştan koyardım ve eski açıklı sürümleri yayından kaldırırdım.


## Bu projede verdiğim kararlar (sorun → karar → bedel)

- **İki dilde aynı hesap:** C# ve JavaScript ayrışabilirdi → ortak eşdeğerlik örnekleri → her değişiklik iki tarafta yapılmalı.
- **SQLite sürüm kapısı:** Açıklı sürüm pakete girebilirdi → derleme duruyor → güncelleme zorunlu hale geldi.

---
Son güncelleme: 19 Kasım 2026.
