using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace VideoZamanEtudu;

// Tablo satırları: değerler geçerli dilde biçimlenmiş metindir (WPF bağlaması için türler public).
public sealed record StudyRow(Study Source, string Code, string Name, string Where, string Count, string Cycles, string State, string Status, string Video, string Updated);
public sealed record ObservationRow(Observation Source, string Start, string End, string Duration, string Step, string Class, string Excluded, string Cycle, string Rating, string Note);
public sealed record SampleRow(Sample Source, string At, string Category, string Class, string Note);
public sealed record OutlierRow(Observation Source, string Step, string Cycle, string Start, string Duration, string Range);
public sealed record IdeaRow(string Step, string Class, string Waste, string Idea, string Contribution, string Share);
public sealed record SamplingViewRow(string Category, string Class, string Count, string Share, string Range, string Required);
public sealed record ResultRow(StepResult Source, string Step, string Count, string Excluded, string Mean, string Median, string Min, string Max,
    string Deviation, string CriticalT, string ErrorPercent, string Sufficiency, string Base, string Pace, string Normal, string Standard,
    string Every, string Contribution, string Method);
public sealed record CycleRow(string Cycle, string Elapsed, string Workload, string Diff);
public sealed record GroupRow(string Seconds, string Count);
public sealed record CompareViewRow(string Step, string Before, string After, string Diff, string DiffPercent);

/// <summary>
/// Ana pencere (v2.2). Arayüz kodla kurulur; beş bölüm: Çalışmalar, Hazırlık, Ölçüm, Analiz, İyileştirme (MainWindow.Setup.cs).
/// Akış ve metinler tarayıcı sürümüyle (Web/js/app.js) aynıdır. Dil değişince pencere yeni dilde yeniden kurulur.
/// Bu dosya: genel yapı, dil, Çalışmalar bölümü. Ölçüm: MainWindow.Measure.cs. Analiz: MainWindow.Analysis.cs.
/// </summary>
public partial class MainWindow : Window
{
    private readonly StudyStore store = new(TestStoreDirectory);
    private readonly TabControl tabs = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock header = new() { FontSize = 23, FontWeight = FontWeights.SemiBold, Foreground = Ui.Ink };
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private List<Study> studyList = new();
    private Study? current;
    private Guid? selectedStudyId;

    // Dil değişince yeni pencerede açılacak etüt ve bölüm.
    private static Guid? reopenId;
    private static int reopenTab;

    // Çalışmalar bölümü
    private readonly DataGrid studies = Ui.Table();
    private readonly TextBox newName = Ui.Input(260);
    private readonly TextBox newCode = Ui.Input(140);
    private readonly TextBox newVideo = Ui.Input(460);
    private readonly ComboBox newType = new() { Width = 170, MinHeight = 28 };
    private readonly TextBlock newStepsLabel = new() { Foreground = Ui.Soft, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 3) };
    private readonly TextBox newSteps = new() { Width = 460, Height = 96, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap };
    private readonly Dictionary<string, TextBox> newInfo = new();
    private readonly TextBlock studiesStatus = Ui.Text("", 13.5, true, Ui.Accent);
    private Button openButton = null!, exportButton = null!, deleteButton = null!, compareButton = null!;

    public MainWindow()
    {
        InitializeComponent();
        Title = L.T("app.title");
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);

        var top = new DockPanel();
        var lang = new Button { Content = L.T("lang.switch"), ToolTip = L.T("lang.switchTitle"), Padding = new Thickness(12, 4, 12, 4), VerticalAlignment = VerticalAlignment.Top };
        lang.Click += (_, _) => Guard(SwitchLanguage);
        var tour = new Button { Content = L.T("tour.open"), Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
        tour.Click += (_, _) => Guard(ShowTour);
        var links = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        links.Children.Add(tour);
        links.Children.Add(lang);
        DockPanel.SetDock(links, Dock.Right);
        top.Children.Add(links);
        top.Children.Add(Ui.Stack(header, Ui.Hint(L.T("edition"))));
        DockPanel.SetDock(top, Dock.Top);
        var shell = new DockPanel { Margin = new Thickness(20, 14, 20, 14) };
        shell.Children.Add(top);
        shell.Children.Add(tabs);
        Content = shell;
        UpdateHeader();

        tabs.Items.Add(new TabItem { Header = L.T("tab.0"), Content = BuildHome() });
        tabs.Items.Add(new TabItem { Header = L.T("tab.1"), Content = BuildSetup(), IsEnabled = false });
        tabs.Items.Add(new TabItem { Header = L.T("tab.2"), Content = BuildMeasure(), IsEnabled = false });
        tabs.Items.Add(new TabItem { Header = L.T("tab.3"), Content = BuildAnalysis(), IsEnabled = false });
        tabs.Items.Add(new TabItem { Header = L.T("tab.4"), Content = BuildImprove(), IsEnabled = false, Visibility = Visibility.Collapsed });
        tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, tabs)) return;
            if (tabs.SelectedIndex != TabMeasure) Pause();
            if (tabs.SelectedIndex == TabSetup && current != null) Guard(RenderStepTable);
            if (tabs.SelectedIndex is TabAnalysis or TabImprove && current != null) Guard(RefreshAnalysis);
        };
        clock.Tick += (_, _) => UpdatePosition();
        clock.Start();
        Closed += (_, _) => { clock.Stop(); video.Close(); };
        PreviewKeyDown += OnShortcut;
        // Liste pencere açıldıktan sonra okunur; hata olursa pencere yine açılır ve kullanıcı uyarılır.
        Loaded += (_, _) => Guard(() =>
        {
            ReloadStudies();
            if (store.LastSkipped > 0) Warn(L.T("msg.skipped", ("n", store.LastSkipped)));
            if (reopenId is Guid id && studyList.FirstOrDefault(s => s.Id == id) is Study study)
            {
                OpenStudy(study);
                tabs.SelectedIndex = reopenTab;
            }
            reopenId = null;
            // İlk açılışta kısa tur (bir kez; sonra üstteki Kısa tur düğmesiyle).
            if (!TourSeen && TestLog == null) ShowTour();
        });
    }

    private void UpdateHeader() => header.Text = current == null
        ? $"{L.T("header.brand")}   /   {L.T("header.studies")}"
        : $"{L.T("header.brand")}   /   {current.Code} · {current.Name}";

    /// <summary>Dil değişince pencere yeni dilde kurulur; açık etüt ve bölüm korunur. Ölçüm kayıtları zaten kaydedilmiştir.</summary>
    private void SwitchLanguage()
    {
        CommitPendingEdits();
        L.SetLang(L.Other);
        reopenId = current?.Id;
        reopenTab = tabs.SelectedIndex;
        var next = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = Left, Top = Top, Width = Width, Height = Height };
        Application.Current.MainWindow = next;
        next.Show();
        if (WindowState == WindowState.Maximized) next.WindowState = WindowState.Maximized;
        Close();
    }

    // ------------------------------------------------------------ ileti ve hata

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception error) { Warn(L.ErrorText(error)); }
    }

    private void Warn(string text, string? title = null)
    {
        if (TestLog != null) { TestLog("UYARI: " + text); return; }
        MessageBox.Show(this, text, title ?? L.T("app.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Inform(string text, string? title = null)
    {
        if (TestLog != null) { TestLog("BİLGİ: " + text); return; }
        MessageBox.Show(this, text, title ?? L.T("app.title"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool Ask(string text, string title, bool warning = false)
    {
        if (TestLog != null) { TestLog("SORU (evet): " + text); return true; }
        return MessageBox.Show(this, text, title, MessageBoxButton.YesNo, warning ? MessageBoxImage.Warning : MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private static Exception Error(string key, params (string, object?)[] values)
    {
        var error = new InvalidOperationException(L.T(key, values));
        return error;
    }

    private Button Btn(string key, Action action, bool primary = false, bool danger = false, string? tipKey = null) =>
        Ui.Button(L.T(key), Guard, action, primary, danger, tipKey is null ? null : L.T(tipKey));

    private static int PositiveInt(string text, string nameKey) =>
        int.TryParse(text.Trim(), out int value) && value > 0 ? value : throw Error("int.invalid", ("name", L.T(nameKey)));

    /// <summary>"1,5", "1.5" ve "1e2" kabul edilir; binlik ayırıcı yok (tarayıcıdaki parseNumber ile aynı).</summary>
    private static double Number(string text, string nameKey)
    {
        var t = text.Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[+-]?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?$") &&
            double.TryParse(t.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && double.IsFinite(v))
            return v;
        throw Error("number.invalid", ("name", L.T(nameKey)));
    }

    // ------------------------------------------------------------ Çalışmalar

    private UIElement BuildHome()
    {
        newVideo.IsReadOnly = true;
        var how = Ui.Stack(Ui.Rich("1. " + L.T("how.1")), Ui.Rich("2. " + L.T("how.2")), Ui.Rich("3. " + L.T("how.3")));
        var note = Ui.Box(Ui.Rich(L.T("home.note"), 13.5), Ui.NoteBack, Ui.NoteLine);
        var demo = new Button { Content = L.T("demo.button"), Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 6, 0) };
        demo.Click += (_, _) => Guard(OpenDemo);
        var demoRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        demoRow.Children.Add(Ui.Hint(L.T("demo.before")));
        demoRow.Children.Add(demo);
        demoRow.Children.Add(Ui.Hint(L.T("demo.after").TrimStart('—', ' ')));

        var infoRow = new WrapPanel();
        foreach (var field in StudyRules.InfoFields)
        {
            var box = Ui.Input(field == "Note" ? 300 : 150);
            newInfo[field] = box;
            infoRow.Children.Add(Ui.Field(L.T("info." + field), box));
        }
        newType.Items.Add(new ComboBoxItem { Content = L.T("studyType.time"), Tag = "" });
        newType.Items.Add(new ComboBoxItem { Content = L.T("studyType.sampling"), Tag = "sampling" });
        newType.SelectedIndex = 0;
        var stepsBox = Ui.WithPlaceholder(newSteps, L.T("placeholder.newSteps"));
        var stepsHint = (TextBlock)stepsBox.Children[1];
        void SyncType()
        {
            bool sampling = (newType.SelectedItem as ComboBoxItem)?.Tag as string == "sampling";
            newStepsLabel.Text = L.T(sampling ? "field.newCategories" : "field.newSteps");
            stepsHint.Text = L.T(sampling ? "placeholder.newCategories" : "placeholder.newSteps");
        }
        newType.SelectionChanged += (_, _) => SyncType();
        SyncType();
        var stepsFrom = Ui.Field(L.T("field.stepsFrom"), newStepsFrom);
        var more = new Expander
        {
            Header = L.T("new.more"), Margin = new Thickness(0, 2, 0, 6),
            Content = Ui.Stack(stepsFrom, newStepsLabel, stepsBox, infoRow)
        };
        // v2.2: etüdün kapsamı (yalın araçlar); varsayılan kapalı, sonradan Hazırlık sekmesinden açılır.
        var newModules = new WrapPanel();
        foreach (var m in StudyRules.ModuleNames)
        {
            var box = new CheckBox { Content = L.T("mod." + m), Margin = new Thickness(0, 2, 18, 4) };
            newModuleBoxes[m] = box;
            newModules.Children.Add(box);
        }
        var modulesBox = Ui.Box(Ui.Stack(Ui.Hint(L.T("mod.legend")), newModules, Ui.Hint(L.T("mod.hint"))));
        newType.SelectionChanged += (_, _) =>
        {
            bool sampling = (newType.SelectedItem as ComboBoxItem)?.Tag as string == "sampling";
            modulesBox.Visibility = stepsFrom.Visibility = sampling ? Visibility.Collapsed : Visibility.Visible;
        };

        openButton = Btn("btn.open", OpenSelected);
        exportButton = Btn("btn.export", ExportStudy);
        deleteButton = Btn("btn.delete", DeleteStudy, danger: true);
        compareButton = Btn("btn.compare", ShowCompare);
        var importButton = Btn("btn.import", ImportStudy);
        importButton.Margin = new Thickness(14, 0, 6, 4);
        compareButton.Margin = new Thickness(14, 0, 6, 4);
        deleteButton.Margin = new Thickness(14, 0, 6, 4);

        Ui.Columns(studies, (L.T("th.code"), "Code", false), (L.T("th.name"), "Name", false), (L.T("th.where"), "Where", false),
            (L.T("th.count"), "Count", true), (L.T("th.cycles"), "Cycles", true), (L.T("th.state"), "State", false),
            (L.T("th.status"), "Status", false), (L.T("th.video"), "Video", false), (L.T("th.updated"), "Updated", false));
        studies.MinHeight = 160;
        studies.SelectionChanged += (_, _) =>
        {
            if (studies.SelectedItem is StudyRow row) selectedStudyId = row.Source.Id;
            UpdateStudyButtons();
        };
        studies.MouseDoubleClick += (_, _) => Guard(OpenSelected);

        var form = Ui.Stack(
            Ui.Text(L.T("home.lead")), how, note, demoRow,
            Ui.Row(Ui.Field(L.T("field.studyType"), newType), Ui.Field(L.T("field.name"), newName), Ui.Field(L.T("field.code"), newCode)),
            Ui.Row(Ui.Field(L.T("field.video"), Ui.WithPlaceholder(newVideo, L.T("placeholder.video"))),
                Btn("btn.pickFile", () => { if (PickVideo() is string p) newVideo.Text = p; })),
            modulesBox,
            more,
            Ui.Row(Btn("btn.create", CreateStudy, primary: true), openButton, importButton, exportButton,
                Btn("btn.backup", BackupAll), compareButton, deleteButton),
            studiesStatus,
            Ui.Hint(L.Strip(L.T("studies.hint"))));
        studiesStatus.Visibility = Visibility.Collapsed;
        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(form, Dock.Top);
        root.Children.Add(form);
        root.Children.Add(studies);
        return root;
    }

    private void SetStudiesStatus(string text)
    {
        studiesStatus.Text = text;
        studiesStatus.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Study? SelectedStudy => studyList.FirstOrDefault(s => s.Id == selectedStudyId);

    private void UpdateStudyButtons()
    {
        bool none = SelectedStudy is null;
        openButton.IsEnabled = exportButton.IsEnabled = deleteButton.IsEnabled = !none;
        compareButton.IsEnabled = studyList.Count >= 2;
    }

    private void ReloadStudies()
    {
        studyList = store.ReadAll();
        var rows = studyList.Select(s => new StudyRow(s, s.Code, s.Name,
            string.Join(" · ", new[] { s.Info.GetValueOrDefault("Station"), s.Info.GetValueOrDefault("Operator") }.Where(x => !string.IsNullOrEmpty(x))),
            s.Observations.Count.ToString(), s.Observations.Count > 0 ? s.Observations.Select(o => o.Cycle).Distinct().Count().ToString() : "",
            StudyState(s), StatusLabel(s), s.VideoLabel, L.Date(s.Updated))).ToList();
        studies.ItemsSource = rows;
        studies.SelectedItem = rows.FirstOrDefault(r => r.Source.Id == selectedStudyId);
        UpdateStudyButtons();
        FillStepSources();
    }

    /// <summary>Listede kısa durum: ölçüm yok / yeterli / k adım yetersiz.</summary>
    private static string StudyState(Study study)
    {
        if (study.Observations.Count == 0) return L.T("state.empty");
        try
        {
            int bad = Analysis.Steps(study).Count(r => r.Sufficiency != "Yeterli" && r.Count > 0);
            return bad > 0 ? L.T("state.need", ("k", bad)) : L.T("state.ok");
        }
        catch (ArgumentException) { return ""; }
    }

    private void CreateStudy()
    {
        if (string.IsNullOrWhiteSpace(newName.Text) || string.IsNullOrWhiteSpace(newCode.Text) || !File.Exists(newVideo.Text))
            throw Error("msg.createMissing");
        bool sampling = (newType.SelectedItem as ComboBoxItem)?.Tag as string == "sampling";
        var lines = newSteps.Text.Split('\n').Select(x => new StepDef { Name = x.Trim() }).ToList();
        // v2.2: adımlar başka etütten alınabilir (yazılan satırlar sona eklenir); kapsam (modüller) seçilir.
        var fromSource = !sampling && NewStepsSource is Study source ? StudyRules.CopySteps(source) : new List<StepDef>();
        var study = new Study
        {
            Name = newName.Text.Trim(), Code = newCode.Text.Trim(), Video = newVideo.Text,
            StudyType = sampling ? "sampling" : "",
            Steps = sampling ? new() : StudyRules.CleanSteps(fromSource.Concat(lines.Where(l => !fromSource.Any(x => x.Name == l.Name)))),
            Modules = sampling ? null : StudyRules.CleanModules(newModuleBoxes.Where(p => p.Value.IsChecked == true).Select(p => p.Key)),
            Categories = sampling ? StudyRules.CleanCategories(lines.Select(x => new SampleCategory { Name = x.Name })) : new(),
            Info = StudyRules.CleanInfo(newInfo.ToDictionary(p => p.Key, p => p.Value.Text))
        };
        store.Save(study);
        newName.Text = newCode.Text = newVideo.Text = newSteps.Text = "";
        newType.SelectedIndex = 0;
        newStepsFrom.SelectedIndex = 0;
        foreach (var box in newModuleBoxes.Values) box.IsChecked = false;
        foreach (var box in newInfo.Values) box.Text = "";
        OpenStudy(study);
    }

    private void OpenSelected()
    {
        if (SelectedStudy is Study study) OpenStudy(study);
    }

    private static string DemoDirectory => TestDemoDirectory ?? Path.Combine(AppContext.BaseDirectory, "demo");
    private static (string Package, string Video, string Code) DemoFiles => L.Lang == "tr"
        ? ("sentetik-depo-etudu.vzetud", "sentetik-depo-etudu.mp4", "DEMO-01")
        : ("synthetic-warehouse-study.vzetud", "synthetic-warehouse-study.mp4", "DEMO-01");

    /// <summary>Demo etüdü: zaten varsa onu açar, yoksa uygulama klasöründeki demo paketini içe alır ve videosunu bağlar.</summary>
    private void OpenDemo()
    {
        var demo = DemoFiles;
        string package = Path.Combine(DemoDirectory, demo.Package);
        if (!File.Exists(package)) throw Error("msg.demoMissing", ("path", DemoDirectory));
        var study = studyList.FirstOrDefault(s => s.Code == demo.Code && s.VideoLabel == demo.Video);
        if (study is null)
        {
            study = StudyTransfer.Import(package);
            AttachDemoVideo(study);
            store.Save(study);
        }
        OpenStudy(study);
    }

    /// <summary>Videosu bağlı olmayan demo etüdüne uygulama klasöründeki demo videosu bağlanır.</summary>
    private static bool AttachDemoVideo(Study study)
    {
        if (study.Video.Length > 0 && File.Exists(study.Video)) return false;
        string path = Path.Combine(DemoDirectory, study.ExpectedVideoName.Length > 0 ? study.ExpectedVideoName : Path.GetFileName(study.Video));
        if (study.VideoLabel is not ("sentetik-depo-etudu.mp4" or "synthetic-warehouse-study.mp4") || !File.Exists(path)) return false;
        study.Video = path;
        return true;
    }

    private void ImportStudy()
    {
        var dialog = new OpenFileDialog { Filter = $"{L.T("d.filter.package")}|*.vzetud;*.zip|{L.T("d.filter.all")}|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        if (StudyBackup.IsZip(dialog.FileName)) { RestoreBackup(dialog.FileName); return; }
        var study = StudyTransfer.Import(dialog.FileName);
        if (!Ask(L.T("msg.importConfirm", ("name", study.Name), ("code", study.Code)), L.T("title.import"))) return;
        store.Save(study);
        OpenStudy(study);
    }

    private void ExportStudy()
    {
        if (SelectedStudy is not Study selected) throw Error("msg.selectToExport");
        var dialog = new SaveFileDialog
        {
            FileName = StudyTransfer.SuggestedFileName(selected, StudyTransfer.Extension), Filter = $"{L.T("d.filter.vzetud")}|*.vzetud",
            DefaultExt = StudyTransfer.Extension, AddExtension = true
        };
        if (dialog.ShowDialog(this) == true) StudyTransfer.Export(selected, dialog.FileName);
    }

    /// <summary>Bütün etütler tek .zip: içinde her etüt ayrı .vzetud paketi (tarayıcı sürümü de açar).</summary>
    private void BackupAll()
    {
        CommitPendingEdits();
        if (studyList.Count == 0) throw Error("msg.backupNone");
        var dialog = new SaveFileDialog
        {
            FileName = $"{L.T("backup.file")}-{DateTime.Now:yyyyMMdd}.zip", Filter = $"{L.T("d.filter.zip")}|*.zip",
            DefaultExt = ".zip", AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var result = StudyBackup.Write(studyList, dialog.FileName);
        SetStudiesStatus(L.T(result.Skipped > 0 ? "msg.backupDoneSkipped" : "msg.backupDone", ("count", result.Count), ("skipped", result.Skipped)));
    }

    private void RestoreBackup(string path)
    {
        var plan = StudyBackup.Read(path, studyList);
        var text = L.T("msg.restoreFound", ("add", plan.Add.Count), ("same", plan.Same));
        if (plan.Failed.Count > 0) text += " " + L.T("msg.restoreFailed", ("count", plan.Failed.Count), ("names", string.Join(", ", plan.Failed.Take(5))));
        if (plan.Add.Count == 0) { Inform(text, L.T("title.restore")); return; }
        if (!Ask(text + " " + L.T("msg.restoreConfirm", ("add", plan.Add.Count)), L.T("title.restore"))) return;
        foreach (var study in plan.Add) store.Save(study);
        ReloadStudies();
        SetStudiesStatus(L.T("msg.restoreDone", ("add", plan.Add.Count)));
    }

    private void DeleteStudy()
    {
        if (SelectedStudy is not Study selected) throw Error("msg.selectToDelete");
        if (!Ask(L.T("msg.deleteConfirm", ("name", selected.Name), ("code", selected.Code)), L.T("title.delete"), warning: true)) return;
        store.Delete(selected.Id);
        if (current?.Id == selected.Id) CloseStudy();
        selectedStudyId = null;
        ReloadStudies();
    }

    private void Save()
    {
        if (current == null) return;
        current.Updated = DateTime.Now;
        store.Save(current);
        ReloadStudies();
    }

    private void OpenStudy(Study study)
    {
        CommitPendingEdits();
        current = study;
        selectedStudyId = study.Id;
        AttachDemoVideo(study);
        UpdateHeader();
        StopMeasure();
        selectedStart = null;
        selectedRow = null;
        selectedResult = null;
        undo.Clear();
        // Adım listesi yoksa ölçümlerden çıkarılır; ilk değişiklikte kaydedilir.
        // Bütün tanım alanları korunur (tür, israf, fikir, istasyon, kare).
        study.Steps = StudyRules.DeriveSteps(study).Select(x => new StepDef
        {
            Name = x.Name, Classification = x.Classification, Note = x.Note, Type = x.Type, Station = x.Station,
            Waste = x.Waste, Idea = x.Idea, FrameMs = x.FrameMs
        }).ToList();
        // Kaldığı yerden devam: son kaydın ardındaki adım ve çevrim; video son kaydın bitişine gider (video hazır olunca).
        var point = StudyRules.Resume(study);
        flow.Index = point.Step;
        flow.Resume = null;
        resumeAt = point.At;
        SetCycle(point.Cycle);
        excludedNext.IsChecked = false;
        foreach (TabItem tab in tabs.Items) tab.IsEnabled = true;
        filterStep.SelectedIndex = -1;
        filterCycle.SelectedIndex = -1;
        ApplyStudyType();
        storyFrames = null;
        bulkSelection.Clear();
        settingsBox.IsExpanded = false;
        RenderSteps();
        RenderSampling();
        RefreshObservations();
        UpdateBulk();
        LoadSettings();
        LoadInfo();
        LoadStatus();
        LoadModules();
        ApplyModules();
        // Yeni etüt (adım ve ölçüm yok): Hazırlık; aksi halde ölçüm.
        tabs.SelectedIndex = !study.IsSampling && study.Steps.Count == 0 && study.Observations.Count == 0 ? TabSetup : TabMeasure;
        LoadVideo();
        ReloadStudies();
    }

    private void CloseStudy()
    {
        Pause();
        StopMeasure();
        selectedStart = null;
        video.Close();
        video.Source = null;
        ready = false;
        timeline.IsEnabled = false;
        current = null;
        selectedRow = null;
        selectedResult = null;
        undo.Clear();
        observations.ItemsSource = null;
        results.ItemsSource = null;
        cycles.ItemsSource = null;
        groups.ItemsSource = null;
        stepsPanel.Children.Clear();
        segments.Children.Clear();
        bulkSelection.Clear();
        UpdateBulk();
        tabs.SelectedIndex = 0;
        ApplyModules();
        for (int i = 1; i < tabs.Items.Count; i++) ((TabItem)tabs.Items[i]).IsEnabled = false;
        UpdateHeader();
    }

    /// <summary>Odaktaki metin kutusunun değişikliği (adım açıklaması, etüt bilgisi, ayar) kayda geçsin.</summary>
    private void CommitPendingEdits()
    {
        if (Keyboard.FocusedElement is TextBox box) box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (current == null) return;
        try { SaveInfo(); } catch (Exception) { /* geçersiz değer kaydedilmez */ }
    }

    private static string? PickVideo()
    {
        var dialog = new OpenFileDialog { Filter = $"{L.T("picker.videos")}|*.mp4;*.wmv;*.avi;*.mov;*.m4v;*.mpg;*.mpeg|{L.T("d.filter.all")}|*.*" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
