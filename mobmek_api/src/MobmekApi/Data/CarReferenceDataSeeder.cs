using MobmekApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MobmekApi.Data;

/// <summary>
/// Seeds a starter list of common car makes and their models. Idempotent: does nothing
/// once any makes exist, so it is safe to run on every Development startup.
/// </summary>
public static class CarReferenceDataSeeder
{
    private static readonly (string Make, string[] Models)[] Starter =
    [
        ("Toyota", ["Corolla", "Hilux", "RAV4", "Camry", "Prius"]),
        ("Honda", ["Civic", "Jazz", "CR-V", "Accord"]),
        ("Mazda", ["Mazda2", "Mazda3", "CX-5", "BT-50"]),
        ("Ford", ["Ranger", "Focus", "Falcon", "Everest"]),
        ("Nissan", ["Navara", "X-Trail", "Qashqai", "Leaf"]),
        ("Mitsubishi", ["Triton", "Outlander", "ASX", "Pajero"]),
        ("Subaru", ["Outback", "Forester", "Impreza", "XV"]),
        ("BMW", ["1 Series", "3 Series", "5 Series", "X5", "Z3"]),
        ("Mercedes-Benz", ["A-Class", "C-Class", "E-Class", "GLC"]),
        ("Volkswagen", ["Golf", "Polo", "Tiguan", "Amarok"]),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.CarMakes.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var (makeName, models) in Starter)
        {
            var make = new CarMake { Name = makeName };
            db.CarMakes.Add(make);

            foreach (var modelName in models)
            {
                db.CarModels.Add(new CarModel { Name = modelName, CarMake = make });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
