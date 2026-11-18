// Video Zaman Etüdü — çevrimdışı çalışma (tarayıcı v1.9.4).
// Yalnız bu sitenin kendi dosyalarını önbelleğe alır; başka hiçbir adrese istek göndermez ve
// kullanıcının videosuna ya da etütlerine dokunmaz (onlar zaten tarayıcıda, IndexedDB'de).
// Strateji: önce ağ (her zaman en yeni sürüm), ağ yoksa önbellek.
// Sürüm değişince CACHE adı değişir; eski önbellek "activate" adımında silinir.
// Testler (tests/sw.test.mjs): CACHE sürümü core.js VERSION ile aynı, FILES listesi Web/ altındaki
// bütün js dosyalarını ve demoları içermeli.
const CACHE = "vze-1.9.4";
const FILES = [
  "./",
  "index.html",
  "404.html",
  "app.css",
  "js/app.js",
  "js/core.js",
  "js/advanced.js",
  "js/charts.js",
  "js/report.js",
  "js/backup.js",
  "js/store.js",
  "js/i18n.js",
  "js/strict-json.js",
  "js/xlsx.js",
  "demo/sentetik-depo-etudu.vzetud",
  "demo/sentetik-depo-etudu.mp4",
  "demo/synthetic-warehouse-study.vzetud",
  "demo/synthetic-warehouse-study.mp4"
];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(FILES)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k.startsWith("vze-") && k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  const url = new URL(request.url);
  // Yalnız kendi adresimizdeki GET istekleri; kısmi (Range) istekler tarayıcıya bırakılır.
  if (request.method !== "GET" || url.origin !== self.location.origin || request.headers.has("range")) return;
  event.respondWith(
    fetch(request)
      .then((response) => {
        if (response.ok && response.status === 200) {
          const copy = response.clone();
          caches.open(CACHE).then((cache) => cache.put(request, copy));
        }
        return response;
      })
      .catch(async () => {
        const cached = await caches.match(request, { ignoreSearch: true });
        if (cached) return cached;
        // Adres çubuğundan açılan sayfa (ör. "/?lang=en") → uygulamanın kendisi.
        if (request.mode === "navigate") return caches.match("index.html");
        return Response.error();
      })
  );
});
