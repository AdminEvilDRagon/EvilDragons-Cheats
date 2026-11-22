// EvilDragon's Cheats - shared ImGui look for the Unreal Engine mods (themes, widgets, log, toast).
#include "ed_ui.h"
#include <algorithm>
#include <mmsystem.h>
#pragma comment(lib, "winmm.lib")
#include <cmath>
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
// Notifications: liquid-glass cards in the bottom-left corner, slide in, fade out, soft "thock" sound.
// Safe to call from any thread.
struct Notice { std::string text; bool error; ULONGLONG born; float slot; };
static std::mutex g_noticeLock;
static std::vector<Notice> g_notices;
static const ULONGLONG kNoticeLife = 3400;
bool g_notifySound = true;

// a short, muffled click: two low sine partials with a fast decay, built once as a WAV in memory
static std::vector<unsigned char> MakeThock(float pitch)
{
    const int rate = 44100, n = rate * 14 / 100;
    std::vector<unsigned char> w(44 + n * 2);
    auto put32 = [&](int at, uint32_t v) { memcpy(&w[at], &v, 4); };
    auto put16 = [&](int at, uint16_t v) { memcpy(&w[at], &v, 2); };
    memcpy(&w[0], "RIFF", 4); put32(4, (uint32_t)w.size() - 8); memcpy(&w[8], "WAVEfmt ", 8);
    put32(16, 16); put16(20, 1); put16(22, 1); put32(24, rate); put32(28, rate * 2); put16(32, 2); put16(34, 16);
    memcpy(&w[36], "data", 4); put32(40, n * 2);
    float lp = 0;
    for (int i = 0; i < n; i++)
    {
        float t = (float)i / rate;
        float env = expf(-t * 38.0f) * (1 - expf(-t * 900.0f));
        float f = pitch * (1.0f + 0.35f * expf(-t * 60.0f));          // tiny downward bend = "dumpf"
        float x = sinf(6.2831853f * f * t) + 0.35f * sinf(6.2831853f * f * 2.01f * t);
        lp += (x - lp) * 0.18f;                                          // low-pass keeps it soft
        short v = (short)(lp * env * 9000);
        put16(44 + i * 2, (uint16_t)v);
    }
    return w;
}

static void PlayThock(bool error)
{
    if (!g_notifySound) return;
    static std::vector<unsigned char> ok = MakeThock(300), bad = MakeThock(190);
    PlaySoundA((LPCSTR)(error ? bad : ok).data(), nullptr, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
}

void Notify(const std::string& s, bool error)
{
    if (s.empty()) return;
    {
        std::lock_guard<std::mutex> lk(g_noticeLock);
        bool again = false;
        for (auto& n : g_notices) if (n.text == s && n.error == error) { n.born = GetTickCount64(); again = true; }
        if (!again)
        {
            g_notices.push_back({ s, error, GetTickCount64(), -1.0f });
            if (g_notices.size() > 5) g_notices.erase(g_notices.begin());
        }
    }
    PlayThock(error);
}
void Toast(const std::string& s) { Notify(s, false); }

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


static float Ease(float t) { t = t < 0 ? 0 : t > 1 ? 1 : t; return 1 - (1 - t) * (1 - t) * (1 - t); }
static ImU32 Rgba(float r, float g, float b, float a) { return ImGui::ColorConvertFloat4ToU32({ r, g, b, a }); }

void DrawToast()
{
    std::lock_guard<std::mutex> lk(g_noticeLock);
    ULONGLONG now = GetTickCount64();
    g_notices.erase(std::remove_if(g_notices.begin(), g_notices.end(), [&](const Notice& n) { return now - n.born > kNoticeLife; }), g_notices.end());
    if (g_notices.empty()) return;
    ImGuiIO& io = ImGui::GetIO();
    ImDrawList* dl = ImGui::GetForegroundDrawList();
    const float pad = 14, gap = 10, margin = 26, icon = 26, round = 16, wrap = 400;
    float y = io.DisplaySize.y - margin;
    float dt = io.DeltaTime > 0 ? io.DeltaTime : 1.0f / 60;
    for (int i = (int)g_notices.size() - 1; i >= 0; i--)
    {
        Notice& n = g_notices[i];
        float age = (now - n.born) / 1000.0f, life = kNoticeLife / 1000.0f;
        float in = Ease(age / 0.42f), out = Ease((life - age) / 0.5f);
        float al = in < out ? in : out;
        ImVec2 ts = ImGui::CalcTextSize(n.text.c_str(), nullptr, false, wrap);
        float h = (ts.y > icon ? ts.y : icon) + pad * 2, w = ts.x + icon + pad * 3;
        float target = y - h;
        n.slot = n.slot < 0 ? target + 18 : n.slot + (target - n.slot) * (1 - expf(-dt * 12));
        float x = margin - (1 - in) * 46;
        ImVec2 a{ x, n.slot }, b{ x + w, n.slot + h };
        ImVec4 ac = n.error ? kRed : T->a;

        // soft shadow
        for (int k = 3; k >= 1; k--)
            dl->AddRectFilled({ a.x - k * 2.0f, a.y - k + 6 }, { b.x + k * 2.0f, b.y + k * 3.0f + 6 }, Rgba(0, 0, 0, 0.07f * al), round + k * 3);
        // glass body: dark translucent, lighter at the top, tinted with the accent
        dl->AddRectFilled(a, b, Rgba(0.06f + ac.x * 0.05f, 0.07f + ac.y * 0.05f, 0.09f + ac.z * 0.05f, 0.62f * al), round);
        dl->AddRectFilledMultiColor({ a.x + 2, a.y + 2 }, { b.x - 2, a.y + h * 0.5f },
                                    Rgba(1, 1, 1, 0.10f * al), Rgba(1, 1, 1, 0.10f * al), Rgba(1, 1, 1, 0.0f), Rgba(1, 1, 1, 0.0f));
        // accent glow from the left
        dl->AddRectFilledMultiColor({ a.x + 2, a.y + 2 }, { a.x + w * 0.45f, b.y - 2 },
                                    Rgba(ac.x, ac.y, ac.z, 0.16f * al), Rgba(ac.x, ac.y, ac.z, 0.0f), Rgba(ac.x, ac.y, ac.z, 0.0f), Rgba(ac.x, ac.y, ac.z, 0.16f * al));
        // rim: bright top highlight, faint everywhere else
        dl->AddRect(a, b, Rgba(1, 1, 1, 0.16f * al), round, 0, 1.0f);
        dl->PathArcTo({ a.x + round, a.y + round }, round - 0.5f, 3.14159f, 4.71239f, 10);
        dl->PathArcTo({ b.x - round, a.y + round }, round - 0.5f, 4.71239f, 6.28318f, 10);
        dl->PathStroke(Rgba(1, 1, 1, 0.38f * al), 0, 1.2f);

        // icon bubble with a check or a cross
        ImVec2 c{ a.x + pad + icon * 0.5f, (a.y + b.y) * 0.5f };
        dl->AddCircleFilled(c, icon * 0.5f, Rgba(ac.x, ac.y, ac.z, 0.22f * al), 24);
        dl->AddCircle(c, icon * 0.5f, Rgba(ac.x, ac.y, ac.z, 0.85f * al), 24, 1.5f);
        ImU32 ic = Rgba(ac.x * 0.4f + 0.6f, ac.y * 0.4f + 0.6f, ac.z * 0.4f + 0.6f, al);
        if (n.error)
        {
            float r = icon * 0.2f;
            dl->AddLine({ c.x - r, c.y - r }, { c.x + r, c.y + r }, ic, 2.0f);
            dl->AddLine({ c.x - r, c.y + r }, { c.x + r, c.y - r }, ic, 2.0f);
        }
        else
        {
            ImVec2 pts[3] = { { c.x - icon * 0.22f, c.y + 0.5f }, { c.x - icon * 0.05f, c.y + icon * 0.17f }, { c.x + icon * 0.24f, c.y - icon * 0.16f } };
            dl->AddPolyline(pts, 3, ic, 0, 2.0f);
        }
        // text
        dl->AddText(ImGui::GetFont(), ImGui::GetFontSize(), { a.x + pad * 2 + icon, a.y + (h - ts.y) * 0.5f }, Rgba(kText.x, kText.y, kText.z, al), n.text.c_str(), nullptr, wrap);
        // time left as a thin line inside the bottom edge
        float left = 1 - age / life;
        if (left > 0) dl->AddRectFilled({ a.x + round, b.y - 3.0f }, { a.x + round + (w - round * 2) * left, b.y - 1.5f }, Rgba(ac.x, ac.y, ac.z, 0.5f * al), 1);
        y = n.slot - gap;
    }
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
