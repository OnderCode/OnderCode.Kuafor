using Kuafor.App.Theme;
using Kuafor.Core;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace Kuafor.App.Pages;

/// <summary>
/// Takvim: üstte hafta şeridi (her günün doluluğu), altta seçili günün saat ızgarası.
/// Randevular süresi kadar yükseklikte blok olarak çizilir; boş bir yarım saate dokunmak o saate randevu açar.
/// </summary>
public sealed class CalendarPage : PageBase
{
    const double PxPerMin = 1.5;
    DateTime _day = Kuafor.Core.Clock.Today;
    DateTime _weekStart = StartOfWeek(Kuafor.Core.Clock.Today);

    protected override bool IsRoot => true;

    public CalendarPage()
    {
        AddFab(Ic.Plus, () => AppointmentEditPage.OpenAsync(Navigation, start: DefaultStart()));
    }

    /// <summary>Ekran görüntüsü için gün seçimi.</summary>
    public void SelectDay(DateTime day) { _day = day.Date; _weekStart = StartOfWeek(_day); }

    static DateTime StartOfWeek(DateTime d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).Date;

    DateTime DefaultStart() => _day == Kuafor.Core.Clock.Today ? SlotFinder.RoundUp(Kuafor.Core.Clock.Now) : _day.Add(AppData.Profile.Week[_day.DayOfWeek].Open);

    protected override async Task LoadAsync()
    {
        var week = AppData.Profile.Week;
        var today = Kuafor.Core.Clock.Today;
        var weekAppts = await AppData.Db.AppointmentsAsync(_weekStart, _weekStart.AddDays(6));
        var views = await AppData.Db.ViewsAsync(weekAppts.Where(a => a.Start.Date == _day));

        // Üst: ay adı + hafta gezinme
        var nav = new HorizontalStackLayout { Spacing = 8 };
        nav.Add(Ui.IconButton(Ic.ChevronLeft, Pal.Text, Pal.Surface, 40, async () => { _weekStart = _weekStart.AddDays(-7); _day = _weekStart.AddDays(_day == today ? 0 : ((int)_day.DayOfWeek + 6) % 7); await ReloadAsync(); }));
        nav.Add(Ui.IconButton(Ic.CalendarToday, Pal.Primary, Pal.PrimarySoft, 40, async () => { _day = today; _weekStart = StartOfWeek(today); await ReloadAsync(); }));
        nav.Add(Ui.IconButton(Ic.ChevronRight, Pal.Text, Pal.Surface, 40, async () => { _weekStart = _weekStart.AddDays(7); _day = _weekStart.AddDays(((int)_day.DayOfWeek + 6) % 7); await ReloadAsync(); }));
        var month = _weekStart.Month == _weekStart.AddDays(6).Month
            ? _weekStart.ToString("MMMM yyyy", Texts.Tr)
            : $"{_weekStart:MMM} – {_weekStart.AddDays(6).ToString("MMM yyyy", Texts.Tr)}";

        // Hafta şeridi
        var strip = new Grid { ColumnSpacing = 6 };
        for (var i = 0; i < 7; i++)
        {
            strip.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var d = _weekStart.AddDays(i);
            var dayAppts = weekAppts.Where(a => a.Start.Date == d && a.BlocksTime).ToList();
            var on = d == _day;
            var open = week[d.DayOfWeek].IsOpen;
            var dots = new HorizontalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.Center, HeightRequest = 6 };
            var n = Math.Min(4, (dayAppts.Count + 2) / 3);
            for (var k = 0; k < n; k++) dots.Add(new BoxView { WidthRequest = 5, HeightRequest = 5, CornerRadius = 2.5, Color = on ? Colors.White : Pal.Now(Pal.Primary) });
            var cell = Ui.Stack(3,
                new Label { Text = Texts.DayAbbr(d.DayOfWeek), FontSize = 11.5, FontFamily = F.Medium, HorizontalTextAlignment = TextAlignment.Center, TextColor = on ? Colors.White : Pal.Now(Pal.Muted) },
                new Label { Text = d.Day.ToString(), FontSize = 18, FontFamily = F.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = on ? Colors.White : d == today ? Pal.Now(Pal.Primary) : open ? Pal.Now(Pal.Text) : Pal.Now(Pal.Faint) },
                dots);
            var b = new Border { Content = cell, Padding = new Thickness(0, 9, 0, 8), StrokeShape = new RoundRectangle { CornerRadius = 16 } };
            if (on) { b.StrokeThickness = 0; b.Background = new LinearGradientBrush([new GradientStop(Pal.Violet, 0), new GradientStop(Pal.Rose, 1)], new Point(0, 0), new Point(1, 1)); }
            else if (d == today) b.Bg(Pal.PrimarySoft).Stroke(Pal.Primary);
            else b.Bg(Pal.Surface).Stroke(Pal.Border);
            b.OnTap(async () => { _day = d; await ReloadAsync(); });
            strip.Add(b, i);
        }

        // Gün özeti
        var wd = week[_day.DayOfWeek];
        var active = views.Where(v => v.Appointment.BlocksTime).ToList();
        var occ = SlotFinder.Occupancy(_day, week, weekAppts.Where(a => a.Start.Date == _day));
        var revenue = active.Sum(v => v.Appointment.Status == AppointmentStatus.Done ? v.Appointment.Paid : v.Appointment.Price);
        var summary = Ui.Columns(
            (Ui.Stack(1, Ui.Text(Texts.DayLong(_day), 16, F.SemiBold), Ui.Muted(wd.IsOpen ? wd.Text : "Kapalı gün", 12.5)), GridLength.Star),
            (Ui.Stack(1, Ui.Text($"{active.Count} randevu", 14, F.SemiBold).Right(), Ui.Muted($"%{occ * 100:0} dolu · {Texts.MoneyShort(revenue)}", 12.5).Right()), GridLength.Auto));

        Show(
            Header("Takvim", month, trailing: nav),
            strip,
            summary,
            wd.IsOpen || active.Count > 0 ? Timeline(views, wd) : Ui.Empty(Ic.Coffee, "Bugün kapalı", "Çalışma saatlerini Ayarlar › Çalışma saatleri bölümünden değiştirebilirsiniz."));
    }

    static void Place(AbsoluteLayout canvas, View v, Rect r)
    {
        canvas.Add(v);
        AbsoluteLayout.SetLayoutBounds(v, r);
        AbsoluteLayout.SetLayoutFlags(v, AbsoluteLayoutFlags.None);
    }

    View Timeline(List<AppointmentView> views, WorkDay wd)
    {
        var open = wd.IsOpen ? wd.Open : TimeSpan.FromHours(9);
        var close = wd.IsOpen ? wd.Close : TimeSpan.FromHours(19);
        foreach (var v in views)
        {
            if (v.Appointment.Start.TimeOfDay < open) open = v.Appointment.Start.TimeOfDay;
            if (v.Appointment.End.TimeOfDay > close) close = v.Appointment.End.TimeOfDay;
        }
        var first = TimeSpan.FromHours(Math.Floor(open.TotalHours));
        var last = TimeSpan.FromHours(Math.Ceiling(close.TotalHours));
        var total = (last - first).TotalMinutes;
        var height = total * PxPerMin + 16;
        const double gutter = 52;

        var canvas = new AbsoluteLayout { HeightRequest = height };
        double Y(TimeSpan t) => (t - first).TotalMinutes * PxPerMin + 8;

        // Saat çizgileri
        for (var t = first; t <= last; t += TimeSpan.FromHours(1))
        {
            var lbl = Ui.Muted(t.ToString(@"hh\:mm"), 11.5);
            Place(canvas, lbl, new Rect(0, Y(t) - 8, gutter - 8, 16));
            var line = new BoxView { HeightRequest = 1 }.Bg(Pal.Border);
            canvas.Add(line);
            AbsoluteLayout.SetLayoutBounds(line, new Rect(1, Y(t), 0.84, 1));
            AbsoluteLayout.SetLayoutFlags(line, AbsoluteLayoutFlags.XProportional | AbsoluteLayoutFlags.WidthProportional);
        }

        // Kapalı ve mola alanları
        void Shade(TimeSpan a, TimeSpan b, string? text)
        {
            if (b <= a) return;
            var box = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Opacity = 0.7 }.Bg(Pal.SurfaceAlt);
            if (text is not null) box.Content = new Label { Text = text, FontSize = 12, FontFamily = F.Medium, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }.Fg(Pal.Faint);
            canvas.Add(box);
            AbsoluteLayout.SetLayoutBounds(box, new Rect(1, Y(a), 0.84, (b - a).TotalMinutes * PxPerMin));
            AbsoluteLayout.SetLayoutFlags(box, AbsoluteLayoutFlags.XProportional | AbsoluteLayoutFlags.WidthProportional);
        }
        if (wd.IsOpen)
        {
            Shade(first, wd.Open, null);
            Shade(wd.Close, last, null);
            if (wd.HasBreak) Shade(wd.BreakStart!.Value, wd.BreakEnd!.Value, "Öğle arası");

            // Boş yarım saatlere dokunma alanları
            var busy = views.Where(v => v.Appointment.BlocksTime).Select(v => (v.Appointment.Start.TimeOfDay, v.Appointment.End.TimeOfDay)).ToList();
            var now = Kuafor.Core.Clock.Now;
            for (var t = wd.Open; t + TimeSpan.FromMinutes(30) <= wd.Close; t += TimeSpan.FromMinutes(30))
            {
                var s = t; var e = t + TimeSpan.FromMinutes(30);
                if (wd.HasBreak && s < wd.BreakEnd && e > wd.BreakStart) continue;
                if (busy.Any(x => s < x.Item2 && e > x.Item1)) continue;
                if (_day.Add(s) < now) continue;
                var hit = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = 10 } };
                var plus = new Label { Text = "+  " + s.ToString(@"hh\:mm"), FontSize = 12, FontFamily = F.Medium, Opacity = 0, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(10, 0) }.Fg(Pal.Primary);
                hit.Content = plus;
                var start = _day.Add(s);
                hit.OnTap(async () =>
                {
                    plus.Opacity = 1; hit.Bg(Pal.PrimarySoft);
                    await AppointmentEditPage.OpenAsync(Navigation, start: start);
                    plus.Opacity = 0; hit.BackgroundColor = Colors.Transparent;
                });
                canvas.Add(hit);
                AbsoluteLayout.SetLayoutBounds(hit, new Rect(1, Y(s) + 1, 0.84, 30 * PxPerMin - 2));
                AbsoluteLayout.SetLayoutFlags(hit, AbsoluteLayoutFlags.XProportional | AbsoluteLayoutFlags.WidthProportional);
            }
        }

        // Randevu blokları
        foreach (var v in views.OrderBy(v => v.Appointment.BlocksTime))
        {
            var a = v.Appointment;
            var color = Pal.Hex(v.ColorHex);
            var faded = !a.BlocksTime;
            var h = Math.Max(30, a.DurationMinutes * PxPerMin - 3);
            var title = new Label { Text = v.Customer.Name, FontFamily = F.SemiBold, FontSize = 13.5, LineBreakMode = LineBreakMode.TailTruncation }.Fg(Pal.Text);
            if (faded) title.TextDecorations = TextDecorations.Strikethrough;
            var sub = Ui.Muted($"{Texts.Time(a.Start)}–{Texts.Time(a.End)}  ·  {v.ServicesText}", 12, 1);
            var stack = new VerticalStackLayout { Spacing = 1 };
            stack.Add(title);
            if (h >= 44) stack.Add(sub);
            if (h >= 70) stack.Add(new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 4, 0, 0), Children = { Ui.StatusPill(a.Status), Ui.Text(Texts.Money(a.Status == AppointmentStatus.Done ? a.Paid : a.Price), 12, F.SemiBold, Pal.Muted) } });
            var grid = new Grid { ColumnDefinitions = [new(4), new(GridLength.Star)], ColumnSpacing = 10 };
            grid.Add(new BoxView { Color = faded ? Pal.Now(Pal.Faint) : color, CornerRadius = 2 }, 0);
            grid.Add(stack, 1);
            var block = new Border
            {
                Content = grid, Padding = new Thickness(6, h >= 44 ? 7 : 4, 10, 4), StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                BackgroundColor = faded ? Pal.Now(Pal.SurfaceAlt) : Pal.Soft(color, Pal.IsDark ? 0.24f : 0.13f),
                Opacity = faded ? 0.6 : 1,
            };
            block.OnTap(() => AppointmentPage.OpenAsync(Navigation, a.Id));
            canvas.Add(block);
            // İptal/gelmedi olanlar dolu bloğun arkasında kalmasın diye sağa küçük çizilir
            AbsoluteLayout.SetLayoutBounds(block, faded ? new Rect(1, Y(a.Start.TimeOfDay) + 1, 0.3, h) : new Rect(1, Y(a.Start.TimeOfDay) + 1, 0.84, h));
            AbsoluteLayout.SetLayoutFlags(block, AbsoluteLayoutFlags.XProportional | AbsoluteLayoutFlags.WidthProportional);
        }

        // Şimdi çizgisi
        var nowT = Kuafor.Core.Clock.Now;
        if (_day == nowT.Date && nowT.TimeOfDay > first && nowT.TimeOfDay < last)
        {
            var y = Y(nowT.TimeOfDay);
            var line = new BoxView { HeightRequest = 2, Color = Pal.Now(Pal.Danger) };
            canvas.Add(line);
            AbsoluteLayout.SetLayoutBounds(line, new Rect(1, y - 1, 0.86, 2));
            AbsoluteLayout.SetLayoutFlags(line, AbsoluteLayoutFlags.XProportional | AbsoluteLayoutFlags.WidthProportional);
            var dot = new Border { WidthRequest = 10, HeightRequest = 10, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 5 }, BackgroundColor = Pal.Now(Pal.Danger) };
            Place(canvas, dot, new Rect(gutter - 10, y - 5, 10, 10));
        }
        return canvas;
    }
}

static class LabelExt
{
    public static Label Right(this Label l) { l.HorizontalTextAlignment = TextAlignment.End; return l; }
}
