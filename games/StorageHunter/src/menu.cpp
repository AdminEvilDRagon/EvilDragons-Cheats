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

// ---------------- paths / log ----------------
static std::string ModDir()
{
    char p[MAX_PATH];
    GetModuleFileNameA(g_self, p, MAX_PATH);
    std::string s(p);
    return s.substr(0, s.find_last_of("\\/") + 1);
}

void Log(const char* fmt, ...)
{
    static std::mutex m;
    std::lock_guard<std::mutex> lk(m);
    static bool first = true;
    FILE* f = fopen((ModDir() + "EvilDragon.StorageHunterCheats.log").c_str(), first ? "w" : "a");
    first = false;
    if (!f) return;
    SYSTEMTIME t; GetLocalTime(&t);
    fprintf(f, "[%02d:%02d:%02d] ", t.wHour, t.wMinute, t.wSecond);
    va_list a; va_start(a, fmt); vfprintf(f, fmt, a); va_end(a);
    fputc('\n', f);
    fclose(f);
}

// ---------------- themes ----------------
struct Theme { const char* name; ImVec4 a, b, g, bg, card; };
static const Theme kThemes[] = {
    { "Dragon",  {0.10f,1.00f,0.30f,1}, {0.65f,1.00f,0.20f,1}, {0.10f,1.00f,0.30f,1}, {0.020f,0.045f,0.030f,0.97f}, {0.060f,0.110f,0.075f,1} },
    { "Neon",    {0.00f,0.94f,1.00f,1}, {1.00f,0.18f,0.75f,1}, {0.22f,1.00f,0.55f,1}, {0.035f,0.035f,0.075f,0.97f}, {0.085f,0.085f,0.150f,1} },
    { "Inferno", {1.00f,0.45f,0.10f,1}, {1.00f,0.15f,0.25f,1}, {1.00f,0.80f,0.20f,1}, {0.060f,0.025f,0.020f,0.97f}, {0.140f,0.065f,0.050f,1} },
    { "Ocean",   {0.20f,0.65f,1.00f,1}, {0.00f,1.00f,0.85f,1}, {0.20f,1.00f,0.80f,1}, {0.020f,0.040f,0.075f,0.97f}, {0.050f,0.095f,0.160f,1} },
    { "Purple",  {0.70f,0.40f,1.00f,1}, {1.00f,0.35f,0.85f,1}, {0.55f,1.00f,0.75f,1}, {0.045f,0.025f,0.075f,0.97f}, {0.110f,0.065f,0.165f,1} },
    { "Gold",    {1.00f,0.80f,0.20f,1}, {1.00f,0.55f,0.10f,1}, {0.65f,1.00f,0.35f,1}, {0.050f,0.042f,0.025f,0.97f}, {0.125f,0.105f,0.060f,1} },
};
static const Theme* T = &kThemes[0];
static const ImVec4 kText{ 0.95f,0.96f,1.00f,1 }, kMuted{ 0.72f,0.76f,0.86f,1 }, kRed{ 1.00f,0.20f,0.36f,1 }, kDark{ 0.03f,0.03f,0.05f,1 };

static ImVec4 Mix(ImVec4 x, ImVec4 y, float t) { return { x.x + (y.x - x.x) * t, x.y + (y.y - x.y) * t, x.z + (y.z - x.z) * t, x.w + (y.w - x.w) * t }; }
static ImU32 U(ImVec4 c) { return ImGui::GetColorU32(c); }

static void ApplyTheme(const std::string& name)
{
    T = &kThemes[0];
    for (auto& t : kThemes) if (_stricmp(t.name, name.c_str()) == 0) T = &t;
    g_set.theme = T->name;

    ImGuiStyle& s = ImGui::GetStyle();
    s = ImGuiStyle();
    s.WindowRounding = 14; s.ChildRounding = 10; s.FrameRounding = 8; s.GrabRounding = 8; s.PopupRounding = 8;
    s.WindowBorderSize = 1.5f; s.ChildBorderSize = 1; s.FrameBorderSize = 0;
    s.WindowPadding = { 16, 14 }; s.FramePadding = { 10, 6 }; s.ItemSpacing = { 8, 8 }; s.ItemInnerSpacing = { 8, 6 };
    s.ScrollbarSize = 10; s.ScrollbarRounding = 8; s.GrabMinSize = 14;

    ImVec4* c = s.Colors;
    ImVec4 fieldBg = Mix(T->bg, { 0,0,0,1 }, 0.5f); fieldBg.w = 1;
    c[ImGuiCol_Text] = kText;
    c[ImGuiCol_TextDisabled] = kMuted;
    c[ImGuiCol_WindowBg] = T->bg;
    c[ImGuiCol_ChildBg] = T->card;
    c[ImGuiCol_PopupBg] = T->card;
    c[ImGuiCol_Border] = Mix(T->card, T->a, 0.55f);
    c[ImGuiCol_FrameBg] = fieldBg;
    c[ImGuiCol_FrameBgHovered] = Mix(fieldBg, T->a, 0.15f);
    c[ImGuiCol_FrameBgActive] = Mix(fieldBg, T->a, 0.25f);
    c[ImGuiCol_SliderGrab] = T->a;
    c[ImGuiCol_SliderGrabActive] = Mix(T->a, { 1,1,1,1 }, 0.3f);
    c[ImGuiCol_Button] = Mix(T->card, T->a, 0.18f);
    c[ImGuiCol_ButtonHovered] = Mix(T->card, T->a, 0.35f);
    c[ImGuiCol_ButtonActive] = Mix(T->card, T->a, 0.55f);
    c[ImGuiCol_CheckMark] = T->g;
    c[ImGuiCol_ScrollbarBg] = { 0,0,0,0 };
    c[ImGuiCol_ScrollbarGrab] = Mix(T->card, T->a, 0.4f);
    c[ImGuiCol_ScrollbarGrabHovered] = T->a;
    c[ImGuiCol_ScrollbarGrabActive] = T->a;
    c[ImGuiCol_Separator] = Mix(T->card, T->a, 0.3f);
    c[ImGuiCol_TextSelectedBg] = Mix(T->bg, T->b, 0.45f);
    c[ImGuiCol_NavHighlight] = { 0,0,0,0 };
}

// ---------------- widgets ----------------
static ImFont* Big() { auto& f = ImGui::GetIO().Fonts->Fonts; return f.Size > 1 ? f[1] : f[0]; }

static bool Btn(const char* label, float w = 0, bool primary = false)
{
    if (primary)
    {
        ImGui::PushStyleColor(ImGuiCol_Button, T->a);
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, Mix(T->a, { 1,1,1,1 }, 0.35f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, Mix(T->a, { 1,1,1,1 }, 0.5f));
        ImGui::PushStyleColor(ImGuiCol_Text, kDark);
    }
    bool r = ImGui::Button(label, { w, 0 });
    if (primary) ImGui::PopStyleColor(4);
    return r;
}

static bool Danger(const char* label, float w = 0)
{
    ImGui::PushStyleColor(ImGuiCol_Button, Mix(T->card, kRed, 0.35f));
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, Mix(T->card, kRed, 0.6f));
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, kRed);
    bool r = ImGui::Button(label, { w, 0 });
    ImGui::PopStyleColor(3);
    return r;
}

static bool Chip(const char* label, bool on, float w = 0)
{
    if (on)
    {
        ImGui::PushStyleColor(ImGuiCol_Button, T->a);
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, Mix(T->a, { 1,1,1,1 }, 0.3f));
        ImGui::PushStyleColor(ImGuiCol_Text, kDark);
    }
    bool r = ImGui::Button(label, { w, 0 });
    if (on) ImGui::PopStyleColor(3);
    return r;
}

// iOS-style switch with the label on the left
static bool Toggle(const char* label, bool* v, const char* hint = nullptr)
{
    ImGui::PushID(label);
    float w = ImGui::GetContentRegionAvail().x;
    ImVec2 p = ImGui::GetCursorScreenPos();
    float h = ImGui::GetFrameHeight();
    bool clicked = ImGui::InvisibleButton("##t", { w, h });
    if (clicked) *v = !*v;
    bool hov = ImGui::IsItemHovered();
    ImDrawList* dl = ImGui::GetWindowDrawList();
    if (hov) dl->AddRectFilled(p, { p.x + w, p.y + h }, U(Mix(T->card, T->a, 0.10f)), 8);
    dl->AddText({ p.x + 8, p.y + (h - ImGui::GetTextLineHeight()) * 0.5f }, U(kText), label);
    if (hint)
    {
        float lw = ImGui::CalcTextSize(label).x;
        dl->AddText({ p.x + 16 + lw, p.y + (h - ImGui::GetTextLineHeight()) * 0.5f }, U(kMuted), hint);
    }
    float sw = 44, sh = 22;
    ImVec2 s{ p.x + w - sw - 6, p.y + (h - sh) * 0.5f };
    ImVec4 on = T->g;
    dl->AddRectFilled(s, { s.x + sw, s.y + sh }, U(*v ? Mix(kDark, on, 0.55f) : Mix(T->card, { 0,0,0,1 }, 0.35f)), sh * 0.5f);
    dl->AddRect(s, { s.x + sw, s.y + sh }, U(*v ? on : Mix(T->card, T->a, 0.4f)), sh * 0.5f, 0, 1.5f);
    float kx = *v ? s.x + sw - sh * 0.5f : s.x + sh * 0.5f;
    dl->AddCircleFilled({ kx, s.y + sh * 0.5f }, sh * 0.5f - 4, U(*v ? on : kMuted));
    ImGui::PopID();
    return clicked;
}

static bool Slider(const char* label, float* v, float lo, float hi, const char* fmt)
{
    ImGui::PushID(label);
    ImGui::TextUnformatted(label);
    ImGui::SameLine();
    char buf[32]; snprintf(buf, sizeof buf, fmt, *v);
    float tw = ImGui::CalcTextSize(buf).x;
    ImGui::SetCursorPosX(ImGui::GetCursorPosX() + ImGui::GetContentRegionAvail().x - tw - 4);
    ImGui::TextColored(T->b, "%s", buf);
    ImGui::SetNextItemWidth(-1);
    bool r = ImGui::SliderFloat("##s", v, lo, hi, "");
    ImGui::PopID();
    return r;
}

static bool BeginCard(const char* title)
{
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, { 12, 10 });
    bool open = ImGui::BeginChild(title, { 0, 0 }, ImGuiChildFlags_Border | ImGuiChildFlags_AutoResizeY | ImGuiChildFlags_AlwaysUseWindowPadding);
    ImGui::PopStyleVar();
    ImGui::PushFont(Big());
    ImGui::SetWindowFontScale(0.72f);
    ImGui::TextColored(T->a, "%s", title);
    ImGui::SetWindowFontScale(1.0f);
    ImGui::PopFont();
    return open;
}
static void EndCard() { ImGui::EndChild(); ImGui::Spacing(); }

static void Note(const char* text) { ImGui::PushTextWrapPos(0); ImGui::TextColored(kMuted, "%s", text); ImGui::PopTextWrapPos(); }

// equal-width buttons in one row
static float Col(int n) { return (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x * (n - 1)) / n; }

static std::string Money(long long v)
{
    char b[32]; snprintf(b, sizeof b, "%lld", v < 0 ? -v : v);
    std::string s(b), o;
    int n = 0;
    for (int i = (int)s.size() - 1; i >= 0; i--) { o.insert(o.begin(), s[i]); if (++n % 3 == 0 && i > 0) o.insert(o.begin(), ','); }
    return (v < 0 ? "-$" : "$") + o;
}

// ---------------- key names ----------------
static std::string KeyName(int vk)
{
    if (vk >= VK_F1 && vk <= VK_F24) return "F" + std::to_string(vk - VK_F1 + 1);
    UINT sc = MapVirtualKeyA(vk, MAPVK_VK_TO_VSC);
    switch (vk) { case VK_INSERT: case VK_DELETE: case VK_HOME: case VK_END: case VK_PRIOR: case VK_NEXT:
        case VK_LEFT: case VK_RIGHT: case VK_UP: case VK_DOWN: sc |= 0x100; }
    char n[64] = {};
    if (GetKeyNameTextA((LONG)(sc << 16), n, sizeof n) > 0) return n;
    return "Key " + std::to_string(vk);
}

// ---------------- toast ----------------
static std::string g_toast;
static double g_toastUntil;
static void Toast(const std::string& s) { g_toast = s; g_toastUntil = ImGui::GetTime() + 2.0; }

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
    f << "Theme=" << g_set.theme << "\nKeyMenu=" << g_set.keyMenu << "\nKeyMoney=" << g_set.keyMoney << "\nKeyFly=" << g_set.keyFly
      << "\nMoneyIndex=" << g_set.moneyIndex << "\nSaveSettings=" << (int)g_set.saveSettings << "\n";
    if (g_set.saveSettings)
        f << "Speed=" << g_set.speedMul << "\nJump=" << g_set.jumpMul << "\nGravity=" << g_set.gravity
          << "\nUnlimitedJumps=" << (int)g_set.unlimitedJumps << "\nFlySpeed=" << g_set.flySpeed
          << "\nAlwaysVerifyFix=" << (int)g_set.alwaysVerifyFix << "\nInfiniteFuel=" << (int)g_set.infiniteFuel
          << "\nFreezeTime=" << (int)g_set.freezeTime << "\nWinAuctions=" << (int)g_set.brokeBidders << "\n";
}

static bool g_dirty;
static double g_dirtyAt;
static void Dirty() { g_dirty = true; g_dirtyAt = ImGui::GetTime(); }

// ---------------- hotkeys ----------------
static int* g_rebind;
static bool g_prev[256];

static bool GameFocused()
{
    DWORD pid = 0;
    GetWindowThreadProcessId(GetForegroundWindow(), &pid);
    return pid == GetCurrentProcessId();
}

static const int kMoney[] = { 1000, 10000, 100000, 1000000 };

void Menu_HandleHotkeys()
{
    bool now[256];
    bool focused = GameFocused();
    for (int k = 1; k < 256; k++) now[k] = focused && (GetAsyncKeyState(k) & 0x8000) != 0;
    auto pressed = [&](int k) { return k > 0 && k < 256 && now[k] && !g_prev[k]; };

    if (g_rebind)
    {
        for (int k = 8; k < 256; k++)
        {
            if (k == VK_SHIFT || k == VK_CONTROL || k == VK_MENU || (k >= VK_LSHIFT && k <= VK_RMENU)) continue;
            if (!pressed(k)) continue;
            if (k != VK_ESCAPE) { *g_rebind = k; Dirty(); }
            g_rebind = nullptr;
            break;
        }
    }
    else
    {
        if (pressed(g_set.keyMenu)) { g_menuOpen = !g_menuOpen; Game_SetMenuOpen(g_menuOpen); }
        bool typing = ImGui::GetIO().WantTextInput;
        if (!typing && g_view.inGame && g_view.allowed)
        {
            if (pressed(g_set.keyMoney)) { int a = kMoney[g_set.moneyIndex & 3]; Game_AddMoney(a); Toast("+" + Money(a)); }
            if (pressed(g_set.keyFly)) { g_set.fly = !g_set.fly; Toast(g_set.fly ? "Fly mode ON" : "Fly mode OFF"); }
        }
    }
    memcpy(g_prev, now, sizeof g_prev);
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
    if (BeginCard("Theme"))
    {
        float w = Col(3);
        for (int i = 0; i < 6; i++)
        {
            if (i % 3) ImGui::SameLine();
            if (Chip(kThemes[i].name, T == &kThemes[i], w)) { ApplyTheme(kThemes[i].name); Dirty(); }
        }
    }
    EndCard();
    if (BeginCard("Hotkeys"))
    {
        struct K { const char* name; int* key; } keys[] = { { "Open / close menu", &g_set.keyMenu }, { "Add money", &g_set.keyMoney }, { "Fly mode", &g_set.keyFly } };
        for (auto& k : keys)
        {
            ImGui::PushID(k.name);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(k.name);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 150);
            std::string lbl = g_rebind == k.key ? "press a key..." : KeyName(*k.key);
            if (Chip(lbl.c_str(), g_rebind == k.key, 150)) g_rebind = k.key;
            ImGui::PopID();
        }
        Note("Click a key, then press the new key. Esc cancels.");
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
    if (g_dirty && ImGui::GetTime() - g_dirtyAt > 0.5) { g_dirty = false; Menu_SaveSettings(); }

    GameView v;
    { std::lock_guard<std::mutex> lk(g_viewLock); v = g_view; }

    ImGuiIO& io = ImGui::GetIO();
    io.MouseDrawCursor = g_menuOpen;

    // toast (hotkeys) - also while the menu is closed
    if (ImGui::GetTime() < g_toastUntil)
    {
        ImDrawList* dl = ImGui::GetForegroundDrawList();
        ImGui::PushFont(Big());
        ImVec2 ts = ImGui::CalcTextSize(g_toast.c_str());
        ImVec2 p{ (io.DisplaySize.x - ts.x) * 0.5f, io.DisplaySize.y * 0.12f };
        dl->AddRectFilled({ p.x - 18, p.y - 10 }, { p.x + ts.x + 18, p.y + ts.y + 10 }, U(T->bg), 12);
        dl->AddRect({ p.x - 18, p.y - 10 }, { p.x + ts.x + 18, p.y + ts.y + 10 }, U(T->a), 12, 0, 2);
        dl->AddText(p, U(kText), g_toast.c_str());
        ImGui::PopFont();
    }

    if (!g_menuOpen) return;

    float h = io.DisplaySize.y - 80;
    ImGui::SetNextWindowPos({ 30, 40 }, ImGuiCond_Always);
    ImGui::SetNextWindowSize({ 520, h > 900 ? 900 : h }, ImGuiCond_Always);
    ImGui::Begin("EvilDragon's Cheats", nullptr, ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoSavedSettings);

    // header: logo, title, game
    ImDrawList* dl = ImGui::GetWindowDrawList();
    ImVec2 wp = ImGui::GetWindowPos(), ws = ImGui::GetWindowSize();
    dl->AddRectFilledMultiColor(wp, { wp.x + ws.x, wp.y + 4 }, U(T->a), U(T->b), U(T->b), U(T->a));
    if (void* logo = Render_LogoTexture()) { ImGui::Image((ImTextureID)logo, { 52, 52 }); ImGui::SameLine(); }
    ImGui::BeginGroup();
    ImGui::PushFont(Big());
    ImGui::TextColored(T->a, "EvilDragon's Cheats");
    ImGui::PopFont();
    ImGui::TextColored(kMuted, "Storage Hunter Simulator  -  %s to close", KeyName(g_set.keyMenu).c_str());
    ImGui::EndGroup();
    ImGui::Spacing();

    // tabs (two rows: 4 + 3)
    float w = Col(4);
    for (int i = 0; i < kTabCount; i++)
    {
        if (i == 4) w = Col(3);
        else if (i) ImGui::SameLine();
        if (g_tab == i)
        {
            ImGui::PushStyleColor(ImGuiCol_Button, T->b);
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, Mix(T->b, { 1,1,1,1 }, 0.3f));
            ImGui::PushStyleColor(ImGuiCol_Text, kDark);
        }
        if (ImGui::Button(kTabs[i], { w, 32 })) g_tab = i;
        if (g_tab == i) ImGui::PopStyleColor(3);
    }
    ImGui::Spacing();

    if (!v.inGame && g_tab != kSettingsTab)
    {
        ImGui::PushStyleColor(ImGuiCol_ChildBg, Mix(T->card, T->a, 0.12f));
        if (BeginCard("Not in a game")) Note("Load a save game to use the cheats.");
        EndCard();
        ImGui::PopStyleColor();
    }
    else if (!v.allowed && g_tab != kSettingsTab)
    {
        ImGui::PushStyleColor(ImGuiCol_ChildBg, Mix(T->card, kRed, 0.25f));
        if (BeginCard("Cheats are disabled")) Note("Other players are in your session. The cheats only work in single-player, so everything is switched off.");
        EndCard();
        ImGui::PopStyleColor();
    }

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
    ImGui::End();
}
