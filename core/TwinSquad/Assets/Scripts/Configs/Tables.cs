using System;
using TwinSquad.Configs.Tb;

namespace TwinSquad.Configs
{
    /// <summary>
    /// 配表总入口。所有业务模块通过 ConfigManager.Tables.TbXxx 访问配表。
    ///
    /// 添加新表步骤：
    ///   1. 在 Configs/Cfg/ 添加配置数据类（如 CharacterConfig.cs）
    ///   2. 在 Configs/Tb/ 添加单表类（参照 TbItem.cs）
    ///   3. 在本类添加字段，并在构造函数中追加一行 jsonLoader 调用
    ///   4. 在 Resources/Configs/ 添加对应 JSON 文件
    /// </summary>
    public class Tables
    {
        public TbItem TbItem { get; }

        // 后续按业务进度逐步添加：
        // public TbCharacter TbCharacter { get; }
        // public TbStage TbStage { get; }
        // public TbSkill TbSkill { get; }
        // public TbGachaPool TbGachaPool { get; }

        /// <summary>
        /// 构造函数：传入加载器（tableName → JSON 文本）。
        /// ConfigManager 负责实际 IO，本类只负责把 JSON 分发给各 Tb 类。
        /// </summary>
        public Tables(Func<string, string> jsonLoader)
        {
            if (jsonLoader == null) throw new ArgumentNullException(nameof(jsonLoader));

            TbItem = new TbItem(jsonLoader("item"));
        }
    }
}
