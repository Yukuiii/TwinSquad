using System;

namespace TwinSquad.Configs.Cfg
{
    /// <summary>
    /// 物品配置（单条）。
    ///
    /// JsonUtility 兼容性约束：
    /// - 字段必须 public 或加 [SerializeField]
    /// - 枚举以 int 形式落盘
    /// - 不允许 Dictionary 直接序列化（如有需要改用 List）
    /// </summary>
    [Serializable]
    public class ItemConfig
    {
        public int id;                  // 物品 ID（主键）
        public string name;             // 名称
        public ItemType type;           // 类型
        public ItemQuality quality;     // 品质
        public int maxStack;            // 堆叠上限（0 表示不可堆叠）
        public string iconPath;         // 图标资源路径（Addressables key）
        public string description;      // 描述文本
        public int sortOrder;           // 排序权重
        public bool canSell;            // 是否可出售
        public int sellPrice;           // 出售价格
        public bool canUse;             // 是否可使用
        public string useParams;        // 使用参数（JSON 字符串，由 Handler 自行解析）
    }
}
