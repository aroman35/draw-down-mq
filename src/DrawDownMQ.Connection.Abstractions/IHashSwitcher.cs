using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Abstractions;

public interface IHashSwitcher
{
    IHashProvider Create(HashType hashType);
}