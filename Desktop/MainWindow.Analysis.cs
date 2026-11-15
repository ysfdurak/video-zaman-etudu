using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace VideoZamanEtudu;

/// <summary>
/// Analiz ve rapor bölümü (tarayıcıdaki v1.5–v1.7 ile aynı): ayarlar yazıldıkça uygulanır, tolerans kalemleri,
/// takt, özet kartları, grafikler, süreç sırasında adım tablosu, adıma özel sıklık ve tempo, çevrim değişkenliği,
/// Excel, yazdır / PDF ve önce / sonra karşılaştırması.
/// </summary>
public partial class MainWindow
{
    private readonly TextBox confidence = Ui.Input(70);
    private readonly TextBox target = Ui.Input(70);
    private readonly TextBox pace = Ui.Input(70);
    private readonly TextBox allowance = Ui.Input(70);
    private readonly TextBox groupMs = Ui.Input(70);
    private readonly TextBox takt = Ui.Input(90);
    private readonly ComboBox method = new() { Width = 250, MinHeight = 28 };
    private readonly CheckBox stable = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 8) };
    private readonly Dictionary<string, TextBox> allowanceParts = new();
    private readonly Expander allowanceBox = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBlock allowanceSum = new() { Foreground = Ui.Soft };
    private readonly TextBlock settingsError = Ui.Text("", 13.5, true, Ui.Danger);
    private readonly TextBlock analysisStatus = Ui.Hint("");
    private readonly Dictionary<string, TextBox> infoBoxes = new();
    private readonly Border emptyState = new();
    private readonly StackPanel analysisBody = new();
    private readonly WrapPanel summary = new();
    private readonly WrapPanel charts = new();
    private readonly DataGrid results = Ui.Table();
    private readonly DataGrid cycles = Ui.Table();
    private readonly DataGrid groups = Ui.Table();
    private readonly TextBlock selStep = new() { FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = Ui.Ink, Margin = new Thickness(0, 2, 16, 8) };
    private readonly TextBox every = Ui.Input(60, "1");
    private readonly TextBox stepPace = Ui.Input(80);
    private Button applyStep = null!;
    private string? selectedResult;
    private bool loadingSettings;

    private UIElement BuildAnalysis()
    {
        method.Items.Add(L.T("method.mean"));
        method.Items.Add(L.T("method.repeated"));
        stable.Content = L.T("check.stable");

        var infoRow = new WrapPanel();
        foreach (var field in StudyRules.InfoFields)
        {
            var box = Ui.Input(field == "Note" ? 320 : 160);
            box.MaxLength = StudyRules.MaxText;
            box.LostFocus += (_, _) => Guard(SaveInfo);
            infoBoxes[field] = box;
            infoRow.Children.Add(Ui.Field(L.T("info." + field), box));
        }
        var info = new Expander { Header = L.T("info.title"), Content = infoRow, Margin = new Thickness(0, 0, 0, 4) };

        foreach (var box in new[] { confidence, target, pace, allowance, groupMs, takt }) box.LostFocus += (_, _) => ApplySettingsInline();
        method.SelectionChanged += (_, _) => { if (!loadingSettings) ApplySettingsInline(); };
        stable.Checked += (_, _) => { if (!loadingSettings) ApplySettingsInline(); };
        stable.Unchecked += (_, _) => { if (!loadingSettings) ApplySettingsInline(); };

        var partsRow = new WrapPanel();
        foreach (var name in StudyRules.AllowancePartNames)
        {
            var box = Ui.Input(70);
            box.LostFocus += (_, _) => ApplySettingsInline();
            box.TextChanged += (_, _) => SyncAllowanceField();
            allowanceParts[name] = box;
            partsRow.Children.Add(Ui.Field(L.T("al." + name), box));
        }
        var partsHead = new StackPanel { Orientation = Orientation.Horizontal };
        partsHead.Children.Add(new TextBlock { Text = L.T("al.title") + " · " });
        partsHead.Children.Add(allowanceSum);
        allowanceBox.Header = partsHead;
        allowanceBox.Content = Ui.Stack(Ui.Hint(L.T("al.hint")), partsRow);

        settingsError.Visibility = Visibility.Collapsed;
        var goMeasure = Btn("btn.goMeasure", () => tabs.SelectedIndex = 1, primary: true);
        goMeasure.HorizontalAlignment = HorizontalAlignment.Left;
        emptyState.Child = Ui.Stack(Ui.Text(L.T("analysis.emptyLead")), goMeasure);
        emptyState.Background = Ui.Paper;
        emptyState.BorderBrush = Ui.Line;
        emptyState.BorderThickness = new Thickness(1);
        emptyState.CornerRadius = new CornerRadius(6);
        emptyState.Padding = new Thickness(16);
        emptyState.Visibility = Visibility.Collapsed;

        Ui.Columns(results, (L.T("th.r.step"), "Step", false), (L.T("th.r.n"), "Count", true), (L.T("th.r.excluded"), "Excluded", true),
            (L.T("th.r.mean"), "Mean", true), (L.T("th.r.median"), "Median", true), (L.T("th.r.min"), "Min", true), (L.T("th.r.max"), "Max", true),
            (L.T("th.r.sd"), "Deviation", true), (L.T("th.r.t"), "CriticalT", true), (L.T("th.r.error"), "ErrorPercent", true),
            (L.T("th.r.sufficiency"), "Sufficiency", false), (L.T("th.r.base"), "Base", true), (L.T("th.r.pace"), "Pace", true),
            (L.T("th.r.normal"), "Normal", true), (L.T("th.r.standard"), "Standard", true), (L.T("th.r.every"), "Every", true),
            (L.T("th.r.contribution"), "Contribution", true), (L.T("th.r.method"), "Method", false));
        Ui.Columns(cycles, (L.T("th.c.cycle"), "Cycle", true), (L.T("th.c.elapsed"), "Elapsed", true), (L.T("th.c.workload"), "Workload", true), (L.T("th.c.diff"), "Diff", true));
        Ui.Columns(groups, (L.T("th.g.mean"), "Seconds", true), (L.T("th.g.count"), "Count", true));
        results.MaxHeight = 380;
        cycles.MaxHeight = 260;
        groups.MaxHeight = 260;
        results.SelectionChanged += (_, _) =>
        {
            if (suppressSelection || results.SelectedItem is not ResultRow row) return;
            selectedResult = row.Source.Step;
            FillStepSettings(row.Source);
            groups.ItemsSource = row.Source.Groups.Select(g => new GroupRow(L.Num3(g.Seconds), g.Count.ToString())).ToList();
            selStep.Text = row.Source.Step;
            applyStep.IsEnabled = true;
        };
        applyStep = Btn("btn.applyStep", ApplyStep);
        var stepSettings = Ui.Box(Ui.Stack(
            Ui.Row(Ui.Field(L.T("sel.label"), selStep), Ui.Field(L.T("field.every"), every),
                Ui.Field(L.T("field.stepPace"), Ui.WithPlaceholder(stepPace, L.T("placeholder.stepPace"))), applyStep),
            Ui.Hint(L.T("sel.hint"))));
        var lower = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cyclesPart = Ui.Stack(Ui.Text(L.T("h.cycles"), 14, true), cycles);
        var groupsPart = Ui.Stack(Ui.Text(L.T("h.groups"), 14, true), groups);
        lower.Children.Add(cyclesPart);
        Grid.SetColumn(groupsPart, 2);
        lower.Children.Add(groupsPart);
        charts.SizeChanged += (_, e) => { if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 40 && current != null) RenderCharts(); };

        analysisBody.Children.Add(Ui.Title(L.T("h.results")));
        analysisBody.Children.Add(summary);
        analysisBody.Children.Add(charts);
        analysisBody.Children.Add(Ui.Help(L.T("help.analysis.title"), L.T("help.analysis.body")));
        analysisBody.Children.Add(Ui.Title(L.T("h.stepTable")));
        analysisBody.Children.Add(stepSettings);
        analysisBody.Children.Add(results);
        analysisBody.Children.Add(lower);

        var page = Ui.Stack(
            info,
            Ui.Title(L.T("h.settings")),
            Ui.Row(Ui.Field(L.T("field.confidence"), confidence), Ui.Field(L.T("field.target"), target), Ui.Field(L.T("field.pace"), pace),
                Ui.Field(L.T("field.allowance"), allowance), Ui.Field(L.T("field.group"), groupMs), Ui.Field(L.T("field.method"), method),
                Ui.Field(L.T("field.takt"), Ui.WithPlaceholder(takt, L.T("placeholder.takt"))), stable),
            allowanceBox,
            settingsError,
            Ui.Row(Btn("btn.excel", Export, primary: true), Btn("btn.print", Print), Btn("btn.tTable", ShowStatistics)),
            analysisStatus,
            emptyState,
            analysisBody);
        page.Margin = new Thickness(12);
        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ------------------------------------------------------------ etüt bilgileri

    private void LoadInfo()
    {
        if (current == null) return;
        foreach (var (field, box) in infoBoxes) box.Text = current.Info.GetValueOrDefault(field, "");
    }

    private void SaveInfo()
    {
        if (current == null) return;
        var info = StudyRules.CleanInfo(infoBoxes.ToDictionary(p => p.Key, p => p.Value.Text));
        if (info.Count == current.Info.Count && info.All(p => current.Info.GetValueOrDefault(p.Key) == p.Value)) return;
        current.Info = info;
        Save();
    }

    // ------------------------------------------------------------ ayarlar

    private void LoadSettings()
    {
        if (current == null) return;
        loadingSettings = true;
        var s = current.Settings;
        confidence.Text = L.Num(s.Confidence, 6);
        target.Text = L.Num(s.TargetError, 6);
        pace.Text = L.Num(s.Pace, 6);
        allowance.Text = L.Num(s.Allowance, 6);
        groupMs.Text = s.GroupMs.ToString();
        method.SelectedIndex = s.RepeatedLow ? 1 : 0;
        stable.IsChecked = s.Stable;
        takt.Text = current.Takt is double t ? L.Num(t, 6) : "";
        var parts = StudyRules.MatchingParts(current.AllowanceParts, s.Allowance);
        foreach (var (name, box) in allowanceParts) box.Text = parts.TryGetValue(name, out var v) ? L.Num(v, 6) : "";
        allowanceBox.IsExpanded = parts.Count > 0;
        settingsError.Visibility = Visibility.Collapsed;
        loadingSettings = false;
        SyncAllowanceField();
    }

    /// <summary>Ayarlar alan değiştikçe uygulanır; geçersiz değer ekranda gösterilir, önceki geçerli ayar kalır.</summary>
    private void ApplySettings()
    {
        if (current == null) return;
        settingsError.Visibility = Visibility.Collapsed;
        var settings = new Settings
        {
            Confidence = Number(confidence.Text, "name.confidence"), TargetError = Number(target.Text, "name.target"),
            Pace = Number(pace.Text, "name.pace"), Allowance = Number(allowance.Text, "name.allowance"),
            GroupMs = PositiveInt(groupMs.Text, "name.group"), RepeatedLow = method.SelectedIndex == 1, Stable = stable.IsChecked == true
        };
        // Kalem girildiyse toplam tolerans kalemlerden gelir.
        var parts = new Dictionary<string, double>();
        foreach (var (name, box) in allowanceParts)
        {
            if (box.Text.Trim().Length == 0) continue;
            double v = Number(box.Text, "al." + name);
            if (v < 0 || v >= 100) throw Error("msg.partInvalid", ("name", L.T("al." + name)));
            if (v > 0) parts[name] = v;
        }
        if (parts.Count > 0) settings.Allowance = StudyRules.AllowanceSum(parts);
        settings.Validate();
        string taktText = takt.Text.Trim();
        double? taktValue = taktText.Length == 0 ? null : StudyRules.CleanTakt(Number(taktText, "name.takt"));
        if (taktText.Length > 0 && taktValue is null) throw Error("msg.taktInvalid");
        current.Settings = settings;
        current.Takt = taktValue;
        current.AllowanceParts = StudyRules.CleanAllowanceParts(parts);
        allowance.Text = L.Num(settings.Allowance, 6);
        SyncAllowanceField();
        Save();
        RefreshAnalysis();
    }

    private void ApplySettingsInline()
    {
        if (loadingSettings || current == null) return;
        try { ApplySettings(); }
        catch (Exception error)
        {
            settingsError.Text = L.ErrorText(error);
            settingsError.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Kalemler doluysa toplam alanı salt okunur olur ve toplamı gösterir.</summary>
    private void SyncAllowanceField()
    {
        bool used = allowanceParts.Values.Any(b => b.Text.Trim().Length > 0);
        allowance.IsReadOnly = used;
        allowance.Background = used ? Ui.Brush("#EEF2F5") : Brushes.White;
        allowance.ToolTip = used ? L.T("al.sumHint") : null;
        double sum = allowanceParts.Values.Sum(b => double.TryParse(b.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0);
        allowanceSum.Text = used ? L.T("al.sum", ("sum", L.Num(Math.Round(sum, 6), 6))) : L.T("al.none");
    }

    private void FillStepSettings(StepResult r)
    {
        every.Text = r.Every.ToString();
        stepPace.Text = current != null && current.StepPace.TryGetValue(r.Step, out var p) ? L.Num(p, 6) : "";
    }

    private void ApplyStep()
    {
        if (current == null || selectedResult is not string step) return;
        current.Frequency[step] = PositiveInt(every.Text, "name.frequency");
        string text = stepPace.Text.Trim();
        if (text.Length == 0) current.StepPace.Remove(step);
        else
        {
            double value = Number(text, "name.stepPace");
            if (!(value > 0 && value <= 300)) throw Error("msg.stepPaceInvalid");
            current.StepPace[step] = value;
        }
        Save();
        RefreshAnalysis();
    }

    // ------------------------------------------------------------ sonuçlar

    private void RefreshAnalysis()
    {
        var study = current;
        if (study == null) return;
        var previous = selectedResult;
        var rows = StudyRules.InProcessOrder(study, Analysis.Steps(study));
        var selected = rows.FirstOrDefault(r => r.Step == previous) ?? rows.FirstOrDefault();
        selectedResult = selected?.Step;
        bool empty = study.Observations.Count == 0;
        emptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        analysisBody.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        selStep.Text = selected?.Step ?? "—";
        applyStep.IsEnabled = selected != null;
        var view = rows.Select(r => new ResultRow(r, r.Step, r.Count.ToString(), r.Excluded.ToString(), L.Num3(r.Mean), L.Num3(r.Median), L.Num3(r.Min), L.Num3(r.Max),
            L.Num3(r.Deviation), L.Num3(r.CriticalT), L.Num3(r.ErrorPercent), L.Result(r.Sufficiency), L.Num3(r.Base),
            L.Num(r.Pace) + (study.StepPace.ContainsKey(r.Step) ? " *" : ""), L.Num3(r.Normal), L.Num3(r.Standard), r.Every.ToString(),
            L.Num3(r.Contribution), L.Result(r.Method))).ToList();
        suppressSelection = true;
        results.ItemsSource = view;
        results.SelectedItem = view.FirstOrDefault(v => v.Source.Step == selectedResult);
        suppressSelection = false;
        if (selected != null && previous != selected.Step) FillStepSettings(selected);
        groups.ItemsSource = selected?.Groups.Select(g => new GroupRow(L.Num3(g.Seconds), g.Count.ToString())).ToList();
        var spread = StudyRules.CycleSpread(Analysis.Cycles(study));
        cycles.ItemsSource = spread.Rows.Select(c => new CycleRow(c.Cycle.ToString(), L.Num3(c.Elapsed), L.Num3(c.Workload), (c.Outlier ? "⚠ " : "") + L.Pct(c.DiffPercent, 0, true))).ToList();
        RenderSummary(study, rows);
        RenderCharts();
        var notes = new List<string> { L.T(study.Settings.Stable ? "guide.stable" : "guide.general") + " " + L.T("guide.tail") };
        if (study.Settings.Allowance == 0 && rows.Count > 0) notes.Add(L.T("guide.noAllowance"));
        analysisStatus.Text = string.Join(" ", notes);
    }

    /// <summary>Özet kartları: ekranda ve yazdırmada aynı içerik.</summary>
    private static List<(string Label, string Value, IReadOnlyList<string> Details)> SummaryCards(Study study, IReadOnlyList<StepResult> rows)
    {
        var cards = new List<(string, string, IReadOnlyList<string>)>();
        if (rows.Count == 0) return cards;
        var (total, missing) = StudyRules.TotalStandard(rows);
        var s = study.Settings;
        var parts = StudyRules.MatchingParts(study.AllowanceParts, s.Allowance);
        string basis = L.T("summary.basis", ("pace", L.Pct(s.Pace)), ("al", L.Pct(s.Allowance, 1))) +
            (parts.Count > 0 ? " (" + string.Join(" + ", StudyRules.AllowancePartNames.Where(parts.ContainsKey).Select(k => PrintReport.PartName(k) + " " + L.Num(parts[k]))) + ")" : "");
        var totalDetails = new List<string>();
        if (missing) totalDetails.Add(L.T("summary.incomplete"));
        totalDetails.Add(basis);
        cards.Add((L.T("summary.total"), L.Seconds(total), totalDetails));
        cards.Add((L.T("summary.rate"), total > 0 ? L.T("summary.rateUnit", ("n", L.Num(3600 / total.Value, 1))) : "—", Array.Empty<string>()));
        if (study.Takt is double takt && total > 0)
        {
            double load = total.Value / takt * 100;
            cards.Add((L.T("summary.takt"), L.T(load <= 100 ? "summary.taktOk" : "summary.taktOver", ("p", L.Pct(load))),
                new[] { L.T("summary.taktDetail", ("std", L.Num3(total)), ("takt", L.Num3(takt))), L.T("summary.operators", ("n", L.Num(total.Value / takt, 2))) }));
        }
        var bad = rows.Where(r => r.Sufficiency == "Yetersiz").ToList();
        int na = rows.Count(r => r.Sufficiency == "Hesaplanamıyor" && r.Count > 0);
        var details = bad.Select(r => L.T("summary.needDetail", ("step", r.Step), ("c", r.Count), ("n", StudyRules.RequiredObservations(r, s)))).ToList();
        if (na > 0) details.Add(L.T("summary.na", ("k", na)));
        cards.Add((L.T("summary.sufficiency"), bad.Count > 0 ? L.T("summary.need", ("k", bad.Count)) : L.T("summary.allOk"), details));
        var spread = StudyRules.CycleSpread(Analysis.Cycles(study));
        if (spread.Rows.Count > 1)
            cards.Add((L.T("summary.cycles"), L.T("summary.cyclesValue", ("n", spread.Rows.Count), ("cv", L.Pct(spread.Variation, 1))),
                spread.Rows.Where(c => c.Outlier).Select(c => L.T("summary.outlier", ("c", c.Cycle), ("d", L.Pct(c.DiffPercent, 0, true)))).ToList()));
        var byClass = new Dictionary<string, long>();
        long all = 0;
        foreach (var o in study.Observations.Where(o => !o.Excluded && o.EndMs > o.StartMs))
        {
            string k = o.Classification.Length > 0 ? o.Classification : L.T("summary.noClass");
            byClass[k] = byClass.GetValueOrDefault(k) + (o.EndMs - o.StartMs);
            all += o.EndMs - o.StartMs;
        }
        if (all > 0)
            cards.Add((L.T("summary.classes"), string.Join(" · ", byClass.OrderByDescending(x => x.Value).Select(x => x.Key + " " + L.Pct((double)x.Value / all * 100))), Array.Empty<string>()));
        return cards;
    }

    private void RenderSummary(Study study, IReadOnlyList<StepResult> rows)
    {
        summary.Children.Clear();
        foreach (var (label, value, details) in SummaryCards(study, rows))
        {
            var stack = Ui.Stack(new TextBlock { Text = label, Foreground = Ui.Soft, FontSize = 12.5 },
                new TextBlock { Text = value, FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = Ui.Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
            foreach (var d in details) stack.Children.Add(new TextBlock { Text = d, FontSize = 12, Foreground = Ui.Ink, TextWrapping = TextWrapping.Wrap });
            var card = Ui.Box(stack);
            card.Width = 250;
            card.Margin = new Thickness(0, 0, 8, 8);
            summary.Children.Add(card);
        }
    }

    /// <summary>Grafik öğeleri: genişlik verilir (ekranda kutuya, yazdırmada sayfaya göre).</summary>
    private List<UIElement> ChartElements(double width, bool framed)
    {
        var list = new List<UIElement>();
        var study = current;
        if (study == null) return list;
        var rows = StudyRules.InProcessOrder(study, Analysis.Steps(study));
        var spread = StudyRules.CycleSpread(Analysis.Cycles(study));
        string unit = L.Lang == "tr" ? "sn" : "s";
        var defs = study.Steps.GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Classification);
        string ClassOf(string step) => defs.TryGetValue(step, out var c) ? c :
            study.Observations.Where(o => o.Step == step && o.Classification != "").GroupBy(o => o.Classification).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "";
        var stepData = rows.Where(r => r.Contribution is not null)
            .Select(r => new StepBar(r.Step, r.Contribution!.Value, ClassOf(r.Step), L.Num3(r.Contribution) + " " + unit)).ToList();
        UIElement Frame(string title, UIElement chart) => framed
            ? Ui.Box(Ui.Stack(Ui.Text(title, 13.5, true), chart))
            : Ui.Stack(Ui.Text(title, 12, true), chart);
        if (stepData.Count > 0)
            list.Add(Frame(L.T("chart.steps"), Charts.StepChart(stepData, width, c => c.Length > 0 ? c : L.T("summary.noClass"))));
        if (spread.Rows.Count > 1)
            list.Add(Frame(L.T("chart.cycles"), Charts.CycleChart(
                spread.Rows.Select(c => new CycleBar(c.Cycle.ToString(), c.Elapsed, c.Outlier, L.Num3(c.Elapsed) + " " + unit)).ToList(),
                width, spread.Mean, L.T("chart.mean", ("v", L.Num3(spread.Mean) + " " + unit)), L.T("th.c.cycle"),
                v => v.ToString(v < 10 ? "0.#" : "0", L.Culture))));
        return list;
    }

    private void RenderCharts()
    {
        charts.Children.Clear();
        double available = charts.ActualWidth > 0 ? charts.ActualWidth : 1100;
        // İki grafik yan yana sığarsa yan yana; değilse alt alta.
        double width = Math.Clamp(available >= 1000 ? available / 2 - 44 : available - 36, 300, 640);
        foreach (var chart in ChartElements(width, framed: true))
        {
            if (chart is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 10, 10);
            charts.Children.Add(chart);
        }
    }

    // ------------------------------------------------------------ çıktılar

    private void ShowStatistics()
    {
        if (current == null) return;
        var s = current.Settings;
        var dfs = Enumerable.Range(1, 30).Concat(new[] { 40, 60, 120 });
        var values = dfs.Select(df => $"{df,4}    {MathNet.Numerics.Distributions.StudentT.InvCDF(0, 1, df, (1 + s.Confidence / 100) / 2).ToString("0.0000", L.Culture)}");
        var window = new Window { Title = L.T("tTable.title", ("c", L.Num(s.Confidence))), Width = 490, Height = 620, Owner = this };
        window.Content = new TextBox
        {
            IsReadOnly = true, Margin = new Thickness(12), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"), Text = L.T("tTable.formula") + "\n\n" + L.T("tTable.header") + "\n" + string.Join("\n", values)
        };
        window.ShowDialog();
    }

    private void Export()
    {
        if (current == null) return;
        ApplySettings();
        SaveInfo();
        var dialog = new SaveFileDialog
        {
            FileName = StudyTransfer.SuggestedFileName(current, ".xlsx", L.T("file.analysisSuffix")), Filter = $"{L.T("d.filter.excel")}|*.xlsx",
            DefaultExt = ".xlsx", AddExtension = true
        };
        if (dialog.ShowDialog(this) == true) StudyExport.Write(current, dialog.FileName, L.Lang);
    }

    private void Print()
    {
        if (current == null) return;
        ApplySettings();
        SaveInfo();
        var rows = StudyRules.InProcessOrder(current, Analysis.Steps(current));
        PrintReport.Print(this, current, SummaryCards(current, rows), width => ChartElements(Math.Min(width, 640), framed: false));
    }

    // ------------------------------------------------------------ önce / sonra

    private void ShowCompare()
    {
        if (studyList.Count < 2) throw Error("msg.compareNeedTwo");
        var before = new ComboBox { MinWidth = 300, MinHeight = 28 };
        var after = new ComboBox { MinWidth = 300, MinHeight = 28 };
        foreach (var s in studyList) { before.Items.Add($"{s.Code} · {s.Name}"); after.Items.Add($"{s.Code} · {s.Name}"); }
        int first = Math.Max(0, studyList.FindIndex(s => s.Id == selectedStudyId));
        before.SelectedIndex = first;
        after.SelectedIndex = first == 0 ? 1 : 0;
        var summaryText = Ui.Text("", 14, true);
        var table = Ui.Table();
        Ui.Columns(table, (L.T("th.r.step"), "Step", false), (L.T("compare.thBefore"), "Before", true), (L.T("compare.thAfter"), "After", true),
            (L.T("compare.thDiff"), "Diff", true), (L.T("compare.thDiffPct"), "DiffPercent", true));
        table.MinHeight = 200;
        void Render()
        {
            if (before.SelectedIndex < 0 || after.SelectedIndex < 0) return;
            var c = StudyRules.Compare(studyList[before.SelectedIndex], studyList[after.SelectedIndex]);
            table.ItemsSource = c.Rows.Select(r => new CompareViewRow(r.Step, L.Num3(r.Before), L.Num3(r.After),
                r.Diff is double d ? (d > 0 ? "+" : "") + L.Num3(d) : "", L.Pct(r.DiffPercent, 1, true))).ToList();
            string Rate(double? x) => x > 0 ? L.Num(3600 / x.Value, 1) : "—";
            summaryText.Text = c.Before is not null && c.After is not null
                ? L.T("compare.result", ("before", L.Seconds(c.Before)), ("after", L.Seconds(c.After)), ("d", L.Pct(c.DiffPercent, 1, true)), ("rb", Rate(c.Before)), ("ra", Rate(c.After)))
                : L.T("compare.incomplete");
        }
        before.SelectionChanged += (_, _) => Guard(Render);
        after.SelectionChanged += (_, _) => Guard(Render);
        var close = new Button { Content = L.T("dialog.close"), IsCancel = true, MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right };
        var body = new DockPanel { Margin = new Thickness(16) };
        var head = Ui.Stack(Ui.Row(Ui.Field(L.T("compare.before"), before), Ui.Field(L.T("compare.after"), after)), summaryText);
        var foot = Ui.Stack(Ui.Hint(L.T("compare.hint")), close);
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(foot, Dock.Bottom);
        body.Children.Add(head);
        body.Children.Add(foot);
        body.Children.Add(table);
        var window = new Window
        {
            Title = L.T("compare.title"), Owner = this, Width = 820, Height = 600, Content = body,
            Background = Ui.Brush("#F0F4F8"), WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        close.Click += (_, _) => window.Close();
        Guard(Render);
        window.ShowDialog();
    }
}
