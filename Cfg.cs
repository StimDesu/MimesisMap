using System;
using MelonLoader;
using UnityEngine.InputSystem;

namespace MimesisMap
{
    public enum RevealMode { Explored, Full }
    public enum MapStyle { Plain, Graphic }
    public enum Orientation { FollowView, NorthUp }
    public enum KeyMode { Hold, Toggle }
    public enum MiniShape { Circle, Square }
    public enum MiniAnchor { TopLeft, TopRight, BottomLeft, BottomRight, Custom }

    internal static class Cfg
    {
        private static MelonPreferences_Category _cat;

        // General
        public static MelonPreferences_Entry<RevealMode> Reveal;
        public static MelonPreferences_Entry<MapStyle> Style;
        public static MelonPreferences_Entry<bool> NeighborHints;
        public static MelonPreferences_Entry<bool> ShowOutdoor;
        public static MelonPreferences_Entry<string> SettingsKey;

        // Big map
        public static MelonPreferences_Entry<string> BigMapKey;
        public static MelonPreferences_Entry<KeyMode> BigMapKeyMode;
        public static MelonPreferences_Entry<float> BigMapSize;
        public static MelonPreferences_Entry<float> BigMapZoom;
        public static MelonPreferences_Entry<float> BigMapOpacity;
        public static MelonPreferences_Entry<float> BigMapMarginTop, BigMapMarginBottom, BigMapMarginSide;
        public static MelonPreferences_Entry<Orientation> BigMapRotateE;
        public static MelonPreferences_Entry<bool> BigMapInteractive;

        // Minimap
        public static MelonPreferences_Entry<bool> MiniEnabled;
        public static MelonPreferences_Entry<string> MiniKey;
        public static MelonPreferences_Entry<float> MiniRange;
        public static MelonPreferences_Entry<float> MiniSize;
        public static MelonPreferences_Entry<MiniShape> MiniShapeE;
        public static MelonPreferences_Entry<Orientation> MiniOrientation;
        public static MelonPreferences_Entry<MiniAnchor> MiniAnchorE;
        public static MelonPreferences_Entry<float> MiniOffsetX;
        public static MelonPreferences_Entry<float> MiniOffsetY;
        public static MelonPreferences_Entry<float> MiniOpacity;

        // Markers
        public static MelonPreferences_Entry<bool> ShowLoot;
        public static MelonPreferences_Entry<bool> LootOnlyRevealed;
        public static MelonPreferences_Entry<bool> ShowTeammates;
        public static MelonPreferences_Entry<bool> ShowNames;
        public static MelonPreferences_Entry<bool> ShowMonsters;
        public static MelonPreferences_Entry<bool> ShowExit;
        public static MelonPreferences_Entry<bool> ShowExitPath;
        public static MelonPreferences_Entry<float> MarkerScale;

        // Sharing
        public static MelonPreferences_Entry<bool> RevealByTeammates;
        public static MelonPreferences_Entry<bool> ShareSteam;

        // Advanced
        public static MelonPreferences_Entry<float> Resolution;

        // Controls: every action has a main and an alternative binding (keyboard key, mouse button or wheel)
        public static Bind BSettings, BBigMap, BMini, BZoomIn, BZoomOut, BRecenter;
        public static Bind[] Binds;

        public static void Init()
        {
            _cat = MelonPreferences.CreateCategory("MimesisMap", "Mimesis Map");

            Reveal = _cat.CreateEntry("RevealMode", RevealMode.Explored, "Reveal mode", "Explored = rooms open as you visit them, Full = whole map visible");
            Style = _cat.CreateEntry("Style", MapStyle.Plain, "Map style", "Plain = schematic floor plan, Graphic = real top-down view from a camera above you");
            NeighborHints = _cat.CreateEntry("NeighborHints", true, "Hint unexplored neighbours", "Faintly draw unexplored rooms adjacent to explored ones");
            ShowOutdoor = _cat.CreateEntry("ShowOutdoor", true, "Show outdoor area");
            SettingsKey = _cat.CreateEntry("SettingsKey", "F8", "Settings window key");

            BigMapKey = _cat.CreateEntry("BigMapKey", "Tab", "Big map key");
            BigMapKeyMode = _cat.CreateEntry("BigMapKeyMode", KeyMode.Hold, "Big map key mode", "Hold = visible while held, Toggle = press to open/close");
            BigMapSize = _cat.CreateEntry("BigMapSize", 1f, "Big map size (fraction of the free screen area)");
            BigMapZoom = _cat.CreateEntry("BigMapZoom", 1f, "Big map zoom (1 = fit whole map)");
            BigMapRotateE = _cat.CreateEntry("BigMapOrientation", Orientation.FollowView, "Big map orientation", "FollowView = rotates with your view, NorthUp = north always up");
            BigMapInteractive = _cat.CreateEntry("BigMapInteractive", true, "Big map shows the cursor in Toggle mode (drag = pan)");
            BigMapMarginTop = _cat.CreateEntry("BigMapMarginTop", 20f, "Big map top margin (px @1080p)");
            BigMapMarginBottom = _cat.CreateEntry("BigMapMarginBottom", 165f, "Big map bottom margin, keeps the hotbar visible (px @1080p)");
            BigMapMarginSide = _cat.CreateEntry("BigMapMarginSide", 510f, "Big map side margin, keeps health/radiation bars visible (px @1080p)");
            BigMapOpacity = _cat.CreateEntry("BigMapOpacity", 0.85f, "Big map background opacity");

            MiniEnabled = _cat.CreateEntry("MinimapEnabled", true, "Minimap enabled");
            MiniKey = _cat.CreateEntry("MinimapKey", "F6", "Minimap toggle key");
            MiniRange = _cat.CreateEntry("MinimapRange", 25f, "Minimap range (metres from player to edge)");
            MiniSize = _cat.CreateEntry("MinimapSize", 260f, "Minimap size (px @1080p)");
            MiniShapeE = _cat.CreateEntry("MinimapShape", MiniShape.Circle, "Minimap shape");
            MiniOrientation = _cat.CreateEntry("MinimapOrientation", Orientation.FollowView, "Minimap orientation", "FollowView = rotates with your view, NorthUp = north always up");
            MiniAnchorE = _cat.CreateEntry("MinimapAnchor", MiniAnchor.TopRight, "Minimap screen corner");
            MiniOffsetX = _cat.CreateEntry("MinimapOffsetX", 20f, "Minimap offset X");
            MiniOffsetY = _cat.CreateEntry("MinimapOffsetY", 20f, "Minimap offset Y");
            MiniOpacity = _cat.CreateEntry("MinimapOpacity", 0.6f, "Minimap background opacity");

            ShowLoot = _cat.CreateEntry("ShowLoot", true, "Show loot");
            LootOnlyRevealed = _cat.CreateEntry("LootOnlyRevealed", true, "Show loot only in explored rooms");
            ShowTeammates = _cat.CreateEntry("ShowTeammates", false, "Show teammates");
            ShowMonsters = _cat.CreateEntry("ShowMonsters", false, "Show monsters (mimics included)");
            ShowNames = _cat.CreateEntry("ShowNames", true, "Show teammate names on big map");
            ShowExit = _cat.CreateEntry("ShowExit", true, "Mark building exits and the entrance room");
            ShowExitPath = _cat.CreateEntry("ShowExitPath", true, "Shortest path to the nearest known exit");
            MarkerScale = _cat.CreateEntry("MarkerScale", 1f, "Marker scale");

            RevealByTeammates = _cat.CreateEntry("RevealByTeammates", true, "Reveal rooms where teammates have been");
            ShareSteam = _cat.CreateEntry("ShareViaSteam", true, "Exchange explored rooms with other players that have this mod");

            Resolution = _cat.CreateEntry("MapResolution", 5f, "Map texels per metre (rebuild on next level)");

            BSettings = new Bind("Окно настроек", SettingsKey, _cat.CreateEntry("SettingsKeyAlt", "", "Settings window key (alternative)"));
            BBigMap = new Bind("Большая карта", BigMapKey, _cat.CreateEntry("BigMapKeyAlt", "", "Big map key (alternative)"));
            BMini = new Bind("Миникарта вкл/выкл", MiniKey, _cat.CreateEntry("MinimapKeyAlt", "", "Minimap toggle key (alternative)"));
            BZoomIn = new Bind("Приблизить большую карту", _cat.CreateEntry("ZoomInKey", "Equals", "Big map zoom in"), _cat.CreateEntry("ZoomInKeyAlt", "WheelUp", "Big map zoom in (alternative)"));
            BZoomOut = new Bind("Отдалить большую карту", _cat.CreateEntry("ZoomOutKey", "Minus", "Big map zoom out"), _cat.CreateEntry("ZoomOutKeyAlt", "WheelDown", "Big map zoom out (alternative)"));
            BRecenter = new Bind("Большая карта: к себе", _cat.CreateEntry("RecenterKey", "R", "Big map recenter"), _cat.CreateEntry("RecenterKeyAlt", "Home", "Big map recenter (alternative)"));
            Binds = new[] { BBigMap, BZoomIn, BZoomOut, BRecenter, BMini, BSettings };
        }

        /// <summary>Puts every MimesisMap setting back to its built-in default and saves.</summary>
        public static void ResetAll()
        {
            foreach (var e in _cat.Entries)
                try { e.ResetToDefault(); } catch (Exception ex) { MelonLogger.Warning($"Reset {e.Identifier} failed: {ex.Message}"); }
            Save();
            MelonLogger.Msg("[MimesisMap] Settings reset to defaults");
        }

        public static void Save()
        {
            try { MelonPreferences.Save(); } catch (Exception e) { MelonLogger.Warning("Save failed: " + e.Message); }
        }

        public static bool TryKey(string name, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(name)) return false;
            return Enum.TryParse(name, true, out key) && key != Key.None;
        }

        // ---- binding names: Key enum names, or the mouse names below; "" = unbound
        public const string WheelUp = "WheelUp", WheelDown = "WheelDown";

        private static UnityEngine.InputSystem.Controls.ButtonControl MouseButton(Mouse m, string name)
        {
            switch (name)
            {
                case "MouseMiddle": return m.middleButton;
                case "MouseRight": return m.rightButton;
                case "MouseBack": return m.backButton;
                case "MouseForward": return m.forwardButton;
                default: return null;
            }
        }

        public static bool PressedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                var m = Mouse.current;
                if (name == WheelUp) return m != null && m.scroll.ReadValue().y > 0.01f;
                if (name == WheelDown) return m != null && m.scroll.ReadValue().y < -0.01f;
                var mb = m != null ? MouseButton(m, name) : null;
                if (mb != null) return mb.wasPressedThisFrame;
                var kb = Keyboard.current;
                return kb != null && TryKey(name, out var k) && kb[k].wasPressedThisFrame;
            }
            catch { return false; }
        }

        public static bool HeldName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == WheelUp || name == WheelDown) return false;
            try
            {
                var m = Mouse.current;
                var mb = m != null ? MouseButton(m, name) : null;
                if (mb != null) return mb.isPressed;
                var kb = Keyboard.current;
                return kb != null && TryKey(name, out var k) && kb[k].isPressed;
            }
            catch { return false; }
        }

        public static string Pretty(string name)
        {
            switch (name)
            {
                case null: case "": return "—";
                case WheelUp: return "Колесо вверх";
                case WheelDown: return "Колесо вниз";
                case "MouseMiddle": return "Средняя кн. мыши";
                case "MouseRight": return "Правая кн. мыши";
                case "MouseBack": return "Мышь: назад";
                case "MouseForward": return "Мышь: вперёд";
                case "Equals": return "=";
                case "Minus": return "-";
                default: return name;
            }
        }
    }

    /// <summary>An action with a main and an alternative binding.</summary>
    internal sealed class Bind
    {
        public readonly string Label;
        public readonly MelonPreferences_Entry<string> Main, Alt;

        public Bind(string label, MelonPreferences_Entry<string> main, MelonPreferences_Entry<string> alt)
        {
            Label = label; Main = main; Alt = alt;
        }

        public bool Pressed => Cfg.PressedName(Main.Value) || Cfg.PressedName(Alt.Value);
        public bool Held => Cfg.HeldName(Main.Value) || Cfg.HeldName(Alt.Value);

        public string Text
        {
            get
            {
                bool m = !string.IsNullOrEmpty(Main.Value), a = !string.IsNullOrEmpty(Alt.Value);
                if (m && a) return Cfg.Pretty(Main.Value) + " / " + Cfg.Pretty(Alt.Value);
                return m ? Cfg.Pretty(Main.Value) : a ? Cfg.Pretty(Alt.Value) : "—";
            }
        }
    }
}
