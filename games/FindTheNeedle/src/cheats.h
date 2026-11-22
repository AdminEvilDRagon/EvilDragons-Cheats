// EvilDragon's Cheats - Find The Needle
#pragma once
#include "ed_ui.h"

// ---------------- settings (written by the menu, read by the game thread) ----------------
struct Settings
{
    // player
    float speedMul = 1.0f;
    bool  fly = false;
    bool  noFallDeath = false;
    bool  infStamina = false;
    bool  infJetpack = false;
    // money
    int   moneyIndex = 1;
    // spawn
    int   spawnCount = 1;
    // menu
    std::string theme = "Dragon";
    int   keyMenu = VK_F1;
    int   keyMoney = VK_F2;
    int   keyFly = VK_F3;
    bool  saveSettings = false;
};

// ---------------- state (written by the game thread, shown by the menu) ----------------
struct TechView { std::string name, branch; int rank = 0, max = 0; };
struct NamedView { std::string name, detail; bool done = false; };
struct PlaceView { std::string name, group; double dist = 0; };

struct GameView
{
    bool inGame = false;
    bool allowed = false;
    double money = 0, debt = 0;
    double stamina = 0, staminaMax = 0, fuel = 0;
    int needlesFound = 0, needlesTotal = 0;
    std::vector<TechView> techs;
    std::vector<NamedView> tools;
    std::vector<NamedView> items;
    std::vector<PlaceView> places;
    bool slot[3] = {};
};

extern Settings g_set;
extern GameView g_view;
extern std::mutex g_viewLock;

void RunOnGame(std::function<void()> fn);

// game.cpp
void Game_AddMoney(double amount);
void Game_PayDebt();
void Game_BuyTech(int index);      // -1 = everything
void Game_GiveTool(int index);     // -1 = every tool
void Game_SpawnItem(int index, int count);
void Game_RefillStamina();
void Game_Teleport(int place);
void Game_SavePos(int slot);
void Game_LoadPos(int slot);

// menu.cpp
void Menu_SaveSettings();
