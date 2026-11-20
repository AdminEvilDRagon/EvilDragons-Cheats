using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StreamerLife2Cheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Streamer Life Simulator 2", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.streamerlife2";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<Key> KeyMenu, KeyMoney, KeyFly;
    internal static ConfigEntry<string> Theme;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", Key.F1, "Open/close menu");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", Key.F2, "Add the selected money amount");
        KeyFly = Config.Bind("Hotkeys", "Fly", Key.F3, "Toggle fly mode");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        Ui.InitialTheme = Theme.Value;
        Ui.UseOsFonts = false;   // Unity 2023.2: OS fonts are invisible in IMGUI
        Ui.Log = m => Logger.LogWarning(m);

        var go = new GameObject("EvilDragonCheatMenu");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        Logger.LogInfo($"EvilDragon's Cheats v{Version} loaded");
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
    public static GameManager GM => GameManager.Instance;
    public static PlayerMotor Motor => GM != null ? GM.playerMotor : null;
    public static EFPController.Player Player => EFPController.Player.instance;
    public static bool InGame => GM != null && Motor != null && Player != null;
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Money", "Stream", "Items", "Best", "Teleport", "World", "Settings" };

    // player
    bool fullNeeds, flying;
    float speedMult = 1f, jumpMult = 1f, flySpeed = 2f;
    float origWalk = -1f, origSprint = -1f, origJump = -1f;
    EFPController.PlayerMovement origFor;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };

    // money
    long moneyAmount = 10_000;
    static readonly long[] MoneyPresets = { 1_000, 10_000, 50_000, 100_000, 500_000, 1_000_000 };
    static readonly float[] BtcPresets = { 0.01f, 0.1f, 1f, 10f };
    float btcAmount = 0.1f;

    // stream
    static readonly int[] FollowerPresets = { 100, 1_000, 10_000, 100_000 };
    static readonly int[] SubPresets = { 10, 100, 1_000, 10_000 };
    static readonly int[] ViewerPresets = { 10, 100, 1_000, 5_000 };
    static readonly int[] PointPresets = { 1, 5, 10, 50 };
    int followerAmount = 1_000, subAmount = 100, viewerAmount = 100, pointAmount = 5;

    // world
    bool freezeTime;
    float timeSpeed = 1f, origTimeCounter = -1f;
    GameManager origTimeFor;
    static readonly float[] TimeSpeedPresets = { 0.5f, 1f, 2f, 5f };

    // items
    class ItemEntry { public Item Prefab; public string Name; public string Category; }
    List<ItemEntry> items;
    float itemsScannedAt = -100f;
    string itemCategory = "All", search = "";
    bool searching, toInventory;
    int searchFieldClickFrame = -1, searchCloseCheckFrame = -1;
    bool searchAllSelected;
    int itemPage, itemAmount = 1;
    ItemEntry selectedItem;
    string[] categories = { "All" };
    static readonly int[] AmountPresets = { 1, 3, 5, 10 };
    const int PageSize = 10;
    int catPage;

    // teleport
    class Place { public string Name; public Transform At; public Vector3 Offset; }
    class CarEntry { public string Name; public MyCar Car; }
    List<Place> houses = new List<Place>(), places = new List<Place>();
    List<CarEntry> cars = new List<CarEntry>();
    float placesScannedAt = -100f;
    bool hasSaved;
    Vector3 savedPos;
    int placePage;

    // number input + key rebinding
    string editId, editBuf = "";
    long editMin, editMax;
    Action<long> editSet;
    ConfigEntry<Key> rebinding;

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
        fullNeeds = Saved.Get("AllNeedsFull", false);
        speedMult = Saved.Get("WalkSpeed", 1f);
        jumpMult = Saved.Get("JumpPower", 1f);
        flySpeed = Saved.Get("FlySpeed", 2f);
        moneyAmount = Saved.Get("MoneyAmount", 10_000L);
        freezeTime = Saved.Get("FreezeTime", false);
        timeSpeed = Saved.Get("TimeSpeed", 1f);
        toInventory = Saved.Get("SpawnToInventory", false);
    }

    void SaveSettings()
    {
        Saved.Set("AllNeedsFull", fullNeeds);
        Saved.Set("WalkSpeed", speedMult);
        Saved.Set("JumpPower", jumpMult);
        Saved.Set("FlySpeed", flySpeed);
        Saved.Set("MoneyAmount", moneyAmount);
        Saved.Set("FreezeTime", freezeTime);
        Saved.Set("TimeSpeed", timeSpeed);
        Saved.Set("SpawnToInventory", toInventory);
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
    static bool Held(Key k)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        try { return kb[k].isPressed; } catch { return false; }
    }

    bool textHooked;
    readonly Queue<char> typed = new Queue<char>();
    void HookText()
    {
        if (textHooked || Keyboard.current == null) return;
        Keyboard.current.onTextInput += c => { if (searching || editId != null) typed.Enqueue(c); };
        textHooked = true;
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();
        HookText();

        // leave the search box as soon as you click anywhere else
        if (searchCloseCheckFrame >= 0 && Time.frameCount > searchCloseCheckFrame)
        {
            if (searchFieldClickFrame != searchCloseCheckFrame) searching = false;
            searchCloseCheckFrame = -1;
        }
        var mouse = Mouse.current;
        if (searching && mouse != null && (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame))
            searchCloseCheckFrame = Time.frameCount;

        if (rebinding == null && Pressed(Plugin.KeyMenu.Value)) { searching = false; editId = null; SetShow(!show); }
        else if (rebinding != null) HandleRebind();
        else if (searching) HandleSearchInput();
        else if (editId != null) HandleNumberInput();
        else
        {
            if (Pressed(Plugin.KeyMoney.Value)) AddMoney(moneyAmount);
            if (Pressed(Plugin.KeyFly.Value)) SetFly(!flying);
        }
        if (!searching && editId == null) typed.Clear();

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        if (!Game.InGame) { flying = false; return; }
        var gm = Game.GM;
        var motor = Game.Motor;

        // keep the menu's freeze on while it is open
        if (show && weFroze && !motor.freezed) motor.SetFreezed(true);

        if (fullNeeds) FillNeeds();

        // movement multipliers, always computed from the original values
        var mv = Game.Player.controller;
        if (mv != null)
        {
            if (origFor != mv) { origFor = mv; origWalk = mv.walkSpeed; origSprint = mv.sprintSpeed; origJump = mv.jumpSpeed; }
            float w = origWalk * speedMult, s = origSprint * speedMult, j = origJump * jumpMult;
            if (mv.walkSpeed != w) mv.walkSpeed = w;
            if (mv.sprintSpeed != s) mv.sprintSpeed = s;
            if (mv.jumpSpeed != j) mv.jumpSpeed = j;
        }

        if (flying) FlyMove();

        if (origTimeFor != gm) { origTimeFor = gm; origTimeCounter = gm.timeCounter; }
        float want = freezeTime ? 0f : origTimeCounter * timeSpeed;
        if (gm.timeCounter != want) gm.timeCounter = want;
    }

    static void FillNeeds()
    {
        var gm = Game.GM;
        var m = Game.Motor;
        if (gm == null || m == null) return;
        m.energy = 100f;
        m.stress = 0f;
        gm.hungerTime = 0;
        gm.toiletTime = 0;
        gm.awekingTime = 0;
        gm.notShowerTime = 0;
        gm.playerGetSick = false;
    }

    // ---------------- menu open/close ----------------
    bool weFroze;
    void SetShow(bool value)
    {
        show = value;
        if (!show) { editId = null; rebinding = null; searching = false; }
        var m = Game.Motor;
        if (m == null || Game.Player == null) { weFroze = false; return; }
        if (show)
        {
            if (!m.freezed) { weFroze = true; m.SetFreezed(true); }
        }
        else
        {
            if (weFroze)
            {
                m.SetFreezed(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            weFroze = false;
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

    void HandleSearchInput()
    {
        // Ctrl+A selects the whole search text: the next key replaces it, Backspace/Delete clears it
        bool ctrl = Held(Key.LeftCtrl) || Held(Key.RightCtrl);
        if (ctrl && Pressed(Key.A)) searchAllSelected = search.Length > 0;
        if (searchAllSelected && Pressed(Key.Delete)) { search = ""; searchAllSelected = false; itemPage = 0; }

        while (typed.Count > 0)
        {
            char c = typed.Dequeue();
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
            itemPage = 0;
        }
        if (Pressed(Key.Enter) || Pressed(Key.NumpadEnter) || Pressed(Key.Escape)) searching = false;
        if (!searching) searchAllSelected = false;
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
        Ui.Label($"Streamer Life Simulator 2  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyMoney.Value.ToString(), "+ Money");
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!Game.InGame) Ui.Label("Load into a game first.", Ui.Sub);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }   // two rows of tabs
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; searching = false; bodyScroll = Vector2.zero; }
        }
        GUILayout.EndHorizontal();
        Ui.Space(6);

        // the tab content scrolls when it is taller than the window; the status line stays at the bottom
        bool showStatus = status.Length > 0 && Time.unscaledTime - statusTime < 6f;
        bodyScroll = GUILayout.BeginScrollView(bodyScroll, false, false, GUIStyle.none, ScrollBar(), GUIStyle.none);
        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawMoney(); break;
            case 2: DrawStream(); break;
            case 3: DrawItems(); break;
            case 4: DrawBest(); break;
            case 5: DrawTeleport(); break;
            case 6: DrawWorld(); break;
            case 7: DrawSettings(); break;
        }
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
        var m = Game.Motor;
        Ui.BeginCard("Needs", m != null ? $"Energy {m.energy:0} · Stress {m.stress:0}" : null);
        fullNeeds = Ui.Toggle(fullNeeds, "All Needs Always Full", "never hungry, tired, stressed, dirty or sick");
        if (Ui.Button("Fill All Needs Now", Ui.Primary)) { if (Check()) { FillNeeds(); SetStatus("All needs filled"); } }
        Ui.EndCard();

        Ui.BeginCard("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump power", Ui.Sub);
        jumpMult = Ui.Chips(jumpMult, JumpPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly freely around the map, through walls");
        bool f = Ui.Toggle(flying, $"Fly  [{Plugin.KeyFly.Value}]", "WASD, Space up, Ctrl down, Shift fast");
        if (f != flying) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        flySpeed = Ui.Chips(flySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();
    }

    void DrawMoney()
    {
        var gm = Game.GM;
        Ui.BeginCard("Money", gm != null ? $"$ {gm.money:N0}" : null);
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + Short(v), 6);
        NumField("money", "Custom amount", moneyAmount, 1, 1_000_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${Short(moneyAmount)}  [{Plugin.KeyMoney.Value}]", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${Short(moneyAmount)}", Ui.Tab)) AddMoney(-moneyAmount);
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Bitcoin", gm != null ? $"{gm.ownedBitcoin:0.#####} BTC" : null);
        btcAmount = Ui.Chips(btcAmount, BtcPresets, v => $"{v:0.##} BTC", 4);
        if (Ui.Button($"+ {btcAmount:0.##} BTC", Ui.Primary)) { if (Check()) { gm.ownedBitcoin += btcAmount; SetStatus($"+{btcAmount:0.##} BTC"); } }
        Ui.EndCard();
    }

    void DrawStream()
    {
        var gm = Game.GM;
        Ui.BeginCard("Channel", gm != null ? $"{gm.totalFollowers:N0} followers  ·  {gm.totalSubs:N0} subs" : null);
        Ui.Label("Followers", Ui.Sub);
        followerAmount = Ui.Chips(followerAmount, FollowerPresets, v => Short(v), 4);
        if (Ui.Button($"+ {Short(followerAmount)} Followers", Ui.Primary)) AddFollowers(followerAmount, 0);
        Ui.Label("Subscribers", Ui.Sub);
        subAmount = Ui.Chips(subAmount, SubPresets, v => Short(v), 4);
        if (Ui.Button($"+ {Short(subAmount)} Subs", Ui.Primary)) AddFollowers(0, subAmount);
        Ui.EndCard();

        Ui.BeginCard("Live Stream", gm != null ? $"{gm.currentViewer:N0} viewers watching right now" : null);
        viewerAmount = Ui.Chips(viewerAmount, ViewerPresets, v => Short(v), 4);
        if (Ui.Button($"+ {Short(viewerAmount)} Live Viewers", Ui.Primary)) AddViewers(viewerAmount);
        Ui.Label("Works best while you are streaming.", Ui.Sub);
        Ui.EndCard();

        var sk = gm != null ? gm.skillManager : null;
        Ui.BeginCard("Skill Points", sk != null ? $"{sk.currentSkillPoint} skill points" : null);
        pointAmount = Ui.Chips(pointAmount, PointPresets, v => "+" + v, 4);
        if (Ui.Button($"+ {pointAmount} Skill Points", Ui.Primary)) AddPoints(pointAmount);
        Ui.EndCard();
    }

    void DrawWorld()
    {
        var gm = Game.GM;
        Ui.BeginCard("Time", gm != null ? $"Day {gm.day}  ·  {(int)gm.hour:00}:{(int)gm.minute:00}" : null);
        freezeTime = Ui.Toggle(freezeTime, "Freeze Time", "the clock stops");
        Ui.Label("Day speed", Ui.Sub);
        timeSpeed = Ui.Chips(timeSpeed, TimeSpeedPresets, v => $"x{v:0.#}", 4);
        GUILayout.BeginHorizontal();
        if (Ui.Button("Morning", Ui.Chip)) SetTime(8f);
        if (Ui.Button("Noon", Ui.Chip)) SetTime(12f);
        if (Ui.Button("Evening", Ui.Chip)) SetTime(18f);
        if (Ui.Button("Night", Ui.Chip)) SetTime(22f);
        GUILayout.EndHorizontal();
        Ui.EndCard();
    }

    void DrawItems()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }
        if (items == null || items.Count == 0 || Time.unscaledTime - itemsScannedAt > 30f) ScanItems();

        Ui.BeginCard("Spawn Settings");
        toInventory = Ui.Chips(toInventory, new[] { false, true }, v => v ? "Into Inventory" : "In Front of Me", 2);
        Ui.Label("Amount", Ui.Sub);
        itemAmount = Ui.Chips(itemAmount, AmountPresets, v => v + "x", 4);
        Ui.EndCard();

        Ui.BeginCard("Browse Items");
        // categories: 8 per page so the chips stay readable
        const int CatsPerPage = 8;
        int catPages = Math.Max(1, (categories.Length + CatsPerPage - 1) / CatsPerPage);
        catPage = Mathf.Clamp(catPage, 0, catPages - 1);
        var shownCats = categories.Skip(catPage * CatsPerPage).Take(CatsPerPage).ToArray();
        string cat = Ui.Chips(itemCategory, shownCats, c => c, 4);
        if (cat != itemCategory) { itemCategory = cat; itemPage = 0; }
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
        string searchShown = searchAllSelected ? $"<color={Ui.AHex}>{search}</color>" : search;   // highlighted = selected with Ctrl+A
        if (Ui.Button(searchShown, searching ? Ui.FieldOn : Ui.Field)) { searching = true; searchFieldClickFrame = Time.frameCount; editId = null; }
        if (search.Length > 0 && Ui.Button("✕", Ui.Chip, Ui.W(36))) { search = ""; itemPage = 0; }
        GUILayout.EndHorizontal();

        var filtered = items.Where(e => (itemCategory == "All" || e.Category == itemCategory)
                                        && (search.Length == 0 || e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        int pages = Math.Max(1, (filtered.Count + PageSize - 1) / PageSize);
        itemPage = Mathf.Clamp(itemPage, 0, pages - 1);

        Ui.Label($"{filtered.Count} items", Ui.Sub);
        foreach (var e in filtered.Skip(itemPage * PageSize).Take(PageSize))
        {
            string label = $"{e.Name}   <size=12><color=#b8c2d6>{e.Category}</color></size>";
            if (Ui.Button(label, selectedItem == e ? Ui.RowOn : Ui.Row)) { selectedItem = e; searching = false; }
        }
        if (pages > 1)
        {
            GUILayout.BeginHorizontal();
            if (Ui.Button("◀", Ui.Chip, Ui.W(50))) itemPage--;
            Ui.Label($"Page {itemPage + 1} / {pages}", Ui.Sub);
            if (Ui.Button("▶", Ui.Chip, Ui.W(50))) itemPage++;
            GUILayout.EndHorizontal();
        }
        Ui.EndCard();

        string btn = selectedItem != null ? $"SPAWN {itemAmount}x {selectedItem.Name}" : "Select an item above";
        if (Ui.Button(btn, Ui.Primary) && selectedItem != null) SpawnItem(selectedItem, itemAmount);
    }

    void DrawBest()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }

        Ui.BeginCard("Best Of Everything", "The best (highest quality / most expensive) item of each kind");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Best Of Everything", Ui.Primary)) SpawnBestAll();
        if (Ui.Button("Best PC", Ui.Primary)) SpawnBestPc();
        GUILayout.EndHorizontal();
        Ui.Label("Best PC = the best case and the best part for every slot inside it.", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Best House", BestHouseInfo());
        GUILayout.BeginHorizontal();
        if (Ui.Button("Best House", Ui.Primary)) GetHouses(true);
        if (Ui.Button("All Houses", Ui.Chip)) GetHouses(false);
        GUILayout.EndHorizontal();
        Ui.EndCard();

        var kinds = BestKinds();
        Ui.BeginCard("Best Of One Kind", $"{kinds.Count} kinds - click one to spawn it");
        foreach (var kv in kinds)
        {
            string label = $"<b>{kv.Key}</b>   <size=12><color=#b8c2d6>{NameOf(kv.Value)}  ·  ${kv.Value.myItem.itemPrice:N0}</color></size>";
            if (Ui.Button(label, Ui.Row))
                SpawnParts(new List<Item> { kv.Value }, "the best " + kv.Key + ": " + NameOf(kv.Value));
        }
        Ui.EndCard();
    }

    void DrawTeleport()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }
        if (Time.unscaledTime - placesScannedAt > 15f) ScanPlaces();

        Ui.BeginCard("Cars", "Your car drives up in front of you");
        if (cars.Count == 0) Ui.Label("No car found.", Ui.Sub);
        foreach (var c in cars)
            if (Ui.Button($"<b>{c.Name}</b>   <size=12><color=#b8c2d6>bring it here</color></size>", Ui.Row)) BringCar(c);
        Ui.EndCard();

        Ui.BeginCard("Position");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Save Position", Ui.Chip)) { savedPos = Game.Player.transform.position; hasSaved = true; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Houses");
        if (houses.Count == 0) Ui.Label("No house found.", Ui.Sub);
        foreach (var h in houses)
            if (Ui.Button($"<b>{h.Name}</b>", Ui.Row)) Go(h);
        Ui.EndCard();

        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places" : null);
        if (places.Count == 0) Ui.Label("Nothing found yet.", Ui.Sub);
        int pages = Math.Max(1, (places.Count + PageSize - 1) / PageSize);
        placePage = Mathf.Clamp(placePage, 0, pages - 1);
        foreach (var pl in places.Skip(placePage * PageSize).Take(PageSize))
            if (Ui.Button(pl.Name, Ui.Row)) Go(pl);
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
        Ui.EndCard();

        Ui.BeginCard("Save", "Saves all toggles so they are active the next time you play");
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
        if (Game.InGame) return true;
        SetStatus("Load into a game first");
        return false;
    }

    void AddMoney(long amount)
    {
        if (!Check()) return;
        var gm = Game.GM;
        if (amount >= 0) gm.AddMoney(amount);
        else gm.money = Mathf.Max(0f, gm.money + amount);
        SetStatus($"{(amount >= 0 ? "+" : "−")}$ {Math.Abs(amount):N0}");
    }

    void AddFollowers(int followers, int subs)
    {
        if (!Check()) return;
        var gm = Game.GM;
        gm.totalFollowers += followers;
        gm.totalSubs += subs;
        SetStatus(followers > 0 ? $"+{followers:N0} followers" : $"+{subs:N0} subscribers");
    }

    void AddViewers(int n)
    {
        if (!Check()) return;
        Game.GM.currentViewer += n;
        SetStatus($"+{n:N0} live viewers");
    }

    void AddPoints(int n)
    {
        if (!Check()) return;
        var sk = Game.GM.skillManager;
        if (sk == null) { SetStatus("Skills not found"); return; }
        sk.currentSkillPoint += n;
        SetStatus($"+{n} skill points");
    }

    void SetTime(float hour)
    {
        if (!Check()) return;
        Game.GM.hour = hour;
        Game.GM.minute = 0f;
        SetStatus($"Time set to {hour:00}:00");
    }

    // fly mode: switch off the movement controller and gravity, move the player ourselves
    void SetFly(bool on)
    {
        if (!Check()) return;
        var p = Game.Player;
        var rb = p.rigidbody;
        if (p.controller != null) p.controller.enabled = !on;
        if (rb != null) { rb.isKinematic = on; if (!on) rb.velocity = Vector3.zero; }
        flying = on;
        SetStatus(on ? "Fly mode on" : "Fly mode off");
    }

    void FlyMove()
    {
        var p = Game.Player;
        if (p.controller != null && p.controller.enabled) p.controller.enabled = false;
        if (p.rigidbody != null && !p.rigidbody.isKinematic) p.rigidbody.isKinematic = true;
        if (show || Game.Motor.freezed) return;
        var cam = p.playerCamera != null ? p.playerCamera : Camera.main;
        Vector3 fwd = cam != null ? cam.transform.forward : p.transform.forward;
        Vector3 right = cam != null ? cam.transform.right : p.transform.right;
        Vector3 dir = Vector3.zero;
        if (Held(Key.W)) dir += fwd;
        if (Held(Key.S)) dir -= fwd;
        if (Held(Key.D)) dir += right;
        if (Held(Key.A)) dir -= right;
        if (Held(Key.Space)) dir += Vector3.up;
        if (Held(Key.LeftCtrl) || Held(Key.C)) dir -= Vector3.up;
        if (dir.sqrMagnitude < 0.01f) return;
        float speed = 6f * flySpeed * (Held(Key.LeftShift) ? 2.5f : 1f);
        p.transform.position += dir.normalized * speed * Time.deltaTime;
    }

    // ---------------- items ----------------
    static string NameOf(Item it) => it.myItem == null ? it.name : (string.IsNullOrEmpty(it.myItem.itemShowedName) ? it.myItem.itemTag : it.myItem.itemShowedName).Trim();

    static string Pretty(string s)
    {
        s = (s ?? "").Replace("_", " ").Trim();
        return s.Length == 0 ? "Other" : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    static string KindOf(Item it) => Pretty(it.myItem.itemGroup);

    static IEnumerable<Item> AllItems()
    {
        var gm = Game.GM;
        if (gm == null || gm.allItems == null) return Enumerable.Empty<Item>();
        return gm.allItems.Where(i => i != null && i.myItem != null && !string.IsNullOrEmpty(i.myItem.itemTag))
                          .GroupBy(i => i.myItem.itemTag).Select(g => g.First());
    }

    void ScanItems()
    {
        itemsScannedAt = Time.unscaledTime;
        items = AllItems().Select(i => new ItemEntry { Prefab = i, Name = NameOf(i), Category = KindOf(i) })
                          .OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
        categories = new[] { "All" }.Concat(items.Select(e => e.Category).Distinct()).ToArray();
    }

    Vector3 Forward(out Transform t)
    {
        var p = Game.Player;
        t = p.transform;
        var cam = p.playerCamera != null ? p.playerCamera : Camera.main;
        var f = cam != null ? cam.transform.forward : t.forward;
        f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        return f.normalized;
    }

    void SpawnItem(ItemEntry e, int amount)
    {
        if (!Check()) return;
        try
        {
            if (toInventory)
            {
                var inv = Game.GM.UIManager != null ? Game.GM.UIManager.inventory : null;
                if (inv == null) { SetStatus("Inventory not found"); return; }
                for (int n = 0; n < amount; n++) inv.AddItemToInventory(e.Prefab.myItem);
                SetStatus($"{amount}x {e.Name} in your inventory");
                return;
            }
            var list = new List<Item>();
            for (int n = 0; n < amount; n++) list.Add(e.Prefab);
            LayOut(list, 5, 0.8f);
            SetStatus($"Spawned {amount}x {e.Name}");
        }
        catch (Exception ex) { Plugin.L.LogError(ex); SetStatus("Could not spawn - see log"); }
    }

    // spawn items in rows in front of you
    void LayOut(List<Item> list, int perRow, float gap)
    {
        var f = Forward(out var t);
        var right = Vector3.Cross(Vector3.up, f);
        var rot = Quaternion.Euler(0f, t.eulerAngles.y + 180f, 0f);
        for (int i = 0; i < list.Count; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = Math.Min(perRow, list.Count - row * perRow);
            var pos = t.position + f * (1.4f + row * gap) + right * ((col - (inRow - 1) / 2f) * gap) + Vector3.up * 1.2f;
            var go = Instantiate(list[i].gameObject, pos, rot);
            go.SetActive(true);
        }
    }

    void SpawnParts(List<Item> list, string what)
    {
        if (!Check()) return;
        try { LayOut(list, 5, 1.1f); SetStatus("Spawned " + what); }
        catch (Exception ex) { Plugin.L.LogError(ex); SetStatus("Could not spawn - see log"); }
    }

    // ---------------- best ----------------
    static float Score(Item it) => it.myItem.quality * 1_000_000f + it.myItem.gb * 1_000f + it.myItem.itemPrice;
    static Item Best(IEnumerable<Item> items) => items.OrderByDescending(Score).FirstOrDefault();

    // consumables and junk are left out of "best of everything"
    static readonly HashSet<string> Junk = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Other", "Food", "Drink", "Pill", "Medicine", "Trash", "Prize", "Cloth", "Clothes" };

    List<KeyValuePair<string, Item>> bestKinds;
    float bestKindsAt = -100f;
    List<KeyValuePair<string, Item>> BestKinds()
    {
        if (bestKinds != null && Time.unscaledTime - bestKindsAt < 30f) return bestKinds;
        bestKindsAt = Time.unscaledTime;
        bestKinds = AllItems().GroupBy(KindOf)
            .Select(g => new KeyValuePair<string, Item>(g.Key, Best(g)))
            .Where(kv => kv.Value != null)
            .OrderBy(kv => kv.Key).ToList();
        Plugin.L.LogInfo("Best per kind: " + string.Join(", ", bestKinds.Select(kv => $"{kv.Key}={NameOf(kv.Value)} (q{kv.Value.myItem.quality}, ${kv.Value.myItem.itemPrice})")));
        return bestKinds;
    }

    // the slots inside a PC case: slot id = item group, how many of each
    static Dictionary<string, int> CaseSlots()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var inv = Game.GM != null && Game.GM.UIManager != null ? Game.GM.UIManager.inventory : null;
        if (inv == null || inv.allCaseSlots == null) return d;
        foreach (var s in inv.allCaseSlots)
            if (s != null && !string.IsNullOrEmpty(s.slotID)) d[s.slotID] = d.TryGetValue(s.slotID, out int n) ? n + 1 : 1;
        return d;
    }

    List<Item> BestPcList()
    {
        var all = AllItems().ToList();
        var list = new List<Item>();
        var pcCase = Best(all.Where(i => i.GetComponent<PcCase>() != null));
        if (pcCase != null) list.Add(pcCase);
        foreach (var slot in CaseSlots())
        {
            var best = Best(all.Where(i => string.Equals(i.myItem.itemGroup, slot.Key, StringComparison.OrdinalIgnoreCase)));
            for (int n = 0; n < slot.Value && best != null; n++) list.Add(best);
        }
        return list;
    }

    void SpawnBestPc()
    {
        if (!Check()) return;
        var list = BestPcList();
        if (list.Count == 0) { SetStatus("No PC parts found"); return; }
        Plugin.L.LogInfo("Best PC: " + string.Join(", ", list.Select(NameOf)));
        SpawnParts(list, $"the best PC ({list.Count} parts)");
    }

    void SpawnBestAll()
    {
        if (!Check()) return;
        var pc = BestPcList();
        var pcGroups = new HashSet<string>(pc.Select(KindOf));
        var list = new List<Item>(pc);
        foreach (var kv in BestKinds())
            if (!Junk.Contains(kv.Key) && !pcGroups.Contains(kv.Key)) list.Add(kv.Value);
        SpawnParts(list, $"the best of everything ({list.Count} items)");
    }

    // ---------------- houses ----------------
    List<BuyableHouse> houseCache;
    List<BuyableHouse> Houses()
    {
        if (houseCache == null || houseCache.Any(h => h == null))
            houseCache = Resources.FindObjectsOfTypeAll<BuyableHouse>().Where(h => h != null && h.gameObject.scene.IsValid()).ToList();
        return houseCache;
    }

    static float HouseValue(BuyableHouse h) => Math.Max(h.rentFee, h.oneTimeRentFee);

    string BestHouseInfo()
    {
        var hs = Houses();
        if (hs.Count == 0) return null;
        var best = hs.OrderByDescending(HouseValue).First();
        return $"Best: {best.houseNameTxt} (${HouseValue(best):N0})  ·  you have {hs.Count(h => h.buyed)} of {hs.Count}";
    }

    void GetHouses(bool onlyBest)
    {
        if (!Check()) return;
        var hs = Houses();
        if (hs.Count == 0) { SetStatus("No houses found"); return; }
        var targets = onlyBest ? new List<BuyableHouse> { hs.OrderByDescending(HouseValue).First() } : hs;
        int n = 0;
        foreach (var h in targets)
        {
            if (h.buyed) continue;
            try { h.RentMe(); h.Save(); n++; }
            catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        }
        SetStatus(n == 0 ? "You already have " + (onlyBest ? "the best house" : "all houses")
                 : onlyBest ? $"{targets[0].houseNameTxt} is yours" : $"{n} houses are yours now");
    }

    // ---------------- teleport ----------------
    static bool OnUi(Component c) => c.GetComponent<RectTransform>() != null || c.GetComponentInParent<Canvas>() != null;

    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        bool log = places.Count == 0;
        var gm = Game.GM;

        // houses: the light switch inside every house
        houses = new List<Place>();
        var switches = new[] { gm.house1lightSwitch, gm.house2LightSwitch, gm.house3LightSwitch, gm.house4LightSwitch };
        for (int i = 0; i < switches.Length; i++)
            if (switches[i] != null) houses.Add(new Place { Name = $"House {i + 1}", At = switches[i].transform, Offset = Vector3.zero });

        // cars and bikes you have
        cars = FindObjectsOfType<MyCar>().Where(c => c != null && c.gameObject.activeInHierarchy)
            .Select(c => new CarEntry { Name = CleanName(c.gameObject.name), Car = c }).OrderBy(c => c.Name).ToList();

        // places: shops, people, map markers
        var list = new List<Place>();
        var seen = new HashSet<string>();
        void Add(string name, Transform t)
        {
            if (t == null || string.IsNullOrEmpty(name) || !t.gameObject.activeInHierarchy || OnUi(t)) return;
            name = CleanName(name);
            if (name.Length == 0 || !seen.Add(name)) return;
            list.Add(new Place { Name = name, At = t });
        }
        if (gm.hippie != null) Add("Hippie", gm.hippie.teleportPoint != null ? gm.hippie.teleportPoint : gm.hippie.transform);
        if (gm.hobo != null) Add("Hobo", gm.hobo.teleportPoint != null ? gm.hobo.teleportPoint : gm.hobo.transform);
        if (gm.mimi != null) Add("Mimi", gm.mimi.teleportPoint != null ? gm.mimi.teleportPoint : gm.mimi.transform);
        foreach (var s in FindObjectsOfType<PawnShop>()) Add("Pawn Shop", s.transform);
        foreach (var s in FindObjectsOfType<DeerBar>()) Add("Deer Bar", s.transform);
        foreach (var s in FindObjectsOfType<ClothStore>()) Add("Cloth Store", s.characterWaitPos != null ? s.characterWaitPos : s.transform);
        foreach (var s in FindObjectsOfType<BarberShop>()) Add("Barber Shop", s.transform);
        foreach (var e in FindObjectsOfType<SickscoreGames.HUDNavigationSystem.HUDNavigationElement>()) Add(e.gameObject.name, e.transform);
        places = list.OrderBy(p => p.Name).ToList();
        if (log) Plugin.L.LogInfo("Teleport places: " + string.Join(", ", places.Select(p => p.Name)) + " | cars: " + string.Join(", ", cars.Select(c => c.Name)));
    }

    static string CleanName(string n)
    {
        n = (n ?? "").Replace("(Clone)", "").Replace("_", " ").Trim();
        foreach (var junk in new[] { "HUD", "Hud", "Indicator", "Marker", "Navigation", "Element" }) n = n.Replace(junk, "");
        n = System.Text.RegularExpressions.Regex.Replace(n, @"([a-z])([A-Z])", "$1 $2");
        n = System.Text.RegularExpressions.Regex.Replace(n, @"\s+", " ").Trim(' ', '-', '.');
        return n.Length == 0 ? n : char.ToUpperInvariant(n[0]) + n.Substring(1);
    }

    bool InCar() => Game.GM != null && Game.GM.carManager != null && Game.GM.carManager.useCar;

    // a free spot on the ground
    static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 0.1f;
        return p;
    }

    void Go(Place pl)
    {
        if (!Check()) return;
        if (pl.At == null) { placesScannedAt = -100f; SetStatus("That place is gone - try again"); return; }
        var t = pl.At;
        var f = t.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        TeleportTo(Ground(t.position + f * 1.2f) + Vector3.up * 0.9f, pl.Name);
    }

    void TeleportTo(Vector3 pos, string name)
    {
        if (!Check()) return;
        if (InCar()) { SetStatus("Get out of the car first"); return; }
        var p = Game.Player;
        if (flying) p.transform.position = pos;
        else p.Teleport(pos);
        SetStatus("Teleported to " + name);
    }

    void BringCar(CarEntry c)
    {
        if (!Check()) return;
        if (c.Car == null) { placesScannedAt = -100f; SetStatus("Car not found"); return; }
        if (InCar()) { SetStatus("You are in a car"); return; }
        var f = Forward(out var t);
        var pos = Ground(t.position + f * 5f) + Vector3.up * 0.6f;
        var rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, f));
        var rb = c.Car.GetComponent<Rigidbody>();
        if (rb == null) rb = c.Car.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
        }
        c.Car.transform.SetPositionAndRotation(pos, rot);
        SetStatus(c.Name + " is here");
    }
}
