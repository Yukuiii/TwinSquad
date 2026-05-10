using UnityEngine;
using UnityEngine.InputSystem;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 玩家控制器（最小版）：
    /// - WASD / 方向键移动
    /// - 自动攻击（定时索敌 + 发射子弹）
    /// - 死亡触发战斗失败（由 BattleManager 监听 EntityDiedEvent 处理）
    /// </summary>
    public class PlayerController : BattleEntity
    {
        [Header("移动")]
        [SerializeField] private float moveSpeed = 6f;

        [Header("攻击")]
        [SerializeField] private float attackInterval = 0.4f;
        [SerializeField] private int attackDamage = 25;
        [SerializeField] private float attackRange = 12f;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform muzzle;

        private float _attackTimer;
        private SimpleSpriteAnimator _animator;
        private bool _wasMoving;

        public void Configure(GameObject bullet, Transform muzzlePoint = null)
        {
            bulletPrefab = bullet;
            if (muzzlePoint != null) muzzle = muzzlePoint;
        }

        protected override void Awake()
        {
            maxHP = Mathf.Max(maxHP, 200);
            base.Awake();
            camp = EntityCamp.Player;
            _animator = GetComponent<SimpleSpriteAnimator>();
        }

        private void Start()
        {
            if (BattleManager.Instance != null)
                BattleManager.Instance.RegisterPlayer(this);
        }

        private void Update()
        {
            if (IsDead) return;
            if (BattleManager.Instance == null || BattleManager.Instance.State != BattleState.Fighting) return;

            HandleMovement();
            HandleAutoAttack();
        }

        private void HandleMovement()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var dir = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dir.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1f;

            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
                transform.position += dir * moveSpeed * Time.deltaTime;

                if (!_wasMoving)
                {
                    _wasMoving = true;
                    if (_animator != null) _animator.Resume();
                }
            }
            else if (_wasMoving)
            {
                _wasMoving = false;
                if (_animator != null) _animator.Pause();
            }
        }

        private void HandleAutoAttack()
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f) return;

            var target = FindClosestEnemy();
            if (target == null) return;

            ShootAt(target);
            _attackTimer = attackInterval;
        }

        private BattleEntity FindClosestEnemy()
        {
            BattleEntity best = null;
            float bestSqr = attackRange * attackRange;
            var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                var sqr = (e.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = e; }
            }
            return best;
        }

        private void ShootAt(BattleEntity target)
        {
            if (bulletPrefab == null) return;
            var origin = muzzle != null ? muzzle.position : transform.position;
            var dir = target.transform.position - origin;
            dir.z = 0f;  // 限制在 XY 平面飞行
            if (dir.sqrMagnitude < 0.001f) return;
            dir.Normalize();

            var bullet = PoolManager.Spawn<Bullet>(bulletPrefab, origin, Quaternion.identity);
            bullet?.Launch(dir, this, attackDamage);
        }

        protected override void OnDeath()
        {
            Debug.Log("[Player] 阵亡");
        }
    }
}
