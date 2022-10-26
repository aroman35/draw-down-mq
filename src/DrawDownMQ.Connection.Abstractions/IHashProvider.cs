namespace DrawDownMQ.Connection.Abstractions;

public interface IHashProvider
{
    Task<byte[]> GetHash(byte[] message, CancellationToken cancellationToken);

    async Task<bool> CompareHash(byte[] message, byte[] hash, CancellationToken cancellationToken)
    {
        var hashGenerated = await GetHash(message, cancellationToken);
        return hashGenerated.SequenceEqual(hash);
    }
    int HashSize { get; }
}