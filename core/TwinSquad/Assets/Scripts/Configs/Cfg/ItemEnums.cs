namespace TwinSquad.Configs.Cfg
{
    /// <summary>
    /// 物品类型。数值与 INVENTORY_SYSTEM.md 一致，配表中 Type 字段填 int。
    /// 注意：枚举值定下后不要随意改动 int 编号，否则旧存档会错位。
    /// </summary>
    public enum ItemType
    {
        Currency   = 0,    // 货币（金币、钻石、体力）
        Material   = 1,    // 升级 / 突破素材
        Equipment  = 2,    // 装备
        Fragment   = 3,    // 角色 / 装备碎片
        ExpItem    = 4,    // 经验道具
        Gift       = 5,    // 礼包
        Quest      = 6,    // 任务道具
        Consumable = 7,    // 消耗品（buff 药）
        Decoration = 8,    // 外观 / 家具
    }

    /// <summary>
    /// 物品品质。配表中 Quality 字段填 int。
    /// </summary>
    public enum ItemQuality
    {
        N   = 1,
        R   = 2,
        SR  = 3,
        SSR = 4,
        UR  = 5,
    }
}
