// The menu for Find The Needle (look and widgets come from shared/unreal).
#include "cheats.h"
#include <cmath>
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
    g_notifySound = I("NotifySound", 1) != 0;
    if (g_set.saveSettings)
    {
        g_set.speedMul = F("Speed", 1);
        g_set.noFallDeath = B("NoFallDeath"); g_set.infStamina = B("InfiniteStamina"); g_set.infJetpack = B("InfiniteJetpack");
    }
}

void Menu_SaveSettings()
{
    std::ofstream f(IniPath());
    f << "Theme=" << g_themeName << "\nKeyMenu=" << g_set.keyMenu << "\nKeyMoney=" << g_set.keyMoney << "\nKeyFly=" << g_set.keyFly
      << "\nMoneyIndex=" << g_set.moneyIndex << "\nSaveSettings=" << (int)g_set.saveSettings
      << "\nNotifySound=" << (int)g_notifySound << "\n";
    if (g_set.saveSettings)
        f << "Speed=" << g_set.speedMul << "\nNoFallDeath=" << (int)g_set.noFallDeath
          << "\nInfiniteStamina=" << (int)g_set.infStamina << "\nInfiniteJetpack=" << (int)g_set.infJetpack << "\n";
}

// ---------------- hotkeys ----------------
static const double kMoney[] = { 10, 100, 1000, 10000 };
static std::string Dollars(double v) { char b[48]; snprintf(b, sizeof b, "$%.2f", v); std::string s = b; return Money((long long)v) + s.substr(s.find('.')); }

void Menu_HandleHotkeys()
{
    ReadKeys();
    if (HandleRebind()) return;
    if (Pressed(g_set.keyMenu)) g_menuOpen = !g_menuOpen;
    bool typing = ImGui::GetIO().WantTextInput;
    if (!typing && g_view.inGame && g_view.allowed)
    {
        if (Pressed(g_set.keyMoney)) Game_AddMoney(kMoney[g_set.moneyIndex & 3]);
        if (Pressed(g_set.keyFly)) { g_set.fly = !g_set.fly; Notify(g_set.fly ? "Fly mode on" : "Fly mode off"); }
    }
}

// ---------------- tabs ----------------
static int g_tab;
static const char* kTabs[] = { "Player", "Money", "Upgrades", "Spawn", "Teleport", "Settings" };
static const int kTabCount = 6, kSettingsTab = 5;

static void TabPlayer(const GameView& v)
{
    if (BeginCard("Body"))
    {
        ImGui::Text("Stamina"); ImGui::SameLine(); ImGui::TextColored(T->b, "%.0f / %.0f", v.stamina, v.staminaMax);
        ImGui::SameLine(); ImGui::TextColored(kMuted, "   Jetpack"); ImGui::SameLine(); ImGui::TextColored(T->b, "%.1f s", v.fuel);
        if (Toggle("Infinite stamina", &g_set.infStamina, "dig, run and jump forever")) Dirty();
        if (Toggle("Infinite jetpack", &g_set.infJetpack, "the tank never runs dry")) Dirty();
        if (Toggle("No fall death", &g_set.noFallDeath, "falls from any height are safe")) Dirty();
        if (Btn("Refill stamina & jetpack", -1)) Game_RefillStamina();
    }
    EndCard();
    if (BeginCard("Movement"))
    {
        if (Slider("Walk speed", &g_set.speedMul, 1, 5, "%.1fx")) Dirty();
        if (Btn("Reset speed", -1)) { g_set.speedMul = 1; Dirty(); }
    }
    EndCard();
    if (BeginCard("Fly mode"))
    {
        std::string hint = "(" + KeyName(g_set.keyFly) + ")";
        Toggle("Fly / noclip", &g_set.fly, hint.c_str());
        Note("The game's own noclip - fly through the hay, walls and the ground.");
    }
    EndCard();
}

static void TabMoney(const GameView& v)
{
    if (BeginCard("Money"))
    {
        ImGui::Text("Wallet"); ImGui::SameLine(); ImGui::TextColored(T->b, "%s", Dollars(v.money).c_str());
        if (v.debt > 0.001) { ImGui::SameLine(); ImGui::TextColored(kRed, "   debt %s", Dollars(v.debt).c_str()); }
        const char* labels[] = { "$10", "$100", "$1,000", "$10,000" };
        float w = Col(4);
        for (int i = 0; i < 4; i++) { if (i) ImGui::SameLine(); if (Chip(labels[i], g_set.moneyIndex == i, w)) { g_set.moneyIndex = i; Dirty(); } }
        w = Col(2);
        double a = kMoney[g_set.moneyIndex & 3];
        if (Btn(("Add " + Dollars(a)).c_str(), w, true)) Game_AddMoney(a);
        ImGui::SameLine();
        if (Danger(("Remove " + Dollars(a)).c_str(), w)) Game_AddMoney(-a);
        static int custom = 500;
        ImGui::SetNextItemWidth(Col(2));
        ImGui::InputInt("##custom", &custom, 0, 0);
        if (custom < 0) custom = 0;
        ImGui::SameLine();
        if (Btn("Add custom amount", -1)) Game_AddMoney(custom);
        if (v.debt > 0.001 && Btn("Pay off the debt", -1)) Game_PayDebt();
        std::string k = "Hotkey " + KeyName(g_set.keyMoney) + " adds the selected amount.";
        Note(k.c_str());
    }
    EndCard();
}

static void TabUpgrades(const GameView& v)
{
    if (BeginCard("Tools"))
    {
        if (Btn("Unlock every tool", -1, true)) Game_GiveTool(-1);
        for (size_t i = 0; i < v.tools.size(); i++)
        {
            ImGui::PushID(1000 + (int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(v.tools[i].name.c_str());
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 96);
            if (v.tools[i].done) { ImGui::TextColored(T->b, "owned"); }
            else if (Btn("Get", 96)) Game_GiveTool((int)i);
            ImGui::PopID();
        }
    }
    EndCard();
    if (BeginCard("Tech"))
    {
        if (Btn("Unlock every upgrade", -1, true)) Game_BuyTech(-1);
        Note("Free - uses the game's own tech tree, your money stays.");
        if (v.techs.empty()) Note("Open this tab again in a moment - the tech tree is still loading.");
        std::string last;
        for (size_t i = 0; i < v.techs.size(); i++)
        {
            const TechView& t = v.techs[i];
            if (t.branch != last) { ImGui::TextColored(T->a, "%s", t.branch.c_str()); last = t.branch; }
            ImGui::PushID((int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(t.name.c_str());
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 190);
            bool maxed = t.rank >= t.max;
            ImGui::TextColored(maxed ? T->b : kMuted, "%d / %d", t.rank, t.max);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 96);
            if (maxed) ImGui::BeginDisabled();
            if (Btn(maxed ? "Max" : "+1", 96)) Game_BuyTech((int)i);
            if (maxed) ImGui::EndDisabled();
            ImGui::PopID();
        }
    }
    EndCard();
}

static void TabSpawn(const GameView& v)
{
    if (BeginCard("Items"))
    {
        const int counts[] = { 1, 5, 10 };
        float w = Col(3);
        for (int i = 0; i < 3; i++)
        {
            if (i) ImGui::SameLine();
            std::string l = std::to_string(counts[i]) + "x";
            if (Chip(l.c_str(), g_set.spawnCount == counts[i], w)) g_set.spawnCount = counts[i];
        }
        static char filter[48] = "";
        ImGui::SetNextItemWidth(-1);
        ImGui::InputTextWithHint("##filter", "Search items...", filter, sizeof filter);
        std::string f = filter;
        for (char& c : f) c = (char)tolower((unsigned char)c);
        if (v.items.empty()) Note("Open this tab again in a moment - the items are still loading.");
        for (size_t i = 0; i < v.items.size(); i++)
        {
            std::string n = v.items[i].name;
            if (!f.empty())
            {
                std::string l = n + " " + v.items[i].detail;
                for (char& c : l) c = (char)tolower((unsigned char)c);
                if (l.find(f) == std::string::npos) continue;
            }
            ImGui::PushID(2000 + (int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(n.c_str());
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 96);
            if (Btn("Spawn", 96, true)) Game_SpawnItem((int)i, g_set.spawnCount);
            ImGui::PopID();
        }
        Note("Items land in front of you.");
    }
    EndCard();
}

static void TabTeleport(const GameView& v)
{
    if (BeginCard("Places"))
    {
        ImGui::Text("Needles found"); ImGui::SameLine(); ImGui::TextColored(T->b, "%d / %d", v.needlesFound, v.needlesTotal);
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
        Note("Needles sit inside the hay - turn on fly mode to dig in or out.");
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
    HotkeyEntry keys[] = { { "Open / close menu", &g_set.keyMenu }, { "Add money", &g_set.keyMoney }, { "Fly mode", &g_set.keyFly } };
    HotkeysCard(keys, 3);
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
    ImGui::TextColored(kMuted, "EvilDragon's Cheats v" ED_VERSION "  -  unofficial mod");
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

    BeginMenuWindow(ED_GAME_NAME, g_set.keyMenu, kTabs, kTabCount, 3, &g_tab);
    if (g_tab != kSettingsTab) StatusCard(v.inGame, v.allowed);

    ImGui::BeginChild("##body", { 0, 0 }, ImGuiChildFlags_None, ImGuiWindowFlags_NoBackground);
    bool lock = (!v.inGame || !v.allowed) && g_tab != kSettingsTab;
    if (lock) ImGui::BeginDisabled();
    switch (g_tab)
    {
    case 0: TabPlayer(v); break;
    case 1: TabMoney(v); break;
    case 2: TabUpgrades(v); break;
    case 3: TabSpawn(v); break;
    case 4: TabTeleport(v); break;
    case 5: TabSettings(v); break;
    }
    if (lock) ImGui::EndDisabled();
    ImGui::EndChild();
    EndMenuWindow();
}
