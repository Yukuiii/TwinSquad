using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 掉落物（池化）：静止后等待玩家靠近，磁吸飞向玩家并拾取。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DropItem : MonoBehaviour, IPoolable
    {
        [SerializeField] private float pickupRange = 1.5f;
        [SerializeField] private float magnetSpeed = 12f;

        private enum State { Idle, Flying }

        private State _state;
        private float _life;

        public void OnSpawn()
        {
            _state = State.Idle;
            _life = 0f;
        }

        public void OnDespawn()
        {
            _state = State.Idle;
            _life = 0f;
        }

        private void Update()
        {
            var player = BattleManager.Instance?.Player;
            if (player == null || player.IsDead) return;

            var toPlayer = player.transform.position - transform.position;
            toPlayer.z = 0f;
            var dist = toPlayer.magnitude;

            if (_state == State.Idle)
            {
                if (dist <= pickupRange)
                    _state = State.Flying;
            }

            if (_state == State.Flying)
            {
                var step = Mathf.Min(magnetSpeed * Time.deltaTime, dist);
                transform.position += toPlayer.normalized * step;

                if (dist < 0.3f)
                {
                    EventBus.Publish(new DropItemPickedUpEvent { Drop = this });
                    PoolManager.Despawn(gameObject);
                }
            }
        }
    }
}
