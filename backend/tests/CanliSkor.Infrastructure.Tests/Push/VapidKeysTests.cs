using System.Buffers.Text;
using System.Security.Cryptography;
using CanliSkor.Infrastructure.Push;

namespace CanliSkor.Infrastructure.Tests.Push;

public class VapidKeysTests
{
    [Fact]
    public void The_same_secret_always_gives_the_same_pair()
    {
        var first = VapidKeys.FromSecret("a long random secret");
        var second = VapidKeys.FromSecret("a long random secret");

        Assert.Equal(first, second);
        Assert.True(first.Persistent);
        Assert.NotEqual(first.PublicKey, VapidKeys.FromSecret("another secret").PublicKey);
    }

    [Fact]
    public void The_public_key_is_the_point_that_belongs_to_the_private_key()
    {
        var keys = VapidKeys.FromSecret("a long random secret");
        var point = Base64Url.DecodeFromChars(keys.PublicKey);

        // What browsers expect: 65 bytes, uncompressed.
        Assert.Equal(65, point.Length);
        Assert.Equal(0x04, point[0]);

        // A signature made with the private key verifies with the public one.
        using var signer = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.DecodeFromChars(keys.PrivateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..] },
        });
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33], Y = point[33..] },
        });
        byte[] data = [1, 2, 3];
        Assert.True(verifier.VerifyData(data, signer.SignData(data, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Without_a_secret_the_pair_is_made_up_and_marked_as_temporary(string? secret)
    {
        var keys = VapidKeys.FromSecret(secret);

        Assert.False(keys.Persistent);
        Assert.Equal(65, Base64Url.DecodeFromChars(keys.PublicKey).Length);
        Assert.NotEqual(keys.PublicKey, VapidKeys.FromSecret(secret).PublicKey);
    }
}
