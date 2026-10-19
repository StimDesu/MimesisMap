using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MimesisMap
{
    /// <summary>
    /// Graphic style: an orthographic camera just above the player's head looking straight down, rendered
    /// manually a few times per second. For the duration of the render only, the level is lit evenly
    /// (dark rooms would come out black) and actors that must not be revealed are switched off.
    /// </summary>
    internal static class GraphicRenderer
    {
        public const float CameraAboveFeet = 2.2f;   // below typical ceilings, so rooms stay visible

        private static Camera _cam;
        private static Light _light;
        private static GameObject _root;
        private static readonly List<Renderer> Hide = new List<Renderer>();
        private static readonly List<Renderer> Hidden = new List<Renderer>();
        private static float _nextScan;
        public static bool Failed { get; private set; }

        public static bool Render(RenderTexture rt, Vector3 feet, float coverMeters, float farClip)
        {
            if (Failed || rt == null) return false;
            try
            {
                Ensure();
                ScanHidden();
                _cam.targetTexture = rt;
                _cam.orthographicSize = coverMeters * 0.5f;
                _cam.farClipPlane = farClip;
                _cam.transform.SetPositionAndRotation(feet + Vector3.up * CameraAboveFeet, Quaternion.Euler(90f, 0f, 0f));

                Hidden.Clear();
                foreach (var r in Hide)
                    if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; Hidden.Add(r); }

                var ambientMode = RenderSettings.ambientMode;
                var ambientLight = RenderSettings.ambientLight;
                var fog = RenderSettings.fog;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
                RenderSettings.fog = false;
                _light.enabled = true;
                try { _cam.Render(); }
                finally
                {
                    _light.enabled = false;
                    RenderSettings.ambientMode = ambientMode;
                    RenderSettings.ambientLight = ambientLight;
                    RenderSettings.fog = fog;
                    foreach (var r in Hidden) if (r != null) r.forceRenderingOff = false;
                    Hidden.Clear();
                }
                return true;
            }
            catch (Exception e)
            {
                Failed = true;
                MelonLogger.Error("[MimesisMap] Graphic style failed, falling back to Plain: " + e);
                return false;
            }
        }

        private static void Ensure()
        {
            if (_cam != null) return;
            _root = new GameObject("MimesisMapGraphic");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _cam = new GameObject("MimesisMapCamera").AddComponent<Camera>();
            _cam.transform.SetParent(_root.transform, false);
            _cam.enabled = false; // rendered manually
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.05f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) _cam.cullingMask &= ~(1 << ui);
            try
            {
                var urp = _cam.GetUniversalAdditionalCameraData();
                urp.renderPostProcessing = false;
                urp.renderShadows = false;
            }
            catch { }

            _light = new GameObject("MimesisMapLight").AddComponent<Light>();
            _light.transform.SetParent(_root.transform, false);
            _light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _light.type = LightType.Directional;
            _light.intensity = 1.1f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
        }

        /// <summary>Your own character is always hidden; others only appear when their markers are enabled.</summary>
        private static void ScanHidden()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1f;
            Hide.Clear();
            try
            {
                if (GameAccess.LocalPlayer != null) Hide.AddRange(GameAccess.LocalPlayer.GetComponentsInChildren<Renderer>(true));
                if (!Cfg.ShowTeammates.Value)
                    foreach (var p in GameAccess.Players) if (p != null) Hide.AddRange(p.GetComponentsInChildren<Renderer>(true));
                if (!Cfg.ShowMonsters.Value)
                    foreach (var m in GameAccess.Monsters) if (m != null) Hide.AddRange(m.GetComponentsInChildren<Renderer>(true));
                if (!Cfg.ShowLoot.Value)
                    foreach (var l in GameAccess.Loot) if (l != null) Hide.AddRange(l.GetComponentsInChildren<Renderer>(true));
            }
            catch { }
        }
    }
}
