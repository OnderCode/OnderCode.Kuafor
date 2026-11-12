using Kuafor.App.Controls;
using Kuafor.App.Theme;
using Kuafor.Core;
using Microsoft.Maui.Controls.Shapes;

namespace Kuafor.App.Pages;

/// <summary>Müşteri listesi: arama (Türkçe harf duyarsız, telefonla da), filtre (sadık, gelme zamanı gelen, yeni, favori).</summary>
public sealed class CustomersPage : PageBase
{
    readonly Entry _search = new() { Placeholder = "Ad ya da telefon ara", FontFamily = F.Regular, FontSize = 15, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
    readonly Segmented _filter = new("Tümü", "Sadık", "Zamanı gelen", "Yeni");
    readonly VerticalStackLayout _list = new() { Spacing = 8 };
    readonly Label _count = Ui.Muted("", 13);
    readonly Border _searchBox;
    List<Customer> _customers = [];
    Dictionary<int, CustomerStats> _stats = [];
    int _limit = 40;

    protected override bool IsRoot => true;

    public CustomersPage()
    {
        _search.SetAppThemeColor(Entry.TextColorProperty, Pal.Text.L, Pal.Text.D);
        _search.SetAppThemeColor(Entry.PlaceholderColorProperty, Pal.Faint.L, Pal.Faint.D);
        _search.TextChanged += (_, _) => { _limit = 40; Render(); };
        _filter.Changed += _ => { _limit = 40; Render(); };
        AddFab(Ic.AccountPlus, () => CustomerEditPage.OpenAsync(Navigation));
        _searchBox = new Border
        {
            Content = Ui.Columns((Ui.Icon(Ic.Magnify, 20, Pal.Muted), GridLength.Auto), (_search, GridLength.Star)),
            Padding = new Thickness(14, 0), HeightRequest = 50, StrokeShape = new RoundRectangle { CornerRadius = 16 },
        }.Bg(Pal.Surface).Stroke(Pal.Border);
    }

    public int Filter { set => _filter.SelectedIndex = value; }

    protected override async Task LoadAsync()
    {
        _customers = await AppData.Db.CustomersAsync();
        _stats = await Agenda.StatsAsync();
        var today = Kuafor.Core.Clock.Today;
        var loyal = _stats.Values.Count(s => s.IsLoyal);
        var due = _stats.Values.Count(s => s.IsDue(today));
        var month = _customers.Count(c => c.CreatedAt >= today.AddDays(-30));
        var stats = Ui.Columns(
            (Parts.Stat(Ic.AccountGroup, Pal.Primary, _customers.Count.ToString(), "Müşteri"), GridLength.Star),
            (Parts.Stat(Ic.Crown, Pal.Warning, loyal.ToString(), "Sadık"), GridLength.Star),
            (Parts.Stat(Ic.AccountClock, Pal.Pink, due.ToString(), "Zamanı gelen"), GridLength.Star));
        stats.ColumnSpacing = 10;
        Show(Header("Müşteriler", month > 0 ? $"Son 30 günde {month} yeni müşteri" : null), stats, _searchBox, _filter, _count, _list);
        Render();
    }

    void Render()
    {
        var today = Kuafor.Core.Clock.Today;
        var q = Texts.Fold(_search.Text ?? "");
        var digits = new string((_search.Text ?? "").Where(char.IsDigit).ToArray());
        IEnumerable<Customer> list = _customers;
        if (q.Length > 0) list = list.Where(c => Texts.Fold(c.Name).Contains(q) || (digits.Length >= 3 && c.Phone.Contains(digits)));
        CustomerStats S(Customer c) => _stats.GetValueOrDefault(c.Id) ?? new CustomerStats(0, 0, null, null, null, null, 0, []);
        list = _filter.SelectedIndex switch
        {
            1 => list.Where(c => S(c).IsLoyal).OrderByDescending(c => S(c).Spent),
            2 => list.Where(c => S(c).IsDue(today)).OrderBy(c => S(c).ExpectedNext),
            3 => list.Where(c => c.CreatedAt >= today.AddDays(-30)).OrderByDescending(c => c.CreatedAt),
            _ => list.OrderByDescending(c => c.IsFavorite).ThenByDescending(c => S(c).LastVisit ?? c.CreatedAt),
        };
        var all = list.ToList();
        _count.Text = all.Count == 0 ? "" : $"{all.Count} müşteri";
        _list.Children.Clear();
        if (all.Count == 0)
        {
            _list.Add(Ui.Empty(Ic.AccountGroup, q.Length > 0 ? "Sonuç yok" : "Bu listede kimse yok", q.Length > 0 ? "Aramayı değiştirin ya da yeni müşteri ekleyin." : "Filtreyi değiştirerek diğer müşterileri görebilirsiniz."));
            return;
        }
        foreach (var c in all.Take(_limit))
        {
            var s = S(c);
            View? trailing = null;
            if (_filter.SelectedIndex == 2 && s.ExpectedNext is { } e)
            {
                var late = (today - e.Date).Days;
                trailing = Ui.Pill(late > 0 ? $"{late} gün geçti" : late == 0 ? "bugün" : $"{-late} gün kaldı", late > 7 ? Pal.Now(Pal.Danger) : Pal.Now(Pal.Warning), 11);
            }
            else if (_filter.SelectedIndex == 3) trailing = Ui.Pill("Yeni", Pal.Now(Pal.Teal), 11);
            _list.Add(Parts.CustomerRow(c, s, () => CustomerPage.OpenAsync(Navigation, c.Id), trailing));
        }
        if (all.Count > _limit)
            _list.Add(Ui.Button($"Daha fazla göster ({all.Count - _limit})", Ic.ChevronDown, false, () => { _limit += 60; Render(); return Task.CompletedTask; }, height: 46));
    }
}

/// <summary>Müşteri kartı: iletişim, istatistikler (ziyaret, harcama, ortalama aralık, beklenen sonraki gelişi), notlar, geçmiş.</summary>
public sealed class CustomerPage : PageBase
{
    readonly int _id;
    CustomerPage(int id) => _id = id;
    public static Task OpenAsync(INavigation nav, int id) => nav.PushAsync(new CustomerPage(id));

    protected override async Task LoadAsync()
    {
        Customer c;
        try { c = await AppData.Db.CustomerAsync(_id); }
        catch (InvalidOperationException) { await Navigation.PopAsync(); return; }
        var appts = await AppData.Db.CustomerAppointmentsAsync(_id);
        var views = await AppData.Db.ViewsAsync(appts);
        var now = Kuafor.Core.Clock.Now;
        var s = Insights.ForCustomer(appts, views.SelectMany(v => v.Items), now);

        var fav = Ui.IconButton(c.IsFavorite ? Ic.Star : Ic.StarOutline, c.IsFavorite ? Pal.Warning : Pal.Muted, Pal.Surface, 42, async () =>
        {
            c.IsFavorite = !c.IsFavorite;
            await AppData.Db.SaveCustomerAsync(c);
            await ReloadAsync();
            Toast(c.IsFavorite ? "Favorilere eklendi" : "Favorilerden çıkarıldı");
        });
        var edit = Ui.IconButton(Ic.Pencil, Pal.Text, Pal.Surface, 42, () => CustomerEditPage.OpenAsync(Navigation, c.Id));
        var head = new HorizontalStackLayout { Spacing = 8, Children = { fav, edit } };

        // Profil kartı
        var tags = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center };
        if (s.IsLoyal) tags.Add(Ui.Pill("Sadık müşteri", Pal.Now(Pal.Warning), 11.5, Ic.Crown));
        if (s.IsDue(now.Date)) tags.Add(Ui.Pill("Gelme zamanı", Pal.Now(Pal.Pink), 11.5, Ic.AccountClock));
        if (Insights.DaysToBirthday(c, now.Date, 14) is { } bd) tags.Add(Ui.Pill(bd == 0 ? "Bugün doğum günü" : $"Doğum günü {bd} gün sonra", Pal.Now(Pal.Teal), 11.5, Ic.CakeVariant));
        if (s.Visits == 0) tags.Add(Ui.Pill("Yeni müşteri", Pal.Now(Pal.Blue), 11.5));
        var avatar = Ui.Avatar(c.Name, 84, c.IsFavorite);
        avatar.HorizontalOptions = LayoutOptions.Center;
        var name = Ui.Text(c.Name, 22, F.Bold); name.HorizontalTextAlignment = TextAlignment.Center;
        var phone = Ui.Muted(string.IsNullOrEmpty(c.Phone) ? "Telefon yok" : Phone.Format(c.Phone), 15); phone.HorizontalTextAlignment = TextAlignment.Center;
        var actions = Ui.Columns(
            (QuickAction(Ic.Phone, "Ara", Pal.Primary, () => OpenAsync(Phone.TelUri(c.Phone))), GridLength.Star),
            (QuickAction(Ic.Whatsapp, "WhatsApp", Pal.Success, () => OpenAsync(Phone.WhatsAppUri(c.Phone, s.IsDue(now.Date) ? Agenda.InviteText(c) : "Merhaba " + Texts.FirstName(c.Name) + ", "))), GridLength.Star),
            (QuickAction(Ic.CalendarPlus, "Randevu", Pal.Pink, () => AppointmentEditPage.OpenAsync(Navigation, customerId: c.Id)), GridLength.Star));
        actions.ColumnSpacing = 10;
        var profile = Ui.Card(Ui.Stack(8, avatar, name, phone, tags, new BoxView { HeightRequest = 4, Color = Colors.Transparent }, actions), 18, 24);

        // İstatistikler
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], ColumnSpacing = 10, RowSpacing = 10 };
        grid.Add(Parts.Stat(Ic.History, Pal.Primary, s.Visits.ToString(), "Ziyaret", s.LastVisit is { } lv ? "son " + Texts.Ago(lv, now) : null), 0, 0);
        grid.Add(Parts.Stat(Ic.CashMultiple, Pal.Success, Texts.MoneyShort(s.Spent), "Toplam harcama", s.Visits > 0 ? "ort. " + Texts.MoneyShort(s.AverageTicket) : null), 1, 0);
        grid.Add(Parts.Stat(Ic.Repeat, Pal.Pink, s.AverageIntervalDays is { } ai ? $"{ai:0} gün" : "—", "Gelme aralığı", s.ExpectedNext is { } en && s.NextAppointment is null ? "beklenen " + en.ToString("d MMM", Texts.Tr) : null), 0, 1);
        grid.Add(Parts.Stat(Ic.AccountCancel, s.NoShows > 0 ? Pal.Danger : Pal.Teal, s.NoShows.ToString(), "Gelmedi", s.NoShows == 0 && s.Visits > 0 ? "hep geldi" : null), 1, 1);

        var body = new List<View> { Header("Müşteri", back: true, trailing: head), profile };

        // Sıradaki randevu
        var next = views.Where(v => v.Appointment.IsActive && v.Appointment.End >= now).OrderBy(v => v.Appointment.Start).FirstOrDefault();
        if (next is not null)
        {
            body.Add(Ui.Section("Sıradaki randevu"));
            body.Add(Parts.AppointmentRow(next, () => AppointmentPage.OpenAsync(Navigation, next.Appointment.Id), true));
        }
        body.Add(grid);

        if (s.FavoriteServices.Count > 0)
        {
            var favs = Ui.Stack(10);
            var max = s.FavoriteServices.Max(f => f.Count);
            foreach (var (svc, count) in s.FavoriteServices.Take(4))
            {
                var track = Ui.Progress((double)count / max, Pal.Now(Pal.Primary));
                favs.Add(Ui.Stack(5, Ui.Columns((Ui.Text(svc, 14, F.Medium), GridLength.Star), (Ui.Muted($"{count} kez", 13), GridLength.Auto)), track));
            }
            body.Add(Ui.Section("Her zamanki hizmetleri"));
            body.Add(Ui.Card(favs));
        }

        var info = Ui.Stack(12);
        info.Add(Ui.ListRow(Ic.NoteTextOutline, Pal.Warning, "Not", string.IsNullOrWhiteSpace(c.Note) ? "Not eklenmemiş (ör. boya numarası, hassasiyet)" : c.Note, null, false));
        info.Add(Ui.ListRow(Ic.CakeVariant, Pal.Teal, "Doğum günü", c.HasBirthday ? $"{c.BirthDay} {Texts.Month(c.BirthMonth)}" : "Belirtilmemiş", null, false));
        info.Add(Ui.ListRow(Ic.AccountPlus, Pal.Blue, "Kayıt", c.CreatedAt.ToString("d MMMM yyyy", Texts.Tr), null, false));
        body.Add(Ui.Card(info).OnTap(() => CustomerEditPage.OpenAsync(Navigation, c.Id)));

        var history = views.Where(v => v != next).OrderByDescending(v => v.Appointment.Start).ToList();
        if (history.Count > 0)
        {
            body.Add(Ui.Section("Geçmiş", count: history.Count.ToString()));
            foreach (var v in history.Take(25)) body.Add(Parts.AppointmentRow(v, () => AppointmentPage.OpenAsync(Navigation, v.Appointment.Id), true));
        }
        Show([.. body]);
    }

    static View QuickAction(string glyph, string text, (Color L, Color D) color, Func<Task> tap)
    {
        var box = Ui.Box(Ui.Stack(4, Ui.Icon(glyph, 22, color).Center(), Ui.Text(text, 12.5, F.SemiBold, color).Center()), Pal.Soft(color.L, 0.1f), 10, 16);
        return box.OnTap(tap);
    }
}

/// <summary>Müşteri ekle / düzenle.</summary>
public sealed class CustomerEditPage : PageBase
{
    readonly int? _id;
    Customer _c = new();
    bool _loaded;
    (View View, Entry Entry) _name, _phone;
    (View View, Editor Editor) _note;
    readonly Picker _month = new() { Title = "Ay", FontFamily = F.Regular };
    readonly Picker _dayPick = new() { Title = "Gün", FontFamily = F.Regular };

    CustomerEditPage(int? id) => _id = id;
    public static Task OpenAsync(INavigation nav, int? id = null) => nav.PushAsync(new CustomerEditPage(id));

    protected override async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        if (_id is { } id) _c = await AppData.Db.CustomerAsync(id);
        _name = Ui.Field("Ad soyad", _c.Name, "ör. Ayşe Yılmaz", Keyboard.Text, Ic.Account);
        _phone = Ui.Field("Telefon", string.IsNullOrEmpty(_c.Phone) ? "" : Phone.Format(_c.Phone), "0636 000 00 00", Keyboard.Telephone, Ic.Phone);
        _note = Ui.Area("Not", _c.Note, "Boya numarası, saç tipi, hassasiyetler…");
        _month.ItemsSource = new[] { "—" }.Concat(Enumerable.Range(1, 12).Select(Texts.Month)).ToList();
        _dayPick.ItemsSource = new[] { "—" }.Concat(Enumerable.Range(1, 31).Select(i => i.ToString())).ToList();
        _month.SelectedIndex = _c.HasBirthday ? _c.BirthMonth : 0;
        _dayPick.SelectedIndex = _c.HasBirthday ? _c.BirthDay : 0;
        foreach (var p in new[] { _month, _dayPick })
        {
            p.SetAppThemeColor(Picker.TextColorProperty, Pal.Text.L, Pal.Text.D);
            p.SetAppThemeColor(Picker.TitleColorProperty, Pal.Muted.L, Pal.Muted.D);
        }
        var birthday = Ui.Stack(6, Ui.Text("Doğum günü (isteğe bağlı)", 13, F.Medium, Pal.Muted),
            Ui.Columns((Ui.Box(_dayPick, Pal.Surface, 6, 14), new GridLength(1, GridUnitType.Star)), (Ui.Box(_month, Pal.Surface, 6, 14), new GridLength(2, GridUnitType.Star))));
        var views = new List<View>
        {
            Header(_id is null ? "Yeni müşteri" : "Müşteriyi düzenle", back: true),
            _name.View, _phone.View, birthday, _note.View,
            Ui.Button("Kaydet", Ic.Check, true, SaveAsync),
        };
        if (_id is not null) views.Add(Ui.Button("Müşteriyi sil", Ic.TrashCanOutline, false, DeleteAsync, Pal.Danger));
        Show([.. views]);
    }

    async Task SaveAsync()
    {
        var name = _name.Entry.Text?.Trim() ?? "";
        if (name.Length < 2) { await DisplayAlertAsync("Ad soyad", "Müşterinin adını yazın.", "Tamam"); return; }
        var phone = _phone.Entry.Text ?? "";
        if (!string.IsNullOrWhiteSpace(phone) && !Phone.IsValid(phone)) { await DisplayAlertAsync("Telefon", "Numara 11 haneli olmalı (ör. 0636 052 41 87).", "Tamam"); return; }
        _c.Name = name;
        _c.Phone = phone;
        _c.Note = string.IsNullOrWhiteSpace(_note.Editor.Text) ? null : _note.Editor.Text.Trim();
        _c.BirthMonth = Math.Max(0, _month.SelectedIndex);
        _c.BirthDay = Math.Max(0, _dayPick.SelectedIndex);
        if (_c.BirthMonth == 0 || _c.BirthDay == 0) _c.BirthMonth = _c.BirthDay = 0;
        if (_c.Id == 0) _c.CreatedAt = Kuafor.Core.Clock.Now;
        await AppData.Db.SaveCustomerAsync(_c);
        AppData.NotifyChanged();
        await Navigation.PopAsync();
        if (_id is null) await CustomerPage.OpenAsync(Navigation, _c.Id);
    }

    async Task DeleteAsync()
    {
        if (!await AskAsync("Müşteri silinsin mi?", $"{_c.Name} ve bütün randevu geçmişi silinecek. Bu işlem geri alınamaz.", "Sil")) return;
        await AppData.Db.DeleteCustomerAsync(_c.Id);
        AppData.NotifyChanged();
        var stack = Navigation.NavigationStack;
        // Düzenleme ve müşteri kartı birlikte kapansın
        if (stack.Count >= 2 && stack[^2] is CustomerPage) Navigation.RemovePage(stack[^2]);
        await Navigation.PopAsync();
    }
}

static class ViewExt
{
    public static Label Center(this Label l) { l.HorizontalTextAlignment = TextAlignment.Center; l.HorizontalOptions = LayoutOptions.Center; return l; }
}
