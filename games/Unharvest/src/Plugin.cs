using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnharvestCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Unharvest", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.unharvest";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<Key> KeyMenu, KeyFly, KeyWither;
    internal static ConfigEntry<string> Theme;
    internal static ConfigEntry<float> MenuScale;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", Key.F1, "Open/close menu");
        KeyFly = Config.Bind("Hotkeys", "Fly", Key.F2, "Toggle fly mode (noclip)");
        KeyWither = Config.Bind("Hotkeys", "WitherAround", Key.F3, "Wither all wheat around you");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        MenuScale = Config.Bind("Interface", "MenuScale", 1f, "Size of the menu (0.75 - 1.3)");
        Ui.InitialTheme = Theme.Value;
        Ui.UseOsFonts = false;   // Unity 6 cannot use Windows fonts in IMGUI
        Ui.Log = m => Logger.LogWarning(m);

        var go = new GameObject("EvilDragonCheatMenu");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        try { new Harmony(Guid).PatchAll(typeof(Patches)); } catch (Exception e) { Logger.LogError(e); }
        Logger.LogInfo($"EvilDragon's Cheats v{Version} loaded");
    }
}

/// What the Harmony patches read. Only active while a game is loaded.
static class Cheat
{
    public static bool Allowed, Fly, FreeUpgrades, InstantWither, HoldToWither, AutoAim, NoSwarmCooldown, MenuOpen;
    public static float Speed = 1f, FlySpeed = 2f, WispMult = 1f;
    public static int ExtraTargets;
}

static class Patches
{
    // walk speed
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.CurrentSpeed), MethodType.Getter)]
    static void Speed(ref float __result) { if (Cheat.Allowed) __result *= Cheat.Speed; }

    // fly: the cheat menu moves the player itself
    [HarmonyPrefix, HarmonyPatch(typeof(PlayerMovement), "FixedUpdate")]
    static bool Walk() => !(Cheat.Allowed && Cheat.Fly);

    // while the menu is open the player does not walk, look or wither
    static readonly MethodInfo ClearInput = AccessTools.Method(typeof(PlayerInputHandler), "ClearAll");

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerInputHandler), "Update")]
    static void NoInput(PlayerInputHandler __instance)
    {
        if (Cheat.MenuOpen) try { ClearInput.Invoke(__instance, null); } catch { }
    }

    // free upgrades
    [HarmonyPrefix, HarmonyPatch(typeof(DespairManager), nameof(DespairManager.TrySpendDespair))]
    static bool Spend(ref bool __result)
    {
        if (!(Cheat.Allowed && Cheat.FreeUpgrades)) return true;
        __result = true;
        return false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(UpgradeTreeController), nameof(UpgradeTreeController.CanAfford))]
    static void Afford(ref bool __result) { if (Cheat.Allowed && Cheat.FreeUpgrades) __result = true; }

    // every wisp is worth more
    [HarmonyPostfix, HarmonyPatch(typeof(UpgradeStats), nameof(UpgradeStats.RollWisp))]
    static void Wisp(ref int value)
    {
        if (Cheat.Allowed && Cheat.WispMult > 1f) value = (int)Math.Min(value * (double)Cheat.WispMult, 1_000_000d);
    }

    // wither faster / more stalks per click / hold to wither / auto aim
    [HarmonyPostfix, HarmonyPatch(typeof(UpgradeStats), nameof(UpgradeStats.GetWitherTime))]
    static void WitherTime(ref float __result) { if (Cheat.Allowed && Cheat.InstantWither) __result = 0.05f; }

    [HarmonyPostfix, HarmonyPatch(typeof(UpgradeStats), nameof(UpgradeStats.GetTargetCount))]
    static void Targets(ref int __result) { if (Cheat.Allowed) __result += Cheat.ExtraTargets; }

    [HarmonyPostfix, HarmonyPatch(typeof(UpgradeStats), nameof(UpgradeStats.HasAutoWither), MethodType.Getter)]
    static void Hold(ref bool __result) { if (Cheat.Allowed && Cheat.HoldToWither) __result = true; }

    [HarmonyPostfix, HarmonyPatch(typeof(UpgradeStats), nameof(UpgradeStats.HasAutoAim), MethodType.Getter)]
    static void Aim(ref bool __result) { if (Cheat.Allowed && Cheat.AutoAim) __result = true; }

    // locust swarm ready again right away
    [HarmonyPostfix, HarmonyPatch(typeof(LocustSwarm), nameof(LocustSwarm.SwarmCooldown), MethodType.Getter)]
    static void SwarmCooldown(ref float __result) { if (Cheat.Allowed && Cheat.NoSwarmCooldown) __result = 0.5f; }
}

/// Cheat settings that "Save Settings" persists to the BepInEx config file.
static class Saved
{
    static readonly Dictionary<string, ConfigEntryBase> entries = new Dictionary<string, ConfigEntryBase>();

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

static class Game
{
    static PlayerMovement me;
    static FieldSpawner wheat;
    static SaveManager foundFor;
    static float lookedAt = -10f;

    /// Finds the player and the field once per game (searching the scene is slow).
    static void Find()
    {
        var s = SaveManager.Instance;
        if (foundFor == s && me != null && wheat != null) return;
        if (foundFor != s) { me = null; wheat = null; foundFor = s; }
        if (s == null || Time.unscaledTime - lookedAt < 1f) return;
        lookedAt = Time.unscaledTime;
        if (me == null) me = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (wheat == null) wheat = UnityEngine.Object.FindAnyObjectByType<FieldSpawner>();
    }

    public static PlayerMovement Me { get { Find(); return me; } }
    public static FieldSpawner Field { get { Find(); return wheat; } }
    public static Rigidbody Body => Me != null ? Me.GetComponent<Rigidbody>() : null;
    public static UpgradeTreeController Tree => UpgradeTreeController.Instance;

    public static bool InGame => SaveManager.Instance != null && SaveManager.Instance.Loaded && Me != null && Me.isActiveAndEnabled && Field != null;

    /// Why the cheats are off right now (null = they are on).
    public static string Why => InGame ? null : "Start or continue a game first.";

    public static readonly AccessTools.FieldRef<UpgradeTreeController, List<UpgradeData>> AllNodes = AccessTools.FieldRefAccess<UpgradeTreeController, List<UpgradeData>>("allNodes");
    public static readonly AccessTools.FieldRef<PaperPickup, PaperData> PaperOf = AccessTools.FieldRefAccess<PaperPickup, PaperData>("paper");
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Despair", "Wither", "Upgrades", "Teleport", "Settings" };
    static readonly float[] MenuSizes = { 0.75f, 0.85f, 1f, 1.15f, 1.3f };

    // player
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };
    float gameSpeed = 1f;
    static readonly float[] GameSpeeds = { 0.5f, 1f, 2f, 3f, 5f };
    bool flyKinematic, flyWasKinematic;

    // despair
    long despairAmount = 1000;
    static readonly long[] DespairPresets = { 100, 1_000, 10_000, 100_000 };
    static readonly float[] WispPresets = { 1f, 2f, 5f, 10f, 100f };

    // wither
    float witherRadius = 10f;
    static readonly float[] WitherRadii = { 5f, 10f, 20f, 40f };
    static readonly int[] TargetPresets = { 0, 5, 20, 50 };
    readonly Queue<int> toWither = new Queue<int>();
    readonly List<int> found = new List<int>();
    const int WitherPerFrame = 300;

    // teleport
    class Place { public string Name, Group; public Vector3 Pos; }
    List<Place> places = new List<Place>();
    List<Place> papers = new List<Place>();
    SaveManager placesFor;
    float placesScannedAt = -100f;
    bool hasSaved, paperEsp;
    Vector3 savedPos;

    // allowed check (once per second)
    bool allowed;
    float allowedAt = -10f;
    string lastWhy = "";

    // number input + key rebinding
    string editId, editBuf = "";
    long editMin, editMax;
    Action<long> editSet;
    ConfigEntry<Key> rebinding;

    string status = "";
    float statusTime;

    void SetStatus(string s) { status = s; statusTime = Time.unscaledTime; Plugin.L.LogInfo(s); }

    // ---------------- saved settings ----------------
    bool settingsLoaded;
    void LoadSettings()
    {
        settingsLoaded = true;
        Cheat.Speed = Saved.Get("WalkSpeed", 1f);
        Cheat.FlySpeed = Saved.Get("FlySpeed", 2f);
        Cheat.FreeUpgrades = Saved.Get("FreeUpgrades", false);
        Cheat.WispMult = Saved.Get("WispMultiplier", 1f);
        Cheat.InstantWither = Saved.Get("InstantWither", false);
        Cheat.HoldToWither = Saved.Get("HoldToWither", false);
        Cheat.AutoAim = Saved.Get("AutoAim", false);
        Cheat.ExtraTargets = Saved.Get("ExtraTargets", 0);
        Cheat.NoSwarmCooldown = Saved.Get("NoSwarmCooldown", false);
        witherRadius = Saved.Get("WitherRadius", 10f);
        despairAmount = Saved.Get("DespairAmount", 1000L);
        paperEsp = Saved.Get("ShowPapers", false);
    }

    void SaveSettings()
    {
        Saved.Set("WalkSpeed", Cheat.Speed);
        Saved.Set("FlySpeed", Cheat.FlySpeed);
        Saved.Set("FreeUpgrades", Cheat.FreeUpgrades);
        Saved.Set("WispMultiplier", Cheat.WispMult);
        Saved.Set("InstantWither", Cheat.InstantWither);
        Saved.Set("HoldToWither", Cheat.HoldToWither);
        Saved.Set("AutoAim", Cheat.AutoAim);
        Saved.Set("ExtraTargets", Cheat.ExtraTargets);
        Saved.Set("NoSwarmCooldown", Cheat.NoSwarmCooldown);
        Saved.Set("WitherRadius", witherRadius);
        Saved.Set("DespairAmount", despairAmount);
        Saved.Set("ShowPapers", paperEsp);
        Saved.Flush();
        SetStatus("Settings saved - they load automatically next time");
    }

    // ---------------- keyboard ----------------
    static bool Pressed(Key k)
    {
        var kb = Keyboard.current;
        if (kb == null || k == Key.None) return false;
        try { return kb[k].wasPressedThisFrame; } catch { return false; }
    }

    bool textHooked;
    readonly Queue<char> typed = new Queue<char>();
    void HookText()
    {
        if (textHooked || Keyboard.current == null) return;
        Keyboard.current.onTextInput += c => { if (editId != null) typed.Enqueue(c); };
        textHooked = true;
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();
        HookText();

        if (Time.unscaledTime - allowedAt > 1f)
        {
            allowedAt = Time.unscaledTime;
            string why;
            try { why = Game.Why; } catch (Exception e) { why = "Error: " + e.Message; }
            bool now = why == null;
            if (why != lastWhy) { Plugin.L.LogInfo(now ? "Cheats on" : "Cheats off: " + why); lastWhy = why; }
            if (allowed && !now) TurnOff();
            allowed = now;
        }

        if (rebinding == null && Pressed(Plugin.KeyMenu.Value)) { editId = null; SetShow(!show); }
        else if (rebinding != null) HandleRebind();
        else if (editId != null) HandleNumberInput();
        else if (allowed)
        {
            if (Pressed(Plugin.KeyFly.Value)) SetFly(!Cheat.Fly);
            if (Pressed(Plugin.KeyWither.Value)) Act("", WitherAround);
        }
        if (editId == null) typed.Clear();

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        Cheat.Allowed = allowed;
        if (!allowed || !Game.InGame) { Cheat.Fly = false; toWither.Clear(); return; }

        // the game sets the time back to normal after its own menus - keep the chosen speed
        if (gameSpeed != 1f && Time.timeScale > 0f && Math.Abs(Time.timeScale - gameSpeed) > 0.01f) Time.timeScale = gameSpeed;

        FlyTick();

        // big amounts of wheat wither a few hundred per frame, so the game does not freeze
        if (toWither.Count > 0)
        {
            var f = Game.Field;
            for (int i = 0; i < WitherPerFrame && toWither.Count > 0; i++)
            {
                int s = toWither.Dequeue();
                if (!f.IsWheatTriggered(s)) f.StartWithering(s);
            }
            if (toWither.Count == 0) SetStatus("Done - the wheat is withering");
        }

        if (paperEsp && Time.unscaledTime - placesScannedAt > 3f) ScanPlaces();
    }

    void FlyTick()
    {
        var rb = Game.Body;
        if (rb == null) return;
        if (!Cheat.Fly)
        {
            if (flyKinematic) { rb.isKinematic = flyWasKinematic; rb.linearVelocity = Vector3.zero; flyKinematic = false; }
            return;
        }
        if (!flyKinematic) { flyWasKinematic = rb.isKinematic; rb.isKinematic = true; flyKinematic = true; }
        var kb = Keyboard.current;
        var cam = Camera.main != null ? Camera.main.transform : Game.Me.transform;
        if (kb == null || show) return;
        Vector3 dir = Vector3.zero;
        if (kb.wKey.isPressed) dir += cam.forward;
        if (kb.sKey.isPressed) dir -= cam.forward;
        if (kb.dKey.isPressed) dir += cam.right;
        if (kb.aKey.isPressed) dir -= cam.right;
        if (kb.spaceKey.isPressed) dir += Vector3.up;
        if (kb.leftCtrlKey.isPressed) dir -= Vector3.up;
        float speed = 8f * Cheat.FlySpeed * (kb.leftShiftKey.isPressed ? 2f : 1f);
        if (dir.sqrMagnitude > 0.001f)
        {
            var pos = Game.Me.transform.position + dir.normalized * speed * Time.unscaledDeltaTime;
            rb.position = pos;
            Game.Me.transform.position = pos;
        }
    }

    void TurnOff()
    {
        Cheat.Allowed = false;
        Cheat.Fly = false;
        flyKinematic = false;
        toWither.Clear();
        placesFor = null;
        if (gameSpeed != 1f) { Time.timeScale = 1f; gameSpeed = 1f; }
    }

    void SetFly(bool on)
    {
        if (on && !Check()) return;
        Cheat.Fly = on;
        SetStatus(on ? "Fly mode on - WASD, Space up, Ctrl down, Shift faster" : "Fly mode off");
    }

    // ---------------- menu open/close ----------------
    CursorLockMode oldLock;
    bool oldVisible;

    void SetShow(bool value)
    {
        if (value == show) return;
        show = value;
        Cheat.MenuOpen = value;
        if (show) { oldLock = Cursor.lockState; oldVisible = Cursor.visible; }
        else
        {
            editId = null; rebinding = null;
            bool free = !Game.InGame || (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) || (UpgradesScreen.Instance != null && UpgradesScreen.Instance.IsOpen);
            if (Game.InGame) { Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free; }
            else { Cursor.lockState = oldLock; Cursor.visible = oldVisible; }
        }
    }

    void LateUpdate()
    {
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ---------------- typed input ----------------
    void HandleNumberInput()
    {
        while (typed.Count > 0)
        {
            char c = typed.Dequeue();
            if (c >= '0' && c <= '9' && editBuf.Length < 12) editBuf += c;
            else if (c == '\b' && editBuf.Length > 0) editBuf = editBuf.Substring(0, editBuf.Length - 1);
        }
        if (Pressed(Key.Delete)) editBuf = "";
        if (editBuf.Length > 0 && long.TryParse(editBuf, out var v)) editSet?.Invoke(Math.Min(editMax, Math.Max(editMin, v)));
        if (Pressed(Key.Enter) || Pressed(Key.NumpadEnter) || Pressed(Key.Escape)) editId = null;
    }

    static Key[] allKeys;
    void HandleRebind()
    {
        if (Pressed(Key.Escape)) { rebinding = null; SetStatus("Cancelled"); return; }
        allKeys = allKeys ?? ((Key[])Enum.GetValues(typeof(Key))).Where(k => k != Key.None && k != Key.Escape && (int)k < 111).ToArray();
        foreach (var k in allKeys)
        {
            if (!Pressed(k)) continue;
            rebinding.Value = k;
            SetStatus($"{rebinding.Definition.Key} → {k}");
            rebinding = null;
            return;
        }
    }

    // ---------------- GUI ----------------
    void OnGUI()
    {
        GUI.matrix = Matrix4x4.identity;
        try { if (allowed && paperEsp && Event.current.type == EventType.Repaint) DrawEsp(); } catch { }
        if (!show) return;
        GUI.depth = -1000;
        Ui.Init();
        bool areaOpen = false;
        var oldMatrix = GUI.matrix;
        try
        {
            // the whole menu scales with "Menu Size" (Settings), so it fits on every screen
            float s = Mathf.Clamp(Plugin.MenuScale.Value, 0.6f, 1.6f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            var rect = new Rect(24, 24, 540, Screen.height / s - 48);
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
        GUI.matrix = oldMatrix;
    }

    void Draw()
    {
        GUILayout.BeginHorizontal();
        if (Ui.Styled && Ui.Logo != null && Ui.Logo.normal.background != null) Ui.Label("", Ui.Logo);
        GUILayout.BeginVertical();
        Ui.Label($"EvilDragon's <color={Ui.BHex}>Cheats</color>", Ui.Title);
        Ui.Label($"Unharvest  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyWither.Value.ToString(), "Wither Around");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!allowed) Ui.Label($"<color=#ff6b6b><b>Cheats are off</b></color> - {lastWhy ?? "start or continue a game first."}", Ui.Sub);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i == 3) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }   // two rows of tabs
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; bodyScroll = Vector2.zero; }
        }
        GUILayout.EndHorizontal();
        Ui.Space(6);

        bool showStatus = status.Length > 0 && Time.unscaledTime - statusTime < 6f;
        bodyScroll = GUILayout.BeginScrollView(bodyScroll, false, false, GUIStyle.none, ScrollBar(), GUIStyle.none);
        // fixed width: long names can't push the content past the right edge
        GUILayout.BeginVertical(GUILayout.Width(BodyWidth), GUILayout.MaxWidth(BodyWidth));
        bool locked = tab != Tabs.Length - 1 && !allowed;
        if (locked) GUI.enabled = false;
        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawDespair(); break;
            case 2: DrawWither(); break;
            case 3: DrawUpgrades(); break;
            case 4: DrawTeleport(); break;
            case 5: DrawSettings(); break;
        }
        if (locked) GUI.enabled = true;
        Ui.Space(4);
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        if (showStatus)
        {
            Ui.Space(6);
            Ui.Label("» " + status, Ui.Status);
        }
    }

    Vector2 bodyScroll;
    // panel 540 - padding 2x18 - scrollbar 6 + 6 gap
    const float BodyWidth = 540f - 36f - 14f;
    GUIStyle scrollBar, scrollThumb;
    Texture2D scrollTex;
    GUIStyle ScrollBar()
    {
        if (scrollBar == null || scrollTex == null)
        {
            scrollTex = new Texture2D(1, 1); scrollTex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.06f)); scrollTex.Apply();
            scrollBar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = 6, margin = new RectOffset(6, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0), border = new RectOffset(0, 0, 0, 0) };
            scrollBar.normal.background = scrollTex;
            var thumbTex = new Texture2D(1, 1);
            ColorUtility.TryParseHtmlString(Ui.AHex, out var a); a.a = 0.8f;
            thumbTex.SetPixel(0, 0, a); thumbTex.Apply();
            scrollThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = 6, border = new RectOffset(0, 0, 0, 0) };
            scrollThumb.normal.background = thumbTex;
        }
        GUI.skin.verticalScrollbarThumb = scrollThumb;
        return scrollBar;
    }

    void NumField(string id, string label, long value, long min, long max, Action<long> set)
    {
        bool editing = editId == id;
        GUILayout.BeginHorizontal();
        Ui.Label(label, Ui.Sub, Ui.W(110));
        string shown = editing ? editBuf : value.ToString("N0");
        if (Ui.Button(shown, editing ? Ui.FieldOn : Ui.Field))
        {
            if (editing) editId = null;
            else { editId = id; editBuf = ""; editMin = min; editMax = max; editSet = set; }
        }
        GUILayout.EndHorizontal();
    }

    static string Grey(string s) => $"   <size=12><color=#b8c2d6>{s}</color></size>";

    // ---------------- tabs ----------------
    void DrawPlayer()
    {
        Ui.BeginCard("Movement");
        Cheat.Speed = Ui.Stepper("Walk Speed", Cheat.Speed, 0.25f, 1f, 5f, "0.00");
        if (Ui.Button("Reset Speed", Ui.Chip)) Cheat.Speed = 1f;
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly through everything and over the whole field");
        bool f = Ui.Toggle(Cheat.Fly, $"Fly  [{Plugin.KeyFly.Value}]", "WASD, Space up, Ctrl down, Shift faster");
        if (f != Cheat.Fly) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        Cheat.FlySpeed = Ui.Chips(Cheat.FlySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();

        Ui.BeginCard("Game Speed", "Everything runs faster or slower");
        float gs = Ui.Chips(gameSpeed, GameSpeeds, v => $"x{v:0.#}", 5);
        if (gs != gameSpeed) { gameSpeed = gs; Time.timeScale = gs; SetStatus($"Game speed x{gs:0.#}"); }
        Ui.EndCard();
    }

    void DrawDespair()
    {
        Ui.BeginCard("Despair", $"{DespairManager.CurrentDespair:N0} to spend  ·  {UpgradePrompt.LifetimeDespair:N0} collected in total");
        despairAmount = Ui.Chips(despairAmount, DespairPresets, v => v.ToString("N0"), 4);
        NumField("despair", "Custom amount", despairAmount, 1, 100_000_000, v => despairAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ {despairAmount:N0}", Ui.Primary)) AddDespair(despairAmount);
        if (Ui.Button($"− {despairAmount:N0}", Ui.Tab)) AddDespair(-despairAmount);
        GUILayout.EndHorizontal();
        Cheat.FreeUpgrades = Ui.Toggle(Cheat.FreeUpgrades, "Free Upgrades", "every upgrade costs nothing");
        Ui.EndCard();

        Ui.BeginCard("Wisp Value", "Every wisp a dead stalk drops is worth more");
        Cheat.WispMult = Ui.Chips(Cheat.WispMult, WispPresets, v => $"x{v:0}", 5);
        Ui.EndCard();
    }

    void DrawWither()
    {
        var f = Game.Field;
        Ui.BeginCard("Wither Around You", $"Everything within {witherRadius:0} m dies - you get the despair as usual");
        float r = Ui.Chips(witherRadius, WitherRadii, v => $"{v:0} m", 4);
        if (r != witherRadius) { witherRadius = r; SetStatus($"Wither range {r:0} m"); }
        if (Ui.Button($"Wither Around You  [{Plugin.KeyWither.Value}]", Ui.Primary)) Act("", WitherAround);
        if (toWither.Count > 0) Ui.Label($"<color={Ui.AHex}>{toWither.Count:N0} stalks still to go…</color>", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Whole Field", f != null ? $"{f.WheatCount:N0} stalks in the field" : null);
        if (Ui.Button("Wither The Whole Field", Ui.Danger)) Act("", WitherAll);
        Ui.Label("Withering everything ends the demo, just like doing it by hand.", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Withering", "Works without buying the upgrades");
        Cheat.InstantWither = Ui.Toggle(Cheat.InstantWither, "Instant Wither", "no charging - a stalk dies right away");
        Cheat.HoldToWither = Ui.Toggle(Cheat.HoldToWither, "Hold To Wither", "keep the mouse button down");
        Cheat.AutoAim = Ui.Toggle(Cheat.AutoAim, "Auto Aim", "finds a stalk near your crosshair");
        Ui.Label("Extra stalks per click", Ui.Sub);
        Cheat.ExtraTargets = Ui.Chips(Cheat.ExtraTargets, TargetPresets, v => v == 0 ? "Normal" : "+" + v, 4);
        Ui.EndCard();

        Ui.BeginCard("Locust Swarm");
        Cheat.NoSwarmCooldown = Ui.Toggle(Cheat.NoSwarmCooldown, "No Cooldown", "send the swarm again right away (once you have bought it)");
        Ui.EndCard();
    }

    void DrawUpgrades()
    {
        var tree = Game.Tree;
        if (tree == null) { Ui.Label("The upgrade tree is not loaded yet.", Ui.Sub); return; }
        var nodes = Game.AllNodes(tree).Where(n => n != null && n.type != UpgradeType.Root).ToList();
        var buyable = nodes.Where(n => !n.isTeaser).ToList();
        int owned = buyable.Count(n => tree.IsPurchased(n));

        Ui.BeginCard("Upgrades", $"{owned} / {buyable.Count} bought  ·  free, no despair needed");
        if (Ui.Button("Buy All Upgrades", Ui.Primary)) Act("", () => Buy(buyable.Where(n => !tree.IsPurchased(n))));
        if (!UpgradePrompt.UpgradesUnlocked) Ui.Label("The upgrade screen (Tab) opens after your first despair - the upgrades work right away anyway.", Ui.Sub);
        Ui.Space(4);
        foreach (var n in buyable)
        {
            GUILayout.BeginHorizontal();
            string name = string.IsNullOrEmpty(n.upgradeName) ? n.name : n.upgradeName;
            Ui.Label($"<b>{name}</b>" + Grey($"{n.cost:N0} despair"), Ui.Text, Ui.W(330));
            if (tree.IsPurchased(n)) Ui.Label("<color=#50ff7a>bought</color>", Ui.Sub);
            else if (Ui.Button("Buy", Ui.Chip)) Act("", () => Buy(new[] { n }));
            GUILayout.EndHorizontal();
        }
        int teasers = nodes.Count - buyable.Count;
        if (teasers > 0) Ui.Label($"{teasers} more upgrades are only in the full game.", Ui.Sub);
        Ui.EndCard();
    }

    // ---------------- actions ----------------
    void AddDespair(long amount)
    {
        Act("", () =>
        {
            if (amount >= 0) DespairManager.AddDespair((int)Math.Min(amount, int.MaxValue - (long)DespairManager.CurrentDespair));
            else DespairManager.SetDespair((int)Math.Max(0, DespairManager.CurrentDespair + amount));
            SetStatus($"Despair: {DespairManager.CurrentDespair:N0}");
        });
    }

    void Buy(IEnumerable<UpgradeData> list)
    {
        var names = list.Select(n => n.name).ToList();
        if (names.Count == 0) { SetStatus("You already have every upgrade"); return; }
        // the game's own way of loading bought upgrades - applies them and refreshes the tree
        Game.Tree.RestorePurchased(names);
        try { SaveManager.Instance?.Save(); } catch { }
        SetStatus(names.Count == 1 ? "Upgrade bought" : $"{names.Count} upgrades bought");
    }

    void WitherAround()
    {
        var f = Game.Field;
        int n = f.FindUntriggeredWheatInRadius(Game.Me.transform.position, witherRadius, found);
        if (n == 0) { SetStatus("No living wheat around you"); return; }
        foreach (int s in found) toWither.Enqueue(s);
        SetStatus($"Withering {n:N0} stalks");
    }

    void WitherAll()
    {
        var f = Game.Field;
        toWither.Clear();
        for (int i = 0; i < f.WheatCount; i++) if (!f.IsWheatTriggered(i)) toWither.Enqueue(i);
        SetStatus(toWither.Count > 0 ? $"Withering {toWither.Count:N0} stalks - this takes a moment" : "The whole field is already dead");
    }

    // ---------------- teleport ----------------
    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        placesFor = SaveManager.Instance;
        var l = new List<Place>();
        var p = new List<Place>();
        try
        {
            var cm = CollectionManager.Instance;
            foreach (var pp in FindObjectsByType<PaperPickup>(FindObjectsSortMode.None))
            {
                if (pp == null || !pp.gameObject.activeInHierarchy) continue;
                var data = Game.PaperOf(pp);
                if (data == null || (cm != null && cm.IsFound(data))) continue;
                p.Add(new Place { Name = string.IsNullOrEmpty(data.title) ? "Paper" : data.title, Group = "Paper", Pos = pp.transform.position });
            }
            int i = 0;
            foreach (var m in FindObjectsByType<MeditationSpot>(FindObjectsSortMode.None))
                if (m != null && m.gameObject.activeInHierarchy) l.Add(new Place { Name = "Meditation Spot" + (++i > 1 ? $" {i}" : ""), Group = "Rest", Pos = m.transform.position });
            foreach (var it in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                if (it == null || !it.gameObject.activeInHierarchy || it.GetComponent<PaperPickup>() != null) continue;
                string w = string.IsNullOrEmpty(it.promptWord) ? it.gameObject.name : it.promptWord;
                l.Add(new Place { Name = w, Group = it.Used && !it.repeatable ? "Already used" : "Something to look at", Pos = it.transform.position });
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("Teleport scan: " + e.Message); }
        places = l;
        papers = p;
    }

    void DrawTeleport()
    {
        if (placesFor != SaveManager.Instance || Time.unscaledTime - placesScannedAt > 3f) ScanPlaces();
        var me = Game.Me;
        Ui.BeginCard("Position");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Save Position", Ui.Chip)) { hasSaved = true; savedPos = me.transform.position; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Vector3 at = me != null ? me.transform.position : Vector3.zero;
        var cm = CollectionManager.Instance;
        Ui.BeginCard("Papers", cm != null ? $"{cm.FoundCount} / {cm.TotalCount} found  ·  every paper makes you faster" : null);
        paperEsp = Ui.Toggle(paperEsp, "Show Papers", "papers you have not found get a label");
        if (papers.Count == 0) Ui.Label("No paper left to find nearby.", Ui.Sub);
        foreach (var p in papers.OrderBy(x => Vector3.Distance(at, x.Pos)))
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{p.Group}  ·  {Vector3.Distance(at, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();

        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places" : "Nothing found yet");
        foreach (var p in places.OrderBy(x => Vector3.Distance(at, x.Pos)))
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{p.Group}  ·  {Vector3.Distance(at, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();
    }

    void TeleportTo(Vector3 pos, string name)
    {
        Act($"Teleported to {name}", () =>
        {
            var target = pos + Vector3.up * 1f;
            var rb = Game.Body;
            if (rb != null) { rb.position = target; if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; }
            Game.Me.transform.position = target;
            placesScannedAt = -100f;
        });
    }

    // ---------------- ESP ----------------
    GUIStyle espStyle, espSubStyle;
    Texture2D espBg;
    void DrawEsp()
    {
        if (papers.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (espStyle == null || espBg == null)
        {
            espStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 18, richText = true };
            espStyle.normal.textColor = Color.white;
            espSubStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 13, richText = true };
            espSubStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            espBg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            espBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f)); espBg.Apply();
        }
        foreach (var p in papers)
        {
            var sp = cam.WorldToScreenPoint(p.Pos + Vector3.up * 0.6f);
            if (sp.z <= 0.2f || sp.z > 2000f) continue;
            string text = $"<color=#c77dff>{p.Name.ToUpperInvariant()}</color>", line2 = $"Paper  ·  {sp.z:0} m";
            var s1 = espStyle.CalcSize(new GUIContent(text));
            var s2 = espSubStyle.CalcSize(new GUIContent(line2));
            float w = Mathf.Max(s1.x, s2.x) + 18f, h = s1.y + s2.y + 6f;
            var box = new Rect(sp.x - w / 2f, Screen.height - sp.y - h, w, h);
            GUI.DrawTexture(box, espBg);
            GUI.Label(new Rect(box.x, box.y + 2f, w, s1.y), text, espStyle);
            GUI.Label(new Rect(box.x, box.y + 2f + s1.y, w, s2.y), line2, espSubStyle);
        }
    }

    // ---------------- settings ----------------
    void DrawSettings()
    {
        Ui.BeginCard("Theme");
        string th = Ui.Chips(Ui.CurrentTheme, Ui.ThemeNames, n => n, 3);
        if (th != Ui.CurrentTheme) { Ui.SetTheme(th); Plugin.Theme.Value = th; scrollBar = null; SetStatus($"Theme: {th}"); }
        Ui.EndCard();

        Ui.BeginCard("Menu Size", "Make the whole menu smaller or bigger - saved right away");
        float ms = Ui.Chips(Plugin.MenuScale.Value, MenuSizes, v => $"{v * 100:0}%", 5);
        if (Math.Abs(ms - Plugin.MenuScale.Value) > 0.001f) { Plugin.MenuScale.Value = ms; SetStatus($"Menu size {ms * 100:0}%"); }
        Ui.EndCard();

        Ui.BeginCard("Keybinds", "Click a key, then press the new key (Esc = cancel)");
        KeyRow("Open menu", Plugin.KeyMenu);
        KeyRow("Fly mode", Plugin.KeyFly);
        KeyRow("Wither around you", Plugin.KeyWither);
        Ui.EndCard();

        Ui.BeginCard("Save", "Saves which cheats are on and your settings for next time");
        if (Ui.Button("Save Settings", Ui.Primary)) SaveSettings();
        Ui.EndCard();
    }

    void KeyRow(string label, ConfigEntry<Key> entry)
    {
        bool active = rebinding == entry;
        GUILayout.BeginHorizontal();
        Ui.Label(label, Ui.Text, Ui.W(220));
        if (Ui.Button(active ? "Press a key…" : entry.Value.ToString(), active ? Ui.FieldOn : Ui.Chip))
            rebinding = active ? null : entry;
        GUILayout.EndHorizontal();
    }

    bool Check()
    {
        if (!Game.InGame) { SetStatus("Start or continue a game first"); return false; }
        return true;
    }

    /// Runs an action; an empty "done" text means the action sets its own status.
    void Act(string done, Action a)
    {
        if (!Check()) return;
        try { a(); if (done.Length > 0) SetStatus(done); }
        catch (Exception e)
        {
            if (e is TargetInvocationException t && t.InnerException != null) e = t.InnerException;
            Plugin.L.LogError(e);
            SetStatus("That did not work: " + e.Message);
        }
    }
}
