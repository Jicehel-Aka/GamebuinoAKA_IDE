using System.Collections.Generic;

namespace GamebuinoAKA.IDE.Models
{
    /// <summary>
    /// Contenu persisté de la banque audio (fichier .aka-soundbank.json
    /// à la racine du workspace ou du projet).
    /// </summary>
    public class SoundBank
    {
        public int Version { get; set; } = 1;
        public List<SoundAsset> Assets { get; set; } = new List<SoundAsset>();

        /// <summary>Thèmes personnalisés déclarés explicitement (en plus de ceux inférés).</summary>
        public List<string> CustomThemes { get; set; } = new List<string>();
    }
}
