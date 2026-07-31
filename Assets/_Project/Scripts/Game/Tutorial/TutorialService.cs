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

        private static HashSet<string> _seen;

        private static HashSet<string> Seen => _seen ??= Load();

        /// <summary>도메인 리로드를 꺼둔 환경에서 지난 플레이의 기억이 남지 않도록 부팅 시 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _seen = null;

        /// <summary>지금까지 본 항목 id들 (디버그 출력용).</summary>
        public static IReadOnlyCollection<string> SeenIds => Seen;

        public static bool HasSeen(string topicId) =>
            !string.IsNullOrWhiteSpace(topicId) && Seen.Contains(topicId);

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
