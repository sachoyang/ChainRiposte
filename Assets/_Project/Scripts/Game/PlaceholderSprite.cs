using UnityEngine;

namespace ChainRiposte.Game
{
    /// <summary>에셋 없이 쓰는 1×1 단색 사각형 스프라이트. 에셋 단계에서 실제 스프라이트로 교체된다.</summary>
    public static class PlaceholderSprite
    {
        private static Sprite _square;

        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    Texture2D tex = Texture2D.whiteTexture; // 4x4 흰색
                    _square = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), pixelsPerUnit: tex.width);
                }

                return _square;
            }
        }
    }
}
