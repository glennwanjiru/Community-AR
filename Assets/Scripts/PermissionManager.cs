using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class PermissionManager : MonoBehaviour
{
    // Singleton instance
    public static PermissionManager Instance { get; private set; }

    // List of common Android permissions
    public enum AndroidPermission
    {
        // Network Permissions
        INTERNET,
        ACCESS_NETWORK_STATE,
        ACCESS_WIFI_STATE,

        // Location Permissions
        ACCESS_FINE_LOCATION,
        ACCESS_COARSE_LOCATION,

        // Storage Permissions
        READ_EXTERNAL_STORAGE,
        WRITE_EXTERNAL_STORAGE,

        // Camera Permissions
        CAMERA,

        // Microphone Permissions
        RECORD_AUDIO,

        // Phone State Permissions
        READ_PHONE_STATE,
        CALL_PHONE
    }

    // Mapping of enum to actual permission strings
    private readonly Dictionary<AndroidPermission, string> permissionMap = new Dictionary<AndroidPermission, string>
    {
        { AndroidPermission.INTERNET, "android.permission.INTERNET" },
        { AndroidPermission.ACCESS_NETWORK_STATE, "android.permission.ACCESS_NETWORK_STATE" },
        { AndroidPermission.ACCESS_WIFI_STATE, "android.permission.ACCESS_WIFI_STATE" },
        { AndroidPermission.ACCESS_FINE_LOCATION, "android.permission.ACCESS_FINE_LOCATION" },
        { AndroidPermission.ACCESS_COARSE_LOCATION, "android.permission.ACCESS_COARSE_LOCATION" },
        { AndroidPermission.READ_EXTERNAL_STORAGE, "android.permission.READ_EXTERNAL_STORAGE" },
        { AndroidPermission.WRITE_EXTERNAL_STORAGE, "android.permission.WRITE_EXTERNAL_STORAGE" },
        { AndroidPermission.CAMERA, "android.permission.CAMERA" },
        { AndroidPermission.RECORD_AUDIO, "android.permission.RECORD_AUDIO" },
        { AndroidPermission.READ_PHONE_STATE, "android.permission.READ_PHONE_STATE" },
        { AndroidPermission.CALL_PHONE, "android.permission.CALL_PHONE" }
    };

    // Callback for permission request results
    public delegate void PermissionCallback(bool granted);

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Check if a specific permission is granted
    /// </summary>
    public bool HasPermission(AndroidPermission permission)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
        using (var packageManager = context.Call<AndroidJavaObject>("getPackageManager"))
        {
            string permissionString = permissionMap[permission];
            int permissionCheck = packageManager.Call<int>("checkPermission", 
                permissionString, 
                context.Call<string>("getPackageName"));
            
            // 0 means permission is granted
            return permissionCheck == 0;
        }
#else
        return true;
#endif
    }

    /// <summary>
    /// Request a single permission
    /// </summary>
    public void RequestPermission(AndroidPermission permission, PermissionCallback onPermissionResult = null)
    {
        StartCoroutine(RequestPermissionCoroutine(permission, onPermissionResult));
    }

    /// <summary>
    /// Request multiple permissions
    /// </summary>
    public void RequestPermissions(List<AndroidPermission> permissions, PermissionCallback onAllPermissionsResult = null)
    {
        StartCoroutine(RequestMultiplePermissionsCoroutine(permissions, onAllPermissionsResult));
    }

    private IEnumerator RequestPermissionCoroutine(AndroidPermission permission, PermissionCallback onPermissionResult)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (HasPermission(permission))
        {
            onPermissionResult?.Invoke(true);
            yield break;
        }

        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            string permissionString = permissionMap[permission];
            
            currentActivity.Call("requestPermissions", new string[] { permissionString }, 0);
        }

        yield return new WaitForSeconds(1f);

        bool isGranted = HasPermission(permission);
        onPermissionResult?.Invoke(isGranted);
#else
        onPermissionResult?.Invoke(true);
        yield return null;
#endif
    }

    private IEnumerator RequestMultiplePermissionsCoroutine(List<AndroidPermission> permissions, PermissionCallback onAllPermissionsResult)
    {
        bool allPermissionsGranted = true;

        foreach (var permission in permissions)
        {
            bool? permissionResult = null;

            RequestPermission(permission, (granted) =>
            {
                permissionResult = granted;
                if (!granted) allPermissionsGranted = false;
            });

            yield return new WaitUntil(() => permissionResult.HasValue);
        }

        onAllPermissionsResult?.Invoke(allPermissionsGranted);
    }

    /// <summary>
    /// Open app settings for manual permission management
    /// </summary>
    public void OpenAppSettings()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
        using (var intent = new AndroidJavaObject("android.content.Intent"))
        {
            intent.Call("setAction", "android.settings.APPLICATION_DETAILS_SETTINGS");
            
            AndroidJavaObject uri = new AndroidJavaClass("android.net.Uri")
                .CallStatic<AndroidJavaObject>("parse", "package:" + context.Call<string>("getPackageName"));
            
            intent.Call("setData", uri);
            intent.Call("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
            
            currentActivity.Call("startActivity", intent);
        }
#endif
    }

    /// <summary>
    /// Utility method to check internet connectivity
    /// </summary>
    public bool IsInternetConnected()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
        using (var connectivityManager = context.Call<AndroidJavaObject>("getSystemService", "connectivity"))
        {
            var networkInfo = connectivityManager.Call<AndroidJavaObject>("getActiveNetworkInfo");
            return networkInfo != null && networkInfo.Call<bool>("isConnected");
        }
#else
        return Application.internetReachability != NetworkReachability.NotReachable;
#endif
    }

    public void RequestBasicNetworkPermissions()
    {
        List<AndroidPermission> networkPermissions = new List<AndroidPermission>
        {
            AndroidPermission.INTERNET,
            AndroidPermission.ACCESS_NETWORK_STATE,
            AndroidPermission.ACCESS_WIFI_STATE
        };

        RequestPermissions(networkPermissions, (allGranted) =>
        {
            if (allGranted)
            {
                Debug.Log("All network permissions granted!");
            }
            else
            {
                Debug.LogWarning("Some network permissions were denied.");
            }
        });
    }
}
