using System;
using System.Collections.Generic;
using Core;
using Core.Scene;
using Core.Unit;
using FairyGUI;
using GameLogic;
using Tools;
using UI;
using UnityEngine;
using party.model;
using XingShuRuntime.CameraPort;
using XingShuRuntime.Core;
using XingShuRuntime.UI;

namespace XingShuRuntime.Utilities
{
/// <summary>
/// Standalone HUD module (#22 player-star-buffs) displaying filtered hero buffs
/// (疾行/移速, 角色专属/印记, 攻防战斗, 减益毒咒, 事件回复, 其他)
/// beside/below each left-side player avatar's star bar (UICom_PlayerInfo.list_Level).
/// </summary>
public sealed class PlayerStarBuffHud : MonoBehaviour
{
    private const float BuffRefreshIntervalSeconds = 0.20f;
    private const float BaseIconSize = 24f;
    private const float IconGap = 2f;
    private const float StripPadX = 4f;
    private const float StripPadY = 3f;

    internal struct BuffCatalogEntry
    {
        internal int BuffId;
        internal string Name;
        internal string Desc;
        internal StarBuffCategory Category;
        internal bool IsActiveInMatch;
        internal bool Enabled;
    }

    private sealed class IconCell
    {
        internal GComponent Root;
        internal GGraph Bg;
        internal GLoader Icon;
        internal GTextField NumText;
        internal GTextField RoundText;
        internal string LastIconUrl = "";
    }

    private sealed class StripSlot
    {
        internal long PlayerId;
        internal GComponent Panel;
        internal GGraph StripBg;
        internal readonly List<IconCell> Cells = new List<IconCell>(8);
        internal float Width;
        internal float Height;
        internal float AnchorX;
        internal float AnchorY;
        internal string LastSignature = "";
        internal bool ActiveInFrame;
        internal bool HasVisibleBuffs;
    }

    private static readonly int[] PropertyBuffIds = { 10004, 10005, 10006, 10008, 10009, 10012, 10013, 10014 };
    private static PlayerStarBuffHud _instance;

    private readonly Dictionary<long, StripSlot> _strips = new Dictionary<long, StripSlot>(4);
    private readonly List<BuffCatalogEntry> _cachedBaseCatalog = new List<BuffCatalogEntry>();
    private readonly HashSet<int> _cachedBaseIdSet = new HashSet<int>();
    private readonly Dictionary<int, StarBuffCategory> _categoryCache = new Dictionary<int, StarBuffCategory>();
    private bool _catalogBuilt;

    private bool _enabled = XingShuConfigKeys.PlayerStarBuffsEnabledDefault;
    private int _anchorMode = XingShuConfigKeys.PlayerStarBuffsAnchorModeDefault;
    private float _offsetX = XingShuConfigKeys.PlayerStarBuffsOffsetXDefault;
    private float _offsetY = XingShuConfigKeys.PlayerStarBuffsOffsetYDefault;
    private float _scale = XingShuConfigKeys.PlayerStarBuffsScaleDefault;
    private int _maxIcons = XingShuConfigKeys.PlayerStarBuffsMaxIconsDefault;
    private bool _locked = XingShuConfigKeys.PlayerStarBuffsLockedDefault;

    private bool _catMove = XingShuConfigKeys.PlayerStarBuffsCatMoveDefault;
    private bool _catHero = XingShuConfigKeys.PlayerStarBuffsCatHeroDefault;
    private bool _catCombat = XingShuConfigKeys.PlayerStarBuffsCatCombatDefault;
    private bool _catDebuff = XingShuConfigKeys.PlayerStarBuffsCatDebuffDefault;
    private bool _catEvent = XingShuConfigKeys.PlayerStarBuffsCatEventDefault;
    private bool _catOther = XingShuConfigKeys.PlayerStarBuffsCatOtherDefault;

    private HashSet<int> _disabledIds = new HashSet<int>();
    private HashSet<int> _enabledIds = new HashSet<int>();

    private GComponent _attachedHost;
    private float _nextBuffRefresh;
    private long _draggingPlayerId;
    private static int _revision;

    internal static int Revision => _revision;

    internal static bool IsEnabled => _instance != null
        ? _instance._enabled
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsEnabled, XingShuConfigKeys.PlayerStarBuffsEnabledDefault);

    internal static int AnchorMode => _instance != null
        ? _instance._anchorMode
        : Mathf.Clamp(XingShuConfigService.GetInt(XingShuConfigKeys.PlayerStarBuffsAnchorMode, XingShuConfigKeys.PlayerStarBuffsAnchorModeDefault), 0, 2);

    internal static float OffsetX => _instance != null
        ? _instance._offsetX
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetX, XingShuConfigKeys.PlayerStarBuffsOffsetXDefault),
            XingShuConfigKeys.PlayerStarBuffsOffsetXMin, XingShuConfigKeys.PlayerStarBuffsOffsetXMax, XingShuConfigKeys.PlayerStarBuffsOffsetXDefault);

    internal static float OffsetY => _instance != null
        ? _instance._offsetY
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetY, XingShuConfigKeys.PlayerStarBuffsOffsetYDefault),
            XingShuConfigKeys.PlayerStarBuffsOffsetYMin, XingShuConfigKeys.PlayerStarBuffsOffsetYMax, XingShuConfigKeys.PlayerStarBuffsOffsetYDefault);

    internal static float Scale => _instance != null
        ? _instance._scale
        : FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerStarBuffsScale, XingShuConfigKeys.PlayerStarBuffsScaleDefault),
            XingShuConfigKeys.PlayerStarBuffsScaleMin, XingShuConfigKeys.PlayerStarBuffsScaleMax, XingShuConfigKeys.PlayerStarBuffsScaleDefault);

    internal static int MaxIcons => _instance != null
        ? _instance._maxIcons
        : Mathf.Clamp(
            XingShuConfigService.GetInt(XingShuConfigKeys.PlayerStarBuffsMaxIcons, XingShuConfigKeys.PlayerStarBuffsMaxIconsDefault),
            XingShuConfigKeys.PlayerStarBuffsMaxIconsMin, XingShuConfigKeys.PlayerStarBuffsMaxIconsMax);

    internal static bool Locked => _instance != null
        ? _instance._locked
        : XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsLocked, XingShuConfigKeys.PlayerStarBuffsLockedDefault);

    internal static bool GetCategoryEnabled(StarBuffCategory category)
    {
        if (_instance != null)
        {
            switch (category)
            {
                case StarBuffCategory.Move: return _instance._catMove;
                case StarBuffCategory.Hero: return _instance._catHero;
                case StarBuffCategory.Combat: return _instance._catCombat;
                case StarBuffCategory.Debuff: return _instance._catDebuff;
                case StarBuffCategory.Event: return _instance._catEvent;
                default: return _instance._catOther;
            }
        }
        switch (category)
        {
            case StarBuffCategory.Move: return XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatMove, XingShuConfigKeys.PlayerStarBuffsCatMoveDefault);
            case StarBuffCategory.Hero: return XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatHero, XingShuConfigKeys.PlayerStarBuffsCatHeroDefault);
            case StarBuffCategory.Combat: return XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatCombat, XingShuConfigKeys.PlayerStarBuffsCatCombatDefault);
            case StarBuffCategory.Debuff: return XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatDebuff, XingShuConfigKeys.PlayerStarBuffsCatDebuffDefault);
            case StarBuffCategory.Event: return XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatEvent, XingShuConfigKeys.PlayerStarBuffsCatEventDefault);
            default: return XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatOther, XingShuConfigKeys.PlayerStarBuffsCatOtherDefault);
        }
    }

    internal static int EnabledCategoryCount
    {
        get
        {
            int count = 0;
            if (GetCategoryEnabled(StarBuffCategory.Move)) count++;
            if (GetCategoryEnabled(StarBuffCategory.Hero)) count++;
            if (GetCategoryEnabled(StarBuffCategory.Combat)) count++;
            if (GetCategoryEnabled(StarBuffCategory.Debuff)) count++;
            if (GetCategoryEnabled(StarBuffCategory.Event)) count++;
            if (GetCategoryEnabled(StarBuffCategory.Other)) count++;
            return count;
        }
    }

    internal static int OverrideCount
    {
        get
        {
            if (_instance != null)
            {
                return _instance._disabledIds.Count + _instance._enabledIds.Count;
            }
            var dis = PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, XingShuConfigKeys.PlayerStarBuffsDisabledIdsDefault));
            var en = PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, XingShuConfigKeys.PlayerStarBuffsEnabledIdsDefault));
            return dis.Count + en.Count;
        }
    }

    internal static void SetEnabled(bool value)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsEnabled, value);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._enabled = value;
            _instance.ForceRefresh();
            if (!value) _instance.HideAllStrips();
        }
        _revision++;
    }

    internal static void SetAnchorMode(int mode)
    {
        int clamped = Mathf.Clamp(mode, 0, 2);
        XingShuConfigService.SetInt(XingShuConfigKeys.PlayerStarBuffsAnchorMode, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._anchorMode = clamped;
            if (clamped == 0)
            {
                SetOffset(4f, 0f);
            }
            else if (clamped == 1)
            {
                SetOffset(-4f, 0f);
            }
            else
            {
                SetOffset(0f, 2f);
            }
        }
        _revision++;
    }

    internal static void SetOffset(float offX, float offY)
    {
        float x = FloatingHintLayoutMath.FiniteClamp(
            offX, XingShuConfigKeys.PlayerStarBuffsOffsetXMin, XingShuConfigKeys.PlayerStarBuffsOffsetXMax, XingShuConfigKeys.PlayerStarBuffsOffsetXDefault);
        float y = FloatingHintLayoutMath.FiniteClamp(
            offY, XingShuConfigKeys.PlayerStarBuffsOffsetYMin, XingShuConfigKeys.PlayerStarBuffsOffsetYMax, XingShuConfigKeys.PlayerStarBuffsOffsetYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetX, x);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetY, y);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._offsetX = x;
            _instance._offsetY = y;
        }
        _revision++;
    }

    internal static void SetScale(float scale)
    {
        float clamped = FloatingHintLayoutMath.FiniteClamp(
            scale, XingShuConfigKeys.PlayerStarBuffsScaleMin, XingShuConfigKeys.PlayerStarBuffsScaleMax, XingShuConfigKeys.PlayerStarBuffsScaleDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerStarBuffsScale, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._scale = clamped;
        }
        _revision++;
    }

    internal static void SetMaxIcons(int maxIcons)
    {
        int clamped = Mathf.Clamp(maxIcons, XingShuConfigKeys.PlayerStarBuffsMaxIconsMin, XingShuConfigKeys.PlayerStarBuffsMaxIconsMax);
        XingShuConfigService.SetInt(XingShuConfigKeys.PlayerStarBuffsMaxIcons, clamped);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._maxIcons = clamped;
            _instance.ForceRefresh();
        }
        _revision++;
    }

    internal static void SetLocked(bool locked)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsLocked, locked);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._locked = locked;
            _instance._draggingPlayerId = 0L;
            _instance.RefreshInteractionStates(modalOpen: false);
        }
        _revision++;
    }

    internal static void SetCategoryEnabled(StarBuffCategory category, bool enabled)
    {
        string key;
        switch (category)
        {
            case StarBuffCategory.Move: key = XingShuConfigKeys.PlayerStarBuffsCatMove; break;
            case StarBuffCategory.Hero: key = XingShuConfigKeys.PlayerStarBuffsCatHero; break;
            case StarBuffCategory.Combat: key = XingShuConfigKeys.PlayerStarBuffsCatCombat; break;
            case StarBuffCategory.Debuff: key = XingShuConfigKeys.PlayerStarBuffsCatDebuff; break;
            case StarBuffCategory.Event: key = XingShuConfigKeys.PlayerStarBuffsCatEvent; break;
            default: key = XingShuConfigKeys.PlayerStarBuffsCatOther; break;
        }
        XingShuConfigService.SetBool(key, enabled);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            switch (category)
            {
                case StarBuffCategory.Move: _instance._catMove = enabled; break;
                case StarBuffCategory.Hero: _instance._catHero = enabled; break;
                case StarBuffCategory.Combat: _instance._catCombat = enabled; break;
                case StarBuffCategory.Debuff: _instance._catDebuff = enabled; break;
                case StarBuffCategory.Event: _instance._catEvent = enabled; break;
                default: _instance._catOther = enabled; break;
            }
            _instance.ForceRefresh();
        }
        _revision++;
    }

    internal static void SetAllCategories(bool enabled)
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatMove, enabled);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatHero, enabled);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatCombat, enabled);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatDebuff, enabled);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatEvent, enabled);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatOther, enabled);
        XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, "");
        XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, "");
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance._catMove = enabled;
            _instance._catHero = enabled;
            _instance._catCombat = enabled;
            _instance._catDebuff = enabled;
            _instance._catEvent = enabled;
            _instance._catOther = enabled;
            _instance._disabledIds.Clear();
            _instance._enabledIds.Clear();
            _instance.ForceRefresh();
        }
        _revision++;
    }

    internal static void SetBuffOverride(int buffId, StarBuffCategory category, bool wantVisible)
    {
        if (buffId <= 0) return;
        bool catDefault = GetCategoryEnabled(category);
        if (_instance != null)
        {
            if (wantVisible == catDefault)
            {
                _instance._disabledIds.Remove(buffId);
                _instance._enabledIds.Remove(buffId);
            }
            else if (wantVisible)
            {
                _instance._enabledIds.Add(buffId);
                _instance._disabledIds.Remove(buffId);
            }
            else
            {
                _instance._disabledIds.Add(buffId);
                _instance._enabledIds.Remove(buffId);
            }
            XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, PlayerStarBuffPolicy.SerializeIdSet(_instance._disabledIds));
            XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, PlayerStarBuffPolicy.SerializeIdSet(_instance._enabledIds));
            XingShuConfigService.Save();
            _instance.ForceRefresh();
        }
        else
        {
            var dis = PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, XingShuConfigKeys.PlayerStarBuffsDisabledIdsDefault));
            var en = PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, XingShuConfigKeys.PlayerStarBuffsEnabledIdsDefault));
            if (wantVisible == catDefault)
            {
                dis.Remove(buffId);
                en.Remove(buffId);
            }
            else if (wantVisible)
            {
                en.Add(buffId);
                dis.Remove(buffId);
            }
            else
            {
                dis.Add(buffId);
                en.Remove(buffId);
            }
            XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, PlayerStarBuffPolicy.SerializeIdSet(dis));
            XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, PlayerStarBuffPolicy.SerializeIdSet(en));
            XingShuConfigService.Save();
        }
        _revision++;
    }

    internal static void ResetLayout()
    {
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsEnabled, XingShuConfigKeys.PlayerStarBuffsEnabledDefault);
        XingShuConfigService.SetInt(XingShuConfigKeys.PlayerStarBuffsAnchorMode, XingShuConfigKeys.PlayerStarBuffsAnchorModeDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetX, XingShuConfigKeys.PlayerStarBuffsOffsetXDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetY, XingShuConfigKeys.PlayerStarBuffsOffsetYDefault);
        XingShuConfigService.SetFloat(XingShuConfigKeys.PlayerStarBuffsScale, XingShuConfigKeys.PlayerStarBuffsScaleDefault);
        XingShuConfigService.SetInt(XingShuConfigKeys.PlayerStarBuffsMaxIcons, XingShuConfigKeys.PlayerStarBuffsMaxIconsDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsLocked, XingShuConfigKeys.PlayerStarBuffsLockedDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatMove, XingShuConfigKeys.PlayerStarBuffsCatMoveDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatHero, XingShuConfigKeys.PlayerStarBuffsCatHeroDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatCombat, XingShuConfigKeys.PlayerStarBuffsCatCombatDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatDebuff, XingShuConfigKeys.PlayerStarBuffsCatDebuffDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatEvent, XingShuConfigKeys.PlayerStarBuffsCatEventDefault);
        XingShuConfigService.SetBool(XingShuConfigKeys.PlayerStarBuffsCatOther, XingShuConfigKeys.PlayerStarBuffsCatOtherDefault);
        XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, XingShuConfigKeys.PlayerStarBuffsDisabledIdsDefault);
        XingShuConfigService.SetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, XingShuConfigKeys.PlayerStarBuffsEnabledIdsDefault);
        XingShuConfigService.Save();
        if (_instance != null)
        {
            _instance.LoadConfig();
            _instance._draggingPlayerId = 0L;
            _instance.ForceRefresh();
            _instance.RefreshInteractionStates(modalOpen: false);
        }
        _revision++;
    }

    internal static List<BuffCatalogEntry> GetCatalogEntries(string searchQuery, int filterTab)
    {
        var results = new List<BuffCatalogEntry>();
        var activeIds = CollectActiveMatchBuffIds();

        var sourceList = _instance != null ? _instance.EnsureBaseCatalog() : BuildStaticCatalogFallback(activeIds);
        var seen = new HashSet<int>();
        string query = string.IsNullOrEmpty(searchQuery) ? "" : searchQuery.Trim();

        // Collect all candidates first (base catalog + any live buff ID not already in base catalog)
        var all = new List<BuffCatalogEntry>(sourceList.Count + activeIds.Count);
        for (int i = 0; i < sourceList.Count; i++)
        {
            var e = sourceList[i];
            seen.Add(e.BuffId);
            e.IsActiveInMatch = activeIds.Contains(e.BuffId);
            e.Enabled = ShouldShowBuffId(e.BuffId, e.Category);
            all.Add(e);
        }

        foreach (int activeId in activeIds)
        {
            if (seen.Contains(activeId)) continue;
            seen.Add(activeId);
            var e = BuildEntryFromId(activeId, isPropertyBuff: Array.IndexOf(PropertyBuffIds, activeId) >= 0);
            e.IsActiveInMatch = true;
            e.Enabled = ShouldShowBuffId(e.BuffId, e.Category);
            all.Add(e);
        }

        for (int i = 0; i < all.Count; i++)
        {
            var item = all[i];
            if (!MatchesFilterTab(item, filterTab)) continue;
            if (!string.IsNullOrEmpty(query) && !MatchesSearch(item, query)) continue;
            results.Add(item);
        }

        results.Sort((a, b) =>
        {
            int act = b.IsActiveInMatch.CompareTo(a.IsActiveInMatch);
            if (act != 0) return act;
            int cat = ((int)a.Category).CompareTo((int)b.Category);
            if (cat != 0) return cat;
            return a.BuffId.CompareTo(b.BuffId);
        });
        return results;
    }

    private static bool MatchesFilterTab(BuffCatalogEntry entry, int filterTab)
    {
        switch (filterTab)
        {
            case 1: return entry.IsActiveInMatch;
            case 2: return entry.Category == StarBuffCategory.Move;
            case 3: return entry.Category == StarBuffCategory.Hero;
            case 4: return entry.Category == StarBuffCategory.Combat;
            case 5: return entry.Category == StarBuffCategory.Debuff;
            case 6: return entry.Category == StarBuffCategory.Event || entry.Category == StarBuffCategory.Other;
            default: return true;
        }
    }

    private static bool MatchesSearch(BuffCatalogEntry entry, string query)
    {
        if (entry.BuffId.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (!string.IsNullOrEmpty(entry.Name) && entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (!string.IsNullOrEmpty(entry.Desc) && entry.Desc.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static HashSet<int> CollectActiveMatchBuffIds()
    {
        var ids = new HashSet<int>();
        try
        {
            if (!SimpleSingletonProvider<GameLogicManager>.hasInstance) return ids;
            var players = SimpleSingletonProvider<GameLogicManager>.inst.battle?.PlayerDatas;
            if (players == null) return ids;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.characterType != CharacterType.Hero || p.buffContainer == null) continue;
                var pair = p.buffContainer.GetShowBuffs(p, isRegister: false);
                var buffs = pair.Item1;
                var propBuffs = pair.Item2;
                if (buffs != null)
                {
                    for (int j = 0; j < buffs.Count; j++)
                    {
                        if (buffs[j] != null && buffs[j].BuffId > 0) ids.Add(buffs[j].BuffId);
                    }
                }
                if (propBuffs != null)
                {
                    for (int j = 0; j < propBuffs.Count; j++)
                    {
                        if (propBuffs[j] != null && propBuffs[j].buffId > 0) ids.Add(propBuffs[j].buffId);
                    }
                }
            }
        }
        catch
        {
        }
        return ids;
    }

    public static void Install(GameObject host)
    {
        if (_instance != null || host == null) return;
        _instance = host.AddComponent<PlayerStarBuffHud>();
        _instance.LoadConfig();
    }

    private void LoadConfig()
    {
        _enabled = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsEnabled, XingShuConfigKeys.PlayerStarBuffsEnabledDefault);
        _anchorMode = Mathf.Clamp(XingShuConfigService.GetInt(XingShuConfigKeys.PlayerStarBuffsAnchorMode, XingShuConfigKeys.PlayerStarBuffsAnchorModeDefault), 0, 2);
        _offsetX = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetX, XingShuConfigKeys.PlayerStarBuffsOffsetXDefault),
            XingShuConfigKeys.PlayerStarBuffsOffsetXMin, XingShuConfigKeys.PlayerStarBuffsOffsetXMax, XingShuConfigKeys.PlayerStarBuffsOffsetXDefault);
        _offsetY = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerStarBuffsOffsetY, XingShuConfigKeys.PlayerStarBuffsOffsetYDefault),
            XingShuConfigKeys.PlayerStarBuffsOffsetYMin, XingShuConfigKeys.PlayerStarBuffsOffsetYMax, XingShuConfigKeys.PlayerStarBuffsOffsetYDefault);
        _scale = FloatingHintLayoutMath.FiniteClamp(
            XingShuConfigService.GetFloat(XingShuConfigKeys.PlayerStarBuffsScale, XingShuConfigKeys.PlayerStarBuffsScaleDefault),
            XingShuConfigKeys.PlayerStarBuffsScaleMin, XingShuConfigKeys.PlayerStarBuffsScaleMax, XingShuConfigKeys.PlayerStarBuffsScaleDefault);
        _maxIcons = Mathf.Clamp(
            XingShuConfigService.GetInt(XingShuConfigKeys.PlayerStarBuffsMaxIcons, XingShuConfigKeys.PlayerStarBuffsMaxIconsDefault),
            XingShuConfigKeys.PlayerStarBuffsMaxIconsMin, XingShuConfigKeys.PlayerStarBuffsMaxIconsMax);
        _locked = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsLocked, XingShuConfigKeys.PlayerStarBuffsLockedDefault);

        _catMove = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatMove, XingShuConfigKeys.PlayerStarBuffsCatMoveDefault);
        _catHero = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatHero, XingShuConfigKeys.PlayerStarBuffsCatHeroDefault);
        _catCombat = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatCombat, XingShuConfigKeys.PlayerStarBuffsCatCombatDefault);
        _catDebuff = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatDebuff, XingShuConfigKeys.PlayerStarBuffsCatDebuffDefault);
        _catEvent = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatEvent, XingShuConfigKeys.PlayerStarBuffsCatEventDefault);
        _catOther = XingShuConfigService.GetBool(XingShuConfigKeys.PlayerStarBuffsCatOther, XingShuConfigKeys.PlayerStarBuffsCatOtherDefault);

        _disabledIds = PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, XingShuConfigKeys.PlayerStarBuffsDisabledIdsDefault));
        _enabledIds = PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, XingShuConfigKeys.PlayerStarBuffsEnabledIdsDefault));
    }

    private void ForceRefresh()
    {
        _nextBuffRefresh = 0f;
        foreach (var slot in _strips.Values)
        {
            slot.LastSignature = "";
        }
    }

    private void LateUpdate()
    {
        try
        {
            Tick();
        }
        catch (Exception)
        {
            ClearAll();
        }
    }

    private void Tick()
    {
        if (!_enabled
            || !SimpleSingletonProvider<SceneManager>.hasInstance
            || SimpleSingletonProvider<SceneManager>.inst.currentType == null
            || SimpleSingletonProvider<SceneManager>.inst.currentType.Value != SceneType.Battle
            || !SimpleSingletonProvider<GameLogicManager>.hasInstance
            || !SimpleSingletonProvider<UIManager>.hasInstance)
        {
            ClearAll();
            return;
        }

        var uiMgr = SimpleSingletonProvider<UIManager>.inst;
        var logic = SimpleSingletonProvider<GameLogicManager>.inst;
        var fight = logic.fight?.battleFightData;
        if ((fight != null && !fight.isEnd) || ShownNativeWindows.Find<FightWindow>(uiMgr) != null)
        {
            HideAllStrips();
            return;
        }

        var battleInfoUi = logic.battle?.battleInfo?.UIObject;
        var battlePlayer = battleInfoUi?.com_BattlePlayer;
        var container = battlePlayer?.com_Container;
        if (battleInfoUi == null || battleInfoUi.isDisposed || !IsVisibleTree(battleInfoUi)
            || battlePlayer == null || !IsVisibleTree(battlePlayer)
            || container == null || !IsVisibleTree(container)
            || battleInfoUi.width < 320f || battleInfoUi.height < 240f)
        {
            _draggingPlayerId = 0L;
            HideAllStrips();
            return;
        }

        GComponent host = battlePlayer.parent == battleInfoUi ? (GComponent)battleInfoUi : battlePlayer;
        GObject anchorSibling = host == battleInfoUi ? (GObject)battlePlayer : container;
        if (_attachedHost != host)
        {
            DisposeAllStrips();
            _draggingPlayerId = 0L;
            _attachedHost = host;
        }

        bool refreshBuffs = Time.unscaledTime >= _nextBuffRefresh;
        if (refreshBuffs)
        {
            _nextBuffRefresh = Time.unscaledTime + BuffRefreshIntervalSeconds;
        }

        bool modalOpen = XingShuUICenter.IsOpen || HandCardVisibility.BlockingWindow != null;
        BeginFrame();

        UIBattleInfo_Button_PlayerInfo[] slots =
        {
            container.com_Player_1,
            container.com_Player_2,
            container.com_Player_3,
            container.com_Player_4
        };

        for (int i = 0; i < slots.Length; i++)
        {
            var head = slots[i];
            if (head == null || head.isDisposed || !IsVisibleTree(head)) continue;
            var p = head.PlayerData;
            if (p == null || p.player?.Hero == null || p.characterType != CharacterType.Hero) continue;
            if (!TryComputeStarRect(head, host, out float starMinX, out float starMinY, out float starW, out float starH)) continue;

            long pid = p.player.Id;
            StripSlot strip = EnsureStripSlot(host, anchorSibling, pid, modalOpen);
            strip.ActiveInFrame = true;

            bool interactive = !_locked && !modalOpen;
            if (strip.Panel.touchable != interactive)
            {
                strip.Panel.touchable = interactive;
                strip.Panel.opaque = interactive;
                strip.Panel.draggable = interactive;
            }
            if (!interactive && _draggingPlayerId == pid)
            {
                _draggingPlayerId = 0L;
            }

            if (refreshBuffs || string.IsNullOrEmpty(strip.LastSignature))
            {
                RebuildStripContent(strip, p);
            }

            if (!strip.HasVisibleBuffs || strip.Width <= 0f)
            {
                strip.Panel.visible = false;
                continue;
            }

            PlayerStarBuffPolicy.ComputeAnchorPosition(
                _anchorMode,
                starMinX, starMinY, starW, starH,
                strip.Width * _scale, strip.Height * _scale,
                0f, 0f,
                out float baseAnchorX, out float baseAnchorY);

            strip.AnchorX = baseAnchorX;
            strip.AnchorY = baseAnchorY;

            if (strip.Panel != null && !strip.Panel.isDisposed)
            {
                strip.Panel.visible = true;
                strip.Panel.SetScale(_scale, _scale);
                if (_draggingPlayerId != pid)
                {
                    FloatingHintLayoutMath.ClampXYToBounds(
                        host.width, host.height,
                        strip.Width, strip.Height,
                        _scale,
                        baseAnchorX + _offsetX, baseAnchorY + _offsetY,
                        4f,
                        out float cx, out float cy);
                    strip.Panel.SetXY(cx, cy);
                }
            }
        }

        HideInactiveStrips();
    }

    private struct ResolvedBuffItem
    {
        internal int BuffId;
        internal string IconUrl;
        internal int Num;
        internal int KeepRound;
    }

    private void RebuildStripContent(StripSlot strip, BattlePlayerData p)
    {
        if (p.buffContainer == null)
        {
            strip.HasVisibleBuffs = false;
            strip.LastSignature = "EMPTY";
            return;
        }

        var pair = p.buffContainer.GetShowBuffs(p, isRegister: false);
        var buffs = pair.Item1;
        var propBuffs = pair.Item2;
        var items = new List<ResolvedBuffItem>(8);

        if (propBuffs != null)
        {
            for (int i = 0; i < propBuffs.Count && items.Count < _maxIcons; i++)
            {
                var pd = propBuffs[i];
                if (pd == null || pd.buffId <= 0 || pd.property == null || pd.property.Value <= 0) continue;
                var cfg = pd.buffId.GetBuffConfigure();
                if (cfg == null || string.IsNullOrEmpty(cfg.Icon)) continue;
                var cat = GetCategoryForBuff(pd.buffId, cfg, isPropertyBuff: true);
                if (!ShouldShowBuffId(pd.buffId, cat)) continue;
                items.Add(new ResolvedBuffItem
                {
                    BuffId = pd.buffId,
                    IconUrl = cfg.Icon,
                    Num = pd.property.Value,
                    KeepRound = 0,
                });
            }
        }

        if (buffs != null)
        {
            for (int i = 0; i < buffs.Count && items.Count < _maxIcons; i++)
            {
                var b = buffs[i];
                if (b == null || b.BuffId <= 0) continue;
                var cfg = b.BuffId.GetBuffConfigure();
                if (cfg == null || string.IsNullOrEmpty(cfg.Icon)) continue;
                var cat = GetCategoryForBuff(b.BuffId, cfg, isPropertyBuff: false);
                if (!ShouldShowBuffId(b.BuffId, cat)) continue;
                items.Add(new ResolvedBuffItem
                {
                    BuffId = b.BuffId,
                    IconUrl = cfg.Icon,
                    Num = b.Progress,
                    KeepRound = b.KeepRound,
                });
            }
        }

        if (items.Count == 0)
        {
            strip.HasVisibleBuffs = false;
            strip.Width = 0f;
            strip.Height = 0f;
            strip.LastSignature = "NONE";
            for (int i = 0; i < strip.Cells.Count; i++)
            {
                strip.Cells[i].Root.visible = false;
            }
            return;
        }

        var sigParts = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            sigParts[i] = items[i].BuffId + ":" + items[i].Num + ":" + items[i].KeepRound;
        }
        string sig = string.Join("|", sigParts);
        strip.HasVisibleBuffs = true;
        if (string.Equals(strip.LastSignature, sig, StringComparison.Ordinal))
        {
            return;
        }
        strip.LastSignature = sig;

        float totalW = StripPadX * 2f + items.Count * BaseIconSize + Math.Max(0, items.Count - 1) * IconGap;
        float totalH = StripPadY * 2f + BaseIconSize;
        strip.Width = totalW;
        strip.Height = totalH;
        strip.Panel.SetSize(totalW, totalH);
        strip.StripBg.SetSize(totalW, totalH);
        strip.StripBg.DrawRoundRect(totalW, totalH, new Color(0.06f, 0.08f, 0.13f, 0.80f), new[] { 6f, 6f, 6f, 6f });

        for (int i = 0; i < items.Count; i++)
        {
            IconCell cell = EnsureCell(strip, i);
            var item = items[i];
            float cx = StripPadX + i * (BaseIconSize + IconGap);
            cell.Root.visible = true;
            cell.Root.SetXY(cx, StripPadY);
            if (!string.Equals(cell.LastIconUrl, item.IconUrl, StringComparison.Ordinal))
            {
                cell.Icon.url = item.IconUrl;
                cell.LastIconUrl = item.IconUrl;
            }
            if (item.Num > 0)
            {
                cell.NumText.visible = true;
                cell.NumText.text = item.Num.ToString();
            }
            else
            {
                cell.NumText.visible = false;
            }
            if (item.KeepRound > 0)
            {
                cell.RoundText.visible = true;
                cell.RoundText.text = item.KeepRound.ToString();
            }
            else
            {
                cell.RoundText.visible = false;
            }
        }

        for (int i = items.Count; i < strip.Cells.Count; i++)
        {
            strip.Cells[i].Root.visible = false;
        }
    }

    private StarBuffCategory GetCategoryForBuff(int buffId, BuffInfoConfigure cfg, bool isPropertyBuff)
    {
        if (_categoryCache.TryGetValue(buffId, out var cached))
        {
            return cached;
        }
        bool hasPoisonOrCurse = false;
        if (cfg != null && cfg.BuffTagType != null)
        {
            for (int i = 0; i < cfg.BuffTagType.Count; i++)
            {
                var tag = cfg.BuffTagType[i];
                if (tag == BuffTagType.Poison || tag == BuffTagType.Curse)
                {
                    hasPoisonOrCurse = true;
                    break;
                }
            }
        }
        string name = SafeGetLocal(cfg != null ? cfg.NameId : 0);
        string desc = SafeGetLocal(cfg != null ? cfg.DescId : 0);
        int buffTypeVal = cfg != null ? (int)cfg.BuffType : 0;
        StarBuffCategory cat = PlayerStarBuffPolicy.ClassifyBuff(buffId, buffTypeVal, hasPoisonOrCurse, isPropertyBuff, name, desc);
        _categoryCache[buffId] = cat;
        return cat;
    }

    private static string SafeGetLocal(int id)
    {
        if (id <= 0) return "";
        try
        {
            return id.GetLocal(UIStringType.Buff) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool ShouldShowBuffId(int buffId, StarBuffCategory category)
    {
        if (_instance != null)
        {
            return PlayerStarBuffPolicy.ShouldShowBuff(
                buffId, category,
                _instance._catMove, _instance._catHero, _instance._catCombat,
                _instance._catDebuff, _instance._catEvent, _instance._catOther,
                _instance._disabledIds, _instance._enabledIds);
        }
        return PlayerStarBuffPolicy.ShouldShowBuff(
            buffId, category,
            GetCategoryEnabled(StarBuffCategory.Move),
            GetCategoryEnabled(StarBuffCategory.Hero),
            GetCategoryEnabled(StarBuffCategory.Combat),
            GetCategoryEnabled(StarBuffCategory.Debuff),
            GetCategoryEnabled(StarBuffCategory.Event),
            GetCategoryEnabled(StarBuffCategory.Other),
            PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsDisabledIds, XingShuConfigKeys.PlayerStarBuffsDisabledIdsDefault)),
            PlayerStarBuffPolicy.ParseIdSet(XingShuConfigService.GetString(XingShuConfigKeys.PlayerStarBuffsEnabledIds, XingShuConfigKeys.PlayerStarBuffsEnabledIdsDefault)));
    }

    private List<BuffCatalogEntry> EnsureBaseCatalog()
    {
        if (_catalogBuilt) return _cachedBaseCatalog;
        BuildCatalogInto(_cachedBaseCatalog, _cachedBaseIdSet, null);
        _catalogBuilt = _cachedBaseCatalog.Count > 0;
        return _cachedBaseCatalog;
    }

    private static List<BuffCatalogEntry> BuildStaticCatalogFallback(HashSet<int> activeIds)
    {
        var list = new List<BuffCatalogEntry>();
        var idSet = new HashSet<int>();
        BuildCatalogInto(list, idSet, activeIds);
        return list;
    }

    private static void BuildCatalogInto(List<BuffCatalogEntry> target, HashSet<int> idSet, HashSet<int> activeIds)
    {
        try
        {
            var infos = StaticConfigure.Buff?.Infos;
            if (infos != null)
            {
                for (int i = 0; i < infos.Count; i++)
                {
                    var cfg = infos[i];
                    if (cfg == null || cfg.Id <= 0 || !cfg.IsShow || string.IsNullOrEmpty(cfg.Icon)) continue;
                    if (!idSet.Add(cfg.Id)) continue;
                    bool isProp = Array.IndexOf(PropertyBuffIds, cfg.Id) >= 0;
                    var entry = BuildEntryFromConfig(cfg, isProp);
                    if (activeIds != null && activeIds.Contains(entry.BuffId)) entry.IsActiveInMatch = true;
                    target.Add(entry);
                }
            }
            for (int i = 0; i < PropertyBuffIds.Length; i++)
            {
                int pid = PropertyBuffIds[i];
                if (!idSet.Add(pid)) continue;
                var entry = BuildEntryFromId(pid, isPropertyBuff: true);
                if (activeIds != null && activeIds.Contains(pid)) entry.IsActiveInMatch = true;
                target.Add(entry);
            }
        }
        catch
        {
        }
    }

    private static BuffCatalogEntry BuildEntryFromId(int buffId, bool isPropertyBuff)
    {
        BuffInfoConfigure cfg = null;
        try { cfg = buffId.GetBuffConfigure(); } catch { }
        return cfg != null
            ? BuildEntryFromConfig(cfg, isPropertyBuff)
            : new BuffCatalogEntry
            {
                BuffId = buffId,
                Name = "Buff #" + buffId,
                Desc = "",
                Category = StarBuffCategory.Other,
                Enabled = ShouldShowBuffId(buffId, StarBuffCategory.Other),
            };
    }

    private static BuffCatalogEntry BuildEntryFromConfig(BuffInfoConfigure cfg, bool isPropertyBuff)
    {
        string name = SafeGetLocal(cfg.NameId);
        if (string.IsNullOrEmpty(name)) name = "Buff #" + cfg.Id;
        string desc = SafeGetLocal(cfg.DescId);
        StarBuffCategory cat = _instance != null
            ? _instance.GetCategoryForBuff(cfg.Id, cfg, isPropertyBuff)
            : PlayerStarBuffPolicy.ClassifyBuff(cfg.Id, (int)cfg.BuffType, false, isPropertyBuff, name, desc);
        return new BuffCatalogEntry
        {
            BuffId = cfg.Id,
            Name = name,
            Desc = desc,
            Category = cat,
            Enabled = ShouldShowBuffId(cfg.Id, cat),
        };
    }

    private StripSlot EnsureStripSlot(GComponent host, GObject anchorSibling, long playerId, bool modalOpen)
    {
        int targetIndex = anchorSibling != null && anchorSibling.parent == host
            ? Math.Min(host.numChildren, host.GetChildIndex(anchorSibling) + 1)
            : host.numChildren;

        if (_strips.TryGetValue(playerId, out var slot)
            && slot.Panel != null && !slot.Panel.isDisposed && slot.Panel.parent == host)
        {
            if (anchorSibling != null && anchorSibling.parent == host)
            {
                int desired = Math.Min(host.numChildren - 1, host.GetChildIndex(anchorSibling) + 1);
                if (host.GetChildIndex(slot.Panel) != desired)
                {
                    host.SetChildIndex(slot.Panel, desired);
                }
            }
            return slot;
        }

        if (slot != null && slot.Panel != null && !slot.Panel.isDisposed)
        {
            slot.Panel.Dispose();
        }

        bool interactive = !_locked && !modalOpen;
        var panel = new GComponent
        {
            name = "XINGSHU_StarBuffs_" + playerId,
            touchable = interactive,
            opaque = interactive,
            draggable = interactive,
            sortingOrder = 0,
        };
        XingShuOverlayInput.Protect(panel);
        if (!interactive)
        {
            panel.touchable = false;
            panel.opaque = false;
        }
        panel.SetPivot(0f, 0f, false);

        long capturedId = playerId;
        panel.onDragStart.Set((EventCallback0)delegate { _draggingPlayerId = capturedId; });
        panel.onDragMove.Set((EventCallback0)delegate { UpdateOffsetFromDrag(capturedId, saveConfig: false); });
        panel.onDragEnd.Set((EventCallback0)delegate
        {
            UpdateOffsetFromDrag(capturedId, saveConfig: true);
            _draggingPlayerId = 0L;
        });

        var stripBg = new GGraph { touchable = false };
        panel.AddChild(stripBg);

        slot = new StripSlot
        {
            PlayerId = playerId,
            Panel = panel,
            StripBg = stripBg,
        };
        host.AddChildAt(panel, targetIndex);
        _strips[playerId] = slot;
        return slot;
    }

    private static IconCell EnsureCell(StripSlot strip, int index)
    {
        if (index < strip.Cells.Count)
        {
            return strip.Cells[index];
        }

        var root = new GComponent { touchable = false };
        root.SetSize(BaseIconSize, BaseIconSize);

        var bg = new GGraph { touchable = false };
        bg.SetSize(BaseIconSize, BaseIconSize);
        bg.DrawRoundRect(BaseIconSize, BaseIconSize, new Color(0.14f, 0.17f, 0.25f, 0.85f), new[] { 4f, 4f, 4f, 4f });
        root.AddChild(bg);

        // Use game's TextureLoader (GLoader subclass) so external UT_Buff_* keys resolve via TextureManager.AsyncLoad.
        GLoader icon = new global::UI.TextureLoader
        {
            touchable = false,
            fill = FillType.Scale,
            align = AlignType.Center,
            verticalAlign = VertAlignType.Middle,
        };
        icon.SetSize(BaseIconSize - 2f, BaseIconSize - 2f);
        icon.SetXY(1f, 1f);
        root.AddChild(icon);

        var numText = new GTextField
        {
            touchable = false,
            autoSize = AutoSizeType.None,
            align = AlignType.Right,
            verticalAlign = VertAlignType.Bottom,
            color = Color.white,
            stroke = 1,
            strokeColor = Color.black,
            visible = false,
        };
        var numFmt = numText.textFormat;
        numFmt.size = 11;
        numFmt.bold = true;
        numText.textFormat = numFmt;
        numText.SetSize(BaseIconSize - 1f, 13f);
        numText.SetXY(0f, BaseIconSize - 12f);
        root.AddChild(numText);

        var roundText = new GTextField
        {
            touchable = false,
            autoSize = AutoSizeType.None,
            align = AlignType.Right,
            verticalAlign = VertAlignType.Top,
            color = new Color(1f, 0.85f, 0.35f, 1f),
            stroke = 1,
            strokeColor = Color.black,
            visible = false,
        };
        var roundFmt = roundText.textFormat;
        roundFmt.size = 10;
        roundFmt.bold = true;
        roundText.textFormat = roundFmt;
        roundText.SetSize(BaseIconSize - 1f, 12f);
        roundText.SetXY(0f, 0f);
        root.AddChild(roundText);

        strip.Panel.AddChild(root);
        var cell = new IconCell
        {
            Root = root,
            Bg = bg,
            Icon = icon,
            NumText = numText,
            RoundText = roundText,
        };
        strip.Cells.Add(cell);
        return cell;
    }

    private void UpdateOffsetFromDrag(long playerId, bool saveConfig)
    {
        if (!_strips.TryGetValue(playerId, out var slot)
            || slot.Panel == null || slot.Panel.isDisposed || _attachedHost == null)
        {
            return;
        }
        FloatingHintLayoutMath.ClampXYToBounds(
            _attachedHost.width, _attachedHost.height,
            slot.Width, slot.Height,
            _scale,
            slot.Panel.x, slot.Panel.y,
            4f,
            out float cx, out float cy);
        slot.Panel.SetXY(cx, cy);

        float nextX = FloatingHintLayoutMath.FiniteClamp(
            cx - slot.AnchorX,
            XingShuConfigKeys.PlayerStarBuffsOffsetXMin, XingShuConfigKeys.PlayerStarBuffsOffsetXMax, _offsetX);
        float nextY = FloatingHintLayoutMath.FiniteClamp(
            cy - slot.AnchorY,
            XingShuConfigKeys.PlayerStarBuffsOffsetYMin, XingShuConfigKeys.PlayerStarBuffsOffsetYMax, _offsetY);

        if (saveConfig)
        {
            SetOffset(nextX, nextY);
        }
        else
        {
            _offsetX = nextX;
            _offsetY = nextY;
        }
    }

    private static bool TryComputeStarRect(
        UIBattleInfo_Button_PlayerInfo head,
        GComponent host,
        out float minX,
        out float minY,
        out float width,
        out float height)
    {
        minX = minY = width = height = 0f;
        var playerInfo = head.com_player as UICom_PlayerInfo;
        GObject anchorObj = playerInfo != null && playerInfo.list_Level != null && !playerInfo.list_Level.isDisposed
            ? (GObject)playerInfo.list_Level
            : head;

        Vector2 tlGlobal = anchorObj.LocalToGlobal(Vector2.zero);
        float rawW = anchorObj.width > 1f ? anchorObj.width : 90f;
        float rawH = anchorObj.height > 1f ? anchorObj.height : 26f;
        Vector2 brGlobal = anchorObj.LocalToGlobal(new Vector2(rawW, rawH));

        Vector2 tl = host.GlobalToLocal(tlGlobal);
        Vector2 br = host.GlobalToLocal(brGlobal);
        if (!PortMath.Finite(tl.x) || !PortMath.Finite(tl.y) || !PortMath.Finite(br.x) || !PortMath.Finite(br.y))
        {
            return false;
        }
        minX = Math.Min(tl.x, br.x);
        minY = Math.Min(tl.y, br.y);
        width = Math.Max(20f, Math.Abs(br.x - tl.x));
        height = Math.Max(14f, Math.Abs(br.y - tl.y));
        return true;
    }

    private static bool IsVisibleTree(GObject obj)
    {
        if (obj == null || obj.isDisposed || !obj.onStage) return false;
        for (GObject node = obj; node != null; node = node.parent)
        {
            if (node.isDisposed || !node.visible || node.alpha <= 0.01f || node.displayObject == null || !node.displayObject.visible)
            {
                return false;
            }
        }
        return true;
    }

    private void RefreshInteractionStates(bool modalOpen)
    {
        bool interactive = !_locked && !modalOpen;
        foreach (var slot in _strips.Values)
        {
            if (slot.Panel == null || slot.Panel.isDisposed) continue;
            slot.Panel.touchable = interactive;
            slot.Panel.opaque = interactive;
            slot.Panel.draggable = interactive;
        }
    }

    private void BeginFrame()
    {
        foreach (var slot in _strips.Values)
        {
            slot.ActiveInFrame = false;
        }
    }

    private void HideInactiveStrips()
    {
        foreach (var kv in _strips)
        {
            if (!kv.Value.ActiveInFrame && kv.Value.Panel != null && !kv.Value.Panel.isDisposed)
            {
                if (_draggingPlayerId == kv.Key) _draggingPlayerId = 0L;
                kv.Value.Panel.visible = false;
            }
        }
    }

    private void HideAllStrips()
    {
        _draggingPlayerId = 0L;
        foreach (var slot in _strips.Values)
        {
            if (slot.Panel != null && !slot.Panel.isDisposed)
            {
                slot.Panel.visible = false;
            }
        }
    }

    private void DisposeAllStrips()
    {
        foreach (var slot in _strips.Values)
        {
            if (slot.Panel != null && !slot.Panel.isDisposed)
            {
                slot.Panel.Dispose();
            }
        }
        _strips.Clear();
    }

    private void ClearAll()
    {
        _draggingPlayerId = 0L;
        DisposeAllStrips();
        _attachedHost = null;
    }

    private void OnDestroy()
    {
        ClearAll();
        if (_instance == this) _instance = null;
    }
}
}
