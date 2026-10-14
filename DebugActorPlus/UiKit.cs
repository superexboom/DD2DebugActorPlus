using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    internal enum ButtonKind
    {
        Normal,
        Primary,
        Danger,
        Chip,
        Tab,
        Ghost,
    }

    /// <summary>A button plus the pieces we restyle (selection, label, enabled state).</summary>
    internal sealed class ButtonView
    {
        public GameObject GameObject;
        public Button Button;
        public Image Background;
        public TextMeshProUGUI Label;
        public LayoutElement Layout;
        public ButtonKind Kind;
        private bool _selected;

        public void SetText(string text)
        {
            if (Label != null) Label.text = text ?? "";
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            Apply();
        }

        public void SetInteractable(bool interactable)
        {
            if (Button != null) Button.interactable = interactable;
            Apply();
        }

        public void SetVisible(bool visible)
        {
            if (GameObject != null && GameObject.activeSelf != visible) GameObject.SetActive(visible);
        }

        public void SetTint(Color color)
        {
            if (Background != null) Background.color = color;
        }

        private void Apply()
        {
            UiTheme theme = U.Theme;
            if (theme == null || Background == null) return;
            bool interactable = Button == null || Button.interactable;
            switch (Kind)
            {
                case ButtonKind.Chip:
                case ButtonKind.Tab:
                    U.Paint(Background, _selected ? theme.ChipSelected : theme.Chip);
                    if (Label != null) Label.color = _selected ? UiTheme.Gold : interactable ? UiTheme.Text : UiTheme.TextDim;
                    break;
                case ButtonKind.Ghost:
                    if (Label != null) Label.color = interactable ? (_selected ? UiTheme.Gold : UiTheme.TextMuted) : UiTheme.TextDim;
                    break;
                default:
                    if (Label != null) Label.color = interactable ? UiTheme.Text : UiTheme.TextDim;
                    break;
            }
        }
    }

    /// <summary>
    /// Image that never sizes the layout. A plain sliced Image reports its sprite's 9-slice border
    /// as preferred size, and the game's frames have 100-350 px borders, which squeezed siblings.
    /// </summary>
    internal sealed class FrameImage : Image
    {
        public override float minWidth => 0f;
        public override float preferredWidth => 0f;
        public override float flexibleWidth => -1f;
        public override float minHeight => 0f;
        public override float preferredHeight => 0f;
        public override float flexibleHeight => -1f;
    }

    /// <summary>Double-click and right-click relay for list rows.</summary>
    internal sealed class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action OnDoubleClick;
        public Action OnRightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClick?.Invoke();
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            {
                OnDoubleClick?.Invoke();
            }
        }
    }

    /// <summary>
    /// Notifies when a RectTransform is resized (virtual lists need their viewport height). The
    /// callback runs on the next Update, never inside the canvas layout pass that reported it.
    /// </summary>
    internal sealed class ResizeRelay : UIBehaviour
    {
        public Action OnResized;
        private bool _pending = true;

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            _pending = true;
        }

        private void Update()
        {
            if (!_pending) return;
            _pending = false;
            try
            {
                OnResized?.Invoke();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug("List resize refresh failed: " + ex.Message);
            }
        }
    }

    internal sealed class BarView
    {
        public RectTransform Root;
        public Image Fill;
        public TextMeshProUGUI Label;

        public void Set(float value, float max, string text)
        {
            float fraction = max <= 0f ? 0f : Mathf.Clamp01(value / max);
            if (Fill != null)
            {
                Fill.rectTransform.anchorMax = new Vector2(fraction, 1f);
            }

            if (Label != null) Label.text = text ?? "";
        }
    }

    internal sealed class ScrollView
    {
        public RectTransform Root;
        public ScrollRect ScrollRect;
        public RectTransform Viewport;
        public RectTransform Content;
    }

    /// <summary>
    /// Type scale in canvas units (reference 1600x900). At 1280x720 multiply by 0.8 for pixels,
    /// so the smallest size still renders at ~12 px.
    /// </summary>
    internal static class FS
    {
        internal const float Tiny = 15f;
        internal const float Small = 16f;
        internal const float Body = 18f;
        internal const float Emph = 20f;
        internal const float Header = 23f;
        internal const float Big = 27f;
    }

    /// <summary>Small uGUI factory. Everything is built in code on the editor's own canvas.</summary>
    internal static class U
    {
        internal static UiTheme Theme;

        internal static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            RectTransform rect = (RectTransform)go.transform;
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }

        internal static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static LayoutElement Size(Component component, float width = -1f, float height = -1f, float flexWidth = -1f, float flexHeight = -1f, float minWidth = -1f, float minHeight = -1f)
        {
            GameObject go = component.gameObject;
            LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            // A fixed size must not inherit flexibility from flexible children (layout groups
            // report their children's flexible sizes), so "fixed" means flexible = 0.
            element.flexibleWidth = flexWidth >= 0f ? flexWidth : width >= 0f ? 0f : -1f;
            element.flexibleHeight = flexHeight >= 0f ? flexHeight : height >= 0f ? 0f : -1f;
            element.minWidth = minWidth >= 0f ? minWidth : width >= 0f && flexWidth < 0f ? width : -1f;
            element.minHeight = minHeight >= 0f ? minHeight : height >= 0f && flexHeight < 0f ? height : -1f;
            return element;
        }

        internal static HorizontalLayoutGroup Row(Transform parent, string name, float spacing = 8f, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(parent, name);
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        internal static VerticalLayoutGroup Column(Transform parent, string name, float spacing = 8f, RectOffset padding = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            RectTransform rect = Rect(parent, name);
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        internal static RectOffset Pad(int all)
        {
            return new RectOffset(all, all, all, all);
        }

        internal static RectOffset Pad(int horizontal, int vertical)
        {
            return new RectOffset(horizontal, horizontal, vertical, vertical);
        }

        internal static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rect = Rect(parent, name);
            Image image = rect.gameObject.AddComponent<FrameImage>();
            Paint(image, sprite);
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Adds a sliced background to an existing (layout) object.</summary>
        internal static Image Background(Component target, Sprite sprite, Color? color = null, bool raycast = false)
        {
            Image image = target.gameObject.GetComponent<Image>() ?? target.gameObject.AddComponent<FrameImage>();
            Paint(image, sprite);
            image.color = color ?? Color.white;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>
        /// Assigns a frame sprite: 9-sliced when it has a border, with the theme's border shrink
        /// factor (game frames have 60-350 px borders made for full-screen panels).
        /// </summary>
        internal static void Paint(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.pixelsPerUnitMultiplier = Theme != null ? Theme.MultiplierOf(sprite) : 1f;
        }

        internal static Image Icon(Transform parent, string name, float size)
        {
            Image image = Image(parent, name, null, Color.white);
            image.preserveAspect = true;
            image.type = UnityEngine.UI.Image.Type.Simple;
            Size(image, size, size);
            return image;
        }

        internal static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            // No icon: keep the layout slot but draw nothing (a grey square looks broken).
            image.color = sprite == null ? new Color(1f, 1f, 1f, 0f) : Color.white;
        }

        private static readonly Dictionary<string, bool> GlyphCache = new Dictionary<string, bool>();

        /// <summary>Returns <paramref name="preferred"/> when the UI font can draw it, else <paramref name="fallback"/>.</summary>
        internal static string Glyph(string preferred, string fallback)
        {
            if (string.IsNullOrEmpty(preferred)) return fallback ?? "";
            if (!GlyphCache.TryGetValue(preferred, out bool ok))
            {
                ok = true;
                try
                {
                    TMP_FontAsset font = Theme?.Body;
                    if (font != null)
                    {
                        foreach (char c in preferred)
                        {
                            if (char.IsWhiteSpace(c)) continue;
                            if (!font.HasCharacter(c, true, true) && (Theme.Title == null || !Theme.Title.HasCharacter(c, true, true)))
                            {
                                ok = false;
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    ok = false;
                }

                GlyphCache[preferred] = ok;
            }

            return ok ? preferred : fallback;
        }

        internal static void ResetGlyphCache()
        {
            GlyphCache.Clear();
        }

        /// <summary>
        /// Lets game descriptions render their inline &lt;sprite name=...&gt; icons. Returns false when
        /// the game's icon sprite asset is not available (callers then replace the tags with names).
        /// </summary>
        internal static bool UseGameSprites(TMP_Text text)
        {
            if (text == null) return false;
            TMP_SpriteAsset sprites = UiTheme.GameSpriteAsset();
            if (sprites == null) return false;
            if (text.spriteAsset != sprites) text.spriteAsset = sprites;
            return true;
        }

        internal static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool title = false, bool wrap = false)
        {
            RectTransform rect = Rect(parent, "Label");
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Theme != null)
            {
                TMP_FontAsset font = title ? Theme.Title : Theme.Body;
                if (font != null) label.font = font;
            }

            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.richText = true;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.text = text ?? "";
            return label;
        }

        internal static ButtonView Button(Transform parent, string text, ButtonKind kind, Action onClick, float width = -1f, float height = 36f, float fontSize = FS.Body)
        {
            RectTransform rect = Rect(parent, "Button");
            Image background = rect.gameObject.AddComponent<FrameImage>();
            Paint(background, SpriteFor(kind));
            background.color = kind == ButtonKind.Ghost ? new Color(1f, 1f, 1f, 0f) : Color.white;
            background.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
            colors.selectedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    try
                    {
                        onClick();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning("UI action failed: " + ex);
                    }

                    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == rect.gameObject)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }
                });
            }

            TextMeshProUGUI label = Label(rect, text, fontSize, UiTheme.Text, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 8f, 2f, 8f, 2f);

            ButtonView view = new ButtonView
            {
                GameObject = rect.gameObject,
                Button = button,
                Background = background,
                Label = label,
                Kind = kind,
            };
            float preferredWidth = width > 0f ? width : Mathf.Max(48f, label.GetPreferredValues(text ?? "").x + 26f);
            view.Layout = Size(rect, preferredWidth, height);
            view.SetSelected(false);
            return view;
        }

        private static Sprite SpriteFor(ButtonKind kind)
        {
            if (Theme == null) return null;
            switch (kind)
            {
                case ButtonKind.Primary: return Theme.ButtonPrimary;
                case ButtonKind.Danger: return Theme.ButtonDanger;
                case ButtonKind.Chip:
                case ButtonKind.Tab: return Theme.Chip;
                case ButtonKind.Ghost: return Theme.Solid;
                default: return Theme.Button;
            }
        }

        internal static TMP_InputField Input(Transform parent, string placeholder, Action<string> onChanged, float height = 38f, float fontSize = FS.Body)
        {
            RectTransform rect = Rect(parent, "Input");
            Image background = rect.gameObject.AddComponent<FrameImage>();
            Paint(background, Theme?.Field);
            background.raycastTarget = true;

            RectTransform area = Rect(rect, "Text Area");
            Stretch(area, 10f, 4f, 10f, 4f);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = Label(area, placeholder, fontSize, UiTheme.TextDim);
            hint.fontStyle = FontStyles.Italic;
            Stretch(hint.rectTransform);
            TextMeshProUGUI text = Label(area, "", fontSize, UiTheme.Text);
            Stretch(text.rectTransform);

            // The labels initialise while active; the input field itself is enabled only after its
            // viewport / text / placeholder references are assigned.
            bool wasActive = rect.gameObject.activeSelf;
            rect.gameObject.SetActive(false);
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            if (Theme?.Body != null) input.fontAsset = Theme.Body;
            input.pointSize = fontSize;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = UiTheme.Gold;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.84f, 0.69f, 0.40f, 0.35f);
            input.richText = false;
            // Esc only leaves the field; keep what was typed (TMP would restore the old text).
            input.restoreOriginalTextOnEscape = false;
            Navigation navigation = input.navigation;
            navigation.mode = Navigation.Mode.None;
            input.navigation = navigation;
            if (onChanged != null)
            {
                input.onValueChanged.AddListener(value =>
                {
                    try
                    {
                        onChanged(value);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning("Input handler failed: " + ex.Message);
                    }
                });
            }

            Size(rect, -1f, height, 1f);
            rect.gameObject.SetActive(wasActive);
            return input;
        }

        internal static ScrollView Scroll(Transform parent, string name, bool horizontal = false, float spacing = 6f, RectOffset padding = null)
        {
            RectTransform root = Rect(parent, name);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            scroll.inertia = false;

            RectTransform viewport = Rect(root, "Viewport");
            Stretch(viewport, 0f, 0f, horizontal ? 0f : 10f, horizontal ? 8f : 0f);
            Image viewportHit = viewport.gameObject.AddComponent<FrameImage>();
            viewportHit.color = new Color(0f, 0f, 0f, 0f);
            viewportHit.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect(viewport, "Content");
            if (horizontal)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
                HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = spacing;
                layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
                VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = spacing;
                layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = horizontal ? null : Scrollbar(root, false);
            scroll.horizontalScrollbar = horizontal ? Scrollbar(root, true) : null;
            if (scroll.verticalScrollbar != null) scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            if (scroll.horizontalScrollbar != null) scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            return new ScrollView { Root = root, ScrollRect = scroll, Viewport = viewport, Content = content };
        }

        private static Scrollbar Scrollbar(RectTransform scrollRoot, bool horizontal)
        {
            RectTransform bar = Rect(scrollRoot, horizontal ? "Scrollbar Horizontal" : "Scrollbar Vertical");
            if (horizontal)
            {
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(0f, 6f);
                bar.anchoredPosition = Vector2.zero;
            }
            else
            {
                bar.anchorMin = new Vector2(1f, 0f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(1f, 0.5f);
                bar.sizeDelta = new Vector2(6f, 0f);
                bar.anchoredPosition = Vector2.zero;
            }

            Image track = bar.gameObject.AddComponent<FrameImage>();
            track.sprite = Theme?.Solid;
            track.color = new Color(0f, 0f, 0f, 0.35f);
            track.raycastTarget = true;

            RectTransform area = Rect(bar, "Sliding Area");
            Stretch(area);
            RectTransform handle = Rect(area, "Handle");
            Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<FrameImage>();
            Paint(handleImage, Theme?.ScrollHandle);
            handleImage.color = Theme != null && Theme.IsGameFrame(Theme.ScrollHandle) ? Color.white : new Color(0.55f, 0.45f, 0.31f, 0.9f);
            handleImage.raycastTarget = true;

            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = horizontal ? UnityEngine.UI.Scrollbar.Direction.LeftToRight : UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            Navigation navigation = scrollbar.navigation;
            navigation.mode = Navigation.Mode.None;
            scrollbar.navigation = navigation;
            return scrollbar;
        }

        internal static BarView Bar(Transform parent, Color fill, float height, float fontSize = FS.Tiny)
        {
            RectTransform root = Rect(parent, "Bar");
            Image back = root.gameObject.AddComponent<FrameImage>();
            Paint(back, Theme?.BarTrough);
            back.color = Color.white;
            back.raycastTarget = false;

            RectTransform fillRect = Rect(root, "Fill");
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            Image fillImage = fillRect.gameObject.AddComponent<FrameImage>();
            fillImage.sprite = Theme?.Solid;
            fillImage.color = fill;
            fillImage.raycastTarget = false;

            // Title font: the body font uses old-style figures ("0" looks like "o").
            TextMeshProUGUI label = Label(root, "", fontSize + 1f, UiTheme.Text, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform);
            // Never hide the numbers: with Ellipsis TMP drops the whole line when it is a bit taller than the bar.
            label.overflowMode = TextOverflowModes.Overflow;
            label.outlineWidth = 0.22f;
            label.outlineColor = new Color32(0, 0, 0, 200);
            Size(root, -1f, height, 1f);
            return new BarView { Root = root, Fill = fillImage, Label = label };
        }

        internal static RectTransform Spacer(Transform parent, float width = -1f, float height = -1f, bool flexible = false)
        {
            RectTransform rect = Rect(parent, "Spacer");
            Size(rect, width, height, flexible ? 1f : -1f, -1f);
            return rect;
        }

        internal static Image Divider(Transform parent, bool vertical = false)
        {
            bool native = !vertical && Theme != null && Theme.IsGameFrame(Theme.Divider);
            Image line = Image(parent, "Divider", native ? Theme.Divider : Theme?.Solid, native ? new Color(1f, 1f, 1f, 0.75f) : new Color(0.42f, 0.33f, 0.22f, 0.55f));
            if (vertical) Size(line, 1f, -1f, -1f, 1f);
            else Size(line, -1f, native ? 8f : 1f, 1f);
            return line;
        }

        internal static void Clear(Transform parent)
        {
            if (parent == null) return;
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in parent) children.Add(child.gameObject);
            foreach (GameObject child in children)
            {
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        internal static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        internal static string Colored(string text, Color color)
        {
            return "<color=#" + Hex(color) + ">" + text + "</color>";
        }
    }

    /// <summary>Scroll list that only instantiates the visible rows (libraries have thousands of entries).</summary>
    internal sealed class VirtualList<TRow> where TRow : class
    {
        private readonly ScrollRect _scroll;
        private readonly RectTransform _content;
        private readonly RectTransform _viewport;
        private readonly float _rowHeight;
        private readonly float _spacing;
        private readonly Func<RectTransform, TRow> _create;
        private readonly Action<TRow, int> _bind;
        private readonly Func<TRow, RectTransform> _rectOf;
        private readonly List<TRow> _rows = new List<TRow>();
        private int _count;
        private int _firstBound = -1;
        private int _boundCount = -1;

        public VirtualList(ScrollView view, float rowHeight, float spacing, Func<RectTransform, TRow> create, Func<TRow, RectTransform> rectOf, Action<TRow, int> bind)
        {
            _scroll = view.ScrollRect;
            _viewport = view.Viewport;
            _content = view.Content;
            _rowHeight = rowHeight;
            _spacing = spacing;
            _create = create;
            _rectOf = rectOf;
            _bind = bind;

            // Rows are positioned manually: no layout group / fitter on this content.
            UnityEngine.Object.DestroyImmediate(_content.GetComponent<ContentSizeFitter>());
            UnityEngine.Object.DestroyImmediate(_content.GetComponent<VerticalLayoutGroup>());
            _scroll.onValueChanged.AddListener(_ => Refresh(false));
            ResizeRelay relay = _viewport.gameObject.AddComponent<ResizeRelay>();
            relay.OnResized = () => Refresh(true);
        }

        public int Count => _count;

        public void SetCount(int count, bool resetScroll)
        {
            _count = Math.Max(0, count);
            _content.sizeDelta = new Vector2(0f, _count * (_rowHeight + _spacing));
            if (resetScroll) _content.anchoredPosition = Vector2.zero;
            Refresh(true);
        }

        public void Refresh(bool force)
        {
            float step = _rowHeight + _spacing;
            float viewportHeight = Mathf.Max(_viewport.rect.height, step * 4f);
            int visible = Mathf.CeilToInt(viewportHeight / step) + 2;
            while (_rows.Count < visible)
            {
                TRow row = _create(_content);
                RectTransform rect = _rectOf(row);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, _rowHeight);
                _rows.Add(row);
                force = true;
            }

            int first = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, _content.anchoredPosition.y) / step), 0, Math.Max(0, _count - 1));
            if (!force && first == _firstBound && _boundCount == _count) return;
            _firstBound = first;
            _boundCount = _count;

            for (int i = 0; i < _rows.Count; i++)
            {
                int index = first + i;
                RectTransform rect = _rectOf(_rows[i]);
                if (index >= _count)
                {
                    if (rect.gameObject.activeSelf) rect.gameObject.SetActive(false);
                    continue;
                }

                if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);
                rect.anchoredPosition = new Vector2(0f, -index * step);
                _bind(_rows[i], index);
            }
        }

        public void RebindVisible()
        {
            Refresh(true);
        }

        public void ScrollTo(int index)
        {
            if (index < 0 || index >= _count) return;
            float step = _rowHeight + _spacing;
            float top = index * step;
            float viewportHeight = _viewport.rect.height;
            float current = _content.anchoredPosition.y;
            if (top < current) current = top;
            else if (top + step > current + viewportHeight) current = top + step - viewportHeight;
            _content.anchoredPosition = new Vector2(0f, Mathf.Max(0f, current));
            Refresh(true);
        }
    }
}
