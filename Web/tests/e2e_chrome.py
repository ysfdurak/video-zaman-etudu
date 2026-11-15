"""Tarayıcı sürümü uçtan uca testi (isteğe bağlı, yerelde).

Gerekenler: Python 3, `pip install playwright`, Google Chrome (H.264 MP4 için; Playwright'ın Chromium'u H.264 oynatmaz).
Çalıştırma (depo kökünde):
    cd Web && python -m http.server 8765 --bind 127.0.0.1      # ayrı pencerede
    python Web/tests/e2e_chrome.py                             # CHROME=... ile Chrome yolu verilebilir
Ekran görüntüleri ve indirilen dosyalar Web/tests/e2e-cikti/ altına yazılır (depoya eklenmez).
"""
import asyncio, json, sys, os, re, time, pathlib
from playwright.async_api import async_playwright
ROOT = str(pathlib.Path(__file__).resolve().parents[2])
URL = os.environ.get("URL", "http://127.0.0.1:8765/index.html")
OUT = os.path.join(ROOT, "Web", "tests", "e2e-cikti"); os.makedirs(OUT, exist_ok=True)
results = []
def check(name, ok, detail=""):
    results.append((ok, name, detail)); print(("GEÇTİ " if ok else "KALDI ") + name + (f"  [{detail}]" if detail else ""), flush=True)

async def main():
    async with async_playwright() as p:
        browser = await p.chromium.launch(executable_path=os.environ.get("CHROME", "/usr/bin/google-chrome"), args=["--autoplay-policy=no-user-gesture-required"])
        ctx = await browser.new_context(accept_downloads=True, viewport={"width": 1440, "height": 900}, locale="tr-TR", bypass_csp=True)
        await ctx.add_init_script("window.showOpenFilePicker = undefined;")
        page = await ctx.new_page()
        errors = []
        page.on("pageerror", lambda e: errors.append(str(e)))
        page.on("console", lambda m: errors.append(m.text) if m.type == "error" else None)
        await page.goto(URL)
        await page.wait_for_selector("#studies tbody tr")
        check("açılış: boş liste mesajı", "Henüz etüt yok" in await page.inner_text("#studies tbody"))
        # 0 tek tıkla demo (sitenin kendi demo dosyaları)
        await page.click("#demo")
        await page.wait_for_function("document.getElementById('status').textContent.includes('Video hazır')", timeout=15000)
        check("Demo etüdü aç: etüt ve video tek tıkla", "DEMO-01" in await page.inner_text("#header") and await page.locator("#observations tbody tr").count() == 14)
        await page.click("#tab-0"); await page.click("#demo"); await page.wait_for_timeout(500); await page.click("#tab-0")
        check("Demo ikinci kez eklenmez", await page.locator("#studies tbody tr", has_text="DEMO-01").count() == 1)
        await page.locator("#studies tbody tr", has_text="DEMO-01").click(); await page.click("#delete"); await page.click("#dialog-yes")
        await page.wait_for_timeout(400)

        async def dialog_text(): 
            await page.wait_for_selector("dialog[open]"); return await page.inner_text("#dialog-text")
        async def answer(yes=True):
            await page.click("#dialog-yes" if yes else "#dialog-no"); await page.wait_for_selector("dialog[open]", state="detached"); await page.wait_for_timeout(300)
        status = lambda: page.inner_text("#status")
        pos = lambda: page.evaluate("Math.round(document.getElementById('video').currentTime*1000)")

        # 1 demo içe al
        async with page.expect_file_chooser() as fc:
            await page.click("#import")
        await (await fc.value).set_files(f"{ROOT}/demo/sentetik-depo-etudu.vzetud")
        t = await dialog_text(); check("içe alma onayı", "DEMO-01" in t, t[:60]); await answer()
        await page.wait_for_selector("#panel-1:not([hidden])")
        await page.wait_for_function("document.getElementById('status').textContent.includes('Video hazır')", timeout=15000)
        check("demo videosu bekleyen etüde video kendiliğinden bağlanır", True)
        check("başlık etüt kodu", "DEMO-01 · Sentetik depo etüdü" in await page.inner_text("#header"))
        check("14 ölçüm satırı", await page.locator("#observations tbody tr").count() == 14)

        # 2 analiz (video bağlı değilken)
        await page.click("#tab-2")
        rows = await page.eval_on_selector_all("#results tbody tr", "rs => rs.map(r => [...r.cells].map(c => c.textContent))")
        d = {r[0]: r for r in rows}
        check("A10 toplama 5,333/5,867/6,902", [d["Toplama"][11], d["Toplama"][12], d["Toplama"][13]] == ["5,333", "5,867", "6,902"], str(d["Toplama"][11:14]))
        check("A10 seyrek katkı 0,863", d["Seyrek kontrol"][15] == "0,863")
        check("A10 bekleme n=0, dışarıda 1", d["Bekleme"][1:3] == ["0", "1"])
        cyc = await page.eval_on_selector_all("#cycles tbody tr", "rs => rs.map(r => [...r.cells].map(c => c.textContent))")
        check("A10 çevrimler 10/11/12 ve 12/13/14", cyc == [["1","10,000","12,000"],["2","11,000","13,000"],["3","12,000","14,000"]], str(cyc))
        check("adımlar Türkçe alfabe sırası", [r[0] for r in rows] == ["Bekleme", "Hazırlık", "Paketleme", "Seyrek kontrol", "Tarama", "Toplama"], str([r[0] for r in rows]))
        await page.screenshot(path=f"{OUT}/3-analiz.png", full_page=True)

        # 3 video bağla
        await page.click("#tab-1")
        async with page.expect_file_chooser() as fc:
            await page.click("#relink")
        await (await fc.value).set_files(f"{ROOT}/demo/sentetik-depo-etudu.mp4")
        t = await dialog_text(); check("yeniden bağlama onayı beklenen ad", "Beklenen dosya: sentetik-depo-etudu.mp4" in t); await answer()
        await page.wait_for_function("document.getElementById('status').textContent.includes('Video hazır')", timeout=15000)
        check("video hazır (H.264)", True)
        await page.locator("#observations tbody tr", has_text="Tarama").first.click()
        await page.wait_for_timeout(300)
        check("satır seçimi videoyu başlangıca götürür (5 sn)", await pos() == 5000 and "00:00:05.000" in await page.inner_text("#position"), str(await pos()))
        await page.screenshot(path=f"{OUT}/2-olcum-demo.png", full_page=True)

        # 4 yeni etüt
        await page.click("#tab-0")
        await page.fill("#new-name", "Tarayıcı deneme"); await page.fill("#new-code", "WEB/01")
        async with page.expect_file_chooser() as fc:
            await page.click("#pick-new-video")
        await (await fc.value).set_files(f"{ROOT}/demo/sentetik-depo-etudu.mp4")
        await page.click("#create")
        await page.wait_for_function("document.getElementById('header').textContent.includes('WEB/01')", timeout=15000)
        await page.wait_for_function("document.getElementById('status').textContent.includes('Video hazır')", timeout=15000)
        check("yeni etüt açıldı", "WEB/01" in await page.inner_text("#header"))

        # 5 K10 ince konumlama ölçümü kesmez
        await page.evaluate("document.getElementById('video').currentTime = 2"); await page.wait_for_timeout(300)
        await page.fill("#step", "Toplama")
        await page.locator("#observations").click()  # odak tabloya
        await page.keyboard.press("Enter")
        check("Enter Başla", "Ölçüm başladı (2,000 sn)" in await status(), await status())
        for _ in range(5): await page.keyboard.press("Shift+ArrowRight")
        await page.keyboard.press("Control+ArrowRight"); await page.keyboard.press("Control+ArrowLeft")
        check("Shift/Ctrl ok: 7 sn, ölçüm sürüyor", await pos() == 7000 and await page.inner_text("#measure") == "Ölç", f"{await pos()} {await page.inner_text('#measure')}")
        await page.keyboard.press("Enter")
        check("Enter Ölç: 5,000 sn kayıt", "Toplama · 5,000 sn" in await status(), await status())
        # 6 ardışık: sonraki aralık aynı bitişten başlar; geriye ince alma uyarısı
        await page.keyboard.press("ArrowLeft")
        check("başlangıç gerisine gelince uyarı", "gerisinde" in await status(), await status())
        await page.keyboard.press("ArrowRight")
        # 7 zaman çizgisi atlaması ölçümü keser
        await page.evaluate("t=document.getElementById('timeline'); t.value='20000'; t.dispatchEvent(new Event('input'))")
        await page.wait_for_timeout(200)
        check("A02 zaman çizgisi atlaması ölçümü keser", "Konum değişti" in await status() and await page.inner_text("#measure") == "Başla")
        check("A02 geçersiz aralık kaydedilmedi", await page.locator("#observations tbody tr").count() == 1)
        await page.wait_for_timeout(600)
        check("A02 kayıt yazımı bittikten sonra da ölçüm kapalı kalır", await page.inner_text("#measure") == "Başla")
        # hızlı ardışık: Enter + hemen atlama (yazma sürerken)
        await page.evaluate("document.getElementById('video').currentTime = 25"); await page.wait_for_timeout(200)
        await page.fill("#step", "Hızlı"); await page.locator("#observations").click(position={"x": 5, "y": 5})
        await page.keyboard.press("Enter"); await page.keyboard.press("Shift+ArrowRight"); await page.keyboard.press("Enter")
        await page.evaluate("t=document.getElementById('timeline'); t.value='30000'; t.dispatchEvent(new Event('input'))")
        await page.wait_for_timeout(600)
        check("yazma sürerken atlama ölçümü yeniden başlatmaz", await page.inner_text("#measure") == "Başla" and await page.locator("#observations tbody tr", has_text="Hızlı").count() == 1)
        await page.locator("#observations tbody tr", has_text="Hızlı").click(); await page.click("#delete-row"); await dialog_text(); await answer()
        # 8 A01 oynatma + duraklatma süresi eklenmez
        await page.evaluate("document.getElementById('video').currentTime = 10"); await page.wait_for_timeout(300)
        await page.fill("#step", "Paketleme")
        await page.click("#measure")              # Başla (odak düğmede)
        await page.keyboard.press(" ")            # Boşluk: oynat (düğmeye tıklamamalı)
        await page.wait_for_timeout(1500)
        await page.keyboard.press(" ")            # duraklat
        check("Boşluk düğmeyi tetiklemedi", await page.inner_text("#measure") == "Ölç" and await page.locator("#observations tbody tr").count() == 1)
        p1 = await pos(); await page.wait_for_timeout(1200); p2 = await pos()
        check("A01 duraklatma süresi eklenmez", p1 == p2 and 1200 <= p1 - 10000 <= 1900, f"{p1-10000} ms")
        await page.locator("#observations").click(position={"x": 5, "y": 5})
        # tablo başlığına tıklama satır seçmez; odak tabloda
        await page.keyboard.press("Enter")
        rows = await page.eval_on_selector_all("#observations tbody tr", "rs => rs.map(r => [...r.cells].map(c => c.textContent))")
        paket = [r for r in rows if r[3] == "Paketleme"]
        check("A01 oynatıcı konumuna göre süre", len(paket) == 1 and abs(int(paket[0][1]) - int(paket[0][0]) - (p1 - 10000)) == 0, str(paket))
        # 9 Enter metin kutusunda ölçüm yapmaz
        await page.click("#step"); await page.keyboard.press("Enter")
        check("Enter metin kutusunda çalışmaz", await page.locator("#observations tbody tr").count() == 2)
        await page.keyboard.press("Tab")
        # 10 Esc
        await page.locator("#observations").click(position={"x": 5, "y": 5})
        await page.keyboard.press("Escape")
        check("Esc Durdur", await page.inner_text("#measure") == "Başla" and "durduruldu" in await status())
        # 11 yavaş oynatma
        await page.select_option("#speed", "0.5")
        await page.evaluate("document.getElementById('video').currentTime = 20"); await page.wait_for_timeout(300)
        await page.click("#play"); await page.wait_for_timeout(2000); await page.click("#pause")
        adv = await pos() - 20000
        check("0,5× hız", 700 <= adv <= 1300, f"{adv} ms / 2 sn")
        await page.select_option("#speed", "1")
        # 12 aralık başlangıcı / ekle (çakışan)
        await page.evaluate("document.getElementById('video').currentTime = 3"); await page.wait_for_timeout(200)
        await page.fill("#step", "Tarama"); await page.click("#mark-start")
        await page.evaluate("document.getElementById('video').currentTime = 5"); await page.wait_for_timeout(200)
        await page.click("#add-marked"); await page.wait_for_timeout(200)
        check("bağımsız aralık 2,000 sn", "Tarama · 2,000 sn" in await status(), await status())
        # 13 düzenleme, toplu sınıf, silme
        await page.locator("#observations tbody tr", has_text="Tarama").click()
        await page.select_option("#classification", "SVA"); await page.check("#excluded"); await page.click("#edit-row"); await page.wait_for_timeout(200)
        tr = await page.locator("#observations tbody tr", has_text="Tarama").inner_text()
        check("satır güncelleme (sınıf + kapsam dışı)", "SVA" in tr and "Evet" in tr, tr)
        await page.locator("#observations tbody tr", has_text="Toplama").click()
        await page.select_option("#classification", "VA"); await page.click("#bulk-class"); await page.wait_for_timeout(200)
        check("toplu sınıf", "VA" in await page.locator("#observations tbody tr", has_text="Toplama").inner_text())
        await page.screenshot(path=f"{OUT}/2-olcum.png", full_page=True)
        await page.locator("#observations tbody tr", has_text="Tarama").click(); await page.click("#delete-row")
        await dialog_text(); await answer()
        check("satır silme, diğerleri aynı", await page.locator("#observations tbody tr").count() == 2)
        # 14 analiz ayarları: geçersiz tolerans reddi, virgüllü değer
        await page.click("#tab-2")
        await page.fill("#allowance", "100"); await page.click("#apply-settings")
        t = await dialog_text(); check("A07 tolerans %100 reddi", "tolerans" in t); await answer()
        await page.fill("#allowance", "12,5"); await page.fill("#pace", "105"); await page.click("#apply-settings")
        await page.wait_for_timeout(200)
        r = {x[0]: x for x in await page.eval_on_selector_all("#results tbody tr", "rs => rs.map(r => [...r.cells].map(c => c.textContent))")}
        check("A07 virgüllü tolerans: 5×1,05/0,875 = 6,000", r["Toplama"][13] == "6,000", r["Toplama"][13])
        await page.click("#t-table"); t = await page.inner_text("#dialog-pre"); check("t tablosu", "12,7062" in t); await answer()
        # 15 Excel
        async with page.expect_download() as dl:
            await page.click("#excel")
        d = await dl.value; await d.save_as(f"{OUT}/web.xlsx"); check("Excel indirme adı", d.suggested_filename == "WEB_01_analiz.xlsx", d.suggested_filename)
        # 16 kalıcılık: sayfa yenileme
        await page.reload(); await page.wait_for_selector("#studies tbody tr")
        names = await page.inner_text("#studies tbody")
        check("yenilemeden sonra etütler duruyor", "WEB/01" in names and "DEMO-01" in names)
        await page.locator("#studies tbody tr", has_text="WEB/01").dblclick()
        await page.wait_for_selector("#panel-1:not([hidden])")
        await page.wait_for_timeout(800)
        check("yenileme sonrası ayarlar ve ölçümler", await page.locator("#observations tbody tr").count() == 2 and "Video hazır" in await status(), await status())
        await page.click("#tab-2")
        check("ayarlar geri geldi", await page.input_value("#allowance") == "12,5" and await page.input_value("#pace") == "105")
        # 17 dışa aktar
        await page.click("#tab-0"); await page.locator("#studies tbody tr", has_text="WEB/01").click()
        async with page.expect_download() as dl:
            await page.click("#export")
        d = await dl.value; await d.save_as(f"{OUT}/web.vzetud")
        j = json.load(open(f"{OUT}/web.vzetud", encoding="utf-8"))
        check("paket adı ve içerik", d.suggested_filename == "WEB_01.vzetud" and j["VideoName"] == "sentetik-depo-etudu.mp4" and j["Settings"]["Allowance"] == 12.5)
        # 18 aynı paketi içe al → ayrı kayıt
        async with page.expect_file_chooser() as fc:
            await page.click("#import")
        await (await fc.value).set_files(f"{OUT}/web.vzetud"); await dialog_text(); await answer()
        await page.click("#tab-0")
        check("A05 aynı paket ikinci kayıt", await page.locator("#studies tbody tr", has_text="WEB/01").count() == 2)
        # 18b A04: demo dışı video adı → video bağlı değil, beklenen ad gösterilir, analiz açılır
        j2 = json.load(open(f"{ROOT}/demo/sentetik-depo-etudu.vzetud", encoding="utf-8")); j2["VideoName"] = "gercek-hat-videosu.mp4"; j2["Code"] = "A04"
        json.dump(j2, open(f"{OUT}/a04.vzetud", "w", encoding="utf-8"), ensure_ascii=False)
        async with page.expect_file_chooser() as fc:
            await page.click("#import")
        await (await fc.value).set_files(f"{OUT}/a04.vzetud"); await dialog_text(); await answer()
        await page.wait_for_timeout(500)
        check("A04 video yokken beklenen dosya adı", "Beklenen dosya: gercek-hat-videosu.mp4" in await status(), await status())
        await page.click("#tab-2")
        check("A04 video yokken analiz açılır", "6,902" in await page.inner_text("#results"))
        await page.click("#tab-0"); await page.locator("#studies tbody tr", has_text="A04").click(); await page.click("#delete"); await dialog_text(); await answer()
        # 19 bozuk paket
        open(f"{OUT}/bozuk.vzetud", "w").write('{"Kind":"video-zaman-etudu","Version":1,"Name":"x"')
        async with page.expect_file_chooser() as fc:
            await page.click("#import")
        await (await fc.value).set_files(f"{OUT}/bozuk.vzetud"); t = await dialog_text()
        check("bozuk paket reddi, kayıt yok", "bozuk" in t); await answer()
        check("kayıt sayısı değişmedi", await page.locator("#studies tbody tr").count() == 3)
        # 20 açık etüdü sil
        await page.locator("#studies tbody tr", has_text="DEMO-01").dblclick(); await page.click("#tab-0")
        await page.locator("#studies tbody tr", has_text="DEMO-01").click(); await page.click("#delete")
        t = await dialog_text(); check("silme onayı", "geri alınamaz" in t); await answer()
        check("açık etüt silindi, sekmeler pasif", await page.is_disabled("#tab-1") and "Çalışmalar" in await page.inner_text("#header") and await page.locator("#studies tbody tr", has_text="DEMO-01").count() == 0)
        await page.screenshot(path=f"{OUT}/1-calismalar.png", full_page=True)
        # 21 bozuk yerel kayıt
        await page.evaluate("""new Promise(r => { const o = indexedDB.open('VideoZamanEtudu'); o.onsuccess = () => { const tx = o.result.transaction('studies','readwrite'); tx.objectStore('studies').put({Id:'bozuk', Name:5}); tx.oncomplete = r; }; })""")
        await page.reload(); t = await dialog_text()
        check("bozuk yerel kayıt atlanır ve uyarılır", "1 etüt kaydı okunamadığı" in t); await answer()
        check("diğer kayıtlar listede", await page.locator("#studies tbody tr").count() == 2)
        # 22 dar ekran
        await page.set_viewport_size({"width": 390, "height": 844})
        await page.screenshot(path=f"{OUT}/mobil.png", full_page=False)
        check("konsol hatası yok", not errors, "; ".join(errors)[:300])
        await browser.close()

asyncio.run(main())
fails = [r for r in results if not r[0]]
print(f"\nUÇTAN UCA: {len(results)-len(fails)}/{len(results)} geçti")
sys.exit(1 if fails else 0)
