using MobmekApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Data;

/// <summary>
/// Ensures the Admin/Employee roles exist and, if no Admin account exists yet, creates the
/// first one from Bootstrap:AdminEmail / Bootstrap:AdminPassword config (set via
/// Bootstrap__AdminEmail / Bootstrap__AdminPassword env vars in production) so there's a way
/// into a fresh deployment. Idempotent: does nothing once any Admin exists. Runs in every
/// environment (unlike the dev-only reference-data seeders) since production needs this too.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        foreach (var role in new[] { "Admin", "Employee" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        if ((await userManager.GetUsersInRoleAsync("Admin")).Count > 0)
        {
            return;
        }

        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No Admin account exists and Bootstrap:AdminEmail / Bootstrap:AdminPassword are not set — " +
                "nobody can log in. Set them (env vars Bootstrap__AdminEmail / Bootstrap__AdminPassword) and restart.");
            return;
        }

        // The account needs an HR record to link to. Reuse an existing title/employment type
        // if one happens to be seeded already, otherwise create the minimal pair so this works
        // on a genuinely empty database.
        var title = await db.EmployeeTitles.FirstOrDefaultAsync(cancellationToken)
            ?? (await db.EmployeeTitles.AddAsync(new EmployeeTitle { Name = "Owner" }, cancellationToken)).Entity;
        var employmentType = await db.EmploymentTypes.FirstOrDefaultAsync(cancellationToken)
            ?? (await db.EmploymentTypes.AddAsync(new EmploymentType { Name = "Full-time" }, cancellationToken)).Entity;
        await db.SaveChangesAsync(cancellationToken);

        var employee = new Employee
        {
            FirstName = configuration["Bootstrap:AdminFirstName"] ?? "Admin",
            LastName = configuration["Bootstrap:AdminLastName"] ?? "User",
            TitleId = title.Id,
            EmploymentTypeId = employmentType.Id,
            ContactNumber = configuration["Bootstrap:AdminContactNumber"] ?? "N/A",
            EmailAddress = email,
            PhysicalAddress = "N/A",
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            EmployeeId = employee.Id,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("Failed to create the bootstrap Admin account: {Errors}", errors);
            return;
        }

        await userManager.AddToRoleAsync(user, "Admin");
        logger.LogInformation("Created bootstrap Admin account for {Email}.", email);
    }
}
