using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EvilDragon;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace CrimeSimulatorCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Crime Simulator", Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "evildragon.cheats.crimesimulator";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<KeyCode> KeyMenu, KeyHeal, KeyCops, KeyMoney;
    internal static ConfigEntry<string> Theme;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", KeyCode.F1, "Open/close menu");
        KeyHeal = Config.Bind("Hotkeys", "Heal", KeyCode.F2, "Full heal");
        KeyCops = Config.Bind("Hotkeys", "CallOffCops", KeyCode.F3, "Call off the police");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", KeyCode.F4, "Add the selected money amount");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        Ui.InitialTheme = Theme.Value;
        Ui.Log = m => Logger.LogWarning(m);

        var go = new GameObject("EvilDragonCheatMenu");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        new Harmony(Guid).PatchAll(typeof(Patches));
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

/// The game has online co-op. Every cheat only works in single-player or when you are alone in the session.
static class Game
{
    public static WorldManager WM => WorldManager.Instance;
    public static FPP_Player Me => WM != null ? WM.localPlayer : null;

    public static bool InGame => WM != null && Me != null;

    public static bool Allowed
    {
        get
        {
            try
            {
                var wm = WM;
                if (wm == null || wm.Runner == null) return false;
                return wm.Runner.IsSinglePlayer || wm.Runner.ActivePlayers.Count() <= 1;
            }
            catch { return false; }
        }
    }
}

public static class Patches
{
    // ---- God mode
    [HarmonyPrefix, HarmonyPatch(typeof(FPP_Player), nameof(FPP_Player.DealDamageToPlayer))]
    static bool NoDamage(FPP_Player __instance) => !(CheatMenu.GodMode && __instance == Game.Me && Game.Allowed);

    // ---- Speed
    [HarmonyPostfix, HarmonyPatch(typeof(ECM.Controllers.BaseFirstPersonController), "forwardSpeed", MethodType.Getter)]
    static void Fwd(ref float __result) { if (Game.Allowed) __result *= CheatMenu.SpeedMult; }
    [HarmonyPostfix, HarmonyPatch(typeof(ECM.Controllers.BaseFirstPersonController), "backwardSpeed", MethodType.Getter)]
    static void Back(ref float __result) { if (Game.Allowed) __result *= CheatMenu.SpeedMult; }
    [HarmonyPostfix, HarmonyPatch(typeof(ECM.Controllers.BaseFirstPersonController), "strafeSpeed", MethodType.Getter)]
    static void Strafe(ref float __result) { if (Game.Allowed) __result *= CheatMenu.SpeedMult; }

    // ---- Police
    [HarmonyPrefix, HarmonyPatch(typeof(PoliceManager), nameof(PoliceManager.PoliceCalled))]
    static bool NoPolice1() => !(CheatMenu.NoPolice && Game.Allowed);
    [HarmonyPrefix, HarmonyPatch(typeof(PoliceManager), nameof(PoliceManager.RPC_PoliceCalled))]
    static bool NoPolice2() => !(CheatMenu.NoPolice && Game.Allowed);
    [HarmonyPrefix, HarmonyPatch(typeof(NPC), nameof(NPC.CallCops))]
    static bool NoPolice3() => !(CheatMenu.NoPolice && Game.Allowed);

    // ---- Heat meter
    [HarmonyPrefix, HarmonyPatch(typeof(WorldManager), nameof(WorldManager.AddHeat))]
    static bool NoHeat(int heat) => !(CheatMenu.NoHeat && heat > 0 && Game.Allowed);

    // ---- Lockpicking
    [HarmonyPrefix, HarmonyPatch(typeof(BasicLockpickMinigame), nameof(BasicLockpickMinigame.LockpickBroke))]
    static bool Unbreak1() => !(CheatMenu.Unbreakable && Game.Allowed);
    [HarmonyPrefix, HarmonyPatch(typeof(AdvancedLockpickMinigame), nameof(AdvancedLockpickMinigame.LockpickBroke))]
    static bool Unbreak2() => !(CheatMenu.Unbreakable && Game.Allowed);
    [HarmonyPrefix, HarmonyPatch(typeof(MasterLockpickMinigame), nameof(MasterLockpickMinigame.LockpickBroke))]
    static bool Unbreak3() => !(CheatMenu.Unbreakable && Game.Allowed);

    [HarmonyPostfix, HarmonyPatch(typeof(BasicLockpickMinigame), nameof(BasicLockpickMinigame.BeginMinigame))]
    static void Instant1(BasicLockpickMinigame __instance) => CheatMenu.QueueUnlock(() => __instance.Unlocked());
    [HarmonyPostfix, HarmonyPatch(typeof(AdvancedLockpickMinigame), nameof(AdvancedLockpickMinigame.BeginMinigame))]
    static void Instant2(AdvancedLockpickMinigame __instance) => CheatMenu.QueueUnlock(() => __instance.Victory());
    [HarmonyPostfix, HarmonyPatch(typeof(MasterLockpickMinigame), nameof(MasterLockpickMinigame.BeginMinigame))]
    static void Instant3(MasterLockpickMinigame __instance) => CheatMenu.QueueUnlock(() => __instance.Victory());
    [HarmonyPostfix, HarmonyPatch(typeof(PadlockMinigame), nameof(PadlockMinigame.BeginMinigame))]
    static void Instant4(PadlockMinigame __instance) => CheatMenu.QueueUnlock(() => __instance.Win());

    // ---- Gear: tools and weapons never lose durability
    [HarmonyPrefix, HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.AddDamageToCurrentItem))]
    static bool NoDurabilityLoss(float dmg) => !(CheatMenu.NoDurability && dmg > 0f && Game.Allowed);

    // ---- Gear: no attack / shoot cooldown. Same checks as the game, minus the timing ones.
    [HarmonyPostfix, HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.CheckIfCanAttack))]
    static void NoCooldown(WeaponManager __instance, ref bool __result)
    {
        if (__result || !CheatMenu.RapidFire || !Game.Allowed) return;
        try
        {
            var w = __instance.currentWeapon;
            var p = __instance.player;
            if (w == null || p == null) return;
            bool ammoOk = w.ammoTypes.Count == 0 || w.ammoTypes[w.currentAmmoIndex].amountInInventory > 0;
            __result = w.canAttack && !p.radialMenuManager.anyMenuActive && p.canInteract
                       && !p.characterController.pause && !__instance.pickingUp && ammoOk;
        }
        catch { }
    }

    // ---- Never upload scores to Steam leaderboards while this mod is installed (fair to other players)
    [HarmonyPrefix, HarmonyPatch(typeof(SteamUserStats), nameof(SteamUserStats.UploadLeaderboardScore))]
    static bool NoLeaderboard(ref SteamAPICall_t __result) { __result = SteamAPICall_t.Invalid; return false; }
}

public class CheatMenu : MonoBehaviour
{
    public static bool GodMode, NoPolice, NoHeat, Unbreakable, InstantLockpick, NoDurability, RapidFire;
    public static float SpeedMult = 1f;

    bool show, infStamina, undetectable, silent, infAmmo, noAttackStamina;
    float damageMult = 1f, rangeMult = 1f, attackSpeed = 1f;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Stealth", "Gear", "Money", "Items", "Settings" };

    static readonly float[] DamagePresets = { 1f, 2f, 5f, 10f, 100f };
    static readonly float[] RangePresets = { 1f, 1.5f, 2f, 3f, 5f };
    static readonly float[] AttackSpeedPresets = { 1f, 1.5f, 2f, 3f };

    // original weapon values, so changing multipliers or switching off restores the game
    class WeaponOrig { public float Npc, Obj, Range, Stamina; }
    readonly Dictionary<Weapon, WeaponOrig> weaponOrig = new();

    // ---------------- item spawner ----------------
    class ItemEntry { public Item Item; public string Name; public string Category; }
    List<ItemEntry> items;
    float itemsScannedAt = -100f;
    string itemCategory = "All", search = "";
    bool searching;
    int searchFieldClickFrame = -1, searchCloseCheckFrame = -1;
    int itemPage, spawnCount = 1;
    bool spawnAtCrosshair = true;
    ItemEntry selectedItem;
    static readonly string[] Categories = { "All", "Weapons", "Ammo", "Tools", "Loot", "Money", "Other" };
    static readonly int[] SpawnCounts = { 1, 5, 10, 25 };
    const int PageSize = 10;

    static string Category(Item i)
    {
        if (i.isWeapon) return "Weapons";
        if (i.isAmmo) return "Ammo";
        if (i.isMoney || i.isCreditCard || i.isGoldenCard) return "Money";
        if (i.isLoot || i.isJewelry) return "Loot";
        if (i.isKey || i.isAccessCard || i.isDrone || i.isFirstAid || i.isArmorVest || i.isSkillBook || i.isItemUpgrade || i.isCarFuel) return "Tools";
        return "Other";
    }

    static string DisplayName(Item i)
    {
        string n = null;
        try { if (!string.IsNullOrEmpty(i.itemName)) n = I2.Loc.LocalizationManager.GetTranslation(i.itemName); } catch { }
        if (string.IsNullOrEmpty(n)) n = !string.IsNullOrEmpty(i.itemName) ? i.itemName : i.name;
        if (!string.IsNullOrEmpty(i.itemNameExtra)) n += " " + i.itemNameExtra;
        return n;
    }

    void ScanItems()
    {
        itemsScannedAt = Time.unscaledTime;
        var seen = new HashSet<string>();
        var list = new List<ItemEntry>();
        foreach (var i in Resources.FindObjectsOfTypeAll<Item>())
        {
            if (i == null || i.itemDropPrefab == null || i.itemDropPrefab.GetComponent<Fusion.NetworkObject>() == null) continue;
            if (!seen.Add(i.name)) continue;
            list.Add(new ItemEntry { Item = i, Name = DisplayName(i), Category = Category(i) });
        }
        items = list.OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
    }

    long moneyAmount = 10_000, xpAmount = 1_000;
    static readonly long[] MoneyPresets = { 1_000, 10_000, 50_000, 100_000, 500_000, 1_000_000 };
    static readonly long[] XpPresets = { 100, 500, 1_000, 5_000, 10_000, 50_000 };

    string status = "";
    float statusTime;

    // number input + key rebinding (read from the keyboard ourselves)
    string editId, editBuf = "";
    long editMin, editMax;
    Action<long> editSet;
    ConfigEntry<KeyCode> rebinding;

    // original values so switching a cheat off restores the game
    float origDetection = -1f, origNoise = -1f;

    // instant lockpick: finish the minigame a moment after it opened
    static Action pendingUnlock;
    static float pendingUnlockAt;
    public static void QueueUnlock(Action a)
    {
        if (!InstantLockpick || !Game.Allowed) return;
        pendingUnlock = a;
        pendingUnlockAt = Time.unscaledTime + 0.15f;
    }

    void SetStatus(string s) { status = s; statusTime = Time.unscaledTime; Plugin.L.LogInfo(s); }

    static string Short(long v) =>
        v >= 1_000_000 ? $"{v / 1_000_000d:0.#}M" :
        v >= 1_000 ? $"{v / 1_000d:0.#}K" : v.ToString();

    // ---------------- saved settings ----------------
    bool settingsLoaded;
    void LoadSettings()
    {
        settingsLoaded = true;
        GodMode = Saved.Get("GodMode", false);
        infStamina = Saved.Get("InfiniteStamina", false);
        SpeedMult = Saved.Get("WalkSpeed", 1f);
        NoPolice = Saved.Get("NoPolice", false);
        NoHeat = Saved.Get("NoHeat", false);
        undetectable = Saved.Get("Undetectable", false);
        silent = Saved.Get("Silent", false);
        Unbreakable = Saved.Get("UnbreakableLockpicks", false);
        InstantLockpick = Saved.Get("InstantLockpick", false);
        moneyAmount = Saved.Get("MoneyAmount", 10_000L);
        NoDurability = Saved.Get("NoDurabilityLoss", false);
        infAmmo = Saved.Get("InfiniteAmmo", false);
        RapidFire = Saved.Get("RapidFire", false);
        attackSpeed = Saved.Get("AttackSpeed", 1f);
        damageMult = Saved.Get("DamageMultiplier", 1f);
        rangeMult = Saved.Get("RangeMultiplier", 1f);
        noAttackStamina = Saved.Get("NoAttackStamina", false);
    }

    void SaveSettings()
    {
        Saved.Set("GodMode", GodMode);
        Saved.Set("InfiniteStamina", infStamina);
        Saved.Set("WalkSpeed", SpeedMult);
        Saved.Set("NoPolice", NoPolice);
        Saved.Set("NoHeat", NoHeat);
        Saved.Set("Undetectable", undetectable);
        Saved.Set("Silent", silent);
        Saved.Set("UnbreakableLockpicks", Unbreakable);
        Saved.Set("InstantLockpick", InstantLockpick);
        Saved.Set("MoneyAmount", moneyAmount);
        Saved.Set("NoDurabilityLoss", NoDurability);
        Saved.Set("InfiniteAmmo", infAmmo);
        Saved.Set("RapidFire", RapidFire);
        Saved.Set("AttackSpeed", attackSpeed);
        Saved.Set("DamageMultiplier", damageMult);
        Saved.Set("RangeMultiplier", rangeMult);
        Saved.Set("NoAttackStamina", noAttackStamina);
        Saved.Flush();
        SetStatus("Settings saved - they load automatically next time");
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();

        // Leave the search box as soon as you click anywhere else (no Enter needed).
        // The click is handled by OnGUI in the same frame, so decide one frame later.
        if (searchCloseCheckFrame >= 0 && Time.frameCount > searchCloseCheckFrame)
        {
            if (searchFieldClickFrame != searchCloseCheckFrame) searching = false;
            searchCloseCheckFrame = -1;
        }
        if (searching && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))) searchCloseCheckFrame = Time.frameCount;

        // The menu key always works (except while choosing a new key), even while typing in a field.
        if (rebinding == null && Input.GetKeyDown(Plugin.KeyMenu.Value))
        {
            searching = false;
            editId = null;
            SetShow(!show);
        }
        else if (rebinding != null) HandleRebind();
        else if (searching) HandleSearchInput();
        else if (editId != null) HandleNumberInput();
        else
        {
            if (Input.GetKeyDown(Plugin.KeyHeal.Value)) Heal();
            if (Input.GetKeyDown(Plugin.KeyCops.Value)) CallOffCops();
            if (Input.GetKeyDown(Plugin.KeyMoney.Value)) AddMoney(moneyAmount);
        }

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    void Tick()
    {
        if (pendingUnlock != null && Time.unscaledTime >= pendingUnlockAt)
        {
            var a = pendingUnlock; pendingUnlock = null;
            try { a(); SetStatus("Lock opened"); } catch (Exception e) { Plugin.L.LogError(e); }
        }

        var me = Game.Me;
        if (me == null) return;
        bool allowed = Game.Allowed;

        if (infStamina && allowed) me.currentStamina = me.maxStamina;

        // When another player joins, put the changed values back right away.
        if (undetectable && allowed)
        {
            if (origDetection < 0f) origDetection = me.detectionSpeed;
            if (me.detectionSpeed != 0f) me.detectionSpeed = 0f;
        }
        else if (origDetection >= 0f) { me.detectionSpeed = origDetection; origDetection = -1f; }

        if (silent && allowed)
        {
            if (origNoise < 0f) origNoise = me.noiseRangeMultiplier;
            if (me.noiseRangeMultiplier != 0f) me.noiseRangeMultiplier = 0f;
        }
        else if (origNoise >= 0f) { me.noiseRangeMultiplier = origNoise; origNoise = -1f; }

        TickGear(me, allowed);
    }

    float animatorSpeedSet = -1f;

    void TickGear(FPP_Player me, bool allowed)
    {
        var wm = me.weaponManager;
        if (wm == null || wm.weapons == null) return;

        bool modsOn = allowed && (damageMult != 1f || rangeMult != 1f || noAttackStamina);
        bool ammoChanged = false;
        foreach (var w in wm.weapons)
        {
            if (w == null) continue;

            // weapon stat multipliers (always computed from the original values)
            if (!weaponOrig.TryGetValue(w, out var o))
            {
                o = new WeaponOrig { Npc = w.attackDamageToNPC, Obj = w.attackDamageToObjects, Range = w.attackSensorRange, Stamina = w.drainStaminaOnAttack };
                weaponOrig[w] = o;
            }
            float npc = modsOn ? o.Npc * damageMult : o.Npc;
            float obj = modsOn ? o.Obj * damageMult : o.Obj;
            float range = modsOn ? o.Range * rangeMult : o.Range;
            float stam = modsOn && noAttackStamina ? 0f : o.Stamina;
            if (w.attackDamageToNPC != npc) w.attackDamageToNPC = npc;
            if (w.attackDamageToObjects != obj) w.attackDamageToObjects = obj;
            if (w.attackSensorRange != range) w.attackSensorRange = range;
            if (w.drainStaminaOnAttack != stam) w.drainStaminaOnAttack = stam;

            // infinite ammo: keep every ammo type topped up
            if (infAmmo && allowed && w.ammoTypes != null)
                foreach (var a in w.ammoTypes)
                    if (a != null && a.amountInInventory < 99) { a.amountInInventory = 99; ammoChanged = true; }
        }
        if (ammoChanged) try { me.inventory.RefreshItemInfo(); } catch { }

        // faster attack animations
        float wanted = allowed && RapidFire ? attackSpeed : 1f;
        if (wm.handAnimator != null && (animatorSpeedSet != wanted || wm.handAnimator.speed != wanted))
        {
            if (wanted != 1f || animatorSpeedSet > 0f) wm.handAnimator.speed = wanted;
            animatorSpeedSet = wanted;
        }
    }

    void RepairAll()
    {
        if (!Check()) return;
        try
        {
            int n = 0;
            foreach (var slot in Game.Me.inventory.itemSlots)
            {
                if (slot == null || slot.assignedItem == null || slot.damagesPerItem == null) continue;
                for (int i = 0; i < slot.damagesPerItem.Count; i++)
                    if (slot.damagesPerItem[i] > 0f) { slot.damagesPerItem[i] = 0f; n++; }
                slot.RefreshAmount();
            }
            Game.Me.inventory.RefreshItemInfo();
            SetStatus(n > 0 ? $"Repaired {n} item(s)" : "Nothing to repair");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    // ---------------- menu open/close: use the same input lock as the game's pause menu ----------------
    bool weFroze;
    void SetShow(bool value)
    {
        show = value;
        if (!show) { editId = null; rebinding = null; searching = false; }
        try
        {
            var me = Game.Me;
            if (show)
            {
                CursorLockManager.Instance?.LockCursor(false);
                if (me != null && !me.pauseActive && !me.characterController.pause)
                {
                    me.characterController.pause = true;
                    me.canInteract = false;
                    weFroze = true;
                }
            }
            else
            {
                if (me != null && weFroze && !me.pauseActive)
                {
                    me.characterController.pause = false;
                    me.canInteract = true;
                    CursorLockManager.Instance?.LockCursor(true);
                }
                else if (me != null && !me.pauseActive) CursorLockManager.Instance?.LockCursor(true);
                weFroze = false;
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("Input lock: " + e.Message); }
    }

    void LateUpdate()
    {
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    // ---------------- keyboard input ----------------
    static readonly KeyCode[] Digits = { KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9 };
    static readonly KeyCode[] PadDigits = { KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9 };

    void HandleNumberInput()
    {
        for (int d = 0; d <= 9; d++)
            if ((Input.GetKeyDown(Digits[d]) || Input.GetKeyDown(PadDigits[d])) && editBuf.Length < 12)
                editBuf += (char)('0' + d);
        if (Input.GetKeyDown(KeyCode.Backspace) && editBuf.Length > 0) editBuf = editBuf.Substring(0, editBuf.Length - 1);
        if (Input.GetKeyDown(KeyCode.Delete)) editBuf = "";
        if (editBuf.Length > 0 && long.TryParse(editBuf, out var v)) editSet?.Invoke(Math.Clamp(v, editMin, editMax));
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape)) editId = null;
    }

    static KeyCode[] allKeys;
    void HandleRebind()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { rebinding = null; SetStatus("Cancelled"); return; }
        allKeys ??= ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Where(k => k < KeyCode.Mouse0 && k != KeyCode.None && k != KeyCode.Escape).ToArray();
        foreach (var k in allKeys)
        {
            if (!Input.GetKeyDown(k)) continue;
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
        Ui.Label($"Crime Simulator  ·  v{Plugin.Version}", Ui.Sub);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyHeal.Value.ToString(), "Heal");
        Ui.KeyHint(Plugin.KeyCops.Value.ToString(), "Lose cops");
        Ui.KeyHint(Plugin.KeyMoney.Value.ToString(), "+ Money");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!Game.InGame) Ui.Label("Load into a game first.", Ui.Sub);
        else if (!Game.Allowed) Ui.Label($"<color=#ff5a7a><b>Online session with other players – cheats are disabled.</b></color>", Ui.Sub);
        else Ui.Label($"<color={Ui.GHex}>●</color> Single-player – cheats active", Ui.Sub);
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; searching = false; }
        GUILayout.EndHorizontal();
        Ui.Space(4);

        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawStealth(); break;
            case 2: DrawGear(); break;
            case 3: DrawMoney(); break;
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
        string shown = editing
            ? (editBuf.Length == 0 ? "_" : editBuf + "_") + $"   <size=12><color={Ui.AHex}>Enter = OK</color></size>"
            : value.ToString("N0") + "   <size=12><color=#b8c2d6>click to type</color></size>";
        if (Ui.Button(shown, editing ? Ui.FieldOn : Ui.Field))
        {
            if (editing) editId = null;
            else { editId = id; editBuf = ""; editMin = min; editMax = max; editSet = set; }
        }
        GUILayout.EndHorizontal();
    }

    void DrawPlayer()
    {
        Ui.BeginCard("Health", "Stay alive no matter what");
        Ui.Toggle2(ref GodMode, "God Mode", ref infStamina, "Infinite Stamina");
        if (Ui.Button($"Full Heal  [{Plugin.KeyHeal.Value}]", Ui.Primary)) Heal();
        Ui.EndCard();

        Ui.BeginCard("Movement", "How fast you walk and run");
        SpeedMult = Ui.Stepper("Walk Speed", SpeedMult, 0.25f, 1f, 5f, "0.00");
        Ui.EndCard();
    }

    void DrawStealth()
    {
        Ui.BeginCard("Police");
        NoPolice = Ui.Toggle(NoPolice, "No Police", "nobody can call the cops");
        if (Ui.Button($"Call Off Cops  [{Plugin.KeyCops.Value}]", Ui.Primary)) CallOffCops();
        Ui.EndCard();

        Ui.BeginCard("Detection", "Stay hidden while you work");
        undetectable = Ui.Toggle(undetectable, "Undetectable", "NPCs can't spot you");
        silent = Ui.Toggle(silent, "Silent", "you make no noise");
        NoHeat = Ui.Toggle(NoHeat, "No Heat", "heat meter never goes up");
        Ui.EndCard();
    }

    void DrawGear()
    {
        Ui.BeginCard("Durability & Ammo", "Tools and weapons");
        GUILayout.BeginHorizontal();
        NoDurability = Ui.Toggle(NoDurability, "No Durability Loss");
        infAmmo = Ui.Toggle(infAmmo, infAmmo ? "Infinite Ammo  ∞" : "Infinite Ammo");
        GUILayout.EndHorizontal();
        if (Ui.Button("Repair All Items", Ui.Tab)) RepairAll();
        Ui.EndCard();

        Ui.BeginCard("Combat", "Works for every gun and melee weapon");
        Ui.Label("Damage", Ui.Sub);
        damageMult = Ui.Chips(damageMult, DamagePresets, v => v >= 100f ? "One-Hit" : $"x{v:0.#}", 5);
        Ui.Label("Range", Ui.Sub);
        rangeMult = Ui.Chips(rangeMult, RangePresets, v => $"x{v:0.#}", 5);
        Ui.Toggle2(ref RapidFire, "No Cooldown", ref noAttackStamina, "No Attack Stamina");
        if (RapidFire)
        {
            Ui.Label("Attack speed", Ui.Sub);
            attackSpeed = Ui.Chips(attackSpeed, AttackSpeedPresets, v => $"x{v:0.#}", 4);
        }
        Ui.EndCard();

        Ui.BeginCard("Lockpicking");
        Ui.Toggle2(ref InstantLockpick, "Instant Lockpick", ref Unbreakable, "Unbreakable Picks");
        Ui.EndCard();
    }

    void DrawMoney()
    {
        int money = 0;
        try { if (Game.WM != null) money = Game.WM.sharedMoney; } catch { }
        Ui.BeginCard("Money");
        Ui.Label($"<size=22><b>$ {money:N0}</b></size>", Ui.Text);
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => "$" + Short(v), 6);
        NumField("money", "Custom amount", moneyAmount, 1, 100_000_000, v => moneyAmount = v);
        GUILayout.BeginHorizontal();
        if (Ui.Button($"+ ${Short(moneyAmount)}  [{Plugin.KeyMoney.Value}]", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− ${Short(moneyAmount)}", Ui.Tab)) AddMoney(-moneyAmount);
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Experience");
        xpAmount = Ui.Chips(xpAmount, XpPresets, v => Short(v) + " XP", 6);
        if (Ui.Button($"+ {Short(xpAmount)} XP", Ui.Primary)) AddXP(xpAmount);
        Ui.EndCard();

        int sp = 0;
        try { sp = Game.Me != null ? Game.Me.skillManager.skillPoints : 0; } catch { }
        Ui.BeginCard("Skill Points");
        GUILayout.BeginHorizontal();
        Ui.Label($"<b>{sp}</b> points", Ui.Text, Ui.W(120));
        if (Ui.Button("+1", Ui.Chip)) AddSkillPoints(1);
        if (Ui.Button("+5", Ui.Chip)) AddSkillPoints(5);
        if (Ui.Button("+10", Ui.Chip)) AddSkillPoints(10);
        GUILayout.EndHorizontal();
        Ui.EndCard();
    }
    void DrawItems()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }
        if (items == null || Time.unscaledTime - itemsScannedAt > 30f) ScanItems();

        Ui.BeginCard("Spawn Settings");
        spawnAtCrosshair = Ui.Chips(spawnAtCrosshair, new[] { true, false }, v => v ? "At Crosshair" : "In Front of Me", 2);
        spawnCount = Ui.Chips(spawnCount, SpawnCounts, v => v + "x", 4);
        Ui.EndCard();

        Ui.BeginCard("Browse Items");
        string cat = Ui.Chips(itemCategory, Categories, c => c, 4);
        if (cat != itemCategory) { itemCategory = cat; itemPage = 0; }

        // search box: typed letters come from Input.inputString while it is active
        GUILayout.BeginHorizontal();
        Ui.Label("Search", Ui.Sub, Ui.W(60));
        // Only the search text itself. While typing, the glowing frame shows the field is active.
        if (Ui.Button(search, searching ? Ui.FieldOn : Ui.Field)) { searching = true; searchFieldClickFrame = Time.frameCount; editId = null; }
        if (search.Length > 0 && Ui.Button("✕", Ui.Chip, Ui.W(36))) { search = ""; itemPage = 0; }
        GUILayout.EndHorizontal();

        var filtered = items.Where(e => (itemCategory == "All" || e.Category == itemCategory)
                                        && (search.Length == 0 || e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        int pages = Math.Max(1, (filtered.Count + PageSize - 1) / PageSize);
        itemPage = Mathf.Clamp(itemPage, 0, pages - 1);

        Ui.Label($"{filtered.Count} items", Ui.Sub);
        if (filtered.Count == 0) Ui.Label("Nothing found.", Ui.Sub);
        foreach (var e in filtered.Skip(itemPage * PageSize).Take(PageSize))
        {
            bool sel = selectedItem == e;
            string label = $"{e.Name}   <size=12><color=#b8c2d6>{e.Category}</color></size>";
            if (Ui.Button(label, sel ? Ui.RowOn : Ui.Row)) { selectedItem = e; searching = false; }
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

        string btn = selectedItem != null ? $"SPAWN {spawnCount}x {selectedItem.Name}" : "Select an item above";
        if (Ui.Button(btn, Ui.Primary) && selectedItem != null) SpawnItem(selectedItem, spawnCount);
    }

    void HandleSearchInput()
    {
        foreach (char c in Input.inputString)
        {
            if (c == '\b') { if (search.Length > 0) search = search.Substring(0, search.Length - 1); }
            else if (c == '\n' || c == '\r') searching = false;
            else if (!char.IsControl(c) && search.Length < 30) search += c;
            itemPage = 0;
        }
        if (Input.GetKeyDown(KeyCode.Escape)) searching = false;
    }

    Vector3 SpawnPoint(int i, int count)
    {
        var me = Game.Me;
        var cam = Camera.main;
        Vector3 fwd = cam != null ? cam.transform.forward : me.transform.forward;
        Vector3 center;
        if (spawnAtCrosshair && cam != null && Physics.Raycast(cam.transform.position, fwd, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            center = hit.point + hit.normal * 0.4f + Vector3.up * 0.3f;
        else
        {
            var f = fwd; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            center = me.transform.position + f * 2f + Vector3.up * 1.2f;
        }
        // stack multiple items a little apart so physics doesn't explode
        int perRow = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        var flat = fwd; flat.y = 0; if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward; flat.Normalize();
        var right = Vector3.Cross(Vector3.up, flat);
        return center + right * ((i % perRow - (perRow - 1) / 2f) * 0.45f) + flat * ((i / perRow) * 0.45f) + Vector3.up * (i * 0.02f);
    }

    void SpawnItem(ItemEntry e, int count)
    {
        if (!Check()) return;
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            try
            {
                var no = Game.WM.Runner.Spawn(e.Item.itemDropPrefab, SpawnPoint(i, count), Quaternion.identity);
                if (no != null) ok++;
            }
            catch (Exception ex) { Plugin.L.LogError(ex); break; }
        }
        SetStatus(ok > 0 ? $"Spawned {ok}x {e.Name}" : "Could not spawn - see log");
    }

    void DrawSettings()
    {
        Ui.BeginCard("Theme");
        string th = Ui.Chips(Ui.CurrentTheme, Ui.ThemeNames, n => n, 3);
        if (th != Ui.CurrentTheme) { Ui.SetTheme(th); Plugin.Theme.Value = th; SetStatus($"Theme: {th}"); }
        Ui.EndCard();

        Ui.BeginCard("Keybinds");
        Ui.Label("Click a key, then press the new key (Esc = cancel).", Ui.Sub);
        KeyRow("Open menu", Plugin.KeyMenu);
        KeyRow("Full heal", Plugin.KeyHeal);
        KeyRow("Call off cops", Plugin.KeyCops);
        KeyRow("Add money", Plugin.KeyMoney);
        Ui.EndCard();

        Ui.BeginCard("Save");
        Ui.Label("Saves all toggles so they are active the next time you play.", Ui.Sub);
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
        if (!Game.InGame) { SetStatus("Load into a game first"); return false; }
        if (!Game.Allowed) { SetStatus("Disabled in online sessions with other players"); return false; }
        return true;
    }

    void Heal()
    {
        if (!Check()) return;
        try { Game.Me.Heal(9999f); SetStatus("Fully healed"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void CallOffCops()
    {
        if (!Check()) return;
        try { Game.WM.policeManager.RPC_CallOffCops(); SetStatus("Cops called off"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void AddMoney(long amount)
    {
        if (!Check()) return;
        try
        {
            Game.WM.RPC_AddMoney((int)Math.Clamp(amount, int.MinValue, int.MaxValue));
            SetStatus((amount >= 0 ? "+" : "−") + $"$ {Math.Abs(amount):N0}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void AddXP(long amount)
    {
        if (!Check()) return;
        try { Game.WM.RPC_AddXP((int)Math.Min(amount, int.MaxValue)); SetStatus($"+{amount:N0} XP"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void AddSkillPoints(int n)
    {
        if (!Check()) return;
        try { Game.Me.skillManager.skillPoints += n; SetStatus($"+{n} skill points"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }
}
