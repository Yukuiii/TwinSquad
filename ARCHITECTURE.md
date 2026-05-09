# TwinSquad 项目架构设计文档

> 项目定位：二次元 + 养成 + 卡牌 + 割草
> 标杆参考：蔚蓝档案、战双帕弥什、弹壳特攻队、物华弥新、重返未来 1999

---

## 一、整体架构分层

```
┌────────────────────────────────────┐
│  UI 层（UGUI + MVP）               │
├────────────────────────────────────┤
│  Gameplay 层（战斗、养成、抽卡）   │
├────────────────────────────────────┤
│  System 层（Manager 单例集合）     │
├────────────────────────────────────┤
│  Data 层（玩家存档、配置表）       │
├────────────────────────────────────┤
│  Framework 层（事件、资源、池）    │
└────────────────────────────────────┘
```

设计原则：**KISS / YAGNI / DRY / SOLID**，避免过度设计，问题驱动架构演进。

---

## 二、核心系统规划（按优先级）

### P0 — 立项即建（基础设施）

#### 1. GameManager（全局入口）✅ 已实现
- 管理游戏生命周期：启动 → 登录 → 主城 → 战斗
- 持有所有 Manager 引用（当前：Save / Config / UI）
- 跨场景数据中转
- 单例模式 + DontDestroyOnLoad
- RuntimeInitializeOnLoadMethod 自动 Bootstrap，无需场景内手动挂载

#### 2. EventBus（事件总线）✅ 已实现
- 解耦战斗、UI、养成系统
- 防止后期模块互相 GetComponent
- 实际采用**类型安全泛型**设计（非字符串 key），编译期即可发现类型错误
- 实际 API：
  ```csharp
  EventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
  EventBus.Publish(new EntityDiedEvent { ... });
  EventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
  ```

#### 3. UIManager（UI 栈管理）✅ 已实现
- 弹窗栈、界面切换、防止 UI 重叠
- 二次元卡牌 UI 量极大：主城 → 抽卡 → 角色详情 → 突破 → 技能 → 装备
- 4 层渲染：HUD / Normal / Popup / Loading，各层独立 Canvas + overrideSorting
- Popup 层栈式管理，同类型 Panel 单例复用
- 该模块是中后期最容易踩坑的地方，必须从第一天就规划好

#### 4. ConfigManager（配置表系统）✅ 已实现（简化版）
- 当前实现：手写 JSON + JsonUtility 加载（`Resources/Configs/{table}.json`）
- 已有配表：TbItem（含 6 条测试数据：金币、钻石、体力、材料、礼包）
- 配表内容：角色数据、关卡数据、抽卡概率、技能参数、掉落表
- **后续可迁移至 Luban**：Tables 类已预留 TbCharacter / TbStage / TbSkill / TbGachaPool 占位

#### 5. SaveManager（存档系统）✅ 已实现
- 玩家数据序列化：角色等级、抽到的卡、资源、关卡进度
- SHA256 + salt checksum 防篡改
- 原子写入（.tmp → 备份旧文件 → 重命名）
- 30 秒定时自动存档 + OnApplicationPause/Quit 触发
- Dirty Flag 模式，避免无变更时写入
- 损坏时自动从备份恢复
- 加密接口预留（当前为明文直通，AES 待接入）

### P1 — 战斗模块前必备

#### 6. ResourceManager（资源加载）⏳ 待实现
- 当前使用 `Resources.Load`，后续迁移至 Addressables
- 角色预制体、特效、UI 都按需加载
- 引用计数管理，避免内存泄漏

#### 7. ObjectPool（对象池）✅ 已实现
- 割草场景必备
- 子弹、敌人、伤害飘字、特效全部入池
- 每帧上百实体生成销毁，无池会导致严重 GC 卡顿
- PoolManager 静态类，API：Spawn / Despawn / Prewarm / Clear / ClearAll
- IPoolable 接口（OnSpawn / OnDespawn 回调）
- 全局 DontDestroyOnLoad 根节点 `[PoolRoot]`

#### 8. BattleSystem（战斗框架）✅ 核心已实现
针对**割草**特性设计，当前已实现部分：
```
BattleManager（场景单例，状态机 Idle/Fighting/Victory/Defeat）
├── EnemySpawner ✅（玩家周围圆周生成，间隔刷怪，池预热）
├── BattleEntity ✅（抽象基类：HP、阵营、TakeDamage、死亡事件）
├── PlayerController ✅（WASD 移动 + 自动索敌射击）
├── EnemyController ✅（追击 AI + 接触伤害 + IPoolable）
├── Bullet ✅（池化子弹，XY 平面飞行，阵营友伤判断）
├── SimpleSpriteAnimator ✅（帧动画，循环/单次，池友好）
├── DamageSystem ⏳（当前内联在 BattleEntity.TakeDamage）
├── SkillSystem ⏳
└── DropSystem ⏳
```

#### 9. CharacterSystem（角色/卡牌系统）
养成核心数据结构：
```
CharacterData（运行时实例）
├── 基础属性（HP / ATK / DEF）
├── 等级、突破、好感度
├── 装备 / 圣遗物槽
├── 技能等级
└── 关联 CharacterConfig（静态配置）
```

### P2 — 内容铺开后再做

- 任务系统、成就系统
- 公会 / 社交（如联网）
- 红点系统（不要过早实现）
- 新手引导系统

---

## 三、技术选型

| 模块 | 当前方案 | 推荐方案（后续迁移） | 选型理由 |
|------|---------|---------------------|---------|
| UI 框架 | UGUI + 自写 MVP ✅ | — | TextMeshPro 已集成 |
| DI 框架 | 无（AddComponent 手动创建） | VContainer | 比 Zenject 轻量、性能好 |
| 配置表 | 手写 JSON + JsonUtility ✅ | Luban | 国内中小项目首选，当前简化版够用 |
| 资源管理 | Resources.Load | Addressables | 热更新友好，需在内容量增大后迁移 |
| 异步 | Coroutine | UniTask | 替代 Coroutine，零 GC |
| 响应式 | 无 | R3 / UniRx | 卡牌 UI 数据绑定必备 |
| 网络（如需要） | 无 | Protobuf + WebSocket / KCP | 卡牌游戏无需帧同步 |
| 热更新（可选） | 无 | HybridCLR / xLua | HybridCLR 更现代 |
| 动画 | 无 | DOTween | UI 动效必备 |

---

## 四、二次元卡牌游戏的特殊关注点

### 1. 角色立绘 / 动画
- Live2D 或 Spine（蔚蓝档案使用 Spine）
- 立绘按需加载，避免内存占用过高

### 2. 抽卡系统
- 概率配置走配表
- 保底逻辑必须严格测试
- 动效用 Timeline 编排（金光、彩光、SSR 演出）

### 3. 大量数值面板 UI
- 角色详情页通常 5–10 个 Tab
- 必须采用 MVP 模式 + 数据绑定，否则后期维护噩梦

### 4. 割草战斗性能
- 同屏 100+ 敌人时考虑 Jobs + Burst 优化伤害计算
- 不要一开始就上 ECS，先 OOP 跑通再说
- 关键瓶颈：碰撞检测、伤害飘字、特效粒子

---

## 五、开发路线图

### 第一阶段：基础框架 ✅ 已完成
- [x] GameManager 全局入口（RuntimeInitializeOnLoadMethod 自动 Bootstrap）
- [x] EventBus 事件总线（类型安全泛型设计）
- [x] UIManager UI 栈管理（4 层渲染 + Popup 栈）
- [x] 配置表系统（手写 JSON + JsonUtility，可迁移至 Luban）
- [x] SaveManager 存档系统（SHA256 防篡改 + 原子写入 + 自动存档）

### 第二阶段：战斗 Demo ✅ 核心已完成
- [x] ObjectPool 对象池（PoolManager 静态类 + IPoolable）
- [x] 单关割草战斗（BattleSceneBootstrap 一键启动）
- [x] 角色控制（WASD 移动 + 自动索敌射击）
- [x] 敌人 AI（追击 + 接触伤害）
- [x] 子弹系统（池化、阵营友伤判断）
- [x] 精灵帧动画（SimpleSpriteAnimator）
- [x] 战斗状态机（Idle → Fighting → Victory/Defeat）
- [ ] 伤害飘字
- [ ] 技能系统（多技能、CD、AOE）
- [ ] 多波次刷怪
- [ ] 掉落系统

### 第三阶段：养成闭环 ⏳ 未开始
- [ ] 角色数据系统 + 存档
- [ ] 抽卡系统 + 卡池配置
- [ ] 主城 UI（角色列表、详情、突破）
- [ ] 角色升级 / 突破 / 技能升级

### 第四阶段：内容铺开 ⏳ 未开始
- [ ] 任务、商店、活动
- [ ] 章节关卡系统
- [ ] 性能优化、热更新接入
- [ ] 红点系统、引导系统

### 当前已完成的美术资源
- Player Idle 帧（8 帧）
- Slime Run 帧（8 帧）
- Bullet 特效图
- 自动导入后处理（CharacterSpritePostprocessor：PPU=256、BottomCenter pivot）

---

## 六、架构原则与禁忌

### 应当遵循
- **KISS**：简单优先，能不抽象就不抽象
- **YAGNI**：只实现当前明确需要的功能
- **DRY**：识别重复代码，主动抽象复用
- **SOLID**：每次代码 review 时对照检查
- **配置驱动**：能配表的不写死
- **事件驱动**：模块间通过 EventBus 解耦

### 应当避免
- 过早引入 ECS / DOTS（OOP 完全够用）
- 自研编辑器（用 Unity 自带 + Odin Inspector）
- 复杂网络框架（除非确定 PvP 联网）
- 微服务架构（卡牌游戏单服够用）
- 直接使用 Resources.Load（一律走 Addressables）
- 散落的 Singleton（统一由 GameManager 管理）

---

## 七、目录结构（当前实际）

```
Assets/
├── Resources/
│   ├── Configs/              # JSON 配表（item.json 等）
│   └── Sprites/
│       ├── Characters/
│       │   ├── Player/       # Idle（8帧）、Run、Attack 目录
│       │   └── Enemies/      # Slime/Run（8帧）
│       └── Effects/          # bullet.png
├── Scenes/                   # HomeScene / GameScene / BattleScene
├── Scripts/
│   ├── Framework/            # EventBus ✅、ObjectPool ✅
│   ├── Managers/             # GameManager ✅、UIManager ✅、SaveManager ✅、ConfigManager ✅
│   ├── Data/                 # PlayerSaveData ✅、SaveMigration ✅
│   ├── Configs/              # Tables ✅、TbItem ✅、ItemConfig ✅、ItemEnums ✅
│   ├── Gameplay/
│   │   └── Battle/           # BattleManager ✅、PlayerController ✅、EnemyController ✅ 等 11 个文件
│   ├── UI/                   # HomeSceneController ✅、UIPanel ✅
│   └── Editor/               # CharacterSpritePostprocessor ✅
└── server/                   # 仅 README 占位
```

---

## 八、参考资料

- 米哈游 GDC / Unite / CEDEC 技术分享
- 《游戏编程模式》Robert Nystrom
- 《游戏引擎架构》Jason Gregory
- Unity 官方 Addressables / DOTS 文档
- Luban 配表工具：https://github.com/focus-creative-games/luban
- VContainer：https://github.com/hadashiA/VContainer
- UniTask：https://github.com/Cysharp/UniTask

---

> 文档版本：v0.2
> 最后更新：2026-05-10
> 维护原则：随项目演进持续迭代，过时章节及时清理
