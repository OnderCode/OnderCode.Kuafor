using Kuafor.App.Theme;
using Kuafor.Core;
using Microsoft.Maui.Controls.Shapes;

namespace Kuafor.App.Pages;

/// <summary>Birden fazla ekranda kullanılan satır ve kartlar.</summary>
public static class Parts
{
    /// <summary>Randevu satırı: saat sütunu, renkli şerit, müşteri, hizmetler, tutar, durum.</summary>
    public static View AppointmentRow(AppointmentView v, Func<Task> tap, bool showDate = false)
    {
        var a = v.Appointment;
        var color = Pal.Hex(v.ColorHex);
        var faded = a.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow;

        var time = Ui.Stack(0,
            Ui.Text(showDate ? a.Start.ToString("d MMM", Texts.Tr) : Texts.Time(a.Start), 15, F.SemiBold),
            Ui.Muted(showDate ? Texts.Time(a.Start) : Texts.Time(a.End), 12));
        time.WidthRequest = 52;
        time.VerticalOptions = LayoutOptions.Center;

        var bar = new BoxView { WidthRequest = 4, CornerRadius = 2, Color = faded ? Pal.Now(Pal.Faint) : color, Margin = new Thickness(0, 2) };

        var name = Ui.Text(v.Customer.Name, 15.5, F.SemiBold, lines: 1);
        if (faded) name.TextDecorations = TextDecorations.Strikethrough;
        var info = Ui.Stack(3, name, Ui.Muted(v.ServicesText, 13, 1));
        info.VerticalOptions = LayoutOptions.Center;

        var right = Ui.Stack(5, Ui.Text(Texts.Money(a.Status == AppointmentStatus.Done ? a.Paid : a.Price), 14, F.SemiBold), Ui.StatusPill(a.Status));
        right.HorizontalOptions = LayoutOptions.End;
        foreach (var c in right.Children.OfType<View>()) c.HorizontalOptions = LayoutOptions.End;

        var g = Ui.Columns((time, GridLength.Auto), (bar, GridLength.Auto), (info, GridLength.Star), (right, GridLength.Auto));
        g.ColumnSpacing = 12;
        var card = Ui.Card(g, 14, 18);
        if (faded) card.Opacity = 0.65;
        return card.OnTap(tap);
    }

    /// <summary>Gün içindeki boşluk satırı: "14:00 – 15:30 boş · Randevu ekle".</summary>
    public static View GapRow(TimeRange gap, Func<Task> tap)
    {
        var row = Ui.Columns(
            (Ui.Icon(Ic.Plus, 18, Pal.Primary), GridLength.Auto),
            (Ui.Text($"{Texts.Time(gap.Start)} – {Texts.Time(gap.End)}  ·  {Texts.Duration(gap.Minutes)} boş", 13.5, F.Medium, Pal.Primary), GridLength.Star),
            (Ui.Text("Ekle", 13.5, F.SemiBold, Pal.Primary), GridLength.Auto));
        row.ColumnSpacing = 8;
        var box = new Border
        {
            Content = row, Padding = new Thickness(14, 11), StrokeDashArray = [4, 3], StrokeShape = new RoundRectangle { CornerRadius = 16 },
        }.Stroke(Pal.Primary, 1.2).Bg(Pal.PrimarySoft);
        box.Opacity = 0.9;
        return box.OnTap(tap);
    }

    /// <summary>Küçük istatistik kutusu.</summary>
    public static Border Stat(string glyph, (Color L, Color D) color, string value, string label, string? note = null)
    {
        var iconBox = Ui.Box(Ui.Icon(glyph, 18, color), Pal.Soft(color.L, 0.13f), 0, 10);
        iconBox.WidthRequest = iconBox.HeightRequest = 34;
        iconBox.HorizontalOptions = LayoutOptions.Start;
        var s = Ui.Stack(2, iconBox, new BoxView { HeightRequest = 6, Color = Colors.Transparent }, Ui.Text(value, 19, F.Bold, lines: 1), Ui.Muted(label, 12, 1));
        if (note is not null) s.Add(Ui.Text(note, 11.5, F.Medium, color, 1));
        return Ui.Card(s, 14, 18);
    }

    /// <summary>Müşteri satırı (liste).</summary>
    public static View CustomerRow(Customer c, CustomerStats? s, Func<Task> tap, View? trailing = null)
    {
        var now = Kuafor.Core.Clock.Now;
        var sub = s is null || s.Visits == 0 ? Phone.Format(c.Phone)
            : $"{s.Visits} ziyaret  ·  son {Texts.Ago(s.LastVisit!.Value, now)}";
        var texts = Ui.Stack(2, Ui.Text(c.Name, 15.5, F.SemiBold, lines: 1), Ui.Muted(sub, 12.5, 1));
        texts.VerticalOptions = LayoutOptions.Center;
        trailing ??= s is { Visits: > 0 } ? Ui.Text(Texts.MoneyShort(s.Spent), 14, F.SemiBold, Pal.Muted) : null;
        var g = Ui.Columns((Ui.Avatar(c.Name, 46, c.IsFavorite), GridLength.Auto), (texts, GridLength.Star));
        if (trailing is not null) { trailing.VerticalOptions = LayoutOptions.Center; g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); g.Add(trailing, 2); }
        g.Padding = new Thickness(14, 10);
        var card = new Border { Content = g, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 18 } }.Bg(Pal.Surface);
        return card.OnTap(tap);
    }

    /// <summary>Gradyanlı vitrin kartı (ana sayfa, kasa).</summary>
    public static Border Hero(View content)
    {
        var b = new Border
        {
            Content = content, Padding = 18, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 24 },
            Background = new LinearGradientBrush([new GradientStop(Pal.C(0x7C3AED), 0), new GradientStop(Pal.C(0xA21CAF), 0.6f), new GradientStop(Pal.C(0xDB2777), 1)], new Point(0, 0), new Point(1, 1)),
            Shadow = new Shadow { Brush = Pal.Violet, Opacity = 0.3f, Radius = 22, Offset = new Point(0, 10) },
        };
        return b;
    }

    public static Label OnHero(string text, double size, string font = F.Regular, double opacity = 1) =>
        new() { Text = text, FontSize = size, FontFamily = font, TextColor = Colors.White.WithAlpha((float)opacity), LineBreakMode = LineBreakMode.TailTruncation };

    /// <summary>Gradyan kart üzerinde yarı saydam düğme.</summary>
    public static Border HeroButton(string glyph, string text, Func<Task> tap)
    {
        var row = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        row.Add(Ui.Icon(glyph, 18, Colors.White));
        row.Add(new Label { Text = text, FontFamily = F.SemiBold, FontSize = 13.5, TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center });
        return new Border
        {
            Content = row, HeightRequest = 42, StrokeThickness = 0, BackgroundColor = Colors.White.WithAlpha(0.2f),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
        }.OnTap(tap);
    }
}
