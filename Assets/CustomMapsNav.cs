// Copyright 2024 Your Name. All Rights Reserved.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityInput = UnityEngine.Input;

namespace Niantic.Lightship.Maps.CustomAssets
{
    public class CustomMapsNav : MonoBehaviour
    {
        [SerializeField]
        private float _mouseScrollSpeed = 0.1f; // Speed for zooming with mouse scroll
        [SerializeField]
        private float _pinchScrollSpeed = 0.002f; // Speed for zooming with pinch
        [SerializeField]
        private float _minimumMapRadius = 10.0f; // Minimum allowable zoom level
        [SerializeField]
        private Camera _camera; // Reference to the camera
        [SerializeField]
        private LightshipMapView _mapView; // Reference to the Lightship map view

        private bool _isPinchPhase; // Indicates if a pinch gesture is occurring
        private float _lastPinchDistance; // Last distance between two fingers during a pinch
        private Vector3 _lastWorldPosition; // Last world position for panning
        private float _mapRadius; // Current radius of the map
        private bool _isPanning; // Indicates if panning is active

        private void Start()
        {
            // Validate camera and map view references
            if (_camera == null || _mapView == null)
            {
                Debug.LogError("Camera or MapView is not assigned!");
                return;
            }

            // Initialize map radius and camera size
            _mapRadius = (float)_mapView.MapRadius;
            _camera.orthographicSize = _mapRadius;

            // Start the GPS service
            StartCoroutine(StartGPS());
        }

        private void Update()
        {
            HandleMouseScroll(); // Handle mouse scrolling for zooming
            HandleTouchInput(); // Handle touch input for zooming and panning
            HandleMousePanning(); // Handle right mouse button for panning
        }

        private void HandleMouseScroll()
        {
            // Check if the mouse scroll wheel is moved
            if (UnityInput.mouseScrollDelta.y != 0)
            {
                var mousePosition = new Vector2(UnityInput.mousePosition.x, UnityInput.mousePosition.y);
                if (!EventSystem.current.IsPointerOverGameObject())
                {
                    // Calculate new map radius based on scroll delta
                    var sizeDelta = UnityInput.mouseScrollDelta.y * _mouseScrollSpeed * _mapRadius;
                    var newMapRadius = Math.Max(_mapRadius - sizeDelta, _minimumMapRadius);
                    UpdateMapRadius(newMapRadius); // Update the map radius
                }
            }
        }

        private void HandleTouchInput()
        {
            // Pinch zooming with touch
            if (UnityInput.touchCount == 2)
            {
                HandlePinchZoom(); // Handle pinch zooming
            }
            else
            {
                _isPinchPhase = false; // Reset pinch phase if not pinching
            }
        }

        private void HandleMousePanning()
        {
            // Check if the right mouse button is pressed
            if (UnityInput.GetMouseButton(1)) // Right mouse button
            {
                if (!_isPanning)
                {
                    _isPanning = true; // Start panning
                    ResetPanTouch();
                }

                Vector3 currentInputPos = new Vector3(UnityInput.mousePosition.x, UnityInput.mousePosition.y, _camera.nearClipPlane);
                var currentWorldPosition = _camera.ScreenToWorldPoint(currentInputPos);
                currentWorldPosition.y = 0.0f; // Set Y to 0 for top-down view

                // Calculate offset and update map center
                if (_lastWorldPosition != Vector3.zero)
                {
                    var offset = currentWorldPosition - _lastWorldPosition;
                    _mapView.OffsetMapCenter(offset);
                }

                _lastWorldPosition = currentWorldPosition; // Update last position
            }
            else
            {
                _isPanning = false; // Reset panning if right mouse button is released
                _lastWorldPosition = Vector3.zero; // Reset if no touch
            }
        }

        private void HandlePinchZoom()
        {
            // Get positions of the two touches
            Vector2 touch0 = UnityInput.GetTouch(0).position;
            Vector2 touch1 = UnityInput.GetTouch(1).position;
            float currentPinchDistance = Vector2.Distance(touch0, touch1);

            if (!_isPinchPhase)
            {
                // Start pinch
                _lastPinchDistance = currentPinchDistance;
                ResetPanTouch();
                _isPinchPhase = true; // Set pinch phase to true
            }
            else
            {
                // Adjust zoom based on pinch distance
                float sizeDelta = (currentPinchDistance - _lastPinchDistance) * _pinchScrollSpeed * _mapRadius;
                var newMapRadius = Math.Max(_mapRadius - sizeDelta, _minimumMapRadius);
                UpdateMapRadius(newMapRadius); // Update the map radius
                _lastPinchDistance = currentPinchDistance; // Update last pinch distance for the next frame
            }
        }

        private void UpdateMapRadius(float newMapRadius)
        {
            _mapView.SetMapRadius(newMapRadius); // Update the map radius
            _camera.orthographicSize = newMapRadius; // Adjust camera size
            _mapRadius = newMapRadius; // Set current map radius
        }

        private void ResetPanTouch()
        {
            _lastWorldPosition = Vector3.zero; // Reset the last position for a new pan
        }

        private IEnumerator StartGPS()
        {
#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
                {
                    // Wait for the user to grant permission
                    Debug.Log("Requesting location permission...");
                    yield return new WaitForSeconds(1);
                }
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                Debug.LogError("GPS is not enabled in the device settings.");
                yield break;  // Use yield break to exit if GPS is not enabled.
            }

            Input.location.Start();

            // Optionally wait for the GPS to initialize
            yield return StartCoroutine(WaitForGPSInitialization());
        }

        private IEnumerator WaitForGPSInitialization()
        {
            int maxWait = 10; // Timeout after 10 seconds
            while (Input.location.status == LocationServiceStatus.Stopped && maxWait > 0)
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }

            if (maxWait <= 0)
            {
                Debug.LogError("GPS initialization timed out.");
            }
            else
            {
                // Retrieve the initial GPS position
                UpdateMapWithGPS(Input.location.lastData);
            }
        }

        private void UpdateMapWithGPS(LocationInfo location)
        {
            // Here you can adjust the map view based on GPS data
            // Set the map center to the user's GPS location

            // Assuming that MapView has a method to set the map center (latitude, longitude)
            // For example:
            Vector2 userLocation = new Vector2(location.latitude, location.longitude);
            _mapView.SetMapCenter(userLocation); // Update map to the current GPS location
            Debug.Log($"GPS Location: Latitude {location.latitude}, Longitude {location.longitude}");
        }
    }
}
