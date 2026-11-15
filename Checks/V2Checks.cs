using System.Text.Json;

namespace VideoZamanEtudu;

/// <summary>
/// Masaüstü v2 (tarayıcı v1.5–v1.7 kuralları): isteğe bağlı paket alanları, adım temposu, tolerans kalemleri,
/// süreç sırası, karşılaştırma, devam noktası, ölçüm sırası ve toplu yedek.
/// Shared/ek-alanlar.json tarayıcı çekirdeğinden üretilir; C# aynı paketi aynı metinle yazmalı ve aynı sonuçları bulmalıdır.
/// </summary>
internal static class V2Checks
{
    private static int pass, fail;

    public static void Run(string? demoPath, string? sharedPath)
    {
        Rules();
        Flow();
        Transfer();
        if (demoPath != null) Backup(demoPath);
        if (sharedPath != null) Shared(sharedPath, demoPath);
        Console.WriteLine($"v2 kontrolleri: {pass} geçti, {fail} kaldı.");
        if (fail > 0) throw new Exception($"{fail} v2 kontrolü başarısız.");
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

    private static bool Same(double? actual, double? expected) =>
        actual is null || expected is null ? actual is null && expected is null
            : Math.Abs(actual.Value - expected.Value) <= 1e-9 * Math.Max(1, Math.Abs(expected.Value));

    private static double? Num(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetDouble();

    private static Observation R(long start, long end, string step, int cycle = 1, bool excluded = false, string cls = "") =>
        new() { StartMs = start, EndMs = end, Step = step, Cycle = cycle, Excluded = excluded, Classification = cls };

    private static void Rules()
    {
        Check("Tolerans kalemleri temizlenir", () =>
            StudyRules.CleanAllowanceParts(new Dictionary<string, double> { ["Personal"] = 5, ["Fatigue"] = 4, ["Delay"] = 0, ["Other"] = -1, ["Foo"] = 3 })
                is { Count: 2 } d && d["Personal"] == 5 && d["Fatigue"] == 4);
        Check("Tolerans kalemleri toplamı %100 olamaz", () =>
            StudyRules.CleanAllowanceParts(new Dictionary<string, double> { ["Personal"] = 60, ["Fatigue"] = 40 }).Count == 0);
        Check("Tolerans toplamı ondalık kaymasız", () => StudyRules.AllowanceSum(new Dictionary<string, double> { ["Personal"] = 0.1, ["Fatigue"] = 0.2 }) == 0.3);
        Check("Kalemler toplam tutmazsa düşer", () =>
            StudyRules.MatchingParts(new Dictionary<string, double> { ["Personal"] = 5, ["Fatigue"] = 4 }, 12).Count == 0 &&
            StudyRules.MatchingParts(new Dictionary<string, double> { ["Personal"] = 5, ["Fatigue"] = 4 }, 9).Count == 2);
        Check("Adım temposu (0, 300] aralığında", () =>
            StudyRules.CleanStepPace(new Dictionary<string, double> { ["A"] = 0, ["B"] = 301, [" C "] = 90 }) is { Count: 1 } p && p["C"] == 90);
        Check("Takt pozitif", () => StudyRules.CleanTakt(0) is null && StudyRules.CleanTakt(-3) is null && StudyRules.CleanTakt(20) == 20);
        Check("Adım listesi: boş ve tekrar atılır, sınıf denetlenir, not kısaltılır", () =>
            StudyRules.CleanSteps(new[] { new StepDef { Name = " A " }, new StepDef { Name = "A" }, new StepDef { Name = "" },
                new StepDef { Name = "B", Classification = "XYZ", Note = new string('x', 300) } }) is { Count: 2 } s &&
            s[0].Name == "A" && s[1].Classification == "" && s[1].Note!.Length == 200);
        Check("Etüt bilgileri: bilinmeyen alan ve boş değer atılır", () =>
            StudyRules.CleanInfo(new Dictionary<string, string> { ["Operator"] = " Ali ", ["Foo"] = "x", ["Shift"] = " " }) is { Count: 1 } i && i["Operator"] == "Ali");
        Check("Kayıt notu boşsa null", () => new Observation { Note = "  " }.Note is null && new Observation { Note = " a " }.Note == "a");

        var study = new Study { Settings = new Settings { Pace = 100 }, Observations = new() { R(0, 1000, "A"), R(0, 2000, "B") } };
        study.StepPace["A"] = 120;
        var rows = Analysis.Steps(study).ToDictionary(r => r.Step);
        Check("Adım temposu yalnız o adımı etkiler", () => rows["A"].Pace == 120 && Same(rows["A"].Normal, 1.2) && rows["B"].Pace == 100 && Same(rows["B"].Normal, 2));

        var noSteps = new Study { Observations = new() { R(5000, 6000, "Son", cls: "NVA"), R(0, 1000, "İlk", cls: "VA"), R(1000, 2000, "İlk", cls: "SVA"), R(2000, 3000, "İlk", cls: "SVA") } };
        Check("Adım listesi yoksa ölçümlerden çıkar (ilk görülme sırası, en sık sınıf)", () =>
            StudyRules.DeriveSteps(noSteps) is { Count: 2 } d && d[0].Name == "İlk" && d[0].Classification == "SVA" && d[1].Classification == "NVA");
        noSteps.Steps = new() { new StepDef { Name = "Son" } };
        Check("Süreç sırası: liste önce, listede olmayanlar sonda", () => StudyRules.StepOrder(noSteps).SequenceEqual(new[] { "Son", "İlk" }));

        var spread = StudyRules.CycleSpread(new[] { new CycleResult(1, 10, 10), new CycleResult(2, 10, 10), new CycleResult(3, 10, 10), new CycleResult(4, 20, 20) });
        Check("Çevrim değişkenliği ve %20 sapma (tam %20 sapma işaretlenmez)", () => Same(spread.Mean, 12.5) && spread.Rows.Single(r => r.Outlier).Cycle == 4);
        Check("İki çevrimde sapma işaretlenmez", () =>
            StudyRules.CycleSpread(new[] { new CycleResult(1, 10, 10), new CycleResult(2, 30, 30) }).Rows.All(r => !r.Outlier));
    }

    private static void Flow()
    {
        var f = new MeasureFlow { Cycle = 1 };
        f.Advance(3); f.Advance(3);
        Check("Ölçüm sırası: son adımdan sonra çevrim artar", () => { f.Advance(3); return f.Index == 0 && f.Cycle == 2; });
        f.Select(2, 3);
        f.AfterRecord(excluded: true, autoAdvance: true, count: 3);
        Check("Kapsam dışı kayıt önceki adıma döner, çevrimi değiştirmez", () => f.Index == 0 && f.Cycle == 2 && f.Resume is null);
        f.AfterRecord(excluded: false, autoAdvance: false, count: 3);
        Check("Otomatik ilerleme kapalıysa adım kalır", () => f.Index == 0);
        f.Added(4);
        Check("Yeni adım seçilir, önceki hatırlanır", () => f.Index == 3 && f.Resume == 0);

        var s = new Study
        {
            Steps = new() { new StepDef { Name = "A" }, new StepDef { Name = "B" } },
            Observations = new() { R(0, 1000, "A", 1), R(1000, 2000, "B", 1), R(2000, 3000, "A", 2), R(3000, 3500, "Bekle", 2, true) }
        };
        Check("Devam noktası: son geçerli kayıttan sonraki adım, son kaydın bitişi", () => StudyRules.Resume(s) == new ResumePoint(1, 2, 3500));
        s.Observations.Add(R(3500, 4000, "B", 2));
        Check("Devam noktası: son adımdan sonra çevrim artar", () => StudyRules.Resume(s) == new ResumePoint(0, 3, 4000));
        Check("Devam noktası: kayıt yoksa ilk adım, çevrim 1", () => StudyRules.Resume(new Study()) == new ResumePoint(0, 1, null));
    }

    private static void Transfer()
    {
        var study = new Study { Name = "E", Code = "K", Observations = new() { R(0, 1000, "A") } };
        var json = System.Text.Encoding.UTF8.GetString(StudyTransfer.ExportBytes(study));
        Check("Ek alan yoksa pakete yazılmaz (Windows v1.2 paketiyle aynı)", () =>
            !json.Contains("Steps") && !json.Contains("Info") && !json.Contains("StepPace") && !json.Contains("Takt") &&
            !json.Contains("AllowanceParts") && !json.Contains("Note"));
        foreach (var (name, replace) in new[]
        {
            ("Steps metin", "\"Steps\":\"x\""), ("Info dizi", "\"Info\":[1]"), ("StepPace metin", "\"StepPace\":{\"A\":\"y\"}"),
            ("Takt metin", "\"Takt\":\"20\""), ("AllowanceParts dizi", "\"AllowanceParts\":[5]")
        })
        {
            var bad = json[..^1] + "," + replace + "}";
            Check("Bozuk isteğe bağlı alan paketi reddettirmez: " + name, () =>
                StudyTransfer.ImportBytes(System.Text.Encoding.UTF8.GetBytes(bad)) is { Steps.Count: 0, Info.Count: 0, StepPace.Count: 0, Takt: null, AllowanceParts.Count: 0 });
        }
        var withNote = json.Replace("\"Cycle\":1}", "\"Cycle\":1,\"Note\":5}");
        Check("Metin olmayan kayıt notu yok sayılır", () => withNote != json && StudyTransfer.ImportBytes(System.Text.Encoding.UTF8.GetBytes(withNote)).Observations[0].Note is null);
        Check("Hata iletilerinde çeviri anahtarı var", () =>
        {
            try { StudyTransfer.ImportBytes(System.Text.Encoding.UTF8.GetBytes(json.Replace("\"Version\":1", "\"Version\":2"))); return false; }
            catch (InvalidDataException e) { return (string?)e.Data["key"] == "pkg.version"; }
        });

        var local = new Study { Name = "E", Code = "K", Takt = 20, Observations = new() { R(0, 1000, "A") } };
        local.Observations[0].Note = "not";
        local.Steps.Add(new StepDef { Name = "A", Classification = "VA", Note = "biter" });
        local.Info["Operator"] = "Ali";
        local.StepPace["A"] = 90;
        local.Settings.Allowance = 9;
        local.AllowanceParts["Personal"] = 5; local.AllowanceParts["Fatigue"] = 4;
        var back = JsonSerializer.Deserialize<Study>(JsonSerializer.Serialize(local))!;
        Check("Yerel kayıt v2 alanlarını geri getirir", () =>
            back.Takt == 20 && back.Observations[0].Note == "not" && back.Steps[0] is { Name: "A", Classification: "VA", Note: "biter" } &&
            back.Info["Operator"] == "Ali" && back.StepPace["A"] == 90 && back.AllowanceParts.Count == 2);
        var old = JsonSerializer.Deserialize<Study>("{\"Name\":\"E\",\"Code\":\"K\",\"Observations\":[],\"Settings\":{},\"Frequency\":{}}")!;
        Check("v1.2 yerel kaydı v2'de açılır (yeni alanlar boş)", () =>
            old.Steps.Count == 0 && old.Info.Count == 0 && old.StepPace.Count == 0 && old.Takt is null && old.AllowanceParts.Count == 0);
    }

    private static void Backup(string demoPath)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vz-yedek-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = StudyTransfer.Import(demoPath);
            var b = StudyTransfer.Import(demoPath); b.Name = "İkinci"; b.Observations = b.Observations.Take(4).ToList();
            var c = StudyTransfer.Import(demoPath); c.Code = "Başka/Kod";
            var zip = Path.Combine(dir, "yedek.zip");
            var written = StudyBackup.Write(new[] { a, b, c }, zip);
            Check("Yedek: üç etüt, ayrı dosya adları", () =>
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
                return written == new BackupResult(3, 0) && archive.Entries.Select(e => e.Name.ToLowerInvariant()).Distinct().Count() == 3 && StudyBackup.IsZip(zip);
            });
            Check("Yedek: boş depoya üçü eklenir", () => StudyBackup.Read(zip, Array.Empty<Study>()) is { Add.Count: 3, Same: 0, Failed.Count: 0 });
            Check("Yedek: kayıtlı olan ikinci kez eklenmez", () => StudyBackup.Read(zip, new[] { a }) is { Add.Count: 2, Same: 1 });
            Check("Yedek olmayan dosya .zip sayılmaz", () => !StudyBackup.IsZip(demoPath));
            var notZip = Path.Combine(dir, "x.zip");
            File.WriteAllText(notZip, "merhaba");
            Check("Bozuk yedek anahtarla reddedilir", () =>
            {
                try { StudyBackup.Read(notZip, Array.Empty<Study>()); return false; }
                catch (InvalidDataException e) { return (string?)e.Data["key"] == "backup.notZip"; }
            });
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static void Shared(string path, string? demoPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        string package = root.GetProperty("Package").GetString()!;
        var e = root.GetProperty("Expected");
        var study = StudyTransfer.ImportBytes(System.Text.Encoding.UTF8.GetBytes(package));
        Check("Ortak paket: C# aynı paketi aynı metinle yazar", () => System.Text.Encoding.UTF8.GetString(StudyTransfer.ExportBytes(study)) == package);
        var rows = StudyRules.InProcessOrder(study, Analysis.Steps(study));
        Check("Ortak paket: süreç sırası", () => rows.Select(r => r.Step).SequenceEqual(e.GetProperty("ProcessOrder").EnumerateArray().Select(x => x.GetString()!)));
        foreach (var x in e.GetProperty("Steps").EnumerateArray())
        {
            string step = x.GetProperty("Step").GetString()!;
            var r = rows.Single(r => r.Step == step);
            Check("Ortak paket: " + step + " tempo, normal, standart, katkı, yeterlilik", () =>
                Same(r.Pace, x.GetProperty("Pace").GetDouble()) && Same(r.Normal, Num(x.GetProperty("Normal"))) &&
                Same(r.Standard, Num(x.GetProperty("Standard"))) && Same(r.Contribution, Num(x.GetProperty("Contribution"))) &&
                r.Sufficiency == x.GetProperty("Sufficiency").GetString());
            if (x.GetProperty("Required").ValueKind == JsonValueKind.Number)
                Check("Ortak paket: " + step + " gereken n", () => StudyRules.RequiredObservations(r, study.Settings) == x.GetProperty("Required").GetInt32());
        }
        var (total, incomplete) = StudyRules.TotalStandard(rows);
        Check("Ortak paket: çevrim başına standart süre", () => Same(total, Num(e.GetProperty("Total"))) && incomplete == e.GetProperty("Incomplete").GetBoolean());
        var spread = StudyRules.CycleSpread(Analysis.Cycles(study));
        var es = e.GetProperty("Spread");
        Check("Ortak paket: çevrim değişkenliği", () =>
            Same(spread.Mean, Num(es.GetProperty("Mean"))) && Same(spread.Deviation, Num(es.GetProperty("Deviation"))) &&
            Same(spread.Variation, Num(es.GetProperty("Variation"))) &&
            spread.Rows.Where(c => c.Outlier).Select(c => c.Cycle).SequenceEqual(es.GetProperty("Outliers").EnumerateArray().Select(c => c.GetInt32())));
        Check("Ortak paket: tolerans kalemleri", () =>
            study.AllowanceParts.Count == e.GetProperty("Parts").EnumerateObject().Count() &&
            e.GetProperty("Parts").EnumerateObject().All(p => Same(study.AllowanceParts[p.Name], p.Value.GetDouble())));
        Check("Ortak paket: kayıt notları", () =>
            study.Observations.Where(o => o.Note != null).Select(o => (o.StartMs, o.Note!))
                .SequenceEqual(e.GetProperty("Notes").EnumerateArray().Select(n => (n.GetProperty("StartMs").GetInt64(), n.GetProperty("Note").GetString()!))));
        Check("Ortak paket: etüt bilgileri, adım açıklamaları, takt", () =>
            study.Info["Operator"] == "A. Kaya" && study.Steps[0].Note == "kutu masaya konunca biter" && study.Takt == 18.5);
        if (demoPath == null) return;
        var plain = StudyTransfer.Import(demoPath);
        Check("Ortak: adım listesi olmayan demoda çıkarılan adımlar", () =>
            StudyRules.DeriveSteps(plain).Select(s => (s.Name, s.Classification))
                .SequenceEqual(e.GetProperty("DerivedSteps").EnumerateArray().Select(s => (s.GetProperty("Name").GetString()!, s.GetProperty("Classification").GetString()!))));
        Check("Ortak: süreç sırası (adım listesiz)", () =>
            StudyRules.StepOrder(plain).SequenceEqual(e.GetProperty("PlainOrder").EnumerateArray().Select(s => s.GetString()!)));
        var cmp = StudyRules.Compare(plain, study);
        var ec = e.GetProperty("Compare");
        Check("Ortak: önce/sonra toplamları", () =>
            Same(cmp.Before, Num(ec.GetProperty("Before"))) && Same(cmp.After, Num(ec.GetProperty("After"))) &&
            Same(cmp.Diff, Num(ec.GetProperty("Diff"))) && Same(cmp.DiffPercent, Num(ec.GetProperty("DiffPercent"))));
        Check("Ortak: önce/sonra satırları", () =>
            cmp.Rows.Count == ec.GetProperty("Rows").GetArrayLength() &&
            cmp.Rows.Zip(ec.GetProperty("Rows").EnumerateArray()).All(p => p.First.Step == p.Second.GetProperty("Step").GetString() &&
                Same(p.First.Before, Num(p.Second.GetProperty("Before"))) && Same(p.First.After, Num(p.Second.GetProperty("After")))));
    }
}
