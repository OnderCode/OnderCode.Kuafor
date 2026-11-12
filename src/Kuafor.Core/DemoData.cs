namespace Kuafor.Core;

/// <summary>
/// Örnek veri: hizmet listesi, yaklaşık 240 müşteri ve son 5 ayın randevuları (her müşterinin kendi gelme ritmiyle),
/// önümüzdeki 3 haftanın randevuları ve giderler. Bütün isimler ve numaralar uydurmadır; numaralar gerçek bir hatta
/// denk gelmesin diye 05xx yerine kullanılmayan 063x ile başlar.
/// </summary>
public static class DemoData
{
    static readonly string[] Women =
    [
        "Ayşe", "Elif", "Zeynep", "Merve", "Büşra", "Esra", "Seda", "Gizem", "Derya", "Ebru", "Özge", "Pınar", "Burcu", "Tuğba", "Melis",
        "Ceren", "İrem", "Damla", "Aslı", "Gamze", "Sibel", "Nur", "Hande", "Cansu", "Ece", "Dilek", "Yasemin", "Hülya", "Nilay", "Şule",
        "Gülşen", "Sevgi", "Arzu", "Nazlı", "Berna", "Didem", "Funda", "Tülay", "Meltem", "Begüm", "Simge", "Selin", "Defne", "Duygu", "Ilgın",
    ];
    static readonly string[] Men = ["Mehmet", "Can", "Emre", "Burak", "Kerem", "Onur", "Serkan", "Volkan", "Tolga", "Kaan", "Barış", "Ozan"];
    static readonly string[] Surnames =
    [
        "Yıldız", "Kaya", "Demir", "Şahin", "Çelik", "Aydın", "Öztürk", "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Koç", "Kurt", "Özdemir",
        "Polat", "Erdem", "Güneş", "Yalçın", "Aksoy", "Tekin", "Karaca", "Bulut", "Ünal", "Sarı", "Işık", "Tuncer", "Bozkurt", "Coşkun", "Ekinci",
    ];
    static readonly string[] Notes =
    [
        "Dip boya 7.1 + %6 oksidan, 35 dk bekleme.", "Kafa derisi hassas, amonyaksız boya kullan.", "Katlı kesim sever, uçlardan 2 parmak.",
        "Kaküllü, fön düz olsun.", "Doğal sarı balyaj; tonlama 9.12.", "Gür saç, inceltme istiyor.", "Kıvırcık, nemlendirici bakım öner.",
        "Ense makine 3, üstler makas.", "Kına kullanmış, boyadan önce test.", "Kısa bob, ense düz.", "Çay yerine su içiyor.", "Hep cumartesi sabahı geliyor.",
    ];

    public static IReadOnlyList<Service> Services() =>
    [
        S("Kadın saç kesimi", ServiceCategory.Cut, 45, 450, "#7C3AED", 1),
        S("Kakül kesimi", ServiceCategory.Cut, 15, 120, "#7C3AED", 2),
        S("Çocuk saç kesimi", ServiceCategory.Cut, 30, 250, "#7C3AED", 3),
        S("Fön", ServiceCategory.Styling, 30, 250, "#0EA5E9", 1),
        S("Maşa / dalga", ServiceCategory.Styling, 45, 400, "#0EA5E9", 2),
        S("Topuz", ServiceCategory.Styling, 60, 750, "#0EA5E9", 3),
        S("Dip boya", ServiceCategory.Color, 90, 1200, "#EC4899", 1),
        S("Tüm boya", ServiceCategory.Color, 120, 1800, "#EC4899", 2),
        S("Röfle", ServiceCategory.Color, 150, 2600, "#EC4899", 3),
        S("Balyaj / ombre", ServiceCategory.Color, 180, 3600, "#EC4899", 4),
        S("Keratin bakım", ServiceCategory.Care, 120, 2800, "#14B8A6", 1),
        S("Saç bakım maskesi", ServiceCategory.Care, 30, 350, "#14B8A6", 2),
        S("Kaş alımı", ServiceCategory.Care, 15, 120, "#14B8A6", 3),
        S("Gelin saçı", ServiceCategory.Special, 120, 4500, "#F59E0B", 1),
        S("Özel gün saçı", ServiceCategory.Special, 90, 1600, "#F59E0B", 2),
        S("Erkek saç kesimi", ServiceCategory.Men, 30, 350, "#2563EB", 1),
        S("Sakal tıraşı", ServiceCategory.Men, 15, 150, "#2563EB", 2),
    ];

    static Service S(string name, ServiceCategory c, int min, decimal price, string color, int order) =>
        new() { Name = name, Category = c, DurationMinutes = min, Price = price, ColorHex = color, SortOrder = order, IsActive = true };

    /// <summary>Uydurma, gerçek hatlara denk gelmeyen numara: 063x xxx xx xx.</summary>
    public static string FakePhone(Random rnd) => $"063{rnd.Next(0, 10)}{rnd.Next(0, 1000):000}{rnd.Next(0, 100):00}{rnd.Next(0, 100):00}";

    sealed class Habit
    {
        public required Customer Customer { get; init; }
        public required int[] Services { get; init; }   // hizmet indeksleri
        public int Interval { get; init; }
        public DateTime Due { get; set; }
        public int PreferredHour { get; init; }
        public bool Saturday { get; init; }
    }

    public static async Task SeedAsync(KuaforDb db, int seed = 2026)
    {
        var rnd = new Random(seed);
        var now = Clock.Now;
        var today = now.Date;
        var week = WorkWeek.Default;
        await db.SaveProfileAsync(new Profile("Selin Kuaför", "Selin", week, Messages.DefaultReminder, Messages.DefaultInvite, "system"));

        var services = Services().ToList();
        await db.Connection.InsertAllAsync(services);
        int Idx(string name) => services.FindIndex(s => s.Name == name);

        // Müşteri profilleri: hizmet alışkanlığı ve gelme aralığı
        var profiles = new (int[] Services, int Min, int Max, double Weight)[]
        {
            ([Idx("Kadın saç kesimi"), Idx("Fön")], 35, 60, 3),
            ([Idx("Dip boya"), Idx("Fön")], 28, 42, 2.5),
            ([Idx("Dip boya"), Idx("Kadın saç kesimi"), Idx("Fön")], 35, 50, 1.6),
            ([Idx("Fön")], 7, 12, 2.0),
            ([Idx("Röfle"), Idx("Fön")], 60, 90, 0.8),
            ([Idx("Balyaj / ombre"), Idx("Kadın saç kesimi")], 80, 120, 0.5),
            ([Idx("Keratin bakım")], 90, 120, 0.4),
            ([Idx("Kakül kesimi")], 18, 28, 0.6),
            ([Idx("Tüm boya"), Idx("Fön")], 40, 60, 0.8),
            ([Idx("Kadın saç kesimi"), Idx("Saç bakım maskesi"), Idx("Fön")], 40, 60, 0.9),
            ([Idx("Erkek saç kesimi")], 18, 30, 1.2),
            ([Idx("Erkek saç kesimi"), Idx("Sakal tıraşı")], 14, 24, 0.7),
            ([Idx("Çocuk saç kesimi")], 35, 60, 0.6),
            ([Idx("Kadın saç kesimi"), Idx("Kaş alımı")], 30, 50, 0.7),
        };
        var totalWeight = profiles.Sum(p => p.Weight);

        var habits = new List<Habit>();
        var used = new HashSet<string>();
        var start = today.AddDays(-150);
        for (var i = 0; i < 240; i++)
        {
            var p = Pick(rnd, profiles, totalWeight);
            var men = p.Services.Any(s => services[s].Category == ServiceCategory.Men);
            string name;
            do name = $"{(men ? Men[rnd.Next(Men.Length)] : Women[rnd.Next(Women.Length)])} {Surnames[rnd.Next(Surnames.Length)]}";
            while (!used.Add(name));
            // Çoğu müşteri dönem başından önce gelmeye başlamış; bir kısmı dönem içinde yeni
            var created = i < 195 ? start.AddDays(-rnd.Next(30, 700)) : start.AddDays(rnd.Next(0, 160));
            if (created > today) created = today.AddDays(-rnd.Next(1, 20));
            var c = new Customer
            {
                Name = name,
                Phone = FakePhone(rnd),
                CreatedAt = created.AddHours(10 + rnd.Next(8)),
                Note = rnd.NextDouble() < 0.28 ? Notes[rnd.Next(Notes.Length)] : null,
                IsFavorite = rnd.NextDouble() < 0.12,
            };
            if (rnd.NextDouble() < 0.45) { var b = new DateTime(2000, 1, 1).AddDays(rnd.Next(365)); c.BirthMonth = b.Month; c.BirthDay = b.Day; }
            var interval = rnd.Next(p.Min, p.Max + 1);
            habits.Add(new Habit
            {
                Customer = c, Services = p.Services, Interval = interval,
                Due = created > start ? created.Date : start.AddDays(rnd.Next(0, interval)),
                PreferredHour = rnd.NextDouble() < 0.5 ? rnd.Next(10, 13) : rnd.Next(14, 18),
                Saturday = rnd.NextDouble() < 0.25,
            });
        }
        // Bu hafta doğum günü olan birkaç müşteri
        foreach (var h in habits.Where(h => h.Customer.IsFavorite).Take(2).Select((h, i) => (h, i)))
        {
            var d = today.AddDays(1 + h.i * 2);
            h.h.Customer.BirthMonth = d.Month; h.h.Customer.BirthDay = d.Day;
        }
        await db.Connection.InsertAllAsync(habits.Select(h => h.Customer));

        // Gün gün randevu yerleştirme
        var all = new List<Appointment>();
        var plan = new List<(Appointment A, List<Service> S)>();
        for (var day = start; day <= today.AddDays(21); day = day.AddDays(1))
        {
            var wd = week[day.DayOfWeek];
            if (!wd.IsOpen) continue;
            var future = day > today ? (day - today).Days : 0;
            // Gelecek günler giderek boşalır (randevular genelde birkaç gün önceden alınır)
            var fill = future == 0 ? 0.92 : Math.Max(0.15, 0.85 - future * 0.05);
            if (day.DayOfWeek == DayOfWeek.Saturday) fill = Math.Min(1, fill + 0.1);
            var target = wd.OpenMinutes * fill;
            var dayList = new List<Appointment>();
            var candidates = habits.Where(h => h.Due <= day && h.Customer.CreatedAt.Date <= day)
                .OrderBy(h => h.Due).ThenBy(_ => rnd.Next()).ToList();
            foreach (var h in candidates)
            {
                if (dayList.Sum(a => a.DurationMinutes) >= target) break;
                if (h.Saturday && day.DayOfWeek != DayOfWeek.Saturday && (day - h.Due).Days < 5) continue;
                var svc = h.Services.Select(i => services[i]).ToList();
                var minutes = svc.Sum(s => s.DurationMinutes);
                var slots = SlotFinder.FreeStarts(day, minutes, week, dayList, DateTime.MinValue);
                if (slots.Count == 0) continue;
                var slot = slots.OrderBy(t => Math.Abs(t.Hour - h.PreferredHour) + rnd.NextDouble() * 2).First();
                var a = new Appointment
                {
                    CustomerId = h.Customer.Id, Start = slot, DurationMinutes = minutes,
                    ListPrice = svc.Sum(s => s.Price), CreatedAt = slot.AddDays(-rnd.Next(1, 12)).AddHours(-rnd.Next(1, 5)),
                    Discount = rnd.NextDouble() < 0.08 ? Math.Round(svc.Sum(s => s.Price) * 0.1m / 10) * 10 : 0,
                };
                if (a.CreatedAt > now) a.CreatedAt = now.AddHours(-rnd.Next(1, 30));
                dayList.Add(a);
                h.Due = day.AddDays(Math.Round(h.Interval * (0.85 + rnd.NextDouble() * 0.35)));
                plan.Add((a, svc));
            }
            all.AddRange(dayList);
        }

        // Durumlar: geçmiş tamamlandı (birkaç gelmedi/iptal), bugün saati geçenler tamamlandı
        foreach (var a in all)
        {
            if (a.End <= now)
            {
                var r = rnd.NextDouble();
                if (r < 0.035) a.Status = AppointmentStatus.NoShow;
                else if (r < 0.085) a.Status = AppointmentStatus.Cancelled;
                else
                {
                    a.Status = AppointmentStatus.Done;
                    a.Paid = a.Price;
                    var pr = rnd.NextDouble();
                    a.Payment = pr < 0.36 ? PaymentMethod.Cash : pr < 0.88 ? PaymentMethod.Card : PaymentMethod.Transfer;
                    a.CompletedAt = a.End.AddMinutes(rnd.Next(0, 10));
                }
            }
            else if (a.Start <= now) a.Status = AppointmentStatus.Arrived;
            else
            {
                a.Status = rnd.NextDouble() < 0.03 ? AppointmentStatus.Cancelled : AppointmentStatus.Planned;
                if (a.Start.Date == today) a.ReminderSentAt = today.AddDays(-1).AddHours(19);
            }
        }
        await db.Connection.RunInTransactionAsync(conn =>
        {
            foreach (var (a, svc) in plan)
            {
                conn.Insert(a);
                foreach (var s in svc)
                    conn.Insert(new AppointmentItem { AppointmentId = a.Id, ServiceId = s.Id, Name = s.Name, DurationMinutes = s.DurationMinutes, Price = s.Price });
            }
        });

        // Giderler
        var expenses = new List<Expense>();
        for (var m = new DateTime(start.Year, start.Month, 1); m <= today; m = m.AddMonths(1))
        {
            if (m >= start.Date) expenses.Add(new Expense { Date = m, Category = ExpenseCategory.Rent, Amount = 18000, Note = "Dükkân kirası" });
            var bill = m.AddDays(9);
            if (bill >= start && bill <= today) expenses.Add(new Expense { Date = bill, Category = ExpenseCategory.Bills, Amount = 1800 + rnd.Next(0, 9) * 100, Note = "Elektrik, su, internet" });
        }
        for (var d = start.AddDays(2); d <= today; d = d.AddDays(7 + rnd.Next(-2, 3)))
            expenses.Add(new Expense { Date = d, Category = ExpenseCategory.Supplies, Amount = 1500 + rnd.Next(0, 26) * 100, Note = rnd.NextDouble() < 0.5 ? "Boya ve oksidan" : "Şampuan, maske, sarf" });
        expenses.Add(new Expense { Date = today.AddDays(-40), Category = ExpenseCategory.Other, Amount = 6500, Note = "Yeni fön makinesi" });

        await db.Connection.InsertAllAsync(expenses);
    }

    static (int[] Services, int Min, int Max, double Weight) Pick(Random rnd, (int[] Services, int Min, int Max, double Weight)[] items, double total)
    {
        var r = rnd.NextDouble() * total;
        foreach (var it in items) { r -= it.Weight; if (r <= 0) return it; }
        return items[^1];
    }
}
