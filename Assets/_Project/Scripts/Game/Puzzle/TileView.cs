using System.Collections;
using System.Collections.Generic;
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
        private SpriteRenderer _bombBadge;
        private SpriteRenderer _enrageBadge;
        // 뱃지 그림 한 벌. 낙하·매치 중에 상태만 갱신되는 경로(SetBombTurns 등)에서도 쓰려고 들고 있는다.
        private StatusArt _art;
        private Color _baseColor;
        private Sprite[] _wallStages;
        private int _maxHp;
        private int _remainingHp;
        private bool _enraged;
        private Color _enrageTint = Color.white;
        // 폭탄 숫자가 카운트 자리를 쓰고 있는가 — 성남 표시가 그것을 덮지 않게 한다.
        private bool _hasBombText;

        // 이번 성남이 시작될 때의 숫자. 커지는 정도를 여기에 견주므로, 몇 박짜리 위협이든
        // "마지막 한 칸"에서 가장 크게 보인다 (박 수를 스테이지마다 바꿔도 연출이 안 깨진다).
        private int _enrageStart;
        private int _enrageShown;
        private Vector3 _countBaseScale = Vector3.one;
        private Coroutine _countPopRoutine;

        public long TileId { get; private set; }

        /// <summary>
        /// 성난 몬스터 뱃지의 겉모습. 값은 <see cref="BoardView"/> 인스펙터가 정하고
        /// 여기서는 그대로 쓰기만 한다 — 튜닝 다이얼이 코드에 흩어지지 않게.
        /// </summary>
        public readonly struct EnrageStyle
        {
            public readonly Color Tint;

            /// <summary>카운트가 1까지 줄었을 때의 숫자 크기 배율. 1이면 안 커진다.</summary>
            public readonly float MaxCountScale;

            /// <summary>숫자가 한 칸 줄 때 순간적으로 튀는 배율.</summary>
            public readonly float PopScale;

            public EnrageStyle(Color tint, float maxCountScale, float popScale)
            {
                Tint = tint;
                MaxCountScale = Mathf.Max(1f, maxCountScale);
                PopScale = Mathf.Max(1f, popScale);
            }
        }

        /// <summary>
        /// 기믹 상태를 알리는 그림 한 벌 (사슬 · 폭탄 · 성남). 값은 <see cref="BoardView"/> 인스펙터가 정한다.
        ///
        /// <para>묶어 두는 이유는 <see cref="Visual"/>과 같다 — 기믹이 늘 때마다 호출부의 인자를 고치지 않기 위해서다.
        /// 비어 있는 칸은 <b>그리지 않는다</b>: 폭탄·성남은 숫자와 틴트만으로도 읽히므로,
        /// 그림이 없다고 표시가 사라지면 안 된다.</para>
        /// </summary>
        public readonly struct StatusArt
        {
            /// <summary>타일 위를 덮는 사슬. 밑그림이 비쳐야 하므로 가운데가 뚫린 그림을 쓴다.</summary>
            public readonly Sprite Chain;

            /// <summary>시한폭탄 뱃지 — 오른쪽 위 모서리.</summary>
            public readonly Sprite Bomb;

            /// <summary>성난 몬스터 뱃지 — 왼쪽 위 모서리(폭탄과 겹치지 않게 반대쪽).</summary>
            public readonly Sprite Enrage;

            /// <summary>뱃지가 차지할 월드 크기(셀 = 1).</summary>
            public readonly float BadgeSize;

            /// <summary>사슬이 차지할 월드 크기. 그림의 픽셀 크기와 무관하게 여기에 맞춘다.</summary>
            public readonly float ChainSize;

            public StatusArt(Sprite chain, Sprite bomb, Sprite enrage, float badgeSize, float chainSize)
            {
                Chain = chain;
                Bomb = bomb;
                Enrage = enrage;
                BadgeSize = badgeSize > 0f ? badgeSize : 0.42f;
                ChainSize = chainSize > 0f ? chainSize : 1f;
            }
        }

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

            // 뿌리는 <b>셀 단위</b>로 둔다(스케일 1 = 한 칸). 그림마다 픽셀 크기·PPU가 달라서 크기를
            // 맞추려면 스케일을 역산해야 하는데, 그걸 뿌리에 걸면 <b>모든 자식이 그 배율을 물려받는다</b> —
            // 뱃지·사슬·숫자가 몬스터 종류마다 다른 크기로 나오던 원인이 이것이었다(받침만 나눠서 피해 있었다).
            // 이제 배율은 그림을 든 Icon 자식만 진다. 자식은 전부 셀 기준이라 나눗셈이 필요 없다.
            view.CreateBackground(visual);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            iconGo.transform.localScale = Vector3.one * ScaleToFit(sprite, visual.IconSize);

            view._renderer = iconGo.AddComponent<SpriteRenderer>();
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
        private void CreateBackground(Visual visual)
        {
            if (visual.Background == null || visual.BackgroundColor.a <= 0f)
                return;

            var go = new GameObject("Background");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * ScaleToFit(visual.Background, visual.BackgroundSize);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = visual.Background;
            renderer.color = visual.BackgroundColor;
            renderer.sortingOrder = -1;
        }

        /// <summary>
        /// 그림이 <paramref name="target"/> 월드 크기 안에 꽉 들어가게 하는 스케일 (비율 유지).
        /// <b>뿌리가 셀 단위(스케일 1)</b>이므로 여기서 나온 값이 곧 그 자식의 월드 크기다 —
        /// 부모 배율로 나눌 필요가 없다.
        /// </summary>
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
        public void ApplyStatus(Tile tile, StatusArt art, EnrageStyle enrage)
        {
            _art = art;
            SetChained(tile.Status.Chained, art.Chain);
            SetBombTurns(tile.Status.BombTurnsRemaining);
            SetEnrageCountdown(tile.Status.EnrageCountdown, enrage);
        }

        /// <summary>시한폭탄 남은 턴 표시. 0 이하면 표시를 지운다 (해체/폭발).</summary>
        public void SetBombTurns(int turns)
        {
            _hasBombText = turns > 0;
            ShowBadge(ref _bombBadge, "BombBadge", _art.Bomb, turns > 0, new Vector2(1f, 1f));
            if (turns <= 0)
            {
                if (_countdownText != null)
                    _countdownText.text = string.Empty;
                return;
            }

            if (_countdownText == null)
                _countdownText = CreateCountdownText();

            // 성남이 키워 놓은 크기를 물려받지 않는다 — 폭탄 숫자는 일정한 크기로 읽혀야 한다.
            _countBaseScale = Vector3.one;
            _countdownText.transform.localScale = _countBaseScale;
            _countdownText.color = new Color(1f, 0.45f, 0.35f);
            _countdownText.text = turns.ToString();
        }

        /// <summary>
        /// 성난 몬스터 표시 — 공격까지 남은 턴 + 몸통 틴트. 0 이하면 평범한 타일로 되돌린다.
        /// <b>틴트까지 거는 이유</b>: 숫자만으로는 보드를 훑을 때 안 읽힌다. 색이 있어야
        /// "저놈부터 없앤다"는 판단이 한눈에 선다.
        /// </summary>
        public void SetEnrageCountdown(int count, EnrageStyle style)
        {
            ShowBadge(ref _enrageBadge, "EnrageBadge", _art.Enrage, count > 0, new Vector2(-1f, 1f));

            bool enraged = count > 0;
            if (_enraged != enraged)
            {
                _enraged = enraged;
                _enrageTint = style.Tint;
                RefreshColor();
            }

            if (!enraged)
            {
                _enrageStart = 0;
                _enrageShown = 0;
                if (_countdownText != null && !_hasBombText)
                {
                    _countdownText.text = string.Empty;
                    _countBaseScale = Vector3.one;
                    _countdownText.transform.localScale = _countBaseScale;
                }
                return;
            }

            if (_countdownText == null)
                _countdownText = CreateCountdownText();

            // 폭탄 숫자가 이미 자리를 쓰고 있으면 그쪽을 덮지 않는다 — 둘 다 걸린 타일은 폭탄이 더 급하다.
            if (_hasBombText)
                return;

            // 새로 성났거나 때린 뒤 재장전됐다 — 이번 사이클의 기준 숫자를 다시 잡는다.
            if (count > _enrageStart)
                _enrageStart = count;

            bool ticked = _enrageShown > 0 && count < _enrageShown;
            _enrageShown = count;

            _countdownText.color = style.Tint;
            _countdownText.text = count.ToString();

            // 숫자가 줄수록 커진다 — 3·2·1이 같은 크기면 "급해지고 있다"가 안 읽힌다.
            float urgency = _enrageStart > 1 ? Mathf.InverseLerp(_enrageStart, 1f, count) : 1f;
            _countBaseScale = Vector3.one * Mathf.Lerp(1f, style.MaxCountScale, urgency);
            _countdownText.transform.localScale = _countBaseScale;

            // 한 칸 줄어든 <b>그 순간</b>에만 튄다. 보드를 다시 그릴 때(스왑·낙하)도 이 함수가 불리는데
            // 그때마다 튀면 아무 일도 없는데 숫자가 계속 들썩인다.
            if (ticked)
                RestartCountPop(style.PopScale);
        }

        private void RestartCountPop(float popScale)
        {
            if (popScale <= 1f)
                return;

            if (_countPopRoutine != null)
                StopCoroutine(_countPopRoutine);
            _countPopRoutine = StartCoroutine(CountPop(popScale));
        }

        private IEnumerator CountPop(float popScale)
        {
            const float Duration = 0.18f;
            Transform text = _countdownText.transform;

            for (float t = 0f; t < Duration; t += Time.deltaTime)
            {
                // 커진 채로 시작해 제 크기로 돌아온다 — 사라지는 쪽이 '줄었다'로 읽힌다
                text.localScale = _countBaseScale * Mathf.Lerp(popScale, 1f, t / Duration);
                yield return null;
            }

            text.localScale = _countBaseScale;
            _countPopRoutine = null;
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

        /// <summary>
        /// 때리는 순간 <b>체력 바 쪽으로 달려들었다가 제자리로</b> 돌아온다 —
        /// 어느 놈이 때렸고 무엇이 깎였는지가 한 동작으로 이어져 읽힌다.
        ///
        /// <para>나갈 때는 짧고 빠르게, 돌아올 때는 길고 느리게. 같은 속도로 오가면
        /// 달려든 것이 아니라 그냥 흔들린 것으로 보인다.</para>
        /// </summary>
        public IEnumerator LungeToward(Vector3 worldTarget, float distance, float seconds)
        {
            if (distance <= 0f)
                yield break;

            Vector3 home = transform.localPosition;
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformPoint(worldTarget)
                : worldTarget;

            Vector3 direction = local - home;
            direction.z = 0f;
            if (direction.sqrMagnitude < 1e-6f)
                yield break;

            Vector3 target = home + direction.normalized * distance;
            float out_ = Mathf.Max(0.02f, seconds * 0.32f);
            float back = Mathf.Max(0.02f, seconds - out_);

            for (float t = 0f; t < out_; t += Time.deltaTime)
            {
                float k = t / out_;
                transform.localPosition = Vector3.Lerp(home, target, k * k); // 가속 — 튀어나가는 느낌
                yield return null;
            }

            for (float t = 0f; t < back; t += Time.deltaTime)
            {
                transform.localPosition = Vector3.Lerp(target, home, Mathf.SmoothStep(0f, 1f, t / back));
                yield return null;
            }

            transform.localPosition = home;
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

        /// <summary>
        /// 모서리 뱃지(폭탄·성남)를 켜고 끈다. <b>그림이 없으면 아무것도 안 만든다</b> —
        /// 뱃지는 덤이고, 진짜 정보는 숫자와 틴트다(그림을 안 꽂아도 게임은 읽힌다).
        ///
        /// <para><paramref name="corner"/>는 셀 안에서의 방향(±1, ±1)이다. 폭탄과 성남을 반대 모서리에
        /// 두는 이유는 <b>한 타일에 둘 다 걸릴 수 있기 때문</b>이다 — 같은 자리면 하나가 다른 하나를 가린다.</para>
        /// </summary>
        private void ShowBadge(ref SpriteRenderer badge, string name, Sprite sprite, bool on, Vector2 corner)
        {
            if (!on || sprite == null)
            {
                if (badge != null)
                    badge.gameObject.SetActive(false);
                return;
            }

            if (badge == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                // 셀 모서리 쪽으로 밀되 살짝 안쪽에 — 완전히 모서리에 붙이면 옆 타일과 붙어 보인다.
                go.transform.localPosition = new Vector3(corner.x * 0.3f, corner.y * 0.3f, 0f);
                go.transform.localScale = Vector3.one * ScaleToFit(sprite, _art.BadgeSize);

                badge = go.AddComponent<SpriteRenderer>();
                badge.sprite = sprite;
                badge.sortingOrder = 6; // 사슬(5) 위, 숫자(10) 아래
            }

            badge.gameObject.SetActive(true);
        }

        private SpriteRenderer CreateChainOverlay(Sprite chainSprite)
        {
            var go = new GameObject("Chain");
            go.transform.SetParent(transform, false);
            // 그림 크기·PPU가 제각각이라 스케일을 그림에서 역산한다 — 타일 아이콘과 같은 규칙.
            go.transform.localScale = chainSprite != null
                ? Vector3.one * ScaleToFit(chainSprite, _art.ChainSize)
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

        /// <summary>
        /// 낙하 — <b>꺾인 길을 따라</b> 간다. 벽에 얹혀 옆으로 미끄러진 타일은 「제 열에서 떨어지다가
        /// 마지막에 옆 칸으로 넘어가는」 두 토막짜리 경로를 갖는데, 시작점과 끝점만 이으면
        /// 비스듬한 직선이 되어 <b>옆 열을 통째로 가로질러</b> 내려온다(남의 자리를 지나간다).
        ///
        /// <para>꺾이는 지점에서 <b>멈췄다 다시 가속하지 않는다</b> — 토막마다 따로 애니메이션하면
        /// 모서리에서 속도가 0이 되어 두 번 떨어지는 것처럼 보인다. 그래서 경로 전체의 길이를
        /// 하나의 자로 삼고 그 위를 훑는다.</para>
        ///
        /// <para>가속하는 이유: <see cref="MoveTo"/>의 SmoothStep은 양 끝이 눕는 곡선이라
        /// 스왑에는 맞지만 낙하에 쓰면 <b>떨어지는 게 아니라 떠 보인다.</b> 중력은 빨라져야 한다.</para>
        /// </summary>
        /// <param name="accelPower">1이면 등속, 2면 중력과 같은 가속. <see cref="BoardView"/>가 정한다.</param>
        public IEnumerator FallAlong(IReadOnlyList<Vector3> path, float duration, float accelPower)
        {
            if (path == null || path.Count == 0)
                yield break;

            Vector3 target = path[path.Count - 1];
            if (duration <= 0f)
            {
                transform.localPosition = target;
                yield break;
            }

            // 토막별 누적 길이 — 이걸 자로 삼아야 꺾이는 곳에서 속도가 안 끊긴다
            var cumulative = new float[path.Count];
            cumulative[0] = 0f;
            for (int i = 1; i < path.Count; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(path[i - 1], path[i]);

            float total = cumulative[path.Count - 1];
            if (total <= Mathf.Epsilon)
            {
                transform.localPosition = target;
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float travelled = Mathf.Pow(t / duration, accelPower) * total;
                transform.localPosition = PointAlong(path, cumulative, travelled);
                yield return null;
            }

            transform.localPosition = target;
        }

        /// <summary>경로 위에서 시작점으로부터 <paramref name="distance"/> 만큼 간 지점.</summary>
        private static Vector3 PointAlong(IReadOnlyList<Vector3> path, float[] cumulative, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                if (distance > cumulative[i])
                    continue;

                float span = cumulative[i] - cumulative[i - 1];
                float u = span <= Mathf.Epsilon ? 1f : (distance - cumulative[i - 1]) / span;
                return Vector3.Lerp(path[i - 1], path[i], u);
            }

            return path[path.Count - 1];
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
