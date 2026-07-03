using MobmekApi.Data;
using MobmekApi.DTOs;
using MobmekApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Services;

public class NoteServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> SeedCustomerAsync(AppDbContext db)
    {
        var customer = await new CustomerService(db).CreateAsync(
            new CreateCustomerRequest("Owner", "Person", "000", null, null, null));
        return customer.Id;
    }

    private static CreateNoteRequest NewNote(
        string title, bool pinned = false, Guid? customerId = null, DateOnly? dueDate = null) =>
        new(title, null, dueDate, null, pinned, false, customerId);

    [Fact]
    public async Task CreateAsync_PersistsNote_WithoutCustomer()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);

        var (note, error) = await service.CreateAsync(NewNote("Order oil filters"));

        Assert.Equal(NoteWriteError.None, error);
        Assert.NotNull(note);
        Assert.Null(note!.CustomerId);
        Assert.Equal(1, await db.Notes.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ResolvesCustomerName_WhenLinked()
    {
        await using var db = CreateContext();
        var customerId = await SeedCustomerAsync(db);
        var service = new NoteService(db);

        var (note, error) = await service.CreateAsync(NewNote("Call back", customerId: customerId));

        Assert.Equal(NoteWriteError.None, error);
        Assert.Equal(customerId, note!.CustomerId);
        Assert.Equal("Owner Person", note.CustomerName);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCustomerNotFound_WhenCustomerMissing()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);

        var (note, error) = await service.CreateAsync(NewNote("Call back", customerId: Guid.NewGuid()));

        Assert.Null(note);
        Assert.Equal(NoteWriteError.CustomerNotFound, error);
    }

    [Fact]
    public async Task CreateAsync_PersistsDueDate()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);

        var (note, error) = await service.CreateAsync(
            NewNote("Follow up", dueDate: new DateOnly(2026, 9, 15)));

        Assert.Equal(NoteWriteError.None, error);
        Assert.Equal(new DateOnly(2026, 9, 15), note!.DueDate);
    }

    [Fact]
    public async Task GetAllAsync_OrdersPinnedFirst()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);
        await service.CreateAsync(NewNote("Plain"));
        await service.CreateAsync(NewNote("Pinned", pinned: true));

        var result = await service.GetAllAsync();

        Assert.Collection(result,
            n => Assert.Equal("Pinned", n.Title),
            n => Assert.Equal("Plain", n.Title));
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields_AndStampsUpdatedAt()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);
        var (created, _) = await service.CreateAsync(NewNote("Old"));

        var (updated, error) = await service.UpdateAsync(
            created!.Id, new UpdateNoteRequest("New", "body", new DateOnly(2026, 10, 1), "yellow", true, true, null));

        Assert.Equal(NoteWriteError.None, error);
        Assert.Equal("New", updated!.Title);
        Assert.Equal(new DateOnly(2026, 10, 1), updated.DueDate);
        Assert.True(updated.IsPinned);
        Assert.True(updated.IsDone);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    private static UpdateNoteRequest UpdateWithDone(string title, bool isDone) =>
        new(title, null, null, null, false, isDone, null);

    [Fact]
    public async Task CreateAsync_StampsDoneAt_WhenCreatedDone()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);

        var (note, _) = await service.CreateAsync(
            new CreateNoteRequest("Already handled", null, null, null, false, true, null));

        Assert.NotNull(note!.DoneAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_StampsDoneAt_WhenMarkedDone()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);
        var (created, _) = await service.CreateAsync(NewNote("Task"));
        Assert.Null(created!.DoneAtUtc);

        var (updated, error) = await service.UpdateAsync(created.Id, UpdateWithDone("Task", isDone: true));

        Assert.Equal(NoteWriteError.None, error);
        Assert.NotNull(updated!.DoneAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_KeepsOriginalDoneAt_WhenEditedWhileStillDone()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);
        var (created, _) = await service.CreateAsync(NewNote("Task"));
        var (done, _) = await service.UpdateAsync(created!.Id, UpdateWithDone("Task", isDone: true));

        var (edited, _) = await service.UpdateAsync(created.Id, UpdateWithDone("Task (edited)", isDone: true));

        Assert.Equal(done!.DoneAtUtc, edited!.DoneAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ClearsDoneAt_WhenReopened()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);
        var (created, _) = await service.CreateAsync(NewNote("Task"));
        await service.UpdateAsync(created!.Id, UpdateWithDone("Task", isDone: true));

        var (reopened, _) = await service.UpdateAsync(created.Id, UpdateWithDone("Task", isDone: false));

        Assert.Null(reopened!.DoneAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);

        var (note, error) = await service.UpdateAsync(
            Guid.NewGuid(), new UpdateNoteRequest("X", null, null, null, false, false, null));

        Assert.Null(note);
        Assert.Equal(NoteWriteError.NotFound, error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesNote_AndReturnsTrue()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);
        var (created, _) = await service.CreateAsync(NewNote("Temp"));

        Assert.True(await service.DeleteAsync(created!.Id));
        Assert.Equal(0, await db.Notes.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        await using var db = CreateContext();
        var service = new NoteService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }
}
