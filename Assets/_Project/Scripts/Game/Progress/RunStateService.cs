using ChainRiposte.Core.Progress;
using ChainRiposte.Game.Characters;
using UnityEngine;

namespace ChainRiposte.Game.Progress
{
    /// <summary>
    /// 성장 캐리 세이브 어댑터 (<c>Docs/PROGRESSION.md</c>). 규칙은 <see cref="RunState"/>(Core)가 갖고,
    /// 여기서는 PlayerPrefs에 읽고 쓰는 것만 한다 — <see cref="ProgressService"/>의 형제.
    ///
    /// <para>런은 <b>캐릭터별로</b> 저장된다(기사/낭인 각자의 사슬). 그래서 키에 캐릭터 id가 들어가고,
    /// 활성 캐릭터가 바뀌면 그 캐릭터의 런으로 다시 로드한다.</para>
    /// </summary>
    public static class RunStateService
    {
        private const string KeyPrefix = "ChainRiposte.Run.v1.";
        private const string DefaultCharacter = "default";

        private static RunState _current;
        private static string _loadedFor;

        /// <summary>활성 캐릭터의 현재 런 (필요 시 로드). 캐릭터가 바뀌면 갈아 끼운다.</summary>
        public static RunState Current
        {
            get
            {
                string id = ActiveCharacterId;
                if (_current == null || _loadedFor != id)
                {
                    _current = RunState.Deserialize(PlayerPrefs.GetString(KeyFor(id), string.Empty));
                    _loadedFor = id;
                }

                return _current;
            }
        }

        public static void Save()
        {
            string id = ActiveCharacterId;
            PlayerPrefs.SetString(KeyFor(id), Current.Serialize());
            PlayerPrefs.Save();
            _loadedFor = id;
        }

        /// <summary>새 게임 — 이 캐릭터의 사슬을 처음부터. (이전 회차 빌드를 물려받지 않게)</summary>
        public static void StartNewRun()
        {
            _current = new RunState();
            _loadedFor = ActiveCharacterId;
            Save();
            Debug.Log($"[Run] '{_loadedFor}' 새 런 시작.");
        }

        /// <summary>현재 캐릭터의 런 세이브 삭제 (디버그 — Tools ▸ ChainRiposte ▸ Progress).</summary>
        public static void ResetCurrent()
        {
            PlayerPrefs.DeleteKey(KeyFor(ActiveCharacterId));
            PlayerPrefs.Save();
            _current = new RunState();
            _loadedFor = ActiveCharacterId;
        }

        private static string ActiveCharacterId
        {
            get
            {
                PlayerCharacterSO character = CharacterService.Current;
                return character != null && !string.IsNullOrEmpty(character.CharacterId)
                    ? character.CharacterId
                    : DefaultCharacter;
            }
        }

        private static string KeyFor(string characterId) => KeyPrefix + characterId;

        // 도메인 리로드를 꺼둔 환경에서 이전 플레이의 정적 상태가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            _current = null;
            _loadedFor = null;
        }
    }
}
