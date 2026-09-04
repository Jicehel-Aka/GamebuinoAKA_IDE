using System;
using System.Collections.Generic;

namespace GamebuinoAKA.IDE.Models
{
    /// <summary>Type de ressource audio.</summary>
    public enum SoundAssetType
    {
        /// <summary>Effet sonore WAV (.wav) ou table FX (.h contenant WAV_SYSTEM / fx_system).</summary>
        SoundFx,
        /// <summary>Musique PMF (.pmf) ou tracker (.h contenant PMF data).</summary>
        Music
    }

    /// <summary>
    /// Représente un asset audio dans la banque sonore.
    /// Peut provenir d'un projet Gamebuino AKA (scan) ou être ajouté manuellement.
    /// </summary>
    public class SoundAsset
    {
        // ── Identité ──────────────────────────────────────────────────────────────
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // ── Classement ───────────────────────────────────────────────────────────
        public SoundAssetType AssetType { get; set; } = SoundAssetType.SoundFx;

        /// <summary>Thème / catégorie libre (ex. "Gameplay", "Ambiance", "UI").</summary>
        public string Theme { get; set; } = string.Empty;

        /// <summary>Tags libres (ex. "explosion", "loop", "8bit").</summary>
        public List<string> Tags { get; set; } = new List<string>();

        // ── Fichier source ────────────────────────────────────────────────────────
        /// <summary>Chemin absolu vers le fichier audio source (.wav, .pmf, .h, …).</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Extension du fichier source (lowercase, avec point).</summary>
        public string FileExtension { get; set; } = string.Empty;

        /// <summary>
        /// Chemin absolu vers le fichier WAV de prévisualisation.
        /// Pour les .wav : identique à FilePath.
        /// Pour les .pmf / .h : chemin d'un WAV converti/généré temporairement (ou vide).
        /// </summary>
        public string PreviewWavPath { get; set; } = string.Empty;

        // ── Provenance ────────────────────────────────────────────────────────────
        /// <summary>Nom du projet d'où cet asset a été importé (vide si ajouté manuellement).</summary>
        public string SourceProject { get; set; } = string.Empty;

        /// <summary>Date d'ajout dans la banque.</summary>
        public DateTime AddedAt { get; set; } = DateTime.Now;

        // ── Metadata ──────────────────────────────────────────────────────────────
        /// <summary>Durée estimée en secondes (0 si inconnue).</summary>
        public double DurationSeconds { get; set; }

        /// <summary>Taille du fichier en octets.</summary>
        public long FileSizeBytes { get; set; }

        // ── Dérivés ───────────────────────────────────────────────────────────────
        /// <summary>Libellé lisible du type.</summary>
        public string TypeLabel => AssetType == SoundAssetType.Music ? "Musique" : "Son FX";

        /// <summary>Icône Unicode selon le type.</summary>
        public string TypeIcon => AssetType == SoundAssetType.Music ? "🎵" : "🔊";

        /// <summary>Durée formatée mm:ss.</summary>
        public string DurationLabel => DurationSeconds > 0
            ? $"{(int)DurationSeconds / 60:D2}:{(int)DurationSeconds % 60:D2}"
            : "--:--";

        /// <summary>Taille formatée.</summary>
        public string SizeLabel => FileSizeBytes >= 1024
            ? $"{FileSizeBytes / 1024.0:F1} Ko"
            : $"{FileSizeBytes} o";

        /// <summary>Concatène les tags pour affichage.</summary>
        public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : string.Empty;
    }
}
