using System.Collections;
using ChainRiposte.Core.Board;
using UnityEngine;

namespace ChainRiposte.Game.Puzzle
{
    /// <summary>타일 하나의 시각 표현. 로직 상태는 갖지 않으며 BoardView가 재생하는 연출만 수행한다.</summary>
    public sealed class TileView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private TextMesh _countdownText;
        private SpriteRenderer _chainOverlay;
        private Color _baseColor;
        private Sprite[] _wallStages;
        private int _maxHp;
        private int _remainingHp;
        private bool _enraged;
        private Color _enrageTint = Color.white;
        // 폭탄 숫자가 카운트 자리를 쓰고 있는가 — 성남 표시가 그것을 덮지 않게 한다.
        private bool _hasBombText;

        public long TileId { get; private set; }

        /// <summary>
        /// 타일 하나를 그리는 데 필요한 그림 정보. 파라미터가 늘어날 때마다 호출부를 고치지 않으려고 묶었다.
        /// </summary>
        public struct Visual
        {
            /// <summary>타일 아트. null이면 플레이스홀더 사각형 + <see cref="Color"/> 착색.</summary>
            public Sprite Sprite;
            public Color Color;

            /// <summary>아이콘 뒤에 깔리는 받침. null이거나 <see cref="BackgroundColor"/> 알파가 0이면 안 그린다.</summary>
            public Sprite Background;
            public Color BackgroundColor;

            /// <summary>아이콘이 차지할 <b>월드 크기</b>(셀 = 1). 그림의 픽셀 크기·PPU와 무관하게 여기에 맞춘다.</summary>
            public float IconSize;

            /// <summary>받침이 차지할 월드 크기. 아이콘보다 살짝 커야 받침으로 읽힌다.</summary>
            public float BackgroundSize;
        }

        public static TileView Create(Transform parent, Tile tile, Visual visual)
        {
            var go = new GameObject($"Tile_{tile.Definition.Id}_{tile.InstanceId}");
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<TileView>();
            view.TileId = tile.InstanceId;

            Sprite sprite = visual.Sprite != null ? visual.Sprite : PlaceholderSprite.Square;

            // 그림마다 픽셀 크기와 PPU가 다르다 — 그대로 두면 어떤 타일은 셀을 채우고 어떤 타일은 점만 하다.
            // 스케일을 그림에서 역산해 항상 같은 크기로 맞춘다(임포트 설정에 기대지 않는다).
            float iconScale = ScaleToFit(sprite, visual.IconSize);
            go.transform.localScale = Vector3.one * iconScale;

            view.CreateBackground(visual, iconScale);

            view._renderer = go.AddComponent<SpriteRenderer>();
            view._renderer.sprite = sprite;
            view._baseColor = visual.Color;
            view._maxHp = tile.Definition.MaxHp;
            view._remainingHp = tile.RemainingHp;
            view.RefreshColor();
            return view;
        }

        /// <summary>
        /// 받침은 아이콘보다 <b>뒤에</b>(sortingOrder -1) 깔리고 타일과 함께 움직인다.
        /// 배경 셀(고정, -10)과 달리 낙하·스왑을 따라가야 아이콘과 어긋나지 않는다.
        /// </summary>
        private void CreateBackground(Visual visual, float parentScale)
        {
            if (visual.Background == null || visual.BackgroundColor.a <= 0f)
                return;

            var go = new GameObject("Background");
            go.transform.SetParent(transform, false);

            // 받침은 아이콘이 아니라 <b>셀</b>에 맞춰야 한다. 자식이라 부모 스케일을 물려받으므로
            // 그만큼 나눠 준다 — 안 그러면 작게 그려진 아이콘을 따라 받침까지 쪼그라든다.
            float scale = ScaleToFit(visual.Background, visual.BackgroundSize);
            go.transform.localScale = Vector3.one * (parentScale > 0f ? scale / parentScale : scale);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = visual.Background;
            renderer.color = visual.BackgroundColor;
            renderer.sortingOrder = -1;
        }

        /// <summary>그림이 <paramref name="target"/> 월드 크기 안에 꽉 들어가게 하는 스케일 (비율 유지).</summary>
        private static float ScaleToFit(Sprite sprite, float target)
        {
            if (target <= 0f)
                target = 1f;
            if (sprite == null)
                return target;

            Vector2 size = sprite.bounds.size;
            float longest = Mathf.Max(size.x, size.y);
            return longest > 0f ? target / longest : target;
        }

        /// <summary>
        /// 벽의 손상 단계 스프라이트 (0 = 온전 … 마지막 = 거의 부서짐).
        /// 지정하면 어두워지는 대신 실제로 금이 간 그림으로 바뀐다.
        /// </summary>
        public void SetWallStages(Sprite[] stages)
        {
            _wallStages = stages != null && stages.Length > 0 ? stages : null;
            RefreshColor();
        }

        /// <summary>내구도형 타일(벽)은 피해를 입을수록 어두워지거나, 손상 단계 그림으로 바뀐다.</summary>
        public void ApplyWallDamage(int damage)
        {
            _remainingHp = Mathf.Max(0, _remainingHp - damage);
            RefreshColor();
        }

        /// <summary>기믹 상태(사슬/폭탄/성남)를 타일에 반영한다 (GDD §3.6).</summary>
        public void ApplyStatus(Tile tile, Sprite chainSprite, Color enrageTint)
        {
            SetChained(tile.Status.Chained, chainSprite);
            SetBombTurns(tile.Status.BombTurnsRemaining);
            SetEnrageCountdown(tile.Status.EnrageCountdown, enrageTint);
        }

        /// <summary>시한폭탄 남은 턴 표시. 0 이하면 표시를 지운다 (해체/폭발).</summary>
        public void SetBombTurns(int turns)
        {
            _hasBombText = turns > 0;
            if (turns <= 0)
            {
                if (_countdownText != null)
                    _countdownText.text = string.Empty;
                return;
            }

            if (_countdownText == null)
                _countdownText = CreateCountdownText();

            _countdownText.color = new Color(1f, 0.45f, 0.35f);
            _countdownText.text = turns.ToString();
        }

        /// <summary>
        /// 성난 몬스터 표시 — 공격까지 남은 턴 + 몸통 틴트. 0 이하면 평범한 타일로 되돌린다.
        /// <b>틴트까지 거는 이유</b>: 숫자만으로는 보드를 훑을 때 안 읽힌다. 색이 있어야
        /// "저놈부터 없앤다"는 판단이 한눈에 선다.
        /// </summary>
        public void SetEnrageCountdown(int turns, Color enrageTint)
        {
            bool enraged = turns > 0;
            if (_enraged != enraged)
            {
                _enraged = enraged;
                _enrageTint = enrageTint;
                RefreshColor();
            }

            if (!enraged)
            {
                if (_countdownText != null && !_hasBombText)
                    _countdownText.text = string.Empty;
                return;
            }

            if (_countdownText == null)
                _countdownText = CreateCountdownText();

            // 폭탄 숫자가 이미 자리를 쓰고 있으면 그쪽을 덮지 않는다 — 둘 다 걸린 타일은 폭탄이 더 급하다.
            if (_hasBombText)
                return;

            _countdownText.color = enrageTint;
            _countdownText.text = turns.ToString();
        }

        /// <summary>성난 몬스터가 때린 순간 한 번 튄다 — HP가 왜 깎였는지가 보드에서 읽혀야 한다.</summary>
        public IEnumerator PunchOnce()
        {
            Vector3 baseScale = transform.localScale;
            const float duration = 0.18f;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f + 0.25f * Mathf.Sin(t / duration * Mathf.PI);
                transform.localScale = baseScale * k;
                yield return null;
            }

            transform.localScale = baseScale;
        }

        /// <summary>사슬 결박 표시 — 스프라이트를 주면 그걸 쓰고, 없으면 어두운 띠로 대체한다.</summary>
        public void SetChained(bool chained, Sprite chainSprite)
        {
            if (!chained)
            {
                if (_chainOverlay != null)
                    _chainOverlay.gameObject.SetActive(false);
                return;
            }

            if (_chainOverlay == null)
                _chainOverlay = CreateChainOverlay(chainSprite);
            _chainOverlay.gameObject.SetActive(true);
        }

        private SpriteRenderer CreateChainOverlay(Sprite chainSprite)
        {
            var go = new GameObject("Chain");
            go.transform.SetParent(transform, false);
            go.transform.localScale = chainSprite != null
                ? Vector3.one
                : new Vector3(1.15f, 0.3f, 1f); // 플레이스홀더: 가운데를 가로지르는 띠

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = chainSprite != null ? chainSprite : PlaceholderSprite.Square;
            renderer.color = chainSprite != null ? Color.white : new Color(0.62f, 0.60f, 0.58f);
            renderer.sortingOrder = 5;
            return renderer;
        }

        private TextMesh CreateCountdownText()
        {
            var go = new GameObject("Countdown");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.characterSize = 0.09f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;

            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = text.font.material;
            meshRenderer.sortingOrder = 10;
            return text;
        }

        public IEnumerator MoveTo(Vector3 localTarget, float duration)
        {
            Vector3 start = transform.localPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.localPosition = Vector3.Lerp(start, localTarget, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }

            transform.localPosition = localTarget;
        }

        public IEnumerator ClearAndDestroy(float duration)
        {
            Vector3 start = transform.localScale;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                transform.localScale = Vector3.Lerp(start, Vector3.zero, t / duration);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void RefreshColor()
        {
            // 손상 단계 그림이 있으면 그걸로 상태를 보여 준다 — 색을 어둡게 하는 건 그림이 없을 때의 대체 표현이다
            if (_wallStages != null && _maxHp > 0)
            {
                float lost = 1f - (float)_remainingHp / _maxHp;
                int index = Mathf.Clamp(Mathf.RoundToInt(lost * (_wallStages.Length - 1)), 0, _wallStages.Length - 1);
                _renderer.sprite = _wallStages[index];
                _renderer.color = Color.white;
                return;
            }

            Color color = _maxHp > 0
                ? Color.Lerp(Color.black, _baseColor, 0.4f + 0.6f * _remainingHp / _maxHp)
                : _baseColor;

            // 성난 놈은 원래 색과 섞어 물들인다 — 통째로 갈아치우면 무슨 몬스터인지 못 알아본다.
            _renderer.color = _enraged ? Color.Lerp(color, _enrageTint, 0.6f) : color;
        }
    }
}
