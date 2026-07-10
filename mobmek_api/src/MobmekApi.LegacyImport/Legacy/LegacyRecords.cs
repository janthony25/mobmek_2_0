namespace MobmekApi.LegacyImport.Legacy;

// One record per legacy MSSQL table (docs/legacy-import-design.md §5), fields matching the
// restored database's columns (verified 2026-07-10). All DateTimes are Auckland wall-clock
// (convert with NzTime). NewInvoices also has AfterpayAmount/AfterpayReceivedPayment columns,
// unused in the real data (0 rows) and deliberately not read.

public sealed record LegacyCustomer(
    int CustomerId,
    string CustomerName,
    string? CustomerAddress,
    string? CustomerEmail,
    string? CustomerNumber,
    DateTime DateAdded,
    DateTime? DateEdited);

public sealed record LegacyCar(
    int CarId,
    string CarRego,
    string? CarModel,
    int? CarYear,
    int CustomerId,
    DateTime DateAdded,
    DateTime? DateEdited);

public sealed record LegacyMake(int MakeId, string MakeName);

public sealed record LegacyCarMake(int CarId, int MakeId);

public sealed record LegacyService(
    int ServiceId,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    DateTime DateCreated,
    DateTime? DateUpdated);

/// <summary>Join row attaching a catalog Service to a Job (old many-to-many).</summary>
public sealed record LegacyJobServiceJoin(
    int JobServiceId,
    int JobId,
    int ServiceId,
    decimal AdditionalAmount,
    DateTime DateAdded);

public sealed record LegacyJob(
    int JobId,
    string Title,
    string Issue,
    string? Notes,
    string? Status,
    int? Odometer,
    decimal? TotalUnitProfit,
    decimal? TotalJobProfit,
    decimal? TotalJobPrice,
    DateOnly? DateStarted,
    DateOnly? DateFinished,
    int CarId,
    int? MechanicId,
    int? AppointmentId,
    DateTime DateAdded,
    DateTime? DateEdited);

public sealed record LegacyJobItem(
    int JobItemId,
    int JobId,
    string ItemName,
    decimal TradePrice,
    decimal RetailPrice,
    string MarkupSolution,
    decimal Markup,
    decimal SellingPrice,
    decimal UnitProfit,
    int ItemQuantity,
    decimal ItemTotal,
    DateTime DateAdded,
    DateTime? DateEdited);

public sealed record LegacyLabour(
    int LabourId,
    int JobId,
    string LabourName,
    decimal LabourHours,
    decimal LabourPrice,
    decimal TotalLabour,
    DateTime DateAdded,
    DateTime? DateEdited);

/// <summary>First-generation invoice: linked directly to a Car (no Job).</summary>
public sealed record LegacyInvoice(
    int InvoiceId,
    int CarId,
    string IssueName,
    string? PaymentTerm,
    string? Notes,
    decimal? LaborPrice,
    decimal? Discount,
    decimal? ShippingFee,
    decimal? SubTotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    decimal? AmountPaid,
    bool IsPaid,
    DateTime? DueDate,
    DateTime? DateAdded,
    DateTime? DateEdited);

public sealed record LegacyInvoiceItem(
    int InvoiceItemId,
    int InvoiceId,
    string ItemName,
    int Quantity,
    decimal ItemPrice,
    decimal? ItemTotal);

/// <summary>First-generation quotation: linked directly to a Car (no Job).</summary>
public sealed record LegacyQuotation(
    int QuotationId,
    int CarId,
    string IssueName,
    string? Notes,
    decimal? LaborPrice,
    decimal? Discount,
    decimal? ShippingFee,
    decimal? SubTotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    DateTime DateAdded,
    DateTime? DateEdited);

public sealed record LegacyQuotationItem(
    int QuotationItemId,
    int QuotationId,
    string ItemName,
    int Quantity,
    decimal ItemPrice,
    decimal? ItemTotal);

/// <summary>Second-generation invoice: linked to a Job.</summary>
public sealed record LegacyNewInvoice(
    int NewInvoiceId,
    int JobId,
    string IssueName,
    string? PaymentTerm,
    string? ModeOfPayment,
    string? Notes,
    decimal? LabourPrice,
    decimal? Discount,
    decimal? ShippingFee,
    decimal? SubTotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    decimal? AmountPaid,
    decimal? CashAmount,
    decimal? CardAmount,
    bool IsPaid,
    string Status,
    DateTime? DueDate,
    DateTime? DatePaid,
    DateTime DateAdded,
    DateTime? DateEdited);

public sealed record LegacyNewInvoiceItem(
    int NewInvoiceItemId,
    int NewInvoiceId,
    string ItemName,
    int Quantity,
    decimal ItemPrice,
    decimal ItemTotal);

/// <summary>Second-generation quotation: linked to a Job.</summary>
public sealed record LegacyNewQuotation(
    int NewQuotationId,
    int JobId,
    string IssueName,
    string? PaymentTerm,
    string? ModeOfPayment,
    string? Notes,
    decimal? LabourPrice,
    decimal? Discount,
    decimal? ShippingFee,
    decimal? SubTotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    bool IsAccepted,
    string Status,
    DateTime? ValidUntil,
    DateTime DateAdded,
    DateTime? DateEdited);

public sealed record LegacyNewQuotationItem(
    int NewQuotationItemId,
    int NewQuotationId,
    string ItemName,
    int Quantity,
    decimal ItemPrice,
    decimal ItemTotal);

public sealed record LegacyAppointment(
    int AppointmentId,
    string CustomerName,
    string? CarRego,
    string CarDetails,
    string Title,
    DateTime AppointmentDate,
    TimeSpan AppointmentTime,
    TimeSpan? TimeEnd,
    string Status,
    string? Type,
    string? Contact,
    string? Notes,
    int? CarId,
    string? GoogleCalendarEventId,
    DateTime DateCreated,
    DateTime? DateEdited,
    DateTime? DateCancelled);

/// <summary>Read only to fold the mechanic's name into imported job notes (§3.4).</summary>
public sealed record LegacyMechanic(
    int MechanicId,
    string MechanicName,
    string Title,
    string? MobileNumber,
    string? Email);
