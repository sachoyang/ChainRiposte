using System.Collections;
using UnityEngine;

namespace ChainRiposte.Game.Puzzle
{
    /// <summary>
    /// 폭탄이 터진 자리에 한 번 피는 폭발 (<see cref="SlashView"/>와 같은 규칙 — 스스로 끝나고 스스로 사라진다).
    ///
    /// <para><b>왜 필요한가</b>: 폭탄이 터졌는지 <b>해체됐는지 화면에서 구분이 안 됐다.</b> 둘 다 타일이
    /// 조용히 줄어들며 사라졌기 때문이다. 터지는 것은 손해이고 해체는 이득인데 같은 그림이면
    /// 플레이어가 자기가 잘한 건지 못한 건지 모른다.</para>
    ///
    /// <para>연출은 <b>가운데서 빠르게 부풀었다가 사그라든다.</b> 커지는 구간을 짧게 잡아야 「펑」으로
    /// 읽힌다 — 커지는 데 시간을 쓰면 폭발이 아니라 풍선이 된다. 셀보다 <b>조금</b> 크게 넘치는 것이
    /// 핵심이라, 크기는 셀 기준으로 정하고 그림에서 역산한다(타일과 같은 규칙).</para>
    /// </summary>
    public sealed class ExplosionView : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        /// <param name="cellSize">셀 한 칸의 월드 크기. 폭발은 이 값의 배수로 커진다.</param>
        public static ExplosionView Create(
            Transform parent, Vector3 localPosition, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject("Explosion");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.zero;

            var view = go.AddComponent<ExplosionView>();
            view._renderer = go.AddComponent<SpriteRenderer>();
            view._renderer.sprite = sprite;
            view._renderer.color = color;
            view._renderer.sortingOrder = sortingOrder;
            return view;
        }

        /// <param name="targetScale">가장 크게 부풀었을 때의 스케일(그림에서 역산한 값).</param>
        /// <param name="growRatio">전체 시간 중 <b>커지는 데</b> 쓰는 비율. 작을수록 「펑」에 가깝다.</param>
        public IEnumerator Play(float duration, float targetScale, float growRatio)
        {
            float grow = Mathf.Clamp01(growRatio);
            Color baseColor = _renderer.color;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;

                // 커지는 구간은 감속(터지는 순간이 가장 빠르다), 그 뒤로는 살짝 더 부풀며 옅어진다.
                float scale = u < grow
                    ? targetScale * Mathf.Sqrt(u / Mathf.Max(Mathf.Epsilon, grow))
                    : targetScale * Mathf.Lerp(1f, 1.12f, (u - grow) / Mathf.Max(Mathf.Epsilon, 1f - grow));

                transform.localScale = Vector3.one * scale;

                // 알파는 커지고 나서부터만 뺀다 — 부푸는 동안 옅어지면 터진 게 안 보인다.
                float alpha = u < grow ? 1f : 1f - (u - grow) / Mathf.Max(Mathf.Epsilon, 1f - grow);
                _renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
