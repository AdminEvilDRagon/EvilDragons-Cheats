using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OneMillionCoinsCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - One Million Coins", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.onemillioncoins";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<Key> KeyMenu, KeyFly, KeyDeposit, KeyMagnet;
    internal static ConfigEntry<string> Theme;
    internal static ConfigEntry<float> MenuScale;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", Key.F1, "Open/close menu");
        KeyFly = Config.Bind("Hotkeys", "Fly", Key.F2, "Toggle fly mode (noclip)");
        KeyDeposit = Config.Bind("Hotkeys", "DepositBag", Key.F3, "Bank every coin in your bag, wherever you are");
        KeyMagnet = Config.Bind("Hotkeys", "CoinMagnet", Key.F4, "Toggle the coin magnet");
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

/// What the Harmony patches read. Only active while a run is loaded.
static class Cheat
{
    public static bool Allowed, Fly, Bottomless, AllItems, MenuOpen;
    public static float Speed = 1f, Jump = 1f, FlySpeed = 2f;
    public static float ToolRange = 1f, ToolPower = 1f, ToolSpeed = 1f;
}

/// Private parts of the game the cheats need.
static class G
{
    static readonly Type P = typeof(GameProgressManager);
    public static readonly AccessTools.FieldRef<GameProgressManager, int> Credit = AccessTools.FieldRefAccess<GameProgressManager, int>("_legacySkillPointCredit");
    public static readonly AccessTools.FieldRef<GameProgressManager, HashSet<SkillId>> Learned = AccessTools.FieldRefAccess<GameProgressManager, HashSet<SkillId>>("_learnedSkills");
    public static readonly AccessTools.FieldRef<GameProgressManager, HashSet<ItemId>> Owned = AccessTools.FieldRefAccess<GameProgressManager, HashSet<ItemId>>("_ownedItems");
    public static readonly MethodInfo RefreshSkills = AccessTools.Method(P, "RefreshSkillProgress", new[] { typeof(bool) });
    public static readonly MethodInfo ApplyReward = AccessTools.Method(P, "ApplyLearnedReward", new[] { typeof(SkillDefinition) });
    public static readonly MethodInfo LockReason = AccessTools.Method(P, "SkillLockReason", new[] { typeof(SkillId), typeof(bool), typeof(bool), typeof(bool) });
    public static readonly MethodInfo SetSpent = AccessTools.PropertySetter(P, nameof(GameProgressManager.SpentSkillPoints));
    public static readonly MethodInfo SetRanked = AccessTools.PropertySetter(P, nameof(GameProgressManager.RankingEligible));
    public static readonly MethodInfo SetShovel = AccessTools.PropertySetter(P, nameof(GameProgressManager.ShovelUnlocked));
    public static readonly MethodInfo SetMagnet = AccessTools.PropertySetter(P, nameof(GameProgressManager.MagnetUnlocked));
    public static readonly MethodInfo SetVacuum = AccessTools.PropertySetter(P, nameof(GameProgressManager.VacuumUnlocked));
    public static readonly FieldInfo Changed = AccessTools.Field(P, "ProgressChanged");
    public static readonly AccessTools.FieldRef<SaveManager, bool> StatsValid = AccessTools.FieldRefAccess<SaveManager, bool>("_statisticsValid");

    public static readonly AccessTools.FieldRef<FirstPersonController, float> Pitch = AccessTools.FieldRefAccess<FirstPersonController, float>("_pitch");
    public static readonly AccessTools.FieldRef<FirstPersonController, float> VertVel = AccessTools.FieldRefAccess<FirstPersonController, float>("_verticalVelocity");
    public static readonly AccessTools.FieldRef<FirstPersonController, Transform> Cam = AccessTools.FieldRefAccess<FirstPersonController, Transform>("_cameraTransform");
    public static readonly AccessTools.FieldRef<FirstPersonController, GoldDiggerHUD> Hud = AccessTools.FieldRefAccess<FirstPersonController, GoldDiggerHUD>("_hud");

    /// Tells the HUD, the skill tree and the save system that something changed.
    public static void NotifyChanged(GameProgressManager p)
    {
        try { (Changed.GetValue(p) as Action)?.Invoke(); } catch (Exception e) { Plugin.L.LogWarning("Refresh: " + e.Message); }
    }

    /// Gives the run every item (tools, bags, boots...). The game rebuilds the item list from
    /// the learned skills on every load, so nothing of this ends up in the save.
    public static int GiveAllItems(GameProgressManager p)
    {
        var owned = Owned(p);
        int n = 0;
        foreach (var it in ItemCatalog.All)
            if (it != null && it.Id != ItemId.VaultPlush && owned.Add(it.Id)) n++;
        SetShovel.Invoke(p, new object[] { true });
        SetMagnet.Invoke(p, new object[] { true });
        SetVacuum.Invoke(p, new object[] { true });
        return n;
    }
}

static class Patches
{
    // walk speed / jump height
    [HarmonyPostfix, HarmonyPatch(typeof(GameProgressManager), nameof(GameProgressManager.MovementSpeedMultiplier), MethodType.Getter)]
    static void Speed(ref float __result) { if (Cheat.Allowed) __result *= Cheat.Speed; }

    [HarmonyPostfix, HarmonyPatch(typeof(GameProgressManager), nameof(GameProgressManager.JumpHeightMultiplier), MethodType.Getter)]
    static void Jump(ref float __result) { if (Cheat.Allowed) __result *= Cheat.Jump; }

    // bottomless bag
    [HarmonyPostfix, HarmonyPatch(typeof(GameProgressManager), nameof(GameProgressManager.CarryCapacity), MethodType.Getter)]
    static void Capacity(ref int __result) { if (Cheat.Allowed && Cheat.Bottomless) __result = Math.Max(__result, 999_999); }

    // stronger tools: bigger radius and reach, more coins per use, faster
    [HarmonyPostfix, HarmonyPatch(typeof(ToolController), nameof(ToolController.GetEffectiveTool))]
    static void ToolStats(ref ToolDefinition __result)
    {
        if (!Cheat.Allowed) return;
        if (Cheat.ToolRange != 1f) { __result.radius *= Cheat.ToolRange; __result.reach *= Cheat.ToolRange; }
        if (Cheat.ToolPower != 1f) __result.strength = (int)Mathf.Min(__result.strength * Cheat.ToolPower, 1_000_000f);
        if (Cheat.ToolSpeed != 1f)
        {
            __result.interval = Mathf.Max(0.02f, __result.interval / Cheat.ToolSpeed);
            __result.flightDuration = Mathf.Max(0.08f, __result.flightDuration / Cheat.ToolSpeed);
        }
    }

    // "all items" survives loading: the game clears the items and gives back only the skill rewards
    [HarmonyPostfix, HarmonyPatch(typeof(GameProgressManager), "RestoreSkills")]
    static void KeepItems(GameProgressManager __instance)
    {
        if (Cheat.AllItems) try { G.GiveAllItems(__instance); } catch (Exception e) { Plugin.L.LogWarning("Items: " + e.Message); }
    }

    // while the menu is open your character stands still, tools do nothing and the mouse is free
    [HarmonyPostfix, HarmonyPatch(typeof(GoldDiggerHUD), nameof(GoldDiggerHUD.BlocksGameplay), MethodType.Getter)]
    static void Block(ref bool __result) { if (Cheat.MenuOpen) __result = true; }

    [HarmonyPostfix, HarmonyPatch(typeof(GoldDiggerHUD), "MenuCursorRequired", MethodType.Getter)]
    static void FreeCursor(ref bool __result) { if (Cheat.MenuOpen) __result = true; }

    // fly: free flight through walls and gold instead of walking
    [HarmonyPrefix, HarmonyPatch(typeof(FirstPersonController), "Update")]
    static bool FlyUpdate(FirstPersonController __instance)
    {
        if (!(Cheat.Allowed && Cheat.Fly)) return true;
        try
        {
            G.VertVel(__instance) = 0f;
            if (!PresentationSettings.WindowHasFocus) return false;
            var hud = G.Hud(__instance);
            if (hud != null && hud.BlocksGameplay) return false;
            var cam = G.Cam(__instance) ?? __instance.transform;

            Vector2 look = GameInput.LookDelta(0.11f);
            if (PresentationSettings.InvertLook) look.y = -look.y;
            __instance.transform.Rotate(0f, look.x, 0f);
            G.Pitch(__instance) = Mathf.Clamp(G.Pitch(__instance) - look.y, -85f, 85f);
            cam.localRotation = Quaternion.Euler(G.Pitch(__instance), 0f, 0f);

            var kb = Keyboard.current;
            if (kb == null) return false;
            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += cam.forward;
            if (kb.sKey.isPressed) dir -= cam.forward;
            if (kb.dKey.isPressed) dir += cam.right;
            if (kb.aKey.isPressed) dir -= cam.right;
            if (kb.spaceKey.isPressed) dir += Vector3.up;
            if (kb.leftCtrlKey.isPressed) dir -= Vector3.up;
            float speed = 8f * Cheat.FlySpeed * (kb.leftShiftKey.isPressed ? 2f : 1f);
            if (dir.sqrMagnitude > 0.001f) __instance.transform.position += dir.normalized * speed * Time.deltaTime;
        }
        catch { }
        return false;
    }
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
    public static GameProgressManager P => GameProgressManager.Instance;

    static FirstPersonController me;
    static CoinFieldManager pile;
    static SaveManager saves;
    static GameProgressManager foundFor;
    static float lookedAt = -10f;

    /// Finds the player, the gold pile and the save system once per run (searching the scene is slow).
    static void Find()
    {
        if (foundFor == P && me != null && pile != null && saves != null) return;
        if (foundFor != P) { me = null; pile = null; saves = null; foundFor = P; }
        if (Time.unscaledTime - lookedAt < 1f) return;
        lookedAt = Time.unscaledTime;
        if (me == null) me = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
        if (pile == null) pile = UnityEngine.Object.FindObjectOfType<CoinFieldManager>();
        if (saves == null) saves = UnityEngine.Object.FindObjectOfType<SaveManager>();
    }

    public static FirstPersonController Me { get { Find(); return me; } }
    public static CoinFieldManager Field { get { Find(); return pile; } }
    public static SaveManager Saves { get { Find(); return saves; } }
    public static GoldDiggerHUD Hud => Me != null ? G.Hud(Me) : null;

    public static bool InGame => P != null && !GameNavigation.IsLoading && Me != null && Me.isActiveAndEnabled;

    /// Why the cheats are off right now (null = they are on).
    public static string Why => InGame ? null : "Start or continue a run first.";

    /// Cheated runs leave the Steam leaderboards (speedrun times and banked coins) alone.
    /// The game does the same for its own testing loadout.
    public static void Taint()
    {
        var p = P;
        if (p == null) return;
        try { if (p.RankingEligible) G.SetRanked.Invoke(p, new object[] { false }); } catch { }
        try { var s = Saves; if (s != null && G.StatsValid(s)) G.StatsValid(s) = false; } catch { }
    }
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Coins", "Skills", "Tools", "Teleport", "Settings" };
    static readonly float[] MenuSizes = { 0.75f, 0.85f, 1f, 1.15f, 1.3f };

    // player
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };
    float gameSpeed = 1f;
    static readonly float[] GameSpeeds = { 0.5f, 1f, 2f, 3f, 5f };

    // coins
    long coinAmount = 1000;
    static readonly long[] CoinPresets = { 100, 1_000, 10_000, 100_000 };
    bool magnet, autoDeposit;
    float magnetRadius = 6f;
    static readonly float[] MagnetRadii = { 3f, 6f, 10f, 16f };
    int pendingBank, pendingBag;
    float magnetAt, depositAt;
    readonly List<GranularCoinPose> feedback = new List<GranularCoinPose>();

    // skills
    long pointAmount = 10;
    static readonly long[] PointPresets = { 1, 5, 10, 50 };

    // tools
    static readonly float[] RangePresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] PowerPresets = { 1f, 2f, 5f, 10f };
    static readonly float[] ToolSpeedPresets = { 1f, 2f, 3f, 5f };

    // teleport
    class Place { public string Name, Group; public Vector3 Pos; }
    List<Place> places = new List<Place>();
    GameProgressManager placesFor;
    float placesScannedAt = -100f;
    bool hasSaved;
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
        Cheat.Jump = Saved.Get("JumpPower", 1f);
        Cheat.FlySpeed = Saved.Get("FlySpeed", 2f);
        Cheat.Bottomless = Saved.Get("BottomlessBag", false);
        Cheat.AllItems = Saved.Get("AllItems", false);
        Cheat.ToolRange = Saved.Get("ToolRange", 1f);
        Cheat.ToolPower = Saved.Get("ToolPower", 1f);
        Cheat.ToolSpeed = Saved.Get("ToolSpeed", 1f);
        magnet = Saved.Get("CoinMagnet", false);
        magnetRadius = Saved.Get("MagnetRadius", 6f);
        autoDeposit = Saved.Get("AutoDeposit", false);
        coinAmount = Saved.Get("CoinAmount", 1000L);
        pointAmount = Saved.Get("PointAmount", 10L);
    }

    void SaveSettings()
    {
        Saved.Set("WalkSpeed", Cheat.Speed);
        Saved.Set("JumpPower", Cheat.Jump);
        Saved.Set("FlySpeed", Cheat.FlySpeed);
        Saved.Set("BottomlessBag", Cheat.Bottomless);
        Saved.Set("AllItems", Cheat.AllItems);
        Saved.Set("ToolRange", Cheat.ToolRange);
        Saved.Set("ToolPower", Cheat.ToolPower);
        Saved.Set("ToolSpeed", Cheat.ToolSpeed);
        Saved.Set("CoinMagnet", magnet);
        Saved.Set("MagnetRadius", magnetRadius);
        Saved.Set("AutoDeposit", autoDeposit);
        Saved.Set("CoinAmount", coinAmount);
        Saved.Set("PointAmount", pointAmount);
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
            if (Pressed(Plugin.KeyDeposit.Value)) Act("", DepositBag);
            if (Pressed(Plugin.KeyMagnet.Value)) SetMagnet(!magnet);
        }
        if (editId == null) typed.Clear();

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    bool AnyCheatOn => Cheat.Fly || Cheat.Bottomless || Cheat.AllItems || magnet || autoDeposit || Cheat.Speed != 1f || Cheat.Jump != 1f
        || Cheat.ToolRange != 1f || Cheat.ToolPower != 1f || Cheat.ToolSpeed != 1f || gameSpeed != 1f;

    bool itemsGiven;

    void Tick()
    {
        Cheat.Allowed = allowed;
        if (!allowed || !Game.InGame) { Cheat.Fly = false; itemsGiven = false; return; }
        var p = Game.P;
        if (AnyCheatOn) Game.Taint();

        if (Cheat.AllItems && !itemsGiven) { itemsGiven = true; if (G.GiveAllItems(p) > 0) G.NotifyChanged(p); }
        if (!Cheat.AllItems) itemsGiven = false;

        // the game sets the time back to normal after its own menus - keep the chosen speed
        if (gameSpeed != 1f && Time.timeScale > 0f && Math.Abs(Time.timeScale - gameSpeed) > 0.01f) Time.timeScale = gameSpeed;

        // big amounts are taken from the gold pile in parts, so the game does not freeze
        if (pendingBank > 0 || pendingBag > 0) WorkPending();

        if (magnet && !show && Time.unscaledTime - magnetAt > 0.2f) { magnetAt = Time.unscaledTime; MagnetTick(); }
        if (autoDeposit && Time.unscaledTime - depositAt > 0.5f)
        {
            depositAt = Time.unscaledTime;
            if (p.CarriedCoins > 0) p.DepositCoins(p.CarriedCoins, manualDelivery: false);
        }
    }

    void TurnOff()
    {
        Cheat.Allowed = false;
        Cheat.Fly = false;
        pendingBank = pendingBag = 0;
        placesFor = null;
        if (gameSpeed != 1f) { Time.timeScale = 1f; gameSpeed = 1f; }
    }

    void SetFly(bool on)
    {
        if (on && !Check()) return;
        Cheat.Fly = on;
        if (on) Game.Taint();
        SetStatus(on ? "Fly mode on - WASD, Space up, Ctrl down, Shift faster" : "Fly mode off");
    }

    void SetMagnet(bool on)
    {
        if (on && !Check()) return;
        magnet = on;
        SetStatus(on ? $"Coin magnet on - pulls the gold within {magnetRadius:0} m into your bag" : "Coin magnet off");
    }

    // ---------------- menu open/close ----------------
    void SetShow(bool value)
    {
        if (value == show) return;
        show = value;
        Cheat.MenuOpen = value;
        if (!show) { editId = null; rebinding = null; }
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
        if (!show) return;
        GUI.depth = -1000;   // above the game's own HUD
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
        Ui.Label($"One Million Coins  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyDeposit.Value.ToString(), "Bank Bag");
        Ui.KeyHint(Plugin.KeyMagnet.Value.ToString(), "Magnet");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!allowed) Ui.Label($"<color=#ff6b6b><b>Cheats are off</b></color> - {lastWhy ?? "start or continue a run first."}", Ui.Sub);

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
            case 1: DrawCoins(); break;
            case 2: DrawSkills(); break;
            case 3: DrawTools(); break;
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
        Ui.Label("Jump height", Ui.Sub);
        Cheat.Jump = Ui.Chips(Cheat.Jump, JumpPresets, v => $"x{v:0.#}", 4);
        if (Ui.Button("Reset Movement", Ui.Chip)) { Cheat.Speed = 1f; Cheat.Jump = 1f; }
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly through walls and through the gold");
        bool f = Ui.Toggle(Cheat.Fly, $"Fly  [{Plugin.KeyFly.Value}]", "WASD, Space up, Ctrl down, Shift faster");
        if (f != Cheat.Fly) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        Cheat.FlySpeed = Ui.Chips(Cheat.FlySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();

        Ui.BeginCard("Game Speed", "Everything runs faster or slower");
        float gs = Ui.Chips(gameSpeed, GameSpeeds, v => $"x{v:0.#}", 5);
        if (gs != gameSpeed) { gameSpeed = gs; Time.timeScale = gs; SetStatus($"Game speed x{gs:0.#}"); }
        Ui.EndCard();

        Ui.BeginCard("Leaderboards", "Runs with cheats are kept off the Steam leaderboards");
        Ui.Label("Speedrun times and banked coins of a cheated run are not uploaded - the game does the same for its own test mode. Achievements still work.", Ui.Sub);
        Ui.EndCard();
    }

    void DrawCoins()
    {
        var p = Game.P;
        if (p == null) return;
        Ui.BeginCard("Bank", $"{p.TotalDepositedCoins:N0} / 1,000,000 banked  ·  {p.UnclaimedWorldCoins:N0} still in the pile");
        coinAmount = Ui.Chips(coinAmount, CoinPresets, v => v.ToString("N0"), 4);
        NumField("coins", "Custom amount", coinAmount, 1, 999_999, v => coinAmount = v);
        Ui.Label("The coins come out of the gold pile (the ones nearest to you), so the pile really gets smaller.", Ui.Sub);
        if (Ui.Button($"Bank {coinAmount:N0} Coins", Ui.Primary)) Act("", () => QueueCoins(bank: true));
        if (Ui.Button($"Put {coinAmount:N0} Coins In Your Bag", Ui.Tab)) Act("", () => QueueCoins(bank: false));
        if (pendingBank > 0 || pendingBag > 0) Ui.Label($"<color={Ui.AHex}>Taking {pendingBank + pendingBag:N0} more coins from the pile…</color>", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Bag", $"{p.CarriedCoins:N0} / {p.CarryCapacity:N0} coins");
        Cheat.Bottomless = Ui.Toggle(Cheat.Bottomless, "Bottomless Bag", "your bag is never full");
        if (Ui.Button($"Bank Your Bag Now  [{Plugin.KeyDeposit.Value}]", Ui.Primary)) Act("", DepositBag);
        autoDeposit = Ui.Toggle(autoDeposit, "Auto Bank", "everything you pick up goes straight into the bank");
        Ui.EndCard();

        Ui.BeginCard("Coin Magnet", "Pulls the gold around you into your bag");
        bool m = Ui.Toggle(magnet, $"Coin Magnet  [{Plugin.KeyMagnet.Value}]", "works with every tool, even your hands");
        if (m != magnet) SetMagnet(m);
        Ui.Label("Magnet range", Ui.Sub);
        float r = Ui.Chips(magnetRadius, MagnetRadii, v => $"{v:0} m", 4);
        if (r != magnetRadius) { magnetRadius = r; SetStatus($"Magnet range {r:0} m"); }
        Ui.EndCard();
    }

    void DrawSkills()
    {
        var p = Game.P;
        if (p == null) return;
        var tree = p.SkillTree;
        int total = tree != null && tree.All != null ? tree.All.Length : 0;
        int learned = 0;
        if (total > 0) foreach (var s in tree.All) if (s != null && p.HasSkill(s.Id)) learned++;

        Ui.BeginCard("Skill Points", $"{p.AvailableSkillPoints:N0} to spend  ·  {learned} / {total} skills learned");
        pointAmount = Ui.Chips(pointAmount, PointPresets, v => "+" + v, 4);
        NumField("points", "Custom amount", pointAmount, 1, 9999, v => pointAmount = v);
        if (Ui.Button($"+ {pointAmount:N0} Skill Points", Ui.Primary)) Act("", () => AddPoints((int)pointAmount));
        Ui.Label("Spend them in the game's skill tree as usual. Extra points stay in your save.", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Learn Everything", "Every skill at once - for free, no coin goal needed");
        if (Ui.Button("Learn All Skills", Ui.Primary)) Act("", LearnAll);
        Ui.Label("When two skills exclude each other, the first one is taken. Tools that are skill rewards come with it.", Ui.Sub);
        Ui.EndCard();
    }

    void DrawTools()
    {
        var p = Game.P;
        Ui.BeginCard("Items", "Every tool, bag and pair of boots");
        bool ai = Ui.Toggle(Cheat.AllItems, "Unlock All Items", "switch tools in your inventory (Tab) - nothing is changed in your save");
        if (ai != Cheat.AllItems)
        {
            Cheat.AllItems = ai; itemsGiven = false;
            SetStatus(ai ? "Every item unlocked - open your inventory to equip a tool" : "Items go back to normal the next time the run loads");
        }
        if (p != null) Ui.Label($"Equipped: <b>{p.EquippedTool}</b>", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Tool Power", "For every tool - also your hands");
        Ui.Label("Range - bigger area and reach", Ui.Sub);
        Cheat.ToolRange = Ui.Chips(Cheat.ToolRange, RangePresets, v => $"x{v:0.#}", 4);
        Ui.Label("Strength - more coins per use", Ui.Sub);
        Cheat.ToolPower = Ui.Chips(Cheat.ToolPower, PowerPresets, v => $"x{v:0}", 4);
        Ui.Label("Speed - faster use", Ui.Sub);
        Cheat.ToolSpeed = Ui.Chips(Cheat.ToolSpeed, ToolSpeedPresets, v => $"x{v:0}", 4);
        if (Ui.Button("Reset Tool Power", Ui.Chip)) { Cheat.ToolRange = Cheat.ToolPower = Cheat.ToolSpeed = 1f; }
        Ui.EndCard();
    }

    // ---------------- coins ----------------
    void QueueCoins(bool bank)
    {
        var p = Game.P;
        var field = Game.Field;
        if (field == null || !field.UsesGranularPile) { SetStatus("This level has no gold pile to take coins from"); return; }
        int left = p.UnclaimedWorldCoins - pendingBank - pendingBag;
        if (left <= 0) { SetStatus("The pile is empty - every coin is collected"); return; }
        int n = (int)Math.Min(coinAmount, left);
        if (!bank)
        {
            int space = p.AvailableCarrySpace - pendingBag;
            if (space <= 0) { SetStatus("Your bag is full - bank it first or turn on Bottomless Bag"); return; }
            n = Math.Min(n, space);
            pendingBag += n;
        }
        else pendingBank += n;
        Game.Taint();
        SetStatus(bank ? $"Banking {n:N0} coins" : $"{n:N0} coins go into your bag");
    }

    const int CoinsPerFrame = 20_000;

    void WorkPending()
    {
        var p = Game.P;
        var field = Game.Field;
        if (field == null || !field.UsesGranularPile) { pendingBank = pendingBag = 0; return; }
        Vector3 at = Game.Me.transform.position;
        int budget = CoinsPerFrame;
        if (pendingBank > 0)
        {
            int want = Math.Min(Math.Min(pendingBank, budget), p.UnclaimedWorldCoins);
            int got = want > 0 ? field.ExtractWallCoins(at, float.NegativeInfinity, float.PositiveInfinity, want, feedback) : 0;
            if (got > 0) p.BankWorldCoins(got);
            pendingBank = got <= 0 ? 0 : pendingBank - got;
            budget -= Math.Max(0, got);
            if (pendingBank == 0) SetStatus($"Done - {p.TotalDepositedCoins:N0} coins banked");
        }
        if (pendingBag > 0 && budget > 0)
        {
            int reserved = p.ReserveCarrySpace(Math.Min(pendingBag, budget));
            int got = reserved > 0 ? field.ExtractWallCoins(at, float.NegativeInfinity, float.PositiveInfinity, reserved, feedback) : 0;
            p.CommitCarryReservation(reserved, got);
            pendingBag = got <= 0 ? 0 : pendingBag - got;
            if (pendingBag == 0) SetStatus($"Done - {p.CarriedCoins:N0} coins in your bag");
        }
    }

    void MagnetTick()
    {
        var p = Game.P;
        var field = Game.Field;
        if (field == null || !field.UsesGranularPile || p.IsComplete) return;
        int reserved = p.ReserveCarrySpace(1500);
        if (reserved <= 0) return;
        int got = 0;
        try { got = field.ExtractGranular(Game.Me.transform.position, magnetRadius, reserved, feedback); }
        finally { p.CommitCarryReservation(reserved, got); }
    }

    void DepositBag()
    {
        var p = Game.P;
        int n = p.CarriedCoins;
        if (n <= 0) { SetStatus("Your bag is empty"); return; }
        int got = p.DepositCoins(n, manualDelivery: true);
        SetStatus(got > 0 ? $"Banked {got:N0} coins  ·  {p.TotalDepositedCoins:N0} / 1,000,000" : "Nothing could be banked right now");
    }

    // ---------------- skills ----------------
    void AddPoints(int n)
    {
        var p = Game.P;
        int max = Math.Max(0, p.SkillTree.TotalPointCost);
        int before = p.AvailableSkillPoints;
        G.Credit(p) = Math.Min(max, G.Credit(p) + n);   // the game keeps at most this many bonus points in a save
        G.RefreshSkills.Invoke(p, new object[] { true });
        G.NotifyChanged(p);
        int got = p.AvailableSkillPoints - before;
        SetStatus(got > 0 ? $"+{got} skill points  ·  {p.AvailableSkillPoints} to spend" : "You already have every skill point there is");
    }

    void LearnAll()
    {
        var p = Game.P;
        var tree = p.SkillTree;
        var learned = G.Learned(p);
        int count = 0;
        bool any = true;
        // skills unlock other skills, so go over the tree until nothing new can be learned
        for (int pass = 0; any && pass < 64; pass++)
        {
            any = false;
            foreach (var s in tree.All)
            {
                if (s == null || p.HasSkill(s.Id)) continue;
                // the game's own check, without the point cost and the coin goal
                if (G.LockReason.Invoke(p, new object[] { s.Id, false, true, false }) != null) continue;
                learned.Add(s.Id);
                G.SetSpent.Invoke(p, new object[] { p.SpentSkillPoints + s.Cost });
                G.ApplyReward.Invoke(p, new object[] { s });
                count++;
                any = true;
            }
        }
        // the points that were spent are added as bonus points, so the save stays valid
        G.RefreshSkills.Invoke(p, new object[] { false });
        int missing = p.SpentSkillPoints - p.EarnedSkillPoints;
        if (missing > 0) { G.Credit(p) += missing; G.RefreshSkills.Invoke(p, new object[] { false }); }
        G.NotifyChanged(p);
        SetStatus(count > 0 ? $"Learned {count} skills" : "There is no skill left to learn");
    }

    // ---------------- teleport ----------------
    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        placesFor = Game.P;
        var l = new List<Place>();
        void Add(Component c, string name, string group) { if (c != null && c.gameObject.activeInHierarchy) l.Add(new Place { Name = name, Group = group, Pos = c.transform.position }); }
        try
        {
            int i = 0;
            foreach (var d in FindObjectsOfType<DepositMachine>()) Add(d, "Deposit Machine" + (++i > 1 ? $" {i}" : ""), "Bank your coins");
            foreach (var t in FindObjectsOfType<UpgradeTerminal>()) Add(t, "Skill Terminal", "Skill tree");
            foreach (var t in FindObjectsOfType<ShopTerminal>()) Add(t, "Shop Terminal", "Shop");
            foreach (var t in FindObjectsOfType<InfrastructureTerminal>()) Add(t, "Infrastructure Terminal", "Machines");
            foreach (var e in FindObjectsOfType<ElevatorControl>()) Add(e, "Elevator Controls", "Cargo elevator");
            foreach (var lift in FindObjectsOfType<CargoElevator>())
            {
                if (lift == null || lift.Car == null || lift.Car.parent == null) continue;
                var parent = lift.Car.parent;
                for (int f = lift.LayerCount - 1; f >= 0; f--)
                {
                    // the landing in front of the elevator on that floor
                    var pos = lift.FloorPosition(f) + parent.TransformVector(new Vector3(0f, 0.3f, -2.6f));
                    l.Add(new Place { Name = $"Floor {lift.GetLayerName(f)}", Group = "Elevator landing", Pos = pos });
                }
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("Teleport scan: " + e.Message); }
        places = l;
    }

    void DrawTeleport()
    {
        if (placesFor != Game.P || Time.unscaledTime - placesScannedAt > 10f) ScanPlaces();
        var me = Game.Me;
        Ui.BeginCard("Position");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Save Position", Ui.Chip)) { hasSaved = true; savedPos = me.transform.position; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        if (Ui.Button("Back to Start", Ui.Chip)) Act("Back at the start", () => { if (!me.ReturnToSpawn()) throw new Exception("the game does not allow it right now"); });
        GUILayout.EndHorizontal();
        Ui.Label("Stuck in the gold? Turn on fly mode, it goes through everything.", Ui.Sub);
        Ui.EndCard();

        Vector3 at = me != null ? me.transform.position : Vector3.zero;
        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places" : "Nothing found yet");
        foreach (var p in places)
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{p.Group}  ·  {Vector3.Distance(at, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();
    }

    void TeleportTo(Vector3 pos, string name)
    {
        Act($"Teleported to {name}", () =>
        {
            var me = Game.Me;
            var cc = me.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            me.transform.position = pos + Vector3.up * 0.5f;
            if (cc != null) cc.enabled = was;
            G.VertVel(me) = 0f;
        });
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
        KeyRow("Bank your bag", Plugin.KeyDeposit);
        KeyRow("Coin magnet", Plugin.KeyMagnet);
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

    // ---------------- actions ----------------
    bool Check()
    {
        if (!Game.InGame) { SetStatus("Start or continue a run first"); return false; }
        return true;
    }

    /// Runs an action; an empty "done" text means the action sets its own status.
    void Act(string done, Action a)
    {
        if (!Check()) return;
        try { Game.Taint(); a(); if (done.Length > 0) SetStatus(done); }
        catch (Exception e)
        {
            if (e is TargetInvocationException t && t.InnerException != null) e = t.InnerException;
            Plugin.L.LogError(e);
            SetStatus("That did not work: " + e.Message);
        }
    }
}
