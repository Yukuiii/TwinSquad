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
    /// 职责：状态机、敌人计数、胜负判定、战斗事件广播。
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        public BattleState State { get; private set; } = BattleState.Idle;
        public BattleEntity Player { get; private set; }
        public int EnemyTotal { get; private set; }
        public int EnemyKilled { get; private set; }

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

        public void StartBattle(int enemyTotal)
        {
            if (State == BattleState.Fighting) return;

            EnemyTotal = enemyTotal;
            EnemyKilled = 0;
            State = BattleState.Fighting;
            _battleStartTime = Time.time;

            EventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
            EventBus.Publish(new BattleStartedEvent { EnemyTotal = enemyTotal });
            Debug.Log($"[BattleManager] 战斗开始（目标击杀：{enemyTotal}）");
        }

        public void EndBattle(bool victory)
        {
            if (State != BattleState.Fighting) return;

            State = victory ? BattleState.Victory : BattleState.Defeat;
            EventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);

            var duration = Time.time - _battleStartTime;
            EventBus.Publish(new BattleEndedEvent
            {
                IsVictory = victory,
                EnemyKilled = EnemyKilled,
                Duration = duration,
            });
            Debug.Log($"[BattleManager] 战斗结束 [{(victory ? "胜利" : "失败")}]  击杀 {EnemyKilled}/{EnemyTotal}  用时 {duration:F1}s");
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
                    if (EnemyKilled >= EnemyTotal) EndBattle(true);
                    break;
            }
        }
    }
}
