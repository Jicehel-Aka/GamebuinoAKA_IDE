#include <Gamebuino-Meta.h>
#include "game.h"

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
    gameUpdate(gb);
    gameRender(gb);
}
