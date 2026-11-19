// EvilDragon's Cheats - loader
// Pretends to be dxgi.dll: forwards every call to the real Windows dxgi.dll
// and loads every .dll from the "EvilDragon" folder next to the game exe.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>

extern "C" void* g_real[20];
#include "names.inc"

static void LoadRealDxgi()
{
    wchar_t sys[MAX_PATH];
    GetSystemDirectoryW(sys, MAX_PATH);
    std::wstring path = std::wstring(sys) + L"\\dxgi.dll";
    HMODULE real = LoadLibraryW(path.c_str());
    if (!real) return;
    for (int i = 0; i < 20; i++) g_real[i] = (void*)GetProcAddress(real, kNames[i]);
}

static BOOL CALLBACK FindGameWindow(HWND hwnd, LPARAM found)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    wchar_t cls[64] = {};
    GetClassNameW(hwnd, cls, 64);
    if (pid == GetCurrentProcessId() && IsWindowVisible(hwnd) && wcscmp(cls, L"UnrealWindow") == 0)
    {
        *(bool*)found = true;
        return FALSE;
    }
    return TRUE;
}

static DWORD WINAPI LoadMods(LPVOID)
{
    // Wait until the engine is up (game window visible), then a few more seconds.
    bool found = false;
    while (!found) { Sleep(500); EnumWindows(FindGameWindow, (LPARAM)&found); }
    Sleep(8000);

    wchar_t exe[MAX_PATH];
    GetModuleFileNameW(nullptr, exe, MAX_PATH);
    std::wstring dir(exe);
    dir = dir.substr(0, dir.find_last_of(L"\\/")) + L"\\EvilDragon\\";

    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW((dir + L"*.dll").c_str(), &fd);
    if (h == INVALID_HANDLE_VALUE) return 0;
    do { LoadLibraryW((dir + fd.cFileName).c_str()); } while (FindNextFileW(h, &fd));
    FindClose(h);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(inst);
        LoadRealDxgi();
        if (HANDLE t = CreateThread(nullptr, 0, LoadMods, nullptr, 0, nullptr)) CloseHandle(t);
    }
    return TRUE;
}
