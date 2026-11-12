using SQLite;

namespace Kuafor.Core;

/// <summary>Randevu + müşteri + kalemler, ekranlarda birlikte gösterilmek için.</summary>
public sealed record AppointmentView(Appointment Appointment, Customer Customer, IReadOnlyList<AppointmentItem> Items)
{
    public string ServicesText => Items.Count == 0 ? "—" : string.Join(", ", Items.Select(i => i.Name));
    public string ColorHex { get; init; } = "#7C3AED";
}

/// <summary>Cihazdaki SQLite veritabanı. Bütün veri telefonda kalır; sunucu yok.</summary>
public sealed class KuaforDb
{
    static bool _batteries;
    readonly SQLiteAsyncConnection _db;
    public string Path { get; }

    public KuaforDb(string path)
    {
        if (!_batteries) { SQLitePCL.Batteries_V2.Init(); _batteries = true; }
        Path = path;
        _db = new SQLiteAsyncConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache | SQLiteOpenFlags.FullMutex);
    }

    public async Task InitAsync()
    {
        await _db.CreateTablesAsync(CreateFlags.None, typeof(Customer), typeof(Service), typeof(Appointment), typeof(AppointmentItem), typeof(Expense));
        await _db.CreateTableAsync<Setting>();
    }

    public Task CloseAsync() => _db.CloseAsync();

    // ---------- Ayarlar ----------
    public async Task<string?> GetAsync(string key) => (await _db.FindAsync<Setting>(key))?.Value;
    public Task SetAsync(string key, string value) => _db.InsertOrReplaceAsync(new Setting { Key = key, Value = value });

    public async Task<Profile> GetProfileAsync() => new(
        await GetAsync("business") ?? "Selin Kuaför",
        await GetAsync("owner") ?? "Selin",
        WorkWeek.Parse(await GetAsync("week")),
        await GetAsync("reminder") ?? Messages.DefaultReminder,
        await GetAsync("invite") ?? Messages.DefaultInvite,
        await GetAsync("theme") ?? "system");

    public async Task SaveProfileAsync(Profile p)
    {
        await SetAsync("business", p.Business.Trim());
        await SetAsync("owner", p.Owner.Trim());
        await SetAsync("week", p.Week.Serialize());
        await SetAsync("reminder", p.ReminderTemplate);
        await SetAsync("invite", p.InviteTemplate);
        await SetAsync("theme", p.Theme);
    }

    // ---------- Müşteriler ----------
    public Task<List<Customer>> CustomersAsync() => _db.Table<Customer>().ToListAsync();
    public Task<Customer> CustomerAsync(int id) => _db.GetAsync<Customer>(id);

    public async Task<int> SaveCustomerAsync(Customer c)
    {
        c.Name = c.Name.Trim();
        c.Phone = Phone.Normalize(c.Phone) ?? c.Phone.Trim();
        if (c.Id == 0) { c.CreatedAt = c.CreatedAt == default ? Clock.Now : c.CreatedAt; await _db.InsertAsync(c); }
        else await _db.UpdateAsync(c);
        return c.Id;
    }

    /// <summary>Müşteriyi ve bütün randevularını siler.</summary>
    public async Task DeleteCustomerAsync(int id)
    {
        var ids = (await _db.Table<Appointment>().Where(a => a.CustomerId == id).ToListAsync()).Select(a => a.Id).ToList();
        foreach (var aid in ids) await DeleteAppointmentAsync(aid);
        await _db.DeleteAsync<Customer>(id);
    }

    // ---------- Hizmetler ----------
    public async Task<List<Service>> ServicesAsync(bool activeOnly = false)
    {
        var all = await _db.Table<Service>().ToListAsync();
        return [.. all.Where(s => !activeOnly || s.IsActive).OrderBy(s => s.Category).ThenBy(s => s.SortOrder).ThenBy(s => s.Name, StringComparer.Create(Texts.Tr, false))];
    }

    public async Task SaveServiceAsync(Service s)
    {
        s.Name = s.Name.Trim();
        if (s.Id == 0) await _db.InsertAsync(s); else await _db.UpdateAsync(s);
    }

    // ---------- Randevular ----------
    public Task<List<Appointment>> AppointmentsAsync(DateTime from, DateTime to)
    {
        var f = from.Date; var t = to.Date.AddDays(1);
        return _db.Table<Appointment>().Where(a => a.Start >= f && a.Start < t).ToListAsync();
    }

    public Task<List<Appointment>> AllAppointmentsAsync() => _db.Table<Appointment>().ToListAsync();
    public Task<List<Appointment>> CustomerAppointmentsAsync(int customerId) => _db.Table<Appointment>().Where(a => a.CustomerId == customerId).ToListAsync();
    public Task<List<AppointmentItem>> AllItemsAsync() => _db.Table<AppointmentItem>().ToListAsync();

    public async Task<List<AppointmentItem>> ItemsAsync(IEnumerable<int> appointmentIds)
    {
        var set = appointmentIds.ToHashSet();
        if (set.Count == 0) return [];
        if (set.Count == 1) { var id = set.First(); return await _db.Table<AppointmentItem>().Where(i => i.AppointmentId == id).ToListAsync(); }
        return [.. (await _db.Table<AppointmentItem>().ToListAsync()).Where(i => set.Contains(i.AppointmentId))];
    }

    /// <summary>Randevuları müşteri ve kalemleriyle birlikte getirir (saat sırasıyla).</summary>
    public async Task<List<AppointmentView>> ViewsAsync(IEnumerable<Appointment> appointments)
    {
        var list = appointments.OrderBy(a => a.Start).ToList();
        if (list.Count == 0) return [];
        var items = (await ItemsAsync(list.Select(a => a.Id))).ToLookup(i => i.AppointmentId);
        var custIds = list.Select(a => a.CustomerId).ToHashSet();
        var customers = (await _db.Table<Customer>().ToListAsync()).Where(c => custIds.Contains(c.Id)).ToDictionary(c => c.Id);
        var services = (await _db.Table<Service>().ToListAsync()).ToDictionary(s => s.Id);
        return [.. list.Select(a =>
        {
            var its = items[a.Id].ToList();
            var color = its.Select(i => services.TryGetValue(i.ServiceId, out var s) ? s.ColorHex : null).FirstOrDefault(c => c is not null) ?? "#7C3AED";
            return new AppointmentView(a, customers.TryGetValue(a.CustomerId, out var c) ? c : new Customer { Name = "Silinmiş müşteri" }, its) { ColorHex = color };
        })];
    }

    public async Task<AppointmentView?> ViewAsync(int appointmentId)
    {
        var a = await _db.FindAsync<Appointment>(appointmentId);
        return a is null ? null : (await ViewsAsync([a])).FirstOrDefault();
    }

    /// <summary>Randevu kaydeder; çalışma saati ve çakışma kuralı denetlenir. Hata varsa mesajı döner.</summary>
    public async Task<string?> SaveAppointmentAsync(Appointment a, IReadOnlyList<Service> services, WorkWeek week, bool force = false)
    {
        if (services.Count == 0) return "En az bir hizmet seçin.";
        if (a.CustomerId == 0) return "Müşteri seçin.";
        a.DurationMinutes = a.DurationMinutes > 0 ? a.DurationMinutes : services.Sum(s => s.DurationMinutes);
        if (!force)
        {
            var sameDay = await AppointmentsAsync(a.Start, a.Start);
            if (SlotFinder.Check(a.Start, a.DurationMinutes, week, sameDay, a.Id == 0 ? null : a.Id) is { } error) return error;
        }
        a.ListPrice = services.Sum(s => s.Price);
        a.Discount = Math.Clamp(a.Discount, 0, a.ListPrice);
        await _db.RunInTransactionAsync(conn =>
        {
            if (a.Id == 0) { a.CreatedAt = a.CreatedAt == default ? Clock.Now : a.CreatedAt; conn.Insert(a); }
            else { conn.Update(a); conn.Execute("DELETE FROM AppointmentItems WHERE AppointmentId = ?", a.Id); }
            foreach (var s in services)
                conn.Insert(new AppointmentItem { AppointmentId = a.Id, ServiceId = s.Id, Name = s.Name, DurationMinutes = s.DurationMinutes, Price = s.Price });
        });
        return null;
    }

    public async Task SetStatusAsync(int id, AppointmentStatus status)
    {
        var a = await _db.GetAsync<Appointment>(id);
        a.Status = status;
        if (status != AppointmentStatus.Done) { a.Paid = 0; a.Payment = PaymentMethod.None; a.CompletedAt = null; }
        await _db.UpdateAsync(a);
    }

    /// <summary>Randevuyu tamamlar ve tahsilatı yazar.</summary>
    public async Task CompleteAsync(int id, decimal paid, PaymentMethod method)
    {
        var a = await _db.GetAsync<Appointment>(id);
        a.Status = AppointmentStatus.Done;
        a.Paid = Math.Max(0, paid);
        a.Payment = method;
        a.CompletedAt = Clock.Now;
        await _db.UpdateAsync(a);
    }

    public async Task MarkRemindedAsync(int id)
    {
        var a = await _db.GetAsync<Appointment>(id);
        a.ReminderSentAt = Clock.Now;
        await _db.UpdateAsync(a);
    }

    public async Task DeleteAppointmentAsync(int id)
    {
        await _db.ExecuteAsync("DELETE FROM AppointmentItems WHERE AppointmentId = ?", id);
        await _db.DeleteAsync<Appointment>(id);
    }

    // ---------- Giderler ----------
    public Task<List<Expense>> ExpensesAsync(DateTime from, DateTime to)
    {
        var f = from.Date; var t = to.Date.AddDays(1);
        return _db.Table<Expense>().Where(e => e.Date >= f && e.Date < t).ToListAsync();
    }

    public Task<List<Expense>> AllExpensesAsync() => _db.Table<Expense>().ToListAsync();
    public Task SaveExpenseAsync(Expense e) => e.Id == 0 ? _db.InsertAsync(e) : _db.UpdateAsync(e);
    public Task DeleteExpenseAsync(int id) => _db.DeleteAsync<Expense>(id);

    // ---------- Toplu ----------
    public async Task<bool> IsEmptyAsync() => await _db.Table<Service>().CountAsync() == 0 && await _db.Table<Customer>().CountAsync() == 0;

    /// <summary>Bütün veriyi siler (ayarlar korunur).</summary>
    public async Task ClearAsync()
    {
        await _db.DeleteAllAsync<AppointmentItem>();
        await _db.DeleteAllAsync<Appointment>();
        await _db.DeleteAllAsync<Customer>();
        await _db.DeleteAllAsync<Expense>();
        await _db.DeleteAllAsync<Service>();
    }

    internal SQLiteAsyncConnection Connection => _db;
}

/// <summary>İşletme profili ve tercihler.</summary>
public sealed record Profile(string Business, string Owner, WorkWeek Week, string ReminderTemplate, string InviteTemplate, string Theme);
