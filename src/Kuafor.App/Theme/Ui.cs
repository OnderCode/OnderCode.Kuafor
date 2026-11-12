using Microsoft.Maui.Controls.Shapes;
using Kuafor.Core;

namespace Kuafor.App.Theme;

/// <summary>Arayüz yapı taşları: metin, ikon, kart, rozet, avatar, düğme. Bütün ekranlar bunlarla kurulur.</summary>
public static class Ui
{
    // ---------- Renk yardımcıları ----------
    public static T Fg<T>(this T label, (Color L, Color D) c) where T : Label { label.SetAppThemeColor(Label.TextColorProperty, c.L, c.D); return label; }
    public static T Bg<T>(this T v, (Color L, Color D) c) where T : VisualElement { v.SetAppThemeColor(VisualElement.BackgroundColorProperty, c.L, c.D); return v; }
    public static Border Stroke(this Border b, (Color L, Color D) c, double thickness = 1)
    {
        b.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(c.L), new SolidColorBrush(c.D));
        b.StrokeThickness = thickness;
        return b;
    }

    // ---------- Metin ----------
    public static Label Text(string? text, double size = 15, string font = F.Regular, (Color L, Color D)? color = null, int lines = 0)
    {
        var l = new Label { Text = text, FontSize = size, FontFamily = font, LineBreakMode = lines == 1 ? LineBreakMode.TailTruncation : LineBreakMode.WordWrap };
        if (lines > 0) l.MaxLines = lines;
        return l.Fg(color ?? Pal.Text);
    }

    public static Label Title(string text, double size = 26) => Text(text, size, F.Bold);
    public static Label Heading(string text, double size = 17) => Text(text, size, F.SemiBold);
    public static Label Muted(string? text, double size = 13, int lines = 0) => Text(text, size, F.Regular, Pal.Muted, lines);

    public static Label Icon(string glyph, double size = 22, (Color L, Color D)? color = null) =>
        new Label { Text = glyph, FontFamily = Ic.Font, FontSize = size, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }.Fg(color ?? Pal.Text);

    public static Label Icon(string glyph, double size, Color color) =>
        new() { Text = glyph, FontFamily = Ic.Font, FontSize = size, TextColor = color, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };

    public static FontImageSource Glyph(string glyph, Color color, double size = 22) => new() { FontFamily = Ic.Font, Glyph = glyph, Color = color, Size = size };

    // ---------- Kutular ----------
    public static Border Card(View content, double padding = 16, double radius = 20, bool shadow = true)
    {
        var b = new Border
        {
            Content = content,
            Padding = padding,
            StrokeShape = new RoundRectangle { CornerRadius = radius },
        }.Bg(Pal.Surface).Stroke(Pal.Border);
        if (shadow) b.Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.05f, Radius = 18, Offset = new Point(0, 6) };
        return b;
    }

    public static Border Box(View content, (Color L, Color D) bg, double padding = 12, double radius = 14) =>
        new Border { Content = content, Padding = padding, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = radius } }.Bg(bg);

    public static Border Box(View content, Color bg, double padding = 12, double radius = 14) =>
        new() { Content = content, Padding = padding, StrokeThickness = 0, BackgroundColor = bg, StrokeShape = new RoundRectangle { CornerRadius = radius } };

    /// <summary>Renkli yumuşak rozet: "Bekleniyor", "Kart"...</summary>
    public static Border Pill(string text, Color color, double size = 12, string? glyph = null)
    {
        var row = new HorizontalStackLayout { Spacing = 4 };
        if (glyph is not null) row.Add(Icon(glyph, size + 1, color));
        row.Add(new Label { Text = text, FontSize = size, FontFamily = F.SemiBold, TextColor = color, VerticalTextAlignment = TextAlignment.Center });
        return new Border
        {
            Content = row, Padding = new Thickness(10, 4), StrokeThickness = 0, BackgroundColor = Pal.Soft(color),
            StrokeShape = new RoundRectangle { CornerRadius = 20 }, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center,
        };
    }

    public static Color StatusColor(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Planned => Pal.Now(Pal.Primary),
        AppointmentStatus.Arrived => Pal.Now(Pal.Warning),
        AppointmentStatus.Done => Pal.Now(Pal.Success),
        AppointmentStatus.NoShow => Pal.Now(Pal.Danger),
        _ => Pal.Now(Pal.Muted),
    };

    public static Border StatusPill(AppointmentStatus s) => Pill(Texts.Of(s), StatusColor(s), 11);

    /// <summary>Baş harfli yuvarlak avatar.</summary>
    public static Border Avatar(string name, double size = 44, bool favorite = false)
    {
        var color = Pal.AvatarFor(name);
        var grid = new Grid
        {
            new Label
            {
                Text = Texts.Initials(name), FontFamily = F.SemiBold, FontSize = size * 0.36, TextColor = color,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            },
        };
        var avatar = new Border
        {
            WidthRequest = size, HeightRequest = size, StrokeThickness = 0, BackgroundColor = Pal.Soft(color, Pal.IsDark ? 0.28f : 0.16f),
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 }, Content = grid, VerticalOptions = LayoutOptions.Center,
        };
        if (!favorite) return avatar;
        var star = Icon(Ic.Star, size * 0.3, Pal.Now(Pal.Warning));
        star.HorizontalOptions = LayoutOptions.End; star.VerticalOptions = LayoutOptions.End;
        star.Margin = new Thickness(0, 0, -2, -2);
        return new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, Content = new Grid { avatar, star }, VerticalOptions = LayoutOptions.Center };
    }

    /// <summary>Yuvarlak ikon düğmesi.</summary>
    public static Border IconButton(string glyph, (Color L, Color D) color, (Color L, Color D)? bg = null, double size = 44, Func<Task>? tap = null)
    {
        var b = new Border
        {
            WidthRequest = size, HeightRequest = size, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            Content = Icon(glyph, size * 0.48, color), VerticalOptions = LayoutOptions.Center,
        }.Bg(bg ?? Pal.SurfaceAlt);
        if (tap is not null) b.OnTap(tap);
        return b;
    }

    /// <summary>Dolgu düğme (birincil) ya da çerçeveli düğme.</summary>
    public static Border Button(string text, string? glyph = null, bool primary = true, Func<Task>? tap = null, (Color L, Color D)? color = null, double height = 52)
    {
        var accent = color ?? Pal.Primary;
        var row = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        if (glyph is not null) row.Add(primary ? Icon(glyph, 20, Pal.OnPrimary) : Icon(glyph, 20, accent));
        var label = Text(text, 15, F.SemiBold, primary ? Pal.OnPrimary : accent);
        label.VerticalTextAlignment = TextAlignment.Center;
        row.Add(label);
        var b = new Border { HeightRequest = height, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = row, Padding = new Thickness(18, 0) };
        if (primary)
        {
            b.StrokeThickness = 0;
            b.Bg(accent);
            b.Shadow = new Shadow { Brush = accent.L, Opacity = 0.28f, Radius = 16, Offset = new Point(0, 6) };
        }
        else b.Bg(Pal.Surface).Stroke(Pal.Border);
        if (tap is not null) b.OnTap(tap);
        return b;
    }

    public static BoxView Divider() => new BoxView { HeightRequest = 1, Margin = new Thickness(0, 2) }.Bg(Pal.Border);

    /// <summary>Bölüm başlığı + isteğe bağlı sağ bağlantı ("Tümü").</summary>
    public static Grid Section(string title, string? action = null, Func<Task>? tap = null, string? count = null)
    {
        var g = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], Margin = new Thickness(4, 8, 4, 0) };
        var left = new HorizontalStackLayout { Spacing = 8 };
        left.Add(Heading(title));
        if (count is not null) left.Add(Pill(count, Pal.Now(Pal.Primary), 11));
        g.Add(left);
        if (action is not null)
        {
            var a = Text(action, 14, F.SemiBold, Pal.Primary);
            a.VerticalTextAlignment = TextAlignment.Center;
            if (tap is not null) a.OnTap(tap);
            g.Add(a, 1);
        }
        return g;
    }

    /// <summary>Dokunma: hafif basma animasyonu + eylem.</summary>
    public static T OnTap<T>(this T view, Func<Task> action) where T : View
    {
        var busy = false;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (busy) return;
            busy = true;
            try
            {
                await view.ScaleToAsync(0.97, 60, Easing.CubicOut);
                await view.ScaleToAsync(1, 90, Easing.CubicOut);
                await action();
            }
            finally { busy = false; }
        };
        view.GestureRecognizers.Add(tap);
        return view;
    }

    public static Grid Columns(params (View View, GridLength Width)[] cells)
    {
        var g = new Grid { ColumnSpacing = 12 };
        for (var i = 0; i < cells.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition(cells[i].Width));
            g.Add(cells[i].View, i);
        }
        return g;
    }

    public static VerticalStackLayout Stack(double spacing, params View[] views)
    {
        var s = new VerticalStackLayout { Spacing = spacing };
        foreach (var v in views) s.Add(v);
        return s;
    }

    /// <summary>Boş durum: ikon + başlık + açıklama.</summary>
    public static View Empty(string glyph, string title, string text)
    {
        var s = Stack(6,
            Icon(glyph, 40, Pal.Faint),
            Text(title, 16, F.SemiBold),
            Muted(text, 13));
        foreach (var c in s.Children.OfType<Label>()) c.HorizontalTextAlignment = TextAlignment.Center;
        s.Padding = new Thickness(24, 28);
        return s;
    }

    /// <summary>Etiketli giriş alanı.</summary>
    public static (View View, Entry Entry) Field(string label, string? value = null, string? placeholder = null, Keyboard? keyboard = null, string? glyph = null)
    {
        var entry = new Entry { Text = value, Placeholder = placeholder, FontFamily = F.Regular, FontSize = 16, Keyboard = keyboard ?? Keyboard.Default, VerticalOptions = LayoutOptions.Center };
        entry.SetAppThemeColor(Entry.TextColorProperty, Pal.Text.L, Pal.Text.D);
        entry.SetAppThemeColor(Entry.PlaceholderColorProperty, Pal.Faint.L, Pal.Faint.D);
        View inner = glyph is null ? entry : Columns((Icon(glyph, 20, Pal.Muted), GridLength.Auto), (entry, GridLength.Star));
        var box = new Border { Content = inner, Padding = new Thickness(14, 2), StrokeShape = new RoundRectangle { CornerRadius = 14 }, HeightRequest = 54 }.Bg(Pal.Surface).Stroke(Pal.Border);
        return (Stack(6, Text(label, 13, F.Medium, Pal.Muted), box), entry);
    }

    public static (View View, Editor Editor) Area(string label, string? value = null, string? placeholder = null, double height = 96)
    {
        var editor = new Editor { Text = value, Placeholder = placeholder, FontFamily = F.Regular, FontSize = 15, HeightRequest = height, AutoSize = EditorAutoSizeOption.Disabled };
        editor.SetAppThemeColor(Editor.TextColorProperty, Pal.Text.L, Pal.Text.D);
        editor.SetAppThemeColor(Editor.PlaceholderColorProperty, Pal.Faint.L, Pal.Faint.D);
        var box = new Border { Content = editor, Padding = new Thickness(10, 4), StrokeShape = new RoundRectangle { CornerRadius = 14 } }.Bg(Pal.Surface).Stroke(Pal.Border);
        return (Stack(6, Text(label, 13, F.Medium, Pal.Muted), box), editor);
    }

    /// <summary>Liste satırı: sol ikon kutusu, başlık, alt yazı, sağda değer/ok.</summary>
    public static Grid ListRow(string glyph, (Color L, Color D) color, string title, string? subtitle = null, View? trailing = null, bool chevron = true)
    {
        var iconBox = Box(Icon(glyph, 20, color), Pal.Soft(color.L, 0.12f), 0, 12);
        iconBox.WidthRequest = iconBox.HeightRequest = 40;
        var texts = Stack(1, Text(title, 15, F.Medium, lines: 1));
        if (subtitle is not null) texts.Add(Muted(subtitle, 12.5, 2));
        texts.VerticalOptions = LayoutOptions.Center;
        var g = Columns((iconBox, GridLength.Auto), (texts, GridLength.Star));
        g.Padding = new Thickness(0, 8);
        if (trailing is not null) { g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); g.Add(trailing, 2); }
        if (chevron) { g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); g.Add(Icon(Ic.ChevronRight, 20, Pal.Faint), g.ColumnDefinitions.Count - 1); }
        return g;
    }

    /// <summary>Oransal ilerleme çubuğu (0–1).</summary>
    public static Grid Progress(double ratio, Color color)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        var g = new Grid { HeightRequest = 6, ColumnDefinitions = [new(new GridLength(Math.Max(ratio, 0.0001), GridUnitType.Star)), new(new GridLength(Math.Max(1 - ratio, 0.0001), GridUnitType.Star))] };
        var track = new BoxView { CornerRadius = 3 }.Bg(Pal.SurfaceAlt);
        g.Add(track, 0); Grid.SetColumnSpan(track, 2);
        g.Add(new BoxView { CornerRadius = 3, Color = color }, 0);
        return g;
    }
}
