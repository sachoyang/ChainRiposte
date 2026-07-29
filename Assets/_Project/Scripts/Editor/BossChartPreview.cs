using System;
using System.Reflection;
using ChainRiposte.Game;
using ChainRiposte.Game.Config;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 채보 미리듣기 — <b>플레이 없이</b> 에디터에서 패턴을 돌려 본다.
    ///
    /// <para><b>왜 필요한가</b>: 지금까지 채보를 확인하려면 인스펙터에서 찍고 → 플레이 →
    /// 퍼즐을 다 풀고 → 보스전까지 가야 했다. 한 번 고칠 때마다 그 왕복을 하면
    /// 채보를 짜는 시간의 대부분이 확인에 샌다. 여기서는 버튼 한 번에 즉시 들린다.</para>
    ///
    /// <para><b>게임과 같은 그림으로 그린다</b> — 원의 반지름은 전투 화면과 똑같이
    /// <c>1 + 남은시간 × 접근속도</c>이고, 링 그림도 게임이 쓰는 <see cref="PlaceholderSprite.Annulus"/>
    /// 그대로다. 판정 띠도 <see cref="PlayerStatsConfigSO"/>에서 실제 수치를 읽는다.
    /// 미리보기가 게임과 다른 자를 쓰면 여기서 맞춘 채보가 게임에서 어긋난다.</para>
    ///
    /// <para>소리는 에디터 내부 API(<c>AudioUtil</c>)를 <b>리플렉션으로</b> 부른다. 유니티 버전마다
    /// 이름이 달라서, 못 찾으면 조용히 그림만 보여 준다 — 소리가 없다고 미리보기가 멈추면 안 된다.</para>
    /// </summary>
    internal sealed class BossChartPreview
    {
        /// <summary>전투 화면(CombatScreen)의 기본값과 같은 값. 여기만 다르면 미리보기가 거짓말을 한다.</summary>
        private const float ApproachSpeed = 1.4f;
        private const float MaxVisibleScale = 3.6f;

        /// <summary>마지막 노트가 지나간 뒤 쉬는 시간(초) — 곧바로 되감으면 끝이 어딘지 안 들린다.</summary>
        private const float LoopTailSeconds = 0.6f;

        private static readonly Color BandColor = new(1f, 1f, 1f, 0.16f);
        private static readonly Color RingColor = new(1f, 1f, 1f, 0.95f);
        private static readonly Color BodyColor = new(0.62f, 0.08f, 0.12f);
        private static readonly Color StageColor = new(0.09f, 0.08f, 0.11f);
        private static readonly Color PlayheadColor = new(0.98f, 0.78f, 0.25f);

        private readonly Action _repaint;

        private int _pattern = -1;
        private double _lastUpdate;
        private float _time;          // 패턴 시작 기준 초. 음수 = 리드인(첫 노트가 다가오는 중)
        private float _leadIn;
        private float _lengthSeconds;
        private int _nextNote;
        private float _flash;         // 방금 타격이 있었나 (0~1)

        private bool _loop = true;
        private bool _metronome;
        private float _lastBeatPlayed = -1f;

        private AudioClip _hitClip;
        private AudioClip _beatClip;

        public BossChartPreview(Action repaint) => _repaint = repaint;

        public bool IsPlaying => _pattern >= 0;

        public bool IsPlayingPattern(int patternIndex) => _pattern == patternIndex;

        public void Stop()
        {
            _pattern = -1;
            _flash = 0f;
            StopAudio();
        }

        /// <summary>에디터 시간으로 굴린다 — 게임의 <c>Time.deltaTime</c>은 플레이 중이 아니면 안 흐른다.</summary>
        public void Tick()
        {
            if (!IsPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Clamp((float)(now - _lastUpdate), 0f, 0.1f); // 창이 멈췄다 돌아온 프레임에서 건너뛰지 않게
            _lastUpdate = now;

            _time += delta;
            _flash = Mathf.Max(0f, _flash - delta * 4f);
            _repaint?.Invoke();
        }

        // ── 조작 줄 ──────────────────────────────────────────────────

        /// <summary>재생/정지 + 반복·메트로놈 토글. 패턴 하나마다 한 줄씩 붙는다.</summary>
        public void DrawTransport(int patternIndex, PatternTiming timing)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool playingThis = IsPlayingPattern(patternIndex);
                if (GUILayout.Button(playingThis ? "■ 정지" : "▶ 미리듣기", GUILayout.Width(96f)))
                {
                    if (playingThis)
                        Stop();
                    else
                        Play(patternIndex, timing);
                }

                using (new EditorGUI.DisabledScope(!playingThis))
                {
                    if (GUILayout.Button("↺ 처음부터", GUILayout.Width(84f)))
                        Restart(timing);
                }

                _loop = GUILayout.Toggle(_loop, "반복", EditorStyles.miniButton, GUILayout.Width(46f));
                _metronome = GUILayout.Toggle(_metronome, "박 소리", EditorStyles.miniButton, GUILayout.Width(60f));

                GUILayout.FlexibleSpace();
                if (playingThis)
                    GUILayout.Label($"{Mathf.Max(0f, _time):0.00}s / {_lengthSeconds:0.00}s", EditorStyles.miniLabel);
            }
        }

        private void Play(int patternIndex, PatternTiming timing)
        {
            _pattern = patternIndex;
            _lastUpdate = EditorApplication.timeSinceStartup;
            Restart(timing);
        }

        private void Restart(PatternTiming timing)
        {
            // 첫 노트가 화면 밖에서부터 다가오도록 가장 긴 예비동작만큼 앞에서 시작한다.
            _leadIn = timing.LongestTelegraphSeconds;
            _lengthSeconds = timing.LengthSeconds;
            _time = -_leadIn;
            _nextNote = 0;
            _flash = 0f;
            _lastBeatPlayed = -1f;
        }

        // ── 무대 (다가오는 원) ────────────────────────────────────────

        /// <summary>
        /// 전투 화면과 같은 그림. 원이 안쪽 흰 원(플레이어)에 닿는 순간이 타격 시점이고,
        /// 회색 띠에 걸쳐 있는 동안이 패링 판정이다.
        /// </summary>
        public void DrawStage(int patternIndex, PatternTiming timing, float height = 168f)
        {
            if (!IsPlayingPattern(patternIndex))
                return;

            Rect row = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(row, StageColor);

            float radius = height * 0.5f - 6f;
            var center = new Vector2(row.center.x, row.center.y);
            float unit = radius / MaxVisibleScale; // 반지름 1 = 몇 픽셀인가

            AdvanceAudio(timing);

            float ringRatio = ResolveRingInnerRatio();
            DrawBand(center, unit, ringRatio);

            // 원은 먼 것부터 그려야 임박한 원이 위에 온다
            for (int i = timing.Notes.Length - 1; i >= 0; i--)
            {
                float untilHit = timing.Notes[i].HitSeconds - _time;
                if (untilHit < 0f || untilHit > timing.Notes[i].TelegraphSeconds)
                    continue;

                float scale = 1f + untilHit * ApproachSpeed;
                if (scale > MaxVisibleScale)
                    continue;

                float nearness = Mathf.InverseLerp(MaxVisibleScale, 1f, scale);
                Color color = RingColor;
                color.a *= Mathf.Lerp(0.2f, 1f, nearness);
                DrawRing(center, scale * unit, color, ringRatio);
            }

            // 플레이어 몸 — 원이 여기 닿으면 타격이다
            float bodyRadius = unit * (1f + _flash * 0.35f);
            DrawDisc(center, bodyRadius, Color.Lerp(BodyColor, Color.white, _flash));

            HandleLoop(timing);
        }

        /// <summary>
        /// 회색 띠 = 패링 판정 구간. 안쪽을 <b>원 두께만큼 밀어 올린다</b> —
        /// 전투 화면의 <c>UpdateParryBand</c>와 같은 계산이라야 여기서 맞춘 채보가 게임에서도 맞는다.
        /// </summary>
        private void DrawBand(Vector2 center, float unit, float ringInnerRatio)
        {
            PlayerStatsConfigSO stats = LoadStats();
            if (stats == null)
                return;

            Core.Stats.PlayerStatsConfig config = stats.ToConfig();
            float outer = 1f + config.BaseParryWindowSeconds * ApproachSpeed;
            float inner = Mathf.Clamp(
                (1f - config.ParryLateGraceSeconds * ApproachSpeed) / ringInnerRatio, 0.05f, outer - 0.001f);
            DrawRing(center, outer * unit, BandColor, inner / outer);
        }

        /// <summary>
        /// 노트 원의 두께. 전투 화면이 무투자 상태에서 두 원을 정확히 포개려고 역산하는 값과 같은 식이다
        /// (<c>k = (2 − 유예×속도) ÷ (2 + 기본윈도우×속도)</c>). 판정을 조여도 미리보기가 따라온다.
        /// </summary>
        private static float ResolveRingInnerRatio()
        {
            PlayerStatsConfigSO stats = LoadStats();
            if (stats == null)
                return 0.88f; // PlaceholderSprite.Ring 의 비율

            Core.Stats.PlayerStatsConfig config = stats.ToConfig();
            float grace = config.ParryLateGraceSeconds * ApproachSpeed;
            float window = config.BaseParryWindowSeconds * ApproachSpeed;
            return PlaceholderSprite.QuantizeRatio(Mathf.Clamp((2f - grace) / (2f + window), 0.05f, 0.99f));
        }

        /// <summary>게임이 쓰는 링 생성기를 그대로 쓴다 — 미리보기와 실제가 같은 그림이어야 한다.</summary>
        private static void DrawRing(Vector2 center, float radius, Color color, float innerRatio)
        {
            Sprite sprite = PlaceholderSprite.Annulus(Mathf.Clamp(innerRatio, 0.05f, 0.98f));
            if (sprite == null || sprite.texture == null)
                return;

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
                sprite.texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static void DrawDisc(Vector2 center, float radius, Color color) =>
            DrawRing(center, radius, color, 0f); // 안쪽 비율 0 = 꽉 찬 원

        // ── 타임라인 위의 재생 위치 ───────────────────────────────────

        /// <summary>기존 타임라인 위에 지금 어디를 지나는지 세로줄로 겹쳐 그린다.</summary>
        public void DrawPlayhead(int patternIndex, Rect timelineRect, float lengthBeats, float secondsPerBeat)
        {
            if (!IsPlayingPattern(patternIndex) || lengthBeats <= 0f || secondsPerBeat <= 0f)
                return;

            float beat = _time / secondsPerBeat;
            if (beat < 0f || beat > lengthBeats)
                return;

            float x = timelineRect.x + beat / lengthBeats * timelineRect.width;
            EditorGUI.DrawRect(new Rect(x - 1f, timelineRect.y, 2f, timelineRect.height), PlayheadColor);
        }

        // ── 소리 ─────────────────────────────────────────────────────

        private void AdvanceAudio(PatternTiming timing)
        {
            while (_nextNote < timing.Notes.Length && timing.Notes[_nextNote].HitSeconds <= _time)
            {
                PlayClip(HitClip());
                _flash = 1f;
                _nextNote++;
            }

            if (!_metronome || timing.SecondsPerBeat <= 0f || _time < 0f)
                return;

            float beat = Mathf.Floor(_time / timing.SecondsPerBeat);
            if (beat > _lastBeatPlayed)
            {
                _lastBeatPlayed = beat;
                PlayClip(BeatClip());
            }
        }

        private void HandleLoop(PatternTiming timing)
        {
            if (_time < _lengthSeconds + LoopTailSeconds)
                return;

            if (_loop)
                Restart(timing);
            else
                Stop();
        }

        /// <summary>
        /// 짧은 클릭을 코드로 굽는다 — 프로젝트에 오디오 파일이 하나도 없어도 소리가 나야 한다.
        /// 사인파를 빠르게 감쇠시키면 '틱'으로 들린다.
        /// </summary>
        private static AudioClip CreateClick(string name, float frequency, float seconds, float volume)
        {
            const int SampleRate = 44100;
            int samples = Mathf.Max(16, (int)(SampleRate * seconds));
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float decay = Mathf.Exp(-t / (seconds * 0.25f));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * decay * volume;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip HitClip() => _hitClip != null
            ? _hitClip
            : _hitClip = CreateClick("ChartHit", 1180f, 0.09f, 0.85f);

        private AudioClip BeatClip() => _beatClip != null
            ? _beatClip
            : _beatClip = CreateClick("ChartBeat", 620f, 0.05f, 0.30f);

        // ── 에디터 내부 오디오 (버전마다 이름이 다르다) ────────────────

        private static MethodInfo _play;
        private static MethodInfo _stop;
        private static bool _audioResolved;

        private static void ResolveAudio()
        {
            if (_audioResolved)
                return;

            _audioResolved = true;
            Type type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (type == null)
                return;

            // 2020 이후 PlayPreviewClip / 그 이전 PlayClip. 둘 다 없으면 소리 없이 그림만 돈다.
            _play = type.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
                        null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                    ?? type.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public,
                        null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                    ?? type.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
                        null, new[] { typeof(AudioClip) }, null)
                    ?? type.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public,
                        null, new[] { typeof(AudioClip) }, null);

            _stop = type.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)
                    ?? type.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);
        }

        private static void PlayClip(AudioClip clip)
        {
            ResolveAudio();
            if (_play == null || clip == null)
                return;

            object[] args = _play.GetParameters().Length == 3
                ? new object[] { clip, 0, false }
                : new object[] { clip };
            _play.Invoke(null, args);
        }

        private static void StopAudio()
        {
            ResolveAudio();
            _stop?.Invoke(null, null);
        }

        private static PlayerStatsConfigSO _stats;

        /// <summary>판정 폭의 원천은 하나뿐이다 — 미리보기가 자기 숫자를 들고 있으면 반드시 어긋난다.</summary>
        private static PlayerStatsConfigSO LoadStats()
        {
            if (_stats != null)
                return _stats;

            string[] guids = AssetDatabase.FindAssets("t:PlayerStatsConfigSO");
            if (guids.Length == 0)
                return null;

            _stats = AssetDatabase.LoadAssetAtPath<PlayerStatsConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return _stats;
        }

        /// <summary>
        /// 미리보기가 쓰는 패턴의 '초' 환산. 인스펙터의 박 단위 값을 한 번에 초로 바꿔 두면
        /// 그리는 쪽과 소리 내는 쪽이 같은 값을 본다.
        /// </summary>
        internal readonly struct PatternTiming
        {
            public readonly Note[] Notes;
            public readonly float SecondsPerBeat;
            public readonly float LengthSeconds;
            public readonly float LongestTelegraphSeconds;

            public PatternTiming(Note[] notes, float secondsPerBeat, float lengthBeats)
            {
                Notes = notes ?? Array.Empty<Note>();
                SecondsPerBeat = secondsPerBeat;
                LengthSeconds = lengthBeats * secondsPerBeat;

                float longest = 0f;
                for (int i = 0; i < Notes.Length; i++)
                    longest = Mathf.Max(longest, Notes[i].TelegraphSeconds);
                LongestTelegraphSeconds = longest;
            }

            internal readonly struct Note
            {
                public readonly float HitSeconds;
                public readonly float TelegraphSeconds;

                public Note(float hitSeconds, float telegraphSeconds)
                {
                    HitSeconds = hitSeconds;
                    TelegraphSeconds = telegraphSeconds;
                }
            }
        }
    }
}
