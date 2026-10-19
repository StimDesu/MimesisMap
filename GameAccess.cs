using System;
using System.Collections.Generic;
using System.Reflection;
using DunGen;
using HarmonyLib;
using Mimic.Actors;
using ReluProtocol.Enum;
using Steamworks;
using UnityEngine;

namespace MimesisMap
{
    /// <summary>Thin, failure-tolerant access to game state.</summary>
    internal static class GameAccess
    {
        private static readonly PropertyInfo CameraManProp = AccessTools.Property(typeof(Hub), "cameraman");
        private static readonly PropertyInfo UiManProp = AccessTools.Property(typeof(Hub), "uiman");
        private static readonly FieldInfo DispatcherField = AccessTools.Field(typeof(Hub), "steamInviteDispatcher");

        public static readonly List<ProtoActor> Players = new List<ProtoActor>();
        public static readonly List<ProtoActor> Monsters = new List<ProtoActor>();
        public static ProtoActor LocalPlayer;
        public static readonly List<LootingLevelObject> Loot = new List<LootingLevelObject>();

        private static float _nextActorScan, _nextLootScan;

        /// <summary>Current game scene controller (level, tram, lobby...) or null.</summary>
        public static GameMainBase CurrentMain()
        {
            try { return Hub.s != null && Hub.TryGetMain(out var main) ? main : null; }
            catch { return null; }
        }

        /// <summary>The game's Esc menu is open.</summary>
        public static bool MenuOpen()
        {
            try { return Hub.s != null && UiManProp?.GetValue(Hub.s) is UIManager ui && ui != null && ui.isGameMenuOpen; }
            catch { return false; }
        }

        /// <summary>All generated dungeons in the scene (a level may contain more than one), in a stable order.</summary>
        public static List<Dungeon> FindDungeons()
        {
            var res = new List<Dungeon>();
            try
            {
                foreach (var d in UnityEngine.Object.FindObjectsByType<Dungeon>(FindObjectsSortMode.None))
                    if (d != null && d.AllTiles != null && d.AllTiles.Count > 0 && d.AllTiles[0] != null) res.Add(d);
            }
            catch { }
            res.Sort((a, b) =>
            {
                var pa = a.AllTiles[0].transform.position; var pb = b.AllTiles[0].transform.position;
                int c = pa.x.CompareTo(pb.x);
                return c != 0 ? c : pa.z.CompareTo(pb.z);
            });
            return res;
        }

        public static int DungeonSeed()
        {
            try { return CurrentMain() is GamePlayScene g ? g.RandDungeonSeed : 0; } catch { return 0; }
        }

        public static RuntimeDungeon FindRuntimeDungeon()
        {
            try
            {
                foreach (var rd in UnityEngine.Object.FindObjectsByType<RuntimeDungeon>(FindObjectsSortMode.None))
                {
                    var d = rd != null ? rd.Generator?.CurrentDungeon : null;
                    if (d != null && d.AllTiles != null && d.AllTiles.Count > 0) return rd;
                }
            }
            catch { }
            return null;
        }

        public static void Scan(bool force = false)
        {
            float now = Time.unscaledTime;
            if (force || now >= _nextActorScan)
            {
                _nextActorScan = now + 0.4f;
                Players.Clear();
                Monsters.Clear();
                LocalPlayer = null;
                try
                {
                    foreach (var a in UnityEngine.Object.FindObjectsByType<ProtoActor>(FindObjectsSortMode.None))
                    {
                        if (a == null) continue;
                        if (a.ActorType == ActorType.Monster) { if (!a.dead) Monsters.Add(a); continue; }
                        if (a.ActorType != ActorType.Player) continue;
                        if (a.AmIAvatar()) LocalPlayer = a; else Players.Add(a);
                    }
                }
                catch { }
            }
            if (force || now >= _nextLootScan)
            {
                _nextLootScan = now + 0.5f;
                Loot.Clear();
                try
                {
                    foreach (var l in UnityEngine.Object.FindObjectsByType<LootingLevelObject>(FindObjectsSortMode.None))
                    {
                        if (l == null || !l.isActiveAndEnabled) continue;
                        if (l.GetComponentInParent<ProtoActor>() != null) continue; // held by someone
                        Loot.Add(l);
                    }
                }
                catch { }
            }
        }

        public static void Clear()
        {
            Players.Clear();
            Monsters.Clear();
            Loot.Clear();
            LocalPlayer = null;
        }

        /// <summary>Actor the map follows: the local avatar, or the spectated player when dead.</summary>
        public static ProtoActor Focus()
        {
            var me = LocalPlayer;
            if (me != null && !me.dead) return me;
            try
            {
                var cm = CameraManProp?.GetValue(Hub.s) as CameraManager;
                int? id = cm?.SpectatorTargetActorID;
                if (id.HasValue)
                {
                    foreach (var p in Players)
                        if (p != null && p.ActorID == id.Value) return p;
                }
            }
            catch { }
            return me;
        }

        public static float Yaw(ProtoActor focus)
        {
            if (focus == LocalPlayer && focus != null && !focus.dead)
            {
                var cam = Camera.main;
                if (cam != null) return cam.transform.eulerAngles.y;
            }
            return focus != null ? focus.transform.eulerAngles.y : 0f;
        }

        public static CSteamID LobbyId()
        {
            try
            {
                if (Hub.s == null) return CSteamID.Nil;
                var d = DispatcherField?.GetValue(Hub.s) as SteamInviteDispatcher;
                if (d == null) d = UnityEngine.Object.FindAnyObjectByType<SteamInviteDispatcher>();
                return d != null ? d.joinedLobbyID : CSteamID.Nil;
            }
            catch { return CSteamID.Nil; }
        }
    }
}
