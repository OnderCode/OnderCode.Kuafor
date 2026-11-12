using System.Globalization;
using Kuafor.Core;

namespace Kuafor.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Texts.Tr;
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.otf", Theme.F.Regular);
                fonts.AddFont("Inter-Medium.otf", Theme.F.Medium);
                fonts.AddFont("Inter-SemiBold.otf", Theme.F.SemiBold);
                fonts.AddFont("Inter-Bold.otf", Theme.F.Bold);
                fonts.AddFont("KuaforIcons.ttf", Theme.Ic.Font);
            });
        return builder.Build();
    }
}
