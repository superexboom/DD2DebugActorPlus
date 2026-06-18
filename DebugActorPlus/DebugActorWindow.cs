using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Assets.Code.Actor;
using Assets.Code.Actor.Queries;
using Assets.Code.Buff;
using Assets.Code.Dot;
using Assets.Code.Game;
using Assets.Code.Item;
using Assets.Code.Library;
using Assets.Code.Locale;
using Assets.Code.Quirk;
using Assets.Code.Source;
using Assets.Code.Trinket;
using Assets.Code.Token;
using Assets.Code.UI;
using Assets.Code.UI.Data;
using Assets.Code.UI.RunLog;
using Assets.Code.UI.Events;
using Assets.Code.Utils;
using BepInEx.Logging;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    internal sealed class DebugActorWindow
    {
        private const int WindowId = 817204;
        private const int MaxRows = 250;
        private static readonly Regex RichTextTagRegex = new Regex("<.*?>");
        private static readonly Regex SpriteTagRegex = new Regex("<sprite(?<attrs>[^>]*)>", RegexOptions.IgnoreCase);
        private static readonly Regex SpriteTagAttributeRegex = new Regex("(?<key>\\w+)\\s*=\\s*['\\\"](?<value>[^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase);
        private static readonly Regex SpriteTagShorthandRegex = new Regex("^\\s*=['\\\"](?<value>[^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase);
        private static readonly Regex InlineWordRegex = new Regex("\\S+|\\s+", RegexOptions.Compiled);
        private static readonly string[] Tabs = { "生命/压力", "Token", "Buff", "DOT", "怪癖", "道具" };
        private const float MinWidth = 1040f;
        private const float MinHeight = 680f;

        private readonly ManualLogSource _log;
        private readonly List<ActorChoice> _actors = new List<ActorChoice>();
        private readonly List<CatalogChoice<TokenDefinition>> _tokens = new List<CatalogChoice<TokenDefinition>>();
        private readonly List<CatalogChoice<BuffDefinition>> _buffs = new List<CatalogChoice<BuffDefinition>>();
        private readonly List<CatalogChoice<DotDefinition>> _dots = new List<CatalogChoice<DotDefinition>>();
        private readonly List<CatalogChoice<QuirkDefinition>> _quirks = new List<CatalogChoice<QuirkDefinition>>();
        private readonly List<CatalogChoice<ItemDefinition>> _combatItems = new List<CatalogChoice<ItemDefinition>>();
        private readonly List<CatalogChoice<ItemDefinition>> _trinkets = new List<CatalogChoice<ItemDefinition>>();
        private readonly Dictionary<string, Sprite> _itemSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _tokenSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _dotSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _buffSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _quirkSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _missingTokenSpriteRetryAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _missingDotSpriteRetryAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _missingBuffSpriteRetryAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, InlineIconData> _inlineSpriteIcons = new Dictionary<string, InlineIconData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _missingInlineSpriteRetryAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IList<InlineSegment>> _inlineSegmentCache = new Dictionary<string, IList<InlineSegment>>(StringComparer.Ordinal);

        private Rect _windowRect = new Rect(70f, 54f, 1420f, 880f);
        private Vector2 _actorScroll;
        private Vector2 _currentScroll;
        private Vector2 _catalogScroll;
        private Vector2 _detailScroll;
        private bool _visible;
        private bool _loaded;
        private bool _stylesReady;
        private bool _resizing;
        private Vector2 _resizeStartMouse;
        private Vector2 _resizeStartSize;
        private uint _selectedActorGuid;
        private int _tab;
        private string _actorSearch = "";
        private string _tokenSearch = "";
        private string _buffSearch = "";
        private string _dotSearch = "";
        private string _quirkSearch = "";
        private string _itemSearch = "";
        private int _quirkMode;
        private int _itemMode;
        private string _selectedTokenId = "";
        private string _selectedBuffId = "";
        private string _selectedDotId = "";
        private string _selectedQuirkId = "";
        private string _selectedCombatItemId = "";
        private string _selectedTrinketId = "";
        private string _healthAmount = "1";
        private string _stressAmount = "1";
        private bool _lockedTeamPosition;
        private bool _dotCrit;
        private string _status = "Alt+J 打开 Debug Actor Plus。";
        private float _nextDrawErrorLogTime;
        private bool _reloadAfterDrawError;
        private float _fontScale = 1.18f;
        private float _styleFontScale = -1f;

        private Texture2D _overlayTex;
        private Texture2D _windowTex;
        private Texture2D _panelTex;
        private Texture2D _panelAltTex;
        private Texture2D _accentTex;
        private Texture2D _accentDimTex;
        private Texture2D _buttonTex;
        private Texture2D _buttonHoverTex;
        private Texture2D _dangerTex;
        private Texture2D _resizeTex;
        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _detailStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _selectedRowStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _selectedTabStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _dangerButtonStyle;

        public DebugActorWindow(ManualLogSource log)
        {
            _log = log;
        }

        public void Toggle()
        {
            if (_visible) Hide();
            else Show();
        }

        public void Show()
        {
            _visible = true;
            _status = "正在加载角色和调试资源库...";
            ReloadData();
        }

        public void Hide()
        {
            _visible = false;
        }

        public void Update()
        {
            if (_reloadAfterDrawError)
            {
                _reloadAfterDrawError = false;
                if (_visible)
                {
                    ReloadData();
                }
            }
        }

        public void Draw()
        {
            if (!_visible) return;

            EnsureStyles();
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _overlayTex);
            _windowRect.width = Mathf.Clamp(_windowRect.width, Mathf.Min(MinWidth, Screen.width - 32f), Screen.width - 32f);
            _windowRect.height = Mathf.Clamp(_windowRect.height, Mathf.Min(MinHeight, Screen.height - 32f), Screen.height - 32f);
            _windowRect.x = Mathf.Clamp(_windowRect.x, 16f, Mathf.Max(16f, Screen.width - _windowRect.width - 16f));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 16f, Mathf.Max(16f, Screen.height - _windowRect.height - 16f));
            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, GUIContent.none, _windowStyle);
            HandleResize();
        }

        private void DrawWindow(int id)
        {
            DrawHeader();

            try
            {
                DrawBody();
            }
            catch (Exception ex)
            {
                HandleDrawException(ex);
                GUI.Label(new Rect(24f, 86f, _windowRect.width - 48f, 40f), _status, _subHeaderStyle);
            }

            DrawResizeHandle();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 62f));
        }

        private void DrawHeader()
        {
            GUI.DrawTexture(new Rect(0f, 0f, _windowRect.width, 62f), _panelAltTex);
            GUI.Label(new Rect(22f, 10f, 360f, 30f), "角色调试 Plus", _headerStyle);
            GUI.Label(new Rect(24f, 40f, 620f, 18f), "Alt+J 呼出。可在战斗中或已加载角色的局外编辑角色状态。", _mutedStyle);

            GUI.Label(new Rect(_windowRect.width - 430f, 18f, 54f, 24f), "字号", _mutedStyle);
            float nextScale = GUI.HorizontalSlider(new Rect(_windowRect.width - 382f, 25f, 120f, 18f), _fontScale, 0.95f, 1.45f);
            if (!Mathf.Approximately(nextScale, _fontScale))
            {
                _fontScale = nextScale;
                _stylesReady = false;
            }
            GUI.Label(new Rect(_windowRect.width - 254f, 18f, 52f, 24f), _fontScale.ToString("0.00"), _mutedStyle);

            if (GUI.Button(new Rect(_windowRect.width - 196f, 16f, 102f, 30f), "重新加载", _buttonStyle))
            {
                ReloadData();
            }

            if (GUI.Button(new Rect(_windowRect.width - 58f, 16f, 34f, 30f), "X", _buttonStyle))
            {
                Hide();
            }
        }

        private void DrawBody()
        {
            GUILayout.BeginArea(new Rect(18f, 76f, _windowRect.width - 36f, _windowRect.height - 94f));
            try
            {
                if (!_loaded)
                {
                    GUILayout.Label(_status, _subHeaderStyle);
                    return;
                }

                GUILayout.BeginHorizontal();
                try
                {
                    DrawActorColumn(Mathf.Clamp(_windowRect.width * 0.25f, 300f, 390f));
                    GUILayout.Space(12f);
                    DrawEditorColumn();
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawActorColumn(float width)
        {
            GUILayout.BeginVertical(_panelStyle, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            try
            {
                GUILayout.Label("角色", _subHeaderStyle);
                _actorSearch = GUILayout.TextField(_actorSearch, _textFieldStyle, GUILayout.Height(30f));

                IEnumerable<ActorChoice> filtered = _actors.Where(actor => PinyinSearch.Matches(_actorSearch, actor.Name, actor.DataId, actor.Guid.ToString()));
                _actorScroll = GUILayout.BeginScrollView(_actorScroll, GUILayout.ExpandHeight(true));
                try
                {
                    foreach (ActorChoice actor in filtered)
                    {
                        string text = $"{actor.Name}  [{actor.DataId}]\n队伍 {actor.TeamIndex}    GUID {actor.Guid}";
                        GUIStyle style = actor.Guid == _selectedActorGuid ? _selectedRowStyle : _rowStyle;
                        if (GUILayout.Button(text, style, GUILayout.Height(56f)))
                        {
                            _selectedActorGuid = actor.Guid;
                            _status = "已选择 " + actor.Name;
                        }
                    }
                }
                finally
                {
                    GUILayout.EndScrollView();
                }

                ActorInstance selected = GetSelectedActor();
                if (selected != null)
                {
                    GUILayout.Space(8f);
                    GUILayout.Label("当前选择", _subHeaderStyle);
                    GUILayout.Label(ActorLine(selected), _smallStyle);
                    GUILayout.Label(StatusLine(selected), _mutedStyle, GUILayout.Height(98f));
                }
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private void DrawEditorColumn()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            try
            {
                DrawTabs();
                GUILayout.Space(10f);

                ActorInstance actor = GetSelectedActor();
                if (actor == null)
                {
                    GUILayout.Label("未选择角色。", _subHeaderStyle);
                    return;
                }

                GUILayout.BeginVertical(_panelStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                try
                {
                    GUILayout.Label(ActorLine(actor), _subHeaderStyle);
                    GUILayout.Label(_status, _mutedStyle, GUILayout.Height(28f));
                    GUILayout.Space(8f);

                    switch (_tab)
                    {
                        case 0:
                            DrawVitalsTab(actor);
                            break;
                        case 1:
                            DrawTokenTab(actor);
                            break;
                        case 2:
                            DrawBuffTab(actor);
                            break;
                        case 3:
                            DrawDotTab(actor);
                            break;
                        case 4:
                            DrawQuirkTab(actor);
                            break;
                        case 5:
                            DrawItemTab(actor);
                            break;
                    }
                }
                finally
                {
                    GUILayout.EndVertical();
                }
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            try
            {
                for (int i = 0; i < Tabs.Length; i++)
                {
                    GUIStyle style = _tab == i ? _selectedTabStyle : _tabStyle;
                    if (GUILayout.Button(Tabs[i], style, GUILayout.Height(38f)))
                    {
                        _tab = i;
                        _currentScroll = Vector2.zero;
                        _catalogScroll = Vector2.zero;
                    }
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private void DrawVitalsTab(ActorInstance actor)
        {
            GUILayout.Label("生命 / 压力", _subHeaderStyle);
            GUILayout.Label($"生命 {actor.DisplayedHp:0}/{actor.DisplayedHpMax:0}    压力 {actor.Stress:0}/{actor.StressMax:0}", _smallStyle);
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.BeginVertical(GUILayout.Width(360f));
                try
                {
                    GUILayout.Label("生命数值", _mutedStyle);
                    _healthAmount = GUILayout.TextField(_healthAmount, _textFieldStyle, GUILayout.Height(30f));
                    DrawQuickAmountButtons(ref _healthAmount, (int)Mathf.Max(1f, actor.DisplayedHpMax));

                    GUILayout.Space(8f);
                    GUILayout.BeginHorizontal();
                    try
                    {
                        if (GUILayout.Button("扣生命", _dangerButtonStyle, GUILayout.Height(38f)))
                        {
                            int amount = ParseAmount(_healthAmount, 0, (int)Mathf.Max(999f, actor.DisplayedHpMax));
                            ApplyAction(actor, $"扣除生命 {amount}", () => actor.ApplyHealthDamage(amount, false, false, actor, DeathType.DEBUG, SourceType.DEBUG, null, false));
                        }

                        if (GUILayout.Button("治疗生命", _buttonStyle, GUILayout.Height(38f)))
                        {
                            int amount = ParseAmount(_healthAmount, 0, (int)Mathf.Max(999f, actor.DisplayedHpMax));
                            ApplyAction(actor, $"治疗生命 {amount}", () => actor.ApplyHealthHeal(amount, false, SourceType.DEBUG, false));
                        }
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }
                }
                finally
                {
                    GUILayout.EndVertical();
                }

                GUILayout.Space(24f);

                GUILayout.BeginVertical(GUILayout.Width(360f));
                try
                {
                    GUILayout.Label("压力数值", _mutedStyle);
                    _stressAmount = GUILayout.TextField(_stressAmount, _textFieldStyle, GUILayout.Height(30f));
                    DrawQuickAmountButtons(ref _stressAmount, (int)Mathf.Max(1f, actor.StressMax));

                    GUILayout.Space(8f);
                    GUILayout.BeginHorizontal();
                    try
                    {
                        if (GUILayout.Button("增加压力", _dangerButtonStyle, GUILayout.Height(38f)))
                        {
                            int amount = ParseAmount(_stressAmount, 0, (int)Mathf.Max(99f, actor.StressMax));
                            ApplyAction(actor, $"增加压力 {amount}", () => actor.ApplyStressDamage(amount, false, SourceType.DEBUG, null, 0U));
                        }

                        if (GUILayout.Button("清减压力", _buttonStyle, GUILayout.Height(38f)))
                        {
                            int amount = ParseAmount(_stressAmount, 0, (int)Mathf.Max(99f, actor.StressMax));
                            ApplyAction(actor, $"清减压力 {amount}", () => actor.ApplyStressHeal(amount, SourceType.DEBUG));
                        }
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }
                }
                finally
                {
                    GUILayout.EndVertical();
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private void DrawTokenTab(ActorInstance actor)
        {
            DrawCatalogTab(_tokens, ref _tokenSearch, ref _selectedTokenId, "Token", "添加 Token", actor,
                () => DrawCurrentStatusItems(
                    "当前 Token",
                    GetCurrentTokens(actor),
                    item => EventDebugActorRemoveToken.Trigger(actor.ActorGuid, item.Value as TokenDefinition)),
                selected =>
                {
                    _lockedTeamPosition = GUILayout.Toggle(_lockedTeamPosition, "锁定站位来源", _smallStyle);
                },
                selected => EventDebugActorAddToken.Trigger(actor.ActorGuid, selected.Value, _lockedTeamPosition, actor.ActorGuid),
                null);
        }

        private void DrawBuffTab(ActorInstance actor)
        {
            DrawCatalogTab(_buffs, ref _buffSearch, ref _selectedBuffId, "Buff", "添加 Buff", actor,
                () => DrawCurrentStatusItems(
                    "当前 Buff",
                    GetCurrentBuffs(actor),
                    item => EventDebugActorRemoveBuff.Trigger(actor.ActorGuid, item.Value as BuffDefinition)),
                selected =>
                {
                    _lockedTeamPosition = GUILayout.Toggle(_lockedTeamPosition, "锁定站位来源", _smallStyle);
                },
                selected => EventDebugActorAddBuff.Trigger(actor.ActorGuid, selected.Value, _lockedTeamPosition),
                null);
        }

        private void DrawDotTab(ActorInstance actor)
        {
            DrawCatalogTab(_dots, ref _dotSearch, ref _selectedDotId, "DOT", "添加 DOT", actor,
                () => DrawCurrentStatusItems(
                    "当前 DOT",
                    GetCurrentDots(actor),
                    item => EventDebugActorRemoveDot.Trigger(actor.ActorGuid, item.Value as DotDefinition)),
                selected =>
                {
                    GUILayout.BeginHorizontal();
                    try
                    {
                        _lockedTeamPosition = GUILayout.Toggle(_lockedTeamPosition, "锁定站位来源", _smallStyle, GUILayout.Width(170f));
                        _dotCrit = GUILayout.Toggle(_dotCrit, "暴击 DOT", _smallStyle, GUILayout.Width(110f));
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }
                },
                selected => EventDebugActorAddDot.Trigger(actor.ActorGuid, selected.Value, _lockedTeamPosition, actor.ActorGuid, _dotCrit),
                null);
        }

        private void DrawQuirkTab(ActorInstance actor)
        {
            GUILayout.BeginHorizontal();
            try
            {
                DrawModeButton("正面怪癖", 0, ref _quirkMode, 112f);
                DrawModeButton("负面怪癖", 1, ref _quirkMode, 112f);
                DrawModeButton("疾病/诅咒", 2, ref _quirkMode, 112f);
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            List<CatalogChoice<QuirkDefinition>> choices = GetQuirkChoicesForMode();
            DrawCatalogTab(choices, ref _quirkSearch, ref _selectedQuirkId, GetQuirkModeLabel(), "添加怪癖", actor,
                () => DrawCurrentStatusItems(
                    "当前怪癖 / 疾病",
                    GetCurrentQuirks(actor),
                    item => EventDebugActorRemoveQuirk.Trigger(actor.ActorGuid, item.Value as QuirkDefinition),
                    true),
                selected => DrawQuirkSlots(actor),
                selected => EventDebugActorAddQuirk.Trigger(actor.ActorGuid, selected.Value),
                null);
        }

        private void DrawItemTab(ActorInstance actor)
        {
            GUILayout.BeginHorizontal();
            try
            {
                DrawModeButton("战斗道具", 0, ref _itemMode, 112f);
                DrawModeButton("饰品", 1, ref _itemMode, 92f);
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            if (_itemMode == 1)
            {
                DrawCatalogTab(_trinkets, ref _itemSearch, ref _selectedTrinketId, "饰品", "装备饰品", actor,
                    () => DrawInventorySlots(actor),
                    selected => GUILayout.Label("把选中的饰品加入该角色的饰品栏；不满足角色条件的饰品会被游戏库存逻辑拒绝或移除。", _mutedStyle),
                    selected => AddTrinket(actor, selected.Value),
                    null);
                return;
            }

            DrawCatalogTab(_combatItems, ref _itemSearch, ref _selectedCombatItemId, "战斗道具", "添加战斗道具", actor,
                () => DrawInventorySlots(actor),
                selected => GUILayout.Label("把选中的战斗道具加入该角色的战斗道具栏。", _mutedStyle),
                selected => EventDebugActorAddCombatItem.Trigger(actor.ActorGuid, selected.Value),
                null);
        }

        private void DrawModeButton(string label, int mode, ref int value, float width)
        {
            if (GUILayout.Button(label, mode == value ? _selectedButtonStyle : _buttonStyle, GUILayout.Width(width), GUILayout.Height(30f)))
            {
                value = mode;
                _catalogScroll = Vector2.zero;
                _detailScroll = Vector2.zero;
            }
        }

        private List<CatalogChoice<QuirkDefinition>> GetQuirkChoicesForMode()
        {
            return _quirks
                .Where(choice =>
                {
                    QuirkDefinition quirk = choice.Value;
                    if (quirk == null) return false;
                    if (_quirkMode == 2) return quirk.IsDisease || quirk.IsCurse;
                    if (_quirkMode == 1) return quirk.IsNegative && !quirk.IsDisease && !quirk.IsCurse;
                    return !quirk.IsNegative && !quirk.IsDisease && !quirk.IsCurse;
                })
                .ToList();
        }

        private string GetQuirkModeLabel()
        {
            return _quirkMode == 2 ? "疾病/诅咒" : _quirkMode == 1 ? "负面怪癖" : "正面怪癖";
        }

        private void DrawCatalogTab<T>(
            List<CatalogChoice<T>> choices,
            ref string search,
            ref string selectedId,
            string label,
            string actionLabel,
            ActorInstance actor,
            Action drawCurrent,
            Action<CatalogChoice<T>> drawOptions,
            Action<CatalogChoice<T>> addAction,
            Action<CatalogChoice<T>> removeAction)
        {
            string selectedLookup = selectedId;
            CatalogChoice<T> selected = choices.FirstOrDefault(choice => choice.Id == selectedLookup);
            if (selected == null && choices.Count > 0)
            {
                selected = choices[0];
                selectedId = selected.Id;
            }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            try
            {
                GUILayout.BeginVertical(_panelStyle, GUILayout.Width(Mathf.Clamp(_windowRect.width * 0.31f, 300f, 430f)), GUILayout.ExpandHeight(true));
                try
                {
                    drawCurrent?.Invoke();
                }
                finally
                {
                    GUILayout.EndVertical();
                }

                GUILayout.Space(12f);

                GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                try
                {
                    GUILayout.Label(label + "库", _subHeaderStyle);
                    search = GUILayout.TextField(search, _textFieldStyle, GUILayout.Height(34f));

                    if (selected != null)
                    {
                        GUILayout.Space(8f);
                        DrawSelectedCatalogDetail(selected, actor, actionLabel, drawOptions, addAction, removeAction);
                    }

                    GUILayout.Space(10f);
                    string searchValue = search;
                    List<CatalogChoice<T>> filtered = choices
                        .Where(choice => choice.Matches(searchValue))
                        .ToList();
                    int total = filtered.Count;
                    GUILayout.Label($"显示 {Math.Min(total, MaxRows)} / {total}", _mutedStyle);

                    _catalogScroll = GUILayout.BeginScrollView(_catalogScroll, GUILayout.ExpandHeight(true));
                    try
                    {
                        string clickedId = null;
                        foreach (CatalogChoice<T> choice in filtered.Take(MaxRows))
                        {
                            DrawCatalogChoiceRow(choice, choice.Id == selectedId, () => clickedId = choice.Id);
                        }

                        if (!string.IsNullOrWhiteSpace(clickedId))
                        {
                            selectedId = clickedId;
                            _detailScroll = Vector2.zero;
                        }
                    }
                    finally
                    {
                        GUILayout.EndScrollView();
                    }
                }
                finally
                {
                    GUILayout.EndVertical();
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private void DrawSelectedCatalogDetail<T>(
            CatalogChoice<T> selected,
            ActorInstance actor,
            string actionLabel,
            Action<CatalogChoice<T>> drawOptions,
            Action<CatalogChoice<T>> addAction,
            Action<CatalogChoice<T>> removeAction)
        {
            GUILayout.BeginVertical(_panelStyle, GUILayout.ExpandWidth(true));
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    if (ShouldDrawLargeCatalogIcon(selected.Value))
                    {
                        DrawSpriteLayout(GetCatalogSprite(selected), 44f);
                    }

                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    try
                    {
                        GUILayout.Label($"{selected.Name}  [{selected.Id}]", _smallStyle);
                        drawOptions?.Invoke(selected);
                    }
                    finally
                    {
                        GUILayout.EndVertical();
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                if (!string.IsNullOrWhiteSpace(selected.Detail))
                {
                    GUILayout.Space(6f);
                    _detailScroll = GUILayout.BeginScrollView(_detailScroll, GUILayout.Height(116f));
                    try
                    {
                        DrawInlineDescription(selected.Detail, 104f);
                    }
                    finally
                    {
                        GUILayout.EndScrollView();
                    }
                }

                GUILayout.Space(8f);
                GUILayout.BeginHorizontal();
                try
                {
                    if (addAction != null && GUILayout.Button(actionLabel, _selectedButtonStyle, GUILayout.Width(150f), GUILayout.Height(38f)))
                    {
                        ApplyAction(actor, $"{actionLabel}: {selected.Name}", () => addAction(selected));
                    }

                    if (removeAction != null && GUILayout.Button("移除", _dangerButtonStyle, GUILayout.Width(110f), GUILayout.Height(38f)))
                    {
                        ApplyAction(actor, $"移除 {selected.Name}", () => removeAction(selected));
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private void DrawCatalogChoiceRow<T>(CatalogChoice<T> choice, bool selected, Action onClick)
        {
            bool showIcon = ShouldDrawCatalogRowIcon(choice.Value);
            float rowHeight = choice.Value is ItemDefinition ? 74f : 68f;
            Rect rect = GUILayoutUtility.GetRect(1f, rowHeight, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, selected ? _accentDimTex : _buttonTex);
            float textX = rect.x + 12f;
            float textWidth = rect.width - 20f;
            if (showIcon)
            {
                DrawSprite(new Rect(rect.x + 8f, rect.y + 8f, 48f, 48f), GetCatalogSprite(choice));
                textX = rect.x + 64f;
                textWidth = rect.width - 72f;
            }

            GUI.Label(new Rect(textX, rect.y + 7f, textWidth, 26f), choice.Name, _smallStyle);
            GUI.Label(new Rect(textX, rect.y + 39f, textWidth, 24f), choice.Id, _mutedStyle);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                onClick();
                Event.current.Use();
            }
        }

        private void DrawCurrentStatusItems(string title, IReadOnlyList<StatusItem> items, Action<StatusItem> removeAction, bool showSlots = false)
        {
            GUILayout.Label(title, _subHeaderStyle);
            if (showSlots)
            {
                GUILayout.Label("下方卡片可直接移除；空槽仅用于确认当前容量。", _mutedStyle);
            }

            _currentScroll = GUILayout.BeginScrollView(_currentScroll, GUILayout.ExpandHeight(true));
            try
            {
                if (items == null || items.Count == 0)
                {
                    GUILayout.Label("当前没有对应状态。", _mutedStyle);
                    return;
                }

                foreach (StatusItem item in items)
                {
                    DrawStatusCard(item, removeAction);
                }
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }

        private void DrawStatusCard(StatusItem item, Action<StatusItem> removeAction)
        {
            GUILayout.BeginVertical(_panelStyle, GUILayout.ExpandWidth(true));
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    if (ShouldDrawStatusIcon(item.Kind))
                    {
                        DrawSpriteLayout(item.Icon, 38f);
                    }

                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    try
                    {
                        GUILayout.Label($"{item.Name}  [{item.Id}]", _smallStyle);
                        if (!string.IsNullOrWhiteSpace(item.Subtitle))
                        {
                            GUILayout.Label(item.Subtitle, _mutedStyle);
                        }
                    }
                    finally
                    {
                        GUILayout.EndVertical();
                    }

                    if (removeAction != null && GUILayout.Button("移除", _dangerButtonStyle, GUILayout.Width(78f), GUILayout.Height(34f)))
                    {
                        ApplyAction(GetSelectedActor(), $"移除 {item.Name}", () => removeAction(item));
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                if (!string.IsNullOrWhiteSpace(item.Detail))
                {
                    DrawInlineDescription(item.Detail, 54f);
                }
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private void DrawQuirkSlots(ActorInstance actor)
        {
            IReadOnlyList<StatusItem> quirks = GetCurrentQuirks(actor);
            int positive = quirks.Count(item => item.Kind == StatusKind.PositiveQuirk);
            int negative = quirks.Count(item => item.Kind == StatusKind.NegativeQuirk);
            int disease = quirks.Count(item => item.Kind == StatusKind.Disease);
            GUILayout.Label($"怪癖槽：正面 {positive}/3    负面 {negative}/3    疾病/诅咒 {disease}/1", _mutedStyle);
        }

        private void DrawInventorySlots(ActorInstance actor)
        {
            GUILayout.Label("当前道具 / 饰品", _subHeaderStyle);
            _currentScroll = GUILayout.BeginScrollView(_currentScroll, GUILayout.ExpandHeight(true));
            try
            {
                DrawInventoryBlock("战斗道具栏", actor.GetCombatSkillInventory());
                GUILayout.Space(8f);
                DrawInventoryBlock("饰品栏", actor.GetTrinketInventory());
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }

        private void DrawInventoryBlock(string title, ItemInventory inventory)
        {
            if (inventory == null)
            {
                GUILayout.Label(title + "：不可用", _mutedStyle);
                return;
            }

            int total = inventory.GetNumberOfTotalSlots();
            int filled = inventory.GetNumberOfFilledSlots();
            GUILayout.Label($"{title}  {filled}/{total}", _smallStyle);
            IReadOnlyList<IReadOnlyItemInstance> items = inventory.GetItems();
            for (int i = 0; i < total; i++)
            {
                IReadOnlyItemInstance item = i < items.Count ? items[i] : null;
                ItemDefinition definition = item?.GetItemDefinition();
                string name = definition == null ? "空槽" : ItemName(definition);
                string id = definition == null ? "" : definition.m_id;
                Rect rect = GUILayoutUtility.GetRect(1f, 66f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, _buttonTex);
                DrawSprite(new Rect(rect.x + 8f, rect.y + 6f, 44f, 44f), GetItemSprite(definition));
                GUI.Label(new Rect(rect.x + 60f, rect.y + 7f, rect.width - 68f, 22f), $"{i + 1}. {name}", _smallStyle);
                string suffix = definition == null ? "可装备槽位" : $"{id}  x{item.GetQty()}";
                GUI.Label(new Rect(rect.x + 60f, rect.y + 34f, rect.width - 68f, 24f), suffix, _mutedStyle);
            }
        }

        private IReadOnlyList<StatusItem> GetCurrentTokens(ActorInstance actor)
        {
            return actor.TokenContainer?.GetInstances()
                .Where(instance => instance?.Definition != null)
                .Select(instance => new StatusItem(
                    instance.Definition.Id,
                    TokenName(instance.Definition),
                    TokenDetail(instance.Definition),
                    "Token",
                    GetTokenSprite(instance.Definition.Id),
                    instance.Definition,
                    StatusKind.Token))
                .ToList() ?? new List<StatusItem>();
        }

        private IReadOnlyList<StatusItem> GetCurrentBuffs(ActorInstance actor)
        {
            return actor.BuffContainer?.GetInstances()
                .Where(instance => instance?.Definition != null)
                .Select(instance => new StatusItem(
                    instance.Definition.Id,
                    BuffName(instance.Definition),
                    BuffDetail(instance.Definition),
                    "Buff",
                    GetBuffSprite(instance.Definition.Id),
                    instance.Definition,
                    StatusKind.Buff))
                .ToList() ?? new List<StatusItem>();
        }

        private IReadOnlyList<StatusItem> GetCurrentDots(ActorInstance actor)
        {
            return actor.DotContainer?.GetInstances()
                .Where(instance => instance?.Definition != null)
                .Select(instance =>
                {
                    string type = string.IsNullOrWhiteSpace(instance.Definition.m_Type) ? instance.Definition.Id : instance.Definition.m_Type;
                    return new StatusItem(
                        instance.Definition.Id,
                        DotName(instance.Definition),
                        DotDetail(instance.Definition),
                        "DOT",
                        GetDotSprite(type),
                        instance.Definition,
                        StatusKind.Dot);
                })
                .ToList() ?? new List<StatusItem>();
        }

        private IReadOnlyList<StatusItem> GetCurrentQuirks(ActorInstance actor)
        {
            return actor.QuirkContainer?.GetInstances()
                .Where(instance => instance?.Definition != null)
                .Select(instance =>
                {
                    QuirkDefinition definition = instance.Definition;
                    StatusKind kind = definition.IsDisease || definition.IsCurse
                        ? StatusKind.Disease
                        : definition.IsNegative
                            ? StatusKind.NegativeQuirk
                            : StatusKind.PositiveQuirk;
                    string subtitle = definition.IsDisease ? "疾病" : definition.IsCurse ? "诅咒" : definition.IsNegative ? "负面怪癖" : "正面怪癖";
                    return new StatusItem(
                        definition.Id,
                        QuirkName(definition),
                        QuirkDetail(definition),
                        subtitle,
                        GetQuirkSprite(kind),
                        definition,
                        kind);
                })
                .ToList() ?? new List<StatusItem>();
        }

        private void DrawQuickAmountButtons(ref string amount, int max)
        {
            GUILayout.BeginHorizontal();
            try
            {
                if (GUILayout.Button("-1", _buttonStyle, GUILayout.Height(28f))) amount = Mathf.Max(0, ParseAmount(amount, 0, max) - 1).ToString();
                if (GUILayout.Button("+1", _buttonStyle, GUILayout.Height(28f))) amount = Mathf.Min(max, ParseAmount(amount, 0, max) + 1).ToString();
                if (GUILayout.Button("+5", _buttonStyle, GUILayout.Height(28f))) amount = Mathf.Min(max, ParseAmount(amount, 0, max) + 5).ToString();
                if (GUILayout.Button("最大", _buttonStyle, GUILayout.Height(28f))) amount = max.ToString();
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private void ReloadData()
        {
            try
            {
                _actors.Clear();
                _tokens.Clear();
                _buffs.Clear();
                _dots.Clear();
                _quirks.Clear();
                _combatItems.Clear();
                _trinkets.Clear();
                _inlineSegmentCache.Clear();

                LoadActors();
                LoadCatalog(_tokens, def => def.Id, TokenName, TokenDetail);
                LoadCatalog(_buffs, def => def.Id, BuffName, BuffDetail);
                LoadCatalog(_dots, def => def.Id, DotName, DotDetail);
                LoadCatalog(_quirks, def => def.Id, QuirkName, QuirkDetail);
                LoadCombatItems();
                LoadTrinkets();

                if (_selectedActorGuid == 0U || !_actors.Any(actor => actor.Guid == _selectedActorGuid))
                {
                    _selectedActorGuid = _actors.Count > 0 ? _actors[0].Guid : 0U;
                }

                EnsureSelectedIds();
                _loaded = true;
                _status = $"已加载 {_actors.Count} 个角色、{_tokens.Count} 个 token、{_buffs.Count} 个 buff、{_dots.Count} 个 DOT、{_quirks.Count} 个怪癖、{_combatItems.Count} 个战斗道具、{_trinkets.Count} 个饰品。";
                _log.LogInfo(_status);
            }
            catch (Exception ex)
            {
                _loaded = false;
                _status = "加载失败：" + ex.Message;
                _log.LogWarning(_status + "\n" + ex);
            }
        }

        private void LoadActors()
        {
            HashSet<uint> seen = new HashSet<uint>();
            if (SingletonMonoBehaviour<Library<uint, ActorInstance>>.HasInstance(false))
            {
                for (int team = 0; team < 2; team++)
                {
                    QueryTeamActors query = QueryTeamActors.Trigger(team, true);
                    foreach (uint actorGuid in query.m_TeamActorGuids)
                    {
                        AddActorChoice(actorGuid, team, seen);
                    }
                }
            }

            if (_actors.Count == 0 && Singleton<GameTypeMgr>.HasInstance())
            {
                foreach (ActorInstance actor in Singleton<GameTypeMgr>.Instance.RosterManager.GetPartyActors())
                {
                    AddActorChoice(actor.ActorGuid, actor.TeamIndex, seen);
                }
            }

            _actors.Sort((a, b) =>
            {
                int teamCompare = a.TeamIndex.CompareTo(b.TeamIndex);
                return teamCompare != 0 ? teamCompare : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void AddActorChoice(uint actorGuid, int teamIndex, HashSet<uint> seen)
        {
            if (actorGuid == 0U || !seen.Add(actorGuid)) return;

            ActorInstance actor = SingletonMonoBehaviour<Library<uint, ActorInstance>>.Instance.GetLibraryElement(actorGuid);
            if (actor == null) return;

            string dataId = CleanDisplayText(actor.ActorDataId);
            string name = CleanDisplayText(actor.ActorName);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = LocalizedText(dataId, dataId, "hero_name_" + dataId, "hero_select_" + dataId);
            }

            _actors.Add(new ActorChoice(actorGuid, name, dataId, teamIndex));
        }

        private static void LoadCatalog<T>(
            List<CatalogChoice<T>> target,
            Func<T, string> idFunc,
            Func<T, string> nameFunc,
            Func<T, string> detailFunc) where T : class, ILibraryElement<string>
        {
            if (!SingletonMonoBehaviour<Library<string, T>>.HasInstance(false)) return;

            Library<string, T> library = SingletonMonoBehaviour<Library<string, T>>.Instance;
            int count = library.GetNumberOfLibraryElements();
            for (int i = 0; i < count; i++)
            {
                T value = library.GetLibraryElementAtIndex(i);
                if (value == null) continue;

                string id = CleanDisplayText(idFunc(value));
                if (string.IsNullOrWhiteSpace(id)) continue;

                string name = CleanDisplayText(nameFunc(value));
                if (string.IsNullOrWhiteSpace(name)) name = id;
                string detail = CleanDescriptionText(detailFunc(value));

                target.Add(new CatalogChoice<T>(id, name, detail, value));
            }

            target.Sort((a, b) =>
            {
                int nameCompare = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return nameCompare != 0 ? nameCompare : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void LoadCombatItems()
        {
            LibraryItem libraryItem = LibraryItem.LibraryItemInstance;
            if (libraryItem?.AllItemSkillIds == null || !SingletonMonoBehaviour<Library<string, ItemDefinition>>.HasInstance(false))
            {
                return;
            }

            Library<string, ItemDefinition> itemLibrary = SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance;
            foreach (string itemId in libraryItem.AllItemSkillIds)
            {
                ItemDefinition item = itemLibrary.GetLibraryElement(itemId);
                if (item == null) continue;

                string name = CleanDisplayText(ItemName(item));
                _combatItems.Add(new CatalogChoice<ItemDefinition>(item.m_id, string.IsNullOrWhiteSpace(name) ? item.m_id : name, CleanDescriptionText(ItemDetail(item)), item));
            }

            SortCatalog(_combatItems);
        }

        private void LoadTrinkets()
        {
            if (!SingletonMonoBehaviour<Library<string, ItemDefinition>>.HasInstance(false))
            {
                return;
            }

            Library<string, ItemDefinition> itemLibrary = SingletonMonoBehaviour<Library<string, ItemDefinition>>.Instance;
            int count = itemLibrary.GetNumberOfLibraryElements();
            for (int i = 0; i < count; i++)
            {
                ItemDefinition item = itemLibrary.GetLibraryElementAtIndex(i);
                if (item == null || item.m_type != ItemType.TRINKET) continue;

                string name = CleanDisplayText(ItemName(item));
                _trinkets.Add(new CatalogChoice<ItemDefinition>(item.m_id, string.IsNullOrWhiteSpace(name) ? item.m_id : name, CleanDescriptionText(ItemDetail(item)), item));
            }

            SortCatalog(_trinkets);
        }

        private static void SortCatalog<T>(List<CatalogChoice<T>> choices)
        {
            choices.Sort((a, b) =>
            {
                int nameCompare = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return nameCompare != 0 ? nameCompare : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void EnsureSelectedIds()
        {
            if (string.IsNullOrWhiteSpace(_selectedTokenId) && _tokens.Count > 0) _selectedTokenId = _tokens[0].Id;
            if (string.IsNullOrWhiteSpace(_selectedBuffId) && _buffs.Count > 0) _selectedBuffId = _buffs[0].Id;
            if (string.IsNullOrWhiteSpace(_selectedDotId) && _dots.Count > 0) _selectedDotId = _dots[0].Id;
            if (string.IsNullOrWhiteSpace(_selectedQuirkId) && _quirks.Count > 0) _selectedQuirkId = _quirks[0].Id;
            if (string.IsNullOrWhiteSpace(_selectedCombatItemId) && _combatItems.Count > 0) _selectedCombatItemId = _combatItems[0].Id;
            if (string.IsNullOrWhiteSpace(_selectedTrinketId) && _trinkets.Count > 0) _selectedTrinketId = _trinkets[0].Id;
        }

        private ActorInstance GetSelectedActor()
        {
            if (_selectedActorGuid == 0U || !SingletonMonoBehaviour<Library<uint, ActorInstance>>.HasInstance(false))
            {
                return null;
            }

            return SingletonMonoBehaviour<Library<uint, ActorInstance>>.Instance.GetLibraryElement(_selectedActorGuid);
        }

        private void ApplyAction(ActorInstance actor, string successMessage, Action action)
        {
            try
            {
                if (actor == null)
                {
                    _status = "未选择角色。";
                    return;
                }

                action();
                _status = successMessage + " -> " + CleanDisplayText(actor.ActorName);
            }
            catch (Exception ex)
            {
                _status = "操作失败：" + ex.Message;
                _log.LogWarning(_status + "\n" + ex);
            }
        }

        private void AddTrinket(ActorInstance actor, ItemDefinition item)
        {
            if (actor == null || item == null)
            {
                return;
            }

            ItemInventory inventory = actor.GetTrinketInventory();
            if (inventory == null)
            {
                throw new InvalidOperationException("角色饰品栏不可用。");
            }

            if (inventory is TrinketItemInventory trinketInventory && !trinketInventory.GetCanEquip(item, false, true))
            {
                throw new InvalidOperationException("该角色不满足饰品装备条件。");
            }

            int leftover = inventory.AddItems(item, 1, false);
            if (leftover > 0)
            {
                throw new InvalidOperationException("饰品栏已满或无法装备该饰品。");
            }
        }

        private static string ActorLine(ActorInstance actor)
        {
            return $"{CleanDisplayText(actor.ActorName)}  队伍 {actor.TeamIndex}  生命 {actor.DisplayedHp:0}/{actor.DisplayedHpMax:0}  压力 {actor.Stress:0}/{actor.StressMax:0}";
        }

        private static string StatusLine(ActorInstance actor)
        {
            return "Token：" + SummarizeTokens(actor) + "\nBuff：" + SummarizeBuffs(actor) + "\nDOT：" + SummarizeDots(actor) + "\n怪癖：" + SummarizeQuirks(actor);
        }

        private static string SummarizeTokens(ActorInstance actor)
        {
            return Summarize(actor.TokenContainer?.GetInstances().Select(instance => TokenName(instance.Definition)));
        }

        private static string SummarizeBuffs(ActorInstance actor)
        {
            return Summarize(actor.BuffContainer?.GetInstances().Select(instance => BuffName(instance.Definition)));
        }

        private static string SummarizeDots(ActorInstance actor)
        {
            return Summarize(actor.DotContainer?.GetInstances().Select(instance => DotName(instance.Definition)));
        }

        private static string SummarizeQuirks(ActorInstance actor)
        {
            return actor.QuirkContainer == null ? "none" : Summarize(actor.QuirkContainer.GetInstances().Select(instance => QuirkName(instance.Definition)));
        }

        private static string Summarize(IEnumerable<string> names)
        {
            if (names == null) return "无";
            List<string> cleaned = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => Short(name, 18)).Take(12).ToList();
            return cleaned.Count == 0 ? "无" : string.Join(", ", cleaned.ToArray());
        }

        private static string TokenName(TokenDefinition token)
        {
            if (token == null) return "";
            try
            {
                string name = CleanDisplayText(TokenDescription.GetUnglyphedNameString(token));
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("token_name_" + token.Id)) return name;
            }
            catch
            {
            }

            return LocalizedText(token.Id, "token_name_" + token.Id, "token_" + token.Id);
        }

        private static string TokenDetail(TokenDefinition token)
        {
            if (token == null) return "";
            try
            {
                return TokenDescription.GetDetailString(token);
            }
            catch
            {
                return "";
            }
        }

        private static string BuffName(BuffDefinition buff)
        {
            if (buff == null) return "";
            string id = buff.Id;
            return LocalizedText(id, "buff_name_" + id, "buff_" + id, id);
        }

        private static string BuffDetail(BuffDefinition buff)
        {
            if (buff == null) return "";
            try
            {
                return BuffDescription.GetDescription(buff, true);
            }
            catch
            {
                return "";
            }
        }

        private static string DotName(DotDefinition dot)
        {
            if (dot == null) return "";
            string type = string.IsNullOrWhiteSpace(dot.m_Type) ? dot.Id : dot.m_Type;
            try
            {
                string name = CleanDisplayText(DotDescription.GetUnglyphedName(type));
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("dot_name_" + type)) return name;
            }
            catch
            {
            }

            return LocalizedText(dot.Id, "dot_name_" + type, "dot_" + type, dot.Id);
        }

        private static string DotDetail(DotDefinition dot)
        {
            if (dot == null) return "";
            try
            {
                int magnitude = DotDescription.GetDotMagnitude(dot);
                string type = string.IsNullOrWhiteSpace(dot.m_Type) ? dot.Id : dot.m_Type;
                return DotDescription.GetDescription(type, magnitude, false);
            }
            catch
            {
                return "";
            }
        }

        private static string QuirkName(QuirkDefinition quirk)
        {
            if (quirk == null) return "";
            try
            {
                string name = CleanDisplayText(QuirkDescription.GetNameString(quirk, (string)null, false));
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains(quirk.Id)) return name;
            }
            catch
            {
            }

            return LocalizedText(quirk.Id, quirk.Id, "quirk_name_" + quirk.Id, "quirk_" + quirk.Id);
        }

        private static string QuirkDetail(QuirkDefinition quirk)
        {
            if (quirk == null) return "";
            try
            {
                string description = QuirkDescription.GetDescriptionString(quirk, (string)null);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    return description;
                }
            }
            catch
            {
            }

            return quirk.IsDisease ? "疾病" : quirk.IsCurse ? "诅咒" : quirk.IsNegative ? "负面怪癖" : "正面怪癖";
        }

        private static string ItemName(ItemDefinition item)
        {
            if (item == null) return "";
            try
            {
                return ItemDescription.GetTitle(item, 1);
            }
            catch
            {
                return LocalizedText(item.m_id, "item_name_" + item.m_id);
            }
        }

        private static string ItemDetail(ItemDefinition item)
        {
            if (item == null) return "";
            try
            {
                return ItemDescription.GetDescription(item, -1, false, 0, false, false, false, false, true, null);
            }
            catch
            {
                return "";
            }
        }

        private Sprite GetCatalogSprite<T>(CatalogChoice<T> choice)
        {
            if (choice == null || choice.Value == null)
            {
                return null;
            }

            if (choice.Value is TokenDefinition token)
            {
                return GetTokenSprite(token.Id);
            }

            if (choice.Value is BuffDefinition buff)
            {
                return GetBuffSprite(buff.Id);
            }

            if (choice.Value is DotDefinition dot)
            {
                return GetDotSprite(string.IsNullOrWhiteSpace(dot.m_Type) ? dot.Id : dot.m_Type);
            }

            if (choice.Value is QuirkDefinition quirk)
            {
                StatusKind kind = quirk.IsDisease || quirk.IsCurse
                    ? StatusKind.Disease
                    : quirk.IsNegative
                        ? StatusKind.NegativeQuirk
                        : StatusKind.PositiveQuirk;
                return GetQuirkSprite(kind);
            }

            if (choice.Value is ItemDefinition item)
            {
                return GetItemSprite(item);
            }

            return null;
        }

        private static bool ShouldDrawCatalogRowIcon<T>(T value)
        {
            return !(value is BuffDefinition) && !(value is DotDefinition);
        }

        private static bool ShouldDrawLargeCatalogIcon<T>(T value)
        {
            return !(value is BuffDefinition) && !(value is DotDefinition);
        }

        private static bool ShouldDrawStatusIcon(StatusKind kind)
        {
            return kind != StatusKind.Buff && kind != StatusKind.Dot;
        }

        private void DrawSpriteLayout(Sprite sprite, float size)
        {
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            GUI.DrawTexture(rect, _panelAltTex);
            DrawSprite(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), sprite);
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            DrawTextureSection(rect, sprite.texture, sprite.textureRect);
        }

        private static void DrawInlineIcon(Rect rect, InlineIconData icon)
        {
            if (icon.Sprite != null)
            {
                DrawSprite(rect, icon.Sprite);
                return;
            }

            if (icon.Texture != null)
            {
                DrawTextureSection(rect, icon.Texture, icon.TextureRect);
            }
        }

        private static void DrawTextureSection(Rect rect, Texture texture, Rect textureRect)
        {
            if (texture == null || textureRect.width <= 0f || textureRect.height <= 0f)
            {
                return;
            }

            Rect fitted = FitRectToAspect(rect, textureRect.width, textureRect.height);
            Rect coords = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(fitted, texture, coords, true);
        }

        private static Rect FitRectToAspect(Rect bounds, float width, float height)
        {
            if (width <= 0f || height <= 0f || bounds.width <= 0f || bounds.height <= 0f)
            {
                return bounds;
            }

            float target = width / height;
            float current = bounds.width / bounds.height;
            if (current > target)
            {
                float fittedWidth = bounds.height * target;
                return new Rect(bounds.x + (bounds.width - fittedWidth) * 0.5f, bounds.y, fittedWidth, bounds.height);
            }

            float fittedHeight = bounds.width / target;
            return new Rect(bounds.x, bounds.y + (bounds.height - fittedHeight) * 0.5f, bounds.width, fittedHeight);
        }

        private Sprite GetItemSprite(ItemDefinition item)
        {
            if (item == null)
            {
                return null;
            }

            if (_itemSprites.TryGetValue(item.m_id, out Sprite cached))
            {
                return cached;
            }

            try
            {
                if (!Singleton<ResourceDatabaseItem>.HasInstance())
                {
                    _itemSprites[item.m_id] = null;
                    return null;
                }

                ResourceItem resource = Singleton<ResourceDatabaseItem>.Instance.GetResource(item.m_id, true, null);
                if (resource?.m_iconPrefab == null || !resource.m_iconPrefab.RuntimeKeyIsValid())
                {
                    _itemSprites[item.m_id] = null;
                    return null;
                }

                AsyncOperationHandle<GameObject> handle = resource.m_iconPrefab.LoadAssetAsync();
                GameObject prefab = handle.WaitForCompletion();
                Image image = prefab != null ? prefab.GetComponentInChildren<Image>(true) : null;
                Sprite sprite = image != null ? image.sprite : null;
                _itemSprites[item.m_id] = sprite;
                return sprite;
            }
            catch
            {
                _itemSprites[item.m_id] = null;
                return null;
            }
        }

        private Sprite GetTokenSprite(string tokenId)
        {
            if (string.IsNullOrWhiteSpace(tokenId))
            {
                return null;
            }

            if (_tokenSprites.TryGetValue(tokenId, out Sprite cached))
            {
                return cached;
            }

            if (_missingTokenSpriteRetryAt.TryGetValue(tokenId, out float retryAt) && Time.unscaledTime < retryAt)
            {
                return null;
            }

            Sprite sprite = null;
            try
            {
                if (Singleton<ResourceDatabaseTokens>.HasInstance())
                {
                    ResourceToken resource = Singleton<ResourceDatabaseTokens>.Instance.GetResource(tokenId, true, null);
                    sprite = resource?.m_TokenSprite;
                }
            }
            catch
            {
                sprite = null;
            }

            if (sprite != null)
            {
                _tokenSprites[tokenId] = sprite;
                _missingTokenSpriteRetryAt.Remove(tokenId);
            }
            else
            {
                _missingTokenSpriteRetryAt[tokenId] = Time.unscaledTime + 5f;
            }

            return sprite;
        }

        private Sprite GetDotSprite(string dotType)
        {
            if (string.IsNullOrWhiteSpace(dotType))
            {
                return null;
            }

            if (_dotSprites.TryGetValue(dotType, out Sprite cached))
            {
                return cached;
            }

            if (_missingDotSpriteRetryAt.TryGetValue(dotType, out float retryAt) && Time.unscaledTime < retryAt)
            {
                return null;
            }

            Sprite sprite = null;
            try
            {
                if (Singleton<ResourceDatabaseDots>.HasInstance())
                {
                    ResourceDot resource = Singleton<ResourceDatabaseDots>.Instance.GetResource(dotType, true, true);
                    sprite = resource?.m_DotSprite;
                }
            }
            catch
            {
                sprite = null;
            }

            if (sprite != null)
            {
                _dotSprites[dotType] = sprite;
                _missingDotSpriteRetryAt.Remove(dotType);
            }
            else
            {
                _missingDotSpriteRetryAt[dotType] = Time.unscaledTime + 5f;
            }

            return sprite;
        }

        private Sprite GetBuffSprite(string buffId)
        {
            if (string.IsNullOrWhiteSpace(buffId))
            {
                return null;
            }

            if (_buffSprites.TryGetValue(buffId, out Sprite cached))
            {
                return cached;
            }

            if (_missingBuffSpriteRetryAt.TryGetValue(buffId, out float retryAt) && Time.unscaledTime < retryAt)
            {
                return null;
            }

            Sprite sprite = null;
            try
            {
                BuffDefinition definition = SingletonMonoBehaviour<Library<string, BuffDefinition>>.HasInstance(false)
                    ? SingletonMonoBehaviour<Library<string, BuffDefinition>>.Instance.GetLibraryElement(buffId)
                    : null;
                if (definition != null)
                {
                    foreach (BuffIconBhv icon in Resources.FindObjectsOfTypeAll<BuffIconBhv>())
                    {
                        if (!BuffIconMatchesDefinition(icon, definition))
                        {
                            continue;
                        }

                        BuffIconProperties properties = GetBuffIconProperties(icon);
                        if (properties?.Icon != null)
                        {
                            sprite = properties.Icon;
                            break;
                        }
                    }
                }
            }
            catch
            {
                sprite = null;
            }

            if (sprite != null)
            {
                _buffSprites[buffId] = sprite;
                _missingBuffSpriteRetryAt.Remove(buffId);
            }
            else
            {
                _missingBuffSpriteRetryAt[buffId] = Time.unscaledTime + 5f;
            }

            return sprite;
        }

        private static bool BuffIconMatchesDefinition(BuffIconBhv icon, BuffDefinition definition)
        {
            if (icon == null || definition == null || icon.RequiredTagCount <= 0 || definition.Tags == null || definition.Tags.Count == 0)
            {
                return false;
            }

            int matches = 0;
            foreach (string tag in definition.Tags)
            {
                if (icon.HasTagRequirement(tag))
                {
                    matches++;
                }
            }

            return matches == icon.RequiredTagCount;
        }

        private static BuffIconProperties GetBuffIconProperties(BuffIconBhv icon)
        {
            FieldInfo field = typeof(BuffIconBhv).GetField("m_iconProperties", BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : field.GetValue(icon) as BuffIconProperties;
        }

        private Sprite GetQuirkSprite(StatusKind kind)
        {
            string key = kind.ToString();
            if (_quirkSprites.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = null;
            try
            {
                string fieldName = kind == StatusKind.PositiveQuirk
                    ? "m_positiveSprite"
                    : kind == StatusKind.NegativeQuirk
                        ? "m_negativeSprite"
                        : "m_diseaseSprite";
                foreach (QuirkLogEntryBhv entry in Resources.FindObjectsOfTypeAll<QuirkLogEntryBhv>())
                {
                    FieldInfo field = typeof(QuirkLogEntryBhv).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                    sprite = field == null ? null : field.GetValue(entry) as Sprite;
                    if (sprite != null)
                    {
                        break;
                    }
                }
            }
            catch
            {
                sprite = null;
            }

            _quirkSprites[key] = sprite;
            return sprite;
        }

        private void DrawInlineDescription(string text, float minHeight)
        {
            IList<InlineSegment> segments = GetCachedInlineSegments(text);
            float width = Mathf.Max(200f, GUILayoutUtility.GetLastRect().width);
            float height = Mathf.Max(minHeight, CalculateInlineHeight(segments, _detailStyle, Mathf.Max(240f, _windowRect.width * 0.34f)));
            Rect rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, _panelAltTex);
            LayoutInlineSegments(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), segments, _detailStyle, true);
        }

        private IList<InlineSegment> GetCachedInlineSegments(string text)
        {
            string key = text ?? string.Empty;
            if (_inlineSegmentCache.TryGetValue(key, out IList<InlineSegment> cached))
            {
                return cached;
            }

            if (_inlineSegmentCache.Count > 512)
            {
                _inlineSegmentCache.Clear();
            }

            cached = BuildInlineSegments(key);
            _inlineSegmentCache[key] = cached;
            return cached;
        }

        private IList<InlineSegment> BuildInlineSegments(string rawText)
        {
            List<InlineSegment> segments = new List<InlineSegment>();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return segments;
            }

            int cursor = 0;
            foreach (Match match in SpriteTagRegex.Matches(rawText))
            {
                if (match.Index > cursor)
                {
                    AddInlineTextSegment(segments, rawText.Substring(cursor, match.Index - cursor));
                }

                string attrs = match.Groups["attrs"].Value;
                string spriteName = GetSpriteTagAttribute(attrs, "name");
                string spriteAssetName = GetSpriteTagAttribute(attrs, "sprite");
                if (string.IsNullOrWhiteSpace(spriteAssetName))
                {
                    spriteAssetName = GetSpriteTagShorthand(attrs);
                }
                if (string.IsNullOrWhiteSpace(spriteName))
                {
                    spriteName = spriteAssetName;
                    spriteAssetName = string.Empty;
                }

                string spriteIndexText = GetSpriteTagAttribute(attrs, "index");
                bool iconAdded = false;
                int spriteIndex;
                if ((!string.IsNullOrWhiteSpace(spriteIndexText) &&
                        int.TryParse(spriteIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out spriteIndex)) ||
                    (!string.IsNullOrWhiteSpace(spriteName) &&
                        int.TryParse(spriteName, NumberStyles.Integer, CultureInfo.InvariantCulture, out spriteIndex)))
                {
                    InlineIconData indexIcon = string.IsNullOrWhiteSpace(spriteAssetName)
                        ? GetTmpSpriteIcon(spriteIndex)
                        : GetTmpSpriteIcon(spriteAssetName, spriteIndex);
                    if (indexIcon.IsValid)
                    {
                        segments.Add(new InlineSegment(indexIcon));
                        iconAdded = true;
                    }
                }

                if (iconAdded)
                {
                    cursor = match.Index + match.Length;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(spriteName))
                {
                    spriteName = GetSpriteTagShorthand(attrs);
                }

                foreach (string spriteId in GetSpriteIdCandidates(spriteName))
                {
                    InlineIconData icon = GetInlineSpriteIcon(spriteAssetName, spriteId);
                    if (icon.IsValid)
                    {
                        segments.Add(new InlineSegment(icon));
                        break;
                    }
                }

                cursor = match.Index + match.Length;
            }

            if (cursor < rawText.Length)
            {
                AddInlineTextSegment(segments, rawText.Substring(cursor));
            }

            return segments;
        }

        private InlineIconData GetInlineSpriteIcon(string spriteAssetName, string spriteId)
        {
            if (string.IsNullOrWhiteSpace(spriteId))
            {
                return default(InlineIconData);
            }

            string cacheKey = BuildInlineSpriteCacheKey(spriteAssetName, spriteId);
            if (_inlineSpriteIcons.TryGetValue(cacheKey, out InlineIconData cached))
            {
                if (cached.IsValid)
                {
                    return cached;
                }

                _inlineSpriteIcons.Remove(cacheKey);
            }

            if (_missingInlineSpriteRetryAt.TryGetValue(cacheKey, out float retryAt) && Time.unscaledTime < retryAt)
            {
                return default(InlineIconData);
            }

            Sprite tokenSprite = GetTokenSprite(spriteId);
            Sprite dotSprite = tokenSprite == null ? GetDotSprite(spriteId) : null;
            Sprite buffSprite = tokenSprite == null && dotSprite == null ? GetBuffSprite(spriteId) : null;
            InlineIconData icon = tokenSprite != null
                ? new InlineIconData(tokenSprite)
                : dotSprite != null
                    ? new InlineIconData(dotSprite)
                    : buffSprite != null
                        ? new InlineIconData(buffSprite)
                        : string.IsNullOrWhiteSpace(spriteAssetName)
                            ? GetTmpSpriteIcon(spriteId)
                            : GetTmpSpriteIcon(spriteAssetName, spriteId);
            if (icon.IsValid)
            {
                _inlineSpriteIcons[cacheKey] = icon;
                _missingInlineSpriteRetryAt.Remove(cacheKey);
            }
            else
            {
                _missingInlineSpriteRetryAt[cacheKey] = Time.unscaledTime + 5f;
            }

            return icon;
        }

        private static string BuildInlineSpriteCacheKey(string spriteAssetName, string spriteId)
        {
            string asset = string.IsNullOrWhiteSpace(spriteAssetName) ? string.Empty : spriteAssetName.Trim().Trim('"', '\'');
            string id = string.IsNullOrWhiteSpace(spriteId) ? string.Empty : spriteId.Trim();
            return asset + "::" + id;
        }

        private static string GetSpriteTagAttribute(string attrs, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attrs) || string.IsNullOrWhiteSpace(attributeName))
            {
                return string.Empty;
            }

            foreach (Match match in SpriteTagAttributeRegex.Matches(attrs))
            {
                if (string.Equals(match.Groups["key"].Value, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return match.Groups["value"].Value;
                }
            }

            return string.Empty;
        }

        private static string GetSpriteTagShorthand(string attrs)
        {
            if (string.IsNullOrWhiteSpace(attrs))
            {
                return string.Empty;
            }

            Match match = SpriteTagShorthandRegex.Match(attrs);
            return match.Success ? match.Groups["value"].Value : string.Empty;
        }

        private static IEnumerable<string> GetSpriteIdCandidates(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                yield break;
            }

            List<string> candidates = new List<string>();
            AddCandidate(candidates, spriteName.Trim().Trim('"', '\''));
            string normalized = candidates[0];
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex < normalized.Length - 1)
            {
                AddCandidate(candidates, normalized.Substring(slashIndex + 1));
            }

            int colonIndex = normalized.LastIndexOf(':');
            if (colonIndex >= 0 && colonIndex < normalized.Length - 1)
            {
                AddCandidate(candidates, normalized.Substring(colonIndex + 1));
            }

            string lower = normalized.ToLowerInvariant();
            AddCandidate(candidates, lower);
            if (lower.StartsWith("token_"))
            {
                AddCandidate(candidates, normalized.Substring("token_".Length));
            }

            if (lower.StartsWith("icon_token_"))
            {
                AddCandidate(candidates, normalized.Substring("icon_token_".Length));
            }

            if (lower.StartsWith("tokens_"))
            {
                AddCandidate(candidates, normalized.Substring("tokens_".Length));
            }

            if (lower.StartsWith("icon_"))
            {
                AddCandidate(candidates, normalized.Substring("icon_".Length));
            }

            if (lower.StartsWith("sprite_"))
            {
                AddCandidate(candidates, normalized.Substring("sprite_".Length));
            }

            foreach (string candidate in candidates.ToArray())
            {
                string compact = candidate == null ? string.Empty : candidate.Trim();
                if (string.IsNullOrWhiteSpace(compact))
                {
                    continue;
                }

                string compactLower = compact.ToLowerInvariant();
                AddCandidate(candidates, compactLower);
                AddCandidate(candidates, compactLower.Replace('-', '_'));

                string prefixBase = compactLower;
                if (prefixBase.StartsWith("icon_"))
                {
                    prefixBase = prefixBase.Substring("icon_".Length);
                }

                AddCandidate(candidates, "icon_" + prefixBase);
                AddCandidate(candidates, "sprite_" + prefixBase);
                AddCandidate(candidates, "token_" + prefixBase);
                AddCandidate(candidates, "tokens_" + prefixBase);
                AddCandidate(candidates, "icon_token_" + prefixBase);
                AddCandidate(candidates, "buff_" + prefixBase);
                AddCandidate(candidates, "dot_" + prefixBase);
                AddCandidate(candidates, "debuff_" + prefixBase);

                if (prefixBase.EndsWith("_icon", StringComparison.Ordinal) && prefixBase.Length > "_icon".Length)
                {
                    AddCandidate(candidates, prefixBase.Substring(0, prefixBase.Length - "_icon".Length));
                }

                string[] visualSuffixes =
                {
                    "_white",
                    "_black",
                    "_outline",
                    "_filled",
                    "_small",
                    "_large",
                    "_positive",
                    "_negative",
                };
                foreach (string suffix in visualSuffixes)
                {
                    if (!prefixBase.EndsWith(suffix, StringComparison.Ordinal) || prefixBase.Length <= suffix.Length)
                    {
                        continue;
                    }

                    string stripped = prefixBase.Substring(0, prefixBase.Length - suffix.Length);
                    AddCandidate(candidates, stripped);
                    AddCandidate(candidates, "icon_" + stripped);
                    AddCandidate(candidates, "sprite_" + stripped);
                    AddCandidate(candidates, "token_" + stripped);
                    AddCandidate(candidates, "tokens_" + stripped);
                    AddCandidate(candidates, "icon_token_" + stripped);
                    AddCandidate(candidates, "buff_" + stripped);
                    AddCandidate(candidates, "dot_" + stripped);
                    AddCandidate(candidates, "debuff_" + stripped);
                }
            }

            foreach (string candidate in candidates)
            {
                yield return candidate;
            }
        }

        private static void AddCandidate(List<string> candidates, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidates.Contains(candidate))
            {
                return;
            }

            candidates.Add(candidate);
        }

        private static InlineIconData GetTmpSpriteIcon(string spriteName)
        {
            try
            {
                Type settingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
                if (settingsType == null)
                {
                    return default(InlineIconData);
                }

                PropertyInfo defaultSpriteAssetProperty = settingsType.GetProperty(
                    "defaultSpriteAsset",
                    BindingFlags.Public | BindingFlags.Static);
                object defaultSpriteAsset = defaultSpriteAssetProperty == null
                    ? null
                    : defaultSpriteAssetProperty.GetValue(null, null);
                InlineIconData icon = FindTmpSpriteIcon(defaultSpriteAsset, spriteName);
                return icon.IsValid ? icon : FindLoadedTmpSpriteIcon(null, spriteName);
            }
            catch
            {
                return default(InlineIconData);
            }
        }

        private static InlineIconData GetTmpSpriteIcon(int spriteIndex)
        {
            if (spriteIndex < 0)
            {
                return default(InlineIconData);
            }

            try
            {
                Type settingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
                if (settingsType == null)
                {
                    return default(InlineIconData);
                }

                PropertyInfo defaultSpriteAssetProperty = settingsType.GetProperty(
                    "defaultSpriteAsset",
                    BindingFlags.Public | BindingFlags.Static);
                object defaultSpriteAsset = defaultSpriteAssetProperty == null
                    ? null
                    : defaultSpriteAssetProperty.GetValue(null, null);
                InlineIconData icon = FindTmpSpriteIcon(defaultSpriteAsset, spriteIndex);
                return icon.IsValid ? icon : FindLoadedTmpSpriteIcon(null, spriteIndex);
            }
            catch
            {
                return default(InlineIconData);
            }
        }

        private static InlineIconData GetTmpSpriteIcon(string spriteAssetName, string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return default(InlineIconData);
            }

            try
            {
                object spriteAsset = TryLoadTmpSpriteAsset(spriteAssetName);
                InlineIconData icon = FindTmpSpriteIcon(spriteAsset, spriteName);
                if (icon.IsValid)
                {
                    return icon;
                }

                icon = FindLoadedTmpSpriteIcon(spriteAssetName, spriteName);
                return icon.IsValid ? icon : GetTmpSpriteIcon(spriteName);
            }
            catch
            {
                return default(InlineIconData);
            }
        }

        private static InlineIconData GetTmpSpriteIcon(string spriteAssetName, int spriteIndex)
        {
            if (spriteIndex < 0)
            {
                return default(InlineIconData);
            }

            try
            {
                object spriteAsset = TryLoadTmpSpriteAsset(spriteAssetName);
                InlineIconData icon = FindTmpSpriteIcon(spriteAsset, spriteIndex);
                if (icon.IsValid)
                {
                    return icon;
                }

                icon = FindLoadedTmpSpriteIcon(spriteAssetName, spriteIndex);
                return icon.IsValid ? icon : GetTmpSpriteIcon(spriteIndex);
            }
            catch
            {
                return default(InlineIconData);
            }
        }

        private static InlineIconData FindTmpSpriteIcon(object spriteAsset, string spriteName)
        {
            if (spriteAsset == null || string.IsNullOrWhiteSpace(spriteName))
            {
                return default(InlineIconData);
            }

            InlineIconData icon = FindTmpSpriteIconInAsset(spriteAsset, spriteName);
            if (icon.IsValid)
            {
                return icon;
            }

            object fallbackAssets = GetMemberValue(spriteAsset, "fallbackSpriteAssets");
            if (!(fallbackAssets is System.Collections.IEnumerable fallbackEnumerable))
            {
                return default(InlineIconData);
            }

            foreach (object fallbackAsset in fallbackEnumerable)
            {
                icon = FindTmpSpriteIconInAsset(fallbackAsset, spriteName);
                if (icon.IsValid)
                {
                    return icon;
                }
            }

            return default(InlineIconData);
        }

        private static InlineIconData FindTmpSpriteIcon(object spriteAsset, int spriteIndex)
        {
            if (spriteAsset == null || spriteIndex < 0)
            {
                return default(InlineIconData);
            }

            InlineIconData icon = FindTmpSpriteIconInAsset(spriteAsset, spriteIndex);
            if (icon.IsValid)
            {
                return icon;
            }

            object fallbackAssets = GetMemberValue(spriteAsset, "fallbackSpriteAssets");
            if (!(fallbackAssets is System.Collections.IEnumerable fallbackEnumerable))
            {
                return default(InlineIconData);
            }

            foreach (object fallbackAsset in fallbackEnumerable)
            {
                icon = FindTmpSpriteIconInAsset(fallbackAsset, spriteIndex);
                if (icon.IsValid)
                {
                    return icon;
                }
            }

            return default(InlineIconData);
        }

        private static object TryLoadTmpSpriteAsset(string spriteAssetName)
        {
            if (string.IsNullOrWhiteSpace(spriteAssetName))
            {
                return null;
            }

            string normalized = spriteAssetName.Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            try
            {
                Type spriteAssetType = Type.GetType("TMPro.TMP_SpriteAsset, Unity.TextMeshPro");
                if (spriteAssetType == null)
                {
                    return null;
                }

                object asset = Resources.Load(normalized, spriteAssetType);
                if (asset != null)
                {
                    return asset;
                }

                if (normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    string withoutExtension = normalized.Substring(0, normalized.Length - ".asset".Length);
                    asset = Resources.Load(withoutExtension, spriteAssetType);
                    if (asset != null)
                    {
                        return asset;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static InlineIconData FindLoadedTmpSpriteIcon(string spriteAssetName, string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return default(InlineIconData);
            }

            try
            {
                Type spriteAssetType = Type.GetType("TMPro.TMP_SpriteAsset, Unity.TextMeshPro");
                if (spriteAssetType == null)
                {
                    return default(InlineIconData);
                }

                UnityEngine.Object[] assets = Resources.FindObjectsOfTypeAll(spriteAssetType);
                InlineIconData fallback = default(InlineIconData);
                foreach (UnityEngine.Object asset in assets)
                {
                    if (asset == null)
                    {
                        continue;
                    }

                    InlineIconData icon = FindTmpSpriteIconInAsset(asset, spriteName);
                    if (!icon.IsValid)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(spriteAssetName) || TmpSpriteAssetNameMatches(asset, spriteAssetName))
                    {
                        return icon;
                    }

                    if (!fallback.IsValid)
                    {
                        fallback = icon;
                    }
                }

                return fallback;
            }
            catch
            {
                return default(InlineIconData);
            }
        }

        private static InlineIconData FindLoadedTmpSpriteIcon(string spriteAssetName, int spriteIndex)
        {
            if (spriteIndex < 0)
            {
                return default(InlineIconData);
            }

            try
            {
                Type spriteAssetType = Type.GetType("TMPro.TMP_SpriteAsset, Unity.TextMeshPro");
                if (spriteAssetType == null)
                {
                    return default(InlineIconData);
                }

                UnityEngine.Object[] assets = Resources.FindObjectsOfTypeAll(spriteAssetType);
                InlineIconData fallback = default(InlineIconData);
                foreach (UnityEngine.Object asset in assets)
                {
                    if (asset == null)
                    {
                        continue;
                    }

                    InlineIconData icon = FindTmpSpriteIconInAsset(asset, spriteIndex);
                    if (!icon.IsValid)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(spriteAssetName) || TmpSpriteAssetNameMatches(asset, spriteAssetName))
                    {
                        return icon;
                    }

                    if (!fallback.IsValid)
                    {
                        fallback = icon;
                    }
                }

                return fallback;
            }
            catch
            {
                return default(InlineIconData);
            }
        }

        private static bool TmpSpriteAssetNameMatches(UnityEngine.Object asset, string spriteAssetName)
        {
            if (asset == null || string.IsNullOrWhiteSpace(spriteAssetName))
            {
                return false;
            }

            string normalized = spriteAssetName.Trim().Trim('"', '\'');
            if (normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - ".asset".Length);
            }

            string assetName = asset.name ?? string.Empty;
            return string.Equals(assetName, normalized, StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/" + assetName, StringComparison.OrdinalIgnoreCase) ||
                assetName.EndsWith("/" + normalized, StringComparison.OrdinalIgnoreCase);
        }

        private static InlineIconData FindTmpSpriteIconInAsset(object spriteAsset, string spriteName)
        {
            if (spriteAsset == null)
            {
                return default(InlineIconData);
            }

            Texture texture = GetMemberValue(spriteAsset, "atlasTexture") as Texture;
            if (texture == null)
            {
                texture = GetMemberValue(spriteAsset, "spriteSheet") as Texture;
            }

            if (texture == null)
            {
                return default(InlineIconData);
            }

            object tableObject = GetMemberValue(spriteAsset, "spriteCharacterTable");
            if (!(tableObject is System.Collections.IEnumerable table))
            {
                return default(InlineIconData);
            }

            foreach (object spriteCharacter in table)
            {
                string name = GetMemberValue(spriteCharacter, "name") as string;
                if (!string.Equals(name, spriteName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return BuildTmpSpriteIcon(texture, spriteCharacter);
            }

            return default(InlineIconData);
        }

        private static InlineIconData FindTmpSpriteIconInAsset(object spriteAsset, int spriteIndex)
        {
            if (spriteAsset == null || spriteIndex < 0)
            {
                return default(InlineIconData);
            }

            Texture texture = GetMemberValue(spriteAsset, "atlasTexture") as Texture;
            if (texture == null)
            {
                texture = GetMemberValue(spriteAsset, "spriteSheet") as Texture;
            }

            if (texture == null)
            {
                return default(InlineIconData);
            }

            object tableObject = GetMemberValue(spriteAsset, "spriteCharacterTable");
            if (!(tableObject is System.Collections.IEnumerable table))
            {
                return default(InlineIconData);
            }

            if (tableObject is System.Collections.IList list && spriteIndex < list.Count)
            {
                return BuildTmpSpriteIcon(texture, list[spriteIndex]);
            }

            int rowIndex = 0;
            foreach (object spriteCharacter in table)
            {
                int unicode = ToInt(GetMemberValue(spriteCharacter, "unicode"));
                int glyphIndex = ToInt(GetMemberValue(spriteCharacter, "glyphIndex"));
                if (rowIndex == spriteIndex || unicode == spriteIndex || glyphIndex == spriteIndex)
                {
                    return BuildTmpSpriteIcon(texture, spriteCharacter);
                }

                rowIndex++;
            }

            return default(InlineIconData);
        }

        private static InlineIconData BuildTmpSpriteIcon(Texture texture, object spriteCharacter)
        {
            if (texture == null || spriteCharacter == null)
            {
                return default(InlineIconData);
            }

            object glyph = GetMemberValue(spriteCharacter, "glyph");
            object glyphRect = GetMemberValue(glyph, "glyphRect") ?? GetMemberValue(spriteCharacter, "glyphRect");
            float x = ToFloat(GetMemberValue(glyphRect, "x"));
            float y = ToFloat(GetMemberValue(glyphRect, "y"));
            float width = ToFloat(GetMemberValue(glyphRect, "width"));
            float height = ToFloat(GetMemberValue(glyphRect, "height"));
            if (width <= 0f || height <= 0f)
            {
                return default(InlineIconData);
            }

            return new InlineIconData(texture, new Rect(x, y, width, height));
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = type.GetField(memberName, flags);
            return field == null ? null : field.GetValue(instance);
        }

        private static float ToFloat(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0f;
            }
        }

        private static int ToInt(object value)
        {
            if (value == null)
            {
                return -1;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return -1;
            }
        }

        private static void AddInlineTextSegment(List<InlineSegment> segments, string text)
        {
            string cleaned = RichTextTagRegex.Replace(text ?? string.Empty, string.Empty).Replace("\r", string.Empty);
            if (!string.IsNullOrEmpty(cleaned))
            {
                segments.Add(new InlineSegment(cleaned));
            }
        }

        private static float CalculateInlineHeight(IList<InlineSegment> segments, GUIStyle style, float width)
        {
            return LayoutInlineSegments(new Rect(0f, 0f, width, 9999f), segments, style, false);
        }

        private static float LayoutInlineSegments(Rect rect, IList<InlineSegment> segments, GUIStyle style, bool draw)
        {
            if (segments == null || segments.Count == 0 || rect.width <= 0f)
            {
                return 0f;
            }

            GUIStyle inlineStyle = new GUIStyle(style) { wordWrap = false, clipping = TextClipping.Clip };
            float lineHeight = Mathf.Max(20f, inlineStyle.CalcHeight(new GUIContent("Mg"), rect.width));
            float iconSize = Mathf.Min(20f, lineHeight);
            float x = rect.x;
            float y = rect.y;
            bool lineHasContent = false;
            bool anyContent = false;

            foreach (InlineSegment segment in segments)
            {
                if (segment.Kind == InlineSegmentKind.Icon)
                {
                    float advance = iconSize + 4f;
                    if (x + advance > rect.xMax && lineHasContent)
                    {
                        y += lineHeight;
                        x = rect.x;
                        lineHasContent = false;
                    }

                    if (draw && y < rect.yMax)
                    {
                        DrawInlineIcon(new Rect(x, y + (lineHeight - iconSize) * 0.5f, iconSize, iconSize), segment.Icon);
                    }

                    x += advance;
                    lineHasContent = true;
                    anyContent = true;
                    continue;
                }

                string[] lines = (segment.Text ?? string.Empty).Split('\n');
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (lineIndex > 0)
                    {
                        y += lineHeight;
                        x = rect.x;
                        lineHasContent = false;
                    }

                    foreach (Match wordMatch in InlineWordRegex.Matches(lines[lineIndex]))
                    {
                        string token = wordMatch.Value;
                        if (string.IsNullOrEmpty(token))
                        {
                            continue;
                        }

                        bool whitespace = string.IsNullOrWhiteSpace(token);
                        if (whitespace && !lineHasContent)
                        {
                            continue;
                        }

                        float tokenWidth = inlineStyle.CalcSize(new GUIContent(token)).x;
                        if (x + tokenWidth > rect.xMax && lineHasContent)
                        {
                            y += lineHeight;
                            x = rect.x;
                            lineHasContent = false;
                            if (whitespace)
                            {
                                continue;
                            }
                        }

                        if (!whitespace && draw && y < rect.yMax)
                        {
                            GUI.Label(new Rect(x, y, tokenWidth + 4f, lineHeight), token, inlineStyle);
                        }

                        x += tokenWidth;
                        if (!whitespace)
                        {
                            lineHasContent = true;
                            anyContent = true;
                        }
                    }
                }
            }

            return anyContent ? Mathf.Max(lineHeight, (lineHasContent ? y + lineHeight : y) - rect.y) : 0f;
        }

        private static string LocalizedText(string fallback, params string[] keys)
        {
            if (!Singleton<Localization>.HasInstance()) return CleanDisplayText(fallback);
            try
            {
                foreach (string key in keys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    string text = CleanDisplayText(Singleton<Localization>.Instance.TryGetString(key, false));
                    if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return text;
                    }
                }
            }
            catch
            {
            }

            return CleanDisplayText(fallback);
        }

        private static string CleanDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = RichTextTagRegex.Replace(text, "");
            cleaned = cleaned.Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "" : cleaned;
        }

        private static string CleanDescriptionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = Regex.Replace(text, "<(?!sprite\\b)[^>]*>", "", RegexOptions.IgnoreCase);
            return cleaned.Replace("\r", "").Trim();
        }

        private static bool MatchesAny(string search, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            return values.Any(value => !string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string Short(string value, int maxLength = 90)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string cleaned = CleanDisplayText(value);
            if (cleaned.Length <= maxLength) return cleaned;
            return cleaned.Substring(0, Mathf.Max(4, maxLength - 3)) + "...";
        }

        private static int ParseAmount(string value, int min, int max)
        {
            if (!int.TryParse(value, out int parsed)) parsed = min;
            return Mathf.Clamp(parsed, min, max);
        }

        private void HandleDrawException(Exception ex)
        {
            _loaded = false;
            _reloadAfterDrawError = true;
            _status = "UI 发现过期游戏数据并恢复：" + ex.GetType().Name + "，正在重新加载。";
            if (Time.unscaledTime >= _nextDrawErrorLogTime)
            {
                _nextDrawErrorLogTime = Time.unscaledTime + 5f;
                _log.LogWarning(_status + "\n" + ex);
            }
        }

        private void HandleResize()
        {
            Event evt = Event.current;
            if (evt == null)
            {
                return;
            }

            Rect grip = new Rect(_windowRect.xMax - 34f, _windowRect.yMax - 34f, 26f, 26f);
            if (evt.type == EventType.MouseDown && grip.Contains(evt.mousePosition))
            {
                _resizing = true;
                _resizeStartMouse = evt.mousePosition;
                _resizeStartSize = new Vector2(_windowRect.width, _windowRect.height);
                evt.Use();
            }
            else if (_resizing && evt.type == EventType.MouseDrag)
            {
                Vector2 delta = evt.mousePosition - _resizeStartMouse;
                _windowRect.width = Mathf.Clamp(_resizeStartSize.x + delta.x, MinWidth, Screen.width - 32f);
                _windowRect.height = Mathf.Clamp(_resizeStartSize.y + delta.y, MinHeight, Screen.height - 32f);
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                _resizing = false;
            }
        }

        private void DrawResizeHandle()
        {
            Rect grip = new Rect(_windowRect.width - 32f, _windowRect.height - 32f, 22f, 22f);
            GUI.DrawTexture(grip, _resizeTex);
        }

        private void EnsureStyles()
        {
            if (_stylesReady && Mathf.Approximately(_styleFontScale, _fontScale)) return;

            _overlayTex = Tex(new Color(0f, 0f, 0f, 0.56f));
            _windowTex = Tex(new Color(0.055f, 0.06f, 0.07f, 0.98f));
            _panelTex = Tex(new Color(0.09f, 0.10f, 0.12f, 0.96f));
            _panelAltTex = Tex(new Color(0.13f, 0.145f, 0.16f, 0.98f));
            _accentTex = Tex(new Color(0.14f, 0.48f, 0.62f, 1f));
            _accentDimTex = Tex(new Color(0.13f, 0.28f, 0.34f, 1f));
            _buttonTex = Tex(new Color(0.17f, 0.185f, 0.20f, 1f));
            _buttonHoverTex = Tex(new Color(0.20f, 0.24f, 0.27f, 1f));
            _dangerTex = Tex(new Color(0.42f, 0.16f, 0.13f, 1f));
            _resizeTex = Tex(new Color(0.33f, 0.60f, 0.68f, 1f));
            int headerFont = ScaleFont(24);
            int subHeaderFont = ScaleFont(16);
            int smallFont = ScaleFont(14);
            int mutedFont = ScaleFont(13);

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = _windowTex, textColor = Color.white },
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(8, 8, 8, 8)
            };
            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = headerFont, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.88f, 0.95f, 1f) } };
            _subHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = subHeaderFont, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.88f, 0.93f, 0.96f) } };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = smallFont, normal = { textColor = new Color(0.88f, 0.90f, 0.92f) }, wordWrap = true };
            _mutedStyle = new GUIStyle(GUI.skin.label) { fontSize = mutedFont, normal = { textColor = new Color(0.60f, 0.66f, 0.70f) }, wordWrap = true };
            _detailStyle = new GUIStyle(GUI.skin.label) { fontSize = mutedFont, normal = { textColor = new Color(0.76f, 0.80f, 0.83f) }, wordWrap = false };
            _panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = _panelTex }, padding = new RectOffset(12, 12, 12, 12) };
            _buttonStyle = ButtonStyle(_buttonTex, _buttonHoverTex, new Color(0.86f, 0.90f, 0.92f), TextAnchor.MiddleCenter);
            _selectedButtonStyle = ButtonStyle(_accentTex, _accentTex, Color.white, TextAnchor.MiddleCenter);
            _dangerButtonStyle = ButtonStyle(_dangerTex, _dangerTex, Color.white, TextAnchor.MiddleCenter);
            _tabStyle = ButtonStyle(_buttonTex, _buttonHoverTex, new Color(0.75f, 0.80f, 0.84f), TextAnchor.MiddleCenter);
            _selectedTabStyle = ButtonStyle(_accentTex, _accentTex, Color.white, TextAnchor.MiddleCenter);
            _rowStyle = ButtonStyle(_buttonTex, _buttonHoverTex, new Color(0.84f, 0.88f, 0.90f), TextAnchor.MiddleLeft);
            _selectedRowStyle = ButtonStyle(_accentDimTex, _accentTex, Color.white, TextAnchor.MiddleLeft);
            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                normal = { background = _panelAltTex, textColor = Color.white },
                focused = { background = _panelAltTex, textColor = Color.white },
                fontSize = smallFont,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _stylesReady = true;
            _styleFontScale = _fontScale;
        }

        private int ScaleFont(int size)
        {
            return Mathf.RoundToInt(size * _fontScale);
        }

        private GUIStyle ButtonStyle(Texture2D normal, Texture2D hover, Color text, TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.button)
            {
                normal = { background = normal, textColor = text },
                hover = { background = hover, textColor = Color.white },
                active = { background = hover, textColor = Color.white },
                focused = { background = normal, textColor = text },
                fontSize = ScaleFont(13),
                fontStyle = FontStyle.Bold,
                alignment = alignment,
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        private static Texture2D Tex(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private sealed class ActorChoice
        {
            public ActorChoice(uint guid, string name, string dataId, int teamIndex)
            {
                Guid = guid;
                Name = name;
                DataId = dataId;
                TeamIndex = teamIndex;
            }

            public uint Guid { get; }
            public string Name { get; }
            public string DataId { get; }
            public int TeamIndex { get; }
        }

        private sealed class CatalogChoice<T>
        {
            public CatalogChoice(string id, string name, string detail, T value)
            {
                Id = id;
                Name = name;
                Detail = detail;
                Value = value;
                Search = PinyinSearch.Prepare(name, id, detail);
            }

            public string Id { get; }
            public string Name { get; }
            public string Detail { get; }
            public T Value { get; }
            private PinyinSearch.PreparedSearch Search { get; }

            public bool Matches(string query)
            {
                return Search == null || Search.Matches(query);
            }
        }

        private enum StatusKind
        {
            Token,
            Buff,
            Dot,
            PositiveQuirk,
            NegativeQuirk,
            Disease,
        }

        private sealed class StatusItem
        {
            public StatusItem(string id, string name, string detail, string subtitle, Sprite icon, object value, StatusKind kind)
            {
                Id = id ?? string.Empty;
                Name = name ?? string.Empty;
                Detail = detail ?? string.Empty;
                Subtitle = subtitle ?? string.Empty;
                Icon = icon;
                Value = value;
                Kind = kind;
            }

            public string Id { get; }
            public string Name { get; }
            public string Detail { get; }
            public string Subtitle { get; }
            public Sprite Icon { get; }
            public object Value { get; }
            public StatusKind Kind { get; }
        }

        private struct InlineIconData
        {
            public readonly Sprite Sprite;
            public readonly Texture Texture;
            public readonly Rect TextureRect;

            public InlineIconData(Sprite sprite)
            {
                Sprite = sprite;
                Texture = null;
                TextureRect = default(Rect);
            }

            public InlineIconData(Texture texture, Rect textureRect)
            {
                Sprite = null;
                Texture = texture;
                TextureRect = textureRect;
            }

            public bool IsValid
            {
                get { return Sprite != null || Texture != null; }
            }
        }

        private enum InlineSegmentKind
        {
            Text,
            Icon,
        }

        private sealed class InlineSegment
        {
            public InlineSegment(string text)
            {
                Kind = InlineSegmentKind.Text;
                Text = text;
            }

            public InlineSegment(InlineIconData icon)
            {
                Kind = InlineSegmentKind.Icon;
                Icon = icon;
            }

            public InlineSegmentKind Kind { get; }
            public string Text { get; }
            public InlineIconData Icon { get; }
        }
    }
}
