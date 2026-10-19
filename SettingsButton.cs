using UnityEngine;
using UnityEngine.UI;

namespace MimesisMap
{
    /// <summary>Adds a "Карта" button to the game's own settings window that opens the mod's settings.</summary>
    internal static class SettingsButton
    {
        private const string SettingsPath = "Hub/UIManager/Canvas/1 - top/UIPrefab_GameSettings(Clone)";
        private const string ButtonName = "MimesisMapSettingsButton";
        private static float _next;
        private static Font _font;

        public static void Tick()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var root = GameObject.Find(SettingsPath);
            if (root == null || root.transform.Find(ButtonName) != null) return;

            var go = new GameObject(ButtonName, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f);
            // the Minimap (ToxesFoxes) mod puts its button at x=180..360, stay to the right of it
            r.anchoredPosition = new Vector2(370f, 5f);
            r.sizeDelta = new Vector2(180f, 40f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.2f, 0.9f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(SettingsWindow.Toggle);

            var t = new GameObject("Text", typeof(RectTransform));
            t.transform.SetParent(go.transform, false);
            var tr = (RectTransform)t.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var text = t.AddComponent<Text>();
            if (_font == null) try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.85f, 0.95f, 1f, 1f);
            text.text = "Карта (мод)";
            text.raycastTarget = false;
        }
    }
}
