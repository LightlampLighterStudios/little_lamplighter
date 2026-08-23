using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneNavigationButton : MonoBehaviour
{
    [SerializeField] private string sceneName;

    private void Awake()
    {
        GetComponent<Button>()?.onClick.AddListener(LoadScene);
    }

    public void Configure(string destination)
    {
        sceneName = destination;
    }

    public void LoadScene()
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
