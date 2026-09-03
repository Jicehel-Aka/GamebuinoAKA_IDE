using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.IDE.Models;

namespace GamebuinoAKA.IDE.Services
{
    public class PlatformIOService
    {
        private readonly SettingsService _settingsService;

        public PlatformIOService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<string> GetVersionAsync()
        {
            try
            {
                var output = await RunPioAsync("--version", null, null, CancellationToken.None);
                return output.Trim().Replace("PlatformIO, version ", "").Trim();
            }
            catch
            {
                return "Non détecté";
            }
        }

        public async Task<bool> IsInstalledAsync()
        {
            var version = await GetVersionAsync();
            return version != "Non détecté";
        }

        public async Task BuildAsync(GamebuinoProject project,
            Action<string>? onOutput, CancellationToken ct = default)
        {
            await RunPioStreamAsync("run", project.FolderPath, onOutput, ct);
        }

        public async Task FlashAsync(GamebuinoProject project,
            Action<string>? onOutput, CancellationToken ct = default)
        {
            await RunPioStreamAsync("run --target upload", project.FolderPath, onOutput, ct);
        }

        public async Task MonitorAsync(GamebuinoProject project,
            Action<string>? onOutput, CancellationToken ct = default)
        {
            await RunPioStreamAsync("device monitor", project.FolderPath, onOutput, ct);
        }

        public async Task CleanAsync(GamebuinoProject project,
            Action<string>? onOutput, CancellationToken ct = default)
        {
            await RunPioStreamAsync("run --target clean", project.FolderPath, onOutput, ct);
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private string GetPioExecutable()
        {
            var configured = _settingsService.Settings.PlatformIOPath;
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
                return configured;

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var candidates = new[]
            {
                // Standard PlatformIO Core install (via extension or standalone)
                Path.Combine(home, ".platformio", "penv", "Scripts", "pio.exe"),
                // PlatformIO installs inside the VS Code extension bundle (Windows)
                Path.Combine(localApp, "Programs", "Microsoft VS Code", "resources", "app",
                    "extensions", "platformio.platformio-ide", "piocore-installer",
                    "penv", "Scripts", "pio.exe"),
                // Python pip global install (Python in PATH)
                Path.Combine(home, "AppData", "Roaming", "Python", "Scripts", "pio.exe"),
                // Python 3.x user install
                Path.Combine(home, "AppData", "Local", "Programs", "Python",
                    "Python311", "Scripts", "pio.exe"),
                Path.Combine(home, "AppData", "Local", "Programs", "Python",
                    "Python312", "Scripts", "pio.exe"),
                Path.Combine(home, "AppData", "Local", "Programs", "Python",
                    "Python310", "Scripts", "pio.exe"),
                Path.Combine(home, "AppData", "Local", "Programs", "Python",
                    "Python39", "Scripts", "pio.exe"),
                // pipx install
                Path.Combine(localApp, "pipx", "venvs", "platformio", "Scripts", "pio.exe"),
                // Scoop
                Path.Combine(home, "scoop", "shims", "pio.exe"),
                // Chocolatey
                @"C:\ProgramData\chocolatey\bin\pio.exe",
                // Last resort: rely on PATH
                "pio",
            };

            foreach (var c in candidates)
                if (c == "pio" || File.Exists(c)) return c;

            return "pio";
        }

        /// <summary>
        /// Scans all known candidate paths and returns the first one found,
        /// or an empty string if none exist on disk.
        /// Used by the Settings page "Auto-detect" to populate the path field.
        /// </summary>
        public string DetectPioPath()
        {
            var exe = GetPioExecutable();
            // "pio" means we only have the PATH fallback — return empty so the UI
            // shows the user that no concrete path was found.
            return exe == "pio" ? string.Empty : exe;
        }

        private async Task<string> RunPioAsync(string args, string? workingDir,
            Action<string>? output, CancellationToken ct)
        {
            var result = new StringBuilder();
            var psi = BuildPsi(args, workingDir);

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                result.AppendLine(e.Data);
                output?.Invoke(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                result.AppendLine(e.Data);
                output?.Invoke(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);
            return result.ToString();
        }

        private async Task RunPioStreamAsync(string args, string? workingDir,
            Action<string>? onOutput, CancellationToken ct)
        {
            var psi = BuildPsi(args, workingDir);
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);
        }

        private ProcessStartInfo BuildPsi(string args, string? workingDir) => new ProcessStartInfo
        {
            FileName = GetPioExecutable(),
            Arguments = args,
            WorkingDirectory = workingDir ?? "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}
