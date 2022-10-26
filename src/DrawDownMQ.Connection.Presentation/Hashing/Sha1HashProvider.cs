using System.Security.Cryptography;
using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Hashing;

public class Sha1HashProvider : IHashProvider
{
    public async Task<byte[]> GetHash(byte[] message, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream(message);
        using var sha1 = SHA1.Create();
        var result = await sha1.ComputeHashAsync(memory, cancellationToken);
        return result;
    }

    public int HashSize => 160 / 8;
}