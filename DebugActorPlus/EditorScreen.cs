using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Code.Actor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    internal abstract class EditorView
    {
        protected readonly EditorScreen Screen;

        protected EditorView(EditorScreen screen)
        {
            Screen = screen;
        }

        public RectTransform Root { get; private set; }

        public abstract string TabTitle { get; }

        public void Build(Transform parent)
        {
            Root = U.Rect(parent, GetType().Name);
            U.Stretch(Root);
            BuildContent();
        }

        protected abstract void BuildContent();

        /// <summary>Called when the tab becomes visible or the selected actor changed.</summary>
        public virtual void OnShown()
        {
        }

        /// <summary>Polled a few times per second while visible; rebuild only when the game state changed.</summary>
        public virtual void Tick(bool force)
        {
        }
    }

    internal sealed class ActorCardView
    {
        public uint Guid;
        public ButtonView Button;
        public Image Portrait;
        public TextMeshProUGUI Name;
        public BarView Health;
        public BarView Stress;
    }

    /// <summary>The editor window: a modal native-styled uGUI canvas.</summary>
    internal sealed class EditorScreen
    {
        private const float TickInterval = 0.25f;

        private PluginSettings _settings;
        private readonly List<ActorCardView> _cards = new List<ActorCardView>();
        private readonly List<ButtonView> _tabButtons = new List<ButtonView>();
        private readonly List<EditorView> _views = new List<EditorView>();

        internal readonly GameData Data = new GameData();
        internal readonly AddOptions Options = new AddOptions();

        private GameObject _root;
        private Canvas _canvas;
        private bool _builtChinese;
        private UiTheme _theme;
        private RectTransform _stripContent;
        private TextMeshProUGUI _context;
        private TextMeshProUGUI _selection;
        private TextMeshProUGUI _status;
        private int _tab;
        private bool _catalogLoaded;
        private bool _forceRefresh;
        private string _actorFingerprint = "";
        private float _nextTick;
        private float _nextErrorLog;
        private string _catalogLanguage = "";
        private string _catalogSignature = "";

        public EditorScreen(PluginSettings settings)
        {
            _settings = settings;
        }

        /// <summary>New settings (config reloaded); takes effect on the next Rebuild.</summary>
        internal void ApplySettings(PluginSettings settings)
        {
            _settings = settings;
            _catalogLoaded = false;
            GameIcons.ClearMissing();
        }

        public bool Visible => _root != null && _root.activeSelf && _canvas != null && _canvas.enabled;

        internal uint SelectedGuid { get; private set; }

        internal ActorInstance SelectedActor => GameData.GetActor(SelectedGuid);

        internal ActorChoice SelectedChoice => Data.Actors.FirstOrDefault(actor => actor.Guid == SelectedGuid);

        internal string SelectedName => SelectedChoice?.Name ?? GameText.ActorName(SelectedActor);

        public void Toggle()
        {
            if (Visible) Close();
            else Open();
        }

        public void Open()
        {
            try
            {
                if (_root != null && _builtChinese != L.IsChinese)
                {
                    // Game language changed while closed ("auto" mode): rebuild the labels.
                    Destroy();
                }

                if (_root == null) Build();
                string language = L.IsChinese ? "zh" : "en";
                string signature = GameData.LibrarySignature();
                if (!_catalogLoaded || language != _catalogLanguage || signature != _catalogSignature)
                {
                    Data.Reload();
                    _catalogLoaded = true;
                    _catalogLanguage = language;
                    _catalogSignature = signature;
                }
                else
                {
                    Data.ReloadActors();
                }

                _actorFingerprint = GameData.ActorFingerprint();
                if (SelectedGuid == 0U || Data.Actors.All(actor => actor.Guid != SelectedGuid))
                {
                    SelectedGuid = Data.Actors.Count > 0 ? Data.Actors[0].Guid : 0U;
                }

                _root.SetActive(true);
                _canvas.enabled = true;
                InputGuard.Active = true;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                RebuildStrip();
                UpdateContext();
                ShowTab(_tab);
                SetStatus(L.F("Loaded {0} actors · {1} tokens · {2} buffs · {3} DOTs · {4} quirks · {5} items.",
                    "已载入 {0} 个角色 · {1} 个标记 · {2} 个增益 · {3} 个持续伤害 · {4} 个特质 · {5} 个物品。",
                    Data.Actors.Count, Data.Tokens.Count, Data.Buffs.Count, Data.Dots.Count, Data.Quirks.Count, Data.Items.Count));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("Failed to open Debug Actor Plus: " + ex);
                Close();
            }
        }

        public void Close()
        {
            InputGuard.Active = false;
            if (_root == null) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_root.transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            if (_canvas != null) _canvas.enabled = false;
            _root.SetActive(false);
        }

        public void Destroy()
        {
            InputGuard.Active = false;
            if (_root != null)
            {
                _root.SetActive(false);
                UnityEngine.Object.Destroy(_root);
            }

            _root = null;
            _canvas = null;
            _theme?.Destroy();
            _theme = null;
            U.Theme = null;
            U.ResetGlyphCache();
            _cards.Clear();
            _tabButtons.Clear();
            _views.Clear();
        }

        /// <summary>Rebuild the whole window (language or theme change) and keep the current selection.</summary>
        public void Rebuild()
        {
            bool wasVisible = Visible;
            Destroy();
            if (wasVisible) Open();
        }

        /// <summary>True while a text field of the editor has keyboard focus (Esc then only leaves the field).</summary>
        public bool IsTyping
        {
            get
            {
                GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected == null || _root == null || !selected.transform.IsChildOf(_root.transform)) return false;
                TMP_InputField input = selected.GetComponent<TMP_InputField>();
                return input != null && input.isFocused;
            }
        }

        public void LeaveTextField()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>
        /// TMP_InputField handles Esc itself (and drops focus) in the EventSystem update, which can
        /// run before the plugin's Update in the same frame, so also remember last frame's state.
        /// </summary>
        public bool WasTyping { get; private set; }

        public void LateTick()
        {
            WasTyping = Visible && IsTyping;
        }

        public void Tick()
        {
            if (!Visible)
            {
                // Never leave the game's input blocked if the canvas went away without Close().
                InputGuard.Active = false;
                return;
            }

            InputGuard.Active = true;
            if (!_forceRefresh && Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + TickInterval;
            bool force = _forceRefresh;
            _forceRefresh = false;

            try
            {
                string fingerprint = GameData.ActorFingerprint();
                if (fingerprint != _actorFingerprint)
                {
                    _actorFingerprint = fingerprint;
                    Data.ReloadActors();
                    if (Data.Actors.All(actor => actor.Guid != SelectedGuid))
                    {
                        SelectedGuid = Data.Actors.Count > 0 ? Data.Actors[0].Guid : 0U;
                    }

                    RebuildStrip();
                    UpdateContext();
                    CurrentView?.OnShown();
                    force = true;
                }

                UpdateStripValues();
                CurrentView?.Tick(force);
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextErrorLog)
                {
                    _nextErrorLog = Time.unscaledTime + 5f;
                    Plugin.Log?.LogWarning("Editor refresh failed (stale game data?): " + ex);
                }
            }
        }

        internal void RequestRefresh()
        {
            _forceRefresh = true;
        }

        internal void SelectActor(uint guid)
        {
            if (guid == SelectedGuid) return;
            SelectedGuid = guid;
            UpdateStripValues();
            UpdateSelectionLabel();
            CurrentView?.OnShown();
            RequestRefresh();
        }

        /// <summary>Runs a game mutation with feedback in the status bar.</summary>
        internal void Run(string successMessage, Action action)
        {
            try
            {
                action();
                SetStatus(successMessage, false);
            }
            catch (Exception ex)
            {
                SetStatus(L.T("Failed: ", "操作失败：") + ex.Message, true);
                Plugin.Log?.LogWarning("Action failed: " + ex);
            }

            RequestRefresh();
        }

        internal void SetStatus(string text, bool error = false)
        {
            if (_status == null) return;
            _status.text = text ?? "";
            _status.color = error ? UiTheme.Danger : UiTheme.TextMuted;
        }

        private EditorView CurrentView => _tab >= 0 && _tab < _views.Count ? _views[_tab] : null;

        private void Build()
        {
            _theme = UiTheme.Build(_settings);
            U.Theme = _theme;

            // Built while active (so TextMeshPro initialises and can measure text) but with the
            // canvas disabled, which keeps it invisible and out of the EventSystem's raycasts.
            _builtChinese = L.IsChinese;
            _root = new GameObject("DD2DebugActorPlus.Canvas", typeof(RectTransform));
            _root.layer = 5;
            UnityEngine.Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            canvas.pixelPerfect = false;
            _canvas = canvas;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            float scale = Mathf.Clamp(_settings?.UiScale ?? 1f, 0.6f, 1.6f);
            // 1600x900 reference: at 1280x720 the UI renders at 0.8x, so text stays readable.
            scaler.referenceResolution = new Vector2(1600f / scale, 900f / scale);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();

            // Full-screen backdrop: dims the game and catches every click outside the window.
            Image backdrop = U.Image(_root.transform, "Backdrop", _theme.Solid, UiTheme.Backdrop, true);
            U.Stretch(backdrop.rectTransform);

            Image window = U.Image(_root.transform, "Window", _theme.Window, Color.white, true);
            U.Stretch(window.rectTransform, 20f, 16f, 20f, 16f);
            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildHeader(window.transform);

            // Main row: actor list on the left, tabs + tab content on the right.
            HorizontalLayoutGroup main = U.Row(window.transform, "Main", 10f, null, TextAnchor.UpperLeft);
            main.childForceExpandHeight = true;
            U.Size(main, -1f, -1f, 1f, 1f);
            BuildActorColumn(main.transform);

            VerticalLayoutGroup right = U.Column(main.transform, "Right", 8f);
            U.Size(right, -1f, -1f, 1f, 1f);
            BuildTabs(right.transform);

            RectTransform body = U.Rect(right.transform, "Body");
            U.Size(body, -1f, -1f, 1f, 1f);
            _views.Add(new VitalsView(this));
            _views.Add(new CatalogView(this, CatalogConfig.Tokens()));
            _views.Add(new CatalogView(this, CatalogConfig.Buffs()));
            _views.Add(new CatalogView(this, CatalogConfig.Dots()));
            _views.Add(new CatalogView(this, CatalogConfig.Quirks()));
            _views.Add(new CatalogView(this, CatalogConfig.Items()));
            _views.Add(new RelationshipView(this));
            if (_settings != null && _settings.SpriteGallery) _views.Add(new SpriteGalleryView(this));
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].Build(body);
                _views[i].Root.gameObject.SetActive(false);
                int index = i;
                _tabButtons[i].SetText(_views[i].TabTitle);
                float tabWidth = Mathf.Max(84f, _tabButtons[i].Label.GetPreferredValues(_views[i].TabTitle).x + 30f);
                _tabButtons[i].Layout.preferredWidth = tabWidth;
                _tabButtons[i].Layout.minWidth = tabWidth;
            }

            BuildStatusBar(window.transform);

            if (_settings != null && _settings.DumpUiSprites)
            {
                UiTheme.DumpCandidateSprites();
            }
        }

        private void BuildHeader(Transform parent)
        {
            HorizontalLayoutGroup header = U.Row(parent, "Header", 12f, new RectOffset(4, 0, 0, 0));
            U.Size(header, -1f, 42f, 1f);

            TextMeshProUGUI title = U.Label(header.transform, L.T("Debug Actor Plus", "角色调试 Plus"), FS.Big, UiTheme.Gold, TextAlignmentOptions.MidlineLeft, true);
            U.Size(title, title.GetPreferredValues().x + 8f, 42f);
            _context = U.Label(header.transform, "", FS.Small, UiTheme.TextMuted);
            U.Size(_context, -1f, 42f, 1f);

            U.Button(header.transform, L.NextModeLabel(), ButtonKind.Normal, () =>
            {
                Plugin.Instance?.SetLanguage(L.NextMode());
            }, 76f, 36f);
            // Reload re-reads the config file and rebuilds the window (frames, scale, language).
            U.Button(header.transform, L.T("Reload", "重新载入"), ButtonKind.Normal, () =>
            {
                if (Plugin.Instance != null) Plugin.Instance.ReloadEditor();
                else
                {
                    _catalogLoaded = false;
                    Open();
                }
            }, -1f, 36f);
            U.Button(header.transform, U.Glyph("✕", U.Glyph("×", "X")), ButtonKind.Danger, Close, 44f, 36f, FS.Emph);
        }

        /// <summary>Heroes and enemies stacked in one scrollable column on the left.</summary>
        private void BuildActorColumn(Transform parent)
        {
            Image column = U.Image(parent, "Actors", _theme.Panel, Color.white);
            U.Size(column, 268f, -1f, 0f, 1f);
            ScrollView list = U.Scroll(column.transform, "ActorScroll", false, 6f, new RectOffset(8, 6, 8, 8));
            U.Stretch(list.Root);
            _stripContent = list.Content;
        }

        private void BuildTabs(Transform parent)
        {
            HorizontalLayoutGroup tabs = U.Row(parent, "Tabs", 6f);
            U.Size(tabs, -1f, 42f, 1f);
            int tabCount = 7 + (_settings != null && _settings.SpriteGallery ? 1 : 0);
            for (int i = 0; i < tabCount; i++)
            {
                int index = i;
                ButtonView tab = U.Button(tabs.transform, "", ButtonKind.Tab, () => ShowTab(index), 96f, 40f, FS.Body);
                _tabButtons.Add(tab);
            }

            // Which actor the tabs are editing, right of the tabs.
            _selection = U.Label(tabs.transform, "", FS.Small, UiTheme.TextMuted, TextAlignmentOptions.MidlineRight);
            U.Size(_selection, -1f, 40f, 1f, -1f, 60f);
        }

        private void BuildStatusBar(Transform parent)
        {
            Image bar = U.Image(parent, "StatusBar", _theme.Panel, Color.white);
            U.Size(bar, -1f, 34f, 1f);
            _status = U.Label(bar.transform, "", FS.Small, UiTheme.TextMuted);
            U.Stretch(_status.rectTransform, 12f, 0f, 230f, 0f);
            TextMeshProUGUI hint = U.Label(bar.transform, L.T("Alt+J / Esc to close", "Alt+J / Esc 关闭"), FS.Tiny, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            hint.rectTransform.anchorMin = new Vector2(1f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.pivot = new Vector2(1f, 0.5f);
            hint.rectTransform.sizeDelta = new Vector2(220f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
        }

        private void ShowTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _views.Count - 1);
            for (int i = 0; i < _views.Count; i++)
            {
                bool active = i == _tab;
                _views[i].Root.gameObject.SetActive(active);
                _tabButtons[i].SetSelected(active);
            }

            CurrentView?.OnShown();
            RequestRefresh();
        }

        private void UpdateContext()
        {
            if (_context == null) return;
            int heroes = Data.Actors.Count(actor => actor.IsHero);
            int enemies = Data.Actors.Count - heroes;
            _context.text = Data.InCombat
                ? L.F("In combat · {0} heroes · {1} enemies", "战斗中 · {0} 名英雄 · {1} 个敌人", heroes, enemies)
                : L.F("Out of combat · {0} party members", "非战斗 · 队伍 {0} 人", Data.Actors.Count);
        }

        private void RebuildStrip()
        {
            if (_stripContent == null) return;
            U.Clear(_stripContent);
            _cards.Clear();

            if (Data.Actors.Count == 0)
            {
                U.Label(_stripContent, L.T("No actors loaded. Open the editor in combat or with a party.", "当前没有可编辑的角色：请在战斗中或有队伍时打开。"), FS.Small, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, false, true);
                UpdateSelectionLabel();
                return;
            }

            foreach (bool heroes in new[] { true, false })
            {
                List<ActorChoice> group = Data.Actors.Where(actor => actor.IsHero == heroes).ToList();
                if (group.Count == 0) continue;
                GroupLabel((heroes ? L.T("Heroes", "英雄") : L.T("Enemies", "敌人")) + "  " + U.Colored(group.Count.ToString(), UiTheme.TextMuted), heroes ? UiTheme.Gold : UiTheme.Danger);
                foreach (ActorChoice actor in group) _cards.Add(ActorCard(actor));
            }

            UpdateStripValues();
            UpdateSelectionLabel();
        }

        private void GroupLabel(string text, Color color)
        {
            TextMeshProUGUI label = U.Label(_stripContent, text, FS.Emph, color, TextAlignmentOptions.BottomLeft, true);
            U.Size(label, -1f, 30f, 1f);
        }

        private ActorCardView ActorCard(ActorChoice actor)
        {
            ActorCardView card = new ActorCardView { Guid = actor.Guid };
            ButtonView button = U.Button(_stripContent, "", ButtonKind.Normal, () => SelectActor(actor.Guid), -1f, 66f);
            U.Paint(button.Background, _theme.Card);
            UnityEngine.Object.Destroy(button.Label.gameObject);
            button.Label = null;
            card.Button = button;

            HorizontalLayoutGroup row = U.Row(button.GameObject.transform, "Row", 8f, new RectOffset(6, 8, 5, 5));
            U.Stretch((RectTransform)row.transform);
            card.Portrait = U.Icon(row.transform, "Portrait", 54f);
            U.SetIcon(card.Portrait, GameIcons.Portrait(GameData.GetActor(actor.Guid)));

            VerticalLayoutGroup info = U.Column(row.transform, "Info", 4f, null, TextAnchor.MiddleLeft);
            U.Size(info, -1f, -1f, 1f);
            card.Name = U.Label(info.transform, actor.Name, FS.Body, UiTheme.Text, TextAlignmentOptions.MidlineLeft, true);
            U.Size(card.Name, -1f, 24f, 1f);

            // Health and stress side by side (monsters have no stress: health takes the row).
            HorizontalLayoutGroup bars = U.Row(info.transform, "Bars", 4f);
            U.Size(bars, -1f, 22f, 1f);
            card.Health = U.Bar(bars.transform, UiTheme.HealthFill, 22f, FS.Tiny - 1f);
            U.Size(card.Health.Root, -1f, 22f, 1.4f);
            card.Stress = U.Bar(bars.transform, UiTheme.StressFill, 22f, FS.Tiny - 1f);
            U.Size(card.Stress.Root, -1f, 22f, 1f);
            return card;
        }

        private void UpdateSelectionLabel()
        {
            if (_selection == null) return;
            ActorChoice choice = SelectedChoice;
            _selection.text = choice == null
                ? ""
                : L.T("Editing ", "当前：") + U.Colored(choice.Name, UiTheme.Gold) + "  " + U.Colored(choice.DataId, UiTheme.TextDim);
        }

        private void UpdateStripValues()
        {
            foreach (ActorCardView card in _cards)
            {
                ActorInstance actor = GameData.GetActor(card.Guid);
                if (actor == null) continue;
                Sprite cardSprite = card.Guid == SelectedGuid ? _theme.CardSelected : _theme.Card;
                if (card.Button.Background.sprite != cardSprite) U.Paint(card.Button.Background, cardSprite);
                float hp = actor.DisplayedHp, hpMax = actor.DisplayedHpMax;
                card.Health.Set(hp, hpMax, $"{hp:0} / {hpMax:0}");
                float stressMax = actor.StressMax;
                bool hasStress = stressMax > 0f;
                if (card.Stress.Root.gameObject.activeSelf != hasStress) card.Stress.Root.gameObject.SetActive(hasStress);
                if (hasStress) card.Stress.Set(actor.Stress, stressMax, $"{actor.Stress:0} / {stressMax:0}");
                if (card.Portrait.sprite == null) U.SetIcon(card.Portrait, GameIcons.Portrait(actor));
            }
        }
    }
}
