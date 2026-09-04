using System;
using System.Collections.Generic;

namespace GamebuinoAKA.IDE.Models
{
    public class AppSettings
    {
        public string WorkspaceFolder { get; set; } = string.Empty;

        // ── PlatformIO ─────────────────────────────────────────────────────────
        public string PlatformIOPath { get; set; } = string.Empty;
        public string GamebuinoLibRepoUrl { get; set; } = "https://github.com/jmp42/Gamebuino_AKA_lib";

        // ── ESP-IDF ────────────────────────────────────────────────────────────
        /// <summary>
        /// Chemin vers idf.py (ex. C:\Espressif\frameworks\esp-idf-vX\tools\idf.py)
        /// OU vers le script d'export (export.bat) qui met idf.py dans le PATH.
        /// Laisser vide pour se rabattre sur « idf.py » du PATH.
        /// </summary>
        public string IdfPyPath { get; set; } = string.Empty;

        /// <summary>
        /// Script d'environnement ESP-IDF à sourcer avant idf.py
        /// (ex. C:\Espressif\export.bat ou %IDF_PATH%\export.bat).
        /// Optionnel : si renseigné, les commandes sont lancées via ce script.
        /// </summary>
        public string IdfExportScript { get; set; } = string.Empty;

        /// <summary>Port série pour flash/monitor ESP-IDF (ex. COM5). Vide = auto.</summary>
        public string IdfSerialPort { get; set; } = string.Empty;

        /// <summary>
        /// Dossier « components/gamebuino » de référence à copier dans un nouveau
        /// projet ESP-IDF (la lib jmp42 vendorée dans tes dépôts). Typiquement
        /// &lt;un de tes projets&gt;\components\gamebuino. Vide = un README explicatif
        /// est écrit à la place.
        /// </summary>
        public string ReferenceGamebuinoComponentPath { get; set; } = string.Empty;

        // ── Éditeur / commun ────────────────────────────────────────────────────
        public string VSCodePath { get; set; } = string.Empty;
        public string Theme { get; set; } = "Dark";

        /// <summary>Chaîne de build proposée par défaut pour un nouveau projet.</summary>
        public BuildSystem DefaultBuildSystem { get; set; } = BuildSystem.EspIdf;

        /// <summary>Format d'export couleur par défaut (BGR565 AKA = correct pour la lib).</summary>
        public ColorFormat DefaultColorFormat { get; set; } = ColorFormat.Bgr565Aka;

        /// <summary>Couleur-clé de transparence par défaut (magenta 0xF81F).</summary>
        public ushort DefaultTransparentKey { get; set; } = 0xF81F;

        public List<string> RecentProjects { get; set; } = new List<string>();
        public int MaxRecentProjects { get; set; } = 10;
        public bool AutoDetectTools { get; set; } = true;

        public static string SettingsFilePath =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GamebuinoAKA",
                "settings.json");
    }
}
