using UnityEditor;
using UnityEngine;

namespace TwinSquad.EditorTools
{
    /// <summary>
    /// 角色 Sprite 自动导入参数预设。
    ///
    /// 作用：
    /// - 凡是放进 Resources/Sprites/Characters/ 下的图，导入时自动应用统一参数
    /// - 避免每次都要手动到 Inspector 改 PPU/Pivot/FilterMode
    ///
    /// 触发时机：
    /// - 新图首次导入：自动生效
    /// - 已存在的图：右键 → Reimport（或 Reimport 整个文件夹）才会触发
    ///
    /// 默认参数：
    /// - PPU = 256（与 256×512 像素人物 = 1×2 世界单位匹配）
    /// - Pivot = Center (0.5, 0.5)（配合 transform.y = 1 让脚底贴地）
    /// - Filter = Bilinear（手绘/二次元，像素风请改 Point）
    /// - 透明通道 = 启用
    /// - Mipmap = 关闭（2D 不需要，省内存）
    /// </summary>
    public class CharacterSpritePostprocessor : AssetPostprocessor
    {
        private const string TargetFolder = "Assets/Resources/Sprites/Characters/";
        private const float CharacterPPU = 256f;

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(TargetFolder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = CharacterPPU;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;

            // pivot 必须通过 TextureImporterSettings 路径
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
        }
    }
}
