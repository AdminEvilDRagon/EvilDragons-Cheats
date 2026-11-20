using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using HarmonyLib;
using Mirror;
using StarterAssets;
using UnityEngine;

namespace SupermarketTogetherCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Supermarket Together", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.supermarkettogether";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<KeyCode> KeyMenu, KeyFly, KeyEsp;
    internal static ConfigEntry<string> Theme;
    internal static ConfigEntry<float> MenuScale;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", KeyCode.F1, "Open/close menu");
        KeyFly = Config.Bind("Hotkeys", "Fly", KeyCode.F2, "Toggle fly mode (noclip)");
        KeyEsp = Config.Bind("Hotkeys", "ThiefEsp", KeyCode.F3, "Show who is a thief");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        MenuScale = Config.Bind("Interface", "MenuScale", 1f, "Size of the menu (0.75 - 1.3)");
        Ui.InitialTheme = Theme.Value;
        Ui.UseOsFonts = false;   // Unity 2023.2 cannot use Windows fonts in IMGUI (it spams errors and lags)
        Ui.Log = m => Logger.LogWarning(m);

        var go = new GameObject("EvilDragonCheatMenu");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        try { new Harmony(Guid).PatchAll(typeof(Patches)); } catch (Exception e) { Logger.LogError(e); }
        Logger.LogInfo($"EvilDragon's Cheats v{Version} loaded");
    }
}

/// What the Harmony patches read. Only active while you play alone.
static class Cheat
{
    public static bool Allowed, Fly, FreeShop, AnyPrice;
    public static float FlySpeed = 2f, Income = 1f;
    public static int ExtraCustomers;
    public static bool MenuOpen;
}

static class Patches
{
    // free shopping: money is never taken away
    // income multiplier: everything you earn is multiplied
    [HarmonyPrefix, HarmonyPatch(typeof(GameData), "UserCode_CmdAlterFunds__Single")]
    static bool Funds(ref float fundsToAdd)
    {
        if (!Cheat.Allowed) return true;
        if (fundsToAdd < 0f && Cheat.FreeShop) return false;
        if (fundsToAdd > 0f) fundsToAdd *= Cheat.Income;
        return true;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GameData), "UserCode_CmdAlterFundsWithoutExperience__Single")]
    static bool FundsNoXp(float fundsToAdd) => !(Cheat.Allowed && Cheat.FreeShop && fundsToAdd < 0f);

    // more customers at the same time
    [HarmonyPrefix, HarmonyPatch(typeof(NPC_Manager), "FixedUpdate")]
    static void MoreCustomers(out int __state)
    {
        __state = 0;
        if (!Cheat.Allowed || Cheat.ExtraCustomers <= 0 || GameData.Instance == null) return;
        __state = Cheat.ExtraCustomers;
        GameData.Instance.maxCustomersNPCs += __state;
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(NPC_Manager), "FixedUpdate")]
    static void MoreCustomersEnd(int __state)
    {
        if (__state != 0 && GameData.Instance != null) GameData.Instance.maxCustomersNPCs -= __state;
    }

    // customers buy at any price: their "too expensive" limit goes way up while they decide
    static float[] savedInflation;

    [HarmonyPrefix, HarmonyPatch(typeof(NPC_Manager), "CustomerNPCControl")]
    static void AnyPrice()
    {
        savedInflation = null;
        if (!(Cheat.Allowed && Cheat.AnyPrice) || ProductListing.Instance == null) return;
        var inf = ProductListing.Instance.tierInflation;
        savedInflation = (float[])inf.Clone();
        for (int i = 0; i < inf.Length; i++) inf[i] *= 1000f;
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(NPC_Manager), "CustomerNPCControl")]
    static void AnyPriceEnd()
    {
        if (savedInflation == null || ProductListing.Instance == null) return;
        Array.Copy(savedInflation, ProductListing.Instance.tierInflation, savedInflation.Length);
        savedInflation = null;
    }

    // while the menu is open your character and camera stand still
    [HarmonyPrefix, HarmonyPatch(typeof(FirstPersonController), "JumpAndGravity")]
    static bool Gravity(FirstPersonController __instance)
    {
        if (Cheat.MenuOpen) __instance.allowPlayerInput = false;
        return !(Cheat.Allowed && Cheat.Fly);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(CustomCameraController), "LateUpdate")]
    static bool NoLook() => !Cheat.MenuOpen;

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerNetwork), "Update")]
    static bool NoClicks(PlayerNetwork __instance) => !(Cheat.MenuOpen && __instance.isLocalPlayer);

    // fly: replace the walking with free flight through walls
    [HarmonyPrefix, HarmonyPatch(typeof(FirstPersonController), "Move")]
    static bool FlyMove(FirstPersonController __instance)
    {
        if (!(Cheat.Allowed && Cheat.Fly) || __instance != FirstPersonController.Instance) return true;
        try
        {
            var cam = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : __instance.transform;
            Vector3 dir = Vector3.zero;
            if (!Cheat.MenuOpen)
            {
                if (Input.GetKey(KeyCode.W)) dir += cam.forward;
                if (Input.GetKey(KeyCode.S)) dir -= cam.forward;
                if (Input.GetKey(KeyCode.D)) dir += cam.right;
                if (Input.GetKey(KeyCode.A)) dir -= cam.right;
                if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;
            }
            float speed = 8f * Cheat.FlySpeed * (Input.GetKey(KeyCode.LeftShift) ? 2f : 1f);
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
    public static FirstPersonController Me => FirstPersonController.Instance;
    public static GameData Data => GameData.Instance;
    public static bool InGame => Me != null && Data != null && NPC_Manager.Instance != null && ProductListing.Instance != null;

    /// Why the cheats are off right now (null = they are on).
    public static string Why
    {
        get
        {
            if (!InGame) return "Load your supermarket first.";
            if (!NetworkServer.active) return "You are not the host of this game.";
            int others = NetworkServer.connections.Count - 1;
            if (others > 0) return $"{others} other player{(others == 1 ? " is" : "s are")} in this game.";
            return null;
        }
    }
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Store", "Products", "Customers", "Day", "Teleport", "Settings" };
    static readonly float[] MenuSizes = { 0.75f, 0.85f, 1f, 1.15f, 1.3f };

    // player
    float speedMult = 1f, jumpMult = 1f;
    float baseWalk = -1f, baseRun, baseCrouch, baseJump;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };

    // store
    long moneyAmount = 1000;
    static readonly long[] MoneyPresets = { 100, 1_000, 10_000, 100_000, 1_000_000 };
    static readonly float[] IncomePresets = { 1f, 2f, 5f, 10f };
    int pointsAmount = 5;
    static readonly int[] PointPresets = { 1, 5, 10, 50 };

    // products
    int boxAmount = 1, productPage;
    static readonly int[] BoxAmounts = { 1, 3, 5, 10 };
    int selectedProduct = -1;
    bool onlyUnlocked;
    const int PageSize = 10;

    // customers
    bool esp, noThieves;
    int spawnCustomers = 5;
    static readonly int[] SpawnCustomerPresets = { 1, 5, 10, 20 };
    static readonly int[] ExtraCustomerPresets = { 0, 5, 10, 25 };

    // day
    bool freezeTime;
    float frozenAt;
    float gameSpeed = 1f;
    static readonly float[] GameSpeeds = { 0.5f, 1f, 2f, 3f, 5f };
    static readonly float[] Hours = { 8f, 12f, 16f, 20f, 22f };

    // search (one box per tab, cleared when you switch tabs)
    string search = "";
    bool searching, searchAllSelected;
    int searchFieldClickFrame = -1, searchCloseCheckFrame = -1;

    // teleport
    class Place { public string Name, Group; public Vector3 Pos; }
    List<Place> places = new List<Place>();
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
    ConfigEntry<KeyCode> rebinding;

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
        Cheat.FreeShop = Saved.Get("FreeShop", false);
        Cheat.Income = Saved.Get("IncomeMultiplier", 1f);
        Cheat.AnyPrice = Saved.Get("AnyPrice", false);
        Cheat.ExtraCustomers = Saved.Get("ExtraCustomers", 0);
        esp = Saved.Get("ThiefEsp", false);
        noThieves = Saved.Get("NoThieves", false);
        moneyAmount = Saved.Get("MoneyAmount", 1000L);
    }

    void SaveSettings()
    {
        Saved.Set("WalkSpeed", speedMult);
        Saved.Set("JumpPower", jumpMult);
        Saved.Set("FlySpeed", Cheat.FlySpeed);
        Saved.Set("FreeShop", Cheat.FreeShop);
        Saved.Set("IncomeMultiplier", Cheat.Income);
        Saved.Set("AnyPrice", Cheat.AnyPrice);
        Saved.Set("ExtraCustomers", Cheat.ExtraCustomers);
        Saved.Set("ThiefEsp", esp);
        Saved.Set("NoThieves", noThieves);
        Saved.Set("MoneyAmount", moneyAmount);
        Saved.Flush();
        SetStatus("Settings saved - they load automatically next time");
    }

    static bool Pressed(KeyCode k) { try { return k != KeyCode.None && Input.GetKeyDown(k); } catch { return false; } }

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();

        if (Time.unscaledTime - allowedAt > 1f)
        {
            allowedAt = Time.unscaledTime;
            string why;
            try { why = Game.Why; } catch (Exception e) { why = "Error: " + e.Message; }
            bool now = why == null;
            if (why != lastWhy) { Plugin.L.LogInfo(now ? "Cheats on" : "Cheats off: " + why); lastWhy = why; }
            if (allowed && !now) { TurnOff(); if (Game.InGame) SetStatus("Cheats are off - " + why); }
            allowed = now;
            if (allowed) try { SecondTick(); } catch (Exception e) { Plugin.L.LogError(e); }
        }

        // leave the search box as soon as you click anywhere else
        if (searchCloseCheckFrame >= 0 && Time.frameCount > searchCloseCheckFrame)
        {
            if (searchFieldClickFrame != searchCloseCheckFrame) searching = false;
            searchCloseCheckFrame = -1;
        }
        if (searching && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))) searchCloseCheckFrame = Time.frameCount;

        if (rebinding == null && Pressed(Plugin.KeyMenu.Value)) { editId = null; searching = false; SetShow(!show); }
        else if (rebinding != null) HandleRebind();
        else if (searching) HandleSearchInput();
        else if (editId != null) HandleNumberInput();
        else if (allowed)
        {
            if (Pressed(Plugin.KeyFly.Value)) SetFly(!Cheat.Fly);
            if (Pressed(Plugin.KeyEsp.Value)) { esp = !esp; SetStatus(esp ? "Thief ESP on" : "Thief ESP off"); }
        }

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        Cheat.Allowed = allowed;
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (!allowed || !Game.InGame) { Cheat.Fly = false; return; }

        var fpc = Game.Me;
        if (baseWalk < 0f) { baseWalk = fpc.MoveSpeed; baseRun = fpc.SprintSpeed; baseCrouch = fpc.CrouchSpeed; baseJump = fpc.JumpHeight; }
        fpc.MoveSpeed = baseWalk * speedMult;
        fpc.SprintSpeed = baseRun * speedMult;
        fpc.CrouchSpeed = baseCrouch * speedMult;
        fpc.JumpHeight = baseJump * jumpMult;

        if (freezeTime && Game.Data.timeOfDay != frozenAt) Game.Data.NetworktimeOfDay = frozenAt;
    }

    /// Runs once per second while the cheats are on.
    void SecondTick()
    {
        if (!noThieves || !Game.InGame) return;
        // the same thing the game does when a camera catches a thief before he steals
        foreach (var npc in Customers())
            if (npc.isAThief && !npc.thiefFleeing && npc.productsIDCarrying.Count == 0) npc.isAThief = false;
    }

    void TurnOff()
    {
        Cheat.Allowed = false;
        Cheat.Fly = false;
        freezeTime = false;
        RestoreMovement();
        if (Time.timeScale != 1f && gameSpeed != 1f) { Time.timeScale = 1f; gameSpeed = 1f; }
    }

    void RestoreMovement()
    {
        try
        {
            var fpc = Game.Me;
            if (fpc != null && baseWalk > 0f) { fpc.MoveSpeed = baseWalk; fpc.SprintSpeed = baseRun; fpc.CrouchSpeed = baseCrouch; fpc.JumpHeight = baseJump; }
        }
        catch { }
        baseWalk = -1f;
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
            Cursor.lockState = oldLock;
            Cursor.visible = oldVisible;
        }
    }

    // ---------------- typed input ----------------
    void HandleNumberInput()
    {
        foreach (char c in Input.inputString)
        {
            if (c >= '0' && c <= '9' && editBuf.Length < 12) editBuf += c;
            else if (c == '\b' && editBuf.Length > 0) editBuf = editBuf.Substring(0, editBuf.Length - 1);
        }
        if (Pressed(KeyCode.Delete)) editBuf = "";
        if (editBuf.Length > 0 && long.TryParse(editBuf, out var v)) editSet?.Invoke(Math.Min(editMax, Math.Max(editMin, v)));
        if (Pressed(KeyCode.Return) || Pressed(KeyCode.KeypadEnter) || Pressed(KeyCode.Escape)) editId = null;
    }

    void HandleSearchInput()
    {
        // Ctrl+A selects the whole search text: the next key replaces it, Backspace/Delete clears it
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && Pressed(KeyCode.A)) searchAllSelected = search.Length > 0;
        if (searchAllSelected && Pressed(KeyCode.Delete)) { search = ""; searchAllSelected = false; ResetPages(); }
        foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (searchAllSelected) search = "";
                else if (search.Length > 0) search = search.Substring(0, search.Length - 1);
                searchAllSelected = false;
            }
            else if (c == '\n' || c == '\r') searching = false;
            else if (!char.IsControl(c) && !ctrl)
            {
                if (searchAllSelected) { search = ""; searchAllSelected = false; }
                if (search.Length < 30) search += c;
            }
            ResetPages();
        }
        if (Pressed(KeyCode.Escape)) searching = false;
        if (!searching) searchAllSelected = false;
    }

    void ResetPages() { productPage = 0; }

    void SearchBar()
    {
        GUILayout.BeginHorizontal();
        Ui.Label("Search", Ui.Sub, Ui.W(60));
        if (!searching) searchAllSelected = false;
        string shown = searchAllSelected ? $"<color={Ui.AHex}>{search}</color>" : search;
        if (Ui.Button(shown, searching ? Ui.FieldOn : Ui.Field)) { searching = true; searchFieldClickFrame = Time.frameCount; editId = null; }
        if (search.Length > 0 && Ui.Button("✕", Ui.Chip, Ui.W(36))) { search = ""; ResetPages(); }
        GUILayout.EndHorizontal();
    }

    bool Matches(string s) => search.Length == 0 || (s != null && s.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

    static KeyCode[] allKeys;
    void HandleRebind()
    {
        if (Pressed(KeyCode.Escape)) { rebinding = null; SetStatus("Cancelled"); return; }
        allKeys = allKeys ?? ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
            .Where(k => k != KeyCode.None && k != KeyCode.Escape && k < KeyCode.Mouse0).ToArray();
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
        try { if (allowed && Game.InGame && esp) DrawEsp(); } catch { }
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
        Ui.Label($"Supermarket Together  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyEsp.Value.ToString(), "Thief ESP");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!allowed) Ui.Label($"<color=#ff6b6b><b>Cheats are off</b></color> - {lastWhy ?? "only when you play alone as the host."}", Ui.Sub);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }   // two rows of tabs
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; searching = false; search = ""; ResetPages(); bodyScroll = Vector2.zero; }
        }
        GUILayout.EndHorizontal();
        Ui.Space(6);

        bool showStatus = status.Length > 0 && Time.unscaledTime - statusTime < 6f;
        bodyScroll = GUILayout.BeginScrollView(bodyScroll, false, false, GUIStyle.none, ScrollBar(), GUIStyle.none);
        // fixed width: long names can't push the content past the right edge (they get cut inside their button)
        GUILayout.BeginVertical(GUILayout.Width(BodyWidth), GUILayout.MaxWidth(BodyWidth));
        bool locked = tab != Tabs.Length - 1 && !allowed;
        if (locked) GUI.enabled = false;
        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawStore(); break;
            case 2: DrawProducts(); break;
            case 3: DrawCustomers(); break;
            case 4: DrawDay(); break;
            case 5: DrawTeleport(); break;
            case 6: DrawSettings(); break;
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

    void Pager(ref int page, int pages)
    {
        if (pages <= 1) return;
        GUILayout.BeginHorizontal();
        if (Ui.Button("◀", Ui.Chip, Ui.W(50))) page--;
        Ui.Label($"Page {page + 1} / {pages}", Ui.Sub);
        if (Ui.Button("▶", Ui.Chip, Ui.W(50))) page++;
        GUILayout.EndHorizontal();
    }

    static string Grey(string s) => $"   <size=12><color=#b8c2d6>{s}</color></size>";

    static string Clock(float h)
    {
        h = Mathf.Repeat(h, 24f);
        int hh = (int)h, mm = (int)((h - hh) * 60f);
        return $"{hh:00}:{mm:00}";
    }

    // ---------------- tabs ----------------
    void DrawPlayer()
    {
        Ui.BeginCard("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump power", Ui.Sub);
        jumpMult = Ui.Chips(jumpMult, JumpPresets, v => $"x{v:0.#}", 4);
        if (Ui.Button("Reset Movement", Ui.Chip)) { speedMult = 1f; jumpMult = 1f; }
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly through walls and the roof");
        bool f = Ui.Toggle(Cheat.Fly, $"Fly  [{Plugin.KeyFly.Value}]", "WASD, Space up, Ctrl down, Shift faster");
        if (f != Cheat.Fly) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        Cheat.FlySpeed = Ui.Chips(Cheat.FlySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();
    }

    void DrawStore()
    {
        var gd = Game.Data;
        float money = gd != null ? gd.gameFunds : 0f;
        int points = gd != null ? gd.gameFranchisePoints : 0, level = gd != null ? gd.gameFranchiseLevel : 0;

        Ui.BeginCard("Money", $"$ {money:N2}");
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + v.ToString("N0"), 5);
        NumField("money", "Custom amount", moneyAmount, 1, 2_000_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${moneyAmount:N0}", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${moneyAmount:N0}", Ui.Tab)) AddMoney(-moneyAmount);
        GUILayout.EndHorizontal();
        Cheat.FreeShop = Ui.Toggle(Cheat.FreeShop, "Free Shopping", "buying, building and bills cost $0 (you still need the price on your account)");
        Ui.Label("Income multiplier - every sale earns more", Ui.Sub);
        Cheat.Income = Ui.Chips(Cheat.Income, IncomePresets, v => $"x{v:0}", 4);
        Ui.EndCard();

        Ui.BeginCard("Franchise", $"Level {level}  ·  {points} franchise points");
        pointsAmount = Ui.Chips(pointsAmount, PointPresets, v => "+" + v, 4);
        if (Ui.Button($"+ {pointsAmount} Franchise Points", Ui.Primary))
            Act($"+{pointsAmount} franchise points", () => gd.NetworkgameFranchisePoints = gd.gameFranchisePoints + pointsAmount);
        if (Ui.Button("Unlock All Product Licenses", Ui.Chip)) Act("Every product license unlocked", UnlockAllProducts);
        Ui.EndCard();

        Ui.BeginCard("Shelves");
        if (Ui.Button("Refill All Shelves", Ui.Primary)) Act("", RefillShelves);
        Ui.Label("Fills every shelf row that already has a product label.", Ui.Sub);
        Ui.EndCard();
    }

    void DrawProducts()
    {
        var pl = ProductListing.Instance;
        int total = pl != null && pl.productsData != null ? pl.productsData.Length : 0;
        var list = new List<int>();
        for (int i = 0; i < total; i++)
        {
            if (pl.productsData[i] == null) continue;
            if (onlyUnlocked && !pl.availableProducts.Contains(i)) continue;
            if (Matches(ProductName(i)) || Matches(pl.productsData[i].productBrand)) list.Add(i);
        }

        Ui.BeginCard("Spawn Products", "Full boxes of any product");
        Ui.Label("Boxes", Ui.Sub);
        boxAmount = Ui.Chips(boxAmount, BoxAmounts, v => v + "x", 4);
        onlyUnlocked = Ui.Toggle(onlyUnlocked, "Only Unlocked Products", "hide products you have no license for");
        SearchBar();
        Ui.Label($"{list.Count} found", Ui.Sub);
        int pages = Math.Max(1, (list.Count + PageSize - 1) / PageSize);
        productPage = Mathf.Clamp(productPage, 0, pages - 1);
        foreach (int id in list.Skip(productPage * PageSize).Take(PageSize))
        {
            var d = pl.productsData[id];
            string hint = $"{d.productBrand}  ·  {d.maxItemsPerBox} per box" + (pl.availableProducts.Contains(id) ? "" : "  ·  <color=#ffb35c>locked</color>");
            if (Ui.Button($"<b>{ProductName(id)}</b>" + Grey(hint), selectedProduct == id ? Ui.RowOn : Ui.Row)) { selectedProduct = id; searching = false; }
        }
        Pager(ref productPage, pages);
        Ui.EndCard();

        bool has = selectedProduct >= 0 && selectedProduct < total;
        if (has) Ui.Label($"Selected: <b>{ProductName(selectedProduct)}</b>", Ui.Text);
        if (Ui.Button(has ? $"SPAWN {boxAmount}x IN FRONT OF ME" : "Select a product above", Ui.Primary) && has)
            Act($"{boxAmount}x {ProductName(selectedProduct)} in front of you", () => SpawnBoxesInFront(selectedProduct, boxAmount));
        Ui.Space(4);
        if (Ui.Button("Deliver to the Delivery Spot (free)", Ui.Tab) && has)
            Act($"{boxAmount}x {ProductName(selectedProduct)} on its way to the delivery spot", () => Deliver(selectedProduct, boxAmount));
    }

    void DrawCustomers()
    {
        var customers = Customers().ToList();
        int thieves = customers.Count(c => c.isAThief);
        int employees = Employees().Count();
        string summary = $"{customers.Count} customers  ·  <color=#ff5050>{thieves} thieves</color>  ·  {employees} employees";

        Ui.BeginCard("Thieves", summary);
        esp = Ui.Toggle(esp, $"Show Thieves  [{Plugin.KeyEsp.Value}]", "red = thief, green = customer, blue = employee - above their head");
        noThieves = Ui.Toggle(noThieves, "No Thieves", "every thief changes his mind before he steals");
        if (Ui.Button("Catch All Thieves Now", Ui.Primary)) Act("", CatchThieves);
        Ui.EndCard();

        Ui.BeginCard("Customers");
        Cheat.AnyPrice = Ui.Toggle(Cheat.AnyPrice, "Customers Pay Any Price", "nothing is \"too expensive\" any more");
        Ui.Label("Extra customers in the store at the same time", Ui.Sub);
        Cheat.ExtraCustomers = Ui.Chips(Cheat.ExtraCustomers, ExtraCustomerPresets, v => "+" + v, 4);
        Ui.Space(4);
        Ui.Label("Spawn customers right now", Ui.Sub);
        spawnCustomers = Ui.Chips(spawnCustomers, SpawnCustomerPresets, v => v + "x", 4);
        if (Ui.Button($"Spawn {spawnCustomers} Customers", Ui.Primary)) Act("", () => SpawnCustomers(spawnCustomers));
        if (Ui.Button("Send All Customers Home", Ui.Chip)) Act("All customers removed", () => NPC_Manager.Instance.RemoveCustomers());
        Ui.EndCard();
    }

    void DrawDay()
    {
        var gd = Game.Data;
        string open = gd != null && gd.isSupermarketOpen ? "<color=#50ff7a>open</color>" : "<color=#ff6b6b>closed</color>";
        Ui.BeginCard("Time", gd != null ? $"Day {gd.gameDay}  ·  {Clock(gd.timeOfDay)}  ·  store {open}" : null);
        bool ft = Ui.Toggle(freezeTime, "Freeze Time", "the clock stops - the day never ends");
        if (ft != freezeTime) { freezeTime = ft; if (gd != null) frozenAt = gd.timeOfDay; }
        Ui.Label("Set the time", Ui.Sub);
        GUILayout.BeginHorizontal();
        foreach (float h in Hours)
            if (Ui.Button(Clock(h), Ui.Chip)) Act($"Time set to {Clock(h)}", () => { gd.NetworktimeOfDay = h; frozenAt = h; });
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Day");
        if (Ui.Button("Open the Store", Ui.Primary)) Act("Store opened", () => gd.CmdOpenSupermarket());
        Ui.Space(4);
        GUILayout.BeginHorizontal();
        if (Ui.Button("Skip to Closing Time", Ui.Tab)) Act("It is closing time - press the end day button or use End Day Now", () => { gd.NetworktimeOfDay = 22.55f; frozenAt = 22.55f; });
        if (Ui.Button("End Day Now", Ui.Tab)) Act("Day ended", EndDay);
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Game Speed");
        float gs = Ui.Chips(gameSpeed, GameSpeeds, v => $"x{v:0.#}", 5);
        if (gs != gameSpeed) { gameSpeed = gs; Time.timeScale = gs; SetStatus($"Game speed x{gs:0.#}"); }
        Ui.EndCard();
    }

    void DrawTeleport()
    {
        if (Time.unscaledTime - placesScannedAt > 5f) ScanPlaces();
        Ui.BeginCard("Position");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Save Position", Ui.Chip)) { hasSaved = true; savedPos = Game.Me.transform.position; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places" : null);
        Vector3 me = Game.Me != null ? Game.Me.transform.position : Vector3.zero;
        foreach (var p in places)
            if (Ui.Button($"<b>{p.Name}</b>" + Grey($"{p.Group}  ·  {Vector3.Distance(me, p.Pos):0} m"), Ui.Row)) TeleportTo(p.Pos, p.Name);
        Ui.EndCard();
    }

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
        KeyRow("Thief ESP", Plugin.KeyEsp);
        Ui.EndCard();

        Ui.BeginCard("Save", "Saves which cheats are on and your settings for next time");
        if (Ui.Button("Save Settings", Ui.Primary)) SaveSettings();
        Ui.EndCard();
    }

    void KeyRow(string label, ConfigEntry<KeyCode> entry)
    {
        bool active = rebinding == entry;
        GUILayout.BeginHorizontal();
        Ui.Label(label, Ui.Text, Ui.W(220));
        if (Ui.Button(active ? "Press a key…" : entry.Value.ToString(), active ? Ui.FieldOn : Ui.Chip))
            rebinding = active ? null : entry;
        GUILayout.EndHorizontal();
    }

    // ---------------- ESP ----------------
    GUIStyle espStyle, espSubStyle;
    Texture2D espBg;
    void DrawEsp()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (espStyle == null || espBg == null)
        {
            espStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 20, richText = true };
            espStyle.normal.textColor = Color.white;
            espSubStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 14, richText = true };
            espSubStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            espBg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            espBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f)); espBg.Apply();
        }
        foreach (var c in Customers())
        {
            if (c.isAThief)
                Label3D(cam, TopOf(c.gameObject), "<color=#ff4040>THIEF</color>", c.thiefFleeing ? "running away" : c.productsIDCarrying.Count > 0 ? $"has {c.productsIDCarrying.Count} products" : null);
            else
                Label3D(cam, TopOf(c.gameObject), "<color=#40ff70>CUSTOMER</color>", c.productsIDCarrying.Count > 0 ? $"{c.productsIDCarrying.Count} products" : null);
        }
        foreach (var e in Employees())
            Label3D(cam, TopOf(e.gameObject), "<color=#4da6ff>EMPLOYEE</color>", e.NPCName);
    }

    /// A point just above the head (or the top of the model).
    static Vector3 TopOf(GameObject go)
    {
        float top = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            if (r != null && r.enabled && (r is SkinnedMeshRenderer || r is MeshRenderer)) top = Mathf.Max(top, r.bounds.max.y);
        var p = go.transform.position;
        return new Vector3(p.x, top > float.MinValue && top < p.y + 4f ? top + 0.35f : p.y + 2.2f, p.z);
    }

    void Label3D(Camera cam, Vector3 world, string text, string sub = null)
    {
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0.2f || sp.z > 120f) return;
        string line2 = (string.IsNullOrEmpty(sub) ? "" : sub + "  ·  ") + $"{sp.z:0} m";
        var s1 = espStyle.CalcSize(new GUIContent(text));
        var s2 = espSubStyle.CalcSize(new GUIContent(line2));
        float w = Mathf.Max(s1.x, s2.x) + 18f, h = s1.y + s2.y + 6f;
        var box = new Rect(sp.x - w / 2f, Screen.height - sp.y - h, w, h);
        GUI.DrawTexture(box, espBg);
        GUI.Label(new Rect(box.x, box.y + 2f, w, s1.y), text, espStyle);
        GUI.Label(new Rect(box.x, box.y + 2f + s1.y, w, s2.y), line2, espSubStyle);
    }

    // ---------------- game helpers ----------------
    static IEnumerable<NPC_Info> Customers()
    {
        var m = NPC_Manager.Instance;
        if (m == null || m.customersnpcParentOBJ == null) yield break;
        foreach (Transform t in m.customersnpcParentOBJ.transform)
        {
            var n = t != null ? t.GetComponent<NPC_Info>() : null;
            if (n != null) yield return n;
        }
    }

    static IEnumerable<NPC_Info> Employees()
    {
        var m = NPC_Manager.Instance;
        if (m == null || m.employeeParentOBJ == null) yield break;
        foreach (Transform t in m.employeeParentOBJ.transform)
        {
            var n = t != null ? t.GetComponent<NPC_Info>() : null;
            if (n != null) yield return n;
        }
    }

    static readonly Dictionary<int, string> nameCache = new Dictionary<int, string>();
    static string ProductName(int id)
    {
        if (nameCache.TryGetValue(id, out var n)) return n;
        try { n = LocalizationManager.instance != null ? LocalizationManager.instance.GetLocalizationString("product" + id) : null; } catch { n = null; }
        if (string.IsNullOrEmpty(n) || n.StartsWith("product")) n = "Product " + id;
        else nameCache[id] = n;
        return n;
    }

    // ---------------- actions ----------------
    bool Check()
    {
        if (!Game.InGame) { SetStatus("Load your supermarket first"); return false; }
        if (!allowed) { SetStatus("Cheats are off while other players are here"); return false; }
        return true;
    }

    /// Runs an action; an empty "done" text means the action sets its own status.
    void Act(string done, Action a)
    {
        if (!Check()) return;
        try { a(); if (done.Length > 0) SetStatus(done); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("That did not work: " + e.Message); }
    }

    void AddMoney(long amount)
    {
        Act($"{(amount >= 0 ? "+" : "−")}$ {Math.Abs(amount):N0}", () =>
        {
            var gd = Game.Data;
            float v = Mathf.Clamp(gd.gameFunds + amount, 0f, 2.14E+09f);
            gd.NetworkgameFunds = Mathf.Round(v * 100f) / 100f;
        });
    }

    void UnlockAllProducts()
    {
        var pl = ProductListing.Instance;
        for (int i = 0; i < pl.unlockedProductTiers.Length; i++) pl.unlockedProductTiers[i] = true;
        pl.updateSkillState();
        pl.updateProductList();
        Game.Data.GetComponent<ManagerBlackboard>().UpdateUnlockedFranchises();
    }

    static readonly System.Reflection.MethodInfo SetRow = AccessTools.Method(typeof(Data_Container), "UserCode_CmdUpdateArrayValues__Int32__Int32__Int32");

    void RefillShelves()
    {
        var m = NPC_Manager.Instance;
        int rows = 0, items = 0;
        for (int i = 0; i < m.shelvesOBJ.transform.childCount; i++)
        {
            var dc = m.shelvesOBJ.transform.GetChild(i).GetComponent<Data_Container>();
            if (dc == null || dc.productInfoArray == null) continue;
            for (int r = 0; r + 1 < dc.productInfoArray.Length; r += 2)
            {
                int pid = dc.productInfoArray[r];
                if (pid < 0 || pid >= ProductListing.Instance.productsData.Length) continue;
                int max = m.GetMaxProductsPerRow(i, pid);
                int have = dc.productInfoArray[r + 1];
                if (have >= max) continue;
                SetRow.Invoke(dc, new object[] { r, pid, max });
                rows++; items += max - have;
            }
        }
        SetStatus(rows == 0 ? "All shelves are already full (only rows with a product label get filled)" : $"Refilled {rows} shelf rows (+{items} products)");
    }

    static readonly System.Reflection.MethodInfo ParentBox = AccessTools.Method(typeof(ManagerBlackboard), "RpcParentBoxOnClient");

    void SpawnBox(int id, Vector3 pos, float yRot)
    {
        var mb = Game.Data.GetComponent<ManagerBlackboard>();
        var data = ProductListing.Instance.productsData[id];
        var go = Instantiate(mb.boxPrefab, pos, Quaternion.Euler(0f, yRot, 0f));
        var box = go.GetComponent<BoxData>();
        box.NetworkproductID = id;
        box.NetworknumberOfProducts = data.maxItemsPerBox;
        try
        {
            foreach (var n in new[] { "Canvas/Image1", "Canvas/Image2" })
            {
                var img = go.transform.Find(n)?.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.sprite = data.productSprite;
            }
        }
        catch { }
        go.transform.SetParent(mb.boxParent);
        NetworkServer.Spawn(go);
        try { ParentBox?.Invoke(mb, new object[] { go }); } catch { }
    }

    void SpawnBoxesInFront(int id, int amount)
    {
        var t = Game.Me.transform;
        var fwd = Vector3.ProjectOnPlane(Camera.main != null ? Camera.main.transform.forward : t.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = t.forward;
        Vector3 basePos = t.position + fwd * 1.6f + Vector3.up * 0.6f;
        float yRot = Quaternion.LookRotation(fwd).eulerAngles.y;
        for (int i = 0; i < amount; i++)
        {
            // a small stack: two side by side, then upwards
            var side = Vector3.Cross(Vector3.up, fwd) * ((i % 2) * 0.7f - 0.35f * (amount > 1 ? 1 : 0));
            SpawnBox(id, basePos + side + Vector3.up * (i / 2) * 0.6f, yRot);
        }
    }

    void Deliver(int id, int amount)
    {
        var mb = Game.Data.GetComponent<ManagerBlackboard>();
        for (int i = 0; i < amount; i++) mb.idsToSpawn.Add(id);
        mb.SpawnRemainingCargoFromAutosave();
    }

    void CatchThieves()
    {
        int changed = 0, removed = 0;
        foreach (var c in Customers().ToList())
        {
            if (!c.isAThief) continue;
            if (!c.thiefFleeing && c.productsIDCarrying.Count == 0) { c.isAThief = false; changed++; continue; }
            // already stealing: he vanishes and leaves without your products
            c.productsIDCarrying.Clear();
            c.productsCarryingPrice.Clear();
            c.isAThief = false;
            NetworkServer.Destroy(c.gameObject);
            removed++;
        }
        SetStatus(changed + removed == 0 ? "No thieves in the store" : $"Caught {changed + removed} thieves" + (removed > 0 ? $" ({removed} were already stealing and got removed)" : ""));
    }

    static readonly System.Reflection.MethodInfo SpawnCustomer = AccessTools.Method(typeof(NPC_Manager), "SpawnCustomerNPC");

    void SpawnCustomers(int amount)
    {
        var m = NPC_Manager.Instance;
        if (m.shelvesOBJ.transform.childCount < 1 || m.checkoutOBJ.transform.childCount < 1 || ProductListing.Instance.availableProducts.Count == 0)
        {
            SetStatus("You need at least one shelf, one checkout and a product license first");
            return;
        }
        for (int i = 0; i < amount; i++) m.StartCoroutine((IEnumerator)SpawnCustomer.Invoke(m, null));
        SetStatus($"{amount} customers are on their way (they arrive within a few seconds)");
    }

    static readonly System.Reflection.MethodInfo EndDayCmd = AccessTools.Method(typeof(GameData), "UserCode_CmdEndDayFromButton");

    void EndDay()
    {
        var gd = Game.Data;
        freezeTime = false;
        if (gd.timeOfDay <= 22f) gd.NetworktimeOfDay = 22.55f;
        gd.NetworkisSupermarketOpen = false;
        EndDayCmd.Invoke(gd, null);
    }

    // ---------------- teleport ----------------
    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        var list = new List<Place>();
        void Add(string name, string group, Vector3 p) { list.Add(new Place { Name = name, Group = group, Pos = p }); }
        try
        {
            var gd = Game.Data;
            var mb = gd.GetComponent<ManagerBlackboard>();
            var m = NPC_Manager.Instance;
            if (mb != null && mb.merchandiseSpawnpoint != null) Add("Delivery Spot", "Boxes you buy land here", mb.merchandiseSpawnpoint.transform.position);
            if (mb != null && mb.emptyBoxSpawnpoint != null) Add("Empty Boxes", "Store", mb.emptyBoxSpawnpoint.transform.position);
            if (gd.playerSpawnPosition != Vector3.zero) Add("Start Point", "Store", gd.playerSpawnPosition);
            if (m != null)
            {
                if (m.checkoutOBJ != null)
                    for (int i = 0; i < m.checkoutOBJ.transform.childCount; i++) Add($"Checkout {i + 1}", "Checkouts", m.checkoutOBJ.transform.GetChild(i).position);
                if (m.storageOBJ != null && m.storageOBJ.transform.childCount > 0) Add("Storage", "Storage shelves", m.storageOBJ.transform.GetChild(0).position);
                if (m.employeeSpawnpoint != null) Add("Staff Entrance", "Employees", m.employeeSpawnpoint.transform.position);
                if (m.restSpotOBJ != null) Add("Break Room", "Employees", m.restSpotOBJ.transform.position);
                if (m.trashSpotOBJ != null) Add("Trash", "Outside", m.trashSpotOBJ.transform.position);
                if (m.recycleSpot1OBJ != null) Add("Box Recycling", "Outside", m.recycleSpot1OBJ.transform.position);
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("Teleport scan: " + e.Message); }
        places = list;
    }

    void TeleportTo(Vector3 pos, string name)
    {
        Act($"Teleported to {name}", () =>
        {
            var me = Game.Me;
            var target = pos + Vector3.up * 0.3f;
            var ft = me.GetComponent<FirstPersonTransform>();
            if (ft != null) ft.coroutineActivator(target, me.transform.eulerAngles.y);
            else
            {
                var cc = me.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                me.transform.position = target;
                if (cc != null) cc.enabled = true;
            }
        });
    }
}
