"""Sentetik depo demo videosunu (768×432, 24 fps, 35 sn, H.264) Türkçe veya İngilizce üretir.

Kullanım (depo kökünde; Pillow ve ffmpeg gerekir):
    python Tools/DemoVideo/demo_video.py en demo/synthetic-warehouse-study.mp4
    python Tools/DemoVideo/demo_video.py tr cikti.mp4

Bölümler demo etüt paketindeki ölçümlerle birebir aynı zamanlardadır; böylece paketteki
zaman damgaları videodaki adımlarla örtüşür. Tarama/Scanning paralel işi açıklama satırında belirtilir.
"""
import subprocess, sys
from PIL import Image, ImageDraw, ImageFont

W, H, FPS, DURATION = 768, 432, 24, 35
BG, TRACK, TICK = (15, 33, 57), (25, 52, 80), (39, 66, 94)
TEAL, ORANGE, WHITE = (94, 214, 188), (232, 170, 94), (255, 255, 255)
MUTED, SUB, BAR_BG = (180, 198, 222), (201, 215, 236), (38, 68, 90)
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

TEXT = {
    "tr": {"header": "VİDEO İŞ ETÜDÜ / SENTETİK DEPO DEMOSU", "cycle": "Çevrim", "unit": "sn"},
    "en": {"header": "VIDEO WORK STUDY / SYNTHETIC WAREHOUSE DEMO", "cycle": "Cycle", "unit": "s"},
}
# (başlangıç, bitiş, çevrim, kapsam dışı, {dil: (adım, açıklama)})
SEGMENTS = [
    (0, 2, 1, False, {"tr": ("Hazırlık", "VA dışı zorunlu iş"), "en": ("Preparation", "Required non-value-added work")}),
    (2, 7, 1, False, {"tr": ("Toplama", "5–7 sn: tarama paralel ilerler"), "en": ("Picking", "5–7 s: scanning runs in parallel")}),
    (7, 10, 1, False, {"tr": ("Paketleme", "Çevrim 1 tamamlanıyor"), "en": ("Packing", "Cycle 1 completes")}),
    (10, 12, 1, True, {"tr": ("Bekleme", "Kapsam dışı aralık"), "en": ("Waiting", "Out-of-scope interval")}),
    (12, 14, 2, False, {"tr": ("Hazırlık", "İkinci çevrim"), "en": ("Preparation", "Second cycle")}),
    (14, 20, 2, False, {"tr": ("Toplama", "18–20 sn: tarama paralel ilerler"), "en": ("Picking", "18–20 s: scanning runs in parallel")}),
    (20, 23, 2, False, {"tr": ("Paketleme", "Çevrim 2 tamamlanıyor"), "en": ("Packing", "Cycle 2 completes")}),
    (23, 25, 3, False, {"tr": ("Hazırlık", "Üçüncü çevrim"), "en": ("Preparation", "Third cycle")}),
    (25, 30, 3, False, {"tr": ("Toplama", "28–30 sn: tarama paralel ilerler"), "en": ("Picking", "28–30 s: scanning runs in parallel")}),
    (30, 33, 3, False, {"tr": ("Paketleme", "Çevrim 3 tamamlanıyor"), "en": ("Packing", "Cycle 3 completes")}),
    (33, 35, 3, False, {"tr": ("Seyrek kontrol", "Her üç çevrimde bir gerçekleşir"), "en": ("Periodic check", "Happens once every three cycles")}),
]

def frame(lang, t, fonts):
    seg = next(s for s in SEGMENTS if s[0] <= t < s[1]) if t < DURATION else SEGMENTS[-1]
    start, end, cycle, excluded, texts = seg
    step, note = texts[lang]
    accent = ORANGE if excluded else TEAL
    tx = TEXT[lang]
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    d.text((36, 28), tx["header"], font=fonts["header"], fill=MUTED)
    d.text((36, 72), f"{tx['cycle']} {cycle:02d} / 03", font=fonts["cycle"], fill=accent)
    d.text((36, 111), step, font=fonts["step"], fill=WHITE)
    d.text((36, 172), note, font=fonts["note"], fill=SUB)
    d.rounded_rectangle((36, 222, 732, 309), radius=12, fill=TRACK)
    for x in range(82, 700, 100):
        d.line((x, 234, x, 297), fill=TICK, width=2)
    frac = (t - start) / (end - start)
    cx = 88 + frac * 593
    d.rounded_rectangle((cx - 24, 244, cx + 24, 288), radius=6, fill=accent)
    d.text((36, 333), f"{t:05.2f} {tx['unit']}", font=fonts["time"], fill=WHITE)
    rng = f"{start:02d}–{end:02d} {tx['unit']}"
    d.text((677 - d.textlength(rng, font=fonts["note"]), 336), rng, font=fonts["note"], fill=MUTED)
    d.rectangle((36, 381, 732, 385), fill=BAR_BG)
    d.rectangle((36, 381, 36 + (732 - 36) * t / DURATION, 385), fill=accent)
    return im

def main(lang, out):
    fonts = {"header": ImageFont.truetype(FONT, 14), "cycle": ImageFont.truetype(BOLD, 19), "step": ImageFont.truetype(BOLD, 42),
             "note": ImageFont.truetype(FONT, 14), "time": ImageFont.truetype(BOLD, 20)}
    ff = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                           "-c:v", "libx264", "-preset", "veryfast", "-crf", "26", "-profile:v", "high", "-pix_fmt", "yuv420p",
                           "-movflags", "+faststart", "-map_metadata", "-1", out], stdin=subprocess.PIPE)
    for i in range(FPS * DURATION):
        ff.stdin.write(frame(lang, i / FPS, fonts).tobytes())
    ff.stdin.close()
    if ff.wait() != 0:
        raise SystemExit("ffmpeg başarısız")

if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in TEXT:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
