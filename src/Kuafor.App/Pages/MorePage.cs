using Kuafor.App.Controls;
using Kuafor.App.Theme;
using Kuafor.Core;
using Microsoft.Maui.Controls.Shapes;

namespace Kuafor.App.Pages;

/// <summary>Ayarlar: işletme bilgisi, çalışma saatleri, hizmetler ve fiyatlar, mesaj şablonları, tema, yedek, örnek veri, hakkında.</summary>
public sealed class MorePage : PageBase
{
    protected override bool IsRoot => true;

    protected override async Task LoadAsync()
    {
        var p = AppData.Profile;
        var services = await AppData.Db.ServicesAsync();
        var demo = await AppData.IsDemoAsync();
        var openDays = p.Week.Days.Count(d => d.IsOpen);

        var avatar = new Border
        {
            WidthRequest = 60, HeightRequest = 60, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Background = new LinearGradientBrush([new GradientStop(Pal.Violet, 0), new GradientStop(Pal.Rose, 1)], new Point(0, 0), new Point(1, 1)),
            Content = Ui.Icon(Ic.ContentCut, 30, Colors.White),
        };
        var business = Ui.Card(Ui.Columns(
            (avatar, GridLength.Auto),
            (Ui.Stack(2, Ui.Text(p.Business, 19, F.Bold), Ui.Muted($"{p.Owner}  ·  {openDays} gün açık", 13.5)), GridLength.Star),
            (Ui.Icon(Ic.Pencil, 20, Pal.Muted), GridLength.Auto)), 16, 22).OnTap(() => Navigation.PushAsync(new ProfileEditPage()));
        foreach (var c in ((Grid)business.Content!).Children.OfType<View>()) c.VerticalOptions = LayoutOptions.Center;

        var work = Group(
            Row(Ic.ClockOutline, Pal.Primary, "Çalışma saatleri", HoursSummary(p.Week), () => Navigation.PushAsync(new HoursPage())),
            Row(Ic.ContentCut, Pal.Pink, "Hizmetler ve fiyatlar", $"{services.Count(s => s.IsActive)} aktif hizmet", () => Navigation.PushAsync(new ServicesPage())),
            Row(Ic.MessageProcessingOutline, Pal.Success, "Mesaj şablonları", "WhatsApp hatırlatma ve davet metni", () => Navigation.PushAsync(new MessagesPage())),
            Row(Ic.BellRingOutline, Pal.Warning, "Hatırlatma merkezi", "Yarının randevuları, gelme zamanı gelenler", () => Navigation.PushAsync(new RemindersPage())));

        var theme = new Segmented("Sistem", "Açık", "Koyu") { SelectedIndex = p.Theme switch { "light" => 1, "dark" => 2, _ => 0 } };
        theme.Changed += async i =>
        {
            await AppData.SaveProfileAsync(AppData.Profile with { Theme = i switch { 1 => "light", 2 => "dark", _ => "system" } });
            await ReloadAsync();
        };
        var look = Ui.Card(Ui.Stack(12, Ui.Columns((Ui.Box(Ui.Icon(Ic.ThemeLightDark, 20, Pal.Blue), Pal.Soft(Pal.Blue.L, 0.13f), 8, 12), GridLength.Auto), (Ui.Stack(1, Ui.Text("Görünüm", 15.5, F.SemiBold), Ui.Muted("Açık, koyu ya da telefonun ayarı", 12.5)), GridLength.Star)), theme), 16);

        var data = Group(
            Row(Ic.DatabaseExportOutline, Pal.Teal, "Yedeği paylaş", "Veritabanı dosyasını Drive, e-posta ya da WhatsApp ile kaydedin", ShareBackupAsync),
            demo
                ? Row(Ic.DeleteSweepOutline, Pal.Danger, "Örnek veriyi sil, sıfırdan başla", "Hizmet listesi korunur", StartFreshAsync)
                : Row(Ic.Refresh, Pal.Primary, "Örnek veriyi yükle", "Uygulamayı denemek için 240 müşterili örnek kuaför", ResetDemoAsync));

        var about = Group(
            Row(Ic.Github, Pal.Text, "Açık kaynak", "github.com/OnderCode/OnderCode.Kuafor", () => OpenAsync("https://github.com/OnderCode/OnderCode.Kuafor")),
            Row(Ic.InformationOutline, Pal.Muted, "Hakkında", $"OnderCode Kuaför {AppInfo.Current.VersionString} · MIT lisansı", () => Navigation.PushAsync(new AboutPage())));

        var views = new List<View> { Header("Ayarlar"), business };
        if (demo)
        {
            var banner = Ui.Box(Ui.Columns((Ui.Icon(Ic.FlashOutline, 20, Pal.Primary), GridLength.Auto), (Ui.Stack(2, Ui.Text("Örnek veriyle çalışıyorsunuz", 14, F.SemiBold, Pal.Primary), Ui.Muted("Bütün müşteriler ve numaralar uydurmadır (063…). Hazır olduğunuzda aşağıdan sıfırdan başlayın.", 12.5)), GridLength.Star)), Pal.PrimarySoft, 14, 16);
            views.Add(banner);
        }
        views.AddRange([Ui.Section("İşletme"), work, Ui.Section("Görünüm"), look, Ui.Section("Veri"), data, Ui.Section("Uygulama"), about]);
        Show([.. views]);
    }

    static string HoursSummary(WorkWeek w)
    {
        var open = w.Days.Where(d => d.IsOpen).ToList();
        if (open.Count == 0) return "Kapalı";
        var first = open[0];
        return $"{Texts.DayAbbr(open[0].Day)}–{Texts.DayAbbr(open[^1].Day)}  {first.Open:hh\\:mm}–{first.Close:hh\\:mm}";
    }

    internal static View Group(params View[] rows)
    {
        var s = new VerticalStackLayout { Spacing = 0 };
        for (var i = 0; i < rows.Length; i++)
        {
            if (i > 0) s.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(52, 0, 0, 0) }.Bg(Pal.Border));
            s.Add(rows[i]);
        }
        return Ui.Card(s, 6, 20);
    }

    internal static View Row(string glyph, (Color L, Color D) color, string title, string? sub, Func<Task> tap)
    {
        var r = Ui.ListRow(glyph, color, title, sub);
        r.Padding = new Thickness(10, 12);
        return r.OnTap(tap);
    }

    async Task ShareBackupAsync()
    {
        var copy = System.IO.Path.Combine(FileSystem.CacheDirectory, $"kuafor-yedek-{Kuafor.Core.Clock.Now:yyyyMMdd-HHmm}.db3");
        await AppData.Db.CloseAsync();
        File.Copy(AppData.DbPath, copy, true);
        await AppData.Db.InitAsync();
        await Share.Default.RequestAsync(new ShareFileRequest { Title = "Kuaför yedeği", File = new ShareFile(copy) });
    }

    async Task StartFreshAsync()
    {
        if (!await AskAsync("Sıfırdan başlansın mı?", "Örnek müşteriler, randevular ve giderler silinecek. Hizmet listesi ve fiyatlar kalır.", "Sıfırdan başla")) return;
        await AppData.StartFreshAsync();
        await ReloadAsync();
        Toast("Hazır! İlk müşterinizi ekleyin.");
    }

    async Task ResetDemoAsync()
    {
        if (!await AskAsync("Örnek veri yüklensin mi?", "Mevcut kayıtlar silinip örnek kuaför verisi yüklenecek.", "Yükle")) return;
        await AppData.ResetDemoAsync();
        await ReloadAsync();
        Toast("Örnek veri yüklendi");
    }
}

/// <summary>İşletme adı ve sahibi.</summary>
public sealed class ProfileEditPage : PageBase
{
    bool _loaded;
    protected override Task LoadAsync()
    {
        if (_loaded) return Task.CompletedTask;
        _loaded = true;
        var p = AppData.Profile;
        var business = Ui.Field("İşletme adı", p.Business, "ör. Selin Kuaför", Keyboard.Text, Ic.StorefrontOutline);
        var owner = Ui.Field("Adınız", p.Owner, "ör. Selin", Keyboard.Text, Ic.Account);
        Show(Header("İşletme", back: true),
            Ui.Muted("İşletme adı mesajlardaki {isletme} yerine yazılır; adınız ana ekrandaki selamlamada kullanılır.", 13.5),
            business.View, owner.View,
            Ui.Button("Kaydet", Ic.Check, true, async () =>
            {
                var b = business.Entry.Text?.Trim();
                if (string.IsNullOrEmpty(b)) { await DisplayAlertAsync("İşletme adı", "Bir ad yazın.", "Tamam"); return; }
                await AppData.SaveProfileAsync(AppData.Profile with { Business = b, Owner = owner.Entry.Text?.Trim() ?? "" });
                await Navigation.PopAsync();
            }));
        return Task.CompletedTask;
    }
}

/// <summary>Haftalık çalışma saatleri: her gün açık/kapalı, açılış-kapanış, öğle arası.</summary>
public sealed class HoursPage : PageBase
{
    bool _loaded;
    WorkWeek _week = WorkWeek.Default;

    protected override Task LoadAsync()
    {
        if (_loaded) return Task.CompletedTask;
        _loaded = true;
        _week = AppData.Profile.Week;
        Render();
        return Task.CompletedTask;
    }

    void Render()
    {
        var views = new List<View> { Header("Çalışma saatleri", back: true), Ui.Muted("Randevu ekranı yalnızca bu saatler içindeki boş zamanları önerir.", 13.5) };
        foreach (var d in _week.Days) views.Add(DayCard(d));
        views.Add(Ui.Button("Kaydet", Ic.Check, true, async () =>
        {
            await AppData.SaveProfileAsync(AppData.Profile with { Week = _week });
            await Navigation.PopAsync();
        }));
        Show([.. views]);
    }

    View DayCard(WorkDay d)
    {
        var sw = new Switch { IsToggled = d.IsOpen, VerticalOptions = LayoutOptions.Center };
        sw.SetAppThemeColor(Switch.OnColorProperty, Pal.Primary.L, Pal.Primary.D);
        sw.Toggled += (_, e) => { _week = _week.With(d with { IsOpen = e.Value }); Render(); };
        var head = Ui.Columns((Ui.Stack(1, Ui.Text(Texts.DayName(d.Day), 15.5, F.SemiBold), Ui.Muted(d.Text, 12.5)), GridLength.Star), (sw, GridLength.Auto));
        if (!d.IsOpen) return Ui.Card(head, 14);
        TimePicker Tp(TimeSpan t, Func<TimeSpan, WorkDay> apply)
        {
            var tp = new TimePicker { Time = t, Format = "HH:mm", FontFamily = F.SemiBold, FontSize = 15, HorizontalOptions = LayoutOptions.Center };
            tp.SetAppThemeColor(TimePicker.TextColorProperty, Pal.Text.L, Pal.Text.D);
            tp.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(TimePicker.Time) || tp.Time is not TimeSpan v || v == t) return;
                _week = _week.With(apply(v));
                Render();
            };
            return tp;
        }
        View Cell(string label, TimePicker tp) => Ui.Box(Ui.Stack(0, Ui.Muted(label, 11.5).Center(), tp), Pal.SurfaceAlt, 6, 12);
        var times = Ui.Columns(
            (Cell("Açılış", Tp(d.Open, v => d with { Open = v })), GridLength.Star),
            (Cell("Kapanış", Tp(d.Close, v => d with { Close = v })), GridLength.Star));
        times.ColumnSpacing = 10;
        var stack = Ui.Stack(12, head, times);
        if (d.HasBreak)
        {
            var br = Ui.Columns(
                (Cell("Ara başı", Tp(d.BreakStart!.Value, v => d with { BreakStart = v })), GridLength.Star),
                (Cell("Ara sonu", Tp(d.BreakEnd!.Value, v => d with { BreakEnd = v })), GridLength.Star),
                (Ui.IconButton(Ic.Close, Pal.Muted, Pal.SurfaceAlt, 40, () => { _week = _week.With(d with { BreakStart = null, BreakEnd = null }); Render(); return Task.CompletedTask; }), GridLength.Auto));
            br.ColumnSpacing = 10;
            stack.Add(br);
        }
        else stack.Add(Ui.Text("+ Öğle arası ekle", 13.5, F.SemiBold, Pal.Primary).OnTap(() => { _week = _week.With(d with { BreakStart = new(13, 0, 0), BreakEnd = new(13, 45, 0) }); Render(); return Task.CompletedTask; }));
        return Ui.Card(stack, 14);
    }
}

/// <summary>Hizmet listesi (kategoriye göre).</summary>
public sealed class ServicesPage : PageBase
{
    public ServicesPage() => AddFab(Ic.Plus, () => Navigation.PushAsync(new ServiceEditPage(null)));

    protected override async Task LoadAsync()
    {
        var services = await AppData.Db.ServicesAsync();
        var views = new List<View> { Header("Hizmetler", $"{services.Count} hizmet · süre ve fiyat", back: true) };
        foreach (var g in services.GroupBy(s => s.Category).OrderBy(g => g.Key))
        {
            views.Add(Ui.Section(Texts.Of(g.Key)));
            var rows = g.Select(s =>
            {
                var dot = new BoxView { WidthRequest = 12, HeightRequest = 12, CornerRadius = 6, Color = Pal.Hex(s.ColorHex), VerticalOptions = LayoutOptions.Center };
                var row = Ui.Columns((dot, GridLength.Auto), (Ui.Stack(1, Ui.Text(s.Name, 15, F.Medium), Ui.Muted(Texts.Duration(s.DurationMinutes) + (s.IsActive ? "" : "  ·  pasif"), 12.5)), GridLength.Star), (Ui.Text(Texts.Money(s.Price), 15, F.SemiBold), GridLength.Auto));
                row.ColumnSpacing = 14;
                row.Padding = new Thickness(12, 12);
                if (!s.IsActive) row.Opacity = 0.5;
                return (View)row.OnTap(() => Navigation.PushAsync(new ServiceEditPage(s)));
            }).ToArray();
            views.Add(MorePage.Group(rows));
        }
        Show([.. views]);
    }
}

/// <summary>Hizmet ekle / düzenle: ad, kategori, süre, fiyat, renk.</summary>
public sealed class ServiceEditPage : PageBase
{
    static readonly string[] Swatches = ["#7C3AED", "#DB2777", "#E11D48", "#EA580C", "#D97706", "#16A34A", "#0D9488", "#0284C7", "#2563EB", "#4F46E5", "#9333EA", "#64748B"];
    readonly Service _s;
    bool _loaded;
    string _color;
    int _minutes;
    readonly HorizontalStackLayout _swatches = new() { Spacing = 8 };
    readonly Label _duration = Ui.Text("", 20, F.Bold);

    public ServiceEditPage(Service? s)
    {
        _s = s ?? new Service { Category = ServiceCategory.Cut, DurationMinutes = 45, ColorHex = Swatches[0] };
        _color = _s.ColorHex;
        _minutes = _s.DurationMinutes;
    }

    protected override Task LoadAsync()
    {
        if (_loaded) return Task.CompletedTask;
        _loaded = true;
        var name = Ui.Field("Hizmet adı", _s.Name, "ör. Kadın saç kesimi", Keyboard.Text, Ic.ContentCut);
        var price = Ui.Field("Fiyat (₺)", _s.Price == 0 ? "" : _s.Price.ToString("0.##", Texts.Tr), "0", Keyboard.Numeric, Ic.CurrencyTry);
        var cats = Enum.GetValues<ServiceCategory>();
        var cat = new Picker { Title = "Kategori", ItemsSource = cats.Select(Texts.Of).ToList(), SelectedIndex = Array.IndexOf(cats, _s.Category), FontFamily = F.Regular };
        cat.SetAppThemeColor(Picker.TextColorProperty, Pal.Text.L, Pal.Text.D);
        var active = new Switch { IsToggled = _s.IsActive };
        active.SetAppThemeColor(Switch.OnColorProperty, Pal.Primary.L, Pal.Primary.D);

        var minus = Ui.IconButton(Ic.CashMinus, Pal.Text, Pal.SurfaceAlt, 44, () => { _minutes = Math.Max(15, _minutes - 15); _duration.Text = Texts.Duration(_minutes); return Task.CompletedTask; });
        ((Label)minus.Content!).Text = "−"; ((Label)minus.Content!).FontFamily = F.Bold; ((Label)minus.Content!).FontSize = 22;
        var plus = Ui.IconButton(Ic.Plus, Pal.Text, Pal.SurfaceAlt, 44, () => { _minutes = Math.Min(480, _minutes + 15); _duration.Text = Texts.Duration(_minutes); return Task.CompletedTask; });
        _duration.Text = Texts.Duration(_minutes);
        _duration.HorizontalTextAlignment = TextAlignment.Center; _duration.VerticalTextAlignment = TextAlignment.Center;
        var duration = Ui.Card(Ui.Columns((minus, GridLength.Auto), (_duration, GridLength.Star), (plus, GridLength.Auto)), 10, 16);

        RenderSwatches();
        var views = new List<View>
        {
            Header(_s.Id == 0 ? "Yeni hizmet" : "Hizmeti düzenle", back: true),
            name.View,
            Ui.Stack(6, Ui.Text("Kategori", 13, F.Medium, Pal.Muted), Ui.Box(cat, Pal.Surface, 6, 14)),
            Ui.Stack(6, Ui.Text("Süre", 13, F.Medium, Pal.Muted), duration),
            price.View,
            Ui.Stack(6, Ui.Text("Takvim rengi", 13, F.Medium, Pal.Muted), new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _swatches, HorizontalScrollBarVisibility = ScrollBarVisibility.Never }),
            Ui.Card(Ui.Columns((Ui.Stack(1, Ui.Text("Aktif", 15, F.SemiBold), Ui.Muted("Pasif hizmetler randevu ekranında görünmez", 12.5)), GridLength.Star), (active, GridLength.Auto)), 14),
            Ui.Button("Kaydet", Ic.Check, true, async () =>
            {
                var n = name.Entry.Text?.Trim();
                if (string.IsNullOrEmpty(n)) { await DisplayAlertAsync("Hizmet adı", "Bir ad yazın.", "Tamam"); return; }
                if (!decimal.TryParse(price.Entry.Text, System.Globalization.NumberStyles.Number, Texts.Tr, out var pr) || pr < 0) { await DisplayAlertAsync("Fiyat", "Geçerli bir fiyat yazın.", "Tamam"); return; }
                _s.Name = n; _s.Price = pr; _s.DurationMinutes = _minutes; _s.ColorHex = _color; _s.IsActive = active.IsToggled;
                _s.Category = cats[Math.Max(0, cat.SelectedIndex)];
                await AppData.Db.SaveServiceAsync(_s);
                await Navigation.PopAsync();
            }),
        };
        Show([.. views]);
        return Task.CompletedTask;
    }

    void RenderSwatches()
    {
        _swatches.Children.Clear();
        foreach (var hex in Swatches)
        {
            var on = string.Equals(hex, _color, StringComparison.OrdinalIgnoreCase);
            var b = new Border
            {
                WidthRequest = 40, HeightRequest = 40, StrokeShape = new RoundRectangle { CornerRadius = 20 },
                BackgroundColor = Pal.Hex(hex), StrokeThickness = on ? 3 : 0,
                Content = on ? Ui.Icon(Ic.Check, 20, Microsoft.Maui.Graphics.Colors.White) : null,
            };
            if (on) b.Stroke(Pal.Text, 3);
            _swatches.Add(b.OnTap(() => { _color = hex; RenderSwatches(); return Task.CompletedTask; }));
        }
    }
}

/// <summary>WhatsApp mesaj şablonları.</summary>
public sealed class MessagesPage : PageBase
{
    bool _loaded;
    protected override Task LoadAsync()
    {
        if (_loaded) return Task.CompletedTask;
        _loaded = true;
        var p = AppData.Profile;
        var reminder = Ui.Area("Randevu hatırlatma", p.ReminderTemplate, height: 120);
        var invite = Ui.Area("Gelme zamanı daveti", p.InviteTemplate, height: 110);
        var preview = Ui.Muted("", 13.5);
        void Update()
        {
            var sample = new Customer { Name = "Ayşe Yılmaz" };
            var a = new Appointment { Start = Kuafor.Core.Clock.Today.AddDays(1).AddHours(14) };
            preview.Text = Messages.Fill(reminder.Editor.Text ?? "", sample, AppData.Profile.Business, a, [new AppointmentItem { Name = "Dip boya" }, new AppointmentItem { Name = "Fön" }]);
        }
        reminder.Editor.TextChanged += (_, _) => Update();
        Update();
        var bubble = new Border
        {
            Content = preview, Padding = new Thickness(14, 10), StrokeThickness = 0, HorizontalOptions = LayoutOptions.End, MaximumWidthRequest = 320,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16, 4, 16, 16) },
        }.Bg((Pal.C(0xDCF8C6), Pal.C(0x005C4B)));
        preview.Fg((Pal.C(0x111B21), Pal.C(0xE9EDEF)));
        var tags = new HorizontalStackLayout { Spacing = 6 };
        foreach (var t in new[] { "{ad}", "{tarih}", "{saat}", "{hizmet}", "{isletme}" })
            tags.Add(Ui.Pill(t, Pal.Now(Pal.Primary), 12).OnTap(() => { reminder.Editor.Text += " " + t; return Task.CompletedTask; }));
        Show(Header("Mesaj şablonları", back: true),
            Ui.Muted("Mesajlar WhatsApp'ta hazır açılır; göndermeden önce düzenleyebilirsiniz. Yer tutucular otomatik doldurulur:", 13.5),
            new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = tags, HorizontalScrollBarVisibility = ScrollBarVisibility.Never },
            reminder.View,
            Ui.Text("ÖNİZLEME", 11.5, F.SemiBold, Pal.Muted), bubble,
            invite.View,
            Ui.Button("Kaydet", Ic.Check, true, async () =>
            {
                await AppData.SaveProfileAsync(AppData.Profile with { ReminderTemplate = reminder.Editor.Text ?? "", InviteTemplate = invite.Editor.Text ?? "" });
                await Navigation.PopAsync();
            }),
            Ui.Button("Varsayılana dön", Ic.UndoVariant, false, () => { reminder.Editor.Text = Messages.DefaultReminder; invite.Editor.Text = Messages.DefaultInvite; return Task.CompletedTask; }));
        return Task.CompletedTask;
    }
}

/// <summary>Hakkında: lisans ve üçüncü taraf bileşenler.</summary>
public sealed class AboutPage : PageBase
{
    protected override Task LoadAsync()
    {
        var logo = new Border
        {
            WidthRequest = 88, HeightRequest = 88, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 28 }, HorizontalOptions = LayoutOptions.Center,
            Background = new LinearGradientBrush([new GradientStop(Pal.Violet, 0), new GradientStop(Pal.Rose, 1)], new Point(0, 0), new Point(1, 1)),
            Content = Ui.Icon(Ic.ContentCut, 44, Microsoft.Maui.Graphics.Colors.White),
        };
        var lic = Ui.Stack(12,
            Ui.ListRow(Ic.Github, Pal.Text, "OnderCode Kuaför", "MIT lisansı · açık kaynak", null, false),
            Ui.ListRow(Ic.Brush, Pal.Primary, "Inter yazı tipi", "SIL Open Font License 1.1", null, false),
            Ui.ListRow(Ic.Palette, Pal.Pink, "Material Design Icons", "Apache License 2.0", null, false),
            Ui.ListRow(Ic.DatabaseExportOutline, Pal.Teal, "sqlite-net · SQLitePCLRaw", "MIT / Apache 2.0", null, false));
        Show(Header("Hakkında", back: true), logo,
            Ui.Text("OnderCode Kuaför", 22, F.Bold).Center(),
            Ui.Muted($"Sürüm {AppInfo.Current.VersionString}", 14).Center(),
            Ui.Muted("Tek başına çalışan, randevuyla iş yapan kuaförler için ücretsiz ve açık kaynak randevu, müşteri ve kasa uygulaması. Veriler yalnızca telefonunuzda tutulur; hesap, reklam ya da internet bağlantısı gerekmez.", 14).Center(),
            Ui.Card(lic),
            Ui.Button("GitHub'da aç", Ic.Github, true, () => OpenAsync("https://github.com/OnderCode/OnderCode.Kuafor")),
            Ui.Button("ondercode.com", Ic.ArrowRight, false, () => OpenAsync("https://ondercode.com")));
        return Task.CompletedTask;
    }
}
