using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace WobblyLifeCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Wobbly Life", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.wobblylife";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<KeyCode> KeyMenu, KeyMoney, KeyFly, KeyGun;
    internal static ConfigEntry<string> Theme;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", KeyCode.F1, "Open/close menu");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", KeyCode.F2, "Add the selected money amount");
        KeyFly = Config.Bind("Hotkeys", "Fly", KeyCode.F3, "Toggle fly mode (noclip)");
        KeyGun = Config.Bind("Hotkeys", "MoneyGun", KeyCode.G, "Hold to shoot money bags (Money Gun must be on)");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
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

static class Patches
{
    // The game's own free camera sits on F1 too: don't open it when F1 is our menu key, or while the menu is open.
    [HarmonyPrefix, HarmonyPatch(typeof(PlayerController), nameof(PlayerController.ClientFreeCameraSelectPressed))]
    static bool NoFreeCam()
    {
        try { if (CheatMenu.MenuOpen || Input.GetKey(Plugin.KeyMenu.Value) || Input.GetKeyUp(Plugin.KeyMenu.Value)) return false; } catch { }
        return true;
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
    public static GameInstance GI => UnitySingleton<GameInstance>.InstanceExists ? UnitySingleton<GameInstance>.Instance : null;

    /// The first local player (split screen player 1).
    public static PlayerController Me
    {
        get
        {
            var gi = GI;
            if (gi == null) return null;
            var list = gi.GetPlayerControllers();
            if (list == null) return null;
            PlayerController best = null;
            foreach (var pc in list)
                if (pc != null && pc.IsLocal() && (best == null || pc.GetLocalPlayerid() < best.GetLocalPlayerid())) best = pc;
            return best;
        }
    }

    public static PlayerCharacter Character => Me != null ? Me.GetPlayerCharacter() : null;
    public static PlayerCharacterMovement Movement => Character != null ? Character.GetPlayerCharacterMovement() : null;
    public static bool InGame => Me != null && Character != null;

    /// Cheats only work when you host and every player is on this PC (alone or split screen).
    public static bool Allowed
    {
        get
        {
            var me = Me;
            if (me == null || me.networkObject == null || !me.networkObject.IsServer()) return false;
            foreach (var pc in GI.GetPlayerControllers())
                if (pc != null && !pc.IsLocal()) return false;
            return true;
        }
    }
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    public static bool MenuOpen;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Money", "Spawn", "Tools", "Unlocks", "Teleport", "Settings" };

    // player
    bool flying;
    float speedMult = 1f, jumpMult = 1f, flySpeed = 2f;
    bool movementTouched;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };

    // money
    long moneyAmount = 10_000;
    static readonly long[] MoneyPresets = { 1_000, 10_000, 50_000, 100_000, 500_000, 1_000_000 };

    // spawn
    class SpawnEntry { public string Name, Category; public AssetReference Ref; public GameObject Go; public VehicleAssetReference Vehicle; public int Cost; }
    readonly List<SpawnEntry> spawnables = new List<SpawnEntry>();
    bool catalogLoading, catalogLoaded;
    string spawnCategory = "All", search = "";
    string[] spawnCategories = { "All" };
    bool searching, searchAllSelected;
    int searchFieldClickFrame = -1, searchCloseCheckFrame = -1;
    int spawnPage, spawnAmount = 1, catPage;
    SpawnEntry selectedSpawn;
    static readonly int[] AmountPresets = { 1, 3, 5, 10 };

    // tools
    int bagAmount = 1000;
    static readonly long[] BagPresets = { 100, 1_000, 10_000, 100_000 };
    bool moneyGun, clickTools = true, bigObjects;
    float gunNext, pushForce = 15f;
    static readonly float[] PushPresets = { 5f, 15f, 40f, 100f };
    class Valuable { public string Name, Address; }
    readonly List<Valuable> valuables = new List<Valuable>();
    bool valuablesScanned;
    class Changed { public GameObject Go; public Rigidbody AddedRb; public MeshCollider Convexed; public bool WasDisabled; public GameObject Clone; public GameObject Original; public Vector3 Pos; public Quaternion Rot; }
    readonly List<Changed> changes = new List<Changed>();

    // teleport
    class Place { public string Name; public Transform At; public string Group; }
    List<Place> places = new List<Place>();
    float placesScannedAt = -100f;
    bool hasSaved;
    Vector3 savedPos;
    int placePage;
    const int PageSize = 10;

    // allowed check (once per second)
    bool allowed;
    float allowedAt = -10f;

    // number input + key rebinding
    string editId, editBuf = "";
    long editMin, editMax;
    Action<long> editSet;
    ConfigEntry<KeyCode> rebinding;

    string status = "";
    float statusTime;

    void SetStatus(string s) { status = s; statusTime = Time.unscaledTime; Plugin.L.LogInfo(s); }

    static string Short(long v) =>
        v >= 1_000_000 ? $"{v / 1_000_000d:0.#}M" :
        v >= 1_000 ? $"{v / 1_000d:0.#}K" : v.ToString();

    // ---------------- saved settings ----------------
    bool settingsLoaded;
    void LoadSettings()
    {
        settingsLoaded = true;
        speedMult = Saved.Get("WalkSpeed", 1f);
        jumpMult = Saved.Get("JumpPower", 1f);
        flySpeed = Saved.Get("FlySpeed", 2f);
        moneyAmount = Saved.Get("MoneyAmount", 10_000L);
    }

    void SaveSettings()
    {
        Saved.Set("WalkSpeed", speedMult);
        Saved.Set("JumpPower", jumpMult);
        Saved.Set("FlySpeed", flySpeed);
        Saved.Set("MoneyAmount", moneyAmount);
        Saved.Flush();
        SetStatus("Settings saved - they load automatically next time");
    }

    // ---------------- keyboard ----------------
    static bool Pressed(KeyCode k) { try { return k != KeyCode.None && Input.GetKeyDown(k); } catch { return false; } }

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();

        if (Time.unscaledTime - allowedAt > 1f)
        {
            allowedAt = Time.unscaledTime;
            bool now = false;
            try { now = Game.Allowed; } catch { }
            if (allowed && !now) { TurnOff(); SetStatus("Another player joined - cheats are off"); }
            allowed = now;
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
            if (Pressed(Plugin.KeyMoney.Value)) AddMoney(moneyAmount);
            if (Pressed(Plugin.KeyFly.Value)) SetFly(!flying);
            bool held = false;
            try { held = Input.GetKey(Plugin.KeyGun.Value); } catch { }
            if (moneyGun && held && !show && Time.unscaledTime >= gunNext) { gunNext = Time.unscaledTime + 0.12f; ShootMoney(); }
            if (clickTools && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && !OverMenu())
            {
                if (Input.GetMouseButtonDown(0)) ClickUnanchor();
                if (Input.GetMouseButtonDown(1)) ClickDestroy();
            }
        }

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        var mv = Game.Movement;
        if (mv == null) { flying = false; return; }
        if (!allowed) return;

        // fly (the game's own noclip) moves with the speed multiplier
        float speed = flying ? 2f * flySpeed : speedMult;
        float jump = jumpMult;
        if (speed != 1f || jump != 1f || movementTouched)
        {
            if (mv.GetSpeedMultiplier() != speed) mv.SetSpeedMultiplier(speed);
            if (mv.GetJumpMultiplier() != jump) mv.SetJumpMultiplier(jump);
            movementTouched = speed != 1f || jump != 1f;
        }
        if (flying && !mv.IsNoClipEnabled()) mv.SetNoClipEnabled(true);
    }

    // everything back to normal (another player joined)
    void TurnOff()
    {
        try
        {
            var mv = Game.Movement;
            if (mv != null)
            {
                if (flying) mv.SetNoClipEnabled(false);
                mv.SetSpeedMultiplier(1f);
                mv.SetJumpMultiplier(1f);
            }
        }
        catch { }
        flying = false;
        movementTouched = false;
    }

    // ---------------- menu open/close ----------------
    PlayerControllerInputManager lockedInput;
    void SetShow(bool value)
    {
        show = value;
        MenuOpen = value;
        if (!show) { editId = null; rebinding = null; searching = false; }
        try
        {
            if (show)
            {
                var me = Game.Me;
                lockedInput = me != null ? me.GetPlayerControllerInputManager() : null;
                if (lockedInput != null) { lockedInput.DisableGameplayInput(this); lockedInput.ShowCursor(this); }
            }
            else if (lockedInput != null)
            {
                lockedInput.EnableGameplayInput(this);
                lockedInput.HideCursor(this);
                lockedInput = null;
            }
        }
        catch (Exception e) { Plugin.L.LogWarning(e.Message); }
    }

    void LateUpdate()
    {
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
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
        if (searchAllSelected && Pressed(KeyCode.Delete)) { search = ""; searchAllSelected = false; spawnPage = 0; }
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
            spawnPage = 0;
        }
        if (Pressed(KeyCode.Escape)) searching = false;
        if (!searching) searchAllSelected = false;
    }

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
        if (!show) return;
        Ui.Init();
        bool areaOpen = false;
        try
        {
            var rect = new Rect(24, 24, 540, Screen.height - 48);
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
        Ui.Label($"Wobbly Life  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyMoney.Value.ToString(), "+ Money");
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyGun.Value.ToString(), "Money Gun");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!Game.InGame) Ui.Label("Load into the world first.", Ui.Sub);
        else if (!allowed) Ui.Label($"<color=#ff6b6b><b>Cheats are off</b></color> - only when you play alone or split screen on this PC (and you are the host).", Ui.Sub);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }   // two rows of tabs
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; searching = false; bodyScroll = Vector2.zero; }
        }
        GUILayout.EndHorizontal();
        Ui.Space(6);

        bool showStatus = status.Length > 0 && Time.unscaledTime - statusTime < 6f;
        bodyScroll = GUILayout.BeginScrollView(bodyScroll, false, false, GUIStyle.none, ScrollBar(), GUIStyle.none);
        bool locked = tab != Tabs.Length - 1 && (!Game.InGame || !allowed);
        if (locked) GUI.enabled = false;
        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawMoney(); break;
            case 2: DrawSpawn(); break;
            case 3: DrawTools(); break;
            case 4: DrawUnlocks(); break;
            case 5: DrawTeleport(); break;
            case 6: DrawSettings(); break;
        }
        if (locked) GUI.enabled = true;
        Ui.Space(4);
        GUILayout.EndScrollView();
        if (showStatus)
        {
            Ui.Space(6);
            Ui.Label("» " + status, Ui.Status);
        }
    }

    Vector2 bodyScroll;
    GUIStyle scrollBar, scrollThumb;
    Texture2D scrollTex;
    // a slim scrollbar in the theme colour
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

    // ---------------- tabs ----------------
    void DrawPlayer()
    {
        Ui.BeginCard("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump power", Ui.Sub);
        jumpMult = Ui.Chips(jumpMult, JumpPresets, v => $"x{v:0.#}", 4);
        if (Ui.Button("Reset Movement", Ui.Chip)) { speedMult = 1f; jumpMult = 1f; }
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "The game's own noclip - fly through everything");
        bool f = Ui.Toggle(flying, $"Fly  [{Plugin.KeyFly.Value}]", "fly where you look, Jump goes up");
        if (f != flying) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        flySpeed = Ui.Chips(flySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();
    }

    void DrawMoney()
    {
        int money = 0;
        try { var emp = Game.Me?.GetPlayerControllerEmployment(); if (emp != null) money = emp.GetLocalMoney(); } catch { }
        Ui.BeginCard("Money", Game.InGame ? $"$ {money:N0}" : null);
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + Short(v), 6);
        NumField("money", "Custom amount", moneyAmount, 1, 1_000_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${Short(moneyAmount)}  [{Plugin.KeyMoney.Value}]", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${Short(moneyAmount)}", Ui.Tab)) AddMoney(-moneyAmount);
        GUILayout.EndHorizontal();
        Ui.EndCard();
    }

    void DrawSpawn()
    {
        if (!catalogLoaded && !catalogLoading && Game.InGame) StartCoroutine(LoadCatalog());
        if (catalogLoading) { Ui.Label($"Loading the shop catalogs... ({spawnables.Count})", Ui.Sub); }

        Ui.BeginCard("Money Bags", "Real money bags - pick them up and cash them in like normal ones");
        Ui.Label("Money in each bag", Ui.Sub);
        bagAmount = (int)Ui.Chips((long)bagAmount, BagPresets, v => "$" + Short(v), 4);
        NumField("bag", "Custom amount", bagAmount, 1, 100_000_000, v => bagAmount = (int)v);
        GUILayout.BeginHorizontal();
        if (Ui.Button("Spawn 1 Bag", Ui.Primary)) SpawnBags(1);
        if (Ui.Button("Spawn 10 Bags", Ui.Chip)) SpawnBags(10);
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Money Gun", $"Hold {Plugin.KeyGun.Value} to shoot money bags where you look");
        moneyGun = Ui.Toggle(moneyGun, "Money Gun", $"hold {Plugin.KeyGun.Value} - each bag holds ${bagAmount:N0}");
        Ui.EndCard();

        if (!valuablesScanned && Game.InGame) ScanValuables();
        Ui.BeginCard("Treasure & Valuables", valuables.Count > 0 ? $"{valuables.Count} kinds - click one to spawn it" : "Nothing found");
        foreach (var v in valuables)
            if (Ui.Button($"<b>{v.Name}</b>", Ui.Row)) SpawnAddress(v.Address, v.Name);
        Ui.EndCard();

        Ui.BeginCard("Spawn Settings", "Things appear in front of you. Vehicles become your vehicle.");
        Ui.Label("Amount", Ui.Sub);
        spawnAmount = Ui.Chips(spawnAmount, AmountPresets, v => v + "x", 4);
        Ui.EndCard();

        Ui.BeginCard("Browse", catalogLoaded ? $"{spawnables.Count} things" : null);
        const int CatsPerPage = 8;
        int catPages = Math.Max(1, (spawnCategories.Length + CatsPerPage - 1) / CatsPerPage);
        catPage = Mathf.Clamp(catPage, 0, catPages - 1);
        var shownCats = spawnCategories.Skip(catPage * CatsPerPage).Take(CatsPerPage).ToArray();
        string cat = Ui.Chips(spawnCategory, shownCats, c => c, 4);
        if (cat != spawnCategory) { spawnCategory = cat; spawnPage = 0; }
        if (catPages > 1)
        {
            GUILayout.BeginHorizontal();
            if (Ui.Button("◀", Ui.Chip, Ui.W(50))) catPage--;
            Ui.Label($"Categories {catPage + 1} / {catPages}", Ui.Sub);
            if (Ui.Button("▶", Ui.Chip, Ui.W(50))) catPage++;
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        Ui.Label("Search", Ui.Sub, Ui.W(60));
        if (!searching) searchAllSelected = false;
        string searchShown = searchAllSelected ? $"<color={Ui.AHex}>{search}</color>" : search;
        if (Ui.Button(searchShown, searching ? Ui.FieldOn : Ui.Field)) { searching = true; searchFieldClickFrame = Time.frameCount; editId = null; }
        if (search.Length > 0 && Ui.Button("✕", Ui.Chip, Ui.W(36))) { search = ""; spawnPage = 0; }
        GUILayout.EndHorizontal();

        var filtered = spawnables.Where(e => (spawnCategory == "All" || e.Category == spawnCategory)
                                           && (search.Length == 0 || e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        int pages = Math.Max(1, (filtered.Count + PageSize - 1) / PageSize);
        spawnPage = Mathf.Clamp(spawnPage, 0, pages - 1);
        Ui.Label($"{filtered.Count} found", Ui.Sub);
        foreach (var e in filtered.Skip(spawnPage * PageSize).Take(PageSize))
        {
            string extra = e.Cost > 0 ? $"{e.Category}  ·  ${e.Cost:N0}" : e.Category;
            if (Ui.Button($"{e.Name}   <size=12><color=#b8c2d6>{extra}</color></size>", selectedSpawn == e ? Ui.RowOn : Ui.Row)) { selectedSpawn = e; searching = false; }
        }
        if (pages > 1)
        {
            GUILayout.BeginHorizontal();
            if (Ui.Button("◀", Ui.Chip, Ui.W(50))) spawnPage--;
            Ui.Label($"Page {spawnPage + 1} / {pages}", Ui.Sub);
            if (Ui.Button("▶", Ui.Chip, Ui.W(50))) spawnPage++;
            GUILayout.EndHorizontal();
        }
        if (catalogLoaded && Ui.Button("Reload List", Ui.Chip)) { catalogLoaded = false; }
        Ui.EndCard();

        string btn = selectedSpawn != null ? $"SPAWN {(selectedSpawn.Vehicle != null ? 1 : spawnAmount)}x {selectedSpawn.Name}" : "Select something above";
        if (Ui.Button(btn, Ui.Primary) && selectedSpawn != null) Spawn(selectedSpawn, selectedSpawn.Vehicle != null ? 1 : spawnAmount);
    }

    void DrawTools()
    {
        Ui.BeginCard("Click Tools", "Aim (or point with the mouse while the menu is open)");
        clickTools = Ui.Toggle(clickTools, "Ctrl + Click", "Ctrl+Left: unanchor / push   Ctrl+Right: destroy");
        bigObjects = Ui.Toggle(bigObjects, "Big objects too", "walls, floors, houses - careful");
        Ui.Label("Push strength", Ui.Sub);
        pushForce = Ui.Chips(pushForce, PushPresets, v => $"{v:0}", 4);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"Put All Back ({changes.Count})", Ui.Primary)) UndoAll();
        GUILayout.EndHorizontal();
        Ui.Label("Destroyed shop items and other moving objects can't come back. Map pieces can - until you reload.", Ui.Sub);
        Ui.EndCard();
    }

    void DrawUnlocks()
    {
        Ui.BeginCard("Unlock Everything", "Saved in your game like normal unlocks");
        if (Ui.Button("Unlock Everything", Ui.Primary)) StartCoroutine(UnlockAll(true, true, true));
        Ui.EndCard();

        Ui.BeginCard("One By One");
        if (Ui.Button("<b>All Clothes</b>   <size=12><color=#b8c2d6>every outfit, hat and accessory</color></size>", Ui.Row)) StartCoroutine(UnlockAll(true, false, false));
        if (Ui.Button("<b>All Vehicles</b>   <size=12><color=#b8c2d6>spawn them at any phone box</color></size>", Ui.Row)) StartCoroutine(UnlockAll(false, true, false));
        if (Ui.Button("<b>All Pets</b>", Ui.Row)) StartCoroutine(UnlockAll(false, false, true));
        Ui.EndCard();
    }

    void DrawTeleport()
    {
        if (Time.unscaledTime - placesScannedAt > 15f) ScanPlaces();

        Ui.BeginCard("Vehicle");
        if (Ui.Button("<b>Bring My Vehicle</b>   <size=12><color=#b8c2d6>the one you got from a phone box</color></size>", Ui.Row)) BringVehicle();
        Ui.EndCard();

        Ui.BeginCard("Position");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Save Position", Ui.Chip)) SavePos();
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places" : null);
        if (places.Count == 0) Ui.Label("Nothing found yet.", Ui.Sub);
        int pages = Math.Max(1, (places.Count + PageSize - 1) / PageSize);
        placePage = Mathf.Clamp(placePage, 0, pages - 1);
        foreach (var pl in places.Skip(placePage * PageSize).Take(PageSize))
            if (Ui.Button($"<b>{pl.Name}</b>" + (pl.Group != null ? $"   <size=12><color=#b8c2d6>{pl.Group}</color></size>" : ""), Ui.Row)) Go(pl);
        if (pages > 1)
        {
            GUILayout.BeginHorizontal();
            if (Ui.Button("◀", Ui.Chip, Ui.W(50))) placePage--;
            Ui.Label($"Page {placePage + 1} / {pages}", Ui.Sub);
            if (Ui.Button("▶", Ui.Chip, Ui.W(50))) placePage++;
            GUILayout.EndHorizontal();
        }
        Ui.EndCard();
    }

    void DrawSettings()
    {
        Ui.BeginCard("Theme");
        string th = Ui.Chips(Ui.CurrentTheme, Ui.ThemeNames, n => n, 3);
        if (th != Ui.CurrentTheme) { Ui.SetTheme(th); Plugin.Theme.Value = th; scrollBar = null; SetStatus($"Theme: {th}"); }
        Ui.EndCard();

        Ui.BeginCard("Keybinds", "Click a key, then press the new key (Esc = cancel)");
        KeyRow("Open menu", Plugin.KeyMenu);
        KeyRow("Add money", Plugin.KeyMoney);
        KeyRow("Fly mode", Plugin.KeyFly);
        KeyRow("Money gun (hold)", Plugin.KeyGun);
        Ui.EndCard();

        Ui.BeginCard("Save", "Saves your movement settings and money amount for next time");
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

    // ---------------- actions ----------------
    bool Check()
    {
        if (!Game.InGame) { SetStatus("Load into the world first"); return false; }
        if (!allowed) { SetStatus("Cheats are off while other players are here"); return false; }
        return true;
    }

    void AddMoney(long amount)
    {
        if (!Check()) return;
        var emp = Game.Me.GetPlayerControllerEmployment();
        if (emp == null) { SetStatus("Money not found"); return; }
        emp.UpdateMoney((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, amount)));
        SetStatus($"{(amount >= 0 ? "+" : "−")}$ {Math.Abs(amount):N0}");
    }

    void SetFly(bool on)
    {
        if (!Check()) return;
        var mv = Game.Movement;
        if (mv == null) { SetStatus("No character"); return; }
        mv.SetNoClipEnabled(on);
        if (!on) mv.SetSpeedMultiplier(speedMult);
        flying = on;
        SetStatus(on ? "Fly mode on - fly where you look, Jump goes up" : "Fly mode off");
    }

    IEnumerator UnlockAll(bool clothes, bool vehicles, bool pets)
    {
        if (!Check()) yield break;
        var me = Game.Me;
        var un = me.GetPlayerControllerUnlocker();
        if (un == null) { SetStatus("Unlocks not found"); yield break; }
        int c = 0, v = 0, p = 0;

        if (clothes && UnitySingleton<ClothingManager>.InstanceExists)
        {
            var all = UnitySingleton<ClothingManager>.Instance.GetAllClothingReferences();
            if (all != null)
                foreach (var r in all)
                {
                    if (r == null) continue;
                    try { un.UnlockClothing(this, r); c++; } catch (Exception e) { Plugin.L.LogWarning(e.Message); }
                    if (c % 10 == 0) yield return null;   // the game loads each piece - don't do all at once
                }
        }
        if (vehicles && UnitySingleton<VehicleManager>.InstanceExists)
        {
            var groups = Traverse.Create(UnitySingleton<VehicleManager>.Instance).Field("allVehiclesAssetReferences").GetValue<List<AllVehiclesAssetReferences>>();
            if (groups != null)
                foreach (var g in groups)
                {
                    var refs = g != null ? g.GetAllVehicleReferences() : null;
                    if (refs == null) continue;
                    foreach (var r in refs)
                    {
                        if (r == null || un.IsVehicleUnlocked(r.vehicleGuid)) continue;
                        try { un.UnlockVehicle(r); v++; } catch (Exception e) { Plugin.L.LogWarning(e.Message); }
                        if (v % 5 == 0) yield return null;
                    }
                }
        }
        if (pets && UnitySingleton<PetManager>.InstanceExists)
        {
            var all = UnitySingleton<PetManager>.Instance.GetAllPets();
            if (all != null)
                foreach (var r in all)
                {
                    if (r == null || r.prefab == null || un.IsPetUnlocked(r)) continue;
                    try { un.UnlockPet(r.prefab); p++; } catch (Exception e) { Plugin.L.LogWarning(e.Message); }
                }
        }
        var parts = new List<string>();
        if (clothes) parts.Add($"{c} clothes");
        if (vehicles) parts.Add($"{v} vehicles");
        if (pets) parts.Add($"{p} pets");
        SetStatus("Unlocked " + string.Join(", ", parts));
    }

    // ---------------- spawn ----------------
    static string FileName(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        key = key.Replace('\\', '/');
        int slash = key.LastIndexOf('/');
        if (slash >= 0) key = key.Substring(slash + 1);
        int dot = key.LastIndexOf('.');
        if (dot > 0) key = key.Substring(0, dot);
        return Pretty(key.Replace("-", " "));
    }

    // the asset's file name without loading the asset itself
    static IEnumerator AssetName(AssetReference r, Action<string> done)
    {
        string name = null;
        AsyncOperationHandle<IList<IResourceLocation>> h = default;
        bool ok = false;
        try { h = Addressables.LoadResourceLocationsAsync(r.RuntimeKey, typeof(GameObject)); ok = true; } catch { }
        if (ok)
        {
            while (!h.IsDone) yield return null;
            try { if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null && h.Result.Count > 0) name = FileName(h.Result[0].PrimaryKey); } catch { }
            try { Addressables.Release(h); } catch { }
        }
        done(name);
    }

    IEnumerator LoadCatalog()
    {
        catalogLoading = true;
        spawnables.Clear();
        var seen = new HashSet<string>();

        // everything the prop shops sell
        var catalogs = new HashSet<ShopPropCatalog>();
        foreach (var c in Resources.FindObjectsOfTypeAll<ShopPropCatalog>()) if (c != null) catalogs.Add(c);
        foreach (var shop in Resources.FindObjectsOfTypeAll<PropShop>())
        {
            ShopPropCatalog c = null;
            try { c = shop != null ? shop.GetPropCatalog() : null; } catch { }
            if (c != null) catalogs.Add(c);
        }
        foreach (var catalog in catalogs)
        {
            var refs = catalog.GetPropItems();
            if (refs == null) continue;
            string category = Pretty(catalog.name.Replace("ShopPropCatalog", "").Replace("PropCatalog", "").Replace("Catalog", ""));
            if (category.Length == 0) category = "Shop";
            foreach (var r in refs)
            {
                if (r == null || !r.RuntimeKeyIsValid() || !seen.Add(r.AssetGUID)) continue;
                AsyncOperationHandle<ShopPropItem> h = default;
                bool ok = false;
                try { h = Addressables.LoadAssetAsync<ShopPropItem>(r); ok = true; } catch { }
                if (!ok) continue;
                while (!h.IsDone) yield return null;
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null || h.Result.propPrefabRef == null) continue;
                var item = h.Result;
                string name = null;
                try { if (item.propTitleLocalized != null && !item.propTitleLocalized.IsEmpty) name = item.propTitleLocalized.GetLocalizedString(); } catch { }
                if (string.IsNullOrEmpty(name)) name = Pretty(item.name);
                spawnables.Add(new SpawnEntry { Name = name, Category = category, Ref = item.propPrefabRef, Cost = item.cost });
            }
        }

        // things dispensers in the world hand out
        foreach (var d in Resources.FindObjectsOfTypeAll<PropDispenser>())
        {
            var go = d != null ? Traverse.Create(d).Field("propPrefab").GetValue<GameObject>() : null;
            if (go == null || !seen.Add("go:" + go.name)) continue;
            spawnables.Add(new SpawnEntry { Name = Pretty(go.name), Category = "Other", Go = go });
        }

        // every vehicle in the game
        if (UnitySingleton<VehicleManager>.InstanceExists)
        {
            var groups = Traverse.Create(UnitySingleton<VehicleManager>.Instance).Field("allVehiclesAssetReferences").GetValue<List<AllVehiclesAssetReferences>>();
            int n = 0;
            if (groups != null)
                foreach (var g in groups)
                {
                    var refs = g != null ? g.GetAllVehicleReferences() : null;
                    if (refs == null) continue;
                    foreach (var r in refs)
                    {
                        if (r == null || r.prefab == null || !seen.Add("v:" + r.guidStr)) continue;
                        string name = null;
                        yield return AssetName(r.prefab, x => name = x);
                        n++;
                        spawnables.Add(new SpawnEntry { Name = string.IsNullOrEmpty(name) ? $"Vehicle {n}" : name, Category = "Vehicles", Ref = r.prefab, Vehicle = r });
                    }
                }
        }

        spawnables.Sort((a, b) => a.Category != b.Category ? string.CompareOrdinal(a.Category, b.Category) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        spawnCategories = new[] { "All" }.Concat(spawnables.Select(e => e.Category).Distinct()).ToArray();
        Plugin.L.LogInfo($"Spawn list: {spawnables.Count} entries in {spawnCategories.Length - 1} categories ({catalogs.Count} shop catalogs)");
        catalogLoading = false;
        catalogLoaded = true;
    }

    Vector3 Front(float dist, out Quaternion rot)
    {
        var cam = Camera.main;
        var f = cam != null ? cam.transform.forward : Game.Character.transform.forward;
        f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        rot = Quaternion.LookRotation(-f);
        return Ground(MyPos() + f * dist);
    }

    void Spawn(SpawnEntry e, int amount)
    {
        if (!Check()) return;
        try
        {
            if (e.Vehicle != null)
            {
                var pos = Front(6f, out var vrot);
                var emp = Game.Me.GetPlayerControllerEmployment();
                NetworkPrefab.SpawnNetworkPrefab(e.Ref, x =>
                {
                    var v = x as PlayerVehicle;
                    if (v != null && emp != null) emp.SetPersonalVehicle(v);
                }, pos + Vector3.up * 0.5f, vrot * Quaternion.Euler(0f, 90f, 0f));
                SetStatus($"Spawned {e.Name}");
                return;
            }
            var basePos = Front(2.5f, out var rot);
            for (int i = 0; i < amount; i++)
            {
                var pos = basePos + Vector3.up * (0.4f + i * 0.8f);
                if (e.Go != null) NetworkPrefab.SpawnNetworkPrefab(e.Go, null, pos, rot);
                else NetworkPrefab.SpawnNetworkPrefab(e.Ref, null, pos, rot);
            }
            SetStatus($"Spawned {amount}x {e.Name}");
        }
        catch (Exception ex) { Plugin.L.LogError(ex); SetStatus("Could not spawn - see log"); }
    }

    // ---------------- money bags ----------------
    const string MoneyBagAddress = "Game/Prefabs/Props/MoneyBag.prefab";

    void SpawnBag(int money, Vector3 pos, Quaternion rot, Vector3 velocity)
    {
        Action<HawkNetworking.HawkNetworkBehaviour> done = x =>
        {
            if (x == null) return;
            var bag = x.GetComponent<MoneyBag>();
            if (bag != null) bag.SetMoney(money);
            if (velocity != Vector3.zero)
                foreach (var rb in x.GetComponentsInChildren<Rigidbody>()) rb.velocity = velocity;
        };
        AssetReference over = null;
        if (UnitySingleton<RewardManager>.InstanceExists)
            over = Traverse.Create(UnitySingleton<RewardManager>.Instance).Field("overrideMoneyBagPrefab").GetValue<AssetReference>();
        if (over != null && over.RuntimeKeyIsValid()) NetworkPrefab.SpawnNetworkPrefab(over, done, pos, rot, null, true, true);
        else NetworkPrefab.SpawnNetworkPrefab(MoneyBagAddress, done, pos, rot, null, true, true);
    }

    void SpawnBags(int count)
    {
        if (!Check()) return;
        try
        {
            var basePos = Front(2.5f, out var rot);
            for (int i = 0; i < count; i++) SpawnBag(bagAmount, basePos + Vector3.up * (0.5f + i * 0.7f), rot, Vector3.zero);
            SetStatus($"Spawned {count} money bag{(count > 1 ? "s" : "")} with ${bagAmount:N0}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not spawn - see log"); }
    }

    void ShootMoney()
    {
        var cam = Camera.main;
        if (cam == null) return;
        try { SpawnBag(bagAmount, cam.transform.position + cam.transform.forward * 2f, Quaternion.LookRotation(cam.transform.forward), cam.transform.forward * 25f); }
        catch (Exception e) { Plugin.L.LogError(e); moneyGun = false; SetStatus("Money gun failed - see log"); }
    }

    // treasure, artifacts etc: everything in the game's asset list that sounds valuable
    void ScanValuables()
    {
        valuablesScanned = true;
        valuables.Clear();
        var seen = new HashSet<string>();
        try
        {
            foreach (var loc in Addressables.ResourceLocators)
                foreach (var key in loc.Keys)
                {
                    var k = key as string;
                    if (k == null || !k.StartsWith("Game/Prefabs/") || !k.EndsWith(".prefab")) continue;
                    if (!System.Text.RegularExpressions.Regex.IsMatch(k, "MoneyBag|Treasure|Artifact|Artefact|Gold|Diamond|Gem|Cash|Coin|Jewel|Chest", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                    if (k.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf("Clothing", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string name = FileName(k);
                    if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                    valuables.Add(new Valuable { Name = name, Address = k });
                }
        }
        catch (Exception e) { Plugin.L.LogWarning("valuables: " + e.Message); }
        valuables.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Plugin.L.LogInfo("Valuables: " + string.Join(", ", valuables.Select(v => v.Name)));
    }

    void SpawnAddress(string address, string name)
    {
        if (!Check()) return;
        try
        {
            var pos = Front(2.5f, out var rot) + Vector3.up * 0.6f;
            NetworkPrefab.SpawnNetworkPrefab(address, null, pos, rot, null, true, true);
            SetStatus("Spawned " + name);
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not spawn - see log"); }
    }

    // ---------------- click tools ----------------
    bool OverMenu() => show && new Rect(24, 24, 540, Screen.height - 48).Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

    bool Aim(out RaycastHit hit)
    {
        hit = default;
        var cam = Camera.main;
        if (cam == null) return false;
        var ray = show ? cam.ScreenPointToRay(Input.mousePosition) : new Ray(cam.transform.position, cam.transform.forward);
        var hits = Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
        var me = Game.Character;
        foreach (var h in hits)
        {
            if (me != null && h.collider.transform.IsChildOf(me.transform)) continue;   // not yourself
            hit = h;
            return true;
        }
        return false;
    }

    static bool IsPlayer(Component c) => c.GetComponentInParent<PlayerCharacter>() != null;

    float SizeOf(Collider c) => c.bounds.extents.magnitude;

    void ClickUnanchor()
    {
        if (!Check() || !Aim(out var hit)) return;
        var col = hit.collider;
        if (IsPlayer(col)) { SetStatus("That's a player"); return; }
        var cam = Camera.main;
        var dir = cam != null ? (show ? cam.ScreenPointToRay(Input.mousePosition).direction : cam.transform.forward) : Vector3.forward;

        // already moves: just push it
        var rb = hit.rigidbody;
        if (rb != null)
        {
            if (rb.isKinematic)
            {
                changes.Add(new Changed { Go = rb.gameObject, Pos = rb.position, Rot = rb.rotation });
                rb.isKinematic = false;
            }
            rb.WakeUp();
            rb.AddForce(dir * pushForce + Vector3.up * pushForce * 0.3f, ForceMode.VelocityChange);
            SetStatus("Pushed " + Pretty(rb.gameObject.name));
            return;
        }
        if (col.GetType().Name == "TerrainCollider") { SetStatus("Can't move the ground"); return; }
        if (!bigObjects && SizeOf(col) > 6f) { SetStatus("Too big - turn on 'Big objects too'"); return; }

        var go = col.gameObject;
        var rend = go.GetComponent<MeshRenderer>();
        var mc = col as MeshCollider;
        // parts of the map are often merged into one big mesh: then we build a loose copy from the collision mesh
        if (rend == null || rend.isPartOfStaticBatch)
        {
            if (mc == null || mc.sharedMesh == null) { SetStatus("This one is baked into the map"); return; }
            var clone = new GameObject(go.name + " (loose)");
            clone.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            clone.transform.localScale = go.transform.lossyScale;
            clone.AddComponent<MeshFilter>().sharedMesh = mc.sharedMesh;
            var cr = clone.AddComponent<MeshRenderer>();
            if (rend != null) cr.sharedMaterials = rend.sharedMaterials;
            var cmc = clone.AddComponent<MeshCollider>();
            cmc.sharedMesh = mc.sharedMesh;
            cmc.convex = true;
            var crb = clone.AddComponent<Rigidbody>();
            crb.mass = Mathf.Clamp(SizeOf(col) * 20f, 1f, 500f);
            crb.AddForce(dir * pushForce * 0.3f, ForceMode.VelocityChange);
            if (rend != null) rend.enabled = false;
            col.enabled = false;
            changes.Add(new Changed { Clone = clone, Original = go });
            SetStatus("Unanchored " + Pretty(go.name));
            return;
        }
        var c = new Changed { Go = go, Pos = go.transform.position, Rot = go.transform.rotation };
        if (mc != null && !mc.convex) { mc.convex = true; c.Convexed = mc; }
        c.AddedRb = go.AddComponent<Rigidbody>();
        c.AddedRb.mass = Mathf.Clamp(SizeOf(col) * 20f, 1f, 500f);
        c.AddedRb.AddForce(dir * pushForce * 0.3f, ForceMode.VelocityChange);
        changes.Add(c);
        SetStatus("Unanchored " + Pretty(go.name));
    }

    void ClickDestroy()
    {
        if (!Check() || !Aim(out var hit)) return;
        var col = hit.collider;
        if (IsPlayer(col)) { SetStatus("That's a player"); return; }
        if (col.GetType().Name == "TerrainCollider") { SetStatus("Can't destroy the ground"); return; }

        // moving / bought objects: the game's own vanish
        var net = col.GetComponentInParent<HawkNetworking.HawkNetworkBehaviour>();
        if (net != null && net.GetComponentInParent<PlayerController>() == null)
        {
            var veh = net.GetComponentInParent<PlayerVehicle>();
            if (veh != null) { try { veh.EvacuateAll(); } catch { } }
            string n = Pretty(net.gameObject.name);
            VanishComponent.VanishAndDestroy(net.gameObject);
            SetStatus("Destroyed " + n);
            return;
        }
        if (!bigObjects && SizeOf(col) > 6f) { SetStatus("Too big - turn on 'Big objects too'"); return; }
        // a loose copy we made: remove it and the original stays hidden
        var cl = changes.FirstOrDefault(x => x.Clone == col.gameObject);
        if (cl != null) { Destroy(cl.Clone); changes.Remove(cl); changes.Add(new Changed { Go = cl.Original, WasDisabled = true }); SetStatus("Destroyed"); return; }
        // map pieces: hide them (comes back on reload or with Put All Back)
        var go = col.gameObject;
        go.SetActive(false);
        changes.Add(new Changed { Go = go, WasDisabled = true });
        SetStatus("Destroyed " + Pretty(go.name));
    }

    void UndoAll()
    {
        int n = 0;
        for (int i = changes.Count - 1; i >= 0; i--)
        {
            var c = changes[i];
            try
            {
                if (c.Clone != null || c.Original != null)
                {
                    if (c.Clone != null) Destroy(c.Clone);
                    if (c.Original != null)
                    {
                        var r = c.Original.GetComponent<MeshRenderer>(); if (r != null) r.enabled = true;
                        var col = c.Original.GetComponent<Collider>(); if (col != null) col.enabled = true;
                    }
                }
                else if (c.Go != null)
                {
                    if (c.WasDisabled) { c.Go.SetActive(true); var r = c.Go.GetComponent<MeshRenderer>(); if (r != null) r.enabled = true; var col = c.Go.GetComponent<Collider>(); if (col != null) col.enabled = true; }
                    else
                    {
                        if (c.AddedRb != null) Destroy(c.AddedRb);
                        else { var rb = c.Go.GetComponent<Rigidbody>(); if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; } }
                        c.Go.transform.SetPositionAndRotation(c.Pos, c.Rot);
                        if (c.Convexed != null) c.Convexed.convex = false;
                    }
                }
                n++;
            }
            catch { }
        }
        changes.Clear();
        SetStatus(n > 0 ? $"Put {n} things back" : "Nothing to put back");
    }

    // ---------------- teleport ----------------
    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        bool log = places.Count == 0;
        var list = new List<Place>();
        var seen = new HashSet<string>();
        void Add(string name, Transform t, string group)
        {
            if (t == null || string.IsNullOrEmpty(name)) return;
            name = name.Trim();
            if (name.Length == 0 || !seen.Add(name)) return;
            list.Add(new Place { Name = name, At = t, Group = group });
        }
        // the developers' own teleport points
        foreach (var d in FindObjectsOfType<DeveloperTeleportHandle>().OrderBy(x => x.GetOrder()))
            Add(Pretty(d.GetTitle()), d.transform, null);
        // named icons on the map
        foreach (var icon in FindObjectsOfType<MinimapIcon>())
        {
            string title = null;
            try
            {
                var tr = Traverse.Create(icon);
                var info = tr.Field("overrideInfo").GetValue<MinimapIconMapInfo>();
                if (info == null) { var data = tr.Field("infoData").GetValue<MinimapIconMapInfoData>(); if (data != null) info = data.GetInfoData(); }
                if (info != null) title = info.GetTitle();
            }
            catch { }
            if (!string.IsNullOrEmpty(title)) Add(title, icon.transform, "map");
        }
        places = list;
        if (log && places.Count > 0) Plugin.L.LogInfo("Teleport places: " + string.Join(", ", places.Select(p => p.Name)));
    }

    static string Pretty(string s)
    {
        s = (s ?? "").Replace("_", " ").Trim();
        s = System.Text.RegularExpressions.Regex.Replace(s, @"([a-z])([A-Z])", "$1 $2");
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 1f;
        return p + Vector3.up * 1f;
    }

    void Go(Place pl)
    {
        if (!Check()) return;
        if (pl.At == null) { placesScannedAt = -100f; SetStatus("That place is gone - try again"); return; }
        TeleportTo(Ground(pl.At.position), pl.Name);
    }

    Vector3 MyPos()
    {
        var mv = Game.Movement;
        if (mv != null && mv.GetHipRigidbody() != null) return mv.GetHipRigidbody().position;
        return Game.Character.transform.position;
    }

    void SavePos()
    {
        if (!Check()) return;
        savedPos = MyPos();
        hasSaved = true;
        SetStatus("Position saved");
    }

    void TeleportTo(Vector3 pos, string name)
    {
        if (!Check()) return;
        var mv = Game.Movement;
        bool wasFlying = flying && mv != null && mv.IsNoClipEnabled();
        if (wasFlying) mv.SetNoClipEnabled(false);   // the noclip body would pull us back
        Game.Me.SetPosition(pos);
        if (wasFlying) mv.SetNoClipEnabled(true);
        SetStatus("Teleported to " + name);
    }

    void BringVehicle()
    {
        if (!Check()) return;
        var emp = Game.Me.GetPlayerControllerEmployment();
        var car = emp != null ? emp.GetPersonalVehicle() : null;
        if (car == null) { SetStatus("You have no vehicle yet - get one at a phone box"); return; }
        var cam = Camera.main;
        var f = cam != null ? cam.transform.forward : Game.Character.transform.forward;
        f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        var pos = Ground(MyPos() + f * 6f) + Vector3.up * 0.5f;
        var rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, f));
        foreach (var rb in car.GetComponentsInChildren<Rigidbody>())
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        var main = car.GetComponent<Rigidbody>();
        if (main != null) { main.position = pos; main.rotation = rot; }
        car.transform.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
        SetStatus("Your vehicle is here");
    }
}
