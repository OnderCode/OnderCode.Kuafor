using Kuafor.App.Pages;
using Kuafor.Core;

namespace Kuafor.App;

/// <summary>
/// Ekran görüntüsü modu (yalnızca geliştirme): uygulama "shots" bayrağıyla açılırsa saati sabitler, örnek veriyi
/// yeniden üretir, sayfaları sırayla gezip PNG olarak kaydeder. Normal kullanımda hiç devreye girmez.
/// </summary>
public static class Shots
{
    public static string? Requested { get; set; }

    public static void Prepare()
    {
        var fixedNow = DateTime.Today.AddHours(11).AddMinutes(20);
        Kuafor.Core.Clock.Set(() => fixedNow);
    }

    public static async Task RunAsync()
    {
        var dir = Requested!;
        try
        {
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            App.ApplyTheme("light");
            await Task.Delay(3000);
            var shell = Shell.Current;
            INavigation Nav() => shell.Navigation;
            async Task<PageBase> Current()
            {
                for (var i = 0; i < 50; i++)
                {
                    if (shell.CurrentPage is PageBase p) return p;
                    await Task.Delay(100);
                }
                throw new InvalidOperationException("Sayfa bulunamadı");
            }

            await Snap(dir, "01-bugun");
            await (await Current()).ScrollToAsync(620);
            await Snap(dir, "02-bugun-program");

            var today = Kuafor.Core.Clock.Today;
            var now = Kuafor.Core.Clock.Now;
            var day = await Agenda.DayAsync(today);
            var next = day.Where(v => v.Appointment.IsActive && v.Appointment.End > now).OrderBy(v => v.Appointment.Start).FirstOrDefault();
            if (next is not null)
            {
                await AppointmentPage.OpenAsync(Nav(), next.Appointment.Id);
                await Snap(dir, "03-randevu");
                await Nav().PopAsync(false);
            }

            var tomorrow = (await Agenda.DayAsync(today.AddDays(1))).FirstOrDefault(v => v.Items.Count >= 2) ?? (await Agenda.DayAsync(today.AddDays(1))).FirstOrDefault();
            await AppointmentEditPage.OpenAsync(Nav(), copyFrom: tomorrow?.Appointment.Id);
            await Task.Delay(800);
            await (await Current()).ScrollToAsync(330);
            await Snap(dir, "04-yeni-randevu");
            await Nav().PopAsync(false);

            await AppShell.GoAsync("calendar");
            await Snap(dir, "05-takvim");

            await AppShell.GoAsync("customers");
            await Snap(dir, "06-musteriler");

            var stats = await Agenda.StatsAsync();
            var customers = await AppData.Db.CustomersAsync();
            var star = customers.Where(c => c.IsFavorite && !string.IsNullOrEmpty(c.Note) && stats.TryGetValue(c.Id, out var s) && s.Visits >= 5)
                .OrderByDescending(c => stats[c.Id].Visits).FirstOrDefault() ?? customers.OrderByDescending(c => stats.GetValueOrDefault(c.Id)?.Visits ?? 0).First();
            await CustomerPage.OpenAsync(Nav(), star.Id);
            await Snap(dir, "07-musteri");
            await Nav().PopAsync(false);

            await AppShell.GoAsync("cash");
            await Snap(dir, "08-kasa");
            await (await Current()).ScrollToAsync(560);
            await Snap(dir, "09-kasa-grafik");

            await AppShell.GoAsync("today");
            await Nav().PushAsync(new RemindersPage(), false);
            await Snap(dir, "10-hatirlatma");
            await Nav().PopAsync(false);

            await AppShell.GoAsync("more");
            await Snap(dir, "11-ayarlar");

            // Koyu tema
            App.ApplyTheme("dark");
            await Task.Delay(500);
            await AppShell.GoAsync("today");
            await Settle();
            await (await Current()).ScrollToAsync(0);
            await Snap(dir, "12-koyu-bugun");
            await AppShell.GoAsync("calendar");
            await Snap(dir, "13-koyu-takvim");
            await AppShell.GoAsync("cash");
            await Settle();
            await (await Current()).ScrollToAsync(0);
            await Snap(dir, "14-koyu-kasa");
            await (await Current()).ScrollToAsync(560);
            await Snap(dir, "15-koyu-kasa-grafik");
            App.ApplyTheme("light");
            await File.WriteAllTextAsync(Path.Combine(dir, "done.txt"), "ok");
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "error.txt"), ex.ToString());
        }
    }

    static async Task Settle() => await Task.Delay(1200);

    static async Task Snap(string dir, string name)
    {
        await Task.Delay(1600);
        var shot = await Screenshot.Default.CaptureAsync();
        if (shot is null) return;
        await using var src = await shot.OpenReadAsync(ScreenshotFormat.Png);
        await using var dst = File.Create(Path.Combine(dir, name + ".png"));
        await src.CopyToAsync(dst);
    }
}
