using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EasyBuildSystem.Examples.Bases.Scripts;
using EasyBuildSystem.Examples.Bases.Scripts.FirstPerson;
using EvilDragon;
using SUPERCharacter;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PumpingSimulator2Cheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Pumping Simulator 2", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.pumpingsimulator2";
    public const string Version = "1.0.1";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<Key> KeyMenu, KeyMoney, KeyFuel, KeyFly;
    internal static ConfigEntry<string> Theme;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", Key.F1, "Open/close menu");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", Key.F2, "Add the selected money amount");
        KeyFuel = Config.Bind("Hotkeys", "FillFuel", Key.F3, "Fill the station's fuel tank");
        KeyFly = Config.Bind("Hotkeys", "Fly", Key.F4, "Toggle fly mode");
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

static class Game
{
    public static SUPERCharacterAIO Player => SUPERCharacterAIO.instance;
    public static bool InGame => Player != null && MoneyUI.instance != null;

    public static StationManager Station
    {
        get
        {
            try
            {
                var p = Player;
                if (p != null) { var s = StationManager.GetNearest(p.transform.position, float.PositiveInfinity); if (s != null) return s; }
                return StationManager.stationManagers.FirstOrDefault(x => x != null);
            }
            catch { return null; }
        }
    }
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Money", "Station", "World", "Items", "Settings" };

    // player
    bool infStamina, noHunger, flying;
    float speedMult = 1f, jumpMult = 1f, flySpeed = 1f;
    float origWalk = -1f, origSprint = -1f, origJump = -1f;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };

    // money
    long moneyAmount = 10_000;
    static readonly long[] MoneyPresets = { 1_000, 10_000, 50_000, 100_000, 500_000, 1_000_000 };
    static readonly int[] RepPresets = { 100, 500, 1_000, 5_000 };
    int repAmount = 500;

    // station
    bool infFuel, workersEnergy, fastDelivery;
    bool origZanama, zanamaSet;

    // world
    bool freezeTime;

    // items
    class OrderEntry { public Order Order; public string Name; public string Category; }
    List<OrderEntry> orders;
    float ordersScannedAt = -100f;
    string itemCategory = "All", search = "";
    bool searching, spawnAtCrosshair = true;
    int searchFieldClickFrame = -1, searchCloseCheckFrame = -1;
    bool searchAllSelected;
    int itemPage, itemAmount = 10;
    OrderEntry selectedOrder;
    string[] categories = { "All" };
    static readonly int[] AmountPresets = { 1, 10, 50, 100 };
    const int PageSize = 10;

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
        infStamina = Saved.Get("InfiniteStamina", false);
        noHunger = Saved.Get("NoHungerThirst", false);
        speedMult = Saved.Get("WalkSpeed", 1f);
        jumpMult = Saved.Get("JumpPower", 1f);
        flySpeed = Saved.Get("FlySpeed", 1f);
        moneyAmount = Saved.Get("MoneyAmount", 10_000L);
        infFuel = Saved.Get("InfiniteFuel", false);
        workersEnergy = Saved.Get("WorkersNeverTired", false);
        fastDelivery = Saved.Get("FastDelivery", false);
        freezeTime = Saved.Get("FreezeTime", false);
        spawnAtCrosshair = Saved.Get("SpawnAtCrosshair", true);
    }

    void SaveSettings()
    {
        Saved.Set("InfiniteStamina", infStamina);
        Saved.Set("NoHungerThirst", noHunger);
        Saved.Set("WalkSpeed", speedMult);
        Saved.Set("JumpPower", jumpMult);
        Saved.Set("FlySpeed", flySpeed);
        Saved.Set("MoneyAmount", moneyAmount);
        Saved.Set("InfiniteFuel", infFuel);
        Saved.Set("WorkersNeverTired", workersEnergy);
        Saved.Set("FastDelivery", fastDelivery);
        Saved.Set("FreezeTime", freezeTime);
        Saved.Set("SpawnAtCrosshair", spawnAtCrosshair);
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
    readonly Queue<char> typed = new();
    void HookText()
    {
        if (textHooked || Keyboard.current == null) return;
        Keyboard.current.onTextInput += c => { if (searching) typed.Enqueue(c); };
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
            if (Pressed(Plugin.KeyFuel.Value)) FillFuel();
            if (Pressed(Plugin.KeyFly.Value)) SetFly(!flying);
        }
        if (!searching) typed.Clear();

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        var p = Game.Player;
        if (p == null) return;

        if (infStamina) p.currentStaminaLevel = p.Stamina;
        if (noHunger && p.currentSurvivalStats != null && p.defaultSurvivalStats != null)
        {
            p.currentSurvivalStats.Hunger = p.defaultSurvivalStats.Hunger;
            p.currentSurvivalStats.Hydration = p.defaultSurvivalStats.Hydration;
        }

        // movement multipliers, always computed from the original values
        if (origWalk < 0f) { origWalk = p.walkingSpeed; origSprint = p.sprintingSpeed; origJump = p.jumpPower; }
        float w = origWalk * speedMult, s = origSprint * speedMult, j = origJump * jumpMult;
        if (p.walkingSpeed != w) p.walkingSpeed = w;
        if (p.sprintingSpeed != s) p.sprintingSpeed = s;
        if (p.jumpPower != j) p.jumpPower = j;

        if (flying)
        {
            var fc = p.GetComponent<Demo_FreeCamera>();
            if (fc != null) fc.speedMultiplier = flySpeed;
        }

        var st = Game.Station;
        if (st != null)
        {
            if (infFuel) { float max = st.maxFuel; if (st.fuel < max) st.fuel = max; }
            if (workersEnergy && st.workers != null)
                foreach (var wk in st.workers) if (wk != null && wk.energy < 100f) wk.energy = 100f;
        }

        var om = OrderManager.instance;
        if (om != null)
        {
            if (fastDelivery) { if (!zanamaSet) { origZanama = om.zanamaExtra; zanamaSet = true; } om.zanamaExtra = true; }
            else if (zanamaSet) { om.zanamaExtra = origZanama; zanamaSet = false; }
        }

        if (EnviroSkyMgr.instance != null)
        {
            if (freezeTime && !timeFrozen) { EnviroSkyMgr.instance.SetTimeProgress(EnviroTime.TimeProgressMode.None); timeFrozen = true; }
            else if (!freezeTime && timeFrozen) { EnviroSkyMgr.instance.SetTimeProgress(EnviroTime.TimeProgressMode.Simulated); timeFrozen = false; }
        }
    }
    bool timeFrozen;

    // ---------------- menu open/close ----------------
    bool wePaused;
    void SetShow(bool value)
    {
        show = value;
        if (!show) { editId = null; rebinding = null; searching = false; }
        var p = Game.Player;
        if (p == null) { wePaused = false; return; }
        if (show)
        {
            if (!p.controllerPaused)
            {
                // Remember first: the game's PausePlayer can throw halfway (after it already paused).
                wePaused = true;
                try { p.PausePlayer(PauseModes.BlockInputOnly); }
                catch { p.controllerPaused = true; }
            }
        }
        else
        {
            if (wePaused)
            {
                try { p.UnpausePlayer(); }
                catch { p.controllerPaused = false; }
                if (p.controllerPaused) p.controllerPaused = false;
            }
            wePaused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ---------------- typed input ----------------
    void HandleNumberInput()
    {
        for (int d = 0; d <= 9; d++)
        {
            if (Pressed(Key.Digit0 + d) || Pressed(Key.Numpad0 + d))
                if (editBuf.Length < 12) editBuf += (char)('0' + d);
        }
        if (Pressed(Key.Backspace) && editBuf.Length > 0) editBuf = editBuf.Substring(0, editBuf.Length - 1);
        if (Pressed(Key.Delete)) editBuf = "";
        if (editBuf.Length > 0 && long.TryParse(editBuf, out var v)) editSet?.Invoke(Math.Clamp(v, editMin, editMax));
        if (Pressed(Key.Enter) || Pressed(Key.NumpadEnter) || Pressed(Key.Escape)) editId = null;
    }

    void HandleSearchInput()
    {
        // Ctrl+A selects the whole search text: the next key replaces it, Backspace/Delete clears it
        var kb = Keyboard.current;
        bool ctrl = kb != null && kb.ctrlKey.isPressed;
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
        allKeys ??= ((Key[])Enum.GetValues(typeof(Key))).Where(k => k != Key.None && k != Key.Escape && (int)k < 111).ToArray();
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
            var rect = new Rect(24, 24, 500, Screen.height - 48);
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
        Ui.Label($"Pumping Simulator 2  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyMoney.Value.ToString(), "+ Money");
        Ui.KeyHint(Plugin.KeyFuel.Value.ToString(), "Fill fuel");
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!Game.InGame) Ui.Label("Load into a game first.", Ui.Sub);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; searching = false; }
        GUILayout.EndHorizontal();
        Ui.Space(4);

        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawMoney(); break;
            case 2: DrawStation(); break;
            case 3: DrawWorld(); break;
            case 4: DrawItems(); break;
            case 5: DrawSettings(); break;
        }

        if (status.Length > 0 && Time.unscaledTime - statusTime < 6f)
        {
            Ui.Space(8);
            Ui.Label("» " + status, Ui.Status);
        }
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
        Ui.BeginCard("Needs", "Never get tired, hungry or thirsty");
        Ui.Toggle2(ref infStamina, "Infinite Stamina", ref noHunger, "No Hunger & Thirst");
        Ui.EndCard();

        Ui.BeginCard("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump power", Ui.Sub);
        jumpMult = Ui.Chips(jumpMult, JumpPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly freely around the map");
        bool f = Ui.Toggle(flying, $"Fly  [{Plugin.KeyFly.Value}]", "WASD + mouse");
        if (f != flying) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        flySpeed = Ui.Chips(flySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();
    }

    void DrawMoney()
    {
        float money = 0f;
        try { if (MoneyUI.instance != null) money = MoneyUI.instance.targetCount; } catch { }
        Ui.BeginCard("Money");
        Ui.Label($"<size=22><b>$ {money:N2}</b></size>", Ui.Text);
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + Short(v), 6);
        NumField("money", "Custom amount", moneyAmount, 1, 1_000_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${Short(moneyAmount)}  [{Plugin.KeyMoney.Value}]", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${Short(moneyAmount)}", Ui.Tab)) RemoveMoney(moneyAmount);
        GUILayout.EndHorizontal();
        Ui.EndCard();

        var rep = ReputationManager.instance;
        Ui.BeginCard("Reputation", rep != null ? $"Level {rep.level}  ·  {rep.exp} / {rep.requiredExp} XP" : null);
        repAmount = Ui.Chips(repAmount, RepPresets, v => Short(v) + " XP", 4);
        if (Ui.Button($"+ {Short(repAmount)} Reputation XP", Ui.Primary)) AddReputation(repAmount);
        Ui.EndCard();
    }

    void DrawStation()
    {
        var st = Game.Station;
        Ui.BeginCard("Fuel", st != null ? $"Tank: {st.fuel:N0} / {st.maxFuel:N0} gal" : null);
        infFuel = Ui.Toggle(infFuel, "Infinite Fuel", "tank never runs empty");
        if (Ui.Button($"Fill Tank  [{Plugin.KeyFuel.Value}]", Ui.Primary)) FillFuel();
        Ui.EndCard();

        Ui.BeginCard("Business");
        Ui.Toggle2(ref workersEnergy, "Workers Never Tired", ref fastDelivery, "Fast Delivery");
        if (Ui.Button("Max All Upgrades", Ui.Primary)) MaxUpgrades();
        Ui.EndCard();
    }

    void DrawWorld()
    {
        string time = "";
        try { if (EnviroSkyMgr.instance != null) { float h = EnviroSkyMgr.instance.GetTimeOfDay(); time = $"{(int)h:00}:{(int)((h % 1f) * 60f):00}"; } } catch { }
        Ui.BeginCard("Time", time.Length > 0 ? $"Current time: {time}" : null);
        freezeTime = Ui.Toggle(freezeTime, "Freeze Time", "the clock stops");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Morning", Ui.Chip)) SetTime(8f);
        if (Ui.Button("Noon", Ui.Chip)) SetTime(12f);
        if (Ui.Button("Evening", Ui.Chip)) SetTime(18f);
        if (Ui.Button("Night", Ui.Chip)) SetTime(23f);
        GUILayout.EndHorizontal();
        Ui.EndCard();
    }

    void DrawItems()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }
        if (orders == null || Time.unscaledTime - ordersScannedAt > 30f) ScanOrders();

        Ui.BeginCard("Spawn Settings", "Items arrive in a delivery box");
        spawnAtCrosshair = Ui.Chips(spawnAtCrosshair, new[] { true, false }, v => v ? "At Crosshair" : "In Front of Me", 2);
        Ui.Label("Amount in the box", Ui.Sub);
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

        var filtered = orders.Where(e => (itemCategory == "All" || e.Category == itemCategory)
                                         && (search.Length == 0 || e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        int pages = Math.Max(1, (filtered.Count + PageSize - 1) / PageSize);
        itemPage = Mathf.Clamp(itemPage, 0, pages - 1);

        Ui.Label($"{filtered.Count} items", Ui.Sub);
        foreach (var e in filtered.Skip(itemPage * PageSize).Take(PageSize))
        {
            string label = $"{e.Name}   <size=12><color=#b8c2d6>{e.Category}</color></size>";
            if (Ui.Button(label, selectedOrder == e ? Ui.RowOn : Ui.Row)) { selectedOrder = e; searching = false; }
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

        string btn = selectedOrder != null ? $"SPAWN {itemAmount}x {selectedOrder.Name}" : "Select an item above";
        if (Ui.Button(btn, Ui.Primary) && selectedOrder != null) SpawnOrder(selectedOrder, itemAmount);
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
        KeyRow("Fill fuel tank", Plugin.KeyFuel);
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
        try { MoneyUI.instance.AddMoney(amount, EconomyManager.IncomeSubType.Sales, false); SetStatus($"+$ {amount:N0}"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void RemoveMoney(long amount)
    {
        if (!Check()) return;
        try { MoneyUI.instance.targetCount = Mathf.Max(0f, MoneyUI.instance.targetCount - amount); SetStatus($"−$ {amount:N0}"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void AddReputation(int amount)
    {
        if (!Check()) return;
        try { ReputationManager.instance.OnGainExp(amount); SetStatus($"+{amount:N0} reputation XP"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void FillFuel()
    {
        if (!Check()) return;
        var st = Game.Station;
        if (st == null) { SetStatus("No station found"); return; }
        st.fuel = st.maxFuel;
        SetStatus("Fuel tank filled");
    }

    void MaxUpgrades()
    {
        if (!Check()) return;
        var um = UpgradeManager.instance;
        if (um == null || um.upgrades == null) { SetStatus("Upgrades not found"); return; }
        int n = 0;
        foreach (var u in um.upgrades)
            if (u != null && u.level < u.maxLevel) { u.level = u.maxLevel; um.OnUpgraded(u); n++; }
        SetStatus(n > 0 ? $"Maxed {n} upgrade(s)" : "All upgrades are already maxed");
    }

    void SetTime(float hour)
    {
        if (!Check()) return;
        try { EnviroSkyMgr.instance.SetTimeOfDay(hour); SetStatus($"Time set to {hour:00}:00"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    // fly mode = the game's own developer free camera
    void SetFly(bool on)
    {
        if (!Check()) return;
        var p = Game.Player;
        var cam = p.GetComponent<Demo_FirstPersonCamera>();
        var ctrl = p.GetComponent<Demo_FirstPersonController>();
        var free = p.GetComponent<Demo_FreeCamera>();
        if (free == null) { SetStatus("Fly mode is not available here"); return; }
        if (cam != null) cam.enabled = !on;
        if (ctrl != null) ctrl.enabled = !on;
        free.enabled = on;
        flying = on;
        SetStatus(on ? "Fly mode on" : "Fly mode off");
    }

    // ---------------- item spawning (as delivery boxes, like the game's own orders) ----------------
    void ScanOrders()
    {
        ordersScannedAt = Time.unscaledTime;
        var om = OrderManager.instance;
        if (om == null || om.orderables == null) { orders = new List<OrderEntry>(); return; }
        var list = new List<OrderEntry>();
        foreach (var o in om.orderables)
        {
            if (o == null || (o.spawnObject == null && o.buildingPart == null)) continue;
            string name;
            try { name = o.localizedName; } catch { name = null; }
            if (string.IsNullOrEmpty(name)) name = o.name;
            list.Add(new OrderEntry { Order = o, Name = name, Category = string.IsNullOrEmpty(o.category) ? "Other" : o.category });
        }
        orders = list.OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
        categories = new[] { "All" }.Concat(orders.Select(e => e.Category).Distinct()).ToArray();
    }

    Vector3 SpawnPos()
    {
        var p = Game.Player;
        var cam = Camera.main;
        var fwd = cam != null ? cam.transform.forward : p.transform.forward;
        if (spawnAtCrosshair && cam != null && Physics.Raycast(cam.transform.position, fwd, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + hit.normal * 0.6f + Vector3.up * 0.4f;
        var f = fwd; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        return p.transform.position + f * 2f + Vector3.up * 1f;
    }

    void SpawnOrder(OrderEntry e, int amount)
    {
        if (!Check()) return;
        try
        {
            var o = e.Order;
            var pos = SpawnPos();
            var rot = Quaternion.Euler(0f, Game.Player.transform.eulerAngles.y, 0f);
            if (o.buildingPart != null || amount > 0)
            {
                var box = Instantiate(GameManager.instance.cardboardBoxObject, pos, rot);
                box.AddComponent<Box>().Initialize(o, amount);
                var pick = box.GetComponent<Pickable>();
                if (o.buildingPart != null)
                {
                    pick.buildingPart = o.buildingPart;
                    pick.pickableName = o.buildingPart.GetGeneralSettings.Name;
                }
                else
                {
                    pick.pickableName = o.name;
                    pick.pickableType = PickableType.Placeable;
                }
            }
            else Instantiate(o.spawnObject, pos, rot);
            SetStatus($"Spawned a box with {amount}x {e.Name}");
        }
        catch (Exception ex) { Plugin.L.LogError(ex); SetStatus("Could not spawn - see log"); }
    }
}
