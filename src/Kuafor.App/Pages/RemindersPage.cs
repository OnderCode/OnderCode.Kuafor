using Kuafor.App.Theme;
using Kuafor.Core;

namespace Kuafor.App.Pages;

/// <summary>
/// Hatırlatma merkezi: yarının (ve bugünün) hatırlatılmamış randevuları, gelme zamanı gelen müşteriler, yaklaşan doğum günleri.
/// Her satırdaki WhatsApp düğmesi mesajı hazır metinle açar ve hatırlatıldı olarak işaretler.
/// </summary>
public sealed class RemindersPage : PageBase
{
    protected override async Task LoadAsync()
    {
        var now = Kuafor.Core.Clock.Now;
        var today = now.Date;
        var upcoming = (await Agenda.DayAsync(today)).Concat(await Agenda.DayAsync(today.AddDays(1)))
            .Where(v => v.Appointment.Status == AppointmentStatus.Planned && v.Appointment.Start > now)
            .OrderBy(v => v.Appointment.Start).ToList();
        var waiting = upcoming.Where(v => v.Appointment.ReminderSentAt is null).ToList();
        var stats = await Agenda.StatsAsync();
        var due = await Agenda.DueAsync(stats);
        var birthdays = await Agenda.BirthdaysAsync(14);

        var total = waiting.Count + due.Count + birthdays.Count;
        var views = new List<View>
        {
            Header("Hatırlatmalar", total == 0 ? "Her şey yolunda" : $"{total} kişiye mesaj atılabilir", back: true),
        };

        // Özet çipleri
        var chips = Ui.Columns(
            (Parts.Stat(Ic.BellRingOutline, Pal.Warning, waiting.Count.ToString(), "Hatırlatılacak"), GridLength.Star),
            (Parts.Stat(Ic.AccountClock, Pal.Pink, due.Count.ToString(), "Zamanı gelen"), GridLength.Star),
            (Parts.Stat(Ic.CakeVariant, Pal.Teal, birthdays.Count.ToString(), "Doğum günü"), GridLength.Star));
        chips.ColumnSpacing = 10;
        views.Add(chips);

        views.Add(Ui.Section("Randevu hatırlatma", count: upcoming.Count > 0 ? $"{upcoming.Count - waiting.Count}/{upcoming.Count}" : null));
        if (upcoming.Count == 0) views.Add(Ui.Card(Ui.Muted("Bugün ve yarın için bekleyen randevu yok.", 13.5), 14));
        else
        {
            var list = Ui.Stack(4);
            foreach (var v in upcoming)
            {
                var a = v.Appointment;
                var sent = a.ReminderSentAt is not null;
                var when = a.Start.Date == today ? "Bugün" : "Yarın";
                View trailing = sent
                    ? Ui.Pill("Gönderildi", Pal.Now(Pal.Success), 11, Ic.Check)
                    : Ui.IconButton(Ic.Whatsapp, Pal.Success, null, 40, async () =>
                    {
                        await OpenAsync(Phone.WhatsAppUri(v.Customer.Phone, Agenda.ReminderText(v)));
                        await AppData.Db.MarkRemindedAsync(a.Id);
                        await ReloadAsync();
                    });
                var row = Ui.Columns(
                    (Ui.Avatar(v.Customer.Name, 42, v.Customer.IsFavorite), GridLength.Auto),
                    (Ui.Stack(1, Ui.Text(v.Customer.Name, 15, F.SemiBold, lines: 1), Ui.Muted($"{when} {Texts.Time(a.Start)}  ·  {v.ServicesText}", 12.5, 1)), GridLength.Star),
                    (trailing, GridLength.Auto));
                row.Padding = new Thickness(0, 6);
                if (sent) row.Opacity = 0.7;
                list.Add(row.OnTap(() => AppointmentPage.OpenAsync(Navigation, a.Id)));
            }
            views.Add(Ui.Card(list, 14));
        }

        views.Add(Ui.Section("Gelme zamanı gelenler", count: due.Count > 0 ? due.Count.ToString() : null));
        if (due.Count == 0) views.Add(Ui.Card(Ui.Muted("Şu an davet edilecek müşteri yok.", 13.5), 14));
        else
        {
            var list = Ui.Stack(4);
            foreach (var d in due.Take(30))
            {
                var late = (today - d.Stats.ExpectedNext!.Value.Date).Days;
                var sub = $"her ~{d.Stats.AverageIntervalDays:0} günde bir  ·  son {Texts.Ago(d.Stats.LastVisit!.Value, now)}";
                var row = Ui.Columns(
                    (Ui.Avatar(d.Customer.Name, 42, d.Customer.IsFavorite), GridLength.Auto),
                    (Ui.Stack(1, Ui.Text(d.Customer.Name, 15, F.SemiBold, lines: 1), Ui.Muted(sub, 12.5, 1),
                        Ui.Text(late > 0 ? $"{late} gün gecikti" : late == 0 ? "Bugün gelmesi beklenirdi" : $"{-late} gün içinde gelmesi beklenir", 12, F.SemiBold, late > 7 ? Pal.Danger : Pal.Warning)), GridLength.Star),
                    (Ui.IconButton(Ic.Whatsapp, Pal.Success, null, 40, () => OpenAsync(Phone.WhatsAppUri(d.Customer.Phone, Agenda.InviteText(d.Customer)))), GridLength.Auto));
                row.Padding = new Thickness(0, 6);
                list.Add(row.OnTap(() => CustomerPage.OpenAsync(Navigation, d.Customer.Id)));
            }
            views.Add(Ui.Card(list, 14));
        }

        if (birthdays.Count > 0)
        {
            views.Add(Ui.Section("Yaklaşan doğum günleri"));
            var list = Ui.Stack(4);
            foreach (var (c, days) in birthdays)
            {
                var row = Ui.Columns(
                    (Ui.Avatar(c.Name, 42, c.IsFavorite), GridLength.Auto),
                    (Ui.Stack(1, Ui.Text(c.Name, 15, F.SemiBold, lines: 1), Ui.Muted($"{c.BirthDay} {Texts.Month(c.BirthMonth)}  ·  {(days == 0 ? "bugün 🎉" : days == 1 ? "yarın" : $"{days} gün sonra")}", 12.5, 1)), GridLength.Star),
                    (Ui.IconButton(Ic.GiftOutline, Pal.Pink, null, 40, () => OpenAsync(Phone.WhatsAppUri(c.Phone, Agenda.BirthdayText(c)))), GridLength.Auto));
                row.Padding = new Thickness(0, 6);
                list.Add(row.OnTap(() => CustomerPage.OpenAsync(Navigation, c.Id)));
            }
            views.Add(Ui.Card(list, 14));
        }
        Show([.. views]);
    }
}
