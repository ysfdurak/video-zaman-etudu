using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClosedXML.Excel;

namespace VideoZamanEtudu;

/// <summary>
/// Öz-deneme (GitHub Actions'ta Windows'ta çalışır): pencereyi iki dilde kurar, demo etüdünü açar,
/// adım, ayar, tolerans kalemi, takt, adım temposu, süzgeç, Excel, yedek ve karşılaştırma yollarını çalıştırır
/// ve her bölümün ekran görüntüsünü PNG olarak kaydeder. Yerel kayıt geçici bir klasördedir; kullanıcı verisine dokunmaz.
/// </summary>
internal static class SelfTest
{
    private static readonly List<string> Log = new();
    private static int fail;

    public static int Run(string demoDirectory, string outDirectory)
    {
        Directory.CreateDirectory(outDirectory);
        string store = Path.Combine(Path.GetTempPath(), "vz-oz-deneme-" + Guid.NewGuid().ToString("N"));
        MainWindow.TestStoreDirectory = store;
        MainWindow.TestDemoDirectory = Path.GetFullPath(demoDirectory);
        MainWindow.TestLog = text => Log.Add("  " + text);
        try
        {
            foreach (var lang in new[] { "tr", "en" }) Language(lang, outDirectory);
        }
        catch (Exception error)
        {
            Fail("beklenmeyen hata: " + error);
        }
        finally
        {
            File.WriteAllLines(Path.Combine(outDirectory, "oz-deneme.txt"), Log);
            try { Directory.Delete(store, true); } catch (IOException) { }
        }
        Console.WriteLine(string.Join(Environment.NewLine, Log));
        return fail == 0 ? 0 : 1;
    }

    private static void Check(string name, bool ok, string detail = "")
    {
        Log.Add((ok ? "GEÇTİ " : "KALDI ") + name + (detail.Length > 0 ? $"  [{detail}]" : ""));
        if (!ok) fail++;
    }
    private static void Fail(string text) { Log.Add("KALDI " + text); fail++; }

    private static void Pump(int ms = 300)
    {
        var until = DateTime.Now.AddMilliseconds(ms);
        do
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        } while (DateTime.Now < until);
    }

    private static void Shot(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        int w = (int)Math.Ceiling(element.ActualWidth), h = (int)Math.Ceiling(element.ActualHeight);
        if (w <= 0 || h <= 0) { Fail("ekran görüntüsü boş: " + Path.GetFileName(path)); return; }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8)), null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, w, h));
        }
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>Kaydırılan sayfanın tamamı: ScrollViewer içeriği tam boyutuyla çizilir.</summary>
    private static FrameworkElement Full(FrameworkElement tab) =>
        tab is System.Windows.Controls.ScrollViewer { Content: FrameworkElement content } ? content : tab;

    private static void Language(string lang, string outDirectory)
    {
        Log.Add($"== {lang} ==");
        L.Use(lang);
        var window = new MainWindow { Width = 1440, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
        window.Show();
        Pump(600);
        Check("pencere açılır, başlık dilde", window.Title == L.T("app.title"), window.Title);
        Shot((FrameworkElement)window.Content, Path.Combine(outDirectory, $"{lang}-0-calismalar.png"));

        window.TestOpenDemo();
        for (int i = 0; i < 40 && !window.TestReady; i++) Pump(150);
        Pump(400);
        var study = window.TestCurrent;
        Check("demo etüdü açılır", study is { Code: "DEMO-01" }, study?.Name ?? "yok");
        if (study is null) { window.Close(); return; }
        Check("adım listesi ölçümlerden çıkar (6 adım)", window.TestStepChips == 6, window.TestStepChips.ToString());
        Check("kaldığı yerden: sıradaki kayıt ilk adım, çevrim 4", window.TestNow == L.T("now.value", ("step", study.Steps[0].Name), ("cycle", 4)), window.TestNow);
        Log.Add("  video hazır: " + window.TestReady + " · durum: " + window.TestStatus);
        if (window.TestReady)
            Check("devam iletisi", window.TestStatus.Contains(L.Clock(35000)), window.TestStatus);
        Check("kayıt tablosu 14 satır", window.TestObservationRows == 14, window.TestObservationRows.ToString());
        window.TestFilterStep(study.Steps[1].Name);
        Pump();
        Check("süzgeç: yalnız seçili adım", window.TestObservationRows == 3, window.TestObservationRows.ToString());
        window.TestFilterStep(L.T("filter.all"));
        window.TestSelectFirstRow();
        Pump();
        Check("kayıt seçilince düzenleyici açılır", window.TestRowEditorVisible);
        window.TestSelectStep(2);
        Check("adım seçimi", window.TestNow.StartsWith(study.Steps[2].Name), window.TestNow);
        window.TestAddStep(lang == "tr" ? "Forklift bekleme" : "Forklift wait");
        Check("yeni adım eklenir ve seçilir", window.TestStepChips == 7 && window.TestNow.StartsWith(lang == "tr" ? "Forklift" : "Forklift"), window.TestNow);
        Shot((FrameworkElement)window.Content, Path.Combine(outDirectory, $"{lang}-1-olcum.png"));

        window.TestSelectTab(2);
        Pump(500);
        window.TestSettings(5, 4, 20);
        Pump(300);
        Check("tolerans kalemleri toplanır", study.Settings.Allowance == 9 && study.AllowanceParts.Count == 2, window.TestAllowance + " " + window.TestSettingsError);
        Check("takt kaydedilir", study.Takt == 20);
        window.TestStepPace(study.Steps[1].Name, 95);
        Check("adım temposu kaydedilir", study.StepPace.TryGetValue(study.Steps[1].Name, out var sp) && sp == 95);
        Pump(300);
        Check("adım tablosu 6 satır", window.TestResultRows == 6, window.TestResultRows.ToString());
        Check("özet kartları (standart, kapasite, takt, yeterlilik, çevrim, dağılım)", window.TestSummaryCards == 6, window.TestSummaryCards.ToString());
        Check("iki grafik", window.TestCharts == 2, window.TestCharts.ToString());
        var cards = window.TestCards();
        Check("standart süre kartında tolerans dökümü", cards.Count > 0 && cards[0].Details.Any(d => d.Contains(PrintReport.PartName("Personal") + " 5")), string.Join(" | ", cards.FirstOrDefault().Details ?? Array.Empty<string>()));
        Shot(Full(window.TestTab(2)), Path.Combine(outDirectory, $"{lang}-2-analiz.png"));

        // v2.1: kayıt temposu, tür/israf/fikir, otomatik dengeleme, standart iş, adım kartları
        window.TestRateRow(1, 105);
        window.TestStepField(1, "Type", "Walk");
        window.TestStepField(1, "Waste", "Motion");
        window.TestStepField(1, "Idea", lang == "tr" ? "rafı yaklaştır" : "move the shelf closer");
        window.TestStepField(2, "Type", "Machine");
        Check("v2.1 tür, israf, fikir kaydedilir", study.Steps[1] is { Type: "Walk", Waste: "Motion", Idea: not null } && study.Steps[2].Type == "Machine");
        window.TestSelectTab(2);
        Pump(300);
        window.TestAutoBalance();
        Pump(300);
        Check("v2.1 otomatik dengeleme: istasyonlar ve Yamazumi", study.Steps.Count(x => x.Station != null) >= 4 && window.TestYamazumi == 1, window.TestBalanceSummary);
        Check("v2.1 standart iş tablosu çizilir", window.TestWork == 1);
        Check("v2.1 iyileştirme listesi (fikir + NVA)", window.TestIdeas == 2, window.TestIdeas.ToString());
        Check("v2.1 kayıt temposu tabloya yansır", Analysis.Steps(study).First(r => r.Step == study.Steps[1].Name).Rated == 1);
        window.TestLoadStory();
        Pump(500);
        Check("v2.1 adım kartlarında video kareleri", window.TestStoryImages >= 5, window.TestStoryImages.ToString());
        Pump(300);
        Shot(Full(window.TestTab(2)), Path.Combine(outDirectory, $"{lang}-4-ileri.png"));

        string xlsx = Path.Combine(outDirectory, $"{lang}-analiz.xlsx");
        StudyExport.Write(study, xlsx, lang);
        using (var book = new XLWorkbook(xlsx))
        {
            var names = book.Worksheets.Select(s => s.Name).ToList();
            Check("Excel: ilk sayfa Özet, v2.1 sayfaları sonda", names.Count == 9 && names[0] == L.TIn(lang, "x.sheet.summary") && names.Contains(L.TIn(lang, "x.sheet.balance")), string.Join(", ", names));
            Check("Excel ham sayfada tempo sütunu", book.Worksheet(2).Row(1).CellsUsed().Any(c => c.GetString() == L.TIn(lang, "x.rating")));
            var sheet = book.Worksheet(1);
            var labels = sheet.Column(1).CellsUsed().Select(c => c.GetString()).ToList();
            Check("Excel özetinde tolerans kalemi ve takt", labels.Any(l => l.StartsWith(L.TIn(lang, "x.o.al"))) && labels.Contains(L.TIn(lang, "x.o.takt")), labels.Count.ToString());
            Check("Excel ham sayfa başlığı dilde", book.Worksheet(2).Cell(1, 2).GetString() == L.TIn(lang, "x.raw").Split('|')[1]);
        }

        string zip = Path.Combine(outDirectory, $"{lang}-yedek.zip");
        var backup = StudyBackup.Write(window.TestStudies, zip);
        var plan = StudyBackup.Read(zip, window.TestStudies);
        Check("yedek: kayıtlı etütler ikinci kez eklenmez", backup.Count == window.TestStudies.Count && plan.Add.Count == 0 && plan.Same == backup.Count,
            $"{backup.Count}/{plan.Same}/{plan.Add.Count}");
        var plain = StudyTransfer.Import(Path.Combine(MainWindow.TestDemoDirectory!, lang == "tr" ? "sentetik-depo-etudu.vzetud" : "synthetic-warehouse-study.vzetud"));
        var cmp = StudyRules.Compare(plain, study);
        Check("önce/sonra: tolerans ve takt değişince fark hesaplanır", cmp.Diff is double d && d != 0, L.Num3(cmp.Diff));

        // v2.1 iş örneklemesi
        var sampling = new Study
        {
            Name = lang == "tr" ? "Forklift kullanımı" : "Forklift use", Code = "IO-01", StudyType = "sampling",
            Video = Path.Combine(MainWindow.TestDemoDirectory!, lang == "tr" ? "sentetik-depo-etudu.mp4" : "synthetic-warehouse-study.mp4"),
            Categories = StudyRules.CleanCategories(new[] { new SampleCategory { Name = lang == "tr" ? "Yük taşıyor" : "Carrying", Classification = "VA" },
                new SampleCategory { Name = lang == "tr" ? "Boş gidiyor" : "Empty travel", Classification = "NVA" }, new SampleCategory { Name = lang == "tr" ? "Bekliyor" : "Waiting", Classification = "NVA" } })
        };
        window.TestOpenStudy(sampling);
        for (int i = 0; i < 40 && !window.TestReady; i++) Pump(150);
        Check("örnekleme paneli açılır", window.TestSamplingVisible && window.TestStatus == L.T("s.ready"), window.TestStatus);
        window.TestNextMoment(2); window.TestRecordSample(0);
        window.TestNextMoment(5); window.TestRecordSample(2);
        window.TestNextMoment(5); window.TestRecordSample(0);
        window.TestNextMoment(5); window.TestRecordSample(1);
        window.TestUndoSample();
        window.TestRandomMoment();
        Check("örnekleme: 3 gözlem (biri geri alındı)", window.TestCurrent?.Samples.Count == 3, window.TestCurrent?.Samples.Count.ToString() ?? "");
        Pump(300);
        Shot((FrameworkElement)window.Content, Path.Combine(outDirectory, $"{lang}-5-ornekleme.png"));
        window.TestSelectTab(2);
        Pump(400);
        Check("örnekleme analizi: 3 kategori, kartlar", window.TestSamplingRows == 3 && window.TestSamplingCards == 3, $"{window.TestSamplingRows}/{window.TestSamplingCards}");
        Shot(Full(window.TestTab(2)), Path.Combine(outDirectory, $"{lang}-6-ornekleme-analiz.png"));
        string sx = Path.Combine(outDirectory, $"{lang}-ornekleme.xlsx");
        StudyExport.Write(window.TestCurrent!, sx, lang);
        using (var book = new XLWorkbook(sx))
            Check("örnekleme Excel’i: 4 sayfa", book.Worksheets.Count == 4 && book.Worksheet(2).Name == L.TIn(lang, "x.sheet.sampling"));

        window.TestSelectTab(0);
        Pump(300);
        Shot((FrameworkElement)window.Content, Path.Combine(outDirectory, $"{lang}-3-liste.png"));
        window.Close();
        Pump(200);
    }
}
