using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 씬 빌더들이 공유하는 uGUI 생성 헬퍼. 런타임이 아니라 <b>에디터에서 실물 오브젝트</b>를 만들어
    /// 씬에 남긴다(이후 자유롭게 편집·에셋 교체). 텍스트는 전부 TextMeshPro.
    /// </summary>
    internal static class EditorUiFactory
    {
        internal static readonly Color DefaultTextColor = new(0.92f, 0.90f, 0.85f);

        internal static Sprite Square =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        internal static void SetupCanvas(GameObject go, int sortingOrder)
        {
            // UnityEngine.Object에는 ?? 를 쓰지 않는다(가짜 null 무시 → MissingComponentException).
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null)
                canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            if (go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();
            // 방향이 바뀌면 기준 해상도도 같이 바뀌어야 한다 (GDD §9.3)
            if (go.GetComponent<OrientationCanvas>() == null)
                go.AddComponent<OrientationCanvas>();
        }

        /// <summary>
        /// 방향별 배치를 붙인다 — <b>현재(세로) 배치를 세로 프리셋으로 저장</b>하고 가로 프리셋은 인자로 깐다.
        /// 생성 후에는 씬에서 드래그하고 인스펙터 버튼으로 다시 저장하면 된다 (GDD §9.3).
        /// </summary>
        internal static void Orient(
            Component target, Vector2 landscapeAnchorMin, Vector2 landscapeAnchorMax,
            Vector2 landscapePivot, Vector2 landscapePosition, Vector2 landscapeSize)
        {
            var layout = Undo.AddComponent<OrientationLayout>(target.gameObject);
            layout.CaptureBothEditorOnly(); // 세로 = 지금 만든 배치
            layout.SetLandscapeEditorOnly(
                landscapeAnchorMin, landscapeAnchorMax, landscapePivot, landscapePosition, landscapeSize);
        }

        /// <summary>앵커가 상하좌우로 늘어나지 않는 일반 요소용 축약형.</summary>
        internal static void Orient(
            Component target, Vector2 landscapeAnchor, Vector2 landscapePivot,
            Vector2 landscapePosition, Vector2 landscapeSize) =>
            Orient(target, landscapeAnchor, landscapeAnchor, landscapePivot, landscapePosition, landscapeSize);

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image Stretch(Transform parent, string name, Color color, bool raycast)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Square;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static TextMeshProUGUI Text(
            Transform parent, string name, Vector2 pos, Vector2 anchor, float size,
            TextAlignmentOptions align, Vector2 sizeDelta, FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.color = DefaultTextColor;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>배경 + Filled 채움 이미지 게이지. 반환값은 fillAmount로 갱신할 채움 이미지.</summary>
        internal static Image Bar(Transform parent, string name, Vector2 pos, Vector2 anchor, Vector2 size, Color fillColor)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = Square;
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);
            bg.raycastTarget = false;

            RectTransform fillRect = NewRect("Fill", rect);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = Square;
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.raycastTarget = false;
            return fill;
        }

        internal static Button Button(
            Transform parent, string name, Vector2 pos, Vector2 anchor, Vector2 pivot, Vector2 size,
            Color imageColor, string labelText, float labelSize, out Image image, out TextMeshProUGUI label)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            image = rect.gameObject.AddComponent<Image>();
            image.sprite = Square;
            image.color = imageColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            RectTransform labelRect = NewRect("Label", rect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = labelSize;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = DefaultTextColor;
            label.text = labelText;
            label.raycastTarget = false;
            return button;
        }
    }
}
