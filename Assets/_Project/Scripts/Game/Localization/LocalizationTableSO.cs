using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChainRiposte.Game.Localization
{
    /// <summary>
    /// 문구 테이블. 키 하나에 한국어/영어를 나란히 두고 인스펙터에서 편집한다.
    /// <b>Resources 폴더에 <c>LocalizationTable</c> 이름으로 두면</b> <see cref="Loc"/>가 자동으로 찾는다
    /// (씬 참조가 필요 없어야 어느 씬에서 시작하든 문구가 나온다).
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Localization Table", fileName = "LocalizationTable")]
    public sealed class LocalizationTableSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("코드가 참조하는 키. 예: title.continue")]
            public string key;
            [TextArea] public string korean;
            [TextArea] public string english;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>키 → 언어별 문구. 중복 키는 먼저 나온 것을 쓴다.</summary>
        public Dictionary<string, (string ko, string en)> ToLookup()
        {
            var lookup = new Dictionary<string, (string, string)>(entries.Length, StringComparer.Ordinal);
            foreach (Entry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.key) || lookup.ContainsKey(entry.key))
                    continue;
                lookup[entry.key] = (entry.korean, entry.english);
            }

            return lookup;
        }

#if UNITY_EDITOR
        /// <summary>에디터 메뉴 전용 — 기존 문구를 읽어 병합할 때 쓴다.</summary>
        public Entry[] EntriesEditorOnly => entries;

        /// <summary>에디터 메뉴 전용 — 기본 문구를 채워 넣는다.</summary>
        public void SetEntriesEditorOnly(Entry[] value) => entries = value;
#endif
    }
}
