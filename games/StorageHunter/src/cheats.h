// EvilDragon's Cheats - Storage Hunter Simulator
#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <functional>
#include <mutex>
#include <string>
#include <vector>

#define ED_VERSION "1.0.0"

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
    // tools / workshop
    bool  alwaysVerifyFix = false;
    // auctions
    bool  brokeBidders = false;
    // vehicle
    bool  infiniteFuel = false;
    // world
    bool  freezeTime = false;
    float timeSpeed = 1.0f;
    // menu
    int   moneyIndex = 2;
    std::string theme = "Dragon";
    int   keyMenu = VK_F1;
    int   keyMoney = VK_F2;
    int   keyFly = VK_F3;
    bool  saveSettings = false;
};

// ---------------- state (written by the game thread, shown by the menu) ----------------
struct DistrictView { std::string name; int order = 0, level = 0, points = 0, required = 0, perks = 0; };

struct GameView
{
    std::vector<DistrictView> districts;
    bool inGame = false;
    bool allowed = false;     // false while other players are in the session
    int  players = 0;
    int  money = 0;
    int  level = 0;
    int  maxLevel = 0;
    int  xp = 0;
    int  tickets = 0;
    int  verifyLevel = 0, fixLevel = 0, lockLevel = 0;
    double timeOfDay = 0;     // 0..2400
    bool hasSky = false;
    int  passedDays = 0;
};

extern Settings g_set;
extern GameView g_view;
extern std::mutex g_viewLock;
extern bool g_menuOpen;
extern HMODULE g_self;

// ---------------- items (filled on the game thread) ----------------
struct ItemEntry { std::string name, cls; int cat = 0, rarity = 0, value = 0, index = 0; };
extern std::vector<ItemEntry> g_items;
extern std::vector<std::string> g_itemCats;   // category names, index = EItemsCategories
extern std::mutex g_itemsLock;
void Game_ScanItems();
void Game_SpawnItem(int index, int count, bool verified);

// run something on the game thread (next tick)
void RunOnGame(std::function<void()> fn);

// game.cpp
void Game_Init();
void Game_SetMenuOpen(bool open);
void Game_AddMoney(int amount);
void Game_AddXp(int amount);
void Game_AddPerkPoints(int amount, int district);   // district -1 = all
void Game_AddRenown(int amount, int district);
void Game_SetToolLevels(int verify, int fix, int lockpick);
void Game_PawnShopLevelUp(int levels);
void Game_BuyPawnShop();
void Game_WinAuction();
void Game_UnlockContainer();
void Game_AuthenticateItem();
void Game_CompleteTask();
void Game_CompleteQuest();
void Game_FillFuel();
void Game_SpawnRandomItems(int count);
void Game_SetTimeOfDay(double t);
void Game_AddTickets(int amount);

// menu.cpp
void Menu_Draw();
void Menu_HandleHotkeys();
void Menu_LoadSettings();
void Menu_SaveSettings();
void Log(const char* fmt, ...);

// render.cpp
void* Render_LogoTexture();   // ImTextureID for the logo (nullptr if not loaded)
