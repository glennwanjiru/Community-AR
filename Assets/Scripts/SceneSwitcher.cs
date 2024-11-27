using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Required for UI components like Button

public class SceneSwitcher : MonoBehaviour
{
    [SerializeField] private Button button; // Reference to the UI Button
    [SerializeField] private string targetSceneName; // The name of the scene to load

    private void Start()
    {
        // Ensure the button is assigned
        if (button != null)
        {
            button.onClick.AddListener(SwitchScene); // Attach the SwitchScene method to the button
        }
        else
        {
            Debug.LogError("Button is not assigned in the Inspector!");
        }
    }

    public void SwitchScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogError("Target scene name is not set!");
        }
    }
}
