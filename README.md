# Gamebuino AKA IDE

> **Launcher Windows WPF pour le développement de jeux sur Gamebuino AKA (ESP32-S3, 320×240)**

---

## Table des matières / Table of Contents

- [🇫🇷 Documentation Française](#-documentation-française)
  - [Description](#description)
  - [Architecture du code](#architecture-du-code)
  - [Prérequis](#prérequis)
  - [Installation](#installation)
  - [Mode d'emploi](#mode-demploi)
- [🇬🇧 English Documentation](#-english-documentation)
  - [Description](#description-1)
  - [Code Architecture](#code-architecture)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation-1)
  - [User Guide](#user-guide)

---

# 🇫🇷 Documentation Française

## Description

**Gamebuino AKA IDE** est un launcher Windows natif (WPF / .NET 5) conçu pour simplifier le développement de jeux sur la console **Gamebuino AKA**, basée sur un microcontrôleur **ESP32-S3** avec un écran de **320×240 pixels**.

Il ne remplace pas VS Code ni PlatformIO — il les complète :

| Ce que fait l'IDE | Ce qu'il ne fait pas |
|---|---|
| Créer des projets depuis des templates | Compiler/exécuter du C++ directement |
| Cloner des jeux existants depuis GitHub | Remplacer l'éditeur de code |
| Lancer VS Code avec le bon workspace | Gérer les dépendances ESP-IDF manuellement |
| Déclencher Build / Flash / Monitor | Fournir un débogueur GDB |
| Éditer et prévisualiser des sprites PNG | Animer en temps réel sur la console |
| Créer des tilemaps et les exporter en C++ | Gérer la mémoire flash directement |

---

## Architecture du code

```
GamebuinoAKA.IDE/
├── GamebuinoAKA.IDE.sln               ← Solution Visual Studio
└── src/
    └── GamebuinoAKA.IDE/
        ├── GamebuinoAKA.IDE.csproj    ← Projet WPF (.NET 5, C# 9)
        ├── App.xaml / App.xaml.cs     ← Point d'entrée, injection de dépendances (DI)
        ├── MainWindow.xaml / .cs      ← Fenêtre principale avec sidebar de navigation
        │
        ├── Models/                    ← Modèles de données (POCO)
        │   ├── AppSettings.cs         ← Paramètres persistés dans %APPDATA%
        │   ├── GamebuinoProject.cs    ← Métadonnées d'un projet (nom, chemin, template…)
        │   ├── SpriteAsset.cs         ← Pixels RGB565, frames, palette
        │   └── TilemapAsset.cs        ← Grille de tuiles, couches bg/fg, tileset
        │
        ├── Services/                  ← Logique métier (sans UI)
        │   ├── SettingsService.cs     ← Lecture/écriture JSON des paramètres
        │   ├── ProjectService.cs      ← Scan workspace, gestion des projets récents
        │   ├── TemplateService.cs     ← Génération de projets depuis templates embarqués
        │   ├── PlatformIOService.cs   ← Appels CLI pio (build, flash, monitor)
        │   ├── VSCodeService.cs       ← Détection et lancement de VS Code
        │   ├── GitService.cs          ← Clone de dépôts GitHub via git CLI
        │   └── AssetService.cs        ← Import PNG→RGB565, export tableaux C++
        │
        ├── ViewModels/                ← MVVM : état et commandes, sans dépendance UI
        │   ├── MainViewModel.cs       ← Navigation entre pages, status bar
        │   ├── HomeViewModel.cs       ← Dashboard, projets récents, actions rapides
        │   ├── ProjectsViewModel.cs   ← Liste de projets, build/flash, clone GitHub
        │   ├── NewProjectViewModel.cs ← Wizard de création de projet
        │   ├── SpriteEditorViewModel.cs  ← Éditeur pixel, palette, export C++
        │   ├── TilemapEditorViewModel.cs ← Éditeur de tilemap, couches, export C++
        │   └── SettingsViewModel.cs   ← Configuration des outils et chemins
        │
        ├── Views/                     ← XAML (UI uniquement, pas de logique)
        │   ├── HomeView.xaml          ← Accueil avec projets récents
        │   ├── ProjectsView.xaml      ← Gestionnaire de projets + dialog clone
        │   ├── NewProjectView.xaml    ← Formulaire de création
        │   ├── SpriteEditorView.xaml  ← Éditeur de sprites
        │   ├── TilemapEditorView.xaml ← Éditeur de tilemaps
        │   └── SettingsView.xaml      ← Page de paramètres
        │
        ├── Controls/                  ← UserControls réutilisables
        │   ├── PixelCanvas.xaml/.cs   ← Rendu pixel-perfect avec zoom, clic pour peindre
        │   └── TilemapGrid.xaml/.cs   ← Grille de tuiles avec sélection et peinture
        │
        ├── Converters/
        │   └── Converters.cs          ← IValueConverter WPF (bool→Visibility, RGB565→Color…)
        │
        ├── Themes/
        │   └── Dark.xaml              ← Thème sombre Gamebuino (violet #7C5CD8)
        │
        └── Templates/                 ← Templates embarqués (EmbeddedResource)
            ├── empty/                 ← Projet vide avec platformio.ini
            ├── hello-world/           ← Affiche "Hello Gamebuino AKA!"
            └── game-template/         ← Structure main.cpp + game.h + game.cpp
```

### Patron de conception

- **MVVM strict** : les ViewModels n'importent aucun namespace `System.Windows.*`
- **Injection de dépendances** via `Microsoft.Extensions.DependencyInjection`
- **Services singletons**, Views et ViewModels singletons (navigation entre pages)
- **Commandes explicites** : `RelayCommand`, `AsyncRelayCommand`, `RelayCommand<T>` de `Microsoft.Toolkit.Mvvm 7.1.2`
- **Notifications** : `SetProperty(ref field, value)` → `INotifyPropertyChanged`

---

## Prérequis

### Obligatoires

| Logiciel | Version minimale | Lien |
|---|---|---|
| **Windows** | 10 / 11 | — |
| **.NET 5 Runtime** | 5.0.x | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/5.0) |
| **Git** | 2.x | [git-scm.com](https://git-scm.com/) |

### Pour compiler le code C++ / flasher la console

| Logiciel | Version minimale | Lien |
|---|---|---|
| **Visual Studio Code** | 1.70+ | [code.visualstudio.com](https://code.visualstudio.com/) |
| **Extension PlatformIO IDE** | dernière | [marketplace.visualstudio.com](https://marketplace.visualstudio.com/items?itemName=platformio.platformio-ide) |
| **Extension C/C++** | dernière | [marketplace.visualstudio.com](https://marketplace.visualstudio.com/items?itemName=ms-vscode.cpptools) |

> PlatformIO télécharge automatiquement la chaîne de compilation Xtensa ESP32 (~300 Mo) lors du premier build.

### Pour compiler l'IDE lui-même (développeurs)

| Outil | Version |
|---|---|
| .NET 5 SDK | 5.0.416+ |
| ou Visual Studio 2019/2022 | avec workload ".NET desktop development" |

---

## Installation

### Option A — Utiliser l'exécutable compilé

1. Copier le dossier `bin/Debug/net5.0-windows/` sur votre machine
2. Lancer `GamebuinoAKA.IDE.exe`
3. Au premier lancement, aller dans **Paramètres** pour configurer :
   - Le dossier workspace (ex: `C:\Users\Jean\Documents\GamebuinoAKA`)
   - Le chemin vers VS Code (auto-détecté si dans le PATH)
   - Le chemin vers `pio.exe` (auto-détecté si PlatformIO est installé)

### Option B — Compiler depuis les sources

```powershell
# Cloner le projet
git clone https://github.com/votre-repo/GamebuinoAKA-IDE.git
cd GamebuinoAKA-IDE

# Compiler
dotnet build GamebuinoAKA.IDE/GamebuinoAKA.IDE.sln

# Lancer
dotnet run --project GamebuinoAKA.IDE/src/GamebuinoAKA.IDE/
```

---

## Mode d'emploi

### 1. Premier lancement — Configurer l'environnement

Au démarrage, la barre de status affiche les versions détectées :

```
PlatformIO: 6.1.11    VSCode: Code.exe
```

Si un outil n'est pas détecté :
1. Cliquer sur **⚙️ Paramètres** dans la sidebar
2. Cliquer sur **🔍 Auto-détecter les outils**
3. Ou renseigner manuellement les chemins
4. Cliquer **💾 Enregistrer**

---

### 2. Créer un nouveau projet

1. Cliquer sur **➕ Nouveau projet** (dashboard ou sidebar)
2. Renseigner le **nom** du projet (ex: `MonPremierJeu`)
3. Choisir un **template** :

| Template | Contenu généré | Usage |
|---|---|---|
| **Empty** | `platformio.ini` + `src/main.cpp` vide | Partir de zéro |
| **Hello World** | Affiche du texte à l'écran | Valider le setup |
| **Game Template** | `main.cpp` + `game.h` + `game.cpp` | Structure jeu complète |

4. Choisir ou confirmer le **dossier de destination**
5. Cliquer **✅ Créer le projet**

**Exemple — platformio.ini généré :**
```ini
[env:gamebuino_aka]
platform = espressif32
board = esp32-s3-devkitc-1
framework = arduino
monitor_speed = 115200
board_build.flash_size = 16MB
board_build.psram_type = opi
lib_deps =
    https://github.com/jmp42/Gamebuino_AKA_lib
build_flags =
    -DBOARD_HAS_PSRAM
    -mfix-esp32-psram-cache-issue
upload_protocol = esptool
```

**Exemple — main.cpp Hello World généré :**
```cpp
#include <Gamebuino-Meta.h>

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
    gb.display.print("Hello Gamebuino AKA!");
}
```

---

### 3. Importer un projet existant depuis GitHub

Pour récupérer un jeu existant (ex: pAKAman, Galaxy Fighter, KONG 2…) :

1. Aller dans **📁 Projets**
2. Cliquer sur **⬇ Cloner GitHub**
3. Coller l'URL du dépôt :
   ```
   https://github.com/Jicehel-Aka/pAKAman
   https://github.com/Jicehel-Aka/Galaxy-Fighter-Pokitto-ported-on-Aka
   https://github.com/Jicehel-Aka/KONG-2-POKITTO-ported-on-AKA
   https://github.com/Jicehel-Aka/Akassebricks
   https://github.com/Jicehel-Aka/Baba-Is-You-Version-AKA-
   ```
4. Le nom du dossier est pré-rempli automatiquement
5. Cliquer **⬇ Cloner**
6. La sortie Git s'affiche en temps réel dans le panneau du bas
7. Une fois terminé, le projet apparaît dans la liste

---

### 4. Travailler sur un projet

Depuis la liste des projets, chaque carte propose :

| Bouton | Action |
|---|---|
| **📝 VS Code** | Ouvre le dossier dans VS Code avec PlatformIO actif |
| **🔨 Build** | Compile le projet via `pio run` |
| **⚡ Flash** | Compile + envoie sur la console via USB |
| **📡 Monitor** | Ouvre le moniteur série (115200 baud) |
| **📂 Explorer** | Ouvre le dossier dans l'Explorateur Windows |
| **🗑 Supprimer** | Supprime le dossier (avec confirmation) |

La sortie de PlatformIO s'affiche en temps réel dans le panneau du bas (fond noir, texte vert).

**Exemple de sortie Build réussie :**
```
Processing gamebuino_aka (platform: espressif32; board: esp32-s3-devkitc-1; framework: arduino)
Compiling .pio/build/gamebuino_aka/src/main.cpp.o
Linking .pio/build/gamebuino_aka/firmware.elf
Building .pio/build/gamebuino_aka/firmware.bin
========================= [SUCCESS] Took 23.45 seconds =========================
```

---

### 5. Éditeur de sprites

1. Cliquer sur **🎨 Sprites** dans la sidebar
2. Cliquer **📂 Importer image** → sélectionner un PNG/BMP
3. Le sprite s'affiche en pixel-perfect avec la palette 16 couleurs RGB565
4. Utiliser le **zoom** (1×, 2×, 4×, 8×) pour éditer pixel par pixel
5. Cliquer sur une couleur de la palette, puis cliquer sur le canvas pour peindre
6. Cliquer **💾 Exporter C++** → génère un tableau à coller dans le code

**Exemple de code exporté :**
```cpp
// Sprite: player (16x16)
// Frames: 4  Frame size: 16x16
const uint16_t player[] PROGMEM = {
    0x0000, 0x7BEF, 0x7BEF, 0xFFFF, 0xFFFF, 0x7BEF, 0x7BEF, 0x0000,
    // ... 256 valeurs RGB565
};
const uint8_t player_frames = 4;
const uint8_t player_frame_width = 16;
const uint8_t player_frame_height = 16;
```

---

### 6. Éditeur de tilemaps

1. Cliquer sur **🗺️ Tilemaps** dans la sidebar
2. Cliquer **📂 Importer tileset** → sélectionner un PNG (grille de tuiles)
3. Configurer la taille des tuiles (ex: 16×16 px) et les dimensions de la map
4. Sélectionner une tuile dans le panneau de gauche (tileset)
5. Peindre sur la grille de droite (map)
6. Choisir la couche active : **Background** ou **Foreground**
7. Cliquer **💾 Exporter C++** → génère les deux tableaux

**Exemple de code exporté :**
```cpp
// Tilemap: level1 (20x15 tiles)
// Tile size: 16x16
const uint8_t level1_bg[] PROGMEM = {
    0x01, 0x01, 0x01, 0x02, 0x01, 0x01, 0x03, 0x01,  // ligne 0
    0x01, 0x04, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01,  // ligne 1
    // ...
};
const uint8_t level1_cols = 20;
const uint8_t level1_rows = 15;
```

---

# 🇬🇧 English Documentation

## Description

**Gamebuino AKA IDE** is a native Windows launcher (WPF / .NET 5) designed to streamline game development for the **Gamebuino AKA** console, powered by an **ESP32-S3** microcontroller with a **320×240 pixel** display.

It does not replace VS Code or PlatformIO — it complements them:

| What the IDE does | What it does NOT do |
|---|---|
| Create projects from templates | Compile/run C++ directly |
| Clone existing games from GitHub | Replace the code editor |
| Open VS Code with the correct workspace | Manage ESP-IDF dependencies manually |
| Trigger Build / Flash / Monitor | Provide a GDB debugger |
| Edit and preview PNG sprites | Animate in real-time on the console |
| Build tilemaps and export to C++ | Manage flash memory directly |

---

## Code Architecture

```
GamebuinoAKA.IDE/
├── GamebuinoAKA.IDE.sln               ← Visual Studio Solution
└── src/
    └── GamebuinoAKA.IDE/
        ├── GamebuinoAKA.IDE.csproj    ← WPF project (.NET 5, C# 9)
        ├── App.xaml / App.xaml.cs     ← Entry point, Dependency Injection (DI)
        ├── MainWindow.xaml / .cs      ← Main window with navigation sidebar
        │
        ├── Models/                    ← Data models (POCOs)
        │   ├── AppSettings.cs         ← Settings persisted in %APPDATA%
        │   ├── GamebuinoProject.cs    ← Project metadata (name, path, template…)
        │   ├── SpriteAsset.cs         ← RGB565 pixels, frames, palette
        │   └── TilemapAsset.cs        ← Tile grid, bg/fg layers, tileset
        │
        ├── Services/                  ← Business logic (no UI dependency)
        │   ├── SettingsService.cs     ← JSON read/write of application settings
        │   ├── ProjectService.cs      ← Workspace scan, recent projects management
        │   ├── TemplateService.cs     ← Project generation from embedded templates
        │   ├── PlatformIOService.cs   ← CLI calls to pio (build, flash, monitor)
        │   ├── VSCodeService.cs       ← Detection and launch of VS Code
        │   ├── GitService.cs          ← GitHub repository cloning via git CLI
        │   └── AssetService.cs        ← PNG→RGB565 import, C++ array export
        │
        ├── ViewModels/                ← MVVM: state and commands, no UI dependency
        │   ├── MainViewModel.cs       ← Page navigation, status bar
        │   ├── HomeViewModel.cs       ← Dashboard, recent projects, quick actions
        │   ├── ProjectsViewModel.cs   ← Project list, build/flash, GitHub clone
        │   ├── NewProjectViewModel.cs ← Project creation wizard
        │   ├── SpriteEditorViewModel.cs  ← Pixel editor, palette, C++ export
        │   ├── TilemapEditorViewModel.cs ← Tilemap editor, layers, C++ export
        │   └── SettingsViewModel.cs   ← Tool paths and configuration
        │
        ├── Views/                     ← XAML (UI only, no business logic)
        │   ├── HomeView.xaml          ← Dashboard with recent projects
        │   ├── ProjectsView.xaml      ← Project manager + clone dialog
        │   ├── NewProjectView.xaml    ← Creation form
        │   ├── SpriteEditorView.xaml  ← Sprite editor
        │   ├── TilemapEditorView.xaml ← Tilemap editor
        │   └── SettingsView.xaml      ← Settings page
        │
        ├── Controls/                  ← Reusable UserControls
        │   ├── PixelCanvas.xaml/.cs   ← Pixel-perfect rendering with zoom, click-to-paint
        │   └── TilemapGrid.xaml/.cs   ← Tile grid with selection and painting
        │
        ├── Converters/
        │   └── Converters.cs          ← WPF IValueConverters (bool→Visibility, RGB565→Color…)
        │
        ├── Themes/
        │   └── Dark.xaml              ← Dark Gamebuino theme (violet #7C5CD8)
        │
        └── Templates/                 ← Embedded templates (EmbeddedResource)
            ├── empty/                 ← Empty project with platformio.ini
            ├── hello-world/           ← Displays "Hello Gamebuino AKA!"
            └── game-template/         ← main.cpp + game.h + game.cpp structure
```

### Design Patterns

- **Strict MVVM**: ViewModels do not import any `System.Windows.*` namespace
- **Dependency Injection** via `Microsoft.Extensions.DependencyInjection`
- **Singleton services**, singleton Views and ViewModels (page navigation)
- **Explicit commands**: `RelayCommand`, `AsyncRelayCommand`, `RelayCommand<T>` from `Microsoft.Toolkit.Mvvm 7.1.2`
- **Change notifications**: `SetProperty(ref field, value)` → `INotifyPropertyChanged`

---

## Prerequisites

### Required

| Software | Minimum Version | Link |
|---|---|---|
| **Windows** | 10 / 11 | — |
| **.NET 5 Runtime** | 5.0.x | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/5.0) |
| **Git** | 2.x | [git-scm.com](https://git-scm.com/) |

### To compile C++ code and flash the console

| Software | Minimum Version | Link |
|---|---|---|
| **Visual Studio Code** | 1.70+ | [code.visualstudio.com](https://code.visualstudio.com/) |
| **PlatformIO IDE extension** | latest | [marketplace.visualstudio.com](https://marketplace.visualstudio.com/items?itemName=platformio.platformio-ide) |
| **C/C++ extension** | latest | [marketplace.visualstudio.com](https://marketplace.visualstudio.com/items?itemName=ms-vscode.cpptools) |

> PlatformIO automatically downloads the Xtensa ESP32 toolchain (~300 MB) on the first build.

### To build the IDE from source (developers)

| Tool | Version |
|---|---|
| .NET 5 SDK | 5.0.416+ |
| or Visual Studio 2019/2022 | with ".NET desktop development" workload |

---

## Installation

### Option A — Use the pre-compiled executable

1. Copy the `bin/Debug/net5.0-windows/` folder to your machine
2. Run `GamebuinoAKA.IDE.exe`
3. On first launch, go to **Settings** to configure:
   - The workspace folder (e.g. `C:\Users\John\Documents\GamebuinoAKA`)
   - The path to VS Code (auto-detected if in the PATH)
   - The path to `pio.exe` (auto-detected if PlatformIO is installed)

### Option B — Build from source

```powershell
# Clone the project
git clone https://github.com/your-repo/GamebuinoAKA-IDE.git
cd GamebuinoAKA-IDE

# Build
dotnet build GamebuinoAKA.IDE/GamebuinoAKA.IDE.sln

# Run
dotnet run --project GamebuinoAKA.IDE/src/GamebuinoAKA.IDE/
```

---

## User Guide

### 1. First launch — Configure the environment

On startup, the status bar shows detected versions:

```
PlatformIO: 6.1.11    VSCode: Code.exe
```

If a tool is not detected:
1. Click **⚙️ Settings** in the sidebar
2. Click **🔍 Auto-detect tools**
3. Or manually enter the paths
4. Click **💾 Save**

---

### 2. Create a new project

1. Click **➕ New project** (dashboard or sidebar)
2. Enter the **project name** (e.g. `MyFirstGame`)
3. Choose a **template**:

| Template | Generated content | Use case |
|---|---|---|
| **Empty** | `platformio.ini` + empty `src/main.cpp` | Start from scratch |
| **Hello World** | Displays text on screen | Validate the setup |
| **Game Template** | `main.cpp` + `game.h` + `game.cpp` | Complete game structure |

4. Choose or confirm the **destination folder**
5. Click **✅ Create project**

**Example — generated platformio.ini:**
```ini
[env:gamebuino_aka]
platform = espressif32
board = esp32-s3-devkitc-1
framework = arduino
monitor_speed = 115200
board_build.flash_size = 16MB
board_build.psram_type = opi
lib_deps =
    https://github.com/jmp42/Gamebuino_AKA_lib
build_flags =
    -DBOARD_HAS_PSRAM
    -mfix-esp32-psram-cache-issue
upload_protocol = esptool
```

**Example — generated Hello World main.cpp:**
```cpp
#include <Gamebuino-Meta.h>

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
    gb.display.print("Hello Gamebuino AKA!");
}
```

---

### 3. Import an existing project from GitHub

To retrieve an existing game (e.g. pAKAman, Galaxy Fighter, KONG 2…):

1. Go to **📁 Projects**
2. Click **⬇ Clone GitHub**
3. Paste the repository URL:
   ```
   https://github.com/Jicehel-Aka/pAKAman
   https://github.com/Jicehel-Aka/Galaxy-Fighter-Pokitto-ported-on-Aka
   https://github.com/Jicehel-Aka/KONG-2-POKITTO-ported-on-AKA
   https://github.com/Jicehel-Aka/Akassebricks
   https://github.com/Jicehel-Aka/Baba-Is-You-Version-AKA-
   ```
4. The folder name is automatically pre-filled
5. Click **⬇ Clone**
6. Git output streams in real-time in the bottom panel
7. Once complete, the project appears in the list

---

### 4. Working with a project

From the project list, each card offers:

| Button | Action |
|---|---|
| **📝 VS Code** | Opens the folder in VS Code with PlatformIO active |
| **🔨 Build** | Compiles the project via `pio run` |
| **⚡ Flash** | Compiles and uploads to the console via USB |
| **📡 Monitor** | Opens the serial monitor (115200 baud) |
| **📂 Explorer** | Opens the folder in Windows Explorer |
| **🗑 Delete** | Deletes the folder (with confirmation) |

PlatformIO output streams in real-time in the bottom panel (black background, green text).

**Example successful build output:**
```
Processing gamebuino_aka (platform: espressif32; board: esp32-s3-devkitc-1; framework: arduino)
Compiling .pio/build/gamebuino_aka/src/main.cpp.o
Linking .pio/build/gamebuino_aka/firmware.elf
Building .pio/build/gamebuino_aka/firmware.bin
========================= [SUCCESS] Took 23.45 seconds =========================
```

---

### 5. Sprite Editor

1. Click **🎨 Sprites** in the sidebar
2. Click **📂 Import image** → select a PNG/BMP
3. The sprite is displayed pixel-perfect with a 16-colour RGB565 palette
4. Use the **zoom** (1×, 2×, 4×, 8×) to edit pixel by pixel
5. Click a palette colour, then click the canvas to paint
6. Click **💾 Export C++** → generates an array to paste into your code

**Example exported code:**
```cpp
// Sprite: player (16x16)
// Frames: 4  Frame size: 16x16
const uint16_t player[] PROGMEM = {
    0x0000, 0x7BEF, 0x7BEF, 0xFFFF, 0xFFFF, 0x7BEF, 0x7BEF, 0x0000,
    // ... 256 RGB565 values
};
const uint8_t player_frames = 4;
const uint8_t player_frame_width = 16;
const uint8_t player_frame_height = 16;
```

---

### 6. Tilemap Editor

1. Click **🗺️ Tilemaps** in the sidebar
2. Click **📂 Import tileset** → select a PNG (grid of tiles)
3. Configure tile size (e.g. 16×16 px) and map dimensions
4. Select a tile in the left panel (tileset)
5. Paint on the right grid (map)
6. Choose the active layer: **Background** or **Foreground**
7. Click **💾 Export C++** → generates both arrays

**Example exported code:**
```cpp
// Tilemap: level1 (20x15 tiles)
// Tile size: 16x16
const uint8_t level1_bg[] PROGMEM = {
    0x01, 0x01, 0x01, 0x02, 0x01, 0x01, 0x03, 0x01,  // row 0
    0x01, 0x04, 0x01, 0x01, 0x01, 0x02, 0x01, 0x01,  // row 1
    // ...
};
const uint8_t level1_cols = 20;
const uint8_t level1_rows = 15;
```

---

## NuGet Packages / Dépendances

| Package | Version | Rôle |
|---|---|---|
| `Microsoft.Toolkit.Mvvm` | 7.1.2 | MVVM (ObservableObject, RelayCommand, AsyncRelayCommand) |
| `Microsoft.Extensions.DependencyInjection` | 5.0.2 | Injection de dépendances |
| `Newtonsoft.Json` | 13.0.3 | Sérialisation des paramètres JSON |
| `System.Drawing.Common` | 5.0.3 | Import et conversion d'images PNG/BMP |

---

## Raccourcis clavier / Keyboard Shortcuts

| Raccourci | Action |
|---|---|
| Pas de raccourcis globaux — navigation par boutons uniquement | — |

---

## Licence / License

MIT — Voir [LICENSE](LICENSE) pour les détails.

---

*Fait avec ❤️ pour la communauté Gamebuino AKA*  
*Made with ❤️ for the Gamebuino AKA community*
