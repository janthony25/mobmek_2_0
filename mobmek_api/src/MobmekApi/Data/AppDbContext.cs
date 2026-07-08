using MobmekApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CarMake> CarMakes => Set<CarMake>();

    public DbSet<CarModel> CarModels => Set<CarModel>();

    public DbSet<Car> Cars => Set<Car>();

    public DbSet<EmployeeTitle> EmployeeTitles => Set<EmployeeTitle>();

    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<JobService> JobServices => Set<JobService>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobMechanic> JobMechanics => Set<JobMechanic>();

    public DbSet<JobItem> JobItems => Set<JobItem>();

    public DbSet<Labour> Labour => Set<Labour>();

    public DbSet<JobServiceLine> JobServiceLines => Set<JobServiceLine>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    public DbSet<GstSetting> GstSettings => Set<GstSetting>();

    public DbSet<BusinessDetails> BusinessDetails => Set<BusinessDetails>();

    public DbSet<ReminderTemplate> ReminderTemplates => Set<ReminderTemplate>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<CashAccount> CashAccounts => Set<CashAccount>();

    public DbSet<TransactionCategory> TransactionCategories => Set<TransactionCategory>();

    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();

    public DbSet<TransactionAttachment> TransactionAttachments => Set<TransactionAttachment>();

    public DbSet<CashFlowSettings> CashFlowSettings => Set<CashFlowSettings>();

    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();

    public DbSet<PlannedTransaction> PlannedTransactions => Set<PlannedTransaction>();

    public DbSet<Payee> Payees => Set<Payee>();

    public DbSet<CategorizationRule> CategorizationRules => Set<CategorizationRule>();

    public DbSet<CashFlowAuditLog> CashFlowAuditLogs => Set<CashFlowAuditLog>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();

    public DbSet<OutboundEmail> OutboundEmails => Set<OutboundEmail>();

    public DbSet<PasswordChangeCode> PasswordChangeCodes => Set<PasswordChangeCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            // One login per employee; block deleting an employee that still has an account.
            entity.HasIndex(u => u.EmployeeId).IsUnique();

            entity.HasOne(u => u.Employee)
                .WithMany()
                .HasForeignKey(u => u.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.Property(a => a.Email).IsRequired().HasMaxLength(200);
            entity.Property(a => a.FailureReason).HasMaxLength(100);
            entity.Property(a => a.IpAddress).HasMaxLength(45); // fits an IPv6 address
            entity.HasIndex(a => a.CreatedAtUtc);

            // An employee's login history should survive the employee record being deleted
            // (HR cleanup shouldn't quietly erase the audit trail).
            entity.HasOne(a => a.Employee)
                .WithMany()
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Price).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.LastName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(30);
            entity.Property(c => c.EmailAddress).HasMaxLength(200);
            entity.Property(c => c.PhysicalAddress).HasMaxLength(500);
            entity.Property(c => c.Notes).HasMaxLength(2000);

            entity.HasMany(c => c.Cars)
                .WithOne(car => car.Customer!)
                .HasForeignKey(car => car.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CarMake>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(m => m.Name).IsUnique();
        });

        modelBuilder.Entity<CarModel>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(100);

            entity.HasOne(m => m.CarMake)
                .WithMany(make => make.Models)
                .HasForeignKey(m => m.CarMakeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Model names are unique within a make.
            entity.HasIndex(m => new { m.CarMakeId, m.Name }).IsUnique();
        });

        modelBuilder.Entity<Car>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Rego).IsRequired().HasMaxLength(20);
            entity.Property(c => c.Vin).HasMaxLength(17);
            entity.Property(c => c.Color).HasMaxLength(50);
            entity.Property(c => c.EngineType).HasMaxLength(50);
            entity.HasIndex(c => c.CustomerId);

            entity.HasOne(c => c.CarMake)
                .WithMany()
                .HasForeignKey(c => c.CarMakeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.CarModel)
                .WithMany()
                .HasForeignKey(c => c.CarModelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeeTitle>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<EmploymentType>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContactNumber).IsRequired().HasMaxLength(30);
            entity.Property(e => e.EmailAddress).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PhysicalAddress).IsRequired().HasMaxLength(500);

            // Block deleting a title/type that is still in use rather than cascading to employees.
            entity.HasOne(e => e.Title)
                .WithMany(t => t.Employees)
                .HasForeignKey(e => e.TitleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EmploymentType)
                .WithMany(t => t.Employees)
                .HasForeignKey(e => e.EmploymentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobService>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Description).HasMaxLength(2000);
            entity.Property(s => s.Price).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Title).IsRequired().HasMaxLength(200);
            entity.Property(j => j.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(j => j.JobNotes).HasMaxLength(4000);
            entity.Property(j => j.InvoiceNotes).HasMaxLength(4000);
            entity.Property(j => j.DiscountType).HasConversion<string>().HasMaxLength(20);
            entity.Property(j => j.DiscountValue).HasColumnType("numeric(18,2)");
            entity.Property(j => j.TotalJobPrice).HasColumnType("numeric(18,2)");
            entity.Property(j => j.TotalJobProfit).HasColumnType("numeric(18,2)");

            // Job references a customer and one of that customer's cars. Restrict so a
            // customer/car can't be deleted out from under an existing job.
            entity.HasOne(j => j.Customer)
                .WithMany()
                .HasForeignKey(j => j.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(j => j.Car)
                .WithMany()
                .HasForeignKey(j => j.CarId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(j => j.CustomerId);
            entity.HasIndex(j => j.CarId);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Title).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(a => a.Notes).HasMaxLength(4000);
            entity.Property(a => a.ContactName).HasMaxLength(200);
            entity.Property(a => a.ContactPhone).HasMaxLength(30);
            entity.Property(a => a.VehicleDescription).HasMaxLength(500);
            entity.Property(a => a.GoogleEventId).HasMaxLength(200);

            // All links are optional; SetNull so deleting a customer/car/job/employee
            // keeps the appointment history (its soft contact snapshot still tells the story).
            entity.HasOne(a => a.Customer)
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Car)
                .WithMany()
                .HasForeignKey(a => a.CarId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Job)
                .WithMany()
                .HasForeignKey(a => a.JobId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Mechanic)
                .WithMany()
                .HasForeignKey(a => a.MechanicId)
                .OnDelete(DeleteBehavior.SetNull);

            // Calendar queries are always by time range.
            entity.HasIndex(a => a.StartUtc);
            entity.HasIndex(a => a.CustomerId);
            entity.HasIndex(a => a.JobId);
        });

        modelBuilder.Entity<JobMechanic>(entity =>
        {
            entity.HasKey(m => new { m.JobId, m.EmployeeId });

            entity.HasOne(m => m.Job)
                .WithMany(j => j.Mechanics)
                .HasForeignKey(m => m.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Employee)
                .WithMany()
                .HasForeignKey(m => m.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ItemName).IsRequired().HasMaxLength(200);
            entity.Property(i => i.MarkupSolution).HasConversion<string>().HasMaxLength(20);
            entity.Property(i => i.TradePrice).HasColumnType("numeric(18,2)");
            entity.Property(i => i.RetailPrice).HasColumnType("numeric(18,2)");
            entity.Property(i => i.Markup).HasColumnType("numeric(18,2)");
            entity.Property(i => i.SellingPrice).HasColumnType("numeric(18,2)");
            entity.Property(i => i.UnitProfit).HasColumnType("numeric(18,2)");
            entity.Property(i => i.ItemTotal).HasColumnType("numeric(18,2)");

            entity.HasOne(i => i.Job)
                .WithMany(j => j.Items)
                .HasForeignKey(i => i.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Labour>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Hours).HasColumnType("numeric(18,2)");
            entity.Property(l => l.RatePerHour).HasColumnType("numeric(18,2)");
            entity.Property(l => l.FixedAmount).HasColumnType("numeric(18,2)");
            entity.Property(l => l.TotalAmount).HasColumnType("numeric(18,2)");

            entity.HasOne(l => l.Job)
                .WithMany(j => j.Labour)
                .HasForeignKey(l => l.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobServiceLine>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.UnitPrice).HasColumnType("numeric(18,2)");
            entity.Property(s => s.LineTotal).HasColumnType("numeric(18,2)");

            entity.HasOne(s => s.Job)
                .WithMany(j => j.ServiceLines)
                .HasForeignKey(s => s.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.JobService)
                .WithMany()
                .HasForeignKey(s => s.JobServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.IssueName).IsRequired().HasMaxLength(255);
            entity.Property(i => i.Notes).HasMaxLength(4000);
            entity.Property(i => i.DocumentType).IsRequired().HasMaxLength(50);
            entity.Property(i => i.Status).IsRequired().HasMaxLength(50);
            entity.Property(i => i.PaymentTerm).HasMaxLength(100);
            entity.Property(i => i.ModeOfPayment).HasMaxLength(100);
            entity.Property(i => i.LabourPrice).HasColumnType("numeric(18,2)");
            entity.Property(i => i.SubTotal).HasColumnType("numeric(18,2)");
            entity.Property(i => i.GstRate).HasColumnType("numeric(5,4)");
            entity.Property(i => i.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(i => i.Discount).HasColumnType("numeric(18,2)");
            entity.Property(i => i.ShippingFee).HasColumnType("numeric(18,2)");
            entity.Property(i => i.TotalAmount).HasColumnType("numeric(18,2)");
            entity.Property(i => i.AmountPaid).HasColumnType("numeric(18,2)");
            entity.Property(i => i.CashAmount).HasColumnType("numeric(18,2)");
            entity.Property(i => i.CardAmount).HasColumnType("numeric(18,2)");

            // An invoice belongs to a job; deleting the job removes its invoices.
            entity.HasOne(i => i.Job)
                .WithMany()
                .HasForeignKey(i => i.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => i.JobId);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(ii => ii.Id);
            entity.Property(ii => ii.ItemName).IsRequired().HasMaxLength(255);
            entity.Property(ii => ii.ItemPrice).HasColumnType("numeric(18,2)");
            entity.Property(ii => ii.ItemTotal).HasColumnType("numeric(18,2)");

            entity.HasOne(ii => ii.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GstSetting>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Rate).HasColumnType("numeric(5,4)");
        });

        modelBuilder.Entity<BusinessDetails>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Address).HasMaxLength(500);
            entity.Property(b => b.Email).HasMaxLength(255);
            entity.Property(b => b.BusinessPhone).HasMaxLength(50);
            entity.Property(b => b.Telephone).HasMaxLength(50);
            entity.Property(b => b.GstNumber).HasMaxLength(50);
            entity.Property(b => b.Website).HasMaxLength(255);
            entity.Property(b => b.BankDetails).HasMaxLength(1000);
            entity.Property(b => b.LogoStorageKey).HasMaxLength(300);
            entity.Property(b => b.LogoFileName).HasMaxLength(255);
            entity.Property(b => b.LogoContentType).HasMaxLength(100);
        });

        modelBuilder.Entity<ReminderTemplate>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Description).HasMaxLength(500);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
            entity.Property(n => n.Body).HasMaxLength(4000);
            entity.Property(n => n.Color).HasMaxLength(50);

            // Optional link; a note survives (unlinked) if its customer is deleted.
            entity.HasOne(n => n.Customer)
                .WithMany()
                .HasForeignKey(n => n.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(n => n.CustomerId);
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Title).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Notes).HasMaxLength(2000);

            // A reminder belongs to a customer; deleting the customer removes their reminders.
            entity.HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Car link is optional; deleting the car leaves the reminder as customer-level.
            entity.HasOne(r => r.Car)
                .WithMany()
                .HasForeignKey(r => r.CarId)
                .OnDelete(DeleteBehavior.SetNull);

            // Title is copied from the template, so a template can be deleted without
            // losing the reminder — the link just clears.
            entity.HasOne(r => r.ReminderTemplate)
                .WithMany()
                .HasForeignKey(r => r.ReminderTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(r => r.CustomerId);
            entity.HasIndex(r => r.CarId);
            entity.HasIndex(r => r.DueDate);
        });
        modelBuilder.Entity<CashAccount>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Type).IsRequired().HasMaxLength(30);
            entity.Property(a => a.AccountNumber).HasMaxLength(50);
            entity.Property(a => a.OpeningBalance).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<TransactionCategory>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Direction).IsRequired().HasMaxLength(10);
            entity.Property(c => c.Group).IsRequired().HasMaxLength(50);
            entity.Property(c => c.DefaultGstTreatment).IsRequired().HasMaxLength(20);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<CashTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Direction).IsRequired().HasMaxLength(10);
            entity.Property(t => t.Amount).HasColumnType("numeric(18,2)");
            entity.Property(t => t.Description).IsRequired().HasMaxLength(500);
            entity.Property(t => t.Counterparty).HasMaxLength(200);
            entity.Property(t => t.GstTreatment).IsRequired().HasMaxLength(20);
            entity.Property(t => t.Notes).HasMaxLength(2000);
            // DB default (not just a C# initializer) so rows that pre-date the status
            // column are backfilled to "Cleared" by the migration.
            entity.Property(t => t.Status).IsRequired().HasMaxLength(15).HasDefaultValue("Cleared");

            // The ledger is history: an account or category that still has transactions
            // cannot be deleted (archive instead).
            entity.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Category)
                .WithMany()
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // If an invoice disappears (its job was deleted), the ledger row survives
            // unlinked — the money still moved.
            entity.HasOne(t => t.Invoice)
                .WithMany()
                .HasForeignKey(t => t.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull);

            // Same reasoning: deleting the schedule doesn't erase the history it produced.
            entity.HasOne(t => t.RecurringTransaction)
                .WithMany()
                .HasForeignKey(t => t.RecurringTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            // A payee is a label over history; deleting it leaves the Counterparty text behind.
            entity.HasOne(t => t.Payee)
                .WithMany()
                .HasForeignKey(t => t.PayeeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(t => t.AccountId);
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => t.Date);
            entity.HasIndex(t => t.InvoiceId);
            entity.HasIndex(t => t.TransferGroupId);
            entity.HasIndex(t => t.SplitGroupId);
            entity.HasIndex(t => t.RecurringTransactionId);
            entity.HasIndex(t => t.PayeeId);
            entity.HasIndex(t => t.Status);
        });

        modelBuilder.Entity<Payee>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.DefaultGstTreatment).HasMaxLength(20);
            entity.Property(p => p.Notes).HasMaxLength(2000);
            entity.HasIndex(p => p.Name).IsUnique();

            entity.HasOne(p => p.DefaultCategory)
                .WithMany()
                .HasForeignKey(p => p.DefaultCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CategorizationRule>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
            entity.Property(r => r.MatchField).IsRequired().HasMaxLength(20);
            entity.Property(r => r.MatchType).IsRequired().HasMaxLength(20);
            entity.Property(r => r.MatchValue).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Direction).HasMaxLength(10);
            entity.Property(r => r.AmountMin).HasColumnType("numeric(18,2)");
            entity.Property(r => r.AmountMax).HasColumnType("numeric(18,2)");
            entity.Property(r => r.SetGstTreatment).HasMaxLength(20);

            // A rule that assigns a category blocks deleting that category (archive instead).
            entity.HasOne(r => r.SetCategory)
                .WithMany()
                .HasForeignKey(r => r.SetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.SetPayee)
                .WithMany()
                .HasForeignKey(r => r.SetPayeeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CashFlowAuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(20);
            entity.Property(a => a.Summary).IsRequired().HasMaxLength(1000);
            entity.Property(a => a.Changes).HasMaxLength(8000);
            entity.HasIndex(a => new { a.EntityType, a.EntityId });
            entity.HasIndex(a => a.CreatedAtUtc);
        });

        modelBuilder.Entity<TransactionAttachment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(255);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(a => a.StorageKey).IsRequired().HasMaxLength(500);

            entity.HasOne(a => a.CashTransaction)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.CashTransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CashFlowSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SafetyBufferAmount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<RecurringTransaction>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Description).IsRequired().HasMaxLength(500);
            entity.Property(r => r.Direction).IsRequired().HasMaxLength(10);
            entity.Property(r => r.Amount).HasColumnType("numeric(18,2)");
            entity.Property(r => r.Counterparty).HasMaxLength(200);
            entity.Property(r => r.GstTreatment).IsRequired().HasMaxLength(20);
            entity.Property(r => r.Frequency).IsRequired().HasMaxLength(20);

            // History (materialised CashTransactions) outlives the account/category it
            // references, same as CashTransaction itself — archive/restrict, don't cascade.
            entity.HasOne(r => r.Account)
                .WithMany()
                .HasForeignKey(r => r.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Category)
                .WithMany()
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlannedTransaction>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(500);
            entity.Property(p => p.Direction).IsRequired().HasMaxLength(10);
            entity.Property(p => p.Amount).HasColumnType("numeric(18,2)");
            entity.Property(p => p.Status).IsRequired().HasMaxLength(20);
            entity.Property(p => p.ScenarioTag).HasMaxLength(20);

            entity.HasOne(p => p.Account)
                .WithMany()
                .HasForeignKey(p => p.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmailSettings>(entity =>
        {
            entity.Property(s => s.FromName).IsRequired().HasMaxLength(200);
            entity.Property(s => s.FromAddress).IsRequired().HasMaxLength(255);
            entity.Property(s => s.ReplyToAddress).HasMaxLength(255);
        });

        modelBuilder.Entity<OutboundEmail>(entity =>
        {
            entity.Property(e => e.ToAddress).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ToName).HasMaxLength(255);
            entity.Property(e => e.CcAddresses).HasMaxLength(1000);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ProviderMessageId).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.InvoiceId);
            entity.HasIndex(e => e.CustomerId);

            // Both links are optional; SetNull so deleting a customer keeps the send history
            // (invoices are never deleted in this app, but SetNull is still the safe default).
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PasswordChangeCode>(entity =>
        {
            entity.Property(c => c.CodeHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(c => c.UserId);
        });
    }

    /// <summary>
    /// Stamps audit timestamps automatically on save so callers never have to.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
