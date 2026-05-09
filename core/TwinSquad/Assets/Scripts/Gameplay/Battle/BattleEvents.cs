namespace TwinSquad.Gameplay.Battle
{
    // ===== 实体级事件 =====
    public struct EntityDamagedEvent
    {
        public BattleEntity Target;
        public BattleEntity Source;
        public int Damage;
        public int RemainingHP;
    }

    public struct EntityDiedEvent
    {
        public BattleEntity Target;
        public BattleEntity Source;
    }

    // ===== 战斗级事件 =====
    public struct BattleStartedEvent
    {
        public int EnemyTotal;
    }

    public struct BattleEndedEvent
    {
        public bool IsVictory;
        public int EnemyKilled;
        public float Duration;
    }
}
