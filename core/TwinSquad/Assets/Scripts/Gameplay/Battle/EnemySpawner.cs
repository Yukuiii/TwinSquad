using System.Collections;
using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 敌人生成器：
    /// - 玩家周围圆形位置持续生成敌人，直到战斗结束
    /// - 间隔生成，避免单帧创建大量对象
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private float spawnRadius = 12f;
        [SerializeField] private float spawnInterval = 0.3f;
        [SerializeField] private bool autoStart = true;

        public GameObject EnemyPrefab
        {
            get => enemyPrefab;
            set => enemyPrefab = value;
        }
        public float BattleDuration { get; set; } = 60f;

        private void Start()
        {
            if (autoStart) StartCoroutine(RunBattle());
        }

        public IEnumerator RunBattle()
        {
            yield return null;

            if (enemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] enemyPrefab 未配置");
                yield break;
            }

            BattleManager.Instance?.StartBattle(BattleDuration);
            PoolManager.Prewarm(enemyPrefab, 16);

            while (BattleManager.Instance != null && BattleManager.Instance.State == BattleState.Fighting)
            {
                var pos = GetRandomPositionAroundPlayer();
                PoolManager.Spawn(enemyPrefab, pos, Quaternion.identity);
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private Vector3 GetRandomPositionAroundPlayer()
        {
            var player = BattleManager.Instance?.Player;
            var center = player != null ? player.transform.position : Vector3.zero;
            var angle = Random.Range(0f, Mathf.PI * 2f);
            return center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnRadius;
        }
    }
}
