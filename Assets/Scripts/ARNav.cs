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

        // Original positioning manager setup
        if (_positioningManager != null)
        {
            _positioningManager.OnStatusChanged += OnStatusChanged;
        }

        // Ensure that recommendation container is correctly set up
        if (_recommendationContainer != null)
        {
            RectTransform containerRect = _recommendationContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                // Ensure a Vertical Layout Group is attached for layout management
                if (_recommendationContainer.GetComponent<VerticalLayoutGroup>() == null)
                {
                    _recommendationContainer.AddComponent<VerticalLayoutGroup>();
                }

                // Ensure Content Size Fitter is attached
                if (_recommendationContainer.GetComponent<ContentSizeFitter>() == null)
                {
                    ContentSizeFitter fitter = _recommendationContainer.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }
        }
    }

    private void OnStatusChanged(WorldPositioningStatus status)
    {
        Debug.Log("Status changed to " + status);

        // Update current user location when status changes
        // You may need to replace this with the correct status check
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
            // Hide recommendation container if no search or if input is empty
            _recommendationContainer.SetActive(false);
            return;
        }

        // Show the recommendation container when typing starts
        _recommendationContainer.SetActive(true);

        // Find matching locations
        var matchingLocations = _locationDatabase
            .Where(loc => loc.locationName.ToLower().Contains(searchText.ToLower()))
            .Take(5) // Limit to 5 recommendations
            .ToList();

        // If no recommendations found, hide the container
        if (matchingLocations.Count == 0)
        {
            _recommendationContainer.SetActive(false);
            return;
        }

        // Create recommendation items
        foreach (var location in matchingLocations)
        {
            // Use Instantiate with the parent transform
            GameObject recommendationItem = Instantiate(_recommendationItemPrefab, _recommendationContainer.transform, false);
            Debug.Log($"Instantiated: {recommendationItem.name}");

            // Configure the recommendation item
            Text recommendationText = recommendationItem.GetComponentInChildren<Text>();
            if (recommendationText != null)
            {
                recommendationText.text = location.locationName;

                // Add click listener to select this location
                Button itemButton = recommendationItem.GetComponent<Button>();
                if (itemButton != null)
                {
                    itemButton.onClick.AddListener(() => SelectRecommendation(location));
                }
            }

            // Add to tracking list
            _currentRecommendations.Add(recommendationItem);
        }

        // Force layout rebuild after new items are added
        LayoutRebuilder.ForceRebuildLayoutImmediate(_recommendationContainer.GetComponent<RectTransform>());
    }

    private void SelectRecommendation(LocationData location)
    {
        // Set the input field text to the selected location
        _searchInputField.text = location.locationName;

        // Clear recommendations
        ClearRecommendations();

        // Hide the recommendation container after selection
        _recommendationContainer.SetActive(false);

        // Trigger search to place the object
        SearchAndPlaceLocation();
    }

    private void ClearRecommendations()
    {
        // Destroy all existing recommendation items
        foreach (var item in _currentRecommendations)
        {
            Destroy(item);
        }
        _currentRecommendations.Clear();
    }

    public void SearchAndPlaceLocation()
    {
        // Clear previous object if exists
        if (_currentPlacedObject != null)
        {
            Destroy(_currentPlacedObject);
        }

        // Clear recommendations
        ClearRecommendations();

        string searchQuery = _searchInputField.text.Trim().ToLower();

        // Find matching location
        LocationData matchedLocation = _locationDatabase
            .FirstOrDefault(loc => loc.locationName.ToLower().Contains(searchQuery));

        if (matchedLocation != null)
        {
            // Select a random prefab if multiple exist
            GameObject selectedPrefab = _placePrefabs[_locationDatabase.IndexOf(matchedLocation) % _placePrefabs.Count];

            // Instantiate object at location
            _currentPlacedObject = Instantiate(selectedPrefab);

            // Position the object
            _objectHelper.AddOrUpdateObject(
                _currentPlacedObject,
                matchedLocation.location.userLatitude,
                matchedLocation.location.userLongitude,
                0,
                Quaternion.identity
            );

            Debug.Log($"Added {_currentPlacedObject.name} with latitude {matchedLocation.location.userLatitude} and {matchedLocation.location.userLongitude}");

            // Calculate and display distance
            float distance = CalculateDistance(_currentUserLocation, matchedLocation.location);
            _distanceText.text = $"Distance: {distance:F2} km to {matchedLocation.locationName}";
        }
        else
        {
            _distanceText.text = "Location not found";
        }
    }

    private float CalculateDistance(UserLatLong start, UserLatLong end)
    {
        const double EarthRadius = 6371; // Earth's radius in kilometers

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
