using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Abstractions;

public interface IEncryptionSwitcher
{
    IEncryptionProvider Create(EncryptionType encryptionType);
}