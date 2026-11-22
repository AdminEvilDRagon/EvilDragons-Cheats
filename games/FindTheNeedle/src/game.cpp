// Find The Needle game logic.
// The DLL stands in for libterrain (Terrain3D): Godot loads it as a GDExtension, we pass the init on to the
// real library (renamed to *.original.dll) and keep the engine interface. Everything that touches the game
// runs on Godot's main thread in the main-loop frame callback; the menu only reads g_view and queues jobs.
#include "cheats.h"
#include "gd.h"
#include <cmath>

using namespace gd;

// ---------------- jobs ----------------
static std::mutex g_qLock;
static std::vector<std::function<void()>> g_queue;
void RunOnGame(std::function<void()> fn) { std::lock_guard<std::mutex> lk(g_qLock); g_queue.push_back(std::move(fn)); }
static void Result(const std::string& s, bool error = false) { Log("%s", s.c_str()); Notify(s, error); }

// ---------------- engine objects (only touched on the main thread) ----------------
struct Refs
{
    V tree, root, game, tech, leaderboard, player, props;
    V techTree, itemDb, needleTypes;   // static data scripts
    bool scripts = false;
};
static Refs* R;   // created on the first frame (Variants need the engine)

static V Node(const V& from, const char* path) { V p = FromStr(path); return Call(from, "get_node_or_null", { &p }); }
static V FirstInGroup(const char* group)
{
    V g = FromName(group);
    V list = Call(R->tree, "get_nodes_in_group", { &g });
    return Size(list) > 0 ? Index(list, 0) : V();
}
static V Script(const char* path) { V loader = Singleton("ResourceLoader"); V p = FromStr(path); return Call(loader, "load", { &p }); }
static V S(const char* s) { return FromStr(s); }
static std::string Txt(const V& v) { return Text(v); }

static bool Refresh()
{
    if (!R) R = new Refs;
    V engine = Singleton("Engine");
    R->tree = Call(engine, "get_main_loop");
    if (R->tree.nil()) return false;
    R->root = Call(R->tree, "get_root");
    R->game = Node(R->root, "GameState");
    R->tech = Node(R->root, "Tech");
    R->leaderboard = Node(R->root, "Leaderboard");
    R->player = FirstInGroup("player");
    R->props = FirstInGroup("prop_manager");
    if (!R->scripts)
    {
        R->techTree = Script("res://scripts/tech/tech_tree.gd");
        R->itemDb = Script("res://scripts/items/item_db.gd");
        R->needleTypes = Script("res://scripts/needles/needle_types.gd");
        R->scripts = !R->techTree.nil();
    }
    return !R->game.nil() && !R->player.nil();
}

// cheat runs never count for the online boards - the game has its own flags for that
static void Taint()
{
    Set(R->game, "debug_used", FromBool(true));
    if (!R->leaderboard.nil()) Set(R->leaderboard, "modded", FromBool(true));
}

// ---------------- data lists ----------------
struct TechEntry { std::string id, name, branch; int max; };
static std::vector<TechEntry> g_techs;
static const char* kTools[] = { "spade", "pitchfork", "broom", "sand_shovel", "metal_detector", "yard_vac", "lighter", "forcetron" };
static const char* kToolNames[] = { "Spade", "Pitchfork", "Broom", "Sand Shovel", "Metal Detector", "Yard Vac", "Lighter", "Forcetron" };
struct ItemEntry { std::string id, name; };
static std::vector<ItemEntry> g_items;

static void BuildLists()
{
    if (g_techs.empty() && !R->techTree.nil())
    {
        V ids = Call(R->techTree, "ids");
        for (int64_t i = 0; i < Size(ids); i++)
        {
            V id = Index(ids, i);
            TechEntry t;
            t.id = Txt(id);
            t.name = Txt(Call(R->techTree, "display_name", { &id }));
            V br = Call(R->techTree, "branch_of", { &id });
            t.branch = Txt(Call(R->techTree, "branch_name", { &br }));
            t.max = (int)ToInt(Call(R->techTree, "max_rank", { &id }));
            if (ToBool(Call(R->techTree, "is_endless", { &id })) || t.max <= 0 || t.max > 50) t.max = std::max(1, std::min(t.max, 20));
            if (t.name.empty()) t.name = t.id;
            g_techs.push_back(t);
        }
        Log("%d techs", (int)g_techs.size());
    }
    if (g_items.empty() && !R->itemDb.nil())
    {
        V ids = Call(R->itemDb, "ids");
        for (int64_t i = 0; i < Size(ids); i++)
        {
            V id = Index(ids, i);
            ItemEntry it{ Txt(id), Txt(Call(R->itemDb, "display_name", { &id })) };
            if (it.name.empty()) it.name = it.id;
            g_items.push_back(it);
        }
        Log("%d items", (int)g_items.size());
    }
}

// ---------------- places ----------------
struct Place { std::string name, group; float pos[3]; };
static std::vector<Place> g_places;
static float g_slots[3][3];
static bool g_slotSet[3];

static void PlayerPos(float out[3]) { ToVec3(Get(R->player, "global_position"), out); }
static void TeleportNow(const float p[3])
{
    Set(R->player, "global_position", FromVec3(p[0], p[1], p[2]));
    Set(R->player, "velocity", FromVec3(0, 0, 0));
}

static void BuildPlaces()
{
    g_places.clear();
    V pos = Get(R->game, "needle_positions");
    V taken = Get(R->game, "needle_taken");
    V types = Get(R->game, "needle_type");
    int64_t n = Size(pos);
    for (int64_t i = 0; i < n; i++)
    {
        if (ToInt(Index(taken, i)) != 0) continue;
        Place p;
        ToVec3(Index(pos, i), p.pos);
        p.pos[1] += 1.2f;
        V type = Index(types, i);
        std::string nm = R->needleTypes.nil() ? "" : Txt(Call(R->needleTypes, "name_of", { &type }));
        p.name = nm.empty() ? "Needle " + std::to_string(i + 1) : nm;
        p.group = "needle in the hay";
        g_places.push_back(p);
    }
    if (!ToBool(Get(R->game, "key_taken")))
    {
        Place p{ "The key", "key" };
        ToVec3(Get(R->game, "key_at"), p.pos);
        if (fabsf(p.pos[0]) + fabsf(p.pos[1]) + fabsf(p.pos[2]) > 0.01f) { p.pos[1] += 1.0f; g_places.push_back(p); }
    }
    Place spawn{ "Yard start", "yard" };
    ToVec3(Get(R->player, "respawn_fallback"), spawn.pos);
    g_places.push_back(spawn);
}

// a tool is yours once its licence is bought (you only carry up to 5 at a time)
static bool ToolOwned(const char* tool)
{
    V id = S(tool);
    if (ToBool(Call(R->game, "has_tool", { &id }))) return true;
    return !R->tech.nil() && (ToBool(Call(R->tech, "is_unlocked", { &id })) || ToInt(Call(R->tech, "rank_of", { &id })) > 0);
}

// ---------------- per frame ----------------
static int g_frames;
static bool g_flyOn, g_mouseFreed, g_godOn;
static float g_speedSet = -1;
static ULONGLONG g_nextView, g_nextPlaces, g_nextTaint;

static void Frame()
{
    g_frames++;
    if (!Refresh()) { std::lock_guard<std::mutex> lk(g_viewLock); g_view.inGame = g_view.allowed = false; return; }
    bool allowed = true;   // single-player game

    std::vector<std::function<void()>> jobs;
    { std::lock_guard<std::mutex> lk(g_qLock); jobs.swap(g_queue); }
    if (!jobs.empty()) Taint();
    for (auto& j : jobs) j();

    V& pl = R->player;
    // movement
    float speed = g_set.speedMul;
    if (speed != g_speedSet) { Set(pl, "move_scale", FromFloat(speed)); g_speedSet = speed; }
    if (g_set.fly != g_flyOn) { Set(pl, "noclip", FromBool(g_set.fly)); g_flyOn = g_set.fly; }
    if (g_set.noFallDeath != g_godOn) { Set(pl, "fall_kills", FromBool(!g_set.noFallDeath)); g_godOn = g_set.noFallDeath; }
    if (g_set.infStamina)
    {
        V st = Get(pl, "stamina");
        if (!st.nil()) Set(st, "current", Call(st, "maximum"));
    }
    if (g_set.infJetpack && !R->tech.nil())
        Set(R->game, "jetpack_fuel", Call(R->tech, "jetpack_tank_seconds"));

    ULONGLONG now = GetTickCount64();
    bool cheating = g_set.fly || g_set.noFallDeath || g_set.infStamina || g_set.infJetpack || g_set.speedMul != 1.0f;
    if (cheating && now >= g_nextTaint) { Taint(); g_nextTaint = now + 2000; }

    // free the mouse while the menu is open
    V input = Singleton("Input");
    if (g_menuOpen && !g_mouseFreed) { Set(input, "mouse_mode", FromInt(0)); g_mouseFreed = true; }
    else if (g_menuOpen) { if (ToInt(Get(input, "mouse_mode")) != 0) Set(input, "mouse_mode", FromInt(0)); }
    else if (g_mouseFreed) { Set(input, "mouse_mode", FromInt(2)); g_mouseFreed = false; }

    if (now < g_nextView) return;
    g_nextView = now + 300;
    BuildLists();
    if (now >= g_nextPlaces) { BuildPlaces(); g_nextPlaces = now + 2000; }

    GameView v;
    v.inGame = true;
    v.allowed = allowed;
    v.money = Num(Get(R->game, "money"));
    v.debt = Num(Get(R->game, "debt"));
    V st = Get(pl, "stamina");
    if (!st.nil()) { v.stamina = Num(Get(st, "current")); v.staminaMax = Num(Call(st, "maximum")); }
    v.fuel = Num(Get(R->game, "jetpack_fuel"));
    v.needlesFound = (int)ToInt(Get(R->game, "needles_found"));
    v.needlesTotal = (int)Size(Get(R->game, "needle_positions"));
    for (auto& t : g_techs)
    {
        V id = S(t.id.c_str());
        v.techs.push_back({ t.name, t.branch, (int)ToInt(Call(R->tech, "rank_of", { &id })), t.max });
    }
    for (int i = 0; i < 8; i++)
    {
        v.tools.push_back({ kToolNames[i], "", ToolOwned(kTools[i]) });
    }
    for (auto& it : g_items) v.items.push_back({ it.name, it.id, false });
    float me[3]; PlayerPos(me);
    for (auto& p : g_places)
    {
        float dx = p.pos[0] - me[0], dy = p.pos[1] - me[1], dz = p.pos[2] - me[2];
        v.places.push_back({ p.name, p.group, sqrt(dx * dx + dy * dy + dz * dz) });
    }
    for (int i = 0; i < 3; i++) v.slot[i] = g_slotSet[i];
    std::lock_guard<std::mutex> lk(g_viewLock);
    g_view = v;
}

static int SafeFrame()
{
    __try { Frame(); return 0; }
    __except (EXCEPTION_EXECUTE_HANDLER) { return 1; }
}
static void OnFrame() { if (SafeFrame()) Log("game frame crashed - skipped"); }

// ---------------- extension entry (stands in for libterrain) ----------------
// install.bat looks for this export to tell our DLL from the real libterrain
extern "C" __declspec(dllexport) const char EvilDragonCheatsProxy[] = "EvilDragon's Cheats - Find The Needle";

using InitFn = Bool (*)(GetProc, void*, Init*);
static std::wstring ExeDir()
{
    wchar_t exe[MAX_PATH];
    GetModuleFileNameW(nullptr, exe, MAX_PATH);
    std::wstring d(exe);
    return d.substr(0, d.find_last_of(L"\\/") + 1);
}

extern "C" __declspec(dllexport) Bool terrain_3d_init(GetProc gp, void* library, Init* init)
{
    HMODULE h = LoadLibraryW((ExeDir() + L"libterrain.windows.release.x86_64.original.dll").c_str());
    InitFn orig = h ? (InitFn)GetProcAddress(h, "terrain_3d_init") : nullptr;
    if (!orig) Log("original libterrain not found - the terrain will be missing");
    Bool ok = orig ? orig(gp, library, init) : 0;
    if (Load(gp))
    {
        static MainLoopCallbacks cb{ nullptr, nullptr, OnFrame };
        api.register_main_loop_callbacks(library, &cb);
        Log("hooked into the engine");
    }
    else Log("engine interface not found - wrong game version?");
    return ok;
}

// the overlay starts only once the game renders frames
void Game_Init()
{
    while (g_frames < 120) Sleep(100);
}

// ---------------- actions ----------------
void Game_AddMoney(double amount)
{
    RunOnGame([=] {
        V a = FromFloat(fabs(amount)), why = S("cheat");
        if (amount >= 0) Call(R->game, "add_money", { &a, &why });
        else
        {
            double have = Num(Get(R->game, "money"));
            V take = FromFloat(std::min(have, fabs(amount)));
            Call(R->game, "spend_money", { &take, &why });
        }
        char b[96]; snprintf(b, sizeof b, "%s $%.2f  (now $%.2f)", amount >= 0 ? "Added" : "Removed", fabs(amount), Num(Get(R->game, "money")));
        Result(b);
    });
}

void Game_PayDebt()
{
    RunOnGame([] {
        double debt = Num(Get(R->game, "debt"));
        if (debt <= 0.001) { Result("You have no debt"); return; }
        V a = FromFloat(debt + 0.01), why = S("cheat");
        Call(R->game, "add_money", { &a, &why });
        Call(R->game, "pay_off_debt");
        Result("Debt paid off");
    });
}

void Game_BuyTech(int index)
{
    RunOnGame([=] {
        if (R->tech.nil() || g_techs.empty()) { Result("The tech tree is not loaded yet", true); return; }
        if (index < 0)
        {
            Call(R->tech, "grant_everything");
            Result("Every upgrade unlocked");
            return;
        }
        if (index >= (int)g_techs.size()) return;
        const TechEntry& t = g_techs[index];
        V id = S(t.id.c_str());
        int rank = (int)ToInt(Call(R->tech, "rank_of", { &id }));
        if (rank >= t.max) { Result(t.name + " is already maxed"); return; }
        V r = FromInt(rank + 1);
        Call(R->tech, "grant", { &id, &r });
        Result(t.name + " rank " + std::to_string(rank + 1));
    });
}

void Game_GiveTool(int index)
{
    RunOnGame([=] {
        int n = 0;
        for (int i = 0; i < 8; i++)
        {
            if (index >= 0 && i != index) continue;
            if (ToolOwned(kTools[i])) continue;
            V id = S(kTools[i]);
            if (!R->tech.nil() && ToInt(Call(R->tech, "rank_of", { &id })) <= 0) { V one = FromInt(1); Call(R->tech, "grant", { &id, &one }); }
            Call(R->game, "grant_tool", { &id });
            n++;
        }
        Result(n ? (index >= 0 ? std::string(kToolNames[index]) + " unlocked" : std::to_string(n) + " tools unlocked") : std::string("Already unlocked"));
    });
}

void Game_SpawnItem(int index, int count)
{
    RunOnGame([=] {
        if (R->props.nil() || index < 0 || index >= (int)g_items.size()) { Result("Can not spawn items here", true); return; }
        const ItemEntry& it = g_items[index];
        V id = S(it.id.c_str());
        // things you wear (jetpack, boots ...) are not dropped - they come with their upgrade
        bool worn = !R->itemDb.nil() && ToBool(Call(R->itemDb, "is_worn", { &id }));
        if (worn)
        {
            V unlock = R->itemDb.nil() ? V() : Call(R->itemDb, "unlock_of", { &id });
            std::string u = unlock.nil() ? "" : Txt(unlock);
            if (u.empty() || u == "<null>") u = it.id;
            V uid = S(u.c_str());
            int have = R->tech.nil() ? 0 : (int)ToInt(Call(R->tech, "rank_of", { &uid }));
            if (have <= 0 && !R->tech.nil()) { V one = FromInt(1); Call(R->tech, "grant", { &uid, &one }); }
            Call(R->game, "grant_tool", { &id });
            Result(it.name + (have > 0 ? " - you already have it" : " unlocked - it is on you now"));
            return;
        }
        int made = 0;
        for (int i = 0; i < count; i++)
        {
            V dist = FromFloat(1.6 + (i % 5) * 0.4), height = FromFloat(0.8 + (i / 5) * 0.4), state = NewOf(DICTIONARY);
            bool ok = false;
            V r = Call(R->props, "spawn_near", { &id, &R->player, &dist, &height, &state }, &ok);
            if (!ok || r.nil()) r = Call(R->props, "spawn_at_feet", { &id, &R->player }, &ok);
            if (ok && !r.nil()) made++;
        }
        if (made) Result(std::to_string(made) + "x " + it.name + " in front of you");
        else Result("The game would not spawn " + it.name, true);
    });
}

void Game_RefillStamina()
{
    RunOnGame([] {
        V st = Get(R->player, "stamina");
        if (!st.nil()) Set(st, "current", Call(st, "maximum"));
        if (!R->tech.nil()) Set(R->game, "jetpack_fuel", Call(R->tech, "jetpack_tank_seconds"));
        Result("Stamina and jetpack refilled");
    });
}

void Game_Teleport(int place)
{
    RunOnGame([=] {
        if (place < 0 || place >= (int)g_places.size()) return;
        TeleportNow(g_places[place].pos);
        Result("Teleported to " + g_places[place].name);
    });
}

void Game_SavePos(int slot)
{
    RunOnGame([=] { PlayerPos(g_slots[slot]); g_slotSet[slot] = true; Result("Position " + std::to_string(slot + 1) + " saved"); });
}

void Game_LoadPos(int slot)
{
    RunOnGame([=] { if (g_slotSet[slot]) { TeleportNow(g_slots[slot]); Result("Teleported to position " + std::to_string(slot + 1)); } });
}
