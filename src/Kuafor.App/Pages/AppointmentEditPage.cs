using Kuafor.App.Theme;
using Kuafor.Core;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace Kuafor.App.Pages;

/// <summary>
/// Yeni randevu / düzenleme: müşteri seç (ya da hızlıca ekle), hizmetleri seç (süre ve tutar toplanır),
/// gün seç (doluluk çubuğuyla), uygun saatlerden birine dokun. Çakışan ya da çalışma saati dışındaki saat zaten listelenmez.
/// </summary>
public sealed class AppointmentEditPage : PageBase
{
    readonly int? _id, _copyFrom, _presetCustomer;
    readonly DateTime? _presetStart;
    bool _loaded;

    List<Service> _services = [];
    List<Customer> _customers = [];
    Dictionary<int, CustomerStats> _stats = [];
    Customer? _customer;
    readonly List<int> _selected = [];
    DateTime _day;
    DateTime? _time;
    Appointment? _editing;

    readonly VerticalStackLayout _customerBox = new() { Spacing = 10 };
    readonly VerticalStackLayout _serviceBox = new() { Spacing = 10 };
    readonly HorizontalStackLayout _days = new() { Spacing = 8 };
    readonly VerticalStackLayout _timeBox = new() { Spacing = 10 };
    readonly Label _summary = Ui.Text("", 14, F.SemiBold, Pal.Primary);
    readonly Entry _search = new() { Placeholder = "Müşteri ara (ad ya da telefon)", FontFamily = F.Regular, FontSize = 15 };
    (View View, Editor Editor) _note;
    (View View, Entry Entry) _discount;

    AppointmentEditPage(int? id, DateTime? start, int? customerId, int? copyFrom)
    {
        _id = id; _presetStart = start; _presetCustomer = customerId; _copyFrom = copyFrom;
        _note = Ui.Area("Not (isteğe bağlı)", placeholder: "ör. boya numarası, özel istek");
        _discount = Ui.Field("İndirim (₺)", "0", keyboard: Keyboard.Numeric, glyph: Ic.TagOutline);
        _search.SetAppThemeColor(Entry.TextColorProperty, Pal.Text.L, Pal.Text.D);
        _search.SetAppThemeColor(Entry.PlaceholderColorProperty, Pal.Faint.L, Pal.Faint.D);
        _search.TextChanged += (_, _) => RenderCustomer();
    }

    public static Task OpenAsync(INavigation nav, int? id = null, DateTime? start = null, int? customerId = null, int? copyFrom = null) =>
        nav.PushAsync(new AppointmentEditPage(id, start, customerId, copyFrom));

    protected override async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        var db = AppData.Db;
        _services = await db.ServicesAsync(activeOnly: true);
        _customers = await db.CustomersAsync();
        _stats = await Agenda.StatsAsync();
        var now = Kuafor.Core.Clock.Now;
        _day = (_presetStart ?? now).Date;

        var source = _id ?? _copyFrom;
        if (source is { } sid && await db.ViewAsync(sid) is { } v)
        {
            _customer = v.Customer;
            _selected.AddRange(v.Items.Select(i => i.ServiceId).Where(id => _services.Any(s => s.Id == id)));
            _note.Editor.Text = v.Appointment.Note;
            if (_id is not null)
            {
                _editing = v.Appointment;
                _day = v.Appointment.Start.Date;
                _time = v.Appointment.Start;
                _discount.Entry.Text = v.Appointment.Discount.ToString("0.##", Texts.Tr);
            }
        }
        else if (_presetCustomer is { } cid) _customer = _customers.FirstOrDefault(c => c.Id == cid);
        if (_presetStart is { } ps && ps.TimeOfDay > TimeSpan.Zero) _time = ps;
        if (_editing is null && !AppData.Profile.Week[_day.DayOfWeek].IsOpen) _day = NextOpen(_day);

        var save = Ui.Button(_id is null ? "Randevuyu kaydet" : "Değişiklikleri kaydet", Ic.Check, true, SaveAsync);
        Show(
            Header(_id is null ? "Yeni randevu" : "Randevuyu düzenle", back: true),
            Step("1", "Müşteri"), Ui.Card(_customerBox, 14),
            Step("2", "Hizmetler"), _serviceBox, _summary,
            Step("3", "Gün"), new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _days, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Margin = new Thickness(-18, 0), Padding = new Thickness(18, 0) },
            Step("4", "Saat"), _timeBox,
            _note.View, _discount.View, save);
        RenderCustomer();
        RenderServices();
        await RenderDaysAsync();
    }

    static View Step(string n, string title)
    {
        var badge = new Border
        {
            WidthRequest = 24, HeightRequest = 24, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label { Text = n, FontFamily = F.Bold, FontSize = 12, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center },
        }.Bg(Pal.Primary);
        var row = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(2, 8, 0, 0) };
        row.Add(badge);
        var t = Ui.Heading(title, 16);
        t.VerticalTextAlignment = TextAlignment.Center;
        row.Add(t);
        return row;
    }

    // ---------- Müşteri ----------
    void RenderCustomer()
    {
        _customerBox.Children.Clear();
        if (_customer is { } sel)
        {
            var s = _stats.GetValueOrDefault(sel.Id);
            var sub = s is { Visits: > 0 } ? $"{Phone.Format(sel.Phone)}  ·  {s.Visits} ziyaret" : Phone.Format(sel.Phone);
            var change = Ui.Text("Değiştir", 14, F.SemiBold, Pal.Primary);
            change.VerticalTextAlignment = TextAlignment.Center;
            change.OnTap(() => { _customer = null; RenderCustomer(); return Task.CompletedTask; });
            _customerBox.Add(Ui.Columns((Ui.Avatar(sel.Name, 46, sel.IsFavorite), GridLength.Auto), (Ui.Stack(2, Ui.Text(sel.Name, 16, F.SemiBold), Ui.Muted(sub, 13)), GridLength.Star), (change, GridLength.Auto)));
            if (!string.IsNullOrWhiteSpace(sel.Note)) _customerBox.Add(Ui.Box(Ui.Columns((Ui.Icon(Ic.NoteTextOutline, 16, Pal.Warning), GridLength.Auto), (Ui.Text(sel.Note, 13, F.Regular, Pal.Text), GridLength.Star)), Pal.Soft(Pal.Now(Pal.Warning), 0.1f), 10, 12));
            return;
        }
        var searchBox = new Border { Content = Ui.Columns((Ui.Icon(Ic.Magnify, 20, Pal.Muted), GridLength.Auto), (_search, GridLength.Star)), Padding = new Thickness(12, 0), HeightRequest = 50, StrokeShape = new RoundRectangle { CornerRadius = 14 } }.Bg(Pal.SurfaceAlt);
        searchBox.StrokeThickness = 0;
        _customerBox.Add(searchBox);
        var q = Texts.Fold(_search.Text ?? "");
        var digits = new string((_search.Text ?? "").Where(char.IsDigit).ToArray());
        var matches = string.IsNullOrWhiteSpace(q)
            ? _customers.OrderByDescending(c => _stats.GetValueOrDefault(c.Id)?.LastVisit ?? DateTime.MinValue).Take(5)
            : _customers.Where(c => Texts.Fold(c.Name).Contains(q) || (digits.Length >= 3 && c.Phone.Contains(digits))).Take(6);
        var any = false;
        foreach (var m in matches)
        {
            any = true;
            var row = Ui.Columns((Ui.Avatar(m.Name, 38, m.IsFavorite), GridLength.Auto), (Ui.Stack(1, Ui.Text(m.Name, 15, F.Medium), Ui.Muted(Phone.Format(m.Phone), 12.5)), GridLength.Star));
            row.Padding = new Thickness(2, 4);
            _customerBox.Add(row.OnTap(() => { _customer = m; _search.Text = ""; RenderCustomer(); PreselectFavorite(m); return Task.CompletedTask; }));
        }
        if (!any) _customerBox.Add(Ui.Muted("Eşleşen müşteri yok.", 13));
        _customerBox.Add(Ui.Button("Yeni müşteri ekle", Ic.AccountPlus, false, NewCustomerAsync, height: 46));
    }

    void PreselectFavorite(Customer c)
    {
        if (_selected.Count > 0 || _stats.GetValueOrDefault(c.Id) is not { FavoriteServices.Count: > 0 } s) return;
        // Müşterinin her zamanki hizmetini öner
        var fav = s.FavoriteServices[0].Name;
        if (_services.FirstOrDefault(x => x.Name == fav) is { } svc) { _selected.Add(svc.Id); RenderServices(); _ = RenderTimesAsync(); }
    }

    async Task NewCustomerAsync()
    {
        var name = await DisplayPromptAsync("Yeni müşteri", "Ad soyad", "Devam", "Vazgeç", "ör. Ayşe Yılmaz", 80, Keyboard.Text, _search.Text ?? "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var phone = await DisplayPromptAsync("Telefon", $"{name.Trim()} için telefon numarası", "Kaydet", "Vazgeç", "0636 000 00 00", 20, Keyboard.Telephone);
        if (phone is null) return;
        if (!string.IsNullOrWhiteSpace(phone) && !Phone.IsValid(phone)) { await DisplayAlertAsync("Telefon", "Numara 11 haneli olmalı (ör. 0636 052 41 87).", "Tamam"); return; }
        var c = new Customer { Name = name.Trim(), Phone = phone ?? "" };
        await AppData.Db.SaveCustomerAsync(c);
        _customers.Add(c);
        _customer = c;
        _search.Text = "";
        RenderCustomer();
        AppData.NotifyChanged();
    }

    // ---------- Hizmetler ----------
    void RenderServices()
    {
        _serviceBox.Children.Clear();
        foreach (var group in _services.GroupBy(s => s.Category))
        {
            _serviceBox.Add(Ui.Text(Texts.Of(group.Key).ToUpper(Texts.Tr), 11.5, F.SemiBold, Pal.Muted));
            var flex = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row, JustifyContent = FlexJustify.Start };
            foreach (var s in group) flex.Add(ServiceChip(s));
            _serviceBox.Add(flex);
        }
        var chosen = _services.Where(s => _selected.Contains(s.Id)).ToList();
        _summary.Text = chosen.Count == 0 ? "Hizmet seçin" : $"Toplam  {Texts.Duration(chosen.Sum(s => s.DurationMinutes))}  ·  {Texts.Money(chosen.Sum(s => s.Price))}";
    }

    View ServiceChip(Service s)
    {
        var on = _selected.Contains(s.Id);
        var color = Pal.Hex(s.ColorHex);
        var row = new HorizontalStackLayout { Spacing = 6 };
        row.Add(on ? Ui.Icon(Ic.CheckCircle, 16, color) : new BoxView { WidthRequest = 8, HeightRequest = 8, CornerRadius = 4, Color = color, VerticalOptions = LayoutOptions.Center });
        row.Add(Ui.Stack(0, Ui.Text(s.Name, 13.5, on ? F.SemiBold : F.Medium), Ui.Muted($"{Texts.Duration(s.DurationMinutes)} · {Texts.Money(s.Price)}", 11.5)));
        var chip = new Border { Content = row, Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 8, 8), StrokeShape = new RoundRectangle { CornerRadius = 14 } };
        if (on) { chip.BackgroundColor = Pal.Soft(color, Pal.IsDark ? 0.25f : 0.1f); chip.Stroke = color; chip.StrokeThickness = 1.5; }
        else chip.Bg(Pal.Surface).Stroke(Pal.Border);
        return chip.OnTap(async () =>
        {
            if (!_selected.Remove(s.Id)) _selected.Add(s.Id);
            RenderServices();
            await RenderTimesAsync();
        });
    }

    int Minutes => _services.Where(s => _selected.Contains(s.Id)).Sum(s => s.DurationMinutes);

    // ---------- Gün ve saat ----------
    static DateTime NextOpen(DateTime d)
    {
        for (var i = 0; i < 14; i++) if (AppData.Profile.Week[d.AddDays(i).DayOfWeek].IsOpen) return d.AddDays(i);
        return d;
    }

    async Task RenderDaysAsync()
    {
        var today = Kuafor.Core.Clock.Today;
        var first = _day < today ? _day : today;
        var appts = await AppData.Db.AppointmentsAsync(first, first.AddDays(27));
        var week = AppData.Profile.Week;
        _days.Children.Clear();
        for (var i = 0; i < 28; i++)
        {
            var d = first.AddDays(i);
            var open = week[d.DayOfWeek].IsOpen;
            var on = d == _day;
            var occ = SlotFinder.Occupancy(d, week, appts.Where(a => a.Id != _editing?.Id));
            var bar = new Grid { HeightRequest = 4, WidthRequest = 30 };
            bar.Add(new BoxView { CornerRadius = 2, Color = on ? Colors.White.WithAlpha(0.35f) : Pal.Now(Pal.SurfaceAlt) });
            if (open && occ > 0) bar.Add(new BoxView { CornerRadius = 2, Color = on ? Colors.White : occ > 0.85 ? Pal.Now(Pal.Danger) : occ > 0.6 ? Pal.Now(Pal.Warning) : Pal.Now(Pal.Success), WidthRequest = Math.Max(4, 30 * occ), HorizontalOptions = LayoutOptions.Start });
            var cell = Ui.Stack(4,
                new Label { Text = d == today ? "Bugün" : Texts.DayAbbr(d.DayOfWeek), FontSize = 11.5, FontFamily = F.Medium, HorizontalTextAlignment = TextAlignment.Center, TextColor = on ? Colors.White : Pal.Now(Pal.Muted) },
                new Label { Text = d.Day.ToString(), FontSize = 19, FontFamily = F.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = on ? Colors.White : open ? Pal.Now(Pal.Text) : Pal.Now(Pal.Faint) },
                bar);
            var b = new Border { Content = cell, WidthRequest = 58, Padding = new Thickness(0, 10, 0, 10), StrokeShape = new RoundRectangle { CornerRadius = 16 } };
            if (on) { b.StrokeThickness = 0; b.Bg(Pal.Primary); }
            else { b.Bg(Pal.Surface).Stroke(Pal.Border); if (!open) b.Opacity = 0.45; }
            if (open) b.OnTap(async () => { _day = d; if (_time?.Date != d) _time = null; await RenderDaysAsync(); });
            _days.Add(b);
        }
        await RenderTimesAsync();
    }

    async Task RenderTimesAsync()
    {
        _timeBox.Children.Clear();
        var minutes = Minutes;
        if (minutes == 0) { _timeBox.Add(Ui.Card(Ui.Muted("Önce hizmet seçin; süreye göre uygun saatler listelenir.", 13.5), 14)); return; }
        var now = Kuafor.Core.Clock.Now;
        var dayAppts = await AppData.Db.AppointmentsAsync(_day, _day);
        var starts = SlotFinder.FreeStarts(_day, minutes, AppData.Profile.Week, dayAppts, _editing is null ? now : DateTime.MinValue, _editing?.Id).ToList();
        if (_time is { } t && t.Date == _day && !starts.Contains(t))
        {
            if (SlotFinder.Check(t, minutes, AppData.Profile.Week, dayAppts, _editing?.Id) is null) starts.Add(t);
            else _time = null;
        }
        if (starts.Count == 0)
        {
            _timeBox.Add(Ui.Card(Ui.Stack(4, Ui.Text("Bu gün için uygun saat yok", 15, F.SemiBold), Ui.Muted($"{Texts.Duration(minutes)} süren bir randevu sığmıyor. Başka bir gün seçin.", 13)), 14));
            return;
        }
        foreach (var (title, list) in new[] { ("Sabah", starts.Where(s => s.Hour < 12)), ("Öğleden sonra", starts.Where(s => s.Hour is >= 12 and < 17)), ("Akşam", starts.Where(s => s.Hour >= 17)) })
        {
            var items = list.OrderBy(x => x).ToList();
            if (items.Count == 0) continue;
            _timeBox.Add(Ui.Text($"{title.ToUpper(Texts.Tr)}  ·  {items.Count} uygun saat", 11.5, F.SemiBold, Pal.Muted));
            var flex = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };
            foreach (var s in items) flex.Add(TimeChip(s, minutes));
            _timeBox.Add(flex);
        }
    }

    View TimeChip(DateTime s, int minutes)
    {
        var on = _time == s;
        var label = new Label { Text = Texts.Time(s), FontFamily = on ? F.Bold : F.SemiBold, FontSize = 15, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        var chip = new Border { Content = label, WidthRequest = 76, HeightRequest = 44, Margin = new Thickness(0, 0, 8, 8), StrokeShape = new RoundRectangle { CornerRadius = 13 } };
        if (on) { chip.StrokeThickness = 0; chip.Bg(Pal.Primary); label.TextColor = Colors.White; }
        else { chip.Bg(Pal.Surface).Stroke(Pal.Border); label.Fg(Pal.Text); }
        return chip.OnTap(async () => { _time = s; await RenderTimesAsync(); });
    }

    // ---------- Kaydet ----------
    async Task SaveAsync()
    {
        if (_customer is null) { await DisplayAlertAsync("Müşteri", "Bir müşteri seçin ya da yeni müşteri ekleyin.", "Tamam"); return; }
        var chosen = _services.Where(s => _selected.Contains(s.Id)).ToList();
        if (chosen.Count == 0) { await DisplayAlertAsync("Hizmet", "En az bir hizmet seçin.", "Tamam"); return; }
        if (_time is null) { await DisplayAlertAsync("Saat", "Uygun saatlerden birini seçin.", "Tamam"); return; }
        _ = decimal.TryParse(_discount.Entry.Text, System.Globalization.NumberStyles.Number, Texts.Tr, out var discount);
        var a = _editing ?? new Appointment();
        a.CustomerId = _customer.Id;
        a.Start = _time.Value;
        a.DurationMinutes = chosen.Sum(s => s.DurationMinutes);
        a.Discount = Math.Max(0, discount);
        a.Note = string.IsNullOrWhiteSpace(_note.Editor.Text) ? null : _note.Editor.Text.Trim();
        if (_editing is null) a.Status = AppointmentStatus.Planned;
        if (await AppData.Db.SaveAppointmentAsync(a, chosen, AppData.Profile.Week) is { } error)
        {
            await DisplayAlertAsync("Kaydedilemedi", error, "Tamam");
            return;
        }
        AppData.NotifyChanged();
        await Navigation.PopAsync();
        if (Shell.Current?.CurrentPage is PageBase page) page.Toast($"{Texts.FirstName(_customer.Name)} · {a.Start:d MMMM HH:mm} kaydedildi");
    }
}
