using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace WingmanInstaller
{
    internal sealed class PayloadFile
    {
        public string Path;
        public string Hash;
        public long Length;
    }

    internal sealed class InstallProgress
    {
        public int Percent;
        public string Message;
        public InstallProgress(int percent, string message) { Percent = percent; Message = message; }
    }

    internal sealed class Package : IDisposable
    {
        public const string Version = "0.91";
        public static readonly string[] Paths = {
            "ProjectWingman/Binaries/Win64/ProjectWingman-Win64-Shipping.exe",
            "ProjectWingman/Content/Paks/~mods/ProjectWingman_PTBR_Assets_P.pak",
            "ProjectWingman/Content/Paks/~mods/ProjectWingman_PTBR_Locres_P.pak"
        };
        private readonly FileStream stream;
        private readonly ZipArchive archive;
        public readonly List<PayloadFile> Files = new List<PayloadFile>();
        public long TotalLength;

        public Package(string path)
        {
            stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
                ZipArchiveEntry manifest = archive.GetEntry("payload.manifest");
                if (manifest == null || manifest.Length > 16384 || archive.Entries.Count != Paths.Length + 1)
                    throw new InvalidDataException("O pacote de instalação está incompleto.");
                using (StreamReader reader = new StreamReader(manifest.Open(), Encoding.UTF8))
                {
                    if (reader.ReadLine() != "PWPTBR\t" + Version)
                        throw new InvalidDataException("Versão do pacote inválida.");
                    HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] fields = line.Split('\t');
                        long length;
                        if (fields.Length != 3 || fields[0].Length != 64 ||
                            !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out length) ||
                            length <= 0 || Array.IndexOf(Paths, fields[2]) < 0 || !seen.Add(fields[2]))
                            throw new InvalidDataException("A lista de arquivos do pacote é inválida.");
                        foreach (char value in fields[0])
                            if (!Uri.IsHexDigit(value)) throw new InvalidDataException("Hash inválido no pacote.");
                        ZipArchiveEntry entry = archive.GetEntry(fields[2]);
                        if (entry == null || entry.Length != length)
                            throw new InvalidDataException("Arquivo ausente ou danificado: " + fields[2]);
                        Files.Add(new PayloadFile { Path = fields[2], Hash = fields[0], Length = length });
                        TotalLength = checked(TotalLength + length);
                    }
                    if (Files.Count != Paths.Length) throw new InvalidDataException("O pacote está incompleto.");
                }
            }
            catch { Dispose(); throw; }
        }

        public Stream Open(PayloadFile file) { return archive.GetEntry(file.Path).Open(); }
        public void Dispose()
        {
            if (archive != null) archive.Dispose();
            if (stream != null) stream.Dispose();
        }

        public static string HashFile(string path)
        {
            using (FileStream input = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return Hex(sha.ComputeHash(input));
        }

        public static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        // The ZIP central directory stores absolute offsets, so the executable
        // prefix is deliberately retained in the same seekable stream.
        public static void Build(string stub, string source, string output, Action<string> log)
        {
            string partial = output + ".building";
            if (File.Exists(partial)) File.Delete(partial);
            try
            {
                File.Copy(stub, partial, false);
                StringBuilder manifest = new StringBuilder("PWPTBR\t" + Version + "\n");
                using (FileStream target = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    target.Position = target.Length;
                    using (ZipArchive zip = new ZipArchive(target, ZipArchiveMode.Create, true))
                    {
                        byte[] buffer = new byte[1024 * 1024];
                        foreach (string relative in Paths)
                        {
                            string path = System.IO.Path.Combine(source, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
                            log("Compactando " + relative);
                            ZipArchiveEntry entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                            using (FileStream input = File.OpenRead(path))
                            using (Stream compressed = entry.Open())
                            using (SHA256 sha = SHA256.Create())
                            {
                                int read;
                                long done = 0;
                                int last = -1;
                                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    compressed.Write(buffer, 0, read);
                                    sha.TransformBlock(buffer, 0, read, buffer, 0);
                                    done += read;
                                    int progress = (int)(done * 100 / input.Length);
                                    if (progress / 10 != last) { last = progress / 10; log(progress + "%"); }
                                }
                                sha.TransformFinalBlock(new byte[0], 0, 0);
                                manifest.Append(Package.Hex(sha.Hash)).Append('\t')
                                    .Append(input.Length.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(relative).Append('\n');
                            }
                        }
                        using (StreamWriter writer = new StreamWriter(zip.CreateEntry("payload.manifest").Open(), new UTF8Encoding(false)))
                            writer.Write(manifest.ToString());
                    }
                    target.Flush(true);
                }
                using (Package check = new Package(partial))
                    log("Pacote validado: " + check.Files.Count + " arquivos, " + check.TotalLength + " bytes descompactados.");
                if (File.Exists(output)) File.Delete(output);
                File.Move(partial, output);
                log("Concluído: " + output);
            }
            catch { if (File.Exists(partial)) File.Delete(partial); throw; }
        }
    }

    internal static class InstallerEngine
    {
        public const string BackupName = "Backup_Traducao_PTBR_v0.91";

        public static string ResolveGameRoot(string selected)
        {
            if (string.IsNullOrWhiteSpace(selected))
                throw new InvalidOperationException("Selecione a pasta em que o Project Wingman está instalado.");
            string root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(selected.Trim().Trim('"')))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root)) throw new InvalidOperationException("A pasta selecionada não existe.");
            if (string.Equals(Path.GetFileName(root), "ProjectWingman", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(Path.Combine(root, "Binaries", "Win64")))
                root = Path.GetDirectoryName(root);
            string exe = Path.Combine(root, Package.Paths[0].Replace('/', Path.DirectorySeparatorChar));
            string paks = Path.Combine(root, "ProjectWingman", "Content", "Paks");
            if (!File.Exists(exe) || !Directory.Exists(paks) || Directory.GetFiles(paks, "*.pak", SearchOption.TopDirectoryOnly).Length == 0)
                throw new InvalidOperationException("Não encontrei o jogo nesta pasta.\nSelecione a pasta principal do Project Wingman, onde ficam os arquivos do jogo.");
            return root;
        }

        public static string Destination(string root, string relative)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!result.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Caminho inválido.");
            return result;
        }

        public static void AssertWritable(string root)
        {
            string probe = Path.Combine(root, ".pw-ptbr-access-" + Guid.NewGuid().ToString("N"));
            using (FileStream file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
        }

        private static void CheckFileLocks(Package package, string root)
        {
            foreach (PayloadFile file in package.Files)
            {
                string target = Destination(root, file.Path);
                if (!File.Exists(target)) continue;
                try { using (FileStream test = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } }
                catch (IOException)
                {
                    throw new IOException("Um arquivo do jogo está em uso. Feche o Project Wingman e tente novamente.\n" + Path.GetFileName(target));
                }
            }
        }

        private sealed class Change
        {
            public string Target;
            public string Previous;
            public bool Existed;
        }

        public static void Install(Package package, string selected, Action<InstallProgress> progress, Action<int> afterCommit)
        {
            string root = ResolveGameRoot(selected);
            AssertWritable(root);
            CheckFileLocks(package, root);
            string backupRoot = Path.Combine(root, BackupName);
            long needed = checked(package.TotalLength + 64L * 1024 * 1024);
            foreach (PayloadFile file in package.Files)
            {
                string target = Destination(root, file.Path);
                if (File.Exists(target) && !File.Exists(Destination(backupRoot, file.Path)))
                    needed = checked(needed + new FileInfo(target).Length);
            }
            string driveRoot = Path.GetPathRoot(root);
            if (!driveRoot.StartsWith("\\", StringComparison.Ordinal) && new DriveInfo(driveRoot).AvailableFreeSpace < needed)
                throw new IOException("Não há espaço livre suficiente. Libere pelo menos " +
                    (needed / 1073741824.0).ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " GB na unidade do jogo.");

            string stage = Path.Combine(root, ".pw-ptbr-stage-" + Guid.NewGuid().ToString("N"));
            List<Change> changed = new List<Change>();
            List<string> newBackups = new List<string>();
            bool keepRecovery = false;
            try
            {
                Directory.CreateDirectory(stage);
                byte[] buffer = new byte[1024 * 1024];
                long done = 0;
                int lastPercent = -1;
                foreach (PayloadFile file in package.Files)
                {
                    string staged = Destination(Path.Combine(stage, "new"), file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(staged));
                    using (Stream input = package.Open(file))
                    using (FileStream output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (SHA256 sha = SHA256.Create())
                    {
                        long copied = 0;
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            copied += read;
                            if (copied > file.Length) throw new InvalidDataException("Arquivo maior do que o esperado.");
                            output.Write(buffer, 0, read);
                            sha.TransformBlock(buffer, 0, read, buffer, 0);
                            done += read;
                            int percent = (int)(done * 85 / package.TotalLength);
                            if (percent != lastPercent)
                            {
                                lastPercent = percent;
                                progress(new InstallProgress(percent, "Extraindo e verificando a tradução... " + percent + "%"));
                            }
                        }
                        sha.TransformFinalBlock(new byte[0], 0, 0);
                        if (copied != file.Length || !string.Equals(Package.Hex(sha.Hash), file.Hash, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("A verificação do pacote falhou. Baixe o instalador novamente.\n" + Path.GetFileName(staged));
                        output.Flush(true);
                    }
                }

                CheckFileLocks(package, root);
                progress(new InstallProgress(87, "Salvando o backup dos arquivos anteriores..."));
                foreach (PayloadFile file in package.Files)
                {
                    string target = Destination(root, file.Path);
                    string original = Destination(backupRoot, file.Path);
                    if (!File.Exists(target) || File.Exists(original)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(original));
                    string pending = original + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { File.Copy(target, pending, false); File.Move(pending, original); newBackups.Add(original); }
                    finally { if (File.Exists(pending)) File.Delete(pending); }
                }

                int count = 0;
                foreach (PayloadFile file in package.Files)
                {
                    string target = Destination(root, file.Path);
                    string previous = Destination(Path.Combine(stage, "previous"), file.Path);
                    Change change = new Change { Target = target, Previous = previous, Existed = File.Exists(target) };
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    Directory.CreateDirectory(Path.GetDirectoryName(previous));
                    changed.Add(change);
                    if (change.Existed) File.Move(target, previous);
                    File.Move(Destination(Path.Combine(stage, "new"), file.Path), target);
                    count++;
                    progress(new InstallProgress(88 + count * 11 / package.Files.Count, "Instalando os arquivos da tradução..."));
                    if (afterCommit != null) afterCommit(count);
                }
                progress(new InstallProgress(100, "Tradução instalada com sucesso!\nInicie o jogo com o idioma configurado para inglês."));
            }
            catch (Exception problem)
            {
                List<string> failures = new List<string>();
                for (int i = changed.Count - 1; i >= 0; i--)
                {
                    Change change = changed[i];
                    try
                    {
                        if (change.Existed && File.Exists(change.Previous)) File.Copy(change.Previous, change.Target, true);
                        else if (!change.Existed && File.Exists(change.Target)) File.Delete(change.Target);
                    }
                    catch { failures.Add(change.Target); }
                }
                if (failures.Count > 0)
                {
                    keepRecovery = true;
                    throw new IOException("A instalação foi interrompida e alguns arquivos precisam ser restaurados.\n" +
                        "O backup de recuperação está em:\n" + stage + "\n\n" + string.Join("\n", failures.ToArray()), problem);
                }
                foreach (string backup in newBackups) { try { File.Delete(backup); } catch { } }
                throw;
            }
            finally
            {
                // Only this run's generated child directory may be removed.
                string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + ".pw-ptbr-stage-";
                if (!keepRecovery && Path.GetFullPath(stage).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                { try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { } }
            }
        }
    }
}
