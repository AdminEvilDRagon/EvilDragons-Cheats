// EvilDragon's Cheats - shared ImGui look for the Unreal Engine mods (themes, widgets, log, toast).
#include "ed_ui.h"
#include <cstdio>
#include <cstdarg>
#include "imgui_internal.h"

std::string g_themeName = "Dragon";

// ---------------- paths / log ----------------
std::string ModDir()
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
    FILE* f = fopen((ModDir() + ED_FILE_BASE ".log").c_str(), first ? "w" : "a");
    first = false;
    if (!f) return;
    SYSTEMTIME t; GetLocalTime(&t);
    fprintf(f, "[%02d:%02d:%02d] ", t.wHour, t.wMinute, t.wSecond);
    va_list a; va_start(a, fmt); vfprintf(f, fmt, a); va_end(a);
    fputc('\n', f);
    fclose(f);
}

// ---------------- themes ----------------
const Theme kThemes[6] = {
    { "Dragon",  {0.10f,1.00f,0.30f,1}, {0.65f,1.00f,0.20f,1}, {0.10f,1.00f,0.30f,1}, {0.020f,0.045f,0.030f,0.97f}, {0.060f,0.110f,0.075f,1} },
    { "Neon",    {0.00f,0.94f,1.00f,1}, {1.00f,0.18f,0.75f,1}, {0.22f,1.00f,0.55f,1}, {0.035f,0.035f,0.075f,0.97f}, {0.085f,0.085f,0.150f,1} },
    { "Inferno", {1.00f,0.45f,0.10f,1}, {1.00f,0.15f,0.25f,1}, {1.00f,0.80f,0.20f,1}, {0.060f,0.025f,0.020f,0.97f}, {0.140f,0.065f,0.050f,1} },
    { "Ocean",   {0.20f,0.65f,1.00f,1}, {0.00f,1.00f,0.85f,1}, {0.20f,1.00f,0.80f,1}, {0.020f,0.040f,0.075f,0.97f}, {0.050f,0.095f,0.160f,1} },
    { "Purple",  {0.70f,0.40f,1.00f,1}, {1.00f,0.35f,0.85f,1}, {0.55f,1.00f,0.75f,1}, {0.045f,0.025f,0.075f,0.97f}, {0.110f,0.065f,0.165f,1} },
    { "Gold",    {1.00f,0.80f,0.20f,1}, {1.00f,0.55f,0.10f,1}, {0.65f,1.00f,0.35f,1}, {0.050f,0.042f,0.025f,0.97f}, {0.125f,0.105f,0.060f,1} },
};
const Theme* T = &kThemes[0];
const ImVec4 kText{ 0.95f,0.96f,1.00f,1 }, kMuted{ 0.72f,0.76f,0.86f,1 }, kRed{ 1.00f,0.20f,0.36f,1 }, kDark{ 0.03f,0.03f,0.05f,1 };

ImVec4 Mix(ImVec4 x, ImVec4 y, float t) { return { x.x + (y.x - x.x) * t, x.y + (y.y - x.y) * t, x.z + (y.z - x.z) * t, x.w + (y.w - x.w) * t }; }
ImU32 U(ImVec4 c) { return ImGui::GetColorU32(c); }

void ApplyTheme(const std::string& name)
{
    extern std::string g_themeName;
    T = &kThemes[0];
    for (auto& t : kThemes) if (_stricmp(t.name, name.c_str()) == 0) T = &t;
    g_themeName = T->name;

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
ImFont* Big() { auto& f = ImGui::GetIO().Fonts->Fonts; return f.Size > 1 ? f[1] : f[0]; }

bool Btn(const char* label, float w, bool primary)
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

bool Danger(const char* label, float w)
{
    ImGui::PushStyleColor(ImGuiCol_Button, Mix(T->card, kRed, 0.35f));
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, Mix(T->card, kRed, 0.6f));
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, kRed);
    bool r = ImGui::Button(label, { w, 0 });
    ImGui::PopStyleColor(3);
    return r;
}

bool Chip(const char* label, bool on, float w)
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
bool Toggle(const char* label, bool* v, const char* hint)
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

bool Slider(const char* label, float* v, float lo, float hi, const char* fmt)
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

bool BeginCard(const char* title)
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
void EndCard() { ImGui::EndChild(); ImGui::Spacing(); }

void Note(const char* text) { ImGui::PushTextWrapPos(0); ImGui::TextColored(kMuted, "%s", text); ImGui::PopTextWrapPos(); }

// equal-width buttons in one row
float Col(int n) { return (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x * (n - 1)) / n; }

std::string Money(long long v)
{
    char b[32]; snprintf(b, sizeof b, "%lld", v < 0 ? -v : v);
    std::string s(b), o;
    int n = 0;
    for (int i = (int)s.size() - 1; i >= 0; i--) { o.insert(o.begin(), s[i]); if (++n % 3 == 0 && i > 0) o.insert(o.begin(), ','); }
    return (v < 0 ? "-$" : "$") + o;
}

// ---------------- key names ----------------
std::string KeyName(int vk)
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
std::string g_toast;
double g_toastUntil;
void Toast(const std::string& s) { g_toast = s; g_toastUntil = ImGui::GetTime() + 2.0; }

bool g_dirty;
double g_dirtyAt;
void Dirty() { g_dirty = true; g_dirtyAt = ImGui::GetTime(); }

// ---------------- hotkeys ----------------
int* g_rebind;
bool g_prev[256];

bool GameFocused()
{
    DWORD pid = 0;
    GetWindowThreadProcessId(GetForegroundWindow(), &pid);
    return pid == GetCurrentProcessId();
}


void DrawToast()
{
    if (ImGui::GetTime() >= g_toastUntil) return;
    ImGuiIO& io = ImGui::GetIO();
    ImDrawList* dl = ImGui::GetForegroundDrawList();
    ImGui::PushFont(Big());
    ImVec2 ts = ImGui::CalcTextSize(g_toast.c_str());
    ImVec2 p{ (io.DisplaySize.x - ts.x) * 0.5f, io.DisplaySize.y * 0.12f };
    dl->AddRectFilled({ p.x - 18, p.y - 10 }, { p.x + ts.x + 18, p.y + ts.y + 10 }, U(T->bg), 12);
    dl->AddRect({ p.x - 18, p.y - 10 }, { p.x + ts.x + 18, p.y + ts.y + 10 }, U(T->a), 12, 0, 2);
    dl->AddText(p, U(kText), g_toast.c_str());
    ImGui::PopFont();
}

bool SaveDue()
{
    if (!g_dirty || ImGui::GetTime() - g_dirtyAt < 0.5) return false;
    g_dirty = false;
    return true;
}

// Edge-detected key presses, read once per frame.
static bool g_now[256];
void ReadKeys()
{
    memcpy(g_prev, g_now, sizeof g_prev);
    bool focused = GameFocused();
    for (int k = 1; k < 256; k++) g_now[k] = focused && (GetAsyncKeyState(k) & 0x8000) != 0;
}
bool Pressed(int vk) { return vk > 0 && vk < 256 && g_now[vk] && !g_prev[vk]; }

// Returns true while a key is being rebound (the caller then skips its own hotkeys).
bool HandleRebind()
{
    if (!g_rebind) return false;
    for (int k = 8; k < 256; k++)
    {
        if (k == VK_SHIFT || k == VK_CONTROL || k == VK_MENU || (k >= VK_LSHIFT && k <= VK_RMENU)) continue;
        if (!Pressed(k)) continue;
        if (k != VK_ESCAPE) { *g_rebind = k; Dirty(); }
        g_rebind = nullptr;
        break;
    }
    return true;
}

void ThemeCard()
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
}

void HotkeysCard(const HotkeyEntry* keys, int count)
{
    if (BeginCard("Hotkeys"))
    {
        for (int i = 0; i < count; i++)
        {
            ImGui::PushID(i);
            ImGui::AlignTextToFramePadding();
            ImGui::TextUnformatted(keys[i].name);
            ImGui::SameLine(ImGui::GetContentRegionAvail().x - 150);
            std::string lbl = g_rebind == keys[i].key ? "press a key..." : KeyName(*keys[i].key);
            if (Chip(lbl.c_str(), g_rebind == keys[i].key, 150)) g_rebind = keys[i].key;
            ImGui::PopID();
        }
        Note("Click a key, then press the new key. Esc cancels.");
    }
    EndCard();
}

// Open / close animation: the window fades in and slides in from the left.
static float g_menuAnim;      // 0 = closed, 1 = open
static bool g_menuAnimOpen;
bool MenuVisible(bool open)
{
    float dt = ImGui::GetIO().DeltaTime;
    if (dt <= 0 || dt > 0.1f) dt = 0.016f;
    g_menuAnimOpen = open;
    g_menuAnim += (open ? dt / 0.18f : -dt / 0.14f);
    if (g_menuAnim < 0) g_menuAnim = 0;
    if (g_menuAnim > 1) g_menuAnim = 1;
    return g_menuAnim > 0;
}

// Window frame: gradient bar, logo, title, tabs. Returns the content area (call EndMenuWindow after).
void BeginMenuWindow(const char* game, int keyMenu, const char* const* tabs, int count, int firstRow, int* tab)
{
    ImGuiIO& io = ImGui::GetIO();
    float h = io.DisplaySize.y - 80;
    float t = g_menuAnim, e = 1 - (1 - t) * (1 - t) * (1 - t);   // ease out
    if (!g_menuAnimOpen) e = t * t;                              // close a bit faster at the end
    ImGui::PushStyleVar(ImGuiStyleVar_Alpha, e);
    ImGui::SetNextWindowPos({ 30 - (1 - e) * 70, 40 + (1 - e) * 12 }, ImGuiCond_Always);
    ImGui::SetNextWindowSize({ 520, h > 900 ? 900 : h }, ImGuiCond_Always);
    ImGuiWindowFlags fl = ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoSavedSettings;
    if (!g_menuAnimOpen) fl |= ImGuiWindowFlags_NoInputs;
    ImGui::Begin("EvilDragon's Cheats", nullptr, fl);

    ImDrawList* dl = ImGui::GetWindowDrawList();
    ImVec2 wp = ImGui::GetWindowPos(), ws = ImGui::GetWindowSize();
    dl->AddRectFilledMultiColor(wp, { wp.x + ws.x, wp.y + 4 }, U(T->a), U(T->b), U(T->b), U(T->a));
    if (void* logo = Render_LogoTexture()) { ImGui::Image((ImTextureID)logo, { 52, 52 }); ImGui::SameLine(); }
    ImGui::BeginGroup();
    ImGui::PushFont(Big());
    ImGui::TextColored(T->a, "EvilDragon's Cheats");
    ImGui::PopFont();
    ImGui::TextColored(kMuted, "%s  -  %s to close", game, KeyName(keyMenu).c_str());
    ImGui::EndGroup();
    ImGui::Spacing();

    float w = Col(firstRow);
    for (int i = 0; i < count; i++)
    {
        if (i == firstRow) w = Col(count - firstRow);
        else if (i) ImGui::SameLine();
        bool on = *tab == i;
        if (on)
        {
            ImGui::PushStyleColor(ImGuiCol_Button, T->b);
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, Mix(T->b, { 1,1,1,1 }, 0.3f));
            ImGui::PushStyleColor(ImGuiCol_Text, kDark);
        }
        if (ImGui::Button(tabs[i], { w, 32 })) *tab = i;
        if (on) ImGui::PopStyleColor(3);
    }
    ImGui::Spacing();
}

void StatusCard(bool inGame, bool allowed)
{
    if (!inGame)
    {
        ImGui::PushStyleColor(ImGuiCol_ChildBg, Mix(T->card, T->a, 0.12f));
        if (BeginCard("Not in a game")) Note("Load a save game to use the cheats.");
        EndCard();
        ImGui::PopStyleColor();
    }
    else if (!allowed)
    {
        ImGui::PushStyleColor(ImGuiCol_ChildBg, Mix(T->card, kRed, 0.25f));
        if (BeginCard("Cheats are disabled")) Note("Other players are in your session. The cheats only work in single-player, so everything is switched off.");
        EndCard();
        ImGui::PopStyleColor();
    }
}

void EndMenuWindow() { ImGui::End(); ImGui::PopStyleVar(); }
