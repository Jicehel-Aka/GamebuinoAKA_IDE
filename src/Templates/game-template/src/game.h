#pragma once
#include <Gamebuino-Meta.h>

// Appelé à chaque frame pour mettre à jour la logique du jeu
void gameUpdate(Gamebuino& gb);

// Appelé à chaque frame pour dessiner l'état du jeu
void gameRender(Gamebuino& gb);
