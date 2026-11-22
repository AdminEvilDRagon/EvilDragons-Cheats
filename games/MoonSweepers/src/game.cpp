// Moon Sweepers game logic. Everything that touches the game runs on Unity's main thread inside the
// MoonWalker.Update hook; the menu only reads g_view and queues jobs with RunOnGame.
#include "cheats.h"
#include "il2cpp.h"
#include "MinHook.h"
#include <unordered_map>

using namespace il2;

static std::mutex g_qLock;
static std::vector<std::function<void()>> g_queue;

void RunOnGame(std::function<void()> fn)
{
    std::lock_guard<std::mutex> lk(g_qLock);
    g_queue.push_back(std::move(fn));
}

static void Result(const std::string& s, bool error = false)
{
    Log("%s", s.c_str());
    Notify(s, error);
}

// ---------------- classes and methods ----------------
static Class *cWorld, *cWalker, *cRunOver, *cLobby, *cDev, *cTree, *cObject, *cComponent, *cTransform, *cCursor, *cCollector;
static Image *gImg, *gCore;

static std::unordered_map<std::string, Method*> g_methods;
static Method* M(Class* c, const char* name, int args)
{
    std::string key = std::to_string((uintptr_t)c) + name + std::to_string(args);
    auto it = g_methods.find(key);
    if (it != g_methods.end()) return it->second;
    Method* m = FindMethod(c, name, args);
    if (!m) Log("method not found: %s (%d)", name, args);
    return g_methods[key] = m;
}

static size_t Off(Class* c, const char* name)
{
    Field* f = class_get_field_from_name(c, name);
    if (!f) { Log("field not found: %s", name); return 0; }
    return field_get_offset(f);
}

static Object* Call(Class* c, const char* name, void* self, std::initializer_list<void*> args = {})
{
    Method* m = M(c, name, (int)args.size());
    if (!m) return nullptr;
    void* a[8] = {};
    int i = 0;
    for (void* p : args) a[i++] = p;
    Object* exc = nullptr;
    Object* r = runtime_invoke(m, self, a, &exc);
    if (exc) { Log("%s threw an exception", name); return nullptr; }
    return r;
}
template <class T> static T Ret(Object* o, T fallback = T{}) { return o ? *(T*)object_unbox(o) : fallback; }

static String* Local() { static String* s; if (!s) s = string_new("local"); return s; }
static String* Str(const std::string& s) { return string_new(s.c_str()); }

// ---------------- game objects ----------------
static void* Walker() { return Call(cRunOver, "get_LocalWalker", nullptr); }
static void* World(void* walker) { static size_t off = Off(cWalker, "world"); return walker && off ? At<void*>(walker, off) : nullptr; }
static void* TransformOf(void* comp) { return comp ? Call(cComponent, "get_transform", comp) : nullptr; }
static Vec3 Pos(void* comp) { void* t = TransformOf(comp); return t ? Ret<Vec3>(Call(cTransform, "get_position", t)) : Vec3{}; }
static Vec3 Up(void* comp) { void* t = TransformOf(comp); return t ? Ret<Vec3>(Call(cTransform, "get_up", t)) : Vec3{ 0, 1, 0 }; }
static Vec3 Forward(void* comp) { void* t = TransformOf(comp); return t ? Ret<Vec3>(Call(cTransform, "get_forward", t)) : Vec3{ 0, 0, 1 }; }

static Array* FindAll(Class* c)
{
    if (!c) return nullptr;
    Object* type = type_get_object(class_get_type(c));
    int inactive = 0, sort = 0;   // FindObjectsInactive.Exclude, FindObjectsSortMode.None
    return (Array*)Call(cObject, "FindObjectsByType", nullptr, { type, &inactive, &sort });
}

static double Wallet(void* world) { return ToDouble(Ret<Decimal>(Call(cWorld, "WalletDollars", world, { Local() }))); }
static void AddMoneyNow(void* world, double amount) { Decimal d = Dec(amount); Call(cWorld, "DevAddMoney", world, { Local(), &d }); }

static bool CrewOnline()
{
    if (!Ret<bool>(Call(cLobby, "get_InLobby", nullptr))) return false;
    Object* list = Call(cLobby, "get_Members", nullptr);
    if (!list) return false;
    Method* count = FindMethod(object_get_class(list), "get_Count", 0);
    Object* exc = nullptr;
    Object* r = count ? runtime_invoke(count, list, nullptr, &exc) : nullptr;
    return Ret<int>(r) > 1;
}

// ---------------- hooks ----------------
static bool g_allowed;
static bool g_freeBuy;   // set while the menu buys something for free

using UpdateFn = void(*)(void*, void*);
using FloatFn = float(*)(void*, String*, void*);
using IntFn = int(*)(void*, String*, void*);
using DoubleFn = double(*)(void*, String*, void*);
using DecFn = Decimal(*)(void*, String*, void*);
static UpdateFn oUpdate;
static FloatFn oJump;
static IntFn oBoosts;
static DoubleFn oCapacity;
static DecFn oPayout;

// MoonDevTools.Enabled is false in release builds and every Dev* helper (money, dust, robots, fly) checks it.
// It only reports true while the menu runs one of its actions or keeps fly / speed on.
using GateFn = bool(*)(void*);
static GateFn oDevEnabled;
static bool g_devWanted;
static bool HDevEnabled(void* m) { return g_devWanted || oDevEnabled(m); }

static float HJump(void* s, String* p, void* m) { float v = oJump(s, p, m); return g_allowed ? v * g_set.jumpMul : v; }
static int HBoosts(void* s, String* p, void* m) { int v = oBoosts(s, p, m); return g_allowed && g_set.infJetpack ? (v < 99 ? 99 : v) : v; }
static double HCapacity(void* s, String* p, void* m) { double v = oCapacity(s, p, m); return g_allowed ? v * g_set.capacityMul : v; }
static Decimal HPayout(void* s, String* p, void* m)
{
    Decimal v = oPayout(s, p, m);
    if (!g_allowed || g_set.payoutMul <= 1.001f) return v;
    double d = ToDouble(v) * g_set.payoutMul;
    Decimal r; r.lo = (uint64_t)llround(d * 1000); r.flags = 3 << 16;   // keep 3 decimals
    return r;
}

static bool Hook(Class* c, const char* name, int args, void* detour, void** orig)
{
    void* target = MethodPtr(FindMethod(c, name, args));
    if (!target) { Log("hook target missing: %s", name); return false; }
    if (MH_CreateHook(target, detour, orig) != MH_OK || MH_EnableHook(target) != MH_OK) { Log("hook failed: %s", name); return false; }
    return true;
}

// ---------------- upgrades ----------------
enum class UpKind { Tool, Container, Tree };
struct UpEntry { UpKind kind; std::string id, name, group; void* node = nullptr; };
static std::vector<UpEntry> g_ups;

static void BuildUpgradeList(void* world)
{
    g_ups.clear();
    if (void* cat = Call(cWorld, "get_UpgradeCatalog", world))
    {
        static size_t oTools = Off(object_get_class((Object*)cat), "tools");
        Array* tools = oTools ? At<Array*>(cat, oTools) : nullptr;
        for (size_t i = 0; tools && i < tools->length; i++)
        {
            void* offer = tools->items[i];
            if (!offer) continue;
            static size_t oTool = Off(object_get_class((Object*)offer), "tool");
            void* def = At<void*>(offer, oTool);
            if (!def) continue;
            static size_t oId = Off(object_get_class((Object*)def), "toolId");
            static size_t oName = Off(object_get_class((Object*)def), "displayName");
            std::string id = Utf8(At<String*>(def, oId)), name = Utf8(At<String*>(def, oName));
            if (id.empty()) continue;
            if (name.empty()) name = id;
            g_ups.push_back({ UpKind::Tool, id, name, "tool" });
            g_ups.push_back({ UpKind::Container, id, name + " container", "container" });
        }
    }
    for (const char* list : { "Nodes", "Gear" })
    {
        Field* f = class_get_field_from_name(cTree, list);
        Array* nodes = nullptr;
        if (f) field_static_get_value(f, &nodes);
        for (size_t i = 0; nodes && i < nodes->length; i++)
        {
            void* n = nodes->items[i];
            if (!n) continue;
            static size_t oId = Off(object_get_class((Object*)n), "id");
            static size_t oTitle = Off(object_get_class((Object*)n), "title");
            std::string id = Utf8(At<String*>(n, oId)), name = Utf8(At<String*>(n, oTitle));
            if (id.empty()) continue;
            g_ups.push_back({ UpKind::Tree, id, name.empty() ? id : name, list[0] == 'G' ? "gear" : "skill tree", n });
        }
    }
}

static UpgradeView ReadUpgrade(void* world, const UpEntry& e)
{
    UpgradeView u{ e.name, e.group };
    Decimal price;
    String* id = Str(e.id);
    switch (e.kind)
    {
    case UpKind::Tool:
        u.level = Ret<int>(Call(cWorld, "ToolLevel", world, { id, Local() }));
        u.canBuy = Ret<bool>(Call(cWorld, "TryGetToolPrice", world, { id, Local(), &price }));
        break;
    case UpKind::Container:
        u.level = Ret<int>(Call(cWorld, "ContainerTier", world, { Local(), id }));
        u.max = Ret<int>(Call(cWorld, "get_ContainerTierCount", world));
        u.canBuy = Ret<bool>(Call(cWorld, "TryGetContainerPrice", world, { Local(), id, &price }));
        break;
    case UpKind::Tree:
        u.level = Ret<int>(Call(cWorld, "TreeLevel", world, { Local(), id }));
        u.max = Ret<int>(Call(cWorld, "TreeMaxLevel", world, { e.node }));
        u.canBuy = u.level < u.max && Ret<bool>(Call(cWorld, "TryGetTreePrice", world, { Local(), id, &price }));
        break;
    }
    return u;
}

// one purchase through the game's own shop; free = the money is put back afterwards
static bool BuyOnce(void* world, const UpEntry& e, bool free, std::string* why)
{
    UpgradeView u = ReadUpgrade(world, e);
    if (!u.canBuy) { if (why) *why = "already maxed"; return false; }
    double before = Wallet(world);
    if (free) AddMoneyNow(world, 1e9);
    const char* fn = e.kind == UpKind::Tool ? "TryPurchaseTool" : e.kind == UpKind::Container ? "TryPurchaseContainer" : "TryPurchaseTreeNode";
    bool ok = false;
    String* msg = nullptr;
    for (int expected : { u.level, u.level + 1 })
    {
        msg = nullptr;
        String* id = Str(e.id);
        if ((ok = Ret<bool>(Call(cWorld, fn, world, { Local(), id, &expected, &msg })))) break;
    }
    if (free) AddMoneyNow(world, before - Wallet(world));
    if (!ok && why) *why = msg ? Utf8(msg) : "the shop said no";
    return ok;
}

// ---------------- places ----------------
struct Place { std::string name, group; void* obj; Vec3 pos, up; };
static std::vector<Place> g_places;
static Vec3 g_slots[3];
static bool g_slotSet[3];

static void BuildPlaces(void* world)
{
    g_places.clear();
    struct Src { const char* cls; const char* name; const char* group; };
    static const Src kSrc[] = {
        { "MoonCollector", "Station", "stations" },
        { "MoonGarage", "Garage", "base" },
        { "MoonUpgradeTreeTerminal", "Upgrade Terminal", "base" },
        { "MoonSupplyWall", "Supply Wall", "base" },
        { "MoonLockerTerminal", "Locker", "base" },
        { "MoonMapConsole", "Map Console", "base" },
        { "MoonToolSwitchBench", "Tool Bench", "base" },
        { "MoonKitchen", "Kitchen", "base" },
        { "MoonBunks", "Bunks", "base" },
        { "MoonGameLounge", "Game Lounge", "base" },
        { "MoonRobotDock", "Robot Dock", "robots" },
        { "MoonLandmark", "Landmark", "landmarks" },
    };
    for (const Src& s : kSrc)
    {
        Class* c = class_from_name(gImg, "MoonCleaners", s.cls);
        Array* objs = FindAll(c);
        for (size_t i = 0; objs && i < objs->length && i < 40; i++)
        {
            void* o = objs->items[i];
            if (!o) continue;
            std::string n = s.name;
            if (objs->length > 1) n += " " + std::to_string(i + 1);
            if (c == cCollector && !Ret<bool>(Call(cWorld, "IsStationFound", world, { o }))) n += "  (not found yet)";
            g_places.push_back({ n, s.group, o, Pos(o), Up(o) });
        }
    }
}

static void TeleportNow(void* walker, Vec3 to)
{
    bool refresh = true;
    Call(cWalker, "Teleport", walker, { &to, &refresh });
}

// ---------------- per frame ----------------
static bool g_flyOn, g_cursorFreed;
static float g_lastSpeed = -1, g_lastFly = -1;
static ULONGLONG g_nextView, g_nextPlaces, g_nextFill;

static void CursorFree(bool on)
{
    int mode = on ? 0 : 1;   // CursorLockMode.None / Locked
    bool vis = false;
    Call(cCursor, "set_lockState", nullptr, { &mode });
    Call(cCursor, "set_visible", nullptr, { &vis });
}

static void Frame(void* walker)
{
    void* world = World(walker);
    bool crew = CrewOnline();
    bool allowed = world && !crew;
    g_allowed = allowed;

    std::vector<std::function<void()>> jobs;
    { std::lock_guard<std::mutex> lk(g_qLock); jobs.swap(g_queue); }
    g_devWanted = allowed;
    if (allowed) for (auto& j : jobs) j();

    // movement
    float speed = allowed ? g_set.speedMul : 1.0f;
    if (speed != g_lastSpeed) { Call(cWalker, "set_DevSpeedMultiplier", walker, { &speed }); g_lastSpeed = speed; }
    bool fly = allowed && g_set.fly;
    if (fly != g_flyOn)
    {
        bool noclip = fly;
        Call(cWalker, "SetFlying", walker, { &fly, &noclip });
        if (Field* f = class_get_field_from_name(cDev, "Noclip")) field_static_set_value(f, &noclip);
        g_flyOn = fly;
        g_lastFly = -1;
    }
    if (fly && g_set.flySpeed != g_lastFly)
    {
        static float base = Ret<float>(Call(cWalker, "get_FlySpeed", walker), 10.0f);
        float v = base * g_set.flySpeed;
        Call(cWalker, "set_FlySpeed", walker, { &v });
        g_lastFly = g_set.flySpeed;
    }

    ULONGLONG now = GetTickCount64();
    if (allowed && g_set.autoFill && now >= g_nextFill)
    {
        g_nextFill = now + 500;
        double cap = Ret<double>(Call(cWorld, "InventoryCapacityKg", world, { Local() }));
        Call(cWorld, "DevSetCarriedDust", world, { Local(), &cap });
    }
    // keep the gate open only for what is switched on
    g_devWanted = allowed && (g_set.fly || g_set.speedMul != 1.0f);

    // cursor for the menu
    if (g_menuOpen) { CursorFree(true); g_cursorFreed = true; }
    else if (g_cursorFreed) { CursorFree(false); g_cursorFreed = false; }

    // view for the menu
    if (now < g_nextView) return;
    g_nextView = now + 300;
    GameView v;
    v.inGame = world != nullptr;
    v.allowed = allowed;
    v.crew = crew;
    if (world)
    {
        v.money = Wallet(world);
        v.dust = Ret<double>(Call(cWorld, "InventoryKilograms", world, { Local() }));
        v.capacity = Ret<double>(Call(cWorld, "InventoryCapacityKg", world, { Local() }));
        v.cleaned = Ret<double>(Call(cWorld, "PlayerCleanedKilograms", world, { Local() }));
        if (g_ups.empty()) BuildUpgradeList(world);
        for (const UpEntry& e : g_ups) v.upgrades.push_back(ReadUpgrade(world, e));
        if (now >= g_nextPlaces) { BuildPlaces(world); g_nextPlaces = now + 3000; }
        Vec3 me = Pos(walker);
        for (const Place& p : g_places) v.places.push_back({ p.name, p.group, Dist(me, p.pos) });
        static std::vector<KindView> robots;
        if (robots.empty())
            for (int k = 0; k < 160; k++)
            {
                if (!Ret<bool>(Call(cWorld, "IsRobotDock", nullptr, { &k }))) continue;
                std::string n = "Robot dock " + std::to_string(k);
                if (void* def = Call(cWorld, "BuildDefinition", world, { &k }))
                {
                    static size_t oName = Off(object_get_class((Object*)def), "displayName");
                    std::string dn = oName ? Utf8(At<String*>(def, oName)) : "";
                    if (!dn.empty()) n = dn;
                }
                robots.push_back({ n, k });
            }
        v.robots = robots;
    }
    for (int i = 0; i < 3; i++) v.slot[i] = g_slotSet[i];
    std::lock_guard<std::mutex> lk(g_viewLock);
    g_view = v;
}

static int SafeFrame(void* walker)
{
    __try { Frame(walker); return 0; }
    __except (EXCEPTION_EXECUTE_HANDLER) { return 1; }
}

static void HUpdate(void* self, void* mi)
{
    oUpdate(self, mi);
    void* me = Walker();
    if (!me || me != self) return;
    if (SafeFrame(me)) Log("game frame crashed - skipped");
}

// ---------------- init ----------------
void Game_Init()
{
    while (!GetModuleHandleW(L"GameAssembly.dll") || !il2::Load()) Sleep(250);
    while (!(gImg = FindImage("Assembly-CSharp.dll")) || !(gCore = FindImage("UnityEngine.CoreModule.dll"))) Sleep(250);
    thread_attach(domain_get());
    cWorld = class_from_name(gImg, "MoonCleaners", "MoonWorld");
    cWalker = class_from_name(gImg, "MoonCleaners", "MoonWalker");
    cRunOver = class_from_name(gImg, "MoonCleaners", "MoonRunOver");
    cLobby = class_from_name(gImg, "MoonCleaners", "MoonSteamLobby");
    cDev = class_from_name(gImg, "MoonCleaners", "MoonDevTools");
    cTree = class_from_name(gImg, "MoonCleaners", "MoonUpgradeTree");
    cCollector = class_from_name(gImg, "MoonCleaners", "MoonCollector");
    cObject = class_from_name(gCore, "UnityEngine", "Object");
    cComponent = class_from_name(gCore, "UnityEngine", "Component");
    cTransform = class_from_name(gCore, "UnityEngine", "Transform");
    cCursor = class_from_name(gCore, "UnityEngine", "Cursor");
    if (!cWorld || !cWalker || !cRunOver || !cObject || !cTransform) { Log("game classes not found - wrong game version?"); return; }
    Hook(cWalker, "Update", 0, (void*)HUpdate, (void**)&oUpdate);
    Hook(cDev, "get_Enabled", 0, (void*)HDevEnabled, (void**)&oDevEnabled);
    Hook(cWorld, "JumpMultiplier", 1, (void*)HJump, (void**)&oJump);
    Hook(cWorld, "JetpackBoosts", 1, (void*)HBoosts, (void**)&oBoosts);
    Hook(cWorld, "InventoryCapacityKg", 1, (void*)HCapacity, (void**)&oCapacity);
    Hook(cWorld, "PayoutMultiplier", 1, (void*)HPayout, (void**)&oPayout);
    Log("game hooks ready");
}

// ---------------- actions (queued, run on the game thread) ----------------
void Game_AddMoney(double amount)
{
    RunOnGame([=] {
        void* w = World(Walker());
        if (!w) return;
        double now = Wallet(w);
        double a = amount < 0 && -amount > now ? -now : amount;
        AddMoneyNow(w, a);
        double after = Wallet(w);
        if (fabs(after - now) < 0.001 && fabs(a) > 0.001) { Result("The game did not take the money", true); return; }
        Result((a >= 0 ? "Added " : "Removed ") + Money((long long)fabs(a)));
    });
}

void Game_BuyUpgrade(int index)
{
    RunOnGame([=] {
        void* w = World(Walker());
        if (!w || g_ups.empty()) return;
        if (index >= 0)
        {
            if (index >= (int)g_ups.size()) return;
            std::string why;
            bool ok = BuyOnce(w, g_ups[index], g_set.freeUpgrades, &why);
            Result(ok ? "Bought " + g_ups[index].name : g_ups[index].name + ": " + why, !ok);
            return;
        }
        int bought = 0;
        for (int pass = 0; pass < 12; pass++)
        {
            int before = bought;
            for (const UpEntry& e : g_ups)
                for (int i = 0; i < 10 && BuyOnce(w, e, true, nullptr); i++) bought++;
            if (bought == before) break;
        }
        Result(bought ? "Bought " + std::to_string(bought) + " upgrade levels for free" : "Everything is already maxed");
    });
}

void Game_FillDust()
{
    RunOnGame([] {
        void* w = World(Walker());
        if (!w) return;
        double cap = Ret<double>(Call(cWorld, "InventoryCapacityKg", w, { Local() }));
        Call(cWorld, "DevSetCarriedDust", w, { Local(), &cap });
        char b[64]; snprintf(b, sizeof b, "Dust bag filled (%.1f kg)", cap);
        Result(b);
    });
}

void Game_EmptyDust()
{
    RunOnGame([] {
        void* w = World(Walker());
        if (!w) return;
        double zero = 0;
        Call(cWorld, "DevSetCarriedDust", w, { Local(), &zero });
        Result("Dust bag emptied");
    });
}

void Game_CleanAround(int rings)
{
    RunOnGame([=] {
        void* me = Walker();
        void* w = World(me);
        if (!w) return;
        Vec3 p = Pos(me);
        int r = rings;
        int n = Ret<int>(Call(cWorld, "DevCompleteRegions", w, { &p, &r }));
        Result("Cleaned " + std::to_string(n) + " sections around you");
    });
}

static Vec3 InFront(void* me, float metres)
{
    Vec3 f = Forward(me), u = Up(me);
    return Pos(me) + f * metres + u * 0.5f;
}

void Game_SpawnRobot(int kind)
{
    RunOnGame([=] {
        void* me = Walker();
        void* w = World(me);
        if (!w) return;
        Vec3 at = InFront(me, 6);
        int k = kind;
        float yaw = 0;
        int id = Ret<int>(Call(cWorld, "DevPlaceRobotDock", w, { Local(), &k, &at, &yaw }), -1);
        Result(id > 0 ? "Robot dock placed in front of you" : "No room here - try a flat spot", id <= 0);
    });
}

// Dust capsules: the game packs at most 1 kg into one capsule, taken from the dust you carry.
// Tries the moon's local space first and plain world space second, and logs what it saw.
void Game_SpawnCapsules(int count)
{
    RunOnGame([=] {
        void* me = Walker();
        void* w = World(me);
        if (!w) return;
        void* wt = TransformOf(w);
        double before = Ret<double>(Call(cWorld, "InventoryKilograms", w, { Local() }));
        double cap = Ret<double>(Call(cWorld, "InventoryCapacityKg", w, { Local() }));
        Vec3 f = Forward(me), up = Up(me);
        Vec3 right{ f.y * up.z - f.z * up.y, f.z * up.x - f.x * up.z, f.x * up.y - f.y * up.x };
        int made = 0;
        static int mode = -1;   // which coordinate space worked last time
        for (int i = 0; i < count; i++)
        {
            double fill = cap > 1.0 ? cap : 1.0;
            Call(cWorld, "DevSetCarriedDust", w, { Local(), &fill });
            double carried = Ret<double>(Call(cWorld, "InventoryKilograms", w, { Local() }));
            float side = (float)(i % 5) - 2.0f, row = (float)(i / 5);
            Vec3 at = Pos(me) + f * (3.0f + row * 1.6f) + right * (side * 1.2f) + up * 0.6f;
            Vec3 lp = wt ? Ret<Vec3>(Call(cTransform, "InverseTransformPoint", wt, { &at }), at) : at;
            Vec3 lu = wt ? Ret<Vec3>(Call(cTransform, "InverseTransformDirection", wt, { &up }), up) : up;
            bool ok = false;
            for (int m = 0; m < 4 && !ok; m++)
            {
                int tryMode = mode >= 0 ? (m == 0 ? mode : m - 1) : m;
                if (mode >= 0 && m > 0 && tryMode == mode) continue;
                Vec3 p = (tryMode & 1) ? at : lp, u = (tryMode & 1) ? up : lu;
                double most = 1.0;
                int id = 0;
                ok = (tryMode & 2) ? Ret<bool>(Call(cWorld, "PackDustPortion", w, { Local(), &p, &u, &most, &id }))
                                   : Ret<bool>(Call(cWorld, "PackDustCanister", w, { Local(), &p, &u, &id }));
                if (ok) mode = tryMode;
                else if (i == 0) Log("capsule try %d refused (carried %.2f kg, point %.1f %.1f %.1f)", tryMode, carried, p.x, p.y, p.z);
            }
            if (ok) made++;
            else break;
        }
        Call(cWorld, "DevSetCarriedDust", w, { Local(), &before });
        if (made) { char b[96]; snprintf(b, sizeof b, "%d dust capsule%s in front of you", made, made == 1 ? "" : "s"); Result(b); }
        else Result("The game would not pack a capsule here", true);
    });
}

void Game_SpawnCatcher()
{
    RunOnGame([] {
        void* me = Walker();
        void* w = World(me);
        if (!w) return;
        Vec3 at = InFront(me, 6);
        float yaw = 0;
        int id = Ret<int>(Call(cWorld, "DevPlaceCatcher", w, { Local(), &at, &yaw }), -1);
        Result(id > 0 ? "Capsule catcher placed in front of you" : "No room here - try a flat spot", id <= 0);
    });
}

void Game_Teleport(int place)
{
    RunOnGame([=] {
        void* me = Walker();
        if (!me || place < 0 || place >= (int)g_places.size()) return;
        const Place& p = g_places[place];
        TeleportNow(me, p.pos + p.up * 2.5f);
        Result("Teleported to " + p.name);
    });
}

void Game_ToDepot()
{
    RunOnGame([] {
        if (void* me = Walker()) { Call(cWalker, "ReturnToDepot", me); Result("Back at the depot"); }
    });
}

void Game_SavePos(int slot)
{
    RunOnGame([=] {
        if (void* me = Walker()) { g_slots[slot] = Pos(me) + Up(me) * 0.5f; g_slotSet[slot] = true; Result("Position " + std::to_string(slot + 1) + " saved"); }
    });
}

void Game_LoadPos(int slot)
{
    RunOnGame([=] {
        void* me = Walker();
        if (me && g_slotSet[slot]) { TeleportNow(me, g_slots[slot]); Result("Teleported to position " + std::to_string(slot + 1)); }
    });
}
