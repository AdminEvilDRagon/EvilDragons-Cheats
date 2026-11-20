// EvilDragon's Cheats - shared pieces of the Unreal Engine mods.
// Each game defines ED_GAME_NAME, ED_FILE_BASE and ED_VERSION (see the game's cheats.h) and implements
// Game_Init, Menu_LoadSettings, Menu_HandleHotkeys and Menu_Draw.
#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <functional>
#include <mutex>
#include <string>
#include <vector>
#include "imgui.h"
#include "ed_game.h"

extern HMODULE g_self;
extern bool g_menuOpen;

// overlay (render.cpp)
void* Render_LogoTexture();

// implemented by the game
void Game_Init();
void Menu_LoadSettings();
void Menu_HandleHotkeys();
void Menu_Draw();

// log + files (next to the mod dll)
std::string ModDir();
void Log(const char* fmt, ...);

// look
struct Theme { const char* name; ImVec4 a, b, g, bg, card; };
extern const Theme kThemes[6];
extern const Theme* T;
extern const ImVec4 kText, kMuted, kRed, kDark;
extern std::string g_themeName;
ImVec4 Mix(ImVec4 x, ImVec4 y, float t);
ImU32 U(ImVec4 c);
void ApplyTheme(const std::string& name);
ImFont* Big();

// widgets
bool Btn(const char* label, float w = 0, bool primary = false);
bool Danger(const char* label, float w = 0);
bool Chip(const char* label, bool on, float w = 0);
bool Toggle(const char* label, bool* v, const char* hint = nullptr);
bool Slider(const char* label, float* v, float lo, float hi, const char* fmt);
bool BeginCard(const char* title);
void EndCard();
void Note(const char* text);
float Col(int n);
std::string Money(long long v);
std::string KeyName(int vk);

// toast, settings save, hotkeys
void Toast(const std::string& s);
void DrawToast();
void Dirty();
bool SaveDue();
bool GameFocused();
void ReadKeys();
bool Pressed(int vk);
bool HandleRebind();
extern int* g_rebind;

// frame
struct HotkeyEntry { const char* name; int* key; };
void ThemeCard();
void HotkeysCard(const HotkeyEntry* keys, int count);
void BeginMenuWindow(const char* game, int keyMenu, const char* const* tabs, int count, int firstRow, int* tab);
void StatusCard(bool inGame, bool allowed);
void EndMenuWindow();
bool MenuVisible(bool open);   // drives the open/close animation; draw the window while true
