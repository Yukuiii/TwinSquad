using System.Collections;
using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 敌人生成器（最小版）：
    /// - 玩家周围圆形位置随机生成 totalCount 个敌人
    /// - 间隔生成，避免单帧创建大量对象
    /// - 自动启动 BattleManager 战斗状态
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int totalCount = 10;
        [SerializeField] private float spawnRadius = 12f;
        [SerializeField] private float spawnInterval = 0.3f;
        [SerializeField] private bool autoStart = true;

        public GameObject EnemyPrefab
        {
            get => enemyPrefab;
            set => enemyPrefab = value;
        }
        public int TotalCount
        {
            get => totalCount;
            set => totalCount = value;
        }

        private void Start()
        {
            if (autoStart) StartCoroutine(RunBattle());
        }

        public IEnumerator RunBattle()
        {
            // 等 1 帧让 Player 注册到 BattleManager
            yield return null;

            if (enemyPrefab == null)
            {
                Debug.LogError("[EnemySpawner] enemyPrefab 未配置");
                yield break;
            }

            BattleManager.Instance?.StartBattle(totalCount);
            PoolManager.Prewarm(enemyPrefab, Mathf.Min(totalCount, 16));

            int spawned = 0;
            while (spawned < totalCount)
            {
                if (BattleManager.Instance == null || BattleManager.Instance.State != BattleState.Fighting)
                    break;

                var pos = GetRandomPositionAroundPlayer();
                PoolManager.Spawn(enemyPrefab, pos, Quaternion.identity);
                spawned++;
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private Vector3 GetRandomPositionAroundPlayer()
        {
            var player = BattleManager.Instance?.Player;
            var center = player != null ? player.transform.position : Vector3.zero;
            var angle = Random.Range(0f, Mathf.PI * 2f);
            // 2D 顶视：圆周在 XY 平面（cos→x, sin→y, z=0）
            return center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnRadius;
        }
    }
}
