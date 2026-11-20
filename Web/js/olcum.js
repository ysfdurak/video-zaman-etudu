// Anonim kullanım ölçümü ve geri bildirim düğmesi (ortak servis: olcum.ysfdurak.com; kaynak: ysfdurak.com → sunucu/olcum).
// Yalnız resmî demo adresinde yüklenir: kendi sunucusuna/şirket içine kurulan kopyalar dışarıya hiç istek atmaz.
// Gönderilenler: ekran adı, aktif süre, cihaz türü, dil. Video, etüt verisi, ad, IP gönderilmez; çerez yok; DNT/GPC'ye uyar.
if (location.hostname === "zaman-etudu.ysfdurak.com") {
  const s = document.createElement("script");
  s.src = "https://olcum.ysfdurak.com/olcum.js";
  s.defer = true;
  s.dataset.proje = "is-etudu";
  s.dataset.ekran = "otomatik";
  s.dataset.renk = "#1f5f8b";
  document.head.appendChild(s);
}
