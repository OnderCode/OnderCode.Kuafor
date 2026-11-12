using Kuafor.App.Theme;
using Kuafor.Core;

namespace Kuafor.App.Pages;

/// <summary>Randevu ayrıntısı: müşteri, hizmet kalemleri, tutar; geldi / tamamla / gelmedi / iptal / düzenle / hatırlat.</summary>
public sealed class AppointmentPage : PageBase
{
    readonly int _id;

    AppointmentPage(int id) => _id = id;

    public static Task OpenAsync(INavigation nav, int id) => nav.PushAsync(new AppointmentPage(id));

    protected override async Task LoadAsync()
    {
        var v = await AppData.Db.ViewAsync(_id);
        if (v is null) { await Navigation.PopAsync(); return; }
        var a = v.Appointment;
        var c = v.Customer;
        var now = Kuafor.Core.Clock.Now;

        var customer = Ui.Columns(
            (Ui.Avatar(c.Name, 54, c.IsFavorite), GridLength.Auto),
            (Ui.Stack(2, Ui.Text(c.Name, 18, F.SemiBold), Ui.Muted(Phone.Format(c.Phone), 14)), GridLength.Star),
            (Ui.IconButton(Ic.Phone, Pal.Primary, Pal.PrimarySoft, 44, () => OpenAsync(Phone.TelUri(c.Phone))), GridLength.Auto),
            (Ui.IconButton(Ic.Whatsapp, Pal.Success, null, 44, () => SendReminderAsync(v)), GridLength.Auto));
        customer.ColumnSpacing = 10;
        foreach (var child in customer.Children.OfType<View>()) child.VerticalOptions = LayoutOptions.Center;
        var customerCard = Ui.Card(customer).OnTap(() => CustomerPage.OpenAsync(Navigation, c.Id));

        var when = Ui.Stack(10,
            Ui.ListRow(Ic.CalendarClock, Pal.Primary, Texts.DayLong(a.Start), $"{Texts.Time(a.Start)} – {Texts.Time(a.End)}  ·  {Texts.Duration(a.DurationMinutes)}  ·  {Texts.Until(a.Start, now)}", Ui.StatusPill(a.Status), false));
        if (a.ReminderSentAt is { } sent)
            when.Add(Ui.ListRow(Ic.BellRingOutline, Pal.Success, "Hatırlatma gönderildi", $"{sent:d MMM HH:mm}", null, false));
        if (!string.IsNullOrWhiteSpace(a.Note))
            when.Add(Ui.ListRow(Ic.NoteTextOutline, Pal.Warning, "Not", a.Note, null, false));
        if (!string.IsNullOrWhiteSpace(c.Note))
            when.Add(Ui.ListRow(Ic.Palette, Pal.Pink, "Müşteri notu", c.Note, null, false));

        var lines = Ui.Stack(10);
        foreach (var i in v.Items)
        {
            var g = Ui.Columns((Ui.Text(i.Name, 15, F.Medium), GridLength.Star), (Ui.Muted(Texts.Duration(i.DurationMinutes), 13), GridLength.Auto), (Ui.Text(Texts.Money(i.Price), 15, F.Medium), GridLength.Auto));
            lines.Add(g);
        }
        lines.Add(Ui.Divider());
        if (a.Discount > 0) lines.Add(Ui.Columns((Ui.Muted("İndirim", 14), GridLength.Star), (Ui.Text("−" + Texts.Money(a.Discount), 14, F.Medium, Pal.Success), GridLength.Auto)));
        lines.Add(Ui.Columns((Ui.Text("Toplam", 16, F.SemiBold), GridLength.Star), (Ui.Text(Texts.Money(a.Price), 18, F.Bold), GridLength.Auto)));
        if (a.Status == AppointmentStatus.Done)
            lines.Add(Ui.Columns((Ui.Muted($"Tahsil edildi · {Texts.Of(a.Payment)}", 14), GridLength.Star), (Ui.Text(Texts.Money(a.Paid), 15, F.SemiBold, Pal.Success), GridLength.Auto)));

        var actions = Ui.Stack(10);
        switch (a.Status)
        {
            case AppointmentStatus.Planned:
                actions.Add(Ui.Columns(
                    (Ui.Button("Geldi", Ic.AccountCheck, false, () => SetAsync(a, AppointmentStatus.Arrived, $"{Texts.FirstName(c.Name)} geldi")), GridLength.Star),
                    (Ui.Button("Tamamla", Ic.CashPlus, true, () => CompletePage.OpenAsync(Navigation, v)), GridLength.Star)));
                actions.Add(Ui.Columns(
                    (Ui.Button("Gelmedi", Ic.AccountCancel, false, () => SetAsync(a, AppointmentStatus.NoShow, "Gelmedi olarak işaretlendi"), Pal.Danger), GridLength.Star),
                    (Ui.Button("İptal et", Ic.CalendarRemove, false, () => CancelAsync(a), Pal.Muted), GridLength.Star)));
                break;
            case AppointmentStatus.Arrived:
                actions.Add(Ui.Button("Tamamla ve tahsil et", Ic.CashPlus, true, () => CompletePage.OpenAsync(Navigation, v)));
                break;
            default:
                actions.Add(Ui.Button("Geri al (bekleniyor yap)", Ic.UndoVariant, false, () => SetAsync(a, AppointmentStatus.Planned, "Randevu yeniden bekleniyor")));
                break;
        }
        if (a.IsActive)
            actions.Add(Ui.Button("WhatsApp ile hatırlat", Ic.Whatsapp, false, () => SendReminderAsync(v), Pal.Success));
        actions.Add(Ui.Columns(
            (Ui.Button("Düzenle", Ic.Pencil, false, () => AppointmentEditPage.OpenAsync(Navigation, a.Id)), GridLength.Star),
            (Ui.Button("Benzerini oluştur", Ic.Repeat, false, () => AppointmentEditPage.OpenAsync(Navigation, copyFrom: a.Id)), GridLength.Star)));
        var delete = Ui.Text("Randevuyu sil", 14, F.SemiBold, Pal.Danger);
        delete.HorizontalOptions = LayoutOptions.Center;
        delete.Margin = new Thickness(0, 6);
        delete.OnTap(() => DeleteAsync(a));
        actions.Add(delete);

        Show(Header("Randevu", back: true), customerCard, Ui.Card(when, 14), Ui.Section("Hizmetler"), Ui.Card(lines), actions);
    }

    async Task SetAsync(Appointment a, AppointmentStatus status, string message)
    {
        await AppData.Db.SetStatusAsync(a.Id, status);
        AppData.NotifyChanged();
        Toast(message);
        await ReloadAsync();
    }

    async Task CancelAsync(Appointment a)
    {
        if (!await AskAsync("Randevu iptal edilsin mi?", "Saat yeniden boşa çıkar. Randevu geçmişte iptal olarak görünür.", "İptal et")) return;
        await SetAsync(a, AppointmentStatus.Cancelled, "Randevu iptal edildi");
    }

    async Task DeleteAsync(Appointment a)
    {
        if (!await AskAsync("Randevu silinsin mi?", "Bu işlem geri alınamaz.", "Sil")) return;
        await AppData.Db.DeleteAppointmentAsync(a.Id);
        AppData.NotifyChanged();
        await Navigation.PopAsync();
    }

    async Task SendReminderAsync(AppointmentView v)
    {
        await OpenAsync(Phone.WhatsAppUri(v.Customer.Phone, Agenda.ReminderText(v)));
        if (v.Appointment.IsActive) await AppData.Db.MarkRemindedAsync(v.Appointment.Id);
        AppData.NotifyChanged();
    }
}

/// <summary>Tahsilat: tutar (indirim kısayolları), ödeme yöntemi.</summary>
public sealed class CompletePage : PageBase
{
    readonly AppointmentView _v;
    static readonly PaymentMethod[] Methods = [PaymentMethod.Card, PaymentMethod.Cash, PaymentMethod.Transfer];

    CompletePage(AppointmentView v) => _v = v;

    public static Task OpenAsync(INavigation nav, AppointmentView v) => nav.PushAsync(new CompletePage(v));

    protected override Task LoadAsync()
    {
        var a = _v.Appointment;
        var amount = Ui.Field("Tahsil edilen tutar (₺)", a.Price.ToString("0.##", Texts.Tr), keyboard: Keyboard.Numeric, glyph: Ic.CurrencyTry);
        amount.Entry.FontSize = 22;
        amount.Entry.FontFamily = F.Bold;
        var method = new Controls.Segmented("Kart", "Nakit", "Havale");
        var quick = new HorizontalStackLayout { Spacing = 8 };
        foreach (var (label, value) in new (string, decimal)[] { ("Tam tutar", a.Price), ("%10 indirim", Math.Round(a.Price * 0.9m / 10) * 10), ("Yuvarla", Math.Floor(a.Price / 50) * 50) })
            quick.Add(Ui.Pill($"{label} · {Texts.Money(value)}", Pal.Now(Pal.Primary), 12.5).OnTap(() => { amount.Entry.Text = value.ToString("0.##", Texts.Tr); return Task.CompletedTask; }));

        var summary = Ui.Card(Ui.Stack(8,
            Ui.Columns((Ui.Avatar(_v.Customer.Name, 44), GridLength.Auto), (Ui.Stack(2, Ui.Text(_v.Customer.Name, 16, F.SemiBold), Ui.Muted(_v.ServicesText, 13, 2)), GridLength.Star)),
            Ui.Divider(),
            Ui.Columns((Ui.Muted("Randevu tutarı", 14), GridLength.Star), (Ui.Text(Texts.Money(a.Price), 15, F.SemiBold), GridLength.Auto))));

        var save = Ui.Button("Tamamla", Ic.Check, true, async () =>
        {
            if (!decimal.TryParse(amount.Entry.Text?.Replace("₺", "").Trim(), System.Globalization.NumberStyles.Number, Texts.Tr, out var paid) || paid < 0)
            {
                await DisplayAlertAsync("Tutar", "Geçerli bir tutar girin.", "Tamam");
                return;
            }
            await AppData.Db.CompleteAsync(a.Id, paid, Methods[method.SelectedIndex]);
            AppData.NotifyChanged();
            await Navigation.PopAsync();
        });

        Show(Header("Tahsilat", "Randevuyu tamamla", back: true), summary, amount.View,
            new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = quick, HorizontalScrollBarVisibility = ScrollBarVisibility.Never },
            Ui.Text("Ödeme yöntemi", 13, F.Medium, Pal.Muted), method, new BoxView { HeightRequest = 8, Color = Colors.Transparent }, save);
        return Task.CompletedTask;
    }
}
