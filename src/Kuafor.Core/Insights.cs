namespace Kuafor.Core;

/// <summary>Müşterinin geçmişinden çıkan özet: ziyaret, harcama, ortalama gelme aralığı, tahmini sonraki ziyaret.</summary>
public sealed record CustomerStats(
    int Visits, decimal Spent, DateTime? LastVisit, DateTime? NextAppointment, double? AverageIntervalDays,
    DateTime? ExpectedNext, int NoShows, IReadOnlyList<(string Name, int Count)> FavoriteServices)
{
    public decimal AverageTicket => Visits == 0 ? 0 : Spent / Visits;
    /// <summary>Sadık müşteri: en az 5 ziyaret.</summary>
    public bool IsLoyal => Visits >= 5;
    /// <summary>Gelme zamanı geldi: beklenen tarih yaklaştı/geçti ve ileri tarihli randevusu yok.</summary>
    public bool IsDue(DateTime today, int leadDays = 3) => NextAppointment is null && ExpectedNext is { } e && e.Date <= today.AddDays(leadDays) && e.Date >= today.AddDays(-60);
}

public static class Insights
{
    public static CustomerStats ForCustomer(IEnumerable<Appointment> appointments, IEnumerable<AppointmentItem> items, DateTime now)
    {
        var list = appointments.OrderBy(a => a.Start).ToList();
        var done = list.Where(a => a.Status == AppointmentStatus.Done).ToList();
        var next = list.Where(a => a.IsActive && a.Start >= now.Date).Select(a => (DateTime?)a.Start).FirstOrDefault();
        double? interval = null;
        if (done.Count >= 2)
        {
            // Son 6 ziyaret arası ortalama (saç uzama ritmi zamanla değişir)
            var recent = done.TakeLast(6).ToList();
            interval = Enumerable.Range(1, recent.Count - 1).Average(i => (recent[i].Start.Date - recent[i - 1].Start.Date).TotalDays);
        }
        var last = done.LastOrDefault()?.Start;
        DateTime? expected = last is { } l && interval is { } iv ? l.Date.AddDays(Math.Round(iv)) : null;
        var doneIds = done.Select(a => a.Id).ToHashSet();
        var fav = items.Where(i => doneIds.Contains(i.AppointmentId)).GroupBy(i => i.Name)
            .Select(g => (g.Key, g.Count())).OrderByDescending(g => g.Item2).ThenBy(g => g.Key, StringComparer.Create(Texts.Tr, false)).Take(4).ToList();
        return new CustomerStats(done.Count, done.Sum(a => a.Paid), last, next, interval, expected,
            list.Count(a => a.Status == AppointmentStatus.NoShow), fav);
    }

    /// <summary>Doğum günü önümüzdeki <paramref name="days"/> gün içinde mi? Kaç gün kaldı?</summary>
    public static int? DaysToBirthday(Customer c, DateTime today, int days = 7)
    {
        if (!c.HasBirthday) return null;
        for (var i = 0; i <= days; i++)
        {
            var d = today.AddDays(i);
            if (d.Month == c.BirthMonth && d.Day == c.BirthDay) return i;
        }
        return null;
    }
}

/// <summary>Kasa özeti: bir dönemdeki ciro, gider, net, ödeme türleri, hizmet dağılımı.</summary>
public sealed record CashSummary(
    DateTime From, DateTime To, decimal Revenue, decimal Expenses, int Done, int NoShows, int Cancelled,
    decimal AverageTicket, IReadOnlyList<(DateTime Day, decimal Revenue)> Daily,
    IReadOnlyList<(PaymentMethod Method, decimal Amount)> Payments,
    IReadOnlyList<(string Service, int Count, decimal Revenue)> Services,
    IReadOnlyList<(ExpenseCategory Category, decimal Amount)> ExpenseGroups,
    decimal PreviousRevenue, int NewCustomers, double Occupancy)
{
    public decimal Net => Revenue - Expenses;
    public double NoShowRate => Done + NoShows == 0 ? 0 : NoShows / (double)(Done + NoShows);
    public double? Change => PreviousRevenue == 0 ? null : (double)((Revenue - PreviousRevenue) / PreviousRevenue);
}

public static class Cash
{
    /// <summary>[from, to] (gün dahil) dönemi için kasa özeti. Ciro tamamlanan randevuların tahsilatıdır.</summary>
    public static CashSummary Summarize(DateTime from, DateTime to, IReadOnlyList<Appointment> appointments, IReadOnlyList<AppointmentItem> items,
        IReadOnlyList<Expense> expenses, IReadOnlyList<Customer> customers, WorkWeek week)
    {
        from = from.Date; to = to.Date;
        bool In(DateTime d) => d.Date >= from && d.Date <= to;
        var period = appointments.Where(a => In(a.Start)).ToList();
        var done = period.Where(a => a.Status == AppointmentStatus.Done).ToList();
        var revenue = done.Sum(a => a.Paid);
        var days = (to - from).Days + 1;
        var daily = Enumerable.Range(0, days).Select(i => from.AddDays(i))
            .Select(d => (d, done.Where(a => a.Start.Date == d).Sum(a => a.Paid))).ToList();
        var pays = done.GroupBy(a => a.Payment).Select(g => (g.Key, g.Sum(a => a.Paid))).OrderByDescending(p => p.Item2).ToList();
        var doneIds = done.ToDictionary(a => a.Id);
        var svc = items.Where(i => doneIds.ContainsKey(i.AppointmentId)).GroupBy(i => i.Name)
            .Select(g =>
            {
                // Tahsilatı kalemlere liste fiyatı oranında dağıt (indirim varsa orantılı düşer)
                var rev = g.Sum(i => { var a = doneIds[i.AppointmentId]; return a.ListPrice == 0 ? 0 : a.Paid * i.Price / a.ListPrice; });
                return (g.Key, g.Count(), Math.Round(rev, 2));
            })
            .OrderByDescending(s => s.Item3).ToList();
        var exp = expenses.Where(e => In(e.Date)).ToList();
        var groups = exp.GroupBy(e => e.Category).Select(g => (g.Key, g.Sum(e => e.Amount))).OrderByDescending(g => g.Item2).ToList();
        var prevFrom = from.AddDays(-days);
        var prev = appointments.Where(a => a.Status == AppointmentStatus.Done && a.Start.Date >= prevFrom && a.Start.Date < from).Sum(a => a.Paid);
        var newCustomers = customers.Count(c => In(c.CreatedAt));
        var openDays = Enumerable.Range(0, days).Select(i => from.AddDays(i)).Where(d => week[d.DayOfWeek].IsOpen && d <= Clock.Today).ToList();
        var occ = openDays.Count == 0 ? 0 : openDays.Average(d => SlotFinder.Occupancy(d, week, period));
        return new CashSummary(from, to, revenue, exp.Sum(e => e.Amount), done.Count,
            period.Count(a => a.Status == AppointmentStatus.NoShow), period.Count(a => a.Status == AppointmentStatus.Cancelled),
            done.Count == 0 ? 0 : Math.Round(revenue / done.Count, 2), daily, pays, svc, groups, prev, newCustomers, occ);
    }
}

/// <summary>WhatsApp hatırlatma ve davet metinleri. {ad}, {tarih}, {saat}, {hizmet}, {isletme} yer tutucuları.</summary>
public static class Messages
{
    public const string DefaultReminder = "Merhaba {ad}, {tarih} saat {saat} için {hizmet} randevunuzu hatırlatırız. Görüşmek üzere! {isletme}";
    public const string DefaultInvite = "Merhaba {ad}, son ziyaretinizin üzerinden epey zaman geçti. Size uygun bir gün ayarlayalım mı? {isletme}";
    public const string DefaultBirthday = "Doğum gününüz kutlu olsun {ad}! Bu ay yapacağınız işlemlerde size özel indirimimiz var. {isletme}";

    public static string Fill(string template, Customer c, string business, Appointment? a = null, IEnumerable<AppointmentItem>? items = null)
    {
        var first = Texts.FirstName(c.Name);
        var when = a is null ? "" : a.Start.Date == Clock.Today ? "bugün" : a.Start.Date == Clock.Today.AddDays(1) ? "yarın" : a.Start.ToString("d MMMM dddd", Texts.Tr);
        var svc = items is null ? "" : string.Join(", ", items.Select(i => i.Name.ToLower(Texts.Tr)));
        return template.Replace("{ad}", first).Replace("{tarih}", when).Replace("{saat}", a is null ? "" : Texts.Time(a.Start))
            .Replace("{hizmet}", svc).Replace("{isletme}", business).Replace("  ", " ").Trim();
    }
}
