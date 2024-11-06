using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Niantic.Lightship.AR.WorldPositioning;
using Niantic.Lightship.AR.XRSubsystems;
using System;

public class PrePlaceWorldObjects : MonoBehaviour
{
    //[SerializeField] private List<Material> _materials = new();
    [SerializeField] private List<GameObject> _possibleObjectsToPlace = new();
    [SerializeField] private List<LatLong> _latLongs = new();
    [SerializeField] private ARWorldPositioningObjectHelper _objectHelper;
    [SerializeField] private ARWorldPositioningManager _positioningManager;


    private List<GameObject> _instantiatedObjects = new();

    void Start()
    {
        foreach (var gpsCoord in _latLongs)
        {
            GameObject newObject = Instantiate(_possibleObjectsToPlace[_latLongs.IndexOf(gpsCoord) % _possibleObjectsToPlace.Count]);
            _objectHelper.AddOrUpdateObject(newObject, gpsCoord.latitude, gpsCoord.longitude, 0, Quaternion.identity);

            Debug.Log($"Added {newObject.name} with latitude {gpsCoord.latitude} and {gpsCoord.longitude}");
        }

        _positioningManager.OnStatusChanged += OnStatusChanged;

       

        
    }

    private void OnStatusChanged(WorldPositioningStatus Status)
    {
        Debug.Log("Status changed to " + Status);
    }





}
[System.Serializable]
public struct LatLong
{
    public double latitude;
    public double longitude;
}
