using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Niantic.Lightship.AR.WorldPositioning;

public class TapPlaceAndRemove : MonoBehaviour
{
    [SerializeField] private GameObject objectToPlace; // Prefab of the object to place
    [SerializeField] private Camera arCamera; // Reference to the AR camera
    [SerializeField] private ARWorldPositioningObjectHelper objectHelper;

    private List<GameObject> placedObjects = new();
    private float lastTapTime = 0f;
    private float doubleTapDelay = 0.3f;

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began && !IsPointerOverUIObject())
            {
                Ray ray = arCamera.ScreenPointToRay(touch.position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    GameObject hitObject = hit.transform.gameObject;

                    if (placedObjects.Contains(hitObject) && Time.time - lastTapTime < doubleTapDelay)
                    {
                        // Double-tap detected: remove the object
                        placedObjects.Remove(hitObject);
                        Destroy(hitObject);
                        Debug.Log("Object removed at position: " + hit.point);
                    }
                    else
                    {
                        // Single tap detected: place the object
                        GameObject newObject = Instantiate(objectToPlace, hit.point, Quaternion.identity);
                        placedObjects.Add(newObject);

                        // Update object's positioning with AR World Positioning
                        LatLong gpsCoord = new LatLong { latitude = hit.point.z, longitude = hit.point.x }; // Example GPS values, adjust as needed
                        objectHelper.AddOrUpdateObject(newObject, gpsCoord.latitude, gpsCoord.longitude, 0, Quaternion.identity);

                        Debug.Log("Object placed at position: " + hit.point);
                    }

                    lastTapTime = Time.time;
                }
            }
        }
    }

    private bool IsPointerOverUIObject()
    {
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y)
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
        return results.Count > 0;
    }
}
