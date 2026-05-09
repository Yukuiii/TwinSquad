using UnityEngine;
using UnityEngine.UI;
using TwinSquad.Managers;

namespace TwinSquad.UI
{
    /// <summary>
    /// 主页场景控制器。仅处理"开始按钮 → 进入游戏场景"。
    /// 场景加载走 GameManager 统一入口（后续可叠加 loading UI、资源预热等）。
    /// </summary>
    public class HomeSceneController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private string gameSceneName;

        private void Awake()
        {
            startButton.onClick.AddListener(LoadGameScene);
        }

        private void OnDestroy()
        {
            startButton.onClick.RemoveListener(LoadGameScene);
        }

        private void LoadGameScene()
        {
            GameManager.Instance.LoadScene(gameSceneName);
        }
    }
}
