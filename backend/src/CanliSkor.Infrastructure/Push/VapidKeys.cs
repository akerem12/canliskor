using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CanliSkor.Infrastructure.Push;

/// <summary>
/// The server's Web Push identity (VAPID): a P-256 key pair, base64url. Browsers subscribe with the public key,
/// and the push services only accept messages signed with the matching private one.
/// </summary>
/// <param name="Persistent">False for a pair made up at startup, which is gone after a restart.</param>
internal sealed record VapidKeys(string PublicKey, string PrivateKey, bool Persistent)
{
    /// <summary>
    /// The same secret always gives the same pair, so one random setting (<c>Push__Secret</c>) is all a
    /// deployment needs and subscriptions survive restarts. Without a secret the pair is random: notifications
    /// still work, but every browser has to subscribe again after a restart (it does, when the site is opened).
    /// </summary>
    public static VapidKeys FromSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            using var random = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return FromParameters(random.ExportParameters(includePrivateParameters: true), persistent: false);
        }

        // A SHA-256 hash is a valid P-256 private key (all but a vanishing share of 256-bit numbers are).
        var d = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = d });
        return FromParameters(key.ExportParameters(includePrivateParameters: true), persistent: true);
    }

    private static VapidKeys FromParameters(ECParameters parameters, bool persistent)
    {
        // The public key as browsers want it: the uncompressed point, 0x04 | X | Y.
        byte[] point = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        return new VapidKeys(Base64Url.EncodeToString(point), Base64Url.EncodeToString(parameters.D!), persistent);
    }
}
