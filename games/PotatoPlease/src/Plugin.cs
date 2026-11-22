using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using PotatoPeeler;
using PotatoPeeler.Platform;
using UnityEngine;
using UnityEngine.InputSystem;
using EvilDragon;

namespace PotatoPleaseCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Potato Please", Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "evildragon.cheats.potatoplease";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<string> KeyMenu, KeyFly, KeyMoney, Theme;
    internal static ConfigEntry<float> MenuScale;

    public override void Load()
    {
        L = Log;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", "F1", "Open/close menu (InputSystem key name, e.g. F1, K, Insert)");
        KeyFly = Config.Bind("Hotkeys", "Fly", "F2", "Toggle fly mode (noclip)");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", "F3", "Add the selected amount of money");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        MenuScale = Config.Bind("Interface", "MenuScale", 1f, "Size of the menu (0.75 - 1.3)");
        Ui.InitialTheme = Theme.Value;
        Ui.UseOsFonts = false;   // Unity 6: OS fonts do not show in IMGUI
        Ui.Manual = true;        // the game strips GUILayout ...
        Ui.OwnButtons = true;    // ... and GUI.Button
        Ui.Log = m => Log.LogWarning(m);

        ClassInjector.RegisterTypeInIl2Cpp<CheatMenu>();
        var go = new GameObject("EvilDragonCheatMenu");
        GameObject.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        var h = new Harmony(Guid);
        foreach (var t in new[] { typeof(Patches), typeof(NoUploads) })
            try { h.PatchAll(t); } catch (Exception e) { Log.LogError($"{t.Name}: {e}"); }
        Log.LogInfo($"EvilDragon's Cheats v{Version} loaded");
    }
}

/// What the Harmony patches read.
static class Cheat
{
    public static bool Allowed, Fly, InfStamina, SharpKnife, MenuOpen;
    public static float FlySpeed = 2f;
}

public static class Patches
{
    // the menu key (F1) is also the game's pause key: the pause menu stays shut while our menu
    // opens/closes or is open
    [HarmonyPrefix, HarmonyPatch(typeof(PauseUI), nameof(PauseUI.Toggle))]
    static bool NoPause()
    {
        if (Cheat.MenuOpen) return false;
        try
        {
            var kb = Keyboard.current;
            if (kb != null && Enum.TryParse<Key>(Plugin.KeyMenu.Value, true, out var k) && kb[k].wasPressedThisFrame) return false;
        }
        catch { }
        return true;
    }

    // infinite stamina while peeling
    [HarmonyPostfix, HarmonyPatch(typeof(PeelSession), nameof(PeelSession.StaminaDrainPerSecond), MethodType.Getter)]
    static void NoDrain(ref float __result) { if (Cheat.Allowed && Cheat.InfStamina) __result = 0f; }

    // the knife never gets dull
    [HarmonyPrefix, HarmonyPatch(typeof(KnifeSharpness), nameof(KnifeSharpness.Dull))]
    static bool NoDull(ref bool __result)
    {
        if (!(Cheat.Allowed && Cheat.SharpKnife)) return true;
        __result = false;
        return false;
    }

    // fly: free flight through walls instead of walking
    [HarmonyPrefix, HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.Tick))]
    static bool FlyTick(PlayerMove __instance, float deltaTime)
    {
        if (!(Cheat.Allowed && Cheat.Fly)) return true;
        try
        {
            if (Cheat.MenuOpen) return false;
            var kb = Keyboard.current;
            var cam = Camera.main != null ? Camera.main.transform : __instance.transform;
            if (kb == null) return false;
            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += cam.forward;
            if (kb.sKey.isPressed) dir -= cam.forward;
            if (kb.dKey.isPressed) dir += cam.right;
            if (kb.aKey.isPressed) dir -= cam.right;
            if (kb.spaceKey.isPressed) dir += Vector3.up;
            if (kb.leftCtrlKey.isPressed) dir -= Vector3.up;
            float speed = 8f * Cheat.FlySpeed * (kb.leftShiftKey.isPressed ? 2f : 1f);
            if (dir.sqrMagnitude > 0.001f) __instance.Warp(__instance.transform.position + dir.normalized * speed * deltaTime);
        }
        catch { }
        return false;
    }
}

/// With the cheat mod installed nothing is sent away: no Steam leaderboard scores
/// and none of the game's play statistics for the developer's balancing.
public static class NoUploads
{
    [HarmonyPrefix, HarmonyPatch(typeof(SteamLeaderboards), nameof(SteamLeaderboards.Submit))]
    static bool Submit() => false;

    [HarmonyPrefix, HarmonyPatch(typeof(SteamLeaderboards), nameof(SteamLeaderboards.Upload))]
    static bool Upload() => false;

    [HarmonyPrefix, HarmonyPatch(typeof(BalanceTelemetry), nameof(BalanceTelemetry.Boot))]
    static bool Telemetry() => false;
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

public class CheatMenu : MonoBehaviour
{
    public CheatMenu(IntPtr ptr) : base(ptr) { }

    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Money", "Upgrades", "Survival", "Spawn", "Teleport", "Settings" };
    const int TeleportTab = 5;
    static readonly float[] MenuSizes = { 0.75f, 0.85f, 1f, 1.15f, 1.3f };

    // player
    float speedMult = 1f, jumpMult = 1f, gameSpeed = 1f;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };
    static readonly float[] GameSpeeds = { 0.5f, 1f, 2f, 3f, 5f };
    PlayerMove baseFor;
    float baseWalk = -1f, baseSprint, baseJump;
    bool baseCanJump;

    // money
    long moneyAmount = 1000;
    static readonly long[] MoneyPresets = { 100, 1_000, 10_000, 100_000 };
    long pointAmount = 5;
    static readonly long[] PointPresets = { 1, 5, 10, 50 };

    // survival
    bool alwaysWarm, noCough;
    float maxWarmth = 1f;
    bool freezeChanged, freezeOrig;

    // teleport
    class Place { public string Name, Group; public Vector3 Pos; }
    List<Place> places = new(), secrets = new();
    float placesScannedAt = -100f;
    bool hasSaved;
    Vector3 savedPos;

    // number input + key rebinding
    string editId, editBuf = "";
    long editMin, editMax;
    Action<long> editSet;
    ConfigEntry<string> rebinding;

    string status = "";
    float statusTime;
    void SetStatus(string s) { status = s; statusTime = Time.unscaledTime; Plugin.L.LogInfo(s); }

    // ---------------- game objects ----------------
    Player player;
    float lookedAt = -10f;

    Player Me
    {
        get
        {
            if (player != null) return player;
            if (Time.unscaledTime - lookedAt < 1f) return null;
            lookedAt = Time.unscaledTime;
            player = FindAll<Player>().FirstOrDefault();
            return player;
        }
    }

    bool InGame => EconomyManager.Instance != null && Me != null && Me.Move != null;

    static List<T> FindAll<T>() where T : Component
    {
        var list = new List<T>();
        try
        {
            foreach (var o in UnityEngine.Object.FindObjectsByType(Il2CppType.Of<T>(), FindObjectsSortMode.None))
            {
                var c = o.TryCast<T>();
                if (c != null) list.Add(c);
            }
        }
        catch (Exception ex) { Plugin.L.LogWarning($"FindObjectsByType<{typeof(T).Name}> failed: " + ex.Message); }
        return list;
    }

    // ---------------- saved settings ----------------
    bool settingsLoaded;
    void LoadSettings()
    {
        settingsLoaded = true;
        speedMult = Saved.Get("WalkSpeed", 1f);
        jumpMult = Saved.Get("JumpPower", 1f);
        Cheat.FlySpeed = Saved.Get("FlySpeed", 2f);
        Cheat.InfStamina = Saved.Get("InfiniteStamina", false);
        Cheat.SharpKnife = Saved.Get("SharpKnife", false);
        alwaysWarm = Saved.Get("AlwaysWarm", false);
        noCough = Saved.Get("NoCough", false);
        moneyAmount = Saved.Get("MoneyAmount", 1000L);
        pointAmount = Saved.Get("PointAmount", 5L);
    }

    void SaveSettings()
    {
        Saved.Set("WalkSpeed", speedMult);
        Saved.Set("JumpPower", jumpMult);
        Saved.Set("FlySpeed", Cheat.FlySpeed);
        Saved.Set("InfiniteStamina", Cheat.InfStamina);
        Saved.Set("SharpKnife", Cheat.SharpKnife);
        Saved.Set("AlwaysWarm", alwaysWarm);
        Saved.Set("NoCough", noCough);
        Saved.Set("MoneyAmount", moneyAmount);
        Saved.Set("PointAmount", pointAmount);
        Saved.Flush();
        SetStatus("Settings saved - they load automatically next time");
    }

    // ---------------- keyboard + mouse ----------------
    static Key ParseKey(string s) => Enum.TryParse<Key>(s, true, out var k) ? k : Key.None;

    static bool Pressed(Keyboard kb, Key k)
    {
        if (k == Key.None) return false;
        try { return kb[k].wasPressedThisFrame; } catch { return false; }
    }

    static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
    static readonly Key[] NumpadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };
    static Key[] allKeys;

    void HandleNumberInput(Keyboard kb)
    {
        for (int d = 0; d <= 9; d++)
            if ((Pressed(kb, DigitKeys[d]) || Pressed(kb, NumpadKeys[d])) && editBuf.Length < 12)
                editBuf += (char)('0' + d);
        if (Pressed(kb, Key.Backspace) && editBuf.Length > 0) editBuf = editBuf.Substring(0, editBuf.Length - 1);
        if (Pressed(kb, Key.Delete)) editBuf = "";
        if (editBuf.Length > 0 && long.TryParse(editBuf, out var v)) editSet?.Invoke(Math.Clamp(v, editMin, editMax));
        if (Pressed(kb, Key.Enter) || Pressed(kb, Key.NumpadEnter) || Pressed(kb, Key.Escape)) editId = null;
    }

    void HandleRebind(Keyboard kb)
    {
        if (Pressed(kb, Key.Escape)) { rebinding = null; SetStatus("Cancelled"); return; }
        allKeys ??= ((Key[])Enum.GetValues(typeof(Key))).Where(k => k != Key.None && k != Key.Escape && (int)k < 111).ToArray();
        foreach (var k in allKeys)
        {
            if (!Pressed(kb, k)) continue;
            rebinding.Value = k.ToString();
            SetStatus($"{rebinding.Definition.Key} → {k}");
            rebinding = null;
            return;
        }
    }

    // The game strips IMGUI buttons, so the menu reads the mouse itself.
    int clickFrame = -1;
    float scroll, contentHeight, viewHeight;
    Rect panelRect;

    void ReadMouse()
    {
        var m = Mouse.current;
        if (m == null || !show) return;
        float s = MenuScaleNow;
        Vector2 pos = m.position.ReadValue();
        var gui = new Vector2(pos.x / s, (Screen.height - pos.y) / s);
        if (m.leftButton.wasPressedThisFrame) { Ui.Click = gui; clickFrame = Time.frameCount; }
        float wheel = m.scroll.ReadValue().y;
        if (wheel != 0f && panelRect.Contains(gui)) scroll -= Math.Sign(wheel) * 60f;
        scroll = Mathf.Clamp(scroll, 0f, Math.Max(0f, contentHeight - viewHeight));
    }

    static float MenuScaleNow => Mathf.Clamp(Plugin.MenuScale.Value, 0.6f, 1.6f);

    // ---------------- update ----------------
    bool allowed;
    float allowedAt = -10f;

    void Update()
    {
        if (!settingsLoaded) LoadSettings();
        if (Time.unscaledTime - allowedAt > 1f)
        {
            allowedAt = Time.unscaledTime;
            bool now = false;
            try { now = InGame; } catch { }
            if (allowed && !now) TurnOff();
            allowed = now;
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (rebinding != null) HandleRebind(kb);
            else if (editId != null) HandleNumberInput(kb);
            else
            {
                if (Pressed(kb, ParseKey(Plugin.KeyMenu.Value))) SetShow(!show);
                if (allowed && Pressed(kb, ParseKey(Plugin.KeyFly.Value))) SetFly(!Cheat.Fly);
                if (allowed && Pressed(kb, ParseKey(Plugin.KeyMoney.Value))) AddMoney(moneyAmount);
            }
        }
        ReadMouse();
        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        Cheat.Allowed = allowed;
        if (!allowed) { Cheat.Fly = false; return; }
        var me = Me;
        var mv = me.Move;

        // walk speed / jump: the game's values are the base, the cheat multiplies them
        if (baseFor != mv || baseWalk < 0f) { baseFor = mv; baseWalk = mv.moveSpeed; baseSprint = mv.sprintSpeed; baseJump = mv.jumpHeight; baseCanJump = mv.canJump; }
        mv.moveSpeed = baseWalk * speedMult;
        mv.sprintSpeed = baseSprint * speedMult;
        mv.jumpHeight = baseJump * jumpMult;
        mv.canJump = baseCanJump || jumpMult > 1f;

        // menu open: the player does not walk, look or use anything
        if (show && me.ControlsEnabled) { me.SetControlsEnabled(false, false); controlsOff = true; }

        var bt = BodyTemperature.Instance;
        if (bt != null)
        {
            if (bt.Warmth > maxWarmth) maxWarmth = bt.Warmth;
            if (alwaysWarm)
            {
                if (!freezeChanged) { freezeOrig = bt.canFreezeToDeath; freezeChanged = true; }
                bt.canFreezeToDeath = false;
                if (bt.Warmth < maxWarmth) bt.SetWarmth(maxWarmth);
            }
            else if (freezeChanged) { bt.canFreezeToDeath = freezeOrig; freezeChanged = false; }
        }

        if (noCough)
        {
            var um = UpgradeManager.Instance;
            var c = um != null ? um.cough : null;
            if (c != null && c.IsAfflicted) c.Cure();
        }

        if (gameSpeed != 1f && Time.timeScale > 0f && Math.Abs(Time.timeScale - gameSpeed) > 0.01f) Time.timeScale = gameSpeed;
        if (show && tab == TeleportTab && Time.unscaledTime - placesScannedAt > 3f) ScanPlaces();
    }

    void TurnOff()
    {
        Cheat.Allowed = false;
        Cheat.Fly = false;
        player = null; baseFor = null; baseWalk = -1f;
        freezeChanged = false;
        maxWarmth = 1f;
        controlsOff = false;
        if (gameSpeed != 1f) { Time.timeScale = 1f; gameSpeed = 1f; }
    }

    void SetFly(bool on)
    {
        if (on && !Check()) return;
        Cheat.Fly = on;
        SetStatus(on ? "Fly mode on - WASD, Space up, Ctrl down, Shift faster" : "Fly mode off");
    }

    // ---------------- menu open/close ----------------
    bool controlsOff;

    void SetShow(bool value)
    {
        if (value == show) return;
        show = value;
        Cheat.MenuOpen = value;
        if (show) return;
        editId = null; rebinding = null; Ui.Click = null;
        try { if (controlsOff && Me != null) Me.SetControlsEnabled(true, true); } catch (Exception e) { Plugin.L.LogWarning("Controls: " + e.Message); }
        controlsOff = false;
    }

    void LateUpdate()
    {
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ---------------- GUI ----------------
    void OnGUI()
    {
        if (!show) { Ui.Click = null; return; }
        // (GUI.depth is stripped in this game)
        Ui.Init();
        var oldMatrix = GUI.matrix;
        try
        {
            float s = MenuScaleNow;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            panelRect = new Rect(24, 24, 540, Screen.height / s - 48);
            Ui.BeginArea(panelRect, Ui.Panel);
            Draw();
            Ui.EndArea();
        }
        catch (Exception e)
        {
            var msg = e.ToString();
            if (msg != lastGuiError) { lastGuiError = msg; Plugin.L.LogError(e); }
        }
        GUI.matrix = oldMatrix;
        if (Time.frameCount != clickFrame) Ui.Click = null;
    }

    void Draw()
    {
        Ui.BeginRow(2);
        if (Ui.Styled && Ui.Logo != null && Ui.Logo.normal.background != null) Ui.Label("", Ui.Logo, Ui.W(56));
        Ui.Label($"EvilDragon's <color={Ui.BHex}>Cheats</color>", Ui.Title);
        Ui.EndRow();
        Ui.Label($"Potato Please  ·  v{Plugin.Version}", Ui.Sub);
        Ui.Space(4);

        Ui.BeginRow(4);
        Ui.Label($"<b>{Plugin.KeyMenu.Value}</b>  Menu", Ui.Sub);
        Ui.Label($"<b>{Plugin.KeyFly.Value}</b>  Fly", Ui.Sub);
        Ui.Label($"<b>{Plugin.KeyMoney.Value}</b>  Money", Ui.Sub);
        Ui.Label("Wheel  Scroll", Ui.Sub);
        Ui.EndRow();

        if (!allowed) Ui.Label("<color=#ff6b6b><b>Cheats are off</b></color> - load your game first.", Ui.Sub);

        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i % 4 == 0) Ui.BeginRow(Math.Min(4, Tabs.Length - i));
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; scroll = 0f; }
            if (i % 4 == 3 || i == Tabs.Length - 1) Ui.EndRow();
        }
        Ui.Space(6);

        bool showStatus = status.Length > 0 && Time.unscaledTime - statusTime < 6f;
        viewHeight = Ui.SpaceLeft - (showStatus ? 34f : 0f);
        Ui.BeginClip(viewHeight, scroll);
        bool locked = tab != Tabs.Length - 1 && !allowed;
        if (locked) GUI.enabled = false;
        try
        {
            switch (tab)
            {
                case 0: DrawPlayer(); break;
                case 1: DrawMoney(); break;
                case 2: DrawUpgrades(); break;
                case 3: DrawSurvival(); break;
                case 4: DrawSpawn(); break;
                case 5: DrawTeleport(); break;
                case 6: DrawSettings(); break;
            }
        }
        finally
        {
            if (locked) GUI.enabled = true;
            contentHeight = Ui.EndClip();
        }
        if (showStatus) Ui.Label("» " + status, Ui.Status);
    }

    void NumField(string id, string label, long value, long min, long max, Action<long> set)
    {
        bool editing = editId == id;
        Ui.BeginRow(2);
        Ui.Label(label, Ui.Sub, Ui.W(140));
        string shown = editing ? editBuf + "_" : value.ToString("N0");
        if (Ui.Button(shown, editing ? Ui.FieldOn : Ui.Field))
        {
            if (editing) editId = null;
            else { editId = id; editBuf = ""; editMin = min; editMax = max; editSet = set; }
        }
        Ui.EndRow();
    }

    static string Grey(string s) => $"   <size=12><color=#b8c2d6>{s}</color></size>";

    // ---------------- tabs ----------------
    void DrawPlayer()
    {
        Ui.BeginCard("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump height", Ui.Sub);
        jumpMult = Ui.Chips(jumpMult, JumpPresets, v => $"x{v:0.#}", 4);
        if (Ui.Button("Reset Movement", Ui.Chip)) { speedMult = 1f; jumpMult = 1f; }
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly through walls and over the snow");
        bool f = Ui.Toggle(Cheat.Fly, $"Fly  [{Plugin.KeyFly.Value}]", "WASD, Space, Ctrl, Shift");
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
        var eco = EconomyManager.Instance;
        string bal = eco != null ? Rubles(eco.Balance) : "-";
        Ui.BeginCard("Money", bal);
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => v.ToString("N0"), 4);
        NumField("money", "Custom amount", moneyAmount, 1, 1_000_000_000, v => moneyAmount = v);
        Ui.BeginRow(2);
        if (Ui.Button($"+ {moneyAmount:N0}", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− {moneyAmount:N0}", Ui.Tab)) AddMoney(-moneyAmount);
        Ui.EndRow();
        Ui.EndCard();

        var sp = SkillPointBank.Instance;
        Ui.BeginCard("Skill Points", sp != null ? $"{sp.Available} to spend" : null);
        pointAmount = Ui.Chips(pointAmount, PointPresets, v => "+" + v, 4);
        if (Ui.Button($"+ {pointAmount} Skill Points", Ui.Primary)) Act("", () => AddPoints((int)pointAmount));
        Ui.EndCard();
    }

    // plain text (the game's own format adds a TextMeshPro sprite tag IMGUI can not show)
    static string Rubles(long units)
    {
        long per = Math.Max(1, Currency.UnitsPerRuble);
        return $"{units / (double)per:N2} ₽";
    }

    void DrawUpgrades()
    {
        var um = UpgradeManager.Instance;
        Ui.BeginCard("Upgrades", "Free - money and skill points stay where they are");
        if (Ui.Button("Max All Upgrades", Ui.Primary)) Act("", MaxAll);
        if (um == null || um.definitions == null) { Ui.Label("Load your game first.", Ui.Sub); Ui.EndCard(); return; }
        foreach (var d in um.definitions)
        {
            if (d == null) continue;
            int lv, max;
            bool consumable;
            try { lv = um.GetLevel(d.id); max = um.GetMaxLevel(d.id); consumable = um.IsConsumable(d.id); } catch { continue; }
            if (max <= 0) continue;
            string name = SafeName(d);
            string kind = d.currency == UpgradeCurrency.SkillPoint ? "skill" : "shop";
            Ui.BeginRow(4);
            Ui.Label($"<b>{name}</b>" + Grey(kind), Ui.Text, Ui.W(250));
            Ui.Label(consumable ? "item" : lv >= max ? $"<color=#50ff7a>{lv} / {max}</color>" : $"{lv} / {max}", Ui.Sub, Ui.W(60));
            if (Ui.Button(consumable ? "Get" : "+", Ui.Chip, Ui.W(56)) && (consumable || lv < max)) Act("", () => Grant(d, 1));
            if (Ui.Button("Max", Ui.Chip, Ui.W(64)) && !consumable && lv < max) Act("", () => Grant(d, max - lv));
            Ui.EndRow();
        }
        Ui.EndCard();
    }

    static string SafeName(UpgradeDefinition d)
    {
        try { var n = d.DisplayName; if (!string.IsNullOrEmpty(n)) return n; } catch { }
        try { if (!string.IsNullOrEmpty(d.displayName)) return d.displayName; } catch { }
        return d.id.ToString();
    }

    void DrawSurvival()
    {
        Ui.BeginCard("Body");
        Cheat.InfStamina = Ui.Toggle(Cheat.InfStamina, "Infinite Stamina", "peel without getting tired");
        alwaysWarm = Ui.Toggle(alwaysWarm, "Always Warm", "the cold can not freeze you");
        noCough = Ui.Toggle(noCough, "No Cough", "the cough is cured right away");
        Ui.EndCard();

        Ui.BeginCard("Knife");
        Cheat.SharpKnife = Ui.Toggle(Cheat.SharpKnife, "Always Sharp", "the knife never gets dull");
        Ui.EndCard();
    }

    // ---------------- spawn ----------------
    int potatoCount = 5, pileCount = 500;
    static readonly int[] PotatoCounts = { 1, 5, 10, 25 };
    static readonly int[] PileCounts = { 100, 500, 2000 };

    void DrawSpawn()
    {
        Ui.BeginCard("Potatoes", "Raw potatoes to peel");
        potatoCount = Ui.Chips(potatoCount, PotatoCounts, v => v.ToString(), 4);
        if (Ui.Button($"Spawn {potatoCount} In Front Of You", Ui.Primary)) Act("", () => SpawnPotatoes(potatoCount));
        Ui.Label("Refill the big potato pile", Ui.Sub);
        pileCount = Ui.Chips(pileCount, PileCounts, v => "+" + v.ToString("N0"), 3);
        if (Ui.Button($"Add {pileCount:N0} To The Pile", Ui.Tab)) Act("", () => RefillPile(pileCount));
        Ui.EndCard();

        Ui.BeginCard("Items");
        Ui.BeginRow(2);
        if (Ui.Button("New Knife", Ui.Chip)) Act("", SpawnKnife);
        if (Ui.Button("New Letters", Ui.Chip)) Act("", () => { var m = LetterMailbox.Instance; if (m == null) throw new Exception("no mailbox here"); m.Refill(true); SetStatus("New letters in the mailbox"); });
        Ui.EndRow();
        if (Ui.Button("Poison Vial", Ui.Chip)) Act("", () => { var pp = PoisonPlot.Instance; if (pp == null) throw new Exception("not in this part of the story"); pp.Spawn(); SetStatus("Poison vial spawned"); });
        Ui.EndCard();

        var ev = VegetableCrateEvent.Instance;
        var crates = ev != null ? ev.cratePrefabs : null;
        Ui.BeginCard("Vegetable Crates", "A crate drops in - fill it for its reward");
        if (crates == null || crates.Length == 0) Ui.Label("No crates in this part of the game.", Ui.Sub);
        else
            for (int i = 0; i < crates.Length; i++)
            {
                var c = crates[i];
                if (c == null) continue;
                string n = c.name.Replace("(Clone)", "").Replace("_", " ");
                if (Ui.Button($"<b>{n}</b>" + Grey($"reward {Rubles(c.reward)}"), Ui.Row)) Act("", () => SetStatus(ev.Spawn(c) ? $"{n} is on its way" : "A crate is already out - finish it first"));
            }
        Ui.EndCard();
    }

    void SpawnPotatoes(int n)
    {
        var act = FindAll<PotatoPileActivator>().FirstOrDefault();
        if (act == null) { SetStatus("No potato pile in this part of the game"); return; }
        var cam = Camera.main != null ? Camera.main.transform : Me.transform;
        Vector3 front = cam.position + cam.forward * 1.2f;
        int got = 0;
        for (int i = 0; i < n; i++)
        {
            var off = new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0.15f * (i / 5), UnityEngine.Random.Range(-0.3f, 0.3f));
            if (act.SpawnLoaded(front + off, UnityEngine.Random.rotation, 1f, false, true, 0) != null) got++;
        }
        SetStatus(got > 0 ? $"Spawned {got} potatoes" : "The game did not let a potato spawn here");
    }

    void RefillPile(int n)
    {
        var all = FindAll<PotatoPileActivator>();
        if (all.Count == 0) { SetStatus("No potato pile in this part of the game"); return; }
        int got = 0;
        foreach (var a in all) { try { got += a.RefillPile(n); } catch (Exception e) { Plugin.L.LogWarning("Refill: " + e.Message); } if (got > 0) break; }
        Plugin.L.LogInfo($"refill: {all.Count} piles, added {got}");
        SetStatus(got > 0 ? $"+{got:N0} potatoes on the pile" : "The pile is full - the game does not take more right now");
    }

    void SpawnKnife()
    {
        var k = FindAll<KnifeSharpener>().FirstOrDefault();
        if (k == null) { SetStatus("No knife sharpener here"); return; }
        k.SpawnKnife();
        SetStatus("A new knife is at the sharpener");
    }

    void DrawTeleport()
    {
        if (Time.unscaledTime - placesScannedAt > 3f) ScanPlaces();
        var me = Me;
        Ui.BeginCard("Position");
        Ui.BeginRow(2);
        if (Ui.Button("Save Position", Ui.Chip) && me != null) { hasSaved = true; savedPos = me.transform.position; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        Ui.EndRow();
        Ui.EndCard();

        Vector3 at = me != null ? me.transform.position : Vector3.zero;
        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places" : "Nothing found yet");
        foreach (var p in places.OrderBy(x => Vector3.Distance(at, x.Pos)))
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{p.Group}  ·  {Vector3.Distance(at, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();

        Ui.BeginCard("Secrets", secrets.Count > 0 ? $"{secrets.Count} hidden things" : "Nothing hidden found nearby");
        foreach (var p in secrets.OrderBy(x => Vector3.Distance(at, x.Pos)))
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{Vector3.Distance(at, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();
    }

    void DrawSettings()
    {
        Ui.BeginCard("Theme");
        string th = Ui.Chips(Ui.CurrentTheme, Ui.ThemeNames, n => n, 3);
        if (th != Ui.CurrentTheme) { Ui.SetTheme(th); Plugin.Theme.Value = th; SetStatus($"Theme: {th}"); }
        Ui.EndCard();

        Ui.BeginCard("Menu Size", "Saved right away");
        float ms = Ui.Chips(Plugin.MenuScale.Value, MenuSizes, v => $"{v * 100:0}%", 5);
        if (Math.Abs(ms - Plugin.MenuScale.Value) > 0.001f) { Plugin.MenuScale.Value = ms; SetStatus($"Menu size {ms * 100:0}%"); }
        Ui.EndCard();

        Ui.BeginCard("Keybinds", "Click a key, then press the new key (Esc = cancel)");
        KeyRow("Open menu", Plugin.KeyMenu);
        KeyRow("Fly mode", Plugin.KeyFly);
        KeyRow("Add money", Plugin.KeyMoney);
        Ui.EndCard();

        Ui.BeginCard("Save", "Saves which cheats are on for next time");
        if (Ui.Button("Save Settings", Ui.Primary)) SaveSettings();
        Ui.EndCard();
    }

    void KeyRow(string label, ConfigEntry<string> entry)
    {
        bool active = rebinding == entry;
        Ui.BeginRow(2);
        Ui.Label(label, Ui.Text, Ui.W(220));
        if (Ui.Button(active ? "Press a key…" : entry.Value, active ? Ui.FieldOn : Ui.Chip)) rebinding = active ? null : entry;
        Ui.EndRow();
    }

    // ---------------- actions ----------------
    void AddMoney(long amount)
    {
        Act("", () =>
        {
            var eco = EconomyManager.Instance;
            long units = amount * Math.Max(1, Currency.UnitsPerRuble);   // the game counts in kopecks
            if (units > 0) eco.AddIncome(units, Me.transform.position, IncomeSource.Other);
            else eco.SetBalance(Math.Max(0, eco.Balance + units));
            SetStatus($"Money: {Rubles(eco.Balance)}");
        });
    }

    void AddPoints(int n)
    {
        var sp = SkillPointBank.Instance;
        if (sp == null) { SetStatus("Skill points are not loaded yet"); return; }
        sp.RestoreGranted(sp.GrantedPoints + n);
        sp.NotifyChanged();
        SetStatus($"+{n} skill points  ·  {sp.Available} to spend");
    }

    void Grant(UpgradeDefinition d, int levels)
    {
        var um = UpgradeManager.Instance;
        int got = um.GrantLevel(d.id, levels);
        SetStatus(got > 0 ? $"{SafeName(d)} +{got}" : $"{SafeName(d)} can not go higher");
    }

    void MaxAll()
    {
        var um = UpgradeManager.Instance;
        int total = 0;
        // some upgrades need others first, so go round a few times
        for (int pass = 0; pass < 8; pass++)
        {
            int now = 0;
            foreach (var d in um.definitions)
            {
                if (d == null) continue;
                try
                {
                    if (um.IsConsumable(d.id)) continue;
                    int lv = um.GetLevel(d.id), max = um.GetMaxLevel(d.id);
                    if (lv < max) now += um.GrantLevel(d.id, max - lv);
                }
                catch (Exception e) { Plugin.L.LogWarning($"Upgrade {d.id}: {e.Message}"); }
            }
            total += now;
            if (now == 0) break;
        }
        SetStatus(total > 0 ? $"{total} upgrade levels added" : "Everything is maxed already");
    }

    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        var l = new List<Place>();
        void Add<T>(string name, string group) where T : Component
        {
            int i = 0;
            foreach (var c in FindAll<T>())
                if (c != null && c.gameObject.activeInHierarchy)
                    l.Add(new Place { Name = name + (++i > 1 ? $" {i}" : ""), Group = group, Pos = c.transform.position });
        }
        Add<Workstation>("Workstation", "Peeling table");
        Add<UpgradeTerminal>("Upgrade Terminal", "Shop and skills");
        Add<PotatoDeliveryZone>("Delivery Zone", "Sell peeled potatoes");
        Add<PotatoPeelMachine>("Peel Machine", "Machines");
        Add<PotatoPackager>("Packager", "Machines");
        Add<ClawCrane>("Claw Crane", "Machines");
        Add<PowerControlPanel>("Power Panel", "Machines");
        Add<KnifeSharpener>("Knife Sharpener", "Tools");
        Add<MopStation>("Mop Station", "Cleaning");
        Add<LetterMailbox>("Mailbox", "Letters");
        Add<DartsStation>("Darts", "Break");
        Add<PuppyBowl>("Dog Bowl", "Your dog");
        places = l;

        var s = new List<Place>();
        foreach (var h in FindAll<HiddenItemPickup>()) if (h != null && h.gameObject.activeInHierarchy) s.Add(new Place { Name = "Hidden Item", Pos = h.transform.position });
        foreach (var h in FindAll<HiddenPaperStash>()) if (h != null && h.gameObject.activeInHierarchy) s.Add(new Place { Name = "Paper Stash", Pos = h.transform.position });
        secrets = s;
    }

    void TeleportTo(Vector3 pos, string name)
    {
        Act($"Teleported to {name}", () => Me.Move.Warp(pos + Vector3.up * 1f));
    }

    bool Check()
    {
        if (!InGame) { SetStatus("Load your game first"); return false; }
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
