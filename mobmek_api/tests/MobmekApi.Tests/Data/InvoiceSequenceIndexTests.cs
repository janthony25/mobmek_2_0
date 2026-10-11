using Microsoft.EntityFrameworkCore;
using MobmekApi.Data;
using MobmekApi.Entities;

namespace MobmekApi.Tests.Data;

/// <summary>
/// Regression test for the partial unique index on (DocumentType, SequenceNumber)
/// (migration MakeInvoiceSequenceNumberIndexPartial) — pins both halves of its contract:
/// the legacy importer's SequenceNumber=0 placeholders must be able to coexist, and two
/// *real* (&gt;0) numbers of the same DocumentType must still collide. EF Core's InMemory
/// provider ignores HasFilter entirely, so this can only be verified against a real
/// Postgres — unlike the rest of the suite, this needs the dev stack running
/// (`docker compose up -d db`). Everything is done inside a transaction that's always
/// rolled back, so nothing is left behind in the dev database.
/// </summary>
public class InvoiceSequenceIndexTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=mobmek;Username=postgres;Password=postgres";

    private static AppDbContext OpenConnection() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Minimal Customer → Car → Job chain an Invoice can attach to.</summary>
    private static async Task<Guid> SeedJobAsync(AppDbContext db)
    {
        var suffix = Guid.NewGuid();
        var make = new CarMake { Name = $"TestMake-{suffix}" };
        var model = new CarModel { Name = $"TestModel-{suffix}", CarMake = make };
        var customer = new Customer { FirstName = "Test", LastName = "Customer", PhoneNumber = "0" };
        var car = new Car { Customer = customer, CarMake = make, CarModel = model, Year = 2020, Rego = suffix.ToString("N")[..10] };
        var job = new Job { Customer = customer, Car = car, Title = "Test job" };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    [Fact]
    public async Task PlaceholderZeros_SameDocumentType_DoNotCollide()
    {
        await using var db = OpenConnection();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var jobId = await SeedJobAsync(db);
            db.Invoices.Add(new Invoice { JobId = jobId, IssueName = "a", DocumentType = "Invoice", SequenceNumber = 0 });
            db.Invoices.Add(new Invoice { JobId = jobId, IssueName = "b", DocumentType = "Invoice", SequenceNumber = 0 });

            // Importer's placeholder state — must not throw.
            await db.SaveChangesAsync();
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task RealSequenceNumbers_SameDocumentType_StillCollide()
    {
        const int testNumber = 987_654_321; // far outside any real sequence range

        await using var db = OpenConnection();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var jobId = await SeedJobAsync(db);
            db.Invoices.Add(new Invoice { JobId = jobId, IssueName = "a", DocumentType = "Invoice", SequenceNumber = testNumber });
            await db.SaveChangesAsync();

            db.Invoices.Add(new Invoice { JobId = jobId, IssueName = "b", DocumentType = "Invoice", SequenceNumber = testNumber });

            // The index's real job: two live invoices must never share a number.
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }
}
