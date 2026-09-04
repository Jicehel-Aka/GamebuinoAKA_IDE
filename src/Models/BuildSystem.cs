namespace GamebuinoAKA.IDE.Models
{
    /// <summary>
    /// Chaîne de build d'un projet.
    ///
    /// PlatformIO  : framework Arduino + lib jmp42/Gamebuino_AKA_lib, build via `pio`.
    ///               C'est la voie « communauté / débutant ».
    ///
    /// EspIdf      : ESP-IDF natif, composants CMake, coquille + core + shell,
    ///               build via `idf.py`. C'est la voie utilisée par les jeux de
    ///               Jicehel (ASTERIA, pAKAman, mAKArena…) : composant
    ///               `components/gamebuino`, tâches FreeRTOS, g_core, i18n 5 langues.
    /// </summary>
    public enum BuildSystem
    {
        PlatformIO = 0,
        EspIdf = 1
    }
}
