using System.Security.Claims;
using MobmekApi.Data;
using MobmekApi.Entities;
using MobmekApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Tests.Data;

/// <summary>Covers <see cref="AppDbContext.SaveChangesAsync(CancellationToken)"/>'s stamping of
/// <see cref="BaseEntity.UpdatedByUserId"/>/<see cref="BaseEntity.UpdatedByName"/> — the piece
/// every other {X}ServiceTests file relies on implicitly but doesn't test directly.</summary>
public class AppDbContextAuditTests
{
    private static AppDbContext CreateDb(IHttpContextAccessor? accessor = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor);

    private static IHttpContextAccessor CreateAccessor(Guid userId, string fullName)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(AppUserClaimsPrincipalFactory.FullNameClaimType, fullName)],
            authenticationType: "Test");
        return new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    [Fact]
    public async Task SaveChangesAsync_NoHttpContext_LeavesUpdatedByNull()
    {
        var db = CreateDb();
        var title = new EmployeeTitle { Name = "Mechanic" };
        db.EmployeeTitles.Add(title);
        await db.SaveChangesAsync();

        title.Name = "Senior Mechanic";
        await db.SaveChangesAsync();

        Assert.NotNull(title.UpdatedAtUtc);
        Assert.Null(title.UpdatedByUserId);
        Assert.Null(title.UpdatedByName);
    }

    [Fact]
    public async Task SaveChangesAsync_WithSignedInUser_StampsUpdatedByFromClaims()
    {
        var userId = Guid.NewGuid();
        var db = CreateDb(CreateAccessor(userId, "Jane Doe"));
        var title = new EmployeeTitle { Name = "Mechanic" };
        db.EmployeeTitles.Add(title);
        await db.SaveChangesAsync();

        title.Name = "Senior Mechanic";
        await db.SaveChangesAsync();

        Assert.Equal(userId, title.UpdatedByUserId);
        Assert.Equal("Jane Doe", title.UpdatedByName);
    }

    [Fact]
    public async Task SaveChangesAsync_OnCreate_DoesNotStampUpdatedBy()
    {
        var db = CreateDb(CreateAccessor(Guid.NewGuid(), "Jane Doe"));
        var title = new EmployeeTitle { Name = "Mechanic" };
        db.EmployeeTitles.Add(title);
        await db.SaveChangesAsync();

        Assert.Null(title.UpdatedAtUtc);
        Assert.Null(title.UpdatedByUserId);
        Assert.Null(title.UpdatedByName);
    }
}
