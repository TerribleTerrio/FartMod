using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Tire : AnimatedItem, IHittable, ITouchable
{
    [Space(15f)]
    [Header("Tire Settings")]
    public GameObject physicsTirePrefab;

    private GameObject physicsTire;

    public GameObject tireObject;

    private Collider tireObjectCollider;

    public Rigidbody tireRigidbody;

    public float tireRadius = 0;

    public float pushForce = 5f;

    public float playerPushCollisionCooldown = 1f;

    private float collisionCooldownTimer;

    public int currentBehaviourStateIndex = 0;

    public int previousBehaviourStateIndex;

    private PlayerControllerB previousPlayerHeldBy;

    private String[] itemToolTips = ["Roll : [LMB]", "", ""];

    private String[] rollToolTips = ["Push : [LMB]", "Rotate : [A] [D]", "Carry : [Q]"];

    private float fallHeightPeak;

    private float bounceTorque = 0.2f;

    [Space(10f)]
    [Header("Movement")]
    private float rollSpeed;

    public float rollingTireSpeed = 0.1f;

    public float rollAcceleration = 1f;

    public Vector3 rollForce;

    public float rollingTireRotationSpeed = 1f;

    public float rollBumpCooldown = 0.5f;

    private float rollBumpTimer = 0f;

    private bool pushed;

    private bool pushedWhileWalking;

    private bool pushedWhileSprinting;

    [Space(10f)]
    [Header("Position Syncing")]
    public float syncPositionInterval = 0.2f;

    public float syncPositionThreshold = 0.5f;

    private float syncTimer;

    private Vector3 physicsTireServerPosition;

    private Quaternion physicsTireServerRotation;

    [Space(10f)]
    [Header("Animations")]
    public Animator rollingTireAnimator;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource[] rollingAudioSources;

    private String groundTypeTag;

    public AudioSource tireBumpAudio;

    public AudioClip switchToHoldingClip;

    public AudioClip switchToRollingCip;

    public AudioClip push;

    public AudioClip walkPush;

    public AudioClip sprintPush;

    public AudioClip[] tireBumpSFX;

    public AudioClip[] tireHitSFX;

    private Coroutine rollVolumeCoroutine;

    private Coroutine rollPitchCoroutine;

    private float rollingAudioPitch;

    // public void OnEnable()
    // {
    //     StartOfRound.Instance.StartNewRoundEvent.AddListener(StartHandlingShipLanding);
    //     CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.AddListener(StartHandlingShipLeaving);
    // }

    // public void OnDisable()
    // {
    //     StartOfRound.Instance.StartNewRoundEvent.RemoveListener(StartHandlingShipLanding);
    //     CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.RemoveListener(StartHandlingShipLeaving);
    // }

    // private void StartHandlingShipLanding()
    // {
    //     if (shipLandingCoroutine != null)
    //     {
    //         StopCoroutine(shipLandingCoroutine);
    //         shipLandingCoroutine = null;
    //     }
    //     shipLandingCoroutine = StartCoroutine(HandleShipLanding());
    // }

    // private void StartHandlingShipLeaving()
    // {
    //     if (shipLeavingCoroutine != null)
    //     {
    //         StopCoroutine(shipLeavingCoroutine);
    //         shipLeavingCoroutine = null;
    //     }
    //     shipLeavingCoroutine = StartCoroutine(HandleShipLeaving());
    // }

    // private IEnumerator HandleShipLanding()
    // {
    //     yield return null;
    //     if (isInShipRoom)
    //     {
    //         yield return new WaitUntil(() => !StartOfRound.Instance.inShipPhase);
    //         yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsAnimator.GetBool("Closed"));
    //     }
    // }

    // private IEnumerator HandleShipLeaving()
    // {
    //     yield return null;
    //     if (isInShipRoom)
    //     {
    //         yield return new WaitUntil(() => RoundManager.Instance.playersManager.shipDoorsAnimator.GetBool("Closed"));
    //         yield return new WaitForSeconds(1f);
    //         yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsEnabled);
    //     }
    // }

    public override void Start()
    {
        base.Start();
        tireObjectCollider = tireObject.GetComponent<Collider>();
        physicsTirePrefab.transform.localScale = base.originalScale;
    }

    public override void Update()
    {
        base.Update();

        if ((playerHeldBy != null) && previousPlayerHeldBy != playerHeldBy)
        {
            previousPlayerHeldBy = playerHeldBy;
        }

        if (collisionCooldownTimer > 0)
        {
            collisionCooldownTimer -= Time.deltaTime;
        }

        if (rollPitchCoroutine == null)
        {
            rollPitchCoroutine = StartCoroutine(LerpRollPitch(rollingAudioPitch, 0.9f, 1.1f));
        }
        for (int i = 0; i < rollingAudioSources.Length; i++)
        {
            rollingAudioSources[i].pitch = rollingAudioPitch;
        }

        bool touchingGround = false;

        //BEHAVIOUR STATES
        switch (currentBehaviourStateIndex)
        {

        //ITEM TIRE
        case 0:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                //ALL CLIENTS
                itemProperties.toolTips = itemToolTips;

                if (previousBehaviourStateIndex == 1)
                {
                    itemAudio.PlayOneShot(switchToHoldingClip);
                }


                //OWNER ONLY
                if (base.IsOwner)
                {
                    EnableTireObjectMeshesServerRpc(true);
                    rollForce = new Vector3(0,0,0);

                    //COMING FROM ROLLING STATE
                    if (previousBehaviourStateIndex == 1)
                    {
                        if (playerHeldBy != null)
                        {
                            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (itemProperties.weight - 1f), 1f, 10f);
                        }
                    }

                    //COMING FROM PHYSICS STATE
                    if (previousBehaviourStateIndex == 2)
                    {
                        if (physicsTire != null && !(playerHeldBy != null))
                        {
                            startFallingPosition = base.transform.position;
                            if (base.transform.parent != null)
                            {
                                startFallingPosition = base.transform.parent.InverseTransformPoint(startFallingPosition);
                            }

                            fallTime = 0f;
                            if (Physics.Raycast(base.transform.position + Vector3.up * tireRadius, Vector3.down, out var hitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
                            {
                                targetFloorPosition = hitInfo.point + itemProperties.verticalOffset * Vector3.up;
                                if (base.transform.parent != null)
                                {
                                    targetFloorPosition = base.transform.parent.InverseTransformPoint(targetFloorPosition);
                                }
                            }
                            else
                            {
                                targetFloorPosition = base.transform.localPosition;
                            }

                            SyncFallPositionServerRpc(startFallingPosition, targetFloorPosition, fallTime);
                        }
                    }

                    //ANY STATE
                    if (playerHeldBy != null)
                    {
                        SetControlTipsForItem();
                        parentObject = playerHeldBy.localItemHolder;
                        playerHeldBy.disableLookInput = false;
                        playerHeldBy.disableMoveInput = false;
                        // playerHeldBy.inSpecialInteractAnimation = false;
                    }
                    else
                    {
                        parentObject = null;
                    }
                }


                //NON-OWNERS ONLY
                if (!base.IsOwner)
                {
                    if (playerHeldBy != null)
                    {
                        parentObject = playerHeldBy.serverItemHolder;
                    }
                    else
                    {
                        parentObject = null;
                    }
                }


                //ALL CLIENTS
                SpawnPhysicsTire(false);

                tireObject.transform.position = base.transform.position;
                tireObject.transform.rotation = base.transform.rotation;
                tireObject.transform.SetParent(base.transform);

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            SetRollAudio(isTouchingGround: false, Vector3.zero);

            break;



        //ROLLING TIRE
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                //ALL CLIENTS
                itemAudio.PlayOneShot(switchToRollingCip);
                PlayAudibleNoise(noiseRange, noiseLoudness);
                rollSpeed = 0;

                tireObject.transform.position = playerHeldBy.transform.position + playerHeldBy.transform.forward * tireRadius + Vector3.up * tireRadius;
                tireObject.transform.rotation = playerHeldBy.transform.rotation;
                tireObject.transform.SetParent(playerHeldBy.transform);
                tireObject.transform.localEulerAngles = new Vector3(0f,90f,0f);


                //OWNER ONLY
                if (base.IsOwner)
                {
                    itemProperties.toolTips = rollToolTips;
                    SetControlTipsForItem();
                    playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (itemProperties.weight - 1f), 1f, 10f);
                    playerHeldBy.disableLookInput = true;
                    playerHeldBy.disableMoveInput = true;
                    // playerHeldBy.inSpecialInteractAnimation = true;
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //ALL CLIENTS
            RaycastHit groundInfo;

            //CATCH IF TIRE IS NOT HELD BY PLAYER
            if (playerHeldBy == null)
            {
                SetBehaviourStateServerRpc(0);
                return;
            }

            Vector3 tireHoldPos = playerHeldBy.transform.position + (playerHeldBy.transform.forward * tireRadius) + (Vector3.up * tireRadius);
            if (Physics.Raycast(tireHoldPos, -Vector3.up, out groundInfo, tireRadius*2 + 0.1f, 268438273, QueryTriggerInteraction.Ignore))
            {
                touchingGround = true;
            }

            //OWNER ONLY
            if (base.IsOwner)
            {
                //CHECK IF TIRE SHOULD BE DROPPED
                if (!touchingGround || playerHeldBy.isJumping || playerHeldBy.isCrouching || playerHeldBy.isPlayerDead)
                {
                    ReleaseRollingTire();
                    return;
                }

                else
                {
                    //ROLLING TIRE PLAYER INPUT/MOVEMENT
                    PlayerInputWhileRolling(groundInfo);
                }
            }

            break;



        //PHYSICS TIRE
        case 2:

            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                //ALL CLIENTS
                collisionCooldownTimer = playerPushCollisionCooldown;
                SpawnPhysicsTire(true);
                tireObject.transform.position = transform.position;
                tireObject.transform.rotation = transform.rotation;
                tireObject.transform.SetParent(transform);
                tireRigidbody = physicsTire.GetComponent<Rigidbody>();


                //OWNER ONLY
                if (base.IsOwner)
                {
                    fallHeightPeak = physicsTire.transform.position.y;
                    Rigidbody playerRigidbody = playerHeldBy.playerRigidbody;

                    playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (itemProperties.weight - 1f), 1f, 10f);
                    playerHeldBy.disableLookInput = false;
                    playerHeldBy.disableMoveInput = false;
                    // playerHeldBy.inSpecialInteractAnimation = false;
                    PlayerControllerB playerPushedBy = playerHeldBy;
                    playerHeldBy.DiscardHeldObject();
                    parentObject = physicsTire.transform;

                    tireRigidbody.AddForce(playerRigidbody.velocity * 2f, ForceMode.Impulse);
                    tireRigidbody.AddForce(rollForce * 500, ForceMode.Impulse);
                    rollForce = new Vector3(0,0,0);

                    if (pushed)
                    {
                        Vector3 force = playerPushedBy.transform.forward * pushForce;
                        float sprintMeterUsed;

                        if (pushedWhileWalking)
                        {
                            if (pushedWhileSprinting)
                            {
                                sprintMeterUsed = 0.3f;
                                PlayPushSoundServerRpc(2);
                                force *= 2f;
                            }
                            else
                            {
                                sprintMeterUsed = 0.2f;
                                PlayPushSoundServerRpc(1);
                            }
                        }
                        else
                        {
                            sprintMeterUsed = 0.1f;
                            PlayPushSoundServerRpc(0);
                            force /= 2f;
                        }

                        playerPushedBy.sprintMeter = Mathf.Clamp(playerPushedBy.sprintMeter - sprintMeterUsed, 0f, 1f);
                        force += Vector3.up;
                        tireRigidbody.AddForce(force, ForceMode.Impulse);
                    }
                    pushed = false;
                    
                    EnableTireObjectMeshesServerRpc(false);
                }


                //ALL CLIENTS
                parentObject = physicsTire.transform;
                rollSpeed = 0;


                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }


            //ENABLE PHYSICS IF OWNER SWITCHES
            if (base.IsOwner)
            {
                if (!wasOwnerLastFrame)
                {
                    EnableTirePhysics(true);
                    wasOwnerLastFrame = true;
                }
            }
            else if (wasOwnerLastFrame)
            {
                EnableTirePhysics(false);
                wasOwnerLastFrame = false;
            }


            //ALL CLIENTS
            if (Physics.Raycast(physicsTire.transform.position, -Vector3.up, out groundInfo, tireRadius*2 + 0.13f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                touchingGround = true;
            }

            if (groundInfo.collider != null)
            {
                SetRollAudio(touchingGround, tireRigidbody.velocity, groundInfo.collider.gameObject.tag);
            }
            else
            {
                SetRollAudio(touchingGround, tireRigidbody.velocity);
            }

            if (parentObject == null)
            {
                parentObject = physicsTire.transform;
            }

            //OWNER ONLY
            if (base.IsOwner)
            {
                if (physicsTire == null)
                {
                    SetBehaviourStateServerRpc(0);
                }

                //PARENT TO PHYSICSREGION
                // if (Physics.Raycast(physicsTire.transform.position, -Vector3.up, out var physRegionCheck, tireRadius*2 + 0.1f, 1342179585, QueryTriggerInteraction.Ignore))
                // {
                //     if (physRegionCheck.collider.transform.GetComponentInChildren<PlayerPhysicsRegion>() != null)
                //     {
                //         PlayerPhysicsRegion physicsRegion = physRegionCheck.collider.gameObject.transform.GetComponentInChildren<PlayerPhysicsRegion>();
                //         Debug.Log("[TIRE]: Found player physics region! Parenting physics tire.");
                //         if (transform.parent != physicsRegion.transform)
                //         {
                //             transform.parent = physicsRegion.transform;
                //         }
                //     }
                // }
                // else if (transform.parent != null)
                // {
                //     transform.parent = null;
                // }

                //CHECK IF INSIDE SHIP
                if (isInShipRoom)
                {
                    //CHECK IF SHIP IS MOVING
                    if (StartOfRound.Instance.shipIsLeaving || (!StartOfRound.Instance.shipHasLanded && !StartOfRound.Instance.inShipPhase))
                    {
                        if (!tireRigidbody.isKinematic)
                        {
                            tireRigidbody.isKinematic = true;
                        }
                    }
                    else
                    {
                        if (tireRigidbody.isKinematic)
                        {
                            tireRigidbody.isKinematic = false;
                        }
                    }
                }

                if (syncTimer < syncPositionInterval)
                {
                    syncTimer += Time.deltaTime;
                }
                else
                {
                    syncTimer = 0f;
                    if (Vector3.Distance(physicsTire.transform.position, physicsTireServerPosition) > syncPositionThreshold)
                    {
                        SyncPhysicsTireTransformServerRpc(physicsTire.transform.position, physicsTire.transform.rotation);
                    }
                    else if (Quaternion.Angle(physicsTire.transform.rotation, physicsTireServerRotation) > 5f)
                    {
                        SyncPhysicsTireTransformServerRpc(physicsTire.transform.position, physicsTire.transform.rotation);
                    }
                }

                if (touchingGround)
                {
                    //ROLLING SFX
                    if (tireRigidbody.velocity.magnitude > 0.2f)
                    {
                        PlayAudibleNoise(noiseRange, noiseLoudness);
                    }

                    //BOUNCE IF FALLEN FAR ENOUGH
                    float tireFallHeight = fallHeightPeak - groundInfo.point.y;
                    fallHeightPeak = groundInfo.point.y;
                    if (tireFallHeight > 2f)
                    {
                        float bounceForce = Mathf.Abs(tireRigidbody.velocity.y * 1.5f * 10) * groundInfo.normal.y;
                        Debug.Log($"[TIRE]: Fell from height of {tireFallHeight}, bouncing with force {bounceForce}!");
                        tireRigidbody.AddForce(groundInfo.normal * bounceForce, ForceMode.Impulse);
                        tireRigidbody.AddTorque(UnityEngine.Random.Range(-bounceTorque,bounceTorque), UnityEngine.Random.Range(-bounceTorque,bounceTorque), UnityEngine.Random.Range(-bounceTorque,bounceTorque) * bounceForce * tireRigidbody.mass);
                        PlayHitSoundServerRpc(groundInfo.normal * bounceForce);
                    }
                    else if (tireFallHeight > 0.2f)
                    {
                        PlayHitSoundServerRpc(new Vector3(0f,0f,0f));
                    }
                }
                else
                {
                    if (physicsTire.transform.position.y > fallHeightPeak)
                    {
                        fallHeightPeak = physicsTire.transform.position.y;
                    }
                }

                if ((Vector3.Angle(physicsTire.transform.forward, Vector3.up) < 30f || Vector3.Angle(-physicsTire.transform.forward, Vector3.up) < 30f) && physicsTire.GetComponent<Rigidbody>().velocity.magnitude < 1f && tireRigidbody.angularVelocity.magnitude < 1f)
                {
                    if (Vector3.Angle(-physicsTire.transform.forward, Vector3.up) < 30f)
                    {
                        SetRestingRotationServerRpc(new Vector3(90f,0f,0f));
                    }
                    else
                    {
                        SetRestingRotationServerRpc(new Vector3(-90f,0f,0f));
                    }
                    SetBehaviourStateServerRpc(0);
                    break;
                }
            }


            //NON-OWNER ONLY
            if (!base.IsOwner)
            {
                physicsTire.transform.position = Vector3.Lerp(physicsTire.transform.position, physicsTireServerPosition, Time.deltaTime * 6f);
                physicsTire.transform.rotation = Quaternion.Lerp(physicsTire.transform.rotation, physicsTireServerRotation, Time.deltaTime * 6f);
            }


            break;
        }
    }


    //SYNCING
    [ServerRpc]
    public void SetBehaviourStateServerRpc(int state)
    {
        SetBehaviourStateClientRpc(state);
    }

    [ClientRpc]
    private void SetBehaviourStateClientRpc(int state)
    {
        currentBehaviourStateIndex = state;
    }

    [ServerRpc]
    private void RotateTireObjectServerRpc(float rotateSpeed)
    {
        RotateTireObjectClientRpc(rotateSpeed);
    }

    [ClientRpc]
    private void RotateTireObjectClientRpc(float rotateSpeed)
    {
        tireObject.transform.eulerAngles += new Vector3(0,0,rotateSpeed);
    }

    [ServerRpc]
    private void SyncTireObjectTransformServerRpc()
    {
        SyncTireObjectTransformClientRpc(tireObject.transform.position, tireObject.transform.rotation);
    }

    [ClientRpc]
    private void SyncTireObjectTransformClientRpc(Vector3 tirePosition, Quaternion tireRotation)
    {
        if (!base.IsOwner)
        {
            tireObject.transform.position = tirePosition;
            tireObject.transform.rotation = tireRotation;
        }
    }

    [ServerRpc]
    private void SyncPhysicsTireTransformServerRpc(Vector3 tirePosition, Quaternion tireRotation)
    {
        SyncPhysicsTireTransformClientRpc(tirePosition, tireRotation);
    }

    [ClientRpc]
    private void SyncPhysicsTireTransformClientRpc(Vector3 tirePosition, Quaternion tireRotation)
    {
        if (physicsTire == null)
        {
            return;
        }

        physicsTireServerPosition = tirePosition;
        physicsTireServerRotation = tireRotation;
    }

    [ServerRpc]
    private void PlayPushSoundServerRpc(int sound)
    {
        PlayPushSoundClientRpc(sound);
    }

    [ClientRpc]
    private void PlayPushSoundClientRpc(int sound)
    {
        AudioClip clip = push;
        switch (sound)
        {
            case 0: clip = push; break;
            case 1: clip = walkPush; break;
            case 2: clip = sprintPush; break;
        }
        itemAudio.PlayOneShot(clip);
    }

    [ServerRpc]
    private void EnableTireObjectMeshesServerRpc(bool enable)
    {
        EnableTireObjectMeshesClientRpc(enable);
    }

    [ClientRpc]
    private void EnableTireObjectMeshesClientRpc(bool enable)
    {
        EnableTireObjectMeshes(enable);
    }

    [ServerRpc]
    private void SetRestingRotationServerRpc(Vector3 vector)
    {
        SetRestingRotationClientRpc(vector);
    }

    [ClientRpc]
    private void SetRestingRotationClientRpc(Vector3 vector)
    {
        itemProperties.restingRotation = vector;
    }

    [ServerRpc]
    private void SyncFallPositionServerRpc(Vector3 startFallPos, Vector3 targetFloorPos, float time)
    {
        SyncFallPositionClientRpc(startFallPos, targetFloorPos, time);
    }

    [ClientRpc]
    private void SyncFallPositionClientRpc(Vector3 startFallPos, Vector3 targetFloorPos, float time)
    {
        startFallingPosition = startFallPos;
        targetFloorPosition = targetFloorPos;
        fallTime = time;
    }

    [ServerRpc(RequireOwnership = false)]
    private void CollideWithLocalPlayerServerRpc(int playerId)
    {
        ChangeOwnershipOfProp((ulong)playerId);
        CollideWithLocalPlayerClientRpc(playerId);
    }

    [ClientRpc]
    private void CollideWithLocalPlayerClientRpc(int playerId)
    {
        if (IsOwner)
        {
            Debug.Log("[TIRE]: Player collision called from client!");
            Vector3 tireVelocity = tireRigidbody.velocity;
            if (tireVelocity.magnitude > 1.5f)
            {
                BumpPlayerServerRpc(playerId, tireVelocity);
            }
        }
    }

    [ServerRpc]
    private void BumpPlayerServerRpc(int playerId, Vector3 tireVelocity)
    {
        BumpPlayerClientRpc(playerId, tireVelocity);
    }

    [ClientRpc]
    private void BumpPlayerClientRpc(int playerId, Vector3 tireVelocity)
    {
        if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId == playerId)
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            float dirDifference = (Vector3.Normalize(tireVelocity) - Vector3.Normalize(player.walkForce)).magnitude;

            int damage = Mathf.RoundToInt(Mathf.Clamp(tireVelocity.magnitude - 5, 0f, 100f) * 5f * dirDifference);
            Vector3 pushDirection = Vector3.Normalize(player.gameplayCamera.transform.position - transform.position);
            Vector3 pushUpForce = new Vector3(0f,0f,0f);

            if (player.jumpCoroutine != null)
            {
                pushDirection = Vector3.Normalize(new Vector3(pushDirection.x, 0f, pushDirection.z));
            }
            else
            {
                pushUpForce = Vector3.up * Mathf.Clamp(tireVelocity.magnitude, 0f, 10f);
            }

            Vector3 pushDirectionForce = pushDirection * Mathf.Clamp(tireVelocity.magnitude * 2, 10f, 100f);
            Vector3 pushForce = (pushDirectionForce + pushUpForce) * dirDifference;

            //PUSH PLAYER
            PreventPlayerJump(player);
            player.externalForceAutoFade += pushForce;

            if (damage > 0)
            {
                player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Bludgeoning, force: pushForce);
            }

            // BounceOffPlayerServerRpc(player.transform.position, dirDifference);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void BounceOffNonOwnerPlayerServerRpc(Vector3 playerPos, Vector3 tirePos, float dirDifference)
    {
        BounceOffNonOwnerPlayerClientRpc(playerPos, tirePos, dirDifference);
    }

    [ClientRpc]
    private void BounceOffNonOwnerPlayerClientRpc(Vector3 playerPos, Vector3 tirePos, float dirDifference)
    {
        if (IsOwner)
        {
            Debug.Log("[TIRE]: Bouncing off non-owner player!");
            BounceOff(playerPos, forceMultiplier: Mathf.Clamp(dirDifference, 0.5f, 1f), extraForce: 5f, overrideTirePosition: true, tirePosition: tirePos);
        }
    }

    [ServerRpc]
    private void CollideWithTireServerRpc(NetworkObjectReference tireRef, Vector3 bouncePosition)
    {
        Debug.Log("[TIRE]: Calling CollideWithTireServerRpc!");
        CollideWithTireClientRpc(tireRef, bouncePosition);
    }

    [ClientRpc]
    private void CollideWithTireClientRpc(NetworkObjectReference tireRef, Vector3 bouncePosition)
    {
        Tire otherTire = ((NetworkObject)tireRef).gameObject.GetComponent<Tire>();
        if (otherTire.currentBehaviourStateIndex == 2 && GameNetworkManager.Instance.localPlayerController.playerClientId == otherTire.OwnerClientId)
        {
            otherTire.BounceOff(bouncePosition);
        }
    }

    [ServerRpc]
    private void PlayHitSoundServerRpc(Vector3 force)
    {
        PlayHitSoundClientRpc(force);
    }

    [ClientRpc]
    private void PlayHitSoundClientRpc(Vector3 force)
    {
        tireBumpAudio.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
        if (force.magnitude > 50f)
        {
            tireBumpAudio.PlayOneShot(tireHitSFX[UnityEngine.Random.Range(0,tireHitSFX.Length)]);
            PlayAudibleNoise(noiseRange, noiseLoudness);
        }
        else
        {
            tireBumpAudio.PlayOneShot(tireBumpSFX[UnityEngine.Random.Range(0,tireHitSFX.Length)]);
            PlayAudibleNoise(noiseRange, noiseLoudness);
        }
    }

    // [ServerRpc]
    // private void DamageOtherPlayerServerRpc(int playerId)
    // {
    //     DamageOtherPlayerClientRpc(playerId);
    // }

    // [ClientRpc]
    // private void DamageOtherPlayerClientRpc(int playerId)
    // {
    //     if ((int)StartOfRound.Instance.NetworkManager.LocalClientId == playerId)
    //     {
    //         StartOfRound.Instance.localPlayerController.DamagePlayer(1, causeOfDeath: CauseOfDeath.Bludgeoning);
    //     }
    // }



    //USING ITEM
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (base.IsOwner)
        {
            if (IsInCollider())
            {
                Debug.Log("[TIRE]: No space for tire!");
                StartCoroutine(DisplayCursorTip("[No room!]"));
                return;
            }

            else
            {
                if (currentBehaviourStateIndex == 0)
                {
                    SetBehaviourStateServerRpc(1);
                    return;
                }
                else if (currentBehaviourStateIndex == 1)
                {
                    pushed = true;
                    SetBehaviourStateServerRpc(2);
                    return;
                }
            }
        }
    }

    public override void ItemInteractLeftRight(bool right)
    {
        base.ItemInteractLeftRight(right);

        if (base.IsOwner)
        {
            if (currentBehaviourStateIndex == 1)
            {
                if (!right)
                {
                    SetBehaviourStateServerRpc(0);
                }
            }
        }
    }

    public override void GrabItem()
    {
        base.transform.localScale = originalScale;
        base.GrabItem();
        base.playerHeldBy.equippedUsableItemQE = true;
        currentUseCooldown = useCooldown;

        if (base.IsOwner)
        {
            if (currentBehaviourStateIndex == 0)
            {
                
            }

            if (currentBehaviourStateIndex == 2)
            {
                SetBehaviourStateServerRpc(0);
            }
        }
    }

    public override void DiscardItem()
    {
        base.playerHeldBy.equippedUsableItemQE = false;
        base.playerHeldBy.disableLookInput = false;
        base.playerHeldBy.disableMoveInput = false;
        // playerHeldBy.inSpecialInteractAnimation = false;
        base.DiscardItem();
        base.transform.localScale = originalScale;
    }

    private bool IsInCollider()
    {
        Collider[] foundColliders = Physics.OverlapBox(playerHeldBy.transform.position + playerHeldBy.transform.forward * tireRadius + Vector3.up * tireRadius, new Vector3(tireRadius/4f,tireRadius/4f,tireRadius/4f), playerHeldBy.transform.rotation, 1342179585, QueryTriggerInteraction.Ignore);

        if (foundColliders.Length > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }



    //ENABLING & DISABLING TIRES
    private void EnableTireObjectMeshes(bool enable)
    {
        MeshRenderer[] meshRenderers = tireObject.GetComponentsInChildren<MeshRenderer>();

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshRenderers[i].enabled = enable;
        }
    }

    private void EnableTirePhysics(bool enable)
    {
        if (physicsTire != null)
        {
            physicsTire.GetComponent<Rigidbody>().isKinematic = !enable;
        }
    }

    private void SpawnPhysicsTire(bool enable)
    {
        if (enable && physicsTire == null)
        {
            Quaternion tireRotation = Quaternion.Euler(tireObject.transform.eulerAngles);
            physicsTire = Instantiate(physicsTirePrefab, tireObject.transform.position, tireRotation);
            physicsTire.GetComponentInChildren<TireReferenceScript>().mainScript = this;
            Rigidbody rigidbody = physicsTire.GetComponent<Rigidbody>();
            if (!base.IsOwner)
            {
                rigidbody.isKinematic = true;
            }
            rigidbody.maxAngularVelocity = 40f;
            rigidbody.maxLinearVelocity = 40f;
        }
        else if (!enable && physicsTire != null)
        {
            Destroy(physicsTire);
        }
    }

    public void EnableSpecialAnimation(PlayerControllerB player, bool enable)
    {
        if (enable)
        {
            player.inSpecialInteractAnimation = true;
            player.clampLooking = true;
            player.minVerticalClamp = 25f;
            player.maxVerticalClamp = -60f;
            player.horizontalClamp = 60f;
            player.gameplayCamera.transform.eulerAngles = Vector3.zero;
        }
        else
        {
            player.gameplayCamera.transform.localEulerAngles = Vector3.zero;
            player.inSpecialInteractAnimation = false;
            player.clampLooking = false;
        }
    }

    private void ReleaseRollingTire()
    {
        if (IsInCollider())
        {
            Debug.Log("[TIRE]: Released while in collider!");
            tireObject.transform.position = playerHeldBy.transform.position;
        }
        SyncTireObjectTransformServerRpc();
        SetBehaviourStateServerRpc(2);
    }



    //COLLISION
    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        //CHECK IF WALL BETWEEN TIRE AND COLLIDER
        RaycastHit hitInfo;
        if (Physics.Linecast(transform.position, other.transform.position, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("[TIRE]: Wall found between collider and tire, abandoning collision!");
            return;
        }

        //IN ITEM STATE
        if (currentBehaviourStateIndex == 0)
        {

        }

        //IN ROLLING STATE
        else if (currentBehaviourStateIndex == 1)
        {

        }

        //IN PHYSICS STATE
        else if (currentBehaviourStateIndex == 2 && physicsTire != null)
        {
            //PLAYER COLLISION
            if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

                //FOR CLIENT OF PLAYER COLLIDED WITH
                if (player == GameNetworkManager.Instance.localPlayerController)
                {
                    Debug.Log("[TIRE]: This player collided with tire!");

                    //CHECK IF TIRE IS NOT ON COOLDOWN OR HELD
                    if ((collisionCooldownTimer > 0) || player == playerHeldBy)
                    {
                        return;
                    }

                    else
                    {
                        // CollideWithLocalPlayerServerRpc((int)player.playerClientId);
                        CollideWithPlayer();
                        collisionCooldownTimer = playerPushCollisionCooldown;
                        return;
                    }
                }

                else
                {
                    return;
                }
            }

            if (!base.IsOwner)
            {
                return;
            }

            //ENEMY COLLISION
            else if (otherObject.GetComponent<EnemyAICollisionDetect>() != null)
            {
                collisionCooldownTimer = 0f;
                float speed = physicsTire.GetComponent<Rigidbody>().velocity.magnitude;
                EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
                
                if (enemy.mainScript as SandSpiderAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                    return;
                }

                if (enemy.mainScript as FlowermanAI != null)
                {
                    FlowermanAI flowerman = enemy.mainScript as FlowermanAI;
                    if (tireRigidbody.velocity.magnitude > 5)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 5f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 5f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    if (flowerman.isInAngerMode)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                        return;
                    }
                    else
                    {
                        return;
                    }
                }

                if (enemy.mainScript as ButlerEnemyAI != null)
                {
                    if (enemy.mainScript.currentBehaviourStateIndex == 2)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 5);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                    }
                    return;
                }

                else if (enemy.mainScript as SpringManAI != null)
                {
                    SpringManAI springman = enemy.mainScript as SpringManAI;
                    if (!springman.hasStopped)
                    {
                        BounceOff(enemy.mainScript.transform.position);
                        return;
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                        return;
                    }
                }

                else if (enemy.mainScript as JesterAI != null)
                {
                    JesterAI jester = enemy.mainScript as JesterAI;
                    if (jester.creatureAnimator.GetBool("poppedOut"))
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 6);
                        return;
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position);
                        return;
                    }
                }

                else if (enemy.mainScript as DressGirlAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position);
                    return;
                }

                else if (enemy.mainScript as CaveDwellerAI != null)
                {
                    CaveDwellerAI caveDweller = enemy.mainScript as CaveDwellerAI;

                    if (caveDweller.hasPlayerFoundBaby)
                    {
                        if (tireRigidbody.velocity.magnitude > 2)
                        {
                            float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 2f, 10f);
                            int damage = Mathf.RoundToInt(Remap(clampedSpeed, 2f, 10f, 1f, 2f));
                            enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                        }
                        if (caveDweller.playerHolding)
                        {
                            return;
                        }
                        else
                        {
                            BounceOff(enemy.mainScript.transform.position);
                            return;
                        }
                    }

                    else if (caveDweller.adultContainer.activeSelf)
                    {
                        if (tireRigidbody.velocity.magnitude > 5)
                        {
                            float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 5f, 30f);
                            int damage = Mathf.RoundToInt(Remap(clampedSpeed, 5f, 30f, 1f, 3f));
                            enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                        }
                        BounceOff(enemy.mainScript.transform.position, extraForce: 5f);
                        return;
                    }
                    else
                    {
                        return;
                    }
                }

                else if (enemy.mainScript as MaskedPlayerEnemy != null)
                {
                    MaskedPlayerEnemy masked = enemy.mainScript as MaskedPlayerEnemy;
                    if (tireRigidbody.velocity.magnitude > 3)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 3f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 3f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    if (masked.sprinting)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 2f);
                        return;
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position);
                        return;
                    }
                }

                else if (enemy.mainScript as CrawlerAI != null)
                {
                    CrawlerAI crawler = enemy.mainScript as CrawlerAI;
                    if (tireRigidbody.velocity.magnitude > 6)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 6f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 6f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    if (crawler.hasEnteredChaseMode)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 5f);
                        return;
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position);
                        return;
                    }
                }

                else if (enemy.mainScript as SandWormAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 100f);
                    return;
                }

                else if (enemy.mainScript as MouthDogAI != null)
                {
                    if (tireRigidbody.velocity.magnitude > 10)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 10f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 10f, 30f, 1f, 2f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    MouthDogAI mouthDog = enemy.mainScript as MouthDogAI;
                    if (mouthDog.inLunge)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 35f);
                        return;
                    }
                    else if (mouthDog.hasEnteredChaseModeFully)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 15f);
                        return;
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 5f);
                        return;
                    }
                }

                else if (enemy.mainScript as ForestGiantAI != null)
                {
                    ForestGiantAI forestGiant = enemy.mainScript as ForestGiantAI;
                    if (forestGiant.chasingPlayer)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 15f);
                        return;
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 5f);
                        return;
                    }
                }

                else if (enemy.mainScript as RadMechAI != null)
                {
                    RadMechAI radMech = enemy.mainScript as RadMechAI;
                    if (radMech.chargingForward)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 35f);
                        return;
                    }
                    else if (radMech.isAlerted)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 15f);
                        return;
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 5f);
                        return;
                    }
                }

                else if (enemy.mainScript as FlowerSnakeEnemy != null)
                {
                    FlowerSnakeEnemy flowerSnake = enemy.mainScript as FlowerSnakeEnemy;
                    if (tireRigidbody.velocity.magnitude > 3)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 3f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 3f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    if (flowerSnake.clingingToPlayer)
                    {
                        return;
                    }
                    else if (flowerSnake.leaping)
                    {
                        BounceOff(enemy.mainScript.transform.position);
                        return;
                    }
                    else
                    {
                        return;
                    }
                }

                else if (enemy.mainScript as CentipedeAI != null)
                {
                    if (tireRigidbody.velocity.magnitude > 5)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 5f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 5f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    BounceOff(enemy.mainScript.transform.position);
                    return;
                }

                else if (enemy.mainScript as BaboonBirdAI != null)
                {
                    if (tireRigidbody.velocity.magnitude > 5)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 5f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 5f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                    return;
                }

                else if (enemy.mainScript as Scarecrow != null)
                {
                    if (tireRigidbody.velocity.magnitude > 3)
                    {
                        enemy.mainScript.HitEnemy(force: 1, playHitSFX: true);
                    }
                    BounceOff(enemy.mainScript.transform.position);
                    return;
                }

                else if (enemy.mainScript as HoarderBugAI != null)
                {
                    if (tireRigidbody.velocity.magnitude > 5)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 5f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 5f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    BounceOff(enemy.mainScript.transform.position);
                    return;
                }

                else if (enemy.mainScript as NutcrackerEnemyAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 2);
                }

                else if (enemy.mainScript as PufferAI != null)
                {
                    if (tireRigidbody.velocity.magnitude > 5)
                    {
                        float clampedSpeed = Mathf.Clamp(tireRigidbody.velocity.magnitude, 5f, 30f);
                        int damage = Mathf.RoundToInt(Remap(clampedSpeed, 5f, 30f, 1f, 3f));
                        enemy.mainScript.HitEnemy(force: damage, playHitSFX: true);
                    }
                    BounceOff(enemy.mainScript.transform.position);
                    return;
                }
            }

            //GRABBABLE OBJECT COLLISION
            else if (otherObject.GetComponent<GrabbableObject>() != null)
            {
                GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();
                Debug.Log("[TIRE]: Collided with grabbable object!");

                switch (gObject)
                {
                    case SoccerBallProp ball:
                        if (!ball.isHeld && !ball.isHeldByEnemy)
                        {
                            ball.BeginKickBall(base.transform.position + Vector3.up, hitByEnemy: false);
                            BounceOff(ball.transform.position, forceMultiplier: 0.5f);
                        }
                        return;
                }
            }

            //TIRE COLLISION
            else if (otherObject.GetComponent<TireReferenceScript>() != null)
            {
                TireReferenceScript tireReferenceScript = otherObject.GetComponent<TireReferenceScript>();
                if (tireReferenceScript.mainScript.gameObject == this.gameObject)
                {
                    return;
                }

                Debug.Log("[TIRE]: Collided with another tire!");
                NetworkObjectReference tireRef = tireReferenceScript.mainScript.gameObject.GetComponent<NetworkObject>();
                
                if (collisionCooldownTimer > 0)
                {
                    Debug.Log("[TIRE: Collision on cooldown!]");
                    return;
                }

                else if (tireRigidbody.velocity.magnitude > 1.5f)
                {
                    BounceOff(otherObject.transform.position);
                    CollideWithTireServerRpc(tireRef, physicsTire.transform.position);
                    collisionCooldownTimer = 0.1f;
                    otherObject.GetComponent<TireReferenceScript>().mainScript.collisionCooldownTimer = 0.1f;
                }
            }
        }
    }

    public void OnExit(Collider other)
    {

    }

    private void CollideWithPlayer()
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        float dirDifference = (Vector3.Normalize(tireRigidbody.velocity) - Vector3.Normalize(player.walkForce)).magnitude;

        int damage = Mathf.RoundToInt(Mathf.Clamp(tireRigidbody.velocity.magnitude - 5, 0f, 100f) * 5f * dirDifference);
        Vector3 pushDirection = Vector3.Normalize(player.gameplayCamera.transform.position - transform.position);
        Vector3 pushUpForce = new Vector3(0f,0f,0f);

        if (player.jumpCoroutine != null)
        {
            pushDirection = Vector3.Normalize(new Vector3(pushDirection.x, 0f, pushDirection.z));
        }
        else
        {
            pushUpForce = Vector3.up * Mathf.Clamp(tireRigidbody.velocity.magnitude, 0f, 10f);
        }

        Vector3 pushDirectionForce = pushDirection * Mathf.Clamp(tireRigidbody.velocity.magnitude * 2, 10f, 100f);
        Vector3 pushPlayerForce = (pushDirectionForce + pushUpForce) * dirDifference;

        //PUSH PLAYER
        PreventPlayerJump(player);
        player.externalForceAutoFade += pushPlayerForce;

        if (damage > 0)
        {
            player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Bludgeoning, force: pushPlayerForce);
        }

        //BOUNCE TIRE
        // BounceOff(player.transform.position, forceMultiplier: Mathf.Clamp(dirDifference, 0.5f, 1f), extraForce: 5f);
        if (IsOwner)
        {
            BounceOff(player.transform.position, forceMultiplier: Mathf.Clamp(dirDifference, 0.5f, 1f), extraForce: 5f);
        }
        else
        {
            BounceOffNonOwnerPlayerServerRpc(player.transform.position, physicsTire.transform.position, dirDifference);
        }
    }

    public void BounceOff(Vector3 otherPos, float forceMultiplier = 1f, float extraForce = 0, float minForceDirectional = 2f, float maxForceDirectional = 20f, float extraForceUpward = 0f, float minForceUpward = 2f, float maxForceUpward = 20f, bool bounceUp = true, bool overrideTirePosition = false, Vector3? tirePosition = null)
    {
        Rigidbody rigidbody = physicsTire.GetComponent<Rigidbody>();

        Vector3 direction = physicsTire.transform.position - otherPos;
        if (overrideTirePosition && tirePosition != null)
        {
            direction = (Vector3)tirePosition - otherPos;
        }
        direction.y = 0f;
        Vector3 speed = rigidbody.velocity;
        speed.y = 0f;
        Vector3 bounceForce = Vector3.Normalize(direction) * (Mathf.Clamp(rigidbody.velocity.magnitude, minForceDirectional, maxForceDirectional) + extraForce);

        if (bounceUp)
        {
            bounceForce += Vector3.up * Mathf.Clamp(rigidbody.velocity.magnitude * 0.5f + extraForceUpward, minForceUpward, maxForceUpward);
        }

        bounceForce *= forceMultiplier * rigidbody.mass;
        rigidbody.AddForce(bounceForce, ForceMode.Impulse);

        PlayHitSoundServerRpc(bounceForce);
    }

    private void PlayAudibleNoise(float range, float loudness)
    {
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, range, loudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, tireHitSFX, randomize: true, 1f, -1);

        if (playerWhoHit != null && currentBehaviourStateIndex == 2)
        {
            BounceOff(playerWhoHit.gameplayCamera.transform.position, forceMultiplier: force, extraForce: 5f);
        }

        return true;
	}

    private void ForcePlayerJump(PlayerControllerB player)
    {
        if (((player.IsOwner && player.isPlayerControlled && (!player.IsServer || player.isHostPlayerObject)) || player.isTestingPlayer) && !player.inSpecialInteractAnimation && (player.isMovementHindered <= 0 || player.isUnderwater) && (player.thisController.isGrounded || (!player.isJumping && player.IsPlayerNearGround())) && !player.isJumping && (!player.isPlayerSliding || player.playerSlidingTimer > 2.5f) && !player.isCrouching)
        {
            player.playerSlidingTimer = 0f;
            player.isJumping = true;
            StartOfRound.Instance.PlayerJumpEvent.Invoke(player);
            player.PlayJumpAudio();
            if (player.jumpCoroutine != null)
            {
                StopCoroutine(player.jumpCoroutine);
            }
            player.jumpCoroutine = StartCoroutine(player.PlayerJump());
            if (StartOfRound.Instance.connectedPlayersAmount!= 0)
            {
                player.PlayerJumpedServerRpc();
            }
        }
    }

    private IEnumerator PreventPlayerJump(PlayerControllerB player)
    {
        player.isFallingNoJump = true;
        yield return new WaitForSeconds(0.15f);
        yield return new WaitUntil(() => player.isGroundedOnServer);
        player.isFallingNoJump = false;
    }

    private void SetRollAudio(bool isTouchingGround, Vector3 movementForce, String groundTag = "Dirt", float crossFadeSpeed = 0.1f)
    {
        bool groundTypeFound = false;

        for (int i = 0; i < rollingAudioSources.Length; i++)
        {
            if (rollingAudioSources[i].gameObject.name.Contains(groundTag))
            {
                groundTypeFound = true;
                break;
            }
        }

        if (!groundTypeFound)
        {
            groundTag = "Dirt";
        }

        if (isTouchingGround && movementForce.magnitude > 0.5f && currentBehaviourStateIndex == 2)
        {
            for (int i = 0; i < rollingAudioSources.Length; i++)
            {
                if (rollingAudioSources[i].gameObject.name.Contains(groundTag))
                {
                    AudioSource correctSource = rollingAudioSources[i];

                    if (!correctSource.isPlaying)
                    {
                        correctSource.Play();
                    }

                    if (correctSource.volume < 1f)
                    {
                        correctSource.volume += crossFadeSpeed;
                    }
                }

                else
                {
                    AudioSource incorrectSource = rollingAudioSources[i];

                    if (incorrectSource.volume > 0f)
                    {
                        incorrectSource.volume -= crossFadeSpeed;
                    }

                    else
                    {
                        incorrectSource.Stop();
                    }
                }
            }
        }

        else
        {
            for (int i = 0; i < rollingAudioSources.Length; i++)
            {
                AudioSource audioSource = rollingAudioSources[i];
                
                if (audioSource.volume > 0f)
                {
                    audioSource.volume -= crossFadeSpeed;
                }

                else
                {
                    audioSource.Stop();
                }
            }
        }
    }

    // private IEnumerator LerpRollVolume(float currentVolume, float targetVolume, float duration)
    // {
    //     float timeElapsed = 0f;
    //     while (timeElapsed < duration)
    //     {
    //         tireRollAudio.volume = Mathf.Lerp(currentVolume, targetVolume, timeElapsed / duration);
    //         timeElapsed += Time.deltaTime;
    //         yield return null;
    //     }

    //     tireRollAudio.volume = targetVolume;

    //     if (tireRollAudio.volume > 0f)
    //     {
    //         tireRollAudio.Play();
    //     }
    //     else
    //     {
    //         tireRollAudio.Pause();
    //     }

    //     rollVolumeCoroutine = null;
    // }

    private IEnumerator LerpRollPitch(float currentPitch, float minPitch, float maxPitch)
    {
        float timeElapsed = 0f;
        float duration = UnityEngine.Random.Range(0.5f, 1.5f);
        float targetPitch = UnityEngine.Random.Range(minPitch, maxPitch);
        while (timeElapsed < duration)
        {
            rollingAudioPitch = Mathf.Lerp(currentPitch, targetPitch, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        rollingAudioPitch = targetPitch;
        rollPitchCoroutine = null;
    }

    private void HinderLookInput(float xSensMultiplier = 0.5f)
    {
        Vector2 lookVector = playerHeldBy.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * IngamePlayerSettings.Instance.settings.lookSensitivity;
        lookVector.x *= xSensMultiplier;
        if (IngamePlayerSettings.Instance.settings.invertYAxis)
        {
            lookVector.y *= -1f;
        }
        StartOfRound.Instance.playerLookMagnitudeThisFrame = lookVector.magnitude * Time.deltaTime;
        if (playerHeldBy.smoothLookMultiplier != 25f)
        {
            playerHeldBy.CalculateSmoothLookingInput(lookVector);
        }
        else
        {
            playerHeldBy.CalculateNormalLookingInput(lookVector);
        }
        if (playerHeldBy.isTestingPlayer || (base.IsServer && playerHeldBy.playersManager.connectedPlayersAmount < 1))
        {
            return;
        }
        if (playerHeldBy.updatePlayerLookInterval > 0.1f && Physics.OverlapSphere(playerHeldBy.transform.position, 35f, playerHeldBy.playerMask).Length != 0)
        {
            playerHeldBy.updatePlayerLookInterval = 0f;
            if (Mathf.Abs(playerHeldBy.oldCameraUp + playerHeldBy.previousYRot - (playerHeldBy.cameraUp + playerHeldBy.thisPlayerBody.eulerAngles.y)) < 3f && !playerHeldBy.playersManager.newGameIsLoading)
            {
                playerHeldBy.UpdatePlayerRotationServerRpc((short)playerHeldBy.cameraUp, (short)playerHeldBy.thisPlayerBody.localEulerAngles.y);
                playerHeldBy.oldCameraUp = playerHeldBy.cameraUp;
                playerHeldBy.previousYRot = playerHeldBy.thisPlayerBody.localEulerAngles.y;
            }
        }
    }

    private void PlayerInputWhileRolling(RaycastHit groundInfo)
    {
        HinderLookInput(0.1f);

        Vector2 moveVector = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Move").ReadValue<Vector2>();
        float sprint = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").ReadValue<float>();

        //CALCULATE TARGET ROLLING SPEED BASED ON CURRENT CONTEXT
            float targetSpeed;

            //CHECK IF MOVING
            if (Math.Abs(moveVector.y) > 0.1f)
            {
                targetSpeed = 1f;

                //SPRINTING
                if (sprint > 0f)
                {
                    targetSpeed += 0.3f;
                    playerHeldBy.sprintMeter = Mathf.Clamp(playerHeldBy.sprintMeter - Time.deltaTime / playerHeldBy.sprintTime * playerHeldBy.carryWeight * 1.5f, 0f, 1f);
                }

                //CARRY WEIGHT
                targetSpeed -= Remap(playerHeldBy.carryWeight, 1f, 10f, 0f, 2f);

                //TERRAIN
                if (groundInfo.collider.gameObject.tag.Contains("Snow"))
                {
                    targetSpeed -= 0.15f;
                }

                //SLOPE
                Vector3 compareVector = -playerHeldBy.transform.forward;
                if (moveVector.y < 0)
                {
                    compareVector *= -1;
                }
                float slope = 90 - Vector3.Angle(groundInfo.normal, compareVector);
                
                if (slope > 0)
                {
                    targetSpeed += Remap(slope, 0, 30, 0, -1f);
                }
                else
                {
                    targetSpeed += Remap(slope, 0, -30, 0, 1);
                }

                //DIRECTION
                if (moveVector.y < 0)
                {
                    targetSpeed *= -1;
                }
            }

            //IF NOT MOVING
            else
            {
                //SET TARGET SPEED TO 0
                targetSpeed = 0f;
            }

        //LERP MOVEMENT SPEED TO TARGET SPEED

            if (Math.Abs(targetSpeed - rollSpeed) > 0.1f)
            {
                //INCREASING SPEED
                if (rollSpeed < targetSpeed)
                {
                    rollSpeed += rollAcceleration * Time.deltaTime;
                }

                //DECREASING SPEED
                else
                {
                    rollSpeed -= rollAcceleration * Time.deltaTime * 0.5f;
                }
            }

            else
            {
                rollSpeed = targetSpeed;
            }

        //MOVEMENT SPEED OVERRIDES
        if (rollBumpTimer <= 0)
        {

            //CHECK IF MOVING FAST ENOUGH TO BUMP
            bool bump = false;
            if (rollSpeed > 0.9f)
            {
                bump = true;
            }

            //TIRE COLLISION

                //CHECK FOR PLAYERS OR ENEMIES
                RaycastHit bumpInfo;
                if (Physics.Raycast(tireObject.transform.position + Vector3.up * 0.1f, playerHeldBy.transform.forward, out bumpInfo, tireRadius, CoronaMod.Masks.PlayerEnemies, QueryTriggerInteraction.Collide))
                {
                    float playerPushForce = 1f;
                    GameObject otherObject = bumpInfo.collider.gameObject;

                    Debug.Log($"[TIRE]: Bumped into {otherObject}.");

                    //IF PLAYER
                    if (otherObject.layer == 3 && bump)
                    {
                        if (otherObject.GetComponent<PlayerControllerB>() != null)
                        {
                            PlayerControllerB otherPlayer = otherObject.GetComponent<PlayerControllerB>();

                            //BUMP OTHER PLAYER
                            otherPlayer.externalForceAutoFade += playerHeldBy.transform.forward * playerPushForce * Time.deltaTime;
                            // DamageOtherPlayerServerRpc((int)otherPlayer.playerClientId);
                        }
                    }

                    //IF ENEMY
                    else if (otherObject.layer == 19)
                    {
                        if (otherObject.GetComponent<EnemyAICollisionDetect>() != null)
                        {
                            playerPushForce = 2f;
                            EnemyAI enemy = otherObject.GetComponent<EnemyAICollisionDetect>().mainScript;

                            //SET PLAYER PUSH FORCE BASED ON ENEMY
                            switch (enemy)
                            {
                                case SpringManAI springMan:
                                    if (!springMan.hasStopped)
                                    {
                                        playerPushForce = 3f;
                                    }
                                    break;

                                case JesterAI jester:
                                    
                                    if (jester.creatureAnimator.GetBool("poppedOut"))
                                    {
                                        playerPushForce = 4f;
                                    }
                                    else
                                    {
                                        playerPushForce = 1f;
                                    }
                                    break;
                                
                                case CaveDwellerAI caveDweller:
                                    if (caveDweller.adultContainer.activeSelf)
                                    {
                                        playerPushForce = 3f;
                                    }
                                    break;
                                
                                case CrawlerAI crawler:
                                    if (crawler.hasEnteredChaseMode)
                                    {
                                        playerPushForce = 4f;
                                    }
                                    break;

                                case SandWormAI:
                                    playerPushForce = 4f;
                                    break;

                                case MouthDogAI mouthDog:
                                    if (mouthDog.hasEnteredChaseModeFully)
                                    {
                                        playerPushForce = 4f;
                                    }
                                    break;

                                case ForestGiantAI:
                                    playerPushForce = 3f;
                                    break;

                                case RadMechAI radMech:
                                    if (radMech.chargingForward)
                                    {
                                        playerPushForce = 4f;
                                    }
                                    break;
                            }

                            //IF FAST ENOUGH TO BUMP
                            if (bump)
                            {
                                //DAMAGE ENEMY
                                enemy.HitEnemyOnLocalClient(1, playerHeldBy.transform.forward, playerHeldBy, playHitSFX: true);

                                //INCREASE PLAYER PUSH FORCE
                                playerPushForce *= 2;
                            }
                        }
                    }

                    //OVERRIDE ROLL SPEED AND BUMP PLAYERHELDBY
                    rollSpeed = 0f;
                    playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * playerPushForce;
                    PlayHitSoundServerRpc(rollForce);
                    rollBumpTimer = rollBumpCooldown;
                }

                //CHECK FOR TERRAIN
                else if (Physics.Raycast(tireObject.transform.position + Vector3.up * 0.1f, playerHeldBy.transform.forward, out bumpInfo, tireRadius, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
                {
                    float playerPushForce = 1f;

                    if (bump)
                    {
                        playerPushForce = 2f;
                    }

                    //OVERRIDE ROLL SPEED AND BUMP PLAYERHELDBY
                    rollSpeed = 0f;
                    playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * playerPushForce;
                    PlayHitSoundServerRpc(rollForce);
                    rollBumpTimer = rollBumpCooldown;
                }

            //PLAYERHELDBY COLLISION

                //RE-CHECK BUMP (INVERTED DIRECTION)
                if (rollSpeed < -0.9f)
                {
                    bump = true;
                }

                //CHECK FOR PLAYERS OR ENEMIES
                if (Physics.Raycast(playerHeldBy.playerEye.transform.position, -playerHeldBy.transform.forward, out bumpInfo, 1f, CoronaMod.Masks.PlayerEnemies, QueryTriggerInteraction.Collide))
                {
                    GameObject otherObject = bumpInfo.collider.gameObject;
                    Debug.Log($"[TIRE]: Player bumped against {otherObject}!");

                    if (otherObject != playerHeldBy.gameObject)
                    {
                        rollSpeed = 0f;
                        float playerPushForce = 1f;

                        if (bump)
                        {
                            ReleaseRollingTire();
                            playerHeldBy.DamagePlayer(1, causeOfDeath: CauseOfDeath.Crushing);
                            Debug.Log($"[TIRE]: Player squished against {otherObject}!");
                        }

                        //OVERRIDE ROLL SPEED AND BUMP PLAYERHELDBY
                        rollSpeed = 0f;
                        playerHeldBy.externalForceAutoFade += playerHeldBy.transform.forward * playerPushForce;
                        PlayHitSoundServerRpc(rollForce);
                        rollBumpTimer = rollBumpCooldown;
                    }
                }

                //CHECK FOR TERRAIN
                else if (Physics.Raycast(playerHeldBy.playerEye.transform.position, -playerHeldBy.transform.forward, out bumpInfo, 1f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
                {
                    GameObject otherObject = bumpInfo.collider.gameObject;
                    Debug.Log($"[TIRE]: Player bumped against {otherObject}!");
                    rollSpeed = 0f;
                    float playerPushForce = 1f;

                    if (bump)
                    {
                        ReleaseRollingTire();
                        playerHeldBy.DamagePlayer(1, causeOfDeath: CauseOfDeath.Crushing);
                        Debug.Log($"[TIRE]: Player squished against {otherObject}!");
                    }

                    //OVERRIDE ROLL SPEED AND BUMP PLAYERHELDBY
                    rollSpeed = 0f;
                    playerHeldBy.externalForceAutoFade += playerHeldBy.transform.forward * playerPushForce;
                    PlayHitSoundServerRpc(rollForce);
                    rollBumpTimer = rollBumpCooldown;
                }
        }

        //BUMP COOLDOWN
        if (rollBumpTimer > 0)
        {
            rollBumpTimer -= Time.deltaTime;
        }

        //IF BUMPED BY STRONG ENOUGH EXTERNAL FORCE
        if (playerHeldBy.externalForceAutoFade.magnitude > 1f)
        {
            rollSpeed = 0;
        }

        //MOVE PLAYER WITH ROLL SPEED
        rollForce = playerHeldBy.transform.forward * rollSpeed * rollingTireSpeed;
        playerHeldBy.thisController.Move(rollForce);

        //SET ANIMATION & PUSH AWAY FORCE
        if (rollingTireAnimator != null)
        {
            rollingTireAnimator.SetFloat("rollSpeed", rollSpeed);
            rollingTireAnimator.SetFloat("moveInputX", moveVector.x);
            rollingTireAnimator.SetFloat("moveInputY", moveVector.y);
            rollingTireAnimator.SetBool("sprinting", sprint > 0);
        }

        pushedWhileWalking = false;
        pushedWhileSprinting = false;

        if (moveVector.y > 0.1f)
        {
            pushedWhileWalking = true;
            pushedWhileSprinting = sprint > 0;
        }

        //ROTATE PLAYER LEFT AND RIGHT
        if (moveVector.x != 0)
        {
            float rotation = rollingTireRotationSpeed;
            if (moveVector.x < 0)
            {
                rotation *= -1;
            }
            
            playerHeldBy.transform.eulerAngles += new Vector3(0f, rotation, 0f);
        }

        //ROTATE TIRE VISUALLY
        PlayAudibleNoise(noiseRange, noiseLoudness);
        RotateTireObjectServerRpc(rollSpeed * 2f);
        tireObject.transform.eulerAngles += new Vector3(0,0,rollSpeed * 2f);

        //TIRE AUDIO
        SetRollAudio(true, rollForce, groundInfo.collider.gameObject.tag);
    }

    private IEnumerator DisplayCursorTip(string cursorTip)
    {
        playerHeldBy.cursorTip.text = cursorTip;
        while(playerHeldBy.playerActions.Movement.ActivateItem.IsPressed())
        {
            Debug.Log("[TIRE]: Player holding use!");
            yield return new WaitForEndOfFrame();
            playerHeldBy.cursorTip.text = cursorTip;
        }
        playerHeldBy.cursorTip.text = "";
    }



    private float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }
}