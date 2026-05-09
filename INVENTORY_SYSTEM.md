# 背包子系统设计文档

> 所属项目：TwinSquad
> 父文档：[ARCHITECTURE.md](./ARCHITECTURE.md)
> 模块定位：游戏数据流中枢，跨 Data / System / UI 三层的核心子系统

---

## 一、模块概述

### 1.1 系统定位

背包系统是二次元卡牌养成游戏的**数据中枢**，几乎所有玩法（战斗掉落、抽卡、任务、商店、养成）的产出与消耗都流经此模块。

### 1.2 核心职责

- 物品的**统一存储与管理**
- 物品的**增删改查**接口
- 物品的**使用逻辑分发**
- 与其他模块的**事件通信**

### 1.3 设计原则

- **统一抽象**：所有可持有资源（物品、货币、材料）抽象为 Item
- **数据驱动**：物品定义全部走配表
- **事件解耦**：通过 EventBus 通知 UI，避免直接依赖
- **开闭原则**：新增物品类型不修改既有代码

---

## 二、架构定位

```
┌─────────────────────────────────────────┐
│  UI 层                                  │
│  ├── BagPanel（背包主界面）             │
│  ├── ItemCell / ItemTooltip             │
│  └── ItemRewardDialog（获得奖励飘窗）   │
├─────────────────────────────────────────┤
│  System 层                              │
│  └── InventoryManager（核心管理器）     │
├─────────────────────────────────────────┤
│  Data 层                                │
│  ├── ItemConfig（静态配表）             │
│  └── InventoryData（运行时数据 + 存档） │
└─────────────────────────────────────────┘
       ↑                    ↓
   配表系统             EventBus 通知
   （Luban）          （UI / 任务 / 红点）
```

---

## 三、物品分类设计

### 3.1 物品类型枚举

```csharp
public enum ItemType {
    Currency,        // 货币（金币、钻石、体力）
    Material,        // 升级 / 突破素材
    Equipment,       // 装备（武器、圣遗物）
    Fragment,        // 角色 / 装备碎片
    ExpItem,         // 经验道具
    Gift,            // 礼包（拆包给一组奖励）
    Quest,           // 任务道具
    Consumable,      // 消耗品（buff 药水）
    Decoration,      // 外观 / 家具
}
```

### 3.2 品质等级

```csharp
public enum ItemQuality {
    N  = 1,
    R  = 2,
    SR = 3,
    SSR = 4,
    UR = 5,
}
```

### 3.3 关于角色卡

**采用方案 A：角色独立系统**

- 角色不进背包，由独立的 `CharacterManager` 管理
- 原因：角色含等级、突破、技能、好感度等复杂数据
- 角色**碎片**（用于召唤角色）走背包，类型为 `Fragment`

---

## 四、数据结构设计

### 4.1 静态配置（来自 Luban 配表）

```csharp
public class ItemConfig {
    public int Id;                  // 物品 ID
    public string Name;             // 名称
    public ItemType Type;           // 类型
    public ItemQuality Quality;     // 品质
    public int MaxStack;            // 堆叠上限（0 表示不可堆叠）
    public string IconPath;         // 图标路径（Addressables）
    public string Description;      // 描述
    public int SortOrder;           // 排序权重
    public bool CanSell;            // 是否可出售
    public int SellPrice;           // 出售价格
    public bool CanUse;             // 是否可使用
    public string UseParams;        // 使用参数（JSON，由对应 Handler 解析）
}
```

### 4.2 运行时数据

```csharp
public class ItemData {
    public int ConfigId;            // 关联 ItemConfig
    public int Count;               // 持有数量
    public long Uid;                // 唯一 ID（不可堆叠物品需要，如装备）
    public Dictionary<string, object> ExtraData;  // 扩展字段
}
```

`ExtraData` 用途：
- 装备的强化等级、附加词条
- 限时道具的过期时间
- 任意自定义字段

### 4.3 背包整体数据

```csharp
public class InventoryData {
    public Dictionary<int, ItemData> StackableItems;     // 可堆叠物品（按 ConfigId 索引）
    public Dictionary<long, ItemData> UniqueItems;       // 不可堆叠物品（按 Uid 索引）
    public long NextUid;                                 // 下一个可用 Uid
}
```

---

## 五、模块目录结构

```
Scripts/Gameplay/Inventory/
├── Data/
│   ├── ItemConfig.cs               # 静态配表数据
│   ├── ItemData.cs                 # 运行时数据
│   ├── InventoryData.cs            # 背包数据集合
│   └── ItemEnums.cs                # 类型 / 品质枚举
├── InventoryManager.cs             # 核心管理器（单例）
├── ItemFactory.cs                  # 创建运行时 Item 实例
├── ItemUseHandler.cs               # 使用逻辑分发器
├── Handlers/                       # 各类物品使用处理器
│   ├── IItemUseHandler.cs          # 处理器接口
│   ├── GiftItemHandler.cs          # 礼包：拆开发奖
│   ├── ExpItemHandler.cs           # 经验药：加角色经验
│   └── ConsumableHandler.cs        # 消耗品：加 buff
└── Events/
    └── InventoryEvents.cs          # 背包相关事件定义
```

---

## 六、核心 API 设计

### 6.1 InventoryManager 接口

```csharp
public class InventoryManager {
    // ===== 基础增删改查 =====
    void AddItem(int configId, int count);
    bool RemoveItem(int configId, int count);
    int GetItemCount(int configId);
    bool HasEnough(int configId, int count);

    // ===== 批量操作 =====
    void AddItems(List<ItemReward> rewards);
    bool ConsumeItems(List<ItemCost> costs);
    bool CanAfford(List<ItemCost> costs);

    // ===== 查询 =====
    List<ItemData> GetItemsByType(ItemType type);
    List<ItemData> GetItemsByQuality(ItemQuality quality);
    ItemData GetItemByUid(long uid);

    // ===== 物品使用 =====
    bool UseItem(int configId, int count = 1);
    bool UseItemByUid(long uid);

    // ===== 持久化 =====
    void Save();
    void Load();
}
```

### 6.2 辅助数据结构

```csharp
public struct ItemReward {
    public int ConfigId;
    public int Count;
}

public struct ItemCost {
    public int ConfigId;
    public int Count;
}
```

---

## 七、与其他模块的交互

### 7.1 数据流图

```
┌──────────────┐
│  抽卡系统    │ ──奖励──→ AddItems()
└──────────────┘
┌──────────────┐
│  战斗掉落    │ ──奖励──→ AddItems()
└──────────────┘
┌──────────────┐
│  任务奖励    │ ──奖励──→ AddItems()
└──────────────┘                        ┌──────────────────┐
                                        │ InventoryManager │
┌──────────────┐                        └──────────────────┘
│  商店购买    │ ──消耗──→ ConsumeItems()        │
│              │ ──获得──→ AddItem()             │
└──────────────┘                                │
┌──────────────┐                                ↓ EventBus
│  角色突破    │ ──消耗──→ ConsumeItems()  ┌──────────────┐
└──────────────┘                          │  UI 刷新     │
┌──────────────┐                          │  红点系统    │
│  合成系统    │ ──消耗──→ ConsumeItems()  │  任务监听    │
│              │ ──产出──→ AddItem()       └──────────────┘
└──────────────┘
```

### 7.2 事件定义

```csharp
public static class InventoryEvents {
    public const string OnItemAdded   = "Inventory.OnItemAdded";
    public const string OnItemRemoved = "Inventory.OnItemRemoved";
    public const string OnItemChanged = "Inventory.OnItemChanged";
    public const string OnItemUsed    = "Inventory.OnItemUsed";
}

public class ItemChangedEvent {
    public int ConfigId;
    public int OldCount;
    public int NewCount;
    public int Delta;
}
```

### 7.3 通信约定

- **写操作**：业务模块直接调用 `InventoryManager` API
- **读操作**：UI 通过 EventBus 监听变化，不主动轮询
- **禁止**：UI 直接持有 InventoryManager 引用做硬编码刷新

---

## 八、特殊设计点

### 8.1 货币统一走背包

**金币、钻石、体力等货币也作为 Item 存在**，类型为 `Currency`。

理由：
- 红点系统、任务奖励、活动奖励的逻辑只需写一套
- 数据持久化、事件通知统一处理
- UI 显示也可复用同一套 ItemCell

避免错误做法：
```csharp
// 不要这么干
class CurrencyManager { ... }
class InventoryManager { ... }
```

### 8.2 装备 / 圣遗物的处理

装备的特殊性：
- **不可堆叠**：每件都有唯一 Uid
- **复杂数据**：强化等级、附加词条、镶嵌孔位

设计建议：
- 底层数据仍存 `InventoryData.UniqueItems`
- 上层封装 `EquipmentManager`，提供穿戴 / 强化 / 分解 API
- `ExtraData` 字段存储装备特有数据：

```csharp
itemData.ExtraData["Level"] = 10;
itemData.ExtraData["Affixes"] = new List<Affix>{...};
```

### 8.3 礼包系统

礼包是**特殊的"奖励容器"**，使用时拆开发放内含物品。

```csharp
// 礼包配置（UseParams 字段）
{
  "rewards": [
    {"id": 1001, "count": 100},
    {"id": 2003, "count": 5}
  ]
}

// GiftItemHandler 处理
public class GiftItemHandler : IItemUseHandler {
    public bool Use(ItemData data, int count) {
        var config = ConfigManager.GetItemConfig(data.ConfigId);
        var rewards = ParseRewards(config.UseParams);
        InventoryManager.Instance.AddItems(rewards);
        return true;
    }
}
```

---

## 九、物品使用：策略模式

### 9.1 设计动机

不同物品使用逻辑差异极大：
- 礼包：拆开发奖
- 经验药：加角色经验
- buff 药：加战斗 buff
- 任务道具：触发任务进度

如果在 `UseItem()` 里写 `switch`，会违反开闭原则。

### 9.2 实现方案

```csharp
public interface IItemUseHandler {
    bool Use(ItemData data, int count);
}

public static class ItemUseHandler {
    private static Dictionary<ItemType, IItemUseHandler> _handlers = new();

    public static void Register(ItemType type, IItemUseHandler handler) {
        _handlers[type] = handler;
    }

    public static bool Use(ItemData data, int count) {
        var config = ConfigManager.GetItemConfig(data.ConfigId);
        if (_handlers.TryGetValue(config.Type, out var handler)) {
            return handler.Use(data, count);
        }
        Debug.LogWarning($"No handler for item type: {config.Type}");
        return false;
    }
}

// 启动时注册
ItemUseHandler.Register(ItemType.Gift, new GiftItemHandler());
ItemUseHandler.Register(ItemType.ExpItem, new ExpItemHandler());
ItemUseHandler.Register(ItemType.Consumable, new ConsumableHandler());
```

新增物品类型只需注册新 Handler，**符合 SOLID 开闭原则**。

---

## 十、UI 层设计

### 10.1 目录结构

```
Scripts/UI/Inventory/
├── BagPanel.cs                 # 背包主界面（Tab 切换）
├── BagPanelPresenter.cs        # 背包业务逻辑
├── ItemCell.cs                 # 单个物品格子
├── ItemTooltip.cs              # 悬浮详情
├── ItemUseDialog.cs            # 使用确认弹窗
└── ItemRewardDialog.cs         # 通用获得奖励飘窗
```

### 10.2 MVP 模式应用

```
View（BagPanel）       ←──绑定数据──→     Presenter（BagPanelPresenter）
       ↑                                          ↑
       └──────── 监听 EventBus ←──────────────────┘
                  OnItemChanged                   ↓
                                            InventoryManager
```

- View 只管展示与输入
- Presenter 监听 InventoryManager 数据变化，刷新 View
- 切忌在 View 里直接调用 `InventoryManager.AddItem()`

### 10.3 通用奖励飘窗

战斗结算、任务领奖、抽卡等多处复用同一组件：

```csharp
public class ItemRewardDialog {
    public void Show(List<ItemReward> rewards);
}
```

---

## 十一、配表设计

### 11.1 ItemConfig 表（Luban）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 物品 ID |
| Name | string | 名称 |
| Type | enum | 类型 |
| Quality | enum | 品质 |
| MaxStack | int | 堆叠上限 |
| IconPath | string | 图标路径 |
| Description | string | 描述文本 |
| SortOrder | int | 排序权重 |
| CanSell | bool | 是否可出售 |
| SellPrice | int | 出售价格 |
| CanUse | bool | 是否可使用 |
| UseParams | string | 使用参数（JSON） |

### 11.2 ID 段规划建议

| ID 段 | 用途 |
|-------|------|
| 1000–1999 | 货币 |
| 10000–19999 | 材料 |
| 20000–29999 | 装备 |
| 30000–39999 | 碎片 |
| 40000–49999 | 经验道具 |
| 50000–59999 | 礼包 |
| 60000–69999 | 任务道具 |
| 70000–79999 | 消耗品 |

---

## 十二、数据持久化

### 12.1 存档格式

- 采用 JSON 序列化（开发期）+ 二进制（上线后）
- 关键字段加 checksum 防作弊

### 12.2 存档时机

- 重要操作后立刻存档（抽卡、购买、突破）
- 主城闲置每 30 秒自动存档
- 退出游戏时强制存档

### 12.3 存档结构示例

```json
{
  "stackableItems": {
    "1001": { "configId": 1001, "count": 99999 },
    "10001": { "configId": 10001, "count": 50 }
  },
  "uniqueItems": {
    "1": {
      "configId": 20001,
      "uid": 1,
      "count": 1,
      "extraData": { "level": 10, "affixes": [...] }
    }
  },
  "nextUid": 2
}
```

---

## 十三、实现路线图

### 阶段 1：基础框架（依赖 P0 系统）
- [ ] ItemConfig 配表导入（Luban）
- [ ] InventoryData 数据结构定义
- [ ] InventoryManager 增删改查 API
- [ ] EventBus 事件接入

### 阶段 2：物品使用系统
- [ ] IItemUseHandler 接口
- [ ] ItemUseHandler 分发器
- [ ] 实现 Gift / Exp / Consumable 三种 Handler

### 阶段 3：UI 层
- [ ] BagPanel 主界面
- [ ] ItemCell 格子组件
- [ ] ItemTooltip 详情悬浮
- [ ] ItemRewardDialog 通用奖励飘窗

### 阶段 4：集成与扩展
- [ ] 战斗掉落接入
- [ ] 抽卡奖励接入
- [ ] 商店购买接入
- [ ] 装备 / 圣遗物子系统
- [ ] 红点系统接入
- [ ] 数据持久化与防作弊

---

## 十四、注意事项与禁忌

### 应当遵循

- 货币统一走 Inventory，不另建 CurrencyManager
- 所有写操作走 InventoryManager，禁止外部直接修改 InventoryData
- UI 通过 EventBus 监听变化，不轮询
- 新增物品类型用策略模式扩展 Handler，不改 InventoryManager
- 不可堆叠物品必须分配 Uid

### 应当避免

- 在 UI 层调用 `InventoryData.StackableItems.Add(...)` 直接改数据
- 在 InventoryManager 中写大段 `switch (itemType)` 业务逻辑
- 用字符串拼接构造物品（应通过 ItemFactory）
- 把角色卡塞进背包（角色独立系统）
- 装备系统与背包系统数据分离存储（应底层统一、上层封装）
- 频繁全量刷新背包 UI（应基于事件局部刷新）

---

## 十五、性能注意点

### 大数据量优化

- 背包物品数量超过 500 时，UI 列表使用**虚拟滚动**（Loop ScrollView）
- 频繁 GetItemsByType 操作建立**类型索引**缓存
- 物品图标采用 Addressables 异步加载 + LRU 缓存

### 事件批处理

批量增加奖励时（如战斗结算掉落 50 件），合并为一次事件：
```csharp
// 错误：触发 50 次 OnItemChanged
foreach (var r in rewards) AddItem(r.ConfigId, r.Count);

// 正确：内部批量处理，最后只触发一次 OnInventoryBatchUpdated
AddItems(rewards);
```

---

## 十六、参考实现

- 蔚蓝档案、战双帕弥什、原神的背包交互可作为体验参考
- 《游戏编程模式》— 策略模式、观察者模式
- 《游戏数值与系统设计》— 物品体系设计

---

> 文档版本：v0.1
> 最后更新：2026-05-09
> 维护原则：随系统迭代持续更新，新增 Handler 类型时同步补充至本文档
