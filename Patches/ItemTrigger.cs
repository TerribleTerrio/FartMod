using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class ItemTrigger : MonoBehaviour, IShockableWithGun
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
	bool IShockableWithGun.CanBeShocked()
	{
		return parentObject.GetComponent<ArtilleryShellItem>().explodeOnShockWithGun == true;
	}
	float IShockableWithGun.GetDifficultyMultiplier()
	{
		return 1;
	}
	NetworkObject IShockableWithGun.GetNetworkObject()
	{
		return parentObject.GetComponent<ArtilleryShellItem>().NetworkObject;
	}
	Vector3 IShockableWithGun.GetShockablePosition()
	{
		return base.transform.position;
	}
	Transform IShockableWithGun.GetShockableTransform()
	{
		return base.transform;
	}
	void IShockableWithGun.ShockWithGun(PlayerControllerB shockedByPlayer)
	{
		if (parentObject.GetComponent<ArtilleryShellItem>().explodeOnShockWithGun)
		{
			parentObject.GetComponent<ArtilleryShellItem>().Detonate();
		}
	}
	void IShockableWithGun.StopShockingWithGun()
	{
	}
}