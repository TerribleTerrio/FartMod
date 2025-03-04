using UnityEngine;
using HarmonyLib;
using GameNetcodeStuff;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
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
        static void ResetFileParams(ref GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame)
            {
                return;
            }

            if (ES3.KeyExists(Scarecrow.threatenedSaveFileKey, Scarecrow.currentSaveFile))
            {
                ES3.DeleteKey(Scarecrow.threatenedSaveFileKey, Scarecrow.currentSaveFile);
                Debug.Log($"[SCARECROW]: Reset saved data! ({Scarecrow.currentSaveFile})");
            }
            if (ES3.KeyExists(Fridge.magnetsKey, Fridge.currentSaveFile))
            {
                ES3.DeleteKey(Fridge.magnetsKey, Fridge.currentSaveFile);
                Debug.Log($"[FRIDGE]: Reset saved data! ({Fridge.currentSaveFile})");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("SaveGameValues")]
        static void SaveFileParams(ref GameNetworkManager __instance)
        {
            if (!__instance.isHostingGame || !StartOfRound.Instance.inShipPhase || StartOfRound.Instance.isChallengeFile)
            {
                return;
            }
            Fridge.Instance?.SaveData();
            ES3.Save(Scarecrow.threatenedSaveFileKey, Scarecrow.timesThreatenedInSaveFile, Scarecrow.currentSaveFile);
            Debug.Log($"[SCARECROW]: Saved new data! ({Scarecrow.currentSaveFile})");
        }
    }

    [HarmonyPatch(typeof(DeleteFileButton))]
    internal class DeleteFileButtonPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("DeleteFile")]
        static void DeleteFile(DeleteFileButton __instance)
        {
            string filePath = __instance.fileToDelete switch
            {
                0 => Info.SaveFileName1,
                1 => Info.SaveFileName2,
                2 => Info.SaveFileName3,
                _ => Info.SaveFileName1
            };

            if (ES3.FileExists(filePath))
            {
                ES3.DeleteFile(filePath);
                CoronaMod.Instance.nls.LogInfo($"Deleted save data in slot {__instance.fileToDelete}!");
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound))]
    internal class StartOfRoundPatch
    {
        public static SimpleEvent EndRoundEvent = new SimpleEvent();

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

        [HarmonyPatch("ShipHasLeft")]
        [HarmonyPostfix]
        static void ShipHasLeft()
        {
            EndRoundEvent.Invoke();
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
            otherObject.GetComponent<BalloonCollisionDetection>()?.mainScript.Pop();
            if (otherObject.GetComponent<Tire>() != null)
            {
                Tire tire = otherObject.GetComponent<Tire>();
                if (tire.currentBehaviourStateIndex == 2 && tire.IsOwner)
                {
                    Vector3 expPosition = explosionPosition + -Vector3.up;
                    float distance = Vector3.Distance(expPosition, tire.transform.position);
                    float forceMult = (distance - 0f) / (damageRange - 0f) * (0f - 3f) + 3f;
                    tire.BounceOff(expPosition, forceMultiplier: forceMult, bounceUp: true, extraForce: 50f);
                }
            }
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

    [HarmonyPatch("OnTriggerStay")]
    [HarmonyPrefix]
    static void OnTriggerStay(SpikeRoofTrap __instance, Collider other)
    {
        if (other.gameObject.GetComponent<BalloonCollisionDetection>() != null)
        {
            other.gameObject.GetComponent<BalloonCollisionDetection>().mainScript.Pop();
            return;
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
            if (__instance.turretMode == TurretMode.Firing || (__instance.turretMode == TurretMode.Berserk && !__instance.enteringBerserkMode))
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
                    otherObject.GetComponent<BalloonCollisionDetection>()?.mainScript.Pop();
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
            otherObject.GetComponent<Radiator>()?.FallOverAndSync(shotgunForward);
            otherObject.GetComponent<BalloonCollisionDetection>()?.mainScript.Pop();
            if (otherObject.GetComponent<Tire>() != null)
            {
                Tire tire = otherObject.GetComponent<Tire>();
                if (tire.currentBehaviourStateIndex == 2 && tire.IsOwner)
                {
                    float distance = Vector3.Distance(shotgunPosition, tire.transform.position);
                    float forceMult = (distance - 0f) / (15f - 0f) * (0f - 1.5f) + 1.5f;
                    tire.BounceOff(shotgunPosition, forceMultiplier: forceMult, extraForce: 20f);
                }
            }
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

[HarmonyPatch(typeof(KillLocalPlayer))]
internal class KillLocalPlayerPatch
{
    [HarmonyPatch("OnTriggerEnter")]
    [HarmonyPostfix]
    static void OnTriggerEnter(KillLocalPlayer __instance, Collider other)
    {
        if (other.gameObject.GetComponent<BalloonCollisionDetection>() != null && __instance.deathAnimation == 1)
        {
            other.gameObject.GetComponent<BalloonCollisionDetection>().mainScript.Pop();
        }
    }
}

[HarmonyPatch(typeof(PlayerControllerB))]
internal class PlayerControllerBPatch
{
    [HarmonyPatch("SwitchToItemSlot")]
    [HarmonyPrefix]
    static void SwitchToItemSlot(PlayerControllerB __instance)
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

[HarmonyPatch(typeof(DepositItemsDesk))]
internal class DepositItemsDeskPatch
{
    [HarmonyPatch("PlaceItemOnCounter")]
    [HarmonyPrefix]
    static void PlaceItemOnCounter(DepositItemsDesk __instance, PlayerControllerB playerWhoTriggered)
    {
		if (playerWhoTriggered.currentlyHeldObjectServer != null && playerWhoTriggered.currentlyHeldObjectServer.gameObject.TryGetComponent(out Balloon balloon) && __instance.deskObjectsContainer.GetComponentsInChildren<GrabbableObject>().Length < 12 && !__instance.inGrabbingObjectsAnimation && GameNetworkManager.Instance != null && playerWhoTriggered == GameNetworkManager.Instance.localPlayerController)
		{
            Debug.Log("[BALLOON]: discard held object called from deposit items desk while holding balloon!");
            balloon.DestroyBalloon();
            balloon.AddBoxCollider();
		}
    }
}

[HarmonyPatch(typeof(InteractTrigger))]
internal class InteractTriggerPatch
{
    [HarmonyPatch("ladderClimbAnimation")]
    [HarmonyPostfix]
    static IEnumerator LadderClimbAnimation(IEnumerator result, PlayerControllerB playerController)
    {
        if (playerController.currentlyHeldObjectServer != null && playerController.currentlyHeldObjectServer.gameObject.GetComponent<Balloon>() != null)
        {
            playerController.DiscardHeldObject();
        }
        while (result.MoveNext())
        {
            yield return result.Current;
        }
    }
}

[HarmonyPatch(typeof(ExtensionLadderItem))]
internal class ExtensionLadderItemPatch
{
    [HarmonyPatch("LadderAnimation")]
    [HarmonyPostfix]
    static IEnumerator LadderAnimation(IEnumerator result, ExtensionLadderItem __instance)
    {
        float prexExt = 0f;
        float prevRot = 0f;
        float currentExt() => __instance.ladderAnimator.GetFloat("extensionAmount");
        float currentRot() => __instance.ladderRotateAnimator.GetFloat("rotationAmount");
        List<Collider> collidersHit = [];
        while (result.MoveNext())
        {
            if (__instance.IsOwner)
            {
                if (ValueChanged(ref prexExt, currentExt()))
                {
                    DetectAndInteract(__instance.killTrigger, falling: false);
                }
                if (ValueChanged(ref prevRot, currentRot()))
                {
                    DetectAndInteract(__instance.killTrigger, falling: true);
                }
            }
            yield return result.Current;
        }
        bool ValueChanged(ref float lhs, float rhs)
        {
            bool change = lhs != rhs;
            lhs = change ? rhs : lhs;
            return change;
        }
        void DetectAndInteract(Collider collider, bool falling)
        {
            Vector3 pos = (falling ? collider.transform.position - collider.transform.right * 0.25f : collider.transform.position) - collider.transform.up * 2f;
            Vector3 size = new((collider as BoxCollider)!.size.x, (collider as BoxCollider)!.size.y * 4f, (collider as BoxCollider)!.size.z);
            Collider[] ladderHit = Physics.OverlapBox(pos, size, collider.transform.rotation, Masks.PlayerPropsEnemiesMapHazardsVehicle, QueryTriggerInteraction.Collide);
            if (ladderHit != null)
            {
                for (int i = 0; i < ladderHit.Length; i++)
                {
                    if (collidersHit.Contains(ladderHit[i]))
                    {
                        continue;
                    }
                    else
                    {
                        collidersHit.Add(ladderHit[i]);
                    }
                    Debug.Log($"[EXTLADDER]: Hit {ladderHit[i].gameObject.name} while {(falling? "falling!" : "extending!")}");
                    if (!falling)
                    {
                        ladderHit[i].gameObject.GetComponent<BalloonCollisionDetection>()?.mainScript.Pop();
                    }
                    else
                    {
                        if ((bool)ladderHit[i].gameObject.GetComponent<TriggerScript>())
                        {
                            if (ladderHit[i].gameObject.GetComponent<TriggerScript>().callOnTriggerEnter == "OnStringTouch")
                            {
                                return;
                            }
                            else if (currentRot() > 0.15f)
                            {
                                ladderHit[i].gameObject.GetComponent<TriggerScript>().objectScript.GetComponent<Balloon>()?.Pop();
                            }
                        }
                        ladderHit[i].gameObject.GetComponent<Toaster>()?.GetComponent<IHittable>().Hit(1, Vector3.zero);
                        if ((bool)ladderHit[i].gameObject.GetComponent<ArtilleryShellItem>())
                        {
                            SoundManager.Instance.PlayAudio1AtPositionForAllClients(ladderHit[i].transform.position, 1);
                            ladderHit[i].gameObject.GetComponent<ArtilleryShellItem>().ArmShellAndSync();
                        }
                        ladderHit[i].gameObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
                        ladderHit[i].gameObject.GetComponent<PunchingBag>()?.PunchAndSync(true);
                        ladderHit[i].gameObject.GetComponent<WhoopieCushionItem>()?.Fart();
                        ladderHit[i].gameObject.GetComponent<Vase>()?.ExplodeAndSync();
                        ladderHit[i].gameObject.GetComponent<Radiator>()?.FallOverAndSync((new Vector3(ladderHit[i].transform.position.x, 0f, ladderHit[i].transform.position.z) - new Vector3(collider.transform.position.x, 0f, collider.transform.position.z)).normalized);
                        if (ladderHit[i].gameObject.GetComponent<PlayerControllerB>() != null)
                        {
                            PlayerControllerB player = ladderHit[i].gameObject.GetComponent<PlayerControllerB>();
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
}

[HarmonyPatch(typeof(BeltBagItem))]
internal class BeltBagItemPatch
{
    [HarmonyPatch("ItemInteractLeftRight")]
    [HarmonyPrefix]
    static void ItemInteractLeftRight(BeltBagItem __instance, bool right)
    {
		if (__instance.playerHeldBy == null || __instance.tryingAddToBag || __instance.objectsInBag.Count >= 15 || right)
		{
			return;
		}
		if (Physics.Raycast(__instance.playerHeldBy.gameplayCamera.transform.position, __instance.playerHeldBy.gameplayCamera.transform.forward, out var hitInfo, 4f, 1073742144, QueryTriggerInteraction.Collide))
		{
			if (hitInfo.collider.gameObject.TryGetComponent(out FridgeMagnet magnet))
			{
				__instance.TryAddObjectToBag(magnet);
			}
		}
    }
}

[HarmonyPatch(typeof(ShipBuildModeManager))]
internal class ShipBuildModeManagerPatch
{
    [HarmonyPatch("PlayerMeetsConditionsToBuild")]
    [HarmonyPostfix]
    static void PlayerMeetsConditionsToBuild(ref bool __result)
    {
        if (!StartOfRound.Instance.inShipPhase && Fridge.Instance != null && Fridge.Instance.LocalPlayerHidingInPridge)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(MaskedPlayerEnemy))]
internal class MaskedPlayerEnemyPatch
{
    [HarmonyPatch("ChooseShipHidingSpot")]
    [HarmonyPostfix]
    static void ChooseShipHidingSpot(MaskedPlayerEnemy __instance)
    {
        if (Fridge.Instance != null && Fridge.Instance.enemyInsideFridge == null)
        {
            Debug.Log("[FRIDGE]: Masked choosing fridge as hiding spot!");
            __instance.shipHidingSpot = Fridge.Instance.ItemContainer.transform.position;
        }
    }

    public static IEnumerator LetMaskedIntoFridge(MaskedPlayerEnemy __instance)
    {
        Debug.Log("[FRIDGE]: Masked opening fridge door!");
        Fridge.Instance!.ForceOpenDoor(loud: 2);
        while (Vector3.Distance(__instance.transform.position with {y = 0f}, __instance.shipHidingSpot with {y = 0f}) > 0.4f)
        {
            yield return null;
        }
        Debug.Log("[FRIDGE]: Masked hiding in fridge!");
    }
}