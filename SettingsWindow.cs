using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MimesisMap
{
    /// <summary>In-game IMGUI settings window (default key F8).</summary>
    internal static class SettingsWindow
    {
        private static bool _open;
        private static Rect _win = new Rect(80, 60, 660, 800);
        private static Vector2 _scroll;
        private static MelonPreferences_Entry<string> _capture;
        private static int _captureFrame;
        private static GUIStyle _header, _small;
        private static float _resAtOpen;

        public static bool Open => _open;
        public static bool Capturing => _capture != null;

        public static void Toggle()
        {
            if (_open) Close(); else Show();
        }

        private static void Show()
        {
            _open = true;
            _resAtOpen = Cfg.Resolution.Value;
            InputBlock.Acquire("settings");
        }

        private static void Close()
        {
            _open = false;
            _capture = null;
            InputBlock.Release("settings");
            Cfg.Save();
            if (Math.Abs(_resAtOpen - Cfg.Resolution.Value) > 0.01f) MapController.ForceRebuild();
        }

        public static void Update()
        {
            if (!_open) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (_capture != null)
            {
                if (Time.frameCount <= _captureFrame) return;
                if (kb.escapeKey.wasPressedThisFrame) { _capture = null; return; }
                foreach (var k in kb.allKeys)
                {
                    if (k == null || !k.wasPressedThisFrame) continue;
                    _capture.Value = k.keyCode.ToString();
                    _capture = null;
                    break;
                }
                return;
            }
            if (kb.escapeKey.wasPressedThisFrame) Close();
        }

        public static void OnGUI()
        {
            if (!_open) return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            if (_skin == null) BuildSkin();
            var oldSkin = GUI.skin;
            GUI.skin = _skin;
            _win = GUILayout.Window(0x4D4D4150, _win, Draw, "Mimesis Map — настройки");
            GUI.skin = oldSkin;
            GUI.matrix = old;
        }

        private static GUISkin _skin;

        /// <summary>Opaque, high-contrast copy of the default skin (the default window is see-through and grey-on-grey).</summary>
        private static void BuildSkin()
        {
            _skin = UnityEngine.Object.Instantiate(GUI.skin);
            UnityEngine.Object.DontDestroyOnLoad(_skin);
            var text = new Color(0.94f, 0.96f, 0.98f);
            var bg = Tex(new Color(0.06f, 0.08f, 0.11f, 0.985f));

            var w = _skin.window;
            w.normal.background = w.onNormal.background = w.focused.background = w.onFocused.background = bg;
            w.normal.textColor = w.onNormal.textColor = w.focused.textColor = w.onFocused.textColor = new Color(0.6f, 0.85f, 1f);
            w.fontSize = 16; w.fontStyle = FontStyle.Bold;
            w.padding = new RectOffset(12, 12, 30, 10);
            w.border = new RectOffset(2, 2, 2, 2);

            _skin.label.fontSize = 14;
            _skin.label.normal.textColor = text;

            var t = _skin.toggle;
            t.fontSize = 14;
            t.normal.textColor = t.onNormal.textColor = t.hover.textColor = t.onHover.textColor = t.active.textColor = t.onActive.textColor = text;

            var b = _skin.button;
            b.fontSize = 13;
            b.border = new RectOffset(1, 1, 1, 1);
            b.normal.background = Tex(new Color(0.17f, 0.21f, 0.27f, 1f));
            b.hover.background = Tex(new Color(0.24f, 0.3f, 0.39f, 1f));
            b.active.background = Tex(new Color(0.3f, 0.42f, 0.56f, 1f));
            b.onNormal.background = b.onHover.background = b.onActive.background = Tex(new Color(0.16f, 0.45f, 0.72f, 1f));
            b.normal.textColor = b.hover.textColor = b.active.textColor = text;
            b.onNormal.textColor = b.onHover.textColor = b.onActive.textColor = Color.white;

            _skin.horizontalSlider.normal.background = Tex(new Color(0.22f, 0.27f, 0.34f, 1f));
            _skin.horizontalSliderThumb.normal.background = Tex(new Color(0.55f, 0.78f, 1f, 1f));
            _skin.horizontalSliderThumb.hover.background = _skin.horizontalSliderThumb.active.background = Tex(new Color(0.75f, 0.9f, 1f, 1f));
            _skin.scrollView.normal.background = null;

            _header = new GUIStyle(_skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };
            _header.normal.textColor = new Color(0.55f, 0.82f, 1f);
            _small = new GUIStyle(_skin.label) { fontSize = 12, wordWrap = true };
            _small.normal.textColor = new Color(0.78f, 0.84f, 0.9f);
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        private static void Draw(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            Header("Общее");
            Choice("Режим карты", Cfg.Reveal, new[] { "Открывать по мере прохождения", "Вся карта сразу" });
            Choice("Стиль", Cfg.Style, new[] { "Схема (Plain)", "Вид сверху (Graphic)" });
            if (Cfg.Style.Value == MapStyle.Graphic)
                GUILayout.Label(GraphicRenderer.Failed ? "Graphic-стиль не запустился (см. лог), используется схема." : "Камера над головой рендерит реальный вид сверху; неоткрытые комнаты закрыты туманом. Немного нагружает видеокарту.", _small);
            Toggle("Подсказывать соседние неоткрытые комнаты (бледно)", Cfg.NeighborHints);
            Toggle("Показывать территорию снаружи здания", Cfg.ShowOutdoor);
            KeyBind("Окно настроек", Cfg.SettingsKey);

            Header("Большая карта");
            KeyBind("Клавиша", Cfg.BigMapKey);
            Choice("Режим клавиши", Cfg.BigMapKeyMode, new[] { "Нажатие (вкл/выкл)", "Удержание" });
            Slider("Размер (доля свободной области)", Cfg.BigMapSize, 0.3f, 1f, "0.00");
            GUILayout.Label("Отступы, чтобы карта не залезала под интерфейс игры (пикс. при 1080p):", _small);
            Slider("Отступ сверху", Cfg.BigMapMarginTop, 0f, 400f, "0");
            Slider("Отступ снизу (панель инвентаря)", Cfg.BigMapMarginBottom, 0f, 400f, "0");
            Slider("Отступ по бокам (здоровье/радиация)", Cfg.BigMapMarginSide, 0f, 800f, "0");
            Choice("Ориентация", Cfg.BigMapRotateE, new[] { "Север всегда сверху", "Вращать по направлению взгляда" });
            Slider("Масштаб (1 = вся карта, колесо мыши / +/- в игре)", Cfg.BigMapZoom, 0.5f, 12f, "0.0");
            Toggle("Курсор на большой карте: колесо — масштаб, перетаскивание — сдвиг, R — к игроку", Cfg.BigMapInteractive);
            Slider("Прозрачность фона", Cfg.BigMapOpacity, 0f, 1f, "0.00");

            Header("Миникарта");
            Toggle("Миникарта включена", Cfg.MiniEnabled);
            KeyBind("Клавиша вкл/выкл", Cfg.MiniKey);
            Slider("Дальность отображения (м от игрока до края)", Cfg.MiniRange, 5f, 150f, "0");
            Slider("Размер (пикс. при 1080p)", Cfg.MiniSize, 100f, 800f, "0");
            Choice("Форма", Cfg.MiniShapeE, new[] { "Круг", "Квадрат" });
            Toggle("Вращать вместе с направлением взгляда", Cfg.MiniRotate);
            Choice("Положение", Cfg.MiniAnchorE, new[] { "Слева сверху", "Справа сверху", "Слева снизу", "Справа снизу", "Своё" });
            bool custom = Cfg.MiniAnchorE.Value == MiniAnchor.Custom;
            Slider(custom ? "Смещение X от центра" : "Отступ X от края", Cfg.MiniOffsetX, custom ? -960f : 0f, 960f, "0");
            Slider(custom ? "Смещение Y от центра" : "Отступ Y от края", Cfg.MiniOffsetY, custom ? -540f : 0f, 540f, "0");
            Slider("Прозрачность фона", Cfg.MiniOpacity, 0f, 1f, "0.00");

            Header("Метки");
            Toggle("Показывать лут (цвет = ценность)", Cfg.ShowLoot);
            Toggle("Лут только в открытых комнатах", Cfg.LootOnlyRevealed);
            Toggle("Показывать союзников", Cfg.ShowTeammates);
            GUILayout.Label("Внимание: мимики — это монстры, поэтому «игрок» без метки союзника выдаёт мимика.", _small);
            Toggle("Показывать монстров (включая мимиков)", Cfg.ShowMonsters);
            Toggle("Имена союзников на большой карте", Cfg.ShowNames);
            Toggle("Отмечать выходы из здания и комнату-вход (зелёным)", Cfg.ShowExit);
            Toggle("Кратчайший путь до ближайшего известного выхода", Cfg.ShowExitPath);
            Slider("Размер меток", Cfg.MarkerScale, 0.5f, 2.5f, "0.00");

            Header("Совместное исследование");
            Toggle("Открывать комнаты, где побывали союзники (видимые вам)", Cfg.RevealByTeammates);
            Toggle("Обмениваться картой с игроками с этим модом (Steam-лобби)", Cfg.ShareSteam);
            GUILayout.Label("Работает без мода у хоста: данные передаются через чат Steam-лобби между клиентами, у которых стоит мод.", _small);

            Header("Дополнительно");
            Slider("Детализация карты (тексель/м, пересборка)", Cfg.Resolution, 2f, 10f, "0.0");
            var m = MapController.Model;
            GUILayout.Label(m != null ? $"Текущая карта: {m.Tiles.Count} комнат, открыто {m.RevealedCount}" : "Карта появится после входа на уровень.", _small);

            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Сбросить исследование", GUILayout.Height(28))) MapController.ResetExploration();
            if (GUILayout.Button("Пересобрать карту", GUILayout.Height(28))) MapController.ForceRebuild();
            if (GUILayout.Button("Сохранить и закрыть", GUILayout.Height(28))) Close();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private static void Header(string t)
        {
            GUILayout.Space(8);
            GUILayout.Label(t, _header);
        }

        private static void Toggle(string label, MelonPreferences_Entry<bool> e)
        {
            bool v = GUILayout.Toggle(e.Value, " " + label);
            if (v != e.Value) e.Value = v;
        }

        private static void Slider(string label, MelonPreferences_Entry<float> e, float min, float max, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(360));
            float v = GUILayout.HorizontalSlider(e.Value, min, max, GUILayout.Width(180));
            GUILayout.Label(v.ToString(fmt), GUILayout.Width(50));
            GUILayout.EndHorizontal();
            if (Math.Abs(v - e.Value) > 1e-5f) e.Value = v;
        }

        private static void Choice<T>(string label, MelonPreferences_Entry<T> e, string[] names) where T : Enum
        {
            GUILayout.Label(label);
            int cur = Convert.ToInt32(e.Value);
            int sel = names.Length > 3
                ? GUILayout.SelectionGrid(cur, names, 3)
                : GUILayout.Toolbar(cur, names);
            if (sel != cur) e.Value = (T)Enum.ToObject(typeof(T), sel);
        }

        private static void KeyBind(string label, MelonPreferences_Entry<string> e)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(360));
            string txt = _capture == e ? "Нажмите клавишу… (Esc — отмена)" : e.Value;
            if (GUILayout.Button(txt, GUILayout.Width(225)))
            {
                _capture = e;
                _captureFrame = Time.frameCount;
            }
            GUILayout.EndHorizontal();
        }
    }
}
