// Shared/esdeger-ornekler.json üreticisi. Hesap veya paket kuralları değiştiğinde çalıştırılır:
//   dotnet run --project Tools/EsdegerUretici -c Release -- Shared/esdeger-ornekler.json demo/sentetik-depo-etudu.vzetud
// Ardından hem Checks (C#) hem Web/tests (JavaScript) yeni dosyaya karşı geçmelidir.
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using MathNet.Numerics.Distributions;
using VideoZamanEtudu;

var outPath = args[0];
var cases = new JsonArray();
Observation R(long s, long e, string st = "İş", int c = 1, bool x = false, string cl = "") => new() { StartMs = s, EndMs = e, Step = st, Cycle = c, Excluded = x, Classification = cl };
void Add(string name, Study st)
{
    var steps = new JsonArray();
    foreach (var r in Analysis.Steps(st))
        steps.Add(new JsonObject
        {
            ["Step"] = r.Step, ["Count"] = r.Count, ["Excluded"] = r.Excluded, ["Mean"] = r.Mean, ["Median"] = r.Median,
            ["Min"] = r.Min, ["Max"] = r.Max, ["Deviation"] = r.Deviation, ["CriticalT"] = r.CriticalT, ["ErrorPercent"] = r.ErrorPercent,
            ["Sufficiency"] = r.Sufficiency, ["Base"] = r.Base, ["Normal"] = r.Normal, ["Standard"] = r.Standard, ["Every"] = r.Every,
            ["Contribution"] = r.Contribution, ["Method"] = r.Method,
            ["Groups"] = new JsonArray(r.Groups.Select(g => (JsonNode)new JsonObject { ["Seconds"] = g.Seconds, ["Count"] = g.Count }).ToArray())
        });
    var cyc = new JsonArray(Analysis.Cycles(st).Select(c => (JsonNode)new JsonObject { ["Cycle"] = c.Cycle, ["Elapsed"] = c.Elapsed, ["Workload"] = c.Workload }).ToArray());
    cases.Add(new JsonObject
    {
        ["Name"] = name,
        ["Input"] = new JsonObject
        {
            ["Settings"] = JsonSerializer.SerializeToNode(st.Settings),
            ["Observations"] = new JsonArray(st.Observations.Select(o => (JsonNode)new JsonObject { ["StartMs"] = o.StartMs, ["EndMs"] = o.EndMs, ["Step"] = o.Step, ["Classification"] = o.Classification, ["Excluded"] = o.Excluded, ["Cycle"] = o.Cycle }).ToArray()),
            ["Frequency"] = JsonSerializer.SerializeToNode(st.Frequency)
        },
        ["Steps"] = steps, ["Cycles"] = cyc
    });
}
Study S(Settings? s, params Observation[] o) => new() { Name = "E", Code = "K", Observations = o.ToList(), Settings = s ?? new() };

Add("demo", StudyTransfer.Import(args[1]));
Add("A06 ortalama", S(null, R(0, 5000), R(0, 6000), R(0, 5000)));
Add("A06 tekrar", S(new() { RepeatedLow = true }, R(0, 5000), R(0, 6000), R(0, 5000)));
Add("A06 tekrarsız", S(new() { RepeatedLow = true }, R(0, 5000), R(0, 6000), R(0, 7000)));
Add("K06 yuvarlama sınırı", S(new() { RepeatedLow = true }, R(0, 5049), R(0, 5050), R(0, 5149), R(0, 5150)));
Add("K06 eşitlik", S(new() { RepeatedLow = true }, R(0, 8000), R(0, 8020), R(0, 6000), R(0, 6040)));
Add("A07", S(new() { Pace = 110, Allowance = 15 }, R(0, 100000)));
Add("A08 tek gözlem", S(null, R(0, 5000)));
Add("A08 beş gözlem", S(null, R(0, 10000), R(0, 10500), R(0, 9800), R(0, 10200), R(0, 9900)));
Add("aynı değerler", S(null, R(0, 1000), R(0, 1000)));
Add("hepsi kapsam dışı", S(null, R(0, 1000, x: true), R(0, 2000, x: true)));
Add("boş etüt", S(null));
Add("K08 boşluk", S(null, R(0, 10000), R(5000, 15000), R(20000, 22000)));
Add("K08 iç içe", S(null, R(0, 10000), R(2000, 3000)));
var fr = S(null, R(0, 6000), R(0, 6000, "B")); fr.Frequency["İş"] = 3; fr.Frequency["Yok"] = 2; Add("A09 sıklık", fr);
Add("Türkçe adlar ve büyük/küçük harf", S(null, R(0, 1000, "ılık"), R(0, 1100, "Ilık"), R(0, 900, "İşaret"), R(0, 800, "işaret"), R(0, 700, "Çıkış"), R(0, 600, "Ölçüm"), R(0, 650, "Zeta"), R(0, 500, "Şerit")));

var rnd = new Random(20261115);
string[] names = { "Toplama", "Paketleme", "Tarama", "Etiket", "Çıkış", "Ölçüm", "İade", "Şerit", "Bekleme", "Kontrol" };
double[] conf = { 80, 90, 95, 99, 99.5 }, target = { 2, 5, 10 }, pace = { 80, 100, 110, 125.5 }, allow = { 0, 10, 15, 33.3 };
int[] gms = { 10, 50, 100, 250, 1000 };
for (int k = 0; k < 45; k++)
{
    var st = S(new()
    {
        Confidence = conf[rnd.Next(conf.Length)], TargetError = target[rnd.Next(target.Length)], Pace = pace[rnd.Next(pace.Length)],
        Allowance = allow[rnd.Next(allow.Length)], RepeatedLow = rnd.Next(2) == 0, GroupMs = gms[rnd.Next(gms.Length)], Stable = rnd.Next(2) == 0
    });
    int nSteps = rnd.Next(1, 6), n = rnd.Next(0, 40);
    var basis = Enumerable.Range(0, nSteps).Select(_ => rnd.Next(300, 30000)).ToArray();
    long t = 0;
    for (int i = 0; i < n; i++)
    {
        int si = rnd.Next(nSteps);
        long dur = basis[si] + (rnd.Next(3) == 0 ? rnd.Next(-20, 21) * 10 : rnd.Next(-2000, 2001));
        if (dur < 1) dur = rnd.Next(1, 50);
        long start = rnd.Next(4) == 0 ? Math.Max(0, t - rnd.Next(0, 3000)) : t + rnd.Next(0, 500);
        st.Observations.Add(R(start, start + dur, names[si], rnd.Next(1, 5), rnd.Next(10) == 0, new[] { "", "VA", "SVA", "NVA" }[rnd.Next(4)]));
        t = start + dur;
    }
    foreach (var nm in names.Take(nSteps)) if (rnd.Next(3) == 0) st.Frequency[nm] = rnd.Next(1, 6);
    Add($"rastgele {k + 1}", st);
}

// t tablosu
var tvals = new JsonArray();
foreach (var c in new[] { 80.0, 90, 95, 99, 99.9 })
    foreach (var df in Enumerable.Range(1, 30).Concat(new[] { 40, 60, 120, 200, 1000 }))
        tvals.Add(new JsonObject { ["Confidence"] = c, ["Df"] = df, ["T"] = StudentT.InvCDF(0, 1, df, (1 + c / 100) / 2) });

// Paket kabul/ret senaryoları: sonucu C# StudyTransfer.Import belirler
var baseStudy = S(new() { Pace = 105.5, Allowance = 12, GroupMs = 250 }, R(0, 1000, "Toplama", 1, false, "VA"), R(1000, 2500, "Paketleme", 2, true, ""));
baseStudy.Name = "Örnek etüt"; baseStudy.Code = "K-1"; baseStudy.Video = @"C:\Gizli\v.mp4"; baseStudy.Frequency["Toplama"] = 2;
var tmp = Path.GetTempFileName(); StudyTransfer.Export(baseStudy, tmp);
string b = File.ReadAllText(tmp);
var muts = new List<(string, byte[])>();
void M(string n, string s) => muts.Add((n, Encoding.UTF8.GetBytes(s)));
M("temel paket", b);
M("girintili ve satır sonlu", JsonSerializer.Serialize(JsonNode.Parse(b), new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) }).Replace("\n", "\r\n"));
muts.Add(("UTF-8 BOM", new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(b)).ToArray()));
muts.Add(("geçersiz UTF-8", Encoding.UTF8.GetBytes(b.Replace("Örnek", "XX")).Select((x, i) => x).ToArray().Concat(new byte[0]).ToArray()));
{ var bytes = Encoding.UTF8.GetBytes(b); int idx = Encoding.UTF8.GetBytes(b[..b.IndexOf("Örnek")]).Length; bytes[idx] = 0xFF; muts[^1] = ("geçersiz UTF-8", bytes); }
M("boş", ""); M("yalnız boşluk", "   "); M("JSON değil", "merhaba"); M("dizi", "[]"); M("null", "null"); M("sayı", "5");
M("sonda virgül", b.TrimEnd().TrimEnd('}') + ",}"); M("yorum satırı", "// not\n" + b); M("kesik dosya", b[..(b.Length / 2)]);
M("ek alan (ileri uyum)", b.Replace("{\"Kind\"", "{\"Extra\":{\"a\":[1,2]},\"Kind\""));
M("gözlemde Seconds ve ek alan", b.Replace("\"Step\":\"Toplama\"", "\"Seconds\":9,\"Fazla\":true,\"Step\":\"Toplama\""));
M("Kind küçük harf", b.Replace("\"Kind\"", "\"kind\"")); M("Kind farklı", b.Replace("video-zaman-etudu", "baska")); M("Kind sayı", b.Replace("\"video-zaman-etudu\"", "5"));
M("Version yok", b.Replace("\"Version\":1,", "")); M("Version metin", b.Replace("\"Version\":1", "\"Version\":\"1\"")); M("Version 1.0", b.Replace("\"Version\":1", "\"Version\":1.0"));
M("Version 1e0", b.Replace("\"Version\":1", "\"Version\":1e0")); M("Version 2", b.Replace("\"Version\":1", "\"Version\":2"));
M("Name yok", b.Replace("\"Name\":\"Örnek etüt\",", "")); M("Name boşluk", b.Replace("\"Name\":\"Örnek etüt\"", "\"Name\":\"  \"")); M("Name null", b.Replace("\"Name\":\"Örnek etüt\"", "\"Name\":null"));
M("Name sayı", b.Replace("\"Name\":\"Örnek etüt\"", "\"Name\":5")); M("Code yok", b.Replace("\"Code\":\"K-1\",", ""));
M("VideoName boş", b.Replace("\"VideoName\":\"v.mp4\"", "\"VideoName\":\"\"")); M("VideoName yol", b.Replace("\"VideoName\":\"v.mp4\"", "\"VideoName\":\"C:\\\\a\\\\b\\\\film.mp4\""));
M("VideoName null", b.Replace("\"VideoName\":\"v.mp4\"", "\"VideoName\":null"));
M("Settings boş nesne", System.Text.RegularExpressions.Regex.Replace(b, "\"Settings\":\\{.*?\\}", "\"Settings\":{}"));
M("Settings null", System.Text.RegularExpressions.Regex.Replace(b, "\"Settings\":\\{.*?\\}", "\"Settings\":null"));
M("Pace metin", b.Replace("\"Pace\":105.5", "\"Pace\":\"105.5\"")); M("Pace üslü", b.Replace("\"Pace\":105.5", "\"Pace\":1.055e2"));
M("Pace 0", b.Replace("\"Pace\":105.5", "\"Pace\":0")); M("Pace çok büyük", b.Replace("\"Pace\":105.5", "\"Pace\":1e400"));
M("Allowance 100", b.Replace("\"Allowance\":12", "\"Allowance\":100")); M("Confidence 100", b.Replace("\"Confidence\":95", "\"Confidence\":100"));
M("GroupMs ondalık", b.Replace("\"GroupMs\":250", "\"GroupMs\":250.5")); M("GroupMs 250.0", b.Replace("\"GroupMs\":250", "\"GroupMs\":250.0")); M("GroupMs 0", b.Replace("\"GroupMs\":250", "\"GroupMs\":0"));
M("GroupMs int taşması", b.Replace("\"GroupMs\":250", "\"GroupMs\":3000000000"));
M("RepeatedLow 1", b.Replace("\"RepeatedLow\":false", "\"RepeatedLow\":1")); M("RepeatedLow true", b.Replace("\"RepeatedLow\":false", "\"RepeatedLow\":true"));
M("Stable metin", b.Replace("\"Stable\":false", "\"Stable\":\"false\""));
M("Observations boş", System.Text.RegularExpressions.Regex.Replace(b, "\"Observations\":\\[.*?\\]", "\"Observations\":[]"));
M("Observations null", System.Text.RegularExpressions.Regex.Replace(b, "\"Observations\":\\[.*?\\]", "\"Observations\":null"));
M("Observations nesne", System.Text.RegularExpressions.Regex.Replace(b, "\"Observations\":\\[.*?\\]", "\"Observations\":{}"));
M("ölçüm null", b.Replace("\"Observations\":[", "\"Observations\":[null,")); M("ölçüm sayı", b.Replace("\"Observations\":[", "\"Observations\":[5,"));
M("ölçüm boş nesne", b.Replace("\"Observations\":[", "\"Observations\":[{},"));
M("ölçüm yalnız zaman", b.Replace("\"Observations\":[", "\"Observations\":[{\"StartMs\":3000,\"EndMs\":4000},"));
M("StartMs ondalık", b.Replace("\"StartMs\":1000", "\"StartMs\":1000.5")); M("StartMs 1000.0", b.Replace("\"StartMs\":1000", "\"StartMs\":1000.0"));
M("StartMs üslü", b.Replace("\"StartMs\":1000", "\"StartMs\":1e3")); M("StartMs negatif", b.Replace("\"StartMs\":1000", "\"StartMs\":-1"));
M("StartMs -0", b.Replace("\"StartMs\":0", "\"StartMs\":-0"));
M("EndMs = StartMs", b.Replace("\"EndMs\":1000", "\"EndMs\":0")); M("EndMs TimeSpan sınırı", b.Replace("\"EndMs\":2500", "\"EndMs\":922337203685477"));
M("EndMs TimeSpan üstü", b.Replace("\"EndMs\":2500", "\"EndMs\":922337203685478")); M("EndMs long üstü", b.Replace("\"EndMs\":2500", "\"EndMs\":99999999999999999999"));
M("EndMs 2^53+1", b.Replace("\"EndMs\":2500", "\"EndMs\":9007199254740993"));
M("Step boş", b.Replace("\"Step\":\"Toplama\"", "\"Step\":\"\"")); M("Step null", b.Replace("\"Step\":\"Toplama\"", "\"Step\":null")); M("Step boşluk", b.Replace("\"Step\":\"Toplama\"", "\"Step\":\" \""));
M("Step kaçışlı", b.Replace("\"Step\":\"Toplama\"", "\"Step\":\"\\u0054oplama \\\"A\\\"\""));
M("Classification XYZ", b.Replace("\"Classification\":\"VA\"", "\"Classification\":\"XYZ\"")); M("Classification va", b.Replace("\"Classification\":\"VA\"", "\"Classification\":\"va\""));
M("Classification null", b.Replace("\"Classification\":\"VA\"", "\"Classification\":null"));
M("Excluded metin", b.Replace("\"Excluded\":true", "\"Excluded\":\"true\"")); M("Cycle 0", b.Replace("\"Cycle\":2", "\"Cycle\":0")); M("Cycle 2.5", b.Replace("\"Cycle\":2", "\"Cycle\":2.5"));
M("Cycle null", b.Replace("\"Cycle\":2", "\"Cycle\":null"));
M("Frequency boş", b.Replace("\"Frequency\":{\"Toplama\":2}", "\"Frequency\":{}")); M("Frequency 0", b.Replace("\"Toplama\":2}", "\"Toplama\":0}"));
M("Frequency ondalık", b.Replace("\"Toplama\":2}", "\"Toplama\":2.5}")); M("Frequency boş anahtar", b.Replace("\"Toplama\":2}", "\"\":2}"));
M("Frequency null", b.Replace("\"Frequency\":{\"Toplama\":2}", "\"Frequency\":null")); M("Frequency dizi", b.Replace("\"Frequency\":{\"Toplama\":2}", "\"Frequency\":[]"));
M("Frequency bilinmeyen adım", b.Replace("\"Toplama\":2}", "\"Toplama\":2,\"Yok\":4}"));
M("çift Name (son kazanır)", b.Replace("\"Name\":\"Örnek etüt\"", "\"Name\":\"İlk\",\"Name\":\"Son\""));
M("geçersiz kaçış", b.Replace("\"Name\":\"Örnek etüt\"", "\"Name\":\"a\\qb\""));
M("kontrol karakteri", b.Replace("\"Name\":\"Örnek etüt\"", "\"Name\":\"a\tb\""));
M("kök sonrası çöp", b + " x"); M("kök sonrası boşluk", b + "\r\n  ");
var pk = new JsonArray();
foreach (var (n, bytes) in muts)
{
    var p = Path.GetTempFileName(); File.WriteAllBytes(p, bytes);
    JsonNode? expected = null; bool ok;
    try
    {
        var s = StudyTransfer.Import(p); ok = true;
        expected = new JsonObject
        {
            ["Name"] = s.Name, ["Code"] = s.Code, ["ExpectedVideoName"] = s.ExpectedVideoName, ["Settings"] = JsonSerializer.SerializeToNode(s.Settings),
            ["Observations"] = new JsonArray(s.Observations.Select(o => (JsonNode)new JsonObject { ["StartMs"] = o.StartMs, ["EndMs"] = o.EndMs, ["Step"] = o.Step, ["Classification"] = o.Classification, ["Excluded"] = o.Excluded, ["Cycle"] = o.Cycle }).ToArray()),
            ["Frequency"] = JsonSerializer.SerializeToNode(s.Frequency)
        };
    }
    catch (InvalidDataException) { ok = false; }
    pk.Add(new JsonObject { ["Name"] = n, ["Base64"] = Convert.ToBase64String(bytes), ["Accept"] = ok, ["Expected"] = expected });
    Console.WriteLine($"{(ok ? "KABUL" : "RET  ")} {n}");
}
var root = new JsonObject
{
    ["Aciklama"] = "Masaüstü (C#) ve tarayıcı (JavaScript) sürümlerinin aynı sonucu verdiğini doğrulayan ortak örnekler. Beklenen değerler C# Core tarafından üretildi. Elle düzenlemeyin; Checks/ExtraChecks ve Web/tests bu dosyayı okur.",
    ["Analysis"] = cases, ["TTable"] = tvals, ["Packages"] = pk
};
File.WriteAllText(outPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) }) + "\n");
Console.WriteLine($"Analiz: {cases.Count}, t: {tvals.Count}, paket: {pk.Count}");
