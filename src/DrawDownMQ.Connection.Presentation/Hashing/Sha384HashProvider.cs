using System.Security.Cryptography;
using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Hashing;

public class Sha384HashProvider : IHashProvider
{
    public async Task<byte[]> GetHash(byte[] message, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream(message);
        using var sha1 = SHA384.Create();
        var result = await sha1.ComputeHashAsync(memory, cancellationToken);
        return result;
    }

    public int HashSize => 384 / 8;
    public void ComputeHash(Span<byte> message, Span<byte> hashBuffer)
    {
        using var sha = SHA384.Create();
        sha.TryComputeHash(message, hashBuffer, out _);
    }
}