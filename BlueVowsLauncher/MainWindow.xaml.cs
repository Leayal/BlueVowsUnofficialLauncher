using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using BlueVowsLauncher.Classes;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Threading;
using System.ComponentModel;
using System.Text.Json;
using MahApps.Metro.Controls;
using System.Buffers;
using System.Net.Http;
using System.Xml;

namespace BlueVowsLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private WebAPI apiClient;
        private CancellationTokenSource cancelSrc;
        const string BaseUrl = "https://raw.githubusercontent.com/Leayal/BlueVowsUnofficialLauncher/master";

        public MainWindow()
        {
            this.apiClient = new WebAPI();
            int width = 1280, height = 720;
            bool localeemu = false;

            try
            {
                var theReader = new Utf8JsonReader(File.ReadAllBytes(Path.Combine(RuntimeVars.RootPath, "Settings.json")));
                if (JsonDocument.TryParseValue(ref theReader, out var settings))
                {
                    if ((settings.RootElement.TryGetProperty("width", out var _width) && settings.RootElement.TryGetProperty("height", out var _height) && (settings.RootElement.TryGetProperty("locale-emu", out var _localeEmu)))
                    && (_width.TryGetInt32(out width) && _height.TryGetInt32(out height)))
                    {
                        localeemu = _localeEmu.GetBoolean();
                    }
                }
            }
            catch { }
            InitializeComponent();
            this.gameClient_Width.Text = width.ToString();
            this.gameClient_Height.Text = height.ToString();
            this.gameClient_LocaleEmu.IsChecked = localeemu;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BlueVowsLauncher.2.ico"))
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                this.Icon = bmp;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                using (var fs = File.Create(Path.Combine(RuntimeVars.RootPath, "Settings.json")))
                using (var writer = new Utf8JsonWriter(fs, new JsonWriterOptions()
                {
                    Indented = true
                }))
                {
                    writer.WriteStartObject();
                    writer.WriteBoolean("locale-emu", this.gameClient_LocaleEmu.IsChecked == true);
                    if (int.TryParse(this.gameClient_Width.Text, out var _width) && int.TryParse(this.gameClient_Height.Text, out var _height))
                    {
                        writer.WriteNumber("width", _width);
                        writer.WriteNumber("height", _height);
                    }
                    writer.WriteEndObject();
                    writer.Flush();
                }
            }
            catch { }
            base.OnClosing(e);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.tabMainMenu.IsSelected = true;
        }

        private async void ButtonStartGame_Click(object sender, RoutedEventArgs e)
        {
            this.mainProgressBar.IsIndeterminate = true;
            if (this.cancelSrc != null)
            {
                this.cancelSrc.Dispose();
            }
            this.cancelSrc = new CancellationTokenSource();
            this.tabProgressing.IsSelected = true;
            this.mainProgressText.Text = "Checking client version";

            int width = 0, height = 0;
            bool localEmu = this.gameClient_LocaleEmu.IsChecked == true;

            if (!int.TryParse(this.gameClient_Width.Text, out width))
            {
                MessageBox.Show(this, $"Invalid game screen width.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(this.gameClient_Height.Text, out height))
            {
                MessageBox.Show(this, $"Invalid game screen height.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var something = await this.apiClient.GetVersionAsync();
                var responseCode = something.RootElement.GetProperty("errornu").GetString();
                if (responseCode == "0")
                {
                    var packageObj = something.RootElement.GetProperty("base").GetProperty("package");
                    var md5s = packageObj.GetProperty("md5").GetProperty("gwphone_windows").GetString();
                    var downloadUrl = packageObj.GetProperty("packageUrl").GetString();
                    var updateVersion = packageObj.GetProperty("tar_version").GetString();

                    if (!string.Equals(this.GetClientVersion(), updateVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        bool isContinue = string.Equals(this.GetDownloadingClientVersion(), updateVersion, StringComparison.OrdinalIgnoreCase);
                        bool isCompleted = false;
                        string targetToDelete;
                        using (var fs = new FileStream(Path.Combine(RuntimeVars.RootPath, "clsy.tmp"), isContinue ? FileMode.OpenOrCreate : FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                        {
                            targetToDelete = fs.Name;
                            this.mainProgressBar.Value = 0;
                            this.mainProgressBar.IsIndeterminate = false;
                            this.mainProgressText.Text = $"Downloading client v{updateVersion}";
                            isCompleted = await this.apiClient.DownloadFile(new Uri(downloadUrl), isContinue ? fs.Length : 0, fs, this.ProgressBarThingie);
                            await fs.FlushAsync();

                            if (isCompleted)
                            {
                                this.mainProgressText.Text = $"Unpacking client v{updateVersion}";
                                await Task.Run(() =>
                                {
                                    fs.Seek(0, SeekOrigin.Begin);
                                    using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
                                    {
                                        string destinationDir = Path.Combine(RuntimeVars.RootPath, "clsy");
                                        Directory.Delete(destinationDir, true);
                                        destinationDir = Directory.CreateDirectory(destinationDir).FullName;
                                        byte[] buffer = new byte[4096];
                                        foreach (var entry in zip.Entries)
                                        {
                                            string thePath = Path.Combine(destinationDir, entry.FullName);
                                            Directory.CreateDirectory(Path.GetDirectoryName(thePath));
                                            using (var destFs = File.Open(thePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                                            using (var stream = entry.Open())
                                            {
                                                destFs.SetLength(entry.Length);
                                                int read = stream.Read(buffer, 0, 4096);
                                                while (read > 0)
                                                {
                                                    destFs.Write(buffer, 0, read);
                                                    read = stream.Read(buffer, 0, 4096);
                                                }
                                                destFs.Flush();
                                            }
                                        }
                                    }
                                });
                                await File.WriteAllTextAsync(Path.Combine(RuntimeVars.RootPath, "clsy.version"), updateVersion.Trim());
                            }
                        }
                        if (isCompleted)
                        {
                            try
                            {
                                File.Delete(targetToDelete);
                            }
                            catch { }

                            await LaunchGame(width, height, localEmu);
                        }
                    }
                    else
                    {
                        await LaunchGame(width, height, localEmu);
                    }
                }
                else
                {
                    MessageBox.Show(this, $"Server response with code {responseCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (InvalidGameScreenSize ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            this.tabMainMenu.IsSelected = true;
        }

        private long theTotal = -1;
        private bool ProgressBarThingie(long current, long total)
        {
            if (this.cancelSrc.IsCancellationRequested)
            {
                return false;
            }
            this.Dispatcher.BeginInvoke(new ProgressBarUpdate(delegate
            {
                if (theTotal != total)
                {
                    theTotal = total;
                    this.mainProgressBar.Value = total;
                }
                this.mainProgressBar.Value = current;
            }));
            return true;
        }

        delegate void ProgressBarUpdate();

        private string GetClientVersion()
        {
            try
            {
                using (var fs = File.OpenText(Path.Combine(RuntimeVars.RootPath, "clsy.version")))
                {
                    // Only first line
                    return fs.ReadLine();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetLocaleEmulatorVersion(string dir)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(Path.Combine(dir, "LEVersion.xml"));
                return doc.SelectSingleNode("LEVersion").Attributes["Version"].Value;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string GetDownloadingClientVersion()
        {
            try
            {
                using (var fs = File.OpenText(Path.Combine(RuntimeVars.RootPath, "clsy.tmp.version")))
                {
                    // Only first line
                    return fs.ReadLine();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task LaunchGame(int clientWidth, int clientHeight, bool localeEmu)
        {
            if (clientWidth < 50 || clientHeight < 50)
            {
                throw new InvalidGameScreenSize();
            }
            this.mainProgressBar.IsIndeterminate = true;
            using (Process proc = new Process())
            {
                // LEProc.exe -runas 69015999-0232-4743-986c-09b557bf28d6 "E:\Entertainment\Blue Vows\苍蓝誓约\clsy\clsy.exe"
                // $"-runas 69015999-0232-4743-986c-09b557bf28d6 {Path.Combine(RuntimeVars.RootPath, "clsy", "clsy.exe")} -screen-width {clientWidth} -screen-height {clientHeight} - platform android"
                if (localeEmu)
                {
                    string workingDir = Path.Combine(RuntimeVars.RootPath, "LocaleEmulator");
                    proc.StartInfo.FileName = Path.Combine(workingDir, "LEProc.exe");
                    if (await Task.Run(() => File.Exists(proc.StartInfo.FileName)))
                    {
                        Task<string> t_localversion = Task.Run(() => this.GetLocaleEmulatorVersion(workingDir)),
                            t_remoteversion = this.apiClient.GetClient().GetStringAsync(BaseUrl + "/LocaleEmulator.zip");
                        string localversion = await t_localversion,
                            remoteversion = await t_remoteversion;
                        
                        if (!string.Equals(localversion, remoteversion.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            this.mainProgressText.Text = $"Updating Locale Emulator";
                            if (!await this.DownloadLocaleEmulator(workingDir))
                            {
                                throw new FileNotFoundException("Locale emulator not found");
                            }
                        }
                    }
                    else
                    {
                        this.mainProgressText.Text = $"Downloading Locale Emulator";
                        if (!await this.DownloadLocaleEmulator(workingDir))
                        {
                            throw new FileNotFoundException("Locale emulator not found");
                        }
                    }
                    proc.StartInfo.WorkingDirectory = workingDir;
                    proc.StartInfo.Arguments = $"-runas 69015999-0232-4743-986c-09b557bf28d6 \"{Path.Combine(RuntimeVars.RootPath, "clsy", "clsy.exe")}\" -screen-width {clientWidth} -screen-height {clientHeight} -platform android";
                }
                else
                {
                    string workingDir = Path.Combine(RuntimeVars.RootPath, "clsy");
                    proc.StartInfo.FileName = Path.Combine(workingDir, "clsy.exe");
                    proc.StartInfo.WorkingDirectory = workingDir;
                    proc.StartInfo.Arguments = $"-screen-width {clientWidth} -screen-height {clientHeight} -platform android";
                }
                this.mainProgressText.Text = $"Launching client";
                proc.Start();
                proc.WaitForExit(500);
            }
        }

        private async Task<bool> DownloadLocaleEmulator(string destination)
        {
            MemoryStream tmpStream = new MemoryStream();
            if (await this.apiClient.DownloadFile(new Uri(BaseUrl + "/LocaleEmulator.txt"), tmpStream, this.ProgressBarThingie))
            {
                tmpStream.Position = 0;
                using (var zipFile = new System.IO.Compression.ZipArchive(tmpStream, System.IO.Compression.ZipArchiveMode.Read))
                {
                    destination = Directory.CreateDirectory(destination).FullName;
                    byte[] buffer = new byte[4096];
                    foreach (var entry in zipFile.Entries)
                    {
                        string destFile = Path.Combine(destination, entry.FullName);
                        using (var fs = File.Create(destFile))
                        using (var entryStream = entry.Open())
                        {
                            int read = await entryStream.ReadAsync(buffer, 0, 4096);
                            while (read > 0)
                            {
                                await fs.WriteAsync(buffer, 0, read);
                                read = await entryStream.ReadAsync(buffer, 0, 4096);
                            }
                            await fs.FlushAsync();
                        }
                    }
                }
                return true;
            }
            else
            {
                tmpStream.Dispose();
                return false;
            }
        }

        class InvalidGameScreenSize : Exception
        {
            public InvalidGameScreenSize() : base("The screen size is too small") { }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.cancelSrc?.Cancel();
        }
    }
}
