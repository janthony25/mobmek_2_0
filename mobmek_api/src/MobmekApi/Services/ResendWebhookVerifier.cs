using System.Security.Cryptography;
using System.Text;

namespace MobmekApi.Services;

/// <summary>
/// Verifies Resend's Svix-style webhook signature: HMAC-SHA256 over
/// <c>"{svix-id}.{svix-timestamp}.{body}"</c>, keyed by the base64 payload of a
/// <c>whsec_...</c> signing secret, compared against one of the space-separated
/// <c>v1,&lt;base64&gt;</c> entries in the <c>svix-signature</c> header.
/// </summary>
public static class ResendWebhookVerifier
{
    public static bool Verify(string secret, string? svixId, string? svixTimestamp, string? svixSignatureHeader, string body)
    {
        if (string.IsNullOrEmpty(svixId) || string.IsNullOrEmpty(svixTimestamp) || string.IsNullOrEmpty(svixSignatureHeader))
        {
            return false;
        }

        byte[] expected;
        try
        {
            var key = DecodeSecret(secret);
            var signedContent = $"{svixId}.{svixTimestamp}.{body}";
            expected = new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        }
        catch (FormatException)
        {
            return false;
        }

        foreach (var entry in svixSignatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = entry.Split(',', 2);
            if (pieces.Length != 2 || pieces[0] != "v1")
            {
                continue;
            }

            byte[] candidate;
            try
            {
                candidate = Convert.FromBase64String(pieces[1]);
            }
            catch (FormatException)
            {
                continue;
            }

            if (candidate.Length == expected.Length && CryptographicOperations.FixedTimeEquals(candidate, expected))
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] DecodeSecret(string secret)
    {
        var withoutPrefix = secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret["whsec_".Length..] : secret;
        return Convert.FromBase64String(withoutPrefix);
    }
}
