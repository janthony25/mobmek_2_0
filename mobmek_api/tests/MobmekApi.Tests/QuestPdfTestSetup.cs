using System.Runtime.CompilerServices;

namespace MobmekApi.Tests;

/// <summary>Sets the QuestPDF license once when the test assembly loads. Production sets this
/// in Program.cs, which tests never run — they instantiate services directly.</summary>
internal static class QuestPdfTestSetup
{
    [ModuleInitializer]
    internal static void Init()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }
}
