// EvilDragon's Cheats - A Game About Chopping Trees
#pragma once
#include "ed_ui.h"

// ---------------- settings (written by the menu, read by the game thread) ----------------
struct Settings
{
    // player
    float speedMul = 1.0f;
    float jumpMul = 1.0f;
    bool  fly = false;
    float flySpeed = 2.0f;
    bool  god = false;
    bool  infStamina = false;
    // chopping
    bool  oneHit = false;
    float chopSpeedMul = 1.0f;
    float logValueMul = 1.0f;
    bool  bigCapacity = false;
    // menu
    int   moneyIndex = 1;
    int   logAmount = 10;
    std::string theme = "Dragon";
    int   keyMenu = VK_F1;
    int   keyMoney = VK_F2;
    int   keyFly = VK_F3;
    bool  saveSettings = false;
};

// ---------------- state (written by the game thread, shown by the menu) ----------------
struct UpgradeView
{
    std::string name;
    int level = 0, max = 0;
    float cost = 0;
    bool oneTime = false, done = false, canBuy = false;
};

struct PlaceView { std::string name; double dist = 0; };

struct GameView
{
    bool inGame = false;
    bool allowed = false;
    double money = 0;
    double health = 0, maxHealth = 0, stamina = 0, maxStamina = 0;
    double logs = 0, maxLogs = 0, weight = 0, maxWeight = 0;
    int carried = 0;
    std::vector<UpgradeView> upgrades;
    std::vector<std::string> treeTypes;
    std::vector<PlaceView> places;
    bool slot[3] = {};
};

extern Settings g_set;
extern GameView g_view;
extern std::mutex g_viewLock;

void RunOnGame(std::function<void()> fn);

// game.cpp
void Game_AddMoney(double amount);
void Game_BuyUpgrade(int index);       // -1 = max everything
void Game_AddLogs(int type, int count); // type -1 = mixed
void Game_SpawnLogs(int type, int count); // in front of you
void Game_ClearLogs();
void Game_RegrowTrees();
void Game_UnlockSkins();
void Game_Heal();
void Game_Teleport(int place);
void Game_SavePos(int slot);
void Game_LoadPos(int slot);

// menu.cpp
void Menu_SaveSettings();
