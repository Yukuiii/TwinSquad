using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 战斗 Demo 一键启动器（2.5D Billboard 版）。
    ///
    /// 视觉：
    /// - 3D 透视相机 + 倾斜俯视角（约 50 度）
    /// - 角色/敌人/子弹用 SpriteRenderer，挂 Billboard 始终面向相机
    /// - 物理仍然是 3D（Capsule/Sphere Collider + Rigidbody）
    /// - 占位 sprite 由代码生成纯色矩形，运行时直接替换 Sprite 即可换皮
    ///
    /// 用法：
    ///   1. 在 Unity 新建空场景（如 BattleScene）
    ///   2. 创建空 GameObject，挂载本脚本
    ///   3. Play —— 自动构建场景与战斗系统
    /// </summary>
    public class BattleSceneBootstrap : MonoBehaviour
    {
        [Header("地图")]
        [SerializeField] private float groundSize = 40f;

        [Header("敌人")]
        [SerializeField] private int enemyCount = 10;

        [Header("相机")]
        [SerializeField] private Vector3 cameraOffset = new(0f, 11f, -9f);

        [Header("配色")]
        [SerializeField] private Color playerColor = new(0.25f, 0.65f, 1f);
        [SerializeField] private Color enemyColor  = new(1f, 0.3f, 0.3f);
        [SerializeField] private Color bulletColor = new(1f, 0.95f, 0.25f);
        [SerializeField] private Color groundColor = new(0.22f, 0.24f, 0.28f);

        // 占位 sprite（生成一次复用）
        private Sprite _playerSprite;
        private Sprite _enemySprite;
        private Sprite _bulletSprite;

        // 真图动画帧（找不到时回退到占位）
        private Sprite[] _playerIdleFrames;

        private void Awake()
        {
            BuildPlaceholderSprites();

            CreateGround();
            CreateLight();

            var battleMgrGo = CreateBattleManager();
            var bulletPrefab = CreateBulletTemplate();
            var enemyPrefab = CreateEnemyTemplate();
            var player = CreatePlayer(bulletPrefab);

            CreateCamera(player.transform);
            CreateSpawner(battleMgrGo, enemyPrefab);
        }

        // ===== 占位 sprite =====
        private void BuildPlaceholderSprites()
        {
            // 玩家：优先加载 Resources 真图（256×512），缺失时回退占位
            _playerIdleFrames = Resources.LoadAll<Sprite>("Sprites/Characters/Player/Idle");
            _playerSprite = (_playerIdleFrames != null && _playerIdleFrames.Length > 0)
                ? _playerIdleFrames[0]
                : CreateRectSprite(playerColor, 50, 100);

            // 敌人/子弹暂未接入真图
            _enemySprite  = CreateRectSprite(enemyColor,  45, 90);
            _bulletSprite = CreateRectSprite(bulletColor, 20, 20);
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
            cam.fieldOfView = 55f;
            cam.transform.position = follow.position + cameraOffset;
            cam.transform.LookAt(follow);

            var followCam = go.AddComponent<SimpleFollowCamera>();
            followCam.Target = follow;
            followCam.Offset = cameraOffset;
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

        // ===== 玩家（2.5D Billboard）=====

        private PlayerController CreatePlayer(GameObject bulletPrefab)
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, 1f, 0f);   // sprite 中心高 1m，底部贴地

            // 视觉：SpriteRenderer + Billboard
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _playerSprite;
            sr.sortingOrder = 10;
            go.AddComponent<Billboard>();

            // 动画：有真图帧时挂播放器（>=2 帧才循环，1 帧没必要）
            if (_playerIdleFrames != null && _playerIdleFrames.Length > 1)
            {
                var anim = go.AddComponent<SimpleSpriteAnimator>();
                anim.Play(_playerIdleFrames, newFps: 8f, newLoop: true);
            }

            // 物理：3D Capsule
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.5f;
            col.height = 2f;
            col.center = Vector3.zero;
            col.direction = 1; // Y 轴

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
            var go = new GameObject("EnemyTemplate");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _enemySprite;
            sr.sortingOrder = 5;
            go.AddComponent<Billboard>();

            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.45f;
            col.height = 1.8f;
            col.center = Vector3.zero;
            col.direction = 1;

            go.AddComponent<EnemyController>();
            go.SetActive(false);
            return go;
        }

        // ===== 子弹模板 =====

        private GameObject CreateBulletTemplate()
        {
            var go = new GameObject("BulletTemplate");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _bulletSprite;
            sr.sortingOrder = 15;
            go.AddComponent<Billboard>();

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.2f;
            col.isTrigger = true;
            col.center = Vector3.zero;

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
            r.material.color = color;
        }

        /// <summary>
        /// 生成纯色矩形 Sprite（占位用）。
        /// pivot 设在 (0.5, 0.5) 中心，配合 transform.y = height/2 让 sprite 底部贴地。
        /// PPU = 50：50 像素 = 1 世界单位。
        /// </summary>
        private static Sprite CreateRectSprite(Color color, int width, int height, float ppu = 50f)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                ppu);
        }
    }

    /// <summary>
    /// 极简跟随相机：保持相对玩家的固定偏移。
    /// </summary>
    public class SimpleFollowCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new(0f, 11f, -9f);
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
