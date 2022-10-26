using DrawDownMQ.Connection.Abstractions;

namespace DrawDownMQ.Connection.Presentation.Encryption;

public class NoneEncryptionProvider : IEncryptionProvider
{
    public Task<byte[]> Encrypt(byte[] sourceMessage, CancellationToken cancellationToken)
    {
        return Task.FromResult(sourceMessage);
    }

    public Task<byte[]> Decrypt(byte[] encryptedMessage, CancellationToken cancellationToken)
    {
        return Task.FromResult(encryptedMessage);
    }
}