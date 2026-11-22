// Game side: everything that touches Unreal objects runs here, on the game thread.
#include "cheats.h"
#include <cmath>
#include <map>
#include "MinHook.h"

#include "SDK/Basic.hpp"
#include "SDK/CoreUObject_classes.hpp"
#include "SDK/Engine_classes.hpp"
#include "SDK/GASCore_classes.hpp"
#include "SDK/TreesMustGo_classes.hpp"
#include "SDK/TreesChopper_classes.hpp"
#include "SDK/BP_Human_classes.hpp"

using namespace SDK;

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

static void Result(const std::string& s)
{
    Log("%s", s.c_str());
    Notify(s);
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

static AHumanCharacter* Me()
{
    APlayerController* pc = PC();
    if (!pc || !pc->Pawn || !pc->Pawn->IsA(AHumanCharacter::StaticClass())) return nullptr;
    return static_cast<AHumanCharacter*>(pc->Pawn);
}

// Co-op game: the cheats only run when you are alone in the session.
static bool Allowed()
{
    UWorld* w = World();
    return w && w->GameState && w->GameState->PlayerArray.Num() <= 1;
}

static ULogInventoryComponent* Logs(AHumanCharacter* me)
{
    if (me && me->IsA(ABP_Human_C::StaticClass())) return static_cast<ABP_Human_C*>(me)->LogInventory;
    return nullptr;
}

template<typename T> static T* Subsystem(APlayerController* pc)
{
    return static_cast<T*>(USubsystemBlueprintLibrary::GetWorldSubsystem(pc, T::StaticClass()));
}

static std::string Text(const FText& t) { return UKismetTextLibrary::Conv_TextToString(t).ToString(); }

// ---------------- GAS attributes ----------------
// FGameplayAttributeData: vtable, BaseValue (0x8), CurrentValue (0xC)
static float GetAttr(void* set, int off) { return set ? *(float*)((uint8*)set + off + 0xC) : 0.0f; }
static void SetAttr(void* set, int off, float v)
{
    if (!set) return;
    *(float*)((uint8*)set + off + 0x8) = v;
    *(float*)((uint8*)set + off + 0xC) = v;
}

// the attribute sets live on the player state, the pawn or (shared money) the game state
struct Sets
{
    UWorld* world = nullptr;
    void* owner = nullptr;
    ULONGLONG scanned = 0;
    UCharacterAttributeSet* character = nullptr;
    UTreeChopperAttributeSet* chopper = nullptr;
    std::vector<UMoneyAttributeSet*> money;
};
static Sets g_sets;

static void FindSets(APlayerController* pc, AHumanCharacter* me)
{
    UWorld* w = World();
    if (g_sets.world != w || g_sets.owner != me) { g_sets = Sets{}; g_sets.world = w; g_sets.owner = me; }
    bool complete = g_sets.character && g_sets.chopper && !g_sets.money.empty();
    if (complete || GetTickCount64() - g_sets.scanned < 3000) return;
    g_sets.scanned = GetTickCount64();
    UObject* ps = pc->PlayerState;
    UObject* gs = w ? w->GameState : nullptr;
    auto mine = [&](UObject* o) { return o->Outer && (o->Outer == ps || o->Outer == me || o->Outer == gs || o->Outer == pc); };
    UClass* cc = UCharacterAttributeSet::StaticClass();
    UClass* tc = UTreeChopperAttributeSet::StaticClass();
    UClass* mc = UMoneyAttributeSet::StaticClass();
    g_sets.money.clear();
    for (int i = 0; i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (!o || o->IsDefaultObject() || !mine(o)) continue;
        if (cc && o->IsA(cc) && (!g_sets.character || o->Outer == me)) g_sets.character = static_cast<UCharacterAttributeSet*>(o);
        else if (tc && o->IsA(tc)) g_sets.chopper = static_cast<UTreeChopperAttributeSet*>(o);
        else if (mc && o->IsA(mc)) g_sets.money.push_back(static_cast<UMoneyAttributeSet*>(o));
    }
    Log("attribute sets: character %p, chopper %p, money x%d", g_sets.character, g_sets.chopper, (int)g_sets.money.size());
}

// Offsets inside the attribute sets (from the SDK).
static const int kHealth = 0x60, kMaxHealth = 0x70, kStamina = 0x90, kMaxStamina = 0xA0;
static const int kTrees = 0x30, kMaxTrees = 0x40, kWeight = 0x50, kMaxWeight = 0x60, kChopSpeed = 0x90, kMaxChopSpeed = 0xA0;
static const int kMoney = 0x30, kMaxMoney = 0x40;

// The money the game is using right now (with shared co-op money it is on the game state).
static UMoneyAttributeSet* MoneySet(APlayerController* pc)
{
    if (g_sets.money.empty()) return nullptr;
    if (g_sets.money.size() == 1) return g_sets.money[0];
    float want = 0;
    if (auto* up = Subsystem<UTreesUpgradeSubsystem>(pc)) want = up->GetAvailableMoney(pc);
    for (auto* m : g_sets.money) if (std::fabs(GetAttr(m, kMoney) - want) < 0.5f) return m;
    return g_sets.money[0];
}

// ---------------- helpers that remember original values ----------------
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

// tree types (data assets): one-hit and log value change them while the cheat is on
struct TreeDef { UTreeDefinitionAsset* def; float health, stump, buck, value; std::string name; };
static std::vector<TreeDef> g_trees;
static ULONGLONG g_treesScanned;
static bool g_treesChanged;

static void FindTreeTypes()
{
    if (!g_trees.empty() || GetTickCount64() - g_treesScanned < 5000) return;
    g_treesScanned = GetTickCount64();
    UClass* cls = UTreeDefinitionAsset::StaticClass();
    for (int i = 0; cls && i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (!o || o->IsDefaultObject() || !o->IsA(cls)) continue;
        auto* d = static_cast<UTreeDefinitionAsset*>(o);
        std::string n = Text(d->TreeName);
        if (n.empty()) n = d->GetName();
        g_trees.push_back({ d, d->MaxHealth, d->StumpMaxHealth, d->LogBuckHealth, d->LogBaseValue, n });
    }
    Log("tree types: %d", (int)g_trees.size());
}

static void ApplyTreeCheats(bool allowed)
{
    FindTreeTypes();
    bool oneHit = allowed && g_set.oneHit;
    float value = allowed ? g_set.logValueMul : 1.0f;
    bool want = oneHit || value != 1.0f;
    if (!want && !g_treesChanged) return;
    for (auto& t : g_trees)
    {
        t.def->MaxHealth = oneHit ? 1.0f : t.health;
        t.def->StumpMaxHealth = oneHit ? 1.0f : t.stump;
        t.def->LogBuckHealth = oneHit ? 1.0f : t.buck;
        t.def->LogBaseValue = t.value * value;
    }
    g_treesChanged = want;

    // trees that are already being chopped keep their own health
    if (!oneHit) return;
    ATreeManager* tm = ATreeManager::GetTreeManager(World());
    if (!tm) return;
    for (int i = 0; i < tm->ActiveProxies.NumAllocated(); i++)
    {
        if (!tm->ActiveProxies.IsValidIndex(i)) continue;
        ATreeProxy* p = tm->ActiveProxies[i].Value();
        if (p && p->HealthAttributeSet && GetAttr(p->HealthAttributeSet, kHealth) > 1.0f) SetAttr(p->HealthAttributeSet, kHealth, 1.0f);
    }
}

// sawmills have their own base price for a log
static std::map<void*, float> g_sawmillValue;
static void ApplySawmillValue(bool allowed, const std::vector<AActor*>& sawmills)
{
    float mul = allowed ? g_set.logValueMul : 1.0f;
    static int kValueOff = -1;
    if (kValueOff < 0)
    {
        // BP_Sawmill_C::Log_Base_Value
        UClass* c = UObject::FindObject<UClass>("BlueprintGeneratedClass BP_Sawmill.BP_Sawmill_C");
        kValueOff = c ? 0x3F8 : 0;
    }
    if (!kValueOff) return;
    for (AActor* a : sawmills)
    {
        float* f = (float*)((uint8*)a + kValueOff);
        auto it = g_sawmillValue.find(a);
        if (it == g_sawmillValue.end()) { if (mul == 1.0f) continue; it = g_sawmillValue.emplace(a, *f).first; }
        *f = it->second * mul;
        if (mul == 1.0f) g_sawmillValue.erase(it);
    }
}

// ---------------- places ----------------
struct Place { std::string name; FVector pos; AActor* actor; };
static std::vector<Place> g_places;
static std::vector<AActor*> g_sawmills;
static ULONGLONG g_placesScanned;
static UWorld* g_placesWorld;

static void ScanPlaces()
{
    UWorld* w = World();
    if (g_placesWorld == w && GetTickCount64() - g_placesScanned < 4000) return;
    g_placesWorld = w; g_placesScanned = GetTickCount64();
    struct K { const char* cls; const char* name; };
    static const K kinds[] = {
        { "BlueprintGeneratedClass BP_Sawmill.BP_Sawmill_C", "Sawmill" },
        { "BlueprintGeneratedClass BP_Trolley.BP_Trolley_C", "Trolley" },
        { "BlueprintGeneratedClass BP_Trolley_Trailer.BP_Trolley_Trailer_C", "Trolley trailer" },
        { "BlueprintGeneratedClass BP_Bench.BP_Bench_C", "Bench" },
        { "BlueprintGeneratedClass BP_Notebook.BP_Notebook_C", "Notebook" },
        { "BlueprintGeneratedClass BP_Husky.BP_Husky_C", "Your dog" },
    };
    std::vector<UClass*> cls;
    for (auto& k : kinds) cls.push_back(UObject::FindObject<UClass>(k.cls));
    std::vector<Place> found;
    std::vector<AActor*> saws;
    int count[16] = {};
    for (int i = 0; i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (!o || o->IsDefaultObject() || !o->Outer || !o->Outer->IsA(ULevel::StaticClass())) continue;
        for (size_t k = 0; k < cls.size(); k++)
        {
            if (!cls[k] || !o->IsA(cls[k])) continue;
            AActor* a = static_cast<AActor*>(o);
            std::string n = kinds[k].name;
            if (++count[k] > 1) n += " " + std::to_string(count[k]);
            found.push_back({ n, a->K2_GetActorLocation(), a });
            if (k == 0) saws.push_back(a);
            break;
        }
    }
    g_places = found;
    g_sawmills = saws;
}

// ---------------- per-frame cheats ----------------
static void* g_who;
static Scaled<float> g_walk, g_jump;
static Scaled<float> g_chop, g_maxChop;
static bool g_flying;
static float g_flySpeedOrig;
static bool g_menuInputLocked;
static FVector g_slots[3];
static bool g_slotSet[3];

static void ResetFor(AHumanCharacter* me)
{
    g_who = me;
    g_walk.Reset(); g_jump.Reset(); g_chop.Reset(); g_maxChop.Reset();
    g_flying = false;
    for (bool& s : g_slotSet) s = false;
    Log("player found");
}

static float g_capOrig[2] = { -1, -1 };

static void ApplyCheats(APlayerController* pc, AHumanCharacter* me, bool allowed, float dt)
{
    if (g_who != me) ResetFor(me);
    FindSets(pc, me);
    UCharacterMovementComponent* cm = me->CharacterMovement;

    if (cm)
    {
        g_walk.Apply(cm->MaxWalkSpeed, allowed ? g_set.speedMul : 1.0f);
        g_jump.Apply(cm->JumpZVelocity, allowed ? g_set.jumpMul : 1.0f);
    }

    if (auto* c = g_sets.character)
    {
        if (allowed && g_set.god && GetAttr(c, kMaxHealth) > 0) SetAttr(c, kHealth, GetAttr(c, kMaxHealth));
        if (allowed && g_set.infStamina && GetAttr(c, kMaxStamina) > 0) SetAttr(c, kStamina, GetAttr(c, kMaxStamina));
    }
    if (auto* t = g_sets.chopper)
    {
        float& cur = *(float*)((uint8*)t + kChopSpeed + 0xC);
        float& max = *(float*)((uint8*)t + kMaxChopSpeed + 0xC);
        float mul = allowed ? g_set.chopSpeedMul : 1.0f;
        g_chop.Apply(cur, mul);
        g_maxChop.Apply(max, mul);
        // bigger log capacity: pinned while on, given back when off
        bool big = allowed && g_set.bigCapacity;
        float* caps[2] = { (float*)((uint8*)t + kMaxTrees + 0xC), (float*)((uint8*)t + kMaxWeight + 0xC) };
        for (int i = 0; i < 2; i++)
        {
            if (big) { if (g_capOrig[i] < 0) g_capOrig[i] = *caps[i]; *caps[i] = 9999.0f; }
            else if (g_capOrig[i] >= 0) { *caps[i] = g_capOrig[i]; g_capOrig[i] = -1; }
        }
    }

    ApplyTreeCheats(allowed);
    ScanPlaces();
    ApplySawmillValue(allowed, g_sawmills);

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

template<typename P> static void CallStatic(const char* fn, P& parms)
{
    static UFunction* f = nullptr;
    if (!f) f = UObject::FindObject<UFunction>(fn);
    if (!f) return;
    UClass* cls = static_cast<UClass*>(f->Outer);
    if (cls && cls->ClassDefaultObject) cls->ClassDefaultObject->ProcessEvent(f, &parms);
}

struct SetInputGameAndUI { APlayerController* PlayerController; UWidget* InWidgetToFocus; uint8 InMouseLockMode; bool bHideCursorDuringCapture; bool bFlushInput; uint8 pad[5]; };
struct SetInputGameOnly { APlayerController* PlayerController; bool bFlushInput; uint8 pad[7]; };

static void SetMenuInput(APlayerController* pc, bool open)
{
    if (open == g_menuInputLocked) return;
    g_menuInputLocked = open;
    pc->bShowMouseCursor = open;
    pc->SetIgnoreLookInput(open);
    pc->SetIgnoreMoveInput(open);
    if (open)
    {
        SetInputGameAndUI p{}; p.PlayerController = pc;
        CallStatic("Function UMG.WidgetBlueprintLibrary.SetInputMode_GameAndUIEx", p);
    }
    else
    {
        SetInputGameOnly p{}; p.PlayerController = pc;
        CallStatic("Function UMG.WidgetBlueprintLibrary.SetInputMode_GameOnly", p);
    }
}

// ---------------- what the menu shows ----------------
static std::vector<UTreesUpgradeDefinitionAsset*> g_upgrades;
static ULONGLONG g_upgradesScanned;

static void FindUpgrades()
{
    if (!g_upgrades.empty() || GetTickCount64() - g_upgradesScanned < 5000) return;
    g_upgradesScanned = GetTickCount64();
    UClass* cls = UTreesUpgradeDefinitionAsset::StaticClass();
    for (int i = 0; cls && i < UObject::GObjects->Num(); i++)
    {
        UObject* o = UObject::GObjects->GetByIndex(i);
        if (o && !o->IsDefaultObject() && o->IsA(cls)) g_upgrades.push_back(static_cast<UTreesUpgradeDefinitionAsset*>(o));
    }
    Log("upgrades: %d", (int)g_upgrades.size());
}

static void FillUpgradeView(APlayerController* pc, std::vector<UpgradeView>& out)
{
    FindUpgrades();
    auto* up = Subsystem<UTreesUpgradeSubsystem>(pc);
    if (!up) return;
    for (auto* d : g_upgrades)
    {
        UpgradeView u;
        u.name = Text(d->UpgradeName);
        if (u.name.empty()) u.name = d->GetName();
        u.oneTime = d->bIsOneTimeAction;
        if (u.oneTime)
        {
            u.max = 1;
            u.done = up->IsActionPurchased(pc, d);
            u.level = u.done ? 1 : 0;
            u.cost = up->GetActionCost(pc, d);
            u.canBuy = !u.done;
        }
        else
        {
            u.max = d->Steps.Num();
            u.level = up->GetCurrentUpgradeLevel(pc, d);
            u.done = up->IsMaxLevel(pc, d);
            u.cost = u.done ? 0 : up->GetNextStepCost(pc, d);
            u.canBuy = !u.done && up->CanPurchaseNextStep(pc, d);
        }
        out.push_back(u);
    }
}

// ---------------- tick ----------------
static void RunJobInner(std::function<void()>* j) { (*j)(); }
static void RunJob(std::function<void()>& j) { GUARD("action", RunJobInner(&j)); }
static void SafeCheats(APlayerController* pc, AHumanCharacter* me, bool allowed, float dt) { GUARD("cheats", ApplyCheats(pc, me, allowed, dt)); }
static void SafeUpgrades(APlayerController* pc, std::vector<UpgradeView>* out) { GUARD("upgrades", FillUpgradeView(pc, *out)); }

static std::vector<UpgradeView> g_upgradeCache;
static ULONGLONG g_upgradeViewAt;

static void Tick(float dt)
{
    APlayerController* pc = PC();
    AHumanCharacter* me = Me();
    bool allowed = Allowed();

    if (pc && g_menuInputLocked != g_menuOpen) SetMenuInput(pc, g_menuOpen);
    if (!pc) g_menuInputLocked = false;

    {
        std::vector<std::function<void()>> jobs;
        { std::lock_guard<std::mutex> lk(g_qLock); jobs.swap(g_queue); }
        if (allowed && me) for (auto& j : jobs) RunJob(j);
    }
    if (me && pc) SafeCheats(pc, me, allowed, dt);
    else { g_sets = Sets{}; g_trees.clear(); g_upgrades.clear(); g_places.clear(); g_sawmills.clear(); g_sawmillValue.clear(); g_treesChanged = false; g_capOrig[0] = g_capOrig[1] = -1; }

    // the upgrade list asks the game a lot - only twice a second and only while the menu is open
    if (me && pc && g_menuOpen && GetTickCount64() - g_upgradeViewAt > 500)
    {
        g_upgradeViewAt = GetTickCount64();
        std::vector<UpgradeView> u;
        SafeUpgrades(pc, &u);
        g_upgradeCache = u;
    }

    GameView v{};
    v.inGame = me != nullptr;
    v.allowed = allowed;
    if (me && pc)
    {
        if (auto* m = MoneySet(pc)) v.money = GetAttr(m, kMoney);
        if (auto* c = g_sets.character) { v.health = GetAttr(c, kHealth); v.maxHealth = GetAttr(c, kMaxHealth); v.stamina = GetAttr(c, kStamina); v.maxStamina = GetAttr(c, kMaxStamina); }
        if (auto* t = g_sets.chopper) { v.logs = GetAttr(t, kTrees); v.maxLogs = GetAttr(t, kMaxTrees); v.weight = GetAttr(t, kWeight); v.maxWeight = GetAttr(t, kMaxWeight); }
        if (auto* li = Logs(me)) v.carried = li->GetLogCount();
        v.upgrades = g_upgradeCache;
        for (auto& t : g_trees) v.treeTypes.push_back(t.name);
        FVector at = me->K2_GetActorLocation();
        for (auto& p : g_places)
        {
            double dx = p.pos.X - at.X, dy = p.pos.Y - at.Y, dz = p.pos.Z - at.Z;
            v.places.push_back({ p.name, std::sqrt(dx * dx + dy * dy + dz * dz) / 100.0 });
        }
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

// ---------------- actions (queued from the menu) ----------------
// The game keeps money in more than one attribute set (player state and game state) and the UI reads
// whichever one it likes, so every set gets the same new value.
static void AddMoneyNow(APlayerController* pc, double amount)
{
    UMoneyAttributeSet* cur = MoneySet(pc);
    if (!cur) { Result("Money not found yet - try again in a moment"); return; }
    double t = GetAttr(cur, kMoney) + amount;
    if (t < 0) t = 0;
    if (t > 2.0e9) t = 2.0e9;
    for (UMoneyAttributeSet* m : g_sets.money)
    {
        SetAttr(m, kMoney, (float)t);
        float maxMoney = GetAttr(m, kMaxMoney);
        if (maxMoney > 0 && t > maxMoney) SetAttr(m, kMaxMoney, (float)t);
    }
}

void Game_AddMoney(double a)
{
    RunOnGame([=] {
        APlayerController* pc = PC();
        if (!pc) return;
        AddMoneyNow(pc, a);
        if (UMoneyAttributeSet* m = MoneySet(pc)) Log("money -> %.0f", GetAttr(m, kMoney));
    });
}

// Buys one step for free: the cost is added first, then the game's own purchase takes it again.
static bool BuyOne(APlayerController* pc, UTreesUpgradeSubsystem* up, UTreesUpgradeDefinitionAsset* d)
{
    if (d->bIsOneTimeAction)
    {
        if (up->IsActionPurchased(pc, d)) return false;
        float cost = up->GetActionCost(pc, d);
        AddMoneyNow(pc, cost);
        up->PurchaseAction(pc, d);
        bool ok = up->IsActionPurchased(pc, d);
        if (!ok) AddMoneyNow(pc, -cost);
        return ok;
    }
    if (up->IsMaxLevel(pc, d) || !up->CanPurchaseNextStep(pc, d)) return false;
    int before = up->GetCurrentUpgradeLevel(pc, d);
    float cost = up->GetNextStepCost(pc, d);
    AddMoneyNow(pc, cost);
    up->PurchaseUpgrade(pc, d);
    bool ok = up->GetCurrentUpgradeLevel(pc, d) > before;
    if (!ok) AddMoneyNow(pc, -cost);
    return ok;
}

void Game_BuyUpgrade(int index)
{
    RunOnGame([=] {
        APlayerController* pc = PC();
        auto* up = pc ? Subsystem<UTreesUpgradeSubsystem>(pc) : nullptr;
        if (!up) { Result("The upgrades are not loaded yet"); return; }
        FindUpgrades();
        if (index >= 0)
        {
            if (index >= (int)g_upgrades.size()) return;
            bool ok = BuyOne(pc, up, g_upgrades[index]);
            Result(ok ? "Upgrade bought" : "That upgrade can not be bought right now");
        }
        else
        {
            // upgrades can unlock other upgrades, so go round until nothing changes
            int bought = 0;
            for (int pass = 0; pass < 40; pass++)
            {
                int now = 0;
                for (auto* d : g_upgrades) if (BuyOne(pc, up, d)) now++;
                bought += now;
                if (!now) break;
            }
            Result(bought ? "Bought " + std::to_string(bought) + " upgrade steps" : "Nothing left to buy right now");
        }
        g_upgradeViewAt = 0;
    });
}

void Game_AddLogs(int type, int count)
{
    RunOnGame([=] {
        AHumanCharacter* me = Me();
        ULogInventoryComponent* li = Logs(me);
        FindTreeTypes();
        if (!li || g_trees.empty()) { Result("Your log bag was not found"); return; }
        int before = li->GetLogCount();
        for (int i = 0; i < count; i++)
        {
            const TreeDef& t = g_trees[type >= 0 && type < (int)g_trees.size() ? type : i % g_trees.size()];
            li->AddLog(t.def, me);
        }
        Result("+" + std::to_string(li->GetLogCount() - before) + " logs - take them to the sawmill");
    });
}

// Real log pieces in the world, in rows in front of you - pick them up or let them roll to the saw.
void Game_SpawnLogs(int type, int count)
{
    RunOnGame([=] {
        AHumanCharacter* me = Me();
        FindTreeTypes();
        if (!me || g_trees.empty()) { Result("No tree types loaded yet"); return; }
        FVector at = me->K2_GetActorLocation();
        FVector fwd = me->GetActorForwardVector(), right = me->GetActorRightVector();
        FRotator rot = me->K2_GetActorRotation();
        rot.Yaw += 90.0;   // lie across your view
        int made = 0;
        for (int i = 0; i < count; i++)
        {
            const TreeDef& t = g_trees[type >= 0 && type < (int)g_trees.size() ? type : i % g_trees.size()];
            UClass* cls = t.def ? t.def->LogPieceClass.Get() : nullptr;
            if (!cls) cls = ATreeLogPiece::StaticClass();
            int row = i / 5, col = i % 5;
            FVector p = at + fwd * (250.0 + row * 90.0) + right * ((col - 2) * 70.0) + FVector{ 0, 0, 80.0 + row * 10.0 };
            FTransform tr = UKismetMathLibrary::MakeTransform(p, rot, FVector{ 1, 1, 1 });
            AActor* a = UGameplayStatics::BeginDeferredActorSpawnFromClass(me, cls, tr, ESpawnActorCollisionHandlingMethod::AlwaysSpawn, nullptr, ESpawnActorScaleMethod::MultiplyWithRoot);
            if (!a) continue;
            if (a->IsA(ATreeLogPiece::StaticClass())) static_cast<ATreeLogPiece*>(a)->InitFromDefinition(t.def);
            UGameplayStatics::FinishSpawningActor(a, tr, ESpawnActorScaleMethod::MultiplyWithRoot);
            made++;
        }
        Result(made ? std::to_string(made) + " logs in front of you" : "The game would not spawn logs here");
    });
}

void Game_ClearLogs() { RunOnGame([] { if (auto* li = Logs(Me())) { li->RemoveAllLogs(); Result("Log bag emptied"); } }); }

void Game_RegrowTrees()
{
    RunOnGame([] {
        ATreeManager* tm = ATreeManager::GetTreeManager(World());
        if (!tm) { Result("The trees are not loaded yet"); return; }
        tm->RegrowAllTrees(5.0f, 1.0f, true, false);
        Result("Every tree grows back");
    });
}

void Game_UnlockSkins()
{
    RunOnGame([] {
        APlayerController* pc = PC();
        UClass* cls = UMosquitoGangCheatManager::StaticClass();
        if (!pc || !cls) return;
        auto* cm = static_cast<UMosquitoGangCheatManager*>(UGameplayStatics::SpawnObject(cls, pc));
        if (!cm) { Result("Could not unlock the skins"); return; }
        cm->Cheat_UnlockAllSkins();
        Result("Skins unlocked (if the game allows it in this version)");
    });
}

void Game_Heal()
{
    RunOnGame([] {
        if (auto* c = g_sets.character) { SetAttr(c, kHealth, GetAttr(c, kMaxHealth)); SetAttr(c, kStamina, GetAttr(c, kMaxStamina)); }
    });
}

void Game_Teleport(int i)
{
    RunOnGame([=] {
        AHumanCharacter* me = Me();
        if (!me || i < 0 || i >= (int)g_places.size()) return;
        AActor* a = g_places[i].actor;
        FVector p = a ? a->K2_GetActorLocation() : g_places[i].pos;
        me->K2_SetActorLocation(p + FVector{ 150, 0, 120 }, false, nullptr, true);
        Result("Teleported to " + g_places[i].name);
    });
}

void Game_SavePos(int s) { RunOnGame([=] { if (AHumanCharacter* me = Me()) if (s >= 0 && s < 3) { g_slots[s] = me->K2_GetActorLocation(); g_slotSet[s] = true; } }); }
void Game_LoadPos(int s) { RunOnGame([=] { if (AHumanCharacter* me = Me()) if (s >= 0 && s < 3 && g_slotSet[s]) me->K2_SetActorLocation(g_slots[s], false, nullptr, true); }); }
