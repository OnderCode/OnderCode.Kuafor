using SQLite;

namespace Kuafor.Core;

/// <summary>Müşteri. Telefon yalnız arama ve WhatsApp hatırlatması içindir; adres ya da kimlik bilgisi tutulmaz.</summary>
[Table("Customers")]
public sealed class Customer
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed, MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(20)] public string Phone { get; set; } = "";
    /// <summary>Saç notları: boya formülü, tercih edilen kesim, hassasiyetler.</summary>
    [MaxLength(500)] public string? Note { get; set; }
    /// <summary>Doğum günü (yıl tutulmaz): ay ve gün.</summary>
    public int BirthMonth { get; set; }
    public int BirthDay { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }

    [Ignore] public bool HasBirthday => BirthMonth is >= 1 and <= 12 && BirthDay is >= 1 and <= 31;
    [Ignore] public string Initials => Texts.Initials(Name);
}

public enum ServiceCategory { Cut, Color, Styling, Care, Special, Men }

/// <summary>Verilen hizmet: süresi ve fiyatı randevu oluştururken varsayılan olarak kullanılır.</summary>
[Table("Services")]
public sealed class Service
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
    public ServiceCategory Category { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public decimal Price { get; set; }
    [MaxLength(9)] public string ColorHex { get; set; } = "#7C3AED";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum AppointmentStatus { Planned, Arrived, Done, NoShow, Cancelled }
public enum PaymentMethod { None, Cash, Card, Transfer }

/// <summary>Randevu. Fiyat ve süre, randevu anındaki hizmet kalemlerinden alınır; sonradan hizmet fiyatı değişse de geçmiş bozulmaz.</summary>
[Table("Appointments")]
public sealed class Appointment
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int CustomerId { get; set; }
    [Indexed] public DateTime Start { get; set; }
    public int DurationMinutes { get; set; }
    public AppointmentStatus Status { get; set; }
    /// <summary>Kalemlerin toplamı (indirim öncesi).</summary>
    public decimal ListPrice { get; set; }
    public decimal Discount { get; set; }
    /// <summary>Tamamlanınca tahsil edilen tutar.</summary>
    public decimal Paid { get; set; }
    public PaymentMethod Payment { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [Ignore] public DateTime End => Start.AddMinutes(DurationMinutes);
    [Ignore] public decimal Price => Math.Max(0, ListPrice - Discount);
    [Ignore] public bool IsActive => Status is AppointmentStatus.Planned or AppointmentStatus.Arrived;
    [Ignore] public bool BlocksTime => Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow);
}

/// <summary>Randevudaki hizmet kalemi (ad, süre ve fiyatın o anki kopyası).</summary>
[Table("AppointmentItems")]
public sealed class AppointmentItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int AppointmentId { get; set; }
    public int ServiceId { get; set; }
    [MaxLength(60)] public string Name { get; set; } = "";
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
}

public enum ExpenseCategory { Supplies, Rent, Bills, Other }

[Table("Expenses")]
public sealed class Expense
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public DateTime Date { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(120)] public string? Note { get; set; }
}

/// <summary>Anahtar-değer ayarlar (işletme adı, çalışma saatleri, hatırlatma metni...).</summary>
[Table("Settings")]
public sealed class Setting
{
    [PrimaryKey, MaxLength(40)] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
