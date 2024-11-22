using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using Unity.Netcode;
using System.Collections;

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

                if (otherObject.GetComponent<Toaster>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    otherObject.GetComponent<Toaster>().Eject();
                    otherObject.GetComponent<Toaster>().EjectServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                }

                if (otherObject.GetComponentInParent<ArtilleryShellItem>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    otherObject.GetComponentInParent<ArtilleryShellItem>().ArmShellAndSync();
                }

                if (otherObject.GetComponent<PunchingBag>() != null)
                {
                    if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }
                    Debug.Log($"Ray hit {hitInfo.collider}.");
                    otherObject.GetComponent<PunchingBag>().PunchAndSync(true, "Explosion");
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
                        otherObject.GetComponent<Vase>().ExplodeAndSync();
                    }
                    else
                    {
                        otherObject.GetComponent<Vase>().Wobble(2);
                        otherObject.GetComponent<Vase>().WobbleServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, 2);
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
                            vase.ExplodeAndSync();
                            continue;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(SpikeRoofTrap))]
    internal class SpikeRoofTrapPatch
    {
        [HarmonyPatch("SlamSpikeTrapSequence")]
        [HarmonyPostfix]

        static IEnumerator SlamSpikeTrapSequence(IEnumerator result, SpikeRoofTrap __instance)
        {
            while (result.MoveNext())
            {
                yield return result.Current;
            }
            Collider[] colliders = Physics.OverlapSphere(__instance.transform.position - Vector3.down * 1f, 2.5f, 1076363336, QueryTriggerInteraction.Collide);
            RaycastHit hitInfo;
            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject otherObject = colliders[i].gameObject;
                Debug.Log($"SlamSpikeTrapSequence found object {otherObject} on layer {otherObject.layer}.");

                if (otherObject.GetComponent<Toaster>() != null)
                {
                    otherObject.GetComponent<Toaster>().Eject();
                    otherObject.GetComponent<Toaster>().EjectServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                }

                if (otherObject.GetComponent<ArtilleryShellItem>() != null)
                {
                    otherObject.GetComponent<ArtilleryShellItem>().ExplodeAndSync();
                }

                if (otherObject.GetComponent<Vase>() != null)
                {
                    otherObject.GetComponent<Vase>().ExplodeAndSync();
                }
                
                if (otherObject.GetComponent<HydraulicStabilizer>() != null)
                {
                    otherObject.GetComponent<HydraulicStabilizer>().GoPsycho();
                }

                if (otherObject.GetComponent<PlayerControllerB>() != null)
                {
                    PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                    if (player.isHoldingObject)
                    {
                        if (player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>() != null)
                        {
                            Vase vase = player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>();
                            vase.ExplodeAndSync();
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(MouthDogAI))]
    internal class MouthDogAIPatch
    {
        [HarmonyPatch("OnCollideWithEnemy")]
        [HarmonyPrefix]

        static void OnCollideWithEnemy(MouthDogAI __instance, Collider other, EnemyAI collidedEnemy = null)
        {
            EnemyAI enemy = other.gameObject.GetComponent<EnemyAICollisionDetect>().mainScript;
            if (__instance.currentBehaviourStateIndex != 2 && !__instance.inLunge && enemy.enemyType.enemyName == "Scarecrow")
			{
                collidedEnemy.enemyType = __instance.enemyType;
			}
        }
    }

    [HarmonyPatch(typeof(BaboonBirdAI))]
    internal class BaboonBirdAIPatch
    {
        [HarmonyPatch("OnCollideWithEnemy")]
        [HarmonyPrefix]

        static void OnCollideWithEnemy(BaboonBirdAI __instance, Collider other, EnemyAI enemyScript = null)
        {
            EnemyAI enemy = other.gameObject.GetComponent<EnemyAICollisionDetect>().mainScript;
            if (enemy.enemyType.enemyName == "Scarecrow")
            {
                enemyScript.enemyType = __instance.enemyType;
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
            RaycastHit[] colliders = Physics.SphereCastAll(shotgunPosition, 5f, shotgunForward, 15f, 1076363336, QueryTriggerInteraction.Collide);

            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject otherObject = colliders[i].collider.gameObject;
                if (Physics.Linecast(shotgunPosition, colliders[i].transform.position + Vector3.up * 0.3f, 1073742080, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (otherObject.GetComponentInParent<ArtilleryShellItem>() != null)
                {
                    otherObject.GetComponentInParent<ArtilleryShellItem>().ArmShellAndSync();
                    continue;
                }

                if (otherObject.GetComponentInParent<Vase>() != null)
                {
                    Vase vase = otherObject.GetComponentInParent<Vase>();
                    vase.ExplodeAndSync();
                    continue;
                }

                if (otherObject.GetComponentInParent<PunchingBag>() != null)
                {
                    otherObject.GetComponentInParent<PunchingBag>().PunchAndSync(true, "Shotgun");
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
                            vase.ExplodeAndSync();
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

    [HarmonyPatch(typeof(Terminal))]
    internal class TerminalPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]

        static void TerminalAwakeScarecrow(Terminal __instance)
        {
            for (int i = 0; i < __instance.enemyFiles.Count; i++)
            {
                if (__instance.enemyFiles[i].creatureName != "Scarecrow")
                {
                    continue;
                }
                else
                {
                    __instance.enemyFiles[i].clearPreviousText = true;
                    __instance.enemyFiles[i].displayText = "Scarecrow\n\nWe led so firmly into the deep below Earth, when we saw thousands of candles burning in rows, some large, others small. Every instant some were extinguished, and others again burnt up, so that the flames seemed to leap hither and thither in perpetual change. See now, these are the lights of lives. One must go out before a new one is lighted. The little belong to the old and young, to the prime belong the large, and to you belonged the most bright and pleasant, which filled the room with warmth. It was extinguished and you fell to the ground. Your body was exhumed and adorned and revered, all clime had come to know what was your worth, and then many more candles were burning even brighter in your place.";
                    __instance.enemyFiles[i].loadImageSlowly = true;
                    __instance.enemyFiles[i].maxCharactersToType = 35;
                }
            }
        }
    }
}