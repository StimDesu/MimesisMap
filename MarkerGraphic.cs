using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MimesisMap
{
    internal enum Shape { Arrow, Dot, Diamond, Square, Ring, Stairs }

    internal struct Marker
    {
        public Vector2 Pos;
        public float Rot;   // degrees, counter-clockwise in UI space; 0 = pointing up
        public float Size;
        public Color Color;
        public Shape Shape;
    }

    /// <summary>Single graphic drawing all dynamic markers of a map view (one draw call).</summary>
    internal sealed class MarkerGraphic : MaskableGraphic
    {
        public readonly List<Marker> Markers = new List<Marker>();
        public readonly List<Vector2> Path = new List<Vector2>();
        public float PathWidth = 3.5f;
        public Color PathColor = new Color(0.35f, 1f, 0.45f, 0.9f);
        private static readonly Color Outline = new Color(0f, 0f, 0f, 0.85f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Path.Count > 1)
            {
                var glow = PathColor; glow.a = 0.16f;
                Line(vh, PathWidth + 9f, glow);
                Line(vh, PathWidth + 2.5f, new Color(0f, 0f, 0f, 0.6f));
                var core = PathColor; core.a = 0.75f;
                Line(vh, PathWidth, core);
                Chevrons(vh);
            }
            foreach (var m in Markers)
            {
                Emit(vh, m, m.Size + 2.5f, Outline);
                Emit(vh, m, m.Size, m.Color);
            }
        }

        private void Line(VertexHelper vh, float w, Color c)
        {
            float h = w * 0.5f;
            for (int k = 0; k + 1 < Path.Count; k++)
            {
                var a = Path[k]; var b = Path[k + 1];
                var d = b - a;
                if (d.sqrMagnitude < 0.01f) continue;
                var n = new Vector2(-d.y, d.x).normalized * h;
                int i = vh.currentVertCount;
                Add(vh, a + n, c); Add(vh, b + n, c); Add(vh, b - n, c); Add(vh, a - n, c);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i, i + 2, i + 3);
            }
            for (int k = 1; k + 1 < Path.Count; k++) Circle(vh, Path[k], h, c);
        }

        /// <summary>Small arrows sliding along the path towards its end.</summary>
        private void Chevrons(VertexHelper vh)
        {
            const float spacing = 26f;
            float phase = (Time.unscaledTime * 32f) % spacing;
            float s = PathWidth * 1.35f + 1.5f;
            var col = new Color(0.85f, 1f, 0.88f, 0.95f);
            float next = phase;
            float walked = 0f;
            for (int k = 0; k + 1 < Path.Count; k++)
            {
                var a = Path[k]; var b = Path[k + 1];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                float rot = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg - 90f;
                var q = Quaternion.Euler(0, 0, rot);
                while (next <= walked + len)
                {
                    var p = Vector2.Lerp(a, b, (next - walked) / len);
                    Poly(vh, p, q, col, new Vector2(0, s), new Vector2(s * 0.9f, -s * 0.2f), new Vector2(0, s * 0.25f), new Vector2(-s * 0.9f, -s * 0.2f));
                    next += spacing;
                }
                walked += len;
            }
        }

        /// <summary>Chaikin corner cutting: turns a polyline into a smooth curve.</summary>
        public static void Smooth(List<Vector2> pts, int iterations)
        {
            var tmp = new List<Vector2>(pts.Count * 2);
            for (int it = 0; it < iterations && pts.Count > 2; it++)
            {
                tmp.Clear();
                tmp.Add(pts[0]);
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    var a = pts[i]; var b = pts[i + 1];
                    if (i > 0) tmp.Add(Vector2.Lerp(a, b, 0.25f));
                    if (i + 2 < pts.Count) tmp.Add(Vector2.Lerp(a, b, 0.75f));
                }
                tmp.Add(pts[pts.Count - 1]);
                pts.Clear();
                pts.AddRange(tmp);
            }
        }

        private static void Emit(VertexHelper vh, Marker m, float s, Color c)
        {
            var q = Quaternion.Euler(0, 0, m.Rot);
            switch (m.Shape)
            {
                case Shape.Arrow:
                    Poly(vh, m.Pos, q, c, new Vector2(0, s * 1.15f), new Vector2(s * 0.8f, -s * 0.8f), new Vector2(0, -s * 0.35f), new Vector2(-s * 0.8f, -s * 0.8f));
                    break;
                case Shape.Diamond:
                    Poly(vh, m.Pos, q, c, new Vector2(0, s), new Vector2(s * 0.75f, 0), new Vector2(0, -s), new Vector2(-s * 0.75f, 0));
                    break;
                case Shape.Square:
                    Poly(vh, m.Pos, Quaternion.identity, c, new Vector2(-s, s), new Vector2(s, s), new Vector2(s, -s), new Vector2(-s, -s));
                    break;
                case Shape.Stairs:
                    // three steps rising to the right
                    for (int k = 0; k < 3; k++)
                    {
                        float x0 = -s + k * (s * 2f / 3f), x1 = x0 + s * 2f / 3f;
                        float y0 = -s, y1 = -s + (k + 1) * (s * 2f / 3f);
                        Poly(vh, m.Pos, Quaternion.identity, c, new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), new Vector2(x0, y0));
                    }
                    break;
                case Shape.Ring:
                    Ring(vh, m.Pos, s, s * 0.6f, c);
                    break;
                default:
                    Circle(vh, m.Pos, s, c);
                    break;
            }
        }

        // convex-ish fan around first vertex is wrong for the arrow notch, so emit it as two triangles explicitly
        private static void Poly(VertexHelper vh, Vector2 o, Quaternion q, Color c, Vector2 a, Vector2 b, Vector2 d, Vector2 e)
        {
            int i = vh.currentVertCount;
            Add(vh, o + (Vector2)(q * a), c);
            Add(vh, o + (Vector2)(q * b), c);
            Add(vh, o + (Vector2)(q * d), c);
            Add(vh, o + (Vector2)(q * e), c);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void Circle(VertexHelper vh, Vector2 o, float r, Color c)
        {
            const int seg = 14;
            int i0 = vh.currentVertCount;
            Add(vh, o, c);
            for (int k = 0; k <= seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg;
                Add(vh, o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, c);
                if (k > 0) vh.AddTriangle(i0, i0 + k, i0 + k + 1);
            }
        }

        private static void Ring(VertexHelper vh, Vector2 o, float r, float ri, Color c)
        {
            const int seg = 16;
            int i0 = vh.currentVertCount;
            for (int k = 0; k <= seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Add(vh, o + d * r, c);
                Add(vh, o + d * ri, c);
                if (k > 0)
                {
                    int b = i0 + (k - 1) * 2;
                    vh.AddTriangle(b, b + 2, b + 1);
                    vh.AddTriangle(b + 1, b + 2, b + 3);
                }
            }
        }

        private static void Add(VertexHelper vh, Vector2 p, Color c)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vh.AddVert(v);
        }
    }
}
