using System;
using UnityEngine;

namespace ChainRiposte.Game.UI
{
    /// <summary>화면 방향 (GDD §9.3). 세로가 기본이고 가로도 성립해야 한다.</summary>
    public enum ScreenLayout
    {
        Portrait = 0,
        Landscape = 1,
    }

    /// <summary>
    /// 화면 방향을 한 곳에서 판정해 알려주는 허브 (GDD §9.3 모바일 뷰 대응).
    /// 씬에 아무것도 놓을 필요 없이 첫 플레이 때 감시자를 자동으로 띄운다 —
    /// UI 요소는 <see cref="OrientationLayout"/>을 붙여 방향별 배치를 갖는다.
    /// </summary>
    public static class OrientationService
    {
        public static ScreenLayout Current { get; private set; } = ScreenLayout.Portrait;

        public static bool IsPortrait => Current == ScreenLayout.Portrait;

        /// <summary>방향이 바뀐 순간 (구독자는 자기 배치를 다시 적용한다).</summary>
        public static event Action<ScreenLayout> Changed;

        /// <summary>해상도만으로 방향을 판정한다 — 정사각형은 세로로 친다.</summary>
        public static ScreenLayout Evaluate(float width, float height) =>
            width > height ? ScreenLayout.Landscape : ScreenLayout.Portrait;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // 도메인 리로드를 끈 환경에서 이전 플레이의 구독이 남는 것을 막는다
            Changed = null;
            Current = Evaluate(Screen.width, Screen.height);

            var go = new GameObject("[OrientationWatcher]") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<OrientationWatcher>();
        }

        /// <summary>감시자가 매 프레임 호출한다. 바뀐 프레임에만 이벤트가 나간다.</summary>
        internal static void Poll(int width, int height)
        {
            ScreenLayout next = Evaluate(width, height);
            if (next == Current)
                return;

            Current = next;
            Changed?.Invoke(next);
        }

        /// <summary>에디터 미리보기용 — 실제 화면과 무관하게 방향을 강제한다.</summary>
        internal static void ForceEditorPreview(ScreenLayout layout)
        {
            Current = layout;
            Changed?.Invoke(layout);
        }
    }

    /// <summary>해상도 변화를 감시하는 부팅 전용 컴포넌트. 씬에 저장되지 않는다.</summary>
    internal sealed class OrientationWatcher : MonoBehaviour
    {
        private void Update() => OrientationService.Poll(Screen.width, Screen.height);
    }
}
