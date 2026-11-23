"""Wikimedia Commons'tan açık lisanslı örnek video/fotoğraf arar ve indirir (yalnız CI'da çalışır; çıktı ornek-veri dalına yazılır).
istek.json: {"ara": ["sorgu", ...]} ya da {"indir": [{"dosya": "File:...", "ad": "kisa-ad"}]}"""
import json, os, sys, urllib.parse, urllib.request

API = "https://commons.wikimedia.org/w/api.php"
UA = {"User-Agent": "ysfdurak-ornek-veri/1.0 (https://github.com/ysfdurak/video-zaman-etudu)"}
CIKTI = sys.argv[2] if len(sys.argv) > 2 else "cikti"
os.makedirs(CIKTI, exist_ok=True)


def get(params):
    q = API + "?" + urllib.parse.urlencode({**params, "format": "json"})
    with urllib.request.urlopen(urllib.request.Request(q, headers=UA), timeout=60) as r:
        return json.load(r)


def bilgi(titles):
    r = get({"action": "query", "titles": "|".join(titles), "prop": "imageinfo", "iiprop": "url|size|mime|extmetadata|mediatype", "iiurlwidth": 480})
    out = []
    for p in r.get("query", {}).get("pages", {}).values():
        ii = (p.get("imageinfo") or [{}])[0]
        em = ii.get("extmetadata", {})
        v = lambda k: (em.get(k) or {}).get("value")
        out.append({"dosya": p.get("title"), "url": ii.get("url"), "thumb": ii.get("thumburl"), "w": ii.get("width"), "h": ii.get("height"),
                    "boyut": ii.get("size"), "sure": ii.get("duration"), "mime": ii.get("mime"), "lisans": v("LicenseShortName"),
                    "yazar": v("Artist"), "aciklama": (v("ImageDescription") or "")[:300], "sayfa": ii.get("descriptionurl")})
    return out


def indir(url, yol):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r, open(yol, "wb") as f:
        while True:
            b = r.read(1 << 20)
            if not b:
                break
            f.write(b)


istek = json.load(open(sys.argv[1], encoding="utf-8"))
if "ara" in istek:
    hepsi = {}
    for s in istek["ara"]:
        r = get({"action": "query", "list": "search", "srsearch": s, "srnamespace": 6, "srlimit": 25})
        titles = [x["title"] for x in r.get("query", {}).get("search", [])]
        for i in range(0, len(titles), 20):
            for b in bilgi(titles[i:i + 20]):
                b["sorgu"] = s
                hepsi[b["dosya"]] = b
    liste = sorted(hepsi.values(), key=lambda x: (x["sorgu"], x["dosya"]))
    os.makedirs(os.path.join(CIKTI, "kucuk"), exist_ok=True)
    for i, b in enumerate(liste):
        b["no"] = i
        if b.get("thumb"):
            try:
                indir(b["thumb"], os.path.join(CIKTI, "kucuk", "%03d.jpg" % i))
            except Exception as e:
                b["thumbHata"] = str(e)[:100]
    json.dump(liste, open(os.path.join(CIKTI, "adaylar.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(len(liste), "aday")
def turevler(dosya):
    r = get({"action": "query", "titles": dosya, "prop": "videoinfo", "viprop": "derivatives"})
    for p in r.get("query", {}).get("pages", {}).values():
        return (p.get("videoinfo") or [{}])[0].get("derivatives") or []
    return []


if "indir" in istek:
    kayit = []
    for x in istek["indir"]:
        try:
            b = bilgi([x["dosya"]])[0]
            url = b["url"]
            if x.get("transcode") and (b.get("mime") or "").startswith(("video", "application/ogg")):
                tv = turevler(x["dosya"])
                hedef = int(x["transcode"].split("p")[0])
                adaylar = sorted([d for d in tv if d.get("type", "").startswith("video/webm") and d.get("height")], key=lambda d: abs(d["height"] - hedef))
                if adaylar:
                    url = adaylar[0]["src"]
            uz = url.rsplit(".", 1)[1].split("?")[0]
            yol = os.path.join(CIKTI, x["ad"] + "." + uz)
            indir(url, yol)
            b["yerel"] = os.path.basename(yol); b["indirilen"] = url
            print("indirildi", yol, os.path.getsize(yol))
        except Exception as e:
            b = {"dosya": x["dosya"], "hata": str(e)[:200]}
            print("HATA", x["dosya"], e)
        kayit.append(b)
    json.dump(kayit, open(os.path.join(CIKTI, "kaynaklar.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
