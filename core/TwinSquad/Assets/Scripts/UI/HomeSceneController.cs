using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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
        SceneManager.LoadScene(gameSceneName);
    }
}