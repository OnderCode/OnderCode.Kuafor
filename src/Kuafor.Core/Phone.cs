namespace Kuafor.Core;

/// <summary>Türkiye telefon numarası: sadeleştirme, "0636 052 41 87" biçimi, arama ve WhatsApp bağlantıları.</summary>
public static class Phone
{
    /// <summary>Yalnız rakamlar, başında 0 olacak şekilde 11 hane ("05xx..." ya da sabit hat). Geçersizse null.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length == 12) digits = digits[2..];
        if (digits.Length == 10 && digits[0] != '0') digits = "0" + digits;
        return digits.Length == 11 && digits[0] == '0' && digits[1] != '0' ? digits : null;
    }

    public static bool IsValid(string? input) => Normalize(input) is not null;

    /// <summary>"0636 052 41 87"</summary>
    public static string Format(string? input)
    {
        var n = Normalize(input);
        return n is null ? input ?? "" : $"{n[..4]} {n[4..7]} {n[7..9]} {n[9..]}";
    }

    public static string TelUri(string phone) => "tel:" + (Normalize(phone) ?? phone);

    /// <summary>WhatsApp sohbet bağlantısı, hazır mesajla: https://wa.me/90636...?text=...</summary>
    public static string WhatsAppUri(string phone, string? text = null)
    {
        var n = Normalize(phone);
        var intl = n is null ? new string(phone.Where(char.IsDigit).ToArray()) : "90" + n[1..];
        return text is null ? $"https://wa.me/{intl}" : $"https://wa.me/{intl}?text={Uri.EscapeDataString(text)}";
    }
}
