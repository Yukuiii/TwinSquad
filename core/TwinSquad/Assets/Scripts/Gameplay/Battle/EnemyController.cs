using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 敌人（最简版 AI）：
    /// - 追击 BattleManager.Player
    /// - 接触玩家造成伤害（带 CD）
    /// - 死亡时入池
    /// - 实现 IPoolable，复用时重置目标和 CD
    /// </summary>
    public class EnemyController : BattleEntity, IPoolable
    {
        [Header("移动")]
        [SerializeField] private float moveSpeed = 3.2f;

        [Header("接触伤害")]
        [SerializeField] private int contactDamage = 10;
        [SerializeField] private float damageCooldown = 1f;
        [SerializeField] private float damageRange = 1.4f;

        [Header("掉落")]
        [SerializeField] private float dropChance = 0.5f;

        private BattleEntity _target;
        private float _damageTimer;
        [SerializeField] private GameObject _dropPrefab;

        protected override void Awake()
        {
            maxHP = Mathf.Max(maxHP, 50);
            base.Awake();
            camp = EntityCamp.Enemy;
        }

        public void OnSpawn()
        {
            _damageTimer = 0f;
            _target = BattleManager.Instance != null ? BattleManager.Instance.Player : null;
        }

        public void OnDespawn()
        {
            _target = null;
        }

        public void SetDropPrefab(GameObject prefab) => _dropPrefab = prefab;

        private void Update()
        {
            if (IsDead || _target == null || _target.IsDead) return;
            if (BattleManager.Instance == null || BattleManager.Instance.State != BattleState.Fighting) return;

            var toTarget = _target.transform.position - transform.position;
            toTarget.z = 0f;  // 限制在 XY 平面
            var dist = toTarget.magnitude;
            if (dist < 0.001f) return;

            var dir = toTarget / dist;
            // 2D 顶视：sprite 永远直立朝屏幕，不旋转

            if (dist > damageRange)
            {
                transform.position += dir * moveSpeed * Time.deltaTime;
            }

            _damageTimer -= Time.deltaTime;
            if (dist <= damageRange && _damageTimer <= 0f)
            {
                _target.TakeDamage(new DamageInfo { Source = this, Damage = contactDamage });
                _damageTimer = damageCooldown;
            }
        }

        protected override void OnDeath()
        {
            if (_dropPrefab != null && Random.value <= dropChance)
            {
                PoolManager.Spawn(_dropPrefab, transform.position, Quaternion.identity);
            }
            PoolManager.Despawn(gameObject);
        }
    }
}
