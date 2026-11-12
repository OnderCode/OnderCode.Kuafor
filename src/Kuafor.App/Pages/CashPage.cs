using Kuafor.App.Controls;
using Kuafor.App.Theme;
using Kuafor.Core;

namespace Kuafor.App.Pages;

/// <summary>
/// Kasa: dönem seçimi (bugün, hafta, ay, 3 ay), ciro / gider / net, önceki döneme göre değişim,
/// günlük ciro grafiği, ödeme türleri halkası, en çok kazandıran hizmetler ve giderler.
/// </summary>
public sealed class CashPage : PageBase
{
    readonly Segmented _period = new("Bugün", "Bu hafta", "Bu ay", "3 ay");
    BarChart _chart = new();
    Donut _donut = new(128);

    protected override bool IsRoot => true;

    public CashPage()
    {
        _period.SelectedIndex = 2;
        _period.Changed += async _ => await ReloadAsync();
        AddFab(Ic.CashMinus, () => ExpenseEditPage.OpenAsync(Navigation), "Gider");
    }

    public int Period { set => _period.SelectedIndex = value; }

    (DateTime From, DateTime To, string Label) Range()
    {
        var today = Kuafor.Core.Clock.Today;
        return _period.SelectedIndex switch
        {
            0 => (today, today, "dün"),
            1 => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today, "geçen hafta"),
            3 => (today.AddDays(-89), today, "önceki 3 ay"),
            _ => (new DateTime(today.Year, today.Month, 1), today, "geçen ay"),
        };
    }

    protected override async Task LoadAsync()
    {
        var (from, to, prevLabel) = Range();
        // Grafikler her yüklemede yeniden oluşturulur (eski kartın içinde kalmasınlar)
        _chart = new BarChart();
        _donut = new Donut(128);
        var db = AppData.Db;
        var appts = await db.AllAppointmentsAsync();
        var items = await db.AllItemsAsync();
        var expenses = await db.AllExpensesAsync();
        var customers = await db.CustomersAsync();
        var s = Cash.Summarize(from, to, appts, items, expenses, customers, AppData.Profile.Week);
        var today = Kuafor.Core.Clock.Today;

        // Bekleyen (bugün ve dönem içinde henüz tamamlanmamış) tutar
        var pending = appts.Where(a => a.Start.Date >= from && a.Start.Date <= to && a.IsActive).Sum(a => a.Price);

        // Vitrin
        var change = s.Change is { } ch
            ? Parts.OnHero($"{(ch >= 0 ? "▲" : "▼")} %{Math.Abs(ch) * 100:0}  {prevLabel} aynı süreye göre", 13, F.Medium, 0.9)
            : Parts.OnHero(" ", 13);
        var heroBody = Ui.Stack(4,
            Parts.OnHero("Ciro", 14, F.Medium, 0.85),
            Parts.OnHero(Texts.Money(s.Revenue), 34, F.Bold),
            change,
            new BoxView { HeightRequest = 10, Color = Colors.Transparent },
            Ui.Columns(
                (HeroCell("Gider", Texts.MoneyShort(s.Expenses)), GridLength.Star),
                (HeroCell("Net", Texts.MoneyShort(s.Net)), GridLength.Star),
                (HeroCell("Bekleyen", Texts.MoneyShort(pending)), GridLength.Star)));
        var hero = Parts.Hero(heroBody);

        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], ColumnSpacing = 10, RowSpacing = 10 };
        grid.Add(Parts.Stat(Ic.CalendarCheck, Pal.Primary, s.Done.ToString(), "Tamamlanan", s.NewCustomers > 0 ? $"{s.NewCustomers} yeni müşteri" : null), 0, 0);
        grid.Add(Parts.Stat(Ic.ReceiptTextOutline, Pal.Pink, Texts.MoneyShort(s.AverageTicket), "Ortalama fiş"), 1, 0);
        grid.Add(Parts.Stat(Ic.ChartDonut, Pal.Teal, $"%{s.Occupancy * 100:0}", "Doluluk"), 0, 1);
        grid.Add(Parts.Stat(Ic.AccountCancel, s.NoShowRate > 0.05 ? Pal.Danger : Pal.Success, $"%{s.NoShowRate * 100:0.#}", "Gelmeme oranı", $"{s.NoShows} gelmedi · {s.Cancelled} iptal"), 1, 1);

        var views = new List<View> { Header("Kasa", PeriodText(from, to)), _period, hero, grid };

        // Grafik
        if (_period.SelectedIndex > 0)
        {
            List<double> values; List<string> labels; int hi;
            if (_period.SelectedIndex == 3)
            {
                // Haftalık toplamlar
                var weeks = s.Daily.GroupBy(d => (d.Day - from).Days / 7).Select(g => (Start: g.First().Day, Sum: g.Sum(x => x.Revenue))).ToList();
                values = [.. weeks.Select(w => (double)w.Sum)];
                labels = [.. weeks.Select((w, i) => i % 2 == 0 ? w.Start.ToString("d MMM", Texts.Tr) : "")];
                hi = weeks.Count - 1;
            }
            else
            {
                var days = _period.SelectedIndex == 1
                    ? Enumerable.Range(0, 7).Select(i => from.AddDays(i)).ToList()
                    : Enumerable.Range(0, DateTime.DaysInMonth(from.Year, from.Month)).Select(i => from.AddDays(i)).ToList();
                var map = s.Daily.ToDictionary(d => d.Day, d => d.Revenue);
                values = [.. days.Select(d => (double)map.GetValueOrDefault(d))];
                labels = [.. days.Select((d, i) => _period.SelectedIndex == 1 ? Texts.DayAbbr(d.DayOfWeek) : (d.Day == 1 || d.Day % 5 == 0 ? d.Day.ToString() : ""))];
                hi = days.IndexOf(today);
            }
            _chart.Set(values, labels, hi, v => Texts.MoneyShort((decimal)v));
            var best = s.Daily.Where(d => d.Revenue > 0).OrderByDescending(d => d.Revenue).FirstOrDefault();
            views.Add(Ui.Section(_period.SelectedIndex == 3 ? "Haftalık ciro" : "Günlük ciro"));
            views.Add(Ui.Card(Ui.Stack(8, _chart, best.Revenue > 0 ? Ui.Muted($"En iyi gün: {Texts.DayShort(best.Day)} · {Texts.Money(best.Revenue)}", 12.5) : Ui.Muted("", 1)), 14));
        }

        // Ödeme türleri
        if (s.Payments.Count > 0)
        {
            var colors = new Dictionary<PaymentMethod, Color> { [PaymentMethod.Card] = Pal.Now(Pal.Primary), [PaymentMethod.Cash] = Pal.Now(Pal.Success), [PaymentMethod.Transfer] = Pal.Now(Pal.Blue), [PaymentMethod.None] = Pal.Now(Pal.Faint) };
            _donut.Set(s.Payments.Select(p => ((double)p.Amount, colors[p.Method])), Texts.MoneyShort(s.Revenue), "tahsilat");
            var legend = Ui.Stack(12);
            foreach (var (m, amount) in s.Payments)
            {
                var dot = new BoxView { WidthRequest = 10, HeightRequest = 10, CornerRadius = 5, Color = colors[m], VerticalOptions = LayoutOptions.Center };
                legend.Add(Ui.Columns((dot, GridLength.Auto), (Ui.Stack(0, Ui.Text(Texts.Of(m), 14, F.Medium), Ui.Muted($"%{(s.Revenue == 0 ? 0 : amount / s.Revenue * 100):0}", 12)), GridLength.Star), (Ui.Text(Texts.MoneyShort(amount), 14, F.SemiBold), GridLength.Auto)));
            }
            legend.VerticalOptions = LayoutOptions.Center;
            var row = Ui.Columns((_donut, GridLength.Auto), (legend, GridLength.Star));
            row.ColumnSpacing = 18;
            views.Add(Ui.Section("Ödeme türleri"));
            views.Add(Ui.Card(row));
        }

        // Hizmetler
        if (s.Services.Count > 0)
        {
            var list = Ui.Stack(12);
            var max = s.Services.Max(x => x.Revenue);
            var svcColors = (await db.ServicesAsync()).ToDictionary(x => x.Name, x => x.ColorHex);
            foreach (var (name, count, rev) in s.Services.Take(6))
            {
                var color = Pal.Hex(svcColors.GetValueOrDefault(name, "#7C3AED"));
                var track = Ui.Progress(max == 0 ? 0 : (double)(rev / max), color);
                list.Add(Ui.Stack(6, Ui.Columns((Ui.Text(name, 14, F.Medium), GridLength.Star), (Ui.Muted($"{count} kez", 12.5), GridLength.Auto), (Ui.Text(Texts.MoneyShort(rev), 14, F.SemiBold), GridLength.Auto)), track));
            }
            views.Add(Ui.Section("En çok kazandıran hizmetler"));
            views.Add(Ui.Card(list));
        }

        // Giderler
        var exp = expenses.Where(e => e.Date.Date >= from && e.Date.Date <= to).OrderByDescending(e => e.Date).ToList();
        views.Add(Ui.Section("Giderler", "Ekle", () => ExpenseEditPage.OpenAsync(Navigation), exp.Count > 0 ? Texts.MoneyShort(exp.Sum(e => e.Amount)) : null));
        if (exp.Count == 0) views.Add(Ui.Card(Ui.Muted("Bu dönemde gider girilmemiş.", 13.5), 14));
        else
        {
            var list = Ui.Stack(12);
            foreach (var e in exp.Take(12))
            {
                var (glyph, color) = e.Category switch
                {
                    ExpenseCategory.Rent => (Ic.StorefrontOutline, Pal.Primary),
                    ExpenseCategory.Bills => (Ic.LightningBolt, Pal.Warning),
                    ExpenseCategory.Supplies => (Ic.SprayBottle, Pal.Pink),
                    _ => (Ic.TagOutline, Pal.Teal),
                };
                list.Add(Ui.ListRow(glyph, color, e.Note ?? Texts.Of(e.Category), $"{Texts.Of(e.Category)} · {e.Date.ToString("d MMM", Texts.Tr)}",
                    Ui.Text("−" + Texts.MoneyShort(e.Amount), 14, F.SemiBold, Pal.Danger)).OnTap(() => ExpenseEditPage.OpenAsync(Navigation, e.Id)));
            }
            views.Add(Ui.Card(list));
        }
        Show([.. views]);
    }

    static View HeroCell(string label, string value) => Ui.Stack(1, Parts.OnHero(label, 12.5, F.Medium, 0.75), Parts.OnHero(value, 17, F.SemiBold));

    static string PeriodText(DateTime from, DateTime to) => from == to
        ? Texts.DayLong(from)
        : $"{from.ToString("d MMM", Texts.Tr)} – {to.ToString("d MMM yyyy", Texts.Tr)}";
}

/// <summary>Gider ekle / düzenle.</summary>
public sealed class ExpenseEditPage : PageBase
{
    readonly int? _id;
    Expense _e = new();
    bool _loaded;
    readonly Segmented _cat = new("Malzeme", "Kira", "Fatura", "Diğer");
    readonly DatePicker _date = new() { Format = "d MMMM yyyy", FontFamily = F.Regular };
    (View View, Entry Entry) _amount, _note;

    ExpenseEditPage(int? id) => _id = id;
    public static Task OpenAsync(INavigation nav, int? id = null) => nav.PushAsync(new ExpenseEditPage(id));

    protected override async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        if (_id is { } id) _e = (await AppData.Db.AllExpensesAsync()).FirstOrDefault(x => x.Id == id) ?? new Expense();
        if (_e.Id == 0) _e.Date = Kuafor.Core.Clock.Today;
        _cat.SelectedIndex = (int)_e.Category;
        _date.Date = _e.Date;
        _date.MaximumDate = Kuafor.Core.Clock.Today.AddDays(31);
        _date.SetAppThemeColor(DatePicker.TextColorProperty, Pal.Text.L, Pal.Text.D);
        _amount = Ui.Field("Tutar (₺)", _e.Amount == 0 ? "" : _e.Amount.ToString("0.##", Texts.Tr), "0", Keyboard.Numeric, Ic.CurrencyTry);
        _note = Ui.Field("Açıklama", _e.Note, "ör. Boya ve oksidan", Keyboard.Text, Ic.NoteTextOutline);
        var date = Ui.Stack(6, Ui.Text("Tarih", 13, F.Medium, Pal.Muted), Ui.Box(_date, Pal.Surface, 8, 14));
        var views = new List<View>
        {
            Header(_id is null ? "Yeni gider" : "Gideri düzenle", back: true),
            Ui.Stack(6, Ui.Text("Tür", 13, F.Medium, Pal.Muted), _cat),
            _amount.View, date, _note.View,
            Ui.Button("Kaydet", Ic.Check, true, SaveAsync),
        };
        if (_e.Id != 0) views.Add(Ui.Button("Gideri sil", Ic.TrashCanOutline, false, async () =>
        {
            if (!await AskAsync("Gider silinsin mi?", "Bu kayıt kalıcı olarak silinecek.", "Sil")) return;
            await AppData.Db.DeleteExpenseAsync(_e.Id);
            await Navigation.PopAsync();
        }, Pal.Danger));
        Show([.. views]);
    }

    async Task SaveAsync()
    {
        if (!decimal.TryParse(_amount.Entry.Text, System.Globalization.NumberStyles.Number, Texts.Tr, out var amount) || amount <= 0)
        {
            await DisplayAlertAsync("Tutar", "Geçerli bir tutar yazın.", "Tamam");
            return;
        }
        _e.Amount = amount;
        _e.Category = (ExpenseCategory)Math.Max(0, _cat.SelectedIndex);
        _e.Date = _date.Date is DateTime picked ? picked.Date : Kuafor.Core.Clock.Today;
        _e.Note = string.IsNullOrWhiteSpace(_note.Entry.Text) ? null : _note.Entry.Text.Trim();
        await AppData.Db.SaveExpenseAsync(_e);
        AppData.NotifyChanged();
        await Navigation.PopAsync();
    }
}
