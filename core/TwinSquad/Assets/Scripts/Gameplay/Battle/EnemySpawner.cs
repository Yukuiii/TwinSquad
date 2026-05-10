using System.Collections;
using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 敌人生成器：
    /// - 正交相机视口外持续生成敌人，直到战斗结束
    /// - 间隔生成，避免单帧创建大量对象
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Camera spawnCamera;
        [SerializeField, Min(0f)] private float spawnScreenPadding = 1.5f;
        [SerializeField] private float spawnInterval = 0.3f;
        [SerializeField] private bool autoStart = true;

        public GameObject EnemyPrefab
        {
            get => enemyPrefab;
            set => enemyPrefab = value;
        }
        public Camera SpawnCamera
        {
            get => spawnCamera;
            set => spawnCamera = value;
        }
        public float BattleDuration { get; set; } = 60f;

        /// <summary>
        /// 组件启动时按配置自动开始战斗刷怪。
        /// </summary>
        private void Start()
        {
            if (autoStart) StartCoroutine(RunBattle());
        }

        /// <summary>
        /// 按固定间隔在屏幕外生成敌人，直到战斗状态结束。
        /// </summary>
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

            var spawnDelay = new WaitForSeconds(spawnInterval);
            var waitForCameraUpdate = new WaitForEndOfFrame();
            while (BattleManager.Instance != null && BattleManager.Instance.State == BattleState.Fighting)
            {
                yield return waitForCameraUpdate;
                if (BattleManager.Instance == null || BattleManager.Instance.State != BattleState.Fighting) yield break;

                var activeCamera = spawnCamera != null ? spawnCamera : Camera.main;
                if (activeCamera == null)
                {
                    Debug.LogError("[EnemySpawner] MainCamera 未找到，无法按屏幕外位置生成敌人");
                    yield break;
                }

                var pos = GetRandomPositionOutsideScreen(activeCamera);
                PoolManager.Spawn(enemyPrefab, pos, Quaternion.identity);
                yield return spawnDelay;
            }
        }

        /// <summary>
        /// 基于当前正交相机视口随机返回一个屏幕外生成点。
        /// </summary>
        private Vector3 GetRandomPositionOutsideScreen(Camera spawnCamera)
        {
            var player = BattleManager.Instance?.Player;
            var spawnZ = player != null ? player.transform.position.z : 0f;
            var viewportDepth = spawnCamera.WorldToViewportPoint(new Vector3(
                spawnCamera.transform.position.x,
                spawnCamera.transform.position.y,
                spawnZ
            )).z;
            var spawnClearance = GetSpawnClearance();
            var viewportPaddingX = spawnClearance / Mathf.Max(spawnCamera.orthographicSize * spawnCamera.aspect * 2f, 0.001f);
            var viewportPaddingY = spawnClearance / Mathf.Max(spawnCamera.orthographicSize * 2f, 0.001f);

            // 固定一条视口外边，另一轴在视口范围内随机，保证敌人从屏幕边缘外进入。
            var viewportPosition = Random.Range(0, 4) switch
            {
                0 => new Vector3(-viewportPaddingX, Random.value, viewportDepth),
                1 => new Vector3(1f + viewportPaddingX, Random.value, viewportDepth),
                2 => new Vector3(Random.value, 1f + viewportPaddingY, viewportDepth),
                _ => new Vector3(Random.value, -viewportPaddingY, viewportDepth),
            };
            var worldPosition = spawnCamera.ViewportToWorldPoint(viewportPosition);
            worldPosition.z = spawnZ;
            return worldPosition;
        }

        /// <summary>
        /// 返回让敌人完整离开视口所需的世界坐标生成间距。
        /// </summary>
        private float GetSpawnClearance()
        {
            if (enemyPrefab == null
                || !enemyPrefab.TryGetComponent<SpriteRenderer>(out var spriteRenderer)
                || spriteRenderer.sprite == null)
            {
                return spawnScreenPadding;
            }

            var spriteSize = spriteRenderer.sprite.bounds.size;
            var scale = spriteRenderer.transform.lossyScale;
            var worldWidth = Mathf.Abs(spriteSize.x * scale.x);
            var worldHeight = Mathf.Abs(spriteSize.y * scale.y);
            return spawnScreenPadding + Mathf.Max(worldWidth, worldHeight);
        }
    }
}
