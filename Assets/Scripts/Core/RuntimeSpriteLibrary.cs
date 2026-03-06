using UnityEngine;

namespace Snow2
{
    public static class RuntimeSpriteLibrary
    {
        private static Sprite _block;

        // 不在代码里生成 Texture/Sprite，统一从 Resources 加载素材。
        public static Sprite WhiteSprite
        {
            get
            {
                if (_block != null)
                {
                    return _block;
                }

                _block = Resources.Load<Sprite>("Block16");
                return _block;
            }
        }
    }
}
