using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 阵营。决定攻击关系（玩家不能伤害玩家，敌人不能伤害敌人）。
    /// </summary>
    public enum EntityCamp
    {
        Player,
        Enemy,
        Neutral,
    }

    /// <summary>
    /// 一次伤害的描述。当前为最简版（YAGNI）：只有伤害值和来源。
    /// 后续可扩展：暴击、元素类型、技能 ID、buff 来源等。
    /// </summary>
    public struct DamageInfo
    {
        public BattleEntity Source;
        public int Damage;
        public bool IsCritical;
    }

    /// <summary>
    /// 战斗实体基类。玩家、敌人、可破坏物均继承此类。
    /// 提供 HP、阵营、伤害结算的统一入口。
    /// </summary>
    public abstract class BattleEntity : MonoBehaviour
    {
        [SerializeField] protected int maxHP = 100;
        [SerializeField] protected EntityCamp camp = EntityCamp.Neutral;

        public int MaxHP => maxHP;
        public int CurrentHP { get; protected set; }
        public EntityCamp Camp => camp;
        public bool IsDead => CurrentHP <= 0;

        protected virtual void Awake()
        {
            CurrentHP = maxHP;
        }

        // 池化对象出池后会再次走 OnEnable，重置血量
        protected virtual void OnEnable()
        {
            CurrentHP = maxHP;
        }

        public virtual void TakeDamage(DamageInfo info)
        {
            if (IsDead) return;
            var dmg = Mathf.Max(0, info.Damage);
            CurrentHP = Mathf.Max(0, CurrentHP - dmg);

            EventBus.Publish(new EntityDamagedEvent
            {
                Target = this,
                Source = info.Source,
                Damage = dmg,
                RemainingHP = CurrentHP,
            });

            OnDamaged(info);

            if (IsDead)
            {
                EventBus.Publish(new EntityDiedEvent { Target = this, Source = info.Source });
                OnDeath();
            }
        }

        protected virtual void OnDamaged(DamageInfo info) { }
        protected virtual void OnDeath() { }
    }
}
