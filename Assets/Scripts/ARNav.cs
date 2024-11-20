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

    [Header("Legacy UI References")]
    [SerializeField] private InputField _searchInputField;
    [SerializeField] private Text _distanceText;
    [SerializeField] private Button _searchButton;
    [SerializeField] private GameObject _recommendationContainer;
    [SerializeField] private GameObject _recommendationItemPrefab;

    [SerializeField] private ARWorldPositioningObjectHelper _objectHelper;
    [SerializeField] private ARWorldPositioningManager _positioningManager;

    [SerializeField] private UserLatLong _currentUserLocation;
    [SerializeField] private GameObject _directionPointer; // 3D Pointer object to show direction

    private GameObject _currentPlacedObject;
    private List<GameObject> _currentRecommendations = new List<GameObject>();

    void Start()
    {
        // Validate references
        if (_searchInputField == null || _distanceText == null ||
            _recommendationContainer == null || _recommendationItemPrefab == null)
        {
            Debug.LogError("Missing UI references in ARNav script!");
            return;
        }

        // Initially hide the recommendation container
        _recommendationContainer.SetActive(false);

        // Add listener to search input for real-time recommendations
        _searchInputField.onValueChanged.AddListener(UpdateRecommendations);

        // Add listener to search button
        _searchButton.onClick.AddListener(SearchAndPlaceLocation);

        // Validate pointer reference
        if (_directionPointer == null)
        {
            Debug.LogError("Direction Pointer is not assigned!");
        }

        // Original positioning manager setup
        if (_positioningManager != null)
        {
            _positioningManager.OnStatusChanged += OnStatusChanged;
        }
    }

    private void OnStatusChanged(WorldPositioningStatus status)
    {
        Debug.Log("Status changed to " + status);

        // Update current user location when status changes
        _currentUserLocation = new UserLatLong
        {
            userLatitude = Input.location.lastData.latitude,
            userLongitude = Input.location.lastData.longitude
        };
    }

    private void UpdateRecommendations(string searchText)
    {
        // Clear previous recommendations
        ClearRecommendations();

        // If search text is too short, don't show recommendations
        if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
        {
            _recommendationContainer.SetActive(false);
            return;
        }

        // Show the recommendation container when typing starts
        _recommendationContainer.SetActive(true);

        // Find matching locations
        var matchingLocations = _locationDatabase
            .Where(loc => loc.locationName.ToLower().Contains(searchText.ToLower()))
            .Take(5)
            .ToList();

        if (matchingLocations.Count == 0)
        {
            _recommendationContainer.SetActive(false);
            return;
        }

        // Create recommendation items
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

            Debug.Log($"Added {_currentPlacedObject.name} at {matchedLocation.location.userLatitude}, {matchedLocation.location.userLongitude}");

            float distance = CalculateDistance(_currentUserLocation, matchedLocation.location);
            _distanceText.text = $"Distance: {distance:F2} km to {matchedLocation.locationName}";

            UpdatePointerDirection(matchedLocation.location);
        }
        else
        {
            _distanceText.text = "Location not found";
        }
    }

    private void UpdatePointerDirection(UserLatLong destination)
    {
        if (_directionPointer == null || _currentUserLocation.userLatitude == 0 || _currentUserLocation.userLongitude == 0)
        {
            Debug.LogWarning("Cannot update direction pointer due to missing data or pointer object.");
            return;
        }

        Vector3 userPosition = new Vector3((float)_currentUserLocation.userLatitude, 0, (float)_currentUserLocation.userLongitude);
        Vector3 destinationPosition = new Vector3((float)destination.userLatitude, 0, (float)destination.userLongitude);
        Vector3 direction = (destinationPosition - userPosition).normalized;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        _directionPointer.transform.rotation = targetRotation;
    }

    private float CalculateDistance(UserLatLong start, UserLatLong end)
    {
        const double EarthRadius = 6371;

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
}

[System.Serializable]
public class LocationData
{
    public string locationName;
    public UserLatLong location;
}

[System.Serializable]
public struct UserLatLong
{
    public double userLatitude;
    public double userLongitude;
}
