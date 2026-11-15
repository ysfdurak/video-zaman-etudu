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
        await page.goto(URL + ("&" if "?" in URL else "?") + "lang=tr")
        await page.wait_for_selector("#studies tbody tr")
        check("açılış: boş liste mesajı", "Henüz etüt yok" in await page.inner_text("#studies tbody"))
        check("seçim yokken Aç/Dışa aktar/Sil/Karşılaştır kapalı", all([await page.is_disabled(f"#{i}") for i in ["open", "export", "delete", "compare"]]))
        status = lambda: page.inner_text("#status")
        now = lambda: page.inner_text("#now-step")
        rows = lambda: page.locator("#observations tbody tr:not(.empty)").count()
        async def goto(ms):
            await page.evaluate(f"(()=>{{const t=document.getElementById('timeline'); t.value={ms}; t.dispatchEvent(new Event('input'))}})()")
            await page.wait_for_timeout(200)
        async def close_dialog():
            if await page.locator("#dialog[open]").count():
                text = await page.inner_text("#dialog-text"); await page.click("#dialog-yes"); await page.wait_for_timeout(200); return text
            return ""

        # 1 Yeni etüt: adım listesi ve künye ile
        await page.fill("#new-name", "Toplama hattı"); await page.fill("#new-code", "TP-01")
        await page.click("#new-more summary"); await page.fill("#new-steps", "Hazırlık\nToplama\nPaketleme")
        await page.fill("#new-info-Operator", "A. Kaya"); await page.fill("#new-info-Station", "Hat 2")
        async with page.expect_file_chooser() as fc: await page.click("#pick-new-video")
        await (await fc.value).set_files(os.path.join(ROOT, "demo", "sentetik-depo-etudu.mp4"))
        await page.click("#create"); await page.wait_for_function("document.getElementById('status').textContent.includes('Video hazır')")
        check("adım araçları yalnız adım seçiliyken", await page.is_visible("#step-tools"))
        check("yeni etüt adım listesiyle açılır", await page.locator("#steps .step-chip").count() == 3 and await now() == "Hazırlık · çevrim 1")
        await page.select_option("#step-class", "SVA"); await page.wait_for_timeout(150)
        await page.locator("#steps .step-chip").nth(1).click(); await page.select_option("#step-class", "VA"); await page.wait_for_timeout(150)
        await page.locator("video").click(); await page.keyboard.press("1")
        check("1–9 tuşu adım seçer", await now() == "Hazırlık · çevrim 1")

        await page.locator("#steps .step-chip").nth(1).click(); await page.fill("#step-note", "ürün kutuya bırakılınca"); await page.press("#step-note", "Tab"); await page.wait_for_timeout(150)
        check("adım açıklaması Sıradaki kayıt kutusunda", "Bitiş anı: ürün kutuya bırakılınca" in await page.inner_text("#now-note"))
        await page.locator("#steps .step-chip").nth(0).click()
        # 2 Zaman çizgisinde gezinmek ölçümü kesmez
        await goto(0); await page.click("#measure"); await goto(1200)
        check("süren ölçümün süresi görünür", "Ölçülüyor: 1,200 sn" in await page.inner_text("#now-live"), await page.inner_text("#now-live"))
        await goto(2000); await page.click("#measure")
        check("çizgide gezinip Ölç kaydeder", await rows() == 1 and "Hazırlık · 2,000 sn" in await status(), await status())
        check("otomatik sıradaki adım", await now() == "Toplama · çevrim 1")
        await goto(7000); await page.click("#measure"); await goto(10000); await page.click("#measure")
        check("son adımdan sonra çevrim artar", await now() == "Hazırlık · çevrim 2")
        check("sınıf adımdan gelir", "SVA" in await page.locator("#observations tbody tr").nth(0).inner_text() and "VA" in await page.locator("#observations tbody tr").nth(1).inner_text())

        # 3 Araya giren bekleme: kapsam dışı, sırayı ilerletmez, önceki adıma döner
        await page.fill("#new-step", "Bekleme"); await page.press("#new-step", "Enter"); await page.wait_for_timeout(150)
        check("Enter yeni adımı ekler ve seçer", await now() == "Bekleme · çevrim 2")
        await page.check("#excluded"); await goto(12000); await page.click("#measure")
        check("kapsam dışı tek kayıtlık", not await page.is_checked("#excluded") and "kapsam dışı" in await status())
        check("kapsam dışı kayıttan sonra önceki adıma döner", await now() == "Hazırlık · çevrim 2", await now())

        # 4 Başlangıcın gerisinde Ölç: kayıt yok, uyarı var, ölçüm sürer
        before = await rows()
        await goto(11000); await page.click("#measure"); await page.wait_for_timeout(200)
        text = await close_dialog()
        check("geride Ölç uyarır, kayıt eklemez", "gerisinde" in text and await rows() == before and await page.inner_text("#measure") == "Ölç")

        # 5 Geri al (Ctrl+Z): kayıt silinir, adım ve çevrim geri gelir, ölçüm başlangıcı geri alınır
        await goto(14000); await page.click("#measure")
        await page.locator("video").click(); await page.keyboard.press("Control+z"); await page.wait_for_timeout(200)
        check("Ctrl+Z son kaydı geri alır", await rows() == before and await now() == "Hazırlık · çevrim 2" and "Geri alındı" in await status())
        await page.click("#measure"); await page.wait_for_timeout(150)
        check("geri alınan kayıt yeniden ölçülebilir", await rows() == before + 1, await status())

        # 6 Oynatarak Enter ile ölçüm
        await page.locator("video").click(); await page.keyboard.press("Space"); await page.wait_for_timeout(1500)
        await page.keyboard.press("Enter"); await page.wait_for_timeout(1500); await page.keyboard.press("Space"); await page.keyboard.press("Escape")
        check("oynatırken Enter kaydeder, Esc durdurur", await rows() == before + 2 and await page.inner_text("#measure") == "Başla")
        check("zaman çizgisinde ölçülen aralıklar", await page.locator("#segments .seg:not(.live)").count() == await rows())

        # 7 Seçili kayıt düzenleme
        await page.locator("#observations tbody tr").nth(2).click(); await page.wait_for_timeout(150)
        check("satır seçimi düzenleyiciyi açar", await page.is_visible("#row-editor") and await page.input_value("#row-step") == "Paketleme")
        await page.fill("#row-note", "raf boştu"); await page.click("#edit-row"); await page.wait_for_timeout(150)
        check("kayıt notu tabloda", "raf boştu" in await page.locator("#observations tbody tr").nth(2).inner_text())
        await page.select_option("#row-class", "NVA"); await page.click("#bulk-class"); await page.wait_for_timeout(150)
        check("toplu sınıf adım tanımını da günceller", "NVA" in await page.inner_text("#steps"))
        await page.click("#row-close")

        # 8 Analiz: süreç sırası, kendiliğinden uygulanan ayarlar, grafikler
        await page.click("#tab-2"); await page.wait_for_timeout(400)
        order = [await page.locator("#results tbody tr").nth(i).locator("td").first.inner_text() for i in range(await page.locator("#results tbody tr").count())]
        check("analiz süreç sırasında", order[:3] == ["Hazırlık", "Toplama", "Paketleme"], str(order))
        check("tolerans %0 uyarısı", "Tolerans %0" in await page.inner_text("#analysis-status"))
        await page.fill("#allowance", "12"); await page.press("#allowance", "Tab"); await page.wait_for_timeout(300)
        check("ayar değişince uygulanır", "Tolerans %0" not in await page.inner_text("#analysis-status"))
        await page.fill("#pace", "x"); await page.press("#pace", "Tab"); await page.wait_for_timeout(200)
        check("geçersiz ayar satır içinde gösterilir", await page.is_visible("#settings-error") and await page.locator("#dialog[open]").count() == 0)
        await page.fill("#pace", "100"); await page.press("#pace", "Tab"); await page.wait_for_timeout(200)
        check("iki grafik", await page.locator("#charts figure").count() == 2)
        await page.fill("#takt", "20"); await page.press("#takt", "Tab"); await page.wait_for_timeout(300)
        check("takt kartı", "Takt" in await page.inner_text("#summary") and "Gereken operatör" in await page.inner_text("#summary"))
        await page.locator("#results tbody tr", has_text="Toplama").click(); await page.fill("#step-pace", "90"); await page.click("#apply-step"); await page.wait_for_timeout(200)
        check("adım temposu tabloda işaretli", "90 *" in await page.locator("#results tbody tr", has_text="Toplama").inner_text())
        check("Yazdır düğmesi", await page.is_visible("#print"))
        await page.screenshot(path=os.path.join(OUT, "analiz.png"), full_page=True)
        async with page.expect_download() as d: await page.click("#excel")
        dl = await d.value; path = os.path.join(OUT, dl.suggested_filename); await dl.save_as(path)
        check("Excel indirildi", dl.suggested_filename == "TP-01_analiz.xlsx")

        # 9 Liste ve önce / sonra
        await page.click("#tab-0"); await page.click("#demo"); await page.wait_for_timeout(1500); await page.click("#tab-0")
        listing = await page.inner_text("#studies tbody")
        check("listede istasyon, ölçüm ve durum", "Hat 2 · A. Kaya" in listing and "adım yetersiz" in listing)
        await page.click("#compare"); await page.wait_for_timeout(300)
        check("karşılaştırma penceresi", "→" in await page.inner_text("#compare-summary") and await page.locator("#compare-table tbody tr").count() >= 4)
        await page.click("#compare-dialog button[value=close]")

        # 10 Paket: adım listesi ve künye dışa aktarılır, geri gelir
        await page.locator("#studies tbody tr", has_text="TP-01").click()
        async with page.expect_download() as d: await page.click("#export")
        pkg = await d.value; ppath = os.path.join(OUT, pkg.suggested_filename); await pkg.save_as(ppath)
        data = json.load(open(ppath, encoding="utf-8"))
        check("pakette takt, adım temposu ve not", data.get("Takt") == 20 and data.get("StepPace") == {"Toplama": 90} and any(o.get("Note") == "raf boştu" for o in data["Observations"]))
        check("pakette Steps ve Info", [s["Name"] for s in data["Steps"]][:3] == ["Hazırlık", "Toplama", "Paketleme"] and data["Info"]["Operator"] == "A. Kaya" and data["Version"] == 1)

        # 11 Boş etüt: analizde boş durum
        await page.fill("#new-name", "Boş"); await page.fill("#new-code", "B-1")
        async with page.expect_file_chooser() as fc: await page.click("#pick-new-video")
        await (await fc.value).set_files(os.path.join(ROOT, "demo", "sentetik-depo-etudu.mp4"))
        await page.click("#create"); await page.wait_for_timeout(1000)
        check("adımsız etütte adım araçları gizli", not await page.is_visible("#step-tools"))
        await page.click("#tab-2"); await page.wait_for_timeout(300)
        check("ölçümsüz analizde boş durum", await page.is_visible("#analysis-empty") and not await page.is_visible("#analysis-body"))
        await page.click("#go-measure"); check("boş durumdan ölçüme geçiş", await page.get_attribute("#tab-1", "aria-selected") == "true")
        check("konsol hatası yok", not errors, "; ".join(errors[:3]))
        await browser.close()
    failed = [r for r in results if not r[0]]
    print(f"\n{len(results) - len(failed)}/{len(results)} geçti")
    sys.exit(1 if failed else 0)

asyncio.run(main())
