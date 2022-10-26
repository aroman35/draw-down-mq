using System.Net;
using System.Text;
using DrawDownMQ.Connection.Abstractions;
using DrawDownMQ.Connection.Common;
using DrawDownMQ.Connection.Presentation.Compression;
using DrawDownMQ.Connection.Presentation.Encryption;
using DrawDownMQ.Connection.Presentation.Hashing;
using DrawDownMQ.Connection.Session;
using DrawDownMQ.Connection.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Xunit;
using Xunit.Abstractions;

namespace DrawDownMQ.Tests;

public class RuntimeTests
{
    private readonly ILoggerFactory _loggerFactory;
    
    public RuntimeTests(ITestOutputHelper testOutputHelper)
    {
        _loggerFactory = LoggerFactory.Create(l =>
        {
            l.SetMinimumLevel(LogLevel.Trace);
            l.AddProvider(new XunitLoggerProvider(testOutputHelper));
        });
    }
    [Fact]
    public async Task SimpleMessageRpc()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var compressionSwitcher = new CompressionSwitcher();
        var encryptionSwitcher = new EncryptionSwitcher();
        var hashSwitcher = new HashSwitcher();
        
        var sessionManager = new SessionsManager(
            compressionSwitcher,
            hashSwitcher,
            encryptionSwitcher,
            _loggerFactory.CreateLogger<ISessionsManager>());
        
        var serverEndpoint = IPEndPoint.Parse("127.0.0.1:13000");
        var server = new DrawDownMqServer(
            serverEndpoint,
            sessionManager,
            _loggerFactory.CreateLogger<IDrawDownMqListener>());

        var serverRuntime = await Task.Factory.StartNew(() => server.StartAsync(cancellationTokenSource.Token), cancellationTokenSource.Token);
        var client = new DrawDownMqClient(
            serverEndpoint,
            CreateRandomHeaders(),
            sessionManager,
            _loggerFactory.CreateLogger<IDrawDownMqListener>());

        await client.StartAsync(cancellationTokenSource.Token);
        var testMessage = Encoding.UTF8.GetBytes("Hello my fucking world!");
        await client.SendAsync(Guid.NewGuid(), testMessage, cancellationTokenSource.Token);
        await client.SendAsync(Guid.NewGuid(), testMessage, cancellationTokenSource.Token);
        await client.SendAsync(Guid.NewGuid(), testMessage, cancellationTokenSource.Token);
        await client.SendAsync(Guid.NewGuid(), testMessage, cancellationTokenSource.Token);
        await Task.Delay(1000, cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();
        await serverRuntime;
    }

    private SessionHeadersCollection CreateRandomHeaders()
    {
        var headersCollection = new List<SessionHeader>
        {
            SessionHeader.ClientId(Guid.NewGuid()),
            SessionHeader.Compression(CompressionType.Gzip),
            SessionHeader.ClientName("test")
        };

        var headers = new SessionHeadersCollection(headersCollection);
        return headers;
    }

    [Fact]
    public async Task HashTests()
    {
        var hashSwitcher = new HashSwitcher();
        var message = Encoding.UTF8.GetBytes("I am a test message, beach!");
        
        foreach (var hashType in Enum.GetValues<HashType>())
        {
            var hashProvider = hashSwitcher.Create(hashType);
            var hash = await hashProvider.GetHash(message, CancellationToken.None);
            var hashMatch = await hashProvider.CompareHash(message, hash, CancellationToken.None);
            Assert.True(hashMatch);
            Assert.Equal(hash.Length, hashProvider.HashSize);
        }
    }

    [Fact]
    public async Task EncryptionTests()
    {
        var encryptionSwitcher = new EncryptionSwitcher();
        var message = Encoding.UTF8.GetBytes("I am a test message, beach!");
        
        foreach (var encryptionType in Enum.GetValues<EncryptionType>())
        {
            var encryptionProvider = encryptionSwitcher.Create(encryptionType);
            var encrypted = await encryptionProvider.Encrypt(message, CancellationToken.None);
            var decrypted = await encryptionProvider.Decrypt(encrypted, CancellationToken.None);
            
            Assert.True(message.SequenceEqual(decrypted));
        }
    }

    [Fact]
    public async Task CompressionTests()
    {
        var compressionSwitcher = new CompressionSwitcher();
        var message = Encoding.UTF8.GetBytes("I am a test message, beach!");
        
        foreach (var compressionType in Enum.GetValues<CompressionType>())
        {
            var compressionProvider = compressionSwitcher.Create(compressionType);
            var compressedMessage = await compressionProvider.Compress(message, CancellationToken.None);
            var decompressed = await compressionProvider.Decompress(compressedMessage, CancellationToken.None);
            
            Assert.True(message.SequenceEqual(decompressed));
        }
    }
}