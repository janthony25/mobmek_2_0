using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Car> Cars => Set<Car>();

    public DbSet<EmployeeTitle> EmployeeTitles => Set<EmployeeTitle>();

    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();

    public DbSet<Employee> Employees => Set<Employee>();

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

        modelBuilder.Entity<Car>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Make).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Model).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Rego).IsRequired().HasMaxLength(20);
            entity.Property(c => c.Vin).HasMaxLength(17);
            entity.Property(c => c.Color).HasMaxLength(50);
            entity.Property(c => c.EngineType).HasMaxLength(50);
            entity.HasIndex(c => c.CustomerId);
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
