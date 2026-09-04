# Gamebuino AKA IDE — modifications

Ces fichiers remplacent (ou complètent) ceux de `GamebuinoAKA.IDE/src/`.
Copie-les au même emplacement en écrasant, en gardant l'arborescence.

## 1. PlatformIO **ou** ESP-IDF (composants CMake + coquille)

- **`Models/BuildSystem.cs`** (nouveau) : enum `PlatformIO` / `EspIdf`.
- **`Models/GamebuinoProject.cs`** : champ `BuildSystem`, `CMakeListsPath`,
  `IsEspIdf`/`IsPlatformIO`, `IsValid` accepte platformio.ini **ou**
  CMakeLists.txt, et `DetectBuildSystem()`.
- **`Services/TemplateService.cs`** (réécrit) : génère au choix un projet
  PlatformIO (inchangé) **ou** un projet ESP-IDF fidèle à ta coquille :
  `CMakeLists.txt` racine (inclut `project.cmake`), `sdkconfig.defaults`
  (PSRAM opi, flash 16 Mo, `FATFS_LFN_NONE`, FreeRTOS 1 kHz), `partitions.csv`,
  `main/CMakeLists.txt` (`REQUIRES gamebuino nvs_flash app_update esp_partition
  esp_system fatfs`), `main/app_main.cpp` (init `g_core` + boucle d'affichage,
  n'utilise que l'API vérifiée dans `gb_graphics.h`), `main/game_module.h`
  (interface coquille), `.vscode/` ESP-IDF, README de projet.
  Le composant `components/gamebuino` est **copié depuis un dossier de référence**
  (Paramètres) s'il est défini, sinon un placeholder explique comment l'ajouter.
- **`Services/EspIdfService.cs`** (nouveau) : build/flash/monitor/fullclean via
  `idf.py` (avec `export.bat` si renseigné, sinon idf.py du PATH ; port série
  configurable).
- **`Services/BuildService.cs`** (nouveau) : aiguille chaque commande vers
  PlatformIO ou ESP-IDF selon le projet.
- **`Services/ProjectService.cs`** : le scan détecte aussi les projets ESP-IDF
  (CMakeLists.txt) et renseigne leur `BuildSystem`.
- **`ViewModels/NewProjectViewModel.cs`** + **`Views/NewProjectView.xaml`** :
  sélecteur ESP-IDF / PlatformIO.
- **`ViewModels/ProjectsViewModel.cs`** : utilise `BuildService`.
- **`App.xaml.cs`** : enregistre `EspIdfService` et `BuildService`.

## 2. RGB565 vs BGR565 — **bug confirmé et corrigé**

Vérifié dans ta lib (`gb_ll_lcd.h`) :
`lcd_color_rgb(r,g,b) = (r>>3) | ((g>>2)<<5) | ((b>>3)<<11)` → **rouge en bits
bas, bleu en bits hauts = BGR565**. `core/graphics.h` le confirme
(`graphics_make_color(...) -> BGR565`). L'IDE produisait du RGB565 standard
(rouge en bits hauts) : **rouge et bleu inversés** à l'écran.

- **`Models/ColorFormat.cs`** (nouveau) : `Bgr565Aka` (défaut) / `Rgb565Std`.
- **`Services/AssetService.cs`** : `Pack`/`Unpack` respectent le format ;
  l'export par défaut est BGR565 (ordre lib). Le format est réglable
  (Paramètres) et mémorisé par asset.

### Cohérence du format de sprite
Les sprites de la coquille sont des **tableaux plats de `uint16`** (truecolor),
dessinés par `graphics_draw_bitmap565(...)` — **pas** de sprite indexé/palette.
`Models/SpriteAsset.cs` a été nettoyé en conséquence (les champs
palette/4 bpp morts sont retirés ; l'export reste 16 bpp, désormais dans le bon
ordre).

## 3. Transparence / couleur de transparence

`graphics_draw_bitmap565(..., use_transparency, transparent_key)` avec
`TRANSPARENT_KEY = 0xF81F` (magenta) par défaut.

- `SpriteAsset`/`TilemapAsset` : `UseTransparency` + `TransparentKey`.
- Import PNG : les pixels alpha < 128 deviennent la couleur-clé.
- Éditeur sprite : cases « utiliser la clé » / « peindre en transparent »,
  bouton « définir la clé = couleur sélectionnée », preview alpha réel.
- Export C++ : commentaire + `#define <NOM>_TRANSPARENT 0xF81F`.

## 4. Performances + sauvegarde ré-éditable

- **Perf** : import image via **`LockBits`** (pointeurs, au lieu de `GetPixel`
  pixel par pixel) ; export via `StringBuilder` pré-dimensionné + table hex.
- **Sauvegarde ré-éditable** : formats JSON **`.gbspr`** (sprite) et
  **`.gbmap`** (tilemap) qui stockent tout (dimensions, frames, format couleur,
  transparence, pixels). Boutons « Enregistrer projet » / « Ouvrir projet » dans
  les deux éditeurs.

## 5. Icône de l'application

- `GamebuinoAKA.IDE.csproj` : `<ApplicationIcon>Assets\logo.ico</...>`.
- `MainWindow.xaml` : icône de fenêtre + logo dans la barre latérale.
- `Assets/logo.ico` et `Assets/logo.png` fournis (fond noir → transparent).

---

## À faire côté Paramètres (une fois)
1. **Composant gamebuino de référence** → le dossier `components/gamebuino`
   d'un de tes projets (ex. `mAKArena/components/gamebuino`). Il sera copié dans
   chaque nouveau projet ESP-IDF.
2. **Script d'environnement ESP-IDF** (`export.bat`) → pour que Build/Flash/
   Monitor fonctionnent. Renseigne aussi le **port série** (ex. COM5).
3. Le **format couleur** par défaut est déjà **BGR565 (AKA)** — à garder.

## Notes
- La cible du `.csproj` fourni est **`net10.0-windows`** (compilée nativement par
  le SDK .NET 10). Les paquets NuGet sont en 8.0.x, compatibles net10.
- `PlatformIOService`, `MainViewModel`, `HomeViewModel` sont inchangés.

---

## Mise à jour — lib embarquée + auto-détection ESP-IDF

### Lib Gamebuino embarquée dans l'IDE
- **`Lib/gamebuino/`** (nouveau) : le composant `components/gamebuino` est
  désormais **fourni avec l'IDE**. Le `.csproj` le copie dans le dossier de
  sortie (`bin\...\Lib\gamebuino`).
- **`Services/TemplateService.cs`** : à la création d'un projet ESP-IDF, la
  source du composant suit cet ordre : dossier de référence configuré → **lib
  embarquée** → placeholder. Résultat : un nouveau projet ESP-IDF est
  **directement buildable**, sans rien configurer.
- `Bin2Hex.exe` (outil de conversion, non nécessaire à la compilation) a été
  retiré du bundle pour ne pas embarquer un binaire inutile. Il reste dans tes
  projets de référence si besoin.

### Auto-détection étendue
Le bouton « Auto-détecter les outils » remplit maintenant aussi :
- **idf.py / export.bat** : lus depuis `~/.espressif/esp_idf.json` (extension
  VS Code), sinon la variable `IDF_PATH`, sinon les emplacements usuels
  (`C:\Espressif\frameworks\esp-idf-*`, `~\esp\esp-idf`, `C:\esp\esp-idf`).
- **Port série** : rempli automatiquement s'il n'y a qu'un seul port COM ;
  si plusieurs, ils sont listés dans le message de statut pour que tu choisisses.
- **Composant gamebuino de référence** : pointé sur la lib embarquée si le champ
  est vide, sinon sur un projet du workspace contenant `components/gamebuino`.

### Fichiers touchés par cette mise à jour
`Services/EspIdfService.cs`, `Services/TemplateService.cs`,
`ViewModels/SettingsViewModel.cs`, `GamebuinoAKA.IDE.csproj`,
et le dossier `Lib/gamebuino/`.

> Note : le `.csproj` ajoute le paquet `System.IO.Ports` (8.0.0) pour énumérer
> les ports COM (compatible net10).

---

## Correctif — routage PlatformIO vs ESP-IDF par projet

**Bug** : un projet PlatformIO pour ESP32 possède souvent aussi un `CMakeLists.txt`
(voire un `project.cmake`), si bien qu'il était classé ESP-IDF → Build/Flash/Monitor
lançaient `idf.py` au lieu de `pio`.

**Correction** (`Models/GamebuinoProject.cs`, `Services/ProjectService.cs`) — nouvelle
priorité de détection :
1. marqueur explicite **`.aka-build`** à la racine du projet (contenu `espidf` ou
   `platformio`) ;
2. **`platformio.ini` présent → PlatformIO** (marqueur décisif : un projet ESP-IDF
   pur n'en a jamais) ;
3. `CMakeLists.txt` avec marqueurs IDF (`project.cmake` / `idf_component_register`
   / dossier `main`) → ESP-IDF ;
4. sinon, la **chaîne par défaut** des Paramètres.

**Override manuel** : chaque carte de projet affiche désormais un badge
PlatformIO/ESP-IDF et un bouton **↔** pour forcer la chaîne. Le choix est figé dans
un fichier `.aka-build` (donc conservé aux prochains scans). Les nouveaux projets
ESP-IDF créés par l'IDE posent ce marqueur automatiquement.

Fichiers touchés : `Models/GamebuinoProject.cs`, `Services/ProjectService.cs`,
`Services/TemplateService.cs`, `ViewModels/ProjectsViewModel.cs`,
`Views/ProjectsView.xaml`.

---

## Correctif — crash au clic sur « Nouveau projet »

`NewProjectViewModel` fixait `DestinationFolder` dans son constructeur AVANT de
créer `CreateProjectCommand` ; or le setter appelle
`CreateProjectCommand.NotifyCanExecuteChanged()` → NullReferenceException →
l'appli se fermait dès l'ouverture de la vue. Corrigé : les commandes sont créées
en premier, et les setters utilisent `?.` par sécurité.
Fichier : `ViewModels/NewProjectViewModel.cs`.

---

## Ajout — journal + capture globale des erreurs

- **`Services/Log.cs`** (nouveau) : journal fichier thread-safe
  (`%APPDATA%\GamebuinoAKA\gamebuino-ide.log`), rotation à ~1 Mo (`.old`),
  méthodes `Info/Warn/Error` et `Clear()`.
- **`App.xaml.cs`** : trois filets de sécurité globaux —
  `DispatcherUnhandledException` (thread UI, `e.Handled=true` : l'appli ne se
  ferme plus), `AppDomain.UnhandledException` (threads de fond) et
  `TaskScheduler.UnobservedTaskException`. Chaque erreur est journalisée **et**
  affichée dans une fenêtre d'erreur indiquant le chemin du journal. Le démarrage
  est aussi protégé.
- **Paramètres → Journal** : chemin du fichier + boutons « Ouvrir le dossier » et
  « Supprimer le journal » (`ViewModels/SettingsViewModel.cs`,
  `Views/SettingsView.xaml`).
- Journalisation ajoutée aux points sensibles : création de projet, clonage
  GitHub, opérations de build/flash/monitor.

Fichiers touchés : `Services/Log.cs` (nouveau), `App.xaml.cs`,
`ViewModels/SettingsViewModel.cs`, `Views/SettingsView.xaml`,
`ViewModels/NewProjectViewModel.cs`, `ViewModels/ProjectsViewModel.cs`.

---

## Politique d'erreurs à trois niveaux

Les gestionnaires globaux appliquent désormais :

1. **Erreur récupérable (thread UI)** → journalisée, affichée, puis l'IDE **revient
   à un état stable** (navigation vers l'accueil, vue minimale sûre) et continue.
2. **Erreur bénigne** (tâche async non observée, thread de fond non terminal) →
   journalisée et signalée, l'IDE continue.
3. **Non récupérable** → journalisée, affichée, puis **fermeture propre automatique**.
   Ce cas couvre : les erreurs critiques (`OutOfMemoryException`,
   `AccessViolationException`, `SEHException`, `AppDomainUnloadedException`,
   `BadImageFormatException`), l'échec de la remise en état stable, l'échec au
   démarrage, et toute erreur survenant *pendant* une récupération (garde
   anti-réentrance `_handlingError`).

Fichier : `App.xaml.cs`.

---

## Sécurité — sprites volumineux (plus de gel de l'IDE)

Charger une grande image (p.ex. 1536x1024 = 1,5 M pixels) figeait l'IDE : la
génération puis l'affichage du code C++ (~12 Mo, 1,5 M de valeurs) saturait le
thread UI.

Correctifs (`ViewModels/SpriteEditorViewModel.cs`) :
- **Garde de taille** : au-delà de l'écran AKA (320x240), l'aperçu du code est
  désactivé et remplacé par un message expliquant d'utiliser « Importer
  spritesheet » (planche) ou « Exporter C++ » (fichier complet).
- **Génération hors thread UI** : l'aperçu et l'export fichier sont produits via
  `Task.Run`, l'UI ne gèle plus.
- **Aperçu plafonné** à ~200 000 caractères (le fichier exporté, lui, reste
  complet).
- « Exporter C++ » est désormais asynchrone et écrit toujours le code **complet**
  (avant, il pouvait écrire l'aperçu tronqué).

---

## Nouveau workflow — découper/réduire un sprite depuis une planche

L'éditeur de sprites est réorganisé autour d'une **image de travail** ; le code
C++ n'est produit qu'à la fin.

- **`Controls/PixelCanvas`** : sélection rectangulaire au **glisser** (rectangle
  bleu visible), en plus du mode peinture. Nouvelles propriétés `SelectionMode`,
  `Selection` (Rect, coordonnées image, two-way) et `SelectCommand`.
- **`Services/AssetService`** : outils image `LoadArgb`, `Crop`, `Resize`
  (bicubique haute qualité ou au plus proche), `PackBitmap`, `BuildSprite`,
  `UnpackToBitmap` (réouverture éditable).
- **`ViewModels/SpriteEditorViewModel`** (réécrit) : importer → sélectionner →
  **Rogner** → régler la **taille cible** → **Réduire** → retoucher au pixel →
  **Convertir** (aperçu code) / **Exporter C++** (fichier). Sélection éditable
  aussi en numérique (X/Y/L/H). Transparence et projet `.gbspr` conservés
  (réouverture reconstruit une image éditable).
- **`Views/SpriteEditorView.xaml`** : panneau d'outils Image / Sélection /
  Réduire / Retouche / Transparence / Conversion. Le panneau de code indique
  qu'il est « généré à la conversion ».

Le bouton « Importer spritesheet » (découpe en grille fixe) est retiré au profit
de la sélection libre + rognage.

---

## Retour de l'import planche (grille fixe, taille paramétrable)

Complément du découpage libre : pour une planche RÉGULIÈRE, on réimporte toute
la page en une série de frames de taille fixe, en un seul geste.

- **`ViewModels/SpriteEditorViewModel`** : `ImportSpritesheetCommand` +
  `FrameWidth`/`FrameHeight` (paramétrables, plus de 16×16 codé en dur) +
  `FrameInfo` (aperçu « cols×rows = N frames », avec avertissement si la taille
  ne divise pas la planche). La conversion/export émet un tableau unique avec
  `_width`/`_height`/`_frames` (métadonnées de frame), conforme à la lib. Le
  rognage/réduction repasse en frame unique ; l'ouverture d'un `.gbspr`
  restaure la découpe.
- **`Services/AssetService.BuildSprite`** : accepte désormais frame largeur /
  hauteur / nombre.
- **`Views/SpriteEditorView.xaml`** : section « Planche (grille fixe) » avec
  champs Frame L / Frame H, bouton « Importer planche (grille) » et résumé.

Découpe libre (glisser + rogner) et import planche coexistent : l'une pour
l'unitaire/l'irrégulier, l'autre pour les planches à grille régulière.

---

## Correctif — image non affichée / non sélectionnable après import

Après « Importer image », l'image ne s'affichait pas (donc rien à sélectionner).
Causes : conversion vers l'affichage trop fragile, et zoom via `Stretch="None"`
qui ne mettait pas l'image à l'échelle.

- **`ViewModels/SpriteEditorViewModel`** : conversion image→affichage fiabilisée
  (passage par un PNG en mémoire, alpha préservé) ; zoom auto ajusté à la taille
  (`FitZoom`) ; **mode sélection activé automatiquement** à l'import d'une image
  (glisser = sélectionner tout de suite) ; sélection réinitialisée à chaque
  nouvelle image de travail.
- **`Controls/PixelCanvas`** : `Stretch="Fill"` + alignement haut-gauche, pour
  que le zoom mette réellement l'image à l'échelle (pixels nets en
  NearestNeighbor) et que le mapping des clics reste exact.
