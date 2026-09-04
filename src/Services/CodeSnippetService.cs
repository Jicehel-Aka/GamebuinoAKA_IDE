using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GamebuinoAKA.IDE.Models;
using Newtonsoft.Json;

namespace GamebuinoAKA.IDE.Services
{
    /// <summary>
    /// Fournit le catalogue complet de snippets (intégrés + utilisateur)
    /// et génère le code source final en combinant le squelette immuable
    /// avec les snippets sélectionnés.
    /// </summary>
    public class CodeSnippetService
    {
        private const string BankFileName = ".aka-snippets.json";
        private readonly SettingsService _settings;

        public CodeSnippetService(SettingsService settings)
        {
            _settings = settings;
        }

        // ── Banque utilisateur ────────────────────────────────────────────────────

        private string BankFilePath =>
            Path.Combine(_settings.Settings.WorkspaceFolder ?? string.Empty, BankFileName);

        public SnippetBank LoadUserBank()
        {
            try
            {
                if (File.Exists(BankFilePath))
                    return JsonConvert.DeserializeObject<SnippetBank>(
                               File.ReadAllText(BankFilePath)) ?? new SnippetBank();
            }
            catch { }
            return new SnippetBank();
        }

        public void SaveUserBank(SnippetBank bank)
        {
            try { File.WriteAllText(BankFilePath, JsonConvert.SerializeObject(bank, Formatting.Indented)); }
            catch { }
        }

        // ── Catalogue complet ─────────────────────────────────────────────────────

        /// <summary>
        /// Renvoie tous les snippets disponibles : intégrés + ceux de l'utilisateur.
        /// Filtre optionnel par système de build.
        /// </summary>
        public List<CodeSnippet> GetAll(BuildSystem? forBuild = null)
        {
            var all = new List<CodeSnippet>(BuiltinSnippets());
            all.AddRange(LoadUserBank().UserSnippets);

            if (forBuild == BuildSystem.PlatformIO)
                return all.Where(s => s.ForPlatformIO).ToList();
            if (forBuild == BuildSystem.EspIdf)
                return all.Where(s => s.ForEspIdf).ToList();
            return all;
        }

        // ── Génération de code ────────────────────────────────────────────────────

        /// <summary>
        /// Génère un dictionnaire filename → contenu à partir des snippets sélectionnés.
        /// Les fichiers retournés viennent COMPLÉTER (ou remplacer pour game.cpp/game.h)
        /// le squelette créé par TemplateService.
        /// </summary>
        public Dictionary<string, string> GenerateFiles(
            IEnumerable<CodeSnippet> selected,
            BuildSystem buildSystem,
            string projectName)
        {
            var snippets = ResolveDependencies(selected.ToList(), buildSystem);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (buildSystem == BuildSystem.PlatformIO)
                GeneratePlatformIO(snippets, projectName, result);
            else
                GenerateEspIdf(snippets, projectName, result);

            return result;
        }

        // ── PlatformIO ────────────────────────────────────────────────────────────

        private static void GeneratePlatformIO(
            List<CodeSnippet> snippets, string projectName,
            Dictionary<string, string> result)
        {
            // Regroupe par fichier cible
            var forGameCpp   = snippets.Where(s => s.TargetFile == SnippetTargetFile.GameCpp).ToList();
            var forGameH     = snippets.Where(s => s.TargetFile == SnippetTargetFile.GameH).ToList();
            var forMain      = snippets.Where(s => s.TargetFile == SnippetTargetFile.MainCpp).ToList();
            var forNewH      = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewHeader).ToList();
            var forNewCpp    = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewCpp).ToList();

            if (forGameCpp.Count > 0 || forGameH.Count > 0)
            {
                result["src/game.h"]   = BuildGameH(forGameH, projectName);
                result["src/game.cpp"] = BuildGameCpp(forGameCpp, projectName);
                // main.cpp qui inclut game.h
                result["src/main.cpp"] = BuildMainCppWithGame(forMain, projectName);
            }
            else if (forMain.Count > 0)
            {
                result["src/main.cpp"] = BuildMainCppInline(forMain, projectName);
            }

            foreach (var s in forNewH)
                result[$"src/{Sanitize(s.Name)}.h"] = s.Code;
            foreach (var s in forNewCpp)
                result[$"src/{Sanitize(s.Name)}.cpp"] = s.Code;
        }

        // ── ESP-IDF ───────────────────────────────────────────────────────────────

        private static void GenerateEspIdf(
            List<CodeSnippet> snippets, string projectName,
            Dictionary<string, string> result)
        {
            var forMain  = snippets.Where(s => s.TargetFile == SnippetTargetFile.MainCpp).ToList();
            var forNewH  = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewHeader).ToList();
            var forNewCpp = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewCpp).ToList();

            if (forMain.Count > 0)
                result["main/app_main.cpp"] = BuildAppMain(forMain, projectName);

            foreach (var s in forNewH)
                result[$"main/{Sanitize(s.Name)}.h"] = s.Code;
            foreach (var s in forNewCpp)
                result[$"main/{Sanitize(s.Name)}.cpp"] = s.Code;
        }

        // ── Constructeurs de fichiers ─────────────────────────────────────────────

        private static string BuildGameH(List<CodeSnippet> snippets, string project)
        {
            var decls = Collect(snippets, "//@@DECLARATIONS@@");
            return
$@"#pragma once
// {project} — game.h
// Généré par Gamebuino AKA IDE
#include <Gamebuino-Meta.h>

// ── Fonctions principales ──────────────────────────────────────────────────
void gameUpdate(Gamebuino& gb);
void gameRender(Gamebuino& gb);

{(decls.Length > 0 ? "// ── Déclarations des modules sélectionnés ──────────────────────────────\n" + decls : "")}
";
        }

        private static string BuildGameCpp(List<CodeSnippet> snippets, string project)
        {
            var includes  = Collect(snippets, "//@@INCLUDES@@");
            var globals   = Collect(snippets, "//@@GLOBALS@@");
            var update    = Collect(snippets, "//@@UPDATE@@");
            var render    = Collect(snippets, "//@@RENDER@@");
            var functions = Collect(snippets, "//@@FUNCTIONS@@");

            return
$@"#include ""game.h""
{(includes.Length > 0 ? includes + "\n" : "")}
// ── Variables d'état ───────────────────────────────────────────────────────
static int playerX = 160;
static int playerY = 120;
static const int PLAYER_SPEED = 2;
{(globals.Length > 0 ? "\n" + globals : "")}
// ── gameUpdate ──────────────────────────────────────────────────────────────
void gameUpdate(Gamebuino& gb) {{
    // Déplacement du joueur
    if (gb.buttons.repeat(BUTTON_LEFT,  1)) playerX -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_RIGHT, 1)) playerX += PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_UP,    1)) playerY -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_DOWN,  1)) playerY += PLAYER_SPEED;
    playerX = max(0, min(315, playerX));
    playerY = max(0, min(235, playerY));
{(update.Length > 0 ? "\n" + Indent(update, 4) : "")}
}}

// ── gameRender ──────────────────────────────────────────────────────────────
void gameRender(Gamebuino& gb) {{
    gb.display.setColor(BLACK);
    gb.display.fill();
    gb.display.setColor(0x7C5C);
    gb.display.fillRect(playerX, playerY, 5, 5);
    gb.display.setColor(WHITE);
    gb.display.setCursor(4, 4);
    gb.display.print(""{project}"");
{(render.Length > 0 ? "\n" + Indent(render, 4) : "")}
}}
{(functions.Length > 0 ? "\n// ── Fonctions helper ─────────────────────────────────────────────────\n" + functions : "")}
";
        }

        private static string BuildMainCppWithGame(List<CodeSnippet> snippets, string project)
        {
            var extra = Collect(snippets, "//@@UPDATE@@") + Collect(snippets, "//@@RENDER@@");
            _ = extra; // les injections vont dans game.cpp
            return
$@"#include <Gamebuino-Meta.h>
#include ""game.h""
// {project} — généré par Gamebuino AKA IDE

Gamebuino gb;

void setup() {{
    gb.begin();
}}

void loop() {{
    gb.waitForUpdate();
    gb.display.clear();
    gameUpdate(gb);
    gameRender(gb);
}}
";
        }

        private static string BuildMainCppInline(List<CodeSnippet> snippets, string project)
        {
            var includes  = Collect(snippets, "//@@INCLUDES@@");
            var globals   = Collect(snippets, "//@@GLOBALS@@");
            var update    = Collect(snippets, "//@@UPDATE@@");
            var render    = Collect(snippets, "//@@RENDER@@");
            var functions = Collect(snippets, "//@@FUNCTIONS@@");

            return
$@"#include <Gamebuino-Meta.h>
{(includes.Length > 0 ? includes + "\n" : "")}
// {project} — généré par Gamebuino AKA IDE
Gamebuino gb;
{(globals.Length > 0 ? "\n" + globals : "")}
void setup() {{
    gb.begin();
}}

void loop() {{
    gb.waitForUpdate();
    gb.display.clear();
{(update.Length > 0 ? Indent(update, 4) + "\n" : "")}
{(render.Length > 0 ? Indent(render, 4) + "\n" : "")}
}}
{(functions.Length > 0 ? "\n" + functions : "")}
";
        }

        private static string BuildAppMain(List<CodeSnippet> snippets, string project)
        {
            var includes  = Collect(snippets, "//@@INCLUDES@@");
            var globals   = Collect(snippets, "//@@GLOBALS@@");
            var update    = Collect(snippets, "//@@UPDATE@@");
            var render    = Collect(snippets, "//@@RENDER@@");
            var functions = Collect(snippets, "//@@FUNCTIONS@@");

            return
$@"/*
 * app_main.cpp — {project} (Gamebuino AKA, ESP-IDF)
 * Généré par Gamebuino AKA IDE
 */
#include ""freertos/FreeRTOS.h""
#include ""freertos/task.h""
#include ""gamebuino.h""
{(includes.Length > 0 ? includes + "\n" : "")}
gb_core     g_core;
gb_graphics gfx;
{(globals.Length > 0 ? "\n" + globals : "")}
{(functions.Length > 0 ? functions + "\n" : "")}
extern ""C"" void app_main(void)
{{
    g_core.init();

    while (true) {{
        gfx.clear(gfx.makeColor(20, 16, 40));
{(update.Length > 0 ? Indent(update, 8) + "\n" : "")}
{(render.Length > 0 ? Indent(render, 8) + "\n" : "")}
        gfx.update();
        vTaskDelay(pdMS_TO_TICKS(16));
    }}
}}
";
        }

        // ── Utilitaires ───────────────────────────────────────────────────────────

        /// <summary>
        /// Extrait la section d'un snippet délimitée par un marqueur @@.
        /// Si le code ne contient PAS le marqueur, retourne tout le code (compatibilité).
        /// </summary>
        private static string Collect(IEnumerable<CodeSnippet> snippets, string marker)
        {
            var lines = new System.Text.StringBuilder();
            foreach (var s in snippets)
            {
                var section = ExtractSection(s.Code, marker);
                if (section.Length > 0)
                {
                    lines.AppendLine($"    // --- {s.Name} ---");
                    lines.AppendLine(section);
                }
            }
            return lines.ToString().TrimEnd();
        }

        /// <summary>
        /// Extrait la zone entre //@@MARKER@@ et le prochain //@@...@@ ou fin de code.
        /// </summary>
        private static string ExtractSection(string code, string marker)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            var start = code.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return string.Empty;

            start = code.IndexOf('\n', start);
            if (start < 0) return string.Empty;
            start++;

            // Cherche le prochain marqueur @@
            var nextMarker = code.IndexOf("//@@", start, StringComparison.Ordinal);
            var section = nextMarker < 0
                ? code[start..]
                : code[start..nextMarker];

            return section.TrimEnd();
        }

        private static string Indent(string code, int spaces)
        {
            var pad = new string(' ', spaces);
            return string.Join('\n',
                code.Split('\n').Select(l => string.IsNullOrWhiteSpace(l) ? l : pad + l));
        }

        private static string Sanitize(string name) =>
            System.Text.RegularExpressions.Regex.Replace(
                string.IsNullOrEmpty(name) ? "module" : name, @"[^a-zA-Z0-9_]", "_");

        /// <summary>
        /// Résout les dépendances entre snippets et déduplique.
        /// </summary>
        private List<CodeSnippet> ResolveDependencies(List<CodeSnippet> selected, BuildSystem build)
        {
            var all   = GetAll(build).ToDictionary(s => s.Id);
            var ids   = new HashSet<string>(selected.Select(s => s.Id));
            var queue = new Queue<CodeSnippet>(selected);

            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                foreach (var dep in s.RequiresSnippetIds)
                {
                    if (!ids.Contains(dep) && all.TryGetValue(dep, out var depSnippet))
                    {
                        ids.Add(dep);
                        queue.Enqueue(depSnippet);
                        selected.Add(depSnippet);
                    }
                }
            }
            return selected;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CATALOGUE INTÉGRÉ
        //  Les marqueurs //@@ZONE@@ délimitent les sections injectées dans le squelette.
        // ════════════════════════════════════════════════════════════════════════

        private static List<CodeSnippet> BuiltinSnippets() => new()
        {
            // ── ENTRÉES ──────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "input_buttons",
                Name = "Lecture des boutons",
                Summary = "Lit A, B, directions et Menu à chaque frame.",
                Explanation =
@"gb.buttons fournit trois méthodes essentielles :
• pressed(btn)  → vrai UNE seule frame au moment de l'appui
• repeat(btn,n) → vrai à l'appui puis toutes les n frames (auto-repeat)
• released(btn) → vrai UNE seule frame au relâchement

Boutons disponibles : BUTTON_A, BUTTON_B, BUTTON_LEFT, BUTTON_RIGHT,
BUTTON_UP, BUTTON_DOWN, BUTTON_MENU.

Exemple typique : utiliser pressed() pour tirer et repeat() pour se déplacer.",
                Category = "Entrées",
                Tags = new() { "input", "boutons", "contrôles" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@UPDATE@@
    // --- Lecture des boutons ---
    if (gb.buttons.pressed(BUTTON_A)) {
        // Action au premier appui sur A
    }
    if (gb.buttons.pressed(BUTTON_B)) {
        // Action au premier appui sur B
    }
    if (gb.buttons.repeat(BUTTON_LEFT,  1)) playerX -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_RIGHT, 1)) playerX += PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_UP,    1)) playerY -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_DOWN,  1)) playerY += PLAYER_SPEED;
"
            },

            // ── GRAPHISMES ────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "gfx_fill_background",
                Name = "Fond coloré",
                Summary = "Efface l'écran avec une couleur de fond.",
                Explanation =
@"gb.display.fill() remplit tout l'écran avec la couleur courante
(setColor / setFillColor).

Couleurs prédéfinies : BLACK, WHITE, RED, GREEN, BLUE, YELLOW, ORANGE,
PINK, PURPLE, BROWN, GRAY, DARKGRAY, LIGHTGRAY, CYAN.

Vous pouvez aussi utiliser une valeur RGB565 16 bits directement :
gb.display.setColor(0x7C5C); // violet Gamebuino AKA",
                Category = "Graphismes",
                Tags = new() { "fond", "couleur", "effacer" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@RENDER@@
    // Fond coloré
    gb.display.setColor(BLACK);
    gb.display.fill();
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_rect",
                Name = "Rectangle / carré",
                Summary = "Dessine un rectangle plein ou en contour.",
                Explanation =
@"gb.display.fillRect(x, y, w, h) → rectangle plein
gb.display.drawRect(x, y, w, h) → contour seulement

L'écran Gamebuino AKA fait 320×240 pixels.
Attention : les coordonnées dépassant les bords sont simplement clampées
(pas de crash), mais pensez à vérifier les collisions avec les bords.",
                Category = "Graphismes",
                Tags = new() { "rectangle", "forme", "dessin" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@RENDER@@
    // Rectangle plein
    gb.display.setColor(BLUE);
    gb.display.fillRect(playerX, playerY, 16, 16);

    // Contour seulement
    gb.display.setColor(WHITE);
    gb.display.drawRect(playerX - 1, playerY - 1, 18, 18);
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_circle",
                Name = "Cercle",
                Summary = "Dessine un cercle plein ou en contour.",
                Explanation =
@"gb.display.fillCircle(x, y, r) → cercle plein
gb.display.drawCircle(x, y, r) → contour

x, y = centre du cercle ; r = rayon en pixels.

Utile pour des balles, explosions, ou n'importe quel objet rond.",
                Category = "Graphismes",
                Tags = new() { "cercle", "forme", "dessin" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@RENDER@@
    // Cercle plein (balle, projectile…)
    gb.display.setColor(YELLOW);
    gb.display.fillCircle(playerX + 8, playerY + 8, 6);
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_text",
                Name = "Affichage de texte",
                Summary = "Place du texte à l'écran avec setCursor + print.",
                Explanation =
@"Workflow classique :
1. gb.display.setColor(couleur)
2. gb.display.setCursor(x, y)
3. gb.display.print(valeur)   ← accepte char*, int, float, String…

La police par défaut fait 5×7 pixels (grossie ×2 = 10×14 px en pratique).
Vous pouvez afficher des variables avec printf-style :
    char buf[32];
    snprintf(buf, sizeof(buf), ""Score: %d"", score);
    gb.display.print(buf);",
                Category = "Graphismes",
                Tags = new() { "texte", "print", "HUD", "score" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int score = 0;

//@@RENDER@@
    // Affichage texte / HUD
    gb.display.setColor(WHITE);
    gb.display.setCursor(4, 4);
    char hud[32];
    snprintf(hud, sizeof(hud), ""Score: %d"", score);
    gb.display.print(hud);
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_sprite",
                Name = "Affichage sprite 16 bits",
                Summary = "Dessine un sprite RGB565 avec couleur-clé de transparence.",
                Explanation =
@"graphics_draw_bitmap565(gfx, x, y, data, w, h, transparent_key, use_transparency)

data est un tableau uint16_t[] PROGMEM déclaré dans un .h d'assets.
La couleur-clé par défaut est 0xF81F (magenta).

Pour générer le tableau, utilisez l'éditeur de sprites intégré à l'IDE
(menu Sprites → exporter en C++), puis incluez le .h généré.

Conseil : nommez votre sprite SNAKE_CASE (player_idle, enemy_run…)
et utilisez player_idle_width / _height / _frames pour les dimensions.",
                Category = "Graphismes",
                Tags = new() { "sprite", "bitmap", "image", "animation" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@INCLUDES@@
// #include ""player_sprite.h""   // ← décommentez et remplacez par votre fichier d'asset

//@@RENDER@@
    // Affichage sprite (décommentez après avoir inclus le fichier d'asset)
    // graphics_draw_bitmap565(gb.display, playerX, playerY,
    //     player_sprite, player_sprite_width, player_sprite_height,
    //     0xF81F, true);
"
            },

            // ── SON ────────────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "sound_beep",
                Name = "Son simple (tone)",
                Summary = "Joue un bip ou une note à une fréquence donnée.",
                Explanation =
@"gb.sound.tone(fréquenceHz, duréeMs) joue une note simple.

Fréquences courantes (octave 4) :
  DO  = 262 Hz   RÉ  = 294 Hz   MI  = 330 Hz
  FA  = 349 Hz   SOL = 392 Hz   LA  = 440 Hz   SI  = 494 Hz

Le son est non-bloquant ; la durée est gérée en arrière-plan.

Astuce : appelez gb.sound.tone(0, 0) pour arrêter un son en cours.",
                Category = "Son",
                Tags = new() { "son", "bip", "tone", "audio" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@UPDATE@@
    // Son simple au premier appui sur A (bip 440 Hz, 100 ms)
    if (gb.buttons.pressed(BUTTON_A)) {
        gb.sound.tone(440, 100);  // LA 4, 100 ms
    }
"
            },

            new CodeSnippet
            {
                Id = "sound_wav",
                Name = "Effet sonore WAV",
                Summary = "Joue un fichier WAV converti en tableau C (via WAV_SYSTEM).",
                Explanation =
@"Workflow :
1. Préparez votre WAV (8 kHz mono recommandé pour l'ESP32-S3).
2. Utilisez la banque sonore de l'IDE pour l'importer dans le projet.
3. Incluez le .h généré (qui contient la table WAV_SYSTEM).
4. Appelez gb.sound.playWav(nom_du_son, sizeof(nom_du_son)).

Ou, pour la lib AKA basse consommation :
    gb_audio_play_wav_system(&WAV_DATA, sizeof(WAV_DATA));",
                Category = "Son",
                Tags = new() { "son", "wav", "effet sonore", "audio" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@INCLUDES@@
// #include ""explode.h""  // ← votre fichier WAV_SYSTEM

//@@UPDATE@@
    // Joue le son WAV à l'appui sur A
    if (gb.buttons.pressed(BUTTON_A)) {
        // gb.sound.playWav(explode_data, sizeof(explode_data));
    }
"
            },

            // ── LOGIQUE JEUX ──────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "logic_score",
                Name = "Système de score",
                Summary = "Variable de score avec affichage et réinitialisation.",
                Explanation =
@"Implémentation minimaliste d'un score :
• Variable globale statique incrémentée à chaque événement de jeu.
• Affiché dans gameRender() via snprintf + display.print().
• Remis à zéro quand le joueur perd / restart.

Pour sauvegarder le highscore entre les parties :
  gb.save.set(0, highScore);
  highScore = gb.save.get(0);",
                Category = "Logique",
                Tags = new() { "score", "compteur", "highscore" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int score     = 0;
static int highScore = 0;

//@@UPDATE@@
    // Incrémente le score (remplacez la condition par votre événement)
    // if (événement) { score += 10; if (score > highScore) highScore = score; }

//@@RENDER@@
    // Affiche le score
    gb.display.setColor(WHITE);
    gb.display.setCursor(4, 4);
    char scoreBuf[32];
    snprintf(scoreBuf, sizeof(scoreBuf), ""Score: %d  Best: %d"", score, highScore);
    gb.display.print(scoreBuf);
"
            },

            new CodeSnippet
            {
                Id = "logic_timer",
                Name = "Timer / cooldown",
                Summary = "Minuteur basé sur les frames pour des cooldowns ou délais.",
                Explanation =
@"gb.frameCount est incrémenté à chaque frame (tourne à ~30 FPS par défaut).

Patterns courants :
• Cooldown :
    if (frameCount - lastShot >= 20) { /* tir autorisé */ }
• Compte à rebours :
    int remaining = max(0, 300 - (int)(gb.frameCount - startFrame));
• Animation par frame :
    int frame = (gb.frameCount / 8) % frameCount;

Rappel : 30 frames ≈ 1 seconde.",
                Category = "Logique",
                Tags = new() { "timer", "cooldown", "frame", "délai" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static uint32_t lastActionFrame = 0;
static const uint32_t ACTION_COOLDOWN = 20; // frames (~0.67 s à 30 fps)

//@@UPDATE@@
    // Cooldown : action possible toutes les ACTION_COOLDOWN frames
    if (gb.frameCount - lastActionFrame >= ACTION_COOLDOWN) {
        if (gb.buttons.pressed(BUTTON_A)) {
            lastActionFrame = gb.frameCount;
            // Votre action ici
        }
    }
"
            },

            new CodeSnippet
            {
                Id = "logic_collision_rect",
                Name = "Collision AABB (rectangle)",
                Summary = "Détection de collision entre deux rectangles axis-aligned.",
                Explanation =
@"AABB = Axis-Aligned Bounding Box : la forme de collision la plus simple.

La fonction checkCollision(ax, ay, aw, ah, bx, by, bw, bh) retourne true
si les deux rectangles se chevauchent.

Pour une liste d'ennemis, parcourez le tableau avec une boucle et appelez
checkCollision pour chaque ennemi contre le joueur.",
                Category = "Logique",
                Tags = new() { "collision", "AABB", "physique", "overlap" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@FUNCTIONS@@
// Collision AABB : retourne true si les deux rectangles se chevauchent
static bool checkCollision(int ax, int ay, int aw, int ah,
                            int bx, int by, int bw, int bh) {
    return ax < bx + bw && ax + aw > bx
        && ay < by + bh && ay + ah > by;
}

//@@UPDATE@@
    // Exemple d'utilisation de checkCollision :
    // if (checkCollision(playerX, playerY, 8, 8, enemyX, enemyY, 8, 8)) {
    //     // collision détectée
    // }
"
            },

            new CodeSnippet
            {
                Id = "logic_state_machine",
                Name = "Machine à états",
                Summary = "Enum + switch pour gérer les états du jeu (menu, jeu, game over).",
                Explanation =
@"Une machine à états simple gère les différentes phases du jeu :
  MENU → PLAYING → GAME_OVER → MENU (cycle)

Chaque état a sa propre logique de mise à jour et son propre rendu.
Ajoutez des états selon vos besoins : PAUSE, LEVEL_COMPLETE, CREDITS…

Conseil : définissez une fonction update et render par état pour garder
le code lisible.",
                Category = "Logique",
                Tags = new() { "état", "FSM", "menu", "game over", "architecture" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
enum GameState { STATE_MENU, STATE_PLAYING, STATE_GAMEOVER };
static GameState currentState = STATE_MENU;

//@@UPDATE@@
    switch (currentState) {
        case STATE_MENU:
            if (gb.buttons.pressed(BUTTON_A)) currentState = STATE_PLAYING;
            break;
        case STATE_PLAYING:
            // Logique de jeu ici
            // if (playerDead) currentState = STATE_GAMEOVER;
            break;
        case STATE_GAMEOVER:
            if (gb.buttons.pressed(BUTTON_A)) { currentState = STATE_MENU; }
            break;
    }

//@@RENDER@@
    switch (currentState) {
        case STATE_MENU:
            gb.display.setColor(WHITE);
            gb.display.setCursor(100, 110);
            gb.display.print(""Appuyez sur A"");
            break;
        case STATE_GAMEOVER:
            gb.display.setColor(RED);
            gb.display.setCursor(116, 110);
            gb.display.print(""GAME OVER"");
            gb.display.setColor(WHITE);
            gb.display.setCursor(96, 130);
            gb.display.print(""A = Recommencer"");
            break;
        default: break;
    }
"
            },

            new CodeSnippet
            {
                Id = "logic_save",
                Name = "Sauvegarde / chargement",
                Summary = "Lit et écrit des entiers dans la mémoire persistante (NVS).",
                Explanation =
@"gb.save est une mini-base de données à cases numérotées (index 0…N).
Chaque case stocke un int32_t.

gb.save.set(index, valeur)   → écrit
gb.save.get(index)           → lit (renvoie 0 si jamais écrit)

Indices recommandés (convention libre) :
  0 = highscore    1 = niveau débloqué    2 = volume

Les données survivent aux redémarrages de la console.",
                Category = "Logique",
                Tags = new() { "sauvegarde", "highscore", "persistance", "nvs" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int highScore = 0;

//@@UPDATE@@
    // Charger le highscore au démarrage (appelable une seule fois)
    // Placez cet appel dans une fonction d'initialisation ou en haut de gameUpdate.
    // highScore = gb.save.get(0);

    // Sauvegarde quand on bat le record
    // if (score > highScore) {
    //     highScore = score;
    //     gb.save.set(0, highScore);
    // }
"
            },

            // ── PHYSIQUE ──────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "physics_gravity",
                Name = "Gravité simple",
                Summary = "Velocity + gravité pour des sauts réalistes.",
                Explanation =
@"Simulation de gravité en deux lignes :
  velocityY += GRAVITY;   // accélération vers le bas
  playerY   += velocityY; // déplacement

Saut : velocityY = -JUMP_FORCE  (valeur négative = vers le haut)

Valeurs typiques pour 30 fps, écran 240 px :
  GRAVITY    = 0.5f
  JUMP_FORCE = 8.0f
  MAX_FALL   = 12.0f (vitesse de chute maximale)

Ajoutez un sol en testant playerY >= FLOOR_Y.",
                Category = "Physique",
                Tags = new() { "gravité", "saut", "platformer", "velocity" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static float velocityY    = 0.0f;
static bool  isOnGround   = false;
static const float GRAVITY    = 0.5f;
static const float JUMP_FORCE = 8.0f;
static const float MAX_FALL   = 12.0f;
static const int   FLOOR_Y    = 220; // Y du sol

//@@UPDATE@@
    // Saut
    if (gb.buttons.pressed(BUTTON_A) && isOnGround) {
        velocityY  = -JUMP_FORCE;
        isOnGround = false;
    }

    // Gravité
    velocityY += GRAVITY;
    if (velocityY >  MAX_FALL) velocityY =  MAX_FALL;
    playerY += (int)velocityY;

    // Sol
    if (playerY >= FLOOR_Y) {
        playerY    = FLOOR_Y;
        velocityY  = 0;
        isOnGround = true;
    }
"
            },

            // ── CAMÉRA / SCROLL ────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "camera_scroll",
                Name = "Caméra scrollante",
                Summary = "Décalage de caméra pour un niveau plus grand que l'écran.",
                Explanation =
@"Une caméra simple est un décalage (cameraX, cameraY) soustrait à toutes
les positions avant le rendu.

Formule : screenX = worldX - cameraX

La caméra suit le joueur avec un offset pour le centrer :
  cameraX = playerX - 160;  // centré sur l'écran 320px
  cameraX = max(0, min(WORLD_WIDTH - 320, cameraX)); // clampe

Dessinez uniquement les objets dont screenX ∈ [-16, 336] (petite marge).",
                Category = "Caméra",
                Tags = new() { "caméra", "scroll", "monde", "niveau" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int cameraX = 0;
static int cameraY = 0;
static const int WORLD_WIDTH  = 1280; // largeur totale du monde
static const int WORLD_HEIGHT = 240;

//@@UPDATE@@
    // La caméra suit le joueur (centrage horizontal)
    cameraX = playerX - 160;
    cameraX = max(0, min(WORLD_WIDTH - 320, cameraX));

//@@RENDER@@
    // Rendu d'un objet avec décalage caméra
    // int screenX = worldObjX - cameraX;
    // int screenY = worldObjY - cameraY;
    // if (screenX > -16 && screenX < 336)
    //     gb.display.fillRect(screenX, screenY, 8, 8);
"
            },

            // ── DÉBOGAGE ──────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "debug_overlay",
                Name = "Overlay de débogage",
                Summary = "Affiche FPS, position et valeurs debug à l'écran.",
                Explanation =
@"Affiche des informations de débogage en superposition.

gb.getCpuLoad() renvoie le pourcentage CPU utilisé (0–100).
gb.frameCount est le numéro de frame courant.

Conseil : désactivez l'overlay en production avec un #define DEBUG_MODE.",
                Category = "Débogage",
                Tags = new() { "debug", "fps", "overlay", "développement" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
#define DEBUG_MODE 1  // Mettez 0 pour désactiver en production

//@@RENDER@@
#if DEBUG_MODE
    // Overlay de débogage (coin supérieur droit)
    gb.display.setColor(YELLOW);
    gb.display.setCursor(240, 4);
    char dbgBuf[40];
    snprintf(dbgBuf, sizeof(dbgBuf), ""CPU:%d%% X:%d Y:%d"",
             gb.getCpuLoad(), playerX, playerY);
    gb.display.print(dbgBuf);
#endif
"
            },
        };
    }
}
