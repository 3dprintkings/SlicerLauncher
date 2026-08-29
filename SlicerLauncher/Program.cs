using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

namespace SlicerLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}

internal sealed class SlicerEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";

    public override string ToString() => Name;
}

internal sealed class LauncherSettings
{
    public bool ShowWelcomeHelp { get; set; } = true;
    public List<SlicerEntry> Slicers { get; set; } = new();
    public string DefaultSlicerPath { get; set; } = "";
    public bool AutoLaunchDefault { get; set; } = false;
    public int CountdownSeconds { get; set; } = 5;
    public List<string> RecentFiles { get; set; } = new();
}

internal static class ConfigService
{
    private static readonly string ConfigFolder = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlicerLauncher");

    private static readonly string JsonPath = System.IO.Path.Combine(ConfigFolder, "settings.json");
    private static readonly string LegacyXmlPath = System.IO.Path.Combine(ConfigFolder, "config.xml");

    public static LauncherSettings Load()
    {
        Directory.CreateDirectory(ConfigFolder);

        try
        {
            if (File.Exists(JsonPath))
            {
                var settings = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(JsonPath));
                if (settings is not null)
                    return settings;
            }

            if (File.Exists(LegacyXmlPath))
            {
                var migrated = MigrateLegacyXml();
                Save(migrated);
                return migrated;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The configuration could not be loaded.\n\n" + ex.Message,
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        var defaults = new LauncherSettings
        {
            ShowWelcomeHelp = true,
            Slicers = SlicerDetectionService.DetectInstalled()
        };
        Save(defaults);
        return defaults;
    }

    public static void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(ConfigFolder);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(JsonPath, json);
    }

    private static LauncherSettings MigrateLegacyXml()
    {
        var settings = new LauncherSettings { ShowWelcomeHelp = true };
        var doc = XDocument.Load(LegacyXmlPath);
        if (doc.Root is null)
            return settings;

        foreach (var element in doc.Root.Elements("Slicer"))
        {
            var name = (string?)element.Attribute("Name");
            var path = (string?)element.Attribute("Path");
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
                settings.Slicers.Add(new SlicerEntry { Name = name, Path = path });
        }

        return settings;
    }
}

internal static class SlicerDetectionService
{
    private sealed record Candidate(string Name, string[] ExecutableNames, string[] FolderPatterns);

    private static readonly Candidate[] Candidates =
    {
        new("Bambu Studio", new[] { "bambu-studio.exe" }, new[] { "Bambu Studio", "Bambu*" }),
        new("ELEGOO Slicer", new[] { "elegoo-slicer.exe", "ElegooSlicer.exe" }, new[] { "ElegooSlicer", "ELEGOO*", "Elegoo*" }),
        new("OrcaSlicer", new[] { "orca-slicer.exe", "OrcaSlicer.exe" }, new[] { "OrcaSlicer", "Orca*" }),
        new("PrusaSlicer", new[] { "prusa-slicer.exe" }, new[] { "Prusa3D", "PrusaSlicer*" }),
        new("Creality Print", new[] { "CrealityPrint.exe", "Creality Print.exe" }, new[] { "Creality Print*", "Creality*" }),
        new("Flash Studio", new[] { "flash studio.exe" }, new[] { "Flashforge", "Flash Studio*" })
    };

    public static List<SlicerEntry> DetectInstalled()
    {
        var results = new List<SlicerEntry>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Known Flashforge Flash Studio installation path.
        var flashStudioPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Flashforge",
            "Flash Studio Desktop",
            "flash studio.exe");

        if (File.Exists(flashStudioPath))
        {
            var fullFlashStudioPath = System.IO.Path.GetFullPath(flashStudioPath);
            seenPaths.Add(fullFlashStudioPath);
            results.Add(new SlicerEntry { Name = "Flash Studio", Path = fullFlashStudioPath });
        }

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
        }
        .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var candidate in Candidates)
        {
            foreach (var path in FindCandidatePaths(candidate, roots))
            {
                string fullPath;
                try { fullPath = System.IO.Path.GetFullPath(path); }
                catch { continue; }

                if (!seenPaths.Add(fullPath))
                    continue;

                results.Add(new SlicerEntry { Name = candidate.Name, Path = fullPath });
                break; // one installation per slicer is enough for the default list
            }
        }

        return results;
    }

    private static IEnumerable<string> FindCandidatePaths(Candidate candidate, string[] roots)
    {
        foreach (var root in roots)
        {
            // Fast checks for common non-versioned install folders.
            foreach (var folderPattern in candidate.FolderPatterns)
            {
                if (folderPattern.Contains('*'))
                    continue;

                foreach (var exeName in candidate.ExecutableNames)
                {
                    var direct = System.IO.Path.Combine(root, folderPattern, exeName);
                    if (File.Exists(direct))
                        yield return direct;
                }
            }

            // Search only likely vendor/product folders, never the whole drive.
            foreach (var folderPattern in candidate.FolderPatterns)
            {
                IEnumerable<string> topFolders;
                try
                {
                    topFolders = Directory.EnumerateDirectories(root, folderPattern, SearchOption.TopDirectoryOnly).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var folder in topFolders)
                {
                    foreach (var found in FindExecutables(folder, candidate.ExecutableNames, maxDepth: 3))
                        yield return found;
                }
            }
        }
    }

    private static IEnumerable<string> FindExecutables(string folder, string[] executableNames, int maxDepth)
    {
        if (maxDepth < 0 || !Directory.Exists(folder))
            yield break;

        foreach (var exeName in executableNames)
        {
            var direct = System.IO.Path.Combine(folder, exeName);
            if (File.Exists(direct))
                yield return direct;
        }

        if (maxDepth == 0)
            yield break;

        string[] children;
        try { children = Directory.GetDirectories(folder); }
        catch { yield break; }

        foreach (var child in children)
        {
            foreach (var found in FindExecutables(child, executableNames, maxDepth - 1))
                yield return found;
        }
    }
}


internal static class BrandAssets
{
    public static readonly Color Yellow = Color.FromArgb(255, 205, 2);
    public static readonly Color DarkGray = Color.FromArgb(55, 55, 55);
    public static readonly Color MediumGray = Color.FromArgb(105, 105, 105);
    public static readonly Color LightGray = Color.FromArgb(245, 245, 245);
    public static readonly Color BorderGray = Color.FromArgb(215, 215, 215);

    public static Image? LoadEmbeddedImage(string suffix)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (name is null) return null;
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) return null;
            using var temp = Image.FromStream(stream);
            return new Bitmap(temp);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class MainForm : Form
{
    private string? _fileToOpen;
    private LauncherSettings _settings;
    private readonly FlowLayoutPanel _slicerPanel = new();
    private readonly Label _fileLabel = new();
    private readonly Panel _countdownPanel = new();
    private readonly Label _countdownLabel = new();
    private readonly Panel _countdownProgress = new();
    private readonly Panel _countdownProgressFill = new();
    private int _countdownMaximumTenths = 1;
    private readonly Button _stopCountdownButton = new();
    private readonly System.Windows.Forms.Timer _countdownTimer = new();
    private int _countdownTenths;
    private SlicerEntry? _countdownSlicer;
    private readonly Label _versionLabel = new();
    private readonly LinkLabel _aboutLink = new();
    private readonly LinkLabel _helpLink = new();

    public MainForm(string? fileToOpen)
    {
        _fileToOpen = NormalizeFile(fileToOpen);
        _settings = ConfigService.Load();
        if (!string.IsNullOrWhiteSpace(_fileToOpen))
            AddRecentFile(_fileToOpen);

        InitializeForm();
        RenderSlicerButtons();

        Shown += (_, _) =>
        {
            if (_settings.ShowWelcomeHelp || _settings.Slicers.Count == 0)
            {
                using var help = new HelpForm();
                help.ShowDialog(this);
                _settings.ShowWelcomeHelp = false;
                ConfigService.Save(_settings);
            }
            StartCountdownIfConfigured();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countdownTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private static string? NormalizeFile(string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        try { return System.IO.Path.GetFullPath(file); }
        catch { return file; }
    }

    private void AddRecentFile(string file)
    {
        if (!File.Exists(file)) return;
        _settings.RecentFiles ??= new List<string>();
        _settings.RecentFiles.RemoveAll(p => string.Equals(p, file, StringComparison.OrdinalIgnoreCase));
        _settings.RecentFiles.Insert(0, file);
        if (_settings.RecentFiles.Count > 10)
            _settings.RecentFiles.RemoveRange(10, _settings.RecentFiles.Count - 10);
        ConfigService.Save(_settings);
    }

    private void InitializeForm()
    {
        Text = "SlicerLauncher";
        Width = 900;
        Height = 690;
        MinimumSize = new Size(700, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = BrandAssets.DarkGray };
        var title = new Label
        {
            Text = "SlicerLauncher",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(35, 24)
        };

        _versionLabel.Text = "v1.1.0";
        _versionLabel.AutoSize = true;
        _versionLabel.ForeColor = Color.Gainsboro;
        _versionLabel.Font = new Font("Segoe UI", 8.5F);
        _versionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _aboutLink.Text = "About";
        _aboutLink.AutoSize = true;
        _aboutLink.LinkColor = Color.Gainsboro;
        _aboutLink.ActiveLinkColor = BrandAssets.Yellow;
        _aboutLink.VisitedLinkColor = Color.Gainsboro;
        _aboutLink.Font = new Font("Segoe UI", 8.5F);
        _aboutLink.Cursor = Cursors.Hand;
        _aboutLink.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _aboutLink.LinkClicked += (_, _) =>
        {
            StopCountdown(false);
            using var about = new AboutForm();
            about.ShowDialog(this);
        };

        _helpLink.Text = "How to use in Fusion 360";
        _helpLink.AutoSize = true;
        _helpLink.LinkColor = Color.Gainsboro;
        _helpLink.ActiveLinkColor = BrandAssets.Yellow;
        _helpLink.VisitedLinkColor = Color.Gainsboro;
        _helpLink.Font = new Font("Segoe UI", 8.5F);
        _helpLink.Cursor = Cursors.Hand;
        _helpLink.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _helpLink.LinkClicked += (_, _) =>
        {
            StopCountdown(false);
            using var help = new HelpForm();
            help.ShowDialog(this);
        };

        header.Controls.Add(title);
        header.Controls.Add(_versionLabel);
        header.Controls.Add(_aboutLink);
        header.Controls.Add(_helpLink);
        header.Resize += (_, _) => PositionHeaderLinks();
        PositionHeaderLinks();

        _fileLabel.Dock = DockStyle.Top;
        _fileLabel.Height = 58;
        _fileLabel.Padding = new Padding(35, 18, 35, 5);
        _fileLabel.ForeColor = BrandAssets.MediumGray;
        _fileLabel.Font = new Font("Segoe UI", 9.5F);
        UpdateFileLabel();

        _countdownPanel.Dock = DockStyle.Top;
        _countdownPanel.Height = 74;
        _countdownPanel.Padding = new Padding(35, 8, 35, 8);
        _countdownPanel.BackColor = Color.White;
        _countdownPanel.Visible = false;

        _countdownLabel.AutoSize = true;
        _countdownLabel.Location = new Point(35, 9);
        _countdownLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _countdownLabel.ForeColor = BrandAssets.DarkGray;

        _countdownProgress.Location = new Point(35, 39);
        _countdownProgress.Height = 10;
        _countdownProgress.Width = 650;
        _countdownProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _countdownProgress.BackColor = BrandAssets.BorderGray;
        _countdownProgress.Padding = new Padding(1);

        _countdownProgressFill.Dock = DockStyle.Left;
        _countdownProgressFill.Width = _countdownProgress.ClientSize.Width - _countdownProgress.Padding.Horizontal;
        _countdownProgressFill.BackColor = BrandAssets.Yellow;
        _countdownProgress.Controls.Add(_countdownProgressFill);

        _stopCountdownButton.Text = "Stop";
        _stopCountdownButton.Size = new Size(100, 34);
        _stopCountdownButton.BackColor = BrandAssets.DarkGray;
        _stopCountdownButton.ForeColor = Color.White;
        _stopCountdownButton.FlatStyle = FlatStyle.Flat;
        _stopCountdownButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _stopCountdownButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _stopCountdownButton.FlatAppearance.BorderSize = 0;
        _stopCountdownButton.Click += (_, _) => StopCountdown(true);
        _countdownPanel.Controls.Add(_countdownLabel);
        _countdownPanel.Controls.Add(_countdownProgress);
        _countdownPanel.Controls.Add(_stopCountdownButton);
        _countdownPanel.Resize += (_, _) =>
        {
            _stopCountdownButton.Left = _countdownPanel.ClientSize.Width - _stopCountdownButton.Width - 35;
            _stopCountdownButton.Top = 19;
            _countdownProgress.Width = Math.Max(120, _stopCountdownButton.Left - 55);
            UpdateCountdownProgress();
        };

        _countdownTimer.Interval = 100;
        _countdownTimer.Tick += (_, _) => CountdownTick();

        _slicerPanel.Dock = DockStyle.Fill;
        _slicerPanel.Padding = new Padding(24, 10, 24, 20);
        _slicerPanel.AutoScroll = true;
        _slicerPanel.WrapContents = true;
        _slicerPanel.FlowDirection = FlowDirection.LeftToRight;
        _slicerPanel.BackColor = BrandAssets.LightGray;
        _slicerPanel.Resize += (_, _) => RenderSlicerButtons();

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 92, BackColor = Color.White };
        footer.Paint += (_, e) => e.Graphics.DrawLine(Pens.LightGray, 0, 0, footer.Width, 0);

        var recentButton = CreateFooterButton("Recent Files");
        recentButton.Click += (_, _) => { StopCountdown(false); OpenRecentFiles(); };

        var manageButton = new Button
        {
            Text = "Manage Slicers",
            Size = new Size(130, 42),
            BackColor = BrandAssets.DarkGray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = true,
            AccessibleName = "Manage slicers"
        };
        manageButton.FlatAppearance.BorderSize = 0;
        manageButton.Click += (_, _) =>
        {
            StopCountdown(false);
            OpenManageSlicers();
        };

        footer.Controls.Add(recentButton);
        footer.Controls.Add(manageButton);
        footer.Resize += (_, _) =>
        {
            recentButton.Left = Math.Max(0, (footer.ClientSize.Width - recentButton.Width) / 2);
            recentButton.Top = 24;

            manageButton.Left = footer.ClientSize.Width - manageButton.Width - 18;
            manageButton.Top = 24;
        };

        Controls.Add(_slicerPanel);
        Controls.Add(_countdownPanel);
        Controls.Add(_fileLabel);
        Controls.Add(header);
        Controls.Add(footer);
    }

    private void PositionHeaderLinks()
    {
        if (_versionLabel.Parent is null) return;

        var right = _versionLabel.Parent.ClientSize.Width - 18;

        _versionLabel.Left = right - _versionLabel.Width;
        _versionLabel.Top = 10;

        _aboutLink.Left = right - _aboutLink.Width;
        _aboutLink.Top = 33;

        _helpLink.Left = right - _helpLink.Width;
        _helpLink.Top = 56;
    }

    private Button CreateFooterButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 120,
            Height = 42,
            Location = new Point(0, 24),
            BackColor = BrandAssets.DarkGray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        }.Also(b => b.FlatAppearance.BorderSize = 0);
    }

    private void UpdateFileLabel()
    {
        _fileLabel.Text = !string.IsNullOrWhiteSpace(_fileToOpen)
            ? "File: " + System.IO.Path.GetFileName(_fileToOpen)
            : "No file received. The selected slicer will start normally.";
    }

    private void RenderSlicerButtons()
    {
        if (_slicerPanel.IsDisposed) return;
        _slicerPanel.SuspendLayout();
        _slicerPanel.Controls.Clear();

        if (_settings.Slicers.Count == 0)
        {
            _slicerPanel.Controls.Add(new Label
            {
                Text = "No slicers configured.\r\n\r\nUse the edit icon above to add your first slicer.",
                ForeColor = BrandAssets.MediumGray,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Margin = new Padding(15)
            });
            _slicerPanel.ResumeLayout();
            return;
        }

        int count = _settings.Slicers.Count;
        int available = Math.Max(400, _slicerPanel.ClientSize.Width - _slicerPanel.Padding.Horizontal);
        int columns;
        int height;
        float fontSize;

        if (count <= 4)
        {
            columns = available >= 720 ? 2 : 1;
            height = 92;
            fontSize = 13F;
        }
        else if (count <= 8)
        {
            columns = available >= 760 ? 3 : 2;
            height = 72;
            fontSize = 11.5F;
        }
        else
        {
            columns = available >= 850 ? 4 : available >= 620 ? 3 : 2;
            height = 58;
            fontSize = 10F;
        }

        int margin = count <= 4 ? 12 : 8;
        int width = Math.Max(150, (available / columns) - (margin * 2));

        foreach (var slicer in _settings.Slicers)
        {
            var button = new Button
            {
                Text = slicer.Name,
                Width = width,
                Height = height,
                Margin = new Padding(margin),
                Padding = new Padding(12, 0, 12, 0),
                BackColor = BrandAssets.Yellow,
                ForeColor = BrandAssets.DarkGray,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Tag = slicer
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => LaunchSlicer((SlicerEntry)button.Tag!);
            _slicerPanel.Controls.Add(button);
        }

        _slicerPanel.ResumeLayout();
    }

    private void OpenManageSlicers()
    {
        using var manager = new ManageSlicersForm(_settings);
        if (manager.ShowDialog(this) != DialogResult.OK)
            return;

        _settings = ConfigService.Load();
        RenderSlicerButtons();
        StartCountdownIfConfigured();
    }

    private void OpenRecentFiles()
    {
        _settings.RecentFiles ??= new List<string>();
        _settings.RecentFiles = _settings.RecentFiles.Where(File.Exists).Take(10).ToList();
        ConfigService.Save(_settings);

        using var recent = new RecentFilesForm(_settings.RecentFiles);
        var result = recent.ShowDialog(this);

        if (recent.Cleared)
        {
            _settings.RecentFiles.Clear();
            ConfigService.Save(_settings);
        }

        if (result != DialogResult.OK || string.IsNullOrWhiteSpace(recent.SelectedFile))
            return;

        _fileToOpen = recent.SelectedFile;
        AddRecentFile(_fileToOpen);
        UpdateFileLabel();
        StartCountdownIfConfigured();
    }

    private SlicerEntry? GetDefaultSlicer()
    {
        if (string.IsNullOrWhiteSpace(_settings.DefaultSlicerPath)) return null;
        return _settings.Slicers.FirstOrDefault(s =>
            string.Equals(s.Path, _settings.DefaultSlicerPath, StringComparison.OrdinalIgnoreCase));
    }

    private void StartCountdownIfConfigured()
    {
        StopCountdown(false);
        if (string.IsNullOrWhiteSpace(_fileToOpen) || !File.Exists(_fileToOpen)) return;
        if (!_settings.AutoLaunchDefault) return;

        var slicer = GetDefaultSlicer();
        if (slicer is null || !File.Exists(slicer.Path)) return;

        int seconds = Math.Clamp(_settings.CountdownSeconds, 1, 30);
        _countdownSlicer = slicer;
        _countdownTenths = seconds * 10;
        _countdownMaximumTenths = seconds * 10;
        UpdateCountdownProgress();
        UpdateCountdownText();
        _countdownPanel.Visible = true;
        _countdownTimer.Start();
    }

    private void CountdownTick()
    {
        _countdownTenths--;
        if (_countdownTenths <= 0)
        {
            _countdownTimer.Stop();
            _countdownProgressFill.Width = 0;
            var slicer = _countdownSlicer;
            _countdownSlicer = null;
            _countdownPanel.Visible = false;
            if (slicer is not null) LaunchSlicer(slicer);
            return;
        }

        UpdateCountdownProgress();
        UpdateCountdownText();
    }

    private void UpdateCountdownProgress()
    {
        int availableWidth = Math.Max(0, _countdownProgress.ClientSize.Width - _countdownProgress.Padding.Horizontal);
        double ratio = _countdownMaximumTenths <= 0 ? 0 : Math.Clamp(_countdownTenths / (double)_countdownMaximumTenths, 0, 1);
        _countdownProgressFill.Width = (int)Math.Round(availableWidth * ratio);
        _countdownProgressFill.Height = Math.Max(0, _countdownProgress.ClientSize.Height - _countdownProgress.Padding.Vertical);
    }

    private void UpdateCountdownText()
    {
        var seconds = (int)Math.Ceiling(_countdownTenths / 10.0);
        _countdownLabel.Text = $"Opening with {_countdownSlicer?.Name} in {seconds} second{(seconds == 1 ? "" : "s")}...";
    }

    private void StopCountdown(bool showStopped)
    {
        var wasRunning = _countdownTimer.Enabled;
        _countdownTimer.Stop();
        _countdownSlicer = null;
        _countdownPanel.Visible = false;
        if (showStopped && wasRunning)
            _fileLabel.Text = "Countdown stopped. Choose the slicer you want to use.";
    }

    private void LaunchSlicer(SlicerEntry slicer)
    {
        StopCountdown(false);
        if (!File.Exists(slicer.Path))
        {
            var result = MessageBox.Show(
                "The slicer could not be found:\r\n\r\n" + slicer.Path + "\r\n\r\nOpen Manage Slicers?",
                "Slicer Not Found",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Yes) OpenManageSlicers();
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = slicer.Path,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(_fileToOpen))
                psi.ArgumentList.Add(_fileToOpen);

            Process.Start(psi);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("The slicer could not be started.\r\n\r\n" + ex.Message,
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class RecentFilesForm : Form
{
    private readonly ListBox _list = new();
    public string? SelectedFile { get; private set; }
    public bool Cleared { get; private set; }

    public RecentFilesForm(IEnumerable<string> files)
    {
        Text = "Recent Files";
        Width = 720;
        Height = 460;
        MinimumSize = new Size(600, 380);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = BrandAssets.DarkGray };
        header.Controls.Add(new Label
        {
            Text = "RECENT FILES",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(28, 22)
        });

        _list.Dock = DockStyle.Fill;
        _list.Margin = new Padding(30);
        _list.HorizontalScrollbar = true;
        foreach (var file in files.Where(File.Exists)) _list.Items.Add(file);
        _list.DoubleClick += (_, _) => Choose();

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30) };
        content.Controls.Add(_list);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 82, BackColor = Color.White };

        var clearButton = new Button
        {
            Text = "Clear List",
            Size = new Size(120, 42),
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        clearButton.FlatAppearance.BorderSize = 1;
        clearButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        clearButton.Click += (_, _) =>
        {
            if (_list.Items.Count == 0) return;
            var result = MessageBox.Show(
                "Clear all recent files?",
                "Clear Recent Files",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            _list.Items.Clear();
            SelectedFile = null;
            Cleared = true;
        };

        var useButton = new Button
        {
            Text = "Use File",
            Size = new Size(120, 42),
            BackColor = BrandAssets.Yellow,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        useButton.FlatAppearance.BorderSize = 0;
        useButton.Click += (_, _) => Choose();
        footer.Controls.Add(clearButton);
        footer.Controls.Add(useButton);
        footer.Resize += (_, _) =>
        {
            clearButton.Left = 30;
            clearButton.Top = 20;
            useButton.Left = footer.ClientSize.Width - useButton.Width - 30;
            useButton.Top = 20;
        };

        Controls.Add(content);
        Controls.Add(header);
        Controls.Add(footer);
    }

    private void Choose()
    {
        if (_list.SelectedItem is not string file) return;
        SelectedFile = file;
        DialogResult = DialogResult.OK;
        Close();
    }
}

internal sealed class ManageSlicersForm : Form
{
    private readonly LauncherSettings _settings;
    private readonly ListBox _list = new();
    private readonly CheckBox _autoLaunch = new();
    private readonly NumericUpDown _countdownSeconds = new();
    private readonly Button _editButton = new();
    private readonly Label _launchPrefix = new();

    private int _dragIndex = -1;

    public ManageSlicersForm(LauncherSettings current)
    {
        _settings = new LauncherSettings
        {
            ShowWelcomeHelp = current.ShowWelcomeHelp,
            Slicers = current.Slicers
                .Select(s => new SlicerEntry { Name = s.Name, Path = s.Path })
                .ToList(),
            DefaultSlicerPath = current.DefaultSlicerPath,
            AutoLaunchDefault = current.AutoLaunchDefault,
            CountdownSeconds = current.CountdownSeconds,
            RecentFiles = (current.RecentFiles ?? new List<string>()).ToList()
        };

        MigrateDefaultToTop();
        SyncDefaultFromOrder();
        InitializeForm();
        RefreshList();

        if (_settings.Slicers.Count > 0)
            _list.SelectedIndex = 0;
    }

    private void InitializeForm()
    {
        Text = "SlicerLauncher - Manage Slicers";
        Width = 900;
        Height = 690;
        MinimumSize = new Size(760, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 88,
            BackColor = BrandAssets.DarkGray
        };

        header.Controls.Add(new Label
        {
            Text = "Manage Slicers",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 24)
        });

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 90,
            BackColor = Color.White
        };
        footer.Paint += (_, e) => e.Graphics.DrawLine(Pens.LightGray, 0, 0, footer.Width, 0);

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(110, 42),
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };
        cancelButton.FlatAppearance.BorderSize = 1;
        cancelButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;

        var saveAndCloseButton = new Button
        {
            Text = "Save && Close",
            Size = new Size(140, 42),
            BackColor = BrandAssets.Yellow,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        saveAndCloseButton.FlatAppearance.BorderSize = 0;
        saveAndCloseButton.Click += (_, _) => SaveAndClose();

        footer.Controls.Add(cancelButton);
        footer.Controls.Add(saveAndCloseButton);
        footer.Resize += (_, _) =>
        {
            saveAndCloseButton.Left = footer.ClientSize.Width - saveAndCloseButton.Width - 30;
            saveAndCloseButton.Top = 23;
            cancelButton.Left = saveAndCloseButton.Left - cancelButton.Width - 12;
            cancelButton.Top = 23;
        };

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BrandAssets.LightGray,
            Padding = new Padding(38, 24, 38, 24)
        };

        // LEFT: slicer list uses the full available content height.
        var leftPanel = new Panel
        {
            BackColor = BrandAssets.LightGray
        };

        var title = new Label
        {
            Text = "Your Slicers",
            AutoSize = true,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = BrandAssets.DarkGray,
            Location = new Point(0, 0)
        };

        _list.Location = new Point(0, 38);
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = 51;
        _list.BorderStyle = BorderStyle.FixedSingle;
        _list.BackColor = Color.White;
        _list.AllowDrop = true;
        _list.Cursor = Cursors.Hand;
        _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        _list.DrawItem += DrawSlicerItem;
        _list.SelectedIndexChanged += (_, _) => _editButton.Enabled = _list.SelectedIndex >= 0;
        _list.DoubleClick += (_, _) => EditSelected();
        _list.MouseDown += (_, e) =>
        {
            _dragIndex = _list.IndexFromPoint(e.Location);
            if (_dragIndex >= 0)
                _list.SelectedIndex = _dragIndex;
        };
        _list.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || _dragIndex < 0)
                return;

            _list.DoDragDrop(_dragIndex, DragDropEffects.Move);
        };
        _list.DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(typeof(int)) == true)
                e.Effect = DragDropEffects.Move;
        };
        _list.DragOver += (_, e) =>
        {
            if (e.Data?.GetDataPresent(typeof(int)) != true)
                return;

            var clientPoint = _list.PointToClient(new Point(e.X, e.Y));
            var index = _list.IndexFromPoint(clientPoint);

            if (index < 0 && _settings.Slicers.Count > 0)
                index = _settings.Slicers.Count - 1;

            if (index >= 0)
                _list.SelectedIndex = index;

            e.Effect = DragDropEffects.Move;
        };
        _list.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(typeof(int)) is not int fromIndex)
                return;

            var clientPoint = _list.PointToClient(new Point(e.X, e.Y));
            var toIndex = _list.IndexFromPoint(clientPoint);

            if (toIndex < 0)
                toIndex = _settings.Slicers.Count - 1;

            MoveSlicer(fromIndex, toIndex);
            _dragIndex = -1;
        };

        leftPanel.Controls.Add(title);
        leftPanel.Controls.Add(_list);

        var dragHint = new Label
        {
            Text = "Drag to reorder. The first Slicer is your default, for automatic launch if desired.",
            AutoSize = true,
            ForeColor = BrandAssets.MediumGray,
            Font = new Font("Segoe UI", 8.5F)
        };
        leftPanel.Controls.Add(dragHint);


        // RIGHT: actions on top, Automatic Launch below on two rows.
        var rightPanel = new Panel
        {
            BackColor = BrandAssets.LightGray
        };

        var actionsLabel = new Label
        {
            Text = "Actions",
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = BrandAssets.DarkGray,
            Location = new Point(0, 0)
        };

        var addButton = new Button
        {
            Text = "Add Slicer",
            Height = 42,
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        addButton.FlatAppearance.BorderSize = 1;
        addButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        addButton.Click += (_, _) => AddSlicer();

        _editButton.Text = "Edit Selected";
        _editButton.Height = 42;
        _editButton.BackColor = Color.White;
        _editButton.ForeColor = BrandAssets.DarkGray;
        _editButton.FlatStyle = FlatStyle.Flat;
        _editButton.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _editButton.Cursor = Cursors.Hand;
        _editButton.Enabled = false;
        _editButton.FlatAppearance.BorderSize = 1;
        _editButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        _editButton.Click += (_, _) => EditSelected();

        var scanButton = new Button
        {
            Text = "Scan Installed Slicers",
            Height = 42,
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        scanButton.FlatAppearance.BorderSize = 1;
        scanButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        scanButton.Click += (_, _) => ScanInstalled();

        var removeButton = new Button
        {
            Text = "Remove Selected",
            Height = 42,
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        removeButton.FlatAppearance.BorderSize = 1;
        removeButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        removeButton.Click += (_, _) => RemoveSelected();

        var autoPanel = new Panel
        {
            BackColor = Color.White
        };
        autoPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(BrandAssets.BorderGray);
            e.Graphics.DrawRectangle(pen, 0, 0, autoPanel.ClientSize.Width - 1, autoPanel.ClientSize.Height - 1);
        };

        _autoLaunch.Text = "Launch Default Slicer";
        _autoLaunch.AutoSize = true;
        _autoLaunch.Checked = _settings.AutoLaunchDefault;
        _autoLaunch.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);

        var autoTitle = new Label
        {
            Text = "Automatic Launch",
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = BrandAssets.DarkGray
        };

        _launchPrefix.AutoSize = true;
        _launchPrefix.ForeColor = BrandAssets.DarkGray;
        _launchPrefix.Font = new Font("Segoe UI", 9.5F);

        var launchSuffix = new Label
        {
            Text = "Seconds",
            AutoSize = true,
            ForeColor = BrandAssets.DarkGray,
            Font = new Font("Segoe UI", 9.5F)
        };

        _countdownSeconds.Minimum = 1;
        _countdownSeconds.Maximum = 30;
        _countdownSeconds.Value = Math.Clamp(_settings.CountdownSeconds, 1, 30);
        _countdownSeconds.Size = new Size(70, 28);
        _countdownSeconds.TextAlign = HorizontalAlignment.Center;

        void UpdateLaunchText()
        {
            _launchPrefix.Text = "Launch after";
        }

        UpdateLaunchText();

        autoPanel.Controls.Add(autoTitle);
        autoPanel.Controls.Add(_autoLaunch);
        autoPanel.Controls.Add(_launchPrefix);
        autoPanel.Controls.Add(_countdownSeconds);
        autoPanel.Controls.Add(launchSuffix);

        rightPanel.Controls.Add(actionsLabel);
        rightPanel.Controls.Add(scanButton);
        rightPanel.Controls.Add(addButton);
        rightPanel.Controls.Add(_editButton);
        rightPanel.Controls.Add(removeButton);
        rightPanel.Controls.Add(autoTitle);
        rightPanel.Controls.Add(autoPanel);

        content.Controls.Add(leftPanel);
        content.Controls.Add(rightPanel);

        content.Resize += (_, _) =>
        {
            const int gap = 28;
            const int sidePadding = 38;

            int usableWidth = Math.Max(650, content.ClientSize.Width - (sidePadding * 2));
            int usableHeight = Math.Max(360, content.ClientSize.Height - 48);

            int rightWidth = Math.Clamp((int)(usableWidth * 0.36), 270, 320);
            int leftWidth = usableWidth - rightWidth - gap;

            leftPanel.SetBounds(sidePadding, 24, leftWidth, usableHeight);
            rightPanel.SetBounds(sidePadding + leftWidth + gap, 24, rightWidth, usableHeight);

            int hintHeight = Math.Max(18, dragHint.PreferredHeight);
            const int hintGap = 4;
            int hintY = Math.Max(42, leftPanel.ClientSize.Height - hintHeight - 7);
            dragHint.Location = new Point(0, hintY + 10);
            _list.SetBounds(
                0,
                38,
                leftPanel.ClientSize.Width,
                Math.Max(280, dragHint.Top - hintGap - 38));

            int buttonWidth = rightPanel.ClientSize.Width;
            int y = 34;
            scanButton.SetBounds(0, y, buttonWidth, 42);
            y += 50;
            addButton.SetBounds(0, y, buttonWidth, 42);
            y += 50;
            _editButton.SetBounds(0, y, buttonWidth, 42);
            y += 50;
            removeButton.SetBounds(0, y, buttonWidth, 42);

            // Add breathing room between actions and Automatic Launch.
            y += 82;

            autoTitle.Location = new Point(0, y - 30);
            autoPanel.SetBounds(0, y, buttonWidth, 131);

            // Clean two-row layout.
            _autoLaunch.Location = new Point(16, 18);

            int sentenceY = 68;
            _launchPrefix.Location = new Point(16, sentenceY + 5);

            int numberX = Math.Min(
                buttonWidth - 95,
                Math.Max(16, _launchPrefix.Right + 10));

            _countdownSeconds.Location = new Point(numberX, sentenceY);
            launchSuffix.Location = new Point(_countdownSeconds.Right + 10, sentenceY + 5);
        };

        // Trigger initial layout once all controls exist.
        content.PerformLayout();
        content.Width += 1;
        content.Width -= 1;

        AcceptButton = saveAndCloseButton;
        CancelButton = cancelButton;

        Controls.Add(content);
        Controls.Add(header);
        Controls.Add(footer);
    }

    private void DrawSlicerItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _settings.Slicers.Count)
            return;

        var slicer = _settings.Slicers[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = e.Bounds;

        // Deliberately avoid the Windows default selection colour (blue).
        using var backgroundBrush = new SolidBrush(selected
            ? Color.FromArgb(255, 249, 224)
            : Color.White);
        e.Graphics.FillRectangle(backgroundBrush, bounds);

        if (selected)
        {
            using var selectionPen = new Pen(BrandAssets.Yellow, 2F);
            e.Graphics.DrawRectangle(selectionPen, bounds.Left + 1, bounds.Top + 1, bounds.Width - 3, bounds.Height - 3);
        }
        else if (e.Index < _settings.Slicers.Count - 1)
        {
            using var separatorPen = new Pen(Color.FromArgb(235, 235, 235));
            e.Graphics.DrawLine(separatorPen, bounds.Left + 42, bounds.Bottom - 1, bounds.Right - 12, bounds.Bottom - 1);
        }

        var starArea = new Rectangle(bounds.Left + 12, bounds.Top, 26, bounds.Height);
        var nameArea = new Rectangle(bounds.Left + 42, bounds.Top, bounds.Width - 105, bounds.Height);
        var handleArea = new Rectangle(bounds.Right - 44, bounds.Top, 30, bounds.Height);

        using var starFont = new Font("Segoe UI Symbol", 13F, FontStyle.Bold);
        using var nameFont = new Font("Segoe UI", 10F, e.Index == 0 ? FontStyle.Bold : FontStyle.Regular);
        using var handleFont = new Font("Segoe UI Symbol", 14F, FontStyle.Bold);

        if (e.Index == 0)
            TextRenderer.DrawText(e.Graphics, "★", starFont, starArea, BrandAssets.Yellow, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        var displayName = e.Index == 0 ? $"{slicer.Name}   Default" : slicer.Name;
        TextRenderer.DrawText(e.Graphics, displayName, nameFont, nameArea, BrandAssets.DarkGray,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, "≡", handleFont, handleArea, BrandAssets.MediumGray,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }

    private void UpdateAutomaticLaunchLabel()
    {
        _launchPrefix.Text = "Launch after";
    }

    private void RefreshList()
    {
        UpdateAutomaticLaunchLabel();
        var selectedIndex = _list.SelectedIndex;
        _list.Items.Clear();

        foreach (var slicer in _settings.Slicers)
            _list.Items.Add(slicer.Name);

        if (_settings.Slicers.Count > 0)
            _list.SelectedIndex = Math.Clamp(selectedIndex, 0, _settings.Slicers.Count - 1);

        _autoLaunch.Text = "Launch Default Slicer";
        _autoLaunch.Enabled = _settings.Slicers.Count > 0;

        _list.Invalidate();
    }

    private void MigrateDefaultToTop()
    {
        if (_settings.Slicers.Count <= 1 || string.IsNullOrWhiteSpace(_settings.DefaultSlicerPath))
            return;

        var index = _settings.Slicers.FindIndex(s =>
            string.Equals(s.Path, _settings.DefaultSlicerPath, StringComparison.OrdinalIgnoreCase));

        if (index <= 0)
            return;

        var defaultSlicer = _settings.Slicers[index];
        _settings.Slicers.RemoveAt(index);
        _settings.Slicers.Insert(0, defaultSlicer);
    }

    private void SyncDefaultFromOrder()
    {
        _settings.DefaultSlicerPath = _settings.Slicers.Count > 0
            ? _settings.Slicers[0].Path
            : "";

        if (_settings.Slicers.Count == 0)
            _settings.AutoLaunchDefault = false;
    }

    private void MoveSlicer(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _settings.Slicers.Count ||
            toIndex < 0 || toIndex >= _settings.Slicers.Count ||
            fromIndex == toIndex)
            return;

        var slicer = _settings.Slicers[fromIndex];
        _settings.Slicers.RemoveAt(fromIndex);

        toIndex = Math.Clamp(toIndex, 0, _settings.Slicers.Count);
        _settings.Slicers.Insert(toIndex, slicer);

        SyncDefaultFromOrder();
        RefreshList();
        _list.SelectedIndex = toIndex;
    }

    private void AddSlicer()
    {
        using var dialog = new EditSlicerForm(_settings.Slicers);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _settings.Slicers.Add(new SlicerEntry
        {
            Name = dialog.SlicerName,
            Path = dialog.SlicerPath
        });

        SyncDefaultFromOrder();
        RefreshList();
        _list.SelectedIndex = _settings.Slicers.Count - 1;
    }

    private void EditSelected()
    {
        var index = _list.SelectedIndex;
        if (index < 0 || index >= _settings.Slicers.Count)
            return;

        var slicer = _settings.Slicers[index];

        using var dialog = new EditSlicerForm(_settings.Slicers, slicer);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        slicer.Name = dialog.SlicerName;
        slicer.Path = dialog.SlicerPath;

        SyncDefaultFromOrder();
        RefreshList();
        _list.SelectedIndex = index;
    }

    private void RemoveSelected()
    {
        var index = _list.SelectedIndex;
        if (index < 0 || index >= _settings.Slicers.Count)
            return;

        var slicer = _settings.Slicers[index];
        var result = MessageBox.Show(
            $"Remove \"{slicer.Name}\"?",
            "Remove Slicer",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _settings.Slicers.RemoveAt(index);
        SyncDefaultFromOrder();

        if (_settings.Slicers.Count == 0)
            _autoLaunch.Checked = false;

        RefreshList();
    }

    private void ScanInstalled()
    {
        var detected = SlicerDetectionService.DetectInstalled();
        var existingPaths = new HashSet<string>(
            _settings.Slicers.Select(s => s.Path),
            StringComparer.OrdinalIgnoreCase);
        var existingNames = new HashSet<string>(
            _settings.Slicers.Select(s => s.Name.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;

        foreach (var slicer in detected)
        {
            if (existingNames.Contains(slicer.Name.Trim()) || !existingPaths.Add(slicer.Path))
                continue;

            existingNames.Add(slicer.Name.Trim());
            _settings.Slicers.Add(new SlicerEntry
            {
                Name = slicer.Name,
                Path = slicer.Path
            });
            added++;
        }

        SyncDefaultFromOrder();
        RefreshList();

        MessageBox.Show(
            added > 0
                ? $"{added} installed slicer{(added == 1 ? "" : "s")} added."
                : "No additional supported slicers were found.",
            "Slicer Scan",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void SaveAndClose()
    {
        SyncDefaultFromOrder();
        _settings.AutoLaunchDefault = _autoLaunch.Checked && _settings.Slicers.Count > 0;
        _settings.CountdownSeconds = (int)_countdownSeconds.Value;

        ConfigService.Save(_settings);
        DialogResult = DialogResult.OK;
        Close();
    }
}


internal sealed class EditSlicerForm : Form
{
    private readonly TextBox _nameBox = new();
    private readonly TextBox _pathBox = new();
    private readonly IReadOnlyList<SlicerEntry> _existingSlicers;
    private readonly SlicerEntry? _editingSlicer;

    public string SlicerName => _nameBox.Text.Trim();
    public string SlicerPath => _pathBox.Text.Trim();

    public EditSlicerForm(IReadOnlyList<SlicerEntry> existingSlicers, SlicerEntry? slicer = null)
    {
        _existingSlicers = existingSlicers;
        _editingSlicer = slicer;
        Text = slicer is null ? "Add Slicer" : "Edit Slicer";
        Width = 650;
        Height = 360;
        MinimumSize = new Size(560, 330);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = BrandAssets.DarkGray
        };

        header.Controls.Add(new Label
        {
            Text = slicer is null ? "Add Slicer" : "Edit Slicer",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(26, 22)
        });

        var nameLabel = new Label
        {
            Text = "Name",
            AutoSize = true,
            Location = new Point(28, 98)
        };

        _nameBox.Location = new Point(28, 122);
        _nameBox.Size = new Size(570, 30);
        _nameBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var pathLabel = new Label
        {
            Text = "Application (.exe)",
            AutoSize = true,
            Location = new Point(28, 165)
        };

        _pathBox.Location = new Point(28, 189);
        _pathBox.Size = new Size(450, 30);
        _pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var browseButton = new Button
        {
            Text = "Browse...",
            Size = new Size(105, 32),
            Location = new Point(493, 187),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        browseButton.FlatAppearance.BorderSize = 1;
        browseButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        browseButton.Click += (_, _) => Browse();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(105, 38),
            Location = new Point(377, 239),
            DialogResult = DialogResult.Cancel,
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        cancelButton.FlatAppearance.BorderSize = 1;
        cancelButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;

        var saveButton = new Button
        {
            Text = slicer is null ? "Add" : "Save",
            Size = new Size(105, 38),
            Location = new Point(492, 239),
            BackColor = BrandAssets.Yellow,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += (_, _) =>
        {
            if (!ValidateInput())
                return;

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(header);
        Controls.Add(nameLabel);
        Controls.Add(_nameBox);
        Controls.Add(pathLabel);
        Controls.Add(_pathBox);
        Controls.Add(browseButton);
        Controls.Add(cancelButton);
        Controls.Add(saveButton);

        Resize += (_, _) =>
        {
            saveButton.Left = ClientSize.Width - saveButton.Width - 26;
            saveButton.Top = ClientSize.Height - saveButton.Height - 22;
            cancelButton.Left = saveButton.Left - cancelButton.Width - 10;
            cancelButton.Top = saveButton.Top;

            _nameBox.Width = Math.Max(300, ClientSize.Width - 56);
            _pathBox.Width = Math.Max(240, ClientSize.Width - 176);
            browseButton.Left = ClientSize.Width - browseButton.Width - 28;
        };

        if (slicer is not null)
        {
            _nameBox.Text = slicer.Name;
            _pathBox.Text = slicer.Path;
        }

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Slicer Application",
            Filter = "Applications (*.exe)|*.exe",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _pathBox.Text = dialog.FileName;

        if (string.IsNullOrWhiteSpace(_nameBox.Text))
            _nameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(
                "Please enter a slicer name.",
                "Missing Name",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _nameBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(_pathBox.Text) || !File.Exists(_pathBox.Text.Trim()))
        {
            MessageBox.Show(
                "Please select a valid slicer application.",
                "Application Not Found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        var normalizedName = _nameBox.Text.Trim();
        var normalizedPath = Path.GetFullPath(_pathBox.Text.Trim());

        var duplicateName = _existingSlicers.Any(s =>
            !ReferenceEquals(s, _editingSlicer) &&
            string.Equals(s.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

        if (duplicateName)
        {
            MessageBox.Show(
                "A slicer with this name already exists.",
                "Duplicate Slicer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _nameBox.Focus();
            _nameBox.SelectAll();
            return false;
        }

        var duplicatePath = _existingSlicers.Any(s =>
        {
            if (ReferenceEquals(s, _editingSlicer)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(s.Path), normalizedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(s.Path.Trim(), _pathBox.Text.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        });

        if (duplicatePath)
        {
            MessageBox.Show(
                "This slicer application is already configured.",
                "Duplicate Slicer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _pathBox.Focus();
            _pathBox.SelectAll();
            return false;
        }

        return true;
    }
}


internal sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = "How to use in Fusion 360";
        Width = 720;
        Height = 550;
        MinimumSize = new Size(650, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = BrandAssets.DarkGray };
        header.Controls.Add(new Label
        {
            Text = "How to use in Fusion 360",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 24)
        });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30), AutoScroll = true };
        var intro = new Label
        {
            Text = "Connect SlicerLauncher to Fusion 360 once, then choose your slicer every time you export a mesh.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = false,
            Width = 620,
            Height = 38,
            Location = new Point(30, 20)
        };
        var steps = new Label
        {
            Text = "1. In Fusion 360, choose Save as Mesh.\n" +
                   "2. Set Preparation Type to Print Utility.\n" +
                   "3. Under Output, set Application to Custom.\n" +
                   "4. Select SlicerLauncher.exe as the custom application.\n" +
                   "5. Choose STL or 3MF as your export format.\n" +
                   "6. Click OK. SlicerLauncher opens and lets you choose the slicer.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = false,
            Width = 620,
            Height = 155,
            Location = new Point(30, 68)
        };
        var pathTitle = new Label
        {
            Text = "Fusion 360 executable",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = true,
            Location = new Point(30, 236)
        };
        var fusionAliasPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "SlicerLauncher.exe");

        var executablePath = new TextBox
        {
            ReadOnly = true,
            Text = fusionAliasPath,
            Location = new Point(30, 260),
            Width = 610,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var note = new Label
        {
            Text = "If Custom is not available, set Preparation Type to Print Utility.\n" +
                   "Fusion 360 normally remembers the selected custom application for future exports.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = false,
            Width = 610,
            Height = 48,
            Location = new Point(30, 304)
        };
        content.Controls.Add(intro);
        content.Controls.Add(steps);
        content.Controls.Add(pathTitle);
        content.Controls.Add(executablePath);
        content.Controls.Add(note);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Color.White };
        var closeButton = new Button
        {
            Text = "Got it",
            Size = new Size(120, 42),
            BackColor = BrandAssets.Yellow,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);
        footer.Resize += (_, _) => { closeButton.Left = footer.ClientSize.Width - closeButton.Width - 30; closeButton.Top = 14; };

        Controls.Add(content);
        Controls.Add(header);
        Controls.Add(footer);
    }
}

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About SlicerLauncher";
        Width = 980;
        Height = 510;
        MinimumSize = new Size(900, 490);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = BrandAssets.DarkGray
        };

        header.Controls.Add(new Label
        {
            Text = "About",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 24)
        });

        var aboutLogo = BrandAssets.LoadEmbeddedImage("logo_about.png");

        var logoBox = new PictureBox
        {
            Image = aboutLogo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(48, 126),
            Size = new Size(160, 160)
        };

        var appName = new Label
        {
            Text = "SlicerLauncher",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = true,
            Location = new Point(235, 126)
        };

        var version = new Label
        {
            Text = "Version 1.1.0",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = true,
            Location = new Point(237, 168)
        };

        var description = new Label
        {
            Text = "A free, open-source Windows utility for opening STL & 3MF files with your slicer of choice, including Fusion 360 support.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = false,
            UseMnemonic = false,
            Location = new Point(235, 210),
            Size = new Size(690, 42)
        };

        var privacy = new Label
        {
            Text = "No ads. No tracking. No telemetry.",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = true,
            Location = new Point(235, 265)
        };

        var copyright = new Label
        {
            Text = "\u00A9 2026 Nino King \u00B7 3dprintkings \u00B7 Licensed under the GNU GPL v3.0",
            Font = new Font("Segoe UI", 9F),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = true,
            Location = new Point(235, 310)
        };

        var website = new LinkLabel
        {
            Text = "www.3dprintkings.ch",
            AutoSize = true,
            LinkColor = BrandAssets.DarkGray,
            ActiveLinkColor = BrandAssets.Yellow,
            VisitedLinkColor = BrandAssets.DarkGray,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(235, 354)
        };
        website.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.3dprintkings.ch",
                    UseShellExecute = true
                });
            }
            catch { }
        };

        var source = new LinkLabel
        {
            Text = "View source on GitHub",
            AutoSize = true,
            LinkColor = BrandAssets.DarkGray,
            ActiveLinkColor = BrandAssets.Yellow,
            VisitedLinkColor = BrandAssets.DarkGray,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Location = new Point(235, 384)
        };
        source.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/3dprintkings/SlicerLauncher",
                    UseShellExecute = true
                });
            }
            catch { }
        };

        Controls.Add(header);
        Controls.Add(logoBox);
        Controls.Add(appName);
        Controls.Add(version);
        Controls.Add(description);
        Controls.Add(privacy);
        Controls.Add(copyright);
        Controls.Add(website);
        Controls.Add(source);
    }
}

internal static class ControlExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}

