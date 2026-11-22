// The menu for Moon Sweepers (look and widgets come from shared/unreal).
#include "cheats.h"
#include <fstream>
#include <map>
#include <cmath>

Settings g_set;
GameView g_view;
std::mutex g_viewLock;
bool g_menuOpen;

// ---------------- settings file ----------------
static std::string IniPath() { return ModDir() + ED_FILE_BASE ".ini"; }

void Menu_LoadSettings()
{
    std::ifstream f(IniPath());
    std::map<std::string, std::string> kv;
    std::string line;
    while (std::getline(f, line))
    {
        auto eq = line.find('=');
        if (eq != std::string::npos) kv[line.substr(0, eq)] = line.substr(eq + 1);
    }
    auto I = [&](const char* k, int d) { auto it = kv.find(k); return it == kv.end() ? d : atoi(it->second.c_str()); };
    auto F = [&](const char* k, float d) { auto it = kv.find(k); return it == kv.end() ? d : (float)atof(it->second.c_str()); };
    auto B = [&](const char* k) { return I(k, 0) != 0; };
    if (kv.count("Theme")) g_set.theme = kv["Theme"];
    g_set.keyMenu = I("KeyMenu", VK_F1);
    g_set.keyMoney = I("KeyMoney", VK_F2);
    g_set.keyFly = I("KeyFly", VK_F3);
    g_set.keyFill = I("KeyFill", VK_F4);
    g_set.moneyIndex = I("MoneyIndex", 1);
    g_set.saveSettings = B("SaveSettings");
    g_notifySound = I("NotifySound", 1) != 0;
    if (g_set.saveSettings)
    {
        g_set.speedMul = F("Speed", 1); g_set.jumpMul = F("Jump", 1); g_set.flySpeed = F("FlySpeed", 1);
        g_set.infJetpack = B("InfiniteJetpack");
        g_set.capacityMul = F("Capacity", 1); g_set.autoFill = B("AutoFill"); g_set.payoutMul = F("Payout", 1);
        g_set.freeUpgrades = B("FreeUpgrades");
    }
}

void Menu_SaveSettings()
{
    std::ofstream f(IniPath());
    f << "Theme=" << g_themeName << "\nKeyMenu=" << g_set.keyMenu << "\nKeyMoney=" << g_set.keyMoney << "\nKeyFly=" << g_set.keyFly
      << "\nKeyFill=" << g_set.keyFill << "\nMoneyIndex=" << g_set.moneyIndex << "\nSaveSettings=" << (int)g_set.saveSettings
      << "\nNotifySound=" << (int)g_notifySound << "\n";
    if (g_set.saveSettings)
        f << "Speed=" << g_set.speedMul << "\nJump=" << g_set.jumpMul << "\nFlySpeed=" << g_set.flySpeed
          << "\nInfiniteJetpack=" << (int)g_set.infJetpack << "\nCapacity=" << g_set.capacityMul << "\nAutoFill=" << (int)g_set.autoFill
          << "\nPayout=" << g_set.payoutMul << "\nFreeUpgrades=" << (int)g_set.freeUpgrades << "\n";
}

// ---------------- hotkeys ----------------
static const int kMoney[] = { 1000, 10000, 100000, 1000000 };

void Menu_HandleHotkeys()
{
    ReadKeys();
    if (HandleRebind()) return;
    if (Pressed(g_set.keyMenu)) g_menuOpen = !g_menuOpen;
    bool typing = ImGui::GetIO().WantTextInput;
    if (!typing && g_view.inGame && g_view.allowed)
    {
        if (Pressed(g_set.keyMoney)) { int a = kMoney[g_set.moneyIndex & 3]; Game_AddMoney(a); Toast("+" + Money(a)); }
        if (Pressed(g_set.keyFly)) { g_set.fly = !g_set.fly; Toast(g_set.fly ? "Fly mode ON" : "Fly mode OFF"); }
        if (Pressed(g_set.keyFill)) { Game_FillDust(); Toast("Dust bag filled"); }
    }
}

// ---------------- tabs ----------------
static int g_tab;
static const char* kTabs[] = { "Player", "Money", "Dust", "Upgrades", "Spawn", "Teleport", "Settings" };
static const int kTabCount = 7, kSettingsTab = 6;


static void TabPlayer(const GameView&)
{
    if (BeginCard("Movement"))
    {
        if (Slider("Walk speed", &g_set.speedMul, 1, 5, "%.1fx")) Dirty();
        if (Slider("Jump height", &g_set.jumpMul, 1, 4, "%.1fx")) Dirty();
        if (Btn("Reset movement", -1)) { g_set.speedMul = 1; g_set.jumpMul = 1; Dirty(); }
    }
    EndCard();
    if (BeginCard("Fly mode"))
    {
        std::string hint = "(" + KeyName(g_set.keyFly) + ")";
        Toggle("Fly / noclip", &g_set.fly, hint.c_str());
        if (Slider("Fly speed", &g_set.flySpeed, 0.5f, 5, "%.1fx")) Dirty();
        Note("The game's own fly mode - you fly through rocks, buildings and the ground.");
    }
    EndCard();
    if (BeginCard("Jetpack"))
    {
        if (Toggle("Jetpack boosts", &g_set.infJetpack, "99 boosts in the air")) Dirty();
    }
    EndCard();
}

static void TabMoney(const GameView& v)
{
    if (BeginCard("Money"))
    {
        ImGui::Text("Wallet"); ImGui::SameLine();
        ImGui::TextColored(T->b, "%s.%02d", Money((long long)v.money).c_str(), (int)(fmod(fabs(v.money), 1.0) * 100 + 0.5) % 100);
        const char* labels[] = { "$1,000", "$10,000", "$100,000", "$1,000,000" };
        float w = Col(4);
        for (int i = 0; i < 4; i++) { if (i) ImGui::SameLine(); if (Chip(labels[i], g_set.moneyIndex == i, w)) { g_set.moneyIndex = i; Dirty(); } }
        w = Col(2);
        int a = kMoney[g_set.moneyIndex & 3];
        if (Btn(("Add " + Money(a)).c_str(), w, true)) Game_AddMoney(a);
        ImGui::SameLine();
        if (Danger(("Remove " + Money(a)).c_str(), w)) Game_AddMoney(-a);
        static int custom = 50000;
        ImGui::SetNextItemWidth(Col(2));
        ImGui::InputInt("##custom", &custom, 0, 0);
        if (custom < 0) custom = 0;
        ImGui::SameLine();
        if (Btn("Add custom amount", -1)) Game_AddMoney(custom);
        std::string k = "Hotkey " + KeyName(g_set.keyMoney) + " adds the selected amount.";
        Note(k.c_str());
    }
    EndCard();
    if (BeginCard("Dust price"))
    {
        if (Slider("Payout", &g_set.payoutMul, 1, 20, "%.1fx")) Dirty();
        Note("Every kilogram you deliver pays more.");
    }
    EndCard();
}

static void TabDust(const GameView& v)
{
    if (BeginCard("Your dust bag"))
    {
        ImGui::Text("Carrying"); ImGui::SameLine();
        ImGui::TextColored(T->b, "%.1f / %.1f kg", v.dust, v.capacity);
        ImGui::SameLine(); ImGui::TextColored(kMuted, "   cleaned %.1f kg", v.cleaned);
        float w = Col(2);
        std::string fill = "Fill the bag (" + KeyName(g_set.keyFill) + ")";
        if (Btn(fill.c_str(), w, true)) Game_FillDust();
        ImGui::SameLine();
        if (Danger("Empty the bag", w)) Game_EmptyDust();
        if (Toggle("Endless dust", &g_set.autoFill, "the bag is always full")) Dirty();
        if (Slider("Bag size", &g_set.capacityMul, 1, 20, "%.0fx")) Dirty();
        Note("Deliver the dust at a station to get paid.");
    }
    EndCard();
    if (BeginCard("Clean the moon"))
    {
        float w = Col(3);
        if (Btn("Clean here", w, true)) Game_CleanAround(1);
        ImGui::SameLine();
        if (Btn("Bigger area", w)) Game_CleanAround(2);
        ImGui::SameLine();
        if (Btn("Huge area", w)) Game_CleanAround(4);
        Note("Finishes the sections around you - uses the game's own developer tool.");
    }
    EndCard();
}

static void TabUpgrades(const GameView& v)
{
    if (BeginCard("Upgrades"))
    {
        if (Btn("Max all upgrades", -1, true)) Game_BuyUpgrade(-1);
        if (Toggle("Free upgrades", &g_set.freeUpgrades, "the Buy buttons below cost nothing")) Dirty();
        Note("Max all is always free - the game's own shop does the buying, your money stays.");
        if (v.upgrades.empty()) Note("Open this tab again in a moment - the upgrades are still loading.");
        for (size_t i = 0; i < v.upgrades.size(); i++)
        {
            const UpgradeView& u = v.upgrades[i];
            ImGui::PushID((int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(u.name.c_str());
            ImGui::SameLine(); ImGui::TextColored(kMuted, " %s", u.group.c_str());
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 190);
            if (u.max > 0) ImGui::TextColored(u.canBuy ? kMuted : T->b, "%d / %d", u.level, u.max);
            else ImGui::TextColored(u.canBuy ? kMuted : T->b, u.canBuy ? "level %d" : "max", u.level);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 96);
            if (!u.canBuy) ImGui::BeginDisabled();
            if (Btn(u.canBuy ? "Buy" : "Max", 96)) Game_BuyUpgrade((int)i);
            if (!u.canBuy) ImGui::EndDisabled();
            ImGui::PopID();
        }
    }
    EndCard();
}

static void TabSpawn(const GameView& v)
{
    if (BeginCard("Dust capsules"))
    {
        static int count = 1;
        const int counts[] = { 1, 5, 10 };
        float w = Col(3);
        for (int i = 0; i < 3; i++)
        {
            if (i) ImGui::SameLine();
            std::string l = std::to_string(counts[i]) + (counts[i] == 1 ? " capsule" : " capsules");
            if (Chip(l.c_str(), count == counts[i], w)) count = counts[i];
        }
        std::string b = "Spawn " + std::to_string(count) + (count == 1 ? " dust capsule" : " dust capsules");
        if (Btn(b.c_str(), -1, true)) Game_SpawnCapsules(count);
        Note("Full capsules land in front of you - carry them to a station or a catcher. Your own bag stays as it was.");
    }
    EndCard();
    if (BeginCard("Machines"))
    {
        if (Btn("Capsule catcher", -1)) Game_SpawnCatcher();
        Note("The catcher is placed in front of you.");
    }
    EndCard();
    if (BeginCard("Robots"))
    {
        if (v.robots.empty()) Note("No robot docks in this game version.");
        for (size_t i = 0; i < v.robots.size(); i++)
        {
            ImGui::PushID((int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(v.robots[i].name.c_str());
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 96);
            if (Btn("Spawn", 96, true)) Game_SpawnRobot(v.robots[i].kind);
            ImGui::PopID();
        }
        Note("The dock with its robot is placed in front of you. Look at a flat spot.");
    }
    EndCard();
}

static void TabTeleport(const GameView& v)
{
    if (BeginCard("Places"))
    {
        if (Btn("Back to the depot", -1, true)) Game_ToDepot();
        if (v.places.empty()) Note("Nothing found yet.");
        for (size_t i = 0; i < v.places.size(); i++)
        {
            ImGui::PushID((int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(v.places[i].name.c_str());
            ImGui::SameLine(); ImGui::TextColored(kMuted, " %s", v.places[i].group.c_str());
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 190);
            ImGui::TextColored(kMuted, "%.0f m", v.places[i].dist);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 96);
            if (Btn("Go", 96, true)) Game_Teleport((int)i);
            ImGui::PopID();
        }
    }
    EndCard();
    if (BeginCard("Saved positions"))
    {
        for (int i = 0; i < 3; i++)
        {
            ImGui::PushID(100 + i);
            ImGui::AlignTextToFramePadding();
            ImGui::Text("Slot %d", i + 1);
            ImGui::SameLine(90);
            ImGui::TextColored(v.slot[i] ? T->b : kMuted, v.slot[i] ? "saved" : "empty");
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 190);
            if (Btn("Save here", 100)) Game_SavePos(i);
            ImGui::SameLine();
            if (!v.slot[i]) ImGui::BeginDisabled();
            if (Btn("Go", 82, true)) Game_LoadPos(i);
            if (!v.slot[i]) ImGui::EndDisabled();
            ImGui::PopID();
        }
    }
    EndCard();
}

static void TabSettings(const GameView&)
{
    ThemeCard();
    HotkeyEntry keys[] = { { "Open / close menu", &g_set.keyMenu }, { "Add money", &g_set.keyMoney }, { "Fly mode", &g_set.keyFly }, { "Fill dust bag", &g_set.keyFill } };
    HotkeysCard(keys, 4);
    if (BeginCard("Notifications"))
    {
        if (Toggle("Notification sound", &g_notifySound, "a soft click with every message")) { Dirty(); if (g_notifySound) Notify("Notification sound on"); }
    }
    EndCard();
    if (BeginCard("Save Settings"))
    {
        if (Toggle("Save Settings", &g_set.saveSettings, "cheats stay on next time")) Dirty();
    }
    EndCard();
    ImGui::TextColored(kMuted, "EvilDragon's Cheats v" ED_VERSION);
}

// ---------------- main window ----------------
static bool g_themed;

void Menu_Draw()
{
    if (!g_themed) { ApplyTheme(g_set.theme); g_themed = true; }
    if (SaveDue()) Menu_SaveSettings();

    GameView v;
    { std::lock_guard<std::mutex> lk(g_viewLock); v = g_view; }

    ImGui::GetIO().MouseDrawCursor = g_menuOpen;
    DrawToast();
    if (!MenuVisible(g_menuOpen)) return;

    BeginMenuWindow(ED_GAME_NAME, g_set.keyMenu, kTabs, kTabCount, 4, &g_tab);
    if (g_tab != kSettingsTab)
    {
        StatusCard(v.inGame, v.allowed);
        if (v.crew) ImGui::TextColored(kRed, "Cheats only work when you play alone.");
    }

    ImGui::BeginChild("##body", { 0, 0 }, ImGuiChildFlags_None, ImGuiWindowFlags_NoBackground);
    bool lock = (!v.inGame || !v.allowed) && g_tab != kSettingsTab;
    if (lock) ImGui::BeginDisabled();
    switch (g_tab)
    {
    case 0: TabPlayer(v); break;
    case 1: TabMoney(v); break;
    case 2: TabDust(v); break;
    case 3: TabUpgrades(v); break;
    case 4: TabSpawn(v); break;
    case 5: TabTeleport(v); break;
    case 6: TabSettings(v); break;
    }
    if (lock) ImGui::EndDisabled();
    ImGui::EndChild();
    EndMenuWindow();
}
