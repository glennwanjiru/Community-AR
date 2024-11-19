using UnityEngine;
using System.IO;

public class FirebaseConfigChecker : MonoBehaviour
{
    void Start()
    {
        string desktopConfigPath = Path.Combine(Application.streamingAssetsPath, "google-services-desktop.json");
        string configPath = Path.Combine(Application.streamingAssetsPath, "google-services.json");

        Debug.Log("Streaming Assets Path: " + Application.streamingAssetsPath);
        Debug.Log("Desktop Config Exists: " + File.Exists(desktopConfigPath));
        Debug.Log("Main Config Exists: " + File.Exists(configPath));
    }
}