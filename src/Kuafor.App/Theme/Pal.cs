namespace Kuafor.App.Theme;

/// <summary>
/// Onda renk düzeni (açık / koyu). Nötr tonlar Onda'nınkiyle aynı; vurgu rengi mor.
/// Her renk (açık, koyu) çiftidir; tema değişince AppThemeBinding ile kendiliğinden güncellenir.
/// </summary>
public static class Pal
{
    public static readonly (Color L, Color D) Bg = (C(0xF3F5F9), C(0x0F1115));
    public static readonly (Color L, Color D) Surface = (C(0xFFFFFF), C(0x181B21));
    public static readonly (Color L, Color D) SurfaceAlt = (C(0xF0F2F6), C(0x21252D));
    public static readonly (Color L, Color D) Border = (C(0xE3E7ED), C(0x2D323C));
    public static readonly (Color L, Color D) Text = (C(0x111827), C(0xE8EAED));
    public static readonly (Color L, Color D) Muted = (C(0x6B7280), C(0x9AA1AC));
    public static readonly (Color L, Color D) Faint = (C(0x9CA3AF), C(0x6B7280));
    public static readonly (Color L, Color D) Primary = (C(0x7C3AED), C(0x9F7AEA));
    public static readonly (Color L, Color D) PrimarySoft = (C(0xF1EBFE), C(0x2A2142));
    public static readonly (Color L, Color D) OnPrimary = (Colors.White, Colors.White);
    public static readonly (Color L, Color D) Success = (C(0x16A34A), C(0x22C55E));
    public static readonly (Color L, Color D) Warning = (C(0xD97706), C(0xF59E0B));
    public static readonly (Color L, Color D) Danger = (C(0xDC2626), C(0xEF4444));
    public static readonly (Color L, Color D) Pink = (C(0xDB2777), C(0xF472B6));
    public static readonly (Color L, Color D) Teal = (C(0x0D9488), C(0x2DD4BF));
    public static readonly (Color L, Color D) Blue = (C(0x2563EB), C(0x60A5FA));

    public static readonly Color Violet = C(0x7C3AED);
    public static readonly Color Rose = C(0xDB2777);

    /// <summary>Avatar renkleri (müşteri adından sabit seçilir).</summary>
    public static readonly Color[] Avatars =
    [
        C(0x7C3AED), C(0xDB2777), C(0x0D9488), C(0x2563EB), C(0xD97706), C(0x059669), C(0xE11D48), C(0x4F46E5), C(0x0891B2), C(0x9333EA),
    ];

    public static Color AvatarFor(string name) => Avatars[(int)((uint)StableHash(name) % Avatars.Length)];

    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;
    public static Color Now((Color L, Color D) c) => IsDark ? c.D : c.L;

    public static Color C(int rgb) => Color.FromRgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    public static Color Hex(string hex) => Color.FromArgb(hex);
    public static Color Soft(Color c, float alpha = 0.14f) => c.WithAlpha(alpha);

    static int StableHash(string s)
    {
        unchecked
        {
            var h = 23;
            foreach (var ch in s) h = h * 31 + ch;
            return h;
        }
    }
}

/// <summary>Yazı tipi takma adları (Inter, OFL lisanslı).</summary>
public static class F
{
    public const string Regular = "Inter";
    public const string Medium = "InterMedium";
    public const string SemiBold = "InterSemiBold";
    public const string Bold = "InterBold";
}
