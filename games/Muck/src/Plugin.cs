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

namespace MuckCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Muck", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.muck";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<KeyCode> KeyMenu, KeyGod, KeyFly;
    internal static ConfigEntry<string> Theme;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", KeyCode.F1, "Open/close menu");
        KeyGod = Config.Bind("Hotkeys", "GodMode", KeyCode.F2, "Toggle god mode");
        KeyFly = Config.Bind("Hotkeys", "Fly", KeyCode.F3, "Toggle fly mode (noclip)");
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

/// What the Harmony patches read. Only active while you play alone.
static class Cheat
{
    public static bool Allowed;
    public static bool God, OneHit, InstantGather;
    public static float Speed = 1f, Jump = 1f, Damage = 1f, AttackSpeed = 1f, Gather = 1f;
}

static class Patches
{
    // while our menu is open the game treats it like its own menus: no walking, no attacking
    [HarmonyPostfix, HarmonyPatch(typeof(OtherInput), nameof(OtherInput.OtherUiActive))]
    static void MenuBlocksInput(ref bool __result) { if (CheatMenu.MenuOpen) __result = true; }

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerStatus), "HandleDamage")]
    static bool NoDamage() => !(Cheat.Allowed && Cheat.God);

    // the multipliers below are only touched for your own player (the game passes null for the local player)
    [HarmonyPostfix, HarmonyPatch(typeof(PowerupInventory), nameof(PowerupInventory.GetSpeedMultiplier))]
    static void Speed(int[] playerPowerups, ref float __result) { if (Cheat.Allowed && playerPowerups == null) __result *= Cheat.Speed; }

    [HarmonyPostfix, HarmonyPatch(typeof(PowerupInventory), nameof(PowerupInventory.GetJumpMultiplier))]
    static void Jump(ref float __result) { if (Cheat.Allowed) __result *= Cheat.Jump; }

    [HarmonyPostfix, HarmonyPatch(typeof(PowerupInventory), nameof(PowerupInventory.GetStrengthMultiplier))]
    static void Strength(int[] playerPowerups, ref float __result) { if (Cheat.Allowed && playerPowerups == null) __result *= Cheat.Damage; }

    [HarmonyPostfix, HarmonyPatch(typeof(PowerupInventory), nameof(PowerupInventory.GetAttackSpeedMultiplier))]
    static void AttackSpeed(int[] playerPowerups, ref float __result) { if (Cheat.Allowed && playerPowerups == null) __result *= Cheat.AttackSpeed; }

    [HarmonyPostfix, HarmonyPatch(typeof(PowerupInventory), nameof(PowerupInventory.GetResourceMultiplier))]
    static void Gather(int[] playerPowerups, ref float __result) { if (Cheat.Allowed && playerPowerups == null) __result *= Cheat.Gather; }

    // instant gather / one hit: trees, rocks and ores break with one hit - with any tool
    [HarmonyPrefix, HarmonyPatch(typeof(HitableResource), nameof(HitableResource.Hit))]
    static void OneHitResource(HitableResource __instance, ref int damage)
    {
        if (!(Cheat.Allowed && (Cheat.InstantGather || Cheat.OneHit))) return;
        try { damage = Math.Max(Math.Max(damage, __instance.hp), 1); } catch { }
    }

    // one hit kill: skip the boss damage cap and send a huge hit
    [HarmonyPrefix, HarmonyPatch(typeof(HitableMob), nameof(HitableMob.Hit))]
    static bool OneHit(HitableMob __instance, float sharpness, int hitEffect, Vector3 hitPos, int hitWeaponType)
    {
        if (!(Cheat.Allowed && Cheat.OneHit)) return true;
        try
        {
            var mob = __instance.GetComponent<Mob>();
            if (mob == null) return true;
            ClientSend.PlayerDamageMob(mob.id, 1_000_000, sharpness, hitEffect, hitPos, hitWeaponType);
            return false;
        }
        catch { return true; }
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
    public static PlayerMovement Me => PlayerMovement.Instance;
    public static bool InGame => Me != null && PlayerStatus.Instance != null && LocalClient.instance != null
                                 && GameManager.instance != null && GameManager.state != GameManager.GameState.Loading;

    /// Other players in the game (destroyed leftovers don't count).
    public static int OtherPlayers
    {
        get
        {
            if (GameManager.players == null || LocalClient.instance == null) return 0;
            int n = 0;
            foreach (var kv in GameManager.players)
                if (kv.Value != null && kv.Key != LocalClient.instance.myId) n++;
            return n;
        }
    }

    /// Why the cheats are off right now (null = they are on).
    public static string Why
    {
        get
        {
            if (!InGame) return "Start a game first.";
            if (!LocalClient.serverOwner) return "You are not the host of this game.";
            int others = OtherPlayers;
            if (others > 0) return $"{others} other player{(others == 1 ? " is" : "s are")} in this game.";
            return null;
        }
    }

    /// Cheats only work when you host the game and nobody else is in it.
    public static bool Allowed => Why == null;

    public static Vector3 Ground(Vector3 p)
    {
        var mask = GameManager.instance != null ? (int)GameManager.instance.whatIsGround : ~0;
        if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out var hit, 200f, mask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return p;
    }
}

public class CheatMenu : MonoBehaviour
{
    bool show;
    public static bool MenuOpen;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Combat", "Items", "Powerups", "Mobs", "World", "Teleport", "Settings" };

    // player
    bool god, infStamina, noHunger, flying;
    float flySpeed = 2f;
    static readonly float[] JumpPresets = { 1f, 1.5f, 2f, 3f };
    static readonly float[] FlySpeedPresets = { 1f, 2f, 4f, 8f };

    // combat
    static readonly float[] DamagePresets = { 1f, 2f, 5f, 10f, 100f };
    static readonly float[] AttackSpeedPresets = { 1f, 1.5f, 2f, 3f };
    bool instantGather;

    // items
    string itemCategory = "All", search = "";
    static readonly string[] ItemCategories = { "All", "Resources", "Tools", "Weapons", "Armor", "Food", "Buildings" };
    bool searching, searchAllSelected;
    int searchFieldClickFrame = -1, searchCloseCheckFrame = -1;
    int itemPage, itemAmount = 1, coinAmount = 1000;
    InventoryItem selectedItem;
    static readonly int[] AmountPresets = { 1, 10, 50, 100 };
    static readonly int[] CoinPresets = { 100, 1000, 10_000, 100_000 };

    // powerups
    int powerupAmount = 1;
    static readonly int[] PowerupAmounts = { 1, 5, 10, 25 };
    string powerupTier = "All";
    static readonly string[] PowerupTiers = { "All", "White", "Blue", "Orange" };

    // world
    bool alwaysDay;
    MobType selectedMob;
    int mobAmount = 1;
    static readonly int[] MobAmounts = { 1, 3, 5, 10 };
    float mobDistance = 10f;
    static readonly float[] MobDistances = { 5f, 10f, 20f, 40f };
    string mobFilter = "All";
    static readonly string[] MobFilters = { "All", "Mobs", "Bosses" };

    // teleport
    class Place { public string Name, Group; public Transform At; }
    List<Place> places = new List<Place>();
    float placesScannedAt = -100f;
    bool hasSaved;
    Vector3 savedPos;
    int placePage;
    const int PageSize = 10;

    // allowed check (once per second)
    bool allowed;
    string lastWhy = "";
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
        god = Saved.Get("GodMode", false);
        infStamina = Saved.Get("InfiniteStamina", false);
        noHunger = Saved.Get("NoHunger", false);
        Cheat.Speed = Saved.Get("WalkSpeed", 1f);
        Cheat.Jump = Saved.Get("JumpPower", 1f);
        flySpeed = Saved.Get("FlySpeed", 2f);
        Cheat.Damage = Saved.Get("Damage", 1f);
        Cheat.AttackSpeed = Saved.Get("AttackSpeed", 1f);
        Cheat.OneHit = Saved.Get("OneHitKill", false);
        instantGather = Saved.Get("InstantGather", false);
        alwaysDay = Saved.Get("AlwaysDay", false);
    }

    void SaveSettings()
    {
        Saved.Set("GodMode", god);
        Saved.Set("InfiniteStamina", infStamina);
        Saved.Set("NoHunger", noHunger);
        Saved.Set("WalkSpeed", Cheat.Speed);
        Saved.Set("JumpPower", Cheat.Jump);
        Saved.Set("FlySpeed", flySpeed);
        Saved.Set("Damage", Cheat.Damage);
        Saved.Set("AttackSpeed", Cheat.AttackSpeed);
        Saved.Set("OneHitKill", Cheat.OneHit);
        Saved.Set("InstantGather", instantGather);
        Saved.Set("AlwaysDay", alwaysDay);
        Saved.Flush();
        SetStatus("Settings saved - they load automatically next time");
    }

    // ---------------- keyboard ----------------
    static bool Pressed(KeyCode k) { try { return k != KeyCode.None && Input.GetKeyDown(k); } catch { return false; } }
    static bool ChatOpen { get { try { return ChatBox.Instance != null && ChatBox.Instance.typing; } catch { return false; } } }

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
        else if (allowed && !ChatOpen)
        {
            if (Pressed(Plugin.KeyGod.Value)) { god = !god; SetStatus(god ? "God mode on" : "God mode off"); }
            if (Pressed(Plugin.KeyFly.Value)) SetFly(!flying);
        }


        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        Cheat.Allowed = allowed;
        Cheat.God = god;
        Cheat.InstantGather = instantGather;
        if (!allowed || !Game.InGame) { if (flying) StopFly(); return; }

        var st = PlayerStatus.Instance;
        if (god)
        {
            if (st.hp < st.maxHp && st.hp > 0f) st.hp = st.maxHp;
            if (st.shield < st.maxShield) st.shield = st.maxShield;
        }
        if (infStamina || flying) st.stamina = st.maxStamina;
        if (noHunger) st.hunger = st.maxHunger;

        if (alwaysDay && DayCycle.time > 0.45f) DayCycle.time = 0.3f;

        if (flying) FlyStep();
    }

    // everything back to normal (another player joined / left the game)
    void TurnOff()
    {
        Cheat.Allowed = false;
        if (flying) StopFly();
    }

    // ---------------- fly ----------------
    void SetFly(bool on)
    {
        if (on && !Check()) return;
        if (on)
        {
            var rb = Game.Me.GetRb();
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            var col = Game.Me.GetPlayerCollider();
            if (col != null) col.enabled = false;
            flying = true;
            SetStatus("Fly mode on - fly where you look, Jump goes up, Ctrl goes down");
        }
        else { StopFly(); SetStatus("Fly mode off"); }
    }

    void StopFly()
    {
        flying = false;
        try
        {
            var me = Game.Me;
            if (me == null) return;
            var rb = me.GetRb();
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            var col = me.GetPlayerCollider();
            if (col != null) col.enabled = true;
        }
        catch { }
    }

    void FlyStep()
    {
        var me = Game.Me;
        var cam = me.GetPlayerCamTransform();
        if (cam == null) return;
        Vector3 dir = Vector3.zero;
        if (!show && !ChatOpen)
        {
            if (Input.GetKey(InputManager.forward)) dir += cam.forward;
            if (Input.GetKey(InputManager.backwards)) dir -= cam.forward;
            if (Input.GetKey(InputManager.right)) dir += cam.right;
            if (Input.GetKey(InputManager.left)) dir -= cam.right;
            if (Input.GetKey(InputManager.jump)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;
        }
        float speed = 15f * flySpeed * (Input.GetKey(InputManager.sprint) ? 2f : 1f);
        if (dir.sqrMagnitude > 0.001f) me.transform.position += dir.normalized * speed * Time.deltaTime;
        var rb = me.GetRb();
        if (!rb.isKinematic) rb.isKinematic = true;
    }

    // ---------------- menu open/close ----------------
    void SetShow(bool value)
    {
        show = value;
        MenuOpen = value;
        if (!show)
        {
            editId = null; rebinding = null; searching = false;
            // give the mouse back to the game unless one of its own menus is open
            try
            {
                bool gameUi = OtherInput.Instance != null && OtherInput.Instance.OtherUiActive();
                bool inv = InventoryUI.Instance != null && InventoryUI.Instance.gameObject.activeInHierarchy;
                if (Game.InGame && !gameUi && !inv) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            catch { }
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
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
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
        Ui.Label($"Muck  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyGod.Value.ToString(), "God Mode");
        Ui.KeyHint(Plugin.KeyFly.Value.ToString(), "Fly");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!allowed) Ui.Label($"<color=#ff6b6b><b>Cheats are off</b></color> - {lastWhy ?? "only when you play alone as the host."}", Ui.Sub);

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
            case 1: DrawCombat(); break;
            case 2: DrawItems(); break;
            case 3: DrawPowerups(); break;
            case 4: DrawMobs(); break;
            case 5: DrawWorld(); break;
            case 6: DrawTeleport(); break;
            case 7: DrawSettings(); break;
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

    void Pager(ref int page, int pages, string what = "Page")
    {
        if (pages <= 1) return;
        GUILayout.BeginHorizontal();
        if (Ui.Button("◀", Ui.Chip, Ui.W(50))) page--;
        Ui.Label($"{what} {page + 1} / {pages}", Ui.Sub);
        if (Ui.Button("▶", Ui.Chip, Ui.W(50))) page++;
        GUILayout.EndHorizontal();
    }

    static string Grey(string s) => $"   <size=12><color=#b8c2d6>{s}</color></size>";

    // ---------------- tabs ----------------
    void DrawPlayer()
    {
        Ui.BeginCard("Survival");
        god = Ui.Toggle(god, $"God Mode  [{Plugin.KeyGod.Value}]", "no damage, full health and shield");
        infStamina = Ui.Toggle(infStamina, "Infinite Stamina", "sprint and jump forever, no drowning");
        noHunger = Ui.Toggle(noHunger, "No Hunger", "always full");
        if (Ui.Button("Heal Now", Ui.Chip)) Heal();
        Ui.EndCard();

        Ui.BeginCard("Movement");
        Cheat.Speed = Ui.Stepper("Walk Speed", Cheat.Speed, 0.25f, 1f, 5f, "0.00");
        Ui.Label("Jump power", Ui.Sub);
        Cheat.Jump = Ui.Chips(Cheat.Jump, JumpPresets, v => $"x{v:0.#}", 4);
        if (Ui.Button("Reset Movement", Ui.Chip)) { Cheat.Speed = 1f; Cheat.Jump = 1f; }
        Ui.EndCard();

        Ui.BeginCard("Fly Mode", "Fly through everything");
        bool f = Ui.Toggle(flying, $"Fly  [{Plugin.KeyFly.Value}]", "fly where you look, Jump up, Ctrl down, Sprint faster");
        if (f != flying) SetFly(f);
        Ui.Label("Fly speed", Ui.Sub);
        flySpeed = Ui.Chips(flySpeed, FlySpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();
    }

    void DrawCombat()
    {
        Ui.BeginCard("Damage");
        Cheat.OneHit = Ui.Toggle(Cheat.OneHit, "One Hit Kill", "mobs, bosses, trees and rocks - all in one hit");
        Ui.Label("Damage", Ui.Sub);
        Cheat.Damage = Ui.Chips(Cheat.Damage, DamagePresets, v => $"x{v:0.#}", 5);
        Ui.Label("Attack speed", Ui.Sub);
        Cheat.AttackSpeed = Ui.Chips(Cheat.AttackSpeed, AttackSpeedPresets, v => $"x{v:0.#}", 4);
        Ui.EndCard();

        Ui.BeginCard("Gathering");
        instantGather = Ui.Toggle(instantGather, "Instant Gather", "trees, rocks and ores break in one hit - with any tool, even your fist");
        Ui.EndCard();
    }

    static string ItemCategory(InventoryItem it)
    {
        switch (it.tag)
        {
            case InventoryItem.ItemTag.Helmet:
            case InventoryItem.ItemTag.Torso:
            case InventoryItem.ItemTag.Legs:
            case InventoryItem.ItemTag.Feet:
            case InventoryItem.ItemTag.Armor: return "Armor";
            case InventoryItem.ItemTag.Arrow: return "Weapons";
        }
        switch (it.type)
        {
            case InventoryItem.ItemType.Axe:
            case InventoryItem.ItemType.Pickaxe:
            case InventoryItem.ItemType.Shovel: return "Tools";
            case InventoryItem.ItemType.Sword:
            case InventoryItem.ItemType.Bow:
            case InventoryItem.ItemType.Shield: return "Weapons";
            case InventoryItem.ItemType.Food: return "Food";
            case InventoryItem.ItemType.Station:
            case InventoryItem.ItemType.Storage: return "Buildings";
        }
        if (it.buildable) return "Buildings";
        return "Resources";
    }

    void DrawItems()
    {
        var im = ItemManager.Instance;
        int gold = 0;
        try { gold = InventoryUI.Instance.GetMoney(); } catch { }

        Ui.BeginCard("Gold Coins", Game.InGame ? $"You have {gold:N0}" : null);
        coinAmount = Ui.Chips(coinAmount, CoinPresets, v => Short(v), 4);
        NumField("coins", "Custom amount", coinAmount, 1, 10_000_000, v => coinAmount = (int)v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"Drop {Short(coinAmount)} in Front", Ui.Primary)) GiveByName("Coin", coinAmount, true);
        if (Ui.Button("Into Inventory", Ui.Tab)) GiveByName("Coin", coinAmount, false);
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Recipes", "Every crafting recipe shows up at once");
        if (Ui.Button("Unlock All Recipes", Ui.Primary)) UnlockRecipes();
        Ui.EndCard();

        Ui.BeginCard("Give Items", "Pick an item, then drop it in front of you or put it straight into your inventory");
        Ui.Label("Amount", Ui.Sub);
        itemAmount = Ui.Chips(itemAmount, AmountPresets, v => v + "x", 4);
        NumField("itemamt", "Custom amount", itemAmount, 1, 10_000, v => itemAmount = (int)v);

        string cat = Ui.Chips(itemCategory, ItemCategories, c => c, 4);
        if (cat != itemCategory) { itemCategory = cat; itemPage = 0; }

        GUILayout.BeginHorizontal();
        Ui.Label("Search", Ui.Sub, Ui.W(60));
        if (!searching) searchAllSelected = false;
        string searchShown = searchAllSelected ? $"<color={Ui.AHex}>{search}</color>" : search;
        if (Ui.Button(searchShown, searching ? Ui.FieldOn : Ui.Field)) { searching = true; searchFieldClickFrame = Time.frameCount; editId = null; }
        if (search.Length > 0 && Ui.Button("✕", Ui.Chip, Ui.W(36))) { search = ""; itemPage = 0; }
        GUILayout.EndHorizontal();

        if (im == null) im = FindObjectOfType<ItemManager>();
        var all = im != null && im.allItems != null ? im.allItems.Values.Where(i => i != null && !string.IsNullOrEmpty(i.name)).ToList() : new List<InventoryItem>();
        var filtered = all.Where(i => (itemCategory == "All" || ItemCategory(i) == itemCategory)
                                    && (search.Length == 0 || i.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                          .OrderBy(i => ItemCategory(i)).ThenBy(i => i.name).ToList();
        int pages = Math.Max(1, (filtered.Count + PageSize - 1) / PageSize);
        itemPage = Mathf.Clamp(itemPage, 0, pages - 1);
        Ui.Label($"{filtered.Count} found", Ui.Sub);
        foreach (var it in filtered.Skip(itemPage * PageSize).Take(PageSize))
        {
            string extra = ItemCategory(it) + (it.tier > 0 ? $"  ·  tier {it.tier}" : "");
            if (Ui.Button(it.name + Grey(extra), selectedItem == it ? Ui.RowOn : Ui.Row)) { selectedItem = it; searching = false; }
        }
        Pager(ref itemPage, pages);
        Ui.EndCard();

        if (selectedItem == null) { Ui.Button("Select an item above", Ui.Primary); return; }
        if (Ui.Button($"DROP {itemAmount}x {selectedItem.name} IN FRONT", Ui.Primary)) Give(selectedItem, itemAmount, true);
        Ui.Space(4);
        if (Ui.Button($"Put {itemAmount}x into Inventory", Ui.Tab)) Give(selectedItem, itemAmount, false);
    }

    void DrawPowerups()
    {
        var im = ItemManager.Instance;
        Ui.BeginCard("Powerups", "Picked up like normal ones - they stack");
        Ui.Label("How many each click", Ui.Sub);
        powerupAmount = Ui.Chips(powerupAmount, PowerupAmounts, v => v + "x", 4);
        if (Ui.Button($"Give Every Powerup {powerupAmount}x", Ui.Primary)) StartCoroutine(GivePowerups(im.allPowerups.Values.ToList(), powerupAmount));
        Ui.EndCard();

        Ui.BeginCard("Pick One");
        powerupTier = Ui.Chips(powerupTier, PowerupTiers, t => t, 4);
        if (im != null && im.allPowerups != null)
            foreach (var p in im.allPowerups.Values.OrderBy(p => p.tier).ThenBy(p => p.name))
            {
                if (powerupTier != "All" && p.tier.ToString() != powerupTier) continue;
                string col = p.tier == Powerup.PowerTier.Orange ? "#ffb347" : p.tier == Powerup.PowerTier.Blue ? "#6fb6ff" : "#ffffff";
                if (Ui.Button($"<color={col}><b>{p.name}</b></color>" + Grey(p.description ?? ""), Ui.Row))
                    StartCoroutine(GivePowerups(new List<Powerup> { p }, powerupAmount));
            }
        Ui.EndCard();
    }

    void DrawWorld()
    {
        Ui.BeginCard("Time");
        bool d = Ui.Toggle(alwaysDay, "Always Day", "the night never comes");
        if (d != alwaysDay) { alwaysDay = d; if (d) DayCycle.time = 0.3f; }
        GUILayout.BeginHorizontal();
        if (Ui.Button("Morning", Ui.Chip)) DayCycle.time = 0.05f;
        if (Ui.Button("Noon", Ui.Chip)) DayCycle.time = 0.25f;
        if (Ui.Button("Night", Ui.Chip)) { alwaysDay = false; DayCycle.time = 0.6f; }
        GUILayout.EndHorizontal();
        Ui.EndCard();
    }

    void DrawMobs()
    {
        Ui.BeginCard("Spawn Mobs & Bosses", "They appear in front of you and attack like normal ones");
        Ui.Label("Amount", Ui.Sub);
        mobAmount = Ui.Chips(mobAmount, MobAmounts, v => v + "x", 4);
        NumField("mobamt", "Custom amount", mobAmount, 1, 50, v => mobAmount = (int)v);
        Ui.Label("Distance", Ui.Sub);
        mobDistance = Ui.Chips(mobDistance, MobDistances, v => $"{v:0} m", 4);
        mobFilter = Ui.Chips(mobFilter, MobFilters, f => f, 3);

        var all = MobSpawner.Instance != null && MobSpawner.Instance.allMobs != null ? MobSpawner.Instance.allMobs : new MobType[0];
        var shown = all.Where(m => m != null && m.mobPrefab != null && !string.IsNullOrEmpty(m.name)
                                   && (mobFilter == "All" || (mobFilter == "Bosses") == m.boss))
                       .Distinct()                                  // the game lists some mobs twice
                       .OrderBy(m => m.boss).ThenBy(m => m.name).ToList();
        Ui.Label($"{shown.Count} kinds", Ui.Sub);
        foreach (var m in shown)
            if (Ui.Button($"<b>{m.name}</b>" + Grey(m.boss ? "Boss" : "Mob"), selectedMob == m ? Ui.RowOn : Ui.Row)) selectedMob = m;
        Ui.EndCard();

        string btn = selectedMob != null ? $"SPAWN {mobAmount}x {selectedMob.name}" : "Select a mob above";
        if (Ui.Button(btn, Ui.Primary) && selectedMob != null) SpawnMobs(selectedMob, mobAmount);
        Ui.Space(6);

        int mobs = 0;
        try { mobs = MobManager.Instance.mobs.Count; } catch { }
        Ui.BeginCard("Kill Mobs", $"{mobs} alive right now");
        if (Ui.Button("Kill All Mobs", Ui.Primary)) KillAll(false);
        if (Ui.Button("Kill All Mobs Near Me (50 m)", Ui.Chip)) KillAll(true);
        Ui.EndCard();
    }

    void DrawTeleport()
    {
        if (Time.unscaledTime - placesScannedAt > 5f) ScanPlaces();

        Ui.BeginCard("Position");
        GUILayout.BeginHorizontal();
        if (Ui.Button("Save Position", Ui.Chip)) { hasSaved = true; savedPos = Game.Me.transform.position; SetStatus("Position saved"); }
        if (Ui.Button("Go to Saved", Ui.Chip)) { if (hasSaved) TeleportTo(savedPos, "your saved position", false); else SetStatus("No position saved yet"); }
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Places", places.Count > 0 ? $"{places.Count} places - nearest first" : null);
        if (places.Count == 0) Ui.Label("Nothing found yet.", Ui.Sub);
        int pages = Math.Max(1, (places.Count + PageSize - 1) / PageSize);
        placePage = Mathf.Clamp(placePage, 0, pages - 1);
        Vector3 me = Game.Me != null ? Game.Me.transform.position : Vector3.zero;
        foreach (var pl in places.Skip(placePage * PageSize).Take(PageSize))
        {
            if (pl.At == null) continue;
            float dist = Vector3.Distance(me, pl.At.position);
            if (Ui.Button($"<b>{pl.Name}</b>" + Grey($"{pl.Group}  ·  {dist:0} m"), Ui.Row)) TeleportTo(pl.At.position, pl.Name, true);
        }
        Pager(ref placePage, pages);
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
        KeyRow("God mode", Plugin.KeyGod);
        KeyRow("Fly mode", Plugin.KeyFly);
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

    // ---------------- actions ----------------
    bool Check()
    {
        if (!Game.InGame) { SetStatus("Start a game first"); return false; }
        if (!allowed) { SetStatus("Cheats are off while other players are here"); return false; }
        return true;
    }

    void Heal()
    {
        if (!Check()) return;
        var st = PlayerStatus.Instance;
        st.hp = st.maxHp; st.shield = st.maxShield; st.stamina = st.maxStamina; st.hunger = st.maxHunger;
        SetStatus("Healed");
    }

    Vector3 Front(float dist = 3f)
    {
        var me = Game.Me;
        var cam = me.GetPlayerCamTransform();
        var fwd = cam != null ? cam.forward : me.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        return me.transform.position + fwd.normalized * dist;
    }

    void DropInWorld(int itemId, int amount, Vector3 pos)
    {
        int oid = ItemManager.Instance.GetNextId();
        ItemManager.Instance.DropItemAtPosition(itemId, amount, pos, oid);
        ServerSend.DropItemAtPosition(itemId, amount, oid, pos);
    }

    void GiveByName(string name, int amount, bool inFront)
    {
        if (!Check()) return;
        var it = ItemManager.Instance.GetItemByName(name);
        if (it == null) { SetStatus($"{name} not found"); return; }
        Give(it, amount, inFront);
    }

    void Give(InventoryItem item, int amount, bool inFront)
    {
        if (!Check()) return;
        try
        {
            int stack = item.stackable ? Math.Max(1, item.max) : 1;
            int left = amount, dropped = 0;
            if (inFront)
            {
                // like the game does it: the items lie in front of you, walk over / press E to pick them up
                while (left > 0)
                {
                    int c = Math.Min(stack, left);
                    left -= c;
                    var pos = Front(2.5f) + Vector3.up + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0f, UnityEngine.Random.Range(-0.6f, 0.6f));
                    DropInWorld(item.id, c, pos);
                }
                SetStatus($"{amount:N0} {item.name} dropped in front of you");
                return;
            }
            while (left > 0)
            {
                int chunk = Math.Min(stack, left);
                left -= chunk;
                var copy = ScriptableObject.CreateInstance<InventoryItem>();
                copy.Copy(item, chunk);
                int rest = InventoryUI.Instance.AddItemToInventory(copy);
                if (rest > 0)
                {
                    DropInWorld(item.id, rest, Game.Me.transform.position + Vector3.up);
                    dropped += rest;
                }
                if (dropped > 0 && left > 0)   // inventory is full: drop the rest in big piles
                {
                    while (left > 0)
                    {
                        int c = Math.Min(stack, left);
                        left -= c;
                        DropInWorld(item.id, c, Game.Me.transform.position + Vector3.up);
                        dropped += c;
                    }
                }
            }
            InventoryUI.Instance.UpdateAllCells();
            SetStatus(dropped > 0 ? $"+{amount:N0} {item.name}  ({dropped:N0} dropped at your feet - inventory full)" : $"+{amount:N0} {item.name}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not give " + item.name); }
    }

    IEnumerator GivePowerups(List<Powerup> list, int each)
    {
        if (!Check()) yield break;
        int n = 0;
        foreach (var p in list)
        {
            if (p == null) continue;
            for (int i = 0; i < each; i++)
            {
                try
                {
                    var pos = Game.Me.transform.position + Vector3.up;
                    int oid = ItemManager.Instance.GetNextId();
                    ItemManager.Instance.DropPowerupAtPosition(p.id, pos, oid);
                    ServerSend.DropPowerupAtPosition(p.id, oid, pos);
                    ClientSend.PickupItem(oid);
                    n++;
                }
                catch (Exception e) { Plugin.L.LogWarning(e.Message); }
                if (n % 5 == 0) yield return null;   // let the game pick them up
            }
        }
        SetStatus(list.Count == 1 ? $"+{each}x {list[0].name}" : $"+{n} powerups");
    }

    void UnlockRecipes()
    {
        if (!Check()) return;
        try
        {
            var ev = UiEvents.Instance;
            int count = ItemManager.Instance.allItems.Count;
            foreach (var field in new[] { "unlockedHard", "unlockedSoft", "stationsUnlocked" })
            {
                var arr = Traverse.Create(ev).Field(field).GetValue<bool[]>();
                if (arr == null) continue;
                for (int i = 0; i < arr.Length; i++) arr[i] = true;
            }
            SetStatus($"All {count} recipes unlocked - open the crafting menu");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not unlock recipes"); }
    }

    void KillAll(bool near)
    {
        if (!Check()) return;
        int n = 0;
        var me = Game.Me.transform.position;
        foreach (var m in MobManager.Instance.mobs.Values.ToList())
        {
            if (m == null) continue;
            if (near && Vector3.Distance(me, m.transform.position) > 50f) continue;
            try { ClientSend.PlayerDamageMob(m.id, 1_000_000, 1f, 0, m.transform.position, 0); n++; }
            catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        }
        SetStatus(n == 0 ? "No mobs found" : $"Killed {n} mobs");
    }

    void SpawnMobs(MobType type, int amount)
    {
        if (!Check()) return;
        try
        {
            for (int i = 0; i < amount; i++)
            {
                float spread = amount > 1 ? Mathf.Min(2f + amount * 0.4f, 10f) : 0f;
                var pos = Game.Ground(Front(mobDistance) + new Vector3(UnityEngine.Random.Range(-spread, spread), 0f, UnityEngine.Random.Range(-spread, spread)));
                MobSpawner.Instance.ServerSpawnNewMob(MobManager.Instance.GetNextId(), type.id, pos, 1f, 1f);
            }
            SetStatus($"Spawned {amount}x {type.name}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not spawn " + type.name); }
    }

    // ---------------- teleport ----------------
    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "");
        int nl = s.IndexOf('\n');
        if (nl >= 0) s = s.Substring(0, nl);
        return s.Trim();
    }

    void ScanPlaces()
    {
        placesScannedAt = Time.unscaledTime;
        var list = new List<Place>();
        try
        {
            if (Boat.Instance != null) list.Add(new Place { Name = "Boat", Group = "Escape", At = Boat.Instance.rbTransform != null ? Boat.Instance.rbTransform : Boat.Instance.transform });
            foreach (var s in FindObjectsOfType<ShrineBoss>())
                list.Add(new Place { Name = s.boss != null ? s.boss.name : "Boss", Group = "Boss shrine", At = s.transform });
            foreach (var s in FindObjectsOfType<ShrineGuardian>())
                list.Add(new Place { Name = Clean(s.GetName()).Replace("Challenge ", ""), Group = "Guardian", At = s.transform });
            foreach (var t in FindObjectsOfType<TraderInteract>())
            {
                string n = Clean(t.GetName());
                int i = n.IndexOf("trade with ", StringComparison.OrdinalIgnoreCase);
                list.Add(new Place { Name = i >= 0 ? n.Substring(i + 11) : "Trader", Group = "Trader", At = t.transform });
            }
            foreach (var s in FindObjectsOfType<ShrineInteractable>())
                if (!s.started) list.Add(new Place { Name = "Battle Shrine", Group = "Shrine", At = s.transform });
        }
        catch (Exception e) { Plugin.L.LogWarning(e.Message); }
        var me = Game.Me != null ? Game.Me.transform.position : Vector3.zero;
        places = list.Where(p => p.At != null).OrderBy(p => Vector3.Distance(me, p.At.position)).ToList();
    }

    void TeleportTo(Vector3 pos, string name, bool ground)
    {
        if (!Check()) return;
        var me = Game.Me;
        Vector3 target = ground ? Game.Ground(pos + new Vector3(2.5f, 0f, 2.5f)) + Vector3.up * 2f : pos;
        var rb = me.GetRb();
        rb.velocity = Vector3.zero;
        me.transform.position = target;
        rb.position = target;
        SetStatus("Teleported to " + name);
    }
}
