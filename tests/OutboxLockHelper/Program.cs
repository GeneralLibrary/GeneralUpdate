namespace OutboxLockHelper;

public static class Program
{
    public static void Main(string[] args)
    {
        using var file = new FileStream(args[0], FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Console.WriteLine("ready");
        Console.ReadLine();
    }
}
