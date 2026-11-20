# MCP Seviye 1 planı — Video İş Etüdü

**Amaç:** Claude (Claude Desktop ya da MCP destekleyen başka bir istemci) etütleri okuyup soru yanıtlayabilsin. Örnek sorular: "PKT-07'de hangi adım yetersiz, kaç gözlem daha lazım?", "Önce/sonra farkı ne?", "En çok israf nerede?"

**Seviye 1'in sınırı:** Yalnız okur, hiçbir şeyi değiştirmez. Bilgisayarda çalışır, sunucu (VPS) gerekmez. Video hiç okunmaz.

## 1. Mimari

```
Core/ (hesap çekirdeği, bugünkü)  ──┬── Desktop/  (WPF arayüz, bugünkü)
                                    ├── Checks/   (kontroller, bugünkü)
                                    └── Mcp/      (YENİ: MCP sunucusu, stdio)
```

- **Dil ve çekirdek:** C# / .NET 8. Hesaplar `Core/`'dan gelir, yani Windows arayüzü ve tarayıcıyla aynı sonuçları verir (eşdeğerlik `Shared/` dosyalarıyla zaten denetleniyor). Bu, "aynı çekirdek, iki arayüz" yapısıdır: insan için WPF/web, yapay zekâ için MCP.
- **Kütüphane:** Resmî C# MCP SDK'sı (`ModelContextProtocol` NuGet paketi). Sürümü sabitlenir; SDK henüz önizleme olduğu için güncellemeler kontrollü yapılır.
- **Taşıma:** stdio. Claude Desktop `VideoZamanEtudu.Mcp.exe`'yi kendisi başlatır; ağ portu açılmaz.
- **Veri kaynakları (yalnız okuma):**
  1. **Windows kaydı:** `%LOCALAPPDATA%\VideoZamanEtudu\studies.sqlite`. Salt okunur bağlantıyla (`Mode=ReadOnly`) açılır; program açıkken de okunabilir.
  2. **Paket klasörü:** `.vzetud` paketleri ve `Tümünü yedekle` .zip'leri. Tarayıcı kullanıcıları için tek yol budur, çünkü tarayıcının deposuna (IndexedDB) dışarıdan erişilemez.
  - İkisi de ayarla verilir: `VZE_STORE` ve `VZE_PAKETLER` ortam değişkenleri. Verilmezse varsayılan Windows kaydı kullanılır.
- **Paylaşılan okuma kodu:** `StudyStore` bugün `Desktop/` içinde. Salt okunur bir `StudyReader` olarak `Core/`'a (ya da küçük bir `Store/` projesine) taşınır, böylece Desktop ve Mcp aynı kodu kullanır.

## 2. Araçlar (tools)

Hepsi salt okunur. Etüt **kod** ile bulunur (ör. `PKT-07`). Aynı kodla birden çok etüt varsa araç listeyi döndürür ve hangisi olduğunu sorar. Her araç `dil` (`tr` / `en`) alır; metinler `Metinler.json`'dan, yani ekrandaki ifadelerle aynıdır.

| Araç | Girdi | Döner |
|---|---|---|
| `etutleri_listele` | `ara?` (kod/ad parçası), `durum?` | Kod, ad, tür, durum (taslak/tamamlandı/onaylı), ölçüm ve çevrim sayısı, yeterlilik özeti, son düzenleme |
| `etut_ozeti` | `kod` | Çevrim başına standart süre, saatlik kapasite, takt doluluğu ve gereken operatör, çevrim değişkenliği, sınıf payları, ayarlar (tempo, tolerans, güven), etüt bilgileri |
| `adim_analizi` | `kod` | Süreç sırasında her adım: n, ortalama, std sapma, hata %, yeterlilik, gereken gözlem, normal ve standart süre, katkı, sınıf, tür, israf, fikir |
| `eksik_gozlemler` | `kod` | Yetersiz ve hesaplanamayan adımlar, her biri için "kaç gözlem daha" (öncelik sırasıyla); "bu etüt için yaklaşık N çevrim daha" |
| `karsilastir` | `once`, `sonra` | Adım adım fark (sn ve %), toplam fark; ayarlar farklıysa uyarı |
| `iyilestirme_firsatlari` | `kod` | NVA adımlar ve israflar (katkı ve payı ile), aykırı kayıtlar, açıksa hat dengeleme özeti (darboğaz, denge verimi, en az istasyon) |
| `ornekleme_sonucu` | `kod` | İş örneklemesi: kategori payları, güven aralıkları, gereken gözlem |

**Bilerek dışarıda bırakılanlar:**
- Ham kayıt listesinin tamamı. Büyük etütte binlerce satır olur ve sohbeti doldurur. Gerekirse Seviye 2'de sayfalı bir araç eklenir.
- Video yolu ve klasör adları. Yalnız video dosya adı döner; bu, Excel ve paketteki kuralla aynı.

**İsteğe bağlı istem (prompt):** `etut_raporu(kod)` — "Bu etüdün yönetim özeti: standart süre, yeterlilik, en büyük üç israf, önerilen sonraki adım". Tek tıkla aynı formatta rapor ister.

## 3. Güvenlik ve gizlilik

- **Salt okunur:** Veritabanı salt okunur açılır, paketler yalnız okunur, yazma aracı yoktur.
- **Ağ yok:** stdio taşıması kullanılır, dışarıya bağlantı açılmaz. Veri yalnız kullanıcının bağladığı Claude istemcisine gider.
- **Girdi güvenliği:** Paketler bugünkü katı doğrulamadan (`StudyTransfer.Import`) geçer. Bozuk dosya atlanır ve uyarılır.
- **Sınır:** Bir yanıtta en fazla 200 adım ya da 200 etüt döner; fazlası "daraltın" uyarısıyla kesilir.

## 4. Test

1. **Araç işlevleri saf fonksiyon:** `Mcp/Tools.cs` çekirdek sonuçlarını biçimler. `Checks/McpChecks.cs` demo paketle her aracı çağırır ve sonuçları bugünkü `Analysis`, `StudyRules` ve `Advanced` çıktılarıyla karşılaştırır. Örnek: `eksik_gozlemler("DEMO-01")` Toplama için 18 (21 − 3) dönmeli.
2. **Protokol denetimi:** `VideoZamanEtudu.Mcp.exe --self-test` sunucuyu kendi içinde başlatır, `tools/list` ve her `tools/call`'ı gerçek MCP iletileriyle dener. GitHub Actions'ta Windows işine eklenir.
3. **Elle deneme:** MCP Inspector (`npx @modelcontextprotocol/inspector`) ile araçlar tek tek çağrılır. Sonra Claude Desktop'ta 5 örnek soru sorulur ve yanıtlar Analiz ekranıyla karşılaştırılır.
4. **İki dil:** Her araç `tr` ve `en` ile çalıştırılır. Metin anahtarlarının eksiksiz olduğunu bugünkü `metinler.test.mjs` denetler.

## 5. Dağıtım

- Windows ZIP'ine `VideoZamanEtudu.Mcp.exe` eklenir (aynı sürüm numarası).
- README'ye Claude Desktop ayarı eklenir (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "zaman-etudu": {
      "command": "C:\\Programlar\\VideoZamanEtudu\\VideoZamanEtudu.Mcp.exe",
      "env": { "VZE_PAKETLER": "C:\\Users\\<kullanıcı>\\Documents\\Etutler" }
    }
  }
}
```

- Tarayıcı kullanıcısı için akış: `Tümünü yedekle (.zip)` ile aldığı yedeği `VZE_PAKETLER` klasörüne koyar; MCP onu okur.

## 6. İş sırası ve süre

| # | İş | Tahmini süre |
|---|---|---|
| 1 | `StudyReader`'ı Desktop'tan ortak yere taşı (salt okunur), Desktop'u ona bağla | 2 saat |
| 2 | `Mcp/` projesi, SDK, stdio, `tools/list` | 2 saat |
| 3 | 7 araç ve biçimlendirme (TR/EN) | 4–5 saat |
| 4 | `McpChecks` ve `--self-test`, CI | 3 saat |
| 5 | Paketleme, README, `PROJE_REHBERI` bölümü | 1–2 saat |
| 6 | Claude Desktop'ta 5 örnek soruyla kabul denemesi | 1 saat |

Toplam yaklaşık 1,5–2 iş günü. Her adım ayrı commit ve tek PR olarak yapılır.

## 7. Kabul ölçütleri

- Claude Desktop'ta "etütlerimi listele" bütün etütleri doğru durumla gösterir.
- `eksik_gozlemler` ve `etut_ozeti`, Analiz ekranındaki sayılarla birebir aynıdır (öz-denemede denetlenir).
- Program açıkken MCP okur, programın kaydını bozmaz ya da kilitlemez.
- Hiçbir araç dosya değiştirmez. Video ya da klasör yolu yanıtta görünmez.

## 8. Sonraki seviyeler (şimdi yapılmaz)

- **Seviye 1,5 (MCP App):** `etut_ozeti` yanıtında özet kartları ve katkı grafiği sohbetin içinde görsel olarak çizilir. Aynı çekirdek, üçüncü arayüz.
- **Seviye 2 (sınırlı yazma):** Durum ya da onay değiştirme, not ekleme, Excel raporu üretme. Her yazma işi istemcide onay ister ve değişiklik geri alınabilir tutulur.
- **Seviye 3 (VPS ve ekip):** Etütler ortak bir sunucuda, kullanıcı girişi ve kişiler arası onay akışı. Yalnız ekipte gerçek ortak kullanım ihtiyacı doğarsa.
