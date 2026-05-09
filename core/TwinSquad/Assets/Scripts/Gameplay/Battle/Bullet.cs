using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 子弹（池化）：直线飞行、Trigger 命中、超时回收。
    /// 同阵营不伤害（防止打到自己）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Bullet : MonoBehaviour, IPoolable
    {
        [SerializeField] private float speed = 15f;
        [SerializeField] private float lifetime = 2f;

        private Vector3 _direction;
        private BattleEntity _source;
        private int _damage;
        private float _life;
        private bool _consumed;

        public void Launch(Vector3 direction, BattleEntity source, int damage)
        {
            _direction = direction.normalized;
            _source = source;
            _damage = damage;
            _life = lifetime;
            _consumed = false;
        }

        public void OnSpawn()
        {
            _consumed = false;
        }

        public void OnDespawn()
        {
            _direction = Vector3.zero;
            _source = null;
            _damage = 0;
            _life = 0f;
            _consumed = true;
        }

        private void Update()
        {
            if (_consumed) return;
            transform.position += _direction * speed * Time.deltaTime;
            _life -= Time.deltaTime;
            if (_life <= 0f) PoolManager.Despawn(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_consumed) return;
            if (!other.TryGetComponent<BattleEntity>(out var entity)) return;
            if (entity.IsDead) return;
            if (_source != null && entity.Camp == _source.Camp) return;

            entity.TakeDamage(new DamageInfo { Source = _source, Damage = _damage });
            PoolManager.Despawn(gameObject);
        }
    }
}
