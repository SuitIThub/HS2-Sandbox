using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class AnimBrowserWindow
    {
        private const float CardNameRowHBase = 20f;
        private float CardNameRowH => AnimBrowserScale.Px(CardNameRowHBase);
        private const float CardTextPadH = 4f;
        private const float GridCardSizeBoost = 1.12f;
        private const string LabelEllipsis = "...";

        private static readonly Color AnimCardBaseTint = new Color(0.32f, 0.32f, 0.34f, 0.62f);
        private static readonly Color AnimCardSelectedTint = new Color(0.22f, 0.48f, 0.98f, 0.62f);

        private GUIStyle? _animCardBaseStyle;
        private GUIStyle? _animCardSelectedStyle;
        private GUIStyle? _animGroupCardStyle;
        private GUIStyle? _animCardNameStyle;
        private GUIStyle? _treeNodeStyle;
        private GUIStyle? _treeNodeSelectedStyle;
        private GUIStyle? _listRowStyle;
        private GUIStyle? _roleButtonStyle;
        private GUIStyle? _roleButtonActiveStyle;
        private GUIStyle? _reviewSectionTitleStyle;

        private int _gridLayoutFrame = -1;
        private float _gridLayoutAvailW;
        private int _gridLayoutColumns;
        private float _gridLayoutCellInnerW;
        private float _gridLayoutContentWidth;
        private float _gridLayoutCardOuterH;

        private void InvalidateGridLayoutCache()
        {
            _gridLayoutFrame = -1;
            _gridLayoutColumns = 0;
        }

        private void InvalidateAnimBrowserStyleCaches()
        {
            _animCardBaseStyle = null;
            _animCardSelectedStyle = null;
            _animGroupCardStyle = null;
            _animCardNameStyle = null;
            _treeNodeStyle = null;
            _treeNodeSelectedStyle = null;
            _listRowStyle = null;
            _roleButtonStyle = null;
            _roleButtonActiveStyle = null;
            _reviewSectionTitleStyle = null;
            _characterHintStyle = null;
            _controlsSectionTitleStyle = null;
            _controlsFieldLabelStyle = null;
            _controlsInfoStyle = null;
            _controlsGroupTitleStyle = null;
            _optionsWrapStyle = null;
            _hotkeySectionBoxStyle = null;
            _hotkeyHeaderStyle = null;
            _hotkeyRowBoxStyle = null;
            _hotkeyActionStyle = null;
            _hotkeyBindingBadgeStyle = null;
            _hotkeyUnassignedBadgeStyle = null;
        }

        private void InitStyles()
        {
            if (_treeNodeStyle != null)
                return;

            var cardChrome = CreateCardChromeTemplate();
            _animCardBaseStyle = CardTintStyle(cardChrome, AnimCardBaseTint);
            _animCardSelectedStyle = CardTintStyle(cardChrome, AnimCardSelectedTint);
            _animGroupCardStyle = CardTintStyle(cardChrome, new Color(0.26f, 0.30f, 0.36f, 0.66f));

            _animCardNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _roleButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(9, GUI.skin.button.fontSize - 1),
                padding = new RectOffset(2, 2, 1, 1),
                margin = new RectOffset(1, 1, 1, 1),
                alignment = TextAnchor.MiddleCenter
            };
            _roleButtonActiveStyle = new GUIStyle(_roleButtonStyle);
            ApplyFlatTint(_roleButtonActiveStyle, MakeTex(4, 4, new Color(0.22f, 0.48f, 0.98f, 0.92f)), Color.white);

            _reviewSectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            _treeNodeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 0, 0),
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            var treeSelBg = MakeTex(4, 4, new Color(0.22f, 0.48f, 0.98f, 0.88f));
            _treeNodeSelectedStyle = new GUIStyle(_treeNodeStyle);
            ApplyFlatTint(_treeNodeSelectedStyle, treeSelBg, Color.white);

            _listRowStyle = new GUIStyle(_treeNodeStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 0, 0)
            };
        }

        private void UpdateGridLayoutIfNeeded(float gridAvailW)
        {
            if (_gridLayoutFrame == Time.frameCount && Mathf.Approximately(_gridLayoutAvailW, gridAvailW))
                return;

            _gridLayoutFrame = Time.frameCount;
            _gridLayoutAvailW = gridAvailW;

            float vsb = VerticalScrollbarWidth();
            float contentWidth = Mathf.Max(80f, gridAvailW - vsb);
            float minCard = AnimBrowserScale.Px(MinCardSize);
            float maxCard = AnimBrowserScale.Px(MaxCardSize);
            float cellInnerW = Mathf.Clamp(
                _cardCellSize * AnimBrowserScale.Factor * GridCardSizeBoost,
                minCard,
                maxCard);

            int columns = Mathf.Max(1, Mathf.FloorToInt((contentWidth + GridCellGap) / (cellInnerW + GridCellGap)));
            float usedW = columns * cellInnerW + (columns - 1) * GridCellGap;
            if (usedW > contentWidth && columns > 1)
            {
                columns--;
                usedW = columns * cellInnerW + (columns - 1) * GridCellGap;
            }

            _gridLayoutColumns = columns;
            _gridLayoutCellInnerW = cellInnerW;
            _gridLayoutContentWidth = usedW;
            _gridLayoutCardOuterH = cellInnerW + CardNameRowH;
        }

        private static float VerticalScrollbarWidth()
        {
            float vsb = GUI.skin.verticalScrollbar != null ? GUI.skin.verticalScrollbar.fixedWidth : 15f;
            return vsb < 10f ? 18f : vsb;
        }

        private float TreeNodeLabelMaxWidth(int depth) =>
            Mathf.Max(40f, TreePanelWidth - 12f - VerticalScrollbarWidth() - depth * 16f - 24f);

        private string GetCachedTreeNodeLabel(AnimViewNode node, GUIStyle style, float maxWidth)
        {
            string label = node.GetDisplayLabel();
            if (node.CachedTruncatedName != null &&
                Mathf.Approximately(node.CachedTruncatedWidth, maxWidth) &&
                node.CachedTruncatedLabelSource == label)
            {
                return node.CachedTruncatedName;
            }

            string result = TruncateWithEllipsis(label, style, maxWidth);
            node.CachedTruncatedName = result;
            node.CachedTruncatedWidth = maxWidth;
            node.CachedTruncatedLabelSource = label;
            return result;
        }

        private string GetCachedTruncatedName(AnimGridItem item, GUIStyle style, float maxWidth)
        {
            string label = _displayCatalog.GetItemDisplayLabel(item);
            if (item.CachedTruncatedName != null &&
                Mathf.Approximately(item.CachedTruncatedNameWidth, maxWidth) &&
                item.CachedTruncatedNameSource == label)
            {
                return item.CachedTruncatedName;
            }

            string result = TruncateWithEllipsis(label, style, maxWidth);
            item.CachedTruncatedName = result;
            item.CachedTruncatedNameWidth = maxWidth;
            item.CachedTruncatedNameSource = label;
            return result;
        }

        private static string TruncateWithEllipsis(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 1f)
                return text ?? string.Empty;

            if (style.CalcSize(new GUIContent(text)).x <= maxWidth)
                return text;

            float ellipsisW = style.CalcSize(new GUIContent(LabelEllipsis)).x;
            float budget = maxWidth - ellipsisW;
            if (budget <= 1f)
                return LabelEllipsis;

            int lo = 0;
            int hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (style.CalcSize(new GUIContent(text.Substring(0, mid))).x <= budget)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return lo <= 0 ? LabelEllipsis : text.Substring(0, lo) + LabelEllipsis;
        }

        private static GUIStyle CreateCardChromeTemplate()
        {
            var box = GUI.skin.box;
            return new GUIStyle
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                clipping = box.clipping
            };
        }

        private static GUIStyle CardTintStyle(GUIStyle chromeTemplate, Color tint)
        {
            var s = new GUIStyle(chromeTemplate) { border = new RectOffset(0, 0, 0, 0) };
            Texture2D bg = MakeTex(4, 4, tint);
            ApplyFlatBackgroundAllStates(s, bg);
            return s;
        }

        private static void ApplyFlatBackgroundAllStates(GUIStyle s, Texture2D bg)
        {
            s.normal.background = bg;
            s.hover.background = bg;
            s.active.background = bg;
            s.focused.background = bg;
            s.onNormal.background = bg;
            s.onHover.background = bg;
            s.onActive.background = bg;
            s.onFocused.background = bg;
        }

        private static void ApplyFlatTint(GUIStyle s, Texture2D bg, Color textColor)
        {
            void Apply(GUIStyleState st)
            {
                st.background = bg;
                st.textColor = textColor;
            }

            Apply(s.normal);
            Apply(s.hover);
            Apply(s.active);
            Apply(s.focused);
            Apply(s.onNormal);
            Apply(s.onHover);
            Apply(s.onActive);
            Apply(s.onFocused);
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            tex.SetPixels(pix);
            tex.Apply(false, false);
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }
    }
}
