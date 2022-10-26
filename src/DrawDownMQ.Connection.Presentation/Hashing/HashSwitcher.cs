using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;

namespace DrawDownMQ.Connection.Presentation.Hashing;

public class HashSwitcher : IHashSwitcher
{
    public IHashProvider Create(HashType hashType)
    {
        return hashType switch
        {
            HashType.SHA1 => new Sha1HashProvider(),
            HashType.SHA256 => new Sha256HashProvider(),
            HashType.SHA384 => new Sha384HashProvider(),
            HashType.SHA512 => new Sha512HashProvider(),
            _ => new Sha1HashProvider()
        };
    }
}