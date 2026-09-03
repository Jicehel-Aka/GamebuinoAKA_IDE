using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GamebuinoAKA.IDE.Services
{
    /// <summary>
    /// Clones or updates a git repository.
    /// Requires git to be available on the PATH (standard on all developer machines).
    /// </summary>
    public class GitService
    {
        private readonly SettingsService _settingsService;

        public GitService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>Returns true if git is available on PATH.</summary>
        public async Task<bool> IsInstalledAsync()
        {
            try
            {
                await RunGitAsync("--version", null, null, CancellationToken.None);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clones <paramref name="repoUrl"/> into
        /// <c>&lt;workspace&gt;/&lt;folderName&gt;</c>.
        /// Streams stdout/stderr to <paramref name="onOutput"/>.
        /// Returns the full path of the cloned folder.
        /// </summary>
        public async Task<string> CloneAsync(string repoUrl, string? folderName,
            Action<string>? onOutput, CancellationToken ct = default)
        {
            var workspace = _settingsService.Settings.WorkspaceFolder;
            if (string.IsNullOrEmpty(workspace))
                throw new InvalidOperationException(
                    "Dossier workspace non configuré. Allez dans Paramètres.");

            Directory.CreateDirectory(workspace);

            if (string.IsNullOrWhiteSpace(folderName))
                folderName = ExtractRepoName(repoUrl);

            var destination = Path.Combine(workspace, folderName);
            if (Directory.Exists(destination))
                throw new InvalidOperationException(
                    $"Le dossier « {folderName} » existe déjà dans le workspace.");

            onOutput?.Invoke($"Clonage de {repoUrl} → {destination}");
            await RunGitStreamAsync($"clone \"{repoUrl}\" \"{destination}\"", workspace, onOutput, ct);

            if (!Directory.Exists(destination))
                throw new InvalidOperationException(
                    "Le clonage a échoué : le dossier de destination n'a pas été créé.");

            return destination;
        }

        /// <summary>
        /// Infers a valid project folder name from a GitHub URL.
        /// e.g. https://github.com/user/my-repo  →  my-repo
        /// </summary>
        public static string ExtractRepoName(string url)
        {
            url = url.TrimEnd('/');
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                url = url[..^4];
            var parts = url.Split('/');
            var name = parts[^1];
            // Replace hyphens and spaces with underscores for C++ folder sanity
            return string.IsNullOrEmpty(name) ? "repo" : name;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static async Task<string> RunGitAsync(string args, string? workingDir,
            Action<string>? output, CancellationToken ct)
        {
            var result = new StringBuilder();
            var psi = BuildPsi(args, workingDir);
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) { result.AppendLine(e.Data); output?.Invoke(e.Data); } };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) { result.AppendLine(e.Data); output?.Invoke(e.Data); } };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);
            return result.ToString();
        }

        private static async Task RunGitStreamAsync(string args, string? workingDir,
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

        private static ProcessStartInfo BuildPsi(string args, string? workingDir) =>
            new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = workingDir ?? "",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
    }
}
