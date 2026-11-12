namespace Kuafor.Tests;

public class InsightTests
{
    static readonly DateTime Today = new(2026, 11, 12);

    [Fact]
    public void Customer_Rhythm_And_Expected_Visit()
    {
        var a = new List<Appointment>();
        var items = new List<AppointmentItem>();
        var id = 1;
        foreach (var daysAgo in new[] { 120, 90, 60, 30 })
        {
            a.Add(new Appointment { Id = id, Start = Today.AddDays(-daysAgo).AddHours(11), DurationMinutes = 90, Status = AppointmentStatus.Done, Paid = 1200, ListPrice = 1200 });
            items.Add(new AppointmentItem { AppointmentId = id, Name = "Dip boya", Price = 1200 });
            id++;
        }
        a.Add(new Appointment { Id = id++, Start = Today.AddDays(-45), Status = AppointmentStatus.NoShow });
        var s = Insights.ForCustomer(a, items, Today.AddHours(9));
        Assert.Equal(4, s.Visits);
        Assert.Equal(4800, s.Spent);
        Assert.Equal(30, s.AverageIntervalDays!.Value, 3);
        Assert.Equal(Today, s.ExpectedNext);
        Assert.Equal(1, s.NoShows);
        Assert.True(s.IsDue(Today));
        Assert.Equal("Dip boya", s.FavoriteServices[0].Name);

        a.Add(new Appointment { Id = id, Start = Today.AddDays(2).AddHours(10), Status = AppointmentStatus.Planned });
        Assert.False(Insights.ForCustomer(a, items, Today.AddHours(9)).IsDue(Today)); // randevusu var
    }

    [Fact]
    public void Birthday_Within_Week()
    {
        var c = new Customer { Name = "Ayşe Yıldız", BirthMonth = 11, BirthDay = 15 };
        Assert.Equal(3, Insights.DaysToBirthday(c, Today));
        Assert.Null(Insights.DaysToBirthday(new Customer { Name = "x" }, Today));
        Assert.Equal("AY", c.Initials);
    }

    [Fact]
    public void Cash_Summary_Splits_Revenue_By_Service_And_Payment()
    {
        var appts = new List<Appointment>
        {
            new() { Id = 1, Start = Today.AddHours(10), DurationMinutes = 75, Status = AppointmentStatus.Done, ListPrice = 700, Discount = 100, Paid = 600, Payment = PaymentMethod.Card },
            new() { Id = 2, Start = Today.AddDays(-1).AddHours(10), DurationMinutes = 30, Status = AppointmentStatus.Done, ListPrice = 250, Paid = 250, Payment = PaymentMethod.Cash },
            new() { Id = 3, Start = Today.AddDays(-1).AddHours(14), DurationMinutes = 30, Status = AppointmentStatus.NoShow, ListPrice = 250 },
            new() { Id = 4, Start = Today.AddDays(-9).AddHours(14), DurationMinutes = 30, Status = AppointmentStatus.Done, ListPrice = 250, Paid = 250, Payment = PaymentMethod.Cash },
        };
        var items = new List<AppointmentItem>
        {
            new() { AppointmentId = 1, Name = "Kadın saç kesimi", Price = 450 }, new() { AppointmentId = 1, Name = "Fön", Price = 250 },
            new() { AppointmentId = 2, Name = "Fön", Price = 250 }, new() { AppointmentId = 4, Name = "Fön", Price = 250 },
        };
        var exp = new List<Expense> { new() { Date = Today.AddDays(-2), Category = ExpenseCategory.Supplies, Amount = 300 } };
        Clock.Set(() => Today.AddHours(20));
        try
        {
            var s = Cash.Summarize(Today.AddDays(-6), Today, appts, items, exp, [], WorkWeek.Default);
            Assert.Equal(850, s.Revenue);
            Assert.Equal(550, s.Net);
            Assert.Equal(2, s.Done);
            Assert.Equal(1 / 3.0, s.NoShowRate, 3);
            Assert.Equal(7, s.Daily.Count);
            Assert.Equal(PaymentMethod.Card, s.Payments[0].Method);
            var fon = s.Services.Single(x => x.Service == "Fön");
            Assert.Equal(2, fon.Count);
            Assert.Equal(250 + 600m * 250 / 700, fon.Revenue, 1);
            Assert.Equal(250, s.PreviousRevenue);
        }
        finally { Clock.Reset(); }
    }

    [Fact]
    public void Message_Template_Fills_Placeholders()
    {
        Clock.Set(() => Today.AddHours(18));
        try
        {
            var c = new Customer { Name = "Zeynep Kaya" };
            var a = new Appointment { Start = Today.AddDays(1).AddHours(14).AddMinutes(30) };
            var text = Messages.Fill(Messages.DefaultReminder, c, "Selin Kuaför", a, [new AppointmentItem { Name = "Dip boya" }, new AppointmentItem { Name = "Fön" }]);
            Assert.Equal("Merhaba Zeynep, yarın saat 14:30 için dip boya, fön randevunuzu hatırlatırız. Görüşmek üzere! Selin Kuaför", text);
        }
        finally { Clock.Reset(); }
    }

    [Fact]
    public void Fold_Is_Turkish_Insensitive() => Assert.Equal(Texts.Fold("IŞIK Çağlayan"), Texts.Fold("ışık çaglayan"));
}
