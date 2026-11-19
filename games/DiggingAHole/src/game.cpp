// Game side: everything that touches Unreal objects runs here, on the game thread.
#include "cheats.h"
#include <cmath>
#include "MinHook.h"

#include "SDK/Basic.hpp"
#include "SDK/CoreUObject_classes.hpp"
#include "SDK/Engine_classes.hpp"
#include "SDK/CubicDestruction_FirstPersonCharacter_classes.hpp"
#include "SDK/UMG_parameters.hpp"
#include "SDK/BP_MoneyBagPeep_classes.hpp"
#include "SDK/BP_TKey1_classes.hpp"
#include "SDK/BP_Mine_Base_classes.hpp"
#include "SDK/BP_Mine2_classes.hpp"
#include "SDK/BP_Mine3_classes.hpp"
#include "SDK/BP_Mine4_classes.hpp"
#include "SDK/BP_Explosion_classes.hpp"
#include "SDK/BP_Dynamite_classes.hpp"
#include "SDK/BP_OreSpawner_classes.hpp"

using namespace SDK;
using Player = ACubicDestruction_FirstPersonCharacter_C;

const char* const kOreNames[kOreCount] = { "Stone", "Coal", "Iron", "Copper", "Silver", "Gold", "Platinum", "Ruby", "Sapphire", "Emerald" };

static std::mutex g_qLock;
static std::vector<std::function<void()>> g_queue;
static DWORD g_gameThread;
static bool g_inTick;
static ULONGLONG g_lastTick;

// ---------------- crash guard ----------------
static int g_excCount;
static int ExcFilter(EXCEPTION_POINTERS* e, const char* where)
{
    // Only catch real crashes. Unreal raises its own exceptions (ensures, 0x4000) and handles them itself.
    if (e->ExceptionRecord->ExceptionCode != EXCEPTION_ACCESS_VIOLATION) return EXCEPTION_CONTINUE_SEARCH;
    if (g_excCount++ < 30)
    {
        void* addr = e->ExceptionRecord->ExceptionAddress;
        HMODULE mod = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCWSTR)addr, &mod);
        char name[MAX_PATH] = "?";
        if (mod) GetModuleFileNameA(mod, name, MAX_PATH);
        const char* base = strrchr(name, '\\');
        Log("exception in %s at %s+0x%llX", where, base ? base + 1 : name, (unsigned long long)((char*)addr - (char*)mod));
    }
    return EXCEPTION_EXECUTE_HANDLER;
}
#define GUARD(where, code) __try { code; } __except (ExcFilter(GetExceptionInformation(), where)) {}

void RunOnGame(std::function<void()> fn)
{
    std::lock_guard<std::mutex> lk(g_qLock);
    g_queue.push_back(std::move(fn));
}

// ---------------- object helpers ----------------
static UWorld* World() { return UWorld::GetWorld(); }

static APlayerController* PC()
{
    UWorld* w = World();
    if (!w || !w->OwningGameInstance || w->OwningGameInstance->LocalPlayers.Num() == 0) return nullptr;
    ULocalPlayer* lp = w->OwningGameInstance->LocalPlayers[0];
    return lp ? lp->PlayerController : nullptr;
}

static Player* Me()
{
    APlayerController* pc = PC();
    if (!pc || !pc->Pawn || !pc->Pawn->IsA(Player::StaticClass())) return nullptr;
    return static_cast<Player*>(pc->Pawn);
}

static bool Allowed()
{
    UWorld* w = World();
    return w && w->GameState && w->GameState->PlayerArray.Num() <= 1;
}

template<typename P> static void CallStatic(const char* fn, P& parms)
{
    static UFunction* f = nullptr;
    if (!f) f = UObject::FindObject<UFunction>(fn);
    if (!f) return;
    UClass* cls = static_cast<UClass*>(f->Outer);
    if (cls && cls->ClassDefaultObject) cls->ClassDefaultObject->ProcessEvent(f, &parms);
}

// A value we multiply. If the game changes it (e.g. after an upgrade), that becomes the new base.
template<typename T> struct Scaled
{
    T base{}, written{};
    bool has = false;
    void Apply(T& field, float mul)
    {
        if (!has || field != written) { base = field; has = true; }
        written = (T)(base * mul);
        field = written;
    }
    void Reset() { has = false; }
};

// A value we pin while a cheat is on and give back when it is turned off.
template<typename T> struct Pinned
{
    T original{};
    bool active = false;
    void Apply(T& field, bool on, T value)
    {
        if (on) { if (!active) { original = field; active = true; } field = value; }
        else if (active) { field = original; active = false; }
    }
};

static void* g_who;
static Scaled<float> g_walk, g_jump, g_grav;
static Scaled<double> g_drill, g_drillNerf, g_radius, g_jetpack;
static Scaled<int32> g_digRadius;
static Pinned<int32> g_jumpMax, g_invSize;
static Pinned<double> g_depthLimit, g_maxDepth;
static bool g_flying;
static float g_flySpeedOrig;
static bool g_menuInputLocked;
static FVector g_slots[3], g_surface;
static bool g_slotSet[3], g_surfaceSet;

static void ResetFor(Player* me)
{
    g_who = me;
    g_walk.Reset(); g_jump.Reset(); g_grav.Reset();
    g_drill.Reset(); g_drillNerf.Reset(); g_radius.Reset(); g_jetpack.Reset(); g_digRadius.Reset();
    g_jumpMax = {}; g_invSize = {}; g_depthLimit = {}; g_maxDepth = {};
    g_flying = false;
    g_surface = me->K2_GetActorLocation();
    g_surfaceSet = true;
    for (bool& s : g_slotSet) s = false;
    Log("player found");
}

static void ApplyCheats(Player* me, bool allowed, float dt)
{
    if (g_who != me) ResetFor(me);
    UCharacterMovementComponent* cm = me->CharacterMovement;

    // movement
    if (cm)
    {
        g_walk.Apply(cm->MaxWalkSpeed, allowed ? g_set.speedMul : 1.0f);
        g_jump.Apply(cm->JumpZVelocity, allowed ? g_set.jumpMul : 1.0f);
        g_grav.Apply(cm->GravityScale, allowed ? g_set.gravity : 1.0f);
    }
    g_jumpMax.Apply(me->JumpMaxCount, allowed && g_set.unlimitedJumps, 999);

    // survival
    if (allowed && g_set.god && me->MaxHealth > 0) me->CurHealth = me->MaxHealth;
    if (allowed && g_set.infBattery && me->MaxBatteryCapacity > 0) me->CurBatteryLevel = me->MaxBatteryCapacity;

    // tools
    float drill = allowed ? g_set.drillMul : 1.0f, radius = allowed ? g_set.radiusMul : 1.0f;
    g_drill.Apply(me->DrillStrength, drill);
    g_drillNerf.Apply(me->DrillStrengthAfterNerf, drill);
    g_radius.Apply(me->DrillRadius, radius);
    g_digRadius.Apply(me->DigRadius, radius);
    g_jetpack.Apply(me->JetpackPower, allowed ? g_set.jetpackMul : 1.0f);
    g_depthLimit.Apply(me->CurDrillLimit, allowed && g_set.noDepthLimit, 1.0e7);
    g_maxDepth.Apply(me->MaxDiggingDepth, allowed && g_set.noDepthLimit, 1.0e7);
    g_invSize.Apply(me->InventorySize, allowed && g_set.bigInventory, 99999);

    // fly / noclip
    if (!cm) return;
    bool fly = allowed && g_set.fly;
    if (fly != g_flying)
    {
        g_flying = fly;
        me->SetActorEnableCollision(!fly);
        cm->SetMovementMode(fly ? EMovementMode::MOVE_Flying : EMovementMode::MOVE_Falling, 0);
        if (fly) { g_flySpeedOrig = cm->MaxFlySpeed; cm->MaxFlySpeed = 1.0f; }
        else cm->MaxFlySpeed = g_flySpeedOrig;
        Log(fly ? "fly on" : "fly off");
    }
    if (!fly) return;
    if (cm->MovementMode != EMovementMode::MOVE_Flying) cm->SetMovementMode(EMovementMode::MOVE_Flying, 0);
    cm->Velocity = FVector();
    if (g_menuOpen) return;
    APlayerController* pc = PC();
    if (!pc) return;
    FRotator r = pc->GetControlRotation();
    double yaw = r.Yaw * 3.14159265358979 / 180.0, pitch = r.Pitch * 3.14159265358979 / 180.0;
    FVector fwd{ std::cos(pitch) * std::cos(yaw), std::cos(pitch) * std::sin(yaw), std::sin(pitch) };
    FVector right{ -std::sin(yaw), std::cos(yaw), 0 };
    FVector dir{};
    auto down = [](int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; };
    if (down('W')) dir = dir + fwd;
    if (down('S')) dir = dir - fwd;
    if (down('D')) dir = dir + right;
    if (down('A')) dir = dir - right;
    if (down(VK_SPACE)) dir.Z += 1;
    if (down(VK_LCONTROL) || down('C')) dir.Z -= 1;
    double len = std::sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
    if (len < 0.01) return;
    double units = 900.0 * g_set.flySpeed * (down(VK_LSHIFT) ? 2.5 : 1.0) * dt / len;
    FVector p = me->K2_GetActorLocation();
    p = p + FVector{ dir.X * units, dir.Y * units, dir.Z * units };
    me->K2_SetActorLocation(p, false, nullptr, true);
}

static void SetMenuInput(bool open)
{
    APlayerController* pc = PC();
    if (!pc) { g_menuInputLocked = false; return; }
    if (open == g_menuInputLocked) return;
    g_menuInputLocked = open;
    pc->bShowMouseCursor = open;
    pc->SetIgnoreLookInput(open);
    pc->SetIgnoreMoveInput(open);
    if (open)
    {
        Params::WidgetBlueprintLibrary_SetInputMode_GameAndUIEx p{};
        p.PlayerController = pc; p.InMouseLockMode = (EMouseLockMode)0; p.bHideCursorDuringCapture = false;
        CallStatic("Function UMG.WidgetBlueprintLibrary.SetInputMode_GameAndUIEx", p);
    }
    else
    {
        Params::WidgetBlueprintLibrary_SetInputMode_GameOnly p{};
        p.PlayerController = pc;
        CallStatic("Function UMG.WidgetBlueprintLibrary.SetInputMode_GameOnly", p);
    }
}

static int32* OreField(Player* me, int i)
{
    int32* f[kOreCount] = { &me->Inv_Stone_Amount, &me->Inv_Coal_Amount, &me->Inv_Iron_Amount, &me->Inv_Copper_Amount, &me->Inv_Silver_Amount,
                            &me->Inv_Gold_Amount, &me->Inv_Platinum_Amount, &me->Inv_Ruby_Amount, &me->Inv_Sapphire_Amount, &me->Inv_Emerald_Amount };
    return (i >= 0 && i < kOreCount) ? f[i] : nullptr;
}

static void RunJobInner(std::function<void()>* j) { (*j)(); }
static void RunJob(std::function<void()>& j) { GUARD("action", RunJobInner(&j)); }
static void SafeCheats(Player* me, bool allowed, float dt) { GUARD("cheats", ApplyCheats(me, allowed, dt)); }

static void CollectOres(float radiusMetres, bool toBag, bool quiet);
static void Tick(float dt)
{
    APlayerController* pc = PC();
    Player* me = Me();
    bool allowed = Allowed();

    if (pc && g_menuInputLocked != g_menuOpen) SetMenuInput(g_menuOpen);
    if (!pc) g_menuInputLocked = false;

    {
        std::vector<std::function<void()>> jobs;
        { std::lock_guard<std::mutex> lk(g_qLock); jobs.swap(g_queue); }
        if (allowed && me) for (auto& j : jobs) RunJob(j);
    }
    if (me) SafeCheats(me, allowed, dt);
    if (me && allowed && g_set.autoCollect)
    {
        static ULONGLONG last;
        if (GetTickCount64() - last > 400) { last = GetTickCount64(); CollectOres(g_set.oreRadius, g_set.oreToBag, true); }
    }

    GameView v{};
    v.inGame = me != nullptr;
    v.allowed = allowed;
    if (me)
    {
        v.money = me->Money;
        for (int i = 0; i < kOreCount; i++) v.ores[i] = *OreField(me, i);
        v.inventorySize = me->InventorySize;
        v.depth = me->PlayerDepth;
        v.health = me->CurHealth; v.maxHealth = me->MaxHealth;
        v.battery = me->CurBatteryLevel; v.maxBattery = me->MaxBatteryCapacity;
        v.drill = me->DrillStrength; v.drillRadius = me->DrillRadius; v.jetpack = me->JetpackPower;
        v.dynamite = me->DynamiteAmount; v.lamps = me->LampAmount; v.keys = me->KeyAmount; v.teleporters = me->TeleporterAmount;
        v.drillUnlocked = me->DrillUnlocked_; v.jetpackUnlocked = me->JetpackUnlocked_;
        for (int i = 0; i < 3; i++) v.slot[i] = g_slotSet[i];
    }
    std::lock_guard<std::mutex> lk(g_viewLock);
    g_view = v;
}

// ---------------- ProcessEvent hook = our game-thread tick ----------------
using ProcessEventFn = void(*)(const UObject*, UFunction*, void*);
static ProcessEventFn oProcessEvent;
static int g_depth;

static void SafeTick(float dt) { GUARD("tick", Tick(dt)); }

static bool IsSafePointInner(UFunction* fn, bool* out) { *out = fn->GetName() == "ReceiveTick"; return true; }
static bool IsSafePoint(UFunction* fn)
{
    static std::vector<std::pair<UFunction*, bool>> known;
    for (auto& k : known) if (k.first == fn) return k.second;
    bool r = false;
    IsSafePointInner(fn, &r);
    if (known.size() < 4096) known.push_back({ fn, r });
    return r;
}

static void HkProcessEvent(const UObject* obj, UFunction* fn, void* parms)
{
    bool game = GetCurrentThreadId() == g_gameThread;
    if (game) g_depth++;
    oProcessEvent(obj, fn, parms);
    if (!game) return;
    g_depth--;
    if (g_depth != 0 || g_inTick) return;
    // Run right after an actor's ReceiveTick: a safe point outside movement/physics updates.
    if (!IsSafePoint(fn) && GetTickCount64() - g_lastTick < 500) return;
    ULONGLONG now = GetTickCount64();
    if (now - g_lastTick < 15) return;
    float dt = g_lastTick ? (float)(now - g_lastTick) / 1000.0f : 0.016f;
    if (dt > 0.1f) dt = 0.1f;
    g_lastTick = now;
    g_inTick = true;
    SafeTick(dt);
    g_inTick = false;
}

static BOOL CALLBACK FindWnd(HWND h, LPARAM out)
{
    DWORD pid = 0;
    DWORD tid = GetWindowThreadProcessId(h, &pid);
    wchar_t cls[64] = {};
    GetClassNameW(h, cls, 64);
    if (pid == GetCurrentProcessId() && wcscmp(cls, L"UnrealWindow") == 0) { *(DWORD*)out = tid; return FALSE; }
    return TRUE;
}

void Game_Init()
{
    while (!g_gameThread) { EnumWindows(FindWnd, (LPARAM)&g_gameThread); if (!g_gameThread) Sleep(250); }
    void* pe = (void*)(InSDKUtils::GetImageBase() + Offsets::ProcessEvent);
    MH_CreateHook(pe, (void*)HkProcessEvent, (void**)&oProcessEvent);
    MH_EnableHook(pe);
    Log("game hooks installed");
}

void Game_SetMenuOpen(bool) {}

// ---------------- actions (queued from the menu) ----------------
#define WITH_ME(body) RunOnGame([=] { if (Player* me = Me()) { body; } })

void Game_AddMoney(int a)
{
    WITH_ME({
        long long t = (long long)me->Money + a;
        if (t < 0) t = 0;
        if (t > 2000000000) t = 2000000000;
        me->Money = (int32)t;
        Log("money -> %d", me->Money);
    });
}
void Game_AddOre(int ore, int a)
{
    WITH_ME({
        for (int i = 0; i < kOreCount; i++)
            if (ore < 0 || ore == i) { int32* f = OreField(me, i); long long t = (long long)*f + a; *f = (int32)(t < 0 ? 0 : (t > 1000000 ? 1000000 : t)); }
    });
}
void Game_ClearOres() { WITH_ME({ for (int i = 0; i < kOreCount; i++) *OreField(me, i) = 0; }); }
void Game_AddTool(int which, int a)
{
    WITH_ME({
        int32* f = which == 0 ? &me->DynamiteAmount : which == 1 ? &me->LampAmount : which == 2 ? &me->KeyAmount : &me->TeleporterAmount;
        *f = (*f + a) < 0 ? 0 : *f + a;
    });
}
void Game_Unlock(bool drill, bool jetpack) { WITH_ME({ if (drill) me->DrillUnlocked_ = true; if (jetpack) me->JetpackUnlocked_ = true; }); }
void Game_Heal() { WITH_ME({ me->CurHealth = me->MaxHealth; me->CurBatteryLevel = me->MaxBatteryCapacity; }); }
void Game_SavePos(int s) { WITH_ME({ if (s >= 0 && s < 3) { g_slots[s] = me->K2_GetActorLocation(); g_slotSet[s] = true; } }); }
void Game_LoadPos(int s) { WITH_ME({ if (s >= 0 && s < 3 && g_slotSet[s]) me->K2_SetActorLocation(g_slots[s], false, nullptr, true); }); }
void Game_ToSurface() { WITH_ME({ if (g_surfaceSet) me->K2_SetActorLocation(g_surface + FVector{ 0, 0, 100 }, false, nullptr, true); }); }

// ---------------- spawning ----------------
const SpawnDef kSpawns[] = {
    { "Money bag",        "BlueprintGeneratedClass BP_MoneyBagPeep.BP_MoneyBagPeep_C" },
    { "Treasure key",     "BlueprintGeneratedClass BP_TKey1.BP_TKey1_C" },
    { "Treasure chest",   "BlueprintGeneratedClass BP_Keytreasure.BP_Keytreasure_C" },
    { "Mine (small)",     "BlueprintGeneratedClass BP_Mine2.BP_Mine2_C" },
    { "Mine (medium)",    "BlueprintGeneratedClass BP_Mine3.BP_Mine3_C" },
    { "Mine (large)",     "BlueprintGeneratedClass BP_Mine4.BP_Mine4_C" },
    { "Dynamite",         "BlueprintGeneratedClass BP_Dynamite.BP_Dynamite_C" },
    { "Big dynamite",     "BlueprintGeneratedClass BP_DynamiteBIG.BP_DynamiteBIG_C" },
    { "Explosion",        "BlueprintGeneratedClass BP_Explosion.BP_Explosion_C" },
    { "Lamp",             "BlueprintGeneratedClass BP_Lamp.BP_Lamp_C" },
    { "Mole",             "BlueprintGeneratedClass BP_Mole.BP_Mole_C" },
};
const int kSpawnCount = sizeof(kSpawns) / sizeof(kSpawns[0]);

// Where the player is looking (or a few metres in front if nothing is hit).
static FVector AimPoint(APlayerController* pc, bool crosshair)
{
    FVector cam{}; FRotator rot{};
    if (pc->PlayerCameraManager) { cam = pc->PlayerCameraManager->GetCameraLocation(); rot = pc->PlayerCameraManager->GetCameraRotation(); }
    else if (pc->Pawn) { cam = pc->Pawn->K2_GetActorLocation(); rot = pc->GetControlRotation(); }
    double yaw = rot.Yaw * 3.14159265358979 / 180.0, pitch = rot.Pitch * 3.14159265358979 / 180.0;
    FVector fwd{ std::cos(pitch) * std::cos(yaw), std::cos(pitch) * std::sin(yaw), std::sin(pitch) };
    FVector front = cam + FVector{ fwd.X * 300, fwd.Y * 300, fwd.Z * 300 };
    if (!crosshair) return front;
    FVector end = cam + FVector{ fwd.X * 5000, fwd.Y * 5000, fwd.Z * 5000 };
    FHitResult hit{};
    TArray<AActor*> ignore;
    bool ok = UKismetSystemLibrary::LineTraceSingle(pc, cam, end, ETraceTypeQuery::TraceTypeQuery1, false, ignore,
        EDrawDebugTrace::None, &hit, true, FLinearColor{}, FLinearColor{}, 0.0f);
    if (!ok) return front;
    // a little back towards the player, so it does not end up inside the ground
    FVector p{ hit.ImpactPoint.X, hit.ImpactPoint.Y, hit.ImpactPoint.Z };
    return p - FVector{ fwd.X * 40, fwd.Y * 40, fwd.Z * 40 };
}

static void Prepare(AActor* a, float radius)
{
    // these pick a random hidden spot on their own unless told they already have one
    if (a->IsA(ABP_MoneyBagPeep_C::StaticClass())) static_cast<ABP_MoneyBagPeep_C*>(a)->LocationFound = true;
    if (a->IsA(ABP_TKey1_C::StaticClass())) static_cast<ABP_TKey1_C*>(a)->LocationFound = true;
    if (a->IsA(ABP_Mine_Base_C::StaticClass())) static_cast<ABP_Mine_Base_C*>(a)->LocationFound = true;
    if (a->IsA(ABP_Mine2_C::StaticClass())) static_cast<ABP_Mine2_C*>(a)->LocationFound_0 = true;
    if (a->IsA(ABP_Mine3_C::StaticClass())) static_cast<ABP_Mine3_C*>(a)->LocationFound_0 = true;
    if (a->IsA(ABP_Mine4_C::StaticClass())) static_cast<ABP_Mine4_C*>(a)->LocationFound_0 = true;
    if (a->IsA(ABP_Explosion_C::StaticClass())) static_cast<ABP_Explosion_C*>(a)->ExplosionRadius *= radius;
    if (a->IsA(ABP_Dynamite_C::StaticClass())) static_cast<ABP_Dynamite_C*>(a)->ExplosionRadius *= radius;
}

void Game_Spawn(int index, int count, bool crosshair, float radius)
{
    if (index < 0 || index >= kSpawnCount) return;
    RunOnGame([=] {
        APlayerController* pc = PC();
        if (!pc) return;
        UClass* cls = UObject::FindObject<UClass>(kSpawns[index].cls);
        if (!cls) { Log("spawn: class %s not loaded", kSpawns[index].cls); return; }
        FVector at = AimPoint(pc, crosshair);
        for (int i = 0; i < count; i++)
        {
            FTransform t{};
            t.Rotation.W = 1;
            t.Scale3D = FVector{ 1, 1, 1 };
            t.Translation = at + FVector{ (double)((i % 5) - 2) * 60.0, (double)((i / 5) % 5 - 2) * 60.0, (double)(i / 25) * 60.0 };
            AActor* a = UGameplayStatics::BeginDeferredActorSpawnFromClass(pc, cls, t, ESpawnActorCollisionHandlingMethod::AlwaysSpawn, nullptr, ESpawnActorScaleMethod::MultiplyWithRoot);
            if (!a) { Log("spawn: %s failed", kSpawns[index].name); return; }
            Prepare(a, radius);
            UGameplayStatics::FinishSpawningActor(a, t, ESpawnActorScaleMethod::MultiplyWithRoot);
        }
        Log("spawned %s x%d", kSpawns[index].name, count);
    });
}

// ---------------- ores ----------------
static ABP_OreSpawner_C* OreSpawner()
{
    static ABP_OreSpawner_C* cached;
    static UWorld* world;
    static ULONGLONG scanned;
    UWorld* w = World();
    if (world != w) { cached = nullptr; world = w; scanned = 0; }
    if (cached || GetTickCount64() - scanned < 3000) return cached;
    scanned = GetTickCount64();
    UClass* cls = ABP_OreSpawner_C::StaticClass();
    for (int i = 0; cls && i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (o && !o->IsDefaultObject() && o->IsA(cls) && o->Outer && o->Outer->Outer == w) return cached = static_cast<ABP_OreSpawner_C*>(o);
    }
    return nullptr;
}

// Which ore is this? The mesh / component names say it (e.g. "SM_Gold").
static int OreTypeFromName(std::string n)
{
    static const char* keys[kOreCount] = { "stone", "coal", "iron", "copper", "silver", "gold", "platin", "ruby", "sapphire", "emerald" };
    for (auto& ch : n) ch = (char)tolower((unsigned char)ch);
    for (int i = kOreCount - 1; i >= 0; i--) if (n.find(keys[i]) != std::string::npos) return i;
    return -1;
}
static std::string MeshNames(UStaticMeshComponent* c)
{
    std::string n = c->GetName();
    if (c->StaticMesh) n += " " + c->StaticMesh->GetName();
    return n;
}

// Ore meshes drawn as instances (one component, many ores).
static std::vector<UInstancedStaticMeshComponent*> g_oreIsms;
static std::vector<int> g_oreIsmType;
static UWorld* g_oreWorld;
static ULONGLONG g_oreScan;
static void ScanOreInstances(bool verbose)
{
    UWorld* w = World();
    if (g_oreWorld == w && GetTickCount64() - g_oreScan < 10000) return;
    g_oreWorld = w; g_oreScan = GetTickCount64();
    g_oreIsms.clear(); g_oreIsmType.clear();
    UClass* cls = UInstancedStaticMeshComponent::StaticClass();
    int total = 0;
    for (int i = 0; cls && i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (!o || o->IsDefaultObject() || !o->IsA(cls)) continue;
        auto* c = static_cast<UInstancedStaticMeshComponent*>(o);
        if (!c->Outer || c->Outer->IsDefaultObject()) continue;
        total++;
        std::string n = MeshNames(c);
        int t = OreTypeFromName(n);
        if (verbose && total <= 25) Log("  ism %s: %d instances (type %d)", n.c_str(), c->GetInstanceCount(), t);
        if (t < 0) continue;
        g_oreIsms.push_back(c);
        g_oreIsmType.push_back(t);
    }
    if (verbose) Log("ores: %d instanced meshes, %d look like ores", total, (int)g_oreIsms.size());
}

static void CollectOres(float radiusMetres, bool toBag, bool quiet)
{
    Player* me = Me();
    if (!me) return;
    FVector at = me->K2_GetActorLocation();
    double r = radiusMetres * 100.0, r2 = r * r;
    int got[kOreCount] = {}, removed = 0;

    // 1) single ore components owned by the ore spawner
    if (ABP_OreSpawner_C* sp = OreSpawner())
    {
        std::vector<UStaticMeshComponent*> hits;
        for (int i = 0; i < sp->RefAllOres.Num(); i++)
        {
            UStaticMeshComponent* c = sp->RefAllOres[i];
            if (!c) continue;
            FVector p = c->K2_GetComponentLocation();
            double dx = p.X - at.X, dy = p.Y - at.Y, dz = p.Z - at.Z;
            if (dx * dx + dy * dy + dz * dz <= r2) hits.push_back(c);
        }
        if (!quiet) Log("ores: spawner has %d ore parts, %d cached locations, %d in range", sp->RefAllOres.Num(), sp->CachedOreLocations.Num(), (int)hits.size());
        for (UStaticMeshComponent* c : hits)
        {
            int t = OreTypeFromName(MeshNames(c));
            if (t >= 0) got[t]++;
            sp->DestroyOre(c);
            removed++;
        }
    }
    else if (!quiet) Log("ores: no ore spawner");

    // 2) instanced ore meshes
    ScanOreInstances(!quiet);
    for (size_t k = 0; k < g_oreIsms.size(); k++)
    {
        UInstancedStaticMeshComponent* c = g_oreIsms[k];
        TArray<int32> idx = c->GetInstancesOverlappingSphere(at, (float)r, true);
        if (idx.Num() == 0) continue;
        got[g_oreIsmType[k]] += idx.Num();
        removed += idx.Num();
        c->RemoveInstances(idx);
    }

    if (toBag)
        for (int i = 0; i < kOreCount; i++) if (got[i]) *OreField(me, i) += got[i];
    if (removed || !quiet) Log("ores: removed %d within %.0f m", removed, radiusMetres);
}

void Game_CollectOres(float radiusMetres, bool toBag) { RunOnGame([=] { CollectOres(radiusMetres, toBag, false); }); }
