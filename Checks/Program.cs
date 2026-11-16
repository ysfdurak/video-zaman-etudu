using VideoZamanEtudu;

static void Near(double? actual, double expected, double tolerance = 1e-8)
{
    if (actual is not double value || !double.IsFinite(value) || Math.Abs(value - expected) > tolerance)
        throw new InvalidOperationException($"Beklenen {expected}, gerçekleşen {actual}");
}
static Observation Row(long start, long end, bool excluded = false) =>
    new() { StartMs = start, EndMs = end, Step = "Toplama", Excluded = excluded };

var study = new Study
{
    Settings = new Settings { Pace = 110, Allowance = 15 },
    Observations = new() { Row(0, 90000), Row(100000, 210000), Row(300000, 999000, true) }
};
study.Frequency["Toplama"] = 10;
var result = Analysis.Steps(study).Single();
Near(result.Count, 2);
Near(result.Excluded, 1);
Near(result.Base, 100);
Near(result.Normal, 110);
Near(result.Standard, 110 / .85);
Near(result.Contribution, 11 / .85);
Near(result.CriticalT, 12.706204736, 1e-6);
Near(result.ErrorPercent, 127.06204736, 1e-5);
study.Settings.RepeatedLow = true;
if (Analysis.Steps(study).Single().Base != null) throw new Exception("Tekrar bulunmamalı.");

study.Observations = new() { Row(0, 10000), Row(5000, 15000), Row(20000, 32000), Row(40000, 52000) };
Near(Analysis.Steps(study).Single().Base, 10);
var overlap = Analysis.Cycles(new Study { Observations = new() { Row(0, 10000), Row(5000, 15000) } }).Single();
Near(overlap.Elapsed, 15);
Near(overlap.Workload, 20);
Console.WriteLine("Hesaplama kabul kontrolleri geçti.");

var transferPath = Path.GetTempFileName();
try
{
    study.Name = "Örnek etüt";
    study.Code = "ET-01";
    study.Video = @"C:\GizliKlasor\örnek.mp4";
    study.Observations[0].Classification = "NVA";
    study.Observations[0].Excluded = true;
    study.Observations[0].Cycle = 3;
    StudyTransfer.Export(study, transferPath);
    var json = File.ReadAllText(transferPath);
    if (json.Contains("GizliKlasor")) throw new Exception("Yerel dosya yolu paylaşıldı.");
    var imported = StudyTransfer.Import(transferPath);
    var importedAgain = StudyTransfer.Import(transferPath);
    if (imported.Id == study.Id || imported.Id == importedAgain.Id || imported.Video != "" ||
        imported.ExpectedVideoName != "örnek.mp4" || imported.Name != study.Name || imported.Code != study.Code ||
        imported.Observations.Count != study.Observations.Count ||
        imported.Observations[0] is not { Classification: "NVA", Excluded: true, Cycle: 3, StartMs: 0, EndMs: 10000 } ||
        imported.Settings.Allowance != 15 || !imported.Settings.RepeatedLow || imported.Frequency["Toplama"] != 10)
        throw new Exception("Etüt aktarımında kayıt bilgileri korunmadı.");

    foreach (var invalid in new[] { "{", json.Replace("\"Version\":1", "\"Version\":2"), json.Replace("\"Cycle\":3", "\"Cycle\":0") })
    {
        File.WriteAllText(transferPath, invalid);
        try
        {
            StudyTransfer.Import(transferPath);
            throw new Exception("Geçersiz paket kabul edildi.");
        }
        catch (InvalidDataException) { }
    }
    Console.WriteLine("Etüt aktarımı kabul kontrolleri geçti.");
}
finally
{
    File.Delete(transferPath);
}

if (args.Length >= 1)
{
    var demo = StudyTransfer.Import(args[0]);
    var steps = Analysis.Steps(demo).ToDictionary(row => row.Step);
    Near(demo.Observations.Count, 14);
    Near(demo.Observations.Count(row => row.Excluded), 1);
    Near(steps["Bekleme"].Count, 0);
    Near(steps["Toplama"].Base, 16d / 3);
    Near(steps["Toplama"].Normal, 16d / 3 * 1.10);
    Near(steps["Toplama"].Standard, 16d / 3 * 1.10 / .85);
    Near(steps["Seyrek kontrol"].Contribution, 2 * 1.10 / .85 / 3);
    var demoCycles = Analysis.Cycles(demo);
    Near(demoCycles.Count, 3);
    for (int i = 0; i < demoCycles.Count; i++)
    {
        Near(demoCycles[i].Cycle, i + 1);
        Near(demoCycles[i].Elapsed, 10 + i);
        Near(demoCycles[i].Workload, 12 + i);
    }
    Console.WriteLine("Sentetik depo etüdü kabul kontrolleri geçti.");
}

ExtraChecks.Run(args.Length >= 1 ? args[0] : null);
if (args.Length >= 2) ParityChecks.Run(args[1]);
// v2: ortak ek alan dosyası eşdeğerlik dosyasının yanında durur.
string? shared = args.Length >= 2 ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "ek-alanlar.json") : null;
V2Checks.Run(args.Length >= 1 ? args[0] : null, shared != null && File.Exists(shared) ? shared : null);
