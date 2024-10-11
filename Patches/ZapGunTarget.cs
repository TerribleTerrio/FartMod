using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class ZapGunTarget : MonoBehaviour, IShockableWithGun
{
    public GameObject parentObject;

    private ZappableObject zappableObject;

    private GrabbableObject grabbableObject;

    private void Start()
    {
        zappableObject = parentObject.GetComponent<ZappableObject>();
        grabbableObject = parentObject.GetComponent<GrabbableObject>();
    }
    
    public float GetDifficultyMultiplier()
    {
        return zappableObject.GetZapDifficulty();
    }

    public Vector3 GetShockablePosition()
    {
        return grabbableObject.transform.position;
    }

    public Transform GetShockableTransform()
    {
        return grabbableObject.transform;
    }

    public NetworkObject GetNetworkObject()
    {
        return grabbableObject.NetworkObject;
    }

    public bool CanBeShocked()
    {
        return true;
    }

    public void StopShockingWithGun()
    {
        zappableObject.StopShockingWithGun();
    }

    public void ShockWithGun(PlayerControllerB shockedByPlayer)
    {
        zappableObject.ShockWithGun(shockedByPlayer);
    }
	
}