using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Core.Net;
using Core.Scene;
using GameLogic;
using Tools;
using UI;
using UnityEngine;
using party.protocol;
using XingShu.Bridge;

namespace XingShu.Runtime
{
    /// <summary>
    /// In-game live room controller executed directly inside an active online Home lobby.
    /// Supports create / modify / start / kick / exit via native RoomLogic RPCs and
    /// broadcasts live RUNTIME_ROOM_SNAPSHOT updates to Desktop Hub.
    /// </summary>
    internal static class NativeRoomController
    {
        private sealed class PendingCommand
        {
            public string RequestId;
            public string Action;
            public string RoomName;
            public string RoomPwd;
            public int Mode;
            public int MapId;
            public int Difficulty;
            public int UpgradePlan;
            public int TimePlan;
            public int SpeedType;
            public bool SkipStory;
            public int RoomLabel;
            public bool IsAddBot;
            public long TargetPlayerId;
            public float StartedAt;
        }

        private static readonly object QueueLock = new object();
        private static readonly Queue<PendingCommand> CommandQueue = new Queue<PendingCommand>();

        private static PendingCommand _active;
        private static RPCAsyncResult _rpc;
        private static string _lastSnapshotFingerprint;
        private static float _lastSnapshotAt;

        internal static void Reset()
        {
            lock (QueueLock)
            {
                CommandQueue.Clear();
            }
            _active = null;
            _rpc = null;
            _lastSnapshotFingerprint = null;
            _lastSnapshotAt = 0f;
        }

        internal static void ForceBroadcastSnapshot()
        {
            _lastSnapshotFingerprint = null;
            BroadcastSnapshotIfNeeded(force: true);
        }

        internal static void EnqueueCommand(BridgeEnvelope envelope)
        {
            if (envelope == null) return;
            string action = MapMessageTypeToAction(envelope.messageType);
            if (string.IsNullOrEmpty(action)) return;

            var data = envelope.payload as Dictionary<string, object> ?? new Dictionary<string, object>();
            string requestId = ParseStr(data, "requestId");
            if (string.IsNullOrEmpty(requestId)) requestId = envelope.requestId ?? string.Empty;
            if (string.IsNullOrEmpty(requestId) || requestId.Length > 128) return;

            int mode = ParseInt(data, "mode", 4);
            if (!IsSupportedMode(mode)) mode = 4;
            int mapId = ParseInt(data, "mapId", 0);
            if (mapId <= 0) mapId = DefaultMapForMode(mode);

            var cmd = new PendingCommand
            {
                RequestId = requestId,
                Action = action,
                RoomName = Truncate(ParseStr(data, "roomName"), 36),
                RoomPwd = Truncate(ParseStr(data, "roomPwd"), 12),
                Mode = mode,
                MapId = mapId,
                Difficulty = ClampInt(ParseInt(data, "difficulty", 2), 0, 3),
                UpgradePlan = ClampInt(ParseInt(data, "upgradePlan", 1), 1, 6),
                TimePlan = ClampInt(ParseInt(data, "timePlan", 2), 1, 3),
                SpeedType = ClampInt(ParseInt(data, "speedType", 2), 1, 2),
                SkipStory = ParseBool(data, "skipStory", false),
                RoomLabel = ClampInt(ParseInt(data, "roomLabel", 0), 0, 99),
                IsAddBot = ParseBool(data, "isAddBot", true),
                TargetPlayerId = ParseLong(data, "targetPlayerId", 0),
                StartedAt = Time.realtimeSinceStartup
            };

            lock (QueueLock)
            {
                if (CommandQueue.Count < 8)
                {
                    CommandQueue.Enqueue(cmd);
                }
            }

            XingShuRuntimeHost.LogLifecycle(string.Format(
                CultureInfo.InvariantCulture,
                "[NativeRoomController] Queued {0}: reqId='{1}', mode={2}, mapId={3}",
                cmd.Action, cmd.RequestId, cmd.Mode, cmd.MapId));
        }

        internal static void Tick()
        {
            TickActiveRpc();

            if (_active == null)
            {
                PendingCommand next = null;
                lock (QueueLock)
                {
                    if (CommandQueue.Count > 0)
                    {
                        next = CommandQueue.Dequeue();
                    }
                }
                if (next != null)
                {
                    StartCommand(next);
                }
            }

            BroadcastSnapshotIfNeeded(force: false);
        }

        private static void StartCommand(PendingCommand cmd)
        {
            if (!ValidateOnlineHomeLobby(out GameLogicManager logic, out string reason))
            {
                SendCommandResult(cmd, false, -1, 0, reason);
                return;
            }

            RoomLogic room = logic.room;
            RoomInfo info = room.curRoomInfo;
            RoomStateType state = room.roomController != null ? room.roomController.roomStateType : RoomStateType.NONE;

            try
            {
                switch (cmd.Action)
                {
                    case "create":
                    {
                        if (info != null || room.IsInRoom)
                        {
                            SendCommandResult(cmd, false, -1, info != null ? info.Id : 0, "当前已在房间中，请先在房间内修改或退出后再建房。");
                            return;
                        }
                        if (logic.match != null && !logic.match.CheckOperateForMatch())
                        {
                            SendCommandResult(cmd, false, -1, 0, "当前正在队列匹配中，请取消匹配后再建房。");
                            return;
                        }
                        if (!logic.account.StartGameLicense())
                        {
                            SendCommandResult(cmd, false, -1, 0, "当前账号处于竞技冷却中，暂无法建房。");
                            return;
                        }

                        DismissOverlaysBeforeEnter();
                        string roomName = !string.IsNullOrEmpty(cmd.RoomName)
                            ? cmd.RoomName
                            : (Truncate(logic.account.GetName(), 24) + "的房间");
                        if (string.IsNullOrEmpty(roomName)) roomName = "星枢房间";
                        roomName = Truncate(roomName, 36);

                        _rpc = room.RequestCreateC2S(
                            roomName,
                            cmd.RoomPwd ?? string.Empty,
                            cmd.MapId,
                            0,
                            cmd.UpgradePlan,
                            cmd.TimePlan,
                            0UL,
                            cmd.SpeedType,
                            cmd.Mode,
                            cmd.Difficulty,
                            cmd.SkipStory,
                            cmd.RoomLabel);
                        _active = cmd;
                        cmd.StartedAt = Time.realtimeSinceStartup;
                        break;
                    }
                    case "modify":
                    {
                        if (info == null || !room.IsInRoom)
                        {
                            SendCommandResult(cmd, false, -1, 0, "当前尚未进入房间，请先创建或加入房间。");
                            return;
                        }
                        if (state != RoomStateType.WAIT)
                        {
                            SendCommandResult(cmd, false, -1, info.Id, "对局已开始，仅等待大厅状态下支持修改房间。");
                            return;
                        }
                        if (!logic.account.IsSelf(info.MasterId))
                        {
                            SendCommandResult(cmd, false, -1, info.Id, "只有当前房主才能修改房间配置。");
                            return;
                        }
                        _rpc = room.RequestChangeRoomC2S(
                            cmd.RoomPwd ?? string.Empty,
                            cmd.MapId,
                            cmd.TimePlan,
                            cmd.UpgradePlan,
                            cmd.SpeedType,
                            cmd.Difficulty,
                            cmd.SkipStory,
                            cmd.RoomLabel);
                        _active = cmd;
                        cmd.StartedAt = Time.realtimeSinceStartup;
                        break;
                    }
                    case "start":
                    {
                        if (info == null || !room.IsInRoom)
                        {
                            SendCommandResult(cmd, false, -1, 0, "当前尚未在房间中，无法开始游戏。");
                            return;
                        }
                        if (state != RoomStateType.WAIT)
                        {
                            SendCommandResult(cmd, false, -1, info.Id, "房间已进入开局流程。");
                            return;
                        }
                        if (!logic.account.IsSelf(info.MasterId))
                        {
                            SendCommandResult(cmd, false, -1, info.Id, "只有当前房主可以点击开始游戏。");
                            return;
                        }
                        if (info.Players != null)
                        {
                            foreach (RoomPlayer player in info.Players)
                            {
                                if (player != null && player.Id != info.MasterId && !player.RoomReady)
                                {
                                    SendCommandResult(cmd, false, 1021, info.Id, "仍有队员未点击准备，暂时无法开局。");
                                    return;
                                }
                            }
                        }
                        DismissOverlaysBeforeEnter();
                        _rpc = room.RequestStartGameC2S(cmd.IsAddBot);
                        _active = cmd;
                        cmd.StartedAt = Time.realtimeSinceStartup;
                        break;
                    }
                    case "kick":
                    {
                        if (info == null || !room.IsInRoom || state != RoomStateType.WAIT)
                        {
                            SendCommandResult(cmd, false, -1, 0, "当前不在等待房间中。");
                            return;
                        }
                        if (!logic.account.IsSelf(info.MasterId))
                        {
                            SendCommandResult(cmd, false, -1, info.Id, "只有房主可以移出玩家。");
                            return;
                        }
                        if (cmd.TargetPlayerId <= 0 || logic.account.IsSelf(cmd.TargetPlayerId))
                        {
                            SendCommandResult(cmd, false, -1, info.Id, "目标玩家 ID 无效。");
                            return;
                        }
                        _rpc = room.RequestKickPlayer(cmd.TargetPlayerId);
                        _active = cmd;
                        cmd.StartedAt = Time.realtimeSinceStartup;
                        break;
                    }
                    case "exit":
                    {
                        if (info == null && !room.IsInRoom)
                        {
                            SendCommandResult(cmd, true, 0, 0, "当前不在房间中。");
                            return;
                        }
                        _rpc = room.RequestExitRoomC2S(ExitRoomC2S.Types.ForceExitType.None);
                        _active = cmd;
                        cmd.StartedAt = Time.realtimeSinceStartup;
                        break;
                    }
                    default:
                        SendCommandResult(cmd, false, -1, 0, "不支持的房间指令。");
                        break;
                }
            }
            catch (Exception ex)
            {
                _active = null;
                _rpc = null;
                SendCommandResult(cmd, false, -1, info != null ? info.Id : 0, "游戏内执行房间操作失败: " + ex.Message);
            }
        }

        private static void TickActiveRpc()
        {
            if (_active == null) return;

            float elapsed = Time.realtimeSinceStartup - _active.StartedAt;
            if (_rpc != null && _rpc.keepWaiting && elapsed < 8.5f)
            {
                return;
            }

            PendingCommand cmd = _active;
            bool timedOut = _rpc == null || _rpc.keepWaiting;
            int errId = _rpc != null ? _rpc.errId : -1;
            _active = null;
            _rpc = null;

            GameLogicManager logic = SimpleSingletonProvider<GameLogicManager>.hasInstance
                ? SimpleSingletonProvider<GameLogicManager>.inst
                : null;
            RoomInfo info = logic?.room?.curRoomInfo;
            long roomId = info != null ? info.Id : 0;

            if (timedOut)
            {
                SendCommandResult(cmd, false, -1, roomId, "游戏服务器响应房间指令超时。");
                return;
            }

            if (errId != 0)
            {
                SendCommandResult(cmd, false, errId, roomId, DescribeRpcError(cmd.Action, errId));
                return;
            }

            if (cmd.Action == "exit")
            {
                try
                {
                    if (SimpleSingletonProvider<UIManager>.hasInstance &&
                        SimpleSingletonProvider<UIManager>.inst?.currentPanel is RoomWaitPanel wp)
                    {
                        SimpleSingletonProvider<UIManager>.inst.ReturnToLastPanel(wp);
                    }
                }
                catch { }
            }

            string okMsg = cmd.Action switch
            {
                "create" => roomId > 0 ? $"已在游戏内创建房间 #{roomId}" : "已发送创建房间请求",
                "modify" => roomId > 0 ? $"已更新房间 #{roomId} 设置" : "房间设置已更新",
                "start" => "已开始游戏对局",
                "kick" => "已移出指定玩家",
                "exit" => "已退出房间",
                _ => "房间操作完成"
            };
            SendCommandResult(cmd, true, 0, roomId, okMsg);
            ForceBroadcastSnapshot();
        }

        private static void BroadcastSnapshotIfNeeded(bool force)
        {
#if !XINGSHU_ANDROID_PREVIEW
            try
            {
                if (!XingShuBridgeClient.Instance.IsHandshakeComplete) return;

                float now = Time.realtimeSinceStartup;
                if (!force && now - _lastSnapshotAt < 0.45f) return;

                GameLogicManager logic = SimpleSingletonProvider<GameLogicManager>.hasInstance
                    ? SimpleSingletonProvider<GameLogicManager>.inst
                    : null;
                RoomLogic room = logic?.room;
                RoomInfo info = room?.curRoomInfo;
                RoomStateType state = room?.roomController != null ? room.roomController.roomStateType : RoomStateType.NONE;
                bool inRoom = room != null && room.IsInRoom && info != null && info.Id > 0 && state != RoomStateType.NONE;

                long selfPid = logic?.account != null ? logic.account.GetPlayerID() : 0;
                var playersList = new List<Dictionary<string, object>>();
                var fp = new StringBuilder();

                if (!inRoom)
                {
                    fp.Append("out");
                }
                else
                {
                    fp.Append(info.Id).Append('|')
                      .Append((int)state).Append('|')
                      .Append(info.MapType).Append('|')
                      .Append(info.MapId).Append('|')
                      .Append(info.Difficulty).Append('|')
                      .Append(info.UpgradePlan).Append('|')
                      .Append(info.TimePlan).Append('|')
                      .Append(info.MasterId).Append('|')
                      .Append(info.Name ?? string.Empty).Append('|')
                      .Append(info.Pwd ?? string.Empty);

                    if (info.Players != null)
                    {
                        foreach (RoomPlayer p in info.Players)
                        {
                            if (p == null) continue;
                            string nick = string.Empty;
                            try { nick = p.GetNick(false) ?? string.Empty; } catch { }
                            bool isHost = p.Id == info.MasterId;
                            bool isSelf = p.Id == selfPid && selfPid > 0;
                            fp.Append('|').Append(p.Id).Append(':').Append(p.Slot).Append(':')
                              .Append(p.RoomReady ? '1' : '0').Append(':').Append(p.IsBot ? 'B' : 'H')
                              .Append(':').Append(nick);
                            playersList.Add(new Dictionary<string, object>
                            {
                                ["playerId"] = p.Id,
                                ["name"] = nick,
                                ["slot"] = p.Slot,
                                ["level"] = p.Level,
                                ["ready"] = p.RoomReady,
                                ["isBot"] = p.IsBot,
                                ["isHost"] = isHost,
                                ["isSelf"] = isSelf
                            });
                        }
                    }
                }

                string nextFp = fp.ToString();
                if (!force && nextFp == _lastSnapshotFingerprint && (!inRoom || now - _lastSnapshotAt < 2.5f))
                {
                    return;
                }

                _lastSnapshotFingerprint = nextFp;
                _lastSnapshotAt = now;

                if (!inRoom)
                {
                    XingShuBridgeClient.Instance.SendEnvelope("RUNTIME_ROOM_SNAPSHOT", new
                    {
                        inRoom = false,
                        roomId = 0L,
                        roomState = "NONE",
                        players = playersList
                    });
                    return;
                }

                XingShuBridgeClient.Instance.SendEnvelope("RUNTIME_ROOM_SNAPSHOT", new
                {
                    inRoom = true,
                    roomId = info.Id,
                    roomState = state.ToString(),
                    name = info.Name ?? string.Empty,
                    hasPwd = !string.IsNullOrEmpty(info.Pwd),
                    roomPwd = info.Pwd ?? string.Empty,
                    mode = info.MapType,
                    mapId = info.MapId,
                    difficulty = info.Difficulty,
                    upgradePlan = info.UpgradePlan,
                    timePlan = info.TimePlan,
                    masterId = info.MasterId,
                    isHost = logic.account != null && logic.account.IsSelf(info.MasterId),
                    maxPlayers = info.RoomPlayerMaxCount,
                    players = playersList
                });
            }
            catch { }
#endif
        }

        private static bool ValidateOnlineHomeLobby(out GameLogicManager logic, out string reason)
        {
            logic = SimpleSingletonProvider<GameLogicManager>.hasInstance
                ? SimpleSingletonProvider<GameLogicManager>.inst
                : null;

            if (!SimpleSingletonProvider<SceneManager>.hasInstance ||
                SimpleSingletonProvider<SceneManager>.inst?.currentType == null ||
                SimpleSingletonProvider<SceneManager>.inst.currentType.Value != SceneType.Home)
            {
                reason = "请先在游戏客户端进入主大厅后再进行开房操作。";
                return false;
            }

            if (logic == null || logic.account == null || !logic.account.online || logic.room == null)
            {
                reason = "游戏账号尚未完成官服登录，请进入主大厅后再试。";
                return false;
            }

            NetManager net = MonoSingletonProvider<NetManager>.inst;
            if (net == null || !net.IsConnected || net.closeType == CloseType.ServerKick)
            {
                reason = "游戏服务器连接已断开，请重连大厅后再试。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static void DismissOverlaysBeforeEnter()
        {
            try { XingShuRuntime.UI.XingShuUICenter.Close(); } catch { }
            try { SimpleSingletonProvider<UIManager>.inst?.CloseAllUnFightWin(); } catch { }
        }

        private static void SendCommandResult(PendingCommand cmd, bool ok, int errId, long roomId, string message)
        {
            XingShuRuntimeHost.LogLifecycle(string.Format(
                CultureInfo.InvariantCulture,
                "[NativeRoomController] Result reqId='{0}' action='{1}' ok={2} errId={3} roomId={4} msg='{5}'",
                cmd.RequestId, cmd.Action, ok, errId, roomId, message));

#if !XINGSHU_ANDROID_PREVIEW
            try
            {
                if (XingShuBridgeClient.Instance.IsHandshakeComplete)
                {
                    XingShuBridgeClient.Instance.SendEnvelope("ROOM_IN_GAME_RESULT", new
                    {
                        requestId = cmd.RequestId,
                        action = cmd.Action,
                        ok,
                        errId,
                        roomId,
                        message
                    });
                }
            }
            catch { }
#endif
        }

        private static string DescribeRpcError(string action, int errId)
        {
            if (errId == 10015)
            {
                return "官方服务器当前未开放该模式或难度入口（错误码 10015 / NotOpen）。";
            }
            return $"游戏内{action}房间未成功（错误码 {errId}）。";
        }

        private static string MapMessageTypeToAction(string msgType)
        {
            switch (msgType)
            {
                case "ROOM_IN_GAME_CREATE": return "create";
                case "ROOM_IN_GAME_MODIFY": return "modify";
                case "ROOM_IN_GAME_START": return "start";
                case "ROOM_IN_GAME_KICK": return "kick";
                case "ROOM_IN_GAME_EXIT": return "exit";
                default: return null;
            }
        }

        private static bool IsSupportedMode(int mode) =>
            mode == 1 || mode == 3 || mode == 4 || mode == 7 || mode == 11 || mode == 12;

        private static int DefaultMapForMode(int mode)
        {
            switch (mode)
            {
                case 1: return 81003;
                case 3: return 81002;
                case 7: return 85001;
                case 11: return 81004;
                case 12: return 83002;
                default: return 82016;
            }
        }

        private static string Truncate(string s, int maxLen) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= maxLen ? s : s.Substring(0, maxLen));

        private static int ClampInt(int val, int min, int max) => val < min ? min : (val > max ? max : val);

        private static string ParseStr(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.TryGetValue(key, out object v) || v == null) return string.Empty;
            return v is string s ? s.Trim() : Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }

        private static long ParseLong(Dictionary<string, object> dict, string key, long fallback)
        {
            if (dict == null || !dict.TryGetValue(key, out object v) || v == null) return fallback;
            if (v is long l) return l;
            if (v is int i) return i;
            if (v is double d) return (long)d;
            return long.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : fallback;
        }

        private static int ParseInt(Dictionary<string, object> dict, string key, int fallback) =>
            (int)ParseLong(dict, key, fallback);

        private static bool ParseBool(Dictionary<string, object> dict, string key, bool fallback)
        {
            if (dict == null || !dict.TryGetValue(key, out object v) || v == null) return fallback;
            if (v is bool b) return b;
            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            return bool.TryParse(s, out bool parsed) ? parsed : fallback;
        }
    }
}
