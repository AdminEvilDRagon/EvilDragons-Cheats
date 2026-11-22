// EvilDragon's Cheats - Moon Sweepers
#pragma once
#include "ed_ui.h"

// ---------------- settings (written by the menu, read by the game thread) ----------------
struct Settings
{
    // player
    float speedMul = 1.0f;
    float jumpMul = 1.0f;
    bool  fly = false;
    float flySpeed = 1.0f;
    bool  infJetpack = false;
    // dust
    float capacityMul = 1.0f;
    bool  autoFill = false;
    float payoutMul = 1.0f;
    // money / upgrades
    bool  freeUpgrades = false;
    int   moneyIndex = 1;
    // menu
    std::string theme = "Dragon";
    int   keyMenu = VK_F1;
    int   keyMoney = VK_F2;
    int   keyFly = VK_F3;
    int   keyFill = VK_F4;
    bool  saveSettings = false;
};

// ---------------- state (written by the game thread, shown by the menu) ----------------
struct UpgradeView { std::string name, group; int level = 0, max = 0; bool canBuy = false; };
struct PlaceView { std::string name, group; double dist = 0; };
struct KindView { std::string name; int kind = 0; };

struct GameView
{
    bool inGame = false;
    bool allowed = false;
    bool crew = false;
    double money = 0;
    double dust = 0, capacity = 0, cleaned = 0;
    std::vector<UpgradeView> upgrades;
    std::vector<PlaceView> places;
    std::vector<KindView> robots;
    bool slot[3] = {};
};

extern Settings g_set;
extern GameView g_view;
extern std::mutex g_viewLock;

void RunOnGame(std::function<void()> fn);

// game.cpp
void Game_AddMoney(double amount);
void Game_BuyUpgrade(int index);       // -1 = max everything
void Game_FillDust();
void Game_EmptyDust();
void Game_CleanAround(int rings);
void Game_SpawnRobot(int kind);
void Game_SpawnCapsules(int count);
void Game_SpawnCatcher();
void Game_Teleport(int place);
void Game_ToDepot();
void Game_SavePos(int slot);
void Game_LoadPos(int slot);

// menu.cpp
void Menu_SaveSettings();
