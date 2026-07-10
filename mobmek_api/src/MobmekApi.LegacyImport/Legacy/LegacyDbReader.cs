using Microsoft.Data.SqlClient;

namespace MobmekApi.LegacyImport.Legacy;

/// <summary>
/// Reads the restored legacy MSSQL database with plain SQL (design §1.1 — no scaffolded EF
/// model of the old schema). One instance holds one open connection; the importer is
/// single-threaded so no pooling concerns.
/// </summary>
public sealed class LegacyDbReader(string connectionString) : IAsyncDisposable
{
    /// <summary>All legacy tables the import touches, in a stable report order.</summary>
    public static readonly string[] Tables =
    [
        "Customers", "Cars", "Makes", "CarMakes", "Services", "JobServices", "Jobs", "JobItems",
        "Labours", "Invoices", "InvoiceItems", "Quotations", "QuotationItems", "NewInvoices",
        "NewInvoiceItems", "NewQuotations", "NewQuotationItems", "Appointments", "Mechanics",
    ];

    private SqlConnection? _connection;

    private async Task<SqlConnection> ConnectAsync(CancellationToken ct)
    {
        if (_connection is null)
        {
            _connection = new SqlConnection(connectionString);
            await _connection.OpenAsync(ct);
        }

        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    /// <summary>Row count per table — the report's source baseline (§6).</summary>
    public async Task<IReadOnlyDictionary<string, int>> CountAllAsync(CancellationToken ct = default)
    {
        var counts = new Dictionary<string, int>();
        foreach (var table in Tables)
        {
            counts[table] = await QueryScalarAsync($"SELECT COUNT(*) FROM dbo.[{table}]", ct);
        }

        return counts;
    }

    public Task<List<LegacyCustomer>> CustomersAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Customers ORDER BY CustomerId",
        r => new LegacyCustomer(
            Int(r, "CustomerId"),
            Str(r, "CustomerName"),
            StrOrNull(r, "CustomerAddress"),
            StrOrNull(r, "CustomerEmail"),
            StrOrNull(r, "CustomerNumber"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyCar>> CarsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Cars ORDER BY CarId",
        r => new LegacyCar(
            Int(r, "CarId"),
            Str(r, "CarRego"),
            StrOrNull(r, "CarModel"),
            IntOrNull(r, "CarYear"),
            Int(r, "CustomerId"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyMake>> MakesAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Makes ORDER BY MakeId",
        r => new LegacyMake(Int(r, "MakeId"), Str(r, "MakeName")),
        ct);

    public Task<List<LegacyCarMake>> CarMakesAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.CarMakes ORDER BY CarId, MakeId",
        r => new LegacyCarMake(Int(r, "CarId"), Int(r, "MakeId")),
        ct);

    public Task<List<LegacyService>> ServicesAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Services ORDER BY ServiceId",
        r => new LegacyService(
            Int(r, "ServiceId"),
            Str(r, "Name"),
            StrOrNull(r, "Description"),
            Dec(r, "Price"),
            Bool(r, "IsActive"),
            Dt(r, "DateCreated"),
            DtOrNull(r, "DateUpdated")),
        ct);

    public Task<List<LegacyJobServiceJoin>> JobServiceJoinsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.JobServices ORDER BY JobServiceId",
        r => new LegacyJobServiceJoin(
            Int(r, "JobServiceId"),
            Int(r, "JobId"),
            Int(r, "ServiceId"),
            Dec(r, "AdditionalAmount"),
            Dt(r, "DateAdded")),
        ct);

    public Task<List<LegacyJob>> JobsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Jobs ORDER BY JobId",
        r => new LegacyJob(
            Int(r, "JobId"),
            Str(r, "Title"),
            Str(r, "Issue"),
            StrOrNull(r, "Notes"),
            StrOrNull(r, "Status"),
            IntOrNull(r, "Odometer"),
            DecOrNull(r, "TotalUnitProfit"),
            DecOrNull(r, "TotalJobProfit"),
            DecOrNull(r, "TotalJobPrice"),
            DateOrNull(r, "DateStarted"),
            DateOrNull(r, "DateFinished"),
            Int(r, "CarId"),
            IntOrNull(r, "MechanicId"),
            IntOrNull(r, "AppointmentId"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyJobItem>> JobItemsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.JobItems ORDER BY JobItemId",
        r => new LegacyJobItem(
            Int(r, "JobItemId"),
            Int(r, "JobId"),
            Str(r, "ItemName"),
            Dec(r, "TradePrice"),
            Dec(r, "RetailPrice"),
            Str(r, "MarkupSolution"),
            Dec(r, "Markup"),
            Dec(r, "SellingPrice"),
            Dec(r, "UnitProfit"),
            Int(r, "ItemQuantity"),
            Dec(r, "ItemTotal"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyLabour>> LaboursAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Labours ORDER BY LabourId",
        r => new LegacyLabour(
            Int(r, "LabourId"),
            Int(r, "JobId"),
            Str(r, "LabourName"),
            Dec(r, "LabourHours"),
            Dec(r, "LabourPrice"),
            Dec(r, "TotalLabour"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyInvoice>> InvoicesAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Invoices ORDER BY InvoiceId",
        r => new LegacyInvoice(
            Int(r, "InvoiceId"),
            Int(r, "CarId"),
            Str(r, "IssueName"),
            StrOrNull(r, "PaymentTerm"),
            StrOrNull(r, "Notes"),
            DecOrNull(r, "LaborPrice"),
            DecOrNull(r, "Discount"),
            DecOrNull(r, "ShippingFee"),
            DecOrNull(r, "SubTotal"),
            DecOrNull(r, "TaxAmount"),
            DecOrNull(r, "TotalAmount"),
            DecOrNull(r, "AmountPaid"),
            Bool(r, "IsPaid"),
            DtOrNull(r, "DueDate"),
            DtOrNull(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyInvoiceItem>> InvoiceItemsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.InvoiceItems ORDER BY InvoiceItemId",
        r => new LegacyInvoiceItem(
            Int(r, "InvoiceItemId"),
            Int(r, "InvoiceId"),
            Str(r, "ItemName"),
            Int(r, "Quantity"),
            Dec(r, "ItemPrice"),
            DecOrNull(r, "ItemTotal")),
        ct);

    public Task<List<LegacyQuotation>> QuotationsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Quotations ORDER BY QuotationId",
        r => new LegacyQuotation(
            Int(r, "QuotationId"),
            Int(r, "CarId"),
            Str(r, "IssueName"),
            StrOrNull(r, "Notes"),
            DecOrNull(r, "LaborPrice"),
            DecOrNull(r, "Discount"),
            DecOrNull(r, "ShippingFee"),
            DecOrNull(r, "SubTotal"),
            DecOrNull(r, "TaxAmount"),
            DecOrNull(r, "TotalAmount"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyQuotationItem>> QuotationItemsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.QuotationItems ORDER BY QuotationItemId",
        r => new LegacyQuotationItem(
            Int(r, "QuotationItemId"),
            Int(r, "QuotationId"),
            Str(r, "ItemName"),
            Int(r, "Quantity"),
            Dec(r, "ItemPrice"),
            DecOrNull(r, "ItemTotal")),
        ct);

    public Task<List<LegacyNewInvoice>> NewInvoicesAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.NewInvoices ORDER BY NewInvoiceId",
        r => new LegacyNewInvoice(
            Int(r, "NewInvoiceId"),
            Int(r, "JobId"),
            Str(r, "IssueName"),
            StrOrNull(r, "PaymentTerm"),
            StrOrNull(r, "ModeOfPayment"),
            StrOrNull(r, "Notes"),
            DecOrNull(r, "LabourPrice"),
            DecOrNull(r, "Discount"),
            DecOrNull(r, "ShippingFee"),
            DecOrNull(r, "SubTotal"),
            DecOrNull(r, "TaxAmount"),
            DecOrNull(r, "TotalAmount"),
            DecOrNull(r, "AmountPaid"),
            DecOrNull(r, "CashAmount"),
            DecOrNull(r, "CardAmount"),
            Bool(r, "IsPaid"),
            Str(r, "Status"),
            DtOrNull(r, "DueDate"),
            DtOrNull(r, "DatePaid"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyNewInvoiceItem>> NewInvoiceItemsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.NewInvoiceItems ORDER BY NewInvoiceItemId",
        r => new LegacyNewInvoiceItem(
            Int(r, "NewInvoiceItemId"),
            Int(r, "NewInvoiceId"),
            Str(r, "ItemName"),
            Int(r, "Quantity"),
            Dec(r, "ItemPrice"),
            Dec(r, "ItemTotal")),
        ct);

    public Task<List<LegacyNewQuotation>> NewQuotationsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.NewQuotations ORDER BY NewQuotationId",
        r => new LegacyNewQuotation(
            Int(r, "NewQuotationId"),
            Int(r, "JobId"),
            Str(r, "IssueName"),
            StrOrNull(r, "PaymentTerm"),
            StrOrNull(r, "ModeOfPayment"),
            StrOrNull(r, "Notes"),
            DecOrNull(r, "LabourPrice"),
            DecOrNull(r, "Discount"),
            DecOrNull(r, "ShippingFee"),
            DecOrNull(r, "SubTotal"),
            DecOrNull(r, "TaxAmount"),
            DecOrNull(r, "TotalAmount"),
            Bool(r, "IsAccepted"),
            Str(r, "Status"),
            DtOrNull(r, "ValidUntil"),
            Dt(r, "DateAdded"),
            DtOrNull(r, "DateEdited")),
        ct);

    public Task<List<LegacyNewQuotationItem>> NewQuotationItemsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.NewQuotationItems ORDER BY NewQuotationItemId",
        r => new LegacyNewQuotationItem(
            Int(r, "NewQuotationItemId"),
            Int(r, "NewQuotationId"),
            Str(r, "ItemName"),
            Int(r, "Quantity"),
            Dec(r, "ItemPrice"),
            Dec(r, "ItemTotal")),
        ct);

    public Task<List<LegacyAppointment>> AppointmentsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Appointments ORDER BY AppointmentId",
        r => new LegacyAppointment(
            Int(r, "AppointmentId"),
            Str(r, "CustomerName"),
            StrOrNull(r, "CarRego"),
            Str(r, "CarDetails"),
            Str(r, "Title"),
            Dt(r, "AppointmentDate"),
            Time(r, "AppointmentTime"),
            TimeOrNull(r, "TimeEnd"),
            Str(r, "Status"),
            StrOrNull(r, "Type"),
            StrOrNull(r, "Contact"),
            StrOrNull(r, "Notes"),
            IntOrNull(r, "CarId"),
            StrOrNull(r, "GoogleCalendarEventId"),
            Dt(r, "DateCreated"),
            DtOrNull(r, "DateEdited"),
            DtOrNull(r, "DateCancelled")),
        ct);

    public Task<List<LegacyMechanic>> MechanicsAsync(CancellationToken ct = default) => QueryAsync(
        "SELECT * FROM dbo.Mechanics ORDER BY MechanicId",
        r => new LegacyMechanic(
            Int(r, "MechanicId"),
            Str(r, "MechanicName"),
            Str(r, "Title"),
            StrOrNull(r, "MobileNumber"),
            StrOrNull(r, "Email")),
        ct);

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        var connection = await ConnectAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private async Task<int> QueryScalarAsync(string sql, CancellationToken ct)
    {
        var connection = await ConnectAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync(ct) ?? 0);
    }

    // Null-safe column accessors (name-based; row counts are tiny so GetOrdinal per call is fine).
    private static string Str(SqlDataReader r, string col) => r.GetString(r.GetOrdinal(col));

    private static string? StrOrNull(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    private static int Int(SqlDataReader r, string col) => r.GetInt32(r.GetOrdinal(col));

    private static int? IntOrNull(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    private static decimal Dec(SqlDataReader r, string col) => r.GetDecimal(r.GetOrdinal(col));

    private static decimal? DecOrNull(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetDecimal(i);
    }

    private static bool Bool(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return !r.IsDBNull(i) && r.GetBoolean(i);
    }

    private static DateTime Dt(SqlDataReader r, string col) => r.GetDateTime(r.GetOrdinal(col));

    private static DateTime? DtOrNull(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetDateTime(i);
    }

    private static DateOnly? DateOrNull(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : DateOnly.FromDateTime(r.GetDateTime(i));
    }

    private static TimeSpan Time(SqlDataReader r, string col) => r.GetTimeSpan(r.GetOrdinal(col));

    private static TimeSpan? TimeOrNull(SqlDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetTimeSpan(i);
    }
}
