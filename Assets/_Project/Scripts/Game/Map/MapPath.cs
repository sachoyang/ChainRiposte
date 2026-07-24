using System.Collections.Generic;
using UnityEngine;

namespace ChainRiposte.Game.Map
{
    /// <summary>
    /// 노드를 잇는 길의 모양. 꺾은선으로 이으면 지도가 아니라 그래프처럼 보이므로
    /// <b>노드를 지나가는 부드러운 곡선</b>(Catmull-Rom)으로 만든다.
    ///
    /// <para>경로선과 캐릭터 이동이 <b>같은 곡선</b>을 쓰는 것이 핵심이다 —
    /// 따로 계산하면 캐릭터가 그려진 길에서 벗어나 걷는다.</para>
    /// </summary>
    public static class MapPath
    {
        /// <summary>길 전체. <paramref name="samplesPerSegment"/>가 1 이하면 예전처럼 꺾은선이다.</summary>
        public static void Build(IReadOnlyList<Vector3> nodes, int samplesPerSegment, List<Vector3> output)
        {
            output.Clear();
            if (nodes == null || nodes.Count == 0)
                return;

            if (nodes.Count == 1 || samplesPerSegment <= 1)
            {
                foreach (Vector3 node in nodes)
                    output.Add(node);
                return;
            }

            for (int i = 0; i < nodes.Count - 1; i++)
                AppendSegment(nodes, i, i + 1, samplesPerSegment, output, includeStart: i == 0);
        }

        /// <summary>
        /// 이웃한 두 노드 사이만. 캐릭터가 한 칸 걸어갈 때 쓴다 —
        /// 시작점을 포함하므로 첫 점은 지금 서 있는 자리다.
        /// </summary>
        public static void Segment(IReadOnlyList<Vector3> nodes, int from, int to, int samplesPerSegment, List<Vector3> output)
        {
            output.Clear();
            if (nodes == null || from < 0 || to < 0 || from >= nodes.Count || to >= nodes.Count)
                return;

            if (samplesPerSegment <= 1 || Mathf.Abs(to - from) != 1)
            {
                output.Add(nodes[from]);
                output.Add(nodes[to]);
                return;
            }

            AppendSegment(nodes, from, to, samplesPerSegment, output, includeStart: true);
        }

        /// <summary>
        /// <paramref name="from"/> → <paramref name="to"/> 한 구간. 방향이 거꾸로여도 그대로 따라간다.
        /// 양 끝 노드는 이웃이 없으므로 자기 자신을 제어점으로 삼는다(끝에서 곡선이 튀지 않게).
        /// </summary>
        private static void AppendSegment(
            IReadOnlyList<Vector3> nodes, int from, int to, int samplesPerSegment, List<Vector3> output, bool includeStart)
        {
            int step = to > from ? 1 : -1;
            Vector3 p0 = nodes[Mathf.Clamp(from - step, 0, nodes.Count - 1)];
            Vector3 p1 = nodes[from];
            Vector3 p2 = nodes[to];
            Vector3 p3 = nodes[Mathf.Clamp(to + step, 0, nodes.Count - 1)];

            for (int s = includeStart ? 0 : 1; s <= samplesPerSegment; s++)
                output.Add(CatmullRom(p0, p1, p2, p3, s / (float)samplesPerSegment));
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
