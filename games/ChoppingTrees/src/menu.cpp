// The menu for A Game About Chopping Trees (look and widgets come from shared/unreal).
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
    g_set.logAmount = I("LogAmount", 10);
    g_set.saveSettings = B("SaveSettings");
    g_notifySound = I("NotifySound", 1) != 0;
    if (g_set.saveSettings)
    {
        g_set.speedMul = F("Speed", 1); g_set.jumpMul = F("Jump", 1); g_set.flySpeed = F("FlySpeed", 2);
        g_set.god = B("God"); g_set.infStamina = B("InfiniteStamina");
        g_set.oneHit = B("OneHit"); g_set.chopSpeedMul = F("ChopSpeed", 1); g_set.logValueMul = F("LogValue", 1);
        g_set.bigCapacity = B("BigCapacity");
    }
}

void Menu_SaveSettings()
{
    std::ofstream f(IniPath());
    f << "Theme=" << g_themeName << "\nKeyMenu=" << g_set.keyMenu << "\nKeyMoney=" << g_set.keyMoney << "\nKeyFly=" << g_set.keyFly
      << "\nMoneyIndex=" << g_set.moneyIndex << "\nLogAmount=" << g_set.logAmount << "\nSaveSettings=" << (int)g_set.saveSettings
      << "\nNotifySound=" << (int)g_notifySound << "\n";
    if (g_set.saveSettings)
        f << "Speed=" << g_set.speedMul << "\nJump=" << g_set.jumpMul << "\nFlySpeed=" << g_set.flySpeed
          << "\nGod=" << (int)g_set.god << "\nInfiniteStamina=" << (int)g_set.infStamina
          << "\nOneHit=" << (int)g_set.oneHit << "\nChopSpeed=" << g_set.chopSpeedMul << "\nLogValue=" << g_set.logValueMul
          << "\nBigCapacity=" << (int)g_set.bigCapacity << "\n";
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
static const char* kTabs[] = { "Player", "Money", "Chopping", "Upgrades", "Spawn", "Teleport", "Settings" };
static const int kTabCount = 7, kSettingsTab = 6;


static void TabPlayer(const GameView& v)
{
    if (BeginCard("Survival"))
    {
        ImGui::Text("Health"); ImGui::SameLine(); ImGui::TextColored(T->b, "%.0f / %.0f", v.health, v.maxHealth);
        ImGui::SameLine(); ImGui::TextColored(kMuted, "   Stamina"); ImGui::SameLine(); ImGui::TextColored(T->b, "%.0f / %.0f", v.stamina, v.maxStamina);
        if (Toggle("God mode", &g_set.god, "falling trees and logs do not hurt you")) Dirty();
        if (Toggle("Infinite stamina", &g_set.infStamina, "chop, sprint and jump forever")) Dirty();
        if (Btn("Heal & refill stamina", -1)) Game_Heal();
    }
    EndCard();
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
        if (Slider("Fly speed", &g_set.flySpeed, 0.5f, 6, "%.1fx")) Dirty();
        Note("WASD to move, Space up, Ctrl or C down, Shift faster. You fly through trees and the ground.");
    }
    EndCard();
}

static void TabMoney(const GameView& v)
{
    if (BeginCard("Money"))
    {
        ImGui::Text("Balance"); ImGui::SameLine();
        ImGui::TextColored(T->b, "%s", Money((long long)v.money).c_str());
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
    if (BeginCard("Log price"))
    {
        if (Slider("Log value", &g_set.logValueMul, 1, 20, "%.1fx")) Dirty();
        Note("Every log sells for more at the sawmill.");
    }
    EndCard();
}

static void TabChopping(const GameView& v)
{
    if (BeginCard("Chopping"))
    {
        if (Toggle("One-hit trees", &g_set.oneHit, "trees, stumps and logs break with one hit")) Dirty();
        if (Slider("Chop speed", &g_set.chopSpeedMul, 1, 5, "%.1fx")) Dirty();
    }
    EndCard();
    if (BeginCard("Your logs"))
    {
        ImGui::Text("Carrying"); ImGui::SameLine(); ImGui::TextColored(T->b, "%d logs", v.carried);
        if (v.maxLogs > 0) { ImGui::SameLine(); ImGui::TextColored(kMuted, "   limit %.0f", v.maxLogs); }
        if (Toggle("Unlimited log capacity", &g_set.bigCapacity, "carry as many logs as you want")) Dirty();
        if (Danger("Empty the log bag", -1)) Game_ClearLogs();
        Note("New logs are in the Spawn tab.");
    }
    EndCard();
}

static void TabUpgrades(const GameView& v)
{
    if (BeginCard("Upgrades"))
    {
        if (Btn("Max all upgrades", -1, true)) Game_BuyUpgrade(-1);
        Note("Free - the game's own shop does the buying, you keep your money.");
        if (v.upgrades.empty()) Note("Open this tab again in a moment - the upgrades are still loading.");
        for (size_t i = 0; i < v.upgrades.size(); i++)
        {
            const UpgradeView& u = v.upgrades[i];
            ImGui::PushID((int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(u.name.c_str());
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 190);
            if (u.oneTime) ImGui::TextColored(u.done ? T->b : kMuted, u.done ? "done" : "not yet");
            else ImGui::TextColored(u.done ? T->b : kMuted, "%d / %d", u.level, u.max);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 96);
            if (!u.canBuy) ImGui::BeginDisabled();
            if (Btn(u.done ? "Max" : "Buy", 96)) Game_BuyUpgrade((int)i);
            if (!u.canBuy) ImGui::EndDisabled();
            ImGui::PopID();
        }
    }
    EndCard();
    if (BeginCard("Skins"))
    {
        if (Btn("Unlock all skins", -1)) Game_UnlockSkins();
        Note("Uses the game's own developer cheat.");
    }
    EndCard();
}

static void TabSpawn(const GameView& v)
{
    if (BeginCard("Logs"))
    {
        ImGui::Text("Carrying"); ImGui::SameLine(); ImGui::TextColored(T->b, "%d logs", v.carried);
        ImGui::SetNextItemWidth(-1);
        if (ImGui::SliderInt("##logamount", &g_set.logAmount, 1, 100, "Amount  %d")) Dirty();
        static int kind = -1;   // -1 = mixed
        static bool intoBag = false;
        float w = Col(2);
        if (Chip("In front of me", !intoBag, w)) intoBag = false;
        ImGui::SameLine();
        if (Chip("Into my log bag", intoBag, w)) intoBag = true;
        if (!v.treeTypes.empty())
        {
            ImGui::TextColored(kMuted, "Kind of tree");
            float w3 = Col(3);
            if (Chip("Mixed", kind < 0, w3)) kind = -1;
            for (size_t i = 0; i < v.treeTypes.size(); i++)
            {
                if ((i + 1) % 3) ImGui::SameLine();
                ImGui::PushID((int)i);
                if (Chip(v.treeTypes[i].c_str(), kind == (int)i, w3)) kind = (int)i;
                ImGui::PopID();
            }
            if (kind >= (int)v.treeTypes.size()) kind = -1;
        }
        std::string label = "Spawn " + std::to_string(g_set.logAmount) + " logs";
        if (Btn(label.c_str(), -1, true)) { if (intoBag) Game_AddLogs(kind, g_set.logAmount); else Game_SpawnLogs(kind, g_set.logAmount); }
        Note(intoBag ? "The logs go straight into your log bag." : "The logs land in rows in front of you - pick them up or carry them to the saw.");
    }
    EndCard();
    if (BeginCard("Forest"))
    {
        if (Btn("Regrow every tree", -1, true)) Game_RegrowTrees();
        Note("Chopped trees grow back within a few seconds.");
    }
    EndCard();
}

static void TabTeleport(const GameView& v)
{
    if (BeginCard("Places"))
    {
        if (v.places.empty()) Note("Nothing found yet.");
        for (size_t i = 0; i < v.places.size(); i++)
        {
            ImGui::PushID((int)i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(v.places[i].name.c_str());
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
    if (g_tab != kSettingsTab) StatusCard(v.inGame, v.allowed);

    ImGui::BeginChild("##body", { 0, 0 }, ImGuiChildFlags_None, ImGuiWindowFlags_NoBackground);
    bool lock = (!v.inGame || !v.allowed) && g_tab != kSettingsTab;
    if (lock) ImGui::BeginDisabled();
    switch (g_tab)
    {
    case 0: TabPlayer(v); break;
    case 1: TabMoney(v); break;
    case 2: TabChopping(v); break;
    case 3: TabUpgrades(v); break;
    case 4: TabSpawn(v); break;
    case 5: TabTeleport(v); break;
    case 6: TabSettings(v); break;
    }
    if (lock) ImGui::EndDisabled();
    ImGui::EndChild();
    EndMenuWindow();
}
