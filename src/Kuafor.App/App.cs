using Kuafor.App.Theme;

namespace Kuafor.App;

public sealed class App : Application
{
    public App()
    {
        UserAppTheme = AppTheme.Unspecified;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new LoadingPage()) { Title = "Kuaför" };
#if WINDOWS
        window.Width = 412; window.Height = 900;
#endif
        return window;
    }

    public static void ApplyTheme(string theme)
    {
        if (Current is null) return;
        Current.UserAppTheme = theme switch { "light" => AppTheme.Light, "dark" => AppTheme.Dark, _ => AppTheme.Unspecified };
    }

    /// <summary>Veritabanı hazırlanırken gösterilen açılış ekranı; sonra sekmeli kabuğa geçer.</summary>
    sealed class LoadingPage : ContentPage
    {
        public LoadingPage()
        {
            BackgroundColor = Pal.Violet;
            Content = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center, Spacing = 14,
                Children =
                {
                    Ui.Icon(Ic.ContentCut, 64, Colors.White),
                    new Label { Text = "Kuaför", FontFamily = F.Bold, FontSize = 28, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                    new ActivityIndicator { IsRunning = true, Color = Colors.White, WidthRequest = 28, HeightRequest = 28 },
                },
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await AppData.EnsureAsync();
            ApplyTheme(AppData.Profile.Theme);
            if (Window is { } w) w.Page = new AppShell();
            if (Shots.Requested is not null) _ = Shots.RunAsync();
        }
    }
}
