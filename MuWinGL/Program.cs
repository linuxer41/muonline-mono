using System.Runtime.InteropServices;

internal class Program
{
#if DEBUG
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
#endif

    private static void Main()
    {
#if DEBUG
        AllocConsole();
#endif
        using var game = new Client.Main.MuGame();
        game.Run();
    }
}