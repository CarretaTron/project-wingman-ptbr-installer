using System;
using WingmanInstaller;

internal static class BuildPackage
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException("Uso: BuildPackage.exe <stub.exe> <pasta da tradução> <saída.exe>");
            Package.Build(args[0], args[1], args[2], Console.WriteLine);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
