using Kuafor.App.Theme;
using Kuafor.Core;

namespace Kuafor.App.Pages;

/// <summary>
/// Ana ekran: günün özeti, sıradaki randevu (geri sayım, ara, WhatsApp, geldi), günün programı ve aradaki boşluklar,
/// yarının hatırlatmaları, gelme zamanı gelen müşteriler ve doğum günleri.
/// </summary>
public sealed class TodayPage : PageBase
{
    protected override bool IsRoot => true;

    public TodayPage()
    {
        AddFab(Ic.Plus, () => AppointmentEditPage.OpenAsync(Navigation), "Randevu");
    }

    protected override async Task LoadAsync()
    {
        var now = Kuafor.Core.Clock.Now;
        var today = now.Date;
        var p = AppData.Profile;
        var views = await Agenda.DayAsync(today);
        var tomorrow = await Agenda.DayAsync(today.AddDays(1));
        var stats = await Agenda.StatsAsync();
        var due = await Agenda.DueAsync(stats);
        var birthdays = await Agenda.BirthdaysAsync();

        var active = views.Where(v => v.Appointment.BlocksTime).ToList();
        var done = active.Where(v => v.Appointment.Status == AppointmentStatus.Done).ToList();
        var earned = done.Sum(v => v.Appointment.Paid);
        var expected = active.Sum(v => v.Appointment.Status == AppointmentStatus.Done ? v.Appointment.Paid : v.Appointment.Price);
        var occupancy = SlotFinder.Occupancy(today, p.Week, active.Select(v => v.Appointment));
        var next = active.FirstOrDefault(v => v.Appointment.IsActive && v.Appointment.End > now);
        var toRemind = tomorrow.Count(v => v.Appointment.IsActive && v.Appointment.ReminderSentAt is null);

        var bell = Ui.IconButton(Ic.BellOutline, Pal.Text, Pal.Surface, 44, () => Navigation.PushAsync(new RemindersPage()));
        View trailing = bell;
        if (toRemind + due.Count > 0)
        {
            var dot = Ui.Pill((toRemind + due.Count).ToString(), Colors.White, 10);
            dot.BackgroundColor = Pal.Now(Pal.Danger);
            dot.Padding = new Thickness(6, 1);
            dot.HorizontalOptions = LayoutOptions.End; dot.VerticalOptions = LayoutOptions.Start;
            dot.Margin = new Thickness(0, -4, -4, 0);
            trailing = new Grid { bell, dot };
        }

        var views2 = new List<View>
        {
            Header($"{Texts.Greeting(now)}, {p.Owner}", Texts.DayLong(today), trailing: trailing),
            NextCard(next, now, p),
            Ui.Columns(
                (Parts.Stat(Ic.CalendarCheck, Pal.Primary, $"{done.Count}/{active.Count}", "randevu"), GridLength.Star),
                (Parts.Stat(Ic.CashMultiple, Pal.Success, Texts.MoneyShort(earned), "kazanılan", expected > earned ? $"+{Texts.MoneyShort(expected - earned)} bekleniyor" : null), GridLength.Star),
                (Parts.Stat(Ic.ChartDonut, Pal.Pink, $"%{occupancy * 100:0}", "doluluk"), GridLength.Star)),
        };
        foreach (var c in ((Grid)views2[2]).Children.OfType<View>()) c.VerticalOptions = LayoutOptions.Fill;

        if (tomorrow.Count > 0)
        {
            var text = toRemind > 0 ? $"Yarın {tomorrow.Count(v => v.Appointment.IsActive)} randevu var, {toRemind} kişiye hatırlatma gönderilmedi." : $"Yarınki {tomorrow.Count(v => v.Appointment.IsActive)} randevunun hepsine hatırlatma gönderildi.";
            var row = Ui.ListRow(toRemind > 0 ? Ic.BellRingOutline : Ic.CheckCircle, toRemind > 0 ? Pal.Warning : Pal.Success, toRemind > 0 ? "Hatırlatma zamanı" : "Hatırlatmalar tamam", text);
            views2.Add(Ui.Card(row, 12, 18).OnTap(() => Navigation.PushAsync(new RemindersPage())));
        }

        views2.Add(Ui.Section("Bugünün programı", count: active.Count == 0 ? null : $"{active.Count}"));
        if (views.Count == 0 && !p.Week[today.DayOfWeek].IsOpen)
            views2.Add(Ui.Card(Ui.Empty(Ic.Coffee, "Bugün kapalısınız", "İyi dinlenmeler! Yarının programı takvimde.")));
        else if (views.Count == 0)
            views2.Add(Ui.Card(Ui.Empty(Ic.CalendarBlankOutline, "Bugün randevu yok", "Sağ alttaki düğmeyle randevu ekleyin.")));
        else
            views2.AddRange(Timeline(views, today, now, p));

        if (due.Count > 0)
        {
            views2.Add(Ui.Section("Gelme zamanı gelenler", "Tümü", () => Navigation.PushAsync(new RemindersPage()), $"{due.Count}"));
            var strip = new HorizontalStackLayout { Spacing = 10 };
            foreach (var d in due.Take(12)) strip.Add(DueCard(d, now));
            views2.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Margin = new Thickness(-18, 0), Padding = new Thickness(18, 0) });
        }

        if (birthdays.Count > 0)
        {
            views2.Add(Ui.Section("Doğum günleri"));
            var list = Ui.Stack(0);
            foreach (var (c, days) in birthdays.Take(4))
            {
                var when = days == 0 ? "Bugün" : days == 1 ? "Yarın" : Texts.DayLong(today.AddDays(days));
                var send = Ui.IconButton(Ic.Whatsapp, Pal.Success, null, 38, () => OpenAsync(Phone.WhatsAppUri(c.Phone, Agenda.BirthdayText(c))));
                list.Add(Ui.Columns((Ui.Avatar(c.Name, 40), GridLength.Auto), (Ui.Stack(1, Ui.Text(c.Name, 15, F.Medium), Ui.Muted(when, 12.5)), GridLength.Star), (send, GridLength.Auto)));
                ((Grid)list.Children[^1]).Padding = new Thickness(0, 6);
            }
            views2.Add(Ui.Card(list, 14));
        }

        Show([.. views2]);
    }

    View NextCard(AppointmentView? next, DateTime now, Profile p)
    {
        if (next is null)
        {
            var gaps = SlotFinder.Gaps(now.Date, p.Week, [], now);
            var tomorrowOpen = p.Week[now.Date.AddDays(1).DayOfWeek].IsOpen;
            var content = Ui.Stack(6,
                Parts.OnHero("SIRADAKİ RANDEVU", 11.5, F.SemiBold, 0.75),
                Parts.OnHero("Bugün başka randevu yok", 21, F.Bold),
                Parts.OnHero(tomorrowOpen ? "Yarının programına takvimden bakabilirsiniz." : "Yarın kapalısınız, iyi dinlenmeler.", 13.5, F.Regular, 0.85),
                new BoxView { HeightRequest = 4, Color = Colors.Transparent },
                Parts.HeroButton(Ic.CalendarMonth, "Takvimi aç", () => AppShell.GoAsync("calendar")));
            return Parts.Hero(content);
        }
        var a = next.Appointment;
        var started = a.Start <= now;
        var when = started ? $"{Texts.Time(a.Start)}'de başladı · {Texts.Time(a.End)}'de biter" : $"{Texts.Time(a.Start)} – {Texts.Time(a.End)}";
        var countdown = new Border
        {
            Content = Parts.OnHero(started ? "Şu an" : Texts.Countdown(a.Start, now), 13, F.SemiBold),
            Padding = new Thickness(12, 5), StrokeThickness = 0, BackgroundColor = Colors.White.WithAlpha(0.22f),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 }, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
        };
        var avatar = new Border
        {
            WidthRequest = 52, HeightRequest = 52, StrokeThickness = 0, BackgroundColor = Colors.White.WithAlpha(0.22f),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 26 },
            Content = new Label { Text = next.Customer.Initials, FontFamily = F.Bold, FontSize = 19, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center },
        };
        var top = Ui.Columns(
            (avatar, GridLength.Auto),
            (Ui.Stack(2, Parts.OnHero(started ? "ŞU ANKİ RANDEVU" : "SIRADAKİ RANDEVU", 11.5, F.SemiBold, 0.75), Parts.OnHero(next.Customer.Name, 20, F.Bold), Parts.OnHero(when, 13.5, F.Medium, 0.9)), GridLength.Star),
            (countdown, GridLength.Auto));
        var services = Parts.OnHero($"{next.ServicesText}  ·  {Texts.Duration(a.DurationMinutes)}  ·  {Texts.Money(a.Price)}", 13.5, F.Regular, 0.92);
        services.LineBreakMode = LineBreakMode.WordWrap;
        services.MaxLines = 2;
        var primary = a.Status == AppointmentStatus.Arrived || started
            ? Parts.HeroButton(Ic.CashPlus, "Tamamla", () => CompletePage.OpenAsync(Navigation, next))
            : Parts.HeroButton(Ic.AccountCheck, "Geldi", async () => { await AppData.Db.SetStatusAsync(a.Id, AppointmentStatus.Arrived); Toast($"{Texts.FirstName(next.Customer.Name)} geldi olarak işaretlendi"); await ReloadAsync(); });
        var buttons = Ui.Columns(
            (Parts.HeroButton(Ic.Phone, "Ara", () => OpenAsync(Phone.TelUri(next.Customer.Phone))), GridLength.Star),
            (Parts.HeroButton(Ic.Whatsapp, "Mesaj", () => OpenAsync(Phone.WhatsAppUri(next.Customer.Phone, Agenda.ReminderText(next)))), GridLength.Star),
            (primary, GridLength.Star));
        buttons.ColumnSpacing = 8;
        var hero = Parts.Hero(Ui.Stack(14, top, services, buttons));
        return hero.OnTap(() => AppointmentPage.OpenAsync(Navigation, a.Id));
    }

    IEnumerable<View> Timeline(List<AppointmentView> views, DateTime today, DateTime now, Profile p)
    {
        var gaps = SlotFinder.Gaps(today, p.Week, views.Select(v => v.Appointment), now, 45).ToList();
        var items = views.Select(v => (v.Appointment.Start, View: (object)v))
            .Concat(gaps.Select(g => (g.Start, View: (object)g)))
            .OrderBy(x => x.Start).ToList();
        foreach (var (_, item) in items)
        {
            if (item is AppointmentView v) yield return Parts.AppointmentRow(v, () => AppointmentPage.OpenAsync(Navigation, v.Appointment.Id));
            else if (item is TimeRange g) yield return Parts.GapRow(g, () => AppointmentEditPage.OpenAsync(Navigation, start: g.Start));
        }
    }

    View DueCard(DueCustomer d, DateTime now)
    {
        var s = d.Stats;
        var late = (now.Date - s.ExpectedNext!.Value.Date).Days;
        var note = late > 0 ? $"{late} gün gecikti" : late == 0 ? "bugün bekleniyor" : $"{-late} gün içinde";
        var invite = new Border
        {
            Content = new HorizontalStackLayout { Spacing = 5, HorizontalOptions = LayoutOptions.Center, Children = { Ui.Icon(Ic.Whatsapp, 16, Pal.Success), Ui.Text("Davet et", 12.5, F.SemiBold, Pal.Success) } },
            Padding = new Thickness(10, 7), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Pal.Soft(Pal.Now(Pal.Success), 0.12f),
        }.OnTap(() => OpenAsync(Phone.WhatsAppUri(d.Customer.Phone, Agenda.InviteText(d.Customer))));
        var name = Ui.Text(Texts.FirstName(d.Customer.Name), 14.5, F.SemiBold, lines: 1);
        name.HorizontalTextAlignment = TextAlignment.Center;
        var sub = Ui.Muted($"her ~{s.AverageIntervalDays:0} günde", 11.5, 1);
        sub.HorizontalTextAlignment = TextAlignment.Center;
        var lateLabel = Ui.Text(note, 11.5, F.Medium, late > 0 ? Pal.Warning : Pal.Primary, 1);
        lateLabel.HorizontalTextAlignment = TextAlignment.Center;
        var avatar = Ui.Avatar(d.Customer.Name, 50, d.Customer.IsFavorite);
        avatar.HorizontalOptions = LayoutOptions.Center;
        var card = Ui.Card(Ui.Stack(6, avatar, name, sub, lateLabel, invite), 12, 20);
        card.WidthRequest = 136;
        return card.OnTap(() => CustomerPage.OpenAsync(Navigation, d.Customer.Id));
    }
}
