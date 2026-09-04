using System.Collections.Generic;

namespace GamebuinoAKA.IDE.Models
{
    /// <summary>
    /// Fichier cible dans lequel le snippet doit être injecté.
    /// </summary>
    public enum SnippetTargetFile
    {
        /// <summary>main.cpp (Arduino) ou app_main.cpp (ESP-IDF) — corps de boucle principale.</summary>
        MainCpp,
        /// <summary>game.cpp — logique et rendu du jeu (PlatformIO game-template).</summary>
        GameCpp,
        /// <summary>game.h — déclarations (PlatformIO game-template).</summary>
        GameH,
        /// <summary>Nouveau fichier .h généré avec le nom du snippet.</summary>
        NewHeader,
        /// <summary>Nouveau fichier .cpp généré avec le nom du snippet.</summary>
        NewCpp
    }

    /// <summary>
    /// Un snippet de code C++ commenté, sélectionnable lors de la création d'un projet.
    /// Le squelette de base (includes, setup, loop…) est immuable ;
    /// les snippets viennent s'y greffer dans les zones marquées.
    /// </summary>
    public class CodeSnippet
    {
        // ── Identité ──────────────────────────────────────────────────────────────
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>Explication courte affichée sous le nom dans le sélecteur.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Explication longue avec exemples, affiché dans le panneau détail.</summary>
        public string Explanation { get; set; } = string.Empty;

        // ── Classement ────────────────────────────────────────────────────────────
        public string Category { get; set; } = string.Empty;

        /// <summary>Tags libres pour la recherche.</summary>
        public List<string> Tags { get; set; } = new();

        // ── Compatibilité ─────────────────────────────────────────────────────────
        /// <summary>true = compatible PlatformIO/Arduino.</summary>
        public bool ForPlatformIO { get; set; } = true;
        /// <summary>true = compatible ESP-IDF.</summary>
        public bool ForEspIdf { get; set; } = false;

        // ── Code ──────────────────────────────────────────────────────────────────
        /// <summary>Fichier dans lequel ce bloc doit être inséré.</summary>
        public SnippetTargetFile TargetFile { get; set; } = SnippetTargetFile.GameCpp;

        /// <summary>
        /// Code C++ complet du snippet, commenté.
        /// Les marqueurs spéciaux délimitent les zones d'injection :
        ///   //@@INCLUDES@@   → bloc d'includes en haut du fichier
        ///   //@@GLOBALS@@    → variables globales / constantes
        ///   //@@DECLARATIONS@@  → déclarations de fonctions (pour les .h)
        ///   //@@UPDATE@@     → corps de gameUpdate() ou de la boucle principale
        ///   //@@RENDER@@     → corps de gameRender() ou de l'affichage
        ///   //@@FUNCTIONS@@  → fonctions helper à ajouter en bas du fichier
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Dépendances : IDs de snippets devant être inclus automatiquement avec celui-ci.
        /// </summary>
        public List<string> RequiresSnippetIds { get; set; } = new();

        // ── Source ────────────────────────────────────────────────────────────────
        /// <summary>false = snippet intégré dans l'IDE (non modifiable) ; true = ajouté par l'utilisateur.</summary>
        public bool IsUserDefined { get; set; } = false;

        // ── Dérivés ───────────────────────────────────────────────────────────────
        public string CompatibilityLabel =>
            (ForPlatformIO && ForEspIdf) ? "PIO + IDF"
            : ForPlatformIO ? "PlatformIO"
            : ForEspIdf ? "ESP-IDF"
            : "—";
    }

    /// <summary>
    /// Banque de snippets personnalisés persistée dans le workspace.
    /// Les snippets intégrés à l'IDE ne sont PAS stockés ici.
    /// </summary>
    public class SnippetBank
    {
        public int Version { get; set; } = 1;
        public List<CodeSnippet> UserSnippets { get; set; } = new();
    }
}
