# 角色卡系统设计文档

> 所属项目：TwinSquad
> 父文档：[ARCHITECTURE.md](./ARCHITECTURE.md)
> 关联文档：[BATTLE_SYSTEM.md](./BATTLE_SYSTEM.md)、[GACHA_SYSTEM.md](./GACHA_SYSTEM.md)、[INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md)
> 系统定位：**养成核心**，承载玩家长期投入与情感连接

---

## 一、模块概述

### 1.1 系统定位

角色卡系统是养成卡牌游戏的**情感与数值核心**，玩家通过抽卡获得角色，通过养成提升角色，通过组队应用于战斗。

参考标杆：原神、崩坏：星穹铁道、战双帕弥什、蔚蓝档案。

### 1.2 核心职责

- 角色数据管理（属性、等级、突破、技能、好感度）
- 角色养成接口（升级、突破、技能升级）
- 装备穿戴管理
- 阵容编辑与战斗输出

### 1.3 设计原则

- **角色独立于背包**：角色不作为 Item，由独立 Manager 管理
- **数据 / 配置分离**：静态信息走配表，运行时数据独立存档
- **属性公式可调整**：所有数值计算公式走配表
- **事件驱动 UI**：所有数据变化通过 EventBus 通知

---

## 二、架构定位

```
┌─────────────────────────────────────────────┐
│  UI 层                                      │
│  ├── CharacterListPanel（角色列表）         │
│  ├── CharacterDetailPanel（详情，多 Tab）   │
│  ├── LevelUpPanel / BreakthroughPanel       │
│  ├── SkillUpgradePanel / EquipPanel         │
│  └── FormationPanel（阵容编辑）             │
├─────────────────────────────────────────────┤
│  Gameplay 层                                │
│  └── CharacterManager（核心管理器）         │
│      ├── LevelSystem（升级）                │
│      ├── BreakthroughSystem（突破）         │
│      ├── SkillSystem（技能升级）            │
│      ├── EquipSystem（装备穿戴）            │
│      ├── FavorSystem（好感度）              │
│      └── FormationSystem（阵容）            │
├─────────────────────────────────────────────┤
│  Data 层                                    │
│  ├── CharacterConfig（静态配置）            │
│  ├── CharacterData（运行时数据）            │
│  ├── LevelExpConfig / BreakthroughConfig    │
│  └── StatsCalculator（属性公式）            │
└─────────────────────────────────────────────┘
        ↓ EventBus
   战斗系统（属性来源）/ 背包系统（消耗材料）
```

---

## 三、核心数据结构

### 3.1 静态配置（CharacterConfig）

```csharp
public class CharacterConfig {
    public int Id;                       // 角色 ID
    public string Name;                  // 名称
    public string CodeName;              // 代号
    public ItemQuality Quality;          // 品质（SR / SSR / UR）
    public Element Element;              // 元素属性（火/水/雷...）
    public WeaponType WeaponType;        // 武器类型
    public CharacterRole Role;           // 定位（输出/辅助/坦克）

    // 基础属性（1 级、未突破）
    public BaseStats BaseStats;

    // 成长系数（每级增长）
    public BaseStats GrowthStats;

    // 资源
    public string PortraitPath;          // 立绘
    public string ModelPath;             // 战斗模型
    public string AvatarPath;            // 头像

    // 技能与突破
    public int[] SkillIds;               // 技能 ID 列表
    public int BreakthroughTableId;      // 突破表 ID
}
```

### 3.2 运行时数据（CharacterData）

```csharp
public class CharacterData {
    public int ConfigId;                 // 关联 CharacterConfig
    public long Uid;                     // 唯一 ID（同角色多份时区分）
    public int Level;                    // 当前等级
    public int Exp;                      // 当前经验
    public int Breakthrough;             // 突破阶数（0–6）
    public int FavorLevel;               // 好感度等级
    public int FavorExp;                 // 好感度经验

    public Dictionary<int, int> SkillLevels;     // 技能 ID → 技能等级
    public Dictionary<EquipSlot, long> EquippedItems;  // 装备槽 → 装备 Uid
    public List<int> Constellations;     // 命之座 / 星魂（重复角色解锁）

    public long ObtainedTime;            // 获取时间
    public bool IsFavorite;              // 是否标记收藏
}
```

### 3.3 战斗属性（FinalStats）

```csharp
public class FinalStats {
    public float HP;
    public float Attack;
    public float Defense;
    public float Speed;
    public float CritRate;
    public float CritDamage;
    public float ElementDamage;          // 元素增伤
    public float Resistance;             // 元素抗性
    // ... 其他次级属性
}
```

---

## 四、属性公式

### 4.1 基础属性计算

```
最终属性 = (基础属性 + 等级成长 × 等级 + 突破加成)
        × (1 + 装备百分比加成)
        + 装备固定加成
        + 好感度 buff
```

### 4.2 公式拆分

```csharp
public static class StatsCalculator {
    public static FinalStats Calculate(CharacterData data) {
        var config = ConfigManager.GetCharacterConfig(data.ConfigId);

        // 1. 基础值（包含等级成长）
        var stats = config.BaseStats + config.GrowthStats * (data.Level - 1);

        // 2. 突破加成
        stats += GetBreakthroughBonus(config, data.Breakthrough);

        // 3. 装备加成
        stats = ApplyEquipmentBonus(stats, data.EquippedItems);

        // 4. 好感度加成
        stats = ApplyFavorBonus(stats, data.FavorLevel);

        // 5. 命之座效果（特殊加成）
        stats = ApplyConstellations(stats, data.Constellations);

        return stats;
    }
}
```

### 4.3 公式走配表

所有系数（成长率、突破倍率、好感度增益）必须走配表，便于数值平衡调整：

```
LevelExpConfig：每级所需经验
BreakthroughConfig：每阶突破属性加成
FavorBonusConfig：每级好感度加成
SkillLevelConfig：技能各等级数值
```

---

## 五、核心子系统

### 5.1 LevelSystem（升级）

```csharp
public class LevelSystem {
    public bool LevelUp(long charUid);                    // 单次升级
    public bool LevelUpToMax(long charUid);               // 一键升满（消耗到不足）
    public bool AddExp(long charUid, int exp);            // 通过经验药加经验
    public int GetMaxLevel(int characterId, int breakthrough);  // 当前突破阶可达上限
}
```

设计要点：
- 等级有**突破段限制**（每个突破阶最高 20/40/60/70/80/90）
- 升级消耗经验药 + 金币
- 经验药可堆叠使用（剩余经验进入存档）

### 5.2 BreakthroughSystem（突破）

```csharp
public class BreakthroughSystem {
    public bool CanBreakthrough(long charUid);
    public bool Breakthrough(long charUid);
    public List<ItemCost> GetBreakthroughCost(int characterId, int targetStage);
}
```

突破前置条件：
- 当前等级达到突破要求
- 持有足够材料（角色专属突破材料 + 通用材料）
- 通常突破阶数对应 1/20/40/50/60/70 级解锁

### 5.3 SkillSystem（技能升级）

```csharp
public class SkillUpgradeSystem {
    public bool CanUpgradeSkill(long charUid, int skillId);
    public bool UpgradeSkill(long charUid, int skillId);
    public int GetMaxSkillLevel(int skillId, int breakthrough);
}
```

技能升级特点：
- 通常每个角色 3–4 个技能
- 技能等级上限受角色突破阶数限制
- 不同技能消耗材料不同（普攻 / 战技 / 大招）

### 5.4 EquipSystem（装备穿戴）

```csharp
public enum EquipSlot {
    Weapon,
    Helmet,
    Armor,
    Boots,
    Accessory,
}

public class EquipSystem {
    public bool Equip(long charUid, long itemUid, EquipSlot slot);
    public bool Unequip(long charUid, EquipSlot slot);
    public List<ItemData> GetEquipped(long charUid);
}
```

注意：
- 装备 Uid 来自 InventoryManager
- 装备已被穿戴时不能在背包中使用 / 出售
- 切换装备触发属性重算

### 5.5 FavorSystem（好感度，可选）

```csharp
public class FavorSystem {
    public bool AddFavorExp(long charUid, int exp);
    public bool LevelUpFavor(long charUid);              // 满经验时升级
    public bool UnlockSkin(long charUid, int skinId);    // 解锁皮肤 / 立绘
}
```

好感度影响：
- 解锁角色立绘 / 故事 / 语音
- 提供小幅属性加成
- 解锁专属皮肤

### 5.6 FormationSystem（阵容）

```csharp
public class Formation {
    public int FormationId;              // 阵容编号（玩家可保存多套）
    public string Name;
    public List<long> CharacterUids;     // 上阵角色（顺序代表位置）
    public int LeaderIndex;              // 队长位置
}

public class FormationSystem {
    public Formation GetCurrent();
    public void SetFormation(Formation formation);
    public bool ValidateFormation(Formation f);          // 校验合法性（重复角色等）
    public List<Formation> GetSavedFormations();
}
```

战斗启动时：
- BattleManager 从 FormationSystem 获取当前阵容
- 实例化对应角色 Entity
- 注入战斗属性

---

## 六、CharacterManager 核心 API

```csharp
public class CharacterManager {
    // ===== 增删 =====
    void AddCharacter(int configId);                     // 抽卡获得 / 任务奖励
    bool HasCharacter(int configId);                     // 是否拥有
    void HandleDuplicate(int configId);                  // 重复获得处理（命之座 / 碎片）

    // ===== 查询 =====
    CharacterData GetCharacter(long uid);
    CharacterData GetCharacterByConfigId(int configId);
    List<CharacterData> GetAllCharacters();
    List<CharacterData> GetCharactersByElement(Element e);

    // ===== 属性 =====
    FinalStats GetFinalStats(long uid);                  // 计算最终属性
    void RefreshStats(long uid);                         // 强制重算（装备变化时）

    // ===== 持久化 =====
    void Save();
    void Load();
}
```

---

## 七、与其他系统交互

### 7.1 输入依赖

- **GachaSystem**：抽卡获得新角色
- **InventoryManager**：消耗升级 / 突破材料
- **ConfigManager**：静态配置数据

### 7.2 输出

- **BattleSystem**：提供战斗属性、技能、模型
- **FormationSystem**：阵容数据
- **TaskSystem**：触发养成相关任务进度

### 7.3 事件定义

```csharp
public static class CharacterEvents {
    public const string OnCharacterObtained    = "Character.OnObtained";
    public const string OnCharacterLevelUp     = "Character.OnLevelUp";
    public const string OnCharacterBreakthrough = "Character.OnBreakthrough";
    public const string OnSkillUpgraded        = "Character.OnSkillUpgraded";
    public const string OnEquipChanged         = "Character.OnEquipChanged";
    public const string OnFavorLevelUp         = "Character.OnFavorLevelUp";
    public const string OnConstellationUnlocked = "Character.OnConstellationUnlocked";
}
```

### 7.4 重复角色处理（来自抽卡）

```csharp
public void OnDuplicateCharacterObtained(int configId) {
    var existing = GetCharacterByConfigId(configId);

    // 1. 检查命之座是否已满
    if (existing.Constellations.Count < MaxConstellations) {
        UnlockConstellation(existing);
    } else {
        // 2. 命之座已满 → 转换为通用代币
        var rewardItemId = GetDuplicateRewardItemId(configId);
        var count = GetDuplicateRewardCount(configId);
        InventoryManager.Instance.AddItem(rewardItemId, count);
    }
}
```

---

## 八、UI 层设计

### 8.1 目录结构

```
Scripts/UI/Character/
├── CharacterListPanel.cs           # 角色列表（筛选 / 排序）
├── CharacterDetailPanel.cs         # 详情（多 Tab）
├── Tabs/
│   ├── AttributesTab.cs            # 属性面板
│   ├── SkillsTab.cs                # 技能列表
│   ├── EquipmentTab.cs             # 装备槽
│   ├── ConstellationTab.cs         # 命之座
│   └── StoryTab.cs                 # 故事 / 好感度
├── LevelUpDialog.cs                # 升级界面
├── BreakthroughDialog.cs           # 突破界面
├── SkillUpgradeDialog.cs           # 技能升级
└── FormationPanel.cs               # 阵容编辑
```

### 8.2 关键设计点

#### 角色详情页（典型 5–7 Tab）
- 属性 Tab：完整属性面板（含 buff 来源拆解）
- 技能 Tab：技能列表 + 升级界面
- 装备 Tab：5 个装备槽 + 当前穿戴
- 命之座 Tab：6 个命星解锁状态
- 故事 Tab：好感度等级 + 解锁的故事 / 语音
- 立绘 Tab：可切换不同皮肤

#### MVP 模式应用
- 数据多、面板复杂，**强烈推荐 MVP + 数据绑定**
- View 只管展示，Presenter 监听 EventBus 刷新
- 切忌在 View 里写大量业务逻辑

#### 阵容编辑器
- 拖拽式角色上阵
- 实时显示阵容总战力
- 元素共鸣（同元素 2 人触发 buff）显示
- 阵容预设保存（多套常用阵容）

---

## 九、配表设计

### 9.1 角色基础配置（CharacterConfig）

字段：ID、名称、品质、元素、武器类型、定位、基础属性、成长属性、技能 ID、突破表 ID、资源路径。

### 9.2 等级经验表（LevelExpConfig）

| 等级 | 所需经验 | 突破要求 |
|------|---------|---------|
| 1–20 | 累积值 | 0 突破 |
| 21–40 | 累积值 | 需 1 突 |
| 41–60 | 累积值 | 需 2 突 |
| ...

### 9.3 突破表（BreakthroughConfig）

字段：角色 ID、突破阶数、所需材料列表、属性加成。

### 9.4 技能升级表（SkillLevelConfig）

字段：技能 ID、技能等级、所需材料、伤害倍率、CD 等数值。

### 9.5 命之座表（ConstellationConfig）

字段：角色 ID、命星序号、效果描述、属性加成 / 技能改造。

### 9.6 好感度表（FavorConfig）

字段：等级、所需经验、解锁内容（立绘 / 故事 / 语音 / 属性加成）。

---

## 十、实现路线图

### 阶段 1：基础数据
- [ ] CharacterConfig 配表导入
- [ ] CharacterData 运行时数据结构
- [ ] CharacterManager 增删查接口
- [ ] StatsCalculator 基础属性计算

### 阶段 2：核心养成
- [ ] LevelSystem 升级
- [ ] BreakthroughSystem 突破
- [ ] SkillUpgradeSystem 技能升级
- [ ] 重复角色处理（命之座）

### 阶段 3：装备与阵容
- [ ] EquipSystem 装备穿戴
- [ ] FormationSystem 阵容管理
- [ ] 战斗系统对接（角色属性注入）

### 阶段 4：UI 与扩展
- [ ] 角色列表 + 详情多 Tab
- [ ] 升级 / 突破 / 技能升级 UI
- [ ] 阵容编辑器
- [ ] FavorSystem 好感度系统
- [ ] 立绘 / 故事解锁

### 阶段 5：润色
- [ ] 数据持久化
- [ ] 战力计算（综合评分）
- [ ] 角色筛选 / 排序
- [ ] 红点提示（可升级 / 可突破）

---

## 十一、注意事项与禁忌

### 应当遵循

- 角色数据独立存档，不混入背包
- 所有属性计算走 StatsCalculator，不分散到各处
- 装备数据底层走 InventoryManager，业务逻辑由 EquipSystem 封装
- 重复角色转化规则配表化
- 属性变化通过 EventBus 通知（含战斗中）
- 阵容更改时校验合法性（去重、人数限制）

### 应当避免

- 把角色作为 Item 塞进背包
- 在多处计算属性（应统一走 StatsCalculator）
- 等级 / 突破上限写死在代码（应走配表）
- 装备穿戴后未触发属性重算
- 角色详情页用单一 GameObject 处理所有 Tab（应拆分子 Panel）
- 阵容数据与战斗逻辑直接耦合（通过事件）

---

## 十二、性能与体验

### 12.1 角色列表性能

- 角色数量超过 50 时使用**虚拟滚动**
- 立绘 / 头像走 Addressables 异步加载 + LRU 缓存
- 列表筛选时本地缓存结果

### 12.2 属性计算缓存

- 属性计算结果缓存，仅在数据变化时重算
- 触发重算的事件：升级、突破、技能升级、装备变化、命之座解锁

### 12.3 数据存档

- 角色数据序列化为 JSON / 二进制
- 关键数据（等级、命之座）加 checksum 防作弊
- 数据版本号管理（便于后续迁移）

---

## 十三、参考资料

- 原神 / 星穹铁道角色养成系统
- 战双帕弥什意识系统
- 蔚蓝档案学生养成
- 《游戏数值与系统设计》

---

> 文档版本：v0.1
> 最后更新：2026-05-09
> 维护原则：随系统迭代持续更新，新增子系统（如皮肤、专属武器）时同步扩展本文档
