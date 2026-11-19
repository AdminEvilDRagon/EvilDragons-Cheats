// The menu for A Game About Digging A Hole (look and widgets come from shared/unreal).
#include "cheats.h"
#include <fstream>
#include <map>

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
    g_set.moneyIndex = I("MoneyIndex", 1);
    g_set.saveSettings = B("SaveSettings");
    if (g_set.saveSettings)
    {
        g_set.speedMul = F("Speed", 1); g_set.jumpMul = F("Jump", 1); g_set.gravity = F("Gravity", 1);
        g_set.unlimitedJumps = B("UnlimitedJumps"); g_set.flySpeed = F("FlySpeed", 2);
        g_set.god = B("God"); g_set.infBattery = B("InfiniteBattery");
        g_set.drillMul = F("Drill", 1); g_set.radiusMul = F("Radius", 1); g_set.jetpackMul = F("Jetpack", 1);
        g_set.noDepthLimit = B("NoDepthLimit"); g_set.bigInventory = B("BigInventory");
        g_set.oreRadius = F("OreRange", 10); g_set.oreToBag = I("OreToBag", 1) != 0; g_set.autoCollect = B("AutoCollect");
    }
}

void Menu_SaveSettings()
{
    std::ofstream f(IniPath());
    f << "Theme=" << g_themeName << "\nKeyMenu=" << g_set.keyMenu << "\nKeyMoney=" << g_set.keyMoney << "\nKeyFly=" << g_set.keyFly
      << "\nMoneyIndex=" << g_set.moneyIndex << "\nSaveSettings=" << (int)g_set.saveSettings << "\n";
    if (g_set.saveSettings)
        f << "Speed=" << g_set.speedMul << "\nJump=" << g_set.jumpMul << "\nGravity=" << g_set.gravity
          << "\nUnlimitedJumps=" << (int)g_set.unlimitedJumps << "\nFlySpeed=" << g_set.flySpeed
          << "\nGod=" << (int)g_set.god << "\nInfiniteBattery=" << (int)g_set.infBattery
          << "\nDrill=" << g_set.drillMul << "\nRadius=" << g_set.radiusMul << "\nJetpack=" << g_set.jetpackMul
          << "\nNoDepthLimit=" << (int)g_set.noDepthLimit << "\nBigInventory=" << (int)g_set.bigInventory
          << "\nOreRange=" << g_set.oreRadius << "\nOreToBag=" << (int)g_set.oreToBag << "\nAutoCollect=" << (int)g_set.autoCollect << "\n";
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
    }
}

// ---------------- tabs ----------------
static int g_tab;
static const char* kTabs[] = { "Player", "Money", "Tools", "Spawn", "Teleport", "Settings" };
static const int kTabCount = 6, kSettingsTab = 5;

static void TabPlayer(const GameView& v)
{
    if (BeginCard("Survival"))
    {
        ImGui::Text("Health"); ImGui::SameLine(); ImGui::TextColored(T->b, "%.0f / %.0f", v.health, v.maxHealth);
        ImGui::SameLine(); ImGui::TextColored(kMuted, "   Battery"); ImGui::SameLine(); ImGui::TextColored(T->b, "%.0f / %.0f", v.battery, v.maxBattery);
        ImGui::SameLine(); ImGui::TextColored(kMuted, "   Depth"); ImGui::SameLine(); ImGui::TextColored(T->b, "%.0f m", v.depth);
        if (Toggle("God mode", &g_set.god, "no damage from falls or lava")) Dirty();
        if (Toggle("Infinite battery", &g_set.infBattery, "drill and jetpack never run out")) Dirty();
        if (Btn("Heal & recharge", -1)) Game_Heal();
    }
    EndCard();
    if (BeginCard("Movement"))
    {
        if (Slider("Walk speed", &g_set.speedMul, 1, 5, "%.1fx")) Dirty();
        if (Slider("Jump height", &g_set.jumpMul, 1, 4, "%.1fx")) Dirty();
        if (Slider("Gravity", &g_set.gravity, 0.1f, 1, "%.2fx")) Dirty();
        if (Toggle("Unlimited jumps", &g_set.unlimitedJumps, "jump again in the air")) Dirty();
        if (Btn("Reset movement", -1)) { g_set.speedMul = 1; g_set.jumpMul = 1; g_set.gravity = 1; g_set.unlimitedJumps = false; Dirty(); }
    }
    EndCard();
    if (BeginCard("Fly mode"))
    {
        std::string hint = "(" + KeyName(g_set.keyFly) + ")";
        Toggle("Fly / noclip", &g_set.fly, hint.c_str());
        if (Slider("Fly speed", &g_set.flySpeed, 0.5f, 6, "%.1fx")) Dirty();
        Note("WASD to move, Space up, Ctrl or C down, Shift faster. You fly through the ground.");
    }
    EndCard();
}

static void TabMoney(const GameView& v)
{
    if (BeginCard("Money"))
    {
        ImGui::Text("Balance"); ImGui::SameLine();
        ImGui::TextColored(T->b, "%s", Money(v.money).c_str());
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
    if (BeginCard("Ores in your bag"))
    {
        static int amount = 10;
        ImGui::SetNextItemWidth(-1);
        ImGui::SliderInt("##oreamount", &amount, 1, 500, "Amount  %d");
        for (int i = 0; i < kOreCount; i++)
        {
            ImGui::PushID(i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(kOreNames[i]);
            ImGui::SameLine(130);
            ImGui::TextColored(T->b, "%d", v.ores[i]);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 150);
            if (Btn(("+" + std::to_string(amount)).c_str(), 80)) Game_AddOre(i, amount);
            ImGui::SameLine();
            if (Btn("Clear", 62)) Game_AddOre(i, -v.ores[i]);
            ImGui::PopID();
        }
        float w = Col(2);
        if (Btn(("+" + std::to_string(amount) + " of every ore").c_str(), w, true)) Game_AddOre(-1, amount);
        ImGui::SameLine();
        if (Danger("Empty the bag", w)) Game_ClearOres();
        if (Toggle("Unlimited bag space", &g_set.bigInventory)) Dirty();
        Note("Sell the ores at the shop for money.");
    }
    EndCard();
    if (BeginCard("Ores around you"))
    {
        if (Slider("Range", &g_set.oreRadius, 2, 100, "%.0f m")) Dirty();
        if (Toggle("Put them in your bag", &g_set.oreToBag, "off = just remove them")) Dirty();
        if (Btn("Remove all ores in range", -1, true)) Game_CollectOres(g_set.oreRadius, g_set.oreToBag);
        if (Toggle("Auto-collect", &g_set.autoCollect, "keeps collecting while you dig")) Dirty();
    }
    EndCard();
}

static void TabTools(const GameView& v)
{
    if (BeginCard("Drill & jetpack"))
    {
        ImGui::TextColored(kMuted, "Now: drill %.1f, radius %.1f, jetpack %.1f", v.drill, v.drillRadius, v.jetpack);
        if (Slider("Drill strength", &g_set.drillMul, 1, 20, "%.1fx")) Dirty();
        if (Slider("Dig radius", &g_set.radiusMul, 1, 5, "%.1fx")) Dirty();
        if (Slider("Jetpack power", &g_set.jetpackMul, 1, 5, "%.1fx")) Dirty();
        if (Toggle("No depth limit", &g_set.noDepthLimit, "dig as deep as you want")) Dirty();
        float w = Col(2);
        if (v.drillUnlocked) ImGui::BeginDisabled();
        if (Btn(v.drillUnlocked ? "Drill unlocked" : "Unlock drill", w)) Game_Unlock(true, false);
        if (v.drillUnlocked) ImGui::EndDisabled();
        ImGui::SameLine();
        if (v.jetpackUnlocked) ImGui::BeginDisabled();
        if (Btn(v.jetpackUnlocked ? "Jetpack unlocked" : "Unlock jetpack", w)) Game_Unlock(false, true);
        if (v.jetpackUnlocked) ImGui::EndDisabled();
        if (Btn("Reset tools", -1)) { g_set.drillMul = 1; g_set.radiusMul = 1; g_set.jetpackMul = 1; Dirty(); }
    }
    EndCard();
    if (BeginCard("Items"))
    {
        struct R { const char* name; int which, have; } rows[] = { { "Dynamite", 0, v.dynamite }, { "Lamps", 1, v.lamps }, { "Keys", 2, v.keys }, { "Teleporters", 3, v.teleporters } };
        for (auto& r : rows)
        {
            ImGui::PushID(r.which);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(r.name);
            ImGui::SameLine(130);
            ImGui::TextColored(T->b, "%d", r.have);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 150);
            if (Btn("+1", 70)) Game_AddTool(r.which, 1);
            ImGui::SameLine();
            if (Btn("+10", 72)) Game_AddTool(r.which, 10);
            ImGui::PopID();
        }
    }
    EndCard();
}

static void TabSpawn(const GameView&)
{
    static int count = 1;
    static bool crosshair = true;
    static float radius = 1.0f;
    if (BeginCard("Spawn"))
    {
        ImGui::SetNextItemWidth(-1);
        ImGui::SliderInt("##count", &count, 1, 25, "Amount  %d");
        float w = Col(2);
        if (Chip("At the crosshair", crosshair, w)) crosshair = true;
        ImGui::SameLine();
        if (Chip("In front of you", !crosshair, w)) crosshair = false;
        w = Col(2);
        for (int i = 0; i < kSpawnCount; i++)
        {
            if (i % 2) ImGui::SameLine();
            ImGui::PushID(i);
            if (Btn(kSpawns[i].name, w, i == 0)) Game_Spawn(i, count, crosshair, radius);
            ImGui::PopID();
        }
    }
    EndCard();
    if (BeginCard("Explosions"))
    {
        Slider("Explosion size", &radius, 0.5f, 5, "%.1fx");
        Note("Used for Dynamite and Explosion. Big explosions dig huge holes - stand back (or turn on God mode).");
    }
    EndCard();
}

static void TabTeleport(const GameView& v)
{
    if (BeginCard("Teleport"))
    {
        if (Btn("Back to where you started", -1, true)) Game_ToSurface();
        Note("Takes you to the spot where you loaded in. Turn on fly mode if you end up inside the ground.");
    }
    EndCard();
    if (BeginCard("Saved positions"))
    {
        for (int i = 0; i < 3; i++)
        {
            ImGui::PushID(i);
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
    HotkeyEntry keys[] = { { "Open / close menu", &g_set.keyMenu }, { "Add money", &g_set.keyMoney }, { "Fly mode", &g_set.keyFly } };
    HotkeysCard(keys, 3);
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
    if (!g_menuOpen) return;

    BeginMenuWindow(ED_GAME_NAME, g_set.keyMenu, kTabs, kTabCount, 3, &g_tab);
    if (g_tab != kSettingsTab) StatusCard(v.inGame, v.allowed);

    ImGui::BeginChild("##body", { 0, 0 }, ImGuiChildFlags_None, ImGuiWindowFlags_NoBackground);
    bool lock = (!v.inGame || !v.allowed) && g_tab != kSettingsTab;
    if (lock) ImGui::BeginDisabled();
    switch (g_tab)
    {
    case 0: TabPlayer(v); break;
    case 1: TabMoney(v); break;
    case 2: TabTools(v); break;
    case 3: TabSpawn(v); break;
    case 4: TabTeleport(v); break;
    case 5: TabSettings(v); break;
    }
    if (lock) ImGui::EndDisabled();
    ImGui::EndChild();
    EndMenuWindow();
}
