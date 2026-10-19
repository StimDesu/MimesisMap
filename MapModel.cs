using System;
using System.Collections.Generic;
using System.Diagnostics;
using DunGen;
using MelonLoader;
using Pathfinding;
using UnityEngine;
using UnityEngine.AI;

namespace MimesisMap
{
    internal sealed class TileInfo
    {
        public int Index;
        public Tile Tile;
        public Bounds Bounds;      // world AABB used for point lookup
        public Rect Rect;          // xz rect covered by the texture (metres)
        public Texture2D Tex;
        public Texture2D Fog;      // solid silhouette, used as fog over unexplored rooms in Graphic style
        public float FloorY, MinY, MaxY;
        public bool IsStairs;
        public Vector3 StairsAt;   // where to draw the stairs icon (middle of the sloped part)
        public bool IsStart;
        public bool Revealed;
        public int[] Neighbors = Array.Empty<int>();
        public string Name;
        public readonly List<Vector3> Doors = new List<Vector3>();   // used doorway positions (path finding fallback)
    }

    internal struct ExitInfo
    {
        public Vector3 Pos;
        public bool ToIndoor;   // outdoor door leading into the building
        public int Tile;        // tile containing an indoor exit, or -1
    }

    /// <summary>Schematic map of one generated dungeon, built from the NavMesh split by DunGen tiles.</summary>
    internal sealed class MapModel
    {
        public string Key;
        public readonly List<Dungeon> Dungeons = new List<Dungeon>();
        public bool DungeonGone { get { foreach (var d in Dungeons) if (d == null) return true; return false; } }
        public readonly List<ExitInfo> Exits = new List<ExitInfo>();
        public string Source;
        public UnityEngine.Object Owner;   // scene controller the map belongs to
        public bool Flat => Tiles.Count == 0;
        public readonly List<TileInfo> Tiles = new List<TileInfo>();
        public Bounds DungeonBounds;
        public Texture2D OutdoorTex;
        public Rect OutdoorRect;
        public int RevealedCount;
        public int StartTile = -1;

        private int _lastLookup = -1;

        public void Destroy()
        {
            foreach (var t in Tiles)
            {
                if (t.Tex != null) UnityEngine.Object.Destroy(t.Tex);
                if (t.Fog != null) UnityEngine.Object.Destroy(t.Fog);
            }
            if (OutdoorTex != null) UnityEngine.Object.Destroy(OutdoorTex);
            Tiles.Clear();
        }

        /// <summary>Index of the tile containing the point or -1.</summary>
        public int FindTile(Vector3 p)
        {
            if (_lastLookup >= 0 && _lastLookup < Tiles.Count && Tiles[_lastLookup].Bounds.Contains(p))
            {
                // still verify there is no smaller tile containing the point
                int best = Smallest(p);
                _lastLookup = best;
                return best;
            }
            _lastLookup = Smallest(p);
            return _lastLookup;
        }

        private int Smallest(Vector3 p)
        {
            int best = -1;
            float bestVol = float.MaxValue;
            for (int i = 0; i < Tiles.Count; i++)
            {
                var b = Tiles[i].Bounds;
                if (!b.Contains(p)) continue;
                var s = b.size;
                float v = s.x * s.y * s.z;
                if (v < bestVol) { bestVol = v; best = i; }
            }
            return best;
        }

        /// <summary>Tile containing the point, or the closest one within maxDist metres (doors sit on tile edges).</summary>
        public int NearestTile(Vector3 p, float maxDist)
        {
            int t = FindTile(p);
            if (t >= 0) return t;
            float best = maxDist * maxDist;
            for (int i = 0; i < Tiles.Count; i++)
            {
                float d = Tiles[i].Bounds.SqrDistance(p);
                if (d < best) { best = d; t = i; }
            }
            return t;
        }

        /// <summary>True when the point is inside the dungeon (in a tile or in its overall bounds).</summary>
        public bool IsIndoor(Vector3 p, out int tile)
        {
            tile = FindTile(p);
            if (tile >= 0) return true;
            var b = DungeonBounds;
            b.Expand(new Vector3(2f, 2f, 2f));
            return Tiles.Count > 0 && b.Contains(p);
        }

        public bool Reveal(int idx)
        {
            if (idx < 0 || idx >= Tiles.Count || Tiles[idx].Revealed) return false;
            Tiles[idx].Revealed = true;
            RevealedCount++;
            return true;
        }

        public bool IsHint(int idx)
        {
            var t = Tiles[idx];
            if (t.Revealed) return false;
            foreach (var n in t.Neighbors)
                if (Tiles[n].Revealed) return true;
            return false;
        }

        // ------------------------------------------------------------------ building

        private const float Pad = 1f;
        private const float AgentRadiusFill = 0.35f;

        public static Bounds BoundsOf(List<Dungeon> dungeons)
        {
            bool first = true;
            var res = new Bounds();
            foreach (var d in dungeons)
                foreach (var t in d.AllTiles)
                {
                    if (t == null) continue;
                    Bounds b;
                    try { b = t.Bounds; } catch { continue; }
                    if (first) { res = b; first = false; } else res.Encapsulate(b);
                }
            return res;
        }

        public static MapModel Build(List<Dungeon> dungeons, string key)
        {
            var sw = Stopwatch.StartNew();
            var model = new MapModel { Key = key };
            model.Dungeons.AddRange(dungeons);
            var tiles = new List<Tile>();
            foreach (var d in dungeons) foreach (var t in d.AllTiles) if (t != null) tiles.Add(t);
            if (tiles.Count == 0) return null;

            var (v, ind, src) = FloorTriangles(BoundsOf(dungeons));
            model.Source = src;
            int triCount = ind.Length / 3;

            float ppm = Mathf.Clamp(Cfg.Resolution.Value, 2f, 10f);
            var tileIndex = new Dictionary<Tile, int>();

            bool first = true;
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var ti = new TileInfo { Index = i, Tile = tile, Name = tile != null ? tile.name : "?" };
                Bounds b;
                try { b = tile.Bounds; } catch { b = new Bounds(tile.transform.position, Vector3.one * 4f); }
                if (first) { model.DungeonBounds = b; first = false; } else model.DungeonBounds.Encapsulate(b);
                var lookup = b;
                lookup.Expand(new Vector3(0.1f, 1.5f, 0.1f));
                ti.Bounds = lookup;
                ti.FloorY = b.min.y;
                ti.MinY = b.min.y;
                ti.MaxY = b.min.y + 1f;
                model.Tiles.Add(ti);
                if (tile != null) tileIndex[tile] = i;
            }

            foreach (var dungeon in dungeons)
            {
                try
                {
                    var main = dungeon.MainPathTiles;
                    if (main != null && main.Count > 0 && main[0] != null && tileIndex.TryGetValue(main[0], out int s))
                    {
                        if (model.StartTile < 0) model.StartTile = s;
                        model.Tiles[s].IsStart = true;
                    }
                }
                catch { }
            }

            // neighbours via used doorways
            for (int i = 0; i < model.Tiles.Count; i++)
            {
                var tile = model.Tiles[i].Tile;
                if (tile == null || tile.UsedDoorways == null) continue;
                var set = new HashSet<int>();
                foreach (var d in tile.UsedDoorways)
                {
                    try
                    {
                        if (d != null) model.Tiles[i].Doors.Add(d.transform.position);
                        var cd = d != null ? d.ConnectedDoorway : null;
                        if (cd != null && cd.Tile != null && tileIndex.TryGetValue(cd.Tile, out int n) && n != i) set.Add(n);
                    }
                    catch { }
                }
                model.Tiles[i].Neighbors = new List<int>(set).ToArray();
            }

            // assign triangles to tiles
            int[] owner = new int[triCount];
            var centroids = new Vector3[triCount];
            var ownLists = new List<int>[model.Tiles.Count];
            var foreignLists = new List<int>[model.Tiles.Count];
            for (int i = 0; i < model.Tiles.Count; i++) { ownLists[i] = new List<int>(); foreignLists[i] = new List<int>(); }
            var outdoor = new List<int>();
            var dungeonOuter = model.DungeonBounds;
            dungeonOuter.Expand(new Vector3(4f, 4f, 4f));

            for (int t = 0; t < triCount; t++)
            {
                Vector3 c = (v[ind[t * 3]] + v[ind[t * 3 + 1]] + v[ind[t * 3 + 2]]) / 3f;
                centroids[t] = c;
                owner[t] = model.Smallest(c);
                if (owner[t] >= 0) ownLists[owner[t]].Add(t);
                else if (!dungeonOuter.Contains(c)) outdoor.Add(t);
            }

            // foreign triangles near each tile (to keep doorway openings free of outlines)
            for (int i = 0; i < model.Tiles.Count; i++)
            {
                var b = model.Tiles[i].Bounds;
                float minX = b.min.x - Pad, maxX = b.max.x + Pad, minZ = b.min.z - Pad, maxZ = b.max.z + Pad;
                float minY = b.min.y - 1f, maxY = b.max.y + 1f;
                for (int t = 0; t < triCount; t++)
                {
                    if (owner[t] == i) continue;
                    var c = centroids[t];
                    if (c.x < minX - 3f || c.x > maxX + 3f || c.z < minZ - 3f || c.z > maxZ + 3f || c.y < minY || c.y > maxY) continue;
                    foreignLists[i].Add(t);
                }
            }

            for (int i = 0; i < model.Tiles.Count; i++)
            {
                var ti = model.Tiles[i];
                var own = ownLists[i];
                if (own.Count > 0)
                {
                    float sumY = 0f, lo = float.MaxValue, hi = float.MinValue;
                    foreach (int t in own)
                    {
                        float y = centroids[t].y;
                        sumY += y;
                        if (y < lo) lo = y;
                        if (y > hi) hi = y;
                    }
                    ti.FloorY = sumY / own.Count;
                    ti.MinY = lo;
                    ti.MaxY = hi;
                    // a room whose walkable floor climbs more than a storey-ish step is a staircase
                    // a staircase connects floors: its doorways sit at different heights.
                    // (a pit or a raised platform also has a height difference, but all its doors are on one level)
                    float dLo = float.MaxValue, dHi = float.MinValue;
                    foreach (var d in ti.Doors) { if (d.y < dLo) dLo = d.y; if (d.y > dHi) dHi = d.y; }
                    ti.IsStairs = (ti.Doors.Count >= 2 && dHi - dLo > 1.8f) || (ti.Name != null && ti.Name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (ti.IsStairs) ti.StairsAt = StairsCenter(own, centroids, lo, hi);
                }
                var b = ti.Bounds;
                var rect = new Rect(b.min.x - Pad, b.min.z - Pad, b.size.x + Pad * 2f, b.size.z + Pad * 2f);
                ti.Rect = rect;
                ti.Tex = Rasterize(rect, ppm, 1024, v, ind, own, foreignLists[i], out ti.Fog);
            }

            if (outdoor.Count > 0)
            {
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                foreach (int t in outdoor)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var p = v[ind[t * 3 + k]];
                        if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                        if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
                    }
                }
                var rect = new Rect(minX - Pad, minZ - Pad, maxX - minX + Pad * 2f, maxZ - minZ + Pad * 2f);
                float oppm = Mathf.Min(ppm, 2048f / Mathf.Max(rect.width, rect.height));
                model.OutdoorRect = rect;
                model.OutdoorTex = Rasterize(rect, oppm, 2048, v, ind, outdoor, null, out _);
            }

            model.RefreshExits();
            MelonLogger.Msg($"[MimesisMap] Map built: {dungeons.Count} building(s), {model.Tiles.Count} rooms, {model.Exits.Count} doors, {triCount} triangles from {src} ({outdoor.Count} outdoor) in {sw.ElapsedMilliseconds} ms");
            return model;
        }

        /// <summary>Map of a scene without a generated dungeon (tram, lobby): just the walkable floor.</summary>
        public static MapModel BuildFlat(string key)
        {
            var (v, ind, source) = FloorTriangles();
            if (ind.Length < 3) return null;
            var model = new MapModel { Key = key };
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            var all = new List<int>(ind.Length / 3);
            for (int t = 0; t < ind.Length / 3; t++)
            {
                all.Add(t);
                for (int k = 0; k < 3; k++)
                {
                    var p = v[ind[t * 3 + k]];
                    if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
                }
            }
            var rect = new Rect(minX - Pad, minZ - Pad, maxX - minX + Pad * 2f, maxZ - minZ + Pad * 2f);
            float ppm = Mathf.Min(Mathf.Clamp(Cfg.Resolution.Value, 2f, 10f), 2048f / Mathf.Max(rect.width, rect.height));
            model.OutdoorRect = rect;
            model.OutdoorTex = Rasterize(rect, ppm, 2048, v, ind, all, null, out _);
            MelonLogger.Msg($"[MimesisMap] Flat map built from {source}: {all.Count} triangles, {rect.width:0}x{rect.height:0} m");
            return model;
        }

        /// <summary>Teleporter doors: exits inside the building and entrances outside. Level objects can appear late, so this is re-run.</summary>
        public void RefreshExits()
        {
            Exits.Clear();
            try
            {
                foreach (var t in UnityEngine.Object.FindObjectsByType<TeleporterLevelObject>(FindObjectsSortMode.None))
                {
                    if (t == null) continue;
                    var pos = t.transform.position;
                    bool toIndoor = t.DestinationIsToInDoor;
                    Exits.Add(new ExitInfo { Pos = pos, ToIndoor = toIndoor, Tile = toIndoor ? -1 : NearestTile(pos + Vector3.up * 0.3f, 3f) });
                }
            }
            catch (Exception e) { MelonLogger.Warning("[MimesisMap] door scan failed: " + e.Message); }
        }

        /// <summary>Number of triangles whose centre lies inside the bounds.</summary>
        public static int CountInside(Vector3[] v, int[] ind, Bounds b)
        {
            b.Expand(new Vector3(1f, 3f, 1f));
            int n = 0;
            for (int t = 0; t + 2 < ind.Length; t += 3)
                if (b.Contains((v[ind[t]] + v[ind[t + 1]] + v[ind[t + 2]]) / 3f)) n++;
            return n;
        }

        /// <summary>
        /// Walkable floor: the Unity NavMesh or the A* recast graph (tram/lobby have no NavMesh).
        /// With bounds, the source covering them better wins.
        /// </summary>
        public static (Vector3[] v, int[] ind, string source) FloorTriangles(Bounds? cover = null)
        {
            var tri = NavMesh.CalculateTriangulation();
            bool hasNav = tri.vertices != null && tri.vertices.Length > 0 && tri.indices != null && tri.indices.Length >= 3;
            if (hasNav && cover == null) return (tri.vertices, tri.indices, "NavMesh");
            var astar = AStarTriangles();
            if (!hasNav) return astar;
            int navIn = CountInside(tri.vertices, tri.indices, cover.Value);
            int aIn = CountInside(astar.v, astar.ind, cover.Value);
            return aIn > navIn * 2 ? astar : (tri.vertices, tri.indices, "NavMesh");
        }

        private static (Vector3[] v, int[] ind, string source) AStarTriangles()
        {
            var verts = new List<Vector3>();
            try
            {
                var data = AstarPath.active != null ? AstarPath.active.data : null;
                if (data?.graphs != null)
                    foreach (var graph in data.graphs)
                        graph?.GetNodes(node =>
                        {
                            if (node is TriangleMeshNode t && t.Walkable)
                                for (int i = 0; i < 3; i++) verts.Add((Vector3)t.GetVertex(i));
                        });
            }
            catch (Exception e) { MelonLogger.Warning("[MimesisMap] A* graph read failed: " + e.Message); }
            var idx = new int[verts.Count - verts.Count % 3];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            return (verts.ToArray(), idx, "A* graph");
        }

        private static Texture2D Rasterize(Rect rect, float ppm, int maxSize, Vector3[] v, int[] ind, List<int> own, List<int> foreign, out Texture2D fog)
        {
            ppm = Mathf.Min(ppm, maxSize / Mathf.Max(1f, Mathf.Max(rect.width, rect.height)));
            int w = Mathf.Clamp(Mathf.CeilToInt(rect.width * ppm), 2, maxSize);
            int h = Mathf.Clamp(Mathf.CeilToInt(rect.height * ppm), 2, maxSize);
            var mOwn = new bool[w * h];
            var mOther = new bool[w * h];
            foreach (int t in own) Tri(mOwn, w, h, rect, ppm, v[ind[t * 3]], v[ind[t * 3 + 1]], v[ind[t * 3 + 2]]);
            if (foreign != null)
                foreach (int t in foreign) Tri(mOther, w, h, rect, ppm, v[ind[t * 3]], v[ind[t * 3 + 1]], v[ind[t * 3 + 2]]);

            int r = Mathf.Max(1, Mathf.RoundToInt(AgentRadiusFill * ppm));
            mOwn = Dilate(mOwn, w, h, r);
            if (foreign != null) mOther = Dilate(mOther, w, h, r);

            var px = new Color32[w * h];
            var empty = new Color32(255, 255, 255, 0);
            var fill = new Color32(255, 255, 255, 105);
            var edge = new Color32(255, 255, 255, 255);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!mOwn[i]) { px[i] = empty; continue; }
                    bool border = false;
                    for (int dy = -1; dy <= 1 && !border; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue; // tile edge: not a wall
                            int j = ny * w + nx;
                            if (!mOwn[j] && !mOther[j]) { border = true; break; }
                        }
                    }
                    px[i] = border ? edge : fill;
                }
            }

            var tex = MakeTex(w, h, px);
            fog = null;
            if (foreign != null)
            {
                // fog silhouette: slightly larger than the room so the walls are covered too
                var solid = Dilate(mOwn, w, h, Mathf.Max(1, Mathf.RoundToInt(0.4f * ppm)));
                var fpx = new Color32[w * h];
                for (int i = 0; i < fpx.Length; i++) fpx[i] = solid[i] ? edge : empty;
                fog = MakeTex(w, h, fpx);
            }
            return tex;
        }

        private static Vector3 StairsCenter(List<int> own, Vector3[] centroids, float lo, float hi)
        {
            // average of triangles in the middle height band = the flight of stairs itself
            float a = lo + (hi - lo) * 0.25f, b = lo + (hi - lo) * 0.75f;
            var sum = Vector3.zero; int n = 0;
            foreach (int t in own) { var c = centroids[t]; if (c.y >= a && c.y <= b) { sum += c; n++; } }
            if (n == 0) foreach (int t in own) { sum += centroids[t]; n++; }
            return sum / Mathf.Max(1, n);
        }

        /// <summary>Is the height on this tile's floor range (with a margin)?</summary>
        public static bool SameFloor(TileInfo t, float y) => y >= t.MinY - 2f && y <= t.MaxY + 2f;

        private static Texture2D MakeTex(int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "MimesisMapTile"
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static void Tri(bool[] m, int w, int h, Rect rect, float ppm, Vector3 a3, Vector3 b3, Vector3 c3)
        {
            var a = new Vector2((a3.x - rect.xMin) * ppm, (a3.z - rect.yMin) * ppm);
            var b = new Vector2((b3.x - rect.xMin) * ppm, (b3.z - rect.yMin) * ppm);
            var c = new Vector2((c3.x - rect.xMin) * ppm, (c3.z - rect.yMin) * ppm);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float area = Edge(a, b, c);
            if (Mathf.Abs(area) < 1e-6f)
            {
                return;
            }
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Edge(b, c, p), w1 = Edge(c, a, p), w2 = Edge(a, b, p);
                    if (area > 0 ? (w0 >= 0 && w1 >= 0 && w2 >= 0) : (w0 <= 0 && w1 <= 0 && w2 <= 0))
                        m[y * w + x] = true;
                }
            }
            // make sure tiny/thin triangles still leave a mark
            int cx = Mathf.Clamp((int)((a.x + b.x + c.x) / 3f), 0, w - 1);
            int cy = Mathf.Clamp((int)((a.y + b.y + c.y) / 3f), 0, h - 1);
            m[cy * w + cx] = true;
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static bool[] Dilate(bool[] m, int w, int h, int r)
        {
            // separable square dilation, then trim corners by a circular check is unnecessary at small radii
            var tmp = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                int last = -1000000;
                for (int x = 0; x < w; x++) if (m[row + x]) last = x; else if (x - last <= r) tmp[row + x] = true;
                last = 1000000;
                for (int x = w - 1; x >= 0; x--) { if (m[row + x]) { last = x; tmp[row + x] = true; } else if (last - x <= r) tmp[row + x] = true; }
            }
            var res = new bool[w * h];
            for (int x = 0; x < w; x++)
            {
                int last = -1000000;
                for (int y = 0; y < h; y++) if (tmp[y * w + x]) last = y; else if (y - last <= r) res[y * w + x] = true;
                last = 1000000;
                for (int y = h - 1; y >= 0; y--) { if (tmp[y * w + x]) { last = y; res[y * w + x] = true; } else if (last - y <= r) res[y * w + x] = true; }
            }
            return res;
        }
    }
}
