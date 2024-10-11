using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class ItemTrigger : MonoBehaviour
{
    public GameObject parentObject;

    public ITouchable itemComponent;

    private void Start()
    {
        itemComponent = parentObject.GetComponent<ITouchable>();
    }
    
    public void OnTriggerEnter(Collider other)
    {
        itemComponent.OnTouch(other);
    }

    public void OnTriggerExit(Collider other)
    {
        itemComponent.OnExit(other);
    }
}