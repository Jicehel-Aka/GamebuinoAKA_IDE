using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.IDE.Models;
using Newtonsoft.Json.Linq;

namespace GamebuinoAKA.IDE.Services
{
    /// <summary>
    /// Pilote ESP-IDF via idf.py + auto-détection de l'installation.
    /// </summary>
    public class EspIdfService
    {
        private readonly SettingsService _settings;

        public EspIdfService(SettingsService settings)
        {
            _settings = settings;
        }

        // ── Build / Flash / Monitor ──────────────────────────────────────────────

        public Task BuildAsync(GamebuinoProject p, Action<string>? onOutput, CancellationToken ct = default)
            => RunIdfAsync(p, "build", onOutput, ct);

        public Task FlashAsync(GamebuinoProject p, Action<string>? onOutput, CancellationToken ct = default)
            => RunIdfAsync(p, $"{PortArg()}flash", onOutput, ct);

        public Task MonitorAsync(GamebuinoProject p, Action<string>? onOutput, CancellationToken ct = default)
            => RunIdfAsync(p, $"{PortArg()}monitor", onOutput, ct);

        public Task CleanAsync(GamebuinoProject p, Action<string>? onOutput, CancellationToken ct = default)
            => RunIdfAsync(p, "fullclean", onOutput, ct);

        public async Task<string> GetVersionAsync()
        {
            try
            {
                var sb = new StringBuilder();
                await RunRawAsync("--version", null, line => sb.AppendLine(line), CancellationToken.None);
                var v = sb.ToString().Trim();
                return string.IsNullOrEmpty(v) ? "Non détecté" : v;
            }
            catch { return "Non détecté"; }
        }

        // ── Auto-détection de l'installation ESP-IDF ─────────────────────────────

        /// <summary>
        /// Cherche une install ESP-IDF et renvoie (export.bat, idf.py).
        /// Ordre : esp_idf.json (extension VS Code) → var IDF_PATH → dossiers usuels.
        /// Une chaîne vide signifie « non trouvé ».
        /// </summary>
        public (string exportScript, string idfPy) DetectInstall()
        {
            foreach (var idfDir in EnumerateIdfCandidates())
            {
                if (string.IsNullOrWhiteSpace(idfDir) || !Directory.Exists(idfDir)) continue;
                var idfPy = Path.Combine(idfDir, "tools", "idf.py");
                if (!File.Exists(idfPy)) continue;
                var export = Path.Combine(idfDir, "export.bat");
                return (File.Exists(export) ? export : "", idfPy);
            }
            return ("", "");
        }

        private IEnumerable<string> EnumerateIdfCandidates()
        {
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var toolsPath = Environment.GetEnvironmentVariable("IDF_TOOLS_PATH"); // ex. C:\ESP-IDF

            // 1) Fichiers esp_idf.json possibles. Il vit à la RACINE d'IDF_TOOLS_PATH
            //    (donc C:\ESP-IDF\esp_idf.json chez toi), ou dans ~/.espressif.
            var jsonCandidates = new List<string>();
            if (!string.IsNullOrEmpty(toolsPath)) jsonCandidates.Add(Path.Combine(toolsPath, "esp_idf.json"));
            jsonCandidates.Add(Path.Combine(user, ".espressif", "esp_idf.json"));
            jsonCandidates.Add(@"C:\ESP-IDF\esp_idf.json");
            jsonCandidates.Add(@"C:\Espressif\esp_idf.json");
            foreach (var json in jsonCandidates)
                foreach (var p in ReadInstallPathsFromJson(json))
                    yield return p;

            // 2) Variable d'environnement IDF_PATH.
            var env = Environment.GetEnvironmentVariable("IDF_PATH");
            if (!string.IsNullOrEmpty(env)) yield return env;

            // 3) Dossiers "frameworks" usuels → sous-dossiers esp-idf* (récent d'abord).
            var frameworkRoots = new List<string>();
            if (!string.IsNullOrEmpty(toolsPath)) frameworkRoots.Add(Path.Combine(toolsPath, "frameworks"));
            frameworkRoots.Add(@"C:\ESP-IDF\frameworks");
            frameworkRoots.Add(@"C:\Espressif\frameworks");
            frameworkRoots.Add(Path.Combine(user, "Espressif", "frameworks"));
            frameworkRoots.Add(Path.Combine(user, "esp"));
            frameworkRoots.Add(@"C:\esp");
            foreach (var root in frameworkRoots)
            {
                if (!Directory.Exists(root)) continue;
                string[] dirs;
                try { dirs = Directory.GetDirectories(root, "esp-idf*"); }
                catch { continue; }
                foreach (var d in dirs.OrderByDescending(x => x))
                    yield return d;
            }

            // 4) Chemins directs.
            yield return Path.Combine(user, "esp", "esp-idf");
            yield return @"C:\esp\esp-idf";
        }

        /// <summary>
        /// Extrait les chemins d'installation d'un esp_idf.json (idfInstalled[*].path).
        /// Repli : toute propriété "path" pointant vers un esp-idf.
        /// </summary>
        private IEnumerable<string> ReadInstallPathsFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) yield break;
            var paths = new List<string>();
            try
            {
                var root = JObject.Parse(File.ReadAllText(jsonPath));
                var selected = (string?)root["idfSelectedId"];
                if (root["idfInstalled"] is JObject installed)
                {
                    if (selected != null && (string?)installed[selected]?["path"] is string sp) paths.Add(sp);
                    foreach (var prop in installed.Properties())
                        if ((string?)prop.Value["path"] is string p) paths.Add(p);
                }
                if (paths.Count == 0)
                    foreach (var t in root.Descendants().OfType<JProperty>())
                        if (t.Name == "path" && (string?)t.Value is string pv &&
                            pv.IndexOf("esp-idf", StringComparison.OrdinalIgnoreCase) >= 0)
                            paths.Add(pv);
            }
            catch { /* json illisible : on ignore */ }
            foreach (var p in paths) yield return p;
        }

        /// <summary>Ports COM disponibles (pour flash/monitor).</summary>
        public string[] DetectSerialPorts()
        {
            try
            {
                return System.IO.Ports.SerialPort.GetPortNames()
                    .Distinct()
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        // ── Interne ──────────────────────────────────────────────────────────────

        private string PortArg()
        {
            var port = _settings.Settings.IdfSerialPort;
            return string.IsNullOrWhiteSpace(port) ? "" : $"-p {port} ";
        }

        private Task RunIdfAsync(GamebuinoProject p, string idfArgs, Action<string>? onOutput, CancellationToken ct)
        {
            var args = $"-C \"{p.FolderPath}\" {idfArgs}";
            return RunRawAsync(args, p.FolderPath, onOutput, ct);
        }

        private async Task<int> RunRawAsync(string idfArgs, string? workingDir,
            Action<string>? onOutput, CancellationToken ct)
        {
            var psi = BuildPsi(idfArgs, workingDir);
            bool sawEnvIssue = false;

            void Handle(string line)
            {
                if (line.IndexOf("virtual environment", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("run the install script", StringComparison.OrdinalIgnoreCase) >= 0
                    || (line.IndexOf("idf.py", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (line.IndexOf("reconnu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         line.IndexOf("not recognized", StringComparison.OrdinalIgnoreCase) >= 0)))
                    sawEnvIssue = true;
                onOutput?.Invoke(line);
            }

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Handle(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Handle(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);
            int code = process.ExitCode;

            if (code == 0)
            {
                onOutput?.Invoke("\n[ESP-IDF] Terminé avec succès (exit 0).");
            }
            else
            {
                onOutput?.Invoke($"\n[ESP-IDF] Échec (exit {code}).");
                if (sawEnvIssue)
                    onOutput?.Invoke(
                        "[ESP-IDF] Astuce : l'environnement Python d'ESP-IDF n'est pas installé " +
                        "(dossier python_env absent). Lance UNE fois le script d'installation, " +
                        "p.ex. install.bat à la racine de ton esp-idf " +
                        "(C:\\ESP-IDF\\frameworks\\esp-idf-v5.5.1\\install.bat), ou « ESP-IDF: Install » " +
                        "dans l'extension VS Code, puis réessaie.");
            }
            return code;
        }

        /// <summary>
        /// Localise le venv Python réellement installé (…\python_env\idf*_env), pour
        /// éviter qu'export.bat ne recalcule un nom de venv basé sur le mauvais python.
        /// </summary>
        private static string? FindPythonEnv(string exportScript)
        {
            try
            {
                // IDF_TOOLS_PATH explicite, sinon déduit du chemin de export.bat :
                //   <root>\frameworks\esp-idf-*\export.bat  →  root = IDF_TOOLS_PATH
                var toolsPath = Environment.GetEnvironmentVariable("IDF_TOOLS_PATH");
                if (string.IsNullOrEmpty(toolsPath) && !string.IsNullOrEmpty(exportScript))
                {
                    var idfDir = Path.GetDirectoryName(exportScript);      // ...\esp-idf-vX
                    var frameworks = Path.GetDirectoryName(idfDir);        // ...\frameworks
                    var root = Path.GetDirectoryName(frameworks);          // ...\ (root)
                    if (root != null) toolsPath = root;
                }
                if (string.IsNullOrEmpty(toolsPath)) return null;

                var venvRoot = Path.Combine(toolsPath, "python_env");
                if (!Directory.Exists(venvRoot)) return null;

                var envs = Directory.GetDirectories(venvRoot, "idf*_env");
                if (envs.Length == 1) return envs[0];
                foreach (var e in envs.OrderByDescending(x => x))
                    if (File.Exists(Path.Combine(e, "Scripts", "python.exe")))
                        return e;
                return null;
            }
            catch { return null; }
        }

        private ProcessStartInfo BuildPsi(string idfArgs, string? workingDir)
        {
            var export = _settings.Settings.IdfExportScript;
            var idfPy = string.IsNullOrWhiteSpace(_settings.Settings.IdfPyPath)
                ? "idf.py"
                : _settings.Settings.IdfPyPath;

            string fileName;
            string arguments;

            if (!string.IsNullOrWhiteSpace(export) && File.Exists(export))
            {
                fileName = "cmd.exe";
                // export.bat recalcule le nom du venv (idf<ver>_py<pyver>_env) d'après le
                // python du PATH. Si ce python diffère de celui qui a créé le venv, il
                // cherche au mauvais endroit. On impose donc le venv réellement installé.
                var venv = FindPythonEnv(export);
                var pre = venv != null ? $"set \"IDF_PYTHON_ENV_PATH={venv}\" && " : "";
                arguments = $"/c {pre}call \"{export}\" && idf.py {idfArgs}";
            }
            else if (idfPy.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "python";
                arguments = $"\"{idfPy}\" {idfArgs}";
            }
            else
            {
                fileName = idfPy;
                arguments = idfArgs;
            }

            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDir ?? "",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }
    }
}
