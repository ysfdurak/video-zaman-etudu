# Sentetik depo etüdü

35 saniyelik animasyon üç toplama çevrimini, kapsam dışı beklemeyi, paralel taramayı ve seyrek kontrolü gösterir. Video ve etüt verisi bu demo için üretilmiştir.

1. Uygulamada **Çalışmalar → Etüdü içe al…** ile `sentetik-depo-etudu.vzetud` dosyasını aç.
2. **Video ve ölçüm → Videoyu yeniden seç** ile `sentetik-depo-etudu.mp4` dosyasını bağla. Örneğin 5. saniyedeki **Tarama** satırını seç ve videonun o konuma gittiğini kontrol et.
3. **Analiz ve rapor** ekranında aşağıdaki sonuçları karşılaştır. **Excel’e aktar…** ile ham ölçüm ve analiz sayfalarını kontrol et.
4. Manuel ölçümü de denemek için aynı videoyla yeni bir etüt aç. Videoda 2. saniyede **Başla**, 7. saniyede **Ölç** seç. Oynatıcıdaki konumu esas alan yaklaşık 5 saniyelik bir kayıt oluşmalı.

| Kontrol | Beklenen |
|---|---:|
| Ham kayıt | 14 |
| Kapsam dışı bekleme | 1 kayıt, 2 sn |
| Toplama gözlemleri | 5 / 6 / 5 sn |
| Toplama baz / normal / standart süre | 5,333 / 5,867 / 6,902 sn |
| Seyrek kontrol | Her 3 çevrimde bir, 0,863 standart sn/çevrim |
| Çevrimlerin ölçülen geçen süresi | 10 / 11 / 12 sn |
| Çevrimlerin toplam iş yükü | 12 / 13 / 14 sn |

Analiz ayarları %110 tempo ve %15 toleranstır. Tarama her çevrimde toplam iş yüküne iki saniye ekler. Aynı zaman dilimi toplamayla çakıştığı için geçen süreye ikinci kez eklenmez.

## English demo

`synthetic-warehouse-study.vzetud` and `synthetic-warehouse-study.mp4` are the same study with English step names (Preparation, Picking, Scanning, Packing, Waiting, Periodic check) and an English-labelled video. Timestamps, settings and results are identical to the Turkish demo (Picking standard time 6.902 s, periodic check 0.863 s/cycle). The browser version opens it with **Open demo study** when the interface is in English; the Windows version can import it as well.

Videolar `Tools/DemoVideo/demo_video.py` ile üretilir (`tr` veya `en`). Bir demo dosyası değişirse `Web/demo/` kopyasını da güncelle; test ikisinin aynı olduğunu denetler.
