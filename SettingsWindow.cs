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
                // Delete / Backspace clears the binding
                if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { _capture.Value = ""; _capture = null; return; }
                string got = null;
                var m = Mouse.current;
                if (m != null)
                {
                    float wheel = m.scroll.ReadValue().y;
                    if (wheel > 0.01f) got = Cfg.WheelUp;
                    else if (wheel < -0.01f) got = Cfg.WheelDown;
                    else if (m.middleButton.wasPressedThisFrame) got = "MouseMiddle";
                    else if (m.rightButton.wasPressedThisFrame) got = "MouseRight";
                    else if (m.backButton.wasPressedThisFrame) got = "MouseBack";
                    else if (m.forwardButton.wasPressedThisFrame) got = "MouseForward";
                }
                if (got == null)
                    foreach (var k in kb.allKeys)
                        if (k != null && k.wasPressedThisFrame) { got = k.keyCode.ToString(); break; }
                if (got != null) { _capture.Value = got; _capture = null; }
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
            _win = GUILayout.Window(0x4D4D4150, _win, Draw, Loc.T("Mimesis Map — настройки", "Mimesis Map — settings"));
            GUI.skin = oldSkin;
            GUI.matrix = old;
        }

        private static GUISkin _skin;
        private static float _resetConfirmUntil;

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
            _skin.label.wordWrap = true;
            _skin.label.normal.textColor = text;

            var t = _skin.toggle;
            t.fontSize = 14;
            t.wordWrap = true;
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

        private const float ContentWidth = 604f;   // fixed, so nothing can push out a horizontal scrollbar

        private static string L(string ru, string en) => Loc.T(ru, en);

        private static void Draw(int id)
        {
            // vertical scrolling only
            _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(ContentWidth));

            Choice(L("Язык / Language", "Language / Язык"), Cfg.Language, new[] { L("Авто (как в системе)", "Auto (system)"), "Русский", "English" });

            Header(L("Управление", "Controls"));
            GUILayout.Label(L("Две привязки на действие: основная и альтернативная. Нажмите кнопку, затем клавишу, колесо или кнопку мыши. Delete — очистить, Esc — отмена. Масштаб колесом выключается очисткой привязки «Колесо».",
                              "Two bindings per action: main and alternative. Click a button, then press a key, scroll the wheel or press a mouse button. Delete clears, Esc cancels. Clear the \"Wheel\" bindings to turn off wheel zoom."), _small);
            foreach (var bd in Cfg.Binds) BindRow(bd);

            Header(L("Общее", "General"));
            Choice(L("Режим карты", "Map mode"), Cfg.Reveal, new[] { L("Открывать по мере прохождения", "Reveal as you explore"), L("Вся карта сразу", "Full map") });
            Choice(L("Стиль", "Style"), Cfg.Style, new[] { L("Схема (Plain)", "Floor plan (Plain)"), L("Вид сверху (Graphic)", "Top-down view (Graphic)") });
            if (Cfg.Style.Value == MapStyle.Graphic)
                GUILayout.Label(GraphicRenderer.Failed
                    ? L("Graphic-стиль не запустился (см. лог), используется схема.", "Graphic style failed to start (see the log); the floor plan is used.")
                    : L("Камера над головой рендерит реальный вид сверху; неоткрытые комнаты закрыты туманом. Немного нагружает видеокарту.", "A camera above your head renders the real top-down view; unexplored rooms are covered by fog. Costs a little GPU time."), _small);
            Toggle(L("Подсказывать соседние неоткрытые комнаты (бледно)", "Faintly show unexplored rooms next to explored ones"), Cfg.NeighborHints);
            Toggle(L("Показывать территорию снаружи здания", "Show the area outside the building"), Cfg.ShowOutdoor);

            Header(L("Большая карта", "Big map"));
            Choice(L("Режим клавиши", "Key mode"), Cfg.BigMapKeyMode, new[] { L("Удержание", "Hold"), L("Нажатие (вкл/выкл)", "Press to toggle") });
            Choice(L("Ориентация", "Orientation"), Cfg.BigMapRotateE, new[] { L("Вращать по направлению взгляда", "Rotate with your view"), L("Север всегда сверху", "North up") });
            Slider(L("Масштаб (1 = вся карта)", "Zoom (1 = whole map)"), Cfg.BigMapZoom, 0.5f, 12f, "0.0");
            Toggle(L("Курсор в режиме «Нажатие»: перетаскивание мышью сдвигает карту", "Cursor in \"Press to toggle\" mode: drag with the mouse to pan"), Cfg.BigMapInteractive);
            Slider(L("Размер (доля свободной области)", "Size (share of the free area)"), Cfg.BigMapSize, 0.3f, 1f, "0.00");
            GUILayout.Label(L("Отступы, чтобы карта не залезала под интерфейс игры (пикс. при 1080p):", "Margins that keep the map clear of the game's HUD (px at 1080p):"), _small);
            Slider(L("Отступ сверху", "Top margin"), Cfg.BigMapMarginTop, 0f, 400f, "0");
            Slider(L("Отступ снизу (панель инвентаря)", "Bottom margin (hotbar)"), Cfg.BigMapMarginBottom, 0f, 400f, "0");
            Slider(L("Отступ по бокам (здоровье/радиация)", "Side margins (health/radiation)"), Cfg.BigMapMarginSide, 0f, 800f, "0");
            Slider(L("Прозрачность фона", "Background opacity"), Cfg.BigMapOpacity, 0f, 1f, "0.00");

            Header(L("Миникарта", "Minimap"));
            Toggle(L("Миникарта включена", "Minimap enabled"), Cfg.MiniEnabled);
            Choice(L("Ориентация", "Orientation"), Cfg.MiniOrientation, new[] { L("Вращать по направлению взгляда", "Rotate with your view"), L("Север всегда сверху", "North up") });
            Slider(L("Дальность (м от игрока до края)", "Range (m from you to the edge)"), Cfg.MiniRange, 5f, 150f, "0");
            Slider(L("Размер (пикс. при 1080p)", "Size (px at 1080p)"), Cfg.MiniSize, 100f, 800f, "0");
            Choice(L("Форма", "Shape"), Cfg.MiniShapeE, new[] { L("Круг", "Circle"), L("Квадрат", "Square") });
            Choice(L("Положение", "Position"), Cfg.MiniAnchorE, new[] { L("Слева сверху", "Top left"), L("Справа сверху", "Top right"), L("Слева снизу", "Bottom left"), L("Справа снизу", "Bottom right"), L("Своё", "Custom") });
            bool custom = Cfg.MiniAnchorE.Value == MiniAnchor.Custom;
            Slider(custom ? L("Смещение X от центра", "X offset from centre") : L("Отступ X от края", "X margin from edge"), Cfg.MiniOffsetX, custom ? -960f : 0f, 960f, "0");
            Slider(custom ? L("Смещение Y от центра", "Y offset from centre") : L("Отступ Y от края", "Y margin from edge"), Cfg.MiniOffsetY, custom ? -540f : 0f, 540f, "0");
            Slider(L("Прозрачность фона", "Background opacity"), Cfg.MiniOpacity, 0f, 1f, "0.00");

            Header(L("Метки", "Markers"));
            Toggle(L("Показывать лут (цвет = ценность)", "Show loot (colour = value)"), Cfg.ShowLoot);
            Toggle(L("Лут только в открытых комнатах", "Loot only in explored rooms"), Cfg.LootOnlyRevealed);
            Toggle(L("Показывать союзников", "Show teammates"), Cfg.ShowTeammates);
            GUILayout.Label(L("Внимание: мимики — это монстры, поэтому «игрок» без метки союзника выдаёт мимика.", "Note: mimics are monsters, so a \"player\" without a teammate marker gives a mimic away."), _small);
            Toggle(L("Показывать монстров (включая мимиков)", "Show monsters (mimics included)"), Cfg.ShowMonsters);
            Toggle(L("Имена союзников на большой карте", "Teammate names on the big map"), Cfg.ShowNames);
            Toggle(L("Отмечать выходы из здания и комнату-вход (зелёным)", "Mark building exits and the entrance room (green)"), Cfg.ShowExit);
            Toggle(L("Кратчайший путь до ближайшего известного выхода", "Shortest path to the nearest known exit"), Cfg.ShowExitPath);
            Slider(L("Размер меток", "Marker size"), Cfg.MarkerScale, 0.5f, 2.5f, "0.00");

            Header(L("Совместное исследование", "Shared exploration"));
            Toggle(L("Открывать комнаты, где побывали союзники (видимые вам)", "Reveal rooms where teammates have been (when you can see them)"), Cfg.RevealByTeammates);
            Toggle(L("Обмениваться картой с игроками с этим модом (Steam-лобби)", "Share the map with players who have this mod (Steam lobby)"), Cfg.ShareSteam);
            GUILayout.Label(L("Работает без мода у хоста: данные передаются через чат Steam-лобби между клиентами, у которых стоит мод.", "The host doesn't need the mod: data goes through the Steam lobby chat between clients that have it."), _small);

            Header(L("Дополнительно", "Advanced"));
            Slider(L("Детализация карты (тексель/м, пересборка)", "Map detail (texels/m, rebuilds the map)"), Cfg.Resolution, 2f, 10f, "0.0");
            var m = MapController.Model;
            GUILayout.Label(m != null
                ? L($"Текущая карта: {m.Tiles.Count} комнат, открыто {m.RevealedCount}", $"Current map: {m.Tiles.Count} rooms, {m.RevealedCount} explored")
                : L("Карта появится после входа на уровень.", "The map appears once you are in a level."), _small);

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(L("Сбросить исследование", "Reset exploration"), GUILayout.Height(28))) MapController.ResetExploration();
            bool confirming = Time.unscaledTime < _resetConfirmUntil;
            if (GUILayout.Button(confirming ? L("Точно? Нажмите ещё раз", "Sure? Click again") : L("Сбросить настройки", "Reset settings"), GUILayout.Height(28)))
            {
                if (confirming)
                {
                    _resetConfirmUntil = 0f;
                    float res = Cfg.Resolution.Value;
                    Cfg.ResetAll();
                    if (Math.Abs(res - Cfg.Resolution.Value) > 0.01f) MapController.ForceRebuild();
                }
                else _resetConfirmUntil = Time.unscaledTime + 3f;
            }
            if (GUILayout.Button(L("Пересобрать карту", "Rebuild map"), GUILayout.Height(28))) MapController.ForceRebuild();
            if (GUILayout.Button(L("Сохранить и закрыть", "Save and close"), GUILayout.Height(28))) Close();
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
            GUILayout.Label(label, GUILayout.Width(364));
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

        private static void BindRow(Bind b)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(b.Label, GUILayout.Width(250));
            BindButton(b.Main);
            BindButton(b.Alt);
            GUILayout.EndHorizontal();
        }

        private static void BindButton(MelonPreferences_Entry<string> e)
        {
            string txt = _capture == e ? Loc.T("Нажмите…", "Press…") : Cfg.Pretty(e.Value);
            if (GUILayout.Button(txt, GUILayout.Width(175)))
            {
                _capture = e;
                _captureFrame = Time.frameCount;
            }
        }
    }
}
