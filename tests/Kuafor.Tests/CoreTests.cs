namespace Kuafor.Tests;

public class PhoneTests
{
    [Theory]
    [InlineData("0636 052 41 87", "06360524187")]
    [InlineData("+90 636 052 41 87", "06360524187")]
    [InlineData("6360524187", "06360524187")]
    [InlineData("(0636) 052-41-87", "06360524187")]
    public void Normalizes(string input, string expected) => Assert.Equal(expected, Phone.Normalize(input));

    [Theory]
    [InlineData("12345")]
    [InlineData("00360524187")]
    [InlineData("")]
    public void Rejects_Invalid(string input) => Assert.Null(Phone.Normalize(input));

    [Fact]
    public void Formats_And_Links()
    {
        Assert.Equal("0636 052 41 87", Phone.Format("06360524187"));
        Assert.Equal("tel:06360524187", Phone.TelUri("0636 052 41 87"));
        Assert.Equal("https://wa.me/906360524187?text=Merhaba%20Ay%C5%9Fe", Phone.WhatsAppUri("0636 052 41 87", "Merhaba Ayşe"));
    }

    [Fact]
    public void Demo_Numbers_Are_Never_Real_Mobile_Lines()
    {
        var rnd = new Random(1);
        for (var i = 0; i < 500; i++)
        {
            var p = DemoData.FakePhone(rnd);
            Assert.StartsWith("063", p);
            Assert.True(Phone.IsValid(p));
        }
    }
}

public class ScheduleTests
{
    static readonly DateTime Tuesday = new(2026, 11, 10);
    static Appointment At(int h, int m, int minutes, AppointmentStatus s = AppointmentStatus.Planned, int id = 0) =>
        new() { Id = id, Start = Tuesday.AddHours(h).AddMinutes(m), DurationMinutes = minutes, Status = s };

    [Fact]
    public void WorkWeek_RoundTrips()
    {
        var w = WorkWeek.Default.With(new WorkDay(DayOfWeek.Monday, false, new(9, 0, 0), new(18, 0, 0)));
        var back = WorkWeek.Parse(w.Serialize());
        Assert.False(back[DayOfWeek.Monday].IsOpen);
        Assert.False(back[DayOfWeek.Sunday].IsOpen);
        Assert.Equal(new TimeSpan(13, 0, 0), back[DayOfWeek.Tuesday].BreakStart);
        Assert.Equal(new TimeSpan(18, 0, 0), back[DayOfWeek.Saturday].Close);
        Assert.Equal(DayOfWeek.Monday, back.Days.First().Day);
        Assert.Equal(WorkWeek.Default.Serialize(), WorkWeek.Parse("bozuk;metin").Serialize());
    }

    [Fact]
    public void Check_Enforces_Hours_Break_And_Overlap()
    {
        var w = WorkWeek.Default;
        var existing = new[] { At(10, 0, 60), At(15, 0, 30, AppointmentStatus.Cancelled) };
        Assert.Null(SlotFinder.Check(Tuesday.AddHours(11), 60, w, existing));
        Assert.Contains("başka randevu", SlotFinder.Check(Tuesday.AddHours(10).AddMinutes(30), 30, w, existing));
        Assert.Contains("Öğle", SlotFinder.Check(Tuesday.AddHours(12).AddMinutes(30), 45, w, existing));
        Assert.Contains("Çalışma saatleri", SlotFinder.Check(Tuesday.AddHours(18).AddMinutes(30), 60, w, existing));
        Assert.Contains("kapalı", SlotFinder.Check(new DateTime(2026, 11, 15, 11, 0, 0), 30, w, existing));
        Assert.Null(SlotFinder.Check(Tuesday.AddHours(15), 30, w, existing)); // iptal edilen saat boşalır
    }

    [Fact]
    public void Editing_Ignores_Itself()
    {
        var a = At(10, 0, 60, id: 7);
        Assert.Null(SlotFinder.Check(Tuesday.AddHours(10).AddMinutes(30), 60, WorkWeek.Default, [a], ignoreId: 7));
    }

    [Fact]
    public void FreeStarts_Skips_Busy_Break_And_Past()
    {
        var w = WorkWeek.Default;
        var existing = new[] { At(10, 0, 90) };
        var starts = SlotFinder.FreeStarts(Tuesday, 60, w, existing, Tuesday.AddHours(9));
        Assert.Equal(Tuesday.AddHours(11).AddMinutes(30), starts[0]);          // 09:30-10:30 randevuyla çakışır
        Assert.DoesNotContain(Tuesday.AddHours(9).AddMinutes(30), starts);
        Assert.Contains(Tuesday.AddHours(11).AddMinutes(30), starts);
        Assert.DoesNotContain(Tuesday.AddHours(12).AddMinutes(30), starts);    // öğle arasına taşar
        Assert.Contains(Tuesday.AddHours(13).AddMinutes(45), starts);
        Assert.Equal(Tuesday.AddHours(18), starts[^1]);                         // 19:00'da biter
        var later = SlotFinder.FreeStarts(Tuesday, 60, w, existing, Tuesday.AddHours(16).AddMinutes(10));
        Assert.Equal(Tuesday.AddHours(16).AddMinutes(15), later[0]);
    }

    [Fact]
    public void FreeStarts_First_Slot_Fits_Before_Busy()
    {
        var starts = SlotFinder.FreeStarts(Tuesday, 30, WorkWeek.Default, [At(10, 0, 90)], DateTime.MinValue);
        Assert.Equal([Tuesday.AddHours(9).AddMinutes(30)], starts.Where(s => s.Hour < 11).ToList());
    }

    [Fact]
    public void Gaps_And_Occupancy()
    {
        var w = WorkWeek.Default;
        var existing = new[] { At(9, 30, 90), At(14, 0, 120), At(17, 0, 60) };
        var gaps = SlotFinder.Gaps(Tuesday, w, existing, DateTime.MinValue);
        Assert.Equal(3, gaps.Count);
        Assert.Equal(new TimeRange(Tuesday.AddHours(11), Tuesday.AddHours(13)), gaps[0]);
        Assert.Equal(new TimeRange(Tuesday.AddHours(16), Tuesday.AddHours(17)), gaps[1]);   // 13:45-14:00 (15 dk) sayılmaz
        Assert.Equal(new TimeRange(Tuesday.AddHours(18), Tuesday.AddHours(19)), gaps[2]);
        Assert.Equal((90 + 120 + 60) / (double)WorkWeek.Default[DayOfWeek.Tuesday].OpenMinutes, SlotFinder.Occupancy(Tuesday, w, existing), 3);
    }
}
