using UnityEngine;
using TwinSquad.Framework;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 战斗 Demo 一键启动器（Survivor.io 风：2D 顶视 + 正交相机 + 平移跟随）。
    ///
    /// 视觉：
    /// - Orthographic 相机，纯顶视无透视；相机仅 XY 平移跟随玩家，不旋转、不 LookAt
    /// - 角色/敌人/子弹用 SpriteRenderer，永远直立画在屏幕上（无 Billboard）
    /// - 移动平面：XY（X=横向，Y=纵向，Z 仅用于深度排序）
    /// - 物理仍用 3D Collider/Rigidbody（避免 OnTrigger 重写），逻辑限制在 XY 平面
    ///
    /// 用法：
    ///   1. Unity 新建空场景
    ///   2. 创建空 GameObject 挂载本脚本
    ///   3. Play —— 自动构建场景与战斗系统
    /// </summary>
    public class BattleSceneBootstrap : MonoBehaviour
    {
        [Header("地图")]
        [SerializeField] private float groundSize = 40f;

        [Header("战斗")]
        [SerializeField] private float battleDuration = 60f;

        [Header("相机")]
        [SerializeField] private float cameraSize = 8f;     // 正交视野半高（半视野单位数）
        [SerializeField] private float cameraDepth = -10f;  // 相机 Z 偏移（远离场景，负值朝外）

        [Header("配色")]
        [SerializeField] private Color playerColor = new(0.25f, 0.65f, 1f);
        [SerializeField] private Color enemyColor  = new(1f, 0.3f, 0.3f);
        [SerializeField] private Color bulletColor = new(1f, 0.95f, 0.25f);
        [SerializeField] private Color dropColor   = new(0.3f, 0.6f, 1f);
        [SerializeField] private Color groundColor = new(0.22f, 0.24f, 0.28f);

        // 占位 sprite（生成一次复用）
        private Sprite _playerSprite;
        private Sprite _enemySprite;
        private Sprite _bulletSprite;

        // 真图渲染颜色：真图用 white 不 tint，占位用对应 color tint 上色
        private Color _bulletRenderColor = Color.white;

        // 真图动画帧（找不到时回退到占位）
        private Sprite[] _playerIdleFrames;
        private Sprite[] _enemyRunFrames;

        private void Awake()
        {
            BuildPlaceholderSprites();

            var ground = CreateGround();

            var battleMgrGo = CreateBattleManager();
            var bulletPrefab = CreateBulletTemplate();
            var dropPrefab = CreateDropTemplate();
            var enemyPrefab = CreateEnemyTemplate(dropPrefab);
            var player = CreatePlayer(bulletPrefab);

            ground.Bind(player.transform);   // 玩家创建后才能绑定跟随

            var battleCamera = CreateCamera(player.transform);
            CreateSpawner(battleMgrGo, enemyPrefab, battleCamera);
        }

        // ===== 占位 sprite =====
        private void BuildPlaceholderSprites()
        {
            // 玩家：优先加载 Resources 真图（256×512），缺失时回退占位（pivot 底部对齐）
            _playerIdleFrames = Resources.LoadAll<Sprite>("Sprites/Characters/Player/Idle");
            _playerSprite = (_playerIdleFrames != null && _playerIdleFrames.Length > 0)
                ? _playerIdleFrames[0]
                : CreateRectSprite(playerColor, 50, 100, pivot: new Vector2(0.5f, 0f));

            // 敌人：优先 Slime Run 真图，缺失则占位
            _enemyRunFrames = Resources.LoadAll<Sprite>("Sprites/Characters/Enemies/Slime/Run");
            _enemySprite = (_enemyRunFrames != null && _enemyRunFrames.Length > 0)
                ? _enemyRunFrames[0]
                : CreateRectSprite(enemyColor, 45, 90, pivot: new Vector2(0.5f, 0f));

            // 子弹：优先真图（自带颜色，不 tint），缺失则用软边圆光点 + bulletColor tint
            var realBullet = Resources.Load<Sprite>("Sprites/Effects/bullet");
            if (realBullet != null)
            {
                _bulletSprite = realBullet;
                _bulletRenderColor = Color.white;
            }
            else
            {
                _bulletSprite = CreateCircleSprite(32, ppu: 64f);
                _bulletRenderColor = bulletColor;
            }
        }

        // ===== 场景元素 =====

        /// <summary>
        /// 地面：用 SpriteRenderer + Tiled 平铺一张地砖图，挂 InfiniteGround 实现无限滚动。
        /// 优先加载 Resources/Sprites/Environments/Tiles/grass_01；缺失则用纯色平铺。
        /// </summary>
        private InfiniteGround CreateGround()
        {
            var go = new GameObject("Ground");
            go.transform.position = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            var realTile = Resources.Load<Sprite>("Sprites/Environments/Tiles/grass_01");
            sr.sprite = realTile != null
                ? realTile
                : CreateRectSprite(groundColor, 256, 256, ppu: 64f);

            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(groundSize, groundSize);
            sr.sortingOrder = -100;  // 永远最底层

            return go.AddComponent<InfiniteGround>();
        }

        /// <summary>
        /// 正交相机：纯顶视，仅 XY 平移跟随玩家，不旋转。
        /// </summary>
        private Camera CreateCamera(Transform follow)
        {
            var existingCamera = Camera.main;
            var go = existingCamera != null ? existingCamera.gameObject : new GameObject("MainCamera");
            go.name = "MainCamera";
            go.tag = "MainCamera";
            var cam = existingCamera != null ? existingCamera : go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            var offset = new Vector3(0f, 0f, cameraDepth);
            cam.transform.position = follow.position + offset;
            // 不再 LookAt：相机始终朝 +Z，2D 顶视

            var followCam = go.GetComponent<SimpleFollowCamera>();
            if (followCam == null) followCam = go.AddComponent<SimpleFollowCamera>();
            followCam.Target = follow;
            followCam.Offset = offset;
            return cam;
        }

        // ===== Battle 系统 =====

        private GameObject CreateBattleManager()
        {
            var go = new GameObject("BattleManager");
            go.AddComponent<BattleManager>();
            return go;
        }

        /// <summary>
        /// 创建敌人生成器并绑定敌人模板与刷怪相机。
        /// </summary>
        private void CreateSpawner(GameObject parent, GameObject enemyPrefab, Camera spawnCamera)
        {
            var go = new GameObject("EnemySpawner");
            go.transform.SetParent(parent.transform, false);
            var spawner = go.AddComponent<EnemySpawner>();
            spawner.EnemyPrefab = enemyPrefab;
            spawner.SpawnCamera = spawnCamera;
            spawner.BattleDuration = battleDuration;
        }

        // ===== 玩家 =====

        private PlayerController CreatePlayer(GameObject bulletPrefab)
        {
            var go = new GameObject("Player");
            go.transform.position = Vector3.zero;

            // 视觉：SpriteRenderer（不需要 Billboard——正交相机不旋转）
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _playerSprite;
            sr.sortingOrder = 10;

            // 动画：有真图帧时挂播放器，初始暂停（等待玩家移动时再播放）
            if (_playerIdleFrames != null && _playerIdleFrames.Length > 1)
            {
                var anim = go.AddComponent<SimpleSpriteAnimator>();
                anim.Play(_playerIdleFrames, newFps: 8f, newLoop: true);
                anim.Pause();
            }

            // 物理：圆形碰撞框（顶视游戏标配），中心在 transform.position（脚底）
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            col.center = Vector3.zero;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;

            var ctrl = go.AddComponent<PlayerController>();
            ctrl.Configure(bulletPrefab);

            return ctrl;
        }

        // ===== 敌人模板 =====

        // ===== 掉落物模板 =====

        private GameObject CreateDropTemplate()
        {
            var go = new GameObject("DropTemplate");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(32, ppu: 64f);
            sr.color = dropColor;
            sr.sortingOrder = 8;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.25f;
            col.isTrigger = true;
            col.center = Vector3.zero;

            go.AddComponent<DropItem>();
            go.SetActive(false);
            return go;
        }

        // ===== 敌人模板 =====

        private GameObject CreateEnemyTemplate(GameObject dropPrefab)
        {
            var go = new GameObject("EnemyTemplate");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _enemySprite;
            sr.sortingOrder = 5;

            if (_enemyRunFrames != null && _enemyRunFrames.Length > 1)
            {
                var anim = go.AddComponent<SimpleSpriteAnimator>();
                anim.Play(_enemyRunFrames, newFps: 8f, newLoop: true);
            }

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.45f;
            col.center = Vector3.zero;

            go.AddComponent<EnemyController>();
            go.GetComponent<EnemyController>().SetDropPrefab(dropPrefab);
            go.SetActive(false);
            return go;
        }

        // ===== 子弹模板 =====

        private GameObject CreateBulletTemplate()
        {
            var go = new GameObject("BulletTemplate");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _bulletSprite;
            sr.color = _bulletRenderColor;
            sr.sortingOrder = 15;

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

        /// <summary>
        /// 生成纯色矩形 Sprite（占位用）。
        /// PPU = 50：50 像素 = 1 世界单位。
        /// pivot 默认 (0.5, 0.5) 中心；传 (0.5, 0) 则底部对齐。
        /// </summary>
        private static Sprite CreateRectSprite(
            Color color,
            int width,
            int height,
            float ppu = 50f,
            Vector2? pivot = null)
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
                pivot ?? new Vector2(0.5f, 0.5f),
                ppu);
        }

        /// <summary>
        /// 生成软边圆形 Sprite（白色基底，运行时通过 color tint）。
        /// </summary>
        private static Sprite CreateCircleSprite(int size = 64, float ppu = 64f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var t = dist / radius;

                    if (t > 1f)
                    {
                        pixels[y * size + x] = new Color(1f, 1f, 1f, 0f);
                    }
                    else
                    {
                        var alpha = Mathf.SmoothStep(1f, 0f, t);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                ppu);
        }
    }

    /// <summary>
    /// 极简跟随相机：保持相对玩家的固定偏移（仅 XY 平移，不旋转）。
    /// </summary>
    public class SimpleFollowCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new(0f, 0f, -10f);
        public float SmoothTime = 0.15f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (Target == null) return;
            var desired = Target.position + Offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, SmoothTime);
            // 不再 LookAt：2D 顶视相机始终保持初始旋转（朝 +Z）
        }
    }
}
