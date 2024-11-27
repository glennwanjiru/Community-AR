using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.WorldPositioning;
using Niantic.Lightship.AR.XRSubsystems;
using System;
using System.Linq;

public class ARNav : MonoBehaviour
{
    [SerializeField] private List<LocationData> _locationDatabase = new List<LocationData>();
    [SerializeField] private List<GameObject> _placePrefabs = new List<GameObject>();

    [Header("UI References")]
    [SerializeField] private InputField _searchInputField;
    [SerializeField] private Text _distanceText;
    [SerializeField] private Button _searchButton;
    [SerializeField] private GameObject _recommendationContainer;
    [SerializeField] private GameObject _recommendationItemPrefab;
    [SerializeField] private Toggle _gyroscopeToggle;
    [SerializeField] private Text _statusText;

    [Header("AR Components")]
    [SerializeField] private ARWorldPositioningObjectHelper _objectHelper;
    [SerializeField] private ARWorldPositioningManager _positioningManager;
    [SerializeField] private UserLatLong _currentUserLocation;

    [Header("Direction Indicator")]
    [SerializeField] private GameObject _directionIndicator;
    [SerializeField] private float _rotationSmoothSpeed = 5f;
    [SerializeField] private bool _useGyroscope = true;

    private GameObject _currentPlacedObject;
    private List<GameObject> _currentRecommendations = new List<GameObject>();
    private Gyroscope _gyro;
    private bool _gyroEnabled;
    private Quaternion _baseRotation;
    private bool _isInitialized;

    void Start()
    {
        InitializeComponents();
        SetupUIElements();
        InitializeGyroscope();
        SetupPositioningManager();
    }

    private void InitializeComponents()
    {
        if (_searchInputField == null || _distanceText == null ||
            _recommendationContainer == null || _recommendationItemPrefab == null ||
            _directionIndicator == null)
        {
            Debug.LogError("Missing required components in ARNav script!");
            return;
        }

        _isInitialized = true;
        _recommendationContainer.SetActive(false);
    }

    private void SetupUIElements()
    {
        _searchInputField.onValueChanged.AddListener(UpdateRecommendations);
        _searchButton.onClick.AddListener(SearchAndPlaceLocation);

        if (_gyroscopeToggle != null)
        {
            _gyroscopeToggle.isOn = _useGyroscope;
            _gyroscopeToggle.onValueChanged.AddListener((value) => ToggleGyroscope());
        }

        SetupRecommendationContainer();
    }

    private void SetupRecommendationContainer()
    {
        RectTransform containerRect = _recommendationContainer.GetComponent<RectTransform>();
        if (containerRect != null)
        {
            if (_recommendationContainer.GetComponent<VerticalLayoutGroup>() == null)
            {
                VerticalLayoutGroup vlg = _recommendationContainer.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 5f;
                vlg.padding = new RectOffset(5, 5, 5, 5);
            }

            if (_recommendationContainer.GetComponent<ContentSizeFitter>() == null)
            {
                ContentSizeFitter fitter = _recommendationContainer.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
    }

    private void InitializeGyroscope()
    {
        if (SystemInfo.supportsGyroscope)
        {
            _gyro = Input.gyro;
            _gyro.enabled = true;
            _gyroEnabled = true;
            _baseRotation = Quaternion.Euler(90f, 0f, 0f);

            UpdateStatusText("Gyroscope initialized");
        }
        else
        {
            _gyroEnabled = false;
            _useGyroscope = false;
            if (_gyroscopeToggle != null)
            {
                _gyroscopeToggle.isOn = false;
                _gyroscopeToggle.interactable = false;
            }
            UpdateStatusText("Gyroscope not available");
        }
    }

    private void SetupPositioningManager()
    {
        if (_positioningManager != null)
        {
            _positioningManager.OnStatusChanged += OnStatusChanged;
        }
        else
        {
            UpdateStatusText("Positioning manager not found");
        }
    }

    private void UpdateStatusText(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
        }
        Debug.Log(message);
    }

    private void OnStatusChanged(WorldPositioningStatus status)
    {
        UpdateStatusText("Position status: " + status);

        if (Input.location.status == LocationServiceStatus.Running)
        {
            _currentUserLocation = new UserLatLong
            {
                userLatitude = Input.location.lastData.latitude,
                userLongitude = Input.location.lastData.longitude
            };

            if (_currentPlacedObject != null)
            {
                UpdateDirectionIndicator();
            }
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;

        if (_currentPlacedObject != null && _directionIndicator != null)
        {
            UpdateDirectionIndicator();
        }

        UpdateDynamicDistance();
    }

    private void UpdateDynamicDistance()
    {
        if (_currentPlacedObject != null && _currentUserLocation != null)
        {
            string searchQuery = _searchInputField.text.Trim().ToLower();
            LocationData matchedLocation = _locationDatabase
                .FirstOrDefault(loc => loc.locationName.ToLower().Contains(searchQuery));

            if (matchedLocation != null)
            {
                float distance = CalculateDistance(_currentUserLocation, matchedLocation.location);
                _distanceText.text = $"Distance: {distance:F2} km to {matchedLocation.locationName}";
            }
        }
    }

    private void UpdateDirectionIndicator()
    {
        if (_currentPlacedObject == null || _currentUserLocation == null)
            return;

        // Get the direction vector to the target location
        Vector3 targetPosition = _currentPlacedObject.transform.position;
        Vector3 userPosition = Camera.main.transform.position;
        Vector3 directionToTarget = (targetPosition - userPosition).normalized;

        // Adjust for the Y-axis plane (ignore elevation differences)
        directionToTarget.y = 0;

        // Get the rotation needed to point towards the target
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        if (_useGyroscope && _gyroEnabled)
        {
            // Apply gyroscope orientation to adjust indicator
            Quaternion deviceOrientation = GetDeviceOrientation();
            Quaternion adjustedRotation = targetRotation * Quaternion.Inverse(deviceOrientation);

            _directionIndicator.transform.rotation = Quaternion.Slerp(
                _directionIndicator.transform.rotation,
                adjustedRotation,
                Time.deltaTime * _rotationSmoothSpeed
            );
        }
        else
        {
            // Rotate the indicator without gyroscope adjustment
            _directionIndicator.transform.rotation = Quaternion.Slerp(
                _directionIndicator.transform.rotation,
                targetRotation,
                Time.deltaTime * _rotationSmoothSpeed
            );
        }
    }


    private Quaternion GetDeviceOrientation()
    {
        Quaternion gyroRotation = Input.gyro.attitude;

        Quaternion correctedGyroRotation = new Quaternion(
            gyroRotation.x,
            gyroRotation.y,
            -gyroRotation.z,
            -gyroRotation.w
        );

        return _baseRotation * correctedGyroRotation;
    }

    private void UpdateRecommendations(string searchText)
    {
        ClearRecommendations();

        if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
        {
            _recommendationContainer.SetActive(false);
            return;
        }

        _recommendationContainer.SetActive(true);

        var matchingLocations = _locationDatabase
            .Where(loc => loc.locationName.ToLower().Contains(searchText.ToLower()))
            .Take(5)
            .ToList();

        if (matchingLocations.Count == 0)
        {
            _recommendationContainer.SetActive(false);
            return;
        }

        foreach (var location in matchingLocations)
        {
            GameObject recommendationItem = Instantiate(_recommendationItemPrefab, _recommendationContainer.transform, false);

            Text recommendationText = recommendationItem.GetComponentInChildren<Text>();
            if (recommendationText != null)
            {
                recommendationText.text = location.locationName;

                Button itemButton = recommendationItem.GetComponent<Button>();
                if (itemButton != null)
                {
                    itemButton.onClick.AddListener(() => SelectRecommendation(location));
                }
            }

            _currentRecommendations.Add(recommendationItem);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_recommendationContainer.GetComponent<RectTransform>());
    }

    private void SelectRecommendation(LocationData location)
    {
        _searchInputField.text = location.locationName;
        ClearRecommendations();
        _recommendationContainer.SetActive(false);
        SearchAndPlaceLocation();
    }

    private void ClearRecommendations()
    {
        foreach (var item in _currentRecommendations)
        {
            Destroy(item);
        }
        _currentRecommendations.Clear();
    }

    public void SearchAndPlaceLocation()
    {
        if (_currentPlacedObject != null)
        {
            Destroy(_currentPlacedObject);
        }

        ClearRecommendations();

        string searchQuery = _searchInputField.text.Trim().ToLower();
        LocationData matchedLocation = _locationDatabase
            .FirstOrDefault(loc => loc.locationName.ToLower().Contains(searchQuery));

        if (matchedLocation != null)
        {
            GameObject selectedPrefab = _placePrefabs[_locationDatabase.IndexOf(matchedLocation) % _placePrefabs.Count];
            _currentPlacedObject = Instantiate(selectedPrefab);

            _objectHelper.AddOrUpdateObject(
                _currentPlacedObject,
                matchedLocation.location.userLatitude,
                matchedLocation.location.userLongitude,
                0,
                Quaternion.identity
            );

            float distance = CalculateDistance(_currentUserLocation, matchedLocation.location);
            _distanceText.text = $"Distance: {distance:F2} km to {matchedLocation.locationName}";

            UpdateDirectionIndicator();
            UpdateStatusText($"Placed object at {matchedLocation.locationName}");
        }
        else
        {
            _distanceText.text = "No matching location found!";
        }
    }

    private Vector3 GetDirectionToDestination()
    {
        Vector3 direction = _currentPlacedObject.transform.position - Camera.main.transform.position;
        return new Vector3(direction.x, 0, direction.z).normalized;
    }

    private void ToggleGyroscope()
    {
        _useGyroscope = !_useGyroscope;
    }

    private float CalculateDistance(UserLatLong userLocation, UserLatLong destinationLocation)
    {
        float lat1 = Mathf.Deg2Rad * (float)userLocation.userLatitude;
        float lon1 = Mathf.Deg2Rad * (float)userLocation.userLongitude;
        float lat2 = Mathf.Deg2Rad * (float)destinationLocation.userLatitude;
        float lon2 = Mathf.Deg2Rad * (float)destinationLocation.userLongitude;

        float earthRadiusKm = 6371.0f;

        float dLat = lat2 - lat1;
        float dLon = lon2 - lon1;

        float a = Mathf.Pow(Mathf.Sin(dLat / 2), 2) +
                  Mathf.Cos(lat1) * Mathf.Cos(lat2) *
                  Mathf.Pow(Mathf.Sin(dLon / 2), 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return earthRadiusKm * c;
    }
}

[Serializable]
public class LocationData
{
    public string locationName;
    public UserLatLong location;
}

[Serializable]
public class UserLatLong
{
    public double userLatitude;
    public double userLongitude;
}
