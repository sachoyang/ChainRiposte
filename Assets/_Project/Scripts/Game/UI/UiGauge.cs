using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 게이지 이미지가 <c>fillAmount</c>에 실제로 반응하도록 보장하는 공용 장치.
    ///
    /// <para><b>왜 필요한가</b>: 9슬라이스 스프라이트를 꽂으면 Unity가 Image Type 을 <c>Sliced</c>로
    /// 되돌린다. 그러면 <c>fillAmount</c>는 아무 효과도 없고 게이지가 <b>조용히</b> 안 줄어든다 —
    /// 에러도 경고도 없이 그림만 멀쩡히 보이므로 원인을 찾기가 아주 어렵다.
    /// 실제로 이 프로젝트에서 전투 게이지 셋(세션 11)과 퍼즐 하단 체력 바(세션 12)가
    /// 같은 이유로 각각 한 번씩 죽었다.</para>
    ///
    /// <para>그래서 <b>씬 상태에 기대지 않고</b> 게이지를 그리는 컴포넌트가 시작할 때 스스로 맞춘다.
    /// 스프라이트를 다시 갈아 끼워도 안 깨진다.</para>
    /// </summary>
    public static class UiGauge
    {
        /// <summary>가로로, 왼쪽에서 오른쪽으로 차는 게이지로 맞춘다. 이미 그렇다면 아무것도 안 한다.</summary>
        public static void EnsureFilled(Image image)
        {
            if (image == null || image.type == Image.Type.Filled)
                return;

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }
}
