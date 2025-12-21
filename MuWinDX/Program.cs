using System.Runtime.InteropServices;

internal class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    private static void Main()
    {
        AllocConsole();
        using var game = new Client.Main.MuGame();
        game.Run();
    }
}