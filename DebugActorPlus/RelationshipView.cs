using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    /// <summary>Affinity bar split into the game's leaning levels, with a marker at the current value.</summary>
    internal sealed class LeaningBarView
    {
        private readonly RectTransform _root;
        private readonly List<KeyValuePair<Image, LeaningLevel>> _segments = new List<KeyValuePair<Image, LeaningLevel>>();
        private readonly RectTransform _marker;
        private int _min;
        private int _max = 20;

        public LeaningBarView(Transform parent, float height)
        {
            _root = U.Rect(parent, "LeaningBar");
            Image back = _root.gameObject.AddComponent<FrameImage>();
            U.Paint(back, U.Theme.BarTrough);
            back.type = Image.Type.Sliced;
            back.raycastTarget = false;
            U.Size(_root, -1f, height, 1f);

            Image marker = U.Image(_root, "Marker", U.Theme.Solid, Color.white);
            _marker = marker.rectTransform;
            _marker.anchorMin = new Vector2(0f, 0f);
            _marker.anchorMax = new Vector2(0f, 1f);
            _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(4f, 6f);
        }

        public void SetLevels(List<LeaningLevel> levels, int min, int max)
        {
            foreach (KeyValuePair<Image, LeaningLevel> pair in _segments)
            {
                if (pair.Key != null) UnityEngine.Object.Destroy(pair.Key.gameObject);
            }

            _segments.Clear();
            _min = min;
            _max = Math.Max(min + 1, max);
            float span = _max - _min + 1;
            List<LeaningLevel> source = levels != null && levels.Count > 0
                ? levels
                : new List<LeaningLevel> { new LeaningLevel { Min = min, Max = max } };
            foreach (LeaningLevel level in source)
            {
                Image segment = U.Image(_root, "Segment", U.Theme.Solid, Color.white);
                RectTransform rect = segment.rectTransform;
                rect.anchorMin = new Vector2(Mathf.Clamp01((level.Min - _min) / span), 0f);
                rect.anchorMax = new Vector2(Mathf.Clamp01((level.Max - _min + 1) / span), 1f);
                rect.offsetMin = new Vector2(1f, 2f);
                rect.offsetMax = new Vector2(-1f, -2f);
                _segments.Add(new KeyValuePair<Image, LeaningLevel>(segment, level));
            }

            _marker.SetAsLastSibling();
        }

        public void SetValue(int value)
        {
            foreach (KeyValuePair<Image, LeaningLevel> pair in _segments)
            {
                bool current = value >= pair.Value.Min && value <= pair.Value.Max;
                Color color = RelationshipView.LevelColor(pair.Value);
                color.a = current ? 0.95f : 0.30f;
                pair.Key.color = color;
            }

            float span = _max - _min + 1;
            float x = Mathf.Clamp01((value - _min + 0.5f) / span);
            _marker.anchorMin = new Vector2(x, 0f);
            _marker.anchorMax = new Vector2(x, 1f);
            _marker.anchoredPosition = Vector2.zero;
        }
    }

    internal sealed class RelationshipView : EditorView
    {
        private readonly List<RelationshipPair> _pairs = new List<RelationshipPair>();
        private List<LeaningLevel> _levels = new List<LeaningLevel>();
        private List<RelationshipPreset> _presets = new List<RelationshipPreset>();
        private readonly List<KeyValuePair<ButtonView, RelationshipPreset>> _presetButtons = new List<KeyValuePair<ButtonView, RelationshipPreset>>();
        private readonly List<Image> _presetFrames = new List<Image>();

        private bool _showAll;
        private bool _adjustLeaning = true;
        private string _selectedKey = "";
        private string _fingerprint = "";
        private int _presetsVersion = -1;

        private TextMeshProUGUI _headerCount;
        private ButtonView _focusChip;
        private ButtonView _allChip;
        private RectTransform _listContent;

        private RectTransform _detailRoot;
        private RectTransform _detailEmpty;
        private TextMeshProUGUI _detailEmptyText;
        private Image _portraitA;
        private Image _portraitB;
        private TextMeshProUGUI _nameA;
        private TextMeshProUGUI _nameB;
        private TextMeshProUGUI _shareNote;
        private TextMeshProUGUI _leaningValue;
        private TextMeshProUGUI _leaningLevel;
        private LeaningBarView _detailBar;
        private TextMeshProUGUI _chanceLine;
        private TextMeshProUGUI _relationshipName;
        private TextMeshProUGUI _relationshipMeta;
        private ButtonView _clearButton;
        private ButtonView _adjustChip;
        private RectTransform _ladder;
        private readonly List<ButtonView> _stepButtons = new List<ButtonView>();

        public RelationshipView(EditorScreen screen) : base(screen)
        {
        }

        public override string TabTitle => L.T("Relationships", "关系");

        internal static Color LevelColor(LeaningLevel level)
        {
            float polarity = Mathf.Clamp(level.PositiveChance - level.NegativeChance, -1f, 1f);
            return polarity < 0f
                ? Color.Lerp(UiTheme.Neutral, UiTheme.Negative, Mathf.Clamp01(-polarity * 1.4f))
                : Color.Lerp(UiTheme.Neutral, UiTheme.Positive, Mathf.Clamp01(polarity * 1.4f));
        }

        private static Color PresetColor(RelationshipPreset preset)
        {
            if (preset.IsNeutral) return new Color(0.30f, 0.29f, 0.27f);
            Color baseColor = preset.IsPositive ? UiTheme.Positive : UiTheme.Negative;
            float strength = Mathf.Lerp(0.35f, 0.75f, Mathf.Abs(preset.Polarity));
            return Color.Lerp(new Color(0.16f, 0.14f, 0.12f), baseColor, strength);
        }

        private static Color RelationshipColor(RelationshipPair pair)
        {
            if (pair.Current == null && pair.Pending == null) return UiTheme.TextMuted;
            return AffinityService.IsPositive(pair.Current ?? pair.Pending) ? UiTheme.Positive : UiTheme.Negative;
        }

        protected override void BuildContent()
        {
            HorizontalLayoutGroup row = U.Row(Root, "Columns", 12f, null, TextAnchor.UpperLeft);
            U.Stretch((RectTransform)row.transform);
            row.childForceExpandHeight = true;
            BuildPairColumn(row.transform);
            BuildDetailColumn(row.transform);
        }

        private void BuildPairColumn(Transform parent)
        {
            VerticalLayoutGroup column = U.Column(parent, "Pairs", 8f, U.Pad(12));
            U.Background(column, U.Theme.Panel);
            U.Size(column, -1f, -1f, 1f, 1f);

            HorizontalLayoutGroup header = U.Row(column.transform, "Header", 8f);
            U.Size(header, -1f, 34f, 1f);
            TextMeshProUGUI title = U.Label(header.transform, L.T("Hero relationships", "英雄关系"), FS.Emph, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Size(title, title.GetPreferredValues().x + 6f, 32f);
            _headerCount = U.Label(header.transform, "", FS.Tiny, UiTheme.TextMuted);
            U.Size(_headerCount, -1f, 32f, 1f);
            _focusChip = U.Button(header.transform, "", ButtonKind.Chip, () =>
            {
                _showAll = false;
                _fingerprint = "";
                Tick(true);
            }, 190f, 32f, FS.Small);
            _allChip = U.Button(header.transform, "", ButtonKind.Chip, () =>
            {
                _showAll = true;
                _fingerprint = "";
                Tick(true);
            }, 130f, 32f, FS.Small);

            TextMeshProUGUI explain = U.Label(column.transform,
                L.T("Each pair of heroes shares ONE affinity value and ONE relationship. Changing it affects both heroes of the pair.",
                    "每两名英雄之间只有一个和睦值、一种关系，由两人共用；修改一对，两人同时生效。卡片左侧始终是当前选中的英雄。"),
                FS.Small, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, false, true);
            U.Size(explain, -1f, -1f, 1f);
            U.Divider(column.transform);

            ScrollView scroll = U.Scroll(column.transform, "PairScroll", false, 8f);
            U.Size(scroll.Root, -1f, -1f, 1f, 1f);
            _listContent = scroll.Content;
        }

        private void BuildDetailColumn(Transform parent)
        {
            RectTransform holder = U.Rect(parent, "DetailHolder");
            U.Size(holder, 470f, -1f, -1f, 1f);
            Image background = holder.gameObject.AddComponent<FrameImage>();
            U.Paint(background, U.Theme.Panel);
            background.type = Image.Type.Sliced;

            _detailEmpty = U.Rect(holder, "Empty");
            U.Stretch(_detailEmpty, 24f, 24f, 24f, 24f);
            _detailEmptyText = U.Label(_detailEmpty, "", FS.Body, UiTheme.TextMuted, TextAlignmentOptions.Center, false, true);
            U.Stretch(_detailEmptyText.rectTransform);

            VerticalLayoutGroup detail = U.Column(holder, "Detail", 10f, U.Pad(16));
            _detailRoot = (RectTransform)detail.transform;
            U.Stretch(_detailRoot);

            // Who and whom: both portraits with the shared link in the middle.
            HorizontalLayoutGroup who = U.Row(detail.transform, "Who", 10f);
            U.Size(who, -1f, 88f, 1f);
            _portraitA = U.Icon(who.transform, "PortraitA", 76f);
            _nameA = U.Label(who.transform, "", FS.Emph, UiTheme.Text, TextAlignmentOptions.MidlineLeft, true, true);
            U.Size(_nameA, -1f, 84f, 1f);
            TextMeshProUGUI link = U.Label(who.transform, Link, 30f, UiTheme.Gold, TextAlignmentOptions.Center, true);
            U.Size(link, 44f, 84f);
            _nameB = U.Label(who.transform, "", FS.Emph, UiTheme.Text, TextAlignmentOptions.MidlineRight, true, true);
            U.Size(_nameB, -1f, 84f, 1f);
            _portraitB = U.Icon(who.transform, "PortraitB", 76f);

            _shareNote = U.Label(detail.transform, "", FS.Small, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, false, true);
            U.Divider(detail.transform);

            // Affinity value.
            HorizontalLayoutGroup leaningHeader = U.Row(detail.transform, "LeaningHeader", 10f);
            U.Size(leaningHeader, -1f, 36f, 1f);
            TextMeshProUGUI leaningTitle = U.Label(leaningHeader.transform, L.T("Affinity", "和睦值"), FS.Emph, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Size(leaningTitle, Mathf.Max(84f, leaningTitle.GetPreferredValues().x + 8f), 36f);
            _leaningValue = U.Label(leaningHeader.transform, "", FS.Header, UiTheme.Text, TextAlignmentOptions.MidlineLeft, true);
            U.Size(_leaningValue, 96f, 36f);
            _leaningLevel = U.Label(leaningHeader.transform, "", FS.Body, UiTheme.Text);
            U.Size(_leaningLevel, -1f, 36f, 1f);

            _detailBar = new LeaningBarView(detail.transform, 22f);

            HorizontalLayoutGroup steps = U.Row(detail.transform, "Steps", 6f);
            U.Size(steps, -1f, 36f, 1f);
            foreach (int delta in new[] { -5, -1, 1, 5 })
            {
                int d = delta;
                _stepButtons.Add(U.Button(steps.transform, (d > 0 ? "+" : "−") + Math.Abs(d), ButtonKind.Normal, () => ChangeLeaning(d), 46f, 36f));
            }

            U.Spacer(steps.transform, 4f, 10f);
            _stepButtons.Add(U.Button(steps.transform, L.T("Min", "最低"), ButtonKind.Normal, () => SetLeaning(AffinityService.LeaningMin), -1f, 36f, FS.Small));
            _stepButtons.Add(U.Button(steps.transform, L.T("Start", "初始"), ButtonKind.Normal, () => SetLeaning(AffinityService.LeaningStart), -1f, 36f, FS.Small));
            _stepButtons.Add(U.Button(steps.transform, L.T("Max", "最高"), ButtonKind.Normal, () => SetLeaning(AffinityService.LeaningMax), -1f, 36f, FS.Small));

            _chanceLine = U.Label(detail.transform, "", FS.Small, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, false, true);
            U.Divider(detail.transform);

            // Current relationship + one-click clear.
            HorizontalLayoutGroup current = U.Row(detail.transform, "Current", 10f);
            U.Size(current, -1f, 50f, 1f);
            TextMeshProUGUI currentTitle = U.Label(current.transform, L.T("Relationship", "当前关系"), FS.Emph, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Size(currentTitle, Mathf.Max(84f, currentTitle.GetPreferredValues().x + 8f), 50f);
            VerticalLayoutGroup currentText = U.Column(current.transform, "CurrentText", 0f, null, TextAnchor.MiddleLeft);
            U.Size(currentText, -1f, 50f, 1f);
            _relationshipName = U.Label(currentText.transform, "", FS.Header, UiTheme.Text, TextAlignmentOptions.MidlineLeft, true);
            U.Size(_relationshipName, -1f, 28f, 1f);
            _relationshipMeta = U.Label(currentText.transform, "", FS.Tiny, UiTheme.TextMuted);
            U.Size(_relationshipMeta, -1f, 20f, 1f);
            _clearButton = U.Button(current.transform, L.T("Clear relationship", "一键清除关系"), ButtonKind.Danger, ClearRelationship, -1f, 40f, FS.Small);

            U.Divider(detail.transform);

            // One-click presets, a single scale from the worst relationship to the best one.
            HorizontalLayoutGroup presetHeader = U.Row(detail.transform, "PresetHeader", 10f);
            U.Size(presetHeader, -1f, 34f, 1f);
            TextMeshProUGUI presetTitle = U.Label(presetHeader.transform, L.T("One-click assign", "一键指定"), FS.Emph, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Size(presetTitle, -1f, 32f, 1f);
            _adjustChip = U.Button(presetHeader.transform, L.T("Also set affinity", "同时调整和睦值"), ButtonKind.Chip, () =>
            {
                _adjustLeaning = !_adjustLeaning;
                _adjustChip.SetSelected(_adjustLeaning);
                RefreshLadderLabels();
            }, -1f, 32f, FS.Small);
            _adjustChip.SetSelected(_adjustLeaning);

            TextMeshProUGUI caption = U.Label(detail.transform,
                L.T("Ordered from the worst relationship to the best. Clicking applies it immediately to both heroes (skill modifiers, act-outs and buffs included).",
                    "从最差排到最好，点击立即对这对英雄生效（关系附带的技能加成、插话和增益会一并挂上）。"),
                FS.Tiny, UiTheme.TextDim, TextAlignmentOptions.TopLeft, false, true);
            U.Size(caption, -1f, -1f, 1f);

            HorizontalLayoutGroup ladder = U.Row(detail.transform, "Ladder", 4f);
            U.Size(ladder, -1f, 72f, 1f);
            _ladder = (RectTransform)ladder.transform;

            HorizontalLayoutGroup scale = U.Row(detail.transform, "Scale", 4f);
            U.Size(scale, -1f, 22f, 1f);
            TextMeshProUGUI worst = U.Label(scale.transform, U.Glyph("◀", U.Glyph("‹", "<")) + " " + L.T("Negative", "负面"), FS.Tiny, UiTheme.Negative);
            U.Size(worst, -1f, 22f, 1f);
            TextMeshProUGUI middle = U.Label(scale.transform, L.T("Neutral", "中立"), FS.Tiny, UiTheme.Neutral, TextAlignmentOptions.Center);
            U.Size(middle, -1f, 22f, 1f);
            TextMeshProUGUI best = U.Label(scale.transform, L.T("Positive", "正面") + " " + U.Glyph("▶", U.Glyph("›", ">")), FS.Tiny, UiTheme.Positive, TextAlignmentOptions.MidlineRight);
            U.Size(best, -1f, 22f, 1f);

            U.Spacer(detail.transform, -1f, -1f, false);
        }

        public override void OnShown()
        {
            _fingerprint = "";
            _levels = AffinityService.Levels();
            _detailBar.SetLevels(_levels, AffinityService.LeaningMin, AffinityService.LeaningMax);
            if (_presetsVersion != Screen.Data.Version || _presets.Count == 0)
            {
                _presetsVersion = Screen.Data.Version;
                _presets = AffinityService.Presets();
                BuildLadder();
            }
        }

        public override void Tick(bool force)
        {
            List<RelationshipPair> pairs = AffinityService.GetPairs(Screen.SelectedGuid);
            uint focus = pairs.Any(pair => pair.InvolvesFocus) ? Screen.SelectedGuid : 0U;
            string fingerprint = focus + "|" + _showAll + "|" + AffinityService.Fingerprint(pairs);
            if (!force && fingerprint == _fingerprint) return;
            _fingerprint = fingerprint;
            _pairs.Clear();
            _pairs.AddRange(pairs);
            RebuildList(focus);
            RefreshDetail();
        }

        private void RebuildList(uint focus)
        {
            U.Clear(_listContent);
            string focusName = focus != 0U ? Screen.SelectedName : "";
            int heroCount = _pairs.SelectMany(pair => new[] { pair.GuidA, pair.GuidB }).Distinct().Count();
            _headerCount.text = _pairs.Count == 0 ? "" : L.F("{0} heroes · {1} pairs", "{0} 名英雄 · {1} 对", heroCount, _pairs.Count);

            bool hasFocus = focus != 0U;
            bool showAll = _showAll || !hasFocus;
            _focusChip.SetVisible(hasFocus);
            _focusChip.SetText(L.F("With {0}", "与 {0} 相关", focusName));
            _focusChip.Layout.preferredWidth = Mathf.Max(120f, _focusChip.Label.GetPreferredValues(_focusChip.Label.text).x + 28f);
            _focusChip.SetSelected(!showAll);
            _allChip.SetText(L.F("All {0}", "全部 {0} 对", _pairs.Count));
            _allChip.SetSelected(showAll);

            List<RelationshipPair> visible = showAll ? _pairs : _pairs.Where(pair => pair.InvolvesFocus).ToList();
            if (visible.Count == 0)
            {
                TextMeshProUGUI empty = U.Label(_listContent,
                    L.T("No relationship data here. Relationships exist between heroes of the current party during a run.",
                        "这里没有关系数据：关系只存在于一局游戏中、当前队伍里的英雄之间。"),
                    FS.Body, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, false, true);
                _selectedKey = "";
                return;
            }

            if (visible.All(pair => pair.Key != _selectedKey)) _selectedKey = visible[0].Key;

            bool sectionOthers = false;
            if (_showAll && hasFocus)
            {
                Section(L.F("Pairs with {0}", "与 {0} 的关系", focusName));
            }

            foreach (RelationshipPair pair in visible)
            {
                if (_showAll && hasFocus && !pair.InvolvesFocus && !sectionOthers)
                {
                    sectionOthers = true;
                    Section(L.T("Between the other heroes", "其他队友之间"));
                }

                PairCard(pair, _showAll && hasFocus && !pair.InvolvesFocus);
            }
        }

        private void Section(string text)
        {
            TextMeshProUGUI label = U.Label(_listContent, text, FS.Small, UiTheme.Gold, TextAlignmentOptions.BottomLeft, true);
            U.Size(label, -1f, 28f, 1f);
        }

        private void PairCard(RelationshipPair pair, bool dimmed)
        {
            string key = pair.Key;
            bool selected = key == _selectedKey;
            ButtonView card = U.Button(_listContent, "", ButtonKind.Normal, () =>
            {
                _selectedKey = key;
                _fingerprint = "";
                Tick(true);
            }, -1f, 100f);
            U.Paint(card.Background, selected ? U.Theme.CardSelected : U.Theme.Card);
            if (dimmed && !selected) card.Background.color = new Color(1f, 1f, 1f, 0.7f);
            UnityEngine.Object.Destroy(card.Label.gameObject);
            card.Label = null;

            HorizontalLayoutGroup row = U.Row(card.GameObject.transform, "Row", 10f, new RectOffset(10, 12, 8, 8));
            U.Stretch((RectTransform)row.transform);

            Image portraitA = U.Icon(row.transform, "PortraitA", 56f);
            U.SetIcon(portraitA, GameIcons.Portrait(pair.ActorA));
            TextMeshProUGUI nameA = U.Label(row.transform, pair.NameA, FS.Body, pair.InvolvesFocus ? UiTheme.Gold : UiTheme.Text, TextAlignmentOptions.MidlineLeft, true, true);
            U.Size(nameA, 104f, 80f);

            VerticalLayoutGroup middle = U.Column(row.transform, "Middle", 4f, null, TextAnchor.MiddleCenter);
            U.Size(middle, -1f, 80f, 1f);
            LeaningLevel level = AffinityService.LevelFor(_levels, pair.Leaning);
            string levelName = level != null ? level.Name : "";
            TextMeshProUGUI top = U.Label(middle.transform, Link + "  " + U.Colored(levelName, level != null ? LevelColor(level) : UiTheme.Text), FS.Small, UiTheme.Gold, TextAlignmentOptions.Center);
            U.Size(top, -1f, 24f, 1f);
            LeaningBarView bar = new LeaningBarView(middle.transform, 14f);
            bar.SetLevels(_levels, pair.Min, pair.Max);
            bar.SetValue(pair.Leaning);
            TextMeshProUGUI value = U.Label(middle.transform, L.F("Affinity {0} / {1}", "和睦值 {0} / {1}", pair.Leaning, pair.Max), FS.Tiny, UiTheme.TextMuted, TextAlignmentOptions.Center);
            U.Size(value, -1f, 20f, 1f);

            TextMeshProUGUI nameB = U.Label(row.transform, pair.NameB, FS.Body, UiTheme.Text, TextAlignmentOptions.MidlineRight, true, true);
            U.Size(nameB, 104f, 80f);
            Image portraitB = U.Icon(row.transform, "PortraitB", 56f);
            U.SetIcon(portraitB, GameIcons.Portrait(pair.ActorB));

            U.Divider(row.transform, true);
            VerticalLayoutGroup relation = U.Column(row.transform, "Relation", 2f, null, TextAnchor.MiddleCenter);
            U.Size(relation, 100f, 80f);
            TextMeshProUGUI relationName = U.Label(relation.transform, RelationshipLabel(pair), FS.Emph, RelationshipColor(pair), TextAlignmentOptions.Center, true);
            U.Size(relationName, 100f, 28f);
            TextMeshProUGUI relationMeta = U.Label(relation.transform, RelationshipMeta(pair), FS.Tiny, UiTheme.TextDim, TextAlignmentOptions.Center, false, true);
            U.Size(relationMeta, 100f, 34f);
        }

        private static string RelationshipLabel(RelationshipPair pair)
        {
            if (pair.Current != null) return AffinityService.RelationshipName(pair.Current);
            if (pair.Pending != null) return AffinityService.RelationshipName(pair.Pending);
            return L.T("None", "无关系");
        }

        private static string RelationshipMeta(RelationshipPair pair)
        {
            if (pair.Current == null && pair.Pending != null) return L.T("pending", "待生效");
            if (pair.Current != null && pair.HasDuration) return L.F("{0} days left", "剩余 {0} 天", pair.DurationRemaining);
            if (pair.Current != null) return L.T("active", "已生效");
            return "";
        }

        private RelationshipPair SelectedPair => _pairs.FirstOrDefault(pair => pair.Key == _selectedKey);

        private void RefreshDetail()
        {
            RelationshipPair pair = SelectedPair;
            bool has = pair != null;
            _detailRoot.gameObject.SetActive(has);
            _detailEmpty.gameObject.SetActive(!has);
            if (!has)
            {
                _detailEmptyText.text = _pairs.Count == 0
                    ? L.T("Select a hero during a run to edit relationships.\nEnemies have no relationships.", "在一局游戏中选择英雄即可编辑关系。\n敌人之间没有关系。")
                    : L.T("Select a pair on the left.", "请在左侧选择一对英雄。");
                return;
            }

            U.SetIcon(_portraitA, GameIcons.Portrait(pair.ActorA));
            U.SetIcon(_portraitB, GameIcons.Portrait(pair.ActorB));
            _nameA.text = pair.NameA;
            _nameB.text = pair.NameB;

            LeaningLevel level = AffinityService.LevelFor(_levels, pair.Leaning);
            _leaningValue.text = $"{pair.Leaning} / {pair.Max}";
            _leaningLevel.text = level != null ? U.Colored(level.Name, LevelColor(level)) : "";
            _detailBar.SetValue(pair.Leaning);
            _chanceLine.text = level == null
                ? ""
                : L.F("When the party next sets out from an inn, this level rolls: negative relationship {0}% · positive relationship {1}% (base chance, before modifiers). Outside the Kingdom mode, entering an inn resets existing relationships.",
                    "下次从旅店出发时按当前档位判定：负面关系 {0}% · 正面关系 {1}%（基础概率，未计加成）。非王国模式下，进入旅店时现有关系会被重置。",
                    Mathf.RoundToInt(level.NegativeChance * 100f), Mathf.RoundToInt(level.PositiveChance * 100f));

            _relationshipName.text = U.Colored(RelationshipLabel(pair), RelationshipColor(pair));
            _relationshipMeta.text = RelationshipMeta(pair);
            _clearButton.SetInteractable(pair.Current != null || pair.Pending != null);

            _shareNote.text = L.F("{0} and {1} share this affinity and relationship. Changes apply to both.",
                "{0} 与 {1} 共用这个和睦值和关系，修改会同时作用于两人。", pair.NameA, pair.NameB);

            string currentId = (pair.Current ?? pair.Pending)?.m_Id ?? "";
            for (int i = 0; i < _presetButtons.Count; i++)
            {
                RelationshipPreset preset = _presetButtons[i].Value;
                bool isCurrent = preset.IsNeutral ? string.IsNullOrEmpty(currentId) : string.Equals(preset.Id, currentId, StringComparison.OrdinalIgnoreCase);
                _presetFrames[i].gameObject.SetActive(isCurrent);
            }

            RefreshLadderLabels();
        }

        private void BuildLadder()
        {
            U.Clear(_ladder);
            _presetButtons.Clear();
            _presetFrames.Clear();
            foreach (RelationshipPreset preset in _presets)
            {
                RelationshipPreset p = preset;
                ButtonView button = U.Button(_ladder, "", ButtonKind.Normal, () => ApplyPreset(p), -1f, 72f, FS.Small);
                U.Size(button.GameObject.transform, 40f, 66f, 1f, -1f, 40f);
                U.Paint(button.Background, U.Theme.Solid);
                button.Background.type = Image.Type.Simple;
                button.SetTint(PresetColor(preset));
                button.Label.enableAutoSizing = true;
                button.Label.fontSizeMin = 12f;
                button.Label.fontSizeMax = FS.Body;
                button.Label.textWrappingMode = TextWrappingModes.Normal;
                button.Label.overflowMode = TextOverflowModes.Overflow;
                U.Stretch(button.Label.rectTransform, 3f, 3f, 3f, 3f);

                Image frame = U.Image(button.GameObject.transform, "Current", U.Theme.Frame, UiTheme.Gold);
                U.Stretch(frame.rectTransform, -2f, -2f, -2f, -2f);
                frame.gameObject.SetActive(false);
                _presetButtons.Add(new KeyValuePair<ButtonView, RelationshipPreset>(button, preset));
                _presetFrames.Add(frame);
            }

            RefreshLadderLabels();
        }

        private void RefreshLadderLabels()
        {
            foreach (KeyValuePair<ButtonView, RelationshipPreset> pair in _presetButtons)
            {
                RelationshipPreset preset = pair.Value;
                string name = preset.IsNeutral ? L.T("Neutral", "中立") : preset.Name;
                string sub = preset.IsNeutral ? L.T("clear", "清除") : "";
                // Compact so it fits the narrow scale cells on one line ("清除→9", "→14").
                if (_adjustLeaning) sub += "→" + preset.TargetLeaning;
                pair.Key.SetText(name + (sub.Length > 0 ? "\n<size=70%>" + U.Colored(sub, new Color(0.95f, 0.92f, 0.85f, 0.8f)) + "</size>" : ""));
            }
        }

        private void ChangeLeaning(int delta)
        {
            RelationshipPair pair = SelectedPair;
            if (pair == null) return;
            SetLeaning(pair.Leaning + delta);
        }

        private void SetLeaning(int value)
        {
            RelationshipPair pair = SelectedPair;
            if (pair == null) return;
            int target = Math.Max(pair.Min, Math.Min(pair.Max, value));
            int before = pair.Leaning;
            string message = L.F("Affinity {0} {4} {1}: {2} → {3}", "和睦值 {0} {4} {1}：{2} → {3}", pair.NameA, pair.NameB, before, target, Link);
            bool bypassed = false;
            Screen.Run(message, () => bypassed = AffinityService.SetLeaning(pair, target));
            if (bypassed) Screen.SetStatus(message + BypassNote);
            _fingerprint = "";
        }

        private void ClearRelationship()
        {
            RelationshipPair pair = SelectedPair;
            if (pair == null) return;
            string name = RelationshipLabel(pair);
            Screen.Run(L.F("Cleared “{2}” between {0} and {1}", "已清除 {0} ⇄ {1} 的关系「{2}」", pair.NameA, pair.NameB, name),
                () => AffinityService.Clear(pair));
            _fingerprint = "";
        }

        private void ApplyPreset(RelationshipPreset preset)
        {
            RelationshipPair pair = SelectedPair;
            if (pair == null) return;
            int before = pair.Leaning;
            string leaningPart = _adjustLeaning ? L.F(" (affinity {0} → {1})", "（和睦值 {0} → {1}）", before, preset.TargetLeaning) : "";
            string message = preset.IsNeutral
                ? L.F("{0} ⇄ {1}: relationship cleared{2}", "{0} ⇄ {1}：已清除关系{2}", pair.NameA, pair.NameB, leaningPart)
                : L.F("{0} ⇄ {1} are now “{2}”{3}", "{0} ⇄ {1} 已设为「{2}」{3}", pair.NameA, pair.NameB, preset.Name, leaningPart);
            bool adjust = _adjustLeaning;
            bool bypassed = false;
            Screen.Run(message, () => bypassed = AffinityService.Apply(pair, preset, adjust));
            if (bypassed) Screen.SetStatus(message + BypassNote);
            _fingerprint = "";
        }

        private static string BypassNote => L.T(
            "  (another mod blocked the affinity decrease — written directly)",
            "  （和睦值下降被其他插件拦截，例如作弊面板的「锁定关系」，已直接写入）");

        private static string Link => U.Glyph("⇄", "&");
    }
}
