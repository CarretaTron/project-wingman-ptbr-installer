using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Project Wingman — Tradução PT-BR")]
[assembly: AssemblyDescription("Instalador da tradução brasileira de Project Wingman por Carreta")]
[assembly: AssemblyProduct("Project Wingman PT-BR")]
[assembly: AssemblyCompany("Carreta")]
[assembly: AssemblyVersion("0.91.0.0")]
[assembly: AssemblyFileVersion("0.91.0.0")]

namespace WingmanInstaller
{
    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 2 && args[0] == "--preview")
                {
                    using (InstallerForm form = new InstallerForm(null, true))
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-20000, -20000);
                        form.ShowInTaskbar = false;
                        form.Show();
                        Application.DoEvents();
                        using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                        { form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(args[1], ImageFormat.Png); }
                        form.Close();
                    }
                    return 0;
                }
                string initial = null;
                if (args.Length == 2 && args[0] == "--install-target-base64")
                    initial = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
                Application.Run(new InstallerForm(initial, false));
                return 0;
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Project Wingman — Instalação", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    internal sealed class ProgressTrack : Control
    {
        private int value;
        public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public ProgressTrack() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(58, 67, 78));
            using (Brush fill = new SolidBrush(value == 100 ? Color.FromArgb(88, 190, 139) : Color.FromArgb(108, 153, 181)))
                e.Graphics.FillRectangle(fill, 0, 0, Width * value / 100, Height);
        }
    }

    internal sealed class FolderButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int x = (Width - 21) / 2;
            int y = (Height - 17) / 2;
            using (Brush back = new SolidBrush(Color.FromArgb(234, 177, 61)))
            using (Brush front = new SolidBrush(Color.FromArgb(255, 210, 91)))
            using (Pen border = new Pen(Color.FromArgb(155, 107, 29)))
            {
                Point[] rear = { new Point(x, y), new Point(x + 8, y), new Point(x + 11, y + 3), new Point(x + 21, y + 3), new Point(x + 21, y + 16), new Point(x, y + 16) };
                e.Graphics.FillPolygon(back, rear); e.Graphics.DrawPolygon(border, rear);
                Point[] face = { new Point(x + 1, y + 6), new Point(x + 22, y + 6), new Point(x + 20, y + 17), new Point(x + 1, y + 17) };
                e.Graphics.FillPolygon(front, face); e.Graphics.DrawPolygon(border, face);
            }
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly Image banner;
        private readonly Image logo;
        private readonly TextBox directory;
        private readonly Button install;
        private readonly Button close;
        private readonly FolderButton browse;
        private readonly Label status;
        private readonly ProgressTrack progress;
        private readonly ToolTip tips;
        private readonly BackgroundWorker worker;
        private readonly bool preview;
        private readonly string initial;
        private bool installing;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public InstallerForm(string initial, bool preview)
        {
            this.preview = preview;
            this.initial = initial;
            banner = LoadImage("banner.jpg");
            logo = LoadImage("logo.png");
            using (Stream icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("launcher.ico")) Icon = new Icon(icon);
            Text = "Project Wingman — Tradução PT-BR v0.91";
            ClientSize = new Size(815, 599);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(14, 24, 35);
            Font = new Font("Segoe UI", 9F);
            DoubleBuffered = true;

            Label instruction = MakeLabel("Selecione a pasta de instalação do Project Wingman\ne clique em Instalar tradução.",
                28, 155, 760, 54, new Font("Segoe UI", 13F, FontStyle.Bold), Color.White);
            Controls.Add(instruction);

            directory = new TextBox { Location = new Point(23, 222), Size = new Size(719, 27),
                Font = new Font("Segoe UI", 9.5F), BorderStyle = BorderStyle.FixedSingle, TabIndex = 0,
                AccessibleName = "Pasta de instalação do Project Wingman" };
            Controls.Add(directory);

            browse = new FolderButton { Location = new Point(752, 216), Size = new Size(42, 36), TabIndex = 1,
                BackColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                AccessibleName = "Procurar a pasta do jogo", UseVisualStyleBackColor = false };
            browse.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            browse.Click += BrowseClicked;
            Controls.Add(browse);

            status = MakeLabel("", 28, 433, 759, 61, new Font("Segoe UI", 10F), Color.White);
            Controls.Add(status);
            progress = new ProgressTrack { Location = new Point(28, 504), Size = new Size(766, 7), Visible = false, AccessibleName = "Progresso da instalação" };
            Controls.Add(progress);

            Label credit = MakeLabel("Tradução por Carreta", 28, 534, 475, 42,
                new Font("Segoe UI", 9F), Color.FromArgb(217, 223, 230));
            credit.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(credit);

            install = MakeButton("Instalar tradução", new Rectangle(517, 534, 170, 42), Color.FromArgb(59, 96, 119), 2);
            install.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            install.FlatAppearance.BorderColor = Color.FromArgb(150, 180, 198);
            install.FlatAppearance.MouseOverBackColor = Color.FromArgb(76, 118, 145);
            install.FlatAppearance.MouseDownBackColor = Color.FromArgb(45, 76, 96);
            install.Click += InstallClicked;
            Controls.Add(install);
            close = MakeButton("Fechar", new Rectangle(697, 534, 98, 42), Color.FromArgb(45, 39, 39), 3);
            close.Click += delegate { Close(); };
            Controls.Add(close);
            AcceptButton = install;
            CancelButton = close;

            tips = new ToolTip();
            tips.SetToolTip(browse, "Selecionar a pasta do Project Wingman");
            tips.SetToolTip(directory, "Selecione a pasta principal do jogo ou a pasta interna ProjectWingman.");

            worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                using (Package package = new Package(Application.ExecutablePath))
                    InstallerEngine.Install(package, (string)e.Argument,
                        delegate(InstallProgress p) { worker.ReportProgress(p.Percent, p); }, null);
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                InstallProgress info = (InstallProgress)e.UserState;
                progress.Value = info.Percent;
                status.Text = info.Message;
            };
            worker.RunWorkerCompleted += InstallationFinished;
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!installing) return;
                e.Cancel = true;
                MessageBox.Show(this, "Aguarde a conclusão da instalação antes de fechar.", "Instalação em andamento", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Shown += FormShown;
        }

        private static Image LoadImage(string name)
        {
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (Image original = Image.FromStream(resource)) return new Bitmap(original);
        }

        private static Label MakeLabel(string text, int x, int y, int width, int height, Font font, Color color)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height),
                BackColor = Color.Transparent, ForeColor = color, Font = font, UseMnemonic = false };
        }

        private static Button MakeButton(string text, Rectangle bounds, Color color, int tab)
        {
            Button button = new Button { Text = text, Bounds = bounds, BackColor = color, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false, Cursor = Cursors.Hand, TabIndex = tab };
            button.FlatAppearance.BorderColor = Color.FromArgb(218, 218, 218);
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            float scale = Math.Max((float)ClientSize.Width / banner.Width, (float)ClientSize.Height / banner.Height);
            float width = banner.Width * scale;
            float height = banner.Height * scale;
            e.Graphics.DrawImage(banner, (ClientSize.Width - width) / 2F, (ClientSize.Height - height) / 2F, width, height);
            using (LinearGradientBrush shade = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(90, 7, 13, 22), Color.FromArgb(225, 5, 8, 13), LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(shade, ClientRectangle);
            e.Graphics.DrawImage(logo, new Rectangle(28, 22, 495, 116));
        }

        private void FormShown(object sender, EventArgs e)
        {
            SendMessage(directory.Handle, 0x1501, new IntPtr(1), @"Ex.: C:\SteamLibrary\steamapps\common\Project Wingman");
            if (preview) directory.Text = @"C:\SteamLibrary\steamapps\common\Project Wingman";
            else if (initial != null) { directory.Text = initial; BeginInvoke(new Action(BeginInstall)); }
            else directory.Text = FindGame();
            ActiveControl = install;
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            try
            {
                string selected = FolderPicker.Show(this, directory.Text);
                if (selected != null) directory.Text = selected;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "Não foi possível abrir o seletor de pastas.\n" + error.Message,
                    "Selecionar a pasta do jogo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InstallClicked(object sender, EventArgs e) { BeginInstall(); }

        private void BeginInstall()
        {
            if (installing) return;
            try
            {
                string root = InstallerEngine.ResolveGameRoot(directory.Text);
                directory.Text = root;
                InstallerEngine.AssertWritable(root);
                using (Package package = new Package(Application.ExecutablePath)) { }
                installing = true;
                directory.Enabled = false;
                browse.Enabled = false;
                install.Enabled = false;
                close.Enabled = false;
                progress.Value = 0;
                progress.Visible = true;
                status.ForeColor = Color.White;
                status.Text = "Preparando a instalação...";
                worker.RunWorkerAsync(root);
            }
            catch (UnauthorizedAccessException) { RelaunchElevated(); }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Não foi possível iniciar a instalação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InstallationFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            installing = false;
            close.Enabled = true;
            if (e.Error == null)
            {
                progress.Value = 100;
                status.ForeColor = Color.FromArgb(169, 238, 190);
                status.Text = "Tradução instalada com sucesso!\nInicie o jogo com o idioma configurado para inglês.";
                install.Text = "Instalada";
                ActiveControl = close;
            }
            else
            {
                directory.Enabled = true;
                browse.Enabled = true;
                install.Enabled = true;
                progress.Visible = false;
                status.ForeColor = Color.FromArgb(255, 204, 182);
                status.Text = "A instalação não foi concluída.";
                if (e.Error is UnauthorizedAccessException) RelaunchElevated();
                else MessageBox.Show(this, e.Error.Message, "Falha na instalação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RelaunchElevated()
        {
            bool admin;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            if (admin)
            {
                MessageBox.Show(this, "O Windows bloqueou o acesso a um arquivo. Verifique as permissões da pasta e feche o jogo antes de tentar novamente.",
                    "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(directory.Text));
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--install-target-base64 " + encoded)
                    { UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath) });
                Close();
            }
            catch (Win32Exception error)
            {
                if (error.NativeErrorCode != 1223)
                    MessageBox.Show(this, error.Message, "Não foi possível obter permissão", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                status.Text = "A instalação precisa da permissão de administrador para gravar nesta pasta.";
            }
        }

        private static string FindGame()
        {
            System.Collections.Generic.HashSet<string> libraries = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (key != null && key.GetValue("SteamPath") is string) libraries.Add((string)key.GetValue("SteamPath"));
            }
            catch { }
            libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed) continue;
                libraries.Add(Path.Combine(drive.Name, "SteamLibrary"));
                libraries.Add(Path.Combine(drive.Name, "Games", "SteamLibrary"));
            }
            foreach (string library in new System.Collections.Generic.List<string>(libraries))
            {
                try
                {
                    string vdf = Path.Combine(library, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                    foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                        libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
                }
                catch { }
            }
            foreach (string library in libraries)
            {
                foreach (string name in new[] { "Project Wingman", "ProjectWingman" })
                {
                    string candidate = Path.Combine(library, "steamapps", "common", name);
                    try { return InstallerEngine.ResolveGameRoot(candidate); } catch { }
                }
            }
            return "";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { banner.Dispose(); logo.Dispose(); tips.Dispose(); worker.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
