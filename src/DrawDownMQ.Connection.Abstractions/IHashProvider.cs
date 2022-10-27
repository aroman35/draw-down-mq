namespace DrawDownMQ.Connection.Abstractions;

public interface IHashProvider
{
    Task<byte[]> GetHash(byte[] message, CancellationToken cancellationToken);

    async Task<bool> CompareHashAsync(byte[] message, byte[] hash, CancellationToken cancellationToken)
    {
        var hashGenerated = await GetHash(message, cancellationToken);
        return hashGenerated.SequenceEqual(hash);
    }
    int HashSize { get; }

    unsafe bool CompareHash(Span<byte> message, Span<byte> hash)
    {
        Span<byte> computedHash = stackalloc byte[HashSize];
        ComputeHash(message, computedHash);
        fixed (byte* computedPtr = computedHash)
        {
            for (var i = 0; i < HashSize; i++)
            {
                if (computedPtr[i] != hash[i])
                    return false;
            }
        }
        return true;
    }
    void ComputeHash(Span<byte> message, Span<byte> hashBuffer);
}