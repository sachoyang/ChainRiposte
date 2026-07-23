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

            /// <summary>받침 크기 (1 = 아이콘과 같은 크기). 살짝 커야 받침으로 읽힌다.</summary>
            public float BackgroundScale;
        }

        public static TileView Create(Transform parent, Tile tile, Visual visual)
        {
            var go = new GameObject($"Tile_{tile.Definition.Id}_{tile.InstanceId}");
            go.transform.SetParent(parent, false);
            // 벽은 셀을 꽉 채워 '지형'으로 읽히게 하고, 움직이는 타일과 확실히 구분한다
            go.transform.localScale = Vector3.one * (tile.Category == TileCategory.Wall ? 1f : 0.9f);

            var view = go.AddComponent<TileView>();
            view.TileId = tile.InstanceId;
            view.CreateBackground(visual);

            view._renderer = go.AddComponent<SpriteRenderer>();
            view._renderer.sprite = visual.Sprite != null ? visual.Sprite : PlaceholderSprite.Square;
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
        private void CreateBackground(Visual visual)
        {
            if (visual.Background == null || visual.BackgroundColor.a <= 0f)
                return;

            var go = new GameObject("Background");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (visual.BackgroundScale > 0f ? visual.BackgroundScale : 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = visual.Background;
            renderer.color = visual.BackgroundColor;
            renderer.sortingOrder = -1;
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

        /// <summary>보스 타일의 듀얼 카운트다운 표시 ("남은초|남은턴").</summary>
        public void SetCountdown(float seconds, int turns)
        {
            if (_countdownText == null)
                _countdownText = CreateCountdownText();

            _countdownText.text = Localization.Loc.GetText("puzzle.countdown", Mathf.CeilToInt(seconds), turns);
        }

        /// <summary>기믹 상태(사슬/폭탄)를 타일에 반영한다 (GDD §3.6).</summary>
        public void ApplyStatus(Tile tile, Sprite chainSprite)
        {
            SetChained(tile.Status.Chained, chainSprite);
            SetBombTurns(tile.Status.BombTurnsRemaining);
        }

        /// <summary>시한폭탄 남은 턴 표시. 0 이하면 표시를 지운다 (해체/폭발).</summary>
        public void SetBombTurns(int turns)
        {
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

            _renderer.color = _maxHp > 0
                ? Color.Lerp(Color.black, _baseColor, 0.4f + 0.6f * _remainingHp / _maxHp)
                : _baseColor;
        }
    }
}
