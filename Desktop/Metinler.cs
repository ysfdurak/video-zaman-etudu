using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoZamanEtudu;

/// <summary>
/// Arayüz dili (TR/EN). Metinler Desktop/Metinler.json'dan gelir; o dosya tarayıcı sürümünün sözlüğünden
/// (Web/js/i18n.js) Tools/Metinler/uret.mjs ile üretilir, böylece iki sürüm aynı ifadeleri kullanır.
/// Seçim %LOCALAPPDATA%\VideoZamanEtudu\dil.txt dosyasında saklanır; yoksa Windows dili Türkçeyse Türkçe, değilse İngilizce.
/// </summary>
internal static class L
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts;
    private static readonly Dictionary<string, Dictionary<string, string>> Results;

    public static string Lang { get; private set; } = "tr";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Lang == "tr" ? "tr-TR" : "en-US");
    public static string Other => Lang == "tr" ? "en" : "tr";

    private static string PreferenceFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoZamanEtudu", "dil.txt");

    static L()
    {
        using var stream = typeof(L).Assembly.GetManifestResourceStream("VideoZamanEtudu.Metinler.json")
            ?? throw new InvalidOperationException("Metinler.json bulunamadı.");
        using var document = JsonDocument.Parse(stream);
        Texts = new();
        foreach (var lang in new[] { "tr", "en" })
            Texts[lang] = document.RootElement.GetProperty(lang).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
        Results = document.RootElement.GetProperty("results").EnumerateObject().ToDictionary(
            p => p.Name, p => p.Value.EnumerateObject().ToDictionary(q => q.Name, q => q.Value.GetString() ?? ""));
        Lang = LoadPreference();
    }

    private static string LoadPreference()
    {
        try
        {
            if (File.Exists(PreferenceFile) && File.ReadAllText(PreferenceFile).Trim() is "tr" or "en")
                return File.ReadAllText(PreferenceFile).Trim();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
    }

    public static void SetLang(string lang)
    {
        if (lang is not ("tr" or "en")) return;
        Lang = lang;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferenceFile)!);
            File.WriteAllText(PreferenceFile, lang);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Çeviri; {ad} yer tutucuları değerlerle değişir. Anahtar yoksa Türkçe, o da yoksa anahtarın kendisi.</summary>
    public static string T(string key, params (string Name, object? Value)[] values) => TIn(Lang, key, values);

    public static string TIn(string lang, string key, params (string Name, object? Value)[] values)
    {
        string text = Texts[lang].TryGetValue(key, out var v) ? v : Texts["tr"].TryGetValue(key, out var w) ? w : key;
        foreach (var (name, value) in values)
            text = text.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.GetCultureInfo(lang == "tr" ? "tr-TR" : "en-US")));
        return text;
    }

    /// <summary>Yalın metin: HTML etiketleri çıkarılır (ör. "<b>Enter</b>" → "Enter").</summary>
    public static string Plain(string key, params (string Name, object? Value)[] values) => Strip(T(key, values));
    public static string Strip(string html) => WebUtility(Regex.Replace(html, "<[^>]+>", ""));
    private static string WebUtility(string s) => s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");

    /// <summary>Çekirdeğin Türkçe sonuç metinleri (ör. "Yeterli") → geçerli dil.</summary>
    public static string Result(string? value, string? lang = null) =>
        value != null && Results.TryGetValue(value, out var map) ? map[lang ?? Lang] : value ?? "";

    public static string Num3(double? value) => value is double v ? v.ToString("0.000", Culture) : "";
    public static string Num(double value, int maxDigits = 2) => value.ToString(maxDigits > 0 ? "0." + new string('#', maxDigits) : "0", Culture);

    /// <summary>Yüzde: Türkçede "%12", İngilizcede "12%"; işaretli istenirse +/− eklenir.</summary>
    public static string Pct(double? value, int digits = 0, bool signed = false)
    {
        if (value is not double v) return "";
        double r = Math.Round(v, digits, MidpointRounding.AwayFromZero);
        string n = Math.Abs(r).ToString("N" + digits, Culture);
        string sign = r < 0 ? "-" : signed && r > 0 ? "+" : "";
        return Lang == "tr" ? sign + "%" + n : sign + n + "%";
    }

    public static string Seconds(double? value) => value is double v ? Num3(v) + " " + (Lang == "tr" ? "sn" : "s") : "—";

    public static string Clock(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return t.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) + (Lang == "tr" ? "," : ".") + t.Milliseconds.ToString("000");
    }

    public static string Date(DateTime value) => Lang == "tr" ? value.ToString("dd.MM.yyyy HH:mm", Culture) : value.ToString("yyyy-MM-dd HH:mm", Culture);

    /// <summary>Hata iletisi: çekirdek hata anahtar taşıyorsa geçerli dilde, değilse olduğu gibi.</summary>
    public static string ErrorText(Exception error)
    {
        if (error.Data["key"] is not string key) return error.Message;
        var values = new List<(string, object?)>();
        foreach (System.Collections.DictionaryEntry entry in error.Data)
            if (entry.Key is string k && k.StartsWith("p:"))
            {
                object? value = entry.Value;
                // Ayrıntı başka bir anahtar olabilir (ör. pkg.settings → settings.invalid).
                if (value is string s && Texts["tr"].ContainsKey(s)) value = T(s);
                values.Add((k[2..], value));
            }
        return T(key, values.ToArray());
    }

    public static IReadOnlyCollection<string> Keys(string lang) => Texts[lang].Keys;
}
