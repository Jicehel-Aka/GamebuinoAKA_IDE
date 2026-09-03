#include "game.h"

// Variables d'état globales
static int playerX = 160;
static int playerY = 120;
static const int PLAYER_SPEED = 2;

void gameUpdate(Gamebuino& gb) {
    // Déplacement du joueur
    if (gb.buttons.repeat(BUTTON_LEFT,  1)) playerX -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_RIGHT, 1)) playerX += PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_UP,    1)) playerY -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_DOWN,  1)) playerY += PLAYER_SPEED;

    // Contraindre à l'écran (320x240)
    playerX = max(0, min(315, playerX));
    playerY = max(0, min(235, playerY));
}

void gameRender(Gamebuino& gb) {
    // Fond
    gb.display.setColor(BLACK);
    gb.display.fill();

    // Joueur (carré 5x5 violet)
    gb.display.setColor(0x7C5C); // Violet Gamebuino
    gb.display.fillRect(playerX, playerY, 5, 5);

    // HUD
    gb.display.setColor(WHITE);
    gb.display.setCursor(4, 4);
    gb.display.print("Gamebuino AKA");
}
