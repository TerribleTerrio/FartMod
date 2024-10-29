using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using Unity.Netcode;

namespace CoronaMod.Patches
{

    internal class NetworkPatches
    {
        [HarmonyPatch(typeof(GameNetworkManager))]
    
        internal class GameNetworkManagerPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("Start")]
            static void AddToPrefabs(ref GameNetworkManager __instance)
            {
                NetworkManager.Singleton.AddNetworkPrefab(CoronaMod.Instance.networkPrefab);
                Debug.Log("Network prefab added to network manager!");
            }
        }

        [HarmonyPatch(typeof(StartOfRound))]

        internal class StartOfRoundPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("Awake")]

            static void SpawnNetworkHandler()
            {
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                {
                    var networkHandlerHost = Object.Instantiate(CoronaMod.Instance.networkPrefab, Vector3.zero, Quaternion.identity);
                    networkHandlerHost.GetComponent<NetworkObject>().Spawn();
                    Debug.Log("Network prefab spawned during StartOfRound Start!");
                }
            }

        }

    }

    // [HarmonyPatch]
    // public class NetworkObjectManager
    // {
        //SPAWN CUSTOM NETWORK HANDLER AT BEGINNING OF GAME
        // static GameObject networkPrefab;

        // [HarmonyPostfix, HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))] 
        // public static void Init()
        // {
        //     if (networkPrefab != null)
        //         return;
            
        //     networkPrefab = (GameObject)CoronaMod.networkbundle.LoadAsset("NetworkHandler");
        //     networkPrefab.AddComponent<NetworkHandler>();
            
        //     NetworkManager.Singleton.AddNetworkPrefab(networkPrefab);
        // }

        // [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        // static void SpawnNetworkHandler()
        // {
        //     if(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        //     {
        //         var networkHandlerHost = Object.Instantiate(networkPrefab, Vector3.zero, Quaternion.identity);
        //         networkHandlerHost.GetComponent<NetworkObject>().Spawn();
        //     }
        // }

        // //SUBSCRIBE EVENTS AT START OF GAME
        // static void SubscribeToHandler()
        // {
        //     NetworkHandler.SyncEvent += ReceivedEventFromServer;
        // }

        // //UNSUBSCRIBE EVENTS ON DISCONNECT
        // [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
        // static void UnsubscribeFromHandler()
        // {
        //     NetworkHandler.SyncEvent -= ReceivedEventFromServer;
        // }

        // static void ReceivedEventFromServer(string eventName)
        // {
        //     // Event Code Here
        // }

        // static void SendEventToClients(string eventName)
        // {
        //     if (!(NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
        //     {
        //         return;
        //     }

        //     NetworkHandler.Instance.EventClientRpc(eventName);
        // }
    // }



    [HarmonyPatch(typeof(Landmine))]
    internal class LandminePatch
    {
        [HarmonyPatch("SpawnExplosion")]
        [HarmonyPrefix]

        static void SpawnExplosion(Landmine __instance, Vector3 explosionPosition, float killRange, float damageRange)
        {
            Debug.Log("---Landmine explosion detected!---");
            Vector3 position = explosionPosition;
            Collider[] colliders = Physics.OverlapSphere(explosionPosition, damageRange, 1076363336, QueryTriggerInteraction.Collide);

            RaycastHit hitInfo;

            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject otherObject = colliders[i].gameObject;
                Debug.Log($"Explosion found object {otherObject} on layer {otherObject.layer}.");

                if (otherObject.GetComponentInParent<ArtilleryShellItem>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    otherObject.GetComponentInParent<ArtilleryShellItem>().ArmShell();
                }

                if (otherObject.GetComponent<Vase>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    if (Vector3.Distance(explosionPosition, otherObject.transform.position) < killRange)
                    {
                        otherObject.GetComponent<Vase>().Shatter(otherObject.GetComponent<Vase>().explodePrefab);
                    }
                    else
                    {
                        otherObject.GetComponent<Vase>().SprintWobble();
                    }
                }

                if (otherObject.GetComponent<PlayerControllerB>() != null)
                {
                    PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                    if (player.isHoldingObject)
                    {
                        if (player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>() != null && Vector3.Distance(explosionPosition, player.currentlyHeldObjectServer.gameObject.transform.position) < killRange)
                        {
                            if (Physics.Linecast(explosionPosition, player.currentlyHeldObjectServer.gameObject.transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                            {
                                continue;
                            }
                            Vase vase = player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>();
                            vase.Shatter(vase.explodePrefab);
                            continue;
                        }
                    }
                }

                if (otherObject.GetComponentInParent<HydraulicStabilizer>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    otherObject.GetComponentInParent<HydraulicStabilizer>().GoPsycho();
                }

            }
        }
    }

    [HarmonyPatch(typeof(ShotgunItem))]
    internal class ShotgunPatch
    {

        [HarmonyPatch("ShootGun")]
        [HarmonyPostfix]

        static void ShootGun(ShotgunItem __instance, Vector3 shotgunPosition, Vector3 shotgunForward)
        {
            RaycastHit[] colliders = Physics.SphereCastAll(shotgunPosition, 5f, shotgunForward, 15f, 1076363336, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject otherObject = colliders[i].collider.gameObject;

                if (Physics.Linecast(shotgunPosition, colliders[i].transform.position + Vector3.up * 0.3f, 1073742080, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (otherObject.GetComponentInParent<ArtilleryShellItem>() != null)
                {
                    otherObject.GetComponentInParent<ArtilleryShellItem>().ArmShell();
                    continue;
                }

                if (otherObject.GetComponentInParent<Vase>() != null)
                {
                    Vase vase = otherObject.GetComponentInParent<Vase>();
                    vase.Shatter(vase.explodePrefab);
                    continue;
                }

                if (otherObject.GetComponent<PlayerControllerB>() != null)
                {
                    PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                    if (player.isHoldingObject)
                    {
                        if (player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>() != null && Vector3.Distance(shotgunPosition, player.currentlyHeldObjectServer.gameObject.transform.position) < 25f)
                        {
                            Vase vase = player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>();
                            vase.Shatter(vase.explodePrefab);
                            continue;
                        }
                    }
                }

                if (otherObject.GetComponentInParent<HydraulicStabilizer>() != null)
                {
                    otherObject.GetComponentInParent<HydraulicStabilizer>().GoPsycho();
                    continue;
                }
            }
        }

    }
}