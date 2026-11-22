// Game side: everything that touches Unreal objects runs here, on the game thread.
#include "cheats.h"
#include <cmath>
#include <algorithm>
#include <map>
#include "MinHook.h"

#include "SDK/Basic.hpp"
#include "SDK/CoreUObject_classes.hpp"
#include "SDK/Engine_classes.hpp"
#include "SDK/StorageHunter_classes.hpp"
#include "SDK/BP_CheatManager_classes.hpp"
#include "SDK/BP_PlayerCharacter_classes.hpp"
#include "SDK/BP_PlayerState_classes.hpp"
#include "SDK/BP_GameState_classes.hpp"
#include "SDK/BP_ExperienceSystem_Comp_classes.hpp"
#include "SDK/BP_TimeSystemChild_Comp_classes.hpp"
#include "SDK/Ultra_Dynamic_Sky_classes.hpp"
#include "SDK/UMG_parameters.hpp"
#include "SDK/BP_ItemsSystem_classes.hpp"
#include "SDK/BP_District_classes.hpp"
#include "SDK/BP_RenownController_Comp_classes.hpp"
#include "SDK/BP_PerksController_Comp_classes.hpp"
#include "SDK/PA_District_classes.hpp"
#include "SDK/BP_Cheats_Comp_classes.hpp"
#include "SDK/BP_TradableItemBase_classes.hpp"
#include "SDK/F_TradableItem_DataRow_v11_structs.hpp"
#include "SDK/BP_PlayerEstate_Plot_classes.hpp"
#include "SDK/PA_PlayerEstate_PlotData_classes.hpp"
#include "SDK/BP_PawnShopBase_classes.hpp"
#include "SDK/BP_ClassicAuctionSiteController_classes.hpp"
#include "SDK/BP_BlindAuctionSiteController_classes.hpp"
#include "SDK/BP_Garage_classes.hpp"
#include "SDK/BP_PlayerCarBase_classes.hpp"

using namespace SDK;

static std::mutex g_qLock;
static std::vector<std::function<void()>> g_queue;
static DWORD g_gameThread;
static bool g_inTick;
static ULONGLONG g_lastTick;

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
        Log("exception %08X in %s at %s+0x%llX (read 0x%llX)", e->ExceptionRecord->ExceptionCode, where, base ? base + 1 : name,
            (unsigned long long)((char*)addr - (char*)mod),
            e->ExceptionRecord->NumberParameters > 1 ? (unsigned long long)e->ExceptionRecord->ExceptionInformation[1] : 0ull);
    }
    return EXCEPTION_EXECUTE_HANDLER;
}
#define GUARD(where, code) __try { code; } __except (ExcFilter(GetExceptionInformation(), where)) {}

static ULONGLONG g_traceUntil;

void RunOnGame(std::function<void()> fn)
{
    std::lock_guard<std::mutex> lk(g_qLock);
    g_queue.push_back(std::move(fn));
}

static std::string TextToString(const FText& t);

// ---------------- object helpers ----------------
template<typename T> static bool Valid(T* o) { return o && !IsBadReadPtr(o, sizeof(void*)); }

static UWorld* World() { return UWorld::GetWorld(); }

static APlayerController* PC()
{
    UWorld* w = World();
    if (!w || !w->OwningGameInstance || w->OwningGameInstance->LocalPlayers.Num() == 0) return nullptr;
    ULocalPlayer* lp = w->OwningGameInstance->LocalPlayers[0];
    return lp ? lp->PlayerController : nullptr;
}

static ABP_PlayerCharacter_C* Me()
{
    APlayerController* pc = PC();
    if (!pc || !pc->Pawn || !pc->Pawn->IsA(ABP_PlayerCharacter_C::StaticClass())) return nullptr;
    return static_cast<ABP_PlayerCharacter_C*>(pc->Pawn);
}

static ABP_GameState_C* GS()
{
    UWorld* w = World();
    if (!w || !w->GameState || !w->GameState->IsA(ABP_GameState_C::StaticClass())) return nullptr;
    return static_cast<ABP_GameState_C*>(w->GameState);
}

static ABP_PlayerState_C* PS()
{
    APlayerController* pc = PC();
    if (!pc || !pc->PlayerState || !pc->PlayerState->IsA(ABP_PlayerState_C::StaticClass())) return nullptr;
    return static_cast<ABP_PlayerState_C*>(pc->PlayerState);
}

// There are several experience components; the one on the game state stays at 0.
static UBP_ExperienceSystem_Comp_C* XpComp()
{
    static UBP_ExperienceSystem_Comp_C* cached;
    static UWorld* cachedWorld;
    UWorld* w = World();
    static ULONGLONG scannedAt;
    if (cachedWorld == w && (cached || GetTickCount64() - scannedAt < 5000)) return cached;
    cachedWorld = w; cached = nullptr; scannedAt = GetTickCount64();
    UBP_ExperienceSystem_Comp_C* best = nullptr;
    for (int i = 0; i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (!o || o->IsDefaultObject() || !o->IsA(UBP_ExperienceSystem_Comp_C::StaticClass())) continue;
        auto* c = static_cast<UBP_ExperienceSystem_Comp_C*>(o);
        Log("xp comp %s: level %d, xp %d", c->GetFullName().c_str(), c->CurrentLevel, c->CurrentExperience);
        if (!best || c->CurrentLevel > best->CurrentLevel || c->CurrentExperience > best->CurrentExperience) best = c;
    }
    return cached = best;
}

// Level, renown and perk points are kept per district.
static std::vector<ABP_District_C*> g_districts;
static std::map<ABP_District_C*, std::string> g_districtNames;
static UWorld* g_districtWorld;
static ULONGLONG g_districtScan;
static const std::vector<ABP_District_C*>& Districts()
{
    UWorld* w = World();
    if (g_districtWorld != w) { g_districts.clear(); g_districtNames.clear(); g_districtWorld = w; g_districtScan = 0; }
    if (g_districts.empty() && GetTickCount64() - g_districtScan > 5000)
    {
        g_districtScan = GetTickCount64();
        UClass* cls = ABP_District_C::StaticClass();
        for (int i = 0; cls && i < UObject::GObjects->Num(); i++)
        {
            UObject* o = UObject::GObjects->GetByIndex(i);
            if (o && !o->IsDefaultObject() && o->IsA(cls) && o->Outer && o->Outer->Outer == w)
                g_districts.push_back(static_cast<ABP_District_C*>(o));
        }
        std::sort(g_districts.begin(), g_districts.end(), [](ABP_District_C* a, ABP_District_C* b) {
            return (a->Data ? a->Data->Order : 0) < (b->Data ? b->Data->Order : 0); });
        Log("districts: %d", (int)g_districts.size());
    }
    return g_districts;
}

static bool Allowed()
{
    UWorld* w = World();
    return w && w->GameState && w->GameState->PlayerArray.Num() <= 1;
}

// The developers' own cheat manager; shipping builds never create it, so we do.
static UBP_CheatManager_C* CM()
{
    APlayerController* pc = PC();
    if (!pc) return nullptr;
    if (pc->CheatManager && pc->CheatManager->IsA(UBP_CheatManager_C::StaticClass()))
        return static_cast<UBP_CheatManager_C*>(pc->CheatManager);
    UClass* cls = UBP_CheatManager_C::StaticClass();
    if (!cls) return nullptr;
    UObject* obj = UGameplayStatics::SpawnObject(cls, pc);
    if (!obj) return nullptr;
    pc->CheatManager = static_cast<UCheatManager*>(obj);
    Log("cheat manager created");
    return static_cast<UBP_CheatManager_C*>(obj);
}

static AUltra_Dynamic_Sky_C* g_sky;
static UWorld* g_skyWorld;
static AUltra_Dynamic_Sky_C* Sky()
{
    UWorld* w = World();
    if (g_skyWorld != w) { g_sky = nullptr; g_skyWorld = w; }
    if (g_sky) return g_sky;
    static ULONGLONG lastScan;
    if (GetTickCount64() - lastScan < 5000) return nullptr;
    lastScan = GetTickCount64();
    UClass* cls = AUltra_Dynamic_Sky_C::StaticClass();
    if (!cls) return nullptr;
    for (int i = 0; i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (o && o->IsA(cls) && !o->IsDefaultObject() && o->Outer && o->Outer->Outer == w)
            return g_sky = static_cast<AUltra_Dynamic_Sky_C*>(o);
    }
    return nullptr;
}

template<typename P> static void CallStatic(const char* fn, P& parms)
{
    static UFunction* f = nullptr;
    if (!f) f = UObject::FindObject<UFunction>(fn);
    if (!f) return;
    UClass* cls = static_cast<UClass*>(f->Outer);
    if (cls && cls->ClassDefaultObject) cls->ClassDefaultObject->ProcessEvent(f, &parms);
}

// ---------------- per-character originals (restored when a cheat is off) ----------------
struct Orig
{
    void* who = nullptr;
    double walk = 0, sprint = 0;
    float jumpZ = 0, gravity = 1, flySpeed = 0;
    int32 jumpMax = 1;
};
static Orig g_orig;
static bool g_flying;
static bool g_lastVerifyAlways;
static bool g_menuInputLocked;
static ULONGLONG g_lastFuel;

static void ApplyMovement(ABP_PlayerCharacter_C* me, bool allowed, float dt)
{
    UCharacterMovementComponent* cm = me->CharacterMovement;
    if (!cm) return;
    if (g_orig.who != me)
    {
        g_orig = { me, me->WalkSpeed, me->SprintSpeed, cm->JumpZVelocity, cm->GravityScale, cm->MaxFlySpeed, me->JumpMaxCount };
        g_flying = false;
    }

    float speed = allowed ? g_set.speedMul : 1.0f;
    double walk = g_orig.walk * speed, sprint = g_orig.sprint * speed;
    if (me->WalkSpeed != walk || me->SprintSpeed != sprint)
    {
        // keep the current movement speed in step with walking/sprinting
        double cur = cm->MaxWalkSpeed;
        if (std::fabs(cur - me->SprintSpeed) < 1.0) cm->MaxWalkSpeed = (float)sprint;
        else if (std::fabs(cur - me->WalkSpeed) < 1.0) cm->MaxWalkSpeed = (float)walk;
        me->WalkSpeed = walk;
        me->SprintSpeed = sprint;
    }
    cm->JumpZVelocity = g_orig.jumpZ * (allowed ? g_set.jumpMul : 1.0f);
    cm->GravityScale = g_orig.gravity * (allowed ? g_set.gravity : 1.0f);
    me->JumpMaxCount = (allowed && g_set.unlimitedJumps) ? 999 : g_orig.jumpMax;

    bool fly = allowed && g_set.fly;
    if (fly != g_flying)
    {
        g_flying = fly;
        me->SetActorEnableCollision(!fly);
        cm->SetMovementMode(fly ? EMovementMode::MOVE_Flying : EMovementMode::MOVE_Walking, 0);
        cm->MaxFlySpeed = fly ? 1.0f : g_orig.flySpeed;
        Log(fly ? "fly on" : "fly off");
    }
    if (fly)
    {
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

static void SafeMovement(ABP_PlayerCharacter_C* me, bool allowed, float dt) { GUARD("movement", ApplyMovement(me, allowed, dt)); }
static void RunJobInner(std::function<void()>* j) { (*j)(); }
static void RunJob(std::function<void()>& j) { GUARD("action", RunJobInner(&j)); }

static void WatchSpawns();
static void UpdateLocations();
static void SafeLocations();
static void SafeCheckCar();
static void NameDistricts();
// NPC bidders lose all their money, so they can't outbid you.
static void BreakBidders()
{
    static ULONGLONG last;
    if (GetTickCount64() - last < 500) return;
    last = GetTickCount64();
    UClass* cls = USHBidderComponent::StaticClass();
    for (int i = 0; cls && i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (!o || o->IsDefaultObject() || !o->IsA(cls)) continue;
        auto* b = static_cast<USHBidderComponent*>(o);
        if (!b->PlayerCharacter && b->MoneyBalance > 0) b->MoneyBalance = 0;
    }
}
static void Tick(float dt)
{
    APlayerController* pc = PC();
    ABP_PlayerCharacter_C* me = Me();
    ABP_GameState_C* gs = GS();
    bool allowed = Allowed();

    // menu input lock follows the menu (also re-applied after loading a new level)
    if (pc && g_menuInputLocked != g_menuOpen) SetMenuInput(g_menuOpen);
    if (!pc) g_menuInputLocked = false;

    {
        std::vector<std::function<void()>> jobs;
        { std::lock_guard<std::mutex> lk(g_qLock); jobs.swap(g_queue); }
        if (allowed && me) for (auto& j : jobs) RunJob(j);
    }

    if (me && !g_traceUntil) { g_traceUntil = 1; Log("in game, tracing top-level calls"); }
    if (me) SafeMovement(me, allowed, dt);
    WatchSpawns();

    if (gs && allowed)
    {
        if (g_set.alwaysVerifyFix != g_lastVerifyAlways)
            if (auto* cm = CM()) { cm->CM_SetVerificationAndFixAlways(g_set.alwaysVerifyFix); g_lastVerifyAlways = g_set.alwaysVerifyFix; }
        if (g_set.infiniteFuel && GetTickCount64() - g_lastFuel > 3000)
            if (auto* cm = CM()) { cm->CM_AddMaxFuel(); g_lastFuel = GetTickCount64(); }
    }
    else if (gs && !allowed && g_lastVerifyAlways)
    {
        if (auto* cm = CM()) { cm->CM_SetVerificationAndFixAlways(false); g_lastVerifyAlways = false; }
    }

    if (gs && allowed && g_set.brokeBidders) BreakBidders();
    if (gs && me) { SafeLocations(); SafeCheckCar(); }

    if (AUltra_Dynamic_Sky_C* sky = gs ? Sky() : nullptr)
    {
        bool freeze = allowed && g_set.freezeTime;
        sky->Animate_Time_of_Day = !freeze;
    }

    // snapshot for the menu
    GameView v{};
    v.inGame = me != nullptr && gs != nullptr;
    v.allowed = allowed;
    v.players = (World() && World()->GameState) ? World()->GameState->PlayerArray.Num() : 0;
    if (ABP_PlayerState_C* ps = PS())
    {
        v.money = static_cast<ASHPlayerState*>(ps)->Credits;
        v.tickets = ps->Tickets;
        v.verifyLevel = ps->VerificationToolLevel;
        v.fixLevel = ps->FixingToolLevel;
        v.lockLevel = ps->LockPickingToolLevel;
    }
    if (gs)
    {
        NameDistricts();
        for (ABP_District_C* d : Districts())
        {
            DistrictView dv;
            dv.order = d->Data ? d->Data->Order : 0;
            dv.name = g_districtNames[d];
            if (dv.name.empty()) dv.name = "District " + std::to_string(dv.order + 1);
            if (auto* r = d->BP_RenownController_Comp) { dv.level = r->CurrentLevel; dv.points = r->CurrentPoints; dv.required = r->RequiredPoints; }
            if (auto* pk = d->BP_PerksController_Comp) dv.perks = pk->PerkPoints;
            v.districts.push_back(dv);
        }
    }
    if (auto* xc = gs ? XpComp() : nullptr)
    {
        v.level = xc->CurrentLevel;
        v.maxLevel = xc->MaxLevel;
        v.xp = xc->CurrentExperience;
    }
    if (gs && gs->BP_TimeSystemChild_Comp) v.passedDays = gs->BP_TimeSystemChild_Comp->PassedDaysNumber;
    if (AUltra_Dynamic_Sky_C* sky = gs ? g_sky : nullptr) { v.hasSky = true; v.timeOfDay = sky->Time_of_Day; }
    std::lock_guard<std::mutex> lk(g_viewLock);
    g_view = v;
}

// ---------------- ProcessEvent hook = our game-thread tick ----------------
using ProcessEventFn = void(*)(const UObject*, UFunction*, void*);
static ProcessEventFn oProcessEvent;

#include <unordered_map>
static bool IsSafePointInner(UFunction* fn, bool* out) { *out = fn->GetName() == "ReceiveTick"; return true; }
static bool IsSafePoint(UFunction* fn)
{
    static std::unordered_map<UFunction*, bool> known;
    auto it = known.find(fn);
    if (it != known.end()) return it->second;
    bool r = false;
    IsSafePointInner(fn, &r);
    known[fn] = r;
    return r;
}

static void SafeTick(float dt) { GUARD("tick", Tick(dt)); }

static int g_depth;
static void Trace(const UObject* obj, UFunction* fn)
{
    static std::vector<std::string> seen;
    if (seen.size() > 300) return;
    std::string n = fn->GetFullName() + " on " + obj->Class->GetName();
    for (auto& x : seen) if (x == n) return;
    seen.push_back(n);
    Log("top-level: %s", n.c_str());
}

static void HkProcessEvent(const UObject* obj, UFunction* fn, void* parms)
{
    bool game = GetCurrentThreadId() == g_gameThread;
    if (game) g_depth++;
    oProcessEvent(obj, fn, parms);
    if (!game) return;
    g_depth--;
    if (g_depth != 0 || g_inTick) return;
    if (g_traceUntil && GetTickCount64() < g_traceUntil) GUARD("trace", Trace(obj, fn));
    // Only run right after an actor's ReceiveTick: a safe point outside movement/physics updates.
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
#define CM_CALL(expr) RunOnGame([=] { if (auto* cm = CM()) { Log("cheat: %s", #expr); cm->expr; } else Log("no cheat manager for %s", #expr); })
void Game_AddMoney(int a)
{
    RunOnGame([=] {
        ASHPlayerState* ps = static_cast<ASHPlayerState*>(PS());
        if (!ps) { Log("money: no player state"); return; }
        int before = ps->Credits;
        long long target = (long long)before + a;
        if (target < 0) target = 0;
        if (target > 2000000000) target = 2000000000;
        ps->ServerSetPlayerCredits((int)target);
        Log("money %d -> %d", before, ps->Credits);
    });
}
void Game_AddXp(int a)
{
    RunOnGame([=] {
        ABP_GameState_C* gs = GS();
        auto* xp = XpComp();
        int before = xp ? xp->CurrentExperience : -1, lvl = xp ? xp->CurrentLevel : -1;
        if (xp) xp->Add_Experience_by_Value(a);
        Log("xp %d (lvl %d) -> %d (lvl %d)", before, lvl, xp ? xp->CurrentExperience : -1, xp ? xp->CurrentLevel : -1);
    });
}
void Game_AddPerkPoints(int a, int district)
{
    RunOnGame([=] {
        int i = 0;
        for (ABP_District_C* d : Districts())
        {
            if ((district < 0 || district == i) && d->BP_PerksController_Comp)
            {
                for (int n = 0; n < a; n++) d->BP_PerksController_Comp->AddPerkPoint();
                Log("perks district %d -> %d", i, d->BP_PerksController_Comp->PerkPoints);
            }
            i++;
        }
    });
}
void Game_AddRenown(int a, int district)
{
    RunOnGame([=] {
        int i = 0;
        for (ABP_District_C* d : Districts())
        {
            if ((district < 0 || district == i) && d->BP_RenownController_Comp)
            {
                auto* r = d->BP_RenownController_Comp;
                int lvl = r->CurrentLevel, pts = r->CurrentPoints;
                r->AddPoints(a);
                Log("renown district %d: lvl %d pts %d -> lvl %d pts %d", i, lvl, pts, r->CurrentLevel, r->CurrentPoints);
            }
            i++;
        }
    });
}
void Game_SetToolLevels(int v, int f, int l)
{
    RunOnGame([=] { if (auto* cm = CM()) { cm->CM_SetVerifiactionLevelAndFixingToolLevel(v, f); cm->CM_SetLockPickupTool(l); } });
}
void Game_PawnShopLevelUp(int n) { CM_CALL(CM_PawnShop_LevelUp(n)); }
void Game_BuyPawnShop() { CM_CALL(CM_PawnShop_BuyPawnShop()); }
void Game_WinAuction() { CM_CALL(CM_DebugWinAuctionEventCall()); }
void Game_UnlockContainer() { CM_CALL(CM_UnlockHighlightedContainer()); }
void Game_AuthenticateItem() { CM_CALL(CM_AuthenticateHistoriTradable()); }
void Game_CompleteTask() { CM_CALL(CM_CompleteCurrentTask()); }
void Game_CompleteQuest() { CM_CALL(CM_CompleteCurrentQuest()); }
void Game_FillFuel() { CM_CALL(CM_AddMaxFuel()); }
void Game_SpawnRandomItems(int n) { RunOnGame([=] { if (auto* cm = CM()) for (int i = 0; i < n; i++) cm->CM_SpawnRandomItem(); }); }
void Game_SetTimeOfDay(double t)
{
    RunOnGame([=] { if (auto* s = Sky()) { s->Time_of_Day = t; s->Internal_Time_of_Day = t; s->Replicated_Time_of_Day = t; } });
}
void Game_AddTickets(int a)
{
    RunOnGame([=] { if (auto* ps = PS()) { int n = ps->Tickets + a; if (n < 0) n = 0; ps->ServertSetTickets(n); ps->ClientSetTickets(n); } });
}

// ---------------- items ----------------
std::vector<ItemEntry> g_items;
std::vector<std::string> g_itemCats;
std::mutex g_itemsLock;
static int g_spawnWatchFrom = -1;    // GObjects index: log tradables created after this
static ULONGLONG g_spawnWatchUntil;

static ABP_ItemsSystem_C* ItemsSystem()
{
    ABP_GameState_C* gs = GS();
    if (!gs || !gs->ChildActor_ItemsSystem) return nullptr;
    AActor* a = gs->ChildActor_ItemsSystem->ChildActor;
    return (a && a->IsA(ABP_ItemsSystem_C::StaticClass())) ? static_cast<ABP_ItemsSystem_C*>(a) : nullptr;
}

static std::string TableInfo(UDataTable* t) { return t ? t->GetName() + " (" + std::to_string(t->RowMap.Num()) + " rows)" : "null"; }

// The English source text of an FText (the display text is in the player's language).
// The text data keeps the source string as an FString near its start; we look for it.
static bool LooksLikeFString(const uint8* p, const wchar_t** str, int* len)
{
    const wchar_t* data = *(const wchar_t* const*)p;
    int32 num = *(const int32*)(p + 8), max = *(const int32*)(p + 12);
    if (!data || num < 2 || num > 512 || max < num || max > 4096 || ((uintptr_t)data & 1)) return false;
    if (IsBadReadPtr(data, num * sizeof(wchar_t))) return false;
    if (data[num - 1] != 0) return false;
    for (int i = 0; i < num - 1; i++) if (data[i] < 0x20) return false;
    *str = data; *len = num - 1;
    return true;
}
static void RawSource(const FText* t, wchar_t* out, int cap)
{
    __try
    {
        const uint8* d = reinterpret_cast<const uint8*>(t->TextData);
        if (!d || IsBadReadPtr(d, 0x60)) return;
        for (int off = 8; off <= 0x50; off += 8)
        {
            const wchar_t* s; int n;
            if (LooksLikeFString(d + off, &s, &n)) { wcsncpy_s(out, cap, s, _TRUNCATE); return; }
        }
    }
    __except (EXCEPTION_EXECUTE_HANDLER) {}
}
static std::string TextToString(const FText& t)
{
    wchar_t buf[512] = {};
    RawSource(&t, buf, 512);
    std::string display = UKismetTextLibrary::Conv_TextToString(t).ToString();
    if (!buf[0]) return display;
    char utf8[1024] = {};
    WideCharToMultiByte(CP_UTF8, 0, buf, -1, utf8, sizeof utf8, nullptr, nullptr);
    return utf8[0] ? utf8 : display;
}

static void ReadRow(uint8* p, ItemEntry* e, bool* soon)
{
    auto* row = reinterpret_cast<FF_TradableItem_DataRow_v11*>(p);
    e->name = TextToString(row->Name_175_860E6C804D172B3202F1E8A589FE1AF2);
    e->cls = row->ItemPrefab_149_01A1152249F00229B64A5C86C0A60A9E.ObjectID.AssetPath.AssetName.ToString();
    e->cat = (int)row->Category_120_0B57E7BD4E0BB423029564873E8DE3B7;
    e->rarity = (int)row->Rarity_185_4008252646D1F70477FAEDB75CB78E2D;
    e->value = row->BaseValue_188_2E7F86EF4EE7313E022E789FAF4989B7;
    *soon = row->isComingSoon_195_93BED48E4ED20912A1C5509FE1B91CE1;
}
static void SafeReadRow(uint8* p, ItemEntry* e, bool* soon, bool* ok)
{
    if (!p) return;
    __try { ReadRow(p, e, soon); *ok = true; }
    __except (ExcFilter(GetExceptionInformation(), "item row")) {}
}
static void CategoryName(ABP_ItemsSystem_C* is, int c, std::string* out)
{
    FText t{};
    is->GetCategoryName((EItemsCategories)c, &t);
    *out = TextToString(t);
}
static void SafeCategoryName(ABP_ItemsSystem_C* is, int c, std::string* out)
{
    __try { CategoryName(is, c, out); }
    __except (ExcFilter(GetExceptionInformation(), "category")) {}
}

void Game_ScanItems()
{
    RunOnGame([] {
        ABP_ItemsSystem_C* is = ItemsSystem();
        if (!is) { Log("items: no items system"); return; }
        UDataTable* table = nullptr;
        for (int i = 0; i < UObject::GObjects->Num() && !table; i++)
        {
            UObject* o = UObject::GObjects->GetByIndex(i);
            if (!o || o->IsDefaultObject() || !o->IsA(UBP_Cheats_Comp_C::StaticClass())) continue;
            auto* cc = static_cast<UBP_Cheats_Comp_C*>(o);
            if (cc->DefaultsItemsDataLocal.Num() > 0) table = cc->DefaultsItemsDataLocal[0];
        }
        if (!table) table = is->DT_BasicTradables;
        if (!table) { Log("items: no table"); return; }
        std::vector<ItemEntry> list;
        int bad = 0;
        // the special (historic) items, e.g. Thor's hammer, get their own category
        // (= every row of the full table that is not in the basic table)
        std::vector<int32> basic;
        if (is->DT_BasicTradables && is->DT_BasicTradables != table)
            for (const auto& bp : is->DT_BasicTradables->RowMap) basic.push_back(bp.Key().ComparisonIndex);
        int specials = 0;
        const int kSpecial = (int)EItemsCategories::EItemsCategories_MAX;
        int rowIdx = 0;
        for (const auto& pair : table->RowMap)
        {
            ItemEntry e;
            e.index = rowIdx++;
            bool ok = false, soon = false;
            SafeReadRow(pair.Value(), &e, &soon, &ok);
            if (!ok) { if (++bad < 5) Log("items: row %d unreadable", e.index); continue; }
            if (e.name.empty()) e.name = pair.Key().ToString();
            if (!basic.empty() && std::find(basic.begin(), basic.end(), pair.Key().ComparisonIndex) == basic.end())
            {
                e.cat = kSpecial; soon = false; specials++;
                Log("  special #%d %s [%s]", e.index, e.name.c_str(), e.cls.c_str());
            }
            if (!soon) list.push_back(e);
        }
        std::vector<std::string> cats;
        for (int c = 0; c < (int)EItemsCategories::EItemsCategories_MAX; c++)
        {
            std::string name;
            SafeCategoryName(is, c, &name);
            cats.push_back(name.empty() ? "Category " + std::to_string(c) : name);
        }
        cats.push_back("Special");
        Log("items: %d special rows", specials);

        // drop the "unknown" category and placeholder items
        auto unknown = [](std::string n) {
            for (auto& ch : n) ch = (char)tolower((unsigned char)ch);
            return n.find("unknown") != std::string::npos || n.find("unbekannt") != std::string::npos;
        };
        for (int c = 0; c < (int)cats.size(); c++)
        {
            Log("  category %d: %s", c, cats[c].c_str());
            if (unknown(cats[c])) cats[c].clear();
        }
        list.erase(std::remove_if(list.begin(), list.end(), [&](const ItemEntry& e) {
            return unknown(e.name) || (e.cat < (int)cats.size() && cats[e.cat].empty()); }), list.end());
        Log("items: %d from %s", (int)list.size(), TableInfo(table).c_str());
        for (int i = 0; i < (int)list.size() && i < 5; i++) Log("  #%d %s [%s]", list[i].index, list[i].name.c_str(), list[i].cls.c_str());

        // which tables does the developers' cheat component use?
        for (int i = 0; i < UObject::GObjects->Num(); i++)
        {
            UObject* o = UObject::GObjects->GetByIndex(i);
            if (!o || o->IsDefaultObject() || !o->IsA(UBP_Cheats_Comp_C::StaticClass())) continue;
            auto* cc = static_cast<UBP_Cheats_Comp_C*>(o);
            Log("cheats comp %s: selected %s", cc->GetFullName().c_str(), TableInfo(cc->SelectedTable).c_str());
            for (int t = 0; t < cc->DefaultsItemsDataLocal.Num(); t++) Log("  items table %d: %s", t, TableInfo(cc->DefaultsItemsDataLocal[t]).c_str());
        }

        std::lock_guard<std::mutex> lk(g_itemsLock);
        g_items.swap(list);
        g_itemCats.swap(cats);
    });
}

void Game_SpawnItem(int index, int count, bool verified)
{
    RunOnGame([=] {
        auto* cm = CM();
        if (!cm) return;
        g_spawnWatchFrom = UObject::GObjects->Num();
        g_spawnWatchUntil = GetTickCount64() + 2000;
        Log("spawn index %d x%d verified=%d", index, count, (int)verified);
        cm->CM_SpawnRangeItem(index, index, count, verified);
    });
}

// called from Tick: log what actually got spawned (to check the index mapping)
static void WatchSpawns()
{
    if (g_spawnWatchFrom < 0) return;
    int n = UObject::GObjects->Num();
    UClass* tc = ABP_TradableItemBase_C::StaticClass();
    for (int i = g_spawnWatchFrom; i < n; i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (o && tc && o->IsA(tc) && !o->IsDefaultObject()) Log("  spawned: %s", o->Class->GetName().c_str());
    }
    g_spawnWatchFrom = n;
    if (GetTickCount64() > g_spawnWatchUntil) g_spawnWatchFrom = -1;
}

static void DistrictName(ABP_District_C* d, std::string* out) { *out = TextToString(d->Data->Name_0); }
static void SafeDistrictName(ABP_District_C* d, std::string* out)
{
    __try { DistrictName(d, out); }
    __except (ExcFilter(GetExceptionInformation(), "district name")) {}
}
static void NameDistricts()
{
    for (ABP_District_C* d : Districts())
    {
        if (!d->Data || g_districtNames.count(d)) continue;
        std::string n;
        SafeDistrictName(d, &n);
        g_districtNames[d] = n;
    }
}

// ---------------- unanchor: let fixed map objects fall / be pushed around ----------------
struct Unanchored
{
    UPrimitiveComponent* comp = nullptr; int32 compIdx = 0;   // the component we made simulate
    FTransform xf{};                                           // where it was
    EComponentMobility mobility = EComponentMobility::Static;
    ECollisionEnabled collision = ECollisionEnabled::QueryAndPhysics;
    USceneComponent* parent = nullptr; int32 parentIdx = 0; FName socket{};
    UInstancedStaticMeshComponent* ism = nullptr; int32 ismIdx = 0;   // instanced mesh: we spawned a stand-in actor
    AActor* spawned = nullptr; int32 spawnedIdx = 0;
};
static std::vector<Unanchored> g_unanchored;
static UWorld* g_unanchorWorld;
static int g_unanchorFails;

static bool Alive(UObject* o, int32 idx) { return o && UObject::GObjects->GetByIndex(idx) == o; }

static void CheckUnanchorWorld()
{
    if (g_unanchorWorld != World()) { g_unanchored.clear(); g_unanchorWorld = World(); }
}

static float Radius(USceneComponent* c)
{
    FVector o, e; float r = 0;
    UKismetSystemLibrary::GetComponentBounds(c, &o, &e, &r);
    return r;
}

static bool Skippable(UPrimitiveComponent* c)
{
    if (!c || !c->IsA(UStaticMeshComponent::StaticClass())) return true;
    AActor* owner = c->GetOwner();
    if (!owner || owner == Me() || owner->IsA(APawn::StaticClass())) return true;
    std::string cls = owner->Class->GetName();
    if (cls.find("Landscape") != std::string::npos || cls.find("Sky") != std::string::npos) return true;
    if (!static_cast<UStaticMeshComponent*>(c)->StaticMesh) return true;
    return false;
}

static bool TooBig(float r) { return !g_set.unanchorBig && r > 300.0f; }

static bool MakeSimulate(UPrimitiveComponent* c)
{
    ECollisionEnabled ce = c->GetCollisionEnabled();
    if (ce != ECollisionEnabled::QueryAndPhysics && ce != ECollisionEnabled::PhysicsOnly) ce = ECollisionEnabled::QueryAndPhysics;
    // rebuild the physics body so it is no longer a static one
    c->SetCollisionEnabled(ECollisionEnabled::NoCollision);
    c->SetCollisionEnabled(ce);
    c->SetSimulatePhysics(true);
    c->SetEnableGravity(!g_set.unanchorNoGravity);
    c->WakeRigidBody(FName());
    return c->IsSimulatingPhysics(FName());
}

// Unanchor one normal mesh component. Returns the component that now simulates (or null).
static UPrimitiveComponent* UnanchorComp(UPrimitiveComponent* c)
{
    if (Skippable(c)) return nullptr;
    // a mesh that is the root of its actor takes the whole actor with it
    AActor* owner = c->GetOwner();
    if (USceneComponent* root = owner->K2_GetRootComponent())
        if (root != c && root->IsA(UStaticMeshComponent::StaticClass()) && !Skippable(static_cast<UPrimitiveComponent*>(root)) && !TooBig(Radius(root)))
            c = static_cast<UPrimitiveComponent*>(root);
    if (c->IsSimulatingPhysics(FName())) return c;
    if (TooBig(Radius(c))) return nullptr;

    Unanchored u;
    u.comp = c; u.compIdx = c->Index;
    u.xf = c->K2_GetComponentToWorld();
    u.mobility = c->Mobility;
    u.collision = c->GetCollisionEnabled();
    u.parent = c->GetAttachParent(); if (u.parent) u.parentIdx = u.parent->Index;
    u.socket = c->GetAttachSocketName();

    if (c->Mobility != EComponentMobility::Movable) c->SetMobility(EComponentMobility::Movable);
    if (!MakeSimulate(c))
    {
        // no physics body (mesh without collision) - put it back
        c->SetSimulatePhysics(false);
        c->SetCollisionEnabled(u.collision);
        if (u.mobility != EComponentMobility::Movable) c->SetMobility(u.mobility);
        g_unanchorFails++;
        return nullptr;
    }
    g_unanchored.push_back(u);
    return c;
}

// Instanced meshes (many copies drawn as one): swap the copy for a real actor that can fall.
static UPrimitiveComponent* UnanchorInstance(UInstancedStaticMeshComponent* ism, int32 item)
{
    if (!ism || item < 0 || !ism->StaticMesh) return nullptr;
    AActor* owner = ism->GetOwner();
    if (!owner || owner->IsA(APawn::StaticClass())) return nullptr;
    FTransform xf{};
    if (!ism->GetInstanceTransform(item, &xf, true)) return nullptr;
    {
        // size of one copy
        FBoxSphereBounds b = ism->StaticMesh->GetBounds();
        double s = (std::max)({ std::fabs(xf.Scale3D.X), std::fabs(xf.Scale3D.Y), std::fabs(xf.Scale3D.Z) });
        if (TooBig((float)(b.SphereRadius * s))) return nullptr;
    }
    AActor* a = UGameplayStatics::BeginDeferredActorSpawnFromClass(World(), AStaticMeshActor::StaticClass(), xf,
        ESpawnActorCollisionHandlingMethod::AlwaysSpawn, nullptr, ESpawnActorScaleMethod::MultiplyWithRoot);
    if (!a) return nullptr;
    UStaticMeshComponent* smc = static_cast<AStaticMeshActor*>(a)->StaticMeshComponent;
    if (smc) smc->SetMobility(EComponentMobility::Movable);
    UGameplayStatics::FinishSpawningActor(a, xf, ESpawnActorScaleMethod::MultiplyWithRoot);
    if (!smc) { a->K2_DestroyActor(); return nullptr; }
    smc->SetStaticMesh(ism->StaticMesh);
    for (int i = 0; i < ism->GetNumMaterials(); i++) smc->SetMaterial(i, ism->GetMaterial(i));
    smc->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
    smc->SetSimulatePhysics(true);
    smc->SetEnableGravity(!g_set.unanchorNoGravity);
    if (!smc->IsSimulatingPhysics(FName())) { a->K2_DestroyActor(); g_unanchorFails++; return nullptr; }
    ism->RemoveInstance(item);

    Unanchored u;
    u.comp = smc; u.compIdx = smc->Index;
    u.xf = xf;
    u.ism = ism; u.ismIdx = ism->Index;
    u.spawned = a; u.spawnedIdx = a->Index;
    g_unanchored.push_back(u);
    return smc;
}

static UPrimitiveComponent* UnanchorHit(UPrimitiveComponent* c, int32 item)
{
    if (!c) return nullptr;
    if (c->IsA(UInstancedStaticMeshComponent::StaticClass()))
        return UnanchorInstance(static_cast<UInstancedStaticMeshComponent*>(c), item);
    return UnanchorComp(c);
}

static void ViewRay(bool useMouse, FVector* start, FVector* dir)
{
    APlayerController* pc = PC();
    if (useMouse && pc->DeprojectMousePositionToWorld(start, dir)) return;
    APlayerCameraManager* cam = pc->PlayerCameraManager;
    *start = cam->GetCameraLocation();
    FRotator r = cam->GetCameraRotation();
    double yaw = r.Yaw * 3.14159265358979 / 180.0, pitch = r.Pitch * 3.14159265358979 / 180.0;
    *dir = FVector{ std::cos(pitch) * std::cos(yaw), std::cos(pitch) * std::sin(yaw), std::sin(pitch) };
}

void Game_UnanchorClick(bool useMouse, bool freeze)
{
    RunOnGame([=] {
        CheckUnanchorWorld();
        APlayerController* pc = PC();
        if (!pc || !pc->PlayerCameraManager) return;
        FVector start, dir;
        ViewRay(useMouse, &start, &dir);
        FVector end = start + FVector{ dir.X * 3000, dir.Y * 3000, dir.Z * 3000 };
        TArray<AActor*> ignore;
        if (Me()) ignore.Add(Me());
        FHitResult hit{};
        if (!UKismetSystemLibrary::LineTraceSingle(World(), start, end, ETraceTypeQuery::TraceTypeQuery1, true, ignore,
            EDrawDebugTrace::None, &hit, true, FLinearColor{}, FLinearColor{}, 0))
        { Notify("Nothing there", true); return; }
        UPrimitiveComponent* c = hit.Component.Get();
        if (!c) { Notify("Nothing there", true); return; }

        if (freeze)
        {
            // anchor it again where it is now
            if (!c->IsSimulatingPhysics(FName())) { Notify("Already anchored", true); return; }
            c->SetSimulatePhysics(false);
            Toast("Anchored");
            return;
        }
        if (c->IsSimulatingPhysics(FName()))
        {
            // already loose: give it a shove
            float f = (std::max)(g_set.unanchorPush, 800.0f);
            c->AddImpulse(FVector{ dir.X * f, dir.Y * f, dir.Z * f + 200 }, FName(), true);
            return;
        }
        g_unanchorFails = 0;
        UPrimitiveComponent* done = UnanchorHit(c, hit.Item);
        if (!done)
        {
            if (g_unanchorFails) Notify("No collision - can't unanchor this one", true);
            else if (Skippable(c) && !c->IsA(UInstancedStaticMeshComponent::StaticClass())) Notify("Can't unanchor that", true);
            else Notify("Too big - turn on 'Big objects' in the World tab", true);
            return;
        }
        if (g_set.unanchorPush > 0)
            done->AddImpulse(FVector{ dir.X * g_set.unanchorPush, dir.Y * g_set.unanchorPush, dir.Z * g_set.unanchorPush }, FName(), true);
        AActor* owner = c->GetOwner();
        Log("unanchored %s (%s)", done->GetName().c_str(), owner ? owner->GetName().c_str() : "?");
        Toast("Unanchored");
    });
}

void Game_UnanchorRange(float meters)
{
    RunOnGame([=] {
        CheckUnanchorWorld();
        ABP_PlayerCharacter_C* me = Me();
        if (!me) return;
        FVector at = me->K2_GetActorLocation();
        float r = meters * 100.0f;
        TArray<EObjectTypeQuery> types;
        types.Add(EObjectTypeQuery::ObjectTypeQuery1);   // WorldStatic
        types.Add(EObjectTypeQuery::ObjectTypeQuery2);   // WorldDynamic
        types.Add(EObjectTypeQuery::ObjectTypeQuery4);   // PhysicsBody
        TArray<AActor*> ignore;
        ignore.Add(me);
        TArray<UPrimitiveComponent*> comps;
        UKismetSystemLibrary::SphereOverlapComponents(World(), at, r, types, UStaticMeshComponent::StaticClass(), ignore, &comps);
        int n = 0;
        const int limit = 300;
        g_unanchorFails = 0;
        std::vector<UPrimitiveComponent*> list;
        for (int i = 0; i < comps.Num(); i++) list.push_back(comps[i]);
        for (UPrimitiveComponent* c : list)
        {
            if (n >= limit) break;
            if (!c || c->IsSimulatingPhysics(FName())) continue;
            if (c->IsA(UInstancedStaticMeshComponent::StaticClass()))
            {
                auto* ism = static_cast<UInstancedStaticMeshComponent*>(c);
                TArray<int32> ids = ism->GetInstancesOverlappingSphere(at, r, true);
                std::vector<int32> sorted;
                for (int k = 0; k < ids.Num(); k++) sorted.push_back(ids[k]);
                std::sort(sorted.rbegin(), sorted.rend());   // remove from the back so indices stay valid
                for (int32 id : sorted) { if (n >= limit) break; if (UnanchorInstance(ism, id)) n++; }
            }
            else if (UnanchorComp(c)) n++;
        }
        Log("unanchored %d objects in %.0f m (%d without collision)", n, meters, g_unanchorFails);
        Toast(n ? "Unanchored " + std::to_string(n) + " objects" : std::string("Nothing to unanchor here"));
    });
}

static void Restore(Unanchored& u)
{
    if (u.spawned)
    {
        if (Alive(u.spawned, u.spawnedIdx)) u.spawned->K2_DestroyActor();
        if (Alive(u.ism, u.ismIdx)) u.ism->AddInstance(u.xf, true);
        return;
    }
    if (!Alive(u.comp, u.compIdx)) return;
    UPrimitiveComponent* c = u.comp;
    c->SetSimulatePhysics(false);
    c->SetEnableGravity(true);
    if (u.parent && Alive(u.parent, u.parentIdx))
        c->K2_AttachToComponent(u.parent, u.socket, EAttachmentRule::KeepWorld, EAttachmentRule::KeepWorld, EAttachmentRule::KeepWorld, false);
    c->K2_SetWorldTransform(u.xf, false, nullptr, true);
    c->SetCollisionEnabled(u.collision);
    if (u.mobility != EComponentMobility::Movable) c->SetMobility(u.mobility);
}

void Game_UndoUnanchor(bool all)
{
    RunOnGame([=] {
        CheckUnanchorWorld();
        int n = 0;
        while (!g_unanchored.empty())
        {
            Restore(g_unanchored.back());
            g_unanchored.pop_back();
            n++;
            if (!all) break;
        }
        if (!n) Notify("Nothing to put back", true);
        else Toast(all ? "Put " + std::to_string(n) + " objects back" : std::string("Put back"));
    });
}

void Game_AnchorAll()
{
    RunOnGame([=] {
        CheckUnanchorWorld();
        int n = 0;
        for (auto& u : g_unanchored)
            if (Alive(u.comp, u.compIdx) && u.comp->IsSimulatingPhysics(FName())) { u.comp->SetSimulatePhysics(false); n++; }
        Toast("Froze " + std::to_string(n) + " objects where they are");
    });
}

void Game_SetUnanchorGravity(bool on)
{
    RunOnGame([=] {
        CheckUnanchorWorld();
        for (auto& u : g_unanchored)
            if (Alive(u.comp, u.compIdx) && u.comp->IsSimulatingPhysics(FName())) { u.comp->SetEnableGravity(on); u.comp->WakeRigidBody(FName()); }
    });
}

// ---------------- teleport: houses, pawn shops, auctions, garage + bring car ----------------
std::vector<LocEntry> g_locs;
std::mutex g_locsLock;
static UWorld* g_locWorld;
static ULONGLONG g_locScan;
static bool g_hasSaved;
static FVector g_savedPos;

static std::vector<AActor*> AllOf(UClass* cls)
{
    std::vector<AActor*> out;
    if (!cls) return out;
    TArray<AActor*> arr;
    UGameplayStatics::GetAllActorsOfClass(World(), cls, &arr);
    for (int i = 0; i < arr.Num(); i++) if (arr[i]) out.push_back(arr[i]);
    return out;
}

static std::string DistrictOf(ABP_District_C* d)
{
    if (!d) return "";
    auto it = g_districtNames.find(d);
    return it != g_districtNames.end() ? it->second : "";
}

static double Dist2(const FVector& a, const FVector& b) { double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z; return x * x + y * y + z * z; }

static void PlotName(ABP_PlayerEstate_Plot_C* p, std::string* out) { if (p->Data) *out = TextToString(p->Data->Name_0); }
static void SafePlotName(ABP_PlayerEstate_Plot_C* p, std::string* out) { GUARD("plot name", PlotName(p, out)); }

static void ScanLocations()
{
    std::vector<LocEntry> list;
    struct Site { FVector at; std::string district; };
    std::vector<Site> sites;

    for (AActor* a : AllOf(ABP_PlayerEstate_Plot_C::StaticClass()))
    {
        auto* p = static_cast<ABP_PlayerEstate_Plot_C*>(a);
        LocEntry e; e.cat = 0;
        SafePlotName(p, &e.name);
        if (e.name.empty()) e.name = "House";
        std::string d = DistrictOf(p->AssignedDistricted);
        if (!d.empty()) e.name += " (" + d + ")";
        if (p->IsBought) e.name += "  - yours";
        FVector at = p->SafeSpot_1 ? p->SafeSpot_1->K2_GetComponentLocation() : a->K2_GetActorLocation();
        e.x = at.X; e.y = at.Y; e.z = at.Z;
        list.push_back(e);
    }
    for (AActor* a : AllOf(ABP_ClassicAuctionSiteController_C::StaticClass()))
    {
        auto* s = static_cast<ABP_ClassicAuctionSiteController_C*>(a);
        std::string d = DistrictOf(s->AssignedDistrict);
        FVector at = s->SignUpController ? s->SignUpController->K2_GetComponentLocation() : a->K2_GetActorLocation();
        sites.push_back({ at, d });
        LocEntry e; e.cat = 2; e.name = "Auction" + (d.empty() ? std::string() : " (" + d + ")");
        e.x = at.X; e.y = at.Y; e.z = at.Z;
        list.push_back(e);
    }
    for (AActor* a : AllOf(ABP_BlindAuctionSiteController_C::StaticClass()))
    {
        auto* s = static_cast<ABP_BlindAuctionSiteController_C*>(a);
        std::string d = DistrictOf(s->AssignedDistrict);
        FVector at = s->SignUpController ? s->SignUpController->K2_GetComponentLocation() : a->K2_GetActorLocation();
        sites.push_back({ at, d });
        LocEntry e; e.cat = 2; e.name = "Blind auction" + (d.empty() ? std::string() : " (" + d + ")");
        e.x = at.X; e.y = at.Y; e.z = at.Z;
        list.push_back(e);
    }
    int shop = 0;
    for (AActor* a : AllOf(ABP_PawnShopBase_C::StaticClass()))
    {
        auto* s = static_cast<ABP_PawnShopBase_C*>(a);
        FVector at = s->NPCCustomerOutsideTarget ? s->NPCCustomerOutsideTarget->K2_GetComponentLocation() : a->K2_GetActorLocation();
        // name it after the closest auction site's district
        std::string d; double best = 1e18;
        for (auto& st : sites) { double q = Dist2(st.at, at); if (q < best && !st.district.empty()) { best = q; d = st.district; } }
        LocEntry e; e.cat = 1; e.name = "Pawn shop " + std::to_string(++shop) + (d.empty() ? std::string() : " (" + d + ")");
        e.x = at.X; e.y = at.Y; e.z = at.Z;
        list.push_back(e);
    }
    int garage = 0;
    for (AActor* a : AllOf(ABP_Garage_C::StaticClass()))
    {
        auto* g = static_cast<ABP_Garage_C*>(a);
        FVector at = g->PlayerMoveSpot ? g->PlayerMoveSpot->K2_GetComponentLocation() : a->K2_GetActorLocation();
        LocEntry e; e.cat = 3; e.name = "Garage" + (garage++ ? " " + std::to_string(garage) : std::string());
        e.x = at.X; e.y = at.Y; e.z = at.Z;
        list.push_back(e);
    }
    std::stable_sort(list.begin(), list.end(), [](const LocEntry& a, const LocEntry& b) { return a.cat != b.cat ? a.cat < b.cat : a.name < b.name; });
    std::lock_guard<std::mutex> lk(g_locsLock);
    g_locs = list;
}

// called from Tick
static void UpdateLocations()
{
    if (g_locWorld != World()) { g_locWorld = World(); g_locScan = 0; g_hasSaved = false; std::lock_guard<std::mutex> lk(g_locsLock); g_locs.clear(); }
    if (GetTickCount64() - g_locScan < 10000) return;
    g_locScan = GetTickCount64();
    ScanLocations();
}

static ABP_PlayerCarBase_C* MyCar()
{
    if (ABP_PlayerState_C* ps = PS())
        if (ps->_CurrentRegisteredPlayerCar) return ps->_CurrentRegisteredPlayerCar;
    ABP_PlayerCarBase_C* best = nullptr;
    double bd = 1e18;
    FVector me = Me() ? Me()->K2_GetActorLocation() : FVector{};
    for (AActor* a : AllOf(ABP_PlayerCarBase_C::StaticClass()))
    {
        double d = Dist2(a->K2_GetActorLocation(), me);
        if (d < bd) { bd = d; best = static_cast<ABP_PlayerCarBase_C*>(a); }
    }
    return best;
}

static void StopPhysics(AActor* a)
{
    USceneComponent* root = a->K2_GetRootComponent();
    if (root && root->IsA(UPrimitiveComponent::StaticClass()))
    {
        auto* p = static_cast<UPrimitiveComponent*>(root);
        p->SetPhysicsLinearVelocity(FVector{}, false, FName());
        p->SetPhysicsAngularVelocityInDegrees(FVector{}, false, FName());
    }
}

// Call a Blueprint event on the car by name (the SDK's generated car functions do not compile).
static ABP_PlayerCarBase_C* MyCar();
static void StopPhysics(AActor* a);
static void CallCarEvent(ABP_PlayerCarBase_C* car, const char* name, FVector at, FRotator rot)
{
    struct { FVector Location; FRotator Rotation; } parms{ at, rot };
    UFunction* f = car->Class->GetFunction("BP_PlayerCarBase_C", name);
    if (f) car->ProcessEvent(f, &parms);
    else Log("car: %s not found", name);
}

// Move the car. The car uses networked Chaos physics, which puts it back if only the actor is moved,
// so use the game's own load-from-save placement first, then its recover event.
static ABP_PlayerCarBase_C* g_carCheck;
static FVector g_carTarget;
static ULONGLONG g_carCheckAt;
static int g_carTries;

static FTransform CarTransform(FVector at, double yawDeg)
{
    FTransform xf{};
    double h = yawDeg * 3.14159265358979 / 360.0;
    xf.Rotation.X = 0; xf.Rotation.Y = 0; xf.Rotation.Z = std::sin(h); xf.Rotation.W = std::cos(h);
    xf.Translation = at;
    xf.Scale3D = FVector{ 1, 1, 1 };
    return xf;
}

static void PlaceCar(ABP_PlayerCarBase_C* car, FVector at, FRotator rot)
{
    FVector before = car->K2_GetActorLocation();
    car->Load(CarTransform(at, rot.Yaw));
    CallCarEvent(car, "Recover", at, rot);
    StopPhysics(car);
    FVector after = car->K2_GetActorLocation();
    Log("car %s: (%.0f %.0f %.0f) -> (%.0f %.0f %.0f)", car->GetName().c_str(), before.X, before.Y, before.Z, after.X, after.Y, after.Z);
    g_carCheck = car; g_carTarget = at; g_carCheckAt = GetTickCount64() + 1500; g_carTries = 0;
}

// a moment later: did the car stay? if not, try again with the other methods
static void CheckCar()
{
    if (!g_carCheck || GetTickCount64() < g_carCheckAt) return;
    ABP_PlayerCarBase_C* car = g_carCheck;
    if (car != MyCar()) { g_carCheck = nullptr; return; }
    FVector now = car->K2_GetActorLocation();
    bool ok = Dist2(now, g_carTarget) < 800.0 * 800.0;
    Log("car check %d: (%.0f %.0f %.0f) %s", g_carTries, now.X, now.Y, now.Z, ok ? "stayed" : "went back");
    if (ok || g_carTries >= 2) { g_carCheck = nullptr; if (!ok) Toast("The game moved the car back"); return; }
    g_carTries++;
    FRotator rot = car->K2_GetActorRotation();
    if (g_carTries == 1) CallCarEvent(car, "Move_Car_", g_carTarget, FRotator{ 0, rot.Yaw, 0 });
    else
    {
        USceneComponent* root = car->K2_GetRootComponent();
        if (root) root->K2_SetWorldLocation(g_carTarget, false, nullptr, true);
    }
    StopPhysics(car);
    g_carCheckAt = GetTickCount64() + 1500;
}

static bool Driving(ABP_PlayerCarBase_C* car) { return car && car->IsPlayerDriving && car->DrivingPlayerCharacter == Me(); }

static void MoveMeTo(FVector at)
{
    ABP_PlayerCharacter_C* me = Me();
    if (!me) return;
    ABP_PlayerCarBase_C* car = MyCar();
    if (Driving(car))
    {
        // in the car: take the car along
        FRotator r = car->K2_GetActorRotation();
        PlaceCar(car, at + FVector{ 0, 0, 150 }, FRotator{ 0, r.Yaw, 0 });
        return;
    }
    me->K2_SetActorLocation(at + FVector{ 0, 0, 100 }, false, nullptr, true);
    if (me->CharacterMovement) me->CharacterMovement->Velocity = FVector{};
}

void Game_TeleportTo(double x, double y, double z)
{
    RunOnGame([=] { MoveMeTo(FVector{ x, y, z }); Toast("Teleported"); });
}

void Game_BringCar()
{
    RunOnGame([] {
        ABP_PlayerCharacter_C* me = Me();
        ABP_PlayerCarBase_C* car = MyCar();
        if (!me || !car) { Log("bring car: no car (registered %d)", PS() && PS()->_CurrentRegisteredPlayerCar ? 1 : 0); Toast("No car found"); return; }
        if (Driving(car)) { Toast("You are already in your car"); return; }
        FRotator r = me->K2_GetActorRotation();
        double yaw = r.Yaw * 3.14159265358979 / 180.0;
        FVector at = me->K2_GetActorLocation() + FVector{ std::cos(yaw) * 600, std::sin(yaw) * 600, 120 };
        PlaceCar(car, at, FRotator{ 0, r.Yaw + 90, 0 });
        Toast("Car is here");
    });
}

void Game_TeleportToCar()
{
    RunOnGame([] {
        ABP_PlayerCarBase_C* car = MyCar();
        if (!car) { Toast("No car found"); return; }
        if (Driving(car)) { Toast("You are already in your car"); return; }
        FRotator r = car->K2_GetActorRotation();
        double yaw = (r.Yaw + 90) * 3.14159265358979 / 180.0;   // next to the driver door
        MoveMeTo(car->K2_GetActorLocation() + FVector{ std::cos(yaw) * -300, std::sin(yaw) * -300, 0 });
        Toast("Teleported to your car");
    });
}

void Game_SavePosition()
{
    RunOnGame([] {
        ABP_PlayerCarBase_C* car = MyCar();
        AActor* who = Driving(car) ? (AActor*)car : (AActor*)Me();
        if (!who) return;
        g_savedPos = who->K2_GetActorLocation();
        g_hasSaved = true;
        Toast("Position saved");
    });
}

void Game_LoadPosition()
{
    RunOnGame([] {
        if (!g_hasSaved) { Toast("No position saved yet"); return; }
        MoveMeTo(g_savedPos - FVector{ 0, 0, 90 });
        Toast("Back at saved position");
    });
}

static void SafeLocations() { GUARD("locations", UpdateLocations()); }

static void SafeCheckCar() { GUARD("car check", CheckCar()); }
