using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Core.Net;
using Core.Scene;
using Core.Unit;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameLogic;
using Tools;
using UI;
using UnityEngine;
using XingShuRuntime.Core;
using XingShuRuntime.Utilities;
using party.model;
using party.protocol;

namespace XingShuRuntime.Targeting
{
    /// <summary>Read-only monster action visualizer: 10-step targeting, dice rolls, path selection, and map step markers.</summary>
    public sealed class MonsterTargetRuntime : MonoBehaviour
    {
        private sealed class Row
        {
            public GTextField Anchor;
            public GTextField Text;
            public GGraph Back;
        }

        private sealed class StepMarkerVisual
        {
            public GGraph Back;
            public GTextField Text;
            public Vector3 WorldPos;
        }

        private sealed class MonsterActionState
        {
            public int Round;
            public long TurnPlayerId;
            public int[] DiceVals;
            public int MovePoint;
            public int RemainingSteps;
            public readonly List<int> ChosenPath = new List<int>();
            public int PursuitNodeId = -1;
            public bool MoveEnded;
            public double LastUpdatedReal;
        }

        private static readonly FieldInfo CharacterMovementField = typeof(global::Core.Unit.Character).GetField(
            "move", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CharacterPathQueueField = typeof(CharacterMove).GetField(
            "pathQueue", BindingFlags.Instance | BindingFlags.NonPublic);

        private static MonsterTargetRuntime _instance;
        private readonly Dictionary<GTextField, Row> _rows = new Dictionary<GTextField, Row>();
        private readonly HashSet<GTextField> _seen = new HashSet<GTextField>();
        private readonly List<GTextField> _remove = new List<GTextField>();
        private readonly List<string> _details = new List<string>();
        private readonly List<string> _actionLines = new List<string>();
        private readonly Dictionary<long, MonsterActionState> _actionStates = new Dictionary<long, MonsterActionState>();
        private readonly List<StepMarkerVisual> _stepMarkers = new List<StepMarkerVisual>();

        private bool _enabled;
        private bool _arrowBoostEnabled;
        private bool _rollMoveEnabled;
        private bool _pathMarkersEnabled;
        private object _room;
        private double _nextTick;
        private double _nextError;
        private int _selected;
        private string _status = "等待进入 PVE 对局";
        private string _actionStatus = "等待怪物投点与移动行动";
        private readonly MonsterAttackWindow _attack = new MonsterAttackWindow();
        private readonly MonsterWalkArrowBoost _arrowBoost = new MonsterWalkArrowBoost();

        private static ThrowDiceS2CRPC.OnThrowDiceS2CServerDelegate _origThrowDice;
        private static MoveS2CRPC.OnMoveS2CServerDelegate _origMove;
        private static MonsterPursuitS2CRPC.OnMonsterPursuitS2CServerDelegate _origPursuit;
        private static RPCMsgManager _hookedRpcMgr;

        internal static bool IsEnabled => _instance != null ? _instance._enabled :
            XingShuConfigService.GetBool(XingShuConfigKeys.MonsterTargetEnabled, XingShuConfigKeys.MonsterTargetEnabledDefault);

        internal static bool ArrowBoostEnabled => _instance != null ? _instance._arrowBoostEnabled :
            XingShuConfigService.GetBool(XingShuConfigKeys.MonsterArrowBoostEnabled, XingShuConfigKeys.MonsterArrowBoostEnabledDefault);

        internal static bool RollMoveEnabled => _instance != null ? _instance._rollMoveEnabled :
            XingShuConfigService.GetBool(XingShuConfigKeys.MonsterActionRollMoveEnabled, XingShuConfigKeys.MonsterActionRollMoveEnabledDefault);

        internal static bool PathMarkersEnabled => _instance != null ? _instance._pathMarkersEnabled :
            XingShuConfigService.GetBool(XingShuConfigKeys.MonsterActionPathMarkersEnabled, XingShuConfigKeys.MonsterActionPathMarkersEnabledDefault);

        internal static string Summary => _instance == null ? "等待怪物行动可视初始化" : _instance.CurrentSummary();

        internal static string ActionSummary => _instance == null ? "等待怪物投点与移动行动" : _instance.CurrentActionSummary();

        internal static void SetEnabled(bool value)
        {
            XingShuConfigService.SetBool(XingShuConfigKeys.MonsterTargetEnabled, value);
            XingShuConfigService.Save();
            if (_instance == null) return;
            _instance._enabled = value;
            _instance._nextTick = 0.0;
            if (!value) _instance.ClearSession();
        }

        internal static void SetArrowBoostEnabled(bool value)
        {
            XingShuConfigService.SetBool(XingShuConfigKeys.MonsterArrowBoostEnabled, value);
            XingShuConfigService.Save();
            if (_instance == null) return;
            _instance._arrowBoostEnabled = value;
            if (!value) _instance._arrowBoost.RestoreAll();
        }

        internal static void SetRollMoveEnabled(bool value)
        {
            XingShuConfigService.SetBool(XingShuConfigKeys.MonsterActionRollMoveEnabled, value);
            XingShuConfigService.Save();
            if (_instance == null) return;
            _instance._rollMoveEnabled = value;
            _instance._nextTick = 0.0;
            if (!value) _instance.ClearStepMarkers();
        }

        internal static void SetPathMarkersEnabled(bool value)
        {
            XingShuConfigService.SetBool(XingShuConfigKeys.MonsterActionPathMarkersEnabled, value);
            XingShuConfigService.Save();
            if (_instance == null) return;
            _instance._pathMarkersEnabled = value;
            _instance._nextTick = 0.0;
            if (!value) _instance.ClearStepMarkers();
        }

        internal static void Select(int delta)
        {
            if (_instance == null || _instance._details.Count == 0) return;
            int count = _instance._details.Count;
            _instance._selected = (_instance._selected + delta % count + count) % count;
        }

        public static void Install(GameObject host)
        {
            if (host != null && host.GetComponent<MonsterTargetRuntime>() == null)
                host.AddComponent<MonsterTargetRuntime>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            _enabled = XingShuConfigService.GetBool(XingShuConfigKeys.MonsterTargetEnabled, XingShuConfigKeys.MonsterTargetEnabledDefault);
            _arrowBoostEnabled = XingShuConfigService.GetBool(XingShuConfigKeys.MonsterArrowBoostEnabled, XingShuConfigKeys.MonsterArrowBoostEnabledDefault);
            _rollMoveEnabled = XingShuConfigService.GetBool(XingShuConfigKeys.MonsterActionRollMoveEnabled, XingShuConfigKeys.MonsterActionRollMoveEnabledDefault);
            _pathMarkersEnabled = XingShuConfigService.GetBool(XingShuConfigKeys.MonsterActionPathMarkersEnabled, XingShuConfigKeys.MonsterActionPathMarkersEnabledDefault);
            Debug.Log("[XingShu Targeting] 怪物行动可视已接入：索敌推演 + 投点与移动路径可视化");
        }

        private void Update()
        {
            try { EnsureActionRpcHooks(); } catch { }
            try { ObserveAttack(); } catch { /* A scene transition has no usable battle state. */ }
            double now = ThinkingClock.RealSeconds();
            if (now < _nextTick) return;
            _nextTick = now + 0.16;
            try { Refresh(); }
            catch (Exception ex)
            {
                ClearRows();
                ClearStepMarkers();
                _details.Clear();
                _actionLines.Clear();
                _status = "索敌数据暂未就绪";
                if (now >= _nextError)
                {
                    _nextError = now + 10.0;
                    XingShu.Runtime.XingShuRuntimeHost.LogLifecycle("[XingShu Targeting] " + ex);
                }
            }
        }

        private void LateUpdate()
        {
            foreach (var row in _rows.Values) Place(row);
            PlaceAllStepMarkers();
            try { SyncArrowBoost(); } catch { /* A scene transition may not have initialized battle state yet. */ }
        }

        private void SyncArrowBoost()
        {
            if (!_arrowBoostEnabled ||
                !SimpleSingletonProvider<GameLogicManager>.hasInstance ||
                !SimpleSingletonProvider<SceneManager>.hasInstance)
            {
                _arrowBoost.RestoreAll();
                return;
            }

            var sceneMgr = SimpleSingletonProvider<SceneManager>.inst;
            var sceneType = sceneMgr != null && sceneMgr.currentType != null ? sceneMgr.currentType.Value : SceneType.None;
            var logic = SimpleSingletonProvider<GameLogicManager>.inst;
            bool isPve = HandCardDisplayManager.IsPVEMode();
            _arrowBoost.Sync(_arrowBoostEnabled, logic, sceneType, isPve);
        }

        private void EnsureActionRpcHooks()
        {
            if (!_rollMoveEnabled)
            {
                RestoreActionRpcHooks();
                return;
            }
            if (!MonoSingletonProvider<NetManager>.hasInstance) return;
            NetManager net = MonoSingletonProvider<NetManager>.inst;
            if (net == null || net.RPC == null) return;

            RPCMsgManager rpc = net.RPC;
            if (!ReferenceEquals(_hookedRpcMgr, rpc))
            {
                RestoreActionRpcHooks();
                _hookedRpcMgr = rpc;
            }

            if (rpc.ThrowDiceS2C != null && rpc.ThrowDiceS2C.OnThrowDiceS2CServerCallBackAsync != InterceptThrowDiceCallback)
            {
                _origThrowDice = rpc.ThrowDiceS2C.OnThrowDiceS2CServerCallBackAsync;
                rpc.ThrowDiceS2C.OnThrowDiceS2CServerCallBackAsync = InterceptThrowDiceCallback;
            }
            if (rpc.MoveS2C != null && rpc.MoveS2C.OnMoveS2CServerCallBackAsync != InterceptMoveCallback)
            {
                _origMove = rpc.MoveS2C.OnMoveS2CServerCallBackAsync;
                rpc.MoveS2C.OnMoveS2CServerCallBackAsync = InterceptMoveCallback;
            }
            if (rpc.MonsterPursuitS2C != null && rpc.MonsterPursuitS2C.OnMonsterPursuitS2CServerCallBackAsync != InterceptPursuitCallback)
            {
                _origPursuit = rpc.MonsterPursuitS2C.OnMonsterPursuitS2CServerCallBackAsync;
                rpc.MonsterPursuitS2C.OnMonsterPursuitS2CServerCallBackAsync = InterceptPursuitCallback;
            }
        }

        private static void RestoreActionRpcHooks()
        {
            RPCMsgManager rpc = _hookedRpcMgr;
            if (rpc == null) return;
            try
            {
                if (rpc.ThrowDiceS2C != null && rpc.ThrowDiceS2C.OnThrowDiceS2CServerCallBackAsync == InterceptThrowDiceCallback)
                {
                    rpc.ThrowDiceS2C.OnThrowDiceS2CServerCallBackAsync = _origThrowDice;
                }
                if (rpc.MoveS2C != null && rpc.MoveS2C.OnMoveS2CServerCallBackAsync == InterceptMoveCallback)
                {
                    rpc.MoveS2C.OnMoveS2CServerCallBackAsync = _origMove;
                }
                if (rpc.MonsterPursuitS2C != null && rpc.MonsterPursuitS2C.OnMonsterPursuitS2CServerCallBackAsync == InterceptPursuitCallback)
                {
                    rpc.MonsterPursuitS2C.OnMonsterPursuitS2CServerCallBackAsync = _origPursuit;
                }
            }
            catch
            {
            }
            _origThrowDice = null;
            _origMove = null;
            _origPursuit = null;
            _hookedRpcMgr = null;
        }

        private static async UniTask InterceptThrowDiceCallback(ThrowDiceS2C model, int errId, bool isDispatch)
        {
            try { _instance?.OnThrowDiceObserved(model); } catch { }
            if (_origThrowDice != null) await _origThrowDice(model, errId, isDispatch);
        }

        private static async UniTask InterceptMoveCallback(MoveS2C model, int errId, bool isDispatch)
        {
            try { _instance?.OnMoveObserved(model); } catch { }
            if (_origMove != null) await _origMove(model, errId, isDispatch);
        }

        private static async UniTask InterceptPursuitCallback(MonsterPursuitS2C model, int errId, bool isDispatch)
        {
            try { _instance?.OnPursuitObserved(model); } catch { }
            if (_origPursuit != null) await _origPursuit(model, errId, isDispatch);
        }

        private BattlePlayerData FindMonsterActor(long playerId)
        {
            if (playerId <= 0 || !SimpleSingletonProvider<GameLogicManager>.hasInstance) return null;
            var logic = SimpleSingletonProvider<GameLogicManager>.inst;
            var players = logic?.battle?.PlayerDatas;
            if (players == null) return null;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p?.player != null && p.player.Id == playerId && p.characterType == CharacterType.Monster)
                {
                    return p;
                }
            }
            return null;
        }

        private MonsterActionState GetOrCreateActionState(long monsterId, int round)
        {
            MonsterActionState st;
            if (!_actionStates.TryGetValue(monsterId, out st))
            {
                st = new MonsterActionState { Round = round };
                _actionStates[monsterId] = st;
            }
            else if (st.Round != round)
            {
                st.Round = round;
                st.DiceVals = null;
                st.MovePoint = 0;
                st.RemainingSteps = 0;
                st.ChosenPath.Clear();
                st.PursuitNodeId = -1;
                st.MoveEnded = false;
            }
            return st;
        }

        private void OnThrowDiceObserved(ThrowDiceS2C model)
        {
            if (!_rollMoveEnabled || model == null || FindMonsterActor(model.PlayerId) == null) return;
            var logic = SimpleSingletonProvider<GameLogicManager>.inst;
            var room = logic?.room?.curRoomInfo;
            if (room == null || !HandCardDisplayManager.IsPVEMode()) return;

            MonsterActionState st = GetOrCreateActionState(model.PlayerId, room.Round);
            st.TurnPlayerId = PveTurnContext.CurrentPlayerId();
            if (model.Vals != null && model.Vals.Count > 0)
            {
                int[] arr = new int[model.Vals.Count];
                for (int i = 0; i < model.Vals.Count; i++) arr[i] = model.Vals[i];
                st.DiceVals = arr;
            }
            else
            {
                st.DiceVals = null;
            }
            st.MovePoint = Math.Max(0, model.MovePoint);
            st.RemainingSteps = Math.Max(0, model.MovePoint);
            st.ChosenPath.Clear();
            st.PursuitNodeId = -1;
            st.MoveEnded = false;
            st.LastUpdatedReal = ThinkingClock.RealSeconds();
            _nextTick = 0.0;
        }

        private void OnPursuitObserved(MonsterPursuitS2C model)
        {
            if (!_rollMoveEnabled || model == null || FindMonsterActor(model.PlayerId) == null) return;
            var logic = SimpleSingletonProvider<GameLogicManager>.inst;
            var room = logic?.room?.curRoomInfo;
            if (room == null || !HandCardDisplayManager.IsPVEMode()) return;

            MonsterActionState st = GetOrCreateActionState(model.PlayerId, room.Round);
            st.TurnPlayerId = PveTurnContext.CurrentPlayerId();
            st.PursuitNodeId = model.NodeId;
            st.LastUpdatedReal = ThinkingClock.RealSeconds();
            _nextTick = 0.0;
        }

        private void OnMoveObserved(MoveS2C model)
        {
            if (!_rollMoveEnabled || model == null || FindMonsterActor(model.PlayerId) == null) return;
            var logic = SimpleSingletonProvider<GameLogicManager>.inst;
            var room = logic?.room?.curRoomInfo;
            if (room == null || !HandCardDisplayManager.IsPVEMode()) return;

            MonsterActionState st = GetOrCreateActionState(model.PlayerId, room.Round);
            st.TurnPlayerId = PveTurnContext.CurrentPlayerId();
            st.ChosenPath.Clear();
            if (model.NodeIds != null)
            {
                for (int i = 0; i < model.NodeIds.Count; i++)
                {
                    st.ChosenPath.Add(model.NodeIds[i]);
                }
            }
            st.MoveEnded = model.End;
            st.LastUpdatedReal = ThinkingClock.RealSeconds();
            _nextTick = 0.0;
        }

        private void ObserveAttack()
        {
            if (!_enabled || !HandCardDisplayManager.IsPVEMode()) return;
            var logic = SimpleSingletonProvider<GameLogicManager>.inst;
            var room = logic?.room?.curRoomInfo;
            if (!ReferenceEquals(_room, room)) { ClearSession(); _room = room; }
            if (room == null || logic.battle?.PlayerDatas == null) return;
            long turn = PveTurnContext.CurrentPlayerId();
            _attack.ObserveTurn(room.Round, turn);
            var fight = logic.fight?.battleFightData;
            if (fight == null || fight.isEnd || fight.attackerInfo == null || fight.attackerInfo.PlayerId != turn) return;
            foreach (var monster in logic.battle.PlayerDatas)
                if (monster?.player != null && monster.player.Id == turn && monster.characterType == CharacterType.Monster)
                { _attack.ObserveAttack(turn); break; }
        }

        private void SyncLiveCharacterMoveState(BattlePlayerData monster, int round, int currentNode, long turnPlayerId, double nowReal)
        {
            if (!_rollMoveEnabled || monster?.CharacterInst == null || monster.player == null) return;
            var ch = monster.CharacterInst;
            int canStep = ch.canStep;
            List<int> liveQueue = null;

            if (CharacterMovementField != null && CharacterPathQueueField != null)
            {
                try
                {
                    object moveObj = CharacterMovementField.GetValue(ch);
                    if (moveObj != null)
                    {
                        var q = CharacterPathQueueField.GetValue(moveObj) as IEnumerable<int>;
                        if (q != null)
                        {
                            foreach (int nid in q)
                            {
                                if (liveQueue == null) liveQueue = new List<int>();
                                liveQueue.Add(nid);
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            if (canStep <= 0 && (liveQueue == null || liveQueue.Count == 0))
            {
                MonsterActionState existing;
                if (_actionStates.TryGetValue(monster.player.Id, out existing) && existing.Round == round && existing.RemainingSteps > 0)
                {
                    existing.RemainingSteps = 0;
                    existing.MoveEnded = true;
                }
                return;
            }

            MonsterActionState st = GetOrCreateActionState(monster.player.Id, round);
            st.TurnPlayerId = turnPlayerId;
            if (canStep >= 0) st.RemainingSteps = canStep;
            if (liveQueue != null && liveQueue.Count > 0 && st.ChosenPath.Count == 0)
            {
                if (currentNode >= 0) st.ChosenPath.Add(currentNode);
                for (int i = 0; i < liveQueue.Count; i++) st.ChosenPath.Add(liveQueue[i]);
            }
            st.MoveEnded = false;
            st.LastUpdatedReal = nowReal;
        }

        private void Refresh()
        {
            if (!SimpleSingletonProvider<GameLogicManager>.hasInstance || !SimpleSingletonProvider<SceneManager>.hasInstance)
            {
                ClearSession();
                _status = "等待进入 PVE 对局";
                _actionStatus = "等待进入 PVE 对局";
                return;
            }
            var logic = SimpleSingletonProvider<GameLogicManager>.inst;
            var room = logic?.room?.curRoomInfo;
            if (!ReferenceEquals(_room, room)) { ClearSession(); _room = room; }
            if (!_enabled && !_rollMoveEnabled)
            {
                ClearRows();
                ClearStepMarkers();
                _status = "怪物行动可视已关闭";
                _actionStatus = "投点与移动可视已关闭";
                return;
            }
            if (room == null || !HandCardDisplayManager.IsPVEMode() || logic.battle?.PlayerDatas == null ||
                SimpleSingletonProvider<SceneManager>.inst.currentType == null ||
                SimpleSingletonProvider<SceneManager>.inst.currentType.Value != SceneType.Battle)
            {
                ClearSession();
                _status = "等待进入 PVE 对局";
                _actionStatus = "等待进入 PVE 对局";
                return;
            }

            var fight = logic.fight?.battleFightData;
            var data = logic.battle.PlayerDatas;
            long currentTurnPlayer = PveTurnContext.CurrentPlayerId();
            double nowReal = ThinkingClock.RealSeconds();

            // Build the shared graph/player snapshot once, not once per monster.
            var snapshot = MonsterTargetContext.ExtractInput();
            _seen.Clear();
            _details.Clear();
            _actionLines.Clear();
            var desiredMarkers = new List<KeyValuePair<MonsterPathMarker, Vector3>>();

            var ui = SimpleSingletonProvider<UIManager>.hasInstance ? SimpleSingletonProvider<UIManager>.inst : null;
            bool combat = (fight != null && !fight.isEnd) || ShownNativeWindows.Find<FightWindow>(ui) != null;
            var landMgr = SimpleSingletonProvider<LandManager>.hasInstance ? SimpleSingletonProvider<LandManager>.inst : null;
            var nodeDict = landMgr != null ? landMgr.NodeDict : null;

            foreach (var monster in data)
            {
                if (monster?.player == null || monster.characterType != CharacterType.Monster ||
                    monster.player.Hero == null || monster.Property?.HP == null || monster.Property.HP.Value <= 0 ||
                    monster.player.Hero.MonsterType == Hero.Types.MonsterType.PveAllyMonster) continue;
                var input = snapshot;
                input.MonsterId = monster.player.Id;
                input.CurrentNode = monster.CharacterInst?.standLand != null ? monster.CharacterInst.standLand.Id : monster.player.NodeId;
                input.MonsterNodeKnown = input.CurrentNode >= 0;
                input.HasAttackedKnown = _attack.HasAttacked(input.MonsterId, room.Round);
                input.HasAttackedThisTurn = input.HasAttackedKnown;

                SyncLiveCharacterMoveState(monster, room.Round, input.CurrentNode, currentTurnPlayer, nowReal);
                MonsterActionState actionState;
                bool hasAction = _actionStates.TryGetValue(input.MonsterId, out actionState) && actionState.Round == room.Round;
                string rollText = (_rollMoveEnabled && hasAction)
                    ? MonsterActionPolicy.FormatRollSummary(actionState.DiceVals, actionState.MovePoint, actionState.RemainingSteps)
                    : "";
                string pathText = (_rollMoveEnabled && hasAction)
                    ? MonsterActionPolicy.FormatPathChoiceSummary(actionState.ChosenPath, actionState.PursuitNodeId, 6)
                    : "";

                if (!string.IsNullOrEmpty(rollText) || !string.IsNullOrEmpty(pathText))
                {
                    var actSb = new StringBuilder();
                    actSb.Append("• ").Append(monster.player.GetNick()).Append("：");
                    if (!string.IsNullOrEmpty(rollText)) actSb.Append(rollText);
                    if (!string.IsNullOrEmpty(pathText))
                    {
                        if (!string.IsNullOrEmpty(rollText)) actSb.Append(" · ");
                        actSb.Append(pathText);
                    }
                    _actionLines.Add(actSb.ToString());
                }

                string directionSource;
                var steps = MonsterTargetContext.ExtractForwardSteps(monster, input.GraphEdges, out directionSource);
                var preview = MonsterTargetPreview.Calculate(input, steps);
                string compact;
                string reason;
                if (preview.AlreadyAttacked)
                {
                    compact = "本回合已攻击 · 继续原路线";
                    reason = "已观察到本回合主动战斗；本回合不再选择追击目标。";
                }
                else if (!preview.IsValid)
                {
                    compact = steps == null ? "索敌：等待箭头同步" : "索敌：等待路径数据";
                    reason = steps == null ? directionSource + "，暂不推测目标。" : "地图连接或玩家位置尚未同步完整，暂不推测目标。";
                }
                else if (preview.Candidates.Count == 0)
                {
                    compact = "10格推演：无候选 · 沿原路线";
                    reason = "仅沿箭头前进，每步排除来路；先行一格后，剩余9格内没有可达的存活玩家。";
                }
                else
                {
                    bool uncertain = preview.HasUncertainTarget;
                    var text = new StringBuilder(uncertain ? "10格候选 → " : "10格推演 → ");
                    var groups = new Dictionary<int, List<MonsterTargetPrediction>>();
                    foreach (var candidate in preview.Candidates)
                    {
                        List<MonsterTargetPrediction> group;
                        if (!groups.TryGetValue(candidate.TargetNodeId, out group)) groups[candidate.TargetNodeId] = group = new List<MonsterTargetPrediction>();
                        group.Add(candidate);
                    }
                    bool first = true;
                    foreach (var group in groups.Values)
                    {
                        group.Sort((a, b) => PlayerSlot(data, a.TargetPlayerId).CompareTo(PlayerSlot(data, b.TargetPlayerId)));
                        if (!first) text.Append(" / "); first = false;
                        for (int i = 0; i < group.Count; i++)
                        {
                            if (i > 0) text.Append(" + ");
                            text.Append(PlayerTag(data, group[i].TargetPlayerId, preview.Candidates.Count == 1));
                        }
                        if (group.Count > 1) text.Append("（同格全体）");
                    }
                    if (preview.TargetTileCount == 1 && !preview.AmbiguousFirstStep)
                        text.Append(" · ").Append(preview.Candidates[0].Distance).Append("格");
                    compact = text.ToString();
                    reason = uncertain ? "仅沿箭头前进；多个箭头出口或同距离先后未确认，保留可能目标。" : "沿箭头前进，不掉头；先选最近目标格，同距按红＞蓝＞绿。该格存活玩家一起受击，本回合不再索敌。最大骰10格，实际骰点可能不足。";
                    if (preview.AmbiguousFirstStep && preview.HasSingleTargetGroup)
                        reason += " 多个箭头出口均指向同一目标格；目标组相同，具体路线及步数仍待确认。";
                    if (preview.SomeRoutesHaveNoTarget) reason += " 部分出口无可达目标。";
                }

                var detail = new StringBuilder();
                detail.Append(monster.player.GetNick()).Append("  ·  地图格 #").Append(input.CurrentNode).Append('\n');
                detail.Append("当前箭头：");
                if (steps == null) detail.Append("等待同步");
                else for (int i = 0; i < steps.Count; i++)
                {
                    if (i > 0) detail.Append(" / ");
                    detail.Append('#').Append(steps[i]);
                }
                if (!string.IsNullOrEmpty(rollText) || !string.IsNullOrEmpty(pathText))
                {
                    detail.Append('\n').Append("本轮投点/选路：")
                        .Append(!string.IsNullOrEmpty(rollText) ? rollText : "未投点")
                        .Append(!string.IsNullOrEmpty(pathText) ? ("  |  " + pathText) : "");
                }
                detail.Append('\n').Append("方向来源：").Append(directionSource).Append('\n');
                detail.Append(compact).Append('\n').Append(reason);
                if (preview.Candidates.Count > 0)
                {
                    detail.Append("\n路径示例：");
                    var path = preview.Candidates[0].Path;
                    for (int i = 0; i < path.Count; i++)
                    {
                        if (i > 0) detail.Append(" → ");
                        detail.Append(path[i]);
                    }
                }
                _details.Add(detail.ToString());

                // Targeting & dice/path head pill on map (never added inside FightWindow).
                if (!combat && (_enabled || (_rollMoveEnabled && (!string.IsNullOrEmpty(rollText) || !string.IsNullOrEmpty(pathText)))))
                {
                    var label = monster.CharacterInst?.window?.Com_AttrInfo?.txt_Name;
                    if (label != null && !label.isDisposed && label.parent != null)
                    {
                        string pillText = _enabled ? compact : "";
                        if (_rollMoveEnabled && (!string.IsNullOrEmpty(rollText) || !string.IsNullOrEmpty(pathText)))
                        {
                            string actLine = string.IsNullOrEmpty(pathText)
                                ? rollText
                                : (string.IsNullOrEmpty(rollText) ? pathText : (rollText + " · " + pathText));
                            pillText = string.IsNullOrEmpty(pillText) ? actLine : (pillText + "\n" + actLine);
                        }
                        Show(label, pillText, preview.HasSingleTargetGroup);
                    }
                }

                // Numbered map step markers while monster is actively rolling/moving or briefly post-move.
                if (!combat && _rollMoveEnabled && _pathMarkersEnabled && hasAction && nodeDict != null && actionState.ChosenPath.Count > 0)
                {
                    bool activeTurn = actionState.RemainingSteps > 0
                        || currentTurnPlayer == input.MonsterId
                        || (nowReal - actionState.LastUpdatedReal) <= 2.5;
                    if (activeTurn)
                    {
                        var markers = MonsterActionPolicy.BuildStepMarkers(actionState.ChosenPath, input.CurrentNode, actionState.PursuitNodeId);
                        for (int m = 0; m < markers.Count; m++)
                        {
                            UnitLand land;
                            if (nodeDict.TryGetValue(markers[m].NodeId, out land) && land != null)
                            {
                                desiredMarkers.Add(new KeyValuePair<MonsterPathMarker, Vector3>(markers[m], land.transform.position + Vector3.up * 0.55f));
                            }
                        }
                    }
                }
            }

            PruneRows();
            SyncStepMarkers(desiredMarkers);
            _status = !_enabled ? "索敌显示已关闭"
                : (_details.Count == 0 ? "当前没有可推演的怪物" : "沿箭头前进 · 最大10格（含先行1格）；已联动投点与选路可视化。");
            _actionStatus = !_rollMoveEnabled ? "投点与移动路径可视已关闭"
                : (_actionLines.Count == 0 ? $"第 {room.Round} 轮 · 等待怪物投点与移动行动" : $"第 {room.Round} 轮怪物行动记录：\n" + string.Join("\n", _actionLines.ToArray()));
            if (_selected >= _details.Count) _selected = 0;
        }

        private void SyncStepMarkers(List<KeyValuePair<MonsterPathMarker, Vector3>> desired)
        {
            if (desired == null || desired.Count == 0 || GRoot.inst == null)
            {
                ClearStepMarkers();
                return;
            }

            while (_stepMarkers.Count < desired.Count)
            {
                var bg = new GGraph { name = "XINGSHU_MonsterStepMarkerBg", touchable = false };
                var txt = new GTextField
                {
                    name = "XINGSHU_MonsterStepMarkerTxt",
                    touchable = false,
                    singleLine = true,
                    autoSize = AutoSizeType.Both,
                    stroke = 1f,
                    strokeColor = new Color(0.08f, 0.08f, 0.12f, 0.9f),
                };
                int insertIdx = Math.Max(0, GRoot.inst.numChildren);
                GRoot.inst.AddChildAt(bg, insertIdx);
                GRoot.inst.AddChildAt(txt, insertIdx + 1);
                _stepMarkers.Add(new StepMarkerVisual { Back = bg, Text = txt });
            }
            for (int i = desired.Count; i < _stepMarkers.Count; i++)
            {
                _stepMarkers[i].Back.visible = false;
                _stepMarkers[i].Text.visible = false;
            }

            for (int i = 0; i < desired.Count; i++)
            {
                var item = desired[i];
                var vis = _stepMarkers[i];
                vis.WorldPos = item.Value;
                string labelText = item.Key.IsPursuitTarget
                    ? $"{item.Key.StepIndex} 追"
                    : (item.Key.IsDestination ? $"{item.Key.StepIndex} 终" : item.Key.StepIndex.ToString());
                var tf = vis.Text.textFormat;
                tf.size = XingShuMobileUIHelper.IsMobile ? 22 : 18;
                tf.bold = true;
                tf.color = Color.white;
                vis.Text.textFormat = tf;
                vis.Text.text = labelText;

                float w = Mathf.Max(26f, vis.Text.width + 12f);
                float h = Mathf.Max(22f, vis.Text.height + 6f);
                Color fill = item.Key.IsPursuitTarget
                    ? new Color(0.88f, 0.24f, 0.42f, 0.92f)
                    : (item.Key.IsDestination ? new Color(0.95f, 0.62f, 0.14f, 0.92f) : new Color(0.16f, 0.56f, 0.92f, 0.88f));
                vis.Back.DrawRoundRect(w, h, fill, new[] { 8f, 8f, 8f, 8f });
            }
            PlaceAllStepMarkers();
        }

        private void PlaceAllStepMarkers()
        {
            if (_stepMarkers.Count == 0 || GRoot.inst == null) return;
            Camera cam = Camera.main;
            for (int i = 0; i < _stepMarkers.Count; i++)
            {
                var vis = _stepMarkers[i];
                if (vis.Back == null || vis.Back.isDisposed || vis.Text == null || vis.Text.isDisposed) continue;
                if (cam == null)
                {
                    vis.Back.visible = false;
                    vis.Text.visible = false;
                    continue;
                }
                Vector3 scr = cam.WorldToScreenPoint(vis.WorldPos);
                if (scr.z <= 0.1f)
                {
                    vis.Back.visible = false;
                    vis.Text.visible = false;
                    continue;
                }
                Vector2 local = GRoot.inst.GlobalToLocal(new Vector2(scr.x, Screen.height - scr.y));
                float bx = local.x - vis.Back.width * 0.5f;
                float by = local.y - vis.Back.height * 0.5f;
                vis.Back.SetXY(bx, by);
                vis.Text.SetXY(bx + (vis.Back.width - vis.Text.width) * 0.5f, by + (vis.Back.height - vis.Text.height) * 0.5f);
                vis.Back.visible = true;
                vis.Text.visible = true;
            }
        }

        private void ClearStepMarkers()
        {
            for (int i = 0; i < _stepMarkers.Count; i++)
            {
                var vis = _stepMarkers[i];
                if (vis.Text != null && !vis.Text.isDisposed) vis.Text.Dispose();
                if (vis.Back != null && !vis.Back.isDisposed) vis.Back.Dispose();
            }
            _stepMarkers.Clear();
        }

        private static string PlayerTag(List<BattlePlayerData> players, long id, bool includeName)
        {
            foreach (var player in players)
                if (player?.player != null && player.player.Id == id)
                {
                    string slot = "P" + (player.player.Slot + 1);
                    if (!includeName) return slot;
                    string name = player.player.GetNick().Replace('[', '［').Replace(']', '］').Replace('\n', ' ');
                    return slot + " " + (name.Length > 8 ? name.Substring(0, 8) + "…" : name);
                }
            return "未知玩家";
        }

        private static int PlayerSlot(List<BattlePlayerData> players, long id)
        {
            foreach (var player in players)
                if (player?.player != null && player.player.Id == id && player.player.Slot >= 0 && player.player.Slot < 4)
                    return player.player.Slot;
            return int.MaxValue;
        }

        private void Show(GTextField anchor, string text, bool single)
        {
            _seen.Add(anchor);
            Row row;
            if (!_rows.TryGetValue(anchor, out row) || row.Text.isDisposed || row.Text.parent != anchor.parent)
            {
                if (row != null) Dispose(row);
                row = new Row { Anchor = anchor };
                row.Back = new GGraph { name = "XINGSHU_MonsterTargetBack", touchable = false };
                anchor.parent.AddChild(row.Back);
                row.Text = new GTextField
                {
                    name = "XINGSHU_MonsterTarget", touchable = false, singleLine = false,
                    autoSize = AutoSizeType.Both, stroke = 0f
                };
                anchor.parent.AddChild(row.Text);
                _rows[anchor] = row;
            }
            var format = row.Text.textFormat;
            format.font = anchor.textFormat.font;
            format.size = Mathf.Clamp(Mathf.RoundToInt(anchor.textFormat.size * 0.88f), 20, 28);
            format.color = new Color(0.16f, 0.13f, 0.18f);
            row.Text.textFormat = format;
            row.Text.text = text;
            float width = row.Text.width + 14f, height = row.Text.height + 6f;
            row.Back.DrawRoundRect(width, height, single ? new Color(1f, 0.94f, 0.76f, 0.94f) :
                new Color(1f, 0.87f, 0.93f, 0.94f), new[] { 6f, 6f, 6f, 6f });
            Place(row);
        }

        private static void Place(Row row)
        {
            if (row.Anchor.isDisposed || row.Text.isDisposed || row.Back.isDisposed || row.Anchor.parent == null) return;
            var kill = row.Anchor.parent.GetChild("XINGSHU_MonsterStatusBack");
            float bottom = kill != null && kill.visible ? kill.y : row.Anchor.y;
            row.Back.SetXY(row.Anchor.x - 2f, bottom - row.Back.height - 4f);
            row.Text.SetXY(row.Back.x + 7f, row.Back.y + 3f);
            row.Text.visible = row.Back.visible = row.Anchor.visible;
            row.Text.alpha = row.Back.alpha = row.Anchor.alpha;
        }

        private string CurrentSummary()
        {
            if (!_enabled) return "索敌显示已关闭";
            if (_details.Count == 0) return _status;
            return "[" + (_selected + 1) + "/" + _details.Count + "]  " + _status + "\n" + _details[_selected];
        }

        private string CurrentActionSummary()
        {
            return _actionStatus ?? "等待怪物投点与移动行动";
        }

        private void PruneRows()
        {
            _remove.Clear();
            foreach (var pair in _rows) if (!_seen.Contains(pair.Key) || pair.Key.isDisposed) _remove.Add(pair.Key);
            foreach (var anchor in _remove) { Dispose(_rows[anchor]); _rows.Remove(anchor); }
        }

        private static void Dispose(Row row)
        {
            if (row.Text != null && !row.Text.isDisposed) row.Text.Dispose();
            if (row.Back != null && !row.Back.isDisposed) row.Back.Dispose();
        }

        private void ClearRows()
        {
            foreach (var row in _rows.Values) Dispose(row);
            _rows.Clear();
            _seen.Clear();
            _remove.Clear();
        }

        private void ClearSession()
        {
            ClearRows();
            ClearStepMarkers();
            _details.Clear();
            _actionLines.Clear();
            _actionStates.Clear();
            _selected = 0;
            _attack.Clear();
        }

        private void OnDisable() { ClearSession(); RestoreActionRpcHooks(); _arrowBoost.RestoreAll(); }
        private void OnDestroy() { ClearSession(); RestoreActionRpcHooks(); _arrowBoost.RestoreAll(); if (_instance == this) _instance = null; }
    }
}
