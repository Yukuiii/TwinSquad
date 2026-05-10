# 战斗系统设计文档

> 所属项目：TwinSquad
> 父文档：[ARCHITECTURE.md](./ARCHITECTURE.md)
> 关联文档：[CHARACTER_SYSTEM.md](./CHARACTER_SYSTEM.md)、[INVENTORY_SYSTEM.md](./INVENTORY_SYSTEM.md)
> 战斗类型：**割草型 ARPG**（同屏 100–300 实体、技能清屏、爽快感优先）

---

## 一、模块概述

> **实现状态：核心战斗循环已完成，可运行 Demo。部分子系统（Skill / Drop / Buff / AI 策略）待实现。**

### 1.1 系统定位

战斗系统是游戏的**核心玩法循环**，承载所有关卡、活动、Boss 战。

参考标杆：弹壳特攻队、向日葵骑士、战双帕弥什。

### 1.2 核心特征

- 同屏敌人数量 100–300
- 玩家以技能 / AOE 清场为主
- 伤害飘字、特效爆炸感
- 单局时长 3–10 分钟
- 关卡末段结算掉落

### 1.3 设计原则

- **数据驱动**：敌人、技能、关卡、波次全部走配表
- **池化先行**：所有运行时频繁创建的对象进对象池
- **事件驱动**：伤害、死亡、掉落通过事件解耦
- **可扩展 AI**：策略模式分离敌人行为

---

## 二、架构定位

```
┌─────────────────────────────────────────────┐
│  UI 层                                      │
│  └── BattleHUD / SkillBar / DamageNumber    │
├─────────────────────────────────────────────┤
│  Gameplay 层                                │
│  ├── BattleManager（战斗总控）              │
│  ├── EntityManager（实体集合）              │
│  ├── EnemySpawner（刷怪）                   │
│  ├── SkillSystem（技能）                    │
│  ├── DamageSystem（伤害计算）               │
│  ├── BuffSystem（增益/减益）                │
│  ├── AISystem（敌人 AI）                    │
│  └── DropSystem（掉落）                     │
├─────────────────────────────────────────────┤
│  Data 层                                    │
│  ├── EnemyConfig / SkillConfig              │
│  ├── StageConfig / WaveConfig               │
│  └── BattleContext（战斗运行时上下文）      │
└─────────────────────────────────────────────┘
        ↓ EventBus
   背包系统（掉落入库）/ 角色系统（属性来源）
```

---

## 三、核心子系统

### 3.1 BattleManager（战斗总控）

职责：
- 战斗生命周期管理（启动 / 进行 / 暂停 / 结算 / 退出）
- 战斗状态机（StateMachine）
- 协调各子系统初始化与销毁

```csharp
public enum BattleState {
    Idle,         // 等待开始 ✅
    Fighting,     // 战斗中 ✅
    Victory,      // 胜利 ✅
    Defeat,       // 失败 ✅
    // Loading,   // 加载关卡资源 ⏳
    // Ready,     // 准备阶段（倒计时）⏳
    // Paused,    // 暂停 ⏳
    // Settling,  // 结算中 ⏳
}
```

### 3.2 EntityManager（实体集合）✅ 基础版已实现

统一管理战斗中所有实体：玩家、敌人、子弹、可拾取物。

```csharp
// 当前实际实现
public abstract class BattleEntity : MonoBehaviour {
    [SerializeField] protected int maxHP = 100;
    [SerializeField] protected EntityCamp camp = EntityCamp.Neutral;

    public int MaxHP { get; }
    public int CurrentHP { get; }
    public EntityCamp Camp { get; }
    public bool IsDead { get; }

    public void TakeDamage(DamageInfo info);    // 内联伤害计算 + 事件发布
    protected virtual void OnDamaged();
    protected virtual void OnDeath();

    // EntityCamp 枚举：Player / Enemy / Neutral
    // DamageInfo 结构体：Source(BattleEntity), Damage(int), IsCritical(bool)
}
```

**当前限制**（待后续迭代）：
- 无独立 EntityType 枚举（通过类继承区分 PlayerController / EnemyController / Bullet）
- 无 BuffContainer（BuffSystem 待实现）
- 无 EntityId（当前通过引用标识）
- 敌人查找使用 `FindObjectsByType`，未做空间分区优化

注意：
- 所有 Entity 通过 `EntityManager.Spawn/Despawn` 进出战场，不直接 Instantiate
- 内部用 ObjectPool 复用

### 3.3 EnemySpawner（刷怪系统）✅ 基础版已实现

按**固定间隔**在当前正交相机视口外随机生成敌人。

```csharp
// 当前实际实现
public class EnemySpawner : MonoBehaviour {
    public IEnumerator RunBattle();                         // 协程间隔生成
    private Vector3 GetRandomPositionOutsideScreen(Camera); // 屏幕外随机位置（XY 平面）
}
// 配置：spawnCamera / spawnScreenPadding / spawnInterval / autoStart
// 自动预热对象池：PoolManager.Prewarm(enemyPrefab, count)
```

**当前限制**：仅支持固定间隔 + 屏幕边缘随机生成。多波次、曲线模式、触发模式待实现。

```csharp
// 规划中的完整配置（待实现）
public class WaveConfig {
    public int WaveId;
    public float StartTime;          // 波次开始时间
    public float Duration;           // 持续时间
    public List<EnemyGroup> Groups;  // 敌人组
}

public class EnemyGroup {
    public int EnemyId;              // 敌人配置 ID
    public int Count;                // 数量
    public SpawnPattern Pattern;     // 生成模式（圆形 / 直线 / 随机）
    public float Interval;           // 生成间隔
}
```

刷怪模式：
- **波次模式**：固定时间出现固定敌人（适合关卡）
- **曲线模式**：按时间曲线动态调整（适合 Roguelike）
- **触发模式**：玩家触发区域 / 击杀 Boss 触发下一波

### 3.4 SkillSystem（技能系统）

**核心数据结构：**

```csharp
public class SkillConfig {
    public int Id;
    public string Name;
    public SkillType Type;           // 主动 / 被动 / 大招
    public float Cooldown;
    public float Range;
    public DamageFormula Formula;    // 伤害公式
    public List<SkillEffect> Effects; // 命中效果（伤害、buff、击退）
    public string VfxPath;           // 特效资源
    public string TargetType;        // 单体 / AOE / 直线 / 扇形
}
```

**释放流程：**
1. 触发条件检查（CD、能量、距离）
2. 索敌（按 TargetType）
3. 播放前摇 / 特效
4. 命中判定 → 触发 Effect 列表
5. 进入冷却

### 3.5 DamageSystem（伤害计算）

**伤害信息结构：**

```csharp
public class DamageInfo {
    public BattleEntity Source;      // 伤害来源
    public BattleEntity Target;      // 受击目标
    public int SkillId;              // 技能 ID
    public DamageType Type;          // 物理 / 元素 / 真实
    public float BaseDamage;         // 基础伤害
    public float FinalDamage;        // 最终伤害（计算后）
    public bool IsCritical;          // 是否暴击
    public bool IsDodged;            // 是否被闪避
}
```

**伤害公式（典型卡牌养成）：**

```
最终伤害 = 基础伤害
         × (1 - 防御减伤)
         × 元素增伤
         × 暴击倍率
         × buff 增伤系数
```

注意：
- 公式应**走配表**，不写死代码
- 暴击 / 闪避先于减伤计算
- 真实伤害无视防御

### 3.6 BuffSystem（buff/debuff）

```csharp
public class Buff {
    public int Id;
    public BuffType Type;            // 增伤 / 减伤 / 控制 / DoT
    public float Duration;
    public int StackCount;           // 当前层数
    public int MaxStack;             // 最大层数
    public StackPolicy Policy;       // 叠加规则
}
```

叠加规则（StackPolicy）：
- **Replace**：覆盖刷新时间
- **Stack**：层数 +1
- **Refresh**：刷新时间不叠加
- **Independent**：独立计时

### 3.7 AISystem（敌人 AI）

采用**状态机 + 行为树混合**：
- 简单杂兵：状态机（巡逻 / 追击 / 攻击）
- Boss：行为树（多阶段切换、技能组合）

策略模式分发：
```csharp
public interface IEnemyAI {
    void OnSpawn(BattleEntity self);
    void OnUpdate(BattleEntity self, float dt);
}

public class BasicMeleeAI : IEnemyAI { ... }
public class RangedShooterAI : IEnemyAI { ... }
public class BossAI : IEnemyAI { ... }
```

### 3.8 DropSystem（掉落系统）

敌人死亡时按掉落表生成可拾取物。

```csharp
public class DropConfig {
    public int EnemyId;
    public List<DropEntry> Drops;
}

public class DropEntry {
    public int ItemId;
    public int MinCount;
    public int MaxCount;
    public float Probability;        // 掉落概率
}
```

掉落物作为 Pickup Entity 出现在场景中，玩家靠近自动拾取，进入 InventoryManager。

---

## 四、数据流（一局战斗完整流程）

```
1. 玩家点击关卡
   → BattleManager.LoadStage(stageId)
   → 加载关卡 / 角色资源
   → 实例化玩家 Entity（属性来自 CharacterManager）

2. 战斗开始（State: Fighting）
   → EnemySpawner 按 WaveConfig 刷怪
   → AISystem 驱动敌人行为
   → 玩家释放技能
       → SkillSystem 索敌 + 播放特效
       → DamageSystem.ApplyDamage()
       → BuffSystem.AddBuff()（如有）
   → 敌人死亡
       → DropSystem.GenerateDrops()
       → 触发掉落 Pickup
       → 玩家拾取 → 进背包

3. 战斗结束
   → 胜利条件：清空所有波次 / 击杀 Boss
   → 失败条件：玩家死亡 / 时间耗尽
   → BattleManager.Settle()
   → 结算奖励：经验、掉落汇总
   → 通过 EventBus 通知 UI 显示结算面板
```

---

## 五、性能优化要点

### 5.1 对象池（必须）

- 敌人、子弹、伤害飘字、特效全部入池
- 池容量按峰值预估 + 20% 冗余
- 进出池时重置状态

### 5.2 伤害计算优化

同屏 200+ 实体时，每帧伤害计算可能成为瓶颈：

- 命中检测用**空间分区**（四叉树 / 网格）替代逐个遍历
- 高频技能伤害结算合并到固定 tick（如每 50ms）
- 同帧多个伤害合并为一条飘字（数字递增）

### 5.3 飘字优化

- 飘字 UI 用 TextMeshPro + 对象池
- 同源同目标短时间多次伤害合并显示
- 飘字超过屏幕外立刻回收

### 5.4 特效优化

- 同种特效限制最大并发数（如同屏最多 30 个爆炸特效）
- 屏幕外特效不渲染或降低播放质量
- 大型 AOE 特效用 GPU Instancing

### 5.5 何时考虑 Jobs/Burst

- 同屏 500+ 时考虑
- 优先优化伤害计算与移动逻辑
- AI 决策保持主线程（频次不高）

---

## 六、配表设计

### 6.1 敌人表（EnemyConfig）

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 敌人 ID |
| Name | string | 名称 |
| HP | int | 血量 |
| Attack | int | 攻击 |
| Defense | int | 防御 |
| Speed | float | 移动速度 |
| AIType | enum | AI 类型 |
| SkillIds | int[] | 技能列表 |
| DropTableId | int | 关联掉落表 |
| PrefabPath | string | 预制体路径 |

### 6.2 技能表（SkillConfig）

字段：基础信息、伤害公式、效果列表、特效路径、CD、范围。

### 6.3 关卡表（StageConfig）

字段：关卡 ID、推荐战力、波次列表、Boss ID、奖励配置。

### 6.4 波次表（WaveConfig）

字段：波次 ID、开始时间、敌人组合、生成位置。

---

## 七、与其他系统交互

### 输入依赖
- **CharacterSystem**：玩家属性、装备技能来源
- **InventorySystem**：消耗品（buff 药）、装备查询
- **ConfigSystem**：所有战斗配表

### 输出
- **InventorySystem**：战斗结算后掉落入库
- **CharacterSystem**：经验值增加
- **TaskSystem**：战斗事件触发任务进度（击杀数、Boss 击败）
- **UI 层**：HUD 显示、伤害飘字、结算

### 事件定义

```csharp
// 当前实际实现（类型安全泛型 EventBus）
public struct EntityDamagedEvent {
    public BattleEntity Entity;
    public BattleEntity Source;
    public int Damage;
    public bool IsCritical;
}

public struct EntityDiedEvent {
    public BattleEntity Entity;
    public BattleEntity Killer;
}

public struct BattleStartedEvent { }

public struct BattleEndedEvent {
    public BattleState Result;      // Victory / Defeat
    public float Duration;
    public int EnemyKilled;
}

// 规划中（待实现）
// OnSkillCast, OnWaveComplete, OnDropGenerated
```

---

## 八、UI 层设计

```
Scripts/UI/Battle/
├── BattleHUD.cs              # 顶部 HUD（血量、能量、波次进度）
├── SkillBar.cs               # 技能按钮组
├── DamageNumber.cs           # 伤害飘字（池化）
├── BossHealthBar.cs          # Boss 血条
├── BattleResultDialog.cs     # 结算面板
└── PauseMenu.cs              # 暂停菜单
```

---

## 九、实现路线图

### 阶段 1：最小可玩 Demo ✅ 已完成
- [x] BattleManager 基础状态机（Idle / Fighting / Victory / Defeat）
- [x] 玩家移动（WASD）+ 自动索敌射击
- [x] 一种敌人（Slime）+ 追击 AI + 接触伤害
- [x] 伤害系统（BattleEntity.TakeDamage，内联实现）
- [x] 单波次刷怪（EnemySpawner，圆周随机生成）
- [x] 子弹系统（Bullet，池化、阵营友伤判断）
- [x] 对象池（PoolManager，Spawn / Despawn / Prewarm）
- [x] 帧动画（SimpleSpriteAnimator）
- [x] 一键启动器（BattleSceneBootstrap，自动构建场景）

### 阶段 2：核心战斗循环 ⏳ 部分完成
- [x] EntityManager 基础版（BattleEntity 抽象基类）
- [x] 对象池完整实现
- [ ] EnemySpawner 多波次配置
- [ ] BuffSystem 基础 buff
- [ ] DropSystem 掉落
- [ ] BattleHUD + 飘字
- [ ] SkillSystem 技能释放

### 阶段 3：内容扩展
- [ ] 多种敌人 AI
- [ ] Boss 战 + 行为树
- [ ] 技能编辑器（Timeline）
- [ ] 战斗结算面板
- [ ] 关卡选择 → 战斗 → 结算闭环

### 阶段 4：性能与体验
- [ ] 空间分区索敌优化
- [ ] 飘字合并显示
- [ ] 特效池化与并发限制
- [ ] 战斗回放（可选）

---

## 十、注意事项与禁忌

### 应当遵循

- 所有运行时实体必须走对象池
- 伤害公式走配表，不写死
- 玩家属性来自 CharacterManager，不在战斗内硬编码
- AI 通过策略模式扩展，禁止 if-else 硬分支
- 战斗内数据修改通过 DamageSystem / BuffSystem 接口，不直接改 Stats

### 应当避免

- 直接 Instantiate / Destroy 敌人或子弹
- 在 Update 里逐帧逐对象做距离检测（用空间分区）
- 在战斗内调用 `Resources.Load`（提前预加载或 Addressables 异步）
- 战斗结束后忘记清理 Entity / 取消事件订阅
- 单技能写满几百行：拆分 SkillEffect 组合
- 把战斗结算 UI 和战斗逻辑耦合在一起（应通过事件）

---

## 十一、参考资料

- 弹壳特攻队战斗设计拆解
- 战双帕弥什动作系统
- 《游戏编程模式》— 状态机、对象池、组件模式
- Unity DOTS Sample（性能瓶颈时参考）

---

> 文档版本：v0.2
> 最后更新：2026-05-10
> 维护原则：随战斗系统迭代持续更新，新增子系统 / 优化方案同步至本文档
