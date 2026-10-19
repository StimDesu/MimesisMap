using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.AI;

namespace MimesisMap
{
    /// <summary>
    /// Shortest path from the player to the nearest known building exit.
    /// Uses the game's A* graph (what MIMESIS actually paths on), then the Unity NavMesh, then a graph of doorways.
    /// In Explored mode only explored rooms may be crossed, so the path never reveals unknown layout.
    /// </summary>
    internal static class ExitPath
    {
        public static readonly List<Vector3> Path = new List<Vector3>();
        public static int TargetExit = -1;
        private static NavMeshPath _nav;
        private static readonly List<Vector3> Tmp = new List<Vector3>();

        public static void Update(MapModel model, Vector3 from, int fromTile, bool full)
        {
            Path.Clear();
            TargetExit = -1;
            if (model == null || model.Flat || fromTile < 0) return;

            float best = float.MaxValue;
            bool navOk = NavMesh.SamplePosition(from, out var start, 3f, NavMesh.AllAreas);
            for (int e = 0; e < model.Exits.Count; e++)
            {
                var ex = model.Exits[e];
                if (ex.ToIndoor || !Known(model, ex.Tile, full)) continue;
                float len = AStar(model, from, ex.Pos, full);
                if (len < 0f && navOk) len = NavPath(model, start.position, ex.Pos, full);
                if (len < 0f) len = DoorPath(model, from, fromTile, ex, full);
                if (len < 0f || len >= best) continue;
                best = len;
                TargetExit = e;
                Path.Clear();
                Path.AddRange(Tmp);
            }
        }

        private static Seeker _seeker;
        private static bool _astarBroken;

        /// <summary>Path on the A* recast graph with a funnel modifier, using our own Seeker (never the game's).</summary>
        private static float AStar(MapModel model, Vector3 from, Vector3 to, bool full)
        {
            Tmp.Clear();
            if (_astarBroken || AstarPath.active == null) return -1f;
            try
            {
                if (_seeker == null)
                {
                    var go = new GameObject("MimesisMapSeeker");
                    Object.DontDestroyOnLoad(go);
                    _seeker = go.AddComponent<Seeker>();
                    go.AddComponent<FunnelModifier>();
                }
                var a = AstarPath.active.GetNearest(from).position;
                var b = AstarPath.active.GetNearest(to).position;
                var p = _seeker.StartPath(a, b);
                p.BlockUntilCalculated();
                var vp = p.vectorPath;
                if (p.error || vp == null || vp.Count < 2 || Vector3.Distance(vp[vp.Count - 1], b) > 1.5f) return -1f;
                float len = 0f;
                for (int i = 0; i < vp.Count; i++)
                {
                    if (i > 0)
                    {
                        len += Vector3.Distance(vp[i - 1], vp[i]);
                        if (!full && !SegmentKnown(model, vp[i - 1], vp[i])) { Tmp.Clear(); return -1f; }
                    }
                    Tmp.Add(vp[i]);
                }
                return len;
            }
            catch (System.Exception e)
            {
                _astarBroken = true;
                MelonLoader.MelonLogger.Warning("[MimesisMap] A* path failed, using fallbacks: " + e.Message);
                Tmp.Clear();
                return -1f;
            }
        }

        private static bool Known(MapModel m, int tile, bool full) => full || (tile >= 0 && m.Tiles[tile].Revealed);

        private static float NavPath(MapModel model, Vector3 from, Vector3 to, bool full)
        {
            Tmp.Clear();
            if (!NavMesh.SamplePosition(to, out var end, 3f, NavMesh.AllAreas)) return -1f;
            if (_nav == null) _nav = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, end.position, NavMesh.AllAreas, _nav) || _nav.status != NavMeshPathStatus.PathComplete) return -1f;
            var c = _nav.corners;
            if (c == null || c.Length < 2) return -1f;
            float len = 0f;
            for (int i = 0; i < c.Length; i++)
            {
                if (i > 0)
                {
                    len += Vector3.Distance(c[i - 1], c[i]);
                    if (!full && !SegmentKnown(model, c[i - 1], c[i])) return -1f;
                }
                Tmp.Add(c[i]);
            }
            return len;
        }

        private static bool SegmentKnown(MapModel model, Vector3 a, Vector3 b)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 1.5f));
            for (int s = 0; s <= steps; s++)
            {
                var p = Vector3.Lerp(a, b, s / (float)steps) + Vector3.up * 0.3f;
                int t = model.FindTile(p);
                if (t >= 0 && !model.Tiles[t].Revealed) return false;
            }
            return true;
        }

        // ---- doorway graph fallback (Dijkstra over doorways; rooms are crossed in straight lines)

        private static float DoorPath(MapModel model, Vector3 from, int fromTile, ExitInfo exit, bool full)
        {
            Tmp.Clear();
            var nodes = new List<(Vector3 pos, int tile)> { (from, fromTile), (exit.Pos, exit.Tile) };
            for (int t = 0; t < model.Tiles.Count; t++)
            {
                if (!full && !model.Tiles[t].Revealed) continue;
                foreach (var d in model.Tiles[t].Doors) nodes.Add((d, t));
            }
            int n = nodes.Count;
            var dist = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[0] = 0f;
            for (int iter = 0; iter < n; iter++)
            {
                int u = -1;
                float bu = float.MaxValue;
                for (int i = 0; i < n; i++) if (!done[i] && dist[i] < bu) { bu = dist[i]; u = i; }
                if (u < 0 || u == 1) break;
                done[u] = true;
                for (int w = 0; w < n; w++)
                {
                    if (done[w] || w == u) continue;
                    float cost;
                    float dd = Vector3.Distance(nodes[u].pos, nodes[w].pos);
                    if (nodes[u].tile == nodes[w].tile) cost = dd;          // across the same room
                    else if (dd < 0.75f) cost = dd;                          // the two sides of one doorway
                    else continue;
                    if (dist[u] + cost < dist[w]) { dist[w] = dist[u] + cost; prev[w] = u; }
                }
            }
            if (dist[1] == float.MaxValue) return -1f;
            for (int at = 1; at >= 0; at = prev[at]) Tmp.Add(nodes[at].pos);
            Tmp.Reverse();
            return dist[1];
        }
    }
}
