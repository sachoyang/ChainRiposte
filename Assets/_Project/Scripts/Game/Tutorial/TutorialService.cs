using System.Collections.Generic;
using UnityEngine;

namespace ChainRiposte.Game.Tutorial
{
    /// <summary>
    /// <b>본 것은 기억한다</b> (<c>Docs/TUTORIAL.md</c> §2.1). <see cref="Progress.ProgressService"/>의 형제로,
    /// PlayerPrefs에 <b>본 항목 id 집합</b>만 둔다.
    ///
    /// <para>저장하는 것이 <b>id 문자열뿐</b>인 것이 요점이다 — 에셋을 지우거나 이름을 바꿔도 세이브가
    /// 안 깨진다(<c>StageProgress</c>·<c>RunState</c>와 같은 규칙). 지금 존재하지 않는 id가 남아 있어도
    /// 아무 일도 일어나지 않는다.</para>
    ///
    /// <para><b>NG+에서는 유지된다.</b> 2회차에 이미 배운 것을 다시 가르치면 그건 안내가 아니라 방해다 —
    /// 그래서 <c>ProgressService.BeginNewGamePlus</c>는 여기를 안 건드린다. 반대로
    /// <c>ProgressService.ResetAll</c>("처음부터")은 여기도 같이 지운다.</para>
    /// </summary>
    public static class TutorialService
    {
        private const string Key = "ChainRiposte.Tutorial.v1";
        private const char Separator = ';';

        /// <summary>
        /// 튜토리얼 전체 on/off. <b>지금은 꺼 둔다</b> (2026-08-03, 사용자 지정 — 포트폴리오 시연에서 뺀다.
        /// 구현은 다 끝나 있고 「future work」로 돌린 것뿐이다).
        ///
        /// <para><b>여기 하나로 끄는 이유</b>: 튜토리얼로 들어가는 문이 셋인데
        /// (<c>TitleController</c> 새 게임 진입 · <c>TutorialDirector</c> 자가 활성 ·
        /// <c>TutorialCard</c> 기믹 카드 큐) <b>셋 다 <see cref="HasSeen"/>를 지난다.</b>
        /// 문마다 따로 끄면 하나는 반드시 빠뜨리고, 에셋·씬을 건드리면 되돌릴 때 배선이 샌다.</para>
        ///
        /// <para><b>다시 켜려면 이 값을 true로 되돌리면 끝이다.</b> 저장된 「본 것」 기록은
        /// 건드리지 않으므로 켜는 순간 원래 상태 그대로 돌아온다.</para>
        /// </summary>
        public static bool Enabled;

        private static HashSet<string> _seen;

        private static HashSet<string> Seen => _seen ??= Load();

        /// <summary>도메인 리로드를 꺼둔 환경에서 지난 플레이의 기억이 남지 않도록 부팅 시 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _seen = null;

        /// <summary>지금까지 본 항목 id들 (디버그 출력용).</summary>
        public static IReadOnlyCollection<string> SeenIds => Seen;

        /// <summary>
        /// 이미 본 항목인가. 부르는 쪽은 전부 "안 봤으면 띄운다"라서,
        /// <see cref="Enabled"/>가 꺼져 있으면 <b>전부 본 것으로 답한다</b> — 그것이 곧 전체 끄기다.
        /// </summary>
        public static bool HasSeen(string topicId) =>
            !Enabled || (!string.IsNullOrWhiteSpace(topicId) && Seen.Contains(topicId));

        /// <summary>본 것으로 기록한다. 이미 본 것이면 false(저장도 안 한다).</summary>
        public static bool MarkSeen(string topicId)
        {
            if (string.IsNullOrWhiteSpace(topicId) || !Seen.Add(topicId))
                return false;

            Save();
            Debug.Log($"[Tutorial] '{topicId}' 를 봤다고 기록했습니다. 누적 {Seen.Count}개.");
            return true;
        }

        /// <summary>전부 안 본 것으로 — 튜토리얼을 다시 보게 만든다.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _seen = new HashSet<string>();
        }

        private static void Save()
        {
            PlayerPrefs.SetString(Key, string.Join(Separator.ToString(), Seen));
            PlayerPrefs.Save();
        }

        private static HashSet<string> Load()
        {
            var seen = new HashSet<string>();
            string raw = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
                return seen;

            foreach (string id in raw.Split(Separator))
            {
                if (!string.IsNullOrWhiteSpace(id))
                    seen.Add(id.Trim());
            }

            return seen;
        }
    }
}
