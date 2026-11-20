// EvilDragon's Cheats - Storage Hunter Simulator
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
    // tools / workshop
    bool  alwaysVerifyFix = false;
    // auctions
    bool  brokeBidders = false;
    // vehicle
    bool  infiniteFuel = false;
    // world
    bool  freezeTime = false;
    float timeSpeed = 1.0f;
    // unanchor
    bool  unanchorClick = true;
    bool  unanchorBig = false;
    bool  unanchorNoGravity = false;
    float unanchorPush = 0.0f;
    float unanchorRange = 8.0f;
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
struct LocEntry { std::string name; int cat = 0; double x = 0, y = 0, z = 0; };   // cat: 0 house, 1 pawn shop, 2 auction, 3 garage
extern std::vector<LocEntry> g_locs;
extern std::mutex g_locsLock;
void Game_TeleportTo(double x, double y, double z);
void Game_BringCar();
void Game_TeleportToCar();
void Game_SavePosition();
void Game_LoadPosition();
void Game_UnanchorClick(bool useMouse, bool freeze);
void Game_UnanchorRange(float meters);
void Game_UndoUnanchor(bool all);
void Game_AnchorAll();
void Game_SetUnanchorGravity(bool on);

// menu.cpp
void Menu_SaveSettings();
