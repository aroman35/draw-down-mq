using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Presentation.Encryption;

public class EncryptionSwitcher : IEncryptionSwitcher
{
    public IEncryptionProvider Create(EncryptionType encryptionType)
    {
        return new NoneEncryptionProvider();
    }
}