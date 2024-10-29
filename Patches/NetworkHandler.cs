using System;
using Unity.Netcode;
using UnityEngine;
namespace CoronaMod;

public class NetworkHandler : NetworkBehaviour
{
    public static NetworkHandler Instance { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            if (Instance)
            {
                Instance.gameObject.GetComponent<NetworkObject>().Despawn();
            }
        }

        Instance = this;

        Debug.Log("NetworkHandler Instance set!");

        base.OnNetworkSpawn();
    }




    //TOASTER RPCS

    [ServerRpc(RequireOwnership = false)]
    public void ToasterEjectServerRpc()
    {
        ToasterEjectClientRpc();
    }

    [ClientRpc]
    public void ToasterEjectClientRpc()
    {
        Debug.Log("HI!!!!!!!!!!!!!!!");
    }
    
}