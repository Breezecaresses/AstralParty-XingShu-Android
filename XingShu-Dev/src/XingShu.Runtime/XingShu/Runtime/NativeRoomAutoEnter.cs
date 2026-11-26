using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Handles Hub / Android Assistant requested automatic room entry once the game reaches the online Home lobby.
    /// Joins the existing room if still present in the official waiting list, or seamlessly recreates it without 11001 toasts.
    /// </summary>
    internal static class NativeRoomAutoEnter
    {
        private enum Phase
        {
            Idle,
            WaitingForLobby,
            QueryingRoom,
            JoiningRoom,
            CreatingRoom,
            ConfirmingInRoom
        }

        private sealed class RequestState
        {
            public string RequestId;
            public long ExpectedPlayerId;
            public string Action;
            public long RoomId;
            public int RoomServerId;
            public string RoomPwd;
            public string RoomName;
            public int Mode;
            public int MapId;
            public int Difficulty;
            public int UpgradePlan;
            public int TimePlan;
            public int SpeedType;
            public bool SkipStory;
            public int RoomLabel;
            public string Source;
            public float EnqueuedAt;
            public float Deadline;
        }

        private static readonly object Sync = new object();
        private static readonly HashSet<string> CompletedIds = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Queue<string> CompletedOrder = new Queue<string>();

        private static RequestState _queued;
        private static RequestState _active;
        private static Phase _phase = Phase.Idle;
        private static RPCAsyncResult _rpc;
        private static float _lobbyReadyAt = -1f;
        private static float _phaseStartedAt;
        private static string _confirmAction = "created";
        private static int _savedRoomListMode;
        private static bool _restoreRoomListMode;

        internal static void Reset()
        {
            lock (Sync)
            {
                _queued = null;
            }
            _active = null;
            _phase = Phase.Idle;
            _rpc = null;
            _lobbyReadyAt = -1f;
            _restoreRoomListMode = false;
        }

        internal static void EnqueueFromBridge(BridgeEnvelope envelope)
        {
            var data = envelope?.payload as Dictionary<string, object>;
            if (data == null) return;
            EnqueueParsed(data, "bridge");
        }

        internal static void EnqueueFromAndroidJson(Dictionary<string, object> data)
        {
            if (data == null) return;
            EnqueueParsed(data, "android");
        }

        private static void EnqueueParsed(Dictionary<string, object> data, string source)
        {
            string requestId = ParseStr(data, "requestId");
            if (string.IsNullOrEmpty(requestId) || requestId.Length > 128) return;

            int mode = ParseInt(data, "mode", 4);
            if (!IsSupportedMode(mode)) mode = 4;

            var req = new RequestState
            {
                RequestId = requestId,
                ExpectedPlayerId = ParseLong(data, "expectedPlayerId", 0),
                Action = NormalizeAction(ParseStr(data, "action")),
                RoomId = ParseLong(data, "roomId", 0),
                RoomServerId = ParseInt(data, "roomServerId", 0),
                RoomPwd = Truncate(ParseStr(data, "roomPwd"), 16),
                RoomName = Truncate(ParseStr(data, "roomName"), 36),
                Mode = mode,
                MapId = ParseInt(data, "mapId", 0),
                Difficulty = ClampInt(ParseInt(data, "difficulty", 2), 0, 3),
                UpgradePlan = ClampInt(ParseInt(data, "upgradePlan", 1), 1, 6),
                TimePlan = ClampInt(ParseInt(data, "timePlan", 2), 1, 3),
                SpeedType = ClampInt(ParseInt(data, "speedType", 2), 1, 2),
                SkipStory = ParseBool(data, "skipStory", false),
                RoomLabel = ClampInt(ParseInt(data, "roomLabel", 0), 0, 99),
                Source = source,
                EnqueuedAt = Time.realtimeSinceStartup,
                Deadline = Time.realtimeSinceStartup + 60f
            };

            if (req.MapId <= 0)
            {
                req.MapId = DefaultMapForMode(req.Mode);
            }

            lock (Sync)
            {
                if (CompletedIds.Contains(req.RequestId)) return;
                if (_active != null && _active.RequestId == req.RequestId) return;
                if (_queued != null && _queued.RequestId == req.RequestId) return;
                _queued = req;
            }

            XingShuRuntimeHost.LogLifecycle(string.Format(
                CultureInfo.InvariantCulture,
                "[NativeRoomAutoEnter] Enqueued ({0}): reqId='{1}', action='{2}', roomId={3}, srvId={4}, mode={5}, mapId={6}, expectedPid={7}",
                source, req.RequestId, req.Action, req.RoomId, req.RoomServerId, req.Mode, req.MapId, req.ExpectedPlayerId));
        }

        internal static void Tick()
        {
            RequestState next = null;
            lock (Sync)
            {
                if (_queued != null)
                {
                    if (_phase == Phase.Idle || _phase == Phase.WaitingForLobby)
                    {
                        next = _queued;
                        _queued = null;
                    }
                }
            }

            if (next != null)
            {
                _active = next;
                _phase = Phase.WaitingForLobby;
                _rpc = null;
                _lobbyReadyAt = -1f;
                _phaseStartedAt = Time.realtimeSinceStartup;
            }

            if (_active == null || _phase == Phase.Idle) return;

            float now = Time.realtimeSinceStartup;
            var logic = SimpleSingletonProvider<GameLogicManager>.hasInstance ? SimpleSingletonProvider<GameLogicManager>.inst : null;

            switch (_phase)
            {
                case Phase.WaitingForLobby:
                    TickWaitingForLobby(now, logic);
                    break;
                case Phase.QueryingRoom:
                    TickQueryingRoom(now, logic);
                    break;
                case Phase.JoiningRoom:
                    TickJoiningRoom(now, logic);
                    break;
                case Phase.CreatingRoom:
                    TickCreatingRoom(now, logic);
                    break;
                case Phase.ConfirmingInRoom:
                    TickConfirmingInRoom(now, logic);
                    break;
            }
        }

        private static void TickWaitingForLobby(float now, GameLogicManager logic)
        {
            if (now >= _active.Deadline)
            {
                Complete("error", 0, "等待游戏登录并进入大厅超时，请回到启动器重试。");
                return;
            }

            if (!SimpleSingletonProvider<SceneManager>.hasInstance ||
                SimpleSingletonProvider<SceneManager>.inst?.currentType == null ||
                SimpleSingletonProvider<SceneManager>.inst.currentType.Value != SceneType.Home)
            {
                _lobbyReadyAt = -1f;
                return;
            }

            if (logic == null || logic.account == null || !logic.account.online || logic.room == null)
            {
                _lobbyReadyAt = -1f;
                return;
            }

            NetManager net = MonoSingletonProvider<NetManager>.inst;
            if (net == null || !net.IsConnected || net.closeType == CloseType.ServerKick)
            {
                _lobbyReadyAt = -1f;
                return;
            }

            long pid = logic.account.GetPlayerID();
            if (pid <= 0 || (_active.ExpectedPlayerId > 0 && pid != _active.ExpectedPlayerId))
            {
                _lobbyReadyAt = -1f;
                return;
            }

            if (logic.room.IsInRoom)
            {
                long inRoomId = logic.room.curRoomInfo != null ? logic.room.curRoomInfo.Id : _active.RoomId;
                Complete("already_in_room", inRoomId, "游戏内已处于房间中。");
                return;
            }

            if (!SimpleSingletonProvider<UIManager>.hasInstance)
            {
                _lobbyReadyAt = -1f;
                return;
            }

            IBasePanel panel = SimpleSingletonProvider<UIManager>.inst.currentPanel;
            if (panel == null || !panel.IsOpen() || panel.config == null)
            {
                _lobbyReadyAt = -1f;
                return;
            }

            UIPanelType pt = panel.config.PanelType;
            if (pt != UIPanelType.Home && pt != UIPanelType.RoomList && pt != UIPanelType.MatchEntrance)
            {
                _lobbyReadyAt = -1f;
                return;
            }

            if (_lobbyReadyAt < 0f)
            {
                _lobbyReadyAt = now;
                return;
            }

            // Allow native OnlineSyncRoomIdS2C & HomePanel.RefreshSystemStatus a brief settle window first.
            if (now - _lobbyReadyAt < 0.85f) return;

            if (logic.watch != null && logic.watch.PlayerIsWatcher())
            {
                Complete("skipped", 0, "当前正在观战中，已跳过自动进房。");
                return;
            }

            if (logic.match != null && !logic.match.CheckOperateForMatch())
            {
                Complete("skipped", 0, "当前正在队列匹配中，请取消匹配后再试。");
                return;
            }

            if (!logic.account.StartGameLicense())
            {
                Complete("skipped", 0, "当前账号处于竞技冷却中，暂无法进房。");
                return;
            }

            DismissForegroundOverlays();

            if (_active.Action == "create" || _active.RoomId <= 0)
            {
                StartCreateRpc(now, logic);
                return;
            }

            if (_active.RoomServerId > 0)
            {
                StartJoinRpc(now, logic, _active.RoomServerId);
                return;
            }

            StartQueryRpc(now, logic);
        }

        private static void StartQueryRpc(float now, GameLogicManager logic)
        {
            if (logic.roomList != null)
            {
                try
                {
                    _savedRoomListMode = logic.roomList.mapModeType;
                    logic.roomList.mapModeType = _active.Mode;
                    if (logic.roomList.mapModeType == _active.Mode)
                    {
                        _restoreRoomListMode = _savedRoomListMode > 0 && _savedRoomListMode != _active.Mode;
                        _rpc = logic.roomList.RequestQueryRoomC2S();
                        _phase = Phase.QueryingRoom;
                        _phaseStartedAt = now;
                        XingShuRuntimeHost.LogLifecycle("[NativeRoomAutoEnter] Querying official waiting lobby for room #" + _active.RoomId);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    XingShuRuntimeHost.LogLifecycle("[NativeRoomAutoEnter] QueryRoom fallback: " + ex.Message);
                }
            }

            if (_active.Action == "join_or_create")
            {
                StartCreateRpc(now, logic);
            }
            else
            {
                Complete("error", 0, "未能查询官方等待大厅以加入指定房间。");
            }
        }

        private static void TickQueryingRoom(float now, GameLogicManager logic)
        {
            if (_rpc != null && _rpc.keepWaiting && now - _phaseStartedAt < 4.5f) return;

            bool timedOut = _rpc == null || _rpc.keepWaiting;
            int errId = _rpc != null ? _rpc.errId : -1;
            RoomShortInfo matched = null;
            if (!timedOut && errId == 0 && logic?.roomList != null)
            {
                try { matched = logic.roomList.GetRoomLabelById(_active.RoomId); } catch { }
            }

            RestoreRoomListMode(logic);

            if (matched != null && matched.Id == _active.RoomId && matched.PlayerCount < 4)
            {
                StartJoinRpc(now, logic, matched.RoomServerId);
                return;
            }

            if (_active.Action == "join_or_create")
            {
                XingShuRuntimeHost.LogLifecycle("[NativeRoomAutoEnter] Room #" + _active.RoomId + " not active in waiting lobby; creating fresh room with identical config");
                StartCreateRpc(now, logic);
            }
            else
            {
                Complete("error", 0, "目标房间 #" + _active.RoomId + " 已结束或不在等待大厅中。");
            }
        }

        private static void StartJoinRpc(float now, GameLogicManager logic, int roomServerId)
        {
            try
            {
                _rpc = logic.room.RequestJoinRoomC2S(_active.RoomId, _active.RoomPwd ?? string.Empty, roomServerId);
                _phase = Phase.JoiningRoom;
                _phaseStartedAt = now;
                _confirmAction = "joined";
                XingShuRuntimeHost.LogLifecycle("[NativeRoomAutoEnter] Sending RequestJoinRoomC2S for #" + _active.RoomId + " (srv=" + roomServerId + ")");
            }
            catch (Exception ex)
            {
                if (_active.Action == "join_or_create")
                {
                    XingShuRuntimeHost.LogLifecycle("[NativeRoomAutoEnter] Join threw, falling back to create: " + ex.Message);
                    StartCreateRpc(now, logic);
                }
                else
                {
                    Complete("error", 0, "进房请求发送异常: " + ex.Message);
                }
            }
        }

        private static void TickJoiningRoom(float now, GameLogicManager logic)
        {
            if (_rpc != null && _rpc.keepWaiting && now - _phaseStartedAt < 8f) return;

            bool timedOut = _rpc == null || _rpc.keepWaiting;
            int errId = _rpc != null ? _rpc.errId : -1;
            if (!timedOut && errId == 0)
            {
                _phase = Phase.ConfirmingInRoom;
                _phaseStartedAt = now;
                _confirmAction = "joined";
                return;
            }

            if (_active.Action == "join_or_create")
            {
                XingShuRuntimeHost.LogLifecycle("[NativeRoomAutoEnter] Join returned errId=" + errId + "; falling back to RequestCreateC2S");
                StartCreateRpc(now, logic);
            }
            else
            {
                Complete("error", 0, "加入官方房间失败（错误码 " + errId + "）。");
            }
        }

        private static void StartCreateRpc(float now, GameLogicManager logic)
        {
            try
            {
                string name = _active.RoomName;
                if (string.IsNullOrEmpty(name))
                {
                    try { name = logic.account.GetName(); } catch { }
                }
                if (string.IsNullOrEmpty(name)) name = "星枢房间";
                name = Truncate(name, 36);

                _rpc = logic.room.RequestCreateC2S(
                    name,
                    _active.RoomPwd ?? string.Empty,
                    _active.MapId,
                    0,
                    _active.UpgradePlan,
                    _active.TimePlan,
                    0UL,
                    _active.SpeedType,
                    _active.Mode,
                    _active.Difficulty,
                    _active.SkipStory,
                    _active.RoomLabel);
                _phase = Phase.CreatingRoom;
                _phaseStartedAt = now;
                _confirmAction = "created";
                XingShuRuntimeHost.LogLifecycle(string.Format(
                    CultureInfo.InvariantCulture,
                    "[NativeRoomAutoEnter] Sending RequestCreateC2S (mode={0}, map={1}, diff={2}, upg={3}, speed={4}, time={5}, label={6})",
                    _active.Mode, _active.MapId, _active.Difficulty, _active.UpgradePlan, _active.SpeedType, _active.TimePlan, _active.RoomLabel));
            }
            catch (Exception ex)
            {
                Complete("error", 0, "游戏内建房发送异常: " + ex.Message);
            }
        }

        private static void TickCreatingRoom(float now, GameLogicManager logic)
        {
            if (_rpc != null && _rpc.keepWaiting && now - _phaseStartedAt < 8f) return;

            if (_rpc == null || _rpc.keepWaiting)
            {
                Complete("error", 0, "官方服务器响应建房超时。");
                return;
            }

            if (_rpc.errId != 0)
            {
                Complete("error", 0, "官方服务器未允许创建该房间（错误码 " + _rpc.errId + "）。");
                return;
            }

            _phase = Phase.ConfirmingInRoom;
            _phaseStartedAt = now;
            _confirmAction = "created";
        }

        private static void TickConfirmingInRoom(float now, GameLogicManager logic)
        {
            if (logic?.room != null && logic.room.IsInRoom)
            {
                long rid = logic.room.curRoomInfo != null ? logic.room.curRoomInfo.Id : 0;
                string verb = _confirmAction == "joined" ? "已在游戏内自动加入房间 #" : "已在游戏内自动创建并进入房间 #";
                Complete(_confirmAction, rid, rid > 0 ? verb + rid : "已自动进入官方房间。");
                return;
            }

            if (now - _phaseStartedAt >= 3.5f)
            {
                Complete("error", 0, "房间请求已应答，但未检测到进入等待房间界面。");
            }
        }

        private static void RestoreRoomListMode(GameLogicManager logic)
        {
            if (!_restoreRoomListMode || logic?.roomList == null) return;
            _restoreRoomListMode = false;
            try { logic.roomList.mapModeType = _savedRoomListMode; } catch { }
        }

        private static void DismissForegroundOverlays()
        {
            try { XingShuRuntime.UI.XingShuUICenter.Close(); } catch { }
            try { SimpleSingletonProvider<UIManager>.inst?.CloseAllUnFightWin(); } catch { }
        }

        private static void Complete(string status, long roomId, string message)
        {
            var req = _active;
            _active = null;
            _phase = Phase.Idle;
            _rpc = null;
            _lobbyReadyAt = -1f;
            _restoreRoomListMode = false;

            if (req == null) return;

            lock (Sync)
            {
                if (CompletedIds.Add(req.RequestId))
                {
                    CompletedOrder.Enqueue(req.RequestId);
                    while (CompletedOrder.Count > 32)
                    {
                        string old = CompletedOrder.Dequeue();
                        CompletedIds.Remove(old);
                    }
                }
            }

            XingShuRuntimeHost.LogLifecycle(string.Format(
                CultureInfo.InvariantCulture,
                "[NativeRoomAutoEnter] Done reqId='{0}' status='{1}' roomId={2} msg='{3}'",
                req.RequestId, status, roomId, message));

#if !XINGSHU_ANDROID_PREVIEW
            try
            {
                if (XingShuBridgeClient.Instance.IsHandshakeComplete)
                {
                    XingShuBridgeClient.Instance.SendEnvelope("ROOM_AUTO_ENTER_RESULT", new
                    {
                        requestId = req.RequestId,
                        status,
                        roomId,
                        message
                    });
                }
            }
            catch { }
#endif
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

        private static string NormalizeAction(string raw)
        {
            if (string.Equals(raw, "create", StringComparison.OrdinalIgnoreCase)) return "create";
            if (string.Equals(raw, "join", StringComparison.OrdinalIgnoreCase)) return "join";
            return "join_or_create";
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= maxLen ? s : s.Substring(0, maxLen);
        }

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
            if (bool.TryParse(s, out bool parsed)) return parsed;
            return fallback;
        }
    }
}
