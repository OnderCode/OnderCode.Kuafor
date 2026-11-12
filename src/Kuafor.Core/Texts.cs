using System.Globalization;

namespace Kuafor.Core;

/// <summary>Türkçe metinler ve biçimlendirme yardımcıları.</summary>
public static class Texts
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Money(decimal v) => v.ToString("#,0.##", Tr) + " ₺";
    public static string MoneyShort(decimal v) => v >= 100_000 ? (v / 1000).ToString("#,0", Tr) + " B ₺" : v.ToString("#,0", Tr) + " ₺";
    public static string Time(DateTime t) => t.ToString("HH:mm", Tr);
    public static string DayLong(DateTime d) => d.ToString("d MMMM dddd", Tr);
    public static string DayShort(DateTime d) => d.ToString("d MMM ddd", Tr);
    public static string DayName(DayOfWeek d) => Tr.DateTimeFormat.GetDayName(d);
    public static string DayAbbr(DayOfWeek d) => Tr.DateTimeFormat.GetAbbreviatedDayName(d);
    public static string Month(int m) => Tr.DateTimeFormat.GetMonthName(m);

    public static string Duration(int minutes) => minutes switch
    {
        < 60 => $"{minutes} dk",
        _ when minutes % 60 == 0 => $"{minutes / 60} sa",
        _ => $"{minutes / 60} sa {minutes % 60} dk",
    };

    /// <summary>"bugün", "dün", "3 gün önce", "2 hafta önce", "4 ay önce".</summary>
    public static string Ago(DateTime when, DateTime now)
    {
        var days = (now.Date - when.Date).Days;
        return days switch
        {
            < 0 => Until(when, now),
            0 => "bugün",
            1 => "dün",
            < 14 => $"{days} gün önce",
            < 60 => $"{days / 7} hafta önce",
            < 365 => $"{days / 30} ay önce",
            _ => $"{days / 365} yıl önce",
        };
    }

    public static string Until(DateTime when, DateTime now)
    {
        var days = (when.Date - now.Date).Days;
        return days switch
        {
            <= 0 => "bugün",
            1 => "yarın",
            < 14 => $"{days} gün sonra",
            _ => $"{days / 7} hafta sonra",
        };
    }

    /// <summary>"12 dk sonra", "1 sa 5 dk sonra", "şimdi".</summary>
    public static string Countdown(DateTime start, DateTime now)
    {
        var m = (int)Math.Ceiling((start - now).TotalMinutes);
        if (m <= 0) return "şimdi";
        return Duration(m) + " sonra";
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpper(Tr),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpper(Tr),
        };
    }

    public static string FirstName(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;

    public static string Greeting(DateTime now) => now.Hour switch
    {
        < 6 => "İyi geceler",
        < 12 => "Günaydın",
        < 18 => "İyi günler",
        _ => "İyi akşamlar",
    };

    public static string Of(ServiceCategory c) => c switch
    {
        ServiceCategory.Cut => "Kesim",
        ServiceCategory.Color => "Boya",
        ServiceCategory.Styling => "Fön ve şekillendirme",
        ServiceCategory.Care => "Bakım",
        ServiceCategory.Special => "Gelin ve özel gün",
        ServiceCategory.Men => "Erkek",
        _ => c.ToString(),
    };

    public static string Of(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Planned => "Bekleniyor",
        AppointmentStatus.Arrived => "Geldi",
        AppointmentStatus.Done => "Tamamlandı",
        AppointmentStatus.NoShow => "Gelmedi",
        AppointmentStatus.Cancelled => "İptal",
        _ => s.ToString(),
    };

    public static string Of(PaymentMethod p) => p switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.Card => "Kart",
        PaymentMethod.Transfer => "Havale/IBAN",
        _ => "—",
    };

    public static string Of(ExpenseCategory c) => c switch
    {
        ExpenseCategory.Supplies => "Malzeme",
        ExpenseCategory.Rent => "Kira",
        ExpenseCategory.Bills => "Fatura",
        _ => "Diğer",
    };

    /// <summary>Türkçe duyarsız arama için sadeleştirme (İ/ı, Ş/ş...).</summary>
    public static string Fold(string s)
    {
        var lower = s.ToLower(Tr);
        Span<char> buf = stackalloc char[lower.Length];
        for (var i = 0; i < lower.Length; i++)
            buf[i] = lower[i] switch { 'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c', 'â' => 'a', 'î' => 'i', 'û' => 'u', _ => lower[i] };
        return new string(buf);
    }
}

/// <summary>Saat kaynağı. Testlerde ve ekran görüntüsü modunda sabitlenebilir.</summary>
public static class Clock
{
    static Func<DateTime> _now = () => DateTime.Now;
    public static DateTime Now => _now();
    public static DateTime Today => Now.Date;
    public static void Set(Func<DateTime> now) => _now = now;
    public static void Reset() => _now = () => DateTime.Now;
}
