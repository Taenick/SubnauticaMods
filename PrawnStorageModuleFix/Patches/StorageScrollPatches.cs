using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PrawnStorageModuleFix.Patches;

[HarmonyPatch(typeof(uGUI_ItemsContainer), "OnResize")]
internal static class StorageScrollPatch
{
    private const int scrollThreshold = 8;
    private const int maxVisibleRows = 8;
    private const int cellSize = 71;
    internal const string viewportName = "PrawnStorageViewport";
    internal const string scrollbarName = "PrawnStorageScrollbar";
    private const float scrollbarWidth = 15f;
    private const float scrollbarPadding = 4f;
    private const float scrollCorrectionY = 20f;

    private static float savedApY = float.NaN;

    // Original content transform values to restore on Uninit
    internal static Vector2 originalAnchoredPosition;
    internal static Vector2 originalAnchorMin;
    internal static Vector2 originalAnchorMax;
    internal static Vector2 originalPivot;

    [HarmonyPostfix]
    static void Postfix(uGUI_ItemsContainer __instance, int width, int height)
    {
        if (width < 8)
            return;

        var contentRt = __instance.rectTransform;
        bool isInViewport = contentRt.parent != null && contentRt.parent.name == viewportName;

        // Only allow Exosuit storage (pivot 0.5, active UI) or content already in our viewport
        if (!isInViewport && contentRt.pivot.y != 0.5f)
            return;
        if (!isInViewport && !__instance.gameObject.activeInHierarchy)
            return;

        float fullWidth = width * cellSize;
        float fullHeight = height * cellSize;
        bool needsScroll = height > scrollThreshold;
        bool hasViewport = isInViewport;

        // Save the stable ap.y from non-scroll sizes
        if (!hasViewport && !needsScroll)
            savedApY = contentRt.anchoredPosition.y;

        // Create viewport if needed
        if (!hasViewport)
        {
            float viewportY;
            float leftX = contentRt.anchoredPosition.x - contentRt.pivot.x * fullWidth;
            if (!float.IsNaN(savedApY))
            {
                viewportY = savedApY + 0.5f * scrollThreshold * cellSize;
            }
            else
            {
                viewportY = 280f;
            }

            // Save original transform so Uninit can restore it for other storages
            originalAnchoredPosition = contentRt.anchoredPosition;
            originalAnchorMin = contentRt.anchorMin;
            originalAnchorMax = contentRt.anchorMax;
            originalPivot = contentRt.pivot;

            var originalParent = contentRt.parent;
            int siblingIndex = contentRt.GetSiblingIndex();

            var viewportGo = new GameObject(viewportName);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportGo.AddComponent<RectMask2D>();

            viewportRt.SetParent(originalParent, false);
            viewportRt.SetSiblingIndex(siblingIndex);
            viewportRt.anchorMin = contentRt.anchorMin;
            viewportRt.anchorMax = contentRt.anchorMax;
            viewportRt.pivot = new Vector2(0f, 1f);
            viewportRt.anchoredPosition = new Vector2(leftX - scrollbarWidth - scrollbarPadding, viewportY);
            viewportRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth);
            viewportRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxVisibleRows * cellSize);

            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 1f);
            contentRt.anchoredPosition = Vector2.zero;

            var scrollbar = CreateScrollbar(originalParent, viewportRt);

            var scrollRect = viewportGo.AddComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = cellSize;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        }

        // Update viewport for current state (scroll or passthrough)
        var viewportRt2 = contentRt.parent.GetComponent<RectTransform>();
        if (needsScroll)
        {
            float visibleHeight = maxVisibleRows * cellSize;
            viewportRt2.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth);
            viewportRt2.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, visibleHeight);

            var sr = viewportRt2.GetComponent<ScrollRect>();
            if (sr != null) sr.enabled = true;

            var rm = viewportRt2.GetComponent<RectMask2D>();
            if (rm != null) rm.enabled = true;

            var sb = viewportRt2.parent?.Find(scrollbarName);
            if (sb != null)
            {
                sb.gameObject.SetActive(true);
                UpdateScrollbarPosition(sb.GetComponent<RectTransform>(), viewportRt2);
            }
        }
        else
        {
            viewportRt2.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullWidth);
            viewportRt2.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fullHeight);

            var sr = viewportRt2.GetComponent<ScrollRect>();
            if (sr != null) sr.enabled = false;

            var rm = viewportRt2.GetComponent<RectMask2D>();
            if (rm != null) rm.enabled = false;

            var sb = viewportRt2.parent?.Find(scrollbarName);
            if (sb != null) sb.gameObject.SetActive(false);

            contentRt.anchoredPosition = Vector2.zero;
        }
    }

    static Scrollbar CreateScrollbar(Transform parent, RectTransform viewportRt)
    {
        var scrollbarGo = new GameObject(scrollbarName);
        var scrollbarRt = scrollbarGo.AddComponent<RectTransform>();
        scrollbarRt.SetParent(parent, false);

        UpdateScrollbarPosition(scrollbarRt, viewportRt);

        var trackImage = scrollbarGo.AddComponent<Image>();
        trackImage.color = new Color(0f, 0.5f, 0.8f, 0.15f);

        var handleGo = new GameObject("Handle");
        var handleRt = handleGo.AddComponent<RectTransform>();
        handleRt.SetParent(scrollbarRt, false);
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.one;
        handleRt.offsetMin = new Vector2(2f, 2f);
        handleRt.offsetMax = new Vector2(-2f, -2f);

        var handleImage = handleGo.AddComponent<Image>();
        handleImage.color = new Color(0f, 0.7f, 1f, 0.6f);

        var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var colors = scrollbar.colors;
        colors.normalColor = new Color(0f, 0.7f, 1f, 0.6f);
        colors.highlightedColor = new Color(0f, 0.8f, 1f, 0.8f);
        colors.pressedColor = new Color(0f, 0.9f, 1f, 1f);
        scrollbar.colors = colors;

        return scrollbar;
    }

    static void UpdateScrollbarPosition(RectTransform scrollbarRt, RectTransform viewportRt)
    {
        scrollbarRt.anchorMin = viewportRt.anchorMin;
        scrollbarRt.anchorMax = viewportRt.anchorMax;
        scrollbarRt.pivot = new Vector2(0f, 1f);

        float viewportRight = viewportRt.anchoredPosition.x + viewportRt.rect.width;
        scrollbarRt.anchoredPosition = new Vector2(viewportRight + 2f, viewportRt.anchoredPosition.y);
        scrollbarRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, scrollbarWidth);
        scrollbarRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportRt.rect.height);
    }
}

[HarmonyPatch(typeof(uGUI_ItemsContainer), "Uninit")]
internal static class StorageUninitPatch
{
    [HarmonyPrefix]
    static void Prefix(uGUI_ItemsContainer __instance)
    {
        var contentRt = __instance.rectTransform;
        if (contentRt.parent == null || contentRt.parent.name != StorageScrollPatch.viewportName)
            return;

        var viewport = contentRt.parent;
        var originalParent = viewport.parent;
        int siblingIndex = viewport.GetSiblingIndex();

        contentRt.SetParent(originalParent, false);
        contentRt.SetSiblingIndex(siblingIndex);

        contentRt.anchorMin = StorageScrollPatch.originalAnchorMin;
        contentRt.anchorMax = StorageScrollPatch.originalAnchorMax;
        contentRt.pivot = StorageScrollPatch.originalPivot;
        contentRt.anchoredPosition = StorageScrollPatch.originalAnchoredPosition;

        Object.Destroy(viewport.gameObject);
        var scrollbar = originalParent.Find(StorageScrollPatch.scrollbarName);
        if (scrollbar != null)
            Object.Destroy(scrollbar.gameObject);

    }
}

[HarmonyPatch(typeof(uGUI_ItemsContainer), "Init")]
internal static class StorageInitRetriggerPatch
{
    [HarmonyPostfix]
    static void Postfix(uGUI_ItemsContainer __instance, ItemsContainer container)
    {
        // Init calls OnResize before SetActive(true), so our scroll patch skips it (inactive).
        // Re-trigger OnResize now that SetActive has run, giving the scroll patch a chance.
        if (container != null && container.sizeX >= 8)
        {
            var method = HarmonyLib.AccessTools.Method(typeof(uGUI_ItemsContainer), "OnResize");
            method?.Invoke(__instance, new object[] { container.sizeX, container.sizeY });
        }
    }
}
