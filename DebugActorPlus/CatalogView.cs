using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    internal sealed class CatalogCategory
    {
        public CatalogCategory(string key, string label)
        {
            Key = key ?? "";
            Label = label ?? "";
        }

        public string Key { get; }
        public string Label { get; }
    }

    /// <summary>What differs between the Token / Buff / DOT / Quirk / Item tabs.</summary>
    internal sealed class CatalogConfig
    {
        public EntryKind Kind;
        public string Tab = "";
        public string Noun = "";
        public string OwnedTitle = "";
        public string AddFormat = "";
        public bool LockedOption;
        public bool CritOption;
        public bool DynamicDotCategories;
        public List<CatalogCategory> Categories = new List<CatalogCategory>();

        internal static CatalogConfig Tokens()
        {
            return new CatalogConfig
            {
                Kind = EntryKind.Token,
                Tab = L.T("Tokens", "标记"),
                Noun = L.T("token", "标记"),
                OwnedTitle = L.T("Current tokens", "当前标记"),
                AddFormat = L.T("Add to {0}", "添加给 {0}"),
                LockedOption = true,
                Categories =
                {
                    new CatalogCategory(GameData.CategoryAll, L.T("All", "全部")),
                    new CatalogCategory(GameData.CategoryPositive, L.T("Positive", "正面")),
                    new CatalogCategory(GameData.CategoryNegative, L.T("Negative", "负面")),
                    new CatalogCategory(GameData.CategoryOther, L.T("Other", "其他")),
                },
            };
        }

        internal static CatalogConfig Buffs()
        {
            return new CatalogConfig
            {
                Kind = EntryKind.Buff,
                Tab = L.T("Buffs", "增益"),
                Noun = L.T("buff", "增益"),
                OwnedTitle = L.T("Current buffs", "当前增益"),
                AddFormat = L.T("Add to {0}", "添加给 {0}"),
                LockedOption = true,
                Categories = { new CatalogCategory(GameData.CategoryAll, L.T("All", "全部")) },
            };
        }

        internal static CatalogConfig Dots()
        {
            return new CatalogConfig
            {
                Kind = EntryKind.Dot,
                Tab = L.T("DOTs", "持续伤害"),
                Noun = L.T("DOT", "持续伤害"),
                OwnedTitle = L.T("Current DOTs", "当前持续伤害"),
                AddFormat = L.T("Apply to {0}", "施加给 {0}"),
                LockedOption = true,
                CritOption = true,
                DynamicDotCategories = true,
                Categories = { new CatalogCategory(GameData.CategoryAll, L.T("All", "全部")) },
            };
        }

        internal static CatalogConfig Quirks()
        {
            return new CatalogConfig
            {
                Kind = EntryKind.Quirk,
                Tab = L.T("Quirks", "特质"),
                Noun = L.T("quirk", "特质"),
                OwnedTitle = L.T("Current quirks & diseases", "当前特质 / 疾病"),
                AddFormat = L.T("Give to {0}", "赋予 {0}"),
                Categories =
                {
                    new CatalogCategory(GameData.CategoryAll, L.T("All", "全部")),
                    new CatalogCategory(GameData.CategoryPositive, L.T("Positive", "正面特质")),
                    new CatalogCategory(GameData.CategoryNegative, L.T("Negative", "负面特质")),
                    new CatalogCategory(GameData.CategoryDisease, L.T("Disease / curse", "疾病 / 诅咒")),
                },
            };
        }

        internal static CatalogConfig Items()
        {
            return new CatalogConfig
            {
                Kind = EntryKind.Item,
                Tab = L.T("Items", "物品"),
                Noun = L.T("item", "物品"),
                OwnedTitle = L.T("Inventory", "携带物品"),
                AddFormat = L.T("Give to {0}", "放入 {0} 的物品栏"),
                Categories =
                {
                    new CatalogCategory(GameData.CategoryAll, L.T("All", "全部")),
                    new CatalogCategory(GameData.CategoryCombatItem, L.T("Combat items", "战斗物品")),
                    new CatalogCategory(GameData.CategoryTrinket, L.T("Trinkets", "饰品")),
                },
            };
        }
    }

    internal sealed class CatalogView : EditorView
    {
        private const float RowHeight = 58f;

        private readonly CatalogConfig _config;
        private readonly List<ButtonView> _chips = new List<ButtonView>();
        private readonly HashSet<object> _owned = new HashSet<object>();
        private List<CatalogEntry> _filtered = new List<CatalogEntry>();

        private string _search = "";
        private string _category = "";
        private CatalogEntry _selected;
        private int _dataVersion = -1;
        private string _ownedFingerprint = "";

        private TextMeshProUGUI _ownedTitle;
        private TMP_InputField _searchInput;
        private RectTransform _ownedContent;
        private RectTransform _chipRow;
        private TextMeshProUGUI _count;
        private VirtualList<CatalogRow> _list;
        private Image _previewIcon;
        private TextMeshProUGUI _previewName;
        private TextMeshProUGUI _previewMeta;
        private TextMeshProUGUI _previewDetail;
        private ScrollView _detailScroll;
        private ButtonView _addButton;
        private ButtonView _removeButton;
        private ButtonView _lockedChip;
        private ButtonView _critChip;
        private TextMeshProUGUI _ownedBadgeLine;

        public CatalogView(EditorScreen screen, CatalogConfig config) : base(screen)
        {
            _config = config;
        }

        public override string TabTitle => _config.Tab;

        private sealed class CatalogRow
        {
            public RectTransform Rect;
            public ButtonView Button;
            public Image Icon;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Id;
            public TextMeshProUGUI Badge;
            public int Index = -1;
        }

        protected override void BuildContent()
        {
            HorizontalLayoutGroup row = U.Row(Root, "Columns", 12f, null, TextAnchor.UpperLeft);
            U.Stretch((RectTransform)row.transform);
            row.childForceExpandHeight = true;

            BuildOwnedColumn(row.transform);
            BuildLibraryColumn(row.transform);
            BuildPreviewColumn(row.transform);
        }

        private void BuildOwnedColumn(Transform parent)
        {
            VerticalLayoutGroup column = U.Column(parent, "Owned", 8f, U.Pad(12));
            U.Background(column, U.Theme.Panel);
            U.Size(column, 300f, -1f, -1f, 1f);
            _ownedTitle = U.Label(column.transform, _config.OwnedTitle, FS.Emph, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Size(_ownedTitle, -1f, 30f, 1f);
            U.Divider(column.transform);
            ScrollView scroll = U.Scroll(column.transform, "OwnedScroll", false, 6f);
            U.Size(scroll.Root, -1f, -1f, 1f, 1f);
            _ownedContent = scroll.Content;
        }

        private void BuildLibraryColumn(Transform parent)
        {
            VerticalLayoutGroup column = U.Column(parent, "Library", 8f, U.Pad(12));
            U.Background(column, U.Theme.Panel);
            U.Size(column, -1f, -1f, 1f, 1f);

            HorizontalLayoutGroup header = U.Row(column.transform, "Header", 8f);
            U.Size(header, -1f, 30f, 1f);
            TextMeshProUGUI title = U.Label(header.transform, L.F("{0} library", "{0}库", _config.Tab), FS.Emph, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Size(title, -1f, 30f, 1f);
            _count = U.Label(header.transform, "", FS.Tiny, UiTheme.TextMuted, TextAlignmentOptions.MidlineRight);
            U.Size(_count, 170f, 30f);

            _searchInput = U.Input(column.transform, L.T("Search name / id / pinyin initials…", "搜索名称 / ID / 拼音或首字母……"), value =>
            {
                _search = value ?? "";
                ApplyFilter(true);
            }, 40f, FS.Body);

            _chipRow = (RectTransform)U.Row(column.transform, "Categories", 6f).transform;
            U.Size(_chipRow, -1f, 34f, 1f);
            BuildChips();

            ScrollView scroll = U.Scroll(column.transform, "List", false, 4f);
            U.Size(scroll.Root, -1f, -1f, 1f, 1f);
            _list = new VirtualList<CatalogRow>(scroll, RowHeight, 4f, CreateRow, row => row.Rect, BindRow);

            TextMeshProUGUI hint = U.Label(column.transform, L.T("Click to preview · double-click to add · right-click to remove from the actor",
                "单击预览 · 双击直接添加 · 右键从角色身上移除"), FS.Tiny, UiTheme.TextDim);
            U.Size(hint, -1f, 22f, 1f);
        }

        private void BuildChips()
        {
            U.Clear(_chipRow);
            _chips.Clear();
            List<CatalogCategory> categories = new List<CatalogCategory>(_config.Categories);
            if (_config.DynamicDotCategories)
            {
                foreach (string type in Screen.Data.DotTypes)
                {
                    categories.Add(new CatalogCategory(type, GameText.DotTypeName(type)));
                }
            }

            if (categories.Count <= 1)
            {
                _chipRow.gameObject.SetActive(false);
                return;
            }

            _chipRow.gameObject.SetActive(true);
            foreach (CatalogCategory category in categories)
            {
                string key = category.Key;
                ButtonView chip = U.Button(_chipRow, category.Label, ButtonKind.Chip, () =>
                {
                    _category = key;
                    foreach (ButtonView other in _chips) other.SetSelected(other.GameObject.name == key);
                    ApplyFilter(true);
                }, -1f, 32f, FS.Small);
                chip.GameObject.name = key;
                chip.SetSelected(key == _category);
                _chips.Add(chip);
            }

            if (categories.All(category => category.Key != _category)) _category = "";
            foreach (ButtonView chip in _chips) chip.SetSelected(chip.GameObject.name == _category);
        }

        private void BuildPreviewColumn(Transform parent)
        {
            VerticalLayoutGroup column = U.Column(parent, "Preview", 10f, U.Pad(16));
            U.Background(column, U.Theme.Panel);
            U.Size(column, 400f, -1f, -1f, 1f);

            HorizontalLayoutGroup header = U.Row(column.transform, "Header", 12f, null, TextAnchor.UpperLeft);
            U.Size(header, -1f, 84f, 1f);
            _previewIcon = U.Icon(header.transform, "Icon", 72f);
            VerticalLayoutGroup titles = U.Column(header.transform, "Titles", 4f);
            U.Size(titles, -1f, 84f, 1f);
            _previewName = U.Label(titles.transform, "", FS.Header, UiTheme.Text, TextAlignmentOptions.TopLeft, true, true);
            _previewMeta = U.Label(titles.transform, "", FS.Tiny, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, false, true);

            U.Divider(column.transform);
            _detailScroll = U.Scroll(column.transform, "Detail", false, 4f, new RectOffset(0, 6, 0, 0));
            U.Size(_detailScroll.Root, -1f, -1f, 1f, 1f);
            _previewDetail = U.Label(_detailScroll.Content, "", FS.Body, UiTheme.Text, TextAlignmentOptions.TopLeft, false, true);

            if (_config.LockedOption || _config.CritOption)
            {
                HorizontalLayoutGroup options = U.Row(column.transform, "Options", 8f);
                U.Size(options, -1f, 34f, 1f);
                if (_config.LockedOption)
                {
                    _lockedChip = U.Button(options.transform, L.T("Lock team position source", "锁定站位来源"), ButtonKind.Chip, () =>
                    {
                        Screen.Options.LockedTeamPosition = !Screen.Options.LockedTeamPosition;
                        SyncOptionChips();
                    }, -1f, 32f, FS.Small);
                }

                if (_config.CritOption)
                {
                    _critChip = U.Button(options.transform, L.T("Critical DOT", "暴击持续伤害"), ButtonKind.Chip, () =>
                    {
                        Screen.Options.DotCrit = !Screen.Options.DotCrit;
                        SyncOptionChips();
                    }, -1f, 32f, FS.Small);
                }
            }

            _ownedBadgeLine = U.Label(column.transform, "", FS.Small, UiTheme.Gold);
            U.Size(_ownedBadgeLine, -1f, 22f, 1f);

            HorizontalLayoutGroup buttons = U.Row(column.transform, "Buttons", 8f);
            U.Size(buttons, -1f, 46f, 1f);
            _addButton = U.Button(buttons.transform, "", ButtonKind.Primary, AddSelected, -1f, 46f, FS.Body);
            U.Size(_addButton.GameObject.transform, -1f, 46f, 1f);
            _removeButton = U.Button(buttons.transform, L.T("Remove", "移除"), ButtonKind.Danger, RemoveSelected, 100f, 46f, FS.Body);
        }

        private void SyncOptionChips()
        {
            _lockedChip?.SetSelected(Screen.Options.LockedTeamPosition);
            _critChip?.SetSelected(Screen.Options.DotCrit);
        }

        private CatalogRow CreateRow(RectTransform content)
        {
            CatalogRow row = new CatalogRow();
            ButtonView button = U.Button(content, "", ButtonKind.Normal, null, -1f, RowHeight);
            U.Paint(button.Background, U.Theme.Card);
            UnityEngine.Object.Destroy(button.Label.gameObject);
            button.Label = null;
            row.Button = button;
            row.Rect = (RectTransform)button.GameObject.transform;
            button.Button.onClick.AddListener(() => Select(row.Index, false));
            ClickRelay relay = button.GameObject.AddComponent<ClickRelay>();
            relay.OnDoubleClick = () =>
            {
                Select(row.Index, false);
                AddSelected();
            };
            relay.OnRightClick = () =>
            {
                Select(row.Index, false);
                if (_selected != null && _owned.Contains(_selected.Value)) RemoveSelected();
            };

            HorizontalLayoutGroup layout = U.Row(row.Rect, "Row", 10f, new RectOffset(8, 10, 4, 4));
            U.Stretch((RectTransform)layout.transform);
            row.Icon = U.Icon(layout.transform, "Icon", 42f);
            VerticalLayoutGroup text = U.Column(layout.transform, "Text", 0f, null, TextAnchor.MiddleLeft);
            U.Size(text, -1f, -1f, 1f);
            row.Name = U.Label(text.transform, "", FS.Body, UiTheme.Text);
            U.Size(row.Name, -1f, 26f, 1f);
            row.Id = U.Label(text.transform, "", FS.Tiny, UiTheme.TextDim);
            U.Size(row.Id, -1f, 20f, 1f);
            row.Badge = U.Label(layout.transform, "", FS.Tiny, UiTheme.Gold, TextAlignmentOptions.MidlineRight);
            U.Size(row.Badge, 80f, 40f);
            return row;
        }

        private void BindRow(CatalogRow row, int index)
        {
            row.Index = index;
            CatalogEntry entry = index >= 0 && index < _filtered.Count ? _filtered[index] : null;
            if (entry == null) return;
            row.Name.text = entry.Name;
            // Unnamed entries (name == id): show what they do instead of repeating the id.
            bool unnamed = string.Equals(entry.Name, entry.Id, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(entry.Detail);
            row.Id.text = (unnamed ? Snippet(entry.Detail) : entry.Id) + CategorySuffix(entry);
            U.SetIcon(row.Icon, entry.Icon);
            bool selected = ReferenceEquals(entry, _selected);
            U.Paint(row.Button.Background, selected ? U.Theme.CardSelected : U.Theme.Card);
            row.Badge.text = _owned.Contains(entry.Value) ? L.T("Owned", "已拥有") : "";
        }

        private static string Snippet(string detail)
        {
            string plain = GameText.Plain(detail);
            return plain.Length <= 48 ? plain : plain.Substring(0, 46) + "…";
        }

        private string CategorySuffix(CatalogEntry entry)
        {
            if (!string.IsNullOrEmpty(_category) || string.IsNullOrEmpty(entry.Category)) return "";
            if (_config.Kind == EntryKind.Dot) return "  ·  " + GameText.DotTypeName(entry.Category);
            CatalogCategory category = _config.Categories.FirstOrDefault(c => c.Key == entry.Category);
            return category == null ? "" : "  ·  " + category.Label;
        }

        public override void OnShown()
        {
            if (_dataVersion != Screen.Data.Version)
            {
                _dataVersion = Screen.Data.Version;
                if (_config.DynamicDotCategories) BuildChips();
                if (_selected != null)
                {
                    _selected = Screen.Data.For(_config.Kind).FirstOrDefault(entry => entry.Id == _selected.Id);
                }

                ApplyFilter(true);
            }

            SyncOptionChips();
            _ownedFingerprint = "";
        }

        public override void Tick(bool force)
        {
            ActorInstance actor = Screen.SelectedActor;
            string fingerprint = actor == null ? "none" : actor.ActorGuid + "#" + GameActions.StateFingerprint(actor);
            if (!force && fingerprint == _ownedFingerprint) return;
            _ownedFingerprint = fingerprint;
            RebuildOwned(actor);
            _list.RebindVisible();
            UpdatePreview();
        }

        private void ApplyFilter(bool resetScroll)
        {
            List<CatalogEntry> source = Screen.Data.For(_config.Kind);
            string category = _category;
            string search = _search;
            _filtered = source
                .Where(entry => string.IsNullOrEmpty(category) || string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase))
                .Where(entry => entry.Matches(search))
                .ToList();
            if (_selected == null && _filtered.Count > 0) _selected = _filtered[0];
            _count.text = L.F("{0} shown / {1} total", "显示 {0} / 共 {1}", _filtered.Count, source.Count);
            _list.SetCount(_filtered.Count, resetScroll);
            UpdatePreview();
        }

        private void Select(int index, bool scroll)
        {
            if (index < 0 || index >= _filtered.Count) return;
            _selected = _filtered[index];
            _detailScroll.Content.anchoredPosition = Vector2.zero;
            if (scroll) _list.ScrollTo(index);
            _list.RebindVisible();
            UpdatePreview();
        }

        private void SelectEntry(CatalogEntry entry)
        {
            if (entry == null) return;
            int index = _filtered.IndexOf(entry);
            if (index < 0)
            {
                // Make it visible: clear filters that hide it.
                _category = "";
                _search = "";
                foreach (ButtonView chip in _chips) chip.SetSelected(chip.GameObject.name == "");
                _searchInput?.SetTextWithoutNotify("");
                ApplyFilter(false);
                index = _filtered.IndexOf(entry);
            }

            _selected = entry;
            if (index >= 0) Select(index, true);
            else UpdatePreview();
        }

        private void UpdatePreview()
        {
            ActorInstance actor = Screen.SelectedActor;
            string actorName = Screen.SelectedName;
            if (_selected == null)
            {
                U.SetIcon(_previewIcon, null);
                _previewName.text = L.T("Nothing selected", "未选择条目");
                _previewMeta.text = "";
                _previewDetail.text = L.T("Pick an entry from the library.", "请在左侧库中选择一个条目。");
                _addButton.SetText(L.T("Add", "添加"));
                _addButton.SetInteractable(false);
                _removeButton.SetVisible(false);
                _ownedBadgeLine.text = "";
                return;
            }

            U.SetIcon(_previewIcon, _selected.Icon);
            _previewName.text = _selected.Name;
            string category = _config.Kind == EntryKind.Dot
                ? GameText.DotTypeName(_selected.Category)
                : _config.Categories.FirstOrDefault(c => c.Key == _selected.Category && c.Key != "")?.Label ?? "";
            _previewMeta.text = _selected.Id + (string.IsNullOrEmpty(category) ? "" : "   ·   " + U.Colored(category, UiTheme.Gold));
            bool icons = U.UseGameSprites(_previewDetail);
            _previewDetail.text = string.IsNullOrWhiteSpace(_selected.Detail)
                ? U.Colored(L.T("The game provides no description for this entry.", "游戏没有为这个条目提供描述。"), UiTheme.TextDim)
                : GameText.SpriteTagsToNames(_selected.Detail, icons ? (Func<string, bool>)UiTheme.HasGameSprite : null);

            bool owned = _owned.Contains(_selected.Value);
            _ownedBadgeLine.text = actor == null ? "" : owned ? L.F("{0} already has this {1}.", "{0} 身上已有这个{1}。", actorName, _config.Noun) : "";
            _addButton.SetText(actor == null ? L.T("Select an actor first", "请先选择角色") : string.Format(_config.AddFormat, actorName));
            _addButton.SetInteractable(actor != null);
            _removeButton.SetVisible(actor != null && owned);
        }

        private void AddSelected()
        {
            ActorInstance actor = Screen.SelectedActor;
            CatalogEntry entry = _selected;
            if (actor == null || entry == null) return;
            Screen.Run(L.F("Added “{0}” to {1}", "已为 {1} 添加「{0}」", entry.Name, Screen.SelectedName), () => GameActions.Add(actor, entry, Screen.Options));
        }

        private void RemoveSelected()
        {
            ActorInstance actor = Screen.SelectedActor;
            CatalogEntry entry = _selected;
            if (actor == null || entry == null) return;
            Screen.Run(L.F("Removed “{0}” from {1}", "已从 {1} 身上移除「{0}」", entry.Name, Screen.SelectedName), () => GameActions.RemoveAll(actor, entry));
        }

        private void RebuildOwned(ActorInstance actor)
        {
            U.Clear(_ownedContent);
            _owned.Clear();
            if (actor == null)
            {
                _ownedTitle.text = _config.OwnedTitle;
                U.Label(_ownedContent, L.T("No actor selected.", "未选择角色。"), FS.Small, UiTheme.TextMuted);
                return;
            }

            List<OwnedEntry> entries = GameActions.Owned(actor, _config.Kind);
            foreach (OwnedEntry entry in entries)
            {
                if (entry.Value != null) _owned.Add(entry.Value);
            }

            int realCount = entries.Count(entry => !entry.IsEmptySlot);
            _ownedTitle.text = _config.OwnedTitle + "  " + U.Colored("· " + realCount, UiTheme.TextMuted);
            if (entries.Count == 0)
            {
                TextMeshProUGUI empty = U.Label(_ownedContent, L.T("Nothing yet. Add from the library →", "目前没有。可从右侧库中添加 →"), FS.Small, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, false, true);
                return;
            }

            string lastGroup = null;
            foreach (OwnedEntry entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Group) && entry.Group != lastGroup)
                {
                    lastGroup = entry.Group;
                    int inGroup = entries.Count(other => other.Group == entry.Group && !other.IsEmptySlot);
                    int slots = entries.Count(other => other.Group == entry.Group);
                    string label = GroupLabel(entry.Group);
                    string counter = _config.Kind == EntryKind.Item ? $"{inGroup} / {slots}" : inGroup.ToString();
                    TextMeshProUGUI header = U.Label(_ownedContent, label + "  " + U.Colored(counter, UiTheme.TextMuted), FS.Small, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
                    U.Size(header, -1f, 28f, 1f);
                }

                OwnedCard(entry);
            }
        }

        private string GroupLabel(string group)
        {
            switch (group)
            {
                case GameData.CategoryCombatItem: return L.T("Combat items", "战斗物品");
                case GameData.CategoryTrinket: return L.T("Trinkets", "饰品");
                case GameData.CategoryPositive: return L.T("Positive quirks", "正面特质");
                case GameData.CategoryNegative: return L.T("Negative quirks", "负面特质");
                case GameData.CategoryDisease: return L.T("Diseases / curses", "疾病 / 诅咒");
                default: return group;
            }
        }

        private void OwnedCard(OwnedEntry entry)
        {
            ButtonView card = U.Button(_ownedContent, "", ButtonKind.Normal, () =>
            {
                if (entry.Value == null) return;
                CatalogEntry match = Screen.Data.For(_config.Kind).FirstOrDefault(candidate => ReferenceEquals(candidate.Value, entry.Value));
                SelectEntry(match);
            }, -1f, 64f);
            U.Paint(card.Background, U.Theme.Card);
            UnityEngine.Object.Destroy(card.Label.gameObject);
            card.Label = null;
            if (entry.IsEmptySlot) card.Background.color = new Color(1f, 1f, 1f, 0.45f);

            HorizontalLayoutGroup row = U.Row(card.GameObject.transform, "Row", 8f, new RectOffset(8, 8, 4, 4));
            U.Stretch((RectTransform)row.transform);
            Image icon = U.Icon(row.transform, "Icon", 40f);
            U.SetIcon(icon, entry.Icon);
            VerticalLayoutGroup text = U.Column(row.transform, "Text", 0f, null, TextAnchor.MiddleLeft);
            U.Size(text, -1f, -1f, 1f);
            string name = entry.Name + (entry.Count > 1 ? U.Colored("  ×" + entry.Count, UiTheme.Gold) : "");
            TextMeshProUGUI nameLabel = U.Label(text.transform, name, FS.Body, entry.IsEmptySlot ? UiTheme.TextDim : UiTheme.Text);
            U.Size(nameLabel, -1f, 26f, 1f);
            TextMeshProUGUI sub = U.Label(text.transform, entry.Subtitle, FS.Tiny, UiTheme.TextDim);
            U.Size(sub, -1f, 20f, 1f);

            if (entry.Remove != null)
            {
                Action remove = entry.Remove;
                string entryName = entry.Name;
                U.Button(row.transform, L.T("Remove", "移除"), ButtonKind.Danger, () =>
                {
                    Screen.Run(L.F("Removed “{0}” from {1}", "已从 {1} 身上移除「{0}」", entryName, Screen.SelectedName), remove);
                }, 64f, 34f, FS.Small);
            }
        }
    }
}
