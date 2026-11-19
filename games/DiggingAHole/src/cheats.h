// EvilDragon's Cheats - A Game About Digging A Hole
#pragma once
#include "ed_ui.h"

// ---------------- settings (written by the menu, read by the game thread) ----------------
struct Settings
{
    // player
    float speedMul = 1.0f;
    float jumpMul = 1.0f;
    float gravity = 1.0f;
    bool  unlimitedJumps = false;
    bool  fly = false;
    float flySpeed = 2.0f;
    bool  god = false;
    bool  infBattery = false;
    // tools
    float drillMul = 1.0f;
    float radiusMul = 1.0f;
    float jetpackMul = 1.0f;
    bool  noDepthLimit = false;
    bool  bigInventory = false;
    // ores
    float oreRadius = 10.0f;      // metres
    bool  oreToBag = true;
    bool  autoCollect = false;
    // menu
    int   moneyIndex = 1;
    std::string theme = "Dragon";
    int   keyMenu = VK_F1;
    int   keyMoney = VK_F2;
    int   keyFly = VK_F3;
    bool  saveSettings = false;
};

// ---------------- state (written by the game thread, shown by the menu) ----------------
static const int kOreCount = 10;
extern const char* const kOreNames[kOreCount];

struct GameView
{
    bool inGame = false;
    bool allowed = false;
    int  money = 0;
    int  ores[kOreCount] = {};
    int  inventorySize = 0;
    double depth = 0, health = 0, maxHealth = 0, battery = 0, maxBattery = 0;
    double drill = 0, drillRadius = 0, jetpack = 0;
    int  dynamite = 0, lamps = 0, keys = 0, teleporters = 0;
    bool drillUnlocked = false, jetpackUnlocked = false;
    bool slot[3] = {};
};

extern Settings g_set;
extern GameView g_view;
extern std::mutex g_viewLock;

void RunOnGame(std::function<void()> fn);

// game.cpp
void Game_SetMenuOpen(bool open);
void Game_AddMoney(int amount);
void Game_AddOre(int ore, int amount);       // ore -1 = all
void Game_ClearOres();
void Game_CollectOres(float radiusMetres, bool toBag);
void Game_AddTool(int which, int amount);    // 0 dynamite, 1 lamps, 2 keys, 3 teleporters
void Game_Unlock(bool drill, bool jetpack);
void Game_Heal();
void Game_SavePos(int slot);
void Game_LoadPos(int slot);
void Game_ToSurface();
struct SpawnDef { const char* name; const char* cls; };
extern const SpawnDef kSpawns[];
extern const int kSpawnCount;
void Game_Spawn(int index, int count, bool crosshair, float radius);

// menu.cpp
void Menu_SaveSettings();
