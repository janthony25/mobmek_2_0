using MobmekApi.Services;

namespace MobmekApi.Tests.Services;

public class EmailTemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesKnownTokens()
    {
        var result = EmailTemplateRenderer.Render(
            "Hi {{CustomerName}}, from {{BusinessName}}.",
            new Dictionary<string, string?> { ["CustomerName"] = "Jane", ["BusinessName"] = "Mobmek" });

        Assert.Equal("Hi Jane, from Mobmek.", result);
    }

    [Fact]
    public void Render_UnknownToken_RendersEmpty()
    {
        var result = EmailTemplateRenderer.Render(
            "Car: {{CarPlate}}.",
            new Dictionary<string, string?>());

        Assert.Equal("Car: .", result);
    }

    [Fact]
    public void Render_NullValueToken_RendersEmpty()
    {
        var result = EmailTemplateRenderer.Render(
            "Car: {{CarPlate}}.",
            new Dictionary<string, string?> { ["CarPlate"] = null });

        Assert.Equal("Car: .", result);
    }

    [Fact]
    public void Render_NoTokens_ReturnsTemplateUnchanged()
    {
        var result = EmailTemplateRenderer.Render("Plain text, no tokens.", new Dictionary<string, string?>());

        Assert.Equal("Plain text, no tokens.", result);
    }
}
