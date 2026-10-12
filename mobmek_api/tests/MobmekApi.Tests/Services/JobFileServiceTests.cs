using System.Text;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class JobFileServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    private static async Task<Guid> SeedJobAsync(AppDbContext db, IFileStorage storage, string rego = "R")
    {
        var customer = await new CustomerService(db, new CarService(db)).CreateAsync(new CreateCustomerRequest("O", "P", "0", null, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Make"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Model"));
        var (car, _) = await new CarService(db).CreateAsync(new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, rego, null, null, null));
        var (job, _) = await new JobService(db, storage).CreateAsync(
            new CreateJobRequest(customer.Id, car!.Id, "Job", JobStatus.Open, 1000, null, null));
        return job!.Id;
    }

    private static MemoryStream Pdf(string content = "file bytes") => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task AddGetDelete_RoundTrip()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var service = new JobFileService(db, storage);

        var bytes = Encoding.UTF8.GetBytes("file bytes");
        var file = await service.AddAsync(jobId, new MemoryStream(bytes), "quote.pdf", "application/pdf", bytes.Length);

        Assert.NotNull(file);
        Assert.Equal("quote.pdf", file!.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(bytes.Length, file.SizeBytes);

        var download = await service.GetContentAsync(jobId, file.Id);
        Assert.NotNull(download);
        using var reader = new StreamReader(download!.Value.Content);
        Assert.Equal("file bytes", await reader.ReadToEndAsync());

        Assert.True(await service.DeleteAsync(jobId, file.Id));
        Assert.Null(await service.GetContentAsync(jobId, file.Id));
    }

    [Fact]
    public async Task AddAsync_ReturnsNull_WhenJobMissing()
    {
        await using var db = CreateContext();
        var service = new JobFileService(db, CreateStorage());

        Assert.Null(await service.AddAsync(Guid.NewGuid(), Pdf(), "x.pdf", "application/pdf", 1));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyThatJobsFiles_OldestFirst()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var otherJobId = await SeedJobAsync(db, storage, "R2");
        var service = new JobFileService(db, storage);

        var first = await service.AddAsync(jobId, Pdf(), "first.pdf", "application/pdf", 11);
        var second = await service.AddAsync(jobId, Pdf(), "second.pdf", "application/pdf", 11);
        await service.AddAsync(otherJobId, Pdf(), "other.pdf", "application/pdf", 11);

        var files = await service.GetAllAsync(jobId);

        Assert.Equal([first!.Id, second!.Id], files.Select(f => f.Id));
    }

    [Fact]
    public async Task GetContentAsync_ReturnsNull_WhenFileBelongsToAnotherJob()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var otherJobId = await SeedJobAsync(db, storage, "R2");
        var service = new JobFileService(db, storage);

        var file = await service.AddAsync(jobId, Pdf(), "quote.pdf", "application/pdf", 11);

        Assert.Null(await service.GetContentAsync(otherJobId, file!.Id));
        Assert.False(await service.DeleteAsync(otherJobId, file.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredFile()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var service = new JobFileService(db, storage);
        var file = await service.AddAsync(jobId, Pdf(), "quote.pdf", "application/pdf", 11);
        var storageKey = (await db.JobFiles.SingleAsync(f => f.Id == file!.Id)).StorageKey;

        Assert.True(await service.DeleteAsync(jobId, file!.Id));

        Assert.Null(await storage.OpenReadAsync(storageKey));
    }

    [Fact]
    public async Task DeletingTheJob_RemovesStoredJobFiles()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var file = await new JobFileService(db, storage).AddAsync(jobId, Pdf(), "quote.pdf", "application/pdf", 11);
        var storageKey = (await db.JobFiles.SingleAsync(f => f.Id == file!.Id)).StorageKey;

        Assert.True(await new JobService(db, storage).DeleteAsync(jobId));

        Assert.Null(await storage.OpenReadAsync(storageKey));
    }
}
