namespace DrawDownMQ.Connection.Abstractions;

public interface IDrawDownMqSerializer
{
    byte[] Serialize<T>(T message);
    T Deserialize<T>(byte[] rawMessage);
}