using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class EmailComposeServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    // One job for "Jane Doe" in a Toyota Hilux, one invoice with a single $110 line.
    private static async Task<(EmailComposeService Compose, Guid JobId, Guid InvoiceId)> SeedInvoiceAsync(
        AppDbContext db, string? customerEmail = "jane@example.com")
    {
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", customerEmail, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Toyota"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Hilux"));
        var (car, _) = await new CarService(db).CreateAsync(
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Brakes", JobStatus.Open, 1000, null, null));

        await new JobItemService(db, jobs).CreateAsync(job!.Id, new CreateJobItemRequest(
            "Pads", TradePrice: 100m, RetailPrice: 100m, MarkupSolution.Dollar, Markup: 10m, ItemQuantity: 1, SellingPrice: null));

        var invoices = new InvoiceService(db, new GstSettingService(db), new BusinessDetailsService(db, CreateStorage()));
        var invoice = await invoices.GenerateAsync(job.Id, new CreateInvoiceRequest(new DateOnly(2026, 8, 1)));

        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));
        return (compose, job.Id, invoice!.Id);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_ReturnsNull_WhenInvoiceMissing()
    {
        await using var db = CreateContext();
        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));

        var draft = await compose.ComposeInvoiceEmailAsync(Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.Null(draft);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_DefaultsRecipient_FromCustomerOnFile()
    {
        await using var db = CreateContext();
        var (compose, jobId, invoiceId) = await SeedInvoiceAsync(db);

        var draft = await compose.ComposeInvoiceEmailAsync(jobId, invoiceId, null);

        Assert.NotNull(draft);
        Assert.Equal("jane@example.com", draft!.DefaultToAddress);
        Assert.Equal("Jane Doe", draft.DefaultToName);
        Assert.NotNull(draft.CustomerId);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_NullRecipient_WhenCustomerHasNoEmailOnFile()
    {
        await using var db = CreateContext();
        var (compose, jobId, invoiceId) = await SeedInvoiceAsync(db, customerEmail: null);

        var draft = await compose.ComposeInvoiceEmailAsync(jobId, invoiceId, null);

        Assert.NotNull(draft);
        Assert.Null(draft!.DefaultToAddress);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_BodyContains_LetterheadTotalAndBankDetails()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var businessDetails = new BusinessDetailsService(db, storage);
        await businessDetails.UpdateAsync(new UpdateBusinessDetailsRequest(
            "Jun Garage", "1 Main St", "shop@jungarage.co.nz", "0400 000 000", null,
            "12 345 678 901", null, "Account: Jun Garage\nBank: ANZ\n12-3456-7890123-00",
            "INV", "QUO"));

        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", "jane@example.com", null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Toyota"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Hilux"));
        var (car, _) = await new CarService(db).CreateAsync(
            new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, "ABC123", null, null, null));
        var jobs = new JobService(db);
        var (job, _) = await jobs.CreateAsync(new CreateJobRequest(customer.Id, car!.Id, "Brakes", JobStatus.Open, 1000, null, null));
        await new JobItemService(db, jobs).CreateAsync(job!.Id, new CreateJobItemRequest(
            "Brake Pads", TradePrice: 100m, RetailPrice: 100m, MarkupSolution.Dollar, Markup: 10m, ItemQuantity: 1, SellingPrice: null));
        var invoices = new InvoiceService(db, new GstSettingService(db), new BusinessDetailsService(db, CreateStorage()));
        var invoice = await invoices.GenerateAsync(job.Id, new CreateInvoiceRequest(null));

        var compose = new EmailComposeService(db, businessDetails, new EmailTemplateService(db));
        var draft = await compose.ComposeInvoiceEmailAsync(job.Id, invoice!.Id, "Thanks for your business!");

        Assert.NotNull(draft);
        var html = draft!.BodyHtml;
        Assert.Contains("Jun Garage", html);
        Assert.Contains("12 345 678 901", html);
        // Line items now live only in the PDF attachment (InvoicePdfService) — the body is a
        // short cover note pointing at it, not a duplicate of the detail.
        Assert.Contains("attached as a PDF", html);
        Assert.DoesNotContain("Brake Pads", html);
        Assert.Contains("Thanks for your business!", html);
        Assert.Contains("ANZ", html);
        Assert.Contains(invoice.TotalAmount.ToString("N2"), html);
    }

    [Fact]
    public async Task ComposeInvoiceEmailAsync_Subject_IncludesDocumentNumberAndBusinessName()
    {
        await using var db = CreateContext();
        var (compose, jobId, invoiceId) = await SeedInvoiceAsync(db);

        var draft = await compose.ComposeInvoiceEmailAsync(jobId, invoiceId, null);

        Assert.Contains("INV-", draft!.Subject);
        Assert.Contains("Mobmek Workshop", draft.Subject);
    }

    [Fact]
    public async Task ComposeReminderEmailAsync_ReturnsNull_WhenReminderMissing()
    {
        await using var db = CreateContext();
        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));

        var draft = await compose.ComposeReminderEmailAsync(Guid.NewGuid(), null);

        Assert.Null(draft);
    }

    [Fact]
    public async Task ComposeReminderEmailAsync_DefaultsRecipient_AndRendersTemplateIntro()
    {
        await using var db = CreateContext();
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", "jane@example.com", null, null));
        var reminder = new Reminder { CustomerId = customer.Id, Title = "WOF due", DueDate = new DateOnly(2026, 11, 1) };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));
        var draft = await compose.ComposeReminderEmailAsync(reminder.Id, null);

        Assert.NotNull(draft);
        Assert.Equal("jane@example.com", draft!.DefaultToAddress);
        Assert.Equal("Jane Doe", draft.DefaultToName);
        Assert.Contains("WOF due", draft.Subject);
        Assert.Contains("Jane Doe", draft.BodyHtml);
        Assert.Contains("WOF due", draft.BodyHtml);
    }

    [Fact]
    public async Task ComposeReminderEmailAsync_CustomIntro_OverridesTemplateDefault()
    {
        await using var db = CreateContext();
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", "jane@example.com", null, null));
        var reminder = new Reminder { CustomerId = customer.Id, Title = "WOF due", DueDate = new DateOnly(2026, 11, 1) };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));
        var draft = await compose.ComposeReminderEmailAsync(reminder.Id, "Please call us to book.");

        Assert.Contains("Please call us to book.", draft!.BodyHtml);
    }

    [Fact]
    public async Task ComposeAppointmentEmailAsync_ReturnsNull_WhenAppointmentMissing()
    {
        await using var db = CreateContext();
        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));

        var draft = await compose.ComposeAppointmentEmailAsync(Guid.NewGuid(), null);

        Assert.Null(draft);
    }

    [Fact]
    public async Task ComposeAppointmentEmailAsync_DefaultsRecipient_FromLinkedCustomer()
    {
        await using var db = CreateContext();
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Jane", "Doe", "0", "jane@example.com", null, null));
        var startUtc = new DateTime(2026, 11, 1, 22, 0, 0, DateTimeKind.Utc);
        var appointment = new Appointment
        {
            Title = "Brake check",
            StartUtc = startUtc,
            EndUtc = startUtc.AddHours(1),
            CustomerId = customer.Id,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));
        var draft = await compose.ComposeAppointmentEmailAsync(appointment.Id, null);

        Assert.NotNull(draft);
        Assert.Equal("jane@example.com", draft!.DefaultToAddress);
        Assert.Equal("Jane Doe", draft.DefaultToName);
        Assert.Contains("Brake check", draft.BodyHtml);
        Assert.Contains(NzTime.FromUtc(startUtc).ToString("h:mm tt"), draft.BodyHtml);
    }

    [Fact]
    public async Task ComposeAppointmentEmailAsync_FallsBackToContactEmail_WhenNotYetConverted()
    {
        await using var db = CreateContext();
        var appointment = new Appointment
        {
            Title = "Brake check",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1),
            ContactName = "Walk-in Caller",
            ContactPhone = "021000000",
            ContactEmail = "caller@example.com",
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var compose = new EmailComposeService(db, new BusinessDetailsService(db, CreateStorage()), new EmailTemplateService(db));
        var draft = await compose.ComposeAppointmentEmailAsync(appointment.Id, null);

        Assert.NotNull(draft);
        Assert.Equal("caller@example.com", draft!.DefaultToAddress);
        Assert.Equal("Walk-in Caller", draft.DefaultToName);
        Assert.Null(draft.CustomerId);
    }
}
