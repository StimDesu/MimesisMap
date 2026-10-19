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
            BigMapInteractive = _cat.CreateEntry("BigMapInteractive", true, "Big map uses the mouse in Toggle mode (wheel = zoom, drag = pan, R = recenter)");
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

        public static bool Pressed(MelonPreferences_Entry<string> e)
        {
            var kb = Keyboard.current;
            if (kb == null || !TryKey(e.Value, out var k)) return false;
            try { return kb[k].wasPressedThisFrame; } catch { return false; }
        }

        public static bool Held(MelonPreferences_Entry<string> e)
        {
            var kb = Keyboard.current;
            if (kb == null || !TryKey(e.Value, out var k)) return false;
            try { return kb[k].isPressed; } catch { return false; }
        }
    }
}
