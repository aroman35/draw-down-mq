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

    bool CompareHash(ReadOnlySpan<byte> message, ReadOnlySpan<byte> hash)
    {
        Span<byte> computedHash = stackalloc byte[HashSize];
        ComputeHash(message, computedHash);
        return computedHash.SequenceEqual(hash);
    }
    void ComputeHash(ReadOnlySpan<byte> message, Span<byte> hashBuffer);
}