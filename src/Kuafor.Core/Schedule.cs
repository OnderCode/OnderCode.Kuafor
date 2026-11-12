using System.Globalization;

namespace Kuafor.Core;

/// <summary>Bir günün çalışma saatleri ve öğle arası.</summary>
public sealed record WorkDay(DayOfWeek Day, bool IsOpen, TimeSpan Open, TimeSpan Close, TimeSpan? BreakStart = null, TimeSpan? BreakEnd = null)
{
    public bool HasBreak => BreakStart is { } s && BreakEnd is { } e && e > s;
    public int OpenMinutes => IsOpen ? (int)(Close - Open).TotalMinutes - (HasBreak ? (int)(BreakEnd!.Value - BreakStart!.Value).TotalMinutes : 0) : 0;
    public string Text => !IsOpen ? "Kapalı" : $"{Fmt(Open)} – {Fmt(Close)}" + (HasBreak ? $"  ·  ara {Fmt(BreakStart!.Value)}–{Fmt(BreakEnd!.Value)}" : "");
    internal static string Fmt(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}";
}

/// <summary>Haftalık çalışma planı; ayarlarda tek satır metin olarak saklanır.</summary>
public sealed class WorkWeek
{
    readonly Dictionary<DayOfWeek, WorkDay> _days;

    public WorkWeek(IEnumerable<WorkDay> days) => _days = days.ToDictionary(d => d.Day);

    public WorkDay this[DayOfWeek day] => _days.TryGetValue(day, out var d) ? d : new WorkDay(day, false, TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>Haftanın günleri pazartesiden başlayarak.</summary>
    public IEnumerable<WorkDay> Days => Enumerable.Range(0, 7).Select(i => this[(DayOfWeek)((i + 1) % 7)]);

    /// <summary>Varsayılan: pazartesi–cumartesi 09:30–19:00, 13:00–13:45 öğle arası; pazar kapalı.</summary>
    public static WorkWeek Default => new(Enumerable.Range(0, 7).Select(i => (DayOfWeek)i).Select(d => d == DayOfWeek.Sunday
        ? new WorkDay(d, false, new(9, 30, 0), new(19, 0, 0))
        : new WorkDay(d, true, new(9, 30, 0), d == DayOfWeek.Saturday ? new(18, 0, 0) : new(19, 0, 0), new(13, 0, 0), new(13, 45, 0))));

    public WorkWeek With(WorkDay day)
    {
        var copy = new Dictionary<DayOfWeek, WorkDay>(_days) { [day.Day] = day };
        return new WorkWeek(copy.Values);
    }

    /// <summary>"1=09:30-19:00/13:00-13:45;0=x;..."</summary>
    public string Serialize() => string.Join(";", Enumerable.Range(0, 7).Select(i => this[(DayOfWeek)i]).Select(d =>
        $"{(int)d.Day}=" + (d.IsOpen ? $"{WorkDay.Fmt(d.Open)}-{WorkDay.Fmt(d.Close)}" + (d.HasBreak ? $"/{WorkDay.Fmt(d.BreakStart!.Value)}-{WorkDay.Fmt(d.BreakEnd!.Value)}" : "") : "x")));

    public static WorkWeek Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Default;
        try
        {
            var days = new List<WorkDay>();
            foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                var day = (DayOfWeek)int.Parse(kv[0], CultureInfo.InvariantCulture);
                if (kv[1] == "x") { days.Add(new WorkDay(day, false, new(9, 30, 0), new(19, 0, 0))); continue; }
                var main = kv[1].Split('/');
                var (o, c) = Range(main[0]);
                if (main.Length > 1) { var (bs, be) = Range(main[1]); days.Add(new WorkDay(day, true, o, c, bs, be)); }
                else days.Add(new WorkDay(day, true, o, c));
            }
            return new WorkWeek(days);
        }
        catch (FormatException) { return Default; }
        catch (IndexOutOfRangeException) { return Default; }
    }

    static (TimeSpan, TimeSpan) Range(string s)
    {
        var p = s.Split('-');
        return (TimeSpan.ParseExact(p[0], @"hh\:mm", CultureInfo.InvariantCulture), TimeSpan.ParseExact(p[1], @"hh\:mm", CultureInfo.InvariantCulture));
    }
}

/// <summary>Bir zaman aralığı (boş aralık ya da dolu blok).</summary>
public readonly record struct TimeRange(DateTime Start, DateTime End)
{
    public int Minutes => (int)(End - Start).TotalMinutes;
    public bool Overlaps(DateTime start, DateTime end) => start < End && end > Start;
}

/// <summary>Randevu yerleştirme kuralları: çalışma saati, öğle arası, çakışma, boş saat önerisi.</summary>
public static class SlotFinder
{
    public const int Step = 15;

    /// <summary>O gün için müsait olmayan aralıklar: öğle arası + iptal/gelmedi dışındaki randevular.</summary>
    static List<TimeRange> Busy(DateTime date, WorkDay day, IEnumerable<Appointment> appointments, int? ignoreId)
    {
        var busy = appointments
            .Where(a => a.BlocksTime && a.Id != ignoreId && a.Start.Date == date.Date)
            .Select(a => new TimeRange(a.Start, a.End)).ToList();
        if (day.HasBreak) busy.Add(new TimeRange(date.Date + day.BreakStart!.Value, date.Date + day.BreakEnd!.Value));
        return busy;
    }

    /// <summary>Randevu bu saate konabilir mi? Konamıyorsa nedeni.</summary>
    public static string? Check(DateTime start, int minutes, WorkWeek week, IEnumerable<Appointment> appointments, int? ignoreId = null)
    {
        if (minutes <= 0) return "Süre sıfırdan büyük olmalı.";
        var day = week[start.DayOfWeek];
        if (!day.IsOpen) return $"{Texts.DayName(start.DayOfWeek)} günleri kapalısınız.";
        var end = start.AddMinutes(minutes);
        if (start.TimeOfDay < day.Open || end.TimeOfDay > day.Close || end.Date != start.Date)
            return $"Çalışma saatleri dışında ({WorkDay.Fmt(day.Open)}–{WorkDay.Fmt(day.Close)}).";
        if (day.HasBreak && new TimeRange(start.Date + day.BreakStart!.Value, start.Date + day.BreakEnd!.Value).Overlaps(start, end))
            return "Öğle arasına denk geliyor.";
        var clash = appointments.Where(a => a.BlocksTime && a.Id != ignoreId).FirstOrDefault(a => a.Start < end && a.End > start);
        return clash is null ? null : $"{Texts.Time(clash.Start)}–{Texts.Time(clash.End)} arasında başka randevu var.";
    }

    /// <summary>Verilen süredeki randevunun başlayabileceği saatler (15 dakikada bir). Bugünse geçmiş saatler atlanır.</summary>
    public static IReadOnlyList<DateTime> FreeStarts(DateTime date, int minutes, WorkWeek week, IEnumerable<Appointment> appointments, DateTime now, int? ignoreId = null)
    {
        var day = week[date.DayOfWeek];
        if (!day.IsOpen || minutes <= 0) return [];
        var busy = Busy(date, day, appointments, ignoreId);
        var result = new List<DateTime>();
        for (var t = date.Date + day.Open; t.AddMinutes(minutes) <= date.Date + day.Close; t = t.AddMinutes(Step))
        {
            if (t < now) continue;
            var end = t.AddMinutes(minutes);
            if (!busy.Any(b => b.Overlaps(t, end))) result.Add(t);
        }
        return result;
    }

    /// <summary>Gün içindeki boş aralıklar (en az <paramref name="minMinutes"/> dakika).</summary>
    public static IReadOnlyList<TimeRange> Gaps(DateTime date, WorkWeek week, IEnumerable<Appointment> appointments, DateTime now, int minMinutes = 30)
    {
        var day = week[date.DayOfWeek];
        if (!day.IsOpen) return [];
        var busy = Busy(date, day, appointments, null).OrderBy(b => b.Start).ToList();
        var gaps = new List<TimeRange>();
        var cursor = date.Date + day.Open;
        if (cursor < now && now.Date == date.Date) cursor = RoundUp(now);
        var close = date.Date + day.Close;
        foreach (var b in busy)
        {
            if (b.Start > cursor) Add(cursor, b.Start < close ? b.Start : close);
            if (b.End > cursor) cursor = b.End;
        }
        Add(cursor, close);
        return gaps;

        void Add(DateTime s, DateTime e) { if ((e - s).TotalMinutes >= minMinutes) gaps.Add(new TimeRange(s, e)); }
    }

    /// <summary>Günün doluluğu (0–1): randevu dakikası / açık dakika.</summary>
    public static double Occupancy(DateTime date, WorkWeek week, IEnumerable<Appointment> appointments)
    {
        var day = week[date.DayOfWeek];
        if (day.OpenMinutes <= 0) return 0;
        var used = appointments.Where(a => a.BlocksTime && a.Start.Date == date.Date).Sum(a => a.DurationMinutes);
        return Math.Min(1, used / (double)day.OpenMinutes);
    }

    public static DateTime RoundUp(DateTime t)
    {
        var m = (t.Minute + Step - 1) / Step * Step;
        return t.Date.AddHours(t.Hour).AddMinutes(m);
    }
}
