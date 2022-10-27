using System.Security.Cryptography;
using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Hashing;

public class Sha512HashProvider : IHashProvider
{
    public async Task<byte[]> GetHash(byte[] message, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream(message);
        using var sha1 = SHA512.Create();
        var result = await sha1.ComputeHashAsync(memory, cancellationToken);
        return result;
    }

    public int HashSize => 512 / 8;

    public void ComputeHash(ReadOnlySpan<byte> message, Span<byte> hashBuffer)
    {
        using var sha = SHA512.Create();
        sha.TryComputeHash(message, hashBuffer, out _);
    }
}