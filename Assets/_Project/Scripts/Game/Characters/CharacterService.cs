using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChainRiposte.Game.Characters
{
    /// <summary>
    /// 캐릭터 목록(Resources)과 선택 상태(PlayerPrefs)를 다루는 창구.
    ///
    /// 목록을 Resources에서 읽는 이유는 <b>씬 빌더를 다시 돌리지 않고 캐릭터를 늘리기 위해서</b>다.
    /// 현지화 CSV와 같은 방식이고, 선택 저장은 <see cref="Progress.ProgressService"/>·
    /// <see cref="Options.GameOptions"/>와 같은 PlayerPrefs 어댑터다.
    /// </summary>
    public static class CharacterService
    {
        /// <summary>이 폴더 밑에 있는 <see cref="PlayerCharacterSO"/>가 곧 선택지다.</summary>
        public const string ResourcesFolder = "Characters";

        private const string Key = "ChainRiposte.Character.v1";

        private static PlayerCharacterSO[] _all;
        private static PlayerCharacterSO _current;
        private static bool _loaded;

        /// <summary>선택이 바뀐 순간 — 성녀·플레이어 그림을 쓰는 쪽이 구독한다.</summary>
        public static event Action<PlayerCharacterSO> Changed;

        /// <summary>정렬된 전체 목록. 하나도 없으면 빈 배열.</summary>
        public static IReadOnlyList<PlayerCharacterSO> All
        {
            get
            {
                if (_all == null)
                {
                    _all = Resources.LoadAll<PlayerCharacterSO>(ResourcesFolder);
                    Array.Sort(_all, (a, b) => a.SortOrder != b.SortOrder
                        ? a.SortOrder.CompareTo(b.SortOrder)
                        : string.CompareOrdinal(a.CharacterId, b.CharacterId));
                }

                return _all;
            }
        }

        /// <summary>
        /// 지금 플레이 중인 캐릭터. 고른 적이 없으면 <b>목록의 첫 번째</b>로 떨어진다 —
        /// Main 씬을 단독 실행해도 그림이 비지 않는다. 캐릭터 에셋이 하나도 없으면 null.
        /// </summary>
        public static PlayerCharacterSO Current
        {
            get
            {
                EnsureLoaded();
                if (_current == null && All.Count > 0)
                    _current = All[0];
                return _current;
            }
        }

        /// <summary>세이브에 남은 선택이 있는가 (선택 화면을 건너뛸지 판단할 때).</summary>
        public static bool HasChosen
        {
            get
            {
                EnsureLoaded();
                return _current != null;
            }
        }

        public static void Select(PlayerCharacterSO character)
        {
            if (character == null)
                return;

            EnsureLoaded();
            _current = character;
            PlayerPrefs.SetString(Key, character.CharacterId);
            PlayerPrefs.Save();
            Changed?.Invoke(character);
        }

        public static PlayerCharacterSO Find(string characterId)
        {
            foreach (PlayerCharacterSO character in All)
            {
                if (character.CharacterId == characterId)
                    return character;
            }

            return null;
        }

        /// <summary>선택 삭제 (디버그/새 게임 취소용). 다음 새 게임에서 다시 고르게 된다.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _current = null;
            _loaded = true;
            Changed?.Invoke(null);
        }

        // 도메인 리로드를 꺼둔 환경에서 이전 플레이의 정적 상태가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            Changed = null;
            _all = null;
            _current = null;
            _loaded = false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            string saved = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(saved))
                _current = Find(saved);
        }
    }
}
