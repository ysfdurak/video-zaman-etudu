# Video İş Etüdü · KVKK ve çalışan bilgilendirme

> **Not:** Bu belge bir başlangıç şablonudur, hukuki görüş değildir. Kullanmadan önce kurumunun hukuk ya da KVKK sorumlusuna onaylat. Veri sorumlusu uygulamayı kuran kurumdur; bu depo yalnızca aracı sağlar.

Video İş Etüdü, iş adımlarının süresini videodan ölçer. Video çalışanın görüntüsünü içerir; görüntü ve ona bağlanan süre ölçümleri kişisel veridir.

## Hangi kişisel veriler işleniyor?

| Veri | Nereden gelir | Neden gerekir |
|---|---|---|
| Video (çalışanın görüntüsü) | Kurum çeker | Adımları izleyip ölçmek |
| Etüt: istasyon, operatör adı ya da kodu, tarih, ölçülen süreler, tempo puanı | Etüdü yapan girer | Standart süre hesabı |
| Notlar ve iyileştirme fikirleri | Etüdü yapan girer | Süreç iyileştirme |

## Nerede durur, kim görür, ne kadar kalır?

- **Yer:** Video bilgisayarda kalır; uygulama videoyu hiçbir yere yüklemez (Windows'ta yalnız dosya yolu, tarayıcıda yalnız dosya izni hatırlanır). Etütler Windows'ta `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite`, tarayıcıda tarayıcının yerel deposunda (IndexedDB) tutulur.
- **Kim görür:** O bilgisayarı kullanan herkes. Ortak bilgisayarda etüt ve video klasörünü kişisel Windows hesabında tut.
- **Ne kadar:** Uygulama videoyu silmez; analizden sonra video dosyasını kurum siler. Etüt kaydı süreleri içerir; operatör adı yerine kod kullanmak riski azaltır.

## Kurumun yapması gerekenler

1. **Aydınlatma:** Çalışanlara, verinin hangi amaçla, hangi hukuki sebeple, kimler tarafından ve ne kadar süre işlendiğini yazılı olarak bildir (aşağıdaki duyuru şablonu başlangıç olabilir).
2. **Amaçla sınırla:** Toplanan veri yalnız yazılan amaç için kullanılır. Başka bir amaca (ör. disiplin, bireysel performans sıralaması) kaydırılacaksa yeniden değerlendir ve çalışanı bilgilendir.
3. **Erişimi daralt:** Yalnız işi gereği görmesi gerekenlere hesap aç; ayrılanların hesabını aynı gün kapat.
4. **Saklama süresini yaz ve uygula:** Süre dolan kayıtları sil ya da anonimleştir; silme tarihini kayıt altına al.
5. **Kayıt envanteri:** Bu işlemeyi kurumun kişisel veri envanterine ekle; VERBİS kaydı gerekiyorsa güncelle.
6. **Talepler:** Çalışanın KVKK 11. madde kapsamındaki taleplerine (bilgi, düzeltme, silme) kimin, kaç gün içinde cevap vereceğini belirle.

## Zaman Etüdü'ne özel noktalar

- **Operatör adı yerine kod:** Etütte operatör alanına ad yerine kod (ör. OP-03) yaz; kod listesini ayrı ve kilitli tut.
- **Tempo puanı kişiye dairdir:** Tempo puanı standart süreyi düzeltmek içindir. Bireysel hız karşılaştırması ya da sıralama olarak paylaşma.
- **Yüz görünmesin:** Mümkünse kamerayı eller ve iş alanı görünecek açıdan kur.
- **Paylaşılan paketler:** `.vzetud` paketi ve .zip yedeği videoyu içermez ama ölçümleri ve notları içerir; kurum dışına gönderirken operatör alanını kontrol et.

## Çalışan duyurusu (şablon)

Köşeli parantezli yerleri doldur.

> [Kurum adı] olarak [istasyon / hat] üzerindeki iş adımlarının süresini ölçmek için zaman etüdü yapıyoruz. Bunun için çalışırken kısa süre videoya alınabilirsin. Video yalnız [bilgisayar] üzerinde tutulur, internete yüklenmez ve analiz bitince [n gün içinde] silinir; raporda yalnız adım süreleri kalır, adın yerine kod kullanılır. Amaç işin akışını ve standart süreyi doğru belirlemek; sonuçlar bireysel performans değerlendirmesinde ya da disiplin sürecinde kullanılmaz. Çekime katılmak istemezsen [kişi]'ye söylemen yeterli. Soruların ve KVKK kapsamındaki talepler için: [kişi / e-posta].

## Çekim öncesi bilgilendirme ve onay (şablon)

Videoya alınacak her çalışana çekimden **önce** okut ve imzalat. Çalışan istemezse çekim yapma ya da yüzü görünmeyecek açıdan çek.

> **Konu:** [İstasyon / iş] için zaman etüdü
> **Ne kaydedilecek:** [Tarih] günü [süre] boyunca, iş istasyonunda çalışırken video.
> **Amaç:** İş adımlarının süresini ölçüp standart süreyi ve iş akışını iyileştirmek. Kayıt bireysel performans değerlendirmesinde, disiplin sürecinde ya da başka bir amaçla kullanılmaz.
> **Nerede tutulacak:** Video yalnız [bilgisayar / klasör] üzerinde durur, internete yüklenmez. Uygulama videoyu hiçbir sunucuya göndermez.
> **Kim görecek:** [Analizi yapan kişi(ler)]. Raporlarda yüzün görünmez ya da [bulanıklaştırılır / kırpılır].
> **Ne kadar kalacak:** Analiz bitince [en geç n gün içinde] video silinir; raporda yalnız süreler ve açılar kalır.
> **Hakların:** Çekime katılmak zorunda değilsin; istediğin an durdurmamı isteyebilirsin. Bilgi, düzeltme ve silme talebin için: [kişi / e-posta].
>
> Okudum, anladım. Çekime katılmayı kabul ediyorum.  Ad soyad: ________  Tarih: ________  İmza: ________

**Sendikalı işyerlerinde** çalışmaya başlamadan önce işyeri temsilcisini bilgilendir; zaman etüdü ve ergonomi ölçümleri toplu sözleşmede ayrıca düzenlenmiş olabilir.

---
Son güncelleme: 19 Kasım 2026.
