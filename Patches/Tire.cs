using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Tire : AnimatedItem, IHittable, ITouchable
{
    [Space(15f)]
    [Header("Tire Settings")]
    public GameObject physicsTirePrefab;

    private GameObject physicsTire;

    public GameObject tireObject;

    private Collider tireObjectCollider;

    private Rigidbody tireRigidbody;

    public float tireRadius = 0;

    public float boostForce = 5f;

    public float playerPushCollisionCooldown = 1f;

    private float playerPushCollisionTimer;

    private bool boost;

    public int currentBehaviourStateIndex = 0;

    public int previousBehaviourStateIndex;

    private PlayerControllerB previousPlayerHeldBy;

    private String[] itemToolTips = ["Roll : [LMB]", ""];

    private String[] rollToolTips = ["Push : [LMB]", "Carry : [Q]"];

    private float fallHeightPeak;

    private Coroutine shipLandingCoroutine;

    private Coroutine shipLeavingCoroutine;

    [Space(10f)]
    [Header("Position Syncing")]
    public float syncPositionInterval = 0.2f;

    public float syncPositionThreshold = 0.5f;

    private float syncTimer;

    private Vector3 physicsTireServerPosition;

    private Quaternion physicsTireServerRotation;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource tireRollAudio;

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

    public void OnEnable()
    {
        StartOfRound.Instance.StartNewRoundEvent.AddListener(StartHandlingShipLanding);
        CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.AddListener(StartHandlingShipLeaving);
    }

    public void OnDisable()
    {
        StartOfRound.Instance.StartNewRoundEvent.RemoveListener(StartHandlingShipLanding);
        CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.RemoveListener(StartHandlingShipLeaving);
    }

    private void StartHandlingShipLanding()
    {
        if (shipLandingCoroutine != null)
        {
            StopCoroutine(shipLandingCoroutine);
            shipLandingCoroutine = null;
        }
        shipLandingCoroutine = StartCoroutine(HandleShipLanding());
    }

    private void StartHandlingShipLeaving()
    {
        if (shipLeavingCoroutine != null)
        {
            StopCoroutine(shipLeavingCoroutine);
            shipLeavingCoroutine = null;
        }
        shipLeavingCoroutine = StartCoroutine(HandleShipLeaving());
    }

    private IEnumerator HandleShipLanding()
    {
        yield return null;
        if (isInShipRoom)
        {
            yield return new WaitUntil(() => !StartOfRound.Instance.inShipPhase);
            tireRigidbody.isKinematic = true;
            yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsAnimator.GetBool("Closed"));
            tireRigidbody.isKinematic = base.IsOwner;
        }
    }

    private IEnumerator HandleShipLeaving()
    {
        yield return null;
        if (isInShipRoom)
        {
            yield return new WaitUntil(() => RoundManager.Instance.playersManager.shipDoorsAnimator.GetBool("Closed"));
            yield return new WaitForSeconds(1f);
            tireRigidbody.isKinematic = true;
            yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsEnabled);
            tireRigidbody.isKinematic = base.IsOwner;
        }
    }

    public override void Start()
    {
        base.Start();
        tireObjectCollider = tireObject.GetComponent<Collider>();
        physicsTirePrefab.transform.localScale = base.originalScale;
        BoxCollider gayTrigger = base.gameObject.GetComponentInChildren<TriggerScript>().gameObject.GetComponent<BoxCollider>();
        gayTrigger.includeLayers = CoronaMod.Masks.PlayerPropsEnemiesMapHazardsVehicle;
        gayTrigger.excludeLayers = -2621449;
    }

    public override void Update()
    {
        base.Update();

        if ((playerHeldBy != null) && previousPlayerHeldBy != playerHeldBy)
        {
            previousPlayerHeldBy = playerHeldBy;
        }

        if (playerPushCollisionTimer > 0)
        {
            playerPushCollisionTimer -= Time.deltaTime;
        }

        if (tireRollAudio.volume > 0f && rollPitchCoroutine == null)
        {
            Debug.Log("[TIRE]: Changing roll audio pitch!");
            rollPitchCoroutine = StartCoroutine(LerpRollPitch(tireRollAudio.pitch, 0.5f, 2f));
        }

        bool touchingGround = false;

        //BEHAVIOUR STATES
        switch (currentBehaviourStateIndex)
        {

        //ITEM TIRE
        case 0:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[TIRE]: Entered held item state.");


                //ALL CLIENTS
                rollVolumeCoroutine = StartCoroutine(LerpRollVolume(tireRollAudio.volume, 0f, 0.3f));
                itemProperties.toolTips = itemToolTips;

                if (previousBehaviourStateIndex == 1)
                {
                    itemAudio.PlayOneShot(switchToHoldingClip);
                }


                //OWNER ONLY
                if (base.IsOwner)
                {
                    EnableTireObjectMeshesServerRpc(true);

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
                    if ((playerHeldBy != null))
                    {
                        SetControlTipsForItem();
                        parentObject = playerHeldBy.localItemHolder;
                    }
                    else
                    {
                        parentObject = null;
                    }
                }


                //NON-OWNERS ONLY
                if (!base.IsOwner)
                {
                    if ((playerHeldBy != null))
                    {
                        parentObject = playerHeldBy.serverItemHolder;
                    }
                    else
                    {
                        parentObject = null;
                    }
                }


                //ALL CLIENTS
                // EnableTireObjectMeshes(true);
                SpawnPhysicsTire(false);

                tireObject.transform.position = base.transform.position;
                tireObject.transform.rotation = base.transform.rotation;
                tireObject.transform.SetParent(base.transform);

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            break;



        //ROLLING TIRE
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[TIRE]: Entered rolling state.");


                //ALL CLIENTS
                itemAudio.PlayOneShot(switchToRollingCip);
                PlayAudibleNoise(noiseRange, noiseLoudness);

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
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //ALL CLIENTS
            RaycastHit groundInfo;
            if (Physics.Raycast(tireObject.transform.position, -Vector3.up, out groundInfo, tireRadius*2 + 0.1f, 268438273, QueryTriggerInteraction.Ignore))
            {
                touchingGround = true;
            }

            //OWNER ONLY
            if (base.IsOwner)
            {
                if (!(playerHeldBy != null))
                {
                    Debug.Log("[TIRE]: Tire not held by player, returning to item state.");
                    SetBehaviourStateServerRpc(0);
                    break;
                }

                if (Physics.Raycast(tireObject.transform.position, playerHeldBy.transform.forward, tireRadius, 268438273, QueryTriggerInteraction.Ignore))
                {
                    playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * 0.1f;
                }

                float steepness;
                if (touchingGround)
                {
                    tireObject.transform.position = groundInfo.point + Vector3.up * tireRadius;

                    Vector3 compareVector = -playerHeldBy.transform.forward;
                    if (!playerHeldBy.movingForward)
                    {
                        compareVector *= -1;
                    }
                    steepness = 90 - Vector3.Angle(groundInfo.normal, compareVector);

                    if (steepness > 0)
                    {
                        playerHeldBy.externalForceAutoFade += Vector3.Normalize(playerHeldBy.walkForce) * Remap(steepness, 0f, 30f, 0f, -0.085f);
                    }
                    else if (steepness < 0)
                    {
                        playerHeldBy.externalForceAutoFade += Vector3.Normalize(playerHeldBy.walkForce) * Remap(steepness, 0f, -30f, 0f, 0.03f);
                    }

                    if (playerHeldBy.isWalking)
                    {
                        PlayAudibleNoise(noiseRange, noiseLoudness);
                        float rotation = playerHeldBy.walkForce.magnitude;
                        if (playerHeldBy.isSprinting)
                        {
                            rotation *= 1.5f;
                        }
                        if (!playerHeldBy.movingForward)
                        {
                            rotation *= -1;
                        }
                        RotateTireObjectServerRpc(rotation);
                        tireObject.transform.eulerAngles += new Vector3(0,0,rotation);
                    }
                }
                else
                {
                    if (IsInCollider())
                    {
                        Debug.Log("[TIRE]: Released while in collider!");
                        tireObject.transform.position = playerHeldBy.transform.position;
                    }
                    SyncTireObjectTransformServerRpc();
                    SetBehaviourStateServerRpc(2);
                    break;
                }

                if (playerHeldBy.isJumping || playerHeldBy.isCrouching || playerHeldBy.isPlayerDead || (playerHeldBy.externalForces + playerHeldBy.externalForceAutoFade).magnitude > 10f)
                {
                    if (IsInCollider())
                    {
                        Debug.Log("[TIRE]: Released while in collider!");
                        tireObject.transform.position = playerHeldBy.transform.position;
                    }
                    SyncTireObjectTransformServerRpc();
                    SetBehaviourStateServerRpc(2);
                    break;
                }

                if (touchingGround && playerHeldBy.isWalking && tireRollAudio.volume < 1f && rollVolumeCoroutine == null)
                {
                    rollVolumeCoroutine = StartCoroutine(LerpRollVolume(tireRollAudio.volume, 1f, 0.5f));
                }
                else if ((!touchingGround || !playerHeldBy.isWalking) && tireRollAudio.volume > 0f && rollVolumeCoroutine == null)
                {
                    rollVolumeCoroutine = StartCoroutine(LerpRollVolume(tireRollAudio.volume, 0f, 0.2f));
                }
            }

            // Vector2 playerMove = playerHeldBy.playerActions.FindAction("Move").ReadValue<Vector2>();
            
            //MAKE A AND D ROTATE PLAYER SLOWLY
            // if (playerMove.magnitude > 0 && (playerMove.x > 0 || playerMove.x < 0))
            // {
            //     //ZERO OUT WALK FORCE
            //     playerHeldBy.walkForce = Vector3.zero;

            //     //ROTATE PLAYER LEFT/RIGHT
            //     float playerRotateSpeed = 1f;
            //     if (playerMove.x < 0)
            //     {
            //         playerRotateSpeed *= -1;
            //     }
            //     playerHeldBy.transform.eulerAngles += new Vector3(0,playerRotateSpeed,0);
            // }

            break;



        //PHYSICS TIRE
        case 2:

            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[TIRE]: Entered physics state.");


                //ALL CLIENTS
                playerPushCollisionTimer = playerPushCollisionCooldown;
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
                    playerHeldBy.DiscardHeldObject();
                    parentObject = physicsTire.transform;

                    tireRigidbody.AddForce(playerRigidbody.velocity * 2f, ForceMode.Impulse);
                    if (boost)
                    {
                        Vector3 force = previousPlayerHeldBy.transform.forward * boostForce;

                        if (previousPlayerHeldBy.movingForward)
                        {
                            if (previousPlayerHeldBy.isSprinting)
                            {
                                PlayPushSoundServerRpc(2);
                                force *= 2f;
                            }
                            else
                            {
                                PlayPushSoundServerRpc(1);
                            }
                        }
                        else
                        {
                            PlayPushSoundServerRpc(0);
                            force /= 2f;
                        }

                        force += Vector3.up;
                        tireRigidbody.AddForce(force, ForceMode.Impulse);
                    }
                    boost = false;
                    
                    EnableTireObjectMeshesServerRpc(false);
                }


                //ALL CLIENTS
                // EnableTireObjectMeshes(false);
                parentObject = physicsTire.transform;


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

                if (Physics.Raycast(physicsTire.transform.position, -Vector3.up, out groundInfo, tireRadius*2 + 0.1f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
                {
                    touchingGround = true;
                }

                if (touchingGround)
                {
                    if (tireRigidbody.velocity.magnitude > 0.2f)
                    {
                        PlayAudibleNoise(noiseRange, noiseLoudness);
                        if (tireRollAudio.volume < 1f && rollVolumeCoroutine == null)
                        {
                            rollVolumeCoroutine = StartCoroutine(LerpRollVolume(tireRollAudio.volume, 1f, 0.5f));
                        }
                    }
                    else if (tireRollAudio.volume > 0f && rollVolumeCoroutine == null)
                    {
                        rollVolumeCoroutine = StartCoroutine(LerpRollVolume(tireRollAudio.volume, 0f, 0.2f));
                    }

                    //BOUNCE IF FALLEN FAR ENOUGH
                    float tireFallHeight = fallHeightPeak - groundInfo.point.y;
                    fallHeightPeak = groundInfo.point.y;
                    if (tireFallHeight > 2f)
                    {
                        float bounceForce = Mathf.Abs(tireRigidbody.velocity.y * 1.5f * 10) * groundInfo.normal.y;
                        Debug.Log($"[TIRE]: Fell from height of {tireFallHeight}, bouncing with force {bounceForce}!");
                        tireRigidbody.AddForce(groundInfo.normal * bounceForce, ForceMode.Impulse);
                        PlayHitSoundServerRpc(groundInfo.normal * bounceForce);
                    }
                    else if (tireFallHeight > 0.2f)
                    {
                        PlayHitSoundServerRpc(new Vector3(0f,0f,0f));
                    }
                }
                else
                {
                    if (tireRollAudio.volume > 0f && rollVolumeCoroutine == null)
                    {
                        rollVolumeCoroutine = StartCoroutine(LerpRollVolume(tireRollAudio.volume, 0f, 0.2f));
                    }

                    if (physicsTire.transform.position.y > fallHeightPeak)
                    {
                        fallHeightPeak = physicsTire.transform.position.y;
                    }
                }

                if ((Vector3.Angle(physicsTire.transform.forward, Vector3.up) < 30f || Vector3.Angle(-physicsTire.transform.forward, Vector3.up) < 30f) && physicsTire.GetComponent<Rigidbody>().velocity.magnitude < 1f)
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

            Debug.Log("[TIRE]: --- Player bumped by tire! ---");
            Debug.Log($"[TIRE]: Direction difference: {dirDifference}");
            Debug.Log($"[TIRE]: Velocity: {tireVelocity.magnitude}");

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

            //FORCE PLAYER JUMP
            // ForcePlayerJump(player);

            if (damage > 0)
            {
                player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Bludgeoning, force: pushForce);
            }

            //BOUNCE TIRE OFF PLAYER
            Vector3 bounceDirection = Vector3.Normalize(physicsTire.transform.position - player.transform.position);
            Vector3 bounceDirectionForce = bounceDirection * Mathf.Clamp(tireVelocity.magnitude * 1.5f, 2f, 20f);
            Vector3 bounceUpForce = Vector3.up * Mathf.Clamp(tireVelocity.magnitude * 0.75f, 3f, 25f);
            Vector3 bounceForce = (bounceDirectionForce + bounceUpForce) * dirDifference * 7f;

            BounceOffPlayerServerRpc(bounceForce);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void BounceOffPlayerServerRpc(Vector3 bounceForce)
    {
        PlayHitSoundServerRpc(bounceForce);
        BounceOffPlayerClientRpc(bounceForce);
    }

    [ClientRpc]
    private void BounceOffPlayerClientRpc(Vector3 bounceForce)
    {
        if (IsOwner)
        {
            Debug.Log("[TIRE]: Bouncing off player!");
            tireRigidbody.AddForce(bounceForce, ForceMode.Impulse);
        }
    }

    [ServerRpc]
    private void CollideWithTireServerRpc(NetworkObjectReference tireRef, Vector3 tireVelocity)
    {
        CollideWithTireClientRpc(tireRef, tireVelocity);
    }

    [ClientRpc]
    private void CollideWithTireClientRpc(NetworkObjectReference tireRef, Vector3 tireVelocity)
    {
        Tire otherTire = ((NetworkObject)tireRef).gameObject.GetComponent<Tire>();
        if (otherTire.currentBehaviourStateIndex == 2 && GameNetworkManager.Instance.localPlayerController.playerClientId == otherTire.OwnerClientId)
        {
            Vector3 bounceForce = tireVelocity + otherTire.tireRigidbody.velocity;
            Vector3 direction = Vector3.Normalize(base.transform.position - otherTire.transform.position);
            otherTire.tireRigidbody.AddForce(direction * bounceForce.magnitude * 10f);
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



    //USING ITEM
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (base.IsOwner)
        {
            if (IsInCollider())
            {
                Debug.Log("[TIRE]: No space for tire!");
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
                    boost = true;
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
        base.DiscardItem();
    }

    private bool IsInCollider()
    {
        Collider[] foundColliders = Physics.OverlapBox(playerHeldBy.transform.position + playerHeldBy.transform.forward * tireRadius + Vector3.up * tireRadius, new Vector3(tireRadius/2f,tireRadius/2f,tireRadius/2f), playerHeldBy.transform.rotation, 1342179585, QueryTriggerInteraction.Ignore);

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
            physicsTire.GetComponent<TireReferenceScript>().mainScript = this;
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

                //CHECK IF LOCAL PLAYER
                if (player == GameNetworkManager.Instance.localPlayerController)
                {
                    Debug.Log("[TIRE]: This player collided with tire!");

                    //CHECK IF TIRE IS NOT ON COOLDOWN OR HELD
                    if ((playerPushCollisionTimer > 0) || player == playerHeldBy)
                    {
                        Debug.Log("[TIRE]: On cooldown or holding, aborting collision!");
                        return;
                    }

                    CollideWithLocalPlayerServerRpc((int)player.playerClientId);
                    playerPushCollisionTimer = playerPushCollisionCooldown;
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
                playerPushCollisionTimer = 0f;
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
            // else if (otherObject.GetComponent<GrabbableObject>() != null)
            // {
            //     GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();
            //     Debug.Log("[TIRE]: Collided with grabbable object!");

            //     switch (gObject)
            //     {
            //         case BowlingBall bowlingBall:
            //             if (!bowlingBall.hasHitGround && !bowlingBall.isHeld && !bowlingBall.isHeldByEnemy)
            //             {

            //             }
            //             return;

            //         case SoccerBallProp ball:
            //             if (!ball.hasHitGround && !ball.isHeld && !ball.isHeldByEnemy)
            //             {
            //                 BounceOff(ball.transform.position, forceMultiplier: 0.2f);
            //                 return;
            //             }
            //             return;
                    
            //         case HydraulicStabilizer hydraulic:
            //             BounceOff(hydraulic.transform.position, forceMultiplier: 1.25f);
            //             hydraulic.GoPsychoAndSync();
            //             return;

            //         case Radiator radiator:
            //             BounceOff(radiator.transform.position, forceMultiplier: 0.75f);
            //             radiator.FallOverAndSync(-(new Vector3(physicsTire.transform.position.x, 0f, physicsTire.transform.position.z) - new Vector3(radiator.transform.position.x, 0f, radiator.transform.position.z)).normalized);
            //             return;
                    
            //         case Tire tire:
            //             Debug.Log("[TIRE]: Collided with another tire!");
            //             if (tire.currentBehaviourStateIndex == 2)
            //             {
            //                 NetworkObjectReference tireRef = tire.gameObject.GetComponent<NetworkObject>();
            //                 CollideWithTireServerRpc(tireRef, tireRigidbody.velocity);
            //             }
            //             return;
            //     }
            // }

            //TIRE COLLISION
            else if (otherObject.GetComponent<TireReferenceScript>() != null)
            {
                Debug.Log("[TIRE]: Collided with another tire!");
                NetworkObjectReference tireRef = otherObject.GetComponent<TireReferenceScript>().mainScript.gameObject.GetComponent<NetworkObject>();
                CollideWithTireServerRpc(tireRef, tireRigidbody.velocity);
            }
        }
    }

    public void OnExit(Collider other)
    {

    }

    public void BounceOff(Vector3 otherPos, float forceMultiplier = 1f, float extraForce = 0, float minForceDirectional = 2f, float maxForceDirectional = 20f, float minForceUpward = 2f, float maxForceUpward = 20f, bool bounceUp = true)
    {
        Rigidbody rigidbody = physicsTire.GetComponent<Rigidbody>();

        Vector3 direction = Vector3.Normalize(physicsTire.transform.position - otherPos);
        Vector3 directionForce = direction * (Mathf.Clamp(rigidbody.velocity.magnitude * 1.5f, minForceDirectional, maxForceDirectional) + extraForce);
        Vector3 upForce = new Vector3(0,0,0);

        if (bounceUp)
        {
            upForce = Vector3.up * Mathf.Clamp(rigidbody.velocity.magnitude * 0.75f, minForceUpward, maxForceUpward);
        }

        Vector3 bounceForce = (directionForce + upForce) * forceMultiplier * 7f;
        rigidbody.AddForce(bounceForce, ForceMode.Impulse);

        PlayHitSoundServerRpc(bounceForce);

        Debug.Log($"[TIRE]: Bounced with force {bounceForce.magnitude}.");
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

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
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
            BounceOff(playerWhoHit.gameplayCamera.transform.position, forceMultiplier: force, extraForce: 15f);
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

    private IEnumerator LerpRollVolume(float currentVolume, float targetVolume, float duration)
    {
        float timeElapsed = 0f;
        while (timeElapsed < duration)
        {
            tireRollAudio.volume = Mathf.Lerp(currentVolume, targetVolume, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        tireRollAudio.volume = targetVolume;

        if (tireRollAudio.volume > 0f)
        {
            tireRollAudio.Play();
        }
        else
        {
            tireRollAudio.Pause();
        }

        rollVolumeCoroutine = null;
    }

    private IEnumerator LerpRollPitch(float currentPitch, float minPitch, float maxPitch)
    {
        float timeElapsed = 0f;
        float duration = UnityEngine.Random.Range(0.5f, 1.5f);
        float targetPitch = UnityEngine.Random.Range(minPitch, maxPitch);
        while (timeElapsed < duration)
        {
            tireRollAudio.pitch = Mathf.Lerp(currentPitch, targetPitch, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        tireRollAudio.pitch = targetPitch;
        rollPitchCoroutine = null;
    }



    private float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }
}