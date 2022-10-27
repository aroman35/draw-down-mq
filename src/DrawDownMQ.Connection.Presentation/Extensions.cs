namespace DrawDownMQ.Connection.Presentation;

public static class Extensions
{
    public static void CopyTo<T>(this Span<T> source, Span<T> destination, int offset)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[offset + i] = source[i];
        }
    }
}