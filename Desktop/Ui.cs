using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace VideoZamanEtudu;

/// <summary>Kodla kurulan arayüzün küçük yapı taşları (renkler, alanlar, satırlar, kutular, tablolar).</summary>
internal static class Ui
{
    public static readonly Brush Ink = Brush("#132637");
    public static readonly Brush Soft = Brush("#5B6B78");
    public static readonly Brush Line = Brush("#D3DDE5");
    public static readonly Brush Accent = Brush("#17364A");
    public static readonly Brush Danger = Brush("#B42318");
    public static readonly Brush Paper = Brushes.White;
    public static readonly Brush NoteBack = Brush("#FFF8E1");
    public static readonly Brush NoteLine = Brush("#F1D88A");
    public static readonly Brush Selected = Brush("#D7EAF3");

    /// <summary>Sınıf renkleri (tarayıcıdaki charts.js ile aynı; renk körlüğü denetiminden geçti).</summary>
    public static readonly Dictionary<string, Color> ClassColors = new()
    {
        ["VA"] = (Color)ColorConverter.ConvertFromString("#1baf7a"),
        ["SVA"] = (Color)ColorConverter.ConvertFromString("#2a78d6"),
        ["NVA"] = (Color)ColorConverter.ConvertFromString("#eb6834"),
        [""] = (Color)ColorConverter.ConvertFromString("#8a979f")
    };
    public static Brush ClassBrush(string? cls) => new SolidColorBrush(ClassColors.TryGetValue(cls ?? "", out var c) ? c : ClassColors[""]);

    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public static TextBlock Text(string text, double size = 14, bool bold = false, Brush? color = null) => new()
    {
        Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 10, 6)
    };

    public static TextBlock Hint(string text) => Text(text, 12.5, false, Soft);

    public static TextBlock Title(string text) => new()
    {
        Text = text, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ink, Margin = new Thickness(0, 12, 0, 6)
    };

    /// <summary>"<b>…</b>" kalın; diğer etiketler atılır.</summary>
    public static TextBlock Rich(string html, double size = 14)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, Foreground = Ink, Margin = new Thickness(0, 2, 10, 6) };
        foreach (var part in Regex.Split(html, "(<b>.*?</b>)"))
        {
            if (part.StartsWith("<b>")) block.Inlines.Add(new Bold(new Run(L.Strip(part))));
            else if (part.Length > 0) block.Inlines.Add(new Run(L.Strip(part)));
        }
        return block;
    }

    /// <summary>Açılır yardım: "<dt>başlık</dt><dd>açıklama</dd>" listesini okunur metne çevirir.</summary>
    public static Expander Help(string title, string html)
    {
        var body = new StackPanel { Margin = new Thickness(8, 4, 8, 6) };
        foreach (Match m in Regex.Matches(html, "<dt>(.*?)</dt><dd>(.*?)</dd>"))
        {
            body.Children.Add(new TextBlock { Text = L.Strip(m.Groups[1].Value), FontWeight = FontWeights.SemiBold, Foreground = Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            body.Children.Add(Rich(m.Groups[2].Value, 13));
        }
        return new Expander { Header = title, Content = body, Margin = new Thickness(0, 4, 0, 6), Foreground = Ink };
    }

    public static TextBox Input(double width, string value = "") =>
        new() { Width = width, Text = value, VerticalContentAlignment = VerticalAlignment.Center, MinHeight = 28 };

    /// <summary>Boşken gri yer tutucu metin gösteren giriş kutusu.</summary>
    public static Grid WithPlaceholder(TextBox box, string placeholder)
    {
        var hint = new TextBlock
        {
            Text = placeholder, Foreground = Soft, IsHitTestVisible = false, Margin = new Thickness(8, 0, 0, 4),
            VerticalAlignment = box.AcceptsReturn ? VerticalAlignment.Top : VerticalAlignment.Center, FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        if (box.AcceptsReturn) hint.Margin = new Thickness(8, 5, 0, 0);
        void Sync() => hint.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
        box.TextChanged += (_, _) => Sync();
        Sync();
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        grid.Children.Add(box);
        grid.Children.Add(hint);
        return grid;
    }

    public static Button Button(string text, Action<Action> guard, Action action, bool primary = false, bool danger = false, string? tip = null)
    {
        var button = new Button { Content = text, ToolTip = tip };
        if (primary) { button.Background = Accent; button.Foreground = Brushes.White; button.FontWeight = FontWeights.SemiBold; }
        if (danger) button.Foreground = Danger;
        button.Click += (_, _) => guard(action);
        return button;
    }

    public static StackPanel Field(string label, UIElement input)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 4) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = Soft, FontSize = 12.5, Margin = new Thickness(0, 2, 0, 3) });
        panel.Children.Add(input);
        return panel;
    }

    public static WrapPanel Row(params UIElement?[] elements)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
        foreach (var element in elements)
        {
            if (element is null) continue;
            if (element is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Bottom;
            panel.Children.Add(element);
        }
        return panel;
    }

    public static Border Box(UIElement child, Brush? background = null, Brush? line = null) => new()
    {
        Child = child, Background = background ?? Paper, BorderBrush = line ?? Line, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8)
    };

    public static StackPanel Stack(params UIElement?[] children)
    {
        var panel = new StackPanel();
        foreach (var child in children) if (child != null) panel.Children.Add(child);
        return panel;
    }

    /// <summary>Sınıf seçimi: görünen "—" boş sınıftır.</summary>
    public static ComboBox ClassCombo(double width = 90)
    {
        var combo = new ComboBox { Width = width, MinHeight = 28, VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var c in StudyRules.Classes) combo.Items.Add(new ComboBoxItem { Content = c == "" ? "—" : c, Tag = c });
        combo.SelectedIndex = 0;
        return combo;
    }
    public static string ClassOf(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
    public static void SetClass(ComboBox combo, string? cls) =>
        combo.SelectedIndex = Math.Max(0, Array.IndexOf(StudyRules.Classes, cls ?? ""));

    public static DataGrid Table() => new()
    {
        AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserSortColumns = false,
        CanUserReorderColumns = false, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
        AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(236, 245, 248)), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        HeadersVisibility = DataGridHeadersVisibility.Column, Background = Paper, BorderBrush = Line, RowHeaderWidth = 0
    };

    private static readonly Style NumberCell = MakeNumberCell();
    private static Style MakeNumberCell()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 6, 0)));
        style.Seal();
        return style;
    }

    /// <summary>Sütunlar: (başlık, özellik, sayı mı). Sayılar sağa yaslanır; değerler satır nesnesinde biçimlenmiş metindir.</summary>
    public static void Columns(DataGrid table, params (string Header, string Path, bool Number)[] columns)
    {
        table.Columns.Clear();
        foreach (var (header, path, number) in columns)
            table.Columns.Add(new DataGridTextColumn
            {
                Header = header, Binding = new System.Windows.Data.Binding(path),
                ElementStyle = number ? NumberCell : null
            });
    }

    /// <summary>Basit metin sorusu (WPF'te hazır giriş kutusu yok).</summary>
    public static string? Ask(Window owner, string title, string prompt, string value)
    {
        var box = new TextBox { Text = value, MinWidth = 360, Margin = new Thickness(0, 6, 0, 10) };
        var ok = new Button { Content = L.T("dialog.ok"), IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = L.T("dialog.cancel"), IsCancel = true, MinWidth = 80 };
        var window = new Window
        {
            Title = title, Owner = owner, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush("#F0F4F8"),
            Content = new StackPanel { Margin = new Thickness(16), Children = { Text(prompt), box, Row(ok, cancel) } }
        };
        ok.Click += (_, _) => { window.DialogResult = true; };
        window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return window.ShowDialog() == true ? box.Text : null;
    }

    public static bool IsTyping() => Keyboard.FocusedElement is TextBox or ComboBox or ComboBoxItem;
}
