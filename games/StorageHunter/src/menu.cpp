// The menu (ImGui) in the EvilDragon style: same themes, colours and layout as the Unity menus.
#include "cheats.h"
#include <cstdio>
#include <cstdarg>
#include <fstream>
#include <map>
#include "imgui.h"
#include "imgui_internal.h"

Settings g_set;
GameView g_view;
std::mutex g_viewLock;
bool g_menuOpen;

// ---------------- settings file ----------------
static std::string IniPath() { return ModDir() + "EvilDragon.StorageHunterCheats.ini"; }

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
    g_set.moneyIndex = I("MoneyIndex", 2);
    g_set.saveSettings = B("SaveSettings");
    if (g_set.saveSettings)
    {
        g_set.speedMul = F("Speed", 1); g_set.jumpMul = F("Jump", 1); g_set.gravity = F("Gravity", 1);
        g_set.unlimitedJumps = B("UnlimitedJumps"); g_set.flySpeed = F("FlySpeed", 2);
        g_set.alwaysVerifyFix = B("AlwaysVerifyFix"); g_set.infiniteFuel = B("InfiniteFuel");
        g_set.freezeTime = B("FreezeTime"); g_set.brokeBidders = B("WinAuctions");
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
          << "\nAlwaysVerifyFix=" << (int)g_set.alwaysVerifyFix << "\nInfiniteFuel=" << (int)g_set.infiniteFuel
          << "\nFreezeTime=" << (int)g_set.freezeTime << "\nWinAuctions=" << (int)g_set.brokeBidders << "\n";
}

static const int kMoney[] = { 1000, 10000, 100000, 1000000 };

void Menu_HandleHotkeys()
{
    ReadKeys();
    if (HandleRebind()) return;
    if (Pressed(g_set.keyMenu)) { g_menuOpen = !g_menuOpen; Game_SetMenuOpen(g_menuOpen); }
    bool typing = ImGui::GetIO().WantTextInput;
    if (!typing && g_view.inGame && g_view.allowed)
    {
        if (Pressed(g_set.keyMoney)) { int a = kMoney[g_set.moneyIndex & 3]; Game_AddMoney(a); Toast("+" + Money(a)); }
        if (Pressed(g_set.keyFly)) { g_set.fly = !g_set.fly; Toast(g_set.fly ? "Fly mode ON" : "Fly mode OFF"); }
    }
}

// ---------------- tabs ----------------
static int g_tab;
static const char* kTabs[] = { "Player", "Money", "Items", "Workshop", "Business", "World", "Settings" };
static const int kTabCount = 7, kSettingsTab = 6;

static void TabPlayer(const GameView& v)
{
    if (BeginCard("Movement"))
    {
        if (Slider("Walk & sprint speed", &g_set.speedMul, 1, 5, "%.1fx")) Dirty();
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
        Note("WASD to move, Space up, Ctrl or C down, Shift faster. You fly through walls.");
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
    if (BeginCard("Renown & perks"))
    {
        static int sel = -1;   // -1 = all districts
        if (v.districts.empty()) Note("No districts found yet.");
        else
        {
            if (sel >= (int)v.districts.size()) sel = -1;
            float cw = Col((int)v.districts.size() + 1);
            if (Chip("All", sel == -1, cw)) sel = -1;
            for (int i = 0; i < (int)v.districts.size(); i++)
            {
                ImGui::SameLine();
                ImGui::PushID(i);
                if (Chip(v.districts[i].name.c_str(), sel == i, cw)) sel = i;
                ImGui::PopID();
            }
            for (int i = 0; i < (int)v.districts.size(); i++)
            {
                const DistrictView& d = v.districts[i];
                if (sel >= 0 && sel != i) continue;
                ImGui::TextUnformatted(d.name.c_str());
                ImGui::SameLine(150);
                ImGui::TextColored(T->b, "Level %d", d.level);
                ImGui::SameLine(250);
                ImGui::TextColored(kMuted, "%d / %d", d.points, d.required);
                ImGui::SameLine(360);
                ImGui::TextColored(kMuted, "%d perk pts", d.perks);
            }
            float w = Col(3);
            if (Btn("+1 level", w, true))
                for (int i = 0; i < (int)v.districts.size(); i++)
                    if (sel < 0 || sel == i) Game_AddRenown((std::max)(1, v.districts[i].required - v.districts[i].points), i);
            ImGui::SameLine();
            if (Btn("+500 renown", w)) Game_AddRenown(500, sel); ImGui::SameLine();
            if (Btn("+5,000 renown", w)) Game_AddRenown(5000, sel);
            w = Col(2);
            if (Btn("+1 perk point", w)) Game_AddPerkPoints(1, sel); ImGui::SameLine();
            if (Btn("+5 perk points", w)) Game_AddPerkPoints(5, sel);
        }
    }
    EndCard();
    if (BeginCard("Tickets"))
    {
        float w = Col(2);
        ImGui::Text("Tickets"); ImGui::SameLine(); ImGui::TextColored(T->b, "%d", v.tickets);
        if (Btn("+10 tickets", w)) Game_AddTickets(10); ImGui::SameLine();
        if (Btn("+100 tickets", w)) Game_AddTickets(100);
    }
    EndCard();
}

static void TabItems(const GameView& v)
{
    static bool requested;
    static char search[64];
    static int cat = -1, selected = -1, count = 1;
    static bool verified = true;
    std::vector<ItemEntry> items;
    std::vector<std::string> cats;
    { std::lock_guard<std::mutex> lk(g_itemsLock); items = g_items; cats = g_itemCats; }
    if (items.empty() && !requested && v.inGame) { requested = true; Game_ScanItems(); }

    if (BeginCard("Spawn items"))
    {
        if (items.empty()) { Note("Loading the item list..."); if (Btn("Reload list", -1)) Game_ScanItems(); EndCard(); return; }

        // categories
        float x = 0, avail = ImGui::GetContentRegionAvail().x;
        auto chip = [&](const char* label, int id) {
            float w = ImGui::CalcTextSize(label).x + 22;
            if (x > 0 && x + w < avail) ImGui::SameLine(); else x = 0;
            if (Chip(label, cat == id, w)) { cat = id; selected = -1; }
            x += w + ImGui::GetStyle().ItemSpacing.x;
        };
        chip("All", -1);
        for (int i = 0; i < (int)cats.size(); i++) { if (cats[i].empty()) continue; ImGui::PushID(i); chip(cats[i].c_str(), i); ImGui::PopID(); }

        ImGui::SetNextItemWidth(-1);
        ImGui::InputTextWithHint("##search", "Search...", search, sizeof search);

        std::string q = search;
        for (auto& ch : q) ch = (char)tolower((unsigned char)ch);
        ImGui::BeginChild("##list", { 0, 300 }, ImGuiChildFlags_Border);
        int shown = 0;
        for (auto& it : items)
        {
            if (cat >= 0 && it.cat != cat) continue;
            if (!q.empty())
            {
                std::string n = it.name;
                for (auto& ch : n) ch = (char)tolower((unsigned char)ch);
                if (n.find(q) == std::string::npos) continue;
            }
            shown++;
            ImGui::PushID(it.index);
            if (ImGui::Selectable(it.name.c_str(), selected == it.index, ImGuiSelectableFlags_AllowDoubleClick))
            {
                selected = it.index;
                if (ImGui::IsMouseDoubleClicked(0)) Game_SpawnItem(it.index, count, verified);
            }
            std::string val = Money(it.value);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - ImGui::CalcTextSize(val.c_str()).x + ImGui::GetCursorPosX() - 8);
            ImGui::TextColored(kMuted, "%s", val.c_str());
            ImGui::PopID();
        }
        if (!shown) ImGui::TextColored(kMuted, "No items found.");
        ImGui::EndChild();

        ImGui::SetNextItemWidth(-1);
        ImGui::SliderInt("##count", &count, 1, 20, "Amount  %d");
        Toggle("Spawn verified", &verified, "already checked & authentic");
        std::string name = "Select an item";
        for (auto& it : items) if (it.index == selected) name = "Spawn " + it.name + (count > 1 ? " x" + std::to_string(count) : "");
        if (selected < 0) ImGui::BeginDisabled();
        if (Btn(name.c_str(), -1, true)) Game_SpawnItem(selected, count, verified);
        if (selected < 0) ImGui::EndDisabled();
        Note("Double-click an item to spawn it right away. Items appear in front of you.");
    }
    EndCard();
}

static void TabWorkshop(const GameView& v)
{
    if (BeginCard("Tool levels"))
    {
        static int verify = -1, fix = -1, lock = -1;
        if (verify < 0 && v.inGame) { verify = v.verifyLevel; fix = v.fixLevel; lock = v.lockLevel; }
        ImGui::TextColored(kMuted, "Now: verification %d, fixing %d, lockpicking %d", v.verifyLevel, v.fixLevel, v.lockLevel);
        ImGui::SetNextItemWidth(-1); ImGui::SliderInt("##verify", &verify, 0, 10, "Verification tool  %d");
        ImGui::SetNextItemWidth(-1); ImGui::SliderInt("##fix", &fix, 0, 10, "Fixing tool  %d");
        ImGui::SetNextItemWidth(-1); ImGui::SliderInt("##lock", &lock, 0, 10, "Lockpicking tool  %d");
        float w = Col(2);
        if (Btn("Apply", w, true)) Game_SetToolLevels(verify < 0 ? 0 : verify, fix < 0 ? 0 : fix, lock < 0 ? 0 : lock);
        ImGui::SameLine();
        if (Btn("All to max", w)) { verify = fix = lock = 10; Game_SetToolLevels(10, 10, 10); }
    }
    EndCard();
    if (BeginCard("Items"))
    {
        if (Toggle("Verify & fix always succeed", &g_set.alwaysVerifyFix)) Dirty();
        float w = Col(2);
        if (Btn("Unlock looked-at container", w)) Game_UnlockContainer(); ImGui::SameLine();
        if (Btn("Authenticate item", w)) Game_AuthenticateItem();
        ImGui::Text("Spawn random items");
        w = Col(3);
        if (Btn("x1", w)) Game_SpawnRandomItems(1); ImGui::SameLine();
        if (Btn("x5", w)) Game_SpawnRandomItems(5); ImGui::SameLine();
        if (Btn("x10", w)) Game_SpawnRandomItems(10);
        Note("Items spawn in front of you.");
    }
    EndCard();
}

static void TabBusiness(const GameView& v)
{
    if (BeginCard("Pawn shop"))
    {
        float w = Col(3);
        if (Btn("Level +1", w)) Game_PawnShopLevelUp(1); ImGui::SameLine();
        if (Btn("Level +5", w)) Game_PawnShopLevelUp(5); ImGui::SameLine();
        if (Btn("Buy pawn shop", w)) Game_BuyPawnShop();
    }
    EndCard();
    if (BeginCard("Auctions & quests"))
    {
        if (Toggle("Win every auction", &g_set.brokeBidders, "NPC bidders have no money")) Dirty();
        float w = Col(2);
        if (Btn("Complete task", w)) Game_CompleteTask(); ImGui::SameLine();
        if (Btn("Complete quest", w)) Game_CompleteQuest();
    }
    EndCard();
    if (BeginCard("Vehicle"))
    {
        if (Toggle("Infinite fuel", &g_set.infiniteFuel)) Dirty();
        if (Btn("Fill the tank", -1)) Game_FillFuel();
    }
    EndCard();
}

static void TabWorld(const GameView& v)
{
    if (BeginCard("Time"))
    {
        if (v.hasSky)
        {
            int t = (int)v.timeOfDay;
            ImGui::Text("Clock"); ImGui::SameLine(); ImGui::TextColored(T->b, "%02d:%02d", (t / 100) % 24, (t % 100) * 60 / 100);
            ImGui::SameLine(); ImGui::TextColored(kMuted, "  day %d", v.passedDays + 1);
        }
        if (Toggle("Freeze time", &g_set.freezeTime)) Dirty();
        float w = Col(4);
        if (Btn("Morning", w)) Game_SetTimeOfDay(800); ImGui::SameLine();
        if (Btn("Noon", w)) Game_SetTimeOfDay(1200); ImGui::SameLine();
        if (Btn("Evening", w)) Game_SetTimeOfDay(1800); ImGui::SameLine();
        if (Btn("Night", w)) Game_SetTimeOfDay(2300);
        static float hour = 12;
        ImGui::SetNextItemWidth(Col(2));
        ImGui::SliderFloat("##hour", &hour, 0, 23.9f, "%.1f h");
        ImGui::SameLine();
        if (Btn("Set time", -1)) Game_SetTimeOfDay(hour * 100.0);
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

    BeginMenuWindow(ED_GAME_NAME, g_set.keyMenu, kTabs, kTabCount, 4, &g_tab);
    if (g_tab != kSettingsTab) StatusCard(v.inGame, v.allowed);

    ImGui::BeginChild("##body", { 0, 0 }, ImGuiChildFlags_None, ImGuiWindowFlags_NoBackground);
    bool lock = (!v.inGame || !v.allowed) && g_tab != kSettingsTab;
    if (lock) ImGui::BeginDisabled();
    switch (g_tab)
    {
    case 0: TabPlayer(v); break;
    case 1: TabMoney(v); break;
    case 2: TabItems(v); break;
    case 3: TabWorkshop(v); break;
    case 4: TabBusiness(v); break;
    case 5: TabWorld(v); break;
    case 6: TabSettings(v); break;
    }
    if (lock) ImGui::EndDisabled();
    ImGui::EndChild();
    EndMenuWindow();
}
