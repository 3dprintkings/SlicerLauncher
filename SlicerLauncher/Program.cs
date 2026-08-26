using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using System.Text.Json;
using System.Xml.Linq;

namespace SlicerLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        FileAssociationService.RefreshExecutablePathIfRegistered();
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
        new("Creality Print", new[] { "CrealityPrint.exe", "Creality Print.exe" }, new[] { "Creality Print*", "Creality*" })
    };

    public static List<SlicerEntry> DetectInstalled()
    {
        var results = new List<SlicerEntry>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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


internal static class FileAssociationService
{
    private const string ProgId = "SlicerLauncher.File";
    private const string RegisteredAppName = "SlicerLauncher";
    private const string AppKey = @"Software\SlicerLauncher";
    private const string CapabilitiesKey = AppKey + @"\Capabilities";

    public static void RefreshExecutablePathIfRegistered()
    {
        try
        {
            using var registeredApps = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications");
            if (registeredApps?.GetValue(RegisteredAppName) is null)
                return;

            using var associations = Registry.CurrentUser.OpenSubKey(CapabilitiesKey + @"\FileAssociations");
            var registerStl = associations?.GetValue(".stl") is not null;
            var register3mf = associations?.GetValue(".3mf") is not null;

            if (registerStl || register3mf)
                WriteApplicationRegistration(registerStl, register3mf);
        }
        catch
        {
            // Association refresh should never prevent the launcher from opening.
        }
    }

    public static bool IsRegistered(string extension)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{NormalizeExtension(extension)}\OpenWithProgids");
            return key?.GetValueNames().Contains(ProgId, StringComparer.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    public static void Apply(bool registerStl, bool register3mf)
    {
        SetExtensionRegistration(".stl", registerStl);
        SetExtensionRegistration(".3mf", register3mf);

        if (!registerStl && !register3mf)
        {
            RemoveApplicationRegistration();
            return;
        }

        WriteApplicationRegistration(registerStl, register3mf);
    }

    public static void OpenWindowsDefaultApps()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "ms-settings:defaultapps",
            UseShellExecute = true
        });
    }

    private static void WriteApplicationRegistration(bool registerStl, bool register3mf)
    {
        var exePath = Application.ExecutablePath;
        var quotedExe = Quote(exePath);

        using (var progId = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progId.SetValue("", "3D model file", RegistryValueKind.String);
            using var icon = progId.CreateSubKey("DefaultIcon");
            icon.SetValue("", quotedExe + ",0", RegistryValueKind.String);
            using var command = progId.CreateSubKey(@"shell\open\command");
            command.SetValue("", quotedExe + " \"%1\"", RegistryValueKind.String);
        }

        using (var app = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Applications\SlicerLauncher.exe"))
        {
            app.SetValue("FriendlyAppName", "SlicerLauncher", RegistryValueKind.String);
            using var command = app.CreateSubKey(@"shell\open\command");
            command.SetValue("", quotedExe + " \"%1\"", RegistryValueKind.String);
            using var supported = app.CreateSubKey("SupportedTypes");
            supported.DeleteValue(".stl", false);
            supported.DeleteValue(".3mf", false);
            if (registerStl) supported.SetValue(".stl", "", RegistryValueKind.String);
            if (register3mf) supported.SetValue(".3mf", "", RegistryValueKind.String);
        }

        using (var capabilities = Registry.CurrentUser.CreateSubKey(CapabilitiesKey))
        {
            capabilities.SetValue("ApplicationName", "SlicerLauncher", RegistryValueKind.String);
            capabilities.SetValue(
                "ApplicationDescription",
                "Open STL and 3MF files with the slicer of your choice.",
                RegistryValueKind.String);

            using var associations = capabilities.CreateSubKey("FileAssociations");
            associations.DeleteValue(".stl", false);
            associations.DeleteValue(".3mf", false);
            if (registerStl) associations.SetValue(".stl", ProgId, RegistryValueKind.String);
            if (register3mf) associations.SetValue(".3mf", ProgId, RegistryValueKind.String);
        }

        using var registeredApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        registeredApps.SetValue(RegisteredAppName, CapabilitiesKey, RegistryValueKind.String);
    }

    private static void RemoveApplicationRegistration()
    {
        using (var registeredApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            registeredApps.DeleteValue(RegisteredAppName, false);

        Registry.CurrentUser.DeleteSubKeyTree(AppKey, false);
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\SlicerLauncher.exe", false);
    }

    private static void SetExtensionRegistration(string extension, bool enabled)
    {
        extension = NormalizeExtension(extension);

        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}\OpenWithProgids");

        if (enabled)
            key.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        else
            key.DeleteValue(ProgId, false);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("File extension is required.", nameof(extension));

        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
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

        _versionLabel.Text = "v1.0.0";
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

        header.Controls.Add(title);
        header.Controls.Add(_versionLabel);
        header.Controls.Add(_aboutLink);
        header.Resize += (_, _) => PositionHeaderLinks();
        PositionHeaderLinks();

        _fileLabel.Dock = DockStyle.Top;
        _fileLabel.Height = 58;
        _fileLabel.Padding = new Padding(35, 18, 20, 5);
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

        var settingsButton = CreateFooterButton("Settings", 120);
        settingsButton.Click += (_, _) => { StopCountdown(false); OpenSettings(); };

        var recentButton = CreateFooterButton("Recent Files", 300);
        recentButton.Click += (_, _) => { StopCountdown(false); OpenRecentFiles(); };

        var helpButton = CreateFooterButton("Help", 480);
        helpButton.Click += (_, _) =>
        {
            StopCountdown(false);
            using var help = new HelpForm();
            help.ShowDialog(this);
        };

        footer.Controls.Add(settingsButton);
        footer.Controls.Add(recentButton);
        footer.Controls.Add(helpButton);

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
        _versionLabel.Top = 14;

        _aboutLink.Left = right - _aboutLink.Width;
        _aboutLink.Top = 50;
    }

    private Button CreateFooterButton(string text, int left)
    {
        return new Button
        {
            Text = text,
            Width = 120,
            Height = 42,
            Location = new Point(left, 24),
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
                Text = "No slicers configured.\r\n\r\nOpen Settings to add your first slicer.",
                ForeColor = BrandAssets.MediumGray,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Margin = new Padding(15)
            });
            _slicerPanel.ResumeLayout();
            return;
        }

        int count = _settings.Slicers.Count;
        int available = Math.Max(400, _slicerPanel.ClientSize.Width - _slicerPanel.Padding.Horizontal - 32);
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

    private void OpenSettings()
    {
        using var settings = new SettingsForm(_settings);
        settings.ShowDialog(this);
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
                "The slicer could not be found:\r\n\r\n" + slicer.Path + "\r\n\r\nOpen Settings?",
                "Slicer Not Found",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Yes) OpenSettings();
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
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
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

internal sealed class SettingsForm : Form
{
    private readonly LauncherSettings _settings;
    private readonly ListBox _list = new();
    private readonly TextBox _nameBox = new();
    private readonly TextBox _pathBox = new();
    private readonly Label _modeLabel = new();
    private readonly Button _saveButton = new();
    private readonly Button _removeButton = new();
    private readonly CheckBox _stlAssociation = new();
    private readonly CheckBox _threeMfAssociation = new();
    private readonly ComboBox _defaultSlicer = new();
    private readonly CheckBox _autoLaunch = new();
    private readonly NumericUpDown _countdownSeconds = new();
    private bool _newMode;

    public SettingsForm(LauncherSettings current)
    {
        _settings = new LauncherSettings
        {
            ShowWelcomeHelp = current.ShowWelcomeHelp,
            Slicers = current.Slicers.Select(s => new SlicerEntry { Name = s.Name, Path = s.Path }).ToList(),
            DefaultSlicerPath = current.DefaultSlicerPath,
            AutoLaunchDefault = current.AutoLaunchDefault,
            CountdownSeconds = current.CountdownSeconds,
            RecentFiles = (current.RecentFiles ?? new List<string>()).ToList()
        };
        InitializeForm();
        RefreshList();
        RefreshDefaultSlicerList();
        if (_settings.Slicers.Count > 0) _list.SelectedIndex = 0;
        else BeginNew();

        FormClosing += (_, _) => SaveAutomaticLaunch(showConfirmation: false);
    }

    private void InitializeForm()
    {
        Text = "SlicerLauncher - Settings";
        Width = 900;
        Height = 865;
        MinimumSize = new Size(780, 800);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = BrandAssets.DarkGray };
        header.Controls.Add(new Label
        {
            Text = "SETTINGS",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 26)
        });

        var listLabel = new Label { Text = "Configured Slicers", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(30, 125) };
        _list.Location = new Point(30, 155);
        _list.Size = new Size(270, 300);
        _list.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (_list.SelectedIndex >= 0)
            {
                _newMode = false;
                LoadSelected();
            }
        };

        _modeLabel.Text = "Edit Slicer";
        _modeLabel.AutoSize = true;
        _modeLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _modeLabel.ForeColor = BrandAssets.MediumGray;
        _modeLabel.Location = new Point(345, 125);

        var nameLabel = new Label { Text = "Name", AutoSize = true, Location = new Point(345, 170) };
        _nameBox.Location = new Point(345, 198);
        _nameBox.Width = 500;
        _nameBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var pathLabel = new Label { Text = "Application (.exe)", AutoSize = true, Location = new Point(345, 250) };
        _pathBox.Location = new Point(345, 278);
        _pathBox.Width = 380;
        _pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        var browseButton = new Button { Text = "Browse...", Location = new Point(740, 275), Size = new Size(105, 35), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        browseButton.Click += (_, _) => Browse();

        var addButton = CreateActionButton("+ Add Slicer", 345, BrandAssets.Yellow, BrandAssets.DarkGray);
        addButton.Click += (_, _) => BeginNew();

        var moveUpButton = new Button
        {
            Text = "↑ Up",
            Location = new Point(30, 468),
            Size = new Size(125, 34),
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        moveUpButton.FlatAppearance.BorderSize = 1;
        moveUpButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        moveUpButton.Click += (_, _) => MoveSelectedSlicer(-1);

        var moveDownButton = new Button
        {
            Text = "↓ Down",
            Location = new Point(175, 468),
            Size = new Size(125, 34),
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        moveDownButton.FlatAppearance.BorderSize = 1;
        moveDownButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        moveDownButton.Click += (_, _) => MoveSelectedSlicer(1);

        var scanButton = new Button
        {
            Text = "Scan Installed",
            Location = new Point(30, 515),
            Size = new Size(270, 38),
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        scanButton.FlatAppearance.BorderSize = 1;
        scanButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        scanButton.Click += (_, _) => ScanInstalled();

        _saveButton.Text = "Save";
        StyleActionButton(_saveButton, 500, BrandAssets.DarkGray, Color.White);
        _saveButton.Click += (_, _) => SaveCurrent();

        _removeButton.Text = "Remove";
        StyleActionButton(_removeButton, 655, Color.White, BrandAssets.DarkGray);
        _removeButton.FlatAppearance.BorderSize = 1;
        _removeButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        _removeButton.Click += (_, _) => RemoveCurrent();

        var autoGroup = new GroupBox
        {
            Text = "Automatic Launch",
            Location = new Point(345, 430),
            Size = new Size(500, 145),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var defaultLabel = new Label { Text = "Default slicer", AutoSize = true, Location = new Point(18, 31) };
        _defaultSlicer.DropDownStyle = ComboBoxStyle.DropDownList;
        _defaultSlicer.Location = new Point(125, 27);
        _defaultSlicer.Size = new Size(230, 30);

        _autoLaunch.Text = "Automatically launch default slicer";
        _autoLaunch.AutoSize = true;
        _autoLaunch.Location = new Point(18, 68);
        _autoLaunch.Checked = _settings.AutoLaunchDefault;

        var countdownLabel = new Label { Text = "Countdown", AutoSize = true, Location = new Point(292, 69) };
        _countdownSeconds.Minimum = 1;
        _countdownSeconds.Maximum = 30;
        _countdownSeconds.Value = Math.Clamp(_settings.CountdownSeconds, 1, 30);
        _countdownSeconds.Location = new Point(372, 65);
        _countdownSeconds.Width = 55;
        var secLabel = new Label { Text = "sec", AutoSize = true, Location = new Point(433, 69) };

        var saveAutoButton = new Button
        {
            Text = "Save",
            Location = new Point(375, 25),
            Size = new Size(95, 32),
            BackColor = BrandAssets.Yellow,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        saveAutoButton.FlatAppearance.BorderSize = 0;
        saveAutoButton.Click += (_, _) => SaveAutomaticLaunch();

        autoGroup.Controls.Add(defaultLabel);
        autoGroup.Controls.Add(_defaultSlicer);
        autoGroup.Controls.Add(_autoLaunch);
        autoGroup.Controls.Add(countdownLabel);
        autoGroup.Controls.Add(_countdownSeconds);
        autoGroup.Controls.Add(secLabel);
        autoGroup.Controls.Add(saveAutoButton);

        var associationGroup = new GroupBox
        {
            Text = "File Associations",
            Location = new Point(345, 590),
            Size = new Size(500, 145),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _stlAssociation.Text = "Register SlicerLauncher for .STL files";
        _stlAssociation.AutoSize = true;
        _stlAssociation.Location = new Point(18, 28);
        _stlAssociation.Checked = FileAssociationService.IsRegistered(".stl");

        _threeMfAssociation.Text = "Register SlicerLauncher for .3MF files";
        _threeMfAssociation.AutoSize = true;
        _threeMfAssociation.Location = new Point(18, 55);
        _threeMfAssociation.Checked = FileAssociationService.IsRegistered(".3mf");

        var applyAssociationsButton = new Button
        {
            Text = "Apply",
            Location = new Point(18, 82),
            Size = new Size(100, 32),
            BackColor = BrandAssets.Yellow,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        applyAssociationsButton.FlatAppearance.BorderSize = 0;
        applyAssociationsButton.Click += (_, _) => ApplyFileAssociations();

        var defaultAppsButton = new Button
        {
            Text = "Windows Default Apps",
            Location = new Point(130, 82),
            Size = new Size(165, 32),
            BackColor = Color.White,
            ForeColor = BrandAssets.DarkGray,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        defaultAppsButton.FlatAppearance.BorderSize = 1;
        defaultAppsButton.FlatAppearance.BorderColor = BrandAssets.BorderGray;
        defaultAppsButton.Click += (_, _) => OpenWindowsDefaultApps();

        var associationNote = new Label
        {
            Text = "Registration adds SlicerLauncher as an available app. Windows controls which app is the default.",
            AutoSize = false,
            ForeColor = BrandAssets.MediumGray,
            Font = new Font("Segoe UI", 8.3F),
            Location = new Point(310, 28),
            Size = new Size(175, 82)
        };

        associationGroup.Controls.Add(_stlAssociation);
        associationGroup.Controls.Add(_threeMfAssociation);
        associationGroup.Controls.Add(applyAssociationsButton);
        associationGroup.Controls.Add(defaultAppsButton);
        associationGroup.Controls.Add(associationNote);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 90, BackColor = Color.White };
        var closeButton = new Button
        {
            Text = "Close",
            Size = new Size(110, 42),
            BackColor = BrandAssets.DarkGray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);
        footer.Resize += (_, _) => { closeButton.Left = footer.ClientSize.Width - closeButton.Width - 30; closeButton.Top = 23; };

        Controls.Add(header);
        Controls.Add(listLabel);
        Controls.Add(_list);
        Controls.Add(_modeLabel);
        Controls.Add(nameLabel);
        Controls.Add(_nameBox);
        Controls.Add(pathLabel);
        Controls.Add(_pathBox);
        Controls.Add(browseButton);
        Controls.Add(addButton);
        Controls.Add(moveUpButton);
        Controls.Add(moveDownButton);
        Controls.Add(scanButton);
        Controls.Add(_saveButton);
        Controls.Add(_removeButton);
        Controls.Add(autoGroup);
        Controls.Add(associationGroup);
        Controls.Add(footer);
    }

    private Button CreateActionButton(string text, int left, Color bg, Color fg)
    {
        var button = new Button { Text = text };
        StyleActionButton(button, left, bg, fg);
        return button;
    }

    private static void StyleActionButton(Button button, int left, Color bg, Color fg)
    {
        button.Location = new Point(left, 345);
        button.Size = new Size(135, 45);
        button.BackColor = bg;
        button.ForeColor = fg;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private void MoveSelectedSlicer(int direction)
    {
        var index = _list.SelectedIndex;
        if (index < 0 || index >= _settings.Slicers.Count) return;

        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= _settings.Slicers.Count) return;

        var item = _settings.Slicers[index];
        _settings.Slicers.RemoveAt(index);
        _settings.Slicers.Insert(newIndex, item);

        ConfigService.Save(_settings);
        RefreshList();
        RefreshDefaultSlicerList();
        _list.SelectedIndex = newIndex;
    }

    private void RefreshList()
    {
        _list.Items.Clear();
        foreach (var s in _settings.Slicers) _list.Items.Add(s);
    }

    private void RefreshDefaultSlicerList()
    {
        _defaultSlicer.Items.Clear();
        foreach (var slicer in _settings.Slicers) _defaultSlicer.Items.Add(slicer);
        var index = _settings.Slicers.FindIndex(s => string.Equals(s.Path, _settings.DefaultSlicerPath, StringComparison.OrdinalIgnoreCase));
        _defaultSlicer.SelectedIndex = index;
        if (index < 0 && _settings.Slicers.Count > 0 && string.IsNullOrWhiteSpace(_settings.DefaultSlicerPath))
            _defaultSlicer.SelectedIndex = 0;
    }

    private void SaveAutomaticLaunch(bool showConfirmation = true)
    {
        if (_autoLaunch.Checked && _defaultSlicer.SelectedIndex < 0 && _defaultSlicer.Items.Count > 0)
            _defaultSlicer.SelectedIndex = 0;

        _settings.DefaultSlicerPath = (_defaultSlicer.SelectedItem as SlicerEntry)?.Path ?? "";
        _settings.AutoLaunchDefault = _autoLaunch.Checked && !string.IsNullOrWhiteSpace(_settings.DefaultSlicerPath);
        _settings.CountdownSeconds = (int)_countdownSeconds.Value;
        ConfigService.Save(_settings);

        if (showConfirmation)
            MessageBox.Show("Automatic launch settings saved.", "Automatic Launch", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BeginNew()
    {
        _newMode = true;
        _list.ClearSelected();
        _nameBox.Clear();
        _pathBox.Clear();
        _modeLabel.Text = "Add New Slicer";
        _saveButton.Text = "Add";
        _removeButton.Enabled = false;
        _nameBox.Focus();
    }

    private void LoadSelected()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _settings.Slicers.Count) return;
        _newMode = false;
        var slicer = _settings.Slicers[_list.SelectedIndex];
        _nameBox.Text = slicer.Name;
        _pathBox.Text = slicer.Path;
        _modeLabel.Text = "Edit Slicer";
        _saveButton.Text = "Save";
        _removeButton.Enabled = true;
    }

    private void ScanInstalled()
    {
        var detected = SlicerDetectionService.DetectInstalled();
        var existingPaths = new HashSet<string>(_settings.Slicers.Select(s => s.Path), StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var slicer in detected)
        {
            if (existingPaths.Add(slicer.Path))
            {
                _settings.Slicers.Add(slicer);
                added++;
            }
        }

        ConfigService.Save(_settings);
        RefreshList();
        RefreshDefaultSlicerList();

        if (_settings.Slicers.Count > 0 && _list.SelectedIndex < 0)
            _list.SelectedIndex = 0;

        MessageBox.Show(
            added > 0
                ? $"{added} installed slicer{(added == 1 ? "" : "s")} added."
                : "No additional supported slicers were found.",
            "Slicer Scan",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ApplyFileAssociations()
    {
        try
        {
            FileAssociationService.Apply(_stlAssociation.Checked, _threeMfAssociation.Checked);

            var enabled = new List<string>();
            if (_stlAssociation.Checked) enabled.Add(".STL");
            if (_threeMfAssociation.Checked) enabled.Add(".3MF");

            MessageBox.Show(
                enabled.Count > 0
                    ? "SlicerLauncher is now registered for " + string.Join(" and ", enabled) +
                      " files. You can select it from Open with or set it as the default app in Windows."
                    : "SlicerLauncher file associations were removed.",
                "File Associations",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The file associations could not be updated.\r\n\r\n" + ex.Message,
                "File Association Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenWindowsDefaultApps()
    {
        try { FileAssociationService.OpenWindowsDefaultApps(); }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Windows Default Apps could not be opened.\r\n\r\n" + ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Slicer Application",
            Filter = "Applications (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _pathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
            _nameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("Please enter a slicer name.", "Missing Name", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _nameBox.Focus();
            return false;
        }
        if (string.IsNullOrWhiteSpace(_pathBox.Text) || !File.Exists(_pathBox.Text.Trim()))
        {
            MessageBox.Show("Please select a valid slicer application.", "Application Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private void SaveCurrent()
    {
        if (!ValidateInput()) return;
        var name = _nameBox.Text.Trim();
        var path = _pathBox.Text.Trim();

        if (_newMode)
        {
            _settings.Slicers.Add(new SlicerEntry { Name = name, Path = path });
            ConfigService.Save(_settings);
            RefreshList();
            RefreshDefaultSlicerList();
            _newMode = false;
            _list.SelectedIndex = _settings.Slicers.Count - 1;
        }
        else
        {
            var index = _list.SelectedIndex;
            if (index < 0 || index >= _settings.Slicers.Count) return;
            var oldPath = _settings.Slicers[index].Path;
            _settings.Slicers[index].Name = name;
            _settings.Slicers[index].Path = path;
            if (string.Equals(_settings.DefaultSlicerPath, oldPath, StringComparison.OrdinalIgnoreCase))
                _settings.DefaultSlicerPath = path;
            ConfigService.Save(_settings);
            RefreshList();
            RefreshDefaultSlicerList();
            _list.SelectedIndex = index;
        }
    }

    private void RemoveCurrent()
    {
        var index = _list.SelectedIndex;
        if (index < 0 || index >= _settings.Slicers.Count) return;
        var removed = _settings.Slicers[index];
        var result = MessageBox.Show($"Remove \"{removed.Name}\"?", "Remove Slicer", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        _settings.Slicers.RemoveAt(index);
        if (string.Equals(_settings.DefaultSlicerPath, removed.Path, StringComparison.OrdinalIgnoreCase))
        {
            _settings.DefaultSlicerPath = "";
            _settings.AutoLaunchDefault = false;
            _autoLaunch.Checked = false;
        }
        ConfigService.Save(_settings);
        RefreshList();
        RefreshDefaultSlicerList();
        if (_settings.Slicers.Count == 0) BeginNew();
        else _list.SelectedIndex = Math.Min(index, _settings.Slicers.Count - 1);
    }
}


internal sealed class HelpForm : Form
{
    public HelpForm()
    {
        Text = "How to use in Fusion 360";
        Width = 720;
        Height = 675;
        MinimumSize = new Size(650, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = BrandAssets.DarkGray };
        header.Controls.Add(new Label
        {
            Text = "HOW TO USE IN FUSION 360",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 26)
        });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(34), AutoScroll = true };
        var intro = new Label
        {
            Text = "Connect SlicerLauncher to Fusion 360 once, then choose your slicer every time you export a mesh.",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = false,
            Width = 620,
            Height = 54,
            Location = new Point(34, 28)
        };
        var steps = new Label
        {
            Text = "1. In Fusion 360, choose Save as Mesh.\n\n" +
                   "2. Set Preparation Type to Print Utility.\n\n" +
                   "3. Under Output, set Application to Custom.\n\n" +
                   "4. Select SlicerLauncher.exe as the custom application.\n\n" +
                   "5. Choose STL or 3MF as your export format.\n\n" +
                   "6. Click OK. SlicerLauncher will open and let you choose the slicer.",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = false,
            Width = 620,
            Height = 305,
            Location = new Point(34, 105)
        };
        var pathTitle = new Label
        {
            Text = "This installation",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = true,
            Location = new Point(34, 425)
        };
        var executablePath = new TextBox
        {
            ReadOnly = true,
            Text = Application.ExecutablePath,
            Location = new Point(34, 452),
            Width = 610,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var note = new Label
        {
            Text = "If Custom is not available, make sure Preparation Type is set to Print Utility.\n\n" +
                   "Fusion 360 normally remembers the selected custom application for future exports.\n\n" +
                   "Tip: In Settings > File Associations, you can also register SlicerLauncher for STL and 3MF files. " +
                   "Then you can open model files directly from Windows and choose your slicer.\n\n" +
                   "Automatic Launch can open a configured default slicer after a countdown. Press Stop or choose another slicer to cancel it.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = false,
            Width = 610,
            Height = 155,
            Location = new Point(34, 495)
        };
        content.Controls.Add(intro);
        content.Controls.Add(steps);
        content.Controls.Add(pathTitle);
        content.Controls.Add(executablePath);
        content.Controls.Add(note);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 80, BackColor = Color.White };
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
        footer.Resize += (_, _) => { closeButton.Left = footer.ClientSize.Width - closeButton.Width - 30; closeButton.Top = 19; };

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
        Width = 650;
        Height = 520;
        MinimumSize = new Size(600, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BrandAssets.LightGray;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = BrandAssets.DarkGray };
        header.Controls.Add(new Label
        {
            Text = "ABOUT",
            ForeColor = BrandAssets.Yellow,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 24)
        });

        var logoBox = new PictureBox
        {
            Image = BrandAssets.LoadEmbeddedImage("logo_about.png"),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(50, 112),
            Size = new Size(220, 38)
        };

        var appName = new Label
        {
            Text = "SlicerLauncher",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = true,
            Location = new Point(50, 184)
        };

        var version = new Label
        {
            Text = "Version 1.0.0",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = true,
            Location = new Point(52, 218)
        };

        var description = new Label
        {
            Text = "A lightweight launcher for sending Fusion 360 mesh exports to the slicer of your choice.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = BrandAssets.DarkGray,
            AutoSize = false,
            Width = 540,
            Height = 44,
            Location = new Point(50, 260)
        };

        var copyright = new Label
        {
            Text = "© 2026 3DPrintKings. All rights reserved.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = BrandAssets.MediumGray,
            AutoSize = false,
            Width = 360,
            Height = 24,
            Location = new Point(50, 318)
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
            Location = new Point(50, 355)
        };
        website.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = "https://www.3dprintkings.ch", UseShellExecute = true }); }
            catch { }
        };

        Controls.Add(header);
        Controls.Add(logoBox);
        Controls.Add(appName);
        Controls.Add(version);
        Controls.Add(description);
        Controls.Add(copyright);
        Controls.Add(website);
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
