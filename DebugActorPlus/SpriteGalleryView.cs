using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DD2DebugActorPlus
{
    /// <summary>
    /// Diagnostics tab (Diagnostics.SpriteGallery): every loaded 9-slice game sprite drawn as a
    /// stretched frame with its name, to choose native frames for the editor's theme.
    /// </summary>
    internal sealed class SpriteGalleryView : EditorView
    {
        private RectTransform _grid;
        private TextMeshProUGUI _summary;
        private TMP_InputField _filter;
        private string _query = "";
        private int _builtCount = -1;

        public SpriteGalleryView(EditorScreen screen) : base(screen)
        {
        }

        public override string TabTitle => L.T("Sprites", "素材");

        protected override void BuildContent()
        {
            VerticalLayoutGroup column = U.Column(Root, "Gallery", 8f, U.Pad(12));
            U.Stretch((RectTransform)column.transform);
            U.Background(column, U.Theme.Panel);

            HorizontalLayoutGroup header = U.Row(column.transform, "Header", 10f);
            U.Size(header, -1f, 40f, 1f);
            _summary = U.Label(header.transform, "", FS.Small, UiTheme.TextMuted);
            U.Size(_summary, -1f, 40f, 1f);
            _filter = U.Input(header.transform, L.T("Filter by name…", "按名称过滤……"), value =>
            {
                _query = value ?? "";
                Rebuild();
            }, 38f, FS.Small);
            U.Size(_filter, 360f, 38f);

            ScrollView scroll = U.Scroll(column.transform, "GalleryScroll", false, 0f, U.Pad(4));
            U.Size(scroll.Root, -1f, -1f, 1f, 1f);
            UnityEngine.Object.DestroyImmediate(scroll.Content.GetComponent<VerticalLayoutGroup>());
            GridLayoutGroup grid = scroll.Content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(232f, 112f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            _grid = scroll.Content;
        }

        public override void OnShown()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            List<Sprite> sprites = Resources.FindObjectsOfTypeAll<Sprite>()
                .Where(sprite => sprite != null && sprite.border != Vector4.zero && sprite.texture != null)
                .Where(sprite => string.IsNullOrEmpty(_query) || sprite.name.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(sprite => sprite.name + "|" + sprite.rect.size)
                .Select(group => group.First())
                .OrderBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sprites.Count == _builtCount && string.IsNullOrEmpty(_query)) return;
            _builtCount = sprites.Count;
            U.Clear(_grid);
            _summary.text = L.F("{0} loaded 9-slice sprites (frames drawn stretched to 210×64)", "已加载的九宫格贴图 {0} 张（按 210×64 拉伸预览）", sprites.Count);

            foreach (Sprite sprite in sprites.Take(400))
            {
                RectTransform cell = U.Rect(_grid, sprite.name);
                Image back = cell.gameObject.AddComponent<FrameImage>();
                back.sprite = U.Theme.Solid;
                back.color = new Color(0.30f, 0.30f, 0.32f, 1f);

                Image frame = U.Image(cell, "Frame", sprite, Color.white);
                frame.type = Image.Type.Sliced;
                RectTransform rect = frame.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(210f, 64f);
                rect.anchoredPosition = new Vector2(11f, -6f);

                TextMeshProUGUI label = U.Label(cell, $"{sprite.name}\n{sprite.rect.width:0}×{sprite.rect.height:0}  b={sprite.border.x:0},{sprite.border.y:0},{sprite.border.z:0},{sprite.border.w:0}",
                    12f, UiTheme.Text, TextAlignmentOptions.TopLeft, false, true);
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(-8f, 38f);
                labelRect.anchoredPosition = new Vector2(0f, 2f);
            }
        }
    }
}
