using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MimesisMap
{
    internal struct ActorDot
    {
        public Vector3 Pos;
        public float Yaw;
        public string Name;
        public bool Indoor;
    }

    /// <summary>Per-frame inputs shared by both views.</summary>
    internal sealed class FrameCtx
    {
        public Vector3 FocusPos;
        public float Yaw;
        public bool Indoor;
        public int CurTile = -1;
        public readonly List<ActorDot> Mates = new List<ActorDot>();
        public readonly List<(Vector3 pos, bool indoor)> Monsters = new List<(Vector3, bool)>();
        public readonly List<(Vector3 pos, int price, bool indoor, int tile)> Loot = new List<(Vector3, int, bool, int)>();
        public int MaxPrice = 1;
    }

    internal sealed class MapView
    {
        private static readonly Color BaseCol = new Color(0.62f, 0.78f, 1f, 1f);
        private static readonly Color CurCol = new Color(1f, 0.86f, 0.42f, 1f);
        private static readonly Color StartCol = new Color(0.5f, 1f, 0.55f, 1f);
        private static readonly Color OutdoorCol = new Color(0.75f, 0.75f, 0.68f, 0.8f);
        private static readonly Color SelfCol = new Color(1f, 1f, 1f, 1f);
        private static readonly Color MateCol = new Color(0.3f, 0.95f, 1f, 1f);
        private static readonly Color StairsCol = new Color(0.78f, 0.68f, 1f, 1f);
        private static readonly Color StairsIcon = new Color(0.93f, 0.88f, 1f, 1f);
        private static readonly Color MonsterCol = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly Color ExitCol = new Color(0.35f, 1f, 0.45f, 1f);
        private static readonly Color LootLo = new Color(0.95f, 0.95f, 0.55f, 1f);
        private static readonly Color LootMid = new Color(1f, 0.72f, 0.2f, 1f);
        private static readonly Color LootHi = new Color(1f, 0.3f, 0.85f, 1f);

        private readonly bool _mini;
        private GameObject _root;
        private RectTransform _panel, _content;
        private Image _bg, _frame;
        private Mask _mask;
        private RawImage[] _tiles = new RawImage[0];
        private bool[] _sameFloor = new bool[0];
        private RawImage _outdoor;
        private RawImage _graphic;
        private RenderTexture _rt;
        private Vector3 _renderedAt;
        private float _renderedCover, _nextRender;
        private bool _hasRender;
        private MarkerGraphic _markers;
        private Text _info;
        private readonly List<Text> _labels = new List<Text>();
        private MapModel _model;
        private MiniShape _shape = (MiniShape)(-1);
        private static Font _font;

        public MapView(bool mini) { _mini = mini; }

        public float LastPpm, LastRot;
        private float _fit;
        private int _fitStamp = -1;

        public bool Visible => _root != null && _root.activeSelf;

        public void Create(Transform canvas)
        {
            _root = new GameObject(_mini ? "MimesisMinimap" : "MimesisBigMap", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _panel = (RectTransform)_root.transform;

            _bg = _root.AddComponent<Image>();
            _bg.raycastTarget = false;
            _mask = _root.AddComponent<Mask>();
            _mask.showMaskGraphic = true;

            var c = new GameObject("Content", typeof(RectTransform));
            c.transform.SetParent(_panel, false);
            _content = (RectTransform)c.transform;
            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;

            var m = new GameObject("Markers", typeof(RectTransform));
            m.transform.SetParent(_panel, false);
            _markers = m.AddComponent<MarkerGraphic>();
            _markers.raycastTarget = false;
            Stretch(_markers.rectTransform);

            var f = new GameObject("Frame", typeof(RectTransform));
            f.transform.SetParent(_panel, false);
            _frame = f.AddComponent<Image>();
            _frame.raycastTarget = false;
            _frame.color = new Color(0.85f, 0.9f, 1f, 0.75f);
            Stretch(_frame.rectTransform);

            if (!_mini)
            {
                _info = MakeText(_panel, 20, TextAnchor.UpperLeft);
                var r = _info.rectTransform;
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(14, -10); r.sizeDelta = new Vector2(-28, 60);
            }
            _root.SetActive(false);
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; }
            _tiles = new RawImage[0];
            _outdoor = null;
            _labels.Clear();
            _model = null;
        }

        public void SetVisible(bool v)
        {
            if (_root != null && _root.activeSelf != v) _root.SetActive(v);
        }

        public void Bind(MapModel model)
        {
            if (_content == null) return;
            foreach (Transform ch in _content) Object.Destroy(ch.gameObject);
            _model = model;
            _tiles = new RawImage[model?.Tiles.Count ?? 0];
            _sameFloor = new bool[_tiles.Length];
            _outdoor = null;
            _graphic = null;
            _hasRender = false;
            if (model == null) return;

            _graphic = MakeImage("Graphic", null, new Rect(0, 0, 1, 1), Color.white);
            _graphic.gameObject.SetActive(false);
            if (model.OutdoorTex != null)
                _outdoor = MakeImage("Outdoor", model.OutdoorTex, model.OutdoorRect, OutdoorCol);
            for (int i = 0; i < model.Tiles.Count; i++)
            {
                var t = model.Tiles[i];
                if (t.Tex == null) continue;
                _tiles[i] = MakeImage("Tile" + i, t.Tex, t.Rect, BaseCol);
                _tiles[i].gameObject.SetActive(false);
            }
        }

        private RawImage MakeImage(string name, Texture2D tex, Rect rect, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.color = col;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = Vector2.zero;
            r.anchoredPosition = rect.min;
            r.sizeDelta = rect.size;
            return img;
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(FrameCtx ctx, Vector2 canvasSize)
        {
            if (_root == null || _model == null || !_root.activeSelf) return;
            var model = _model;
            bool full = Cfg.Reveal.Value == RevealMode.Full;
            bool hints = Cfg.NeighborHints.Value;
            float ms = Mathf.Clamp(Cfg.MarkerScale.Value, 0.3f, 3f);

            // ---- panel layout
            float size;
            var shape = _mini ? Cfg.MiniShapeE.Value : MiniShape.Square;
            if (_mini)
            {
                size = Mathf.Clamp(Cfg.MiniSize.Value, 80f, 900f);
                PlaceMini(size);
                _bg.color = new Color(0.03f, 0.05f, 0.08f, Mathf.Clamp01(Cfg.MiniOpacity.Value));
            }
            else
            {
                // free area between the game's HUD elements, then scaled by the size setting
                float top = Mathf.Max(0f, Cfg.BigMapMarginTop.Value), bottom = Mathf.Max(0f, Cfg.BigMapMarginBottom.Value);
                float side = Mathf.Max(0f, Cfg.BigMapMarginSide.Value);
                float k = Mathf.Clamp(Cfg.BigMapSize.Value, 0.3f, 1f);
                float h = Mathf.Max(200f, canvasSize.y - top - bottom) * k;
                float w = Mathf.Max(200f, canvasSize.x - side * 2f) * k;
                size = h;
                _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
                _panel.anchoredPosition = new Vector2(0f, (bottom - top) * 0.5f);
                _panel.sizeDelta = new Vector2(Mathf.Min(w, h * 1.6f), h);
                _bg.color = new Color(0.02f, 0.03f, 0.05f, Mathf.Clamp01(Cfg.BigMapOpacity.Value));
            }
            if (shape != _shape)
            {
                _shape = shape;
                _bg.sprite = shape == MiniShape.Circle ? Sprites.Circle : null;
                _frame.sprite = shape == MiniShape.Circle ? Sprites.Ring : Sprites.Frame;
                _frame.type = shape == MiniShape.Circle ? Image.Type.Simple : Image.Type.Sliced;
            }
            Vector2 panelSize = _panel.sizeDelta;

            // ---- visible content & view transform
            bool indoor = ctx.Indoor;
            Rect vis = default;
            bool any = false;
            bool graphic = Cfg.Style.Value == MapStyle.Graphic && !GraphicRenderer.Failed;
            for (int i = 0; i < _tiles.Length; i++)
            {
                var img = _tiles[i];
                if (img == null) continue;
                var t = model.Tiles[i];
                bool hint = !full && hints && !t.Revealed && model.IsHint(i);
                bool include = indoor && (full || t.Revealed || hint);
                if (include) { if (!any) { vis = t.Rect; any = true; } else vis = Union(vis, t.Rect); }

                if (graphic)
                {
                    // the camera image shows the rooms; unexplored ones are covered by fog of their own shape
                    bool fog = indoor && !full && !t.Revealed && t.Fog != null;
                    if (img.gameObject.activeSelf != fog) img.gameObject.SetActive(fog);
                    if (!fog) continue;
                    if (img.texture != t.Fog) img.texture = t.Fog;
                    img.color = new Color(0.01f, 0.015f, 0.02f, hint ? 0.8f : 0.97f);
                    continue;
                }

                if (img.gameObject.activeSelf != include) img.gameObject.SetActive(include);
                if (!include) continue;
                if (img.texture != t.Tex) img.texture = t.Tex;
                Color col = i == ctx.CurTile ? CurCol : (t.IsStart && Cfg.ShowExit.Value ? StartCol : t.IsStairs ? StairsCol : BaseCol);
                float a = 1f;
                if (hint) a = 0.22f;
                else if (full && !t.Revealed) a = 0.6f;
                bool same = MapModel.SameFloor(t, ctx.FocusPos.y);
                if (!same) a *= 0.45f;
                // rooms of the current floor are drawn over overlapping rooms of other floors
                if (_sameFloor[i] != same)
                {
                    _sameFloor[i] = same;
                    if (same) img.transform.SetAsLastSibling();
                    else img.transform.SetSiblingIndex(_graphic != null ? 1 : 0);
                }
                col.a = a;
                img.color = col;
            }
            if (_outdoor != null)
            {
                bool showOut = !indoor && (Cfg.ShowOutdoor.Value || model.Flat);
                bool drawOut = showOut && !graphic;
                if (_outdoor.gameObject.activeSelf != drawOut) _outdoor.gameObject.SetActive(drawOut);
                if (showOut && !any) { vis = model.OutdoorRect; any = true; }
            }

            Vector2 focus = new Vector2(ctx.FocusPos.x, ctx.FocusPos.z);
            Vector2 center;
            float ppm, rot;
            if (_mini)
            {
                ppm = (Mathf.Min(panelSize.x, panelSize.y) * 0.5f) / Mathf.Clamp(Cfg.MiniRange.Value, 3f, 300f);
                center = focus;
                rot = Cfg.MiniRotate.Value ? ctx.Yaw : 0f;
            }
            else
            {
                // centred on you; zoom 1 fits everything known around you
                if (!any) vis = new Rect(focus - Vector2.one * 30f, Vector2.one * 60f);
                rot = Cfg.BigMapRotateE.Value == Orientation.FollowView ? ctx.Yaw : 0f;
                // the fit is measured once per opening, so the map doesn't keep rescaling while you walk
                float fit = _fit;
                if (_fitStamp != MapController.BigOpenStamp || fit <= 0f)
                {
                _fitStamp = MapController.BigOpenStamp;
                if (Cfg.BigMapRotateE.Value == Orientation.FollowView)
                {
                    float r = 5f;
                    foreach (var c in new[] { vis.min, vis.max, new Vector2(vis.xMin, vis.yMax), new Vector2(vis.xMax, vis.yMin) })
                        r = Mathf.Max(r, Vector2.Distance(c, focus));
                    fit = (Mathf.Min(panelSize.x, panelSize.y) * 0.5f - 20f) / r;
                }
                else
                {
                    float hw = Mathf.Max(5f, Mathf.Abs(vis.xMin - focus.x), Mathf.Abs(vis.xMax - focus.x));
                    float hh = Mathf.Max(5f, Mathf.Abs(vis.yMin - focus.y), Mathf.Abs(vis.yMax - focus.y));
                    fit = Mathf.Min((panelSize.x * 0.5f - 20f) / hw, (panelSize.y * 0.5f - 50f) / hh);
                }
                _fit = fit;
                }
                ppm = fit * Mathf.Clamp(Cfg.BigMapZoom.Value, 0.5f, 12f);
                center = focus + MapController.BigPan;
            }
            LastPpm = ppm;
            LastRot = rot;
            UpdateGraphic(graphic, ctx, center, ppm, panelSize);

            var R = Quaternion.Euler(0, 0, rot);
            _content.localScale = new Vector3(ppm, ppm, 1f);
            _content.localRotation = R;
            _content.anchoredPosition = -(Vector2)(R * (center * ppm));

            // ---- markers
            var list = _markers.Markers;
            list.Clear();
            int labelIdx = 0;
            float edge = Mathf.Min(panelSize.x, panelSize.y) * 0.5f - 8f;

            if (indoor)
            {
                for (int i = 0; i < model.Tiles.Count; i++)
                {
                    var t = model.Tiles[i];
                    if (!t.IsStairs || !(full || t.Revealed)) continue;
                    var p = ToPanel(t.StairsAt, center, ppm, R);
                    if (!Inside(p, panelSize)) continue;
                    var col = StairsIcon;
                    if (!MapModel.SameFloor(t, ctx.FocusPos.y)) col.a = 0.5f;
                    list.Add(new Marker { Pos = p, Size = 5.5f * ms, Color = col, Shape = Shape.Stairs });
                }
            }

            if (Cfg.ShowLoot.Value)
            {
                foreach (var l in ctx.Loot)
                {
                    if (l.indoor != indoor) continue;
                    if (indoor && !full && Cfg.LootOnlyRevealed.Value && (l.tile < 0 || !model.Tiles[l.tile].Revealed)) continue;
                    var p = ToPanel(l.pos, center, ppm, R);
                    if (!Inside(p, panelSize)) continue;
                    float rel = l.price / (float)Mathf.Max(1, ctx.MaxPrice);
                    var col = rel >= 0.66f ? LootHi : rel >= 0.33f ? LootMid : LootLo;
                    if (Mathf.Abs(l.pos.y - ctx.FocusPos.y) > 3.5f) col.a = 0.4f;
                    list.Add(new Marker { Pos = p, Rot = 0, Size = 4.5f * ms, Color = col, Shape = Shape.Diamond });
                }
            }

            if (Cfg.ShowMonsters.Value)
            {
                foreach (var mo in ctx.Monsters)
                {
                    if (mo.indoor != indoor) continue;
                    var p = ToPanel(mo.pos, center, ppm, R);
                    if (!Inside(p, panelSize)) continue;
                    var col = MonsterCol;
                    if (Mathf.Abs(mo.pos.y - ctx.FocusPos.y) > 3.5f) col.a = 0.4f;
                    list.Add(new Marker { Pos = p, Size = 5f * ms, Color = col, Shape = Shape.Dot });
                }
            }

            var path = _markers.Path;
            path.Clear();
            if (Cfg.ShowExit.Value)
            {
                if (indoor && Cfg.ShowExitPath.Value)
                {
                    foreach (var w in ExitPath.Path) path.Add(ToPanel(w, center, ppm, R));
                    MarkerGraphic.Smooth(path, 2);
                }
                for (int e = 0; e < model.Exits.Count; e++)
                {
                    var ex = model.Exits[e];
                    // indoor exits on the indoor layer once their room is known; entrance doors on the outdoor layer
                    if (ex.ToIndoor == indoor) continue;
                    if (indoor && !full && (ex.Tile < 0 || !model.Tiles[ex.Tile].Revealed)) continue;
                    var p = ToPanel(ex.Pos, center, ppm, R);
                    bool target = e == ExitPath.TargetExit;
                    if (_mini && target) p = ClampEdge(p, edge, shape);
                    else if (!Inside(p, panelSize)) continue;
                    list.Add(new Marker { Pos = p, Size = (target ? 6.5f : 5f) * ms, Color = ExitCol, Shape = Shape.Square });
                }
            }

            if (Cfg.ShowTeammates.Value)
            {
                foreach (var mate in ctx.Mates)
                {
                    if (mate.Indoor != indoor) continue;
                    var p = ToPanel(mate.Pos, center, ppm, R);
                    bool clamped = false;
                    if (_mini)
                    {
                        var c2 = ClampEdge(p, edge, shape);
                        clamped = c2 != p;
                        p = c2;
                    }
                    else if (!Inside(p, panelSize)) continue;
                    var col = MateCol;
                    if (Mathf.Abs(mate.Pos.y - ctx.FocusPos.y) > 3.5f) col.a = 0.45f;
                    list.Add(clamped
                        ? new Marker { Pos = p, Size = 4f * ms, Color = col, Shape = Shape.Dot }
                        : new Marker { Pos = p, Rot = rot - mate.Yaw, Size = 7f * ms, Color = col, Shape = Shape.Arrow });
                    if (!_mini && Cfg.ShowNames.Value && !string.IsNullOrEmpty(mate.Name))
                        SetLabel(labelIdx++, mate.Name, p + new Vector2(0, 12f * ms), MateCol);
                }
            }

            list.Add(new Marker { Pos = ToPanel(ctx.FocusPos, center, ppm, R), Rot = rot - ctx.Yaw, Size = 8f * ms, Color = SelfCol, Shape = Shape.Arrow });
            _markers.SetVerticesDirty();

            for (int i = labelIdx; i < _labels.Count; i++)
                if (_labels[i].gameObject.activeSelf) _labels[i].gameObject.SetActive(false);

            if (_info != null)
            {
                string mode = full ? "полная" : "исследование";
                string where = model.Flat ? "Схема локации" : indoor ? $"Комнат открыто: {model.RevealedCount} / {model.Tiles.Count}" : "Снаружи";
                var legend = "<color=#ffffff>▲</color> вы   <color=#c7adff>▙</color> лестница";
                if (Cfg.ShowTeammates.Value) legend += "   <color=#4df2ff>▲</color> союзники";
                if (Cfg.ShowMonsters.Value) legend += "   <color=#ff4033>●</color> монстры";
                if (Cfg.ShowExit.Value) legend += "   <color=#59ff73>■</color> выход" + (Cfg.ShowExitPath.Value ? " <color=#59ff73>—</color> путь" : "");
                if (Cfg.ShowLoot.Value) legend += "   лут: <color=#f2f28c>◆</color> дешёвый <color=#ffb833>◆</color> средний <color=#ff4dd9>◆</color> ценный";
                _info.text = $"{where}   (режим: {mode})\n<size=15>{legend}\n<color=#9ab>[{Cfg.BigMapKey.Value}] закрыть   колесо/[+]/[-] масштаб   перетаскивание — сдвиг   [R] к себе   [{Cfg.SettingsKey.Value}] настройки</color></size>";
            }
        }

        /// <summary>Re-renders the top-down camera image when needed and places it in map space (metres).</summary>
        private void UpdateGraphic(bool graphic, FrameCtx ctx, Vector2 center, float ppm, Vector2 panelSize)
        {
            if (_graphic == null) return;
            if (!graphic)
            {
                if (_graphic.gameObject.activeSelf) _graphic.gameObject.SetActive(false);
                return;
            }
            // metres visible across the panel diagonal (rotation-safe), plus slack so the image can slide between renders
            float viewDiag = new Vector2(panelSize.x, panelSize.y).magnitude / Mathf.Max(ppm, 0.001f);
            float cover = Mathf.Min(viewDiag * (_mini ? 1.35f : 1.05f), 300f);
            int res = _mini ? 512 : 1024;
            if (_rt == null || _rt.width != res)
            {
                if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
                _rt = new RenderTexture(res, res, 16, RenderTextureFormat.ARGB32) { name = "MimesisMapGraphic" };
                _rt.Create();
                _hasRender = false;
            }
            var target = _mini ? ctx.FocusPos : new Vector3(center.x, ctx.FocusPos.y, center.y);
            float slack = (_renderedCover - viewDiag) * 0.5f;
            float moved = new Vector2(target.x - _renderedAt.x, target.z - _renderedAt.z).magnitude;
            bool stale = !_hasRender || Time.unscaledTime >= _nextRender || moved > slack * 0.8f
                         || Mathf.Abs(target.y - _renderedAt.y) > 1.5f || cover > _renderedCover * 1.05f || cover < _renderedCover * 0.6f;
            if (stale)
            {
                if (GraphicRenderer.Render(_rt, target, cover, 40f))
                {
                    _renderedAt = target;
                    _renderedCover = cover;
                    _hasRender = true;
                }
                _nextRender = Time.unscaledTime + (_mini ? 0.15f : 0.5f);
            }
            bool show = _hasRender;
            if (_graphic.gameObject.activeSelf != show) _graphic.gameObject.SetActive(show);
            if (!show) return;
            if (_graphic.texture != _rt) _graphic.texture = _rt;
            var r = _graphic.rectTransform;
            r.anchoredPosition = new Vector2(_renderedAt.x - _renderedCover * 0.5f, _renderedAt.z - _renderedCover * 0.5f);
            r.sizeDelta = new Vector2(_renderedCover, _renderedCover);
        }

        private void PlaceMini(float size)
        {
            float ox = Cfg.MiniOffsetX.Value, oy = Cfg.MiniOffsetY.Value;
            Vector2 a, pos;
            switch (Cfg.MiniAnchorE.Value)
            {
                case MiniAnchor.TopLeft: a = new Vector2(0, 1); pos = new Vector2(ox, -oy); break;
                case MiniAnchor.BottomLeft: a = new Vector2(0, 0); pos = new Vector2(ox, oy); break;
                case MiniAnchor.BottomRight: a = new Vector2(1, 0); pos = new Vector2(-ox, oy); break;
                case MiniAnchor.Custom: a = new Vector2(0.5f, 0.5f); pos = new Vector2(ox, oy); break;
                default: a = new Vector2(1, 1); pos = new Vector2(-ox, -oy); break;
            }
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = a;
            _panel.anchoredPosition = pos;
            _panel.sizeDelta = new Vector2(size, size);
        }

        private static Vector2 ToPanel(Vector3 w, Vector2 center, float ppm, Quaternion R)
        {
            var d = (new Vector2(w.x, w.z) - center) * ppm;
            return R * d;
        }

        private static bool Inside(Vector2 p, Vector2 size) => Mathf.Abs(p.x) <= size.x * 0.5f + 10f && Mathf.Abs(p.y) <= size.y * 0.5f + 10f;

        private static Vector2 ClampEdge(Vector2 p, float edge, MiniShape shape)
        {
            if (shape == MiniShape.Circle)
                return p.magnitude > edge ? p.normalized * edge : p;
            return new Vector2(Mathf.Clamp(p.x, -edge, edge), Mathf.Clamp(p.y, -edge, edge));
        }

        private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private void SetLabel(int idx, string text, Vector2 pos, Color col)
        {
            while (_labels.Count <= idx)
            {
                var t = MakeText(_panel, 15, TextAnchor.LowerCenter);
                t.rectTransform.sizeDelta = new Vector2(200, 24);
                t.rectTransform.pivot = new Vector2(0.5f, 0f);
                _labels.Add(t);
            }
            var l = _labels[idx];
            if (!l.gameObject.activeSelf) l.gameObject.SetActive(true);
            l.text = text;
            l.color = col;
            l.rectTransform.anchoredPosition = pos;
        }

        private static Text MakeText(Transform parent, int size, TextAnchor anchor)
        {
            if (_font == null)
            {
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (_font == null) try { _font = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
            }
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = new Color(0.9f, 0.95f, 1f, 1f);
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.9f);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            return t;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
