using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using Steamworks;
using UnityEngine;

namespace MimesisMap
{
    /// <summary>
    /// Exchanges explored room indices between clients that run this mod, using Steam lobby chat messages.
    /// The host does not need the mod: vanilla clients simply ignore lobby chat.
    /// </summary>
    internal static class Sharing
    {
        private const string Prefix = "\u0001MMAP1|";
        private static Callback<LobbyChatMsg_t> _cb;
        private static bool _initFailed;
        private static float _nextSend, _nextPeriodic;
        private static bool _dirty;
        private static readonly byte[] Buf = new byte[4096];

        public static event Action<string, List<int>> Received;

        public static void EnsureInit()
        {
            if (_cb != null || _initFailed) return;
            try
            {
                _cb = Callback<LobbyChatMsg_t>.Create(OnChat);
            }
            catch (Exception e)
            {
                _initFailed = true;
                MelonLogger.Warning("[MimesisMap] Steam sharing unavailable: " + e.Message);
            }
        }

        public static void MarkDirty() => _dirty = true;

        public static void Tick(MapModel model)
        {
            if (model == null || !Cfg.ShareSteam.Value) return;
            EnsureInit();
            float now = Time.unscaledTime;
            bool periodic = now >= _nextPeriodic;
            if ((!_dirty || now < _nextSend) && !periodic) return;
            if (model.RevealedCount == 0) { _nextPeriodic = now + 5f; return; }
            _dirty = false;
            _nextSend = now + 1.5f;
            _nextPeriodic = now + 25f;
            Send(model);
        }

        private static void Send(MapModel model)
        {
            try
            {
                var lobby = GameAccess.LobbyId();
                if (!lobby.IsValid() || lobby == CSteamID.Nil) return;
                var sb = new StringBuilder(Prefix);
                sb.Append(model.Key).Append('|');
                bool first = true;
                foreach (var t in model.Tiles)
                {
                    if (!t.Revealed) continue;
                    if (!first) sb.Append(',');
                    sb.Append(t.Index);
                    first = false;
                }
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                if (bytes.Length > 4000) return;
                SteamMatchmaking.SendLobbyChatMsg(lobby, bytes, bytes.Length);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[MimesisMap] share send failed: " + e.Message);
            }
        }

        private static void OnChat(LobbyChatMsg_t msg)
        {
            try
            {
                if (!Cfg.ShareSteam.Value) return;
                var lobby = new CSteamID(msg.m_ulSteamIDLobby);
                int len = SteamMatchmaking.GetLobbyChatEntry(lobby, (int)msg.m_iChatID, out CSteamID user, Buf, Buf.Length, out EChatEntryType _);
                if (len <= Prefix.Length) return;
                if (user == SteamUser.GetSteamID()) return;
                string s = Encoding.UTF8.GetString(Buf, 0, len).TrimEnd('\0');
                if (!s.StartsWith(Prefix, StringComparison.Ordinal)) return;
                var parts = s.Substring(Prefix.Length).Split('|');
                if (parts.Length < 2) return;
                var ids = new List<int>();
                foreach (var p in parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(p, out int id)) ids.Add(id);
                Received?.Invoke(parts[0], ids);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[MimesisMap] share receive failed: " + e.Message);
            }
        }
    }
}
