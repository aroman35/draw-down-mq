namespace DrawDownMQ.Connection.Abstractions;

public interface ICompressionProvider
{
    Task<byte[]> Compress(byte[] sourceMessage, CancellationToken cancellationToken);
    Task<byte[]> Decompress(byte[] compressedMessage, CancellationToken cancellationToken);
    Task Decompress(Memory<byte> sourceMessage, Memory<byte> buffer, CancellationToken cancellationToken);
}

public interface IEncryptionProvider
{
    Task<byte[]> Encrypt(byte[] sourceMessage, CancellationToken cancellationToken);
    Task<byte[]> Decrypt(byte[] encryptedMessage, CancellationToken cancellationToken);
}