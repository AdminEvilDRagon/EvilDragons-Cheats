using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using UnityEngine;

namespace StreamerLifeCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Streamer Life Simulator", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.streamerlife";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<KeyCode> KeyMenu, KeyMoney, KeyFly;
    internal static ConfigEntry<string> Theme;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", KeyCode.F1, "Open/close menu");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", KeyCode.F2, "Add the selected money amount");
        KeyFly = Config.Bind("Hotkeys", "Fly", KeyCode.F3, "Toggle fly mode");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        Ui.InitialTheme = Theme.Value;
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

/// The game keeps money, points and bitcoin in PlayerPrefs.
static class Prefs
{
    public const string Wallet = "money", Card = "cardmoney", Bank = "bankmoney", Btc = "coinbtc", Points = "streamerpoint";
    public static float F(string k) => PlayerPrefs.GetFloat(k);
    public static void AddF(string k, float v) => PlayerPrefs.SetFloat(k, Mathf.Max(0f, PlayerPrefs.GetFloat(k) + v));
    public static int I(string k) => PlayerPrefs.GetInt(k);
    public static void AddI(string k, int v) => PlayerPrefs.SetInt(k, Math.Max(0, PlayerPrefs.GetInt(k) + v));
}

static class Game
{
    public static PlayerController Player => PlayerController.Instance;
    public static VHS.FirstPersonController Fps => Player != null ? Player.FpsController : null;
    public static bool InGame => Player != null && Fps != null && PlayerStats.Instance != null;
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
    float origWalk = -1f, origRun = -1f, origJump = -1f;
    VHS.FirstPersonController origFor;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };

    // money
    long moneyAmount = 10_000;
    bool toCard = true;
    static readonly long[] MoneyPresets = { 1_000, 10_000, 50_000, 100_000, 500_000, 1_000_000 };
    static readonly float[] BtcPresets = { 0.01f, 0.1f, 1f, 10f };
    float btcAmount = 0.1f;

    // stream
    static readonly int[] FollowerPresets = { 100, 1_000, 10_000, 100_000 };
    static readonly int[] SubPresets = { 10, 100, 1_000, 10_000 };
    static readonly int[] ViewerPresets = { 10, 100, 1_000, 5_000 };
    int followerAmount = 1_000, subAmount = 100, viewerAmount = 100, pointAmount = 5;
    static readonly int[] PointPresets = { 1, 5, 10, 50 };

    // world
    bool freezeTime;
    float timeSpeed = 1f, origTimeCounter = -1f;
    TimeManager origTimeFor;
    static readonly float[] TimeSpeedPresets = { 0.5f, 1f, 2f, 5f };

    // teleport
    class Place { public string Name; public Transform At; public bool UseRot; }
    class CarEntry { public string Name; public GameObject Car; }
    List<Place> houses = new List<Place>(), places = new List<Place>();
    List<CarEntry> cars = new List<CarEntry>();
    float placesScannedAt = -100f;
    bool hasSaved;
    Vector3 savedPos;
    int placePage;

    // items
    class ItemEntry { public Parts Prefab; public string Name; public string Category; }
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
        fullNeeds = Saved.Get("AllNeedsFull", false);
        speedMult = Saved.Get("WalkSpeed", 1f);
        jumpMult = Saved.Get("JumpPower", 1f);
        flySpeed = Saved.Get("FlySpeed", 2f);
        moneyAmount = Saved.Get("MoneyAmount", 10_000L);
        toCard = Saved.Get("MoneyToCard", true);
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
        Saved.Set("MoneyToCard", toCard);
        Saved.Set("FreezeTime", freezeTime);
        Saved.Set("TimeSpeed", timeSpeed);
        Saved.Set("SpawnToInventory", toInventory);
        Saved.Flush();
        SetStatus("Settings saved - they load automatically next time");
    }

    // ---------------- keyboard ----------------
    static bool Pressed(KeyCode k) => k != KeyCode.None && Input.GetKeyDown(k);
    static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();

        // leave the search box as soon as you click anywhere else
        if (searchCloseCheckFrame >= 0 && Time.frameCount > searchCloseCheckFrame)
        {
            if (searchFieldClickFrame != searchCloseCheckFrame) searching = false;
            searchCloseCheckFrame = -1;
        }
        if (searching && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
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

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        if (!Game.InGame) { flying = false; return; }
        var fps = Game.Fps;

        // keep the menu's freeze on while it is open (the game clears it when panels close)
        if (show && weFroze && !Game.Player.freezed) Game.Player.freezed = true;

        if (fullNeeds) FillNeeds();

        // movement multipliers, always computed from the original values
        if (origFor != fps) { origFor = fps; origWalk = fps.walkSpeed; origRun = fps.runSpeed; origJump = fps.jumpSpeed; }
        float w = origWalk * speedMult, r = origRun * speedMult, j = origJump * jumpMult;
        if (fps.walkSpeed != w) fps.walkSpeed = w;
        if (fps.runSpeed != r) fps.runSpeed = r;
        if (fps.jumpSpeed != j) fps.jumpSpeed = j;

        if (flying) FlyMove(fps);

        var tm = TimeManager.Instance;
        if (tm != null)
        {
            if (origTimeFor != tm) { origTimeFor = tm; origTimeCounter = tm.timeCounter; }
            float want = freezeTime ? 0f : origTimeCounter * timeSpeed;
            if (tm.timeCounter != want) tm.timeCounter = want;
        }
    }

    static void FillNeeds()
    {
        var s = PlayerStats.Instance;
        if (s == null) return;
        s.health = 100f; s.energy = 100f; s.hunger = 100f; s.hygiene = 100f; s.toilet = 100f;
        if (s.happiness < 100f) s.happiness = 100f;
        if (TimeManager.Instance != null) TimeManager.Instance.awakeTime = 0f;
    }

    // ---------------- menu open/close ----------------
    bool weFroze;
    void SetShow(bool value)
    {
        show = value;
        if (!show) { editId = null; rebinding = null; searching = false; }
        var p = Game.Player;
        if (p == null) { weFroze = false; return; }
        if (show)
        {
            if (!p.freezed) { weFroze = true; p.freezed = true; }
        }
        else
        {
            if (weFroze)
            {
                p.freezed = false;
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
        bool ctrl = Ctrl;
        if (ctrl && Pressed(KeyCode.A)) searchAllSelected = search.Length > 0;
        if (searchAllSelected && Pressed(KeyCode.Delete)) { search = ""; searchAllSelected = false; itemPage = 0; }

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
            itemPage = 0;
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
        Ui.Label($"Streamer Life Simulator  ·  v{Plugin.Version}", Ui.Sub);
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
        var s = PlayerStats.Instance;
        Ui.BeginCard("Needs", s != null ? $"Health {s.health:0} · Energy {s.energy:0} · Hunger {s.hunger:0} · Hygiene {s.hygiene:0} · Toilet {s.toilet:0}" : null);
        fullNeeds = Ui.Toggle(fullNeeds, "All Needs Always Full", "never hungry, tired, dirty or sick");
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
        Ui.BeginCard("Money", Game.InGame ? $"Wallet ${Prefs.F(Prefs.Wallet):N0}  ·  Bank card ${Prefs.F(Prefs.Card):N0}  ·  Savings ${Prefs.F(Prefs.Bank):N0}" : null);
        Ui.Label("Put the money on", Ui.Sub);
        toCard = Ui.Chips(toCard, new[] { true, false }, v => v ? "Bank Card" : "Wallet (cash)", 2);
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + Short(v), 6);
        NumField("money", "Custom amount", moneyAmount, 1, 1_000_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${Short(moneyAmount)}  [{Plugin.KeyMoney.Value}]", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${Short(moneyAmount)}", Ui.Tab)) AddMoney(-moneyAmount);
        GUILayout.EndHorizontal();
        Ui.Label("Skills and online shops pay with the bank card, shops in town with cash.", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Bitcoin", Game.InGame ? $"{Prefs.F(Prefs.Btc):0.#####} BTC" : null);
        btcAmount = Ui.Chips(btcAmount, BtcPresets, v => $"{v:0.##} BTC", 4);
        if (Ui.Button($"+ {btcAmount:0.##} BTC", Ui.Primary)) { if (Check()) { Prefs.AddF(Prefs.Btc, btcAmount); SetStatus($"+{btcAmount:0.##} BTC"); } }
        Ui.EndCard();
    }

    void DrawStream()
    {
        var ads = BlenderShowAds.Instance;
        Ui.BeginCard("Channel", ads != null ? $"{ads.totalFollowers:N0} followers  ·  {ads.totalSubs:N0} subs  ·  level {ads.blenderLevel}" : null);
        Ui.Label("Followers", Ui.Sub);
        followerAmount = Ui.Chips(followerAmount, FollowerPresets, v => Short(v), 4);
        if (Ui.Button($"+ {Short(followerAmount)} Followers", Ui.Primary)) AddFollowers(followerAmount, 0);
        Ui.Label("Subscribers", Ui.Sub);
        subAmount = Ui.Chips(subAmount, SubPresets, v => Short(v), 4);
        if (Ui.Button($"+ {Short(subAmount)} Subs", Ui.Primary)) AddFollowers(0, subAmount);
        Ui.EndCard();

        var sm = StreamManager.Instance;
        Ui.BeginCard("Live Stream", sm != null ? $"{sm.liveViewerCount:N0} viewers watching right now" : null);
        viewerAmount = Ui.Chips(viewerAmount, ViewerPresets, v => Short(v), 4);
        if (Ui.Button($"+ {Short(viewerAmount)} Live Viewers", Ui.Primary)) AddViewers(viewerAmount);
        Ui.Label("Only while you are streaming.", Ui.Sub);
        Ui.EndCard();

        Ui.BeginCard("Skill Points", Game.InGame ? $"{Prefs.I(Prefs.Points)} streamer points" : null);
        pointAmount = Ui.Chips(pointAmount, PointPresets, v => "+" + v, 4);
        if (Ui.Button($"+ {pointAmount} Streamer Points", Ui.Primary)) AddPoints(pointAmount);
        Ui.Label("Open the skill tree with X.", Ui.Sub);
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
        if (Ui.Button("Save Position", Ui.Chip)) { savedPos = Game.Fps.transform.position; hasSaved = true; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position"); else SetStatus("No position saved yet"); }
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Houses");
        int home = HouseNumber.Instance != null ? HouseNumber.Instance.houseNumber : 0;
        for (int k = 0; k < houses.Count; k++)
        {
            string n = $"<b>{houses[k].Name}</b>" + (k + 1 == home ? $"   <size=12><color={Ui.AHex}>your home</color></size>" : "");
            if (Ui.Button(n, k + 1 == home ? Ui.RowOn : Ui.Row)) Go(houses[k]);
        }
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

    void DrawWorld()
    {
        var tm = TimeManager.Instance;
        Ui.BeginCard("Time", tm != null ? $"Current time: {tm.timeStr}" : null);
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

    void DrawBest()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }

        Ui.BeginCard("Best Of Everything", "The best (strongest / most expensive) item of each kind");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Best Of Everything", Ui.Primary)) SpawnBestAll();
        if (Ui.Button("Best PC", Ui.Primary)) SpawnBestPc();
        GUILayout.EndHorizontal();
        Ui.Label("Best PC = case, CPU + matching mainboard, 4x RAM, 2x graphics card, 2x drive.", Ui.Sub);
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
            string label = $"<b>{kv.Key}</b>   <size=12><color=#b8c2d6>{NameOf(kv.Value.item)}  ·  ${kv.Value.item.itemPrice:N0}</color></size>";
            if (Ui.Button(label, Ui.Row))
                SpawnParts(new List<Parts> { kv.Value }, "the best " + kv.Key + ": " + NameOf(kv.Value.item));
        }
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
        string cat = Ui.Chips(itemCategory, categories, c => c, 4);
        if (cat != itemCategory) { itemCategory = cat; itemPage = 0; }

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

    void DrawSettings()
    {
        Ui.BeginCard("Theme");
        string th = Ui.Chips(Ui.CurrentTheme, Ui.ThemeNames, n => n, 3);
        if (th != Ui.CurrentTheme) { Ui.SetTheme(th); Plugin.Theme.Value = th; SetStatus($"Theme: {th}"); }
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
        if (Game.InGame) return true;
        SetStatus("Load into a game first");
        return false;
    }

    void AddMoney(long amount)
    {
        if (!Check()) return;
        Prefs.AddF(toCard ? Prefs.Card : Prefs.Wallet, amount);
        SetStatus($"{(amount >= 0 ? "+" : "−")}$ {Math.Abs(amount):N0} {(toCard ? "on your bank card" : "in your wallet")}");
    }

    void AddFollowers(int followers, int subs)
    {
        if (!Check()) return;
        var ads = BlenderShowAds.Instance;
        if (ads == null) { SetStatus("Channel not found"); return; }
        try
        {
            ads.totalFollowers += followers;
            ads.totalSubs += subs;
            ads.SaveGame();
            try { ads.CheckBlenderLevelUp(); } catch { }
            SetStatus(followers > 0 ? $"+{followers:N0} followers" : $"+{subs:N0} subscribers");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void AddViewers(int n)
    {
        if (!Check()) return;
        var sm = StreamManager.Instance;
        if (sm == null || sm.ops == null || !sm.ops.streamisStart) { SetStatus("Start a stream first"); return; }
        sm.liveViewerCount += n;
        SetStatus($"+{n:N0} live viewers");
    }

    void AddPoints(int n)
    {
        if (!Check()) return;
        Prefs.AddI(Prefs.Points, n);
        SetStatus($"+{n} streamer points");
    }

    void SetTime(float hour)
    {
        if (!Check()) return;
        var tm = TimeManager.Instance;
        if (tm == null) { SetStatus("Clock not found"); return; }
        tm.time = hour;
        SetStatus($"Time set to {hour:00}:00");
    }

    // fly mode: switch off the character controller and move the player ourselves
    void SetFly(bool on)
    {
        if (!Check()) return;
        var fps = Game.Fps;
        var cc = fps.GetComponent<CharacterController>();
        fps.enabled = !on;
        if (cc != null) cc.enabled = !on;
        flying = on;
        SetStatus(on ? "Fly mode on" : "Fly mode off");
    }

    void FlyMove(VHS.FirstPersonController fps)
    {
        if (fps.enabled) fps.enabled = false;
        var cc = fps.GetComponent<CharacterController>();
        if (cc != null && cc.enabled) cc.enabled = false;
        if (show || Game.Player.freezed) return;
        var cam = Camera.main;
        Vector3 fwd = cam != null ? cam.transform.forward : fps.transform.forward;
        Vector3 right = cam != null ? cam.transform.right : fps.transform.right;
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += fwd;
        if (Input.GetKey(KeyCode.S)) dir -= fwd;
        if (Input.GetKey(KeyCode.D)) dir += right;
        if (Input.GetKey(KeyCode.A)) dir -= right;
        if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
        if (Ctrl || Input.GetKey(KeyCode.C)) dir -= Vector3.up;
        if (dir.sqrMagnitude < 0.01f) return;
        float speed = 6f * flySpeed * (Input.GetKey(KeyCode.LeftShift) ? 2.5f : 1f);
        fps.transform.position += dir.normalized * speed * Time.deltaTime;
    }

    // ---------------- teleport ----------------
    static bool OnUi(Component c) => c.GetComponent<RectTransform>() != null || c.GetComponentInParent<Canvas>() != null;

    static readonly string[] CarNames = { "Old Car", "Porsche", "Van", "SUV", "Japanese Car", "Classic Car", "Roof Car" };

    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        bool log = places.Count == 0;

        // houses: the taxi app knows where they are
        houses = new List<Place>();
        var zuber = Resources.FindObjectsOfTypeAll<ZuberApp>().FirstOrDefault(z => z != null && z.gameObject.scene.IsValid());
        if (zuber != null)
        {
            var pts = new[] { zuber.house1TeleportPos, zuber.house2TeleportPos, zuber.house3TeleportPos, zuber.house4TeleportPos, zuber.house5TeleportPos };
            for (int i = 0; i < pts.Length; i++)
                if (pts[i] != null) houses.Add(new Place { Name = $"House {i + 1}", At = pts[i], UseRot = true });
        }

        // cars
        cars = new List<CarEntry>();
        var cm = CarManager.Instance;
        if (cm != null)
        {
            var objs = new[] { cm.dadyCar, cm.porscheCar, cm.vanCar, cm.suvCar, cm.japaneseCar, cm.classicCar, cm.roofCar };
            for (int i = 0; i < objs.Length; i++)
                if (objs[i] != null && objs[i].activeInHierarchy) cars.Add(new CarEntry { Name = CarNames[i], Car = objs[i] });
        }

        // places: named things in the world, NPCs, gas station, pet shop
        var list = new List<Place>();
        var seen = new HashSet<string>();
        void Add(string name, Component c)
        {
            if (c == null || string.IsNullOrEmpty(name) || !c.gameObject.activeInHierarchy || OnUi(c)) return;
            name = name.Trim();
            if (name.Length == 0 || !seen.Add(name)) return;   // first one of each name only
            list.Add(new Place { Name = name, At = c.transform });
        }
        foreach (var g in FindObjectsOfType<GasMachine>()) Add("Gas Station", g);
        foreach (var d in FindObjectsOfType<BuyableDog>()) Add("Pet Shop", d);
        foreach (var d in FindObjectsOfType<DialogLine>())
            if (d.npcDialog != null) Add(d.npcDialog.npcName, d);
        foreach (var it in FindObjectsOfType<ItemInfo>()) Add(it.showedName, it);
        places = list.OrderBy(p => p.Name).ToList();
        if (log) Plugin.L.LogInfo("Teleport places: " + string.Join(", ", places.Select(p => p.Name)));
    }

    bool InCar()
    {
        var cm = CarManager.Instance;
        return cm != null && cm.rearWheelDrive != null && cm.rearWheelDrive.enterCarCamera != null && cm.rearWheelDrive.enterCarCamera.enteredCar;
    }

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
        if (pl.UseRot) { TeleportTo(pl.At.position, pl.Name, pl.At); return; }
        var t = pl.At;
        var f = t.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        TeleportTo(Ground(t.position + f * 1.5f), pl.Name);
    }

    void TeleportTo(Vector3 pos, string name, Transform rot = null)
    {
        if (!Check()) return;
        if (InCar()) { SetStatus("Get out of the car first"); return; }
        var pc = Game.Player;
        if (flying) pc.transform.position = pos;
        else if (rot != null) pc.SetNewPositionAndRotation(rot);
        else pc.SetNewPositionAndRotation(pos);
        SetStatus("Teleported to " + name);
    }

    void BringCar(CarEntry c)
    {
        if (!Check()) return;
        if (c.Car == null) { placesScannedAt = -100f; SetStatus("Car not found"); return; }
        if (InCar()) { SetStatus("You are in a car"); return; }
        var t = Game.Fps.transform;
        var cam = Camera.main;
        var f = cam != null ? cam.transform.forward : t.forward;
        f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
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

    // ---------------- items ----------------
    static string CategoryOf(Item it)
    {
        if (it.isFood) return "Food";
        if (it.isCloth) return "Clothes";
        if (it.isComputerComponents) return "PC Parts";
        if (it.isFurniture) return "Furniture";
        return "Other";
    }

    void ScanItems()
    {
        itemsScannedAt = Time.unscaledTime;
        var all = AllGameObjets.Instance;
        var list = new List<ItemEntry>();
        if (all != null && all.allParts != null)
        {
            var seen = new HashSet<string>();
            foreach (var p in all.allParts)
            {
                if (p == null || p.item == null || string.IsNullOrEmpty(p.item.itemName)) continue;
                if (!seen.Add(p.item.itemName)) continue;
                string name = string.IsNullOrEmpty(p.item.showdItemName) ? p.item.itemName : p.item.showdItemName;
                list.Add(new ItemEntry { Prefab = p, Name = name, Category = CategoryOf(p.item) });
            }
        }
        items = list.OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
        categories = new[] { "All" }.Concat(items.Select(e => e.Category).Distinct()).ToArray();
    }

    Vector3 SpawnPos(int i)
    {
        var cam = Camera.main;
        var t = Game.Fps.transform;
        var f = cam != null ? cam.transform.forward : t.forward;
        f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        var pos = t.position + f * 1.5f + Vector3.up * (1.2f + i * 0.4f);
        if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point + hit.normal * 0.3f + Vector3.up * (0.3f + i * 0.4f);
        return pos;
    }

    // ---------------- best PC ----------------
    static float Score(Item it)
    {
        if (it.isCpu) return it.ghz * 1000f + it.itemPrice * 0.001f;
        if (it.isRam) return it.ramGB * 100000f + it.ramMhz + it.itemPrice * 0.001f;
        if (it.isDisplayCard) return it.displayCardGB * 100000f + it.itemPrice * 0.001f;
        if (it.isHdd) return it.hddCapacity * 10f + it.hddSpeed + it.itemPrice * 0.001f;
        return it.itemPrice;
    }

    static Parts Best(IEnumerable<Parts> parts) => parts.OrderByDescending(p => Score(p.item)).FirstOrDefault();

    void SpawnBestPc()
    {
        if (!Check()) return;
        try
        {
            var all = AllGameObjets.Instance;
            if (all == null || all.allParts == null) { SetStatus("Items not found"); return; }
            var parts = all.allParts.Where(p => p != null && p.item != null).GroupBy(p => p.item.itemName).Select(g => g.First()).ToList();

            // CPU + mainboard need the same socket: take the best CPU that has a mainboard
            var boards = parts.Where(p => p.item.isMotherboard).ToList();
            Parts cpu = null, board = null;
            foreach (var c in parts.Where(p => p.item.isCpu).OrderByDescending(p => Score(p.item)))
            {
                var b = Best(boards.Where(x => x.item.socketName == c.item.socketName));
                if (b != null) { cpu = c; board = b; break; }
            }
            var ram = Best(parts.Where(p => p.item.isRam));
            var gpu = Best(parts.Where(p => p.item.isDisplayCard));
            var drive = Best(parts.Where(p => p.item.isHdd));

            var pcCase = Best(parts.Where(p => KindOf(p.item) == "Case"));
            var list = new List<Parts>();
            if (pcCase != null) list.Add(pcCase);
            if (board != null) list.Add(board);
            if (cpu != null) list.Add(cpu);
            for (int i = 0; i < 4 && ram != null; i++) list.Add(ram);
            for (int i = 0; i < 2 && gpu != null; i++) list.Add(gpu);
            for (int i = 0; i < 2 && drive != null; i++) list.Add(drive);
            if (list.Count == 0) { SetStatus("No PC parts found"); return; }

            LayOut(list, 4, 0.5f);
            string Name(Parts p) => p == null ? "-" : NameOf(p.item);
            Plugin.L.LogInfo($"Best PC: case {Name(pcCase)}, board {Name(board)}, cpu {Name(cpu)} ({cpu?.item.socketName}), ram 4x {Name(ram)}, gpu 2x {Name(gpu)}, drive 2x {Name(drive)}");
            SetStatus($"Spawned the best PC: {Name(cpu)}, {Name(gpu)} x2, {Name(ram)} x4");
        }
        catch (Exception ex) { Plugin.L.LogError(ex); SetStatus("Could not spawn - see log"); }
    }

    static string NameOf(Item it) => string.IsNullOrEmpty(it.showdItemName) ? it.itemName : it.showdItemName;

    static string Pretty(string s)
    {
        s = (s ?? "").Replace("_", " ").Trim();
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    // what kind of thing an item is: PC part type, else the game's own item group
    static string KindOf(Item it)
    {
        if (it.isCpu) return "CPU";
        if (it.isMotherboard) return "Mainboard";
        if (it.isRam) return "RAM";
        if (it.isDisplayCard) return "Graphics Card";
        if (it.isHdd) return "Drive";
        if (!string.IsNullOrEmpty(it.itemGroup)) return Pretty(it.itemGroup);
        if (!string.IsNullOrEmpty(it.componentType)) return Pretty(it.componentType);
        return CategoryOf(it);
    }

    List<KeyValuePair<string, Parts>> bestKinds;
    float bestKindsAt = -100f;
    List<KeyValuePair<string, Parts>> BestKinds()
    {
        if (bestKinds != null && Time.unscaledTime - bestKindsAt < 30f) return bestKinds;
        bestKindsAt = Time.unscaledTime;
        var all = AllGameObjets.Instance;
        bestKinds = new List<KeyValuePair<string, Parts>>();
        if (all == null || all.allParts == null) return bestKinds;
        bestKinds = all.allParts.Where(p => p != null && p.item != null && !string.IsNullOrEmpty(p.item.itemName))
            .GroupBy(p => KindOf(p.item))
            .Where(g => g.Key != "Trashitem" && (g.Count() > 1 || g.First().item.itemPrice > 0))
            .Select(g => new KeyValuePair<string, Parts>(g.Key, Best(g)))
            .OrderBy(kv => kv.Key).ToList();
        Plugin.L.LogInfo("Best per kind: " + string.Join(", ", bestKinds.Select(kv => $"{kv.Key}={NameOf(kv.Value.item)} (${kv.Value.item.itemPrice})")));
        return bestKinds;
    }

    // consumables and trash are left out of "best of everything"
    static readonly HashSet<string> Junk = new HashSet<string> { "Cat", "Drink", "Food", "Pill", "Syringe", "Trashitem", "Cloth" };

    void SpawnBestAll()
    {
        if (!Check()) return;
        var list = new List<Parts>();
        foreach (var kv in BestKinds())
        {
            var it = kv.Value.item;
            if (Junk.Contains(kv.Key)) continue;
            // a full PC: 4x RAM, 2x graphics card, 2x drive
            int n = it.isRam ? 4 : (it.isDisplayCard || it.isHdd) ? 2 : 1;
            for (int i = 0; i < n; i++) list.Add(kv.Value);
        }
        // the CPU must fit the mainboard socket
        var cpu = list.FirstOrDefault(p => p.item.isCpu);
        var board = list.FirstOrDefault(p => p.item.isMotherboard);
        if (cpu != null && board != null && cpu.item.socketName != board.item.socketName)
        {
            var all = AllGameObjets.Instance.allParts.Where(p => p != null && p.item != null).ToList();
            var fit = Best(all.Where(p => p.item.isMotherboard && p.item.socketName == cpu.item.socketName));
            if (fit != null) list[list.IndexOf(board)] = fit;
        }
        SpawnParts(list, $"the best of everything ({list.Count} items)");
    }

    void SpawnParts(List<Parts> list, string what)
    {
        if (!Check()) return;
        try { LayOut(list, 5, 1.1f); SetStatus("Spawned " + what); }
        catch (Exception ex) { Plugin.L.LogError(ex); SetStatus("Could not spawn - see log"); }
    }

    // spawn items in rows in front of you
    void LayOut(List<Parts> list, int perRow, float gap)
    {
        var cam = Camera.main;
        var t = Game.Fps.transform;
        var f = cam != null ? cam.transform.forward : t.forward;
        f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
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

    // ---------------- houses ----------------
    static RealEstateManager reCache;
    static RealEstateManager RealEstate()
    {
        if (reCache == null) reCache = Resources.FindObjectsOfTypeAll<RealEstateManager>().FirstOrDefault(r => r != null && r.gameObject.scene.IsValid());
        return reCache;
    }

    static string BestHouseInfo()
    {
        var re = RealEstate();
        if (re == null) return null;
        float[] v = { re.house1Value, re.house2Value, re.house3Value, re.house4Value };
        int best = Array.IndexOf(v, v.Max());
        int owned = Enumerable.Range(1, 4).Count(i => PlayerPrefs.GetInt($"house{i}buyed") >= 5);
        return $"Best: house for ${v[best]:N0}  ·  you own {owned} of 4";
    }

    void GetHouses(bool onlyBest)
    {
        if (!Check()) return;
        var re = RealEstate();
        if (re == null) { SetStatus("Real estate not found"); return; }
        float[] v = { re.house1Value, re.house2Value, re.house3Value, re.house4Value };
        int best = Array.IndexOf(v, v.Max());
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            if (onlyBest && i != best) continue;
            if (PlayerPrefs.GetInt($"house{i + 1}buyed") >= 5) continue;
            PlayerPrefs.SetInt($"house{i + 1}buyed", 50);
            n++;
        }
        try { re.CheckBuyButtonsStatus(); } catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        SetStatus(n == 0 ? "You already own " + (onlyBest ? "the best house" : "all houses")
                 : onlyBest ? $"The best house (${v[best]:N0}) is yours" : $"{n} houses are yours now");
    }

    void SpawnItem(ItemEntry e, int amount)
    {
        if (!Check()) return;
        try
        {
            if (toInventory)
            {
                var inv = Inventory.Instance;
                if (inv == null) { SetStatus("Inventory not found"); return; }
                int added = 0;
                for (int n = 0; n < amount; n++)
                {
                    bool free = false;
                    for (int i = 0; i < inv.slotNumber && i < inv.inventorySlots.Length; i++)
                        if (inv.inventorySlots[i] != null && inv.inventorySlots[i].myItem == null) { free = true; break; }
                    if (!free) break;
                    inv.AddItem(e.Prefab.item, e.Prefab.durability);
                    added++;
                }
                SetStatus(added == amount ? $"{added}x {e.Name} in your inventory" : added > 0 ? $"Only {added}x fit into your inventory" : "Your inventory is full");
                return;
            }
            var rot = Quaternion.Euler(0f, Game.Fps.transform.eulerAngles.y + 180f, 0f);
            for (int n = 0; n < amount; n++)
            {
                var go = Instantiate(e.Prefab.gameObject, SpawnPos(n), rot);
                go.SetActive(true);
            }
            SetStatus($"Spawned {amount}x {e.Name}");
        }
        catch (Exception ex) { Plugin.L.LogError(ex); SetStatus("Could not spawn - see log"); }
    }
}
