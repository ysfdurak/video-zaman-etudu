using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace VideoZamanEtudu;

public partial class MainWindow : Window
{
    private readonly StudyStore store = new();
    private readonly TabControl tabs = new();
    private readonly DataGrid studies = Table();
    private readonly DataGrid observations = Table();
    private readonly DataGrid results = Table();
    private readonly DataGrid cycles = Table();
    private readonly DataGrid groups = Table();
    private readonly MediaElement video = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform };
    private readonly Slider timeline = new() { IsMoveToPointEnabled = true, IsEnabled = false };
    private readonly TextBlock position = Label("00:00.000");
    private readonly TextBlock status = Label("");
    private readonly TextBlock analysisStatus = Label("");
    private readonly TextBox step = Input(150, "Adım");
    private readonly TextBox cycle = Input(55, "1");
    private readonly ComboBox classification = new() { Width = 95, ItemsSource = new[] { "", "VA", "SVA", "NVA" }, SelectedIndex = 0 };
    private readonly CheckBox excluded = new() { Content = "Kapsam dışı" };
    private readonly TextBox confidence = Input(65);
    private readonly TextBox target = Input(65);
    private readonly TextBox pace = Input(65);
    private readonly TextBox allowance = Input(65);
    private readonly TextBox groupMs = Input(65);
    private readonly TextBox every = Input(65, "1");
    private readonly CheckBox stable = new() { Content = "Süreç kararlı" };
    private readonly ComboBox method = new() { Width = 230, ItemsSource = new[] { "Aritmetik ortalama", "En çok tekrar eden en küçük süre" } };
    private readonly Button measure = new() { Content = "Başla" };
    private readonly TextBlock header = Label("");
    private static readonly double[] Speeds = { 0.25, 0.5, 1, 1.5, 2 };
    private readonly ComboBox speed = new() { Width = 80, ItemsSource = new[] { "0,25×", "0,5×", "1×", "1,5×", "2×" }, SelectedIndex = 2 };
    private const long NudgeMaxMs = 1000;
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private Study? current;
    private long? anchor;
    private long? selectedStart;
    private bool ready, playing, syncingTimeline, suppressSelection;

    public MainWindow()
    {
        InitializeComponent();
        var shell = new DockPanel { Margin = new Thickness(20) };
        header.FontSize = 23;
        header.FontWeight = FontWeights.SemiBold;
        UpdateHeader();
        DockPanel.SetDock(header, Dock.Top);
        shell.Children.Add(header);
        shell.Children.Add(tabs);
        Content = shell;
        tabs.Items.Add(new TabItem { Header = "1  Çalışmalar", Content = BuildHome() });
        tabs.Items.Add(new TabItem { Header = "2  Video ve ölçüm", Content = BuildMeasure(), IsEnabled = false });
        tabs.Items.Add(new TabItem { Header = "3  Analiz ve rapor", Content = BuildAnalysis(), IsEnabled = false });
        tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, tabs)) return;
            if (tabs.SelectedIndex != 1) Pause();
            if (tabs.SelectedIndex == 2 && current != null) RefreshAnalysis();
        };
        clock.Tick += (_, _) => UpdatePosition();
        clock.Start();
        Closed += (_, _) => { clock.Stop(); video.Close(); };
        PreviewKeyDown += OnShortcut;
        speed.SelectionChanged += (_, _) => { if (playing) video.SpeedRatio = SelectedSpeed; };
        // Liste pencere açıldıktan sonra okunur; hata olursa pencere yine açılır ve kullanıcı uyarılır.
        Loaded += (_, _) => Guard(() =>
        {
            ReloadStudies();
            if (store.LastSkipped > 0)
                MessageBox.Show(this, $"{store.LastSkipped} etüt kaydı okunamadığı için listede gösterilmiyor. Diğer kayıtlar kullanılabilir.",
                    "Video Zaman Etüdü", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private void UpdateHeader() => header.Text = current == null
        ? "VIDEO ZAMAN ETÜDÜ   /   Çalışmalar"
        : $"VIDEO ZAMAN ETÜDÜ   /   {current.Code} · {current.Name}";

    private double SelectedSpeed => speed.SelectedIndex >= 0 && speed.SelectedIndex < Speeds.Length ? Speeds[speed.SelectedIndex] : 1;

    private UIElement BuildHome()
    {
        var code = Input(130);
        var name = Input(240);
        var path = Input(400);
        path.IsReadOnly = true;
        Columns(studies, ("Kod", "Code"), ("Etüt adı", "Name"), ("Video", "VideoLabel"), ("Son düzenleme", "Updated"));
        studies.MouseDoubleClick += (_, _) => Guard(OpenSelected);
        return Layout(studies,
            Label("Yeni etüt oluştur veya kayıtlı bir etüdü aç."),
            Row(Field("Etüt adı", name), Field("Etüt kodu", code)),
            Row(Field("Video dosyası", path), Action("Dosya seç…", () => { if (PickVideo() is string p) path.Text = p; })),
            Row(Action("Yeni çalışma başlat", () =>
            {
                if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(code.Text) || !File.Exists(path.Text))
                    throw new ArgumentException("Etüt adı, kodu ve geçerli video dosyası seçin.");
                var study = new Study { Name = name.Text.Trim(), Code = code.Text.Trim(), Video = path.Text };
                store.Save(study);
                OpenStudy(study);
            }), Action("Seçili çalışmayı aç", OpenSelected),
                Action("Etüdü içe al…", ImportStudy), Action("Seçili etüdü dışa aktar…", ExportStudy),
                Action("Seçili etüdü sil…", DeleteStudy)));
    }

    private UIElement BuildMeasure()
    {
        Columns(observations, ("Başlangıç (ms)", "StartMs"), ("Bitiş (ms)", "EndMs"), ("Süre (sn)", "Seconds"),
            ("İş adımı", "Step"), ("Sınıf", "Classification"), ("Kapsam dışı", "Excluded"), ("Çevrim", "Cycle"));
        observations.SelectionChanged += (_, _) =>
        {
            if (suppressSelection || observations.SelectedItem is not Observation row) return;
            step.Text = row.Step;
            cycle.Text = row.Cycle.ToString();
            classification.SelectedItem = row.Classification;
            excluded.IsChecked = row.Excluded;
            if (ready) Seek(row.StartMs);
        };
        video.MediaOpened += (_, _) =>
        {
            ready = video.NaturalDuration.HasTimeSpan;
            timeline.IsEnabled = ready;
            if (ready) timeline.Maximum = Math.Max(1, video.NaturalDuration.TimeSpan.TotalMilliseconds);
            status.Text = ready ? "Video hazır. İstediğiniz konuma gidip Başla’ya basın." : "Video süresi okunamadı.";
        };
        video.MediaFailed += (_, _) =>
        {
            ready = playing = timeline.IsEnabled = false;
            StopMeasure();
            status.Text = "Video açılamadı. Dosyayı veya Windows video codec desteğini kontrol edin.";
        };
        video.MediaEnded += (_, _) => { Pause(); UpdatePosition(); };
        timeline.ValueChanged += (_, _) => { if (!syncingTimeline && ready) Seek((long)timeline.Value); };
        measure.Click += (_, _) => Guard(Measure);
        var frame = new Border { Background = Brushes.Black, Child = video, MinHeight = 150 };
        var player = new DockPanel { Margin = new Thickness(8, 0, 0, 0) };
        var controls = new StackPanel();
        controls.Children.Add(timeline);
        controls.Children.Add(Row(position, Action("Oynat", Play), Action("Duraklat", Pause),
            Action("◀ 0,1 sn", () => Nudge(-100)), Action("0,1 sn ▶", () => Nudge(100)),
            Field("Hız", speed), Action("Videoyu yeniden seç", Relink)));
        controls.Children.Add(Label("Kısayollar: Boşluk oynat/duraklat · Enter Başla/Ölç · Esc Durdur · ←/→ 0,1 sn · Shift+←/→ 1 sn · Ctrl+←/→ 0,04 sn (≈1 kare). " +
            "Kısayollar metin kutusu veya açılır liste seçiliyken çalışmaz. İnce konumlama (en fazla 1 sn) ölçümü kesmez; zaman çizgisinde atlama keser."));
        DockPanel.SetDock(controls, Dock.Bottom);
        player.Children.Add(controls);
        player.Children.Add(frame);
        var split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(47, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(53, GridUnitType.Star) });
        split.Children.Add(observations);
        Grid.SetColumn(player, 1);
        split.Children.Add(player);
        return Layout(split, status,
            Row(Field("İş adımı", step), Field("Çevrim", cycle), Field("Sınıf", classification), excluded,
                measure, Action("Durdur", StopMeasure), Action("Aralık başlangıcı al", MarkStart), Action("Aralığı ekle", AddMarked)),
            Row(Action("Seçili satırı güncelle", EditRow), Action("Sınıfı aynı adı taşıyanlara uygula", BulkClassify),
                Action("Seçili satırı sil", DeleteRow)),
            Label("Ölç son aralığı kaydeder. Durdur ölçüm modunu kapatır. Çakışan veya bağımsız aralık için başlangıcı alıp bitiş konumunda aralığı ekleyin."));
    }

    private UIElement BuildAnalysis()
    {
        Columns(results, ("Adım", "Step"), ("n", "Count"), ("Dışarıda", "Excluded"), ("Ort.", "Mean"),
            ("Medyan", "Median"), ("Min", "Min"), ("Maks", "Max"), ("Std sapma", "Deviation"), ("t", "CriticalT"),
            ("Hata %", "ErrorPercent"), ("Yeterlilik", "Sufficiency"), ("Baz", "Base"), ("Normal", "Normal"),
            ("Standart", "Standard"), ("Her N çevrim", "Every"), ("Katkı", "Contribution"), ("Yöntem", "Method"));
        Columns(cycles, ("Çevrim", "Cycle"), ("Ölçülen geçen süre (sn)", "Elapsed"), ("İş yükü (sn)", "Workload"));
        Columns(groups, ("Grup ortalaması (sn)", "Seconds"), ("Tekrar", "Count"));
        results.SelectionChanged += (_, _) =>
        {
            if (current == null || results.SelectedItem is not StepResult row) return;
            groups.ItemsSource = row.Groups;
            every.Text = row.Every.ToString();
        };
        var lower = new TabControl { Height = 155, Margin = new Thickness(0, 8, 0, 0) };
        lower.Items.Add(new TabItem { Header = "Çevrimler", Content = cycles });
        lower.Items.Add(new TabItem { Header = "Seçili adımın süre grupları", Content = groups });
        var body = new DockPanel();
        DockPanel.SetDock(lower, Dock.Bottom);
        body.Children.Add(lower);
        body.Children.Add(results);
        return Layout(body,
            Row(Field("Güven %", confidence), Field("Hedef hata %", target), Field("Tempo %", pace),
                Field("Tolerans %", allowance), Field("Grup (ms)", groupMs), Field("Baz süre yöntemi", method), stable),
            Row(Action("Ayarları uygula", ApplySettings), Field("Her N çevrimde", every),
                Action("Seçili adıma sıklık uygula", ApplyFrequency), Action("t tablosu ve formül", ShowStatistics),
                Action("Excel’e aktar…", Export)),
            analysisStatus);
    }

    private void OpenSelected()
    {
        if (studies.SelectedItem is Study study) OpenStudy(study);
    }

    private void ImportStudy()
    {
        var dialog = new OpenFileDialog { Filter = "Video Zaman Etüdü paketi|*.vzetud" };
        if (dialog.ShowDialog() != true) return;

        var study = StudyTransfer.Import(dialog.FileName);
        if (MessageBox.Show($"“{study.Name}” ({study.Code}) yeni bir etüt olarak eklenecek. Devam edilsin mi?",
            "Etüdü içe al", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        store.Save(study);
        OpenStudy(study);
    }

    private void ExportStudy()
    {
        if (studies.SelectedItem is not Study selected)
            throw new InvalidOperationException("Dışa aktarmak için listeden bir etüt seçin.");

        var dialog = new SaveFileDialog
        {
            FileName = StudyTransfer.SuggestedFileName(selected, StudyTransfer.Extension), Filter = "Video Zaman Etüdü paketi|*.vzetud",
            DefaultExt = StudyTransfer.Extension, AddExtension = true
        };
        if (dialog.ShowDialog() == true) StudyTransfer.Export(selected, dialog.FileName);
    }

    private void DeleteStudy()
    {
        if (studies.SelectedItem is not Study selected)
            throw new InvalidOperationException("Silmek için listeden bir etüt seçin.");
        if (MessageBox.Show(this,
                $"“{selected.Name}” ({selected.Code}) etüdü ve tüm ölçümleri bu bilgisayardan silinecek. Video dosyası silinmez. " +
                "Bu işlem geri alınamaz; gerekirse önce dışa aktarın. Silinsin mi?",
                "Etüdü sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        store.Delete(selected.Id);
        if (current?.Id == selected.Id) CloseStudy();
        ReloadStudies();
    }

    private void CloseStudy()
    {
        Pause();
        StopMeasure();
        selectedStart = null;
        video.Close();
        video.Source = null;
        ready = timeline.IsEnabled = false;
        current = null;
        observations.ItemsSource = null;
        results.ItemsSource = null;
        cycles.ItemsSource = null;
        groups.ItemsSource = null;
        tabs.SelectedIndex = 0;
        for (int i = 1; i < tabs.Items.Count; i++) ((TabItem)tabs.Items[i]).IsEnabled = false;
        UpdateHeader();
    }

    private void OpenStudy(Study study)
    {
        current = study;
        UpdateHeader();
        StopMeasure();
        selectedStart = null;
        foreach (TabItem tab in tabs.Items) tab.IsEnabled = true;
        tabs.SelectedIndex = 1;
        RefreshObservations();
        LoadSettings();
        LoadVideo();
        ReloadStudies();
    }

    private void LoadVideo()
    {
        Pause();
        video.Close();
        ready = timeline.IsEnabled = false;
        position.Text = "00:00.000";
        syncingTimeline = true;
        timeline.Value = 0;
        syncingTimeline = false;
        if (current == null || !File.Exists(current.Video))
        {
            string expected = current?.VideoLabel ?? "";
            status.Text = string.IsNullOrEmpty(expected)
                ? "Video bağlı değil. Ölçümleri inceleyebilir veya bir video seçebilirsiniz."
                : $"Video bağlı değil veya bulunamadı. Beklenen dosya: {expected}. Videoyu yeniden seçebilirsiniz.";
            return;
        }
        status.Text = "Video açılıyor…";
        video.Source = null;
        video.Source = new Uri(current.Video);
        video.Pause();
    }

    private void Relink()
    {
        if (current == null || PickVideo() is not string path) return;
        string expected = current.VideoLabel;
        string videoNote = string.IsNullOrEmpty(expected) ? "" : $" Beklenen dosya: {expected}.";
        if (current.Observations.Count > 0 && MessageBox.Show("Mevcut ölçümler seçilen videoya aynı zaman damgalarıyla bağlanacak. Aynı videoyu seçtiğinizden emin olun." + videoNote + " Devam edilsin mi?",
            "Videoyu yeniden bağla", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        current.Video = path;
        Save();
        StopMeasure();
        selectedStart = null;
        LoadVideo();
    }

    private static string? PickVideo()
    {
        var dialog = new OpenFileDialog { Filter = "Video dosyaları|*.mp4;*.wmv;*.avi;*.mov;*.m4v;*.mpg;*.mpeg|Tüm dosyalar|*.*" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void Play()
    {
        if (!ready) return;
        video.SpeedRatio = SelectedSpeed;
        video.Play();
        playing = true;
    }

    private void Pause()
    {
        if (ready && playing) video.Pause();
        playing = false;
    }

    /// <summary>
    /// İnce konumlama: videoyu duraklatır ve en fazla 1 sn ileri/geri alır. Ardışık ölçümü kesmez,
    /// çünkü kullanıcı adım sınırını bulmak için yalnızca bitiş noktasını düzeltir. Büyük atlamalar
    /// (zaman çizgisi, satır seçimi) K02 gereği ölçümü keser.
    /// </summary>
    private void Nudge(long deltaMs)
    {
        RequireVideo();
        deltaMs = Math.Clamp(deltaMs, -NudgeMaxMs, NudgeMaxMs);
        Pause();
        long target = Math.Clamp(PositionMs + deltaMs, 0, (long)timeline.Maximum);
        video.Position = TimeSpan.FromMilliseconds(target);
        UpdatePosition();
        if (anchor is long start && target <= start)
            status.Text = $"Konum ölçüm başlangıcında veya gerisinde ({start / 1000d:0.000} sn). Ölç için ileri alın.";
    }

    private void OnShortcut(object sender, KeyEventArgs e)
    {
        if (tabs.SelectedIndex != 1 || current == null) return;
        if (Keyboard.FocusedElement is TextBox or ComboBox or ComboBoxItem) return;
        var modifiers = Keyboard.Modifiers;
        long step = modifiers.HasFlag(ModifierKeys.Shift) ? 1000 : modifiers.HasFlag(ModifierKeys.Control) ? 40 : 100;
        switch (e.Key)
        {
            case Key.Space: Guard(() => { if (playing) Pause(); else Play(); }); break;
            case Key.Enter: Guard(Measure); break;
            case Key.Escape:
                if (anchor.HasValue) { StopMeasure(); status.Text = "Ölçüm durduruldu. Yeni kayıt eklenmedi."; }
                break;
            case Key.Left: Guard(() => Nudge(-step)); break;
            case Key.Right: Guard(() => Nudge(step)); break;
            default: return;
        }
        e.Handled = true;
    }

    private long PositionMs => (long)Math.Round(video.Position.TotalMilliseconds);

    private void UpdatePosition()
    {
        if (!ready) return;
        syncingTimeline = true;
        timeline.Value = PositionMs;
        syncingTimeline = false;
        position.Text = video.Position.ToString(@"hh\:mm\:ss\.fff");
    }

    private void Seek(long time)
    {
        if (anchor.HasValue)
        {
            StopMeasure();
            status.Text = "Konum değişti. Yeni konumda Başla’ya basın.";
        }
        video.Position = TimeSpan.FromMilliseconds(time);
        position.Text = video.Position.ToString(@"hh\:mm\:ss\.fff");
    }

    private void StopMeasure()
    {
        anchor = null;
        measure.Content = "Başla";
    }

    private void Measure()
    {
        RequireVideo();
        long now = PositionMs;
        if (!anchor.HasValue)
        {
            anchor = now;
            measure.Content = "Ölç";
            status.Text = $"Ölçüm başladı ({now / 1000d:0.000} sn).";
            return;
        }
        AddObservation(anchor.Value, now);
        anchor = now;
    }

    private void MarkStart()
    {
        RequireVideo();
        StopMeasure();
        selectedStart = PositionMs;
        status.Text = $"Aralık başlangıcı alındı ({selectedStart / 1000d:0.000} sn).";
    }

    private void AddMarked()
    {
        RequireVideo();
        if (selectedStart is not long start) throw new ArgumentException("Önce aralık başlangıcını alın.");
        AddObservation(start, PositionMs);
        selectedStart = null;
    }

    private void RequireVideo()
    {
        if (!ready || current == null) throw new InvalidOperationException("Ölçüm için önce bir video açın.");
    }

    private void AddObservation(long start, long end)
    {
        if (end <= start) throw new ArgumentException("Bitiş, başlangıçtan sonra olmalı.");
        var row = new Observation { StartMs = start, EndMs = end };
        FillRow(row);
        if (current!.Observations.Any(x => x.StartMs == start && x.EndMs == end && x.Cycle == row.Cycle &&
            x.Step == row.Step && x.Excluded == row.Excluded))
            throw new ArgumentException("Bu ölçüm aynı adım ve çevrim için zaten kayıtlı.");
        current.Observations.Add(row);
        Save();
        RefreshObservations();
        status.Text = $"Kaydedildi · {row.Step} · {row.Seconds:0.000} sn";
    }

    private void FillRow(Observation row)
    {
        var name = step.Text.Trim();
        if (name.Length == 0) throw new ArgumentException("Adım adı boş olamaz.");
        int cycleNumber = PositiveInt(cycle.Text, "Çevrim");
        row.Step = name;
        row.Cycle = cycleNumber;
        row.Classification = classification.SelectedItem as string ?? "";
        row.Excluded = excluded.IsChecked == true;
    }

    private void EditRow()
    {
        if (observations.SelectedItem is not Observation row) return;
        FillRow(row);
        Save();
        RefreshObservations(row);
    }

    private void BulkClassify()
    {
        if (observations.SelectedItem is not Observation row || current == null) return;
        foreach (var item in current.Observations.Where(x => x.Step == row.Step))
            item.Classification = classification.SelectedItem as string ?? "";
        Save();
        RefreshObservations(row);
    }

    private void DeleteRow()
    {
        if (observations.SelectedItem is not Observation row || current == null) return;
        if (MessageBox.Show("Seçili ölçüm silinsin mi?", "Ölçümü sil", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        current.Observations.Remove(row);
        Save();
        RefreshObservations();
    }

    private void ReloadStudies() => studies.ItemsSource = store.ReadAll();

    private void Save()
    {
        if (current == null) return;
        current.Updated = DateTime.Now;
        store.Save(current);
        ReloadStudies();
    }

    private void RefreshObservations(Observation? selected = null)
    {
        suppressSelection = true;
        observations.ItemsSource = current?.Observations.OrderBy(x => x.StartMs).ThenBy(x => x.EndMs).ToList();
        observations.SelectedItem = selected;
        suppressSelection = false;
    }

    private void LoadSettings()
    {
        if (current == null) return;
        var s = current.Settings;
        confidence.Text = s.Confidence.ToString();
        target.Text = s.TargetError.ToString();
        pace.Text = s.Pace.ToString();
        allowance.Text = s.Allowance.ToString();
        groupMs.Text = s.GroupMs.ToString();
        method.SelectedIndex = s.RepeatedLow ? 1 : 0;
        stable.IsChecked = s.Stable;
    }

    private void ApplySettings()
    {
        if (current == null) return;
        var settings = new Settings
        {
            Confidence = Number(confidence.Text, "Güven"), TargetError = Number(target.Text, "Hedef hata"),
            Pace = Number(pace.Text, "Tempo"), Allowance = Number(allowance.Text, "Tolerans"),
            GroupMs = PositiveInt(groupMs.Text, "Grup hassasiyeti"), RepeatedLow = method.SelectedIndex == 1,
            Stable = stable.IsChecked == true
        };
        settings.Validate();
        current.Settings = settings;
        Save();
        RefreshAnalysis();
    }

    private void ApplyFrequency()
    {
        if (current == null || results.SelectedItem is not StepResult row) return;
        current.Frequency[row.Step] = PositiveInt(every.Text, "Sıklık");
        Save();
        RefreshAnalysis();
    }

    private void RefreshAnalysis()
    {
        if (current == null) return;
        var previous = (results.SelectedItem as StepResult)?.Step;
        var rows = Analysis.Steps(current);
        results.ItemsSource = rows;
        results.SelectedItem = rows.FirstOrDefault(x => x.Step == previous) ?? rows.FirstOrDefault();
        cycles.ItemsSource = Analysis.Cycles(current);
        groups.ItemsSource = (results.SelectedItem as StepResult)?.Groups;
        bool stableProcess = current.Settings.Stable;
        analysisStatus.Text = $"Toyota esinli rehber: {(stableProcess ? "5 gözlem (kararlı süreç). Tekrar eden düşük süreyi inceleyin." : "10 gözlem (genel). Ortalama ile başlayın.")} " +
            "İstatistiksel yeterlilik ayrı hesaplanır. Westinghouse puan tablosu doğrulanana kadar manuel tempo kullanılır.";
    }

    private void ShowStatistics()
    {
        if (current == null) return;
        var s = current.Settings;
        var dfs = Enumerable.Range(1, 30).Concat(new[] { 40, 60, 120 });
        var values = dfs.Select(df => $"{df,4}    {MathNet.Numerics.Distributions.StudentT.InvCDF(0, 1, df, (1 + s.Confidence / 100) / 2):0.0000}");
        var window = new Window { Title = $"Student-t tablosu · %{s.Confidence}", Width = 490, Height = 620, Owner = this };
        window.Content = new TextBox { IsReadOnly = true, Margin = new Thickness(12), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"), Text = "Bağıl hata = t × s / (√n × ortalama)\n\n  sd    Kritik t\n" + string.Join("\n", values) };
        window.ShowDialog();
    }

    private void Export()
    {
        if (current == null) return;
        ApplySettings();
        var dialog = new SaveFileDialog { FileName = StudyTransfer.SuggestedFileName(current, ".xlsx", "_analiz"), Filter = "Excel çalışma kitabı|*.xlsx" };
        if (dialog.ShowDialog() == true) StudyExport.Write(current, dialog.FileName);
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception error) { MessageBox.Show(error.Message, "Video Zaman Etüdü", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private Button Action(string title, Action action)
    {
        var button = new Button { Content = title };
        button.Click += (_, _) => Guard(action);
        return button;
    }

    private static int PositiveInt(string text, string name) => int.TryParse(text, out int value) && value > 0
        ? value : throw new ArgumentException($"{name} pozitif bir tam sayı olmalı.");

    private static double Number(string text, string name) =>
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
         double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
         double.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("tr-TR"), out value)) && double.IsFinite(value)
         ? value : throw new ArgumentException($"{name} geçerli bir sayı olmalı.");

    private static TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(0, 3, 10, 7), TextWrapping = TextWrapping.Wrap };
    private static TextBox Input(double width, string value = "") => new() { Width = width, Text = value, VerticalContentAlignment = VerticalAlignment.Center };
    private static DataGrid Table() => new() { AutoGenerateColumns = false, IsReadOnly = true,
        CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
        AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(236, 245, 248)), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
    private static void Columns(DataGrid table, params (string Header, string Path)[] columns)
    {
        foreach (var (header, path) in columns)
            table.Columns.Add(new DataGridTextColumn { Header = header,
                Binding = new Binding(path) { StringFormat = path switch
                {
                    "Updated" => "dd.MM.yyyy HH:mm",
                    "Seconds" or "Mean" or "Median" or "Min" or "Max" or "Deviation" or "CriticalT" or
                    "ErrorPercent" or "Base" or "Normal" or "Standard" or "Contribution" or "Elapsed" or "Workload" => "0.000",
                    _ => null
                } } });
    }

    private static UIElement Field(string name, UIElement input)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 4) };
        panel.Children.Add(Label(name));
        panel.Children.Add(input);
        return panel;
    }

    private static WrapPanel Row(params UIElement[] elements)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
        foreach (var element in elements)
        {
            if (element is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Bottom;
            panel.Children.Add(element);
        }
        return panel;
    }

    private static DockPanel Layout(UIElement body, params UIElement[] header)
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        var top = new StackPanel();
        foreach (var element in header) top.Children.Add(element);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(body);
        return root;
    }
}
