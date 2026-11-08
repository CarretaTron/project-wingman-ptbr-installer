using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using WingmanInstaller;

internal static class InstallerTests
{
    private static int passed;
    private static string sandbox;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FALHOU: " + message);
        passed++;
        Console.WriteLine("OK " + passed + ": " + message);
    }

    private static void Write(string root, string relative, string text)
    {
        string path = InstallerEngine.Destination(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static string Read(string root, string relative)
    { return File.ReadAllText(InstallerEngine.Destination(root, relative)); }

    private static string Fixture(string name)
    {
        string root = Path.Combine(sandbox, name);
        Write(root, Package.Paths[0], "EXECUTAVEL ORIGINAL");
        Write(root, "ProjectWingman/Content/Paks/ProjectWingman-WindowsNoEditor.pak", "PAK ORIGINAL DO JOGO");
        Write(root, "ProjectWingman/Content/Paks/~mods/OutroMod.pak", "MOD DO USUARIO");
        return root;
    }

    private static void NoStage(string root)
    { Check(Directory.GetDirectories(root, ".pw-ptbr-stage-*").Length == 0, "pasta temporária removida"); }

    private static void Progress(InstallProgress progress) { }

    private static int Main(string[] args)
    {
        try
        {
            string build = Path.GetFullPath(args[0]);
            sandbox = Path.Combine(build, "testes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sandbox);
            File.WriteAllText(Path.Combine(sandbox, ".installer-test-marker"), "generated fixture");
            string stub = Path.Combine(sandbox, "prefix.bin");
            File.WriteAllBytes(stub, new byte[3073]);
            string source = Path.Combine(sandbox, "fonte");
            for (int i = 0; i < Package.Paths.Length; i++) Write(source, Package.Paths[i], new string((char)('A' + i), 10000 + i));
            string small = Path.Combine(sandbox, "pequeno.exe");
            Package.Build(stub, source, small, delegate(string line) { });

            using (Package package = new Package(small))
            {
                Check(package.Files.Count == 3, "pacote com prefixo executável contém os três arquivos");
                string root = Fixture("Jogo teste com acentos áé");
                string mods = Package.Paths[2];
                Write(root, mods, "TRADUCAO ANTERIOR");
                Check(InstallerEngine.ResolveGameRoot(Path.Combine(root, "ProjectWingman")) == root, "seleção da pasta interna é normalizada");
                Check(InstallerEngine.ResolveGameRoot("\"" + root + "\"") == root, "caminho entre aspas é aceito");
                bool rejected = false;
                try { InstallerEngine.ResolveGameRoot(source); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "pasta do pacote não é confundida com instalação do jogo");
                rejected = false;
                try { InstallerEngine.Destination(root, "../fora.txt"); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "caminho fora da pasta de destino é rejeitado");

                InstallerEngine.Install(package, root, Progress, null);
                foreach (PayloadFile file in package.Files)
                    Check(Package.HashFile(InstallerEngine.Destination(root, file.Path)) == file.Hash, "arquivo instalado confere: " + Path.GetFileName(file.Path));
                Check(Read(Path.Combine(root, InstallerEngine.BackupName), Package.Paths[0]) == "EXECUTAVEL ORIGINAL", "backup do executável original");
                Check(Read(Path.Combine(root, InstallerEngine.BackupName), mods) == "TRADUCAO ANTERIOR", "backup da tradução anterior");
                Check(Read(root, "ProjectWingman/Content/Paks/~mods/OutroMod.pak") == "MOD DO USUARIO", "outro mod permanece intacto");
                NoStage(root);
                InstallerEngine.Install(package, root, Progress, null);
                Check(Read(Path.Combine(root, InstallerEngine.BackupName), Package.Paths[0]) == "EXECUTAVEL ORIGINAL", "reinstalação preserva o primeiro backup");
                NoStage(root);

                string failureRoot = Fixture("Teste de falha e restauração");
                rejected = false;
                try
                {
                    InstallerEngine.Install(package, failureRoot, Progress,
                        delegate(int index) { if (index == 2) throw new IOException("FALHA SIMULADA"); });
                }
                catch (IOException problem) { rejected = problem.Message == "FALHA SIMULADA"; }
                Check(rejected, "falha simulada durante a substituição é reportada");
                Check(Read(failureRoot, Package.Paths[0]) == "EXECUTAVEL ORIGINAL", "falha restaura o executável anterior");
                Check(!File.Exists(InstallerEngine.Destination(failureRoot, Package.Paths[1])), "falha remove o arquivo novo já instalado");
                Check(!File.Exists(InstallerEngine.Destination(failureRoot, Package.Paths[2])), "falha não instala os arquivos restantes");
                Check(Read(failureRoot, "ProjectWingman/Content/Paks/~mods/OutroMod.pak") == "MOD DO USUARIO", "falha preserva outros mods");
                NoStage(failureRoot);

                string lockRoot = Fixture("Jogo aberto");
                using (FileStream locked = new FileStream(InstallerEngine.Destination(lockRoot, Package.Paths[0]), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    rejected = false;
                    try { InstallerEngine.Install(package, lockRoot, Progress, null); } catch (IOException) { rejected = true; }
                    Check(rejected, "arquivo do jogo em uso bloqueia a instalação");
                }
                Check(Read(lockRoot, Package.Paths[0]) == "EXECUTAVEL ORIGINAL", "arquivo bloqueado permanece intacto");
                NoStage(lockRoot);
            }

            string corrupt = Path.Combine(sandbox, "corrompido.exe");
            using (ZipArchive archive = ZipFile.Open(corrupt, ZipArchiveMode.Create))
            {
                StringBuilder manifest = new StringBuilder("PWPTBR\t0.91\n");
                foreach (string path in Package.Paths)
                {
                    using (Stream output = archive.CreateEntry(path).Open()) output.WriteByte(0x41);
                    manifest.Append(new string('0', 64)).Append("\t1\t").Append(path).Append('\n');
                }
                using (StreamWriter writer = new StreamWriter(archive.CreateEntry("payload.manifest").Open(), new UTF8Encoding(false))) writer.Write(manifest.ToString());
            }
            string corruptRoot = Fixture("Pacote corrompido");
            using (Package package = new Package(corrupt))
            {
                bool rejected = false;
                try { InstallerEngine.Install(package, corruptRoot, Progress, null); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "SHA-256 incorreto impede a instalação");
            }
            Check(Read(corruptRoot, Package.Paths[0]) == "EXECUTAVEL ORIGINAL", "pacote corrompido não altera o jogo");
            NoStage(corruptRoot);

            if (args.Length == 3)
            {
                Console.WriteLine("Verificação do instalador completo...");
                string root = Fixture("Instalação real com todos os arquivos");
                int last = -1;
                using (Package package = new Package(args[1]))
                {
                    InstallerEngine.Install(package, root, delegate(InstallProgress p)
                    { if (p.Percent / 10 != last) { last = p.Percent / 10; Console.WriteLine("Instalação completa: " + p.Percent + "%"); } }, null);
                    foreach (PayloadFile file in package.Files)
                    {
                        string actual = Package.HashFile(InstallerEngine.Destination(root, file.Path));
                        string original = Package.HashFile(InstallerEngine.Destination(args[2], file.Path));
                        Check(actual == file.Hash && actual == original, "SHA-256 confere com a fonte original: " + Path.GetFileName(file.Path));
                    }
                    Check(Read(Path.Combine(root, InstallerEngine.BackupName), Package.Paths[0]) == "EXECUTAVEL ORIGINAL", "instalação completa mantém backup");
                    Check(Read(root, "ProjectWingman/Content/Paks/~mods/OutroMod.pak") == "MOD DO USUARIO", "instalação completa mantém outros mods");
                    NoStage(root);
                }
            }
            Console.WriteLine("SUCESSO: " + passed + " verificações passaram.");
            // Resolve and verify the exact dedicated fixture path before recursive cleanup.
            string allowed = build.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "testes-";
            if (Path.GetFullPath(sandbox).StartsWith(allowed, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(sandbox, ".installer-test-marker"))) Directory.Delete(sandbox, true);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine("Arquivos para diagnóstico: " + sandbox);
            return 1;
        }
    }
}
