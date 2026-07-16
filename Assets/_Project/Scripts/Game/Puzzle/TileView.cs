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
        private Color _baseColor;
        private int _maxHp;
        private int _remainingHp;

        public long TileId { get; private set; }

        /// <param name="sprite">타일 아트. null이면 플레이스홀더 사각형 + color 착색으로 표시.</param>
        public static TileView Create(Transform parent, Tile tile, Color color, Sprite sprite = null)
        {
            var go = new GameObject($"Tile_{tile.Definition.Id}_{tile.InstanceId}");
            go.transform.SetParent(parent, false);
            // 벽은 셀을 꽉 채워 '지형'으로 읽히게 하고, 움직이는 타일과 확실히 구분한다
            go.transform.localScale = Vector3.one * (tile.Category == TileCategory.Wall ? 1f : 0.9f);

            var view = go.AddComponent<TileView>();
            view.TileId = tile.InstanceId;
            view._renderer = go.AddComponent<SpriteRenderer>();
            view._renderer.sprite = sprite != null ? sprite : PlaceholderSprite.Square;
            view._baseColor = color;
            view._maxHp = tile.Definition.MaxHp;
            view._remainingHp = tile.RemainingHp;
            view.RefreshColor();
            return view;
        }

        /// <summary>내구도형 타일(벽)은 피해를 입을수록 어두워진다.</summary>
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

            _countdownText.text = $"{Mathf.CeilToInt(seconds)}s|{turns}t";
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
            _renderer.color = _maxHp > 0
                ? Color.Lerp(Color.black, _baseColor, 0.4f + 0.6f * _remainingHp / _maxHp)
                : _baseColor;
        }
    }
}
