using System.Text;
using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;
using JobService = MobmekApi.Services.JobService;

namespace MobmekApi.Tests.Services;

public class JobPhotoServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static IFileStorage CreateStorage() =>
        new LocalFileStorage(Path.Combine(Path.GetTempPath(), "mobmek-tests", Guid.NewGuid().ToString("N")));

    private static async Task<Guid> SeedJobAsync(AppDbContext db, IFileStorage storage, string rego = "R")
    {
        var customer = await new CustomerService(db).CreateAsync(new CreateCustomerRequest("O", "P", "0", null, null, null));
        var make = await new CarMakeService(db).CreateAsync(new CreateCarMakeRequest("Make"));
        var model = await new CarModelService(db).CreateAsync(new CreateCarModelRequest(make.Id, "Model"));
        var (car, _) = await new CarService(db).CreateAsync(new CreateCarRequest(customer.Id, make.Id, model!.Id, 2020, rego, null, null, null));
        var (job, _) = await new JobService(db, storage).CreateAsync(
            new CreateJobRequest(customer.Id, car!.Id, "Job", JobStatus.Open, 1000, null, null));
        return job!.Id;
    }

    private static MemoryStream Jpeg(string content = "photo bytes") => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task AddGetDelete_RoundTrip()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var service = new JobPhotoService(db, storage);

        var bytes = Encoding.UTF8.GetBytes("photo bytes");
        var photo = await service.AddAsync(jobId, new MemoryStream(bytes), "damage.jpg", "image/jpeg", bytes.Length);

        Assert.NotNull(photo);
        Assert.Equal("damage.jpg", photo!.FileName);
        Assert.Equal("image/jpeg", photo.ContentType);
        Assert.Equal(bytes.Length, photo.SizeBytes);

        var download = await service.GetContentAsync(jobId, photo.Id);
        Assert.NotNull(download);
        using var reader = new StreamReader(download!.Value.Content);
        Assert.Equal("photo bytes", await reader.ReadToEndAsync());

        Assert.True(await service.DeleteAsync(jobId, photo.Id));
        Assert.Null(await service.GetContentAsync(jobId, photo.Id));
    }

    [Fact]
    public async Task AddAsync_ReturnsNull_WhenJobMissing()
    {
        await using var db = CreateContext();
        var service = new JobPhotoService(db, CreateStorage());

        Assert.Null(await service.AddAsync(Guid.NewGuid(), Jpeg(), "x.png", "image/png", 1));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyThatJobsPhotos_OldestFirst()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var otherJobId = await SeedJobAsync(db, storage, "R2");
        var service = new JobPhotoService(db, storage);

        var first = await service.AddAsync(jobId, Jpeg(), "first.jpg", "image/jpeg", 11);
        var second = await service.AddAsync(jobId, Jpeg(), "second.jpg", "image/jpeg", 11);
        await service.AddAsync(otherJobId, Jpeg(), "other.jpg", "image/jpeg", 11);

        var photos = await service.GetAllAsync(jobId);

        Assert.Equal([first!.Id, second!.Id], photos.Select(p => p.Id));
    }

    [Fact]
    public async Task GetContentAsync_ReturnsNull_WhenPhotoBelongsToAnotherJob()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var otherJobId = await SeedJobAsync(db, storage, "R2");
        var service = new JobPhotoService(db, storage);

        var photo = await service.AddAsync(jobId, Jpeg(), "damage.jpg", "image/jpeg", 11);

        Assert.Null(await service.GetContentAsync(otherJobId, photo!.Id));
        Assert.False(await service.DeleteAsync(otherJobId, photo.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredFile()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var service = new JobPhotoService(db, storage);
        var photo = await service.AddAsync(jobId, Jpeg(), "damage.jpg", "image/jpeg", 11);
        var storageKey = (await db.JobPhotos.SingleAsync(p => p.Id == photo!.Id)).StorageKey;

        Assert.True(await service.DeleteAsync(jobId, photo!.Id));

        Assert.Null(await storage.OpenReadAsync(storageKey));
    }

    [Fact]
    public async Task DeletingTheJob_RemovesStoredPhotoFiles()
    {
        await using var db = CreateContext();
        var storage = CreateStorage();
        var jobId = await SeedJobAsync(db, storage);
        var photo = await new JobPhotoService(db, storage).AddAsync(jobId, Jpeg(), "damage.jpg", "image/jpeg", 11);
        var storageKey = (await db.JobPhotos.SingleAsync(p => p.Id == photo!.Id)).StorageKey;

        Assert.True(await new JobService(db, storage).DeleteAsync(jobId));

        Assert.Null(await storage.OpenReadAsync(storageKey));
    }
}
