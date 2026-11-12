using Kuafor.Core;

namespace Kuafor.App.Pages;

/// <summary>Gelme zamanı gelen müşteri: beklenen tarih ve ortalama aralık.</summary>
public sealed record DueCustomer(Customer Customer, CustomerStats Stats);

/// <summary>Ekranlar arası paylaşılan hesaplar: hatırlatılacak randevular, gelme zamanı gelenler, doğum günleri.</summary>
public static class Agenda
{
    public static async Task<List<AppointmentView>> DayAsync(DateTime day)
    {
        var list = await AppData.Db.AppointmentsAsync(day, day);
        return await AppData.Db.ViewsAsync(list);
    }

    /// <summary>Müşteri başına istatistik (bütün geçmişten).</summary>
    public static async Task<Dictionary<int, CustomerStats>> StatsAsync()
    {
        var appts = (await AppData.Db.AllAppointmentsAsync()).ToLookup(a => a.CustomerId);
        var items = (await AppData.Db.AllItemsAsync()).ToLookup(i => i.AppointmentId);
        var now = Kuafor.Core.Clock.Now;
        var customers = await AppData.Db.CustomersAsync();
        return customers.ToDictionary(c => c.Id, c =>
        {
            var list = appts[c.Id].ToList();
            return Insights.ForCustomer(list, list.SelectMany(a => items[a.Id]), now);
        });
    }

    public static async Task<List<DueCustomer>> DueAsync(Dictionary<int, CustomerStats>? stats = null)
    {
        stats ??= await StatsAsync();
        var today = Kuafor.Core.Clock.Today;
        var customers = await AppData.Db.CustomersAsync();
        return [.. customers.Where(c => stats.TryGetValue(c.Id, out var s) && s.IsDue(today))
            .Select(c => new DueCustomer(c, stats[c.Id]))
            .OrderBy(d => d.Stats.ExpectedNext)];
    }

    public static async Task<List<(Customer Customer, int Days)>> BirthdaysAsync(int days = 7)
    {
        var today = Kuafor.Core.Clock.Today;
        return [.. (await AppData.Db.CustomersAsync())
            .Select(c => (c, Insights.DaysToBirthday(c, today, days)))
            .Where(x => x.Item2 is not null).Select(x => (x.c, x.Item2!.Value)).OrderBy(x => x.Item2)];
    }

    public static string ReminderText(AppointmentView v) => Messages.Fill(AppData.Profile.ReminderTemplate, v.Customer, AppData.Profile.Business, v.Appointment, v.Items);
    public static string InviteText(Customer c) => Messages.Fill(AppData.Profile.InviteTemplate, c, AppData.Profile.Business);
    public static string BirthdayText(Customer c) => Messages.Fill(Messages.DefaultBirthday, c, AppData.Profile.Business);
}
