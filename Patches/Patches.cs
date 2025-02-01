using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using Unity.Netcode;
using System.Collections;
namespace CoronaMod.Patches;

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

        [HarmonyPostfix]
        [HarmonyPatch("ResetSavedGameValues")]
        static void ResetScarecrowSaveFileParams(ref GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame)
            {
                return;
            }
            if (ES3.KeyExists(Scarecrow.threatenedSaveFileKey, Scarecrow.currentSaveSlotSaveFileName))
            {
                ES3.DeleteKey(Scarecrow.threatenedSaveFileKey, Scarecrow.currentSaveSlotSaveFileName);
                Debug.Log($"[SCARECROW]: Reset times threatened in current save file! ({Scarecrow.currentSaveSlotSaveFileName})");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("SaveGameValues")]
        static void SaveScarecrowSaveFileParams(ref GameNetworkManager __instance)
        {
            if (!StartOfRound.Instance.inShipPhase || StartOfRound.Instance.isChallengeFile)
            {
                return;
            }
            ES3.Save(Scarecrow.threatenedSaveFileKey, Scarecrow.timesThreatenedInSaveFile, Scarecrow.currentSaveSlotSaveFileName);
            Debug.Log($"[SCARECROW]: Saved times threatened to current save file! ({Scarecrow.currentSaveSlotSaveFileName})");
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
    static void SpawnExplosion(Vector3 explosionPosition, float killRange, float damageRange)
    {
        Debug.Log("---Landmine explosion detected!---");
        Collider[] colliders = Physics.OverlapSphere(explosionPosition, damageRange, Masks.PlayerPropsEnemiesMapHazardsVehicle, QueryTriggerInteraction.Collide);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (Physics.Linecast(explosionPosition, colliders[i].transform.position + Vector3.up * 0.3f, out var _, Masks.RoomVehicle, QueryTriggerInteraction.Ignore))
            {
                continue;
            }
            GameObject otherObject = colliders[i].gameObject;
            otherObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
            otherObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
            otherObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
            otherObject.GetComponent<PunchingBag>()?.PunchAndSync(true, "Explosion");
            otherObject.GetComponent<Balloon>()?.Pop();
            if (otherObject.GetComponent<Vase>() != null)
            {
                if (Vector3.Distance(explosionPosition, otherObject.transform.position) < killRange)
                {
                    otherObject.GetComponent<Vase>().ExplodeAndSync();
                }
                else if (!otherObject.GetComponent<Vase>().isHeld)
                {
                    otherObject.GetComponent<Vase>().WobbleAndSync(2);
                }
            }
            otherObject.GetComponent<Radiator>()?.FallOverAndSync(-(new Vector3(explosionPosition.x, 0f, explosionPosition.z) - new Vector3(otherObject.transform.position.x, 0f, otherObject.transform.position.z)).normalized);
            if (otherObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                if (player.isHoldingObject && Vector3.Distance(explosionPosition, player.currentlyHeldObjectServer.gameObject.transform.position) < killRange)
                {
                    player.currentlyHeldObjectServer.gameObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
                    player.currentlyHeldObjectServer.gameObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
                    player.currentlyHeldObjectServer.gameObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
                    player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>()?.ExplodeAndSync();
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
        Collider[] colliders = Physics.OverlapSphere(__instance.transform.position - Vector3.down * 1f, 2.5f, Masks.PlayerPropsEnemiesMapHazardsVehicle, QueryTriggerInteraction.Collide);
        for (int i = 0; i < colliders.Length; i++)
        {
            GameObject otherObject = colliders[i].gameObject;
            otherObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
            otherObject.GetComponent<ArtilleryShellItem>()?.ExplodeAndSync();
            otherObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
            otherObject.GetComponent<Vase>()?.ExplodeAndSync();
            otherObject.GetComponent<WhoopieCushionItem>()?.Fart();
            otherObject.GetComponent<Radiator>()?.FallOverAndSync(-(new Vector3(__instance.transform.position.x, 0f, __instance.transform.position.z) - new Vector3(otherObject.transform.position.x, 0f, otherObject.transform.position.z)).normalized);
            if (otherObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                if (player.isHoldingObject)
                {
                    player.currentlyHeldObjectServer.gameObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
                    player.currentlyHeldObjectServer.gameObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
                    player.currentlyHeldObjectServer.gameObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
                    player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>()?.ExplodeAndSync();
                }
            }
        }
    }
}

[HarmonyPatch(typeof(Turret))]
internal class TurretPatch
{
    static float lastInterval;

    [HarmonyPatch("Update")]
    [HarmonyPrefix]
    static void InteractUpdate(Turret __instance)
    {
        if (Time.realtimeSinceStartup - lastInterval > 0.2f)
        {
            lastInterval = Time.realtimeSinceStartup;
            if (__instance.turretMode == TurretMode.Firing || __instance.turretMode == TurretMode.Berserk)
            {
                Ray propRay = new Ray(__instance.aimPoint.position - Vector3.up * 0.25f, __instance.aimPoint.forward);
                if (Physics.Raycast(propRay, out RaycastHit propHit, 30f, Masks.PlayerPropsEnemiesMapHazardsVehicle, QueryTriggerInteraction.Ignore))
                {
                    GameObject otherObject = propHit.collider.gameObject;
                    otherObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
                    otherObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
                    otherObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
                    otherObject.GetComponent<Vase>()?.ExplodeAndSync();
                    otherObject.GetComponent<Radiator>()?.FallOverAndSync(__instance.aimPoint.forward);
                    if (otherObject.GetComponent<PlayerControllerB>() != null)
                    {
                        PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                        if (player.isHoldingObject)
                        {
                            player.currentlyHeldObjectServer.gameObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
                            player.currentlyHeldObjectServer.gameObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
                            player.currentlyHeldObjectServer.gameObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
                            player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>()?.ExplodeAndSync();
                        }
                    }
                }
            }
        }
    }
}

[HarmonyPatch(typeof(ShotgunItem))]
internal class ShotgunPatch
{
    [HarmonyPatch("ShootGun")]
    [HarmonyPostfix]
    static void ShootGun(Vector3 shotgunPosition, Vector3 shotgunForward)
    {
        RaycastHit[] colliders = Physics.SphereCastAll(shotgunPosition, 5f, shotgunForward, 15f, Masks.PlayerPropsEnemiesMapHazardsVehicle, QueryTriggerInteraction.Collide);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (Physics.Linecast(shotgunPosition, colliders[i].transform.position + Vector3.up * 0.3f, Masks.RoomVehicle, QueryTriggerInteraction.Ignore))
            {
                continue;
            }
            GameObject otherObject = colliders[i].collider.gameObject;
            otherObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
            otherObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
            otherObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
            otherObject.GetComponent<PunchingBag>()?.PunchAndSync(true, "Shotgun");
            otherObject.GetComponent<Vase>()?.ExplodeAndSync();
            otherObject.GetComponent<Balloon>()?.Pop();
            if (otherObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
                if (player.isHoldingObject && Vector3.Distance(shotgunPosition, player.currentlyHeldObjectServer.gameObject.transform.position) < 23f)
                {
                    player.currentlyHeldObjectServer.gameObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
                    player.currentlyHeldObjectServer.gameObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
                    player.currentlyHeldObjectServer.gameObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
                    player.currentlyHeldObjectServer.gameObject.GetComponent<Vase>()?.ExplodeAndSync();
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

[HarmonyPatch(typeof(DoublewingAI))]
internal class DoubleWingAIPatch
{
    [HarmonyPatch("DoAIInterval")]
    [HarmonyPostfix]
    static void DoAIInterval(DoublewingAI __instance)
    {
        if (__instance.currentBehaviourStateIndex == 0 && __instance.oddInterval && !__instance.alertingBird)
        {
            Scarecrow[] scarecrows = GameObject.FindObjectsByType<Scarecrow>(FindObjectsSortMode.None);
            if (scarecrows.Length > 0)
            {
                for (int i = 0; i < scarecrows.Length; i++)
                {
                    if (Vector3.Distance(scarecrows[i].transform.position, __instance.transform.position) < 8f)
                    {
                        __instance.alertingBird = true;
                        __instance.AlertBirdServerRpc();
                    }
                }
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
                __instance.enemyFiles[i].displayText = "Scarecrow\n\nWe led so firmly into the deep below Earth, where we saw thousands of candles burning in rows, some large, others small.\n\nSee now, these are the lights of lives. One must go out before a new one is lighted. The little belong to the old and young, to the prime belong the large, and to you belonged the most bright and pleasant, which filled the room with warmth.\n\nIt was extinguished and you fell to the ground. Your body was exhumed and adorned and revered, all clime had come to know what was your worth, and then all the more lights were burning even brighter in your place.";
                __instance.enemyFiles[i].loadImageSlowly = true;
                __instance.enemyFiles[i].maxCharactersToType = 35;
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControllerB))]
internal class PlayerControllerBPatch
{
    [HarmonyPatch("SwitchToItemSlot")]
    [HarmonyPrefix]
    static void SwitchToItemSlot(PlayerControllerB __instance, int slot, GrabbableObject fillSlotWithItem = null)
    {
        if (__instance.currentlyHeldObjectServer != null)
        {
            if (__instance.currentlyHeldObjectServer.gameObject.GetComponent<Balloon>() != null)
            {
                __instance.DiscardHeldObject();
            }
        }
    }
}
