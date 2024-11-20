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
        // Validate essential components
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
        // Setup search functionality
        _searchInputField.onValueChanged.AddListener(UpdateRecommendations);
        _searchButton.onClick.AddListener(SearchAndPlaceLocation);

        // Setup gyroscope toggle if available
        if (_gyroscopeToggle != null)
        {
            _gyroscopeToggle.isOn = _useGyroscope;
            _gyroscopeToggle.onValueChanged.AddListener((value) => ToggleGyroscope());
        }

        // Setup recommendation container
        SetupRecommendationContainer();
    }

    private void SetupRecommendationContainer()
    {
        if (_recommendationContainer != null)
        {
            RectTransform containerRect = _recommendationContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                // Add VerticalLayoutGroup if missing
                if (_recommendationContainer.GetComponent<VerticalLayoutGroup>() == null)
                {
                    VerticalLayoutGroup vlg = _recommendationContainer.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 5f;
                    vlg.padding = new RectOffset(5, 5, 5, 5);
                }

                // Add ContentSizeFitter if missing
                if (_recommendationContainer.GetComponent<ContentSizeFitter>() == null)
                {
                    ContentSizeFitter fitter = _recommendationContainer.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
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
    }

    private void UpdateDirectionIndicator()
    {
        Vector3 directionToDestination = GetDirectionToDestination();

        if (directionToDestination == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToDestination);

        if (_useGyroscope && _gyroEnabled)
        {
            Quaternion deviceOrientation = GetDeviceOrientation();
            Quaternion finalRotation = deviceOrientation * targetRotation;

            _directionIndicator.transform.rotation = Quaternion.Slerp(
                _directionIndicator.transform.rotation,
                finalRotation,
                Time.deltaTime * _rotationSmoothSpeed
            );
        }
        else
        {
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

        // Convert from right-handed to left-handed coordinate system
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
            _distanceText.text = "Location not found";
            UpdateStatusText("Location not found in database");
        }
    }

    private Vector3 GetDirectionToDestination()
    {
        LocationData matchedLocation = _locationDatabase.FirstOrDefault(loc =>
            loc.locationName.ToLower().Contains(_searchInputField.text.Trim().ToLower()));

        if (matchedLocation != null)
        {
            Vector3 cameraPosition = Camera.main.transform.position;
            Vector3 destinationPosition = new Vector3(
                (float)matchedLocation.location.userLongitude,
                cameraPosition.y,
                (float)matchedLocation.location.userLatitude
            );

            Vector3 direction = destinationPosition - cameraPosition;
            direction.y = 0;
            return direction.normalized;
        }
        return Vector3.zero;
    }

    public void ToggleGyroscope()
    {
        if (SystemInfo.supportsGyroscope)
        {
            _useGyroscope = !_useGyroscope;
            if (_useGyroscope)
            {
                _gyro.enabled = true;
                _gyroEnabled = true;
                UpdateStatusText("Gyroscope enabled");
            }
            else
            {
                _gyro.enabled = false;
                _gyroEnabled = false;
                UpdateStatusText("Gyroscope disabled");
            }
        }
    }

    private float CalculateDistance(UserLatLong start, UserLatLong end)
    {
        const double EarthRadius = 6371; // km

        var lat1 = ToRadians(start.userLatitude);
        var lon1 = ToRadians(start.userLongitude);
        var lat2 = ToRadians(end.userLatitude);
        var lon2 = ToRadians(end.userLongitude);

        var dlat = lat2 - lat1;
        var dlon = lon2 - lon1;

        var a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dlon / 2) * Math.Sin(dlon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return (float)(EarthRadius * c);
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    private void OnDestroy()
    {
        if (_positioningManager != null)
        {
            _positioningManager.OnStatusChanged -= OnStatusChanged;
        }
    }
}

[System.Serializable]
public class LocationData
{
    public string locationName;
    public UserLatLong location;
}

[System.Serializable]
public class UserLatLong
{
    public double userLatitude;
    public double userLongitude;

    public Vector3 ToWorldPosition()
    {
        return new Vector3((float)userLongitude, 0f, (float)userLatitude);
    }
}