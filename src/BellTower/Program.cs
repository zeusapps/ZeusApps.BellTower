using System.Text;
using BellTower.Cli;

namespace BellTower;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
        try
        {
            return CliApplication.Run(args);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            Console.Error.WriteLine("bell-tower: " + exception.Message);
            return 1;
        }
    }
}
