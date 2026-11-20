using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ShiftAtMidnightCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Shift At Midnight", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.shiftatmidnight";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<KeyCode> KeyMenu, KeyGod, KeyFly, KeyEsp;
    internal static ConfigEntry<string> Theme;
    internal static ConfigEntry<float> MenuScale;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", KeyCode.F1, "Open/close menu");
        KeyGod = Config.Bind("Hotkeys", "GodMode", KeyCode.F2, "Toggle god mode");
        KeyFly = Config.Bind("Hotkeys", "Fly", KeyCode.F3, "Toggle fly mode (noclip)");
        KeyEsp = Config.Bind("Hotkeys", "DoppelgangerEsp", KeyCode.F4, "Show who is a doppelganger");
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

/// What the Harmony patches read. Only active while you play alone.
static class Cheat
{
    public static bool Allowed, God, Fly, FreeShop, OneHit;
    public static float FlySpeed = 2f, WeaponDamage = 1f;
    public static bool MenuOpen;
}

static class Patches
{
    static bool Mine(PlayerManager pm) => ClientPlayer.Instance != null && ClientPlayer.Instance.playerMan == pm;

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.TakeDamage))]
    static bool NoDamage(PlayerManager __instance) => !(Cheat.Allowed && Cheat.God && Mine(__instance));

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.Downed))]
    static bool NoDowned(PlayerManager __instance) => !(Cheat.Allowed && Cheat.God && Mine(__instance));

    [HarmonyPrefix, HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.PurchaseItem))]
    static void FreeShop(ref float cost) { if (Cheat.Allowed && Cheat.FreeShop) cost = 0f; }

    // one hit: monsters and customers go down with one hit
    [HarmonyPrefix, HarmonyPatch(typeof(Hittable), nameof(Hittable.Hit))]
    static void OneHit(Hittable __instance, ref float damage)
    {
        if (Cheat.Allowed && Cheat.OneHit) damage = Mathf.Max(damage, __instance.health + 1f);
    }

    // weapon damage multiplier (pistol, shotgun, melee)
    [HarmonyPrefix, HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.Shoot))]
    static void PistolDamage(ref float damage) { if (Cheat.Allowed) damage *= Cheat.WeaponDamage; }

    [HarmonyPrefix, HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.ShootShotgun))]
    static void ShotgunDamage(ref float damage) { if (Cheat.Allowed) damage *= Cheat.WeaponDamage; }

    [HarmonyPrefix, HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.MeleeAttack))]
    static void MeleeDamage(ref float damage) { if (Cheat.Allowed) damage *= Cheat.WeaponDamage; }

    // fly: replace the walking with free flight through walls
    [HarmonyPrefix, HarmonyPatch(typeof(FPSController), nameof(FPSController.Move))]
    static bool FlyMove(FPSController __instance)
    {
        if (!(Cheat.Allowed && Cheat.Fly) || !__instance.isLocalPlayer) return true;
        try
        {
            var cam = __instance.playerCamera != null ? __instance.playerCamera.transform : __instance.transform;
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
            __instance.moveDirection = Vector3.zero;
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
    public static ClientPlayer Me => ClientPlayer.Instance;
    public static bool InGame => Me != null && Me.playerMan != null && Me.fpsScript != null && StoreManager.Instance != null;

    /// Why the cheats are off right now (null = they are on).
    public static string Why
    {
        get
        {
            if (!InGame) return "Start a shift first.";
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
    static readonly string[] Tabs = { "Player", "Store", "Customers", "Monsters", "Weapons", "Items", "Teleport", "Settings" };

    // weapons (inventory ids of the game)
    const int Pistol = 2, Shotgun = 45, Flamethrower = 50;
    static readonly int[] WeaponIds = { Pistol, Shotgun, Flamethrower, 47, 48, 49 };
    const int Mop = 5, Flashlight = 4, Emotiscope = 11;
    static readonly int[] ToolIds = { Mop, Flashlight, Emotiscope };
    bool dropOnFloor;
    static readonly float[] MenuSizes = { 0.75f, 0.85f, 1f, 1.15f, 1.3f };
    static readonly float[] DamagePresets = { 1f, 2f, 5f, 10f, 100f };
    static readonly float[] FireRates = { 2f, 5f, 10f, 20f };
    bool infAmmo, noJam, rapidFire, autoFire;
    float autoFireRate = 5f, nextAutoShot;
    static readonly AccessTools.FieldRef<InventoryManager, bool> CanShoot = AccessTools.FieldRefAccess<InventoryManager, bool>("canShoot");
    static readonly AccessTools.FieldRef<InventoryManager, bool> CanAttack = AccessTools.FieldRefAccess<InventoryManager, bool>("canAttack");

    // player
    bool god, infStamina;
    float speedMult = 1f, jumpMult = 1f;
    float baseWalk = -1f, baseRun, baseCrouch, baseJump;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };

    // store
    long moneyAmount = 100;
    static readonly long[] MoneyPresets = { 50, 100, 500, 1_000, 10_000 };
    float gameSpeed = 1f;
    static readonly float[] GameSpeeds = { 0.5f, 1f, 2f, 3f, 5f };

    // customers / monsters
    bool esp, espMonsters = true;
    int customerPage;
    const int PageSize = 10;

    // items / npcs
    int itemAmount = 1, itemPage, npcAmount = 1, coinAmount = 10;
    static readonly int[] ItemAmounts = { 1, 3, 5, 10 };
    static readonly int[] CoinAmounts = { 1, 5, 10, 25 };
    GameObject selectedItem;
    Npc selectedNpc;

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
        god = Saved.Get("GodMode", false);
        infStamina = Saved.Get("InfiniteStamina", false);
        speedMult = Saved.Get("WalkSpeed", 1f);
        jumpMult = Saved.Get("JumpPower", 1f);
        Cheat.FlySpeed = Saved.Get("FlySpeed", 2f);
        Cheat.FreeShop = Saved.Get("FreeShop", false);
        Cheat.OneHit = Saved.Get("OneHit", false);
        esp = Saved.Get("DoppelgangerEsp", false);
        espMonsters = Saved.Get("MonsterEsp", true);
        moneyAmount = Saved.Get("MoneyAmount", 100L);
        infAmmo = Saved.Get("InfiniteAmmo", false);
        noJam = Saved.Get("NoGunJams", false);
        rapidFire = Saved.Get("NoCooldown", false);
        autoFire = Saved.Get("FullAuto", false);
        autoFireRate = Saved.Get("FullAutoRate", 5f);
        Cheat.WeaponDamage = Saved.Get("WeaponDamage", 1f);
    }

    void SaveSettings()
    {
        Saved.Set("GodMode", god);
        Saved.Set("InfiniteStamina", infStamina);
        Saved.Set("WalkSpeed", speedMult);
        Saved.Set("JumpPower", jumpMult);
        Saved.Set("FlySpeed", Cheat.FlySpeed);
        Saved.Set("FreeShop", Cheat.FreeShop);
        Saved.Set("OneHit", Cheat.OneHit);
        Saved.Set("DoppelgangerEsp", esp);
        Saved.Set("MonsterEsp", espMonsters);
        Saved.Set("MoneyAmount", moneyAmount);
        Saved.Set("InfiniteAmmo", infAmmo);
        Saved.Set("NoGunJams", noJam);
        Saved.Set("NoCooldown", rapidFire);
        Saved.Set("FullAuto", autoFire);
        Saved.Set("FullAutoRate", autoFireRate);
        Saved.Set("WeaponDamage", Cheat.WeaponDamage);
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
            if (Pressed(Plugin.KeyGod.Value)) { god = !god; SetStatus(god ? "God mode on" : "God mode off"); }
            if (Pressed(Plugin.KeyFly.Value)) SetFly(!Cheat.Fly);
            if (Pressed(Plugin.KeyEsp.Value)) { esp = !esp; SetStatus(esp ? "Doppelganger ESP on" : "Doppelganger ESP off"); }
        }

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        Cheat.Allowed = allowed;
        Cheat.God = god;
        if (!allowed || !Game.InGame) { Cheat.Fly = false; return; }

        var pm = Game.Me.playerMan;
        var fps = Game.Me.fpsScript;
        if (god) { if (pm.health < pm.maxHealth) pm.health = pm.maxHealth; }
        if (infStamina || Cheat.Fly) pm.stamina = pm.maxStamina;

        if (baseWalk < 0f) { baseWalk = fps.walkSpeed; baseRun = fps.runSpeed; baseCrouch = fps.crouchSpeed; baseJump = fps.jumpPower; }
        fps.walkSpeed = baseWalk * speedMult;
        fps.runSpeed = baseRun * speedMult;
        fps.crouchSpeed = baseCrouch * speedMult;
        fps.jumpPower = baseJump * jumpMult;

        WeaponTick(Game.Me.inventoryMan);

        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void TurnOff()
    {
        Cheat.Allowed = false;
        Cheat.Fly = false;
        RestoreMovement();
        if (Time.timeScale != 1f && gameSpeed != 1f) { Time.timeScale = 1f; gameSpeed = 1f; }
    }

    void RestoreMovement()
    {
        try
        {
            var fps = Game.Me != null ? Game.Me.fpsScript : null;
            if (fps != null && baseWalk > 0f) { fps.walkSpeed = baseWalk; fps.runSpeed = baseRun; fps.crouchSpeed = baseCrouch; fps.jumpPower = baseJump; }
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
    bool lockedByUs;
    void SetShow(bool value)
    {
        show = value;
        Cheat.MenuOpen = value;
        if (!show) { editId = null; rebinding = null; }
        try
        {
            var me = Game.Me;
            if (me == null) return;
            if (show)
            {
                // the same lock the game uses for its own menus
                if (!me.fpsScript.lockMove && !me.fpsScript.lockCam)
                {
                    me.fpsScript.lockMove = true;
                    me.fpsScript.lockCam = true;
                    me.playerMan.paused = true;
                    if (SpeakingManager.Instance != null) SpeakingManager.Instance.ignoreClicks = true;
                    lockedByUs = true;
                }
                me.fpsScript.UnlockCursor();
            }
            else if (lockedByUs)
            {
                me.fpsScript.lockMove = false;
                me.fpsScript.lockCam = false;
                me.playerMan.paused = false;
                if (SpeakingManager.Instance != null) SpeakingManager.Instance.ignoreClicks = false;
                me.fpsScript.LockCursor();
                lockedByUs = false;
            }
        }
        catch (Exception e) { Plugin.L.LogWarning(e.Message); }
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

    void ResetPages() { itemPage = 0; customerPage = 0; }

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
        try { if (allowed && Game.InGame) DrawEsp(); } catch { }
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
        Ui.Label($"Shift At Midnight  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyGod.Value.ToString(), "God");
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyEsp.Value.ToString(), "ESP");
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
        // fixed width: long names can't push the content past the right edge any more (they get cut inside their button)
        GUILayout.BeginVertical(GUILayout.Width(BodyWidth), GUILayout.MaxWidth(BodyWidth));
        bool locked = tab != Tabs.Length - 1 && !allowed;
        if (locked) GUI.enabled = false;
        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawStore(); break;
            case 2: DrawCustomers(); break;
            case 3: DrawMonsters(); break;
            case 4: DrawWeapons(); break;
            case 5: DrawItems(); break;
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

    static string Pretty(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("(Clone)", "").Replace('_', ' ').Trim();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(s[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------------- tabs ----------------
    void DrawPlayer()
    {
        Ui.BeginCard("Survival");
        god = Ui.Toggle(god, $"God Mode  [{Plugin.KeyGod.Value}]", "monsters can't hurt or down you");
        infStamina = Ui.Toggle(infStamina, "Infinite Stamina", "sprint forever");
        if (Ui.Button("Heal Now", Ui.Chip)) { if (Check()) { var pm = Game.Me.playerMan; pm.health = pm.maxHealth; SetStatus("Healed"); } }
        Ui.EndCard();

        Ui.BeginCard("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump power", Ui.Sub);
        jumpMult = Ui.Chips(jumpMult, JumpPresets, v => $"x{v:0.#}", 4);
        if (Ui.Button("Reset Movement", Ui.Chip)) { speedMult = 1f; jumpMult = 1f; }
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly through walls");
        bool f = Ui.Toggle(Cheat.Fly, $"Fly  [{Plugin.KeyFly.Value}]", "WASD, Space up, Ctrl down, Shift faster");
        if (f != Cheat.Fly) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        Cheat.FlySpeed = Ui.Chips(Cheat.FlySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();
    }

    void DrawStore()
    {
        float money = 0f; int tokens = 0, refreshes = 0;
        try { money = SaveManager.Instance.money; tokens = SaveManager.Instance.tokens; refreshes = SaveManager.Instance.refreshes; } catch { }

        Ui.BeginCard("Money", $"$ {money:N2}");
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + v.ToString("N0"), 5);
        NumField("money", "Custom amount", moneyAmount, 1, 1_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${moneyAmount:N0}", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${moneyAmount:N0}", Ui.Tab)) AddMoney(-moneyAmount);
        GUILayout.EndHorizontal();
        Cheat.FreeShop = Ui.Toggle(Cheat.FreeShop, "Free Shopping", "everything on the computer shop costs $0");
        Ui.EndCard();

        Ui.BeginCard("Shop & Tokens", $"{tokens} tokens  ·  {refreshes} shop refreshes");
        GUILayout.BeginHorizontal();
        if (Ui.Button("+ 10 Tokens", Ui.Chip)) Act("+10 tokens", () => StoreManager.Instance.ChangeTokenBalance(10));
        if (Ui.Button("+ 5 Shop Refreshes", Ui.Chip)) Act("+5 shop refreshes", () => PurchaseManager.Instance.AddRefreshes(5));
        GUILayout.EndHorizontal();
        if (Ui.Button("+1 Inventory Slot", Ui.Chip)) Act("+1 inventory slot", AddInventorySlot);
        Ui.EndCard();

        Ui.BeginCard("Shift");
        Ui.Label("Game speed", Ui.Sub);
        float gs = Ui.Chips(gameSpeed, GameSpeeds, v => $"x{v:0.#}", 5);
        if (gs != gameSpeed) { gameSpeed = gs; Time.timeScale = gs; SetStatus($"Game speed x{gs:0.#}"); }
        if (Ui.Button("Skip to End of Shift", Ui.Primary)) Act("Shift skipped - walk to the bus outside", SkipShift);
        Ui.Label("Tops your revenue up to today's goal, so the shift counts.", Ui.Sub);
        Ui.EndCard();

        int day = 0;
        try { day = CurrentDayManager.Instance.curDay; } catch { }
        Ui.BeginCard("Tutorial", day == 1 ? "You are in the tutorial (shift 1)" : "Only on the first shift");
        if (Ui.Button("Skip Tutorial", Ui.Primary))
        {
            if (day != 1) SetStatus("The tutorial is already over");
            else Act("Tutorial skipped - walk to the bus outside to start shift 2", SkipTutorial);
        }
        Ui.EndCard();
    }

    void DrawCustomers()
    {
        // compact block with a fixed height, so spawning NPCs never pushes the list below down
        var customers = FindObjectsOfType<StoreBrowseBehaviour>().Where(c => c != null && !c.leaving).ToList();
        int doppels = customers.Count(c => c.isDoppelganger);
        string summary = customers.Count == 0 ? "Nobody in the store"
            : $"{customers.Count} in the store  ·  <color=#50ff7a>{customers.Count - doppels} human</color>  ·  <color=#ff5050>{doppels} doppelganger</color>";
        Ui.BeginCard("Doppelgangers", summary);
        esp = Ui.Toggle(esp, $"Show Doppelgangers  [{Plugin.KeyEsp.Value}]", "green = human, red = doppelganger above their head");
        if (Ui.Button("Kill All Doppelgangers", Ui.Primary)) KillCustomers(true);
        Ui.EndCard();

        DrawNpcSpawner();

        // the detailed list goes last, opened on demand
        Ui.Space(6);
        if (Ui.Button(showCustomerList ? "Hide Who Is in the Store" : $"Show Who Is in the Store ({customers.Count})", Ui.Chip)) showCustomerList = !showCustomerList;
        if (showCustomerList)
        {
            Ui.BeginCard("In the Store Now");
            if (customers.Count == 0) Ui.Label("Nobody here.", Ui.Sub);
            foreach (var c in customers)
            {
                Ui.Label($"<b>{Identity(c)}</b>   {WhoTheyClaim(c)}", Ui.Text);
            }
            Ui.EndCard();
        }
    }

    bool showCustomerList;

    void DrawNpcSpawner()
    {
        var all = AllNpcs().Where(n => Matches(Pretty(n.id))).ToList();
        Ui.BeginCard("Spawn NPCs", "Every customer and doppelganger of the game");
        Ui.Label("Amount", Ui.Sub);
        npcAmount = Ui.Chips(npcAmount, ItemAmounts, v => v + "x", 4);
        SearchBar();
        Ui.Label($"{all.Count} found", Ui.Sub);
        int pages = Math.Max(1, (all.Count + PageSize - 1) / PageSize);
        customerPage = Mathf.Clamp(customerPage, 0, pages - 1);
        foreach (var n in all.Skip(customerPage * PageSize).Take(PageSize))
            if (Ui.Button($"<b>{Pretty(n.id)}</b>" + Grey(n.isDoppelganger ? "<color=#ff7070>doppelganger</color>" : "human"), selectedNpc == n ? Ui.RowOn : Ui.Row)) { selectedNpc = n; searching = false; }
        Pager(ref customerPage, pages);
        Ui.EndCard();

        bool has = selectedNpc != null;
        string name = has ? Pretty(selectedNpc.id) : "";
        if (has) Ui.Label($"Selected: <b>{name}</b>" + (selectedNpc.isDoppelganger ? "  <color=#ff7070>(doppelganger)</color>" : ""), Ui.Text);
        if (Ui.Button(has ? $"SPAWN {npcAmount}x AT THE ENTRANCE" : "Select an NPC above", Ui.Primary) && has) SpawnNpc(selectedNpc, npcAmount, false);
        Ui.Space(4);
        GUILayout.BeginHorizontal();
        if (Ui.Button("Spawn in Front of Me", Ui.Tab) && has) SpawnNpc(selectedNpc, npcAmount, true);
        if (Ui.Button("Call as Next Customer", Ui.Tab) && has) CallCustomer(selectedNpc);
        GUILayout.EndHorizontal();
    }

    void DrawMonsters()
    {
        bool hunt = false;
        try { hunt = StoreManager.Instance.inHunt; } catch { }
        Ui.BeginCard("Combat");
        Cheat.OneHit = Ui.Toggle(Cheat.OneHit, "One Hit Kill", "monsters and customers die with one hit");
        espMonsters = Ui.Toggle(espMonsters, "Monster ESP", "see every monster through walls");
        Ui.EndCard();

        int count = 0;
        try { count = HuntManager.Instance != null ? HuntManager.Instance.allEnemies.Count(e => e != null) : 0; } catch { }
        Ui.BeginCard("Monsters", hunt ? $"Hunt is on - {count} monsters" : $"{count} monsters");
        if (Ui.Button("Kill All Monsters", Ui.Primary)) KillMonsters();
        Ui.EndCard();
    }

    // ---------------- weapons ----------------
    void WeaponTick(InventoryManager inv)
    {
        if (inv == null) return;
        if (noJam && inv.gunJammed) inv.gunJammed = false;
        int slot = inv.curInventorySlot;
        int held = inv.holdingIndex;
        bool gun = held == Pistol || held == Shotgun || held == Flamethrower;
        if (!gun || slot < 0 || inv.crateStorages == null || slot >= inv.crateStorages.Length) return;

        if (infAmmo)
        {
            int max = held == Pistol ? inv.pistolBulletIcons.Length : held == Shotgun ? inv.shotgunBulletIcons.Length : 999;
            if (inv.crateStorages[slot] < max)
            {
                inv.crateStorages[slot] = max;
                try
                {
                    var icons = held == Pistol ? inv.pistolBulletIcons : held == Shotgun ? inv.shotgunBulletIcons : null;
                    if (icons != null) foreach (var i in icons) if (i != null) i.SetActive(true);
                    StoreManager.Instance.rToReload.SetActive(false);
                    StoreManager.Instance.noMoreAmmo.SetActive(false);
                }
                catch { }
            }
        }
        if (rapidFire) { CanShoot(inv) = true; CanAttack(inv) = true; }

        // full auto for pistol and shotgun: hold the left mouse button
        if (autoFire && !show && (held == Pistol || held == Shotgun) && Input.GetKey(KeyCode.Mouse0) && !Input.GetKeyDown(KeyCode.Mouse0)
            && Time.time >= nextAutoShot && inv.crateStorages[slot] > 0 && !inv.gunJammed)
        {
            nextAutoShot = Time.time + 1f / autoFireRate;
            try
            {
                if (!infAmmo) inv.crateStorages[slot]--;
                if (held == Pistol) { inv.Shoot(50f); inv.pistolAnim.SetTrigger("Shoot"); }
                else { inv.ShootShotgun(15f); inv.shotgunAnim.SetTrigger("Shoot"); }
            }
            catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        }
    }

    static string ItemName(InventoryManager inv, int id)
    {
        try
        {
            if (inv != null && inv.objSprites != null && id >= 0 && id < inv.objSprites.Length && inv.objSprites[id] != null)
                return Pretty(inv.objSprites[id].name);
        }
        catch { }
        switch (id)
        {
            case Pistol: return "Pistol"; case Shotgun: return "Shotgun"; case Flamethrower: return "Flamethrower";
            case Mop: return "Mop"; case Flashlight: return "Flashlight"; case Emotiscope: return "Emotiscope";
        }
        return $"Item {id}";
    }

    void GiveItem(int id)
    {
        if (!Check()) return;
        var inv = Game.Me.inventoryMan;
        bool room = false;
        for (int i = 0; i < inv.maxInventorySlots; i++)
            if (inv.inventoryIds[i] == -1 || (inv.inventoryIds[i] == id && inv.inventoryAmounts[i] < inv.maxStack[id])) { room = true; break; }
        if (!room) { SetStatus("Your inventory is full - drop something first (or +1 Inventory Slot in Store)"); return; }
        int ammo = id == Pistol ? inv.pistolBulletIcons.Length : id == Shotgun ? inv.shotgunBulletIcons.Length : id == Flamethrower ? 300 : 6;
        try { inv.PickupNewObj(id, ammo); SetStatus($"{ItemName(inv, id)} is in your hands"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not give " + ItemName(inv, id)); }
    }

    /// The real pickup object of an item (the one lying in the store), as a prefab we can spawn.
    static GameObject PickupPrefab(int id)
    {
        GameObject sceneCopy = null;
        foreach (var p in Resources.FindObjectsOfTypeAll<PickupObject>())
        {
            if (p == null || p.objectIndex != id || p.GetComponent<NetworkIdentity>() == null) continue;
            if (!p.gameObject.scene.IsValid()) return p.gameObject;   // a real prefab
            if (sceneCopy == null) sceneCopy = p.gameObject;
        }
        foreach (var o in new List<GameObject>(SaveManager.Instance?.instantiablesAtlas ?? new GameObject[0]))
        {
            var p = o != null ? o.GetComponent<PickupObject>() : null;
            if (p != null && p.objectIndex == id) return o;
        }
        return null;
    }

    void SpawnPickup(int id)
    {
        if (!Check()) return;
        var inv = Game.Me.inventoryMan;
        var prefab = PickupPrefab(id);
        if (prefab == null) { GiveItem(id); return; }   // no loose version of it: put it in your hands instead
        try
        {
            var cam = Game.Me.fpsScript.playerCamera.transform;
            var go = Instantiate(prefab, cam.position + cam.forward * 1.5f, Quaternion.identity);
            go.SetActive(true);
            NetworkServer.Spawn(go);
            SetStatus($"{ItemName(inv, id)} spawned in front of you");
        }
        catch (Exception e) { Plugin.L.LogError(e); GiveItem(id); }
    }

    void DrawWeapons()
    {
        var inv = Game.Me != null ? Game.Me.inventoryMan : null;
        Ui.BeginCard("Get Weapons & Tools", "Pick where it goes, then click what you want");
        dropOnFloor = Ui.Chips(dropOnFloor ? 1 : 0, new[] { 0, 1 }, v => v == 0 ? "Into My Hands" : "Spawn in Front of Me", 2) == 1;
        Ui.Space(4);
        foreach (var id in WeaponIds.Concat(ToolIds))
        {
            string kind = id == Pistol || id == Shotgun || id == Flamethrower ? "gun" : WeaponIds.Contains(id) ? "melee" : "tool";
            if (Ui.Button($"<b>{ItemName(inv, id)}</b>" + Grey(kind), Ui.Row)) { if (dropOnFloor) SpawnPickup(id); else GiveItem(id); }
        }
        Ui.EndCard();

        Ui.BeginCard("Weapon Mods");
        infAmmo = Ui.Toggle(infAmmo, "Infinite Ammo", "never reload - pistol, shotgun and flamethrower");
        noJam = Ui.Toggle(noJam, "No Gun Jams", "the gun never jams");
        rapidFire = Ui.Toggle(rapidFire, "No Cooldown", "shoot and swing as fast as you click");
        autoFire = Ui.Toggle(autoFire, "Full Auto", "hold the left mouse button with the pistol or shotgun");
        if (autoFire)
        {
            Ui.Label("Shots per second", Ui.Sub);
            autoFireRate = Ui.Chips(autoFireRate, FireRates, v => $"{v:0}", 4);
        }
        Ui.Label("Weapon damage", Ui.Sub);
        Cheat.WeaponDamage = Ui.Chips(Cheat.WeaponDamage, DamagePresets, v => $"x{v:0}", 5);
        Ui.EndCard();
    }

    void DrawItems()
    {
        Ui.BeginCard("Coins", "Real coins - pick them up like the ones the game hides");
        coinAmount = Ui.Chips(coinAmount, CoinAmounts, v => v + "x", 4);
        NumField("coins", "Custom amount", coinAmount, 1, 200, v => coinAmount = (int)v);
        if (Ui.Button($"Spawn {coinAmount} Coins in Front of Me", Ui.Primary)) SpawnCoins(coinAmount);
        Ui.EndCard();

        var objs = SpawnList().Where(o => Matches(Pretty(o.name))).ToList();
        Ui.BeginCard("Spawn Items", "Delivery boxes and items from the shop - they drop in front of you");
        Ui.Label("Amount", Ui.Sub);
        itemAmount = Ui.Chips(itemAmount, ItemAmounts, v => v + "x", 4);
        SearchBar();
        Ui.Label($"{objs.Count} found", Ui.Sub);
        int pages = Math.Max(1, (objs.Count + PageSize - 1) / PageSize);
        itemPage = Mathf.Clamp(itemPage, 0, pages - 1);
        foreach (var o in objs.Skip(itemPage * PageSize).Take(PageSize))
            if (Ui.Button($"<b>{Pretty(o.name)}</b>", selectedItem == o ? Ui.RowOn : Ui.Row)) { selectedItem = o; searching = false; }
        Pager(ref itemPage, pages);
        Ui.EndCard();

        string btn = selectedItem != null ? $"SPAWN {itemAmount}x {Pretty(selectedItem.name)}" : "Select an item above";
        if (Ui.Button(btn, Ui.Primary) && selectedItem != null) SpawnObject(selectedItem, itemAmount);
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
        KeyRow("God mode", Plugin.KeyGod);
        KeyRow("Fly mode", Plugin.KeyFly);
        KeyRow("Doppelganger ESP", Plugin.KeyEsp);
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
    void DrawEsp()
    {
        if (!esp && !espMonsters) return;
        var cam = Game.Me.fpsScript.playerCamera;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        if (espStyle == null || espBg == null)
        {
            // own styles (not the game's skin), so the tags look the same everywhere
            espStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 20, richText = true };
            espStyle.normal.textColor = Color.white;
            espSubStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 14, richText = true };
            espSubStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            espBg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            espBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f)); espBg.Apply();
        }

        if (esp)
            foreach (var c in FindObjectsOfType<StoreBrowseBehaviour>())
                if (c != null && c.gameObject.activeInHierarchy) Label3D(cam, TopOf(c.gameObject, c.head), Identity(c), WhoTheyClaim(c));
        if (espMonsters && HuntManager.Instance != null && HuntManager.Instance.allEnemies != null)
            foreach (var e in HuntManager.Instance.allEnemies)
                if (e != null && e.gameObject.activeInHierarchy) Label3D(cam, TopOf(e.gameObject, null), "<color=#ff8c1a>MONSTER</color>", Pretty(e.gameObject.name));
    }

    /// A point just above the head (or the top of the model).
    static Vector3 TopOf(GameObject go, Transform head)
    {
        if (head != null) return head.position + Vector3.up * 0.45f;
        float top = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            if (r != null && r.enabled && (r is SkinnedMeshRenderer || r is MeshRenderer)) top = Mathf.Max(top, r.bounds.max.y);
        var p = go.transform.position;
        return new Vector3(p.x, top > float.MinValue && top < p.y + 6f ? top + 0.35f : p.y + 2.2f, p.z);
    }

    /// What the customer really is.
    static string Identity(StoreBrowseBehaviour c)
    {
        var h = c.hittable;
        bool monster = h != null && (h.enemy != null || h.chaseScript != null);
        if (c.isDoppelganger) return "<color=#ff4040>DOPPELGANGER</color>";
        if (monster) return "<color=#ff8c1a>MONSTER</color>";
        if (c.isThief) return "<color=#ffd23f>THIEF</color>";
        return "<color=#40ff70>HUMAN</color>";
    }

    /// The name on their ID / the person they say they are.
    static string WhoTheyClaim(StoreBrowseBehaviour c)
    {
        string name = !string.IsNullOrEmpty(c.idDatabaseName) ? c.idDatabaseName : c.gameObject.name;
        name = Pretty(name);
        if (string.IsNullOrEmpty(name)) return null;
        return c.isDoppelganger ? $"pretends to be {name}" : name;
    }

    void Label3D(Camera cam, Vector3 world, string text, string sub = null)
    {
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0.2f || sp.z > 120f) return;
        // a dark tag above the head: big coloured word, the name and distance below it
        string line2 = (string.IsNullOrEmpty(sub) ? "" : sub + "  ·  ") + $"{sp.z:0} m";
        var s1 = espStyle.CalcSize(new GUIContent(text));
        var s2 = espSubStyle.CalcSize(new GUIContent(line2));
        float w = Mathf.Max(s1.x, s2.x) + 18f, h = s1.y + s2.y + 6f;
        var box = new Rect(sp.x - w / 2f, Screen.height - sp.y - h, w, h);
        GUI.DrawTexture(box, espBg);
        GUI.Label(new Rect(box.x, box.y + 2f, w, s1.y), text, espStyle);
        GUI.Label(new Rect(box.x, box.y + 2f + s1.y, w, s2.y), line2, espSubStyle);
    }

    Texture2D espBg;

    // ---------------- actions ----------------
    bool Check()
    {
        if (!Game.InGame) { SetStatus("Start a shift first"); return false; }
        if (!allowed) { SetStatus("Cheats are off while other players are here"); return false; }
        return true;
    }

    void Act(string done, Action a)
    {
        if (!Check()) return;
        try { a(); SetStatus(done); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("That did not work: " + e.Message); }
    }

    void AddMoney(long amount)
    {
        Act($"{(amount >= 0 ? "+" : "−")}$ {Math.Abs(amount):N0}", () =>
        {
            StoreManager.Instance.ChangeRevenue("Cheats", amount);
            try { PurchaseManager.Instance.LoadTotalBalance(); } catch { }
        });
    }

    // end the shift: make sure today's money goal is reached (or the shift does not count) and call the bus
    void SkipShift()
    {
        var eod = EODReportValues.Instance;
        if (eod != null)
        {
            float missing = eod.mandatoryRevenue - (eod.todayMoneyGained - eod.todayMoneyLost);
            if (missing > 0f) StoreManager.Instance.ChangeRevenue("Cheats", Mathf.Ceil(missing) + 1f);
        }
        EventManager.Instance.EODBus();
    }

    void SkipTutorial()
    {
        // close every tutorial step and hint, then end shift 1
        if (TutorialManager.Instance != null)
        {
            var t = TutorialManager.Instance;
            t.alreadyDone = true;
            t.finishedObjectives = true;
            try { if (t.objectiveObjs != null) foreach (var o in t.objectiveObjs) if (o != null) o.SetActive(false); } catch { }
            try { if (t.tutorialObjCanvas != null) t.tutorialObjCanvas.SetActive(false); } catch { }
            try { if (t.tutorialObjCanvasHolder != null) t.tutorialObjCanvasHolder.SetActive(false); } catch { }
            try { if (t.tutorialObjCanvasHolderAsWell != null) t.tutorialObjCanvasHolderAsWell.SetActive(false); } catch { }
            try { if (t.instructions != null) t.instructions.SetActive(false); } catch { }
            try { if (t.outlines != null) foreach (var o in t.outlines) if (o != null) o.enabled = false; } catch { }
        }
        if (DialogueTutorialManager.Instance != null) DialogueTutorialManager.Instance.alreadyDone = true;
        SkipShift();
    }

    void AddInventorySlot()
    {
        var sm = SaveManager.Instance;
        sm.NetworkmaxInventorySpace = sm.maxInventorySpace + 1;
        foreach (var inv in FindObjectsOfType<InventoryManager>(true))
            inv.SetMaxInventorySlots(sm.maxInventorySpace);
    }

    void KillCustomers(bool onlyDoppels)
    {
        if (!Check()) return;
        int n = 0;
        foreach (var h in FindObjectsOfType<Hittable>())
        {
            if (h == null || h.die || h.browseScript == null) continue;
            if (onlyDoppels && !h.browseScript.isDoppelganger) continue;
            try { h.Hit(h.health + 1000f, Game.Me.transform.position, true); n++; } catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        }
        SetStatus(n == 0 ? "No doppelgangers in the store" : $"Killed {n} doppelganger{(n == 1 ? "" : "s")}");
    }

    void KillMonsters()
    {
        if (!Check()) return;
        int n = 0;
        var list = new List<Hittable>();
        try
        {
            if (HuntManager.Instance != null)
                foreach (var e in HuntManager.Instance.allEnemies)
                    if (e != null) { var h = e.GetComponentInChildren<Hittable>() ?? e.GetComponentInParent<Hittable>(); if (h != null) list.Add(h); }
        }
        catch { }
        foreach (var h in FindObjectsOfType<Hittable>())
            if (h != null && h.enemy != null && !list.Contains(h)) list.Add(h);
        foreach (var h in list)
        {
            if (h.die) continue;
            try { h.Hit(h.health + 1000f, Game.Me.transform.position, true); n++; } catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        }
        SetStatus(n == 0 ? "No monsters found" : $"Hit {n} monster{(n == 1 ? "" : "s")}");
    }

    void CallCustomer(Npc npc)
    {
        Act($"{Pretty(npc.id)} comes in next", () =>
        {
            var dm = CurrentDayManager.Instance;
            dm.listOfOccurrences.Insert(Mathf.Clamp(dm.curOccurrence, 0, dm.listOfOccurrences.Count), "NPC");
            dm.todaysNpcs.Insert(Mathf.Clamp(dm.npcIndex, 0, dm.todaysNpcs.Count), npc);
        });
    }

    List<Npc> npcCache;
    float npcCacheAt = -100f;
    List<Npc> AllNpcs()
    {
        if (npcCache != null && Time.unscaledTime - npcCacheAt < 10f) return npcCache;
        npcCacheAt = Time.unscaledTime;
        var list = new List<Npc>();
        try { var gen = CurrentDayManager.Instance?.customerGenManager; if (gen != null && gen.allRandomSpawningNpcs != null) list.AddRange(gen.allRandomSpawningNpcs); } catch { }
        try { list.AddRange(Resources.FindObjectsOfTypeAll<Npc>()); } catch { }
        npcCache = list.Where(n => n != null && n.prefab != null && !string.IsNullOrEmpty(n.id))
                       .GroupBy(n => n.id.ToLowerInvariant()).Select(g => g.First())
                       .OrderBy(n => n.isDoppelganger).ThenBy(n => Pretty(n.id)).ToList();
        return npcCache;
    }

    void SpawnNpc(Npc npc, int amount, bool inFront)
    {
        if (!Check()) return;
        try
        {
            var cam = Game.Me.fpsScript.playerCamera.transform;
            for (int i = 0; i < amount; i++)
            {
                Vector3 pos;
                if (inFront)
                {
                    var fwd = cam.forward; fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f) fwd = Game.Me.transform.forward;
                    pos = Game.Me.transform.position + fwd.normalized * 2.5f + Game.Me.transform.right * (i - (amount - 1) / 2f) * 1.2f;
                }
                else pos = StoreManager.Instance.npcSpawnPoint.position + Vector3.right * (i * 1.2f);
                NetworkServer.Spawn(Instantiate(npc.prefab, pos, Quaternion.identity));
            }
            SetStatus($"Spawned {amount}x {Pretty(npc.id)}{(npc.isDoppelganger ? " (doppelganger)" : "")}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not spawn " + Pretty(npc.id)); }
    }

    void SpawnCoins(int amount)
    {
        if (!Check()) return;
        try
        {
            var prefab = StoreManager.Instance.coin;
            if (prefab == null) { SetStatus("Coins not found"); return; }
            var cam = Game.Me.fpsScript.playerCamera.transform;
            for (int i = 0; i < amount; i++)
            {
                var pos = cam.position + cam.forward * 1.5f + UnityEngine.Random.insideUnitSphere * 0.4f;
                NetworkServer.Spawn(Instantiate(prefab, pos, Quaternion.identity));
            }
            SetStatus($"Spawned {amount} coins");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not spawn coins"); }
    }

    List<GameObject> spawnCache;
    float spawnCacheAt = -100f;
    List<GameObject> SpawnList()
    {
        if (spawnCache != null && Time.unscaledTime - spawnCacheAt < 10f) return spawnCache;
        spawnCacheAt = Time.unscaledTime;
        var set = new List<GameObject>();
        try
        {
            if (PurchaseManager.Instance != null && PurchaseManager.Instance.purchaseObjects != null)
                foreach (var o in PurchaseManager.Instance.purchaseObjects) if (o != null && !set.Contains(o)) set.Add(o);
            if (SaveManager.Instance != null && SaveManager.Instance.instantiablesAtlas != null)
                foreach (var o in SaveManager.Instance.instantiablesAtlas) if (o != null && !set.Contains(o)) set.Add(o);
        }
        catch { }
        spawnCache = set.Where(o => o.GetComponent<NetworkIdentity>() != null)
                        .GroupBy(o => Pretty(o.name)).Select(g => g.First())
                        .OrderBy(o => Pretty(o.name)).ToList();
        return spawnCache;
    }

    void SpawnObject(GameObject prefab, int amount)
    {
        if (!Check()) return;
        try
        {
            var cam = Game.Me.fpsScript.playerCamera.transform;
            for (int i = 0; i < amount; i++)
            {
                var pos = cam.position + cam.forward * 1.6f + Vector3.up * (0.3f * i);
                var go = Instantiate(prefab, pos, Quaternion.identity);
                NetworkServer.Spawn(go);
            }
            SetStatus($"Spawned {amount}x {Pretty(prefab.name)}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not spawn " + Pretty(prefab.name)); }
    }

    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        var list = new List<Place>();
        try
        {
            var tp = StoreTravelPoints.Instance;
            if (tp != null)
            {
                if (tp.checkoutPoint != null) list.Add(new Place { Name = "Checkout", Group = "Store", Pos = tp.checkoutPoint.position });
                if (tp.exitPoint != null) list.Add(new Place { Name = "Store Exit", Group = "Store", Pos = tp.exitPoint.position });
            }
            var sm = StoreManager.Instance;
            if (sm != null)
            {
                if (sm.npcSpawnPoint != null) list.Add(new Place { Name = "Customer Entrance", Group = "Outside", Pos = sm.npcSpawnPoint.position });
                if (sm.carSpawnPoint != null) list.Add(new Place { Name = "Road", Group = "Outside", Pos = sm.carSpawnPoint.position });
            }
            var hm = HuntManager.Instance;
            if (hm != null)
            {
                if (hm.leavePoint != null) list.Add(new Place { Name = "Monster Exit", Group = "Hunt", Pos = hm.leavePoint.position });
            }
            // the dumpster behind the store: stand next to it, on the side facing the store
            var dumpsters = FindObjectsOfType<Dumpster>();
            for (int i = 0; i < dumpsters.Length; i++)
            {
                var d = dumpsters[i].transform.position;
                Vector3 toward = tp != null && tp.checkoutPoint != null ? tp.checkoutPoint.position - d : -dumpsters[i].transform.forward;
                toward.y = 0f;
                if (toward.sqrMagnitude < 0.01f) toward = Vector3.forward;
                list.Insert(Math.Min(4, list.Count), new Place { Name = dumpsters.Length > 1 ? $"Trash Bin {i + 1}" : "Trash Bin (back)", Group = "Outside", Pos = d + toward.normalized * 2.5f });
            }
            if (hm != null)
            {
                if (hm.allVents != null)
                    for (int i = 0; i < hm.allVents.Length; i++)
                        if (hm.allVents[i] != null) list.Add(new Place { Name = $"Vent {i + 1}", Group = "Vents", Pos = hm.allVents[i].position });
            }
        }
        catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        places = list;
    }

    void TeleportTo(Vector3 pos, string name)
    {
        if (!Check()) return;
        var me = Game.Me;
        var target = pos + Vector3.up * 1.2f;
        var cc = me.GetComponent<CharacterController>();
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        me.transform.position = target;
        if (cc != null) cc.enabled = was;
        me.fpsScript.moveDirection = Vector3.zero;
        SetStatus("Teleported to " + name);
    }
}
