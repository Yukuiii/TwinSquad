using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    public enum BattleState
    {
        Idle,
        Fighting,
        Victory,
        Defeat,
    }

    /// <summary>
    /// 战斗总控（场景级单例，不跨场景）。
    /// 职责：倒计时、敌人计数、胜负判定、战斗事件广播。
    /// 胜利条件：存活到倒计时结束。
    /// 失败条件：玩家死亡。
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        public BattleState State { get; private set; } = BattleState.Idle;
        public BattleEntity Player { get; private set; }
        public int EnemyKilled { get; private set; }
        public float BattleDuration { get; private set; } = 60f;
        public float TimeRemaining { get; private set; }

        private float _battleStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
        }

        public void RegisterPlayer(BattleEntity player) => Player = player;

        public void StartBattle(float duration = 60f)
        {
            if (State == BattleState.Fighting) return;

            BattleDuration = duration;
            TimeRemaining = duration;
            EnemyKilled = 0;
            State = BattleState.Fighting;
            _battleStartTime = Time.time;

            EventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
            EventBus.Publish(new BattleStartedEvent { Duration = duration });
            Debug.Log($"[BattleManager] 战斗开始（存活 {duration:F0}s）");
        }

        private void Update()
        {
            if (State != BattleState.Fighting) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                EndBattle(true);
            }
        }

        public void EndBattle(bool victory)
        {
            if (State != BattleState.Fighting) return;

            State = victory ? BattleState.Victory : BattleState.Defeat;
            EventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
            CleanupScene();

            var duration = Time.time - _battleStartTime;
            EventBus.Publish(new BattleEndedEvent
            {
                IsVictory = victory,
                EnemyKilled = EnemyKilled,
                Duration = duration,
            });
            Debug.Log($"[BattleManager] 战斗结束 [{(victory ? "胜利" : "失败")}]  击杀 {EnemyKilled}  用时 {duration:F1}s");
        }

        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (State != BattleState.Fighting || evt.Target == null) return;

            switch (evt.Target.Camp)
            {
                case EntityCamp.Player:
                    EndBattle(false);
                    break;
                case EntityCamp.Enemy:
                    EnemyKilled++;
                    break;
            }
        }

        private void CleanupScene()
        {
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy != null && !enemy.IsDead)
                    PoolManager.Despawn(enemy.gameObject);
            }
            foreach (var drop in Object.FindObjectsByType<DropItem>(FindObjectsSortMode.None))
            {
                if (drop != null)
                    PoolManager.Despawn(drop.gameObject);
            }
        }
    }
}
