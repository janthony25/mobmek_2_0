using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            entity.Property(b => b.Phone).HasMaxLength(50);
            entity.Property(b => b.Email).HasMaxLength(255);
            entity.Property(b => b.Abn).HasMaxLength(50);
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
