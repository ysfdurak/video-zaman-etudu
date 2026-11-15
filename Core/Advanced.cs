using MathNet.Numerics.Distributions;

namespace VideoZamanEtudu;

public record OutlierGroup(string Step, double Q1, double Q3, double Low, double High, IReadOnlyList<Observation> Rows);
public record HistogramBin(double From, double To, int Count);
public record BalanceStep(string Step, double Contribution, string Classification);
public record BalanceStation(string Station, double Load, IReadOnlyList<BalanceStep> Steps);
public record BalanceResult(IReadOnlyList<BalanceStation> Stations, IReadOnlyList<BalanceStep> Unassigned, double? Total, double? Max,
    string? Bottleneck, double? Efficiency, double? Loss, int? MinOperators, IReadOnlyList<string> OverTakt);
public record WorkRow(string Step, string Type, double Start, double End, double Seconds);
public record StandardWorkResult(IReadOnlyList<WorkRow> Rows, double Operator, double Total);
public record Improvement(string Step, string Classification, string Waste, string Idea, double? Contribution, double? Share);
public record SamplingRow(string Category, string Classification, int Count, double? Share, double? Half, double? Low, double? High, int? Required);
public record SamplingResult(int N, double Z, double Error, IReadOnlyList<SamplingRow> Rows, int? Required, IReadOnlyDictionary<string, double>? ByClass);

/// <summary>
/// v2.1 ileri analizler (tarayıcı karşılığı: Web/js/advanced.js): aykırı değer (IQR), histogram, hat dengeleme (Yamazumi),
/// otomatik dengeleme, standart iş kombinasyon tablosu, iyileştirme listesi, adım karesi ve iş örneklemesi.
/// Shared/ek-alanlar.json ile tarayıcıyla aynı sonucu verdiği denetlenir.
/// </summary>
public static class Advanced
{
    private static long Dur(Observation o) => o.EndMs - o.StartMs;
    private static bool Valid(Observation o) => !o.Excluded && o.EndMs > o.StartMs;

    /// <summary>Doğrusal ara değerli yüzdelik (R tip 7). Değerler sıralı olmalı.</summary>
    public static double Quantile(IReadOnlyList<double> sorted, double p)
    {
        double h = (sorted.Count - 1) * p;
        int lo = (int)Math.Floor(h), hi = (int)Math.Ceiling(h);
        return sorted[lo] + (h - lo) * (sorted[hi] - sorted[lo]);
    }

    private static List<StepResult> Rows(Study study) => StudyRules.InProcessOrder(study, Analysis.Steps(study));

    /// <summary>IQR kuralının dışında kalan geçerli kayıtlar (en az 4 geçerli kayıtlı adımlarda).</summary>
    public static IReadOnlyList<OutlierGroup> Outliers(Study study, double factor = 1.5)
    {
        var result = new List<OutlierGroup>();
        foreach (var step in Rows(study).Select(r => r.Step))
        {
            var rows = study.Observations.Where(o => o.Step == step && Valid(o)).ToList();
            if (rows.Count < 4) continue;
            var sorted = rows.Select(o => (double)Dur(o)).Order().ToList();
            double q1 = Quantile(sorted, 0.25), q3 = Quantile(sorted, 0.75), iqr = q3 - q1;
            double low = q1 - factor * iqr, high = q3 + factor * iqr;
            var outside = rows.Where(o => Dur(o) < low - 1e-9 || Dur(o) > high + 1e-9).OrderBy(o => o.StartMs).ToList();
            if (outside.Count > 0) result.Add(new OutlierGroup(step, q1 / 1000, q3 / 1000, low / 1000, high / 1000, outside));
        }
        return result;
    }

    /// <summary>Eşit genişlikte histogram; sınıf sayısı √n (3–10).</summary>
    public static IReadOnlyList<HistogramBin> Histogram(Study study, string step)
    {
        var values = study.Observations.Where(o => o.Step == step && Valid(o)).Select(o => Dur(o) / 1000d).Order().ToList();
        if (values.Count == 0) return Array.Empty<HistogramBin>();
        double min = values[0], max = values[^1];
        int bins = Math.Min(10, Math.Max(3, (int)Math.Ceiling(Math.Sqrt(values.Count))));
        if (max - min < 1e-9) return new[] { new HistogramBin(min, max, values.Count) };
        double width = (max - min) / bins;
        var counts = new int[bins];
        foreach (var v in values) counts[Math.Min(bins - 1, (int)Math.Floor((v - min) / width + 1e-9))]++;
        return Enumerable.Range(0, bins).Select(i => new HistogramBin(min + i * width, min + (i + 1) * width, counts[i])).ToList();
    }

    /// <summary>Hat dengeleme: istasyon yükleri, darboğaz, denge verimi ve kaybı, takt varsa en az operatör.</summary>
    public static BalanceResult LineBalance(Study study, double? takt = null)
    {
        takt ??= study.Takt;
        var rows = Rows(study).Where(r => r.Contribution is not null).ToList();
        var defs = StudyRules.DeriveSteps(study).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First());
        var stations = new List<(string Station, double Load, List<BalanceStep> Steps)>();
        var unassigned = new List<BalanceStep>();
        foreach (var r in rows)
        {
            var def = defs.GetValueOrDefault(r.Step);
            var item = new BalanceStep(r.Step, r.Contribution!.Value, def?.Classification ?? "");
            string name = def?.Station ?? "";
            if (name.Length == 0) { unassigned.Add(item); continue; }
            int i = stations.FindIndex(s => s.Station == name);
            if (i < 0) { stations.Add((name, 0, new List<BalanceStep>())); i = stations.Count - 1; }
            stations[i].Steps.Add(item);
            stations[i] = (name, stations[i].Load + item.Contribution, stations[i].Steps);
        }
        double total = rows.Aggregate(0d, (s, r) => s + r.Contribution!.Value);
        double assigned = stations.Aggregate(0d, (s, x) => s + x.Load);
        double? max = stations.Count > 0 ? stations.Max(s => s.Load) : null;
        string? bottleneck = max is double m ? stations.First(s => s.Load == m).Station : null;
        double? efficiency = stations.Count > 0 && max > 0 ? assigned / (stations.Count * max.Value) * 100 : null;
        return new BalanceResult(
            stations.Select(s => new BalanceStation(s.Station, s.Load, s.Steps)).ToList(), unassigned,
            rows.Count > 0 ? total : null, max, bottleneck, efficiency, efficiency is null ? null : 100 - efficiency,
            takt > 0 && rows.Count > 0 ? (int)Math.Ceiling(total / takt.Value - 1e-9) : null,
            takt > 0 ? stations.Where(s => s.Load > takt.Value + 1e-9).Select(s => s.Station).ToList() : new List<string>());
    }

    /// <summary>Otomatik dengeleme: süreç sırası korunur, yük + adım > takt olunca sonraki istasyon ("1", "2", …).</summary>
    public static IReadOnlyList<(string Step, string Station)> AutoBalance(Study study, double? takt = null)
    {
        takt ??= study.Takt;
        var result = new List<(string, string)>();
        if (!(takt > 0)) return result;
        int station = 1;
        double load = 0;
        foreach (var r in Rows(study))
        {
            double c = r.Contribution ?? 0;
            if (load > 0 && load + c > takt.Value + 1e-9) { station++; load = 0; }
            result.Add((r.Step, station.ToString()));
            load += c;
        }
        return result;
    }

    /// <summary>Standart iş kombinasyon tablosu: el/yürüme/bekleme ardışık, makine işi paralel; süre = katkı.</summary>
    public static StandardWorkResult StandardWork(Study study)
    {
        var defs = StudyRules.DeriveSteps(study).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First());
        double cursor = 0, end = 0;
        var rows = new List<WorkRow>();
        foreach (var r in Rows(study).Where(r => r.Contribution is not null))
        {
            string type = defs.GetValueOrDefault(r.Step)?.Type is { Length: > 0 } t ? t : "Manual";
            double start = cursor, finish = start + r.Contribution!.Value;
            if (type != "Machine") cursor = finish;
            end = Math.Max(end, finish);
            rows.Add(new WorkRow(r.Step, type, start, finish, r.Contribution.Value));
        }
        return new StandardWorkResult(rows, cursor, Math.Max(end, cursor));
    }

    /// <summary>İsraf türü ya da fikri girilmiş adımlar ve NVA adımlar; pay = katkı ÷ çevrim standardı.</summary>
    public static IReadOnlyList<Improvement> Improvements(Study study)
    {
        var rows = Rows(study);
        double total = rows.Aggregate(0d, (s, r) => s + (r.Contribution ?? 0));
        var defs = StudyRules.DeriveSteps(study).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First());
        return rows.Where(r => defs.TryGetValue(r.Step, out var d) && (d.Waste != null || d.Idea != null || d.Classification == "NVA"))
            .Select(r =>
            {
                var d = defs[r.Step];
                return new Improvement(r.Step, d.Classification, d.Waste ?? "", d.Idea ?? "", r.Contribution,
                    total > 0 && r.Contribution is double c ? c / total * 100 : null);
            }).ToList();
    }

    /// <summary>Adım karesi: tanımlıysa o; yoksa adımın ilk geçerli kaydının ortası (ms).</summary>
    public static long? StepFrame(Study study, string step)
    {
        if (study.Steps.FirstOrDefault(x => x.Name == step)?.FrameMs is long f) return f;
        var first = study.Observations.Where(o => o.Step == step && Valid(o)).OrderBy(o => o.StartMs).FirstOrDefault();
        return first is null ? null : (long)Math.Round((first.StartMs + first.EndMs) / 2d, MidpointRounding.AwayFromZero);
    }

    /// <summary>İş örneklemesi: pay, p ± z·√(p(1−p)/n) ve hedef mutlak hata için gereken gözlem sayısı.</summary>
    public static SamplingResult AnalyzeSampling(Study study)
    {
        int n = study.Samples.Count;
        double z = Normal.InvCDF(0, 1, (1 + study.Settings.Confidence / 100) / 2);
        double e = (study.SampleError ?? 5) / 100;
        var names = study.Categories.Select(c => c.Name).ToList();
        foreach (var s in study.Samples) if (!names.Contains(s.Category)) names.Add(s.Category);
        var cls = study.Categories.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.First().Classification);
        var rows = names.Select(name =>
        {
            int count = study.Samples.Count(s => s.Category == name);
            double? p = n > 0 ? (double)count / n : null;
            double? half = p is double pv ? z * Math.Sqrt(pv * (1 - pv) / n) : null;
            return new SamplingRow(name, cls.GetValueOrDefault(name) ?? "", count, p * 100, half * 100,
                p is null ? null : Math.Max(0, p.Value - half!.Value) * 100, p is null ? null : Math.Min(1, p.Value + half!.Value) * 100,
                p is double q ? (int)Math.Ceiling(z * z * q * (1 - q) / (e * e) - 1e-9) : null);
        }).ToList();
        Dictionary<string, double>? byClass = null;
        if (n > 0)
        {
            byClass = new() { ["VA"] = 0, ["SVA"] = 0, ["NVA"] = 0, [""] = 0 };
            foreach (var r in rows) byClass[r.Classification] += r.Share ?? 0;
        }
        return new SamplingResult(n, z, e * 100, rows, rows.Count > 0 && n > 0 ? rows.Max(r => r.Required) : null, byClass);
    }

    /// <summary>Rastgele gözlem anı: mevcut gözlemlere en az gap ms uzak (en fazla 200 deneme).</summary>
    public static long? RandomMoment(Study study, long durationMs, Random random, long gap = 1000)
    {
        if (durationMs <= 0) return null;
        for (int i = 0; i < 200; i++)
        {
            long at = (long)Math.Floor(random.NextDouble() * durationMs);
            if (study.Samples.All(s => Math.Abs((s.AtMs ?? 0) - at) >= gap)) return at;
        }
        return (long)Math.Floor(random.NextDouble() * durationMs);
    }
}
