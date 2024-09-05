using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace cvm_updater
{
    internal class Program
    {

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public class Release
        {
            public string Version { get; set; }
            public string ChangeLog { get; set; }
            public string DownloadLink { get; set; }
        }

        static async Task Main(string[] args)
        {

            // Hide the console window
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            string programFilesX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string classicVolumeMixerPath = System.IO.Path.Combine(programFilesX86Path, "Classic Volume Mixer", "ClassicVolumeMixer.exe");

            // Version info
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(classicVolumeMixerPath);
            string currentVersion = versionInfo.FileVersion;
            Release latestVersion = await GetLatestVersionAsync();
            bool newVersionAvailable = new Version(latestVersion.Version).CompareTo(new Version(currentVersion)) > 0;

            // Default popup values
            string messageBoxCaption = "Classic Volume Mixer | Update checker";
            string messageBoxText = "You are already using the latest version of Classic Volume Mixer";
            MessageBoxButton messageBoxButton = MessageBoxButton.OK;


            if (newVersionAvailable)
            {
                messageBoxText = $"There is a new version available.\n\nCurrent version: {currentVersion}\nLatest version: {latestVersion.Version}\n\nDo you want to install the new version?";
                messageBoxButton = MessageBoxButton.YesNo;
            }

            MessageBoxResult result = MessageBox.Show(messageBoxText, messageBoxCaption, messageBoxButton);
            if (result == MessageBoxResult.Yes)
            {
                var urlToDownload = latestVersion.DownloadLink;
                string downloadPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClassicVolumeMixer", "setup.exe");

                // Ensure the directory exists
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(downloadPath));

                // Download the latest volume mixer
                using (HttpClient client = new HttpClient())
                {
                    using (HttpResponseMessage response = await client.GetAsync(urlToDownload, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        {
                            using (var fileStream = new System.IO.FileStream(downloadPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true))
                            {
                                await contentStream.CopyToAsync(fileStream);
                            }
                        }
                    }
                }

                // Close the current classic volume mixer
                var processes = Process.GetProcessesByName("ClassicVolumeMixer");
                foreach (var process in processes)
                {
                    process.Kill();
                    process.WaitForExit();
                }

                // Install classic volume mixer
                var installerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = downloadPath,
                        Arguments = "/SILENT",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                installerProcess.Start();
                installerProcess.WaitForExit();

                // Start classic volume mixer
                System.Diagnostics.Process.Start(classicVolumeMixerPath);

                // Popup message update complete
                MessageBox.Show("Update complete!", "Classic Volume Mixer | Update checker", MessageBoxButton.OK);
            }
            else
            {
                // No update, closing

            }
        }

        private static async Task<Release> GetLatestVersionAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                string url = "https://api.github.com/repos/popeen/classic-volume-mixer/releases";
                client.DefaultRequestHeaders.Add("User-Agent", "cvm-updater");
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(jsonResponse);

                var setupExeAsset = ((IEnumerable<dynamic>)json[0].assets)
             .FirstOrDefault(asset => ((string)asset.name).Equals("setup.exe", StringComparison.OrdinalIgnoreCase));

                if (setupExeAsset == null)
                {
                    throw new Exception("setup.exe not found in the latest release assets.");
                }

                return new Release
                {
                    Version = json[0].tag_name,
                    ChangeLog = json[0].html_url,
                    DownloadLink = setupExeAsset.browser_download_url
                };

            }
        }

    }
}
