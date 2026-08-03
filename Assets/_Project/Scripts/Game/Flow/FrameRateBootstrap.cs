using UnityEngine;

namespace ChainRiposte.Game.Flow
{
    /// <summary>
    /// 목표 프레임을 못 박는다. <b>안 박으면 안드로이드는 30으로 돈다</b> —
    /// <c>Application.targetFrameRate</c>의 기본값 −1은 "플랫폼 기본값"이고 모바일에서 그게 30이다.
    /// 화면이 60Hz든 120Hz든 상관없이 30에서 잘린다.
    ///
    /// <para>패링이 0.25초 창을 다투는 게임이라 이 값이 곧 <b>판정 해상도</b>다.
    /// 30fps면 한 프레임이 33ms — Lv0 판정 폭(0.25초)의 13%를 한 프레임이 먹는다.
    /// 입력을 정확히 눌러도 프레임 경계에 걸려 놓치는 일이 생긴다.</para>
    ///
    /// <para>모바일에서 <c>QualitySettings.vSyncCount</c>는 무시되므로 이쪽이 유일한 손잡이다.
    /// 에디터·데스크톱은 vSync가 잡고 있어 이 값이 사실상 상한으로만 작동한다.</para>
    /// </summary>
    public static class FrameRateBootstrap
    {
        /// <summary>기기가 60Hz 아래면 알아서 그 아래로 떨어진다 — 위로 밀어 올리지는 못한다.</summary>
        private const int TargetFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = TargetFps;
        }
    }
}
