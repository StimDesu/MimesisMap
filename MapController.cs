using System;
using System.Collections.Generic;
using DunGen;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MimesisMap
{
    internal static class MapController
    {
        private static MapModel _model;
        private static Dungeon _seenDungeon;
        private static GameMainBase _seenMain;
        private static float _mainSeenAt, _modelBuiltAt;
        private static float _seenAt, _navBuiltAt = -1f, _nextDungeonScan, _nextReveal;
        private static GameObject _canvasGo;
        private static RectTransform _canvasRect;
        private static MapView _mini, _big;
        private static bool _bigOpen;
        private static readonly Dictionary<string, HashSet<int>> Explored = new Dictionary<string, HashSet<int>>();
        private static readonly FrameCtx Ctx = new FrameCtx();

        public static MapModel Model => _model;
        public static Vector2 BigPan;
        public static int BigOpenStamp;

        private static void Zoom(float k) => Cfg.BigMapZoom.Value = Mathf.Clamp(Cfg.BigMapZoom.Value * k, 0.5f, 12f);

        public static void Init()
        {
            Sharing.Received += OnShared;
        }

        public static void OnNavMeshBuilt()
        {
            _navBuiltAt = Time.unscaledTime;
            _nextNavCheck = 0f;
        }

        public static void ResetExploration()
        {
            if (_model == null) return;
            foreach (var t in _model.Tiles) t.Revealed = false;
            _model.RevealedCount = 0;
            Explored.Remove(_model.Key);
        }

        public static void ForceRebuild()
        {
            Teardown();
            _seenDungeon = null;
        }

        // ------------------------------------------------------------------ update

        public static void Update()
        {
            if (!SettingsWindow.Capturing)
            {
                if (Cfg.Pressed(Cfg.SettingsKey)) SettingsWindow.Toggle();
                if (Cfg.Pressed(Cfg.MiniKey)) { Cfg.MiniEnabled.Value = !Cfg.MiniEnabled.Value; Cfg.Save(); }
                bool wasOpen = _bigOpen;
                if (Cfg.BigMapKeyMode.Value == KeyMode.Hold) _bigOpen = Cfg.Held(Cfg.BigMapKey);
                else if (Cfg.Pressed(Cfg.BigMapKey)) _bigOpen = !_bigOpen;
                if (_bigOpen && !wasOpen) { BigPan = Vector2.zero; BigOpenStamp++; }   // every opening starts centred on you

                var kb = Keyboard.current;
                if (_bigOpen && kb != null)
                {
                    if (kb.escapeKey.wasPressedThisFrame) _bigOpen = false;
                    if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) Zoom(1.25f);
                    if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) Zoom(1f / 1.25f);
                    if (kb.rKey.wasPressedThisFrame || kb.homeKey.wasPressedThisFrame) { BigPan = Vector2.zero; BigOpenStamp++; }
                }
            }

            // interactive big map: free cursor, wheel zooms, dragging pans
            bool interactive = _bigOpen && _model != null && Cfg.BigMapKeyMode.Value == KeyMode.Toggle && Cfg.BigMapInteractive.Value;
            InputBlock.Set("bigmap", interactive);
            var mouse = Mouse.current;
            if (interactive && mouse != null && !SettingsWindow.Open)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.01f) Zoom(1.15f); else if (wheel < -0.01f) Zoom(1f / 1.15f);
                if (mouse.leftButton.isPressed && _big != null && _big.LastPpm > 0.0001f)
                {
                    var d = mouse.delta.ReadValue() * (1080f / Mathf.Max(1, Screen.height)) / _big.LastPpm;
                    BigPan -= (Vector2)(Quaternion.Euler(0, 0, -_big.LastRot) * d);
                }
            }

            var main = GameAccess.CurrentMain();
            if (main == null)
            {
                Diagnose("no game scene");
                if (_model != null) Teardown();
                _seenDungeon = null;
                _seenMain = null;
                return;
            }
            if (main != _seenMain) { _seenMain = main; _mainSeenAt = Time.unscaledTime; }

            // scene changed, dungeon destroyed, or the scene's floor was rebuilt -> drop the map
            if (_model != null && (_model.Owner != main || (!_model.Flat && _model.DungeonGone) || (_model.Flat && _navBuiltAt > _modelBuiltAt)))
                Teardown();

            if (_model == null && Time.unscaledTime >= _nextDungeonScan)
            {
                _nextDungeonScan = Time.unscaledTime + 0.5f;
                TryBuild(main);
            }
        }

        private static float _nextNavCheck, _nextExitScan, _nextPath;
        private static int _seenTileCount;

        private static void TryBuild(GameMainBase main)
        {
            var dungeons = GameAccess.FindDungeons();
            if (dungeons.Count == 0)
            {
                // Levels get a dungeon a moment after load; everything else (tram, lobby) is mapped from its floor.
                if (main is GamePlayScene && Time.unscaledTime - _mainSeenAt < 10f) { Diagnose("no generated dungeon in this scene yet"); return; }
                if (GameAccess.LocalPlayer == null) GameAccess.Scan(true);
                if (GameAccess.LocalPlayer == null || Time.unscaledTime - _mainSeenAt < 1f) { Diagnose("no local player yet"); return; }
                MapModel flat = null;
                try { flat = MapModel.BuildFlat("scene:" + main.GetType().Name + ":" + main.GetInstanceID()); }
                catch (Exception e) { MelonLogger.Error("[MimesisMap] Flat map build failed: " + e); _nextDungeonScan = Time.unscaledTime + 10f; }
                if (flat == null) { Diagnose("no NavMesh or A* graph in " + main.GetType().Name + " yet"); _nextDungeonScan = Mathf.Max(_nextDungeonScan, Time.unscaledTime + 2f); return; }
                Install(flat, main);
                return;
            }

            // wait until generation has settled (same tile count for a second)
            int tileCount = 0;
            foreach (var d in dungeons) tileCount += d.AllTiles.Count;
            if (dungeons[0] != _seenDungeon || tileCount != _seenTileCount)
            {
                _seenDungeon = dungeons[0];
                _seenTileCount = tileCount;
                _seenAt = Time.unscaledTime;
                Diagnose("dungeon generating");
                return;
            }
            if (Time.unscaledTime - _seenAt < 1f) return;

            // wait until the walkable floor really covers the building (NavMesh, or the A* graph as fallback)
            if (Time.unscaledTime < _nextNavCheck) return;
            _nextNavCheck = Time.unscaledTime + 1f;
            var bounds = MapModel.BoundsOf(dungeons);
            var (v, ind, src) = MapModel.FloorTriangles(bounds);
            int inside = MapModel.CountInside(v, ind, bounds);
            if (inside < 20)
            {
                Diagnose($"waiting for walkable floor inside the building ({src}: {ind.Length / 3} triangles, {inside} inside {dungeons.Count} building(s), bounds {bounds.center:0} size {bounds.size:0})");
                return;
            }

            string key;
            try
            {
                var p = dungeons[0].AllTiles[0].transform.position;
                key = $"{GameAccess.DungeonSeed()}:{tileCount}:{Mathf.RoundToInt(p.x)}:{Mathf.RoundToInt(p.z)}";
            }
            catch { key = "unknown:" + dungeons[0].GetInstanceID(); }

            MapModel model;
            try
            {
                model = MapModel.Build(dungeons, key);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[MimesisMap] Map build failed: " + e);
                _nextDungeonScan = Time.unscaledTime + 10f;
                return;
            }
            if (model == null) return;
            Install(model, main);
        }

        private static void Install(MapModel model, GameMainBase main)
        {
            _model = model;
            _model.Owner = main;
            _modelBuiltAt = Time.unscaledTime;
            var key = model.Key;
            if (Explored.TryGetValue(key, out var set))
                foreach (int i in set) _model.Reveal(i);

            EnsureCanvas();
            _mini.Bind(_model);
            _big.Bind(_model);
        }

        private static void Teardown()
        {
            _mini?.Bind(null);
            _big?.Bind(null);
            _mini?.SetVisible(false);
            _big?.SetVisible(false);
            _model?.Destroy();
            _model = null;
            _bigOpen = _bigOpen && Cfg.BigMapKeyMode.Value == KeyMode.Hold;
            InputBlock.Release("bigmap");
            GameAccess.Clear();
        }

        private static void EnsureCanvas()
        {
            if (_canvasGo != null) return;
            _canvasGo = new GameObject("MimesisMapCanvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            _canvasRect = (RectTransform)_canvasGo.transform;
            _mini = new MapView(true);
            _mini.Create(_canvasGo.transform);
            _big = new MapView(false);
            _big.Create(_canvasGo.transform);
        }

        public static void LateUpdate()
        {
            if (_model == null || _canvasGo == null) return;

            GameAccess.Scan();
            var focus = GameAccess.Focus();
            if (focus == null)
            {
                Diagnose("no local player yet");
                _mini.SetVisible(false);
                _big.SetVisible(false);
                return;
            }

            // ---- build frame context
            var ctx = Ctx;
            ctx.FocusPos = focus.transform.position;
            ctx.Yaw = GameAccess.Yaw(focus);
            ctx.Indoor = _model.IsIndoor(ctx.FocusPos + Vector3.up * 0.3f, out ctx.CurTile);
            ctx.Mates.Clear();
            foreach (var p in GameAccess.Players)
            {
                if (p == null || p.dead || p == focus) continue;
                var pos = p.transform.position;
                ctx.Mates.Add(new ActorDot { Pos = pos, Yaw = p.transform.eulerAngles.y, Name = SafeName(p), Indoor = _model.IsIndoor(pos + Vector3.up * 0.3f, out _) });
            }
            if (focus != GameAccess.LocalPlayer && GameAccess.LocalPlayer != null && !GameAccess.LocalPlayer.dead)
            {
                var me = GameAccess.LocalPlayer;
                ctx.Mates.Add(new ActorDot { Pos = me.transform.position, Yaw = me.transform.eulerAngles.y, Name = SafeName(me), Indoor = _model.IsIndoor(me.transform.position, out _) });
            }
            ctx.Monsters.Clear();
            if (Cfg.ShowMonsters.Value)
                foreach (var mo in GameAccess.Monsters)
                    if (mo != null && !mo.dead)
                    {
                        var pos = mo.transform.position;
                        ctx.Monsters.Add((pos, _model.IsIndoor(pos + Vector3.up * 0.3f, out _)));
                    }
            ctx.Loot.Clear();
            ctx.MaxPrice = 1;
            if (Cfg.ShowLoot.Value)
            {
                foreach (var l in GameAccess.Loot)
                {
                    if (l == null) continue;
                    var pos = l.transform.position;
                    if (NearPlayer(pos, ctx)) continue; // held in hand: only the player's marker shows
                    bool ind = _model.IsIndoor(pos + Vector3.up * 0.2f, out int tile);
                    int price = Mathf.Max(0, l.marketPrice);
                    if (price > ctx.MaxPrice) ctx.MaxPrice = price;
                    ctx.Loot.Add((pos, price, ind, tile));
                }
            }

            // ---- reveal
            if (Time.unscaledTime >= _nextReveal)
            {
                _nextReveal = Time.unscaledTime + 0.15f;
                bool changed = RevealAt(ctx.FocusPos);
                if (GameAccess.LocalPlayer != null && GameAccess.LocalPlayer != focus && !GameAccess.LocalPlayer.dead)
                    changed |= RevealAt(GameAccess.LocalPlayer.transform.position);
                if (Cfg.RevealByTeammates.Value)
                    foreach (var p in GameAccess.Players)
                        if (p != null && !p.dead) changed |= RevealAt(p.transform.position);
                if (changed) Sharing.MarkDirty();
            }
            Sharing.Tick(_model);

            // ---- doors & path to the nearest exit
            if (Time.unscaledTime >= _nextExitScan)
            {
                _nextExitScan = Time.unscaledTime + 3f;
                _model.RefreshExits();
            }
            if (Time.unscaledTime >= _nextPath)
            {
                _nextPath = Time.unscaledTime + 0.4f;
                if (Cfg.ShowExit.Value && Cfg.ShowExitPath.Value && ctx.Indoor)
                    ExitPath.Update(_model, ctx.FocusPos, ctx.CurTile, Cfg.Reveal.Value == RevealMode.Full);
                else { ExitPath.Path.Clear(); ExitPath.TargetExit = -1; }
            }

            // ---- views
            if (GameAccess.MenuOpen())
            {
                _mini.SetVisible(false);
                _big.SetVisible(false);
                Diagnose("menu open");
                return;
            }
            Diagnose(null);
            bool showBig = _bigOpen;
            _big.SetVisible(showBig);
            _mini.SetVisible(!showBig && Cfg.MiniEnabled.Value);
            Vector2 cs = _canvasRect != null ? _canvasRect.rect.size : new Vector2(1920, 1080);
            _mini.Tick(ctx, cs);
            _big.Tick(ctx, cs);
        }

        private static bool NearPlayer(Vector3 p, FrameCtx ctx)
        {
            if (Near(p, ctx.FocusPos)) return true;
            foreach (var m in ctx.Mates) if (Near(p, m.Pos)) return true;
            return false;
        }

        private static bool Near(Vector3 a, Vector3 b) =>
            Mathf.Abs(a.y - b.y) < 2.2f && new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude < 1f;

        private static string _lastDiag;
        /// <summary>Logs state changes of why the map is (not) shown, to debug "I don't see it".</summary>
        private static void Diagnose(string why)
        {
            if (why == _lastDiag) return;
            _lastDiag = why;
            MelonLogger.Msg(why == null ? "[MimesisMap] Map shown" : "[MimesisMap] Map hidden: " + why);
        }

        private static bool RevealAt(Vector3 pos)
        {
            int t = _model.FindTile(pos + Vector3.up * 0.3f);
            if (t < 0 || !_model.Reveal(t)) return false;
            Remember(_model.Key, t);
            return true;
        }

        private static void Remember(string key, int idx)
        {
            if (!Explored.TryGetValue(key, out var set)) Explored[key] = set = new HashSet<int>();
            set.Add(idx);
        }

        private static void OnShared(string key, List<int> ids)
        {
            foreach (int i in ids) Remember(key, i);
            if (_model == null || _model.Key != key) return;
            foreach (int i in ids) _model.Reveal(i);
        }

        private static string SafeName(Mimic.Actors.ProtoActor p)
        {
            try { return p.nickName; } catch { return null; }
        }
    }

    [HarmonyPatch(typeof(NavManager), nameof(NavManager.Build))]
    internal static class NavBuildPatch
    {
        public static bool Ok;
        private static bool Prepare() { Ok = true; return true; }
        private static void Postfix() => MapController.OnNavMeshBuilt();
    }
}
