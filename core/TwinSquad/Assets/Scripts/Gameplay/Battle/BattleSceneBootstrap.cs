using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 战斗 Demo 一键启动器。
    ///
    /// 用法：
    ///   1. 在 Unity 新建空场景（如 BattleScene）
    ///   2. 在场景中创建空 GameObject，挂载本脚本
    ///   3. Play —— 自动构建地面、灯光、相机、玩家、敌人模板、Spawner、BattleManager
    ///
    /// 不依赖任何美术资源，全部用 Unity 原生 primitive。
    /// 跑通核心循环后，可以替换 prefab、加配表、加 UI。
    /// </summary>
    public class BattleSceneBootstrap : MonoBehaviour
    {
        [Header("地图")]
        [SerializeField] private float groundSize = 40f;

        [Header("敌人")]
        [SerializeField] private int enemyCount = 10;
        [SerializeField] private float spawnRadius = 12f;
        [SerializeField] private float spawnInterval = 0.3f;

        [Header("配色")]
        [SerializeField] private Color playerColor = new(0.2f, 0.6f, 1f);
        [SerializeField] private Color enemyColor  = new(1f, 0.25f, 0.25f);
        [SerializeField] private Color bulletColor = new(1f, 0.95f, 0.2f);
        [SerializeField] private Color groundColor = new(0.18f, 0.18f, 0.2f);

        private void Awake()
        {
            CreateGround();
            CreateLight();
            var battleMgrGo = CreateBattleManager();
            var bulletPrefab = CreateBulletTemplate();
            var enemyPrefab = CreateEnemyTemplate();
            var player = CreatePlayer(bulletPrefab);
            CreateCamera(player.transform);
            CreateSpawner(battleMgrGo, enemyPrefab);
        }

        // ===== 场景元素 =====

        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(groundSize / 10f, 1f, groundSize / 10f);
            Tint(ground, groundColor);
        }

        private void CreateLight()
        {
            var go = new GameObject("DirectionalLight");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
        }

        private void CreateCamera(Transform follow)
        {
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            cam.transform.position = follow.position + new Vector3(0f, 18f, -14f);
            cam.transform.LookAt(follow);

            var followCam = go.AddComponent<SimpleFollowCamera>();
            followCam.Target = follow;
            followCam.Offset = new Vector3(0f, 18f, -14f);
        }

        // ===== Battle 系统 =====

        private GameObject CreateBattleManager()
        {
            var go = new GameObject("BattleManager");
            go.AddComponent<BattleManager>();
            return go;
        }

        private void CreateSpawner(GameObject parent, GameObject enemyPrefab)
        {
            var go = new GameObject("EnemySpawner");
            go.transform.SetParent(parent.transform, false);
            var spawner = go.AddComponent<EnemySpawner>();
            spawner.EnemyPrefab = enemyPrefab;
            spawner.TotalCount = enemyCount;
        }

        // ===== 玩家 =====

        private PlayerController CreatePlayer(GameObject bulletPrefab)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.position = new Vector3(0f, 1f, 0f);
            Tint(go, playerColor);

            // 物理：Trigger 命中需要 Rigidbody
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            var ctrl = go.AddComponent<PlayerController>();
            ctrl.Configure(bulletPrefab);
            return ctrl;
        }

        // ===== 敌人模板 =====

        private GameObject CreateEnemyTemplate()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "EnemyTemplate";
            go.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            Tint(go, enemyColor);

            // 不要 Rigidbody（Bullet 自己带 Trigger Rigidbody 即可触发）
            go.AddComponent<EnemyController>();

            // 模板隐藏，作为 PoolManager.Spawn 的源
            go.SetActive(false);
            return go;
        }

        // ===== 子弹模板 =====

        private GameObject CreateBulletTemplate()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BulletTemplate";
            go.transform.localScale = Vector3.one * 0.3f;
            Tint(go, bulletColor);

            // Trigger 命中需要 Rigidbody（Bullet 一方）
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            go.AddComponent<Bullet>();
            go.SetActive(false);
            return go;
        }

        // ===== 工具 =====

        private static void Tint(GameObject go, Color color)
        {
            if (!go.TryGetComponent<Renderer>(out var r)) return;
            // 实例化材质，避免修改默认 sharedMaterial
            r.material.color = color;
        }
    }

    /// <summary>
    /// 极简跟随相机：保持相对玩家的固定偏移。
    /// </summary>
    public class SimpleFollowCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new(0f, 18f, -14f);
        public float SmoothTime = 0.15f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (Target == null) return;
            var desired = Target.position + Offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, SmoothTime);
            transform.LookAt(Target);
        }
    }
}
