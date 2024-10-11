using UnityEngine;
using UnityEngine.AI;

public class FridgeMagnetMaster : GrabbableObject
{
    public ExtraItemSpawnManager extraItemSpawnManager;

    public override void Start()
    {
        base.Start();
        extraItemSpawnManager.SpawnExtraItems(base.transform.position);
    }

}