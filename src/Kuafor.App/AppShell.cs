using Kuafor.App.Pages;
using Kuafor.App.Theme;

namespace Kuafor.App;

/// <summary>Alt sekmeler: Bugün, Takvim, Müşteriler, Kasa, Ayarlar.</summary>
public sealed class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        this.SetAppThemeColor(BackgroundColorProperty, Pal.Bg.L, Pal.Bg.D);
        this.SetAppThemeColor(TabBarBackgroundColorProperty, Pal.Surface.L, Pal.Surface.D);
        this.SetAppThemeColor(TabBarForegroundColorProperty, Pal.Primary.L, Pal.Primary.D);
        this.SetAppThemeColor(TabBarTitleColorProperty, Pal.Primary.L, Pal.Primary.D);
        this.SetAppThemeColor(TabBarUnselectedColorProperty, Pal.Muted.L, Pal.Muted.D);
        this.SetAppThemeColor(ForegroundColorProperty, Pal.Text.L, Pal.Text.D);
        this.SetAppThemeColor(TitleColorProperty, Pal.Text.L, Pal.Text.D);

        var tabs = new TabBar { Route = "main" };
        tabs.Items.Add(Tab<TodayPage>("Bugün", Ic.HomeVariantOutline, "today"));
        tabs.Items.Add(Tab<CalendarPage>("Takvim", Ic.CalendarMonth, "calendar"));
        tabs.Items.Add(Tab<CustomersPage>("Müşteriler", Ic.AccountGroup, "customers"));
        tabs.Items.Add(Tab<CashPage>("Kasa", Ic.Wallet, "cash"));
        tabs.Items.Add(Tab<MorePage>("Ayarlar", Ic.Cog, "more"));
        Items.Add(tabs);
    }

    static ShellContent Tab<T>(string title, string glyph, string route) where T : Page => new()
    {
        Title = title,
        Route = route,
        Icon = new FontImageSource { FontFamily = Ic.Font, Glyph = glyph, Size = 24, Color = Colors.Gray },
        ContentTemplate = new DataTemplate(typeof(T)),
    };

    /// <summary>Sekmeye geç (kök sayfaya döner).</summary>
    public static Task GoAsync(string route) => Current.GoToAsync("//main/" + route);
}
