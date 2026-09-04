using System;
using System.IO;
using System.Text;

namespace GamebuinoAKA.IDE.Services
{
    /// <summary>
    /// Journal fichier simple et thread-safe. Statique pour être appelable de
    /// partout (gestionnaires d'exceptions globaux compris) sans passer par la DI.
    /// Le fichier peut être ouvert ou supprimé depuis les Paramètres.
    /// </summary>
    public static class Log
    {
        private static readonly object _lock = new object();
        private const long MaxBytes = 1_000_000; // ~1 Mo : au-delà, on repart propre.

        /// <summary>Dossier du journal (même racine que les paramètres).</summary>
        public static string LogFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GamebuinoAKA");

        /// <summary>Chemin complet du fichier journal.</summary>
        public static string LogFilePath => Path.Combine(LogFolder, "gamebuino-ide.log");

        public static void Info(string message) => Write("INFO", message, null);
        public static void Warn(string message) => Write("WARN", message, null);
        public static void Error(string message, Exception? ex = null) => Write("ERREUR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(LogFolder);

                    // Rotation basique : si le fichier dépasse la taille max, on
                    // le renomme en .old (un seul historique) et on repart.
                    var path = LogFilePath;
                    if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    {
                        var old = path + ".old";
                        try { if (File.Exists(old)) File.Delete(old); File.Move(path, old); }
                        catch { try { File.Delete(path); } catch { /* ignore */ } }
                    }

                    var sb = new StringBuilder();
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                      .Append(" [").Append(level).Append("] ")
                      .Append(message);
                    if (ex != null)
                        sb.Append(Environment.NewLine).Append(ex);
                    sb.Append(Environment.NewLine);

                    File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                }
                catch { /* la journalisation ne doit jamais faire échouer l'appli */ }
            }
        }

        /// <summary>Supprime le journal (et son historique). Renvoie true si OK.</summary>
        public static bool Clear()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
                    var old = LogFilePath + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    return true;
                }
                catch { return false; }
            }
        }
    }
}
