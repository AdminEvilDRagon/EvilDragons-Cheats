using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CanEmAllCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Can 'Em All", Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "evildragon.cheats.canemall";
    public const string Version = "2.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<string> KeyMenu, KeyKill, KeyTeleport, KeySpawn, Theme;

    public override void Load()
    {
        L = Log;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", "F1", "Open/close menu (InputSystem key name, e.g. F1, K, Insert)");
        KeyKill = Config.Bind("Hotkeys", "KillAll", "K", "Kill all enemies");
        KeyTeleport = Config.Bind("Hotkeys", "TeleportAll", "T", "Teleport all creatures and cans");
        KeySpawn = Config.Bind("Hotkeys", "Spawn", "J", "Spawn selected creature");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));

        ClassInjector.RegisterTypeInIl2Cpp<CheatMenu>();
        var go = new GameObject("EvilDragonCheatMenu");
        GameObject.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        new Harmony(Guid).PatchAll(typeof(Patches));
        Log.LogInfo($"EvilDragon's Cheats v{Version} loaded");
    }
}

/// Cheat settings that "Save Settings" persists to the BepInEx config file.
static class Saved
{
    static readonly Dictionary<string, ConfigEntryBase> entries = new();

    static ConfigEntry<T> E<T>(string key, T def)
    {
        if (!entries.TryGetValue(key, out var e))
        {
            e = Plugin.Cfg.Bind("Saved Settings", key, def);
            entries[key] = e;
        }
        return (ConfigEntry<T>)e;
    }

    public static T Get<T>(string key, T def) => E(key, def).Value;
    public static void Set<T>(string key, T value) => E(key, value).Value = value;
    public static void Flush() => Plugin.Cfg.Save();
}

public static class Patches
{
    [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.Damage))]
    static bool PlayerDamage() => !CheatMenu.GodMode;

    [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.CheckDamage))]
    static bool PlayerCheckDamage() => !CheatMenu.GodMode;

    static float savedSpeed;
    [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.Move))]
    static void MovePre(Player __instance)
    {
        savedSpeed = __instance.moveSpeedMultiplier;
        __instance.moveSpeedMultiplier = savedSpeed * CheatMenu.SpeedMult;
    }
    [HarmonyPostfix, HarmonyPatch(typeof(Player), nameof(Player.Move))]
    static void MovePost(Player __instance) => __instance.moveSpeedMultiplier = savedSpeed;

    [HarmonyPrefix, HarmonyPatch(typeof(Enemy), nameof(Enemy.Damage))]
    static void EnemyDamage(ref float damage)
    {
        if (CheatMenu.OneHit) damage = 999999f;
    }

    // HUD: show ∞ instead of the ammo count while infinite ammo is on
    [HarmonyPostfix, HarmonyPatch(typeof(BulletCounter), nameof(BulletCounter.UpdateBulletCount))]
    static void BulletCountPost(BulletCounter __instance) => CheatMenu.ApplyInfinity(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(BulletCounter), nameof(BulletCounter.UpdateFuelCount))]
    static void FuelCountPost(BulletCounter __instance) => CheatMenu.ApplyInfinity(__instance);

    [HarmonyPrefix, HarmonyPatch(typeof(MoneyManager), nameof(MoneyManager.AddMoney))]
    static void AddMoneyPre(ref int amount)
    {
        if (CheatMenu.MoneyMult > 1 && amount > 0)
            amount = (int)Math.Min((long)amount * CheatMenu.MoneyMult, int.MaxValue);
    }
}

// ---------------------------------------------------------------------------------
// IMGUI helpers. The game strips parts of IMGUI, so non-trivial calls are guarded and
// fall back to plain calls if missing.
// ---------------------------------------------------------------------------------
static class Ui
{
    static bool spaceOk = true, widthOk = true, styleCallOk = true;
    public static bool Styled;

    public static GUIStyle Panel, Title, Sub, Section, Tab, TabOn, Row, RowOn, Chip, ChipOn, Primary, Danger, Text, Status, Field, FieldOn, KeyCap, KeyText;

    // ---------------- themes ----------------
    // A = main accent (primary buttons, selected chips, headers, panel glow)
    // B = second accent (active tab, title, editing field)
    // G = "on" colour for toggles, Bg/Card = background tint
    sealed class Theme
    {
        public readonly string Name;
        public readonly Color A, B, G, Bg, Card;
        public Theme(string name, Color a, Color b, Color g, Color bg, Color card) { Name = name; A = a; B = b; G = g; Bg = bg; Card = card; }
    }

    static Color H(float r, float g, float b, float a = 1f) => new(r, g, b, a);

    static readonly Theme[] Themes =
    {
        new("Dragon",  H(0.10f, 1.00f, 0.30f), H(0.65f, 1.00f, 0.20f), H(0.10f, 1.00f, 0.30f), H(0.020f, 0.045f, 0.030f, 0.97f), H(0.060f, 0.110f, 0.075f)),
        new("Neon",    H(0.00f, 0.94f, 1.00f), H(1.00f, 0.18f, 0.75f), H(0.22f, 1.00f, 0.55f), H(0.035f, 0.035f, 0.075f, 0.97f), H(0.085f, 0.085f, 0.150f)),
        new("Inferno", H(1.00f, 0.45f, 0.10f), H(1.00f, 0.15f, 0.25f), H(1.00f, 0.80f, 0.20f), H(0.060f, 0.025f, 0.020f, 0.97f), H(0.140f, 0.065f, 0.050f)),
        new("Ocean",   H(0.20f, 0.65f, 1.00f), H(0.00f, 1.00f, 0.85f), H(0.20f, 1.00f, 0.80f), H(0.020f, 0.040f, 0.075f, 0.97f), H(0.050f, 0.095f, 0.160f)),
        new("Purple",  H(0.70f, 0.40f, 1.00f), H(1.00f, 0.35f, 0.85f), H(0.55f, 1.00f, 0.75f), H(0.045f, 0.025f, 0.075f, 0.97f), H(0.110f, 0.065f, 0.165f)),
        new("Gold",    H(1.00f, 0.80f, 0.20f), H(1.00f, 0.55f, 0.10f), H(0.65f, 1.00f, 0.35f), H(0.050f, 0.042f, 0.025f, 0.97f), H(0.125f, 0.105f, 0.060f)),
    };
    public static readonly string[] ThemeNames = Array.ConvertAll(Themes, t => t.Name);
    public static string CurrentTheme = "Dragon";

    static Color Bg, Card, CardHover, CardLine, Cyan, CyanHover, Magenta, MagentaHover, Green, OnFill, OnFillHover, FieldBg;
    static readonly Color Red = H(1.00f, 0.20f, 0.36f);
    static readonly Color RedHover = H(1.00f, 0.40f, 0.52f);
    static readonly Color TextCol = H(0.95f, 0.96f, 1.00f);
    static readonly Color Muted = H(0.72f, 0.76f, 0.86f);
    static readonly Color Dark = H(0.03f, 0.03f, 0.05f);
    public static string AHex = "#1aff4d", BHex = "#a6ff33", GHex = "#1aff4d";

    static string ToHex(Color c) => $"#{(int)(c.r * 255):x2}{(int)(c.g * 255):x2}{(int)(c.b * 255):x2}";

    static void LoadTheme(string name)
    {
        var t = Array.Find(Themes, x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Themes[0];
        CurrentTheme = t.Name;
        Cyan = t.A; CyanHover = Color.Lerp(t.A, Color.white, 0.40f);
        Magenta = t.B; MagentaHover = Color.Lerp(t.B, Color.white, 0.30f);
        Green = t.G;
        Bg = t.Bg; Card = t.Card;
        CardHover = Color.Lerp(t.Card, t.A, 0.12f);
        CardLine = Color.Lerp(t.Card, t.A, 0.30f);
        OnFill = Color.Lerp(H(0.03f, 0.03f, 0.03f), t.G, 0.18f);
        OnFillHover = Color.Lerp(H(0.03f, 0.03f, 0.03f), t.G, 0.26f);
        FieldBg = Color.Lerp(t.Bg, Color.black, 0.5f); FieldBg.a = 1f;
        AHex = ToHex(t.A);
        BHex = ToHex(t.B);
        GHex = ToHex(t.G);
    }

    /// Switch theme at runtime: styles are rebuilt on the next OnGUI.
    public static void SetTheme(string name)
    {
        LoadTheme(name);
        Panel = null;
        Styled = false;
    }

    // ---------------- logo ----------------
    static Texture2D logoTex;
    public static GUIStyle Logo;
    static Texture2D LoadLogo()
    {
        if (logoTex != null) return logoTex;
        try
        {
            using var s = typeof(Ui).Assembly.GetManifestResourceStream("CanEmAllCheats.logo96.rgba");
            if (s == null) return null;
            var bytes = new byte[s.Length];
            int read = 0; while (read < bytes.Length) read += s.Read(bytes, read, bytes.Length - read);
            const int size = 96;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0, p = 0; i < size * size; i++, p += 4)
                t.SetPixel(i % size, i / size, new Color32(bytes[p], bytes[p + 1], bytes[p + 2], bytes[p + 3]));
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            logoTex = t;
        }
        catch (Exception e) { Plugin.L.LogWarning("Logo: " + e.Message); }
        return logoTex;
    }

    /// Anti-aliased rounded-rect texture with optional outline, used 9-sliced via style.border.
    static Texture2D Tex(Color c, int radius = 6, Color? outline = null, float ow = 1.6f)
    {
        int size = radius * 2 + 2;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        var o = outline ?? c;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
            float cover = Mathf.Clamp01(radius - d + 0.5f);
            float inner = outline.HasValue ? Mathf.Clamp01(radius - ow - d + 0.5f) : 1f;
            var col = Color.Lerp(o, c, inner);
            t.SetPixel(x, y, new Color(col.r, col.g, col.b, col.a * cover));
        }
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    static void SetOffsets(RectOffset o, int l, int r, int t, int b)
    {
        o.left = l; o.right = r; o.top = t; o.bottom = b;
    }

    static void Border(GUIStyle s, int r) { try { SetOffsets(s.border, r, r, r, r); } catch { } }

    // Fonts: Windows' Segoe UI family instead of the thin default Arial
    static Font FBody, FBold, FTitle;
    static Font OsFont(int size, params string[] names)
    {
        try
        {
            var f = Font.CreateDynamicFontFromOSFont(names, size);
            if (f != null) f.hideFlags = HideFlags.HideAndDontSave;
            return f;
        }
        catch (Exception e) { Plugin.L.LogWarning("OS font " + names[0] + ": " + e.Message); return null; }
    }

    static void ApplyFont(GUIStyle s, bool bold, bool title = false)
    {
        var f = title ? (FTitle ?? FBold) : bold ? FBold : FBody;
        if (f != null) { s.font = f; s.fontStyle = FontStyle.Normal; }
        else if (bold || title) s.fontStyle = FontStyle.Bold;
    }

    static GUIStyle Btn(Color bg, Color hover, Color text, int size, TextAnchor align, bool bold, int padH, int h, int radius = 6, Color? line = null, Color? lineHover = null)
    {
        var s = new GUIStyle();
        var n = Tex(bg, radius, line);
        var hv = Tex(hover, radius, lineHover ?? line);
        var pr = Tex(Color.Lerp(hover, Color.white, 0.15f), radius, lineHover ?? line);
        s.normal.background = n; s.normal.textColor = text;
        s.hover.background = hv; s.hover.textColor = text;
        s.active.background = pr; s.active.textColor = text;
        s.focused.background = n; s.focused.textColor = text;
        s.onNormal.background = n; s.onNormal.textColor = text;
        s.fontSize = size; s.alignment = align; s.richText = true;
        s.wordWrap = false;
        s.clipping = TextClipping.Clip;
        s.stretchWidth = true;
        s.fixedHeight = h;
        ApplyFont(s, bold);
        Border(s, radius);
        try { SetOffsets(s.padding, padH, padH, 0, 0); SetOffsets(s.margin, 3, 3, 3, 3); } catch { }
        return s;
    }

    static GUIStyle Lbl(Color c, int size, bool bold, TextAnchor align = TextAnchor.MiddleLeft, bool title = false)
    {
        var s = new GUIStyle();
        s.normal.textColor = c; s.fontSize = size; s.richText = true; s.alignment = align;
        s.wordWrap = true;
        s.stretchWidth = true;
        ApplyFont(s, bold, title);
        try { SetOffsets(s.padding, 4, 4, 2, 2); SetOffsets(s.margin, 3, 3, 2, 2); } catch { }
        return s;
    }

    public static void Init()
    {
        if (Panel != null) return;
        try
        {
            if (Card.a == 0f) LoadTheme(Plugin.Theme.Value);

            Panel = new GUIStyle();
            Panel.normal.background = Tex(Bg, 14, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f), 2f);
            Border(Panel, 14);
            try { SetOffsets(Panel.padding, 18, 18, 16, 16); } catch { }

            if (FBody == null)
            {
                FBody = OsFont(15, "Segoe UI Semibold", "Segoe UI", "Arial");
                FBold = OsFont(15, "Segoe UI Bold", "Segoe UI Semibold", "Arial");
                FTitle = OsFont(22, "Segoe UI Black", "Segoe UI Bold", "Arial");
            }

            Logo = new GUIStyle();
            Logo.normal.background = LoadLogo();
            Logo.fixedWidth = 56; Logo.fixedHeight = 56;
            try { SetOffsets(Logo.margin, 0, 10, 0, 0); } catch { }

            Title = Lbl(Cyan, 22, true, TextAnchor.LowerLeft, title: true);
            Sub = Lbl(Muted, 13, false);
            Section = Lbl(Cyan, 13, true);
            Text = Lbl(TextCol, 15, false);
            Status = Lbl(Green, 14, true);
            KeyText = Lbl(TextCol, 13, true);

            Tab = Btn(Card, CardHover, TextCol, 15, TextAnchor.MiddleCenter, true, 6, 34, 7, CardLine, Magenta);
            TabOn = Btn(Magenta, MagentaHover, Dark, 15, TextAnchor.MiddleCenter, true, 6, 34, 7);
            Row = Btn(Card, CardHover, TextCol, 15, TextAnchor.MiddleLeft, true, 14, 36, 7, CardLine, Cyan);
            RowOn = Btn(OnFill, OnFillHover, Color.Lerp(Green, Color.white, 0.65f), 15, TextAnchor.MiddleLeft, true, 14, 36, 7, Green, Green);
            Chip = Btn(Card, CardHover, TextCol, 14, TextAnchor.MiddleCenter, true, 6, 30, 7, CardLine, Cyan);
            ChipOn = Btn(Cyan, CyanHover, Dark, 14, TextAnchor.MiddleCenter, true, 6, 30, 7);
            Primary = Btn(Cyan, CyanHover, Dark, 16, TextAnchor.MiddleCenter, true, 10, 42, 8);
            Danger = Btn(Red, RedHover, Color.white, 15, TextAnchor.MiddleCenter, true, 10, 38, 8);
            Field = Btn(FieldBg, Card, TextCol, 15, TextAnchor.MiddleLeft, true, 12, 32, 7, CardLine, Cyan);
            FieldOn = Btn(FieldBg, FieldBg, Magenta, 15, TextAnchor.MiddleLeft, true, 12, 32, 7, Magenta, Magenta);
            KeyCap = Btn(FieldBg, FieldBg, Cyan, 13, TextAnchor.MiddleCenter, true, 4, 24, 6, Cyan, Cyan);
            Styled = true;
        }
        catch (Exception e)
        {
            Plugin.L.LogWarning("Custom styles unavailable, using default skin: " + e.Message);
            Styled = false;
            Panel = new GUIStyle();
        }
    }

    public static GUILayoutOption[] W(float w)
    {
        if (!widthOk) return Array.Empty<GUILayoutOption>();
        try { return new[] { GUILayout.Width(w) }; } catch { widthOk = false; return Array.Empty<GUILayoutOption>(); }
    }

    public static void Space(float px)
    {
        if (spaceOk) { try { GUILayout.Space(px); return; } catch { spaceOk = false; } }
        GUILayout.Label("");
    }

    public static bool Button(string text, GUIStyle style, params GUILayoutOption[] opts)
    {
        if (Styled && styleCallOk && style != null)
        {
            try { return GUILayout.Button(text, style, opts); }
            catch { styleCallOk = false; }
        }
        return GUILayout.Button(text, opts);
    }

    public static void Label(string text, GUIStyle style, params GUILayoutOption[] opts)
    {
        if (Styled && styleCallOk && style != null)
        {
            try { GUILayout.Label(text, style, opts); return; }
            catch { styleCallOk = false; }
        }
        GUILayout.Label(text, opts);
    }

    public static void Header(string text) { Space(6); Label(text.ToUpperInvariant(), Section); }

    public static bool Toggle(bool v, string label, string hint = null)
    {
        string txt = (v ? $"<color={GHex}>●</color>  " : "<color=#6a7080>○</color>  ") + label
                   + (hint != null ? $"    <size=13><color={(v ? "#e6ffee" : "#b8c2d6")}>{hint}</color></size>" : "");
        if (!Styled) txt = (v ? "[ON]  " : "[OFF] ") + label;
        return Button(txt, v ? RowOn : Row) ? !v : v;
    }

    public static T Chips<T>(T current, T[] values, Func<T, string> fmt, int perRow = 6)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i % perRow == 0) GUILayout.BeginHorizontal();
            bool on = EqualityComparer<T>.Default.Equals(values[i], current);
            if (Button(on && !Styled ? $"> {fmt(values[i])} <" : fmt(values[i]), on ? ChipOn : Chip)) current = values[i];
            if (i % perRow == perRow - 1 || i == values.Length - 1) GUILayout.EndHorizontal();
        }
        return current;
    }

    /// Key badge + description, e.g. [K] Kill all
    public static void KeyHint(string key, string text)
    {
        if (!Styled) { Label($"[{key}] {text}", Sub); return; }
        Label(key, KeyCap, W(Mathf.Max(28, 10 + key.Length * 9)));
        Label(text, KeyText);
    }

    public static float Stepper(string label, float v, float step, float min, float max, string fmt = "0.0")
    {
        GUILayout.BeginHorizontal();
        Label($"{label}", Text, W(150));
        Label($"<b>{v.ToString(fmt)}x</b>", Text, W(70));
        if (Button("−", Chip, W(40))) v -= step;
        if (Button("+", Chip, W(40))) v += step;
        GUILayout.EndHorizontal();
        return Mathf.Clamp((float)Math.Round(v, 2), min, max);
    }
}

public class CheatMenu : MonoBehaviour
{
    public CheatMenu(IntPtr ptr) : base(ptr) { }

    public static bool GodMode, OneHit, InfAmmo;
    public static float SpeedMult = 1f;
    public static int MoneyMult = 1;

    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Money", "Factory", "Spawn", "Settings" };

    // ---------------- saved settings ----------------
    bool settingsLoaded;

    void LoadSettings()
    {
        settingsLoaded = true;
        try
        {
            GodMode = Saved.Get("GodMode", false);
            OneHit = Saved.Get("OneHit", false);
            InfAmmo = Saved.Get("InfiniteAmmo", false);
            autoKill = Saved.Get("AutoKill", false);
            infStamina = Saved.Get("InfiniteStamina", false);
            SpeedMult = Saved.Get("WalkSpeed", 1f);
            MoneyMult = Saved.Get("IncomeMultiplier", 1);
            turbo = Saved.Get("TurboMachines", false);
            fastConveyor = Saved.Get("FastConveyor", false);
            allShiny = Saved.Get("AllShiny", false);
            turretAmmo = Saved.Get("TurretInfiniteAmmo", false);
            cansPerEnemyBonus = Saved.Get("ExtraCansPerEnemy", 0);
            atCrosshair = Saved.Get("SpawnAtCrosshair", true);
            tpToCrosshair = Saved.Get("TeleportToCrosshair", true);
            spawnCount = Saved.Get("SpawnCount", 1L);
            canCount = Saved.Get("CanCount", 5L);
            canValue = Saved.Get("CanValue", 1000L);
        }
        catch (Exception e) { Plugin.L.LogWarning("Load settings: " + e.Message); }
    }

    void SaveSettings()
    {
        try
        {
            Saved.Set("GodMode", GodMode);
            Saved.Set("OneHit", OneHit);
            Saved.Set("InfiniteAmmo", InfAmmo);
            Saved.Set("AutoKill", autoKill);
            Saved.Set("InfiniteStamina", infStamina);
            Saved.Set("WalkSpeed", SpeedMult);
            Saved.Set("IncomeMultiplier", MoneyMult);
            Saved.Set("TurboMachines", turbo);
            Saved.Set("FastConveyor", fastConveyor);
            Saved.Set("AllShiny", allShiny);
            Saved.Set("TurretInfiniteAmmo", turretAmmo);
            Saved.Set("ExtraCansPerEnemy", cansPerEnemyBonus);
            Saved.Set("SpawnAtCrosshair", atCrosshair);
            Saved.Set("TeleportToCrosshair", tpToCrosshair);
            Saved.Set("SpawnCount", spawnCount);
            Saved.Set("CanCount", canCount);
            Saved.Set("CanValue", canValue);
            Saved.Flush();
            SetStatus("Settings saved - they load automatically next time");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not save settings - see log"); }
    }

    // player
    bool infStamina, freezeTimer, autoKill;
    float ammoTick, autoKillTick, frozenTime = -1f, timeScale = 1f;

    // money
    long moneyAmount = 1_000_000;

    // factory
    bool turbo, turretAmmo, allShiny, fastConveyor;
    int cansPerEnemyBonus;
    float origInterval = -1f, origConveyor = -1f, origShiny = -1f;
    int origProcMult = -1, origCansPerEnemy = -1;

    // spawning
    int enemyIdx;
    long spawnCount = 1, canCount = 5, canValue = 1000;
    bool spawnShiny, canShiny, atCrosshair = true;
    string status = "";
    float statusTime;

    // number input (TextField is stripped in this game, so we read the keyboard ourselves)
    string editId, editBuf = "";
    long editMin, editMax;
    Action<long> editSet;

    // key rebinding
    ConfigEntry<string> rebinding;

    // teleport
    bool tpToCrosshair = true;
    Rect menuRect;

    static readonly long[] CountPresets = { 1, 5, 10, 25, 50, 100 };
    static readonly long[] CanCountPresets = { 1, 5, 10, 25, 50, 100 };
    static readonly long[] CanValuePresets = { 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000 };
    static readonly long[] MoneyPresets = { 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000, 1_000_000_000 };
    static readonly int[] MultPresets = { 1, 2, 5, 10, 50, 100 };

    void SetStatus(string s) { status = s; statusTime = Time.unscaledTime; Plugin.L.LogInfo(s); }

    static string Short(long v) =>
        v >= 1_000_000_000 ? $"{v / 1_000_000_000d:0.#}B" :
        v >= 1_000_000 ? $"{v / 1_000_000d:0.#}M" :
        v >= 1_000 ? $"{v / 1_000d:0.#}K" : v.ToString();

    static string Nice(string n)
    {
        if (string.IsNullOrEmpty(n)) return "?";
        n = n.Replace("(Clone)", "").Replace("_", " ").Trim();
        // strip leading sort index like "1 Dragon Spark"
        int sp = n.IndexOf(' ');
        if (sp > 0 && int.TryParse(n.Substring(0, sp), out _)) n = n.Substring(sp + 1);
        return n;
    }

    // ---------------- HUD infinity ----------------
    static string infGlyph;
    public static void ApplyInfinity(BulletCounter bc)
    {
        if (!InfAmmo || bc == null) return;
        try
        {
            var t = bc.text;
            if (t == null) return;
            if (infGlyph == null)
            {
                infGlyph = "∞";
                try { if (t.font != null && !t.font.HasCharacter('∞', true, true)) infGlyph = "<b>oo</b>"; } catch { }
            }
            t.text = infGlyph;
        }
        catch { }
    }

    static void RefillAllWeapons()
    {
        try
        {
            var inv = Inventory.Instance;
            if (inv == null || inv.allWeapons == null) return;
            foreach (var w in inv.allWeapons)
                if (w != null) RefillWeapon(w);
        }
        catch { }
    }

    static void RefillWeapon(Weapon w)
    {
        var pistol = w.TryCast<Pistol>();
        if (pistol != null) { if (pistol.bulletCount < pistol.maxBulletCount) pistol.bulletCount = pistol.maxBulletCount; return; }
        var ak = w.TryCast<AK47>();
        if (ak != null) { if (ak.bulletCount < ak.maxBulletCount) ak.bulletCount = ak.maxBulletCount; return; }
        var smg = w.TryCast<SMG>();
        if (smg != null) { if (smg.bulletCount < smg.maxBulletCount) smg.bulletCount = smg.maxBulletCount; return; }
        var shotgun = w.TryCast<Shotgun>();
        if (shotgun != null) { if (shotgun.bulletCount < shotgun.maxBulletCount) shotgun.bulletCount = shotgun.maxBulletCount; return; }
        var rocket = w.TryCast<RocketLauncher>();
        if (rocket != null) { if (rocket.bulletCount < rocket.maxBulletCount) rocket.bulletCount = rocket.maxBulletCount; return; }
        var flame = w.TryCast<FlameThrower>();
        if (flame != null) { if (flame.fuel < flame.maxFuel) flame.fuel = flame.maxFuel; return; }
        var saw = w.TryCast<Chainsaw>();
        if (saw != null) { if (saw.fuel < saw.maxFuel) saw.fuel = saw.maxFuel; }
    }

    static void RefreshAmmoHud()
    {
        try
        {
            var inv = Inventory.Instance;
            if (inv == null || inv.allWeapons == null || inv.allWeapons.Count == 0) return;
            var w = inv.allWeapons[Mathf.Clamp(inv.currentWeaponIndex, 0, inv.allWeapons.Count - 1)];
            if (w != null && w.gameObject.activeInHierarchy) w.Select();
        }
        catch { }
    }

    // ---------------- keyboard ----------------
    static Key ParseKey(string s) => Enum.TryParse<Key>(s, true, out var k) ? k : Key.None;

    static bool Pressed(Keyboard kb, Key k)
    {
        if (k == Key.None) return false;
        try { return kb[k].wasPressedThisFrame; } catch { return false; }
    }

    static readonly Key[] DigitKeys =
    {
        Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
    };
    static readonly Key[] NumpadKeys =
    {
        Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9,
    };
    static Key[] allKeys;

    void HandleNumberInput(Keyboard kb)
    {
        for (int d = 0; d <= 9; d++)
            if ((Pressed(kb, DigitKeys[d]) || Pressed(kb, NumpadKeys[d])) && editBuf.Length < 13)
                editBuf += (char)('0' + d);
        if (Pressed(kb, Key.Backspace) && editBuf.Length > 0) editBuf = editBuf.Substring(0, editBuf.Length - 1);
        if (Pressed(kb, Key.Delete)) editBuf = "";

        if (editBuf.Length > 0 && long.TryParse(editBuf, out var v))
            editSet?.Invoke(Math.Clamp(v, editMin, editMax));

        if (Pressed(kb, Key.Enter) || Pressed(kb, Key.NumpadEnter) || Pressed(kb, Key.Escape)) editId = null;
    }

    void HandleRebind(Keyboard kb)
    {
        if (Pressed(kb, Key.Escape)) { rebinding = null; SetStatus("Cancelled"); return; }
        allKeys ??= (Key[])Enum.GetValues(typeof(Key));
        foreach (var k in allKeys)
        {
            if (k == Key.None || k == Key.Escape) continue;
            if (Pressed(kb, k))
            {
                rebinding.Value = k.ToString();
                SetStatus($"{rebinding.Definition.Key} → {k}");
                rebinding = null;
                return;
            }
        }
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (rebinding != null) HandleRebind(kb);
            else if (editId != null) HandleNumberInput(kb);
            else
            {
                if (Pressed(kb, ParseKey(Plugin.KeyMenu.Value))) show = !show;
                if (Pressed(kb, ParseKey(Plugin.KeyKill.Value))) KillAllEnemies();
                if (Pressed(kb, ParseKey(Plugin.KeyTeleport.Value))) TeleportToMe(true, true, null);
                if (Pressed(kb, ParseKey(Plugin.KeySpawn.Value))) SpawnSelected();
            }
        }
        if (!show) { editId = null; rebinding = null; }
        if (show != inputBlocked) SetGameInput(!show);

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    // While the menu is open, the game must not react to clicks/mouse (no shooting, no camera turning).
    bool inputBlocked;
    void SetGameInput(bool enabled)
    {
        inputBlocked = !enabled;
        try
        {
            var pi = Player.Instance != null ? Player.Instance.playerInput : null;
            if (pi == null) return;
            if (enabled) pi.ActivateInput(); else pi.DeactivateInput();
        }
        catch (Exception e) { Plugin.L.LogWarning("Input toggle: " + e.Message); }
    }

    void Tick()
    {
        var p = Player.Instance;
        if (p != null && infStamina) { p.infiniteStamina = true; p.currentStamina = p.maxStamina; }

        // Refill ammo fields directly every frame. (Calling Weapon.RestoreAmmo() made the
        // rocket launcher restart its reload animation over and over.)
        if (InfAmmo) RefillAllWeapons();

        ammoTick -= Time.unscaledDeltaTime;
        if (ammoTick <= 0f)
        {
            ammoTick = 0.25f;
            if (InfAmmo && BulletCounter.Instance != null) ApplyInfinity(BulletCounter.Instance);
            if (turretAmmo && GameManager.Instance != null) GameManager.Instance.RestoreTurretAmmo();
        }

        if (autoKill)
        {
            autoKillTick -= Time.unscaledDeltaTime;
            if (autoKillTick <= 0f) { autoKillTick = 1f; KillAllEnemies(silent: true); }
        }

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (freezeTimer) { if (frozenTime < 0f) frozenTime = gm.Timer; gm._Timer_k__BackingField = frozenTime; }
        else frozenTime = -1f;

        if (turbo)
        {
            if (origInterval < 0f) { origInterval = gm.processInterval; origProcMult = gm.processSpeedMultiplier; }
            gm.processInterval = 0.01f;
            gm.processSpeedMultiplier = Math.Max(origProcMult, 1) * 20;
        }
        else if (origInterval >= 0f) { gm.processInterval = origInterval; gm.processSpeedMultiplier = origProcMult; origInterval = -1f; }

        if (fastConveyor)
        {
            if (origConveyor < 0f) origConveyor = gm.conveyorSpeed;
            if (gm.conveyorSpeed != origConveyor * 4f) { gm.conveyorSpeed = origConveyor * 4f; gm.UpdateConveyorSpeed(); }
        }
        else if (origConveyor >= 0f) { gm.conveyorSpeed = origConveyor; gm.UpdateConveyorSpeed(); origConveyor = -1f; }

        if (allShiny) { if (origShiny < 0f) origShiny = gm.shinyCanProbability; gm.shinyCanProbability = 1f; }
        else if (origShiny >= 0f) { gm.shinyCanProbability = origShiny; origShiny = -1f; }

        if (cansPerEnemyBonus > 0)
        {
            if (origCansPerEnemy < 0) origCansPerEnemy = gm.cansPerEnemy;
            gm.cansPerEnemy = origCansPerEnemy + cansPerEnemyBonus;
        }
        else if (origCansPerEnemy >= 0) { gm.cansPerEnemy = origCansPerEnemy; origCansPerEnemy = -1; }

        if (!gm.IsPaused && timeScale != 1f && Time.timeScale != timeScale) Time.timeScale = timeScale;
    }

    void LateUpdate()
    {
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ---------------- GUI ----------------
    void OnGUI()
    {
        if (!show) return;
        Ui.Init();
        bool areaOpen = false;
        try
        {
            var rect = new Rect(24, 24, 460, Screen.height - 48);
            menuRect = rect;
            if (Ui.Styled) GUILayout.BeginArea(rect, Ui.Panel); else GUILayout.BeginArea(rect, GUI.skin.box);
            areaOpen = true;
            Draw();
            GUILayout.EndArea();
            areaOpen = false;
        }
        catch (Exception e)
        {
            var msg = e.ToString();
            if (msg != lastGuiError) { lastGuiError = msg; Plugin.L.LogError(e); }
            if (areaOpen) try { GUILayout.EndArea(); } catch { }
        }
    }

    void Draw()
    {
        GUILayout.BeginHorizontal();
        if (Ui.Styled && Ui.Logo != null && Ui.Logo.normal.background != null) Ui.Label("", Ui.Logo);
        GUILayout.BeginVertical();
        Ui.Label($"EvilDragon's <color={Ui.BHex}>Cheats</color>", Ui.Title);
        Ui.Label($"Can 'Em All  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(6);
        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyKill.Value, "Kill all");
        Ui.KeyHint(Plugin.KeyTeleport.Value, "Teleport all");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeySpawn.Value, "Spawn");
        Ui.KeyHint(Plugin.KeyMenu.Value, "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(6);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; }
        GUILayout.EndHorizontal();
        Ui.Space(4);

        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawMoney(); break;
            case 2: DrawFactory(); break;
            case 3: DrawSpawn(); break;
            case 4: DrawSettings(); break;
        }

        if (status.Length > 0 && Time.unscaledTime - statusTime < 6f)
        {
            Ui.Space(8);
            Ui.Label("» " + status, Ui.Status);
        }
    }

    /// Clickable number field. Click it, type digits, Enter to confirm.
    long NumField(string id, string label, long value, long min, long max, Action<long> set)
    {
        bool editing = editId == id;
        GUILayout.BeginHorizontal();
        Ui.Label(label, Ui.Sub, Ui.W(110));
        string shown = editing ? (editBuf.Length == 0 ? "_" : editBuf + "_") + $"   <size=12><color={Ui.AHex}>Enter = OK</color></size>" : value.ToString("N0") + "   <size=12><color=#b8c2d6>click to type</color></size>";
        if (Ui.Button(shown, editing ? Ui.FieldOn : Ui.Field))
        {
            if (editing) editId = null;
            else { editId = id; editBuf = ""; editMin = min; editMax = max; editSet = set; }
        }
        GUILayout.EndHorizontal();
        return value;
    }

    void DrawPlayer()
    {
        Ui.Header("Combat");
        GodMode = Ui.Toggle(GodMode, "God Mode", "no damage");
        OneHit = Ui.Toggle(OneHit, "One-Hit Kill");
        bool ia = Ui.Toggle(InfAmmo, InfAmmo ? "Infinite Ammo  ∞" : "Infinite Ammo");
        if (ia != InfAmmo) { InfAmmo = ia; if (InfAmmo && BulletCounter.Instance != null) ApplyInfinity(BulletCounter.Instance); else RefreshAmmoHud(); }
        autoKill = Ui.Toggle(autoKill, "Auto Kill", "kills everything every second");

        Ui.Header("Movement");
        bool st = Ui.Toggle(infStamina, "Infinite Stamina");
        if (st != infStamina)
        {
            infStamina = st;
            if (!st && Player.Instance != null) Player.Instance.infiniteStamina = Player.Instance.defaultInfiniteStamina;
        }
        SpeedMult = Ui.Stepper("Walk Speed", SpeedMult, 0.5f, 1f, 10f);

        Ui.Header("Time");
        freezeTimer = Ui.Toggle(freezeTimer, "Freeze Day Timer");
        float ts = Ui.Stepper("Game Speed", timeScale, 0.25f, 0.25f, 5f, "0.00");
        if (ts != timeScale) { timeScale = ts; Time.timeScale = ts; }

        Ui.Header("Actions");
        GUILayout.BeginHorizontal();
        if (Ui.Button($"Kill All  [{Plugin.KeyKill.Value}]", Ui.Danger)) KillAllEnemies();
        if (Ui.Button("Save Settings", Ui.Tab)) SaveSettings();
        GUILayout.EndHorizontal();
    }

    void DrawMoney()
    {
        var mm = MoneyManager.Instance;
        Ui.Header("Balance");
        Ui.Label($"<size=22><b>$ {(mm != null ? mm.Money.ToString("N0") : "-")}</b></size>", Ui.Text);

        Ui.Header("Amount");
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => Short(v), 6);
        NumField("money", "Custom amount", moneyAmount, 1, 1_000_000_000_000_000L, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ {Short(moneyAmount)}", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− {Short(moneyAmount)}", Ui.Tab)) AddMoney(-moneyAmount);
        if (Ui.Button("= Set", Ui.Tab) && mm != null) { mm.SetMoney(moneyAmount); SetStatus($"Money set to {moneyAmount:N0}"); }
        GUILayout.EndHorizontal();

        Ui.Header("Income Multiplier");
        MoneyMult = Ui.Chips(MoneyMult, MultPresets, v => "x" + v, 6);

        Ui.Header("Rebirth Points");
        var sd = SaveManager.Instance != null ? SaveManager.Instance.CurrentSaveData : null;
        GUILayout.BeginHorizontal();
        Ui.Label($"<b>{(sd != null ? sd.upgradePoints.ToString() : "-")}</b> points", Ui.Text, Ui.W(120));
        if (Ui.Button("+1", Ui.Chip)) AddPoints(1);
        if (Ui.Button("+10", Ui.Chip)) AddPoints(10);
        if (Ui.Button("+100", Ui.Chip)) AddPoints(100);
        GUILayout.EndHorizontal();
    }

    void DrawFactory()
    {
        var gm = GameManager.Instance;
        if (gm == null) { Ui.Label("Load into a game first.", Ui.Text); return; }

        Ui.Header("Processing");
        turbo = Ui.Toggle(turbo, "Turbo Machines", "20x faster");
        fastConveyor = Ui.Toggle(fastConveyor, "Conveyor x4");
        allShiny = Ui.Toggle(allShiny, "All Cans Shiny");
        Ui.Label($"Interval {gm.processInterval:0.00}s  ·  Speed x{gm.processSpeedMultiplier}  ·  {gm.cansPerEnemy} cans/enemy", Ui.Sub);

        Ui.Header("Extra Cans per Enemy");
        cansPerEnemyBonus = Ui.Chips(cansPerEnemyBonus, new[] { 0, 1, 2, 5, 10, 25 }, v => v == 0 ? "Off" : "+" + v, 6);

        Ui.Header("Defense");
        turretAmmo = Ui.Toggle(turretAmmo, "Turrets Infinite Ammo");

        Ui.Header("Actions");
        if (Ui.Button("Turn On All Machines, Turrets & Robots", Ui.Tab))
        {
            try { gm.TurnOnMachines(true); gm.TurnOnTurrets(); gm.TurnOnRobotCleaner(); SetStatus("Machines, turrets and robots on"); }
            catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
        }
        if (Ui.Button("Unlock All Skills", Ui.Primary)) UnlockAllSkills();
    }

    void DrawSpawn()
    {
        var truck = Truck.Instance;
        bool haveEnemies = truck != null && truck.allEnemyPrefabs != null && truck.allEnemyPrefabs.Count > 0;

        Ui.Header("Spawn Location");
        atCrosshair = Ui.Chips(atCrosshair, new[] { true, false }, v => v ? "At Crosshair" : "In Front of Me", 2);

        Ui.Header("Creature");
        Enemy prefab = null;
        if (!haveEnemies) Ui.Label("Load into a game first.", Ui.Text);
        else
        {
            int n = truck.allEnemyPrefabs.Count;
            enemyIdx = Mathf.Clamp(enemyIdx, 0, n - 1);
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            enemyIdx = Ui.Chips(enemyIdx, idx, i => Nice(truck.allEnemyPrefabs[i] != null ? truck.allEnemyPrefabs[i].name : null), 3);
            prefab = truck.allEnemyPrefabs[enemyIdx];

            spawnCount = Ui.Chips(spawnCount, CountPresets, v => v.ToString(), 6);
            NumField("spawnCount", "Custom count", spawnCount, 1, 1000, v => spawnCount = v);
            spawnShiny = Ui.Toggle(spawnShiny, "Shiny Variant");
            if (Ui.Button($"SPAWN {spawnCount}x {Nice(prefab != null ? prefab.name : null)}  [{Plugin.KeySpawn.Value}]", Ui.Primary)) SpawnSelected();
        }

        Ui.Header("Cans");
        canValue = Ui.Chips(canValue, CanValuePresets, v => "$" + Short(v), 6);
        NumField("canValue", "Custom value", canValue, 1, int.MaxValue, v => canValue = v);
        canCount = Ui.Chips(canCount, CanCountPresets, v => v.ToString(), 6);
        NumField("canCount", "Custom count", canCount, 1, 1000, v => canCount = v);
        canShiny = Ui.Toggle(canShiny, "Shiny Cans");
        if (Ui.Button($"SPAWN {canCount}x CAN  (${Short(canValue)})", Ui.Primary)) SpawnCans();

        Ui.Header("Teleport");
        tpToCrosshair = Ui.Chips(tpToCrosshair, new[] { true, false }, v => v ? "To Crosshair / Mouse" : "To Me", 2);
        GUILayout.BeginHorizontal();
        if (Ui.Button("All Creatures", Ui.Tab)) TeleportToMe(true, false, null);
        if (prefab != null && Ui.Button("Only " + Nice(prefab.name), Ui.Tab)) TeleportToMe(true, false, Nice(prefab.name));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (Ui.Button("All Cans", Ui.Tab)) TeleportToMe(false, true, null);
        if (Ui.Button($"Everything  [{Plugin.KeyTeleport.Value}]", Ui.Primary)) TeleportToMe(true, true, null);
        GUILayout.EndHorizontal();

        Ui.Space(4);
        if (Ui.Button($"Kill All  [{Plugin.KeyKill.Value}]", Ui.Danger)) KillAllEnemies();
    }

    void DrawSettings()
    {
        Ui.Header("Theme");
        string th = Ui.Chips(Ui.CurrentTheme, Ui.ThemeNames, n => n, 3);
        if (th != Ui.CurrentTheme)
        {
            Ui.SetTheme(th);
            Plugin.Theme.Value = th;
            SetStatus($"Theme: {th}");
        }

        DrawKeys();

        Ui.Header("Settings");
        Ui.Label("Saves all toggles and values so they are active the next time you start the game.", Ui.Sub);
        if (Ui.Button("Save Settings", Ui.Primary)) SaveSettings();
    }

    void DrawKeys()
    {
        Ui.Header("Keybinds");
        Ui.Label("Click a key, then press the new key (Esc = cancel).", Ui.Sub);
        KeyRow("Open menu", Plugin.KeyMenu);
        KeyRow("Kill all enemies", Plugin.KeyKill);
        KeyRow("Teleport everything", Plugin.KeyTeleport);
        KeyRow("Spawn selected creature", Plugin.KeySpawn);
        Ui.Space(6);
        Ui.Label($"Saved in BepInEx\\config\\{Plugin.Guid}.cfg", Ui.Sub);
    }

    void KeyRow(string label, ConfigEntry<string> entry)
    {
        bool active = rebinding == entry;
        GUILayout.BeginHorizontal();
        Ui.Label(label, Ui.Text, Ui.W(220));
        if (Ui.Button(active ? "Press a key…" : entry.Value, active ? Ui.FieldOn : Ui.Chip))
            rebinding = active ? null : entry;
        GUILayout.EndHorizontal();
    }

    // ---------------- actions ----------------
    Vector3 SpawnPoint(int i, int count, float baseDist, float spacing = 1.1f)
    {
        var p = Player.Instance;
        var cam = Camera.main;
        var origin = p != null ? p.transform.position : Vector3.zero;
        var fwd = cam != null ? cam.transform.forward : Vector3.forward;

        Vector3 center;
        RaycastHit hit;
        if (atCrosshair && cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 80f))
            center = hit.point - cam.transform.forward * 0.6f + Vector3.up * 0.4f;
        else
        {
            var f = fwd; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            center = origin + f * baseDist + Vector3.up * 0.5f;
        }
        return center + GridOffset(i, count, fwd, spacing);
    }

    static Vector3 GridOffset(int i, int count, Vector3 fwd, float spacing)
    {
        var flat = fwd; flat.y = 0; if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward; flat.Normalize();
        var right = Vector3.Cross(Vector3.up, flat);
        int perRow = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        float x = (i % perRow - (perRow - 1) / 2f) * spacing;
        float z = (i / perRow) * spacing;
        return right * x + flat * z;
    }

    static Weapon CurrentWeapon()
    {
        try
        {
            var inv = Inventory.Instance;
            if (inv == null || inv.allWeapons == null || inv.allWeapons.Count == 0) return null;
            return inv.allWeapons[Mathf.Clamp(inv.currentWeaponIndex, 0, inv.allWeapons.Count - 1)];
        }
        catch { return null; }
    }

    static List<T> FindAll<T>() where T : Component
    {
        var list = new List<T>();
        try
        {
            var found = UnityEngine.Object.FindObjectsByType(Il2CppType.Of<T>(), FindObjectsSortMode.None);
            foreach (var o in found)
            {
                var c = o.TryCast<T>();
                if (c != null) list.Add(c);
            }
        }
        catch (Exception ex) { Plugin.L.LogWarning($"FindObjectsByType<{typeof(T).Name}> failed: " + ex.Message); }
        return list;
    }

    static List<Enemy> AllEnemies()
    {
        var list = FindAll<Enemy>();
        var seen = new HashSet<IntPtr>();
        foreach (var e in list) seen.Add(e.Pointer);
        try
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.enemies != null)
                foreach (var e in gm.enemies)
                    if (e != null && seen.Add(e.Pointer)) list.Add(e);
        }
        catch { }
        return list;
    }

    void KillAllEnemies(bool silent = false)
    {
        int killed = 0, total = 0;
        var weapon = CurrentWeapon();
        foreach (var e in AllEnemies())
        {
            try
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.IsDead) continue;
                total++;
                var pos = e.transform.position;
                e.Damage(999999f, Vector3.up, pos, 1f, true, false, weapon, false, true, 1f);
                if (!e.IsDead) { e.health = 0f; e.Damage(1f, Vector3.up, pos, 1f, false, false, weapon, false, false, 1f); }
                if (e.IsDead) killed++;
            }
            catch (Exception ex) { Plugin.L.LogError(ex); }
        }
        if (!silent || killed > 0)
            SetStatus(total == 0 ? "No living enemies found" : $"Killed {killed}/{total} enemies");
    }

    /// Where teleported things land: at the crosshair (or mouse when the menu is open) or right in front of the player.
    Vector3 TeleportTarget(out Vector3 flat)
    {
        var p = Player.Instance;
        var cam = Camera.main;
        var fwd = cam != null ? cam.transform.forward : p.transform.forward;
        flat = fwd; flat.y = 0; if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward; flat.Normalize();

        if (tpToCrosshair && cam != null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            try
            {
                var mouse = Mouse.current;
                if (show && mouse != null)
                {
                    var mp = mouse.position.ReadValue();
                    var guiPoint = new Vector2(mp.x, Screen.height - mp.y);
                    if (!menuRect.Contains(guiPoint)) ray = cam.ScreenPointToRay(new Vector3(mp.x, mp.y, 0));
                }
            }
            catch { }
            if (Physics.Raycast(ray, out RaycastHit hit, 120f))
                return hit.point + hit.normal * 0.6f + Vector3.up * 0.4f;
            return ray.origin + ray.direction * 10f;
        }
        return p.transform.position + flat * 2.5f + Vector3.up * 0.8f;
    }

    static Vector3 Jitter(float r) => new(UnityEngine.Random.Range(-r, r), UnityEngine.Random.Range(0f, r), UnityEngine.Random.Range(-r, r));

    void TeleportToMe(bool enemies, bool cans, string onlyType)
    {
        var p = Player.Instance;
        if (p == null) { SetStatus("No player found"); return; }
        var target = TeleportTarget(out var flat);

        int ne = 0, nc = 0;
        if (enemies)
        {
            var list = new List<Enemy>();
            foreach (var e in AllEnemies())
            {
                try
                {
                    if (e == null || !e.gameObject.activeInHierarchy || e.isHeld) continue;
                    if (onlyType != null && Nice(e.name) != onlyType) continue;
                    list.Add(e);
                }
                catch { }
            }
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    var pos = target + Jitter(0.35f) + Vector3.up * (i * 0.05f);
                    Place(list[i], pos, Quaternion.LookRotation(-flat));
                    if (!list[i].IsDead) try { list[i].ClearCurrentPath(); } catch { }
                    ne++;
                }
                catch (Exception ex) { Plugin.L.LogError(ex); }
            }
        }
        if (cans)
        {
            var list = new List<Can>();
            foreach (var c in FindAll<Can>())
            {
                try
                {
                    if (c == null || !c.gameObject.activeInHierarchy || c.IsInPool || c.isHeld) continue;
                    list.Add(c);
                }
                catch { }
            }
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    var pos = target + Jitter(0.25f) + Vector3.up * (0.3f + i * 0.03f);
                    var c = list[i];
                    c.transform.position = pos;
                    if (c.rb != null) { c.rb.position = pos; if (!c.rb.isKinematic) { c.rb.linearVelocity = Vector3.zero; c.rb.angularVelocity = Vector3.zero; } }
                    nc++;
                }
                catch (Exception ex) { Plugin.L.LogError(ex); }
            }
        }

        var parts = new List<string>();
        if (enemies) parts.Add($"{ne} creature(s)");
        if (cans) parts.Add($"{nc} can(s)");
        SetStatus(string.Join(" + ", parts) + (tpToCrosshair ? " teleported to crosshair" : " teleported to you"));
    }

    void SpawnSelected()
    {
        var truck = Truck.Instance;
        if (truck == null || truck.allEnemyPrefabs == null || truck.allEnemyPrefabs.Count == 0) { SetStatus("Load into a game first"); return; }
        enemyIdx = Mathf.Clamp(enemyIdx, 0, truck.allEnemyPrefabs.Count - 1);
        SpawnEnemies(truck, truck.allEnemyPrefabs[enemyIdx]);
    }

    void SpawnEnemies(Truck truck, Enemy prefab)
    {
        if (prefab == null) return;
        int ok = 0, count = (int)spawnCount;
        float radius = truck.roamRadius > 0 ? truck.roamRadius : 10f;
        for (int i = 0; i < count; i++)
        {
            try
            {
                var pos = SpawnPoint(i, count, 4f);
                var rot = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                Enemy e = null;
                try { e = truck.GetEnemy(prefab, pos, rot); } catch (Exception ex) { Plugin.L.LogWarning("GetEnemy: " + ex.Message); }
                if (e == null) e = UnityEngine.Object.Instantiate(prefab, pos, rot);
                if (e == null) continue;

                e.gameObject.SetActive(true);
                try { e.Restore(); } catch (Exception ex) { Plugin.L.LogWarning("Restore: " + ex.Message); }
                Place(e, pos, rot);
                try { e.SpawnRoaming(radius); } catch (Exception ex) { Plugin.L.LogWarning("SpawnRoaming: " + ex.Message); }
                if ((e.transform.position - pos).sqrMagnitude > 4f) Place(e, pos, rot);
                if (spawnShiny) try { e.SetShiny(true); } catch { }
                try { GameManager.Instance?.RegisterEnemy(e); } catch { }
                try { if (!truck.spawnedEnemies.Contains(e)) truck.spawnedEnemies.Add(e); } catch { }
                ok++;
            }
            catch (Exception ex) { Plugin.L.LogError(ex); }
        }
        SetStatus($"Spawned {ok}x {Nice(prefab.name)}");
    }

    static void Place(Enemy e, Vector3 pos, Quaternion rot)
    {
        e.transform.SetPositionAndRotation(pos, rot);
        try
        {
            if (e.rb != null)
            {
                e.rb.position = pos;
                e.rb.rotation = rot;
                if (!e.rb.isKinematic) { e.rb.linearVelocity = Vector3.zero; e.rb.angularVelocity = Vector3.zero; }
            }
        }
        catch { }
    }

    void SpawnCans()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.canProcessors == null || gm.canProcessors.Length == 0) { SetStatus("No can prefab found"); return; }
        var prefab = gm.canProcessors[0].canPrefab;
        int ok = 0, count = (int)canCount;
        for (int i = 0; i < count; i++)
        {
            try
            {
                var pos = SpawnPoint(i, count, 2f, 0.45f) + Vector3.up * 0.8f;
                var can = gm.GetCanFromPool(prefab, pos, Quaternion.identity);
                if (can == null) continue;
                can.gameObject.SetActive(true);
                can.Initialize(UnityEngine.Random.Range(0, 4), false, canShiny);
                can.price = (int)Math.Min(canValue, int.MaxValue);
                can.quantity = 1;
                can.transform.position = pos;
                if (can.rb != null) { can.rb.position = pos; if (!can.rb.isKinematic) can.rb.linearVelocity = Vector3.zero; }
                ok++;
            }
            catch (Exception ex) { Plugin.L.LogError(ex); }
        }
        SetStatus($"Spawned {ok} can(s) worth ${canValue:N0}");
    }

    void UnlockAllSkills()
    {
        var sm = SkillManager.Instance;
        if (sm == null || sm.skillNodes == null) { SetStatus("Skill tree not found"); return; }
        var sd = SaveManager.Instance != null ? SaveManager.Instance.CurrentSaveData : null;
        int count = 0;
        foreach (var node in sm.skillNodes)
        {
            var s = node != null ? node.skill : null;
            if (s == null || s.isPurchased) continue;
            try
            {
                s.isPurchased = true;
                s.ApplySkill();
                if (sd != null && sd.unlockedSkills != null && !sd.unlockedSkills.Contains(s.skillId)) sd.unlockedSkills.Add(s.skillId);
                count++;
            }
            catch (Exception ex) { Plugin.L.LogError(ex); }
        }
        try { GameManager.Instance?.skillTreeView?.RefreshNodeLockStates(); } catch (Exception ex) { Plugin.L.LogError(ex); }
        SetStatus($"Unlocked {count} skills");
    }

    void AddMoney(long amount)
    {
        var mm = MoneyManager.Instance;
        if (mm == null) return;
        mm.SetMoney(Math.Max(0, mm.Money + amount));
        SetStatus((amount >= 0 ? "+" : "−") + $"$ {Math.Abs(amount):N0}");
    }

    void AddPoints(int n)
    {
        var sm = SaveManager.Instance;
        if (sm == null || sm.CurrentSaveData == null) return;
        sm.CurrentSaveData.upgradePoints += n;
        try { PermanentUpgradeManager.Instance?.UpdatePointsText(); } catch { }
        SetStatus($"+{n} rebirth points");
    }
}
