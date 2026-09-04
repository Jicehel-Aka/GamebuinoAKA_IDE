using System;
using System.Collections.Generic;
using System.IO;

namespace GamebuinoAKA.IDE.Models
{
    public class GamebuinoProject
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string Template { get; set; } = "empty";

        /// <summary>Chaîne de build du projet (PlatformIO ou ESP-IDF).</summary>
        public BuildSystem BuildSystem { get; set; } = BuildSystem.PlatformIO;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<string> RecentFiles { get; set; } = new List<string>();

        // ── Dérivés ──────────────────────────────────────────────────────────────
        public string PlatformIniPath => Path.Combine(FolderPath, "platformio.ini");
        public string CMakeListsPath => Path.Combine(FolderPath, "CMakeLists.txt");
        public string SrcPath => Path.Combine(FolderPath, "src");
        public string MainPath => Path.Combine(FolderPath, "main");

        public bool IsPlatformIO => BuildSystem == BuildSystem.PlatformIO;
        public bool IsEspIdf => BuildSystem == BuildSystem.EspIdf;

        /// <summary>Libellé lisible de la chaîne, pour l'UI.</summary>
        public string BuildSystemLabel => IsEspIdf ? "ESP-IDF" : "PlatformIO";

        public bool IsValid =>
            !string.IsNullOrEmpty(FolderPath) &&
            (File.Exists(PlatformIniPath) || File.Exists(CMakeListsPath));

        /// <summary>Nom du fichier marqueur pour forcer la chaîne d'un projet.</summary>
        public const string BuildMarkerFile = ".aka-build";

        /// <summary>
        /// Détecte la chaîne de build d'un dossier existant.
        /// Priorité :
        ///  1. marqueur explicite « .aka-build » (contenu : espidf / platformio) ;
        ///  2. platformio.ini présent → PlatformIO (marqueur DÉCISIF : un projet
        ///     ESP-IDF pur n'en a jamais ; un projet PlatformIO ESP32 a souvent
        ///     AUSSI un CMakeLists.txt, d'où l'ancienne confusion) ;
        ///  3. CMakeLists.txt avec marqueurs IDF (project.cmake / idf_component_register
        ///     / dossier main) → ESP-IDF ;
        ///  4. sinon, la valeur par défaut fournie.
        /// </summary>
        public static BuildSystem DetectBuildSystem(string folder,
            BuildSystem fallback = BuildSystem.PlatformIO)
        {
            // 1) Override explicite posé par l'utilisateur / l'IDE.
            try
            {
                var marker = Path.Combine(folder, BuildMarkerFile);
                if (File.Exists(marker))
                {
                    var v = File.ReadAllText(marker).Trim().ToLowerInvariant();
                    if (v.Contains("idf")) return BuildSystem.EspIdf;
                    if (v.Contains("pio") || v.Contains("platformio")) return BuildSystem.PlatformIO;
                }
            }
            catch { /* ignore */ }

            // 2) platformio.ini est décisif.
            if (File.Exists(Path.Combine(folder, "platformio.ini")))
                return BuildSystem.PlatformIO;

            // 3) Marqueurs ESP-IDF.
            var cmake = Path.Combine(folder, "CMakeLists.txt");
            if (File.Exists(cmake))
            {
                try
                {
                    var txt = File.ReadAllText(cmake);
                    if (txt.Contains("project.cmake") || txt.Contains("idf_component_register") ||
                        Directory.Exists(Path.Combine(folder, "main")))
                        return BuildSystem.EspIdf;
                }
                catch { /* ignore */ }
            }

            // 4) Défaut.
            return fallback;
        }

        /// <summary>Écrit le marqueur « .aka-build » pour figer la chaîne du projet.</summary>
        public void WriteBuildMarker()
        {
            try
            {
                File.WriteAllText(Path.Combine(FolderPath, BuildMarkerFile),
                    IsEspIdf ? "espidf" : "platformio");
            }
            catch { /* best-effort */ }
        }
    }
}
