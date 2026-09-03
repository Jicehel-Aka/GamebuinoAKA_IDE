using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GamebuinoAKA.IDE.Models;

namespace GamebuinoAKA.IDE.Services
{
    public class TemplateService
    {
        private const string ResourcePrefix = "GamebuinoAKA.IDE.Templates.";

        /// <summary>Creates a new project from the given template into the destination folder.</summary>
        public async Task CreateProjectAsync(string projectName, string template, string destinationFolder)
        {
            var projectDir = System.IO.Path.Combine(destinationFolder, projectName);
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(System.IO.Path.Combine(projectDir, "src"));
            Directory.CreateDirectory(System.IO.Path.Combine(projectDir, ".vscode"));

            // Write platformio.ini (common to all templates)
            await WriteFileAsync(projectDir, "platformio.ini", BuildPlatformIni(projectName));

            // Write src/main.cpp from embedded resource or fallback
            var mainCppContent = ReadEmbeddedTemplate(template, "src.main.cpp")
                                 ?? GetFallbackMainCpp(template);
            await WriteFileAsync(System.IO.Path.Combine(projectDir, "src"), "main.cpp", mainCppContent);

            // Additional files for game-template
            if (template == "game-template")
            {
                var gameH = ReadEmbeddedTemplate(template, "src.game.h") ?? GetFallbackGameH();
                var gameCpp = ReadEmbeddedTemplate(template, "src.game.cpp") ?? GetFallbackGameCpp();
                await WriteFileAsync(System.IO.Path.Combine(projectDir, "src"), "game.h", gameH);
                await WriteFileAsync(System.IO.Path.Combine(projectDir, "src"), "game.cpp", gameCpp);
            }

            // Write .vscode/c_cpp_properties.json
            await WriteFileAsync(System.IO.Path.Combine(projectDir, ".vscode"), "c_cpp_properties.json",
                BuildCppProperties());

            // Write .vscode/extensions.json
            await WriteFileAsync(System.IO.Path.Combine(projectDir, ".vscode"), "extensions.json",
                BuildExtensionsJson());
        }

        private static async Task WriteFileAsync(string directory, string filename, string content)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(System.IO.Path.Combine(directory, filename), content);
        }

        private static string? ReadEmbeddedTemplate(string template, string relativePath)
        {
            var resourceName = $"{ResourcePrefix}{template}.{relativePath}";
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static string BuildPlatformIni(string projectName)
        {
            return
@"[env:gamebuino_aka]
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
upload_protocol = esptool";
        }

        private static string GetFallbackMainCpp(string template)
        {
            switch (template)
            {
                case "hello-world":
                    return
@"#include <Gamebuino-Meta.h>

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
    gb.display.print(""Hello Gamebuino AKA!"");
}";
                case "game-template":
                    return
@"#include <Gamebuino-Meta.h>
#include ""game.h""

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
    gameUpdate(gb);
    gameRender(gb);
}";
                default:
                    return
@"#include <Gamebuino-Meta.h>

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
}";
            }
        }

        private static string GetFallbackGameH()
        {
            return
@"#pragma once
#include <Gamebuino-Meta.h>

void gameUpdate(Gamebuino& gb);
void gameRender(Gamebuino& gb);";
        }

        private static string GetFallbackGameCpp()
        {
            return
@"#include ""game.h""

void gameUpdate(Gamebuino& gb) {
    // Logique de jeu ici
}

void gameRender(Gamebuino& gb) {
    // Rendu ici
}";
        }

        private static string BuildCppProperties()
        {
            return
@"{
    ""configurations"": [
        {
            ""name"": ""PlatformIO"",
            ""includePath"": [
                ""${workspaceFolder}/**"",
                ""${env:USERPROFILE}/.platformio/packages/framework-arduinoespressif32/cores/esp32"",
                ""${env:USERPROFILE}/.platformio/packages/framework-arduinoespressif32/variants/esp32s3"",
                ""${env:USERPROFILE}/.platformio/lib/**""
            ],
            ""defines"": [
                ""ARDUINO"",
                ""BOARD_HAS_PSRAM"",
                ""ESP32"",
                ""ESP_PLATFORM""
            ],
            ""compilerPath"": ""${env:USERPROFILE}/.platformio/packages/toolchain-xtensa-esp-elf/bin/xtensa-esp-elf-gcc.exe"",
            ""cStandard"": ""c11"",
            ""cppStandard"": ""c++17"",
            ""intelliSenseMode"": ""gcc-x64""
        }
    ],
    ""version"": 4
}";
        }

        private static string BuildExtensionsJson()
        {
            return
@"{
    ""recommendations"": [
        ""platformio.platformio-ide"",
        ""ms-vscode.cpptools""
    ]
}";
        }
    }
}
