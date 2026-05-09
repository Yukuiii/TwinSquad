# 抽卡系统设计文档

> 所属项目：TwinSquad
> 父文档：[ARCHITECTURE.md](./ARCHITECTURE.md)
> 关联文档：[CHARACTER_SYSTEM.md](./CHARACTER_SYSTEM.md)、[INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md)
> 系统定位：**核心商业化模块**，关乎玩家留存与付费转化

---

## 一、模块概述

### 1.1 系统定位

抽卡是二次元卡牌游戏的**核心商业化与角色获取通道**，承载长线运营节奏。

参考标杆：
- **原神**：双 50/50 + 大保底
- **崩坏：星穹铁道**：UP 池 + 武器池分离
- **明日方舟**：累计保底 + 倒计时
- **蔚蓝档案**：固定保底 + 三选一

### 1.2 核心特征

- 多种卡池类型（常驻、限定、新手、武器等）
- 严格的概率与保底机制
- 抽卡演出（金光、彩光、SSR 演出）
- 抽卡历史与记录
- 强防作弊保障（关键数据服务器校验）

### 1.3 设计原则

- **概率配置化**：所有概率走配表，便于策划调整
- **保底独立模块**：每个池子独立保底状态
- **结果可追溯**：完整记录每次抽卡历史
- **演出与逻辑分离**：抽卡结果先确定，演出仅做表现

---

## 二、架构定位

```
┌─────────────────────────────────────────────┐
│  UI 层                                      │
│  ├── GachaPanel（抽卡主界面）               │
│  ├── GachaAnimationPlayer（演出）           │
│  ├── GachaResultDialog（结果展示）          │
│  └── GachaHistoryPanel（抽卡历史）          │
├─────────────────────────────────────────────┤
│  Gameplay 层                                │
│  └── GachaManager（抽卡总控）               │
│      ├── PoolManager（卡池管理）            │
│      ├── ProbabilityCalculator（概率计算）  │
│      ├── PitySystem（保底系统）             │
│      └── GachaHistory（历史记录）           │
├─────────────────────────────────────────────┤
│  Data 层                                    │
│  ├── GachaPoolConfig（卡池配置）            │
│  ├── GachaItemConfig（卡池物品池）          │
│  └── GachaPlayerData（玩家抽卡数据）        │
└─────────────────────────────────────────────┘
        ↓ EventBus
   背包系统（消耗+获得） / 角色系统（角色入库）
```

---

## 三、卡池类型设计

### 3.1 卡池类型枚举

```csharp
public enum GachaPoolType {
    Beginner,        // 新手池（限抽次数）
    Standard,        // 常驻池
    LimitedChar,     // 限定角色池（UP）
    LimitedWeapon,   // 限定武器池
    Themed,          // 主题活动池
}
```

### 3.2 卡池关键属性

| 属性 | 说明 |
|------|------|
| 持续时间 | 限定池有开始 / 结束时间 |
| 消耗物品 | 通常用代币（如"星辉"）抽卡 |
| 单抽 / 十连 | 价格与概率可能不同 |
| UP 角色 | 限定池突出的高概率角色 |
| 保底次数 | 抽到保底前的最大次数 |
| 大保底机制 | 是否有 50/50 等机制 |

---

## 四、概率与保底机制

### 4.1 基础概率（参考原神模式）

| 品质 | 基础概率 | 综合概率（含保底） |
|------|---------|-------------------|
| SSR  | 0.6%    | 1.6%              |
| SR   | 5.1%    | 13%               |
| R    | 94.3%   | 85.4%             |

### 4.2 保底机制（典型实现）

#### 小保底
- 抽 90 次必出 SSR（90 是经验值，按项目调）
- 第 75 抽起概率递增（软保底）

#### 大保底（UP 池）
- 第一次出的 SSR 不一定是 UP 角色（50/50）
- 若非 UP，下一个 SSR 必为 UP（大保底）

#### SR 保底
- 每 10 抽必出 1 个 SR 及以上

### 4.3 保底数据结构

```csharp
public class PityState {
    public int PoolId;
    public int SSRCounter;           // 距离上次 SSR 的次数
    public int SRCounter;            // 距离上次 SR 的次数
    public bool IsBigPityActive;     // 大保底是否激活（上次 SSR 非 UP）
}
```

### 4.4 概率计算流程

```
1. 检查 SSR 保底
   if (SSRCounter >= MaxSSRPity) → 强制出 SSR
   else if (SSRCounter >= SoftPityStart) → 概率递增
   else → 基础概率

2. 滚动随机数判定品质（SSR / SR / R）

3. 如果命中 SSR：
   a. 判定是否为 UP 角色
   b. 若上次大保底激活 → 必为 UP
   c. 否则按 50/50 判定
   d. 重置或保留大保底状态

4. 在对应品质池中按权重随机选定具体物品
```

### 4.5 保底必须要做对的事

- **保底独立**：常驻池与限定池保底**不互通**
- **状态持久化**：保底次数必须存档（含大保底标记）
- **服务器校验**：联网游戏中保底数据由服务器维护
- **测试用例覆盖**：边界情况（第 89 抽、第 90 抽、连续大保底）必须自动化测试

---

## 五、数据结构设计

### 5.1 卡池配置（GachaPoolConfig）

```csharp
public class GachaPoolConfig {
    public int Id;
    public string Name;
    public GachaPoolType Type;
    public long StartTime;           // 限定池开始时间
    public long EndTime;             // 限定池结束时间
    public int CostItemId;           // 消耗物品 ID
    public int CostPerSingle;        // 单抽消耗
    public int CostPerTen;           // 十连消耗
    public int MaxSSRPity;           // SSR 硬保底
    public int SoftPityStart;        // SSR 软保底起点
    public int MaxSRPity;            // SR 保底
    public bool HasUpMechanism;      // 是否有大保底
    public List<int> UpItemIds;      // UP 物品列表
    public int ItemPoolId;           // 关联物品池
}
```

### 5.2 物品池（GachaItemPool）

```csharp
public class GachaItemPool {
    public int PoolId;
    public List<GachaItemEntry> SSRItems;
    public List<GachaItemEntry> SRItems;
    public List<GachaItemEntry> RItems;
}

public class GachaItemEntry {
    public int ItemId;
    public int Weight;               // 权重（用于该品质内随机）
    public bool IsUp;                // 是否为 UP
}
```

### 5.3 玩家抽卡数据

```csharp
public class GachaPlayerData {
    public Dictionary<int, PityState> PityStates;     // 每个池子保底状态
    public Dictionary<int, int> TotalPullCount;       // 每个池子累计抽数
    public List<GachaRecord> History;                 // 抽卡历史
}

public class GachaRecord {
    public int PoolId;
    public int ItemId;
    public ItemQuality Quality;
    public long Timestamp;
    public bool IsPity;              // 是否为保底触发
}
```

---

## 六、抽卡流程

```
1. 玩家点击抽卡按钮（单抽 / 十连）
   ↓
2. 前置检查
   - 卡池是否在有效期
   - 消耗物品是否充足（InventoryManager.HasEnough）
   ↓
3. 扣除消耗
   - InventoryManager.ConsumeItems(cost)
   ↓
4. 概率计算（可能多次）
   for (i = 0; i < pullCount; i++) {
     - PityState 更新
     - 品质判定（含软硬保底）
     - 物品判定（含 UP 机制）
     - 记录 GachaRecord
   }
   ↓
5. 物品入库
   - 角色 → CharacterManager（含已有角色转碎片逻辑）
   - 武器 / 道具 → InventoryManager.AddItems()
   ↓
6. 触发演出
   - 单抽：播放对应品质演出
   - 十连：先播放整体演出，再展示物品列表
   ↓
7. 历史记录持久化
   - SaveManager.Save()
   - 通过 EventBus 通知 UI 刷新
```

---

## 七、演出系统设计

### 7.1 演出分级

| 抽卡结果 | 演出表现 |
|---------|---------|
| 全 R | 蓝光、平淡过场 |
| 含 SR | 紫光、中等演出 |
| 含 SSR | 金光、长演出（5–8 秒） |
| SSR + UP | 彩光、专属演出（8–12 秒） |

### 7.2 演出与逻辑分离

**重要原则：**抽卡结果在调用 `GachaManager.Pull()` 时已确定，演出只是**回放结果**。

```csharp
// 错误：演出过程中决定结果
gachaAnimation.OnLightShow += () => {
    var result = RandomPick();  // 不要这样
};

// 正确：先算结果，再演出
var results = GachaManager.Pull(poolId, count);
gachaAnimationPlayer.Play(results);
```

理由：
- 玩家可跳过演出，逻辑必须独立
- 联网游戏中结果由服务器返回，客户端只负责演出
- 防作弊：演出层无法影响结果

### 7.3 跳过逻辑

- 玩家点击屏幕跳过演出
- 直接展示结果列表
- 重复物品高亮显示（"NEW" / "已拥有"）

---

## 八、与其他系统交互

### 8.1 输入依赖

- **InventoryManager**：检查 + 消耗抽卡代币
- **ConfigManager**：卡池配置、物品池
- **CharacterManager**：判断角色是否已拥有

### 8.2 输出

- **InventoryManager**：道具 / 武器入库
- **CharacterManager**：新角色入库
- **TaskManager**：累计抽卡数触发任务进度

### 8.3 事件定义

```csharp
public static class GachaEvents {
    public const string OnPullStart       = "Gacha.OnPullStart";
    public const string OnPullComplete    = "Gacha.OnPullComplete";
    public const string OnSSRObtained     = "Gacha.OnSSRObtained";
    public const string OnPityTriggered   = "Gacha.OnPityTriggered";
}
```

### 8.4 重复角色处理

抽到已拥有角色时的处理（参考原神 / 星铁）：
- 转换为该角色的**专属碎片**（命星 / 重复"星魂"）
- 或转换为通用代币（如"无名星辉"）
- 数量配置走配表，不写死

```csharp
public class DuplicateRule {
    public int CharacterId;
    public int FragmentItemId;       // 转换后的碎片 ID
    public int FragmentCount;        // 转换数量
}
```

---

## 九、UI 层设计

```
Scripts/UI/Gacha/
├── GachaPanel.cs               # 主界面（卡池切换、抽卡按钮）
├── GachaPanelPresenter.cs      # 业务逻辑
├── GachaAnimationPlayer.cs     # 演出播放器
├── GachaResultDialog.cs        # 抽卡结果展示
├── GachaHistoryPanel.cs        # 抽卡历史
└── PityProgressBar.cs          # 保底进度条
```

### 9.1 关键 UI 要素

- **卡池预览**：UP 角色立绘、概率公示
- **保底进度**：距离 SSR 还有多少抽（玩家关键决策依据）
- **十连按钮**：通常是主要付费入口，需要醒目设计
- **抽卡历史**：合规要求，必须提供
- **概率公示**：法规要求（中国大陆游戏必须）

---

## 十、配表设计

### 10.1 卡池表（GachaPoolConfig）

字段：池子 ID、类型、名称、起止时间、消耗、保底参数、UP 列表、物品池关联。

### 10.2 物品池表（GachaItemPool）

字段：池子 ID、品质、物品 ID、权重、是否 UP。

### 10.3 重复转换表（DuplicateRule）

字段：角色 ID、转换碎片 ID、数量。

### 10.4 演出表（GachaAnimationConfig）

字段：演出 ID、触发条件、动画资源、时长。

---

## 十一、防作弊设计

### 11.1 客户端策略（单机）

- 抽卡数据加密存档
- 关键字段（保底计数、抽卡次数）加 checksum
- 防止存档篡改

### 11.2 联网策略（在线游戏）

- **抽卡结果由服务器决定**，客户端仅显示
- 服务器维护保底状态、抽卡历史
- 客户端发起请求 → 服务器扣费 + 计算 → 返回结果
- 服务器记录每次抽卡日志（用于客诉、合规）

### 11.3 合规要求

- **概率公示**：所有可获得物品概率必须公开
- **抽卡历史**：玩家可查看至少 90 天历史
- **未成年人保护**：付费抽卡需配合实名认证

---

## 十二、实现路线图

### 阶段 1：核心抽卡逻辑
- [ ] GachaPoolConfig 配表导入
- [ ] PityState 数据结构
- [ ] ProbabilityCalculator 基础概率
- [ ] 单抽 / 十连接口

### 阶段 2：保底与 UP
- [ ] 软硬保底实现
- [ ] 大保底（50/50）机制
- [ ] 重复角色转碎片

### 阶段 3：UI 与演出
- [ ] GachaPanel 主界面
- [ ] 抽卡演出（金光 / 彩光）
- [ ] 结果展示与跳过
- [ ] 抽卡历史面板
- [ ] 保底进度显示

### 阶段 4：合规与防作弊
- [ ] 概率公示页
- [ ] 抽卡历史持久化（90 天+）
- [ ] 关键数据加密
- [ ] 防作弊校验

---

## 十三、注意事项与禁忌

### 应当遵循

- 抽卡概率全部走配表
- 保底状态必须按池子独立维护
- 演出与逻辑完全分离
- 重复角色转碎片规则配置化
- 概率必须公开展示
- 关键数据持久化与加密

### 应当避免

- 在演出过程中调用随机函数决定结果
- 不同卡池共享保底状态
- 抽卡逻辑与 UI 耦合（应通过 EventBus）
- 关键概率写死在代码（应配表）
- 忽略边界测试（90 抽、连大保底、池子切换）
- 抽卡时不扣费先发奖（顺序错误导致回滚困难）

---

## 十四、测试要点

### 14.1 自动化测试用例

- 第 90 抽必出 SSR
- 软保底曲线验证（75–89 抽概率递增）
- 大保底激活后下一个 SSR 必为 UP
- 50/50 长期统计概率收敛
- 池子切换不影响其他池子保底
- 抽卡消耗物品不足时应失败

### 14.2 长期统计

- 模拟万次抽卡，验证综合 SSR 概率（通常 1.6%）
- 验证 UP 角色长期出货率（应约为 SSR 的 50%）

---

## 十五、参考资料

- 原神 / 星穹铁道抽卡系统拆解
- 国家新闻出版总署关于游戏概率公示要求
- 《Probability and Statistics for Games》

---

> 文档版本：v0.1
> 最后更新：2026-05-09
> 维护原则：法规要求变更时优先更新合规章节
