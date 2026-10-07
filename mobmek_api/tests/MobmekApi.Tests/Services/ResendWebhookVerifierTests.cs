using System.Security.Cryptography;
using System.Text;
using MobmekApi.Services;

namespace MobmekApi.Tests.Services;

public class ResendWebhookVerifierTests
{
    private const string Secret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw"; // same shape as a real Svix secret, not a real one

    private static string Sign(string secret, string svixId, string svixTimestamp, string body)
    {
        var withoutPrefix = secret["whsec_".Length..];
        var key = Convert.FromBase64String(withoutPrefix);
        var signedContent = $"{svixId}.{svixTimestamp}.{body}";
        var hash = new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        return $"v1,{Convert.ToBase64String(hash)}";
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        const string body = """{"type":"email.delivered","data":{"email_id":"abc"}}""";
        const string id = "msg_1";
        const string timestamp = "1700000000";
        var signature = Sign(Secret, id, timestamp, body);

        Assert.True(ResendWebhookVerifier.Verify(Secret, id, timestamp, signature, body));
    }

    [Fact]
    public void Verify_WrongSecret_ReturnsFalse()
    {
        const string body = """{"type":"email.delivered","data":{"email_id":"abc"}}""";
        const string id = "msg_1";
        const string timestamp = "1700000000";
        var signature = Sign("whsec_" + Convert.ToBase64String(new byte[32]), id, timestamp, body);

        Assert.False(ResendWebhookVerifier.Verify(Secret, id, timestamp, signature, body));
    }

    [Fact]
    public void Verify_TamperedBody_ReturnsFalse()
    {
        const string body = """{"type":"email.delivered","data":{"email_id":"abc"}}""";
        const string id = "msg_1";
        const string timestamp = "1700000000";
        var signature = Sign(Secret, id, timestamp, body);

        Assert.False(ResendWebhookVerifier.Verify(Secret, id, timestamp, signature, body + "tampered"));
    }

    [Fact]
    public void Verify_MultipleSignatureEntries_AcceptsIfAnyMatch()
    {
        const string body = """{"type":"email.delivered","data":{"email_id":"abc"}}""";
        const string id = "msg_1";
        const string timestamp = "1700000000";
        var real = Sign(Secret, id, timestamp, body);
        var header = $"v1,bm90dGhlcmlnaHRvbmU= {real}";

        Assert.True(ResendWebhookVerifier.Verify(Secret, id, timestamp, header, body));
    }

    [Theory]
    [InlineData(null, "1700000000", "v1,abc")]
    [InlineData("msg_1", null, "v1,abc")]
    [InlineData("msg_1", "1700000000", null)]
    [InlineData("msg_1", "1700000000", "")]
    public void Verify_MissingHeader_ReturnsFalse(string? id, string? timestamp, string? signature)
    {
        Assert.False(ResendWebhookVerifier.Verify(Secret, id, timestamp, signature, "{}"));
    }

    [Fact]
    public void Verify_MalformedSecret_ReturnsFalseInsteadOfThrowing()
    {
        Assert.False(ResendWebhookVerifier.Verify("whsec_not-valid-base64!!", "msg_1", "1700000000", "v1,abc", "{}"));
    }
}
