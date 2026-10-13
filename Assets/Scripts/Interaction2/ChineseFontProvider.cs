using TMPro;
using UnityEngine;

namespace VRFire.Utils
{
    /// <summary>
    /// 共享中文字体资产提供器。
    /// TextMeshPro 的默认字体（LiberationSans SDF）不含中文字形，任何中文都会渲染成方块（□）。
    /// 本类在运行时从内置的 Noto Sans CJK 源字体
    /// （Assets/Scripts/Interaction/Resources/NotoSansCJKsc-Regular.otf）
    /// 生成“动态 SDF”字体资产并缓存，供所有运行时创建的文本组件使用。
    /// 动态模式会在首次遇到某中文字符时按需烘焙进图集，因此无需预先枚举字符。
    /// 用法：text.font = ChineseFontProvider.GetFontOrDefault();
    /// </summary>
    public static class ChineseFontProvider
    {
        private const string SourceFontResourcesPath = "NotoSansCJKsc-Regular";

        private static TMP_FontAsset _cachedAsset;

        /// <summary>中文字体资产（动态 SDF，按需烘焙中文 glyph）。首次访问时生成并进程内缓存。</summary>
        public static TMP_FontAsset Asset
        {
            get
            {
                if (_cachedAsset != null)
                    return _cachedAsset;

                Font sourceFont = Resources.Load<Font>(SourceFontResourcesPath);
                if (sourceFont == null)
                {
                    Debug.LogError(
                        "[ChineseFontProvider] 找不到内置中文字体源文件 NotoSansCJKsc-Regular " +
                        "（应位于 Assets/Scripts/Interaction/Resources/NotoSansCJKsc-Regular.otf）。",
                        null);
                    return null;
                }

                _cachedAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (_cachedAsset == null)
                    Debug.LogError("[ChineseFontProvider] 无法从 Noto Sans CJK 源字体生成 TMP 字体资产。", null);

                return _cachedAsset;
            }
        }

        /// <summary>
        /// 返回中文字体资产；若生成失败则回退到 TextMeshPro 默认字体，保证不为 null。
        /// Noto Sans CJK 同时包含拉丁字母/数字/标点，因此中英文混排都由它正常显示。
        /// </summary>
        public static TMP_FontAsset GetFontOrDefault()
        {
            return Asset ?? TMP_Settings.defaultFontAsset;
        }
    }
}
