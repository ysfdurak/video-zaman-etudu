using System.Text.Json;
using System.Text.RegularExpressions;
using MathNet.Numerics.Distributions;

namespace VideoZamanEtudu;

/// <summary>
/// Kabul kriterlerinin kenar durumları, bozuk paket senaryoları ve yerel kayıt biçimi.
/// Herhangi bir kontrol başarısız olursa sonda istisna fırlatılır; GitHub Actions adımı kırmızıya döner.
/// </summary>
internal static class ExtraChecks
{
    private static int pass, fail;

    public static void Run(string? demoPath)
    {
        Calculations();
        Transfer();
        LocalRecord();
        if (demoPath != null) Demo(demoPath);
        Console.WriteLine($"Ek kontroller: {pass} geçti, {fail} kaldı.");
        if (fail > 0) throw new Exception($"{fail} ek kontrol başarısız.");
    }

    private static void Check(string name, Func<bool> test)
    {
        bool ok;
        string detail = "";
        try { ok = test(); }
        catch (Exception error) { ok = false; detail = $" ({error.GetType().Name}: {error.Message})"; }
        if (ok) pass++;
        else { fail++; Console.WriteLine("KALDI  " + name + detail); }
    }

    private static bool Near(double? actual, double expected, double tolerance = 1e-6) =>
        actual is double value && Math.Abs(value - expected) <= tolerance;

    private static bool Rejects<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }

    private static Observation R(long start, long end, string step = "İş", int cycle = 1, bool excluded = false) =>
        new() { StartMs = start, EndMs = end, Step = step, Cycle = cycle, Excluded = excluded };

    private static Study S(params Observation[] rows) => new() { Name = "Etüt", Code = "K-1", Observations = rows.ToList() };

    private static void Calculations()
    {
        // A06 — iki baz süre yöntemi
        var a6 = S(R(0, 5000), R(0, 6000), R(0, 5000));
        Check("A06 ortalama 5,333", () => Near(Analysis.Steps(a6).Single().Base, 16d / 3));
        a6.Settings.RepeatedLow = true;
        Check("A06 tekrar yöntemi 5", () => Near(Analysis.Steps(a6).Single().Base, 5));
        var once = S(R(0, 5000), R(0, 6000), R(0, 7000));
        once.Settings.RepeatedLow = true;
        Check("A06 tekrarsız veri → boş baz ve ortalama önerisi", () =>
            Analysis.Steps(once).Single() is { Base: null, Standard: null } r && r.Method.Contains("Tekrar yok"));

        var rounding = S(R(0, 5049), R(0, 5050), R(0, 5149));
        rounding.Settings.RepeatedLow = true;
        Check("K06 en yakın gruba yuvarlama, baz = grubun ham ortalaması",
            () => Near(Analysis.Steps(rounding).Single().Base, (5.050 + 5.149) / 2));
        var tie = S(R(0, 8000), R(0, 8020), R(0, 6000), R(0, 6040));
        tie.Settings.RepeatedLow = true;
        Check("K06 eşitlikte düşük ortalamalı grup", () => Near(Analysis.Steps(tie).Single().Base, 6.02));

        // A07 — tempo ve tolerans
        var a7 = S(R(0, 100000));
        a7.Settings.Pace = 110;
        a7.Settings.Allowance = 15;
        Check("A07 normal 110, standart 129,4118", () =>
            Analysis.Steps(a7).Single() is var r && Near(r.Normal, 110) && Near(r.Standard, 110 / .85));
        foreach (var (name, settings) in new (string, Settings)[]
                 {
                     ("tolerans 100", new() { Allowance = 100 }), ("tolerans -1", new() { Allowance = -1 }),
                     ("güven 0", new() { Confidence = 0 }), ("güven 100", new() { Confidence = 100 }),
                     ("tempo 0", new() { Pace = 0 }), ("hedef hata 0", new() { TargetError = 0 }),
                     ("grup 0 ms", new() { GroupMs = 0 }), ("tempo NaN", new() { Pace = double.NaN })
                 })
            Check("K07 ayar reddi: " + name, () => Rejects<ArgumentException>(settings.Validate));
        Check("K07 tolerans 99,9 kabul", () => { new Settings { Allowance = 99.9 }.Validate(); return true; });

        // A08 — yeterlilik
        Check("A08 tek gözlem hesaplanamaz", () =>
            Analysis.Steps(S(R(0, 5000))).Single() is { Sufficiency: "Hesaplanamıyor", CriticalT: null, ErrorPercent: null });
        var a8 = S(R(0, 10000), R(0, 10500), R(0, 9800), R(0, 10200), R(0, 9900));
        double[] v = { 10, 10.5, 9.8, 10.2, 9.9 };
        double mean = v.Average(), sd = Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / 4);
        var a8r = Analysis.Steps(a8).Single();
        Check("A08 bağıl hata = t·s/(√n·ort)·100", () =>
            Near(a8r.ErrorPercent, StudentT.InvCDF(0, 1, 4, .975) * sd / (Math.Sqrt(5) * mean) * 100, 1e-9));
        Check("A08 hata hedefe eşitse yeterli", () =>
        {
            a8.Settings.TargetError = a8r.ErrorPercent!.Value;
            return Analysis.Steps(a8).Single().Sufficiency == "Yeterli";
        });
        a8.Settings.Confidence = 90;
        a8.Settings.TargetError = 5;
        Check("A08 t değeri güven ayarını kullanır", () =>
            Near(Analysis.Steps(a8).Single().CriticalT, StudentT.InvCDF(0, 1, 4, .95), 1e-9));
        Check("K05 çift n medyanı", () =>
            Near(Analysis.Steps(S(R(0, 1000), R(0, 2000), R(0, 3000), R(0, 4000))).Single().Median, 2.5));

        // A03 — sınıf ve kapsam dışı
        var a3 = S(R(0, 4000), R(4000, 9000, excluded: true));
        var before = Analysis.Steps(a3).Single();
        a3.Observations[0].Classification = "NVA";
        var after = Analysis.Steps(a3).Single();
        Check("A03 sınıf değişikliği analizi değiştirmez", () => before.Mean == after.Mean && before.Count == after.Count);
        Check("A03 kapsam dışı istatistiğe ve çevrime girmez", () =>
            after is { Count: 1, Excluded: 1 } && Near(Analysis.Cycles(a3).Single().Elapsed, 4));

        // A09 / K08 — paralel ve seyrek iş
        var gap = Analysis.Cycles(S(R(0, 10000), R(5000, 15000), R(20000, 22000))).Single();
        Check("K08 ölçülmemiş boşluk geçen süreye eklenmez", () => Near(gap.Elapsed, 17) && Near(gap.Workload, 22));
        var nested = Analysis.Cycles(S(R(0, 10000), R(2000, 3000))).Single();
        Check("K08 iç içe aralık bir kez sayılır", () => Near(nested.Elapsed, 10) && Near(nested.Workload, 11));
        var rare = S(R(0, 6000));
        rare.Frequency["İş"] = 3;
        Check("A09 seyrek katkı 2 sn/çevrim", () => Near(Analysis.Steps(rare).Single().Contribution, 2));
    }

    private static void Transfer()
    {
        string path = Path.Combine(Path.GetTempPath(), "vz-" + Guid.NewGuid().ToString("N") + StudyTransfer.Extension);
        var extra = new List<string> { path };
        try
        {
            var source = S(R(0, 1000, "Toplama"), R(1000, 2500, "Paketleme", 2));
            source.Name = "Örnek ğüşıöç";
            source.Video = @"C:\Users\ali\GizliKlasor\v.mp4";
            StudyTransfer.Export(source, path);
            string json = File.ReadAllText(path);
            Check("A05 pakette yerel yol ve yerel kimlik yok", () =>
                !json.Contains("GizliKlasor") && !json.Contains("ali") && !json.Contains(source.Id.ToString()));
            Check("Paket Türkçe harfleri okunur yazar", () => json.Contains("Örnek ğüşıöç"));
            var imported = StudyTransfer.Import(path);
            Check("A05 içe alınan etüt videosuz, beklenen ad korunur", () => imported is { Video: "", VideoLabel: "v.mp4", Name: "Örnek ğüşıöç" });
            string again = path + "2" + StudyTransfer.Extension;
            extra.Add(again);
            StudyTransfer.Export(imported, again);
            Check("Yeniden dışa aktarımda beklenen video adı korunur", () => StudyTransfer.Import(again).ExpectedVideoName == "v.mp4");

            var invalid = new (string Name, string Content)[]
            {
                ("boş dosya", ""), ("JSON değil", "merhaba"), ("dizi", "[]"),
                ("farklı tür", json.Replace("video-zaman-etudu", "baska")),
                ("sürüm metin", json.Replace("\"Version\":1", "\"Version\":\"1\"")),
                ("sürüm 2", json.Replace("\"Version\":1", "\"Version\":2")),
                ("ad eksik", json.Replace("\"Name\":", "\"Nm\":")),
                ("ad boş", Regex.Replace(json, "\"Name\":\"[^\"]*\"", "\"Name\":\" \"")),
                ("ölçümler null", Regex.Replace(json, "\"Observations\":\\[.*?\\]", "\"Observations\":null")),
                ("ayarlar null", Regex.Replace(json, "\"Settings\":\\{.*?\\}", "\"Settings\":null")),
                ("ölçüm null", json.Replace("\"Observations\":[", "\"Observations\":[null,")),
                ("bitiş ≤ başlangıç", json.Replace("\"EndMs\":1000", "\"EndMs\":0")),
                ("negatif başlangıç", json.Replace("\"StartMs\":1000", "\"StartMs\":-5")),
                ("ondalık çevrim", json.Replace("\"Cycle\":2", "\"Cycle\":2.5")),
                ("aşırı büyük bitiş", json.Replace("\"EndMs\":2500", "\"EndMs\":99999999999999999999")),
                ("adım boş", json.Replace("\"Step\":\"Toplama\"", "\"Step\":\"\"")),
                ("geçersiz sınıf", json.Replace("\"Classification\":\"\"", "\"Classification\":\"XYZ\"")),
                ("tolerans %100", json.Replace("\"Allowance\":0", "\"Allowance\":100")),
                ("sıklık 0", json.Replace("\"Frequency\":{}", "\"Frequency\":{\"Toplama\":0}"))
            };
            foreach (var (name, content) in invalid)
            {
                string file = Path.Combine(Path.GetTempPath(), "vz-" + Guid.NewGuid().ToString("N"));
                extra.Add(file);
                File.WriteAllText(file, content);
                Check("A05 bozuk paket reddi: " + name, () => Rejects<InvalidDataException>(() => StudyTransfer.Import(file)));
            }
            // Metin değiştirmeleri sessizce boşa düşerse yukarıdaki reddetme testleri anlamsızlaşır.
            Check("Bozuk paket senaryoları gerçekten değiştirilmiş", () =>
                invalid.Where(x => x.Name is not ("boş dosya" or "JSON değil" or "dizi")).All(x => x.Content != json));
            Check("Kod boş etüt dışa aktarılamaz", () =>
            {
                var empty = S(R(0, 1));
                empty.Code = "";
                return Rejects<InvalidDataException>(() => StudyTransfer.Export(empty, path + ".x"));
            });

            // Dosya adı önerisi
            Check("Dosya adı: kod kullanılır", () => StudyTransfer.SuggestedFileName(S(), ".vzetud") == "K-1.vzetud");
            Check("Dosya adı: geçersiz karakterler değişir", () =>
                StudyTransfer.SuggestedFileName(new Study { Code = "A/B:C*?" }, ".xlsx", "_analiz") == "A_B_C___analiz.xlsx");
            Check("Dosya adı: boş kod ve ayrılmış ad", () =>
                StudyTransfer.SuggestedFileName(new Study { Code = " " }, ".vzetud") == "VideoZamanEtudu.vzetud" &&
                StudyTransfer.SuggestedFileName(new Study { Code = "con" }, ".xlsx") == "con_.xlsx");
        }
        finally
        {
            foreach (var file in extra) if (File.Exists(file)) File.Delete(file);
        }
    }

    private static void LocalRecord()
    {
        // StudyStore SQLite'a aynı JsonSerializer ile Study nesnesini yazar.
        var study = S(R(0, 1000, "A"), R(500, 1500, "B", 2, true));
        study.Video = @"C:\v\a.mp4";
        study.Frequency["A"] = 4;
        study.Settings = new() { Confidence = 90, TargetError = 3, Pace = 105, Allowance = 12, RepeatedLow = true, GroupMs = 250, Stable = true };
        study.Observations[0].Classification = "SVA";
        var back = JsonSerializer.Deserialize<Study>(JsonSerializer.Serialize(study))!;
        Check("A04 yerel kayıt ölçüm, sınıf, dışlama, çevrim, sıklık ve ayarları geri getirir", () =>
            back.Id == study.Id && back.Video == study.Video && back.Updated == study.Updated && back.Frequency["A"] == 4 &&
            back.Settings is { Confidence: 90, TargetError: 3, Pace: 105, Allowance: 12, RepeatedLow: true, GroupMs: 250, Stable: true } &&
            back.Observations[0].Classification == "SVA" &&
            back.Observations[1] is { Excluded: true, Cycle: 2, Step: "B", StartMs: 500, EndMs: 1500 });
    }

    private static void Demo(string path)
    {
        var steps = Analysis.Steps(StudyTransfer.Import(path)).ToDictionary(x => x.Step);
        Check("A10 toplama 5,333 / 5,867 / 6,902 sn", () =>
            steps["Toplama"] is var r && Math.Round(r.Base!.Value, 3) == 5.333 &&
            Math.Round(r.Normal!.Value, 3) == 5.867 && Math.Round(r.Standard!.Value, 3) == 6.902);
        Check("A10 seyrek katkı 0,863 sn/çevrim", () => Math.Round(steps["Seyrek kontrol"].Contribution!.Value, 3) == 0.863);
    }
}
