using Kuafor.Core;

namespace Kuafor.App;

/// <summary>Uygulama genelinde tek veritabanı ve işletme profili. İlk açılışta örnek veriyle doldurulur.</summary>
public static class AppData
{
    static KuaforDb? _db;
    static Task? _init;

    public static KuaforDb Db => _db ?? throw new InvalidOperationException("Veritabanı hazır değil.");
    public static Profile Profile { get; private set; } = new("Selin Kuaför", "Selin", WorkWeek.Default, Messages.DefaultReminder, Messages.DefaultInvite, "system");

    /// <summary>Veri değişti (randevu, müşteri, ayar...). Açık sayfalar kendini tazeler.</summary>
    public static event Action? Changed;
    public static void NotifyChanged() => Changed?.Invoke();

    public static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "kuafor.db3");

    public static Task EnsureAsync() => _init ??= InitAsync();

    static async Task InitAsync()
    {
        if (Shots.Requested is not null) { Shots.Prepare(); if (File.Exists(DbPath)) File.Delete(DbPath); }
        _db = new KuaforDb(DbPath);
        await _db.InitAsync();
        if (await _db.IsEmptyAsync() && await _db.GetAsync("seeded") is null)
        {
            await DemoData.SeedAsync(_db);
            await _db.SetAsync("seeded", "1");
            await _db.SetAsync("demo", "1");
        }
        Profile = await _db.GetProfileAsync();
    }

    public static async Task SaveProfileAsync(Profile p)
    {
        await Db.SaveProfileAsync(p);
        Profile = p;
        App.ApplyTheme(p.Theme);
        NotifyChanged();
    }

    public static async Task<bool> IsDemoAsync() => await Db.GetAsync("demo") == "1";

    /// <summary>Örnek veriyi silip boş başlar (hizmet listesi korunur).</summary>
    public static async Task StartFreshAsync()
    {
        var services = await Db.ServicesAsync();
        await Db.ClearAsync();
        foreach (var s in services) { s.Id = 0; await Db.SaveServiceAsync(s); }
        await Db.SetAsync("demo", "0");
        NotifyChanged();
    }

    public static async Task ResetDemoAsync()
    {
        await Db.ClearAsync();
        await DemoData.SeedAsync(Db);
        await Db.SetAsync("demo", "1");
        Profile = await Db.GetProfileAsync();
        NotifyChanged();
    }
}
