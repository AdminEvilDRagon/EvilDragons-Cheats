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
    public const string Version = "1.2.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<KeyCode> KeyMenu, KeyHeal, KeyCops, KeyMoney, KeyTeleport, KeyKillAll;
    internal static ConfigEntry<string> Theme;

    void Awake()
    {
        L = Logger;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", KeyCode.F1, "Open/close menu");
        KeyHeal = Config.Bind("Hotkeys", "Heal", KeyCode.F2, "Full heal");
        KeyCops = Config.Bind("Hotkeys", "CallOffCops", KeyCode.F3, "Call off the police");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", KeyCode.F4, "Add the selected money amount");
        KeyTeleport = Config.Bind("Hotkeys", "TeleportToCrosshair", KeyCode.F5, "Teleport to where you aim");
        KeyKillAll = Config.Bind("Hotkeys", "KillAllNPCs", KeyCode.F6, "Kill all NPCs");
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

    // ---- God mode also covers the police taser and sleep darts (they bypass normal damage)
    [HarmonyPrefix, HarmonyPatch(typeof(FPP_Player), nameof(FPP_Player.RPC_Tazed))]
    static bool NoTaser(FPP_Player __instance) => !(CheatMenu.GodMode && __instance == Game.Me && Game.Allowed);

    [HarmonyPrefix, HarmonyPatch(typeof(FPP_Player), nameof(FPP_Player.DealSleepiness))]
    static bool NoSleepDarts(FPP_Player __instance) => !(CheatMenu.GodMode && __instance == Game.Me && Game.Allowed);

    [HarmonyPrefix, HarmonyPatch(typeof(FPP_Player), nameof(FPP_Player.PlayerDeath))]
    static bool NoDeath(FPP_Player __instance) => !(CheatMenu.GodMode && __instance == Game.Me && Game.Allowed);

    // ---- World border: the sniper that kills you when you leave the map
    [HarmonyPrefix, HarmonyPatch(typeof(BoundsSniper), "Update")]
    static void NoBorderSniper(BoundsSniper __instance)
    {
        if (CheatMenu.NoWorldBorder && Game.Allowed && __instance.playerToKill == Game.Me) __instance.killedPlayer = true;
    }

    // ---- Immune to pepper spray
    [HarmonyPrefix, HarmonyPatch(typeof(FPP_Player), nameof(FPP_Player.GotPepperSprayed))]
    static bool NoPepper(FPP_Player __instance) => !(CheatMenu.NoPepperSpray && __instance == Game.Me && Game.Allowed);

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
    public static bool NoPepperSpray, NoWorldBorder;
    public static bool GodMode, NoPolice, NoHeat, Unbreakable, InstantLockpick, NoDurability, RapidFire;
    public static float SpeedMult = 1f;

    bool show, infStamina, undetectable, silent, infAmmo, noAttackStamina, fuseHelper;

    // fuse minigame helper: the game shuffles the fuse list on start – that shuffled list is the solution
    FuseMinigame activeFuse;
    float fuseScanAt;

    void DrawFuseOverlay()
    {
        if (Time.unscaledTime >= fuseScanAt)
        {
            fuseScanAt = Time.unscaledTime + 0.25f;
            activeFuse = FindObjectOfType<FuseMinigame>();
        }
        var mg = activeFuse;
        if (mg == null || mg.fuses == null || mg.mainCam == null) return;

        Ui.Init();
        for (int i = 0; i < mg.fuses.Count; i++)
        {
            var f = mg.fuses[i];
            if (f == null || f.isTakenOff) continue;
            var sp = mg.mainCam.WorldToScreenPoint(f.transform.position);
            if (sp.z < 0f) continue;
            bool next = i == mg.currentFuseIndex;
            float size = next ? 46f : 34f;
            var rect = new Rect(sp.x - size / 2f, Screen.height - sp.y - size / 2f, size, size);
            var style = Ui.Styled ? (next ? Ui.ChipOn : Ui.KeyCap) : GUI.skin.box;
            GUI.Label(rect, (i + 1).ToString(), style);
        }
    }
    float damageMult = 1f, rangeMult = 1f, attackSpeed = 1f;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Stealth", "Gear", "Money", "Items", "NPCs", "Teleport", "Settings" };

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
    bool searchAllSelected;
    int itemPage, spawnCount = 1;
    bool spawnAtCrosshair = true;
    ItemEntry selectedItem;
    static readonly string[] Categories = { "All", "Weapons", "Ammo", "Tools", "Skills", "Plans", "Loot", "Money", "Other" };
    static readonly int[] SpawnCounts = { 1, 5, 10, 25 };
    const int PageSize = 10;

    // heist plans: items that a location requires before you can rob it
    class HeistPlan { public string Location; public List<Item> Items; }
    List<HeistPlan> heistPlans = new();
    HashSet<Item> planItems = new();

    string Category(Item i)
    {
        if (planItems.Contains(i)) return "Plans";
        if (i.isSkillBook) return "Skills";
        if (i.isWeapon) return "Weapons";
        if (i.isAmmo) return "Ammo";
        if (i.isMoney || i.isCreditCard || i.isGoldenCard) return "Money";
        if (i.isLoot || i.isJewelry) return "Loot";
        if (i.isKey || i.isAccessCard || i.isDrone || i.isFirstAid || i.isArmorVest || i.isItemUpgrade || i.isCarFuel) return "Tools";
        return "Other";
    }

    static string DisplayName(Item i)
    {
        string n = null;
        try { n = i.GetItemName(); } catch { }
        if (string.IsNullOrEmpty(n))
        {
            try { if (!string.IsNullOrEmpty(i.itemName)) n = I2.Loc.LocalizationManager.GetTranslation(i.itemName); } catch { }
            if (string.IsNullOrEmpty(n)) n = !string.IsNullOrEmpty(i.itemName) ? i.itemName : i.name;
            if (!string.IsNullOrEmpty(i.itemNameExtra)) n += " " + i.itemNameExtra;
        }
        return n;
    }

    static string LocationName(Location l)
    {
        string n = l.locationName;
        if (l.translateLocationName) try { var t = I2.Loc.LocalizationManager.GetTranslation(l.locationName); if (!string.IsNullOrEmpty(t)) n = t; } catch { }
        return n;
    }

    static bool CanSpawn(Item i) => i != null && i.itemDropPrefab != null && i.itemDropPrefab.GetComponent<Fusion.NetworkObject>() != null;

    bool loggedSkipped;
    void ScanItems()
    {
        itemsScannedAt = Time.unscaledTime;

        // every place the game keeps item lists, not only what happens to be loaded right now
        var all = new List<Item>();
        all.AddRange(Resources.FindObjectsOfTypeAll<Item>());
        try { if (Computer.Instance != null && Computer.Instance.itemScripts != null) all.AddRange(Computer.Instance.itemScripts); } catch { }

        heistPlans = new List<HeistPlan>();
        planItems = new HashSet<Item>();
        try
        {
            if (Game.WM != null && Game.WM.locations != null)
                foreach (var loc in Game.WM.locations)
                {
                    if (loc == null || !loc.requireItems || loc.requiredItems == null || loc.requiredItems.Count == 0) continue;
                    var req = loc.requiredItems.Where(x => x != null).ToList();
                    heistPlans.Add(new HeistPlan { Location = LocationName(loc), Items = req });
                    foreach (var it in req) { planItems.Add(it); all.Add(it); }
                }
        }
        catch { }

        var seen = new HashSet<int>();   // by instance, several items can share an asset name
        var list = new List<ItemEntry>();
        var skipped = new List<string>();
        foreach (var i in all)
        {
            if (i == null || !seen.Add(i.GetInstanceID())) continue;
            if (!CanSpawn(i)) { skipped.Add(i.name); continue; }
            list.Add(new ItemEntry { Item = i, Name = DisplayName(i), Category = Category(i) });
        }
        items = list.OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();

        if (!loggedSkipped && skipped.Count > 0)
        {
            loggedSkipped = true;
            Plugin.L.LogInfo($"{list.Count} spawnable items, {skipped.Count} without a drop prefab: {string.Join(", ", skipped.Take(40))}");
        }
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
        fuseHelper = Saved.Get("FuseOrder", false);
        NoPepperSpray = Saved.Get("ImmuneToPepperSpray", false);
        NoWorldBorder = Saved.Get("NoWorldBorder", false);
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
        Saved.Set("FuseOrder", fuseHelper);
        Saved.Set("ImmuneToPepperSpray", NoPepperSpray);
        Saved.Set("NoWorldBorder", NoWorldBorder);
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
            if (Input.GetKeyDown(Plugin.KeyTeleport.Value)) TeleportToCrosshair();
            if (Input.GetKeyDown(Plugin.KeyKillAll.Value)) KillAllNpcs();
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
        if (GodMode && allowed && me.sleepiness > 0f) me.sleepiness = 0f;

        // no world border: the leave-the-map countdown never starts
        if (NoWorldBorder && allowed && (me.showMapExitWarning || me.amountOfExitTriggers > 0))
        {
            me.showMapExitWarning = false;
            me.amountOfExitTriggers = 0;
        }

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
        if (fuseHelper && Game.Allowed)
            try { DrawFuseOverlay(); } catch (Exception e) { var m = e.ToString(); if (m != lastGuiError) { lastGuiError = m; Plugin.L.LogError(e); } }
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
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Ui.KeyHint(Plugin.KeyTeleport.Value.ToString(), "Teleport");
        Ui.KeyHint(Plugin.KeyKillAll.Value.ToString(), "Kill all");
        Ui.KeyHint(Plugin.KeyMenu.Value.ToString(), "Menu");
        GUILayout.EndHorizontal();
        Ui.Space(4);

        if (!Game.InGame) Ui.Label("Load into a game first.", Ui.Sub);
        else if (!Game.Allowed) Ui.Label($"<color=#ff5a7a><b>Online session with other players – cheats are disabled.</b></color>", Ui.Sub);
        else Ui.Label($"<color={Ui.GHex}>●</color> Single-player – cheats active", Ui.Sub);
        Ui.Space(4);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i == 4) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }   // two rows of tabs
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; editId = null; rebinding = null; searching = false; }
        }
        GUILayout.EndHorizontal();
        Ui.Space(4);

        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawStealth(); break;
            case 2: DrawGear(); break;
            case 3: DrawMoney(); break;
            case 4: DrawItems(); break;
            case 5: DrawNpcs(); break;
            case 6: DrawTeleport(); break;
            case 7: DrawSettings(); break;
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
        Ui.Label("God Mode also protects you from police tasers, sleep darts and the sniper.", Ui.Sub);
        NoWorldBorder = Ui.Toggle(NoWorldBorder, "No World Border", "leave the map without dying");
        bool pep = Ui.Toggle(NoPepperSpray, "Immune to Pepper Spray", "no blinding effect");
        if (pep != NoPepperSpray)
        {
            NoPepperSpray = pep;
            if (pep) ClearPepper();
        }
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

        Ui.BeginCard("Locks & Electronics");
        Ui.Toggle2(ref InstantLockpick, "Instant Lockpick", ref Unbreakable, "Unbreakable Picks");
        fuseHelper = Ui.Toggle(fuseHelper, "Fuse Order", "shows which fuse to pull next");
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

        if (heistPlans.Count > 0)
        {
            Ui.BeginCard("Heist Plans", "Spawns what a robbery needs – pick it up or put it in your truck");
            for (int i = 0; i < heistPlans.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < Math.Min(i + 2, heistPlans.Count); k++)
                {
                    var hp = heistPlans[k];
                    if (Ui.Button(hp.Location, Ui.Chip)) SpawnPlan(hp);
                }
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();
        }

        Ui.BeginCard("Browse Items");
        string cat = Ui.Chips(itemCategory, Categories, c => c, 5);
        if (cat != itemCategory) { itemCategory = cat; itemPage = 0; }

        // search box: typed letters come from Input.inputString while it is active
        GUILayout.BeginHorizontal();
        Ui.Label("Search", Ui.Sub, Ui.W(60));
        // Only the search text itself. While typing, the glowing frame shows the field is active.
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
        // Ctrl+A selects the whole search text: the next key replaces it, Backspace/Delete clears it
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && Input.GetKeyDown(KeyCode.A)) searchAllSelected = search.Length > 0;
        if (searchAllSelected && Input.GetKeyDown(KeyCode.Delete)) { search = ""; searchAllSelected = false; itemPage = 0; }

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
        if (Input.GetKeyDown(KeyCode.Escape)) searching = false;
        if (!searching) searchAllSelected = false;
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

    void SpawnPlan(HeistPlan hp)
    {
        if (!Check()) return;
        int ok = 0;
        var spawnable = hp.Items.Where(CanSpawn).ToList();
        for (int i = 0; i < spawnable.Count; i++)
        {
            try { if (Game.WM.Runner.Spawn(spawnable[i].itemDropPrefab, SpawnPoint(i, spawnable.Count), Quaternion.identity) != null) ok++; }
            catch (Exception ex) { Plugin.L.LogError(ex); }
        }
        if (ok == 0) SetStatus($"Could not spawn the plan for {hp.Location} - see log");
        else SetStatus($"Spawned plan for {hp.Location}: " + string.Join(", ", spawnable.Select(DisplayName)));
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

    // ---------------- NPCs ----------------
    class NpcEntry { public Fusion.NetworkObject Prefab; public string Name; }
    List<NpcEntry> npcPrefabs;
    NpcEntry selectedNpc;
    int npcSpawnCount = 1, npcPage;
    static readonly int[] NpcCounts = { 1, 3, 5, 10 };

    static string NiceName(string n) => string.IsNullOrEmpty(n) ? "?" : n.Replace("(Clone)", "").Replace("_", " ").Trim();

    void ScanNpcs()
    {
        npcPrefabs = new List<NpcEntry>();
        try
        {
            var table = Fusion.NetworkProjectConfig.Global.PrefabTable;
            foreach (var (id, _) in table.GetEntries())
            {
                try
                {
                    var no = table.Load(id, true);
                    if (no != null && no.GetComponent<NPC>() != null)
                        npcPrefabs.Add(new NpcEntry { Prefab = no, Name = NiceName(no.name) });
                }
                catch { }
            }
        }
        catch (Exception e) { Plugin.L.LogError(e); }
        npcPrefabs = npcPrefabs.GroupBy(x => x.Name).Select(g => g.First()).OrderBy(x => x.Name).ToList();
        Plugin.L.LogInfo($"Found {npcPrefabs.Count} NPC types");
    }

    void DrawNpcs()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }

        int alive = 0;
        try { alive = FindObjectsOfType<NPC>().Count(n => n != null && !n.knockedDown); } catch { }
        Ui.BeginCard("Kill All", $"{alive} NPCs standing in this area");
        if (Ui.Button($"Kill All NPCs  [{Plugin.KeyKillAll.Value}]", Ui.Danger)) KillAllNpcs();
        Ui.EndCard();

        if (npcPrefabs == null) ScanNpcs();
        Ui.BeginCard("Spawn NPCs", "Appear at your crosshair or in front of you");
        spawnAtCrosshair = Ui.Chips(spawnAtCrosshair, new[] { true, false }, v => v ? "At Crosshair" : "In Front of Me", 2);
        npcSpawnCount = Ui.Chips(npcSpawnCount, NpcCounts, v => v + "x", 4);
        if (npcPrefabs.Count == 0) Ui.Label("No NPC types found.", Ui.Sub);
        int pages = Math.Max(1, (npcPrefabs.Count + PageSize - 1) / PageSize);
        npcPage = Mathf.Clamp(npcPage, 0, pages - 1);
        foreach (var e in npcPrefabs.Skip(npcPage * PageSize).Take(PageSize))
            if (Ui.Button(e.Name, selectedNpc == e ? Ui.RowOn : Ui.Row)) selectedNpc = e;
        if (pages > 1)
        {
            GUILayout.BeginHorizontal();
            if (Ui.Button("◀", Ui.Chip, Ui.W(50))) npcPage--;
            Ui.Label($"Page {npcPage + 1} / {pages}", Ui.Sub);
            if (Ui.Button("▶", Ui.Chip, Ui.W(50))) npcPage++;
            GUILayout.EndHorizontal();
        }
        Ui.EndCard();

        string btn = selectedNpc != null ? $"SPAWN {npcSpawnCount}x {selectedNpc.Name}" : "Select an NPC above";
        if (Ui.Button(btn, Ui.Primary) && selectedNpc != null) SpawnNpc(selectedNpc, npcSpawnCount);
    }

    void SpawnNpc(NpcEntry e, int count)
    {
        if (!Check()) return;
        int ok = 0;
        for (int i = 0; i < count; i++)
        {
            try
            {
                var pos = SpawnPoint(i, count);
                var rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(Game.Me.transform.position - pos, Vector3.up).normalized + Vector3.forward * 0.0001f);
                if (Game.WM.Runner.Spawn(e.Prefab, pos, rot) != null) ok++;
            }
            catch (Exception ex) { Plugin.L.LogError(ex); break; }
        }
        SetStatus(ok > 0 ? $"Spawned {ok}x {e.Name}" : "Could not spawn - see log");
    }

    void KillAllNpcs()
    {
        if (!Check()) return;
        int n = 0;
        foreach (var npc in FindObjectsOfType<NPC>())
        {
            try
            {
                if (npc == null || npc.knockedDown) continue;
                npc.RPC_DealDamage(9999f, false, true);
                n++;
            }
            catch (Exception e) { Plugin.L.LogError(e); }
        }
        SetStatus(n > 0 ? $"Took down {n} NPC(s)" : "No NPCs standing");
    }

    // ---------------- teleport ----------------
    readonly Vector3?[] savedPositions = new Vector3?[3];

    void DrawTeleport()
    {
        if (!Game.InGame) { Ui.Label("Load into a game first.", Ui.Text); return; }

        Ui.BeginCard("Quick Teleport");
        if (Ui.Button($"To Crosshair  [{Plugin.KeyTeleport.Value}]", Ui.Primary)) TeleportToCrosshair();
        GUILayout.BeginHorizontal();
        if (Ui.Button("To Truck", Ui.Tab)) TeleportToTruck();
        if (Ui.Button("To Start Point", Ui.Tab)) TeleportToSpawn();
        GUILayout.EndHorizontal();
        Ui.EndCard();

        Ui.BeginCard("Saved Positions", "Save where you stand and jump back later (this session)");
        for (int i = 0; i < savedPositions.Length; i++)
        {
            GUILayout.BeginHorizontal();
            Ui.Label($"Slot {i + 1}", Ui.Text, Ui.W(70));
            if (Ui.Button("Save here", Ui.Chip)) { savedPositions[i] = Game.Me.transform.position; SetStatus($"Position saved in slot {i + 1}"); }
            if (savedPositions[i].HasValue)
            {
                if (Ui.Button("Teleport", Ui.ChipOn)) TeleportTo(savedPositions[i].Value, $"slot {i + 1}");
            }
            else Ui.Label("<color=#6a7080>empty</color>", Ui.Sub, Ui.W(110));
            GUILayout.EndHorizontal();
        }
        Ui.EndCard();
    }

    void TeleportTo(Vector3 pos, string where)
    {
        if (!Check()) return;
        try
        {
            var me = Game.Me;
            me.SetPlayerPosition(pos, me.transform.eulerAngles, snapToGroundd: true);
            SetStatus("Teleported to " + where);
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void TeleportToCrosshair()
    {
        if (!Check()) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            TeleportTo(hit.point + hit.normal * 0.5f + Vector3.up * 0.2f, "crosshair");
        else SetStatus("Aim at something to teleport there");
    }

    void TeleportToTruck()
    {
        if (!Check()) return;
        var t = Game.WM.truckObj;
        if (t == null) { SetStatus("No truck in this area"); return; }
        TeleportTo(t.position + t.right * 3f + Vector3.up * 0.5f, "truck");
    }

    void TeleportToSpawn()
    {
        if (!Check()) return;
        try
        {
            var sp = Game.WM.spawnPoints[Game.WM.spawnPointIndex].playerSpawns[0];
            TeleportTo(sp.position, "start point");
        }
        catch { SetStatus("No start point in this area"); }
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
        KeyRow("Teleport to crosshair", Plugin.KeyTeleport);
        KeyRow("Kill all NPCs", Plugin.KeyKillAll);
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

    // if you are already sprayed when switching it on, end the effect right away
    void ClearPepper()
    {
        try
        {
            var me = Game.Me;
            if (me != null && Game.Allowed && me.isPepperSprayed) me.pepperSprayTimer = 0f;
        }
        catch { }
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
