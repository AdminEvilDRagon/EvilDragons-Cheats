using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.InputSystem;
using EvilDragon;

namespace KebabChefsCheats;

[BepInPlugin(Guid, "EvilDragon's Cheats - Kebab Chefs! Prologue", Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "evildragon.cheats.kebabchefs";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static ConfigFile Cfg;
    internal static ConfigEntry<string> KeyMenu, KeyMoney, Theme;

    public override void Load()
    {
        L = Log;
        Cfg = Config;
        KeyMenu = Config.Bind("Hotkeys", "Menu", "F1", "Open/close menu (InputSystem key name, e.g. F1, K, Insert)");
        KeyMoney = Config.Bind("Hotkeys", "AddMoney", "F2", "Add the selected amount of money");
        Theme = Config.Bind("Interface", "Theme", "Dragon", "Menu theme: " + string.Join(", ", Ui.ThemeNames));
        Ui.InitialTheme = Theme.Value;
        Ui.Log = m => Log.LogWarning(m);

        ClassInjector.RegisterTypeInIl2Cpp<CheatMenu>();
        var go = new GameObject("EvilDragonCheatMenu");
        GameObject.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CheatMenu>();
        new Harmony(Guid).PatchAll(typeof(Patches));
        Log.LogInfo($"EvilDragon's Cheats v{Version} loaded");
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

public static class Patches
{
    // Blackjack: pick the card that is drawn (you get the best card, the dealer the worst one)
    [HarmonyPrefix, HarmonyPatch(typeof(BlackjackManager), nameof(BlackjackManager.DrawCardToSlotServerRpc))]
    static void DrawServer(int slot, ref int card) { if (CheatMenu.RigBlackjack && CheatMenu.Allowed) card = CheatMenu.PickCard(slot, card); }

    [HarmonyPrefix, HarmonyPatch(typeof(BlackjackGame), nameof(BlackjackGame.DrawCardToSlot))]
    static void DrawLocal(int slotIdx, ref int cardIdx) { if (CheatMenu.RigBlackjack && CheatMenu.Allowed) cardIdx = CheatMenu.PickCard(slotIdx, cardIdx); }

    // ingredients never go bad
    [HarmonyPrefix, HarmonyPatch(typeof(IngredientManager), nameof(IngredientManager.RotItems))]
    static bool RotItems() => !(CheatMenu.NoRot && CheatMenu.Allowed);

    // more money from every payment
    [HarmonyPrefix, HarmonyPatch(typeof(RestaurantManager), nameof(RestaurantManager.EarnMoney))]
    static void EarnMoney(ref float moneyAmount, MoneyTransactionType type, CurrencyType currencyType, bool isLoan)
    {
        if (CheatMenu.IncomeMult > 1 && moneyAmount > 0 && !isLoan && currencyType == CurrencyType.Money && !CheatMenu.InOwnCall && CheatMenu.Allowed)
            moneyAmount *= CheatMenu.IncomeMult;
    }
}

public class CheatMenu : MonoBehaviour
{
    public CheatMenu(IntPtr ptr) : base(ptr) { }

    public static bool NoRot, InOwnCall, Allowed, RigBlackjack;
    public static int IncomeMult = 1;

    bool show;
    int tab;
    string lastGuiError;
    static readonly string[] Tabs = { "Player", "Money", "Restaurant", "Items", "Settings" };

    // player
    float speedMult = 1f, jumpMult = 1f;
    bool blackjackHelp = true, instantInteract;
    Scaled progressBar;
    readonly Dictionary<IntPtr, float> origInteract = new();
    float interactScan;
    Scaled walk, run, jump;

    // A game value we multiply; if the game changes it, that becomes the new base.
    struct Scaled
    {
        float baseV, written; bool has;
        public float Apply(float field, float mult)
        {
            if (!has || Math.Abs(field - written) > 0.0001f) { baseV = field; has = true; }
            written = baseV * mult;
            return written;
        }
        public void Reset() { has = false; }
    }
    // time
    bool freezeTime;
    float timeMult = 1f, baseDaySpeed = -1f, writtenDaySpeed = -1f;
    // money
    long moneyAmount = 10_000;
    static readonly long[] MoneyPresets = { 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000 };
    static readonly int[] MultPresets = { 1, 2, 5, 10, 50, 100 };
    // items
    List<Ingredient> ingredients;
    string search = "";
    bool searching, searchAll;
    Rect searchRect;
    int searchClickFrame = -1;
    int page, spawnCount = 1;
    static readonly int[] CountPresets = { 1, 5, 10, 25, 50 };
    const int PageSize = 12;

    string status = "";
    float statusTime;
    ConfigEntry<string> rebinding;
    bool settingsLoaded;
    float nextAllowedCheck;

    void SetStatus(string s) { status = s; statusTime = Time.unscaledTime; Plugin.L.LogInfo(s); }

    static string Short(long v) =>
        v >= 1_000_000_000 ? $"{v / 1_000_000_000d:0.#}B" :
        v >= 1_000_000 ? $"{v / 1_000_000d:0.#}M" :
        v >= 1_000 ? $"{v / 1_000d:0.#}K" : v.ToString();

    // ---------------- game access ----------------
    static RestaurantManager RM => GameServer.restaurantM;
    static GameData Data => RM != null ? RM.gameData : null;

    // Single-player only: off when this is someone else's game or other players joined ours.
    static bool CheckAllowed()
    {
        try
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return true;
            if (!nm.IsServer) return false;
            return UnityEngine.Object.FindObjectsOfType<PlayerCharacter>().Length <= 1;
        }
        catch { return true; }
    }

    // ---------------- settings ----------------
    void LoadSettings()
    {
        settingsLoaded = true;
        try
        {
            speedMult = Saved.Get("WalkSpeed", 1f);
            jumpMult = Saved.Get("JumpHeight", 1f);
            blackjackHelp = Saved.Get("BlackjackHelper", true);
            RigBlackjack = Saved.Get("RigBlackjack", false);
            instantInteract = Saved.Get("InstantInteract", false);
            timeMult = Saved.Get("TimeSpeed", 1f);
            freezeTime = Saved.Get("FreezeTime", false);
            NoRot = Saved.Get("NoRot", false);
            IncomeMult = Saved.Get("IncomeMultiplier", 1);
        }
        catch (Exception e) { Plugin.L.LogWarning("Load settings: " + e.Message); }
    }

    void SaveSettings()
    {
        try
        {
            Saved.Set("WalkSpeed", speedMult);
            Saved.Set("JumpHeight", jumpMult);
            Saved.Set("BlackjackHelper", blackjackHelp);
            Saved.Set("RigBlackjack", RigBlackjack);
            Saved.Set("InstantInteract", instantInteract);
            Saved.Set("TimeSpeed", timeMult);
            Saved.Set("FreezeTime", freezeTime);
            Saved.Set("NoRot", NoRot);
            Saved.Set("IncomeMultiplier", IncomeMult);
            Saved.Flush();
            SetStatus("Settings saved - they load automatically next time");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not save settings - see log"); }
    }

    // ---------------- keyboard ----------------
    static Key ParseKey(string s) => Enum.TryParse<Key>(s, true, out var k) ? k : Key.None;

    static bool Pressed(Keyboard kb, Key k)
    {
        if (k == Key.None) return false;
        try { return kb[k].wasPressedThisFrame; } catch { return false; }
    }

    static Key[] allKeys;

    void HandleRebind(Keyboard kb)
    {
        if (Pressed(kb, Key.Escape)) { rebinding = null; SetStatus("Cancelled"); return; }
        allKeys ??= (Key[])Enum.GetValues(typeof(Key));
        foreach (var k in allKeys)
        {
            if (k == Key.None || k == Key.Escape || k == Key.IMESelected) continue;
            if (Pressed(kb, k))
            {
                rebinding.Value = k.ToString();
                SetStatus($"{rebinding.Definition.Key} → {k}");
                rebinding = null;
                return;
            }
        }
    }

    // Typing into the item search (text fields are not available in this game's IMGUI).
    void HandleSearch(Keyboard kb)
    {
        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        if (ctrl && Pressed(kb, Key.A)) { searchAll = search.Length > 0; return; }
        if (Pressed(kb, Key.Enter) || Pressed(kb, Key.NumpadEnter) || Pressed(kb, Key.Escape)) { searching = false; searchAll = false; return; }
        if (Pressed(kb, Key.Backspace) || Pressed(kb, Key.Delete))
        {
            if (searchAll || Pressed(kb, Key.Delete)) search = "";
            else if (search.Length > 0) search = search.Substring(0, search.Length - 1);
            searchAll = false; page = 0;
            return;
        }
        if (ctrl) return;
        char typed = '\0';
        for (Key k = Key.A; k <= Key.Z; k++) if (Pressed(kb, k)) typed = (char)('a' + (k - Key.A));
        for (Key k = Key.Digit1; k <= Key.Digit0; k++) if (Pressed(kb, k)) typed = k == Key.Digit0 ? '0' : (char)('1' + (k - Key.Digit1));
        if (Pressed(kb, Key.Space)) typed = ' ';
        if (typed == '\0') return;
        if (searchAll) { search = ""; searchAll = false; }
        if (search.Length < 24) { search += typed; page = 0; }
    }

    // A click anywhere outside the search field ends the search.
    void CheckSearchClickAway()
    {
        if (!searching) return;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Time.frameCount == searchClickFrame) return;
        var pos = mouse.position.ReadValue();
        var gui = new Vector2(pos.x, Screen.height - pos.y);
        if (!searchRect.Contains(gui)) { searching = false; searchAll = false; }
    }

    // ---------------- update ----------------
    void Update()
    {
        if (!settingsLoaded) LoadSettings();
        if (Time.unscaledTime >= nextAllowedCheck) { nextAllowedCheck = Time.unscaledTime + 1f; Allowed = CheckAllowed(); }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (rebinding != null) HandleRebind(kb);
            else if (searching)
            {
                // the menu key always closes the menu, even while typing
                if (Pressed(kb, ParseKey(Plugin.KeyMenu.Value))) show = false;
                else { HandleSearch(kb); CheckSearchClickAway(); }
            }
            else
            {
                if (Pressed(kb, ParseKey(Plugin.KeyMenu.Value))) show = !show;
                if (Allowed && Pressed(kb, ParseKey(Plugin.KeyMoney.Value))) AddMoney(moneyAmount);
            }
        }
        if (!show) { rebinding = null; searching = false; searchAll = false; }
        SetPlayerControl(!show);

        try { Tick(); } catch (Exception e) { Plugin.L.LogError(e); }
    }

    // While the menu is open the player does not walk or look around.
    bool controlOff, prevMove, prevLook;
    void SetPlayerControl(bool on)
    {
        if (on != controlOff) return;
        try
        {
            var fpc = FirstPersonController.Instance;
            if (fpc == null) { controlOff = false; return; }
            if (!on)
            {
                prevMove = fpc._CanMove_k__BackingField; prevLook = fpc.canRotateView;
                fpc._CanMove_k__BackingField = false; fpc.canRotateView = false;
            }
            else
            {
                // always give control back (the game locks it again itself when it needs to)
                fpc._CanMove_k__BackingField = true; fpc.canRotateView = true;
            }
            controlOff = !on;
        }
        catch (Exception e) { Plugin.L.LogWarning("Player control: " + e.Message); controlOff = !on; }
    }

    void LateUpdate()
    {
        if (show) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    void Tick()
    {
        // walk / run speed and jump (if the game changes a value, that becomes the new base)
        var fpc = FirstPersonController.Instance;
        if (fpc != null)
        {
            float m = Allowed ? speedMult : 1f, j = Allowed ? jumpMult : 1f;
            if (fpc.m_WalkSpeed <= 0f) { walk.Reset(); run.Reset(); jump.Reset(); m = j = 1f; }
            fpc.m_WalkSpeed = walk.Apply(fpc.m_WalkSpeed, m);
            fpc.m_RunSpeed = run.Apply(fpc.m_RunSpeed, m);
            fpc.m_JumpSpeed = jump.Apply(fpc.m_JumpSpeed, j);
        }
        else { walk.Reset(); run.Reset(); jump.Reset(); }

        // instant interact: no hold time, progress bars much faster
        var cii = GameServer.LocalItemInteract;
        if (cii != null) cii.progressBarSpeed = progressBar.Apply(cii.progressBarSpeed, Allowed && instantInteract ? 20f : 1f);
        else progressBar.Reset();
        bool wantInstant = Allowed && instantInteract;
        if (wantInstant && Time.unscaledTime >= interactScan)
        {
            interactScan = Time.unscaledTime + 1f;
            foreach (var it in UnityEngine.Object.FindObjectsOfType<Kebab.Interaction.Interaction>())
            {
                if (it == null) continue;
                var key = it.Pointer;
                if (!origInteract.ContainsKey(key)) origInteract[key] = it.interactTime;
                if (it.interactTime > 0f) it.interactTime = 0f;
            }
        }
        else if (!wantInstant && origInteract.Count > 0)
        {
            foreach (var it in UnityEngine.Object.FindObjectsOfType<Kebab.Interaction.Interaction>())
                if (it != null && origInteract.TryGetValue(it.Pointer, out var t)) it.interactTime = t;
            origInteract.Clear();
        }

        // day speed / freeze
        var day = GameServer.dayM;
        if (day != null)
        {
            if (baseDaySpeed < 0f || Math.Abs(day._dayProceedSpeed - writtenDaySpeed) > 0.0001f) baseDaySpeed = day._dayProceedSpeed;
            float mult = !Allowed ? 1f : freezeTime ? 0f : timeMult;
            writtenDaySpeed = baseDaySpeed * mult;
            day._dayProceedSpeed = writtenDaySpeed;
        }
        else baseDaySpeed = -1f;
    }

    // ---------------- actions ----------------
    void AddMoney(long amount, CurrencyType currency = CurrencyType.Money)
    {
        var rm = RM;
        if (rm == null) { SetStatus("Load into your restaurant first"); return; }
        try
        {
            InOwnCall = true;
            rm.EarnMoney(amount, MoneyTransactionType.None, currency, false);
            SetStatus($"{(amount >= 0 ? "+" : "")}{amount:N0} {(currency == CurrencyType.Money ? "money" : currency.ToString())}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
        finally { InOwnCall = false; }
    }

    void SetMoney(long amount)
    {
        var rm = RM;
        if (rm == null) return;
        try { InOwnCall = true; rm.SetMoney(amount, MoneyTransactionType.None, CurrencyType.Money); SetStatus($"Money set to {amount:N0}"); }
        catch (Exception e) { Plugin.L.LogError(e); }
        finally { InOwnCall = false; }
    }

    void AddXp(int amount)
    {
        var rm = RM;
        if (rm == null) return;
        try { rm.GainExperience(amount); SetStatus($"+{amount:N0} XP"); }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void AddCookPoints(int amount)
    {
        var d = Data;
        if (d == null) return;
        d.CookPoints = Math.Max(0, d.CookPoints + amount);
        SetStatus($"Cook points: {d.CookPoints}");
    }

    void MaxPerks()
    {
        var pm = GameServer.perkM;
        if (pm == null || pm.perkDatas == null) { SetStatus("Perks not loaded yet"); return; }
        int ups = 0;
        try
        {
            foreach (var pd in pm.perkDatas)
            {
                if (pd == null || pd.stageDatas == null) continue;
                for (int i = 0; i < 12 && pm.GetPerkStage(pd.perkType) < pd.stageDatas.Length; i++)
                {
                    int before = pm.GetPerkStage(pd.perkType);
                    if (!pm.ActivatePerk(pd.perkType, 0) || pm.GetPerkStage(pd.perkType) == before) break;
                    ups++;
                }
            }
            SetStatus($"Perks upgraded ({ups} levels)");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void ClearBills()
    {
        var bm = GameServer.bankM;
        if (bm == null) return;
        try
        {
            if (bm.activeDebts != null) for (int i = 0; i < bm.activeDebts.Length; i++) bm.activeDebts[i] = 0f;
            bm._RemainingCreditDebt_k__BackingField = 0f;
            SetStatus("All bills and loans paid off");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Error - see log"); }
    }

    void LoadIngredients()
    {
        try
        {
            ingredients = Resources.FindObjectsOfTypeAll<Ingredient>()
                .Where(i => i != null && i.itemPrefab != null && !i.isRottenIngredient)
                .GroupBy(i => i.ingredientKey).Select(g => g.First())
                .OrderBy(i => Nice(i.ingredientKey)).ToList();
            SetStatus($"{ingredients.Count} ingredients found");
        }
        catch (Exception e) { Plugin.L.LogError(e); ingredients = new List<Ingredient>(); }
    }

    // Creates the item the same way the game does (ItemManager.CreateItem), a little in front of the camera.
    void Spawn(Ingredient ing, int count)
    {
        if (ing == null || ing.itemPrefab == null) return;
        var im = GameServer.itemM;
        if (im == null) { SetStatus("Load into your restaurant first"); return; }
        try
        {
            var cam = Camera.main;
            Vector3 origin = cam != null ? cam.transform.position : (GameServer.LocalPlayerTransform != null ? GameServer.LocalPlayerTransform.position + Vector3.up * 1.5f : Vector3.zero);
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
            flat = flat.sqrMagnitude < 0.01f ? Vector3.forward : flat.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            string name = ing.itemPrefab.name;
            for (int i = 0; i < count; i++)
            {
                int col = i % 5, row = i / 5;
                Vector3 pos = origin + flat * (1.2f + row * 0.35f) + right * ((col - 2) * 0.3f) + Vector3.down * 0.4f;
                im.CreateItem(name, pos, Quaternion.identity, null);
            }
            SetStatus($"Spawned {count}x {Nice(ing.ingredientKey)}");
        }
        catch (Exception e) { Plugin.L.LogError(e); SetStatus("Could not spawn - see log"); }
    }

    static string Nice(string key)
    {
        if (string.IsNullOrEmpty(key)) return "?";
        var s = key.Replace("_", " ").Trim();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && char.IsLower(s[i - 1])) sb.Append(' ');
            sb.Append(i == 0 ? char.ToUpperInvariant(s[i]) : s[i]);
        }
        return sb.ToString();
    }

    // ---------------- blackjack helper ----------------
    string lastErr;
    void LogOnce(Exception e) { var m = e.Message; if (m != lastErr) { lastErr = m; Plugin.L.LogWarning("Blackjack helper: " + m); } }

    static BlackjackSlot MySlot(BlackjackGame game)
    {
        if (game == null || game.playerSlots == null) return null;
        ulong me = 0;
        try { var nm = Unity.Netcode.NetworkManager.Singleton; if (nm != null) me = nm.LocalClientId; } catch { }
        foreach (var slot in game.playerSlots)
            if (slot != null && slot.Player != null && slot.Player.ID == me) return slot;
        return null;
    }

    // Best total of a hand (aces count 11 unless that busts) and whether an ace still counts as 11.
    static int HandValue(BlackjackSlot slot, out bool soft, out int cards)
    {
        int sum = 0, aces = 0; cards = 0;
        if (slot.Cards != null)
            foreach (var c in slot.Cards)
            {
                if (c == null) continue;
                cards++;
                int v = c.CardPoint;
                if (v == 1 || v == 11) { aces++; sum += 1; } else sum += Math.Min(v, 10);
            }
        soft = aces > 0 && sum + 10 <= 21;
        return soft ? sum + 10 : sum;
    }

    // Basic strategy for a game where you can only hit or stay.
    static bool ShouldHit(int total, bool soft, int dealer)
    {
        if (dealer == 1) dealer = 11;
        if (soft)
        {
            if (total >= 19) return false;
            if (total == 18) return dealer >= 9;
            return true;
        }
        if (total >= 17) return false;
        if (total >= 13) return dealer >= 7;
        if (total == 12) return !(dealer >= 4 && dealer <= 6);
        return true;
    }

    // Which card index to draw into a slot. Player: the card that gets closest to 21 without going over.
    // Dealer: a card that makes him bust, otherwise the lowest card.
    public static int PickCard(int slotIdx, int wanted)
    {
        try
        {
            var game = GameServer.blackjackM != null ? GameServer.blackjackM.blackjackGame : null;
            if (game == null || game.cards == null || game.cards.Length == 0) return wanted;
            var slot = game.GetSlot(slotIdx);
            if (slot == null) return wanted;
            bool house = slot == game.houseSlot || slot.IsHouseSlot;
            bool mine = !house && slot == MySlot(game);
            if (!house && !mine) return wanted;
            int now = HandValue(slot, out bool soft, out _);
            int best = wanted, bestScore = int.MinValue;
            for (int i = 0; i < game.cards.Length; i++)
            {
                var c = game.cards[i];
                if (c == null) continue;
                int v = c.CardPoint;
                int total = AddCard(now, soft, v);
                int score = mine ? (total > 21 ? -100 : total) : (total > 21 ? 1000 : -total);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
        catch { return wanted; }
    }

    static int AddCard(int total, bool soft, int v)
    {
        bool ace = v == 1 || v == 11;
        int sum = total + (ace ? 11 : Math.Min(v, 10));
        int softAces = (soft ? 1 : 0) + (ace ? 1 : 0);
        while (sum > 21 && softAces > 0) { sum -= 10; softAces--; }
        return sum;
    }

    void DrawBlackjackAdvice()
    {
        var bm = GameServer.blackjackM;
        var hud = BlackjackHUD.Instance;
        if (bm == null || hud == null || hud.hitStayGO == null || !hud.hitStayGO.activeInHierarchy) return;
        var game = bm.blackjackGame;
        var mine = MySlot(game);
        if (mine == null || game.houseSlot == null) return;
        int total = HandValue(mine, out bool soft, out int n);
        if (n == 0) return;
        int dealer = 0;
        if (game.houseSlot.Cards != null && game.houseSlot.Cards.Count > 0 && game.houseSlot.Cards[0] != null)
            dealer = Math.Min(game.houseSlot.Cards[0].CardPoint, 11);
        bool hit = total < 21 && ShouldHit(total, soft, dealer == 0 ? 10 : dealer);

        Ui.Init();
        var r = new Rect(Screen.width / 2f - 170, Screen.height * 0.16f, 340, 92);
        if (Ui.Styled) GUI.Box(r, "", Ui.Panel); else GUI.Box(r, "");
        var big = hit ? Ui.Primary : Ui.Danger;
        string word = hit ? "HIT" : "STAY";
        var btn = new Rect(r.x + 16, r.y + 14, r.width - 32, 42);
        if (Ui.Styled) GUI.Label(btn, $"<size=24><b>{word}</b></size>", big); else GUI.Label(btn, word);
        string info = $"You {(soft ? "soft " : "")}{total}   ·   Dealer shows {(dealer == 11 || dealer == 1 ? "A" : dealer.ToString())}";
        var line = new Rect(r.x + 16, r.y + 60, r.width - 32, 24);
        if (Ui.Styled) GUI.Label(line, info, Ui.Sub); else GUI.Label(line, info);
    }

    // ---------------- GUI ----------------
    // This game strips GUILayout completely, so the menu uses the toolkit's own layout then.
    static bool probed;
    static void Probe()
    {
        probed = true;
        try { var r = new Rect(-500, -500, 10, 10); GUILayout.BeginArea(r); GUILayout.Label("x"); GUILayout.EndArea(); Ui.Manual = false; }
        catch { Ui.Manual = true; Plugin.L.LogInfo("GUILayout is stripped in this game - using manual layout"); }
    }

    void OnGUI()
    {
        if (blackjackHelp && Allowed) try { DrawBlackjackAdvice(); } catch (Exception e) { LogOnce(e); }
        if (!show) return;
        if (!probed) Probe();
        Ui.Init();
        bool areaOpen = false;
        try
        {
            var rect = new Rect(24, 24, 470, Screen.height - 48);
            Ui.BeginArea(rect, Ui.Panel);
            areaOpen = true;
            Draw();
            Ui.EndArea();
            areaOpen = false;
        }
        catch (Exception e)
        {
            var msg = e.ToString();
            if (msg != lastGuiError) { lastGuiError = msg; Plugin.L.LogError(e); }
            if (areaOpen) try { Ui.EndArea(); } catch { }
        }
    }

    void Draw()
    {
        bool logo = Ui.Styled && Ui.Logo != null && Ui.Logo.normal.background != null;
        Ui.BeginRow(logo ? 2 : 1);
        if (logo) Ui.Label("", Ui.Logo, Ui.W(60));
        Ui.Label($"EvilDragon's <color={Ui.BHex}>Cheats</color>", Ui.Title);
        Ui.EndRow();
        Ui.Label($"Kebab Chefs! Prologue  ·  v{Plugin.Version}", Ui.Sub);
        Ui.Space(6);
        Ui.BeginRow(4);
        Ui.KeyHint(Plugin.KeyMenu.Value, "Menu");
        Ui.KeyHint(Plugin.KeyMoney.Value, "+ " + Short(moneyAmount));
        Ui.EndRow();
        Ui.Space(6);

        Ui.BeginRow(Tabs.Length);
        for (int i = 0; i < Tabs.Length; i++)
            if (Ui.Button(Tabs[i], i == tab ? Ui.TabOn : Ui.Tab)) { tab = i; rebinding = null; searching = false; }
        Ui.EndRow();
        Ui.Space(4);

        if (tab != 4 && !Allowed)
        {
            Ui.Header("Cheats are disabled");
            Ui.Label("Other players are in this session. The cheats only work when you play alone.", Ui.Text);
        }
        else if (tab != 4 && RM == null)
        {
            Ui.Header("Not in a game");
            Ui.Label("Load your restaurant to use the cheats.", Ui.Text);
        }
        else switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawMoney(); break;
            case 2: DrawRestaurant(); break;
            case 3: DrawItems(); break;
        }
        if (tab == 4) DrawSettings();

        if (status.Length > 0 && Time.unscaledTime - statusTime < 6f)
        {
            Ui.Space(8);
            Ui.Label("» " + status, Ui.Status);
        }
    }

    void DrawPlayer()
    {
        Ui.Header("Movement");
        speedMult = Ui.Stepper("Walk Speed", speedMult, 0.25f, 0.5f, 5f, "0.00");
        jumpMult = Ui.Stepper("Jump Height", jumpMult, 0.25f, 1f, 5f, "0.00");

        Ui.Header("Time");
        freezeTime = Ui.Toggle(freezeTime, "Freeze Time", "the clock stops");
        timeMult = Ui.Stepper("Day Speed", timeMult, 0.25f, 0.25f, 5f, "0.00");
        var day = GameServer.dayM;
        if (day != null)
        {
            string clock = "";
            try { clock = day.GetHourText(); } catch { }
            Ui.Label($"Day {day.DayOn}  ·  {clock}", Ui.Sub);
        }

        Ui.Header("Food");
        NoRot = Ui.Toggle(NoRot, "Ingredients Never Rot");

        Ui.Header("Blackjack");
        blackjackHelp = Ui.Toggle(blackjackHelp, "Blackjack Helper", "shows Hit or Stay at the table");
        RigBlackjack = Ui.Toggle(RigBlackjack, "Lucky Cards", "you get the best card, the dealer the worst");

        Ui.Header("Interaction");
        instantInteract = Ui.Toggle(instantInteract, "Instant Interact", "no holding, bars fill 20x faster");
    }

    void DrawMoney()
    {
        var d = Data;
        Ui.Header("Balance");
        Ui.Label($"<size=22><b>$ {(d != null ? d.Money.ToString("N0") : "-")}</b></size>", Ui.Text);

        Ui.Header("Amount");
        moneyAmount = Ui.Chips(moneyAmount, MoneyPresets, v => Short(v), 6);
        Ui.BeginRow(3);
        if (Ui.Button($"+ {Short(moneyAmount)}", Ui.Primary)) AddMoney(moneyAmount);
        if (Ui.Button($"− {Short(moneyAmount)}", Ui.Tab)) AddMoney(-moneyAmount);
        if (Ui.Button("= Set", Ui.Tab)) SetMoney(moneyAmount);
        Ui.EndRow();

        Ui.Header("Income Multiplier");
        IncomeMult = Ui.Chips(IncomeMult, MultPresets, v => "x" + v, 6);
        Ui.Label("Customers pay more for every order.", Ui.Sub);

        Ui.Header("Festival & Event Coins");
        if (d != null) Ui.Label($"Festival {d.FestivalCurrency:N0}  ·  Event {d.EventCurrency:N0}", Ui.Sub);
        Ui.BeginRow(2);
        if (Ui.Button("+100 Festival", Ui.Chip)) AddMoney(100, CurrencyType.FestivalCurrency);
        if (Ui.Button("+100 Event", Ui.Chip)) AddMoney(100, CurrencyType.EventCurrency);
        Ui.EndRow();

        Ui.Header("Bank");
        if (Ui.Button("Pay Off All Bills & Loans", Ui.Danger)) ClearBills();
    }

    void DrawRestaurant()
    {
        var d = Data;
        Ui.Header("Level & XP");
        if (d != null)
        {
            int max = 0;
            try { max = RestaurantManager.GetMaxExpByLevel(d.RestaurantLevel); } catch { }
            Ui.Label($"<size=18><b>Level {d.RestaurantLevel}</b></size>   {d.ExperiencePoint:N0} / {max:N0} XP", Ui.Text);
        }
        Ui.BeginRow(4);
        if (Ui.Button("+100 XP", Ui.Chip)) AddXp(100);
        if (Ui.Button("+1K XP", Ui.Chip)) AddXp(1_000);
        if (Ui.Button("+10K XP", Ui.Chip)) AddXp(10_000);
        if (Ui.Button("+100K XP", Ui.Chip)) AddXp(100_000);
        Ui.EndRow();

        Ui.Header("Cook Points");
        Ui.BeginRow(4);
        Ui.Label($"<b>{(d != null ? d.CookPoints.ToString() : "-")}</b> points", Ui.Text, Ui.W(120));
        if (Ui.Button("+1", Ui.Chip)) AddCookPoints(1);
        if (Ui.Button("+10", Ui.Chip)) AddCookPoints(10);
        if (Ui.Button("+100", Ui.Chip)) AddCookPoints(100);
        Ui.EndRow();

        Ui.Header("Perks");
        var pm = GameServer.perkM;
        if (pm != null && pm.perkDatas != null)
            foreach (var pd in pm.perkDatas)
            {
                if (pd == null) continue;
                int stage = 0, max = pd.stageDatas != null ? pd.stageDatas.Length : 0;
                try { stage = pm.GetPerkStage(pd.perkType); } catch { }
                Ui.BeginRow(2);
                Ui.Label($"{Nice(pd.perkType.ToString())}  <color={Ui.BHex}>{stage}/{max}</color>", Ui.Text, Ui.W(300));
                if (stage < max && Ui.Button("+1", Ui.Chip))
                    try { pm.ActivatePerk(pd.perkType, 0); } catch (Exception e) { Plugin.L.LogError(e); }
                Ui.EndRow();
            }
        if (Ui.Button("Max All Perks (free)", Ui.Primary)) MaxPerks();
    }

    void DrawItems()
    {
        if (ingredients == null) LoadIngredients();
        Ui.Header("Search");
        string shown = searchAll ? $"<color={Ui.BHex}><b>{search}</b></color>" : search;
        if (Ui.Button(shown, searching ? Ui.FieldOn : Ui.Field))
        {
            if (!searching) { searching = true; searchClickFrame = Time.frameCount; }
            searchAll = false;
        }
        searchRect = Ui.LastRect;

        Ui.Header("Amount");
        spawnCount = Ui.Chips(spawnCount, CountPresets, v => v.ToString(), 5);

        var list = ingredients.Where(i => search.Length == 0 || Nice(i.ingredientKey).ToLowerInvariant().Contains(search)).ToList();
        int pages = Math.Max(1, (list.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        Ui.Header($"Ingredients ({list.Count})");
        for (int i = page * PageSize; i < Math.Min(list.Count, (page + 1) * PageSize); i++)
        {
            var ing = list[i];
            Ui.BeginRow(2);
            Ui.Label(Nice(ing.ingredientKey), Ui.Text, Ui.W(300));
            if (Ui.Button($"Spawn {spawnCount}", Ui.Chip)) Spawn(ing, spawnCount);
            Ui.EndRow();
        }
        Ui.BeginRow(3);
        if (Ui.Button("◀ Prev", Ui.Tab) && page > 0) page--;
        Ui.Label($"  page {page + 1} / {pages}  ", Ui.Sub);
        if (Ui.Button("Next ▶", Ui.Tab) && page < pages - 1) page++;
        Ui.EndRow();
        if (Ui.Button("Reload List", Ui.Tab)) LoadIngredients();
    }

    void DrawSettings()
    {
        Ui.Header("Theme");
        string th = Ui.Chips(Ui.CurrentTheme, Ui.ThemeNames, n => n, 3);
        if (th != Ui.CurrentTheme)
        {
            Ui.SetTheme(th);
            Plugin.Theme.Value = th;
            SetStatus($"Theme: {th}");
        }

        Ui.Header("Keybinds");
        Ui.Label("Click a key, then press the new key (Esc = cancel).", Ui.Sub);
        KeyRow("Open menu", Plugin.KeyMenu);
        KeyRow("Add money", Plugin.KeyMoney);

        Ui.Header("Settings");
        Ui.Label("Saves all toggles and values so they are active the next time you start the game.", Ui.Sub);
        if (Ui.Button("Save Settings", Ui.Primary)) SaveSettings();
        Ui.Space(6);
        Ui.Label($"Saved in BepInEx\\config\\{Plugin.Guid}.cfg", Ui.Sub);
    }

    void KeyRow(string label, ConfigEntry<string> entry)
    {
        bool active = rebinding == entry;
        Ui.BeginRow(2);
        Ui.Label(label, Ui.Text, Ui.W(220));
        if (Ui.Button(active ? "Press a key…" : entry.Value, active ? Ui.FieldOn : Ui.Chip))
            rebinding = active ? null : entry;
        Ui.EndRow();
    }
}
