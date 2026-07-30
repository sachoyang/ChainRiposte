using System.Collections.Generic;
using ChainRiposte.Core.Combat;
using ChainRiposte.Game.Config;
using UnityEngine;

namespace ChainRiposte.Game.Memories
{
    /// <summary>
    /// 삼킨 기억의 <b>id</b>(세이브에 남는 것)를 실제 에셋으로 되돌려 주는 창구.
    ///
    /// <para>목록을 Resources에서 읽는 이유는 <see cref="Characters.CharacterService"/>와 같다 —
    /// <b>씬을 다시 배선하지 않고 기억을 늘리기 위해서다.</b> 세이브에는 id만 남으므로,
    /// 지난 판에서 삼킨 기억은 그 보스 에셋이 로드돼 있지 않아도 여기서 찾을 수 있어야 한다.</para>
    /// </summary>
    public static class MemoryLibrary
    {
        /// <summary>이 폴더 밑에 있는 <see cref="BossMemorySO"/>가 곧 기억 전체다.</summary>
        public const string ResourcesFolder = "Memories";

        private static Dictionary<string, BossMemorySO> _byId;

        /// <summary>id로 기억 에셋을 찾는다. 없으면 null (에셋이 지워졌거나 id를 바꾼 옛 세이브).</summary>
        public static BossMemorySO Find(string memoryId)
        {
            if (string.IsNullOrWhiteSpace(memoryId))
                return null;

            EnsureLoaded();
            return _byId.TryGetValue(memoryId.Trim(), out BossMemorySO memory) ? memory : null;
        }

        /// <summary>
        /// id 목록을 에셋으로 바꿔 <b>순서를 지켜</b> 돌려준다. 못 찾은 id는 조용히 건너뛴다 —
        /// 기억 하나를 못 찾았다고 아이콘 줄 전체가 비면 안 된다.
        /// </summary>
        public static List<BossMemorySO> Resolve(IReadOnlyList<string> memoryIds)
        {
            var result = new List<BossMemorySO>();
            if (memoryIds == null)
                return result;

            foreach (string id in memoryIds)
            {
                BossMemorySO memory = Find(id);
                if (memory != null)
                    result.Add(memory);
            }

            return result;
        }

        /// <summary>이 기억들이 전투에 주는 효과의 합. 하나도 없으면 기본 규칙과 같은 값이 나온다.</summary>
        public static BossMemoryConfig CombinedConfig(IReadOnlyList<string> memoryIds)
        {
            List<BossMemorySO> memories = Resolve(memoryIds);
            var configs = new List<BossMemoryConfig>(memories.Count);
            foreach (BossMemorySO memory in memories)
                configs.Add(memory.ToConfig());

            return BossMemoryConfig.Combine(configs);
        }

        // 도메인 리로드를 꺼둔 환경에서 이전 플레이의 정적 상태가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _byId = null;

        private static void EnsureLoaded()
        {
            if (_byId != null)
                return;

            _byId = new Dictionary<string, BossMemorySO>();
            foreach (BossMemorySO memory in Resources.LoadAll<BossMemorySO>(ResourcesFolder))
            {
                if (memory == null)
                    continue;

                // id가 겹치면 세이브에서 둘을 구분할 방법이 없다 — 조용히 덮지 않고 알린다.
                if (_byId.TryGetValue(memory.MemoryId, out BossMemorySO existing))
                {
                    Debug.LogError(
                        $"[Memory] id '{memory.MemoryId}'가 겹칩니다: {existing.name} ↔ {memory.name}. " +
                        "한쪽의 Memory Id를 바꿔 주세요.", memory);
                    continue;
                }

                _byId.Add(memory.MemoryId, memory);
            }
        }
    }
}
