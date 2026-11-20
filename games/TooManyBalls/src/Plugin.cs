using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using HarmonyLib;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TooManyBallsCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Too Many Balls", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.toomanyballs";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<Key> KeyMenu, KeyFly, KeyCollect, KeySell;
    internal static ConfigEntry<string> Theme;
    internal static ConfigEntry<float> MenuScale;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", Key.F1, "Open/close menu");
        KeyFly = Config.Bind("Hotkeys", "Fly", Key.F2, "Toggle fly mode (noclip)");
        KeyCollect = Config.Bind("Hotkeys", "CollectArea", Key.F3, "Collect every ball in the area you are in");
        KeySell = Config.Bind("Hotkeys", "SellAll", Key.F4, "Sell all your balls, wherever you are");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        MenuScale = Config.Bind("Interface", "MenuScale", 1f, "Size of the menu (0.75 - 1.3)");
        Ui.InitialTheme = Theme.Value;
        Ui.Log = m => Logger.LogWarning(m);

        var go = new GameObject("EvilDragonCheatMenu");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        try { new Harmony(Guid).PatchAll(typeof(Patches)); } catch (Exception e) { Logger.LogError(e); }
        Logger.LogInfo($"EvilDragon's Cheats v{Version} loaded");
    }
}

/// What the Harmony patches read. Only active while a save is loaded.
static class Cheat
{
    public static bool Allowed, Fly, FreeShop, Bottomless, InstantPick, InfBombs, BombAll, NoOverheat, NoCooldowns;
    public static float FlySpeed = 2f, SellMult = 1f, BombRadius = 1f;
    public static int GrabSize;   // 0 = normal
    public static bool MenuOpen;

    public static readonly int[] GrabBalls = { 0, 25, 75, 200 };
    public static readonly float[] GrabRadius = { 0f, 1.5f, 3f, 5f };
}

static class Patches
{
    // free upgrades: every purchase (and helper wage) succeeds and costs nothing
    [HarmonyPrefix, HarmonyPatch(typeof(EconomyManager), nameof(EconomyManager.TrySpend))]
    static bool Spend(ref bool __result)
    {
        if (!(Cheat.Allowed && Cheat.FreeShop)) return true;
        __result = true;
        return false;
    }

    // every ball sells for more
    [HarmonyPostfix, HarmonyPatch(typeof(EconomyManager), nameof(EconomyManager.GetSellPrice))]
    static void SellPrice(ref float __result)
    {
        if (Cheat.Allowed && Cheat.SellMult != 1f) __result *= Cheat.SellMult;
    }

    // bottomless basket
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.CanPick))]
    static void CanPick(ref bool __result)
    {
        if (Cheat.Allowed && Cheat.Bottomless) __result = true;
    }

    // instant pickup: almost no wait between two grabs. A tiny gap stays on purpose - grabbing
    // every single frame (while holding the mouse) starts hundreds of ball animations per frame and lags.
    static readonly AccessTools.FieldRef<BallPicker, float> PickCooldown = AccessTools.FieldRefAccess<BallPicker, float>("pickCooldownTimer");
    static float MinPickGap => Cheat.GrabSize >= 2 ? 0.2f : 0.08f;   // huge grabs move hundreds of balls at once

    [HarmonyPrefix, HarmonyPatch(typeof(BallPicker), "HandlePickInput")]
    static void NoPickCooldown(BallPicker __instance)
    {
        if (Cheat.Allowed && Cheat.InstantPick && PickCooldown(__instance) > MinPickGap) PickCooldown(__instance) = MinPickGap;
    }

    // bigger grab: one click takes many balls around the one you aim at
    static readonly AccessTools.FieldRef<BallPicker, Collider[]> PickResults = AccessTools.FieldRefAccess<BallPicker, Collider[]>("results");
    static readonly AccessTools.FieldRef<BallPicker, GameData> PickData = AccessTools.FieldRefAccess<BallPicker, GameData>("data");
    static float savedRadius = -1f;
    static int savedGreedy = -1, savedGreedyLevel;

    [HarmonyPrefix, HarmonyPatch(typeof(BallPicker), "TryPickOnce")]
    static void BigGrab(BallPicker __instance)
    {
        savedRadius = -1f; savedGreedy = -1;
        int g = Cheat.GrabSize;
        if (!Cheat.Allowed || g <= 0 || g >= Cheat.GrabBalls.Length) return;
        var data = PickData(__instance);
        var vals = __instance.config != null ? __instance.config.greedyValues : null;
        if (data == null || vals == null || data.greedyLevel < 0 || data.greedyLevel >= vals.Length) return;
        if (PickResults(__instance) == null || PickResults(__instance).Length < 256) PickResults(__instance) = new Collider[256];
        savedRadius = __instance.sphereRadius;
        savedGreedyLevel = data.greedyLevel;
        savedGreedy = vals[savedGreedyLevel];
        __instance.sphereRadius = Mathf.Max(savedRadius, Cheat.GrabRadius[g]);
        vals[savedGreedyLevel] = Mathf.Max(savedGreedy, Cheat.GrabBalls[g]);
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(BallPicker), "TryPickOnce")]
    static void BigGrabEnd(BallPicker __instance)
    {
        if (savedRadius >= 0f) __instance.sphereRadius = savedRadius;
        if (savedGreedy >= 0 && __instance.config != null) __instance.config.greedyValues[savedGreedyLevel] = savedGreedy;
        savedRadius = -1f; savedGreedy = -1;
    }

    // infinite bombs
    [HarmonyPrefix, HarmonyPatch(typeof(ToolCooldownManager), nameof(ToolCooldownManager.TryUseGrenade))]
    static bool Grenade(ref bool __result)
    {
        if (!(Cheat.Allowed && Cheat.InfBombs)) return true;
        __result = true;
        return false;
    }

    // bombs pull every ball in range, and the range can be bigger
    [HarmonyPostfix, HarmonyPatch(typeof(MagnetBombScript), nameof(MagnetBombScript.maxBalls), MethodType.Getter)]
    static void BombBalls(ref float __result)
    {
        if (Cheat.Allowed && Cheat.BombAll) __result = 100000f;
    }

    static readonly AccessTools.FieldRef<MagnetBombScript, bool> BombTriggered = AccessTools.FieldRefAccess<MagnetBombScript, bool>("triggered");

    [HarmonyPrefix, HarmonyPatch(typeof(MagnetBombScript), nameof(MagnetBombScript.TriggerMagnet))]
    static void BombRange(MagnetBombScript __instance)
    {
        if (BombTriggered(__instance)) return;
        if (Cheat.Allowed && Cheat.BombRadius > 1f) __instance.radius *= Cheat.BombRadius;
        BombCleanup.Remember(__instance);
    }

    // vacuum never overheats
    [HarmonyPrefix, HarmonyPatch(typeof(ToolCooldownManager), nameof(ToolCooldownManager.AddVacuumHeat))]
    static bool VacuumHeat() => !(Cheat.Allowed && Cheat.NoOverheat);

    // scanner and teleport without cooldown
    [HarmonyPrefix, HarmonyPatch(typeof(TPCalculator), nameof(TPCalculator.TryUse))]
    static bool TpReady(ref bool __result)
    {
        if (!(Cheat.Allowed && Cheat.NoCooldowns)) return true;
        __result = true;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(ScannerCalculator), nameof(ScannerCalculator.TryUse))]
    static bool ScanReady(ref bool __result)
    {
        if (!(Cheat.Allowed && Cheat.NoCooldowns)) return true;
        __result = true;
        return false;
    }

    // while the menu is open the camera stands still
    [HarmonyPrefix, HarmonyPatch(typeof(FirstPersonController), "LateUpdate")]
    static bool NoLook() => !Cheat.MenuOpen;

    [HarmonyPrefix, HarmonyPatch(typeof(FirstPersonController), "JumpAndGravity")]
    static bool Gravity() => !(Cheat.Allowed && Cheat.Fly);

    // fly: replace the walking with free flight through walls
    [HarmonyPrefix, HarmonyPatch(typeof(FirstPersonController), "Move")]
    static bool FlyMove(FirstPersonController __instance)
    {
        if (!(Cheat.Allowed && Cheat.Fly)) return true;
        try
        {
            var kb = Keyboard.current;
            var cam = Camera.main != null ? Camera.main.transform : __instance.transform;
            Vector3 dir = Vector3.zero;
            if (kb != null && !Cheat.MenuOpen)
            {
                if (kb.wKey.isPressed) dir += cam.forward;
                if (kb.sKey.isPressed) dir -= cam.forward;
                if (kb.dKey.isPressed) dir += cam.right;
                if (kb.aKey.isPressed) dir -= cam.right;
                if (kb.spaceKey.isPressed) dir += Vector3.up;
                if (kb.leftCtrlKey.isPressed) dir -= Vector3.up;
            }
            float speed = 8f * Cheat.FlySpeed * (kb != null && kb.leftShiftKey.isPressed ? 2f : 1f);
            if (dir.sqrMagnitude > 0.001f) __instance.transform.position += dir.normalized * speed * Time.deltaTime;
        }
        catch { }
        return false;
    }
}

/// The game turns every ball a bomb pulls into a moving physics ball with expensive
/// "continuous" collision and never turns it back. After lots of bombs (infinite bombs,
/// bigger range) thousands of balls stay like that and the game gets slower and slower.
/// This puts them back to how they were once they lie still, and removes used bombs.
static class BombCleanup
{
    class Ball { public Rigidbody Rb; public bool Kinematic; public CollisionDetectionMode Mode; public float Drag, AngDrag, Since; }
    static readonly Dictionary<Rigidbody, Ball> balls = new Dictionary<Rigidbody, Ball>();
    static readonly List<MagnetBombScript> bombs = new List<MagnetBombScript>();
    static readonly List<Rigidbody> done = new List<Rigidbody>();

    public static void Remember(MagnetBombScript bomb)
    {
        try
        {
            if (!bombs.Contains(bomb)) bombs.Add(bomb);
            foreach (var c in Physics.OverlapSphere(bomb.transform.position, bomb.radius, bomb.ballLayer))
            {
                var rb = c.attachedRigidbody;
                if (rb == null || balls.ContainsKey(rb)) continue;
                balls[rb] = new Ball { Rb = rb, Kinematic = rb.isKinematic, Mode = rb.collisionDetectionMode, Drag = rb.drag, AngDrag = rb.angularDrag, Since = Time.time };
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("Bomb cleanup: " + e.Message); }
    }

    /// Called twice per second.
    public static void Tick()
    {
        if (balls.Count == 0 && bombs.Count == 0) return;
        float now = Time.time;
        done.Clear();
        foreach (var b in balls.Values)
        {
            var rb = b.Rb;
            if (rb == null || !rb.gameObject.activeInHierarchy) { done.Add(rb); continue; }
            float age = now - b.Since;
            if (age < 3.5f) continue;   // the bomb is still pulling
            if (age < 15f && !rb.IsSleeping() && rb.velocity.sqrMagnitude > 0.0025f) continue;   // still rolling
            rb.collisionDetectionMode = b.Mode;
            rb.drag = b.Drag;
            rb.angularDrag = b.AngDrag;
            if (b.Kinematic) rb.isKinematic = true;
            done.Add(rb);
        }
        foreach (var rb in done) balls.Remove(rb);

        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var bomb = bombs[i];
            if (bomb == null) { bombs.RemoveAt(i); continue; }
            if (bomb.gameObject.activeSelf) continue;   // still working
            UnityEngine.Object.Destroy(bomb.gameObject);
            bombs.RemoveAt(i);
        }
    }

    public static void Clear() { balls.Clear(); bombs.Clear(); }
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
    public static GameManager GM => GameManager.instance;
    public static UIManager UI => UIManager.Instance;
    public static GameData Data => GM != null ? GM.data : null;
    public static FirstPersonController Me => UI != null ? UI.FPSController : null;
    public static bool InGame => GM != null && GM.data != null && UI != null && Me != null && GM.cc != null;

    /// Why the cheats are off right now (null = they are on).
    public static string Why => InGame ? null : "Load your save first.";
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Money", "Balls", "Tools", "Upgrades", "Areas", "Teleport", "Settings" };
    static readonly float[] MenuSizes = { 0.75f, 0.85f, 1f, 1.15f, 1.3f };

    // player
    float speedMult = 1f, jumpMult = 1f;
    float baseWalk = -1f, baseRun, baseJump;
    FirstPersonController baseFor;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };
    float gameSpeed = 1f;
    static readonly float[] GameSpeeds = { 0.5f, 1f, 2f, 3f, 5f };

    // money
    long moneyAmount = 100;
    static readonly long[] MoneyPresets = { 10, 100, 1_000, 10_000, 100_000 };
    static readonly float[] SellPresets = { 1f, 2f, 5f, 10f, 100f };

    // balls
    bool ballEsp;
    static readonly int[] GrabSizes = { 0, 1, 2, 3 };
    static readonly string[] GrabNames = { "Normal", "Big", "Huge", "Massive" };
    const int BallEspMaxRemaining = 150;
    List<Vector3> espBalls = new List<Vector3>();
    float espScannedAt = -10f;

    // tools
    float vacuumRadius = 1f;
    static readonly float[] VacuumPresets = { 1f, 2f, 3f, 5f };
    static readonly float[] BombPresets = { 1f, 2f, 4f, 8f };

    // teleport
    class Place { public string Name, Group; public Vector3 Pos; }
    List<Place> places = new List<Place>();
    List<Place> collectibles = new List<Place>();
    float placesScannedAt = -100f;
    bool hasSaved, itemEsp;
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
        speedMult = Saved.Get("WalkSpeed", 1f);
        jumpMult = Saved.Get("JumpPower", 1f);
        Cheat.FlySpeed = Saved.Get("FlySpeed", 2f);
        Cheat.FreeShop = Saved.Get("FreeUpgrades", false);
        Cheat.SellMult = Saved.Get("SellMultiplier", 1f);
        Cheat.Bottomless = Saved.Get("BottomlessBasket", false);
        Cheat.InstantPick = Saved.Get("InstantPickup", false);
        Cheat.GrabSize = Saved.Get("GrabSize", 0);
        Cheat.InfBombs = Saved.Get("InfiniteBombs", false);
        Cheat.BombAll = Saved.Get("BombPullsAll", false);
        Cheat.BombRadius = Saved.Get("BombRadius", 1f);
        Cheat.NoOverheat = Saved.Get("NoOverheat", false);
        vacuumRadius = Saved.Get("VacuumRadius", 1f);
        Cheat.NoCooldowns = Saved.Get("NoCooldowns", false);
        ballEsp = Saved.Get("ShowLastBalls", false);
        itemEsp = Saved.Get("ShowCollectibles", false);
        moneyAmount = Saved.Get("MoneyAmount", 100L);
    }

    void SaveSettings()
    {
        Saved.Set("WalkSpeed", speedMult);
        Saved.Set("JumpPower", jumpMult);
        Saved.Set("FlySpeed", Cheat.FlySpeed);
        Saved.Set("FreeUpgrades", Cheat.FreeShop);
        Saved.Set("SellMultiplier", Cheat.SellMult);
        Saved.Set("BottomlessBasket", Cheat.Bottomless);
        Saved.Set("InstantPickup", Cheat.InstantPick);
        Saved.Set("GrabSize", Cheat.GrabSize);
        Saved.Set("InfiniteBombs", Cheat.InfBombs);
        Saved.Set("BombPullsAll", Cheat.BombAll);
        Saved.Set("BombRadius", Cheat.BombRadius);
        Saved.Set("NoOverheat", Cheat.NoOverheat);
        Saved.Set("VacuumRadius", vacuumRadius);
        Saved.Set("NoCooldowns", Cheat.NoCooldowns);
        Saved.Set("ShowLastBalls", ballEsp);
        Saved.Set("ShowCollectibles", itemEsp);
        Saved.Set("MoneyAmount", moneyAmount);
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
            if (Pressed(Plugin.KeyCollect.Value)) Act("", () => CollectArea(Game.UI.currentArea));
            if (Pressed(Plugin.KeySell.Value)) Act("", SellNow);
        }
        if (editId == null) typed.Clear();

        if (Time.unscaledTime - cleanupAt > 0.5f)
        {
            cleanupAt = Time.unscaledTime;
            try { BombCleanup.Tick(); } catch (Exception e) { Plugin.L.LogWarning("Bomb cleanup: " + e.Message); }
        }

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        Cheat.Allowed = allowed;
        if (!allowed || !Game.InGame) { Cheat.Fly = false; return; }

        var fpc = Game.Me;
        if (baseFor != fpc || baseWalk < 0f) { baseFor = fpc; baseWalk = fpc.MoveSpeed; baseRun = fpc.SprintSpeed; baseJump = fpc.JumpHeight; }
        fpc.MoveSpeed = baseWalk * speedMult;
        fpc.SprintSpeed = baseRun * speedMult;
        fpc.JumpHeight = baseJump * jumpMult;

        // while the menu is open your character does not walk, look or grab
        if (show)
        {
            var inp = Game.UI.inputs;
            if (inp != null)
            {
                inp.move = Vector2.zero; inp.look = Vector2.zero;
                inp.jump = false; inp.pickHeld = false; inp.pickPressed = false;
            }
        }

        // bigger vacuum
        var vac = Game.GM.vacuumController;
        if (vac != null && vac.Collider != null && Game.GM.config != null)
        {
            var radii = Game.GM.config.vacuumRadiusValues;
            int lv = Mathf.Clamp(Game.Data.VaccumRadiusLevel, 0, radii.Length - 1);
            float want = radii[lv] * vacuumRadius;
            if (Math.Abs(vac.Collider.radius - want) > 0.001f) vac.Collider.radius = want;
        }

        if (ballEsp && Time.unscaledTime - espScannedAt > 0.5f) ScanEspBalls();
    }

    float cleanupAt;

    void TurnOff()
    {
        BombCleanup.Clear();
        Cheat.Allowed = false;
        Cheat.Fly = false;
        baseWalk = -1f; baseFor = null;
        if (gameSpeed != 1f) { Time.timeScale = 1f; gameSpeed = 1f; }
        inputOff = null;
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
    PlayerInput inputOff;

    void SetShow(bool value)
    {
        if (value == show) return;
        show = value;
        Cheat.MenuOpen = value;
        if (show)
        {
            oldLock = Cursor.lockState; oldVisible = Cursor.visible;
            // the game's own keys (shop, pause, tools) stay quiet while the menu is open
            try
            {
                var pi = Game.Me != null ? Game.Me.GetComponent<PlayerInput>() : null;
                if (pi != null && pi.inputIsActive) { pi.DeactivateInput(); inputOff = pi; }
            }
            catch (Exception e) { Plugin.L.LogWarning("Input off: " + e.Message); }
        }
        else
        {
            editId = null; rebinding = null;
            try { if (inputOff != null) inputOff.ActivateInput(); } catch (Exception e) { Plugin.L.LogWarning("Input on: " + e.Message); }
            inputOff = null;
            if (Game.InGame)
            {
                bool free = GameWantsCursor();
                Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = free;
            }
            else { Cursor.lockState = oldLock; Cursor.visible = oldVisible; }
        }
    }

    static bool GameWantsCursor()
    {
        var ui = Game.UI;
        return ui.IsShopOpen || ui.IsStorageOpen || ui.IsPaused || ui.IsAreaPannelActive || ui.IsCassettePaneActive || ui.IsOwnerinteract || ui.IsGameComplete || !ui.IsGameInitialized;
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
        try { if (allowed && Game.InGame && Event.current.type == EventType.Repaint) DrawEsp(); } catch { }
        if (!show) return;
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
        Ui.Label($"Too Many Balls  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyCollect.Value.ToString(), "Collect Area");
        Ui.KeyHint(Plugin.KeySell.Value.ToString(), "Sell");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!allowed) Ui.Label($"<color=#ff6b6b><b>Cheats are off</b></color> - {lastWhy ?? "load your save first."}", Ui.Sub);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }   // two rows of tabs
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
            case 1: DrawMoney(); break;
            case 2: DrawBalls(); break;
            case 3: DrawTools(); break;
            case 4: DrawUpgrades(); break;
            case 5: DrawAreas(); break;
            case 6: DrawTeleport(); break;
            case 7: DrawSettings(); break;
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
        var d = Game.Data;
        Ui.BeginCard("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump power", Ui.Sub);
        jumpMult = Ui.Chips(jumpMult, JumpPresets, v => $"x{v:0.#}", 4);
        if (Ui.Button("Reset Movement", Ui.Chip)) { speedMult = 1f; jumpMult = 1f; }
        if (d != null && (!d.sprintUnlocked || !d.jumpUnlocked))
        {
            Ui.Label("Sprint and jump are rewards you have not earned yet.", Ui.Sub);
            if (Ui.Button("Unlock Sprint & Jump", Ui.Chip)) Act("Sprint (Shift) and jump (Space) unlocked", () => { d.sprintUnlocked = true; d.jumpUnlocked = true; });
        }
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly through walls and over every fence");
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

    void DrawMoney()
    {
        var d = Game.Data;
        float money = d != null ? d.money : 0f;
        Ui.BeginCard("Money", $"$ {money:N2}");
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + v.ToString("N0"), 5);
        NumField("money", "Custom amount", moneyAmount, 1, 100_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${moneyAmount:N0}", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${moneyAmount:N0}", Ui.Tab)) AddMoney(-moneyAmount);
        GUILayout.EndHorizontal();
        Cheat.FreeShop = Ui.Toggle(Cheat.FreeShop, "Free Upgrades", "every upgrade, tool and helper costs $0 - helpers work for free");
        Ui.EndCard();

        float price = 0f;
        try { if (Game.GM != null) price = Game.GM.economy.GetSellPrice(); } catch { }
        Ui.BeginCard("Ball Price", $"$ {price:0.00} per ball");
        Ui.Label("Sell multiplier - every ball is worth more", Ui.Sub);
        Cheat.SellMult = Ui.Chips(Cheat.SellMult, SellPresets, v => $"x{v:0}", 5);
        Ui.EndCard();

        Ui.BeginCard("Sell", "No need to walk back to the sell basket");
        if (Ui.Button($"Sell All Balls Now  [{Plugin.KeySell.Value}]", Ui.Primary)) Act("", SellNow);
        Ui.EndCard();
    }

    void DrawBalls()
    {
        var d = Game.Data;
        var inv = Game.GM != null ? Game.GM.inventory : null;
        string basket = d != null && inv != null ? $"{d.carriedBalls} / {inv.GetPickLimit()} balls" + (d.storedBags > 0 ? $"  ·  {d.storedBags} bags stored" : "") : null;

        Ui.BeginCard("Basket", basket);
        Cheat.Bottomless = Ui.Toggle(Cheat.Bottomless, "Bottomless Basket", "your basket is never full");
        if (Ui.Button($"Sell All Balls Now  [{Plugin.KeySell.Value}]", Ui.Primary)) Act("", SellNow);
        Ui.EndCard();

        Ui.BeginCard("Picking");
        Cheat.InstantPick = Ui.Toggle(Cheat.InstantPick, "Instant Pickup", "no waiting time between two grabs");
        Ui.Label("Grab size - how many balls one click takes", Ui.Sub);
        int g = Ui.Chips(Cheat.GrabSize, GrabSizes, i => GrabNames[i], 4);
        if (g != Cheat.GrabSize) { Cheat.GrabSize = g; SetStatus(g == 0 ? "Normal grab" : $"{GrabNames[g]} grab - up to {Cheat.GrabBalls[g]} balls per click"); }
        Ui.EndCard();

        var area = Game.UI != null ? Game.UI.currentArea : null;
        string here = area != null && area.config != null ? $"{area.config.AreaName}  ·  {SafeRemaining(area):N0} balls left" : "You are not in an area right now";
        Ui.BeginCard("This Area", here);
        if (Ui.Button($"Collect Every Ball Here  [{Plugin.KeyCollect.Value}]", Ui.Primary)) Act("", () => CollectArea(area));
        Ui.Label("Fills your basket - turn on Bottomless Basket to take them all.", Ui.Sub);
        ballEsp = Ui.Toggle(ballEsp, "Show Last Balls", $"marks the balls that are left once fewer than {BallEspMaxRemaining} remain");
        Ui.EndCard();
    }

    void DrawTools()
    {
        var d = Game.Data;
        Ui.BeginCard("Unlock", "Tools and abilities you normally buy or earn");
        if (d != null)
        {
            UnlockRow("Fluid Pickup", "hold the mouse to keep grabbing", d.fluidUnlocked, () => d.fluidUnlocked = true);
            UnlockRow("Magnet Bomb", "key 2", d.bombUnlocked, () => d.bombUnlocked = true);
            UnlockRow("Vacuum", "key 3", d.ballGathererUnlocked, () => d.ballGathererUnlocked = true);
            UnlockRow("Scanner", "shows balls through walls", d.scannerUnlocked, () => d.scannerUnlocked = true);
            UnlockRow("Teleport", "back to the area's start point", d.teleportUnlocked, () => d.teleportUnlocked = true);
            UnlockRow("Sprint", "Shift", d.sprintUnlocked, () => d.sprintUnlocked = true);
            UnlockRow("Jump", "Space", d.jumpUnlocked, () => d.jumpUnlocked = true);
            Ui.Space(4);
            if (Ui.Button("Unlock Everything", Ui.Primary)) Act("Every tool and ability unlocked", UnlockAll);
        }
        Ui.EndCard();

        Ui.BeginCard("Magnet Bomb");
        Cheat.InfBombs = Ui.Toggle(Cheat.InfBombs, "Infinite Bombs", "throw as many as you want, no recharge");
        Cheat.BombAll = Ui.Toggle(Cheat.BombAll, "Pull Every Ball", "no limit on how many balls one bomb pulls");
        Ui.Label("Bomb range", Ui.Sub);
        Cheat.BombRadius = Ui.Chips(Cheat.BombRadius, BombPresets, v => $"x{v:0}", 4);
        Ui.EndCard();

        Ui.BeginCard("Vacuum");
        Cheat.NoOverheat = Ui.Toggle(Cheat.NoOverheat, "Never Overheats", "suck as long as you want");
        Ui.Label("Vacuum range", Ui.Sub);
        float vr = Ui.Chips(vacuumRadius, VacuumPresets, v => $"x{v:0}", 4);
        if (vr != vacuumRadius) { vacuumRadius = vr; SetStatus($"Vacuum range x{vr:0}"); }
        Ui.EndCard();

        Ui.BeginCard("Scanner & Teleport");
        Cheat.NoCooldowns = Ui.Toggle(Cheat.NoCooldowns, "No Cooldown", "use the scanner and the teleport again right away");
        Ui.EndCard();
    }

    void UnlockRow(string name, string hint, bool on, Action unlock)
    {
        GUILayout.BeginHorizontal();
        Ui.Label($"<b>{name}</b>" + Grey(hint), Ui.Text, Ui.W(330));
        if (on) Ui.Label("<color=#50ff7a>unlocked</color>", Ui.Sub);
        else if (Ui.Button("Unlock", Ui.Chip)) Act($"{name} unlocked", () => { unlock(); RefreshAll(); });
        GUILayout.EndHorizontal();
    }

    // ---------------- upgrades ----------------
    class Upg
    {
        public string Name, Hint;
        public Func<GameData, int> Get;
        public Action<GameData, int> Set;
        public Func<UpgradeConfig, int> Max;
        public int Min;
    }

    static readonly Upg[] Upgrades =
    {
        new Upg { Name = "Dexterity", Hint = "faster grabbing", Get = d => d.dexterityLevel, Set = (d, v) => d.dexterityLevel = v, Max = c => c.dexterityValues.Length - 1 },
        new Upg { Name = "Greedy", Hint = "more balls per grab", Get = d => d.greedyLevel, Set = (d, v) => d.greedyLevel = v, Max = c => c.greedyValues.Length - 1 },
        new Upg { Name = "Basket Size", Hint = "more balls in your basket", Get = d => d.pickLimitLevel, Set = (d, v) => d.pickLimitLevel = v, Max = c => c.pickLimits.Length - 1 },
        new Upg { Name = "Ball Price", Hint = "every ball sells for more", Get = d => d.sellPriceLevel, Set = (d, v) => d.sellPriceLevel = v, Max = c => c.sellPrices.Length - 1 },
        new Upg { Name = "Bomb Pull", Hint = "balls per bomb", Get = d => d.bombPullAmountLevel, Set = (d, v) => d.bombPullAmountLevel = v, Max = c => c.bombPullAmountValues.Length - 1 },
        new Upg { Name = "Bomb Count", Hint = "bombs you can carry", Get = d => d.bombAmountLevel, Set = (d, v) => d.bombAmountLevel = v, Max = c => c.bombAmountValues.Length - 1 },
        new Upg { Name = "Bomb Recharge", Hint = "new bombs come faster", Get = d => d.bombResetRateLevel, Set = (d, v) => d.bombResetRateLevel = v, Max = c => c.bombResetValues.Length - 1 },
        new Upg { Name = "Vacuum Range", Hint = "wider vacuum", Get = d => d.VaccumRadiusLevel, Set = (d, v) => d.VaccumRadiusLevel = v, Max = c => c.vacuumRadiusValues.Length - 1 },
        new Upg { Name = "Vacuum Cooling", Hint = "longer until it overheats", Get = d => d.VaccumOverHeatLevel, Set = (d, v) => d.VaccumOverHeatLevel = v, Max = c => c.vacuumOverHeatValues.Length - 1 },
        new Upg { Name = "Sprint Speed", Hint = "faster sprint", Get = d => d.SprintSpeedLevel, Set = (d, v) => d.SprintSpeedLevel = v, Max = c => 4 },
        new Upg { Name = "Scanner Range", Hint = "bigger scan", Get = d => d.ScannerLevel, Set = (d, v) => d.ScannerLevel = v, Max = c => 2 },
        new Upg { Name = "Helper 1", Hint = "robot - level 0 = not hired", Min = -1, Get = d => d.Helper1Unlocked ? d.Helper1Level : -1, Set = (d, v) => { d.Helper1Unlocked = v >= 0; d.Helper1Level = Math.Max(0, v); }, Max = c => c.Helper1Wage.Length - 1 },
        new Upg { Name = "Helper 2", Hint = "level 0 = not hired", Min = -1, Get = d => d.Helper2Unlocked ? d.Helper2Level : -1, Set = (d, v) => { d.Helper2Unlocked = v >= 0; d.Helper2Level = Math.Max(0, v); }, Max = c => c.Helper2Wage.Length - 1 },
        new Upg { Name = "Helper 3", Hint = "level 0 = not hired", Min = -1, Get = d => d.Helper3Unlocked ? d.Helper3Level : -1, Set = (d, v) => { d.Helper3Unlocked = v >= 0; d.Helper3Level = Math.Max(0, v); }, Max = c => c.Helper3Wage.Length - 1 },
    };

    void DrawUpgrades()
    {
        var d = Game.Data;
        var c = Game.GM != null ? Game.GM.config : null;
        Ui.BeginCard("Upgrades", "Set any upgrade level - free, no money needed");
        if (Ui.Button("Max All Upgrades", Ui.Primary)) Act("Every upgrade is maxed and every helper is hired", MaxAll);
        Ui.Space(4);
        if (d != null && c != null)
            foreach (var u in Upgrades)
            {
                int max, lv;
                try { max = u.Max(c); lv = u.Get(d); } catch { continue; }
                // helpers: -1 = not hired, shown as level 0; hired = level 1..
                int shownLv = u.Min < 0 ? lv + 1 : lv, shownMax = u.Min < 0 ? max + 1 : max;
                GUILayout.BeginHorizontal();
                Ui.Label($"<b>{u.Name}</b>" + Grey(u.Hint), Ui.Text, Ui.W(250));
                Ui.Label(lv >= max ? $"<color=#50ff7a>{shownLv} / {shownMax}</color>" : $"{shownLv} / {shownMax}", Ui.Sub, Ui.W(56));
                if (Ui.Button("−", Ui.Chip, Ui.W(40)) && lv > u.Min) SetUpgrade(u, lv - 1);
                if (Ui.Button("+", Ui.Chip, Ui.W(40)) && lv < max) SetUpgrade(u, lv + 1);
                if (Ui.Button("Max", Ui.Chip, Ui.W(56)) && lv < max) SetUpgrade(u, max);
                GUILayout.EndHorizontal();
            }
        Ui.EndCard();
    }

    void SetUpgrade(Upg u, int lv)
    {
        Act("", () =>
        {
            u.Set(Game.Data, Mathf.Clamp(lv, u.Min, u.Max(Game.GM.config)));
            RefreshAll();
            SetStatus($"{u.Name} set");
        });
    }

    void MaxAll()
    {
        var d = Game.Data;
        var c = Game.GM.config;
        foreach (var u in Upgrades) u.Set(d, u.Max(c));
        d.fluidUnlocked = d.bombUnlocked = d.ballGathererUnlocked = true;
        RefreshAll();
    }

    void UnlockAll()
    {
        var d = Game.Data;
        d.fluidUnlocked = d.bombUnlocked = d.ballGathererUnlocked = true;
        d.scannerUnlocked = d.teleportUnlocked = d.sprintUnlocked = d.jumpUnlocked = true;
        RefreshAll();
    }

    /// Makes the game's shop, HUD and world show the new levels and unlocks.
    void RefreshAll()
    {
        var gm = Game.GM;
        var d = gm.data;
        var ui = Game.UI;
        var up = gm.upgrades;
        void Try(Action a) { try { a(); } catch (Exception e) { Plugin.L.LogWarning("Refresh: " + e.Message); } }

        Try(() => gm.upgradeManager.Initialize(d));
        Try(() => gm.helperManager.Initialize(d));
        Try(() => gm.UpdateScannerRange());
        if (up != null)
        {
            Try(() => up.bagScript.UpdateSize());
            Try(() => up.grenadeManager.OnGrenadeCapacityUpgraded());
            Try(() => up.vacuumController.RefreshColliderUpgrade());
            Try(() => up.helper1.SetActive(d.Helper1Unlocked));
            Try(() => up.helper1Bot.SetActive(d.Helper1Unlocked));
            Try(() => up.helper2.SetActive(d.Helper2Unlocked));
            Try(() => up.helper3.SetActive(d.Helper3Unlocked));
            Try(() => up.fluidTextScript.UpdateText());
            Try(() => up.dexterityTextScript.UpdateText());
            Try(() => up.greedyTextScript.UpdateText());
            Try(() => up.pullTextScript.UpdateText());
            Try(() => up.bombResetTextScript.UpdateText());
            Try(() => up.bombAmountTextScript.UpdateText());
            Try(() => up.vacuumRadiusTextScript.UpdateText());
            Try(() => up.vacuumOverheatTextScript.UpdateText());
            Try(() => up.basketTextUpgrade.UpdateText());
            Try(() => up.helper1TextScript.UpdateText());
            Try(() => up.helper2TextScript.UpdateText());
            Try(() => up.helper3TextScript.UpdateText());
        }
        if (d.bombUnlocked) Try(() => ui.BombPurchased());
        if (d.ballGathererUnlocked) Try(() => ui.VaccumPurchased());
        if (d.teleportUnlocked) Try(() => { ui.TeleportButtonUI.SetActive(true); gm.tpCooldown.enabled = true; });
        if (d.scannerUnlocked) Try(() => { ui.ScannerButtonUI.SetActive(true); gm.scannerCooldown.enabled = true; });
        Try(() => ui.UpdateBallInfo(0.25f));
        Try(() => ui.UpdateMoney(0.25f));
    }

    // ---------------- areas ----------------
    static int SafeRemaining(AreaManager a) { try { return a.GetRemainingBalls(); } catch { return 0; } }

    void DrawAreas()
    {
        var areas = Game.GM != null && Game.GM.areas != null ? Game.GM.areas : new AreaManager[0];
        var cur = Game.UI != null ? Game.UI.currentArea : null;
        int left = 0, total = 0;
        foreach (var a in areas) if (a != null && a.config != null) { left += SafeRemaining(a); total += a.config.totalBalls; }
        Ui.BeginCard("Areas", areas.Length > 0 ? $"{areas.Length} areas  ·  {left:N0} of {total:N0} balls left" : null);
        Ui.Label("Teleport to an area or collect all of its balls from anywhere.", Ui.Sub);
        Ui.Space(2);
        foreach (var a in areas)
        {
            if (a == null || a.config == null) continue;
            int rem = SafeRemaining(a), tot = Math.Max(1, a.config.totalBalls);
            string pct = rem <= 0 ? "<color=#50ff7a>cleared</color>" : $"{rem:N0} left  ·  {100f - rem * 100f / tot:0}%";
            GUILayout.BeginHorizontal();
            Ui.Label($"<b>{a.config.AreaName}</b>" + (a == cur ? $" <color={Ui.AHex}>(here)</color>" : "") + Grey(pct), Ui.Text, Ui.W(250));
            if (Ui.Button("Teleport", Ui.Chip, Ui.W(96))) TeleportTo(AreaSpot(a), a.config.AreaName);
            if (Ui.Button("Collect", Ui.Chip, Ui.W(96)) && rem > 0) Act("", () => CollectArea(a));
            GUILayout.EndHorizontal();
        }
        Ui.EndCard();
    }

    static Vector3 AreaSpot(AreaManager a) => a.TeleportPoint != null ? a.TeleportPoint.position : a.transform.position;

    static readonly AccessTools.FieldRef<Ball, bool> BallTaken = AccessTools.FieldRefAccess<Ball, bool>("IsInteracted");

    void CollectArea(AreaManager area)
    {
        if (area == null) { SetStatus("Walk into an area first (or use the Areas tab)"); return; }
        var inv = Game.GM.inventory;
        int got = 0;
        bool full = false;
        var balls = area.balls;
        // the game refreshes the area text (with an animation) after every single ball -
        // with thousands of balls that is thousands of animations, so it waits until the end
        var ui = Game.UI;
        var shownArea = ui.currentArea;
        ui.currentArea = null;
        try
        {
        for (int i = 0; i < balls.Length; i++)
        {
            var go = balls[i];
            if (go == null || !go.activeSelf) continue;
            var b = go.GetComponent<Ball>();
            if (b == null || BallTaken(b)) continue;
            if (!inv.CanPick()) { full = true; break; }
            BallTaken(b) = true;
            inv.AddBall();
            area.OnBallPicked(i, IsPickedByPlayer: false);
            go.SetActive(false);
            got++;
        }
        }
        finally { ui.currentArea = shownArea; }
        try { ui.UpdateBallInfo(0.25f); ui.UpdateAreaInfo(); } catch { }
        string name = area.config != null ? area.config.AreaName : "this area";
        if (got == 0) SetStatus(full ? "Your basket is full - sell first or turn on Bottomless Basket" : $"No balls left in {name}");
        else SetStatus($"Collected {got:N0} balls in {name}" + (full ? " - basket is full" : ""));
    }

    void SellNow()
    {
        var gm = Game.GM;
        int n = gm.inventory.GetTotalBallsForSelling();
        if (n <= 0) { SetStatus("Your basket is empty"); return; }
        float before = gm.data.money;
        gm.economy.SellAll(gm.inventory);
        Game.UI.UpdateMoney(0.5f);
        Game.UI.UpdateBallInfo(0.25f);
        try { SoundManager.instance.PlayCoindSound(); } catch { }
        SetStatus($"Sold {n:N0} balls for $ {gm.data.money - before:N2}");
    }

    void AddMoney(long amount)
    {
        Act($"{(amount >= 0 ? "+" : "−")}$ {Math.Abs(amount):N0}", () =>
        {
            var d = Game.Data;
            d.money = Mathf.Round(Mathf.Clamp(d.money + amount, 0f, 1e9f) * 100f) / 100f;
            Game.UI.UpdateMoney(0.25f);
        });
    }

    // ---------------- teleport ----------------
    static readonly AccessTools.FieldRef<NPCScript, string> NpcName = AccessTools.FieldRefAccess<NPCScript, string>("npcName");

    // The world has tens of thousands of objects, so searching it (FindObjectsOfType) is slow.
    // It happens once per save; after that only the found objects are checked.
    class Spot { public Component C; public string Name, Group; public bool Item; }
    List<Spot> spots = new List<Spot>();
    GameManager spotsFor;
    float spotsFoundAt = -100f;

    void FindSpots()
    {
        spotsFor = Game.GM;
        spotsFoundAt = Time.unscaledTime;
        var l = new List<Spot>();
        void Add(Component c, string name, string group, bool item = false) { l.Add(new Spot { C = c, Name = name, Group = group, Item = item }); }
        try
        {
            foreach (var b in FindObjectsOfType<Basket>()) Add(b, "Sell Basket", "Sell your balls");
            foreach (var b in FindObjectsOfType<BagUpgradeScript>()) Add(b, "Basket Upgrades", "Bigger basket");
            foreach (var o in FindObjectsOfType<OwnerScript>()) Add(o, "Owner", "Talk to the owner");
            foreach (var g in FindObjectsOfType<GolfControllerManager>()) Add(g, "Golf", "Mini game");
            foreach (var g in FindObjectsOfType<BasketBallGameDetector>()) Add(g, "Basketball", "Mini game");
            foreach (var c in FindObjectsOfType<CassettePlayer>()) Add(c, "Cassette Player", "Music");
            foreach (var n in FindObjectsOfType<NPCScript>())
            {
                string nm = null;
                try { nm = NpcName(n); } catch { }
                Add(n, string.IsNullOrEmpty(nm) ? "Person" : nm, "People");
            }
            foreach (var c in FindObjectsOfType<Cassette>()) Add(c, $"Cassette {c.CassetteId + 1}", "Collectible", true);
            foreach (var i in FindObjectsOfType<NPCItem>()) Add(i, $"Lost Item {i.ItemIndex + 1}", "Lost & Found", true);
        }
        catch (Exception e) { Plugin.L.LogWarning("Teleport scan: " + e.Message); }
        spots = l;
    }

    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        // search again only for a new save (or after a while if nothing was found yet)
        if (spotsFor != Game.GM || (spots.Count == 0 && Time.unscaledTime - spotsFoundAt > 30f)) FindSpots();
        var list = new List<Place>();
        var items = new List<Place>();
        var inspect = Game.GM != null ? Game.GM.InspectionPoint : null;
        foreach (var s in spots)
        {
            if (s.C == null || !s.C.gameObject.activeInHierarchy) continue;
            if (s.C is NPCItem ni && (!ni.enabled || ni.transform.parent == inspect)) continue;   // already found
            (s.Item ? items : list).Add(new Place { Name = s.Name, Group = s.Group, Pos = s.C.transform.position });
        }
        places = list;
        collectibles = items;
    }

    void DrawTeleport()
    {
        if (Time.unscaledTime - placesScannedAt > 3f) ScanPlaces();
        Ui.BeginCard("Position");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Save Position", Ui.Chip)) { hasSaved = true; savedPos = Game.Me.transform.position; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Vector3 me = Game.Me != null ? Game.Me.transform.position : Vector3.zero;
        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places  ·  areas are in the Areas tab" : null);
        foreach (var p in places)
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{p.Group}  ·  {Vector3.Distance(me, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();

        Ui.BeginCard("Collectibles", collectibles.Count > 0 ? $"{collectibles.Count} not found yet" : "Nothing left to find nearby");
        itemEsp = Ui.Toggle(itemEsp, "Show Collectibles", "cassettes and lost items get a label");
        foreach (var p in collectibles.OrderBy(x => Vector3.Distance(me, x.Pos)))
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{p.Group}  ·  {Vector3.Distance(me, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();
    }

    void TeleportTo(Vector3 pos, string name)
    {
        Act($"Teleported to {name}", () =>
        {
            var cc = Game.GM.cc;
            try { Game.UI.currentArea?.ForceExitArea(); } catch { }
            bool was = cc.enabled;
            cc.enabled = false;
            cc.transform.position = pos + Vector3.up * 0.5f;
            cc.enabled = was;
            placesScannedAt = -100f;
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
        KeyRow("Collect area", Plugin.KeyCollect);
        KeyRow("Sell all balls", Plugin.KeySell);
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

    // ---------------- ESP ----------------
    void ScanEspBalls()
    {
        espScannedAt = Time.unscaledTime;
        espBalls.Clear();
        var area = Game.UI.currentArea;
        if (area == null || area.balls == null || SafeRemaining(area) > BallEspMaxRemaining) return;
        Vector3 me = Game.Me.transform.position;
        espBalls = area.balls.Where(b => b != null && b.activeInHierarchy).Select(b => b.transform.position)
            .OrderBy(p => (p - me).sqrMagnitude).Take(60).ToList();
    }

    GUIStyle espStyle, espSubStyle;
    Texture2D espBg;
    void DrawEsp()
    {
        bool balls = ballEsp && espBalls.Count > 0;
        bool items = itemEsp && collectibles.Count > 0;
        if (!balls && !items) return;
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
        if (balls) foreach (var p in espBalls) Label3D(cam, p + Vector3.up * 0.4f, "<color=#ffd23f>BALL</color>", null, 400f);
        if (items)
        {
            if (Time.unscaledTime - placesScannedAt > 3f) ScanPlaces();
            foreach (var p in collectibles) Label3D(cam, p.Pos + Vector3.up * 0.6f, $"<color=#c77dff>{p.Name.ToUpperInvariant()}</color>", p.Group, 2000f);
        }
    }

    void Label3D(Camera cam, Vector3 world, string text, string sub, float maxDist)
    {
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0.2f || sp.z > maxDist) return;
        string line2 = (string.IsNullOrEmpty(sub) ? "" : sub + "  ·  ") + $"{sp.z:0} m";
        var s1 = espStyle.CalcSize(new GUIContent(text));
        var s2 = espSubStyle.CalcSize(new GUIContent(line2));
        float w = Mathf.Max(s1.x, s2.x) + 18f, h = s1.y + s2.y + 6f;
        var box = new Rect(sp.x - w / 2f, Screen.height - sp.y - h, w, h);
        GUI.DrawTexture(box, espBg);
        GUI.Label(new Rect(box.x, box.y + 2f, w, s1.y), text, espStyle);
        GUI.Label(new Rect(box.x, box.y + 2f + s1.y, w, s2.y), line2, espSubStyle);
    }

    // ---------------- actions ----------------
    bool Check()
    {
        if (!Game.InGame) { SetStatus("Load your save first"); return false; }
        return true;
    }

    /// Runs an action; an empty "done" text means the action sets its own status.
    void Act(string done, Action a)
    {
        if (!Check()) return;
        try { a(); if (done.Length > 0) SetStatus(done); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("That did not work: " + e.Message); }
    }
}
