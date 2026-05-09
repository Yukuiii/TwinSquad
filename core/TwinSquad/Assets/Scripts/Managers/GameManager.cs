using UnityEngine;
using UnityEngine.SceneManagement;
using TwinSquad.Framework;

namespace TwinSquad.Managers
{
    /// <summary>
    /// 全局游戏入口。
    /// - 单例 + DontDestroyOnLoad
    /// - 通过 RuntimeInitializeOnLoadMethod 自动 Bootstrap，无需在场景中手动挂载
    /// - 持有所有 Manager 引用，统一生命周期
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ===== 子系统引用 =====
        public UIManager UI { get; private set; }
        // 后续按需添加：ConfigManager、SaveManager、InventoryManager、CharacterManager...

        public bool IsInitialized { get; private set; }

        // 应用启动时自动创建（任何场景启动均生效）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(GameManager));
            go.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }

        private void Init()
        {
            UI = gameObject.AddComponent<UIManager>();

            IsInitialized = true;
            Debug.Log("[GameManager] Initialized");
            EventBus.Publish(new GameInitializedEvent());
        }

        // ===== 全局工具方法 =====

        /// <summary>请求加载场景（统一入口，便于后续接入 loading UI、Addressables）。</summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GameManager] LoadScene: sceneName 为空");
                return;
            }
            EventBus.Publish(new SceneWillLoadEvent { SceneName = sceneName });
            SceneManager.LoadScene(sceneName);
        }

        private void OnApplicationQuit()
        {
            EventBus.ClearAll();
            IsInitialized = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    // ===== 全局事件定义 =====

    public struct GameInitializedEvent { }

    public struct SceneWillLoadEvent
    {
        public string SceneName;
    }
}
