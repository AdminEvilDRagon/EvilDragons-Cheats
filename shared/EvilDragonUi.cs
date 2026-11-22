// EvilDragon's Cheats - shared IMGUI toolkit (themes, logo, buttons, toggles, chips).
// Linked into every game project. Works on Mono and IL2CPP (BepInEx 5 / 6) and only uses
// IMGUI calls that survive aggressive engine stripping.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvilDragon;
// ---------------------------------------------------------------------------------
// IMGUI helpers. The game strips parts of IMGUI, so non-trivial calls are guarded and
// fall back to plain calls if missing.
// ---------------------------------------------------------------------------------
public static class Ui
{
    /// Theme used until the player picks one; set from the game's config before the first OnGUI.
    public static string InitialTheme = "Dragon";
    public static Action<string> Log;
    static void Warn(string m) { try { Log?.Invoke(m); } catch { } }

    static bool spaceOk = true, widthOk = true, styleCallOk = true, cardOk = true;
    public static bool Styled;

    public static GUIStyle Panel, Title, Sub, Section, Tab, TabOn, Row, RowOn, Chip, ChipOn, Primary, Danger, Text, Status, Field, FieldOn, KeyCap, KeyText, CardBox, CardTitle;

    // ---------------- themes ----------------
    // A = main accent (primary buttons, selected chips, headers, panel glow)
    // B = second accent (active tab, title, editing field)
    // G = "on" colour for toggles, Bg/Card = background tint
    sealed class Theme
    {
        public readonly string Name;
        public readonly Color A, B, G, Bg, Card;
        public Theme(string name, Color a, Color b, Color g, Color bg, Color card) { Name = name; A = a; B = b; G = g; Bg = bg; Card = card; }
    }

    static Color H(float r, float g, float b, float a = 1f) => new(r, g, b, a);

    static readonly Theme[] Themes =
    {
        new("Dragon",  H(0.10f, 1.00f, 0.30f), H(0.65f, 1.00f, 0.20f), H(0.10f, 1.00f, 0.30f), H(0.020f, 0.045f, 0.030f, 0.97f), H(0.060f, 0.110f, 0.075f)),
        new("Neon",    H(0.00f, 0.94f, 1.00f), H(1.00f, 0.18f, 0.75f), H(0.22f, 1.00f, 0.55f), H(0.035f, 0.035f, 0.075f, 0.97f), H(0.085f, 0.085f, 0.150f)),
        new("Inferno", H(1.00f, 0.45f, 0.10f), H(1.00f, 0.15f, 0.25f), H(1.00f, 0.80f, 0.20f), H(0.060f, 0.025f, 0.020f, 0.97f), H(0.140f, 0.065f, 0.050f)),
        new("Ocean",   H(0.20f, 0.65f, 1.00f), H(0.00f, 1.00f, 0.85f), H(0.20f, 1.00f, 0.80f), H(0.020f, 0.040f, 0.075f, 0.97f), H(0.050f, 0.095f, 0.160f)),
        new("Purple",  H(0.70f, 0.40f, 1.00f), H(1.00f, 0.35f, 0.85f), H(0.55f, 1.00f, 0.75f), H(0.045f, 0.025f, 0.075f, 0.97f), H(0.110f, 0.065f, 0.165f)),
        new("Gold",    H(1.00f, 0.80f, 0.20f), H(1.00f, 0.55f, 0.10f), H(0.65f, 1.00f, 0.35f), H(0.050f, 0.042f, 0.025f, 0.97f), H(0.125f, 0.105f, 0.060f)),
    };
    public static readonly string[] ThemeNames = Array.ConvertAll(Themes, t => t.Name);
    public static string CurrentTheme = "Dragon";

    static Color Bg, Card, CardHover, CardLine, Cyan, CyanHover, Magenta, MagentaHover, Green, OnFill, OnFillHover, FieldBg;
    static readonly Color Red = H(1.00f, 0.20f, 0.36f);
    static readonly Color RedHover = H(1.00f, 0.40f, 0.52f);
    static readonly Color TextCol = H(0.95f, 0.96f, 1.00f);
    static readonly Color Muted = H(0.72f, 0.76f, 0.86f);
    static readonly Color Dark = H(0.03f, 0.03f, 0.05f);
    public static string AHex = "#1aff4d", BHex = "#a6ff33", GHex = "#1aff4d";

    static string ToHex(Color c) => $"#{(int)(c.r * 255):x2}{(int)(c.g * 255):x2}{(int)(c.b * 255):x2}";

    static void LoadTheme(string name)
    {
        var t = Array.Find(Themes, x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Themes[0];
        CurrentTheme = t.Name;
        Cyan = t.A; CyanHover = Color.Lerp(t.A, Color.white, 0.40f);
        Magenta = t.B; MagentaHover = Color.Lerp(t.B, Color.white, 0.30f);
        Green = t.G;
        Bg = t.Bg; Card = t.Card;
        CardHover = Color.Lerp(t.Card, t.A, 0.12f);
        CardLine = Color.Lerp(t.Card, t.A, 0.30f);
        OnFill = Color.Lerp(H(0.03f, 0.03f, 0.03f), t.G, 0.18f);
        OnFillHover = Color.Lerp(H(0.03f, 0.03f, 0.03f), t.G, 0.26f);
        FieldBg = Color.Lerp(t.Bg, Color.black, 0.5f); FieldBg.a = 1f;
        AHex = ToHex(t.A);
        BHex = ToHex(t.B);
        GHex = ToHex(t.G);
    }

    /// Switch theme at runtime: styles are rebuilt on the next OnGUI.
    public static void SetTheme(string name)
    {
        LoadTheme(name);
        Panel = null;
        Styled = false;
    }

    // ---------------- logo ----------------
    static Texture2D logoTex;
    public static GUIStyle Logo;
    static Texture2D LoadLogo()
    {
        if (logoTex != null) return logoTex;
        try
        {
            using var s = typeof(Ui).Assembly.GetManifestResourceStream("EvilDragon.logo96.rgba");
            if (s == null) return null;
            var bytes = new byte[s.Length];
            int read = 0; while (read < bytes.Length) read += s.Read(bytes, read, bytes.Length - read);
            const int size = 96;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0, p = 0; i < size * size; i++, p += 4)
                t.SetPixel(i % size, i / size, new Color32(bytes[p], bytes[p + 1], bytes[p + 2], bytes[p + 3]));
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            logoTex = t;
        }
        catch (Exception e) { Warn("Logo: " + e.Message); }
        return logoTex;
    }

    /// Anti-aliased rounded-rect texture with optional outline, used 9-sliced via style.border.
    static Texture2D Tex(Color c, int radius = 6, Color? outline = null, float ow = 1.6f)
    {
        int size = radius * 2 + 2;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        var o = outline ?? c;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
            float cover = Mathf.Clamp01(radius - d + 0.5f);
            float inner = outline.HasValue ? Mathf.Clamp01(radius - ow - d + 0.5f) : 1f;
            var col = Color.Lerp(o, c, inner);
            t.SetPixel(x, y, new Color(col.r, col.g, col.b, col.a * cover));
        }
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    static void SetOffsets(RectOffset o, int l, int r, int t, int b)
    {
        o.left = l; o.right = r; o.top = t; o.bottom = b;
    }

    static void Border(GUIStyle s, int r) { try { SetOffsets(s.border, r, r, r, r); } catch { } }

    // Fonts: Windows' Segoe UI family instead of the thin default Arial
    static Font FBody, FBold, FTitle;
    /// false = keep Unity's built-in font. Unity 2023.2+ draws IMGUI text with TextCore, which shows nothing for OS fonts.
    public static bool UseOsFonts = true;
    static Font OsFont(int size, params string[] names)
    {
        try
        {
            var f = Font.CreateDynamicFontFromOSFont(names, size);
            if (f != null) f.hideFlags = HideFlags.HideAndDontSave;
            return f;
        }
        catch (Exception e) { Warn("OS font " + names[0] + ": " + e.Message); return null; }
    }

    static void ApplyFont(GUIStyle s, bool bold, bool title = false)
    {
        var f = title ? (FTitle ?? FBold) : bold ? FBold : FBody;
        if (f != null) { s.font = f; s.fontStyle = FontStyle.Normal; }
        else if (bold || title) s.fontStyle = FontStyle.Bold;
    }

    static GUIStyle Btn(Color bg, Color hover, Color text, int size, TextAnchor align, bool bold, int padH, int h, int radius = 6, Color? line = null, Color? lineHover = null)
    {
        var s = new GUIStyle();
        var n = Tex(bg, radius, line);
        var hv = Tex(hover, radius, lineHover ?? line);
        var pr = Tex(Color.Lerp(hover, Color.white, 0.15f), radius, lineHover ?? line);
        s.normal.background = n; s.normal.textColor = text;
        s.hover.background = hv; s.hover.textColor = text;
        s.active.background = pr; s.active.textColor = text;
        s.focused.background = n; s.focused.textColor = text;
        s.onNormal.background = n; s.onNormal.textColor = text;
        s.fontSize = size; s.alignment = align; s.richText = true;
        s.wordWrap = false;
        s.clipping = TextClipping.Clip;
        s.stretchWidth = true;
        s.fixedHeight = h;
        ApplyFont(s, bold);
        Border(s, radius);
        try { SetOffsets(s.padding, padH, padH, 0, 0); SetOffsets(s.margin, 3, 3, 3, 3); } catch { }
        return s;
    }

    static GUIStyle Lbl(Color c, int size, bool bold, TextAnchor align = TextAnchor.MiddleLeft, bool title = false)
    {
        var s = new GUIStyle();
        s.normal.textColor = c; s.fontSize = size; s.richText = true; s.alignment = align;
        s.wordWrap = true;
        s.stretchWidth = true;
        ApplyFont(s, bold, title);
        try { SetOffsets(s.padding, 4, 4, 2, 2); SetOffsets(s.margin, 3, 3, 2, 2); } catch { }
        return s;
    }

    public static void Init()
    {
        if (Panel != null) return;
        try
        {
            if (Card.a == 0f) LoadTheme(InitialTheme);

            Panel = new GUIStyle();
            Panel.normal.background = Tex(Bg, 14, new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f), 2f);
            Border(Panel, 14);
            try { SetOffsets(Panel.padding, 18, 18, 16, 16); } catch { }

            if (FBody == null && UseOsFonts)
            {
                FBody = OsFont(15, "Segoe UI Semibold", "Segoe UI", "Arial");
                FBold = OsFont(15, "Segoe UI Bold", "Segoe UI Semibold", "Arial");
                FTitle = OsFont(22, "Segoe UI Black", "Segoe UI Bold", "Arial");
            }

            Logo = new GUIStyle();
            Logo.normal.background = LoadLogo();
            Logo.fixedWidth = 56; Logo.fixedHeight = 56;
            try { SetOffsets(Logo.margin, 0, 10, 0, 0); } catch { }

            Title = Lbl(Cyan, 22, true, TextAnchor.LowerLeft, title: true);
            Sub = Lbl(Muted, 13, false);
            Section = Lbl(Cyan, 13, true);
            Text = Lbl(TextCol, 15, false);
            Status = Lbl(Green, 14, true);
            KeyText = Lbl(TextCol, 13, true);

            Tab = Btn(Card, CardHover, TextCol, 15, TextAnchor.MiddleCenter, true, 6, 34, 7, CardLine, Magenta);
            TabOn = Btn(Magenta, MagentaHover, Dark, 15, TextAnchor.MiddleCenter, true, 6, 34, 7);
            Row = Btn(Card, CardHover, TextCol, 15, TextAnchor.MiddleLeft, true, 14, 36, 7, CardLine, Cyan);
            RowOn = Btn(OnFill, OnFillHover, Color.Lerp(Green, Color.white, 0.65f), 15, TextAnchor.MiddleLeft, true, 14, 36, 7, Green, Green);
            Chip = Btn(Card, CardHover, TextCol, 14, TextAnchor.MiddleCenter, true, 6, 30, 7, CardLine, Cyan);
            ChipOn = Btn(Cyan, CyanHover, Dark, 14, TextAnchor.MiddleCenter, true, 6, 30, 7);
            Primary = Btn(Cyan, CyanHover, Dark, 16, TextAnchor.MiddleCenter, true, 10, 42, 8);
            Danger = Btn(Red, RedHover, Color.white, 15, TextAnchor.MiddleCenter, true, 10, 38, 8);
            Field = Btn(FieldBg, Card, TextCol, 15, TextAnchor.MiddleLeft, true, 12, 32, 7, CardLine, Cyan);
            FieldOn = Btn(FieldBg, FieldBg, Magenta, 15, TextAnchor.MiddleLeft, true, 12, 32, 7, Magenta, Magenta);
            KeyCap = Btn(FieldBg, FieldBg, Cyan, 13, TextAnchor.MiddleCenter, true, 4, 24, 6, Cyan, Cyan);

            CardBox = new GUIStyle();
            var cardBg = Color.Lerp(Bg, Card, 0.55f); cardBg.a = 1f;
            CardBox.normal.background = Tex(cardBg, 10, Color.Lerp(cardBg, Cyan, 0.22f), 1.4f);
            Border(CardBox, 10);
            try { SetOffsets(CardBox.padding, 12, 12, 8, 10); SetOffsets(CardBox.margin, 0, 0, 4, 6); } catch { }
            CardTitle = Lbl(Cyan, 14, true);
            Styled = true;
        }
        catch (Exception e)
        {
            Warn("Custom styles unavailable, using default skin: " + e.Message);
            Styled = false;
            Panel = new GUIStyle();
        }
    }

    // ---------------- manual layout ----------------
    // Some games strip GUILayout completely (only GUI.Button/Label/Box with a Rect are left).
    // Set Manual = true there: every helper then places itself with its own simple layout.
    // Rows need their column count up front: BeginRow(n) ... EndRow().
    public static bool Manual;
    /// Some games strip GUI.Button too (only GUI.Label/Box survive). With OwnButtons (needs Manual)
    /// buttons are drawn as boxes and the game feeds the mouse click in itself:
    /// set Click (GUI coordinates, after the menu scale) for one frame, the button under it fires.
    public static bool OwnButtons;
    public static Vector2? Click;
    /// Screen rect of the last button/label (manual layout) - e.g. to detect clicks outside a field.
    public static Rect LastRect;
    static Rect clip;
    static bool clipping;
    static Rect clipSavedArea;
    static float clipScroll;
    static GUIStyle noStyle;
    static GUIContent noContent;
    static Rect area;
    static float cy, rowUsed, rowH, pendingW = -1f;
    static bool inRow;
    static int rowCols, rowPlaced;
    const float Gap = 6f;

    public static void BeginArea(Rect r, GUIStyle style)
    {
        if (!Manual)
        {
            if (Styled && style != null) GUILayout.BeginArea(r, style); else GUILayout.BeginArea(r, GUI.skin.box);
            return;
        }
        if (Styled && style != null) { if (OwnButtons) GUI.Label(r, "", style); else GUI.Box(r, "", style); } else GUI.Box(r, "");
        float l = 14, t = 14, rr = 14, b = 14;
        try { if (Styled && style != null) { l = style.padding.left; t = style.padding.top; rr = style.padding.right; b = style.padding.bottom; } } catch { }
        area = new Rect(r.x + l, r.y + t, r.width - l - rr, r.height - t - b);
        cy = area.y; inRow = false; pendingW = -1f;
    }

    public static void EndArea() { if (!Manual) GUILayout.EndArea(); }

    /// Manual layout only: a scrollable part of the area, height px tall, scrolled down by scroll px.
    /// Returns nothing; EndClip() returns how tall the content was (to limit the scrolling).
    public static void BeginClip(float height, float scroll)
    {
        if (!Manual || clipping) return;
        clip = new Rect(area.x, cy, area.width, Math.Max(10f, height));
        noStyle ??= new GUIStyle();
        noContent ??= new GUIContent();
        GUI.BeginGroup(clip, noContent, noStyle);
        clipping = true;
        clipSavedArea = area;
        clipScroll = scroll;
        area = new Rect(0f, 0f, clip.width, clip.height);
        cy = -scroll; inRow = false; pendingW = -1f;
    }

    public static float EndClip()
    {
        if (!clipping) return 0f;
        float content = cy + clipScroll;
        GUI.EndGroup();
        clipping = false;
        area = clipSavedArea;
        cy = clip.yMax + Gap; inRow = false; pendingW = -1f;
        return content;
    }

    /// Manual layout: what is left of the area below the current position.
    public static float SpaceLeft => Manual ? Math.Max(0f, area.yMax - cy) : 0f;

    public static void BeginRow(int columns)
    {
        if (!Manual) { GUILayout.BeginHorizontal(); return; }
        inRow = true; rowCols = Math.Max(1, columns); rowPlaced = 0; rowUsed = 0; rowH = 0;
    }

    public static void EndRow()
    {
        if (!Manual) { GUILayout.EndHorizontal(); return; }
        if (inRow) { cy += rowH + Gap; inRow = false; }
    }

    static Rect Next(float h)
    {
        float w;
        if (inRow)
        {
            int remaining = Math.Max(1, rowCols - rowPlaced);
            float free = area.width - rowUsed - Gap * (remaining - 1);
            w = pendingW > 0 ? Math.Min(pendingW, Math.Max(10f, free)) : Math.Max(10f, free / remaining);
            var r = new Rect(area.x + rowUsed, cy, w, h);
            rowUsed += w + Gap; rowPlaced++; rowH = Math.Max(rowH, h);
            pendingW = -1f;
            return r;
        }
        w = pendingW > 0 ? Math.Min(pendingW, area.width) : area.width;
        pendingW = -1f;
        var rr = new Rect(area.x, cy, w, h);
        cy += h + Gap;
        return rr;
    }

    static float Height(GUIStyle s, string text, bool button)
    {
        if (Styled && s != null && s.fixedHeight > 0) return s.fixedHeight;
        int fs = Styled && s != null && s.fontSize > 0 ? s.fontSize : 14;
        int i = text != null ? text.IndexOf("<size=", StringComparison.Ordinal) : -1;
        if (i >= 0 && int.TryParse(new string(text.Substring(i + 6).TakeWhileDigit()), out var big)) fs = Math.Max(fs, big);
        return button ? Math.Max(28f, fs * 2f) : fs * 1.45f + 6f;
    }

    static char[] TakeWhileDigit(this string t)
    {
        int n = 0;
        while (n < t.Length && char.IsDigit(t[n])) n++;
        return t.Substring(0, n).ToCharArray();
    }

    public static GUILayoutOption[] W(float w)
    {
        if (Manual) { pendingW = w; return Array.Empty<GUILayoutOption>(); }
        if (!widthOk) return Array.Empty<GUILayoutOption>();
        try { return new[] { GUILayout.Width(w) }; } catch { widthOk = false; return Array.Empty<GUILayoutOption>(); }
    }

    public static void Space(float px)
    {
        if (Manual) { if (!inRow) cy += px; return; }
        if (spaceOk) { try { GUILayout.Space(px); return; } catch { spaceOk = false; } }
        GUILayout.Label("");
    }

    // Without options a button/label is at least as wide as its text, so a long name pushes the whole
    // menu past the right edge. MinWidth(0) lets it shrink to the menu width instead (text gets clipped).
    static GUILayoutOption[] shrink;
    static GUILayoutOption[] Fit(GUILayoutOption[] opts)
    {
        if (opts != null && opts.Length > 0) return opts;
        // manual layout never uses options - and creating one crashes games that stripped GUILayout
        if (Manual) return Array.Empty<GUILayoutOption>();
        if (shrink == null) { try { shrink = new[] { GUILayout.MinWidth(0f) }; } catch { shrink = Array.Empty<GUILayoutOption>(); } }
        return shrink;
    }

    public static bool Button(string text, GUIStyle style, params GUILayoutOption[] opts)
    {
        opts = Fit(opts);
        if (Manual)
        {
            var r = Next(Height(style, text, true));
            LastRect = r;
            if (!OwnButtons) return Styled && style != null ? GUI.Button(r, text, style) : GUI.Button(r, text);
            // a label with a style draws the style's background too - GUI.Label survives every strip
            if (Styled && style != null) GUI.Label(r, text, style); else GUI.Box(r, text);
            if (!Click.HasValue || !GUI.enabled) return false;
            var p = Click.Value;
            if (clipping) { if (!clip.Contains(p)) return false; p -= clip.position; }
            if (!r.Contains(p)) return false;
            Click = null;
            return true;
        }
        bool hit;
        if (Styled && styleCallOk && style != null)
        {
            try { hit = GUILayout.Button(text, style, opts); try { LastRect = GUILayoutUtility.GetLastRect(); } catch { } return hit; }
            catch { styleCallOk = false; }
        }
        hit = GUILayout.Button(text, opts);
        try { LastRect = GUILayoutUtility.GetLastRect(); } catch { }
        return hit;
    }

    public static void Label(string text, GUIStyle style, params GUILayoutOption[] opts)
    {
        opts = Fit(opts);
        if (Manual)
        {
            var r = Next(Height(style, text, false));
            if (Styled && style != null) GUI.Label(r, text, style); else GUI.Label(r, text);
            return;
        }
        if (Styled && styleCallOk && style != null)
        {
            try { GUILayout.Label(text, style, opts); return; }
            catch { styleCallOk = false; }
        }
        GUILayout.Label(text, opts);
    }

    public static void Header(string text) { Space(6); Label(text.ToUpperInvariant(), Section); }

    /// A framed section ("card") with a title and an optional one-line description.
    /// Always pair with EndCard().
    public static void BeginCard(string title, string desc = null)
    {
        if (Manual) { Space(4); Label(title.ToUpperInvariant(), CardTitle ?? Section); if (desc != null) Label(desc, Sub); return; }
        bool framed = false;
        if (Styled && cardOk && CardBox != null)
        {
            try { GUILayout.BeginVertical(CardBox); framed = true; }
            catch { cardOk = false; }
        }
        if (!framed) { GUILayout.BeginVertical(); Space(4); }
        Label(title.ToUpperInvariant(), CardTitle ?? Section);
        if (desc != null) Label(desc, Sub);
    }

    public static void EndCard() { if (!Manual) GUILayout.EndVertical(); }

    /// Two toggles side by side (compact rows inside cards).
    public static void Toggle2(ref bool a, string la, ref bool b, string lb)
    {
        BeginRow(2);
        a = Toggle(a, la);
        b = Toggle(b, lb);
        EndRow();
    }

    public static bool Toggle(bool v, string label, string hint = null)
    {
        string txt = (v ? $"<color={GHex}>●</color>  " : "<color=#6a7080>○</color>  ") + label
                   + (hint != null ? $"    <size=13><color={(v ? "#e6ffee" : "#b8c2d6")}>{hint}</color></size>" : "");
        if (!Styled) txt = (v ? "[ON]  " : "[OFF] ") + label;
        return Button(txt, v ? RowOn : Row) ? !v : v;
    }

    public static T Chips<T>(T current, T[] values, Func<T, string> fmt, int perRow = 6)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i % perRow == 0) BeginRow(Math.Min(perRow, values.Length - i));
            bool on = EqualityComparer<T>.Default.Equals(values[i], current);
            if (Button(on && !Styled ? $"> {fmt(values[i])} <" : fmt(values[i]), on ? ChipOn : Chip)) current = values[i];
            if (i % perRow == perRow - 1 || i == values.Length - 1) EndRow();
        }
        return current;
    }

    /// Key badge + description, e.g. [K] Kill all
    public static void KeyHint(string key, string text)
    {
        if (!Styled) { Label($"[{key}] {text}", Sub); return; }
        Label(key, KeyCap, W(Mathf.Max(28, 10 + key.Length * 9)));
        Label(text, KeyText);
    }

    public static float Stepper(string label, float v, float step, float min, float max, string fmt = "0.0")
    {
        BeginRow(4);
        Label($"{label}", Text, W(150));
        Label($"<b>{v.ToString(fmt)}x</b>", Text, W(70));
        if (Button("−", Chip, W(40))) v -= step;
        if (Button("+", Chip, W(40))) v += step;
        EndRow();
        return Mathf.Clamp((float)Math.Round(v, 2), min, max);
    }
}
