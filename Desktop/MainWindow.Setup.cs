using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoZamanEtudu;

/// <summary>
/// v2.2 sade düzen (tarayıcıdaki v1.9 ile aynı akış): 1 Çalışmalar · 2 Hazırlık · 3 Ölçüm · 4 Analiz · 5 İyileştirme.
/// Bu dosya: Hazırlık sekmesi (adım tablosu, etüdün kapsamı, etüt bilgileri), adımları başka etütten alma,
/// etüt durumu ve onay, Çıktı al menüsü, toplu kayıt düzenleme, İyileştirme sekmesi ve ilk kullanım turu.
/// </summary>
public partial class MainWindow
{
    private const int TabStudies = 0, TabSetup = 1, TabMeasure = 2, TabAnalysis = 3, TabImprove = 4;

    // Hazırlık
    private readonly Grid stepTable = new() { Margin = new Thickness(0, 4, 0, 6) };
    private readonly TextBox setupNewStep = Ui.Input(300);
    private readonly ComboBox setupNewStepClass = Ui.ClassCombo();
    private readonly Dictionary<string, CheckBox> moduleBoxes = new();
    private readonly Dictionary<string, CheckBox> newModuleBoxes = new();
    private readonly ComboBox newStepsFrom = new() { MinWidth = 300, MinHeight = 28 };
    private bool loadingModules;

    // Durum ve onay
    private readonly ComboBox studyStatus = new() { Width = 150, MinHeight = 28 };
    private readonly TextBox approvedBy = Ui.Input(180);
    private readonly DatePicker approvedAt = new() { Width = 150, MinHeight = 28 };
    private readonly List<FrameworkElement> approvalFields = new();
    private readonly TextBlock approvedNote = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
    private Border approvedNoteBox = null!;
    private bool loadingStatus;

    // Çıktı al menüsü
    private readonly ContextMenu outputMenu = new();
    private MenuItem printStoryItem = null!, tTableItem = null!;

    // Toplu düzenleme
    private readonly Border bulkEditor = new();
    private readonly TextBlock bulkTitle = Ui.Text("", 14, true);
    private readonly TextBox bulkStep = Ui.Input(180);
    private readonly TextBox bulkCycle = Ui.Input(55);
    private readonly TextBox bulkRating = Ui.Input(60);
    private ComboBox bulkClass = null!, bulkExcluded = null!;
    private readonly HashSet<Observation> bulkSelection = new();

    // Analiz alt sekmeleri ve İyileştirme sekmesi
    private readonly TabControl analysisTabs = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly TabControl improveTabs = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Dictionary<string, TabItem> moduleTabs = new();
    private readonly Border improveEmpty = new();
    private readonly TextBlock settingsSummary = new() { Foreground = Ui.Soft };
    private Button frameButton = null!;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------ Hazırlık sekmesi

    private UIElement BuildSetup()
    {
        setupNewStep.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Guard(() => AddStepFrom(setupNewStep, setupNewStepClass)); } };
        var modules = new WrapPanel();
        foreach (var m in StudyRules.ModuleNames)
        {
            var box = new CheckBox { Content = L.T("mod." + m), Margin = new Thickness(0, 2, 18, 4) };
            box.Checked += (_, _) => { if (!loadingModules) Guard(SaveModules); };
            box.Unchecked += (_, _) => { if (!loadingModules) Guard(SaveModules); };
            moduleBoxes[m] = box;
            modules.Children.Add(box);
        }
        var infoRow = new WrapPanel();
        foreach (var field in StudyRules.InfoFields)
        {
            var box = Ui.Input(field == "Note" ? 320 : 160);
            box.MaxLength = StudyRules.MaxText;
            box.LostFocus += (_, _) => Guard(SaveInfo);
            infoBoxes[field] = box;
            infoRow.Children.Add(Ui.Field(L.T("info." + field), box));
        }
        var addRow = Ui.Row(Ui.Field(L.T("field.newStep"), Ui.WithPlaceholder(setupNewStep, L.T("placeholder.newStep"))),
            Ui.Field(L.T("field.class"), setupNewStepClass),
            Btn("btn.addStep", () => { AddStepFrom(setupNewStep, setupNewStepClass); setupNewStep.Focus(); }),
            Gap(Btn("btn.copySteps", ShowCopySteps)));
        var timePart = Ui.Stack(
            Ui.Title(L.T("h.steps")), Ui.Hint(L.T("setup.stepsHint")),
            new ScrollViewer { Content = stepTable, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled },
            addRow,
            Ui.Title(L.T("h.modules")),
            Ui.Box(Ui.Stack(Ui.Hint(L.T("mod.legend")), modules, Ui.Hint(L.T("mod.hint")))));
        timeOnly.Add(timePart);
        var samplingHint = Ui.Hint(L.T("setup.samplingHint"));
        samplingOnly.Add(samplingHint);
        samplingHint.Visibility = Visibility.Collapsed;
        var next = Btn("btn.goMeasureNext", () => tabs.SelectedIndex = TabMeasure, primary: true);
        next.HorizontalAlignment = HorizontalAlignment.Left;
        var page = Ui.Stack(Ui.Text(L.T("setup.lead")), timePart, samplingHint, Ui.Title(L.T("info.title")), infoRow, next);
        page.Margin = new Thickness(12);
        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static FrameworkElement Gap(FrameworkElement e) { e.Margin = new Thickness(14, 0, 6, 4); return e; }

    /// <summary>Adım tablosu: her satırda ad, sınıf, (tür), bitiş anı, (israf, fikir), ölçüm sayısı, taşı/sil.</summary>
    private void RenderStepTable()
    {
        stepTable.Children.Clear();
        stepTable.RowDefinitions.Clear();
        stepTable.ColumnDefinitions.Clear();
        if (current == null || current.IsSampling) return;
        bool work = current.HasModule("Work"), ideas = current.HasModule("Ideas");
        var heads = new List<(string Text, double Width)> { ("#", 34), (L.T("th.r.step"), 200), (L.T("th.class"), 96) };
        if (work) heads.Add((L.T("field.workType"), 130));
        heads.Add((L.T("field.stepNote"), 260));
        if (ideas) { heads.Add((L.T("field.waste"), 150)); heads.Add((L.T("field.idea"), 230)); }
        heads.Add((L.T("th.count"), 64));
        heads.Add((L.T("setup.thActions"), 120));
        foreach (var h in heads) stepTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(h.Width) });
        void Put(UIElement e, int row, int col)
        {
            Grid.SetRow(e, row); Grid.SetColumn(e, col);
            if (e is FrameworkElement fe) { fe.Margin = new Thickness(0, 0, 6, 4); fe.VerticalAlignment = VerticalAlignment.Center; }
            stepTable.Children.Add(e);
        }
        stepTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < heads.Count; c++) Put(new TextBlock { Text = heads[c].Text, FontWeight = FontWeights.SemiBold, Foreground = Ui.Soft, FontSize = 12.5 }, 0, c);
        var list = StepList;
        if (list.Count == 0)
        {
            stepTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var empty = Ui.Hint(L.T("steps.empty"));
            Put(empty, 1, 0);
            Grid.SetColumnSpan(empty, heads.Count);
            return;
        }
        var counts = current.Observations.GroupBy(o => o.Step).ToDictionary(g => g.Key, g => g.Count());
        for (int i = 0; i < list.Count; i++)
        {
            var st = list[i];
            int index = i, row = i + 1, col = 0;
            stepTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Put(new TextBlock { Text = (i + 1).ToString(), Foreground = Ui.Soft }, row, col++);
            var name = new TextBox { Text = st.Name, MaxLength = StudyRules.MaxStepName, MinHeight = 28, VerticalContentAlignment = VerticalAlignment.Center };
            AutomationName(name, $"{L.T("th.r.step")} {i + 1}");
            name.LostFocus += (_, _) => Guard(() => RenameStepAt(index, name));
            Put(name, row, col++);
            var cls = Ui.ClassCombo(90);
            Ui.SetClass(cls, st.Classification);
            AutomationName(cls, $"{L.T("field.class")} · {st.Name}");
            cls.SelectionChanged += (_, _) => Guard(() => SetStepClassAt(index, Ui.ClassOf(cls)));
            Put(cls, row, col++);
            if (work)
            {
                var type = Ui.TagCombo(StudyRules.WorkTypes.Select(t => (t, L.T("type." + t))), 124);
                Ui.SetTag(type, st.Type);
                AutomationName(type, $"{L.T("field.workType")} · {st.Name}");
                type.SelectionChanged += (_, _) => Guard(() => SetStepFieldAt(index, "Type", Ui.TagOf(type)));
                Put(type, row, col++);
            }
            var note = new TextBox { Text = st.Note ?? "", MaxLength = StudyRules.MaxText, MinHeight = 28, VerticalContentAlignment = VerticalAlignment.Center };
            AutomationName(note, $"{L.T("field.stepNote")} · {st.Name}");
            note.LostFocus += (_, _) => Guard(() => SetStepFieldAt(index, "Note", note.Text));
            Put(i == 0 ? Ui.WithPlaceholder(note, L.T("placeholder.stepNote")) : note, row, col++);
            if (ideas)
            {
                var waste = Ui.TagCombo(StudyRules.Wastes.Select(w => (w, L.T("waste." + w))), 144);
                Ui.SetTag(waste, st.Waste);
                AutomationName(waste, $"{L.T("field.waste")} · {st.Name}");
                waste.SelectionChanged += (_, _) => Guard(() => SetStepFieldAt(index, "Waste", Ui.TagOf(waste)));
                Put(waste, row, col++);
                var idea = new TextBox { Text = st.Idea ?? "", MaxLength = StudyRules.MaxText, MinHeight = 28, VerticalContentAlignment = VerticalAlignment.Center };
                AutomationName(idea, $"{L.T("field.idea")} · {st.Name}");
                idea.LostFocus += (_, _) => Guard(() => SetStepFieldAt(index, "Idea", idea.Text));
                Put(i == 0 ? Ui.WithPlaceholder(idea, L.T("placeholder.idea")) : idea, row, col++);
            }
            Put(new TextBlock { Text = counts.GetValueOrDefault(st.Name).ToString(), TextAlignment = TextAlignment.Right }, row, col++);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (text, tip, action, enabled, danger) in new (string, string, Action, bool, bool)[]
            {
                ("▲", L.T("setup.up"), () => MoveStepAt(index, -1), i > 0, false),
                ("▼", L.T("setup.down"), () => MoveStepAt(index, 1), i < list.Count - 1, false),
                ("✕", L.T("setup.remove"), () => RemoveStepAt(index), true, true)
            })
            {
                var b = Ui.Button(text, Guard, action, danger: danger, tip: tip);
                b.IsEnabled = enabled;
                b.MinWidth = 30;
                b.Margin = new Thickness(0, 0, 4, 0);
                AutomationName(b, $"{tip} · {st.Name}");
                actions.Children.Add(b);
            }
            Put(actions, row, col);
        }
    }

    private static void AutomationName(DependencyObject e, string name) => System.Windows.Automation.AutomationProperties.SetName(e, name);

    private void AddStepFrom(TextBox input, ComboBox cls)
    {
        if (current == null) return;
        string name = input.Text.Trim();
        if (name.Length == 0) throw Error("msg.stepEmpty");
        if (StepList.Any(x => x.Name == name)) throw Error("msg.stepExists", ("step", name));
        StepList.Add(new StepDef { Name = name, Classification = Ui.ClassOf(cls) });
        input.Text = "";
        // Yeni adım seçilir; araya giren bir kayıtsa (kapsam dışı) sonra önceki adıma dönülür.
        flow.Added(StepList.Count);
        SaveSteps();
        if (ready && anchor is null) SetStatus(L.T("status.ready"));
    }

    /// <summary>Alan değişikliği (tür, bitiş anı, israf, fikir); tablo yeniden çizilmez (odak kaybolmasın).</summary>
    private void SetStepFieldAt(int index, string field, string value)
    {
        if (current == null || index < 0 || index >= StepList.Count) return;
        var st = StepList[index];
        string? v = value.Trim().Length == 0 ? null : value.Trim();
        switch (field)
        {
            case "Type": if (st.Type == v) return; st.Type = v; break;
            case "Waste": if (st.Waste == v) return; st.Waste = v; break;
            case "Idea": v = StudyRules.CleanNote(v); if (st.Idea == v) return; st.Idea = v; break;
            case "Note": v = StudyRules.CleanNote(v); if (st.Note == v) return; st.Note = v; break;
        }
        SaveSteps(table: false);
    }

    private void SetStepClassAt(int index, string cls)
    {
        if (current == null || index < 0 || index >= StepList.Count) return;
        var st = StepList[index];
        if (st.Classification == cls) return;
        st.Classification = cls;
        string name = st.Name;
        SaveSteps(table: false);
        var others = current.Observations.Where(o => o.Step == name && o.Classification != cls).ToList();
        if (others.Count > 0 && Ask(L.T("msg.stepClassExisting", ("n", others.Count), ("step", name), ("cls", cls.Length > 0 ? cls : "—")), L.T("title.stepClass")))
        {
            foreach (var o in others) o.Classification = cls;
            RefreshObservations();
            Save();
        }
    }

    /// <summary>Ad değişikliği kayıtlara, sıklığa ve adım temposuna da yansır.</summary>
    private void RenameStepAt(int index, TextBox box)
    {
        if (current == null || index < 0 || index >= StepList.Count) return;
        var st = StepList[index];
        string next = box.Text.Trim();
        if (next.Length == 0 || next == st.Name) { box.Text = st.Name; return; }
        if (StepList.Any(x => x != st && x.Name == next)) { box.Text = st.Name; throw Error("msg.stepExists", ("step", next)); }
        string old = st.Name;
        st.Name = next;
        foreach (var o in current.Observations.Where(o => o.Step == old)) o.Step = next;
        if (current.Frequency.Remove(old, out int every)) current.Frequency[next] = every;
        if (current.StepPace.Remove(old, out double pace)) current.StepPace[next] = pace;
        RefreshObservations();
        SaveSteps();
    }

    private void MoveStepAt(int i, int delta)
    {
        var list = StepList;
        int j = i + delta;
        if (j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        if (flow.Index == i) flow.Index = j; else if (flow.Index == j) flow.Index = i;
        SaveSteps();
    }

    private void RemoveStepAt(int i)
    {
        if (current == null || i < 0 || i >= StepList.Count) return;
        var st = StepList[i];
        int used = current.Observations.Count(o => o.Step == st.Name);
        if (!Ask(used > 0 ? L.T("msg.stepRemoveUsed", ("step", st.Name), ("n", used)) : L.T("msg.stepRemove", ("step", st.Name)), L.T("title.stepRemove"))) return;
        StepList.RemoveAt(i);
        if (flow.Index > i) flow.Index--;
        SaveSteps();
    }

    // ------------------------------------------------------------ kapsam (modüller)

    private void LoadModules()
    {
        if (current == null) return;
        loadingModules = true;
        foreach (var (m, box) in moduleBoxes) box.IsChecked = current.HasModule(m);
        loadingModules = false;
    }

    private void SaveModules()
    {
        if (current == null) return;
        current.Modules = StudyRules.CleanModules(moduleBoxes.Where(p => p.Value.IsChecked == true).Select(p => p.Key));
        ApplyModules();
        Save();
    }

    /// <summary>Kapalı araçlar gizlenir; İyileştirme sekmesi araç yoksa (ya da örneklemede) görünmez.</summary>
    private void ApplyModules()
    {
        var study = current;
        bool sampling = study?.IsSampling == true;
        bool On(string m) => study != null && !sampling && study.HasModule(m);
        foreach (var (m, tab) in moduleTabs) tab.Visibility = On(m) ? Visibility.Visible : Visibility.Collapsed;
        bool any = StudyRules.ModuleNames.Any(On);
        var improve = (TabItem)tabs.Items[TabImprove];
        improve.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (!any && tabs.SelectedIndex == TabImprove) tabs.SelectedIndex = TabAnalysis;
        if (improveTabs.SelectedItem is TabItem sel && sel.Visibility != Visibility.Visible)
            improveTabs.SelectedItem = moduleTabs.Values.FirstOrDefault(t => t.Visibility == Visibility.Visible);
        frameButton.Visibility = On("Cards") ? Visibility.Visible : Visibility.Collapsed;
        printStoryItem.Visibility = On("Cards") ? Visibility.Visible : Visibility.Collapsed;
        tTableItem.Visibility = sampling ? Visibility.Collapsed : Visibility.Visible;
        RenderStepTable();
    }

    // ------------------------------------------------------------ adımları başka etütten al

    private List<Study> StepSources(Guid? except) =>
        studyList.Where(s => s.Id != except && !s.IsSampling && StudyRules.CopySteps(s).Count > 0).ToList();

    private void FillStepSources()
    {
        var keep = (newStepsFrom.SelectedItem as ComboBoxItem)?.Tag;
        newStepsFrom.Items.Clear();
        newStepsFrom.Items.Add(new ComboBoxItem { Content = L.T("stepsFrom.none"), Tag = null });
        foreach (var s in StepSources(null))
            newStepsFrom.Items.Add(new ComboBoxItem { Content = $"{s.Code} · {s.Name} ({StudyRules.CopySteps(s).Count})", Tag = s.Id });
        newStepsFrom.SelectedIndex = Math.Max(0, newStepsFrom.Items.Cast<ComboBoxItem>().ToList().FindIndex(x => Equals(x.Tag, keep)));
    }

    private Study? NewStepsSource => (newStepsFrom.SelectedItem as ComboBoxItem)?.Tag is Guid id ? studyList.FirstOrDefault(s => s.Id == id) : null;

    /// <summary>append: mevcut adımlar korunur, eksikler sona eklenir. replace: liste kaynakla değişir; ölçülmüş adımlar sona eklenerek korunur.</summary>
    private void CopyStepsFrom(Study source, bool replace)
    {
        if (current == null) return;
        var copied = StudyRules.CopySteps(source);
        var measured = current.Observations.Select(o => o.Step).ToHashSet();
        var list = StepList;
        current.Steps = replace
            ? copied.Concat(list.Where(x => measured.Contains(x.Name) && !copied.Any(c => c.Name == x.Name))).ToList()
            : list.Concat(copied.Where(c => !list.Any(x => x.Name == c.Name))).ToList();
        flow.Index = 0;
        SaveSteps();
        SetStatus(L.T("copy.done", ("n", copied.Count), ("source", $"{source.Code} · {source.Name}")));
    }

    private void ShowCopySteps()
    {
        if (current == null) return;
        var sources = StepSources(current.Id);
        if (sources.Count == 0) { Inform(L.T("copy.none")); return; }
        var source = new ComboBox { MinWidth = 380, MinHeight = 28 };
        foreach (var s in sources) source.Items.Add($"{s.Code} · {s.Name}");
        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 460, Foreground = Ui.Soft, Margin = new Thickness(0, 6, 0, 8) };
        source.SelectionChanged += (_, _) =>
        {
            var list = source.SelectedIndex >= 0 ? StudyRules.CopySteps(sources[source.SelectedIndex]) : new List<StepDef>();
            preview.Text = list.Count > 0 ? L.T("copy.preview", ("n", list.Count), ("names", string.Join(" · ", list.Select(x => x.Name)))) : "";
        };
        source.SelectedIndex = 0;
        var append = new RadioButton { Content = L.T("copy.append"), IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
        var replace = new RadioButton { Content = L.T("copy.replace"), Margin = new Thickness(0, 2, 0, 6) };
        var ok = new Button { Content = L.T("copy.ok"), IsDefault = true, MinWidth = 110, Background = Ui.Accent, Foreground = Brushes.White, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = L.T("dialog.cancel"), IsCancel = true, MinWidth = 90 };
        var window = new Window
        {
            Title = L.T("copy.title"), Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Ui.Brush("#F0F4F8"),
            Content = new StackPanel
            {
                Margin = new Thickness(16), MaxWidth = 520,
                Children = { Ui.Hint(L.T("copy.hint")), Ui.Field(L.T("copy.source"), source), Ui.Text(L.T("copy.mode"), 13, true), append, replace, preview, Ui.Row(ok, cancel) }
            }
        };
        ok.Click += (_, _) => window.DialogResult = true;
        if (window.ShowDialog() != true || source.SelectedIndex < 0) return;
        CopyStepsFrom(sources[source.SelectedIndex], replace.IsChecked == true);
    }

    // ------------------------------------------------------------ durum ve onay

    private static string StatusLabel(Study s)
    {
        string label = L.T("status." + (s.Status.Length > 0 ? s.Status : "draft"));
        if (s.Status != "approved") return label;
        return string.Join(" · ", new[] { label, s.ApprovedBy, s.ApprovedAt.Length >= 10 ? Day(s.ApprovedAt) : "" }.Where(x => x.Length > 0));
    }

    /// <summary>yyyy-MM-dd → Türkçede gg.aa.yyyy (tarayıcıdaki fmtDay ile aynı).</summary>
    private static string Day(string iso)
    {
        var p = iso[..10].Split('-');
        return L.Lang == "tr" ? $"{p[2]}.{p[1]}.{p[0]}" : $"{p[0]}-{p[1]}-{p[2]}";
    }

    private UIElement BuildStatusRow()
    {
        studyStatus.Items.Add(L.T("status.draft"));
        studyStatus.Items.Add(L.T("status.done"));
        studyStatus.Items.Add(L.T("status.approved"));
        studyStatus.SelectedIndex = 0;
        approvedBy.MaxLength = 80;
        studyStatus.SelectionChanged += (_, _) => { if (!loadingStatus) Guard(SaveStatus); };
        approvedBy.LostFocus += (_, _) => { if (!loadingStatus) Guard(SaveStatus); };
        approvedAt.SelectedDateChanged += (_, _) => { if (!loadingStatus) Guard(SaveStatus); };
        var by = Ui.Field(L.T("field.approvedBy"), approvedBy);
        var at = Ui.Field(L.T("field.approvedAt"), approvedAt);
        approvalFields.Add(by);
        approvalFields.Add(at);
        return Ui.Row(Ui.Field(L.T("field.status"), studyStatus), by, at);
    }

    private void LoadStatus()
    {
        if (current == null) return;
        loadingStatus = true;
        studyStatus.SelectedIndex = current.Status switch { "done" => 1, "approved" => 2, _ => 0 };
        approvedBy.Text = current.ApprovedBy;
        approvedAt.SelectedDate = current.ApprovedAt.Length >= 10 &&
            DateTime.TryParseExact(current.ApprovedAt[..10], "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var d) ? d : null;
        loadingStatus = false;
        bool approved = current.Status == "approved";
        foreach (var f in approvalFields) f.Visibility = approved ? Visibility.Visible : Visibility.Collapsed;
        approvedNote.Text = approved ? L.T("approved.note", ("detail", StatusLabel(current))) : "";
        approvedNoteBox.Visibility = approved ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveStatus()
    {
        if (current == null) return;
        current.Status = studyStatus.SelectedIndex switch { 1 => "done", 2 => "approved", _ => "" };
        if (current.Status == "approved")
        {
            string by = approvedBy.Text.Trim();
            current.ApprovedBy = by.Length > 80 ? by[..80] : by;
            // Tarih boşsa bugün.
            var date = approvedAt.SelectedDate ?? DateTime.Today;
            current.ApprovedAt = date.ToString("yyyy-MM-dd", Invariant);
        }
        else { current.ApprovedBy = ""; current.ApprovedAt = ""; }
        LoadStatus();
        Save();
    }

    // ------------------------------------------------------------ Çıktı al menüsü

    private Button BuildOutputButton()
    {
        MenuItem Item(string key, Action action)
        {
            var item = new MenuItem { Header = L.T(key) };
            item.Click += (_, _) => Guard(action);
            outputMenu.Items.Add(item);
            return item;
        }
        Item("btn.excel", Export);
        Item("btn.print", Print);
        printStoryItem = Item("btn.printStory", PrintStory);
        tTableItem = Item("btn.tTable", ShowStatistics);
        var button = new Button
        {
            Content = L.T("btn.output") + "  ▾", Background = Ui.Accent, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(14, 5, 14, 5), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
        };
        button.Click += (_, _) =>
        {
            outputMenu.PlacementTarget = button;
            outputMenu.Placement = PlacementMode.Bottom;
            outputMenu.IsOpen = true;
        };
        return button;
    }

    private void UpdateSettingsSummary()
    {
        if (current == null) return;
        var s = current.Settings;
        settingsSummary.Text = " · " + (current.IsSampling
            ? L.T("set.summarySampling", ("conf", L.Num(s.Confidence)), ("err", current.SampleError is double e ? L.Num(e) : L.T("set.auto")))
            : L.T("set.summary", ("conf", L.Num(s.Confidence)), ("target", L.Num(s.TargetError)), ("pace", L.Num(s.Pace)), ("al", L.Num(s.Allowance)),
                ("takt", current.Takt is double t ? L.T("set.takt", ("takt", L.Num(t))) : "")));
    }

    // ------------------------------------------------------------ İyileştirme sekmesi

    private UIElement BuildImprove()
    {
        void Add(string module, string key, UIElement content)
        {
            var tab = new TabItem { Header = L.T(key), Content = content };
            moduleTabs[module] = tab;
            improveTabs.Items.Add(tab);
        }
        Add("Balance", "sub.balance", BuildBalanceSection());
        Add("Work", "sub.work", BuildWorkSection());
        Add("Ideas", "sub.ideas", BuildIdeasSection());
        Add("Cards", "sub.story", BuildStorySection());
        improveTabs.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, improveTabs) && current != null) Guard(RefreshAnalysis); };
        improveEmpty.Child = Ui.Text(L.T("analysis.emptyLead"));
        improveEmpty.Background = Ui.Paper;
        improveEmpty.BorderBrush = Ui.Line;
        improveEmpty.BorderThickness = new Thickness(1);
        improveEmpty.CornerRadius = new CornerRadius(6);
        improveEmpty.Padding = new Thickness(16);
        improveEmpty.Visibility = Visibility.Collapsed;
        var page = Ui.Stack(Ui.Hint(L.T("improve.lead")), improveEmpty, improveTabs);
        page.Margin = new Thickness(12);
        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ------------------------------------------------------------ toplu düzenleme (Ctrl/Shift ile çoklu seçim)

    private UIElement BuildBulkEditor()
    {
        bulkClass = new ComboBox { Width = 120, MinHeight = 28 };
        bulkClass.Items.Add(new ComboBoxItem { Content = L.T("bulk.keep"), Tag = "keep" });
        foreach (var c in StudyRules.Classes) bulkClass.Items.Add(new ComboBoxItem { Content = c == "" ? "—" : c, Tag = c });
        bulkClass.SelectedIndex = 0;
        bulkExcluded = Ui.TagCombo(new[] { ("yes", L.T("yes")), ("no", L.T("no")) }, 120);
        ((ComboBoxItem)bulkExcluded.Items[0]).Content = L.T("bulk.keep");
        bulkEditor.Child = Ui.Stack(bulkTitle, Ui.Hint(L.T("bulk.hint")),
            Ui.Row(Ui.Field(L.T("field.step"), bulkStep), Ui.Field(L.T("field.cycle"), bulkCycle), Ui.Field(L.T("field.class"), bulkClass),
                Ui.Field(L.T("check.excluded"), bulkExcluded), Ui.Field(L.T("field.rating"), bulkRating)),
            Ui.Row(Btn("bulk.apply", BulkApply, primary: true), Btn("bulk.delete", BulkDelete, danger: true),
                Btn("btn.rowClose", () => { bulkSelection.Clear(); RefreshObservations(); UpdateBulk(); })));
        bulkEditor.Background = Ui.Paper;
        bulkEditor.BorderBrush = Ui.Line;
        bulkEditor.BorderThickness = new Thickness(1);
        bulkEditor.CornerRadius = new CornerRadius(6);
        bulkEditor.Padding = new Thickness(12, 8, 12, 8);
        bulkEditor.Margin = new Thickness(0, 8, 0, 0);
        bulkEditor.Visibility = Visibility.Collapsed;
        return bulkEditor;
    }

    private void UpdateBulk()
    {
        if (current != null) bulkSelection.RemoveWhere(o => !current.Observations.Contains(o));
        bulkEditor.Visibility = bulkSelection.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        bulkTitle.Text = L.T("bulk.title", ("n", bulkSelection.Count));
    }

    private void BulkApply()
    {
        if (current == null || bulkSelection.Count == 0) return;
        string name = bulkStep.Text.Trim();
        int? cycle = bulkCycle.Text.Trim().Length > 0 ? PositiveInt(bulkCycle.Text, "name.cycle") : null;
        double? rating = bulkRating.Text.Trim().Length > 0 ? ReadRating(bulkRating.Text) : null;
        string cls = (bulkClass.SelectedItem as ComboBoxItem)?.Tag as string ?? "keep";
        string ex = Ui.TagOf(bulkExcluded);
        if (name.Length == 0 && cycle is null && rating is null && cls == "keep" && ex.Length == 0) throw Error("bulk.nothing");
        foreach (var o in bulkSelection)
        {
            if (name.Length > 0) o.Step = name;
            if (cycle is int c) o.Cycle = c;
            if (cls != "keep") o.Classification = cls;
            if (ex.Length > 0) o.Excluded = ex == "yes";
            if (rating is double r) o.Rating = r;
        }
        if (name.Length > 0 && !StepList.Any(x => x.Name == name))
        {
            StepList.Add(new StepDef { Name = name, Classification = cls == "keep" ? "" : cls });
            current.Steps = StudyRules.CleanSteps(current.Steps);
            RenderSteps();
        }
        int n = bulkSelection.Count;
        bulkStep.Text = bulkCycle.Text = bulkRating.Text = "";
        bulkClass.SelectedIndex = 0;
        bulkExcluded.SelectedIndex = 0;
        Save();
        RefreshObservations();
        SetStatus(L.T("bulk.done", ("n", n)));
    }

    private void BulkDelete()
    {
        if (current == null || bulkSelection.Count == 0) return;
        int n = bulkSelection.Count;
        if (!Ask(L.T("bulk.deleteConfirm", ("n", n)), L.T("title.deleteRow"))) return;
        current.Observations.RemoveAll(o => bulkSelection.Contains(o));
        if (selectedRow != null && bulkSelection.Contains(selectedRow)) SelectRow(null);
        bulkSelection.Clear();
        Save();
        RefreshObservations();
        UpdateBulk();
        SetStatus(L.T("bulk.deleted", ("n", n)));
    }

    // ------------------------------------------------------------ ilk kullanım turu

    private static string TourFile => Path.Combine(TestStoreDirectory ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoZamanEtudu"), "tur-goruldu");

    private static bool TourSeen
    {
        get { try { return File.Exists(TourFile); } catch (Exception) { return true; } }
    }

    private void ShowTour()
    {
        if (TestLog != null) return;
        int step = 0;
        const int n = 5;
        var count = Ui.Hint("");
        var title = new TextBlock { FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Ink, Margin = new Thickness(0, 2, 0, 8) };
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 440, Foreground = Ui.Ink, Margin = new Thickness(0, 0, 0, 12) };
        var back = new Button { Content = L.T("tour.back"), MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var nextButton = new Button { Content = L.T("tour.next"), MinWidth = 90, IsDefault = true, Background = Ui.Accent, Foreground = Brushes.White, Margin = new Thickness(0, 0, 8, 0) };
        var close = new Button { Content = L.T("tour.skip"), IsCancel = true, MinWidth = 90 };
        var original = tabs.Items.Cast<TabItem>().Select(t => (t.Background, t.BorderBrush)).ToList();
        var highlight = Ui.Brush("#FCEFC7");
        var window = new Window
        {
            Title = L.T("tour.open"), Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Ui.Brush("#F0F4F8"),
            Content = new StackPanel { Margin = new Thickness(18), Children = { count, title, text, Ui.Row(back, nextButton, close) } }
        };
        void Show()
        {
            count.Text = L.T("tour.count", ("i", step + 1), ("n", n));
            title.Text = L.T($"tour.{step + 1}.title");
            text.Text = L.T($"tour.{step + 1}.text");
            back.IsEnabled = step > 0;
            nextButton.Content = L.T(step == n - 1 ? "tour.finish" : "tour.next");
            for (int k = 0; k < tabs.Items.Count; k++)
            {
                var tab = (TabItem)tabs.Items[k];
                tab.Background = k == step ? highlight : original[k].Background;
                tab.BorderBrush = k == step ? Ui.Brush("#E0A526") : original[k].BorderBrush;
            }
        }
        back.Click += (_, _) => { step = Math.Max(0, step - 1); Show(); };
        nextButton.Click += (_, _) => { if (step >= n - 1) window.Close(); else { step++; Show(); } };
        window.Closed += (_, _) =>
        {
            for (int k = 0; k < tabs.Items.Count; k++) { ((TabItem)tabs.Items[k]).Background = original[k].Background; ((TabItem)tabs.Items[k]).BorderBrush = original[k].BorderBrush; }
            try { Directory.CreateDirectory(Path.GetDirectoryName(TourFile)!); File.WriteAllText(TourFile, "1"); } catch (Exception) { /* yazılamazsa bir dahaki açılışta yine gösterilir */ }
        };
        Show();
        window.ShowDialog();
    }
}
