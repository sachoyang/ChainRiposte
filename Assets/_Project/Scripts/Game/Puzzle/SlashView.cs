using System.Collections;
using UnityEngine;

namespace ChainRiposte.Game.Puzzle
{
    /// <summary>
    /// 매치 한 줄을 <b>한 번에</b> 베는 검기. 타일마다 따로 번쩍이면 3개 매치에 세 번 터져
    /// 시각적 소음이 되므로, 매치의 단위(=라인)에 하나만 긋는다.
    ///
    /// <para>ㄱ/T자는 <see cref="BoardView"/>가 가로 줄과 세로 줄로 쪼개 <b>동시에</b> 두 번 긋는다 —
    /// 한 획으로 꺾어 그릴 수는 없고, 두 획이 겹쳐 터지는 편이 "한 수로 둘을 벴다"로 읽힌다.</para>
    ///
    /// <para>개수가 판마다 달라지므로 런타임 생성이 맞다(타일과 같은 규칙). 그림은
    /// <see cref="BoardView"/>의 슬롯으로 교체한다 — 비우면 <see cref="PlaceholderSprite.Slash"/>
    /// (가운데가 굵고 양 끝이 뾰족하게 모이는 눈 모양)를 쓴다. 사각형을 늘려 쓰면 끝이 일자로
    /// 뚝 끊겨 '막대'로 보인다.</para>
    /// </summary>
    public sealed class SlashView : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        /// <summary>
        /// 줄의 양 끝(월드 좌표)을 잇는 검기를 놓는다.
        /// 끝을 <paramref name="overshoot"/>만큼 넘겨 그어야 벤 자국이 타일에 갇히지 않고 지나간 것처럼 보인다.
        /// </summary>
        public static SlashView Create(
            Transform parent, Vector3 from, Vector3 to, Sprite sprite, Color color,
            float thickness, float overshoot, int sortingOrder)
        {
            var go = new GameObject("Slash");
            go.transform.SetParent(parent, false);

            var slash = go.AddComponent<SlashView>();
            slash._renderer = go.AddComponent<SpriteRenderer>();
            slash._renderer.sprite = sprite != null ? sprite : PlaceholderSprite.Slash;
            slash._renderer.color = color;
            slash._renderer.sortingOrder = sortingOrder;

            Vector3 delta = to - from;
            float length = delta.magnitude + overshoot * 2f;
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            // 스프라이트의 원래 크기를 1x1로 보고 늘린다 — 실제 아트를 꽂아도 같은 규칙이 적용된다.
            Vector2 unit = slash._renderer.sprite.bounds.size;
            slash.transform.localScale = new Vector3(
                unit.x > 0f ? length / unit.x : length,
                unit.y > 0f ? thickness / unit.y : thickness,
                1f);

            return slash;
        }

        /// <summary>긋고 사라진다 — 굵기가 확 벌어졌다가 얇아지며 지워진다(칼이 지나간 잔상).</summary>
        public IEnumerator Play(float duration)
        {
            Vector3 baseScale = transform.localScale;
            Color baseColor = _renderer.color;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;

                // 굵기: 처음 15%에 확 벌어졌다가 얇아진다
                float width = k < 0.15f ? Mathf.Lerp(0.25f, 1.4f, k / 0.15f) : Mathf.Lerp(1.4f, 0.1f, (k - 0.15f) / 0.85f);
                transform.localScale = new Vector3(baseScale.x, baseScale.y * width, baseScale.z);

                Color color = baseColor;
                color.a = baseColor.a * (1f - k * k); // 끝에서 급히 사라져야 잔상으로 읽힌다
                _renderer.color = color;

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
