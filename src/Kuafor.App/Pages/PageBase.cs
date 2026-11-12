using Kuafor.App.Theme;
using Microsoft.Maui.Controls.Shapes;

namespace Kuafor.App.Pages;

/// <summary>
/// Bütün sayfaların tabanı: üst başlık, kaydırılan gövde, sağ altta yüzen düğme ve kısa bildirim (toast).
/// Sayfa her göründüğünde verisini yeniden yükler.
/// </summary>
public abstract class PageBase : ContentPage
{
    protected readonly VerticalStackLayout Body = new() { Spacing = 14, Padding = new Thickness(18, 8, 18, 120) };
    protected readonly Grid Root = new();
    protected readonly ScrollView Scroller;
    readonly Border _toast;
    readonly Label _toastText = new() { FontFamily = F.Medium, FontSize = 14, TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center };
    bool _loading;

    protected PageBase(bool scroll = true)
    {
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, IsRoot);
        this.Bg(Pal.Bg);
        Scroller = new ScrollView { Content = Body };
        if (scroll) Root.Add(Scroller);
        _toast = new Border
        {
            Content = new HorizontalStackLayout { Spacing = 10, Children = { Ui.Icon(Ic.CheckCircle, 20, Colors.White), _toastText } },
            Padding = new Thickness(16, 12), StrokeThickness = 0, BackgroundColor = Color.FromArgb("#E6111827"),
            StrokeShape = new RoundRectangle { CornerRadius = 16 }, Margin = new Thickness(18, 0, 18, 24),
            VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.Center, IsVisible = false, Opacity = 0,
        };
        Root.Add(_toast);
        Content = Root;
    }

    /// <summary>Sekme kök sayfası mı (alt sekmeler görünür)?</summary>
    protected virtual bool IsRoot => false;

    protected abstract Task LoadAsync();

    public async Task ReloadAsync()
    {
        if (_loading) return;
        _loading = true;
        try { await LoadAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("Bir sorun oluştu", ex.Message, "Tamam"); }
        finally { _loading = false; }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AppData.EnsureAsync();
        await ReloadAsync();
    }

    /// <summary>Gövdeyi belirli bir yüksekliğe kaydırır (ekran görüntüsü modu için).</summary>
    public Task ScrollToAsync(double y) => Scroller.ScrollToAsync(0, y, false);

    /// <summary>Gövdeyi yeni içerikle değiştirir.</summary>
    protected void Show(params View[] views)
    {
        Body.Children.Clear();
        foreach (var v in views) Body.Add(v);
    }

    /// <summary>Sayfa başlığı. Alt sayfalarda geri düğmesi.</summary>
    protected View Header(string title, string? subtitle = null, bool back = false, View? trailing = null)
    {
        var g = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12, Margin = new Thickness(0, 10, 0, 4) };
        if (back) g.Add(Ui.IconButton(Ic.ChevronLeft, Pal.Text, Pal.Surface, 42, () => Navigation.PopAsync()), 0);
        var texts = Ui.Stack(2, back ? Ui.Text(title, 21, F.Bold) : Ui.Title(title));
        if (subtitle is not null) texts.Add(Ui.Muted(subtitle, 14));
        texts.VerticalOptions = LayoutOptions.Center;
        g.Add(texts, 1);
        if (trailing is not null) g.Add(trailing, 2);
        return g;
    }

    /// <summary>Sağ altta yüzen yuvarlak düğme (ör. yeni randevu).</summary>
    protected void AddFab(string glyph, Func<Task> tap, string? text = null)
    {
        var row = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
        row.Add(Ui.Icon(glyph, 24, Colors.White));
        if (text is not null) row.Add(new Label { Text = text, FontFamily = F.SemiBold, FontSize = 15, TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center });
        var fab = new Border
        {
            Content = row, HeightRequest = 58, MinimumWidthRequest = 58, Padding = new Thickness(text is null ? 0 : 20, 0),
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 29 },
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 18, 20),
            Background = new LinearGradientBrush([new GradientStop(Pal.Violet, 0), new GradientStop(Pal.Rose, 1)], new Point(0, 0), new Point(1, 1)),
            Shadow = new Shadow { Brush = Pal.Violet, Opacity = 0.4f, Radius = 18, Offset = new Point(0, 8) },
        };
        if (text is null) fab.WidthRequest = 58;
        fab.OnTap(tap);
        Root.Add(fab);
    }

    public async void Toast(string text)
    {
        _toastText.Text = text;
        _toast.IsVisible = true;
        _toast.TranslationY = 20;
        await Task.WhenAll(_toast.FadeToAsync(1, 160), _toast.TranslateToAsync(0, 0, 160, Easing.CubicOut));
        await Task.Delay(2200);
        await _toast.FadeToAsync(0, 200);
        _toast.IsVisible = false;
    }

    protected Task<bool> AskAsync(string title, string message, string yes = "Evet", string no = "Vazgeç") => DisplayAlertAsync(title, message, yes, no);

    /// <summary>Uygulama dışı bağlantı açar (arama, WhatsApp).</summary>
    protected async Task OpenAsync(string uri)
    {
        try { await Launcher.Default.OpenAsync(new Uri(uri)); }
        catch (Exception) { await DisplayAlertAsync("Açılamadı", "Bu işlem için uygun bir uygulama bulunamadı.", "Tamam"); }
    }
}
