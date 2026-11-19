// EvilDragon's Cheats - Storage Hunter Simulator
// Loaded by the EvilDragon loader (dxgi.dll) once the game window is up.
#include "cheats.h"
#include "MinHook.h"

HMODULE g_self;
bool Render_Hook();

static DWORD WINAPI Start(LPVOID)
{
    Log("EvilDragon's Cheats v%s starting", ED_VERSION);
    if (MH_Initialize() != MH_OK) { Log("MinHook init failed"); return 0; }
    Menu_LoadSettings();
    Game_Init();
    Render_Hook();
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_self = inst;
        DisableThreadLibraryCalls(inst);
        if (HANDLE t = CreateThread(nullptr, 0, Start, nullptr, 0, nullptr)) CloseHandle(t);
    }
    return TRUE;
}
