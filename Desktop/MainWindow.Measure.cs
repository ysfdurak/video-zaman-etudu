using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VideoZamanEtudu;

/// <summary>
/// Video ve ölçüm bölümü (tarayıcıdaki v1.5–v1.7 akışı):
/// adım listesi, Başla/Ölç ile ardışık kayıt, otomatik sıradaki adım ve çevrim, kapsam dışı ara kayıt,
/// son kaydı geri alma, zaman çizgisinde ölçülen aralıklar, kayıt düzenleyici ve süzgeç.
/// Zaman çizgisinde gezinmek ölçümü kesmez; Ölç başlangıcın gerisindeyse kayıt eklenmez ve uyarı verilir.
/// </summary>
public partial class MainWindow
{
    private readonly MediaElement video = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform };
    private readonly Slider timeline = new() { IsMoveToPointEnabled = true, IsEnabled = false, Margin = new Thickness(0, 2, 0, 4) };
    private readonly Canvas segments = new() { Height = 10, Background = Ui.Brush("#E3EAEE"), ClipToBounds = true, Margin = new Thickness(5, 6, 5, 0) };
    private readonly Rectangle liveSegment = new() { Height = 10, Fill = Ui.Brush("#17364A"), Opacity = 0.35, Visibility = Visibility.Collapsed };
    private readonly WrapPanel legend = new() { Margin = new Thickness(0, 2, 0, 4) };
    private readonly TextBlock position = new() { FontFamily = new FontFamily("Consolas"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 4), Foreground = Ui.Ink };
    private readonly TextBlock status = Ui.Text("", 14, true, Ui.Accent);
    private static readonly double[] Speeds = { 0.25, 0.5, 1, 1.5, 2 };
    private readonly ComboBox speed = new() { Width = 80, SelectedIndex = 2, MinHeight = 28 };
    private const long NudgeMaxMs = 1000;

    private readonly WrapPanel stepsPanel = new() { Margin = new Thickness(0, 2, 0, 4) };
    private readonly StackPanel stepTools = new();
    private readonly ComboBox stepClass = Ui.ClassCombo();
    private readonly TextBox stepNote = Ui.Input(340);
    private Button stepLeft = null!, stepRight = null!;
    private readonly TextBox newStep = Ui.Input(220);
    private readonly ComboBox newStepClass = Ui.ClassCombo();
    private readonly CheckBox autoAdvance = new() { IsChecked = true, Margin = new Thickness(0, 4, 0, 2) };
    private readonly TextBox cycleBox = Ui.Input(55, "1");
    private readonly TextBlock nowStep = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Ui.Ink };
    private readonly TextBlock nowNote = Ui.Hint("");
    private readonly TextBlock nowLive = Ui.Text("", 13, true, Ui.Accent);
    private readonly CheckBox excludedNext = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
    private readonly Button measureButton = new() { MinWidth = 120, FontSize = 16, FontWeight = FontWeights.SemiBold, Background = Ui.Accent, Foreground = Brushes.White, Padding = new Thickness(18, 8, 18, 8) };

    private readonly DataGrid observations = Ui.Table();
    private readonly ComboBox filterStep = new() { MinWidth = 140, MinHeight = 28 };
    private readonly ComboBox filterCycle = new() { MinWidth = 80, MinHeight = 28 };
    private readonly TextBlock obsCount = Ui.Hint("");
    private readonly Border rowEditor = new();
    private readonly TextBox rowStep = Ui.Input(180);
    private readonly ComboBox rowClass = Ui.ClassCombo();
    private readonly TextBox rowCycle = Ui.Input(55);
    private readonly CheckBox rowExcluded = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) };
    private readonly TextBox rowNote = Ui.Input(260);

    private readonly MeasureFlow flow = new();
    private readonly List<(Observation Row, int StepIndex, int Cycle)> undo = new();
    private Observation? selectedRow;
    private long? anchor, selectedStart, resumeAt;
    private bool ready, playing, syncingTimeline, suppressSelection, fillingFilters;

    private double SelectedSpeed => speed.SelectedIndex >= 0 && speed.SelectedIndex < Speeds.Length ? Speeds[speed.SelectedIndex] : 1;
    private List<StepDef> StepList => current?.Steps ?? new List<StepDef>();
    private StepDef? CurrentStep => flow.Index >= 0 && flow.Index < StepList.Count ? StepList[flow.Index] : null;

    private UIElement BuildMeasure()
    {
        foreach (var s in new[] { "0.25", "0.5", "1", "1.5", "2" }) speed.Items.Add(L.T("speed." + s));
        speed.SelectedIndex = 2;
        speed.SelectionChanged += (_, _) => { if (playing) video.SpeedRatio = SelectedSpeed; };
        measureButton.Content = L.T("btn.start");
        measureButton.Click += (_, _) => Guard(Measure);
        autoAdvance.Content = new TextBlock { Text = L.T("check.autoAdvance"), TextWrapping = TextWrapping.Wrap };
        excludedNext.Content = new TextBlock { Text = L.T("check.excludedNext"), TextWrapping = TextWrapping.Wrap };
        excludedNext.Checked += (_, _) => UpdateNow();
        excludedNext.Unchecked += (_, _) => UpdateNow();
        cycleBox.TextChanged += (_, _) => UpdateNow();
        rowExcluded.Content = L.T("check.excluded");

        // Video
        video.MediaOpened += (_, _) =>
        {
            ready = video.NaturalDuration.HasTimeSpan;
            timeline.IsEnabled = ready;
            if (ready) timeline.Maximum = Math.Max(1, video.NaturalDuration.TimeSpan.TotalMilliseconds);
            SetStatus(ready ? (StepList.Count > 0 ? L.T("status.ready") : L.T("status.addSteps")) : L.T("status.noDuration"));
            if (ready && resumeAt is long at)
            {
                resumeAt = null;
                long target = Math.Min(at, (long)timeline.Maximum);
                video.Position = TimeSpan.FromMilliseconds(target);
                UpdatePosition();
                if (CurrentStep is StepDef st) SetStatus(L.T("status.resume", ("at", L.Clock(target)), ("step", st.Name), ("cycle", cycleBox.Text)));
            }
            RenderSegments();
        };
        video.MediaFailed += (_, _) =>
        {
            ready = playing = false;
            timeline.IsEnabled = false;
            StopMeasure();
            SetStatus(L.T("status.videoError"));
        };
        video.MediaEnded += (_, _) => { Pause(); UpdatePosition(); };
        timeline.ValueChanged += (_, _) => { if (!syncingTimeline && ready) Seek((long)timeline.Value); };
        segments.SizeChanged += (_, _) => RenderSegments();
        var frame = new Border { Background = Brushes.Black, Child = video, MinHeight = 200 };
        var controls = Ui.Stack(segments, timeline,
            Ui.Row(position, Btn("btn.play", Play), Btn("btn.pause", Pause),
                Btn("btn.back", () => Nudge(-100), tipKey: "title.back"), Btn("btn.forward", () => Nudge(100), tipKey: "title.forward"),
                Ui.Field(L.T("field.speed"), speed), Btn("btn.relink", Relink)),
            legend,
            Ui.Hint(L.T("shortcuts")));
        var player = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(controls, Dock.Bottom);
        player.Children.Add(controls);
        player.Children.Add(frame);

        // Adımlar
        stepLeft = Btn("btn.stepLeft", () => MoveStep(-1), tipKey: "title.stepLeft");
        stepRight = Btn("btn.stepRight", () => MoveStep(1), tipKey: "title.stepRight");
        stepClass.SelectionChanged += (_, _) => { if (!suppressSelection) Guard(ChangeStepClass); };
        stepNote.LostFocus += (_, _) => Guard(SaveStepNote);
        stepTools.Children.Add(Ui.Row(stepLeft, stepRight, Ui.Field(L.T("field.class"), stepClass),
            Btn("btn.stepRename", RenameStep), Btn("btn.stepRemove", RemoveStep, danger: true)));
        stepTools.Children.Add(Ui.Field(L.T("field.stepNote"), Ui.WithPlaceholder(stepNote, L.T("placeholder.stepNote"))));
        newStep.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Guard(AddStep); } };
        var stepsHead = new DockPanel();
        var stepsHint = Ui.Hint(L.T("steps.hint"));
        DockPanel.SetDock(stepsHint, Dock.Right);
        stepsHead.Children.Add(stepsHint);
        stepsHead.Children.Add(Ui.Text(L.T("h.steps"), 14, true));
        var stepsBox = Ui.Box(Ui.Stack(stepsHead, stepsPanel, stepTools,
            Ui.Row(Ui.Field(L.T("field.newStep"), Ui.WithPlaceholder(newStep, L.T("placeholder.newStep"))),
                Ui.Field(L.T("field.class"), newStepClass), Btn("btn.addStep", AddStep)),
            autoAdvance));

        // Sıradaki kayıt
        var nowBox = Ui.Box(Ui.Row(
            Ui.Field(L.T("field.cycle"), Ui.Row(Btn("d.minus", () => SetCycle(CycleValue() - 1), tipKey: "title.cycleDown"), cycleBox,
                Btn("d.plus", () => SetCycle(CycleValue() + 1), tipKey: "title.cycleUp"))),
            Ui.Field(L.T("now.label"), Ui.Stack(nowStep, nowNote, nowLive)),
            excludedNext));

        // Kayıtlar
        Ui.Columns(observations, (L.T("th.start"), "Start", true), (L.T("th.end"), "End", true), (L.T("th.duration"), "Duration", true),
            (L.T("th.step"), "Step", false), (L.T("th.class"), "Class", false), (L.T("th.excluded"), "Excluded", false),
            (L.T("th.cycle"), "Cycle", true), (L.T("th.note"), "Note", false));
        observations.MinHeight = 140;
        observations.SelectionChanged += (_, _) =>
        {
            if (suppressSelection || observations.SelectedItem is not ObservationRow row) return;
            SelectRow(row.Source);
            // Kaydın başına gider; süren ölçüm kesilmez.
            if (ready) Seek(row.Source.StartMs);
        };
        filterStep.SelectionChanged += (_, _) => { if (!fillingFilters) RefreshObservations(); };
        filterCycle.SelectionChanged += (_, _) => { if (!fillingFilters) RefreshObservations(); };
        var filterRow = Ui.Row(Ui.Field(L.T("filter.step"), filterStep), Ui.Field(L.T("filter.cycle"), filterCycle), obsCount);

        rowEditor.Child = Ui.Stack(
            Ui.Text(L.T("h.rowEditor"), 14, true),
            Ui.Row(Ui.Field(L.T("field.step"), rowStep), Ui.Field(L.T("field.class"), rowClass), Ui.Field(L.T("field.cycle"), rowCycle), rowExcluded,
                Ui.Field(L.T("field.rowNote"), Ui.WithPlaceholder(rowNote, L.T("placeholder.rowNote")))),
            Ui.Row(Btn("btn.editRow", EditRow, primary: true), Btn("btn.bulk", BulkClassify), Btn("btn.deleteRow", DeleteRow, danger: true),
                Btn("btn.rowClose", () => { SelectRow(null); RefreshObservations(); })));
        rowEditor.Background = Ui.Paper;
        rowEditor.BorderBrush = Ui.Line;
        rowEditor.BorderThickness = new Thickness(1);
        rowEditor.CornerRadius = new CornerRadius(6);
        rowEditor.Padding = new Thickness(12, 8, 12, 8);
        rowEditor.Margin = new Thickness(0, 8, 0, 0);
        rowEditor.Visibility = Visibility.Collapsed;
        var interval = new Expander
        {
            Header = L.T("more.interval"), Margin = new Thickness(0, 6, 0, 0),
            Content = Ui.Stack(Ui.Row(Btn("btn.markStart", MarkStart), Btn("btn.addMarked", AddMarked)), Ui.Hint(L.T("measure.hint")))
        };

        var side = new DockPanel();
        var sideTop = Ui.Stack(stepsBox, nowBox,
            Ui.Row(measureButton, Btn("btn.stop", () => { if (anchor.HasValue) { StopMeasure(); SetStatus(L.T("status.stopped")); } }),
                Btn("btn.undo", UndoLast, tipKey: "title.undo")),
            Ui.Help(L.T("help.measure.title"), L.T("help.measure.body")),
            filterRow);
        var sideBottom = Ui.Stack(rowEditor, interval);
        DockPanel.SetDock(sideTop, Dock.Top);
        DockPanel.SetDock(sideBottom, Dock.Bottom);
        side.Children.Add(sideTop);
        side.Children.Add(sideBottom);
        side.Children.Add(observations);
        var sideScroll = new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        var split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45, GridUnitType.Star), MinWidth = 420 });
        split.Children.Add(player);
        Grid.SetColumn(sideScroll, 1);
        split.Children.Add(sideScroll);
        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(status, Dock.Top);
        root.Children.Add(status);
        root.Children.Add(split);
        return root;
    }

    private void SetStatus(string text) => status.Text = text;

    // ------------------------------------------------------------ video

    private void LoadVideo()
    {
        Pause();
        video.Close();
        ready = false;
        timeline.IsEnabled = false;
        position.Text = L.Clock(0);
        syncingTimeline = true;
        timeline.Value = 0;
        syncingTimeline = false;
        segments.Children.Clear();
        if (current == null || !File.Exists(current.Video))
        {
            string expected = current?.VideoLabel ?? "";
            SetStatus(string.IsNullOrEmpty(expected) ? L.T("status.noVideo") : L.T("status.noVideoExpected", ("name", expected)));
            return;
        }
        SetStatus(L.T("status.opening"));
        video.Source = null;
        video.Source = new Uri(current.Video);
        video.Pause();
    }

    private void Relink()
    {
        if (current == null || PickVideo() is not string path) return;
        string expected = current.VideoLabel;
        string note = string.IsNullOrEmpty(expected) ? "" : L.T("relink.note", ("name", expected));
        string differs = !string.IsNullOrEmpty(expected) && !string.Equals(System.IO.Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase)
            ? L.T("relink.differs", ("name", System.IO.Path.GetFileName(path))) : "";
        if (current.Observations.Count > 0 && !Ask(L.T("relink.confirm", ("note", note), ("differs", differs)), L.T("title.relink"))) return;
        current.Video = path;
        Save();
        StopMeasure();
        selectedStart = null;
        LoadVideo();
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

    /// <summary>İnce konumlama: videoyu duraklatır ve en fazla 1 sn ileri/geri alır.</summary>
    private void Nudge(long deltaMs)
    {
        RequireVideo();
        deltaMs = Math.Clamp(deltaMs, -NudgeMaxMs, NudgeMaxMs);
        Pause();
        long target = Math.Clamp(PositionMs + deltaMs, 0, (long)timeline.Maximum);
        video.Position = TimeSpan.FromMilliseconds(target);
        UpdatePosition();
        if (anchor is long start && target <= start) SetStatus(L.T("status.nudgeBehind", ("s", L.Num3(start / 1000d))));
    }

    private long PositionMs => (long)Math.Round(video.Position.TotalMilliseconds);

    private void UpdatePosition()
    {
        if (!ready) return;
        long now = PositionMs;
        syncingTimeline = true;
        timeline.Value = now;
        syncingTimeline = false;
        position.Text = L.Clock(now);
        UpdateLive(now);
    }

    /// <summary>Konum değişikliği ardışık ölçümü kesmez; adımın bittiği kare bulunup Ölç'e basılabilir.</summary>
    private void Seek(long time)
    {
        video.Position = TimeSpan.FromMilliseconds(time);
        position.Text = L.Clock(time);
        if (anchor is long start)
            SetStatus(L.T(time > start ? "status.measuring" : "status.nudgeBehind", ("s", L.Num3(start / 1000d))));
        UpdateLive(time);
    }

    private void RequireVideo()
    {
        if (!ready || current == null) throw Error("msg.needVideo");
    }

    private void OnShortcut(object sender, KeyEventArgs e)
    {
        if (tabs.SelectedIndex != 1 || current == null || Ui.IsTyping()) return;
        var modifiers = Keyboard.Modifiers;
        long step = modifiers.HasFlag(ModifierKeys.Shift) ? 1000 : modifiers.HasFlag(ModifierKeys.Control) ? 40 : 100;
        int digit = e.Key is >= Key.D1 and <= Key.D9 ? e.Key - Key.D1 : e.Key is >= Key.NumPad1 and <= Key.NumPad9 ? e.Key - Key.NumPad1 : -1;
        if (digit >= 0 && modifiers == ModifierKeys.None) { Guard(() => SelectStep(digit)); e.Handled = true; return; }
        switch (e.Key)
        {
            case Key.Space: Guard(() => { if (playing) Pause(); else Play(); }); break;
            case Key.Enter: Guard(Measure); break;
            case Key.Escape:
                if (anchor.HasValue) { StopMeasure(); SetStatus(L.T("status.stopped")); }
                break;
            case Key.Left: Guard(() => Nudge(-step)); break;
            case Key.Right: Guard(() => Nudge(step)); break;
            case Key.Add or Key.OemPlus: Guard(() => SetCycle(CycleValue() + 1)); break;
            case Key.Subtract or Key.OemMinus: Guard(() => SetCycle(CycleValue() - 1)); break;
            case Key.Z when modifiers == ModifierKeys.Control: Guard(UndoLast); break;
            default: return;
        }
        e.Handled = true;
    }

    // ------------------------------------------------------------ zaman çizgisindeki ölçülen aralıklar

    private static readonly Brush ExcludedBrush = MakeHatch();
    private static Brush MakeHatch()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Ui.Brush("#C7D0D6"), null, new RectangleGeometry(new Rect(0, 0, 6, 6))));
        group.Children.Add(new GeometryDrawing(null, new Pen(Ui.Brush("#8a979f"), 1.4), new LineGeometry(new Point(0, 6), new Point(6, 0))));
        var brush = new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 6, 6), ViewportUnits = BrushMappingMode.Absolute };
        brush.Freeze();
        return brush;
    }

    private void RenderSegments()
    {
        segments.Children.Clear();
        legend.Children.Clear();
        double total = timeline.Maximum, width = segments.ActualWidth;
        if (current == null || !ready || !(total > 0) || width <= 0) return;
        foreach (var o in current.Observations)
        {
            var seg = new Rectangle
            {
                Height = 10, Width = Math.Max(1, (o.EndMs - o.StartMs) / total * width),
                Fill = o.Excluded ? ExcludedBrush : Ui.ClassBrush(o.Classification), Stroke = Ui.Paper, StrokeThickness = 0.5,
                ToolTip = $"{o.Step} · {L.Seconds(o.Seconds)} · {L.T("th.cycle")} {o.Cycle}"
            };
            Canvas.SetLeft(seg, o.StartMs / total * width);
            segments.Children.Add(seg);
        }
        segments.Children.Add(liveSegment);
        var used = current.Observations.Select(o => o.Excluded ? "ex" : o.Classification).ToHashSet();
        foreach (var c in new[] { "VA", "SVA", "NVA", "", "ex" }.Where(used.Contains))
        {
            var swatch = new Rectangle { Width = 10, Height = 10, Fill = c == "ex" ? ExcludedBrush : Ui.ClassBrush(c), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            legend.Children.Add(swatch);
            legend.Children.Add(new TextBlock { Text = c == "ex" ? L.T("th.excluded") : c == "" ? L.T("summary.noClass") : c, Foreground = Ui.Soft, FontSize = 12, Margin = new Thickness(0, 0, 12, 0) });
        }
        UpdateLive(PositionMs);
    }

    /// <summary>Süren ölçüm: zaman çizgisinde açık aralık ve Sıradaki kayıt kutusunda geçen süre.</summary>
    private void UpdateLive(long now)
    {
        double total = timeline.Maximum, width = segments.ActualWidth;
        if (anchor is not long start)
        {
            liveSegment.Visibility = Visibility.Collapsed;
            nowLive.Visibility = Visibility.Collapsed;
            return;
        }
        nowLive.Visibility = Visibility.Visible;
        nowLive.Text = now > start ? L.T("now.live", ("s", L.Num3((now - start) / 1000d))) : L.T("now.liveBehind");
        if (total > 0 && width > 0 && now > start)
        {
            liveSegment.Visibility = Visibility.Visible;
            liveSegment.Width = Math.Max(1, (now - start) / total * width);
            Canvas.SetLeft(liveSegment, start / total * width);
        }
        else liveSegment.Visibility = Visibility.Collapsed;
    }

    // ------------------------------------------------------------ adım listesi

    private void RenderSteps()
    {
        stepsPanel.Children.Clear();
        var list = StepList;
        flow.Clamp(list.Count);
        if (list.Count == 0) stepsPanel.Children.Add(Ui.Hint(L.T("steps.empty")));
        for (int i = 0; i < list.Count; i++)
        {
            var st = list[i];
            bool on = i == flow.Index;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border
            {
                Background = on ? Ui.Brush("#2E5168") : Ui.Brush("#E3EAEE"), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock { Text = i < 9 ? (i + 1).ToString() : "·", FontSize = 11.5, Foreground = on ? Brushes.White : Ui.Soft }
            });
            content.Children.Add(new TextBlock { Text = st.Name, Foreground = on ? Brushes.White : Ui.Ink });
            if (st.Classification.Length > 0)
                content.Children.Add(new Border
                {
                    BorderBrush = Ui.ClassBrush(st.Classification), BorderThickness = new Thickness(3, 0, 0, 0), Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(4, 0, 0, 0),
                    Child = new TextBlock { Text = st.Classification, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = on ? Brushes.White : Ui.Ink }
                });
            // Düz düğme: seçili adım koyu zeminde (ToggleButton'ın sistem vurgusu açık mavide beyaz yazıyı okunmaz yapıyordu).
            var chip = new Button
            {
                Content = content, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 6),
                Background = on ? Ui.Accent : Ui.Paper, BorderBrush = on ? Ui.Accent : Ui.Line, ToolTip = st.Note
            };
            int index = i;
            chip.Click += (_, _) => Guard(() => SelectStep(index));
            chip.SetValue(AutomationProperties.NameProperty, st.Name + (on ? " ✓" : ""));
            stepsPanel.Children.Add(chip);
        }
        var current_ = CurrentStep;
        stepTools.Visibility = current_ is null ? Visibility.Collapsed : Visibility.Visible;
        if (current_ is not null)
        {
            suppressSelection = true;
            Ui.SetClass(stepClass, current_.Classification);
            suppressSelection = false;
            stepNote.Text = current_.Note ?? "";
        }
        stepLeft.IsEnabled = flow.Index > 0;
        stepRight.IsEnabled = flow.Index < list.Count - 1;
        UpdateNow();
    }

    private void UpdateNow()
    {
        var st = CurrentStep;
        string cycle = cycleBox.Text.Trim().Length > 0 ? cycleBox.Text.Trim() : "?";
        nowStep.Text = st is null ? L.T("now.none") : L.T("now.value", ("step", st.Name), ("cycle", cycle)) + (excludedNext.IsChecked == true ? " · " + L.T("now.excluded") : "");
        nowNote.Text = st?.Note is string note ? L.T("now.note", ("note", note)) : "";
        nowNote.Visibility = st?.Note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Elle adım seçimi. Önceki adım hatırlanır: araya giren kapsam dışı kayıttan sonra sıra oraya döner.</summary>
    private void SelectStep(int index)
    {
        SaveStepNote();
        flow.Select(index, StepList.Count);
        RenderSteps();
    }

    private void SaveSteps()
    {
        if (current == null) return;
        current.Steps = StudyRules.CleanSteps(current.Steps);
        RenderSteps();
        Save();
    }

    private void AddStep()
    {
        if (current == null) return;
        string name = newStep.Text.Trim();
        if (name.Length == 0) throw Error("msg.stepEmpty");
        if (StepList.Any(x => x.Name == name)) throw Error("msg.stepExists", ("step", name));
        SaveStepNote();
        StepList.Add(new StepDef { Name = name, Classification = Ui.ClassOf(newStepClass) });
        newStep.Text = "";
        // Yeni adım seçilir; araya giren bir kayıtsa (kapsam dışı) sonra önceki adıma dönülür.
        flow.Added(StepList.Count);
        SaveSteps();
        if (ready && anchor is null) SetStatus(L.T("status.ready"));
    }

    private void MoveStep(int delta)
    {
        var list = StepList;
        int i = flow.Index, j = i + delta;
        if (j < 0 || j >= list.Count) return;
        (list[i], list[j]) = (list[j], list[i]);
        flow.Index = j;
        SaveSteps();
    }

    private void ChangeStepClass()
    {
        if (current == null || CurrentStep is not StepDef st) return;
        string cls = Ui.ClassOf(stepClass);
        if (st.Classification == cls) return;
        st.Classification = cls;
        SaveSteps();
        var others = current.Observations.Where(o => o.Step == st.Name && o.Classification != cls).ToList();
        if (others.Count > 0 && Ask(L.T("msg.stepClassExisting", ("n", others.Count), ("step", st.Name), ("cls", cls.Length > 0 ? cls : "—")), L.T("title.stepClass")))
        {
            foreach (var o in others) o.Classification = cls;
            RefreshObservations();
            Save();
        }
    }

    private void SaveStepNote()
    {
        if (current == null || CurrentStep is not StepDef st) return;
        var note = StudyRules.CleanNote(stepNote.Text);
        if (note == st.Note) return;
        st.Note = note;
        UpdateNow();
        Save();
    }

    private void RenameStep()
    {
        if (current == null || CurrentStep is not StepDef st) return;
        var next = Ui.Ask(this, L.T("btn.stepRename").TrimEnd('…'), L.T("msg.stepRename", ("step", st.Name)), st.Name)?.Trim();
        if (string.IsNullOrEmpty(next) || next == st.Name) return;
        if (StepList.Any(x => x.Name == next)) throw Error("msg.stepExists", ("step", next));
        string old = st.Name;
        st.Name = next;
        foreach (var o in current.Observations.Where(o => o.Step == old)) o.Step = next;
        if (current.Frequency.Remove(old, out int every)) current.Frequency[next] = every;
        if (current.StepPace.Remove(old, out double pace)) current.StepPace[next] = pace;
        RefreshObservations();
        SaveSteps();
    }

    private void RemoveStep()
    {
        if (current == null || CurrentStep is not StepDef st) return;
        int used = current.Observations.Count(o => o.Step == st.Name);
        if (!Ask(used > 0 ? L.T("msg.stepRemoveUsed", ("step", st.Name), ("n", used)) : L.T("msg.stepRemove", ("step", st.Name)), L.T("title.stepRemove"))) return;
        StepList.RemoveAt(flow.Index);
        SaveSteps();
    }

    // ------------------------------------------------------------ çevrim

    private void SetCycle(int n)
    {
        cycleBox.Text = Math.Max(1, n).ToString();
        UpdateNow();
    }

    private int CycleValue() => PositiveInt(cycleBox.Text, "name.cycle");

    // ------------------------------------------------------------ ölçüm

    private void StopMeasure()
    {
        anchor = null;
        measureButton.Content = L.T("btn.start");
        UpdateLive(0);
    }

    private void Measure()
    {
        RequireVideo();
        if (CurrentStep is null) throw Error("msg.noSteps");
        long now = PositionMs;
        if (anchor is not long start)
        {
            anchor = now;
            measureButton.Content = L.T("btn.measure");
            SetStatus(L.T("status.started", ("s", L.Num3(now / 1000d))));
            UpdateLive(now);
            return;
        }
        if (now <= start) throw Error("msg.behindStart", ("s", L.Num3(start / 1000d)));
        bool excluded = excludedNext.IsChecked == true;
        AddObservation(start, now);
        anchor = now;
        AfterRecord(excluded);
    }

    /// <summary>Kayıttan sonra sıra: kapsam dışı kayıt sırayı ilerletmez; normal kayıtta otomatik ilerleme açıksa sıradaki adım.</summary>
    private void AfterRecord(bool excluded)
    {
        flow.Cycle = CycleValue();
        flow.AfterRecord(excluded, autoAdvance.IsChecked == true, StepList.Count);
        SetCycle(flow.Cycle);
        RenderSteps();
    }

    private void MarkStart()
    {
        RequireVideo();
        selectedStart = PositionMs;
        SetStatus(L.T("status.markStart", ("s", L.Num3(selectedStart.Value / 1000d))));
    }

    private void AddMarked()
    {
        RequireVideo();
        if (selectedStart is not long start) throw Error("msg.markFirst");
        if (CurrentStep is null) throw Error("msg.noSteps");
        AddObservation(start, PositionMs);
        selectedStart = null;
    }

    private void AddObservation(long start, long end)
    {
        if (end <= start) throw Error("msg.endAfterStart");
        var st = CurrentStep!;
        int cycle = CycleValue();
        var row = new Observation { StartMs = start, EndMs = end, Step = st.Name, Classification = st.Classification, Excluded = excludedNext.IsChecked == true, Cycle = cycle };
        if (current!.Observations.Any(x => x.StartMs == start && x.EndMs == end && x.Cycle == row.Cycle && x.Step == row.Step && x.Excluded == row.Excluded))
            throw Error("msg.duplicate");
        current.Observations.Add(row);
        undo.Add((row, flow.Index, cycle));
        // "Kapsam dışı" yalnız bir kayıt içindir.
        excludedNext.IsChecked = false;
        Save();
        RefreshObservations();
        UpdateNow();
        SetStatus(L.T(row.Excluded ? "status.savedExcluded" : "status.saved", ("step", row.Step), ("s", L.Num3(row.Seconds)), ("cycle", cycle)));
    }

    /// <summary>Son eklenen kaydı siler; adım, çevrim ve ölçüm başlangıcı o kaydın öncesine döner.</summary>
    private void UndoLast()
    {
        if (current == null) return;
        (Observation Row, int StepIndex, int Cycle)? last = null;
        while (undo.Count > 0)
        {
            var item = undo[^1];
            undo.RemoveAt(undo.Count - 1);
            if (current.Observations.Contains(item.Row)) { last = item; break; }
        }
        if (last is null) { SetStatus(L.T("status.nothingToUndo")); return; }
        var (row, stepIndex, cycle) = last.Value;
        current.Observations.Remove(row);
        if (selectedRow == row) SelectRow(null);
        flow.Index = stepIndex;
        SetCycle(cycle);
        if (anchor == row.EndMs) anchor = row.StartMs;
        RenderSteps();
        Save();
        RefreshObservations();
        SetStatus(L.T("status.undone", ("step", row.Step), ("s", L.Num3(row.Seconds))));
    }

    // ------------------------------------------------------------ kayıtlar

    private void SelectRow(Observation? row)
    {
        selectedRow = row;
        rowEditor.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
        if (row is null) return;
        rowStep.Text = row.Step;
        rowCycle.Text = row.Cycle.ToString();
        Ui.SetClass(rowClass, row.Classification);
        rowExcluded.IsChecked = row.Excluded;
        rowNote.Text = row.Note ?? "";
    }

    private void EditRow()
    {
        if (selectedRow is not Observation row || current == null) return;
        string name = rowStep.Text.Trim();
        if (name.Length == 0) throw Error("msg.stepEmpty");
        int cycle = PositiveInt(rowCycle.Text, "name.cycle");
        row.Cycle = cycle;
        row.Step = name;
        row.Classification = Ui.ClassOf(rowClass);
        row.Excluded = rowExcluded.IsChecked == true;
        row.Note = rowNote.Text;
        if (!StepList.Any(x => x.Name == name)) { StepList.Add(new StepDef { Name = name, Classification = row.Classification }); RenderSteps(); }
        Save();
        RefreshObservations();
        SetStatus(L.T("status.rowUpdated", ("step", row.Step)));
    }

    private void BulkClassify()
    {
        if (selectedRow is not Observation row || current == null) return;
        string cls = Ui.ClassOf(rowClass);
        foreach (var item in current.Observations.Where(x => x.Step == row.Step)) item.Classification = cls;
        if (StepList.FirstOrDefault(x => x.Name == row.Step) is StepDef st) st.Classification = cls;
        RenderSteps();
        Save();
        RefreshObservations();
    }

    private void DeleteRow()
    {
        if (selectedRow is not Observation row || current == null) return;
        if (!Ask(L.T("msg.deleteRowConfirm"), L.T("title.deleteRow"))) return;
        current.Observations.Remove(row);
        SelectRow(null);
        Save();
        RefreshObservations();
    }

    /// <summary>Kayıt tablosu süzgeci: adım ve çevrim (yalnız görünümü daraltır, hesaplara etkisi yok).</summary>
    private static void FillFilter(ComboBox combo, IReadOnlyList<string> values)
    {
        string? keep = combo.SelectedIndex > 0 ? combo.SelectedItem as string : null;
        combo.Items.Clear();
        combo.Items.Add(L.T("filter.all"));
        foreach (var v in values) combo.Items.Add(v);
        combo.SelectedIndex = keep != null && values.Contains(keep) ? values.ToList().IndexOf(keep) + 1 : 0;
    }

    private void RefreshObservations()
    {
        var all = current?.Observations.OrderBy(x => x.StartMs).ThenBy(x => x.EndMs).ToList() ?? new List<Observation>();
        fillingFilters = true;
        FillFilter(filterStep, current == null ? new List<string>() : StudyRules.StepOrder(current).Where(n => all.Any(o => o.Step == n)).ToList());
        FillFilter(filterCycle, all.Select(o => o.Cycle).Distinct().Order().Select(c => c.ToString()).ToList());
        fillingFilters = false;
        string? fs = filterStep.SelectedIndex > 0 ? filterStep.SelectedItem as string : null;
        string? fc = filterCycle.SelectedIndex > 0 ? filterCycle.SelectedItem as string : null;
        var rows = all.Where(o => (fs == null || o.Step == fs) && (fc == null || o.Cycle.ToString() == fc)).ToList();
        obsCount.Text = all.Count == 0 ? L.T("obs.empty")
            : rows.Count == all.Count ? L.T("filter.countAll", ("total", all.Count)) : L.T("filter.count", ("n", rows.Count), ("total", all.Count));
        var view = rows.Select(o => new ObservationRow(o, L.Clock(o.StartMs), L.Clock(o.EndMs), L.Num3(o.Seconds), o.Step, o.Classification,
            L.T(o.Excluded ? "yes" : "no"), o.Cycle.ToString(), o.Note ?? "")).ToList();
        suppressSelection = true;
        observations.ItemsSource = view;
        observations.SelectedItem = view.FirstOrDefault(v => v.Source == selectedRow);
        suppressSelection = false;
        RenderSegments();
    }
}
