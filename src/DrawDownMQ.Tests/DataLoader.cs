namespace DrawDownMQ.Tests;

public static class DataLoader
{
    public static async Task<byte[]> ReadSampleShort()
    {
        return await File.ReadAllBytesAsync("rawData/sample_1.txt");
    }
    
    public static async Task<byte[]> ReadSampleLong()
    {
        return await File.ReadAllBytesAsync("rawData/sample_2.txt");
    }
    
    public static async Task<byte[]> ReadSampleJson()
    {
        return await File.ReadAllBytesAsync("rawData/sample_3.json");
    }
}