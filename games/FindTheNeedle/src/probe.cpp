// Find The Needle - structure probe (development tool, not shipped).
// Stands in for libterrain (Terrain3D): forwards the extension init to the real DLL, then dumps the
// game's autoloads, scene tree and script variables to EvilDragon\FindTheNeedle-probe-<n>.txt.
// A dump is written ~10 s after start and every time F8 is pressed.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstdio>
#include <string>
#include "gd.h"

using namespace gd;

static void* g_library;
static FILE* g_out;
static int g_lines;

static std::wstring Dir()
{
    wchar_t exe[MAX_PATH];
    GetModuleFileNameW(nullptr, exe, MAX_PATH);
    std::wstring d(exe);
    return d.substr(0, d.find_last_of(L"\\/") + 1);
}

static void Out(const std::string& s)
{
    if (!g_out || g_lines > 60000) return;
    fputs(s.c_str(), g_out); fputc('\n', g_out);
    g_lines++;
}

static std::string Short(std::string s, size_t n = 160)
{
    for (char& c : s) if (c == '\n' || c == '\r') c = ' ';
    return s.size() > n ? s.substr(0, n) + "..." : s;
}

static V Key(const V& dict, const char* k) { V key = FromStr(k); return Call(dict, "get", { &key }); }

static void DumpScript(const V& node, const std::string& pad)
{
    V script = Call(node, "get_script");
    if (script.nil()) return;
    V props = Call(node, "get_property_list");
    int64_t n = Size(props);
    for (int64_t i = 0; i < n; i++)
    {
        V d = Index(props, i);
        int64_t usage = ToInt(Key(d, "usage"));
        if (!(usage & 4096)) continue;   // PROPERTY_USAGE_SCRIPT_VARIABLE
        std::string name = Text(Key(d, "name"));
        V val = Get(node, name.c_str());
        Out(pad + "  var " + name + " = " + Short(Text(val)) + "   (type " + std::to_string(val.type()) + ")");
    }
    V methods = Call(script, "get_script_method_list");
    int64_t m = Size(methods);
    std::string line;
    for (int64_t i = 0; i < m; i++)
    {
        V d = Index(methods, i);
        std::string name = Text(Key(d, "name"));
        V args = Key(d, "args");
        std::string an;
        for (int64_t j = 0; j < Size(args); j++) { if (j) an += ","; an += Text(Key(Index(args, j), "name")); }
        line += name + "(" + an + ") ";
        if (line.size() > 200) { Out(pad + "  func " + line); line.clear(); }
    }
    if (!line.empty()) Out(pad + "  func " + line);
    V sigs = Call(script, "get_script_signal_list");
    int64_t s = Size(sigs);
    line.clear();
    for (int64_t i = 0; i < s; i++) line += Text(Key(Index(sigs, i), "name")) + " ";
    if (!line.empty()) Out(pad + "  signal " + line);
}

static int g_nodes;
static void DumpNode(const V& node, int depth, int maxDepth, bool detail)
{
    if (++g_nodes > 6000) return;
    std::string pad(depth * 2, ' ');
    V script = Call(node, "get_script");
    std::string path = script.nil() ? "" : Text(Get(script, "resource_path"));
    std::string extra;
    V pos = Get(node, "global_position");
    if (!pos.nil()) extra = "  @" + Text(pos);
    Out(pad + Text(Call(node, "get_name")) + " [" + Text(Call(node, "get_class")) + "]" + (path.empty() ? "" : "  " + path) + extra);
    if (detail && !script.nil()) DumpScript(node, pad);
    if (depth >= maxDepth) return;
    V kids = Call(node, "get_children");
    int64_t n = Size(kids);
    for (int64_t i = 0; i < n; i++) DumpNode(Index(kids, i), depth + 1, maxDepth, detail);
}

// constants of a script (tables of techs, tools, prices ...)
static void DumpConstants(const char* title, const V& script)
{
    if (script.nil()) return;
    Out(std::string("\n==== constants of ") + title + "  " + Text(Get(script, "resource_path")) + " ====");
    V map = Call(script, "get_script_constant_map");
    V keys = Call(map, "keys");
    int64_t n = Size(keys);
    for (int64_t i = 0; i < n; i++)
    {
        V k = Index(keys, i);
        V v = Call(map, "get", { &k });
        Out("  const " + Text(k) + " = " + Short(Text(v), 20000));
    }
}

// plain objects (RefCounted helpers like stamina / jetpack)
static void DumpObject(const char* title, const V& obj)
{
    if (obj.nil()) return;
    Out(std::string("\n==== object ") + title + " [" + Text(Call(obj, "get_class")) + "] ====");
    V props = Call(obj, "get_property_list");
    int64_t n = Size(props);
    for (int64_t i = 0; i < n; i++)
    {
        V d = Index(props, i);
        int64_t usage = ToInt(Key(d, "usage"));
        if (!(usage & 4096)) continue;
        std::string name = Text(Key(d, "name"));
        Out("  var " + name + " = " + Short(Text(Get(obj, name.c_str()))));
    }
    V script = Call(obj, "get_script");
    if (!script.nil())
    {
        V methods = Call(script, "get_script_method_list");
        std::string line;
        for (int64_t i = 0; i < Size(methods); i++) line += Text(Key(Index(methods, i), "name")) + " ";
        Out("  func " + line);
    }
}

static V Child(const V& node, const char* path) { V p = FromStr(path); return Call(node, "get_node_or_null", { &p }); }

static void Dump(const char* why)
{
    static int count;
    std::wstring dir = Dir() + L"EvilDragon\\";
    CreateDirectoryW(dir.c_str(), nullptr);
    std::wstring file = dir + L"FindTheNeedle-probe-" + std::to_wstring(++count) + L".txt";
    g_out = _wfopen(file.c_str(), L"w");
    g_lines = 0; g_nodes = 0;
    Out(std::string("dump: ") + why);
    V engine = Singleton("Engine");
    V tree = Call(engine, "get_main_loop");
    if (tree.nil()) { Out("no main loop"); fclose(g_out); g_out = nullptr; return; }
    V root = Call(tree, "get_root");
    V scene = Get(tree, "current_scene");
    Out("current scene: " + (scene.nil() ? std::string("-") : Text(Call(scene, "get_name")) + "  " + Text(Get(scene, "scene_file_path"))));
    Out("\n==== root children (autoloads + scene) ====");
    V kids = Call(root, "get_children");
    int64_t n = Size(kids);
    for (int64_t i = 0; i < n; i++)
    {
        V k = Index(kids, i);
        bool isScene = !scene.nil() && ToObj(k) == ToObj(scene);
        if (isScene) continue;
        DumpNode(k, 0, 3, true);
    }
    if (!scene.nil())
    {
        Out("\n==== current scene ====");
        DumpNode(scene, 0, 7, true);
    }
    for (const char* a : { "GameState", "Tech", "Cfg", "SaveManager", "Leaderboard", "Achievements" })
        DumpConstants(a, Call(Child(root, a), "get_script"));
    V player;
    {
        V pl = FromName("player");
        V list = Call(tree, "get_nodes_in_group", { &pl });
        if (Size(list) > 0) player = Index(list, 0);
    }
    if (!player.nil())
    {
        DumpConstants("Player", Call(player, "get_script"));
        for (const char* m : { "stamina", "jetpack", "_boots", "carry", "build", "shop", "catalog", "tech_panel", "death" })
        {
            V o = Get(player, m);
            DumpObject(m, o);
            if (ToObj(o)) DumpConstants(m, Call(o, "get_script"));
        }
    }
    V loader = Singleton("ResourceLoader");
    for (const char* path : { "res://scripts/items/item_db.gd", "res://scripts/tech/tech_tree.gd", "res://scripts/needles/needle_types.gd",
                              "res://scripts/items/scrap_db.gd", "res://scripts/items/product_levels.gd", "res://scripts/items/prop_manager.gd" })
    {
        V p = FromStr(path);
        V script = Call(loader, "load", { &p });
        DumpConstants(path, script);
        if (script.nil()) continue;
        V methods = Call(script, "get_script_method_list");
        std::string line;
        for (int64_t i = 0; i < Size(methods); i++)
        {
            V d = Index(methods, i);
            V args = Key(d, "args");
            std::string an;
            for (int64_t j = 0; j < Size(args); j++) { if (j) an += ","; an += Text(Key(Index(args, j), "name")); }
            line += Text(Key(d, "name")) + "(" + an + ") ";
            if (line.size() > 200) { Out("  func " + line); line.clear(); }
        }
        if (!line.empty()) Out("  func " + line);
    }
    Out("\n==== global classes ====");
    V ps = Singleton("ProjectSettings");
    V classes = Call(ps, "get_global_class_list");
    for (int64_t i = 0; i < Size(classes); i++)
    {
        V d = Index(classes, i);
        Out("  class " + Text(Key(d, "class")) + " : " + Text(Key(d, "base")) + "  " + Text(Key(d, "path")));
    }
    Out("\n==== groups of interest ====");
    for (const char* g : { "player", "Player", "items", "item", "needle", "pickup", "interactable", "enemy", "npc" })
    {
        V gn = FromName(g);
        V nodes = Call(tree, "get_nodes_in_group", { &gn });
        int64_t c = Size(nodes);
        if (c) Out(std::string("group ") + g + ": " + std::to_string(c));
    }
    fclose(g_out);
    g_out = nullptr;
}

static int g_frame;
static bool g_f8;
static void Frame()
{
    g_frame++;
    if (g_frame == 600) Dump("10 seconds after start");
    bool f8 = (GetAsyncKeyState(VK_F8) & 0x8000) != 0;
    if (f8 && !g_f8) Dump("F8");
    g_f8 = f8;
}

using InitFn = Bool (*)(GetProc, void*, Init*);

extern "C" __declspec(dllexport) Bool terrain_3d_init(GetProc gp, void* library, Init* init)
{
    g_library = library;
    std::wstring real = Dir() + L"libterrain.windows.release.x86_64.original.dll";
    HMODULE h = LoadLibraryW(real.c_str());
    InitFn orig = h ? (InitFn)GetProcAddress(h, "terrain_3d_init") : nullptr;
    Bool ok = orig ? orig(gp, library, init) : 0;
    if (Load(gp))
    {
        static MainLoopCallbacks cb{ nullptr, nullptr, Frame };
        api.register_main_loop_callbacks(library, &cb);
    }
    return ok;
}

::BOOL WINAPI DllMain(HINSTANCE, DWORD, LPVOID) { return TRUE; }
