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

#### 1. GameManager（全局入口）
- 管理游戏生命周期：启动 → 登录 → 主城 → 战斗
- 持有所有 Manager 引用
- 跨场景数据中转
- 单例模式 + DontDestroyOnLoad

#### 2. EventBus（事件总线）
- 解耦战斗、UI、养成系统
- 防止后期模块互相 GetComponent
- 接口示例：
  ```csharp
  EventBus.Emit("OnLevelUp", new LevelUpData{...});
  EventBus.On("OnLevelUp", HandleLevelUp);
  ```

#### 3. UIManager（UI 栈管理）
- 弹窗栈、界面切换、防止 UI 重叠
- 二次元卡牌 UI 量极大：主城 → 抽卡 → 角色详情 → 突破 → 技能 → 装备
- 该模块是中后期最容易踩坑的地方，必须从第一天就规划好

#### 4. ConfigManager（配置表系统）
- 工作流：Excel → JSON / 二进制 → C# 类
- 推荐工具：**Luban**（开源、国内卡牌游戏常用）
- 配表内容：角色数据、关卡数据、抽卡概率、技能参数、掉落表

#### 5. SaveManager（存档系统）
- 玩家数据序列化：角色等级、抽到的卡、资源、关卡进度
- 单机：JSON + 加密；联网：服务器存
- 关键数据加 checksum 防作弊

### P1 — 战斗模块前必备

#### 6. ResourceManager（资源加载）
- 强制使用 Addressables，禁用 Resources.Load
- 角色预制体、特效、UI 都按需加载
- 引用计数管理，避免内存泄漏

#### 7. ObjectPool（对象池）
- 割草场景必备
- 子弹、敌人、伤害飘字、特效全部入池
- 每帧上百实体生成销毁，无池会导致严重 GC 卡顿

#### 8. BattleSystem（战斗框架）
针对**割草**特性设计：
```
BattleManager
├── EnemySpawner（按波次/曲线生成）
├── EntityManager（玩家 + 敌人统一管理）
├── DamageSystem（伤害计算 + 数字飘字）
├── SkillSystem（技能释放、CD、连招）
└── DropSystem（掉落、拾取）
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

| 模块 | 推荐方案 | 选型理由 |
|------|---------|---------|
| UI 框架 | UGUI + 自写 MVP | TextMeshPro 已集成 |
| DI 框架 | VContainer | 比 Zenject 轻量、性能好 |
| 配置表 | Luban | 国内中小项目首选 |
| 资源管理 | Addressables | Unity 官方，热更新友好 |
| 异步 | UniTask | 替代 Coroutine，零 GC |
| 响应式 | R3 / UniRx | 卡牌 UI 数据绑定必备 |
| 网络（如需要） | Protobuf + WebSocket / KCP | 卡牌游戏无需帧同步 |
| 热更新（可选） | HybridCLR / xLua | HybridCLR 更现代 |
| 动画 | DOTween | UI 动效必备 |

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

### 第一阶段：基础框架（当前）
- [ ] GameManager 全局入口
- [ ] EventBus 事件总线
- [ ] UIManager UI 栈管理
- [ ] Luban 配置表接入
- [ ] Addressables 资源系统

### 第二阶段：战斗 Demo
- [ ] ObjectPool 对象池
- [ ] 单关割草战斗（刷怪 → 结算）
- [ ] 角色控制 + 技能释放
- [ ] 伤害系统 + 飘字

### 第三阶段：养成闭环
- [ ] 角色数据系统 + 存档
- [ ] 抽卡系统 + 卡池配置
- [ ] 主城 UI（角色列表、详情、突破）
- [ ] 角色升级 / 突破 / 技能升级

### 第四阶段：内容铺开
- [ ] 任务、商店、活动
- [ ] 章节关卡系统
- [ ] 性能优化、热更新接入
- [ ] 红点系统、引导系统

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

## 七、目录结构规划（建议）

```
Assets/
├── Art/                  # 美术资源
│   ├── Characters/
│   ├── UI/
│   ├── Effects/
│   └── Scenes/
├── Audio/                # 音频资源
├── Configs/              # Luban 生成配表
├── Prefabs/              # 预制体
│   ├── Characters/
│   ├── Enemies/
│   ├── UI/
│   └── Effects/
├── Scenes/               # 场景文件
├── Scripts/
│   ├── Framework/        # 框架层（EventBus、Pool、Singleton）
│   ├── Managers/         # 系统管理器（GameManager、UIManager…）
│   ├── Data/             # 数据层（Config、Save、Runtime Data）
│   ├── Gameplay/         # 玩法层
│   │   ├── Battle/
│   │   ├── Character/
│   │   ├── Gacha/
│   │   └── Stage/
│   ├── UI/               # UI 层（View / Presenter）
│   └── Utils/            # 工具类
└── Settings/             # URP、输入、构建配置
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

> 文档版本：v0.1
> 最后更新：2026-05-09
> 维护原则：随项目演进持续迭代，过时章节及时清理
