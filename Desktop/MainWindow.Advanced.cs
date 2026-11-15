using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VideoZamanEtudu;

/// <summary>
/// v2.1 analiz bölümleri (tarayıcıdaki v1.8 ile aynı): dağılım ve aykırı değerler, hat dengeleme (Yamazumi),
/// standart iş kombinasyon tablosu, iyileştirme fikirleri, adım kartları ve iş örneklemesi sonuçları.
/// </summary>
public partial class MainWindow
{
    private readonly List<FrameworkElement> timeOnly = new();
    private readonly List<FrameworkElement> samplingOnly = new();
    private readonly TextBox sampleError = Ui.Input(70);
    private readonly StackPanel histBox = new();
    private readonly DataGrid outlierGrid = Ui.Table();
    private Button excludeOutliers = null!;
    private readonly TextBlock balanceSummary = Ui.Text("", 13.5, true);
    private readonly StackPanel yamazumiBox = new();
    private readonly StackPanel balanceRows = new();
    private readonly StackPanel workBox = new();
    private readonly DataGrid ideasGrid = Ui.Table();
    private readonly WrapPanel storyPanel = new();
    private readonly StackPanel samplingBody = new();
    private readonly WrapPanel samplingSummary = new();
    private readonly StackPanel samplingChart = new();
    private readonly DataGrid samplingGrid = Ui.Table();

    private static string Sec(double? v) => L.Seconds(v);
    private static string StationLabel(string name) => System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d+$") ? L.T("lb.station", ("n", name)) : name;
    private static string Pct0(double? v, int d = 1) => L.Pct(v, d);
    private static string AxisFormat(double v) => v.ToString(v < 10 ? "0.#" : "0", L.Culture);

    /// <summary>Analiz sayfasına eklenecek v2.1 bölümleri (adım tablosunun altına).</summary>
    private UIElement BuildAdvancedSections()
    {
        Ui.Columns(outlierGrid, (L.T("th.r.step"), "Step", false), (L.T("th.c.cycle"), "Cycle", true), (L.T("th.start"), "Start", true),
            (L.T("th.duration"), "Duration", true), (L.T("th.o.range"), "Range", true));
        outlierGrid.MaxHeight = 220;
        outlierGrid.MouseDoubleClick += (_, _) => Guard(() =>
        {
            if (outlierGrid.SelectedItem is not OutlierRow row) return;
            tabs.SelectedIndex = 1;
            SelectRow(row.Source);
            if (ready) Seek(row.Source.StartMs);
            RefreshObservations();
        });
        excludeOutliers = Btn("btn.excludeOutliers", ExcludeOutliers);
        Ui.Columns(ideasGrid, (L.T("th.r.step"), "Step", false), (L.T("th.class"), "Class", false), (L.T("field.waste"), "Waste", false),
            (L.T("field.idea"), "Idea", false), (L.T("th.r.contribution"), "Contribution", true), (L.T("th.share"), "Share", true));
        ideasGrid.MaxHeight = 240;
        storyPanel.Margin = new Thickness(0, 6, 0, 10);
        return Ui.Stack(
            Ui.Title(L.T("h.dist")), Ui.Hint(L.T("dist.hint")), histBox, outlierGrid, Ui.Row(excludeOutliers),
            Ui.Title(L.T("h.balance")), Ui.Hint(L.T("balance.hint")),
            Ui.Row(Btn("btn.autoBalance", AutoBalanceStations), Btn("btn.clearBalance", ClearStations)),
            balanceSummary, yamazumiBox, Ui.Box(balanceRows),
            Ui.Title(L.T("h.work")), Ui.Hint(L.T("work.hint")), workBox,
            Ui.Title(L.T("h.ideas")), Ui.Hint(L.T("ideas.hint")), ideasGrid,
            Ui.Title(L.T("h.story")), Ui.Hint(L.T("story.hint")), Ui.Row(Btn("btn.loadStory", LoadStory), Btn("btn.printStory", PrintStory)), storyPanel);
    }

    private UIElement BuildSamplingBody()
    {
        Ui.Columns(samplingGrid, (L.T("s.thCategory"), "Category", false), (L.T("th.class"), "Class", false), (L.T("s.thCount"), "Count", true),
            (L.T("th.share"), "Share", true), (L.T("s.thRange"), "Range", true), (L.T("s.thRequired"), "Required", true));
        samplingBody.Children.Add(Ui.Title(L.T("h.results")));
        samplingBody.Children.Add(samplingSummary);
        samplingBody.Children.Add(samplingChart);
        samplingBody.Children.Add(samplingGrid);
        samplingBody.Children.Add(Ui.Help(L.T("s.help.title"), L.T("s.help.body")));
        samplingBody.Visibility = Visibility.Collapsed;
        return samplingBody;
    }

    private double ChartWidth => Math.Clamp((analysisBody.ActualWidth > 0 ? analysisBody.ActualWidth : 1100) - 40, 300, 760);

    private static Border Card(string label, string value, IEnumerable<string>? details = null)
    {
        var stack = Ui.Stack(new TextBlock { Text = label, Foreground = Ui.Soft, FontSize = 12.5 },
            new TextBlock { Text = value, FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = Ui.Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
        foreach (var d in details ?? Array.Empty<string>()) stack.Children.Add(new TextBlock { Text = d, FontSize = 12, Foreground = Ui.Ink, TextWrapping = TextWrapping.Wrap });
        var card = Ui.Box(stack);
        card.Width = 250;
        card.Margin = new Thickness(0, 0, 8, 8);
        return card;
    }

    /// <summary>Bölümleri yeniden çizer (adım kartları verisiyle; kareler istekle).</summary>
    private void RenderAdvanced(Study study, IReadOnlyList<StepResult> rows, StepResult? selected)
    {
        // Dağılım
        histBox.Children.Clear();
        var bins = selected is null ? Array.Empty<HistogramBin>() : Advanced.Histogram(study, selected.Step);
        if (bins.Count > 0)
            histBox.Children.Add(Ui.Box(Ui.Stack(Ui.Text(L.T("chart.hist", ("step", selected!.Step)), 13.5, true),
                Charts.HistogramChart(bins, ChartWidth, L.T("chart.histAxis"), v => L.Num(v)))));
        else histBox.Children.Add(Ui.Hint(L.T("hist.empty")));
        var outs = Advanced.Outliers(study);
        var outRows = outs.SelectMany(g => g.Rows.Select(o => new OutlierRow(o, g.Step, o.Cycle.ToString(), L.Clock(o.StartMs), L.Num3(o.Seconds),
            $"{L.Num3(Math.Max(0, g.Low))} – {L.Num3(g.High)}"))).ToList();
        outlierGrid.ItemsSource = outRows;
        outlierGrid.Visibility = outRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        excludeOutliers.IsEnabled = outRows.Count > 0;
        if (outRows.Count == 0) histBox.Children.Add(Ui.Hint(L.T("outliers.none")));

        // Hat dengeleme
        var b = Advanced.LineBalance(study);
        var parts = new List<string>();
        parts.Add(b.Stations.Count > 0
            ? L.T("balance.summary", ("k", b.Stations.Count), ("b", StationLabel(b.Bottleneck!)), ("max", L.Num3(b.Max)), ("eff", Pct0(b.Efficiency)), ("loss", Pct0(b.Loss)))
            : L.T("balance.none"));
        if (b.MinOperators is int n) parts.Add(L.T("balance.minOps", ("n", n), ("takt", L.Num(study.Takt ?? 0))));
        if (b.OverTakt.Count > 0) parts.Add(L.T("balance.over", ("list", string.Join(", ", b.OverTakt.Select(StationLabel)))));
        balanceSummary.Text = string.Join(" ", parts);
        yamazumiBox.Children.Clear();
        if (b.Stations.Count > 0)
        {
            var bars = b.Stations.Select(s => new StackBar(StationLabel(s.Station), s.Steps.Select(p => new StackPart(p.Step, p.Contribution, p.Classification)).ToList(), s.Load, L.Num3(s.Load))).ToList();
            if (b.Unassigned.Count > 0)
            {
                double load = b.Unassigned.Sum(p => p.Contribution);
                bars.Add(new StackBar(L.T("balance.unassigned"), b.Unassigned.Select(p => new StackPart(p.Step, p.Contribution, p.Classification)).ToList(), load, L.Num3(load)));
            }
            yamazumiBox.Children.Add(Ui.Box(Ui.Stack(Ui.Text(L.T("chart.yamazumi"), 13.5, true),
                Charts.YamazumiChart(bars, ChartWidth, study.Takt, L.T("chart.takt", ("v", Sec(study.Takt))), AxisFormat, c => c.Length > 0 ? c : L.T("summary.noClass")))));
        }
        balanceRows.Children.Clear();
        var head = new Grid();
        foreach (var w in new[] { 3, 1, 1.2, 1.4 }) head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
        void Cell(Grid g, int col, UIElement e) { Grid.SetColumn(e, col); g.Children.Add(e); }
        Cell(head, 0, Ui.Text(L.T("th.r.step"), 12.5, true)); Cell(head, 1, Ui.Text(L.T("th.class"), 12.5, true));
        Cell(head, 2, Ui.Text(L.T("th.r.contribution"), 12.5, true)); Cell(head, 3, Ui.Text(L.T("th.station"), 12.5, true));
        balanceRows.Children.Add(head);
        var defs = study.Steps.GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First());
        foreach (var r in rows.Where(r => r.Contribution is not null))
        {
            var line = new Grid();
            foreach (var w in new[] { 3, 1, 1.2, 1.4 }) line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
            var box = new TextBox { Text = defs.GetValueOrDefault(r.Step)?.Station ?? "", MaxLength = 40, Width = 110, HorizontalAlignment = HorizontalAlignment.Left, MinHeight = 26 };
            string step = r.Step;
            box.LostFocus += (_, _) => Guard(() => SetStation(step, box.Text));
            Cell(line, 0, Ui.Text(r.Step, 13)); Cell(line, 1, Ui.Text(defs.GetValueOrDefault(r.Step)?.Classification ?? "", 13));
            Cell(line, 2, Ui.Text(L.Num3(r.Contribution), 13)); Cell(line, 3, box);
            balanceRows.Children.Add(line);
        }

        // Standart iş
        workBox.Children.Clear();
        var work = Advanced.StandardWork(study);
        if (work.Rows.Count > 0)
            workBox.Children.Add(Ui.Box(Ui.Stack(
                Ui.Text($"{L.T("chart.work")} · {L.T("work.summary", ("op", Sec(work.Operator)), ("total", Sec(work.Total)))}", 13.5, true),
                Charts.WorkChart(work.Rows.Select(r => new GanttRow(r.Step, r.Type, L.T("type." + r.Type), r.Start, r.End, L.Num3(r.Seconds))).ToList(),
                    ChartWidth, study.Takt, L.T("chart.takt", ("v", Sec(study.Takt))), work.Total, AxisFormat),
                Ui.Hint(string.Join("    ", StudyRules.WorkTypes.Select(k => (k switch { "Manual" => "━", "Machine" => "╍", "Walk" => "┅", _ => "┈" }) + " " + L.T("type." + k)))))));
        else workBox.Children.Add(Ui.Hint(L.T("work.empty")));

        // İyileştirme
        var ideas = Advanced.Improvements(study);
        ideasGrid.ItemsSource = ideas.Select(x => new IdeaRow(x.Step, x.Classification, x.Waste.Length > 0 ? L.T("waste." + x.Waste) : "", x.Idea,
            L.Num3(x.Contribution), Pct0(x.Share))).ToList();

        RenderStory(null);
    }

    private void SetStation(string step, string value)
    {
        if (current == null) return;
        var st = current.Steps.FirstOrDefault(x => x.Name == step);
        if (st is null) { st = new StepDef { Name = step }; current.Steps.Add(st); }
        var v = value.Trim();
        v = v.Length > 40 ? v[..40] : v;
        if ((st.Station ?? "") == v) return;
        st.Station = v.Length > 0 ? v : null;
        current.Steps = StudyRules.CleanSteps(current.Steps);
        Save();
        RefreshAnalysis();
    }

    private void AutoBalanceStations()
    {
        if (current == null) return;
        if (!(current.Takt > 0)) throw Error("balance.needTakt");
        current.Steps = StudyRules.DeriveSteps(current).Select(x => new StepDef { Name = x.Name, Classification = x.Classification, Note = x.Note, Type = x.Type, Station = x.Station, Waste = x.Waste, Idea = x.Idea, FrameMs = x.FrameMs }).ToList();
        foreach (var (step, station) in Advanced.AutoBalance(current))
        {
            var st = current.Steps.FirstOrDefault(x => x.Name == step);
            if (st is null) { st = new StepDef { Name = step }; current.Steps.Add(st); }
            st.Station = station;
        }
        current.Steps = StudyRules.CleanSteps(current.Steps);
        Save();
        RefreshAnalysis();
    }

    private void ClearStations()
    {
        if (current == null) return;
        foreach (var st in current.Steps) st.Station = null;
        Save();
        RefreshAnalysis();
    }

    private void ExcludeOutliers()
    {
        if (current == null) return;
        var rows = Advanced.Outliers(current).SelectMany(g => g.Rows).ToList();
        if (rows.Count == 0) return;
        if (!Ask(L.T("msg.excludeOutliersConfirm", ("n", rows.Count)), L.T("title.excludeOutliers"))) return;
        foreach (var o in rows) { o.Excluded = true; o.Note ??= L.T("note.outlier"); }
        Save();
        RefreshObservations();
        RefreshAnalysis();
        analysisStatus.Text = L.T("status.outliersExcluded", ("n", rows.Count));
    }

    // ------------------------------------------------------------ adım kartları

    /// <summary>Adım kartları; frames verilirse kartlarda video karesi gösterilir.</summary>
    private List<Border> StoryCards(Study study, IReadOnlyDictionary<string, BitmapSource>? frames, double width = 250)
    {
        var rows = StudyRules.InProcessOrder(study, Analysis.Steps(study)).ToDictionary(r => r.Step);
        var names = StudyRules.StepOrder(study).Where(n => rows.TryGetValue(n, out var r) && r.Count > 0).ToList();
        var cards = new List<Border>();
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            var def = study.Steps.FirstOrDefault(x => x.Name == name) ?? new StepDef { Name = name };
            var r = rows[name];
            var stack = new StackPanel();
            if (frames != null && frames.TryGetValue(name, out var img))
                stack.Children.Add(new Image { Source = img, Width = width - 24, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 6) });
            else
                stack.Children.Add(new Border { Background = Ui.Brush("#E3EAEE"), Height = (width - 24) * 9 / 16, Margin = new Thickness(0, 0, 0, 6),
                    Child = new TextBlock { Text = Advanced.StepFrame(study, name) is long f ? L.T("story.frame", ("t", L.Clock(f))) : "—", Foreground = Ui.Soft, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            stack.Children.Add(Ui.Text($"{i + 1}. {name}", 15, true));
            stack.Children.Add(Ui.Hint(string.Join(" · ", new[] { def.Classification, def.Type is string t ? L.T("type." + t) : "", r.Standard is double sd ? Sec(sd) : "" }.Where(x => x.Length > 0))));
            if (def.Note != null) stack.Children.Add(Ui.Text(L.T("now.note", ("note", def.Note)), 13));
            if (def.Waste != null) stack.Children.Add(Ui.Text($"{L.T("field.waste")}: {L.T("waste." + def.Waste)}", 13, false, Ui.Brush("#9A3412")));
            if (def.Idea != null) stack.Children.Add(Ui.Text($"{L.T("field.idea")}: {def.Idea}", 13));
            var card = Ui.Box(stack);
            card.Width = width;
            card.Margin = new Thickness(0, 0, 10, 10);
            cards.Add(card);
        }
        return cards;
    }

    private void RenderStory(IReadOnlyDictionary<string, BitmapSource>? frames)
    {
        storyPanel.Children.Clear();
        if (current == null) return;
        var cards = StoryCards(current, frames);
        if (cards.Count == 0) storyPanel.Children.Add(Ui.Hint(L.T("story.empty")));
        foreach (var c in cards) storyPanel.Children.Add(c);
    }

    /// <summary>
    /// Adım kareleri ayrı bir MediaPlayer ile çizilir (oynatıcının konumu değişmez): konuma git, kare gelince
    /// DrawingVisual + RenderTargetBitmap ile yakala.
    /// </summary>
    internal Dictionary<string, BitmapSource> CaptureFrames(Study study, int width = 320)
    {
        if (!File.Exists(study.Video)) throw Error("story.noVideo");
        var result = new Dictionary<string, BitmapSource>();
        var player = new MediaPlayer { ScrubbingEnabled = true, IsMuted = true };
        bool opened = false, failed = false;
        player.MediaOpened += (_, _) => opened = true;
        player.MediaFailed += (_, _) => failed = true;
        player.Open(new Uri(study.Video));
        WaitFor(() => opened || failed, 5000);
        if (!opened) { player.Close(); throw Error("status.videoError"); }
        player.Pause();
        int w = player.NaturalVideoWidth > 0 ? player.NaturalVideoWidth : 16, h = player.NaturalVideoHeight > 0 ? player.NaturalVideoHeight : 9;
        int height = Math.Max(1, (int)Math.Round(width * (double)h / w));
        foreach (var name in StudyRules.StepOrder(study))
        {
            if (Advanced.StepFrame(study, name) is not long at) continue;
            player.Position = TimeSpan.FromMilliseconds(at);
            WaitFor(() => false, 350);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawVideo(player, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            result[name] = bitmap;
        }
        player.Close();
        return result;
    }

    /// <summary>Arayüzü kilitlemeden kısa bekleme (medya olayları işlenir).</summary>
    private static void WaitFor(Func<bool> done, int ms)
    {
        var until = DateTime.Now.AddMilliseconds(ms);
        while (!done() && DateTime.Now < until)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        }
    }

    private Dictionary<string, BitmapSource>? storyFrames;
    private void LoadStory()
    {
        if (current == null) return;
        storyFrames = CaptureFrames(current);
        RenderStory(storyFrames);
    }
    internal int TestStoryImages => storyPanel.Children.OfType<Border>().Count(b => b.Child is StackPanel s && s.Children.OfType<Image>().Any());

    private void PrintStory()
    {
        if (current == null) return;
        try { storyFrames = CaptureFrames(current); } catch (Exception) { storyFrames = null; }
        RenderStory(storyFrames);
        PrintReport.PrintStory(this, current, width => StoryCards(current, storyFrames, width));
    }

    // ------------------------------------------------------------ iş örneklemesi sonuçları

    private void RefreshSampling()
    {
        if (current == null) return;
        var r = Advanced.AnalyzeSampling(current);
        samplingSummary.Children.Clear();
        foreach (var (label, value, details) in SamplingCards(current)) samplingSummary.Children.Add(Card(label, value, details));
        analysisStatus.Text = r.N > 0 ? "" : L.T("s.empty.analysis");
        samplingChart.Children.Clear();
        if (r.N > 0)
            samplingChart.Children.Add(Ui.Box(Ui.Stack(Ui.Text(L.T("s.chart"), 13.5, true), Charts.SamplingChart(SamplingBars(r), ChartWidth))));
        samplingGrid.ItemsSource = r.Rows.Select(x => new SamplingViewRow(x.Category, x.Classification, x.Count.ToString(), Pct0(x.Share),
            x.Low is null ? "" : $"{Pct0(x.Low)} – {Pct0(x.High)}", x.Required?.ToString() ?? "")).ToList();
    }

    private static List<ShareRow> SamplingBars(SamplingResult r) =>
        r.Rows.Select(x => new ShareRow(x.Category, x.Share ?? 0, x.Low ?? 0, x.High ?? 0, x.Classification,
            $"{Pct0(x.Share)} ± {(x.Half ?? 0).ToString("0.0", L.Culture)}")).ToList();

    private static List<(string Label, string Value, IReadOnlyList<string> Details)> SamplingCards(Study study)
    {
        var r = Advanced.AnalyzeSampling(study);
        var cards = new List<(string, string, IReadOnlyList<string>)> { (L.T("s.card.n"), r.N.ToString(), Array.Empty<string>()) };
        if (r.N > 0 && r.Required is int req && r.ByClass != null)
        {
            cards.Add((L.T("s.card.required"), L.T("s.card.requiredValue", ("n", req)),
                new[] { req <= r.N ? L.T("s.card.enough", ("e", L.Num(r.Error))) : L.T("s.card.more", ("k", req - r.N)) }));
            cards.Add((L.T("s.card.classes"), string.Join(" · ", new[] { "VA", "SVA", "NVA", "" }.Where(c => r.ByClass[c] > 0)
                .Select(c => (c.Length > 0 ? c : L.T("summary.noClass")) + " " + L.Pct(r.ByClass[c]))), Array.Empty<string>()));
        }
        return cards;
    }
}
