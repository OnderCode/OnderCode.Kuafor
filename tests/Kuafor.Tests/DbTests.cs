namespace Kuafor.Tests;

public sealed class DbTests : IAsyncLifetime
{
    readonly string _path = Path.Combine(Path.GetTempPath(), $"kuafor-test-{Guid.NewGuid():N}.db3");
    KuaforDb _db = null!;
    static readonly DateTime Now = new(2026, 11, 12, 11, 20, 0); // perşembe

    public async Task InitializeAsync()
    {
        Clock.Set(() => Now);
        _db = new KuaforDb(_path);
        await _db.InitAsync();
    }

    public async Task DisposeAsync()
    {
        Clock.Reset();
        await _db.CloseAsync();
        try { File.Delete(_path); } catch (IOException) { }
    }

    [Fact]
    public async Task Appointment_Roundtrip_With_Rules()
    {
        var services = DemoData.Services().Take(2).ToList();
        foreach (var s in services) await _db.SaveServiceAsync(s);
        var cid = await _db.SaveCustomerAsync(new Customer { Name = " Ayşe Yıldız ", Phone = "0636 052 41 87" });
        var c = await _db.CustomerAsync(cid);
        Assert.Equal("Ayşe Yıldız", c.Name);
        Assert.Equal("06360524187", c.Phone);

        var week = WorkWeek.Default;
        var a = new Appointment { CustomerId = cid, Start = Now.Date.AddDays(1).AddHours(10) };
        Assert.Null(await _db.SaveAppointmentAsync(a, services, week));
        Assert.Equal(60, a.DurationMinutes);
        Assert.Equal(570, a.ListPrice);

        var clash = new Appointment { CustomerId = cid, Start = Now.Date.AddDays(1).AddHours(10).AddMinutes(30) };
        Assert.Contains("başka randevu", await _db.SaveAppointmentAsync(clash, services.Take(1).ToList(), week));
        Assert.Equal("En az bir hizmet seçin.", await _db.SaveAppointmentAsync(clash, [], week));

        // Düzenleme: aynı randevu kendi saatiyle çakışmaz, kalemler yenilenir
        a.DurationMinutes = 0;
        Assert.Null(await _db.SaveAppointmentAsync(a, services.Take(1).ToList(), week));
        var view = await _db.ViewAsync(a.Id);
        Assert.Single(view!.Items);
        Assert.Equal("Ayşe Yıldız", view.Customer.Name);

        await _db.CompleteAsync(a.Id, 400, PaymentMethod.Card);
        var done = (await _db.ViewAsync(a.Id))!.Appointment;
        Assert.Equal(AppointmentStatus.Done, done.Status);
        Assert.Equal(400, done.Paid);
        await _db.SetStatusAsync(a.Id, AppointmentStatus.Planned);
        Assert.Equal(0, (await _db.ViewAsync(a.Id))!.Appointment.Paid);

        await _db.DeleteCustomerAsync(cid);
        Assert.Empty(await _db.AllAppointmentsAsync());
        Assert.Empty(await _db.AllItemsAsync());
    }

    [Fact]
    public async Task Profile_Defaults_And_Save()
    {
        var p = await _db.GetProfileAsync();
        Assert.Equal(Messages.DefaultReminder, p.ReminderTemplate);
        await _db.SaveProfileAsync(p with { Business = "Defne Saç Tasarım", Week = p.Week.With(new WorkDay(DayOfWeek.Monday, false, new(9, 0, 0), new(18, 0, 0))) });
        var back = await _db.GetProfileAsync();
        Assert.Equal("Defne Saç Tasarım", back.Business);
        Assert.False(back.Week[DayOfWeek.Monday].IsOpen);
    }

    [Fact]
    public async Task Demo_Data_Is_Lively_And_Consistent()
    {
        await DemoData.SeedAsync(_db);
        var customers = await _db.CustomersAsync();
        var appts = await _db.AllAppointmentsAsync();
        var items = await _db.AllItemsAsync();
        var week = WorkWeek.Default;

        Assert.InRange(customers.Count, 230, 250);
        Assert.All(customers, c => Assert.StartsWith("063", c.Phone));
        Assert.InRange(appts.Count, 750, 4000);
        Assert.All(appts, a => Assert.Contains(items, i => i.AppointmentId == a.Id));

        // Kurallar: çalışma saati içinde, öğle arasında değil, çakışma yok
        foreach (var day in appts.GroupBy(a => a.Start.Date))
        {
            var list = day.Where(a => a.BlocksTime).OrderBy(a => a.Start).ToList();
            for (var i = 1; i < list.Count; i++) Assert.True(list[i].Start >= list[i - 1].End, $"çakışma {list[i].Start}");
            foreach (var a in day) Assert.Null(SlotFinder.Check(a.Start, a.DurationMinutes, week, []));
        }

        // Geçmiş tamamlandı ve tahsil edildi, gelecek bekliyor
        Assert.All(appts.Where(a => a.Status == AppointmentStatus.Done), a => Assert.True(a.Paid > 0 && a.Payment != PaymentMethod.None && a.End <= Now));
        Assert.DoesNotContain(appts, a => a.Start > Now && a.Status is AppointmentStatus.Done or AppointmentStatus.NoShow);
        var today = appts.Where(a => a.Start.Date == Now.Date && a.BlocksTime).ToList();
        Assert.InRange(today.Count, 5, 14);
        Assert.Contains(today, a => a.Status == AppointmentStatus.Done);
        Assert.Contains(today, a => a.Status == AppointmentStatus.Planned);
        Assert.Contains(appts, a => a.Start.Date == Now.Date.AddDays(1) && a.Status == AppointmentStatus.Planned && a.ReminderSentAt is null);

        // Müşteri ritmi: gelme zamanı gelen ve doğum günü yaklaşan müşteriler var
        var due = customers.Count(c => Insights.ForCustomer(appts.Where(a => a.CustomerId == c.Id), items, Now).IsDue(Now.Date));
        Assert.InRange(due, 3, 120);
        Assert.True(customers.Count(c => Insights.DaysToBirthday(c, Now.Date) is not null) >= 2);

        // Kasa: bu ay ciro ve gider makul
        var month = Cash.Summarize(new DateTime(2026, 11, 1), Now.Date, appts, items, await _db.AllExpensesAsync(), customers, week);
        Assert.True(month.Revenue > month.Expenses);
        Assert.True(month.Done > 50);
        Assert.InRange(month.Occupancy, 0.5, 1);
        Assert.NotEmpty(month.Services);
    }
}
