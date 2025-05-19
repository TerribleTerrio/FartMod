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

    private Coroutine shipLandingCoroutine;

    private Coroutine shipLeavingCoroutine;

    private float rollingAudibleNoiseCooldown = 3f;

    private float rollingAudibleNoiseTimer = 0f;

    private bool disablePhysics = false;

    [Space(10f)]
    [Header("Movement")]
    private float rollingTireCurrentSpeed;

    public float rollingTireSpeed = 0.1f;

    public float rollingTireAcceleration = 1f;

    public Vector3 rollingTireCurrentForce;

    public float rollingTireLeftRightSpeed = 1f;

    private float rollingTireLeftRightSpeedCurrent = 0f;

    public float rollingTireLeftRightAcceleration = 1f;

    public float rollingTireBumpSoundCooldown = 0.5f;

    private float rollingTireBumpSoundTimer = 0f;

    private bool pushed;

    private bool pushedWhileWalking;

    private bool pushedWhileSprinting;

    [Space(10f)]
    [Header("Position Syncing")]
    public float syncPositionInterval = 0.2f;

    public float syncPositionThreshold = 0.5f;

    private float syncPositionTimer;

    private Vector3 physicsTireServerPosition;

    private Quaternion physicsTireServerRotation;

    private float lastPlayerRotation = 0;

    [Space(10f)]
    [Header("Animations")]
    public Animator rollingTireAnimator;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource[] rollingAudioSources;

    public AudioSource tireBumpAudioSource;

    public AudioClip switchToHoldingClip;

    public AudioClip switchToRollingCip;

    public AudioClip pushAwayClip;

    public AudioClip pushAwayWalkingClip;

    public AudioClip pushAwaySprintingClip;

    public AudioClip[] tireBumpSFX;

    public AudioClip[] tireHitSFX;

    private Coroutine rollPitchCoroutine;

    private float rollingAudioPitch;

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
        Debug.Log("[TIRE]: Handling ship landing!");
        if (isInShipRoom)
        {
            yield return new WaitUntil(() => !StartOfRound.Instance.inShipPhase);
            Debug.Log("[TIRE]: Temporarily disabling rigidbody physics!");
            if (base.IsOwner)
            {
                disablePhysics = true;
            }
            yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsAnimator.GetBool("Closed"));
            Debug.Log("[TIRE]: Re-enabling rigidbody physics!");
            if (base.IsOwner)
            {
                disablePhysics = false;
            }
        }
    }

    private IEnumerator HandleShipLeaving()
    {
        yield return null;
        if (isInShipRoom)
        {
            yield return new WaitUntil(() => RoundManager.Instance.playersManager.shipDoorsAnimator.GetBool("Closed"));
            Debug.Log("[TIRE]: Temporarily disabling rigidbody physics!");
            if (base.IsOwner)
            {
                disablePhysics = true;
            }
            yield return new WaitForSeconds(1f);
            yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsEnabled);
            Debug.Log("[TIRE]: Re-enabling rigidbody physics!");
            if (base.IsOwner)
            {
                disablePhysics = false;
            }
        }
    }

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

        if (!isInShipRoom && StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(base.transform.position))
        {
            if (!scrapPersistedThroughRounds)
            {
                RoundManager.Instance.CollectNewScrapForThisRound(this);
            }
            isInShipRoom = true;
        }
        else if (isInShipRoom && !StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(base.transform.position))
        {
            isInShipRoom = false;
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

                    //COMING FROM ROLLING STATE
                    if (previousBehaviourStateIndex == 1)
                    {
                        if (playerHeldBy != null)
                        {
                            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (itemProperties.weight - 1f), 1f, 10f);
                            playerHeldBy.externalForceAutoFade += rollingTireCurrentForce * 10;
                        }
                    }

                    rollingTireCurrentForce = new Vector3(0,0,0);
                    rollingTireLeftRightSpeedCurrent = 0f;

                    //COMING FROM PHYSICS STATE
                    if (previousBehaviourStateIndex == 2 && physicsTire != null && playerHeldBy == null)
                    {
                        //PARENT TO PHYSICS REGION IF FOUND
                        Transform parentTransform = null;
                        PlayerPhysicsRegion physicsRegion;
                        RaycastHit hitInfo;
                        if (Physics.Raycast(base.transform.position, -Vector3.up, out hitInfo, 80f, 1342179585, QueryTriggerInteraction.Ignore))
                        {
                            parentTransform = hitInfo.collider.gameObject.transform;
                        }
                        if (parentTransform != null)
                        {
                            physicsRegion = parentTransform.GetComponentInChildren<PlayerPhysicsRegion>();
                            if (physicsRegion != null && physicsRegion.allowDroppingItems && physicsRegion.itemDropCollider.ClosestPoint(hitInfo.point) == hitInfo.point)
                            {
                                Debug.LogError("[TIRE]: Physics region object has network object, parenting to: " + parentTransform.gameObject.name);
                                NetworkObject parentNetworkObject = physicsRegion.parentNetworkObject;
                                ParentToPhysicsRegionServerRpc(parentNetworkObject);
                            }
                            else if (isInElevator)
                            {
                                ParentObjectServerRpc(parentToShip: true);
                            }
                        }
                        else
                        {
                            ParentObjectServerRpc(parentToShip: false);
                        }

                        startFallingPosition = base.transform.position;
                        if (base.transform.parent != null)
                        {
                            Debug.Log($"[TIRE]: Found transform parent: {base.transform.parent.gameObject.name}!");
                            startFallingPosition = base.transform.parent.InverseTransformPoint(startFallingPosition);
                        }

                        fallTime = 0f;
                        if (Physics.Raycast(base.transform.position + Vector3.up * tireRadius, Vector3.down, out hitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
                        {
                            Debug.Log("[TIRE]: Raycast found target floor position!");
                            targetFloorPosition = hitInfo.point + itemProperties.verticalOffset * Vector3.up;
                            if (base.transform.parent != null)
                            {
                                targetFloorPosition = base.transform.parent.InverseTransformPoint(targetFloorPosition);
                            }
                        }
                        else
                        {
                            Debug.Log("[TIRE]: Raycast did not find target floor position!");
                            targetFloorPosition = base.transform.localPosition;
                        }

                        SyncFallPositionServerRpc(startFallingPosition, targetFloorPosition, fallTime);
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
                rollingTireCurrentSpeed = 0;

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

            //CATCH IF TIRE IS NOT HELD BY PLAYER
            if (playerHeldBy == null)
            {
                SetBehaviourStateServerRpc(0);
                return;
            }

            //OWNER ONLY
            if (base.IsOwner)
            {
                //ROLLING TIRE PLAYER INPUT/MOVEMENT
                PlayerInputWhileRolling();
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

                    if (previousBehaviourStateIndex == 1)
                    {
                        playerHeldBy.externalForceAutoFade += rollingTireCurrentForce * 10;
                    }

                    playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (itemProperties.weight - 1f), 1f, 10f);
                    playerHeldBy.disableLookInput = false;
                    playerHeldBy.disableMoveInput = false;
                    // playerHeldBy.inSpecialInteractAnimation = false;
                    PlayerControllerB playerPushedBy = playerHeldBy;
                    playerHeldBy.DiscardHeldObject();
                    parentObject = physicsTire.transform;

                    tireRigidbody.AddForce(playerRigidbody.velocity * 2f, ForceMode.Impulse);
                    tireRigidbody.AddForce(rollingTireCurrentForce * 500, ForceMode.Impulse);
                    rollingTireCurrentForce = new Vector3(0,0,0);
                    rollingTireLeftRightSpeedCurrent = 0f;

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
                rollingTireCurrentSpeed = 0;


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
            RaycastHit groundInfo;
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

                if (disablePhysics && !tireRigidbody.isKinematic)
                {
                    tireRigidbody.isKinematic = true;
                }
                else if (!disablePhysics && tireRigidbody.isKinematic)
                {
                    tireRigidbody.isKinematic = false;
                }

                //CHECK IF ON SHIP
                if (isInElevator)
                {
                    if (physicsTire.transform.parent != StartOfRound.Instance.elevatorTransform)
                    {
                        Debug.Log("[TIRE]: Setting physics tire parent to elevator transform.");
                        physicsTire.transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
                    }
                    // //CHECK IF SHIP IS MOVING
                    // if (StartOfRound.Instance.shipIsLeaving || (!StartOfRound.Instance.shipHasLanded && !StartOfRound.Instance.inShipPhase))
                    // {
                    //     if (!tireRigidbody.isKinematic)
                    //     {
                    //         tireRigidbody.isKinematic = true;
                    //     }
                    // }
                    // else
                    // {
                    //     if (tireRigidbody.isKinematic)
                    //     {
                    //         tireRigidbody.isKinematic = false;
                    //     }
                    // }
                }
                else
                {
                    if (physicsTire.transform.parent == StartOfRound.Instance.elevatorTransform)
                    {
                        physicsTire.transform.SetParent(StartOfRound.Instance.propsContainer, true);
                    }
                }

                if (syncPositionTimer < syncPositionInterval)
                {
                    syncPositionTimer += Time.deltaTime;
                }
                else
                {
                    syncPositionTimer = 0f;
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
        AudioClip clip = pushAwayClip;
        switch (sound)
        {
            case 0: clip = pushAwayClip; break;
            case 1: clip = pushAwayWalkingClip; break;
            case 2: clip = pushAwaySprintingClip; break;
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

    [ServerRpc]
    private void ParentToPhysicsRegionServerRpc(NetworkObjectReference parentObject)
    {
        ParentToPhysicsRegionClientRpc(parentObject);
    }

    [ClientRpc]
    private void ParentToPhysicsRegionClientRpc(NetworkObjectReference parentObject)
    {
        NetworkObject parentTo = parentObject;
        if (parentTo != null)
        {
            PlayerPhysicsRegion physicsRegion = parentTo.GetComponentInChildren<PlayerPhysicsRegion>();
            if (physicsRegion != null && physicsRegion.allowDroppingItems)
            {
                transform.SetParent(physicsRegion.physicsTransform, worldPositionStays: true);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ParentObjectServerRpc(bool parentToShip = false)
    {
        ParentObjectClientRpc(parentToShip);
    }

    [ClientRpc]
    private void ParentObjectClientRpc(bool parentToShip = false)
    {
        if (parentToShip)
        {
            transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
        }
        else
        {
            transform.SetParent(StartOfRound.Instance.propsContainer, true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void BumpPlayerServerRpc(int playerId, float playerPushForce, int damage = 0)
    {
        BumpPlayerClientRpc(playerId, playerPushForce, damage);
    }

    [ClientRpc]
    private void BumpPlayerClientRpc(int playerId, float playerPushForce, int damage)
    {
        if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId == playerId)
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;

            Vector3 pushDirection = player.transform.position - base.transform.position;
            pushDirection.y = 0;
            pushDirection = Vector3.Normalize(pushDirection);

            player.externalForceAutoFade += pushDirection * playerPushForce;
            
            if (damage > 0)
            {
                player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Bludgeoning);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void BounceOffNonOwnerPlayerServerRpc(Vector3 playerPos, Vector3 tirePos, float pushTireForce)
    {
        BounceOffNonOwnerPlayerClientRpc(playerPos, tirePos, pushTireForce);
    }

    [ClientRpc]
    private void BounceOffNonOwnerPlayerClientRpc(Vector3 playerPos, Vector3 tirePos, float pushTireForce)
    {
        if (IsOwner)
        {
            Debug.Log("[TIRE]: Bouncing off non-owner player!");
            BounceOff(playerPos, extraForce: pushTireForce, overrideTirePosition: true, tirePosition: tirePos);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CollideWithPlayerServerRpc(int playerId, Vector3 tireVelocity)
    {
        CollideWithPlayerClientRpc(playerId, tireVelocity);
    }

    [ClientRpc]
    private void CollideWithPlayerClientRpc(int playerId, Vector3 tireVelocity)
    {
        if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId == playerId)
        {
            CollideWithPlayer(tireVelocity);
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
        tireBumpAudioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
        if (force.magnitude > 50f)
        {
            tireBumpAudioSource.PlayOneShot(tireHitSFX[UnityEngine.Random.Range(0,tireHitSFX.Length)]);
            PlayAudibleNoise(noiseRange, noiseLoudness);
        }
        else
        {
            tireBumpAudioSource.PlayOneShot(tireBumpSFX[UnityEngine.Random.Range(0,tireHitSFX.Length)]);
            PlayAudibleNoise(noiseRange, noiseLoudness);
        }
    }

    [ServerRpc]
    private void SyncPlayerRotationServerRpc(Vector3 playerRotation, int playerId)
    {
        SyncPlayerRotationClientRpc(playerRotation, playerId);
    }

    [ClientRpc]
    private void SyncPlayerRotationClientRpc(Vector3 playerRotation, int playerId)
    {
        if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId != playerId)
        {
            Debug.Log("[TIRE]: Attempting to sync player rotation!");
            StartOfRound.Instance.allPlayerScripts[playerId].transform.eulerAngles = playerRotation;
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
        //IN ITEM STATE
        if (currentBehaviourStateIndex == 0)
        {
            return;
        }

        //IN ROLLING STATE
        else if (currentBehaviourStateIndex == 1)
        {
            return;
        }

        //IN PHYSICS STATE
        else if (currentBehaviourStateIndex == 2 && physicsTire != null)
        {
            if (!base.IsOwner)
            {
                return;
            }

            GameObject otherObject = other.gameObject;

            //CHECK IF WALL BETWEEN TIRE AND COLLIDER
            RaycastHit hitInfo;
            if (Physics.Linecast(transform.position, other.transform.position, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            //PLAYER COLLISION
            if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

                //CHECK IF TIRE IS NOT ON COOLDOWN OR HELD
                if ((collisionCooldownTimer > 0) || player == playerHeldBy)
                {
                    return;
                }

                else
                {
                    CollideWithPlayerServerRpc((int)player.playerClientId, tireRigidbody.velocity);
                    collisionCooldownTimer = playerPushCollisionCooldown;
                    return;
                }
            }

            //ENEMY COLLISION
            if (otherObject.GetComponent<EnemyAICollisionDetect>() != null)
            {
                collisionCooldownTimer = 0f;
                float speed = tireRigidbody.velocity.magnitude;
                EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
                
                if (enemy.mainScript as SandSpiderAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 3);

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 6, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 1, playHitSFX: true);
                    }

                    return;
                }

                if (enemy.mainScript as FlowermanAI != null)
                {
                    FlowermanAI flowerman = enemy.mainScript as FlowermanAI;

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 8, playHitSFX: true);
                        BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                        BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                    }
                    else if (flowerman.isInAngerMode)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 3);
                    }

                    return;
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

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 6, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 1, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as SpringManAI != null)
                {
                    SpringManAI springman = enemy.mainScript as SpringManAI;

                    if (!springman.hasStopped)
                    {
                        BounceOff(enemy.mainScript.transform.position);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 10);
                    }

                    return;
                }

                else if (enemy.mainScript as JesterAI != null)
                {
                    JesterAI jester = enemy.mainScript as JesterAI;

                    if (jester.creatureAnimator.GetBool("poppedOut"))
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 15);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position);
                    }

                    return;
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
                        if (caveDweller.playerHolding)
                        {
                            return;
                        }
                        else
                        {
                            BounceOff(enemy.mainScript.transform.position);
                        }

                        if (speed > 10)
                        {
                            enemy.mainScript.HitEnemyOnLocalClient(force: 8, playHitSFX: true);
                        }
                        else if (speed > 6)
                        {
                            enemy.mainScript.HitEnemyOnLocalClient(force: 4, playHitSFX: true);
                        }
                        else if (speed > 3)
                        {
                            enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                        }
                    }

                    else if (caveDweller.adultContainer.activeSelf)
                    {
                        if (caveDweller.leaping)
                        {
                            BounceOff(enemy.mainScript.transform.position, extraForce: 20f);
                        }
                        else
                        {
                            BounceOff(enemy.mainScript.transform.position, extraForce: 12f);
                        }

                        if (speed > 10)
                        {
                            enemy.mainScript.HitEnemyOnLocalClient(force: 8, playHitSFX: true);
                        }

                        else if (speed > 6)
                        {
                            enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                        }
                    }

                    return;
                }

                else if (enemy.mainScript as MaskedPlayerEnemy != null)
                {
                    MaskedPlayerEnemy masked = enemy.mainScript as MaskedPlayerEnemy;

                    if (masked.sprinting)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 5f);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position);
                    }

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 10, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 3, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as CrawlerAI != null)
                {
                    CrawlerAI crawler = enemy.mainScript as CrawlerAI;

                    if (crawler.hasEnteredChaseMode)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 15f);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position);
                    }

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 5, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 1, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as SandWormAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 100f);
                    return;
                }

                else if (enemy.mainScript as MouthDogAI != null)
                {
                    MouthDogAI mouthDog = enemy.mainScript as MouthDogAI;

                    if (mouthDog.inLunge)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 35f);
                    }
                    else if (mouthDog.hasEnteredChaseModeFully)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 20f);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 10f);
                    }

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 5, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as ForestGiantAI != null)
                {
                    ForestGiantAI forestGiant = enemy.mainScript as ForestGiantAI;

                    if (forestGiant.chasingPlayer)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 20f);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 10f);
                    }

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 3, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as RadMechAI != null)
                {
                    RadMechAI radMech = enemy.mainScript as RadMechAI;

                    if (radMech.chargingForward)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 35f);
                    }
                    else if (radMech.isAlerted)
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 15f);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, extraForce: 10f);
                    }

                    return;
                }

                else if (enemy.mainScript as FlowerSnakeEnemy != null)
                {
                    FlowerSnakeEnemy flowerSnake = enemy.mainScript as FlowerSnakeEnemy;

                    if (flowerSnake.clingingToPlayer)
                    {
                        return;
                    }

                    if (flowerSnake.leaping)
                    {
                        BounceOff(enemy.mainScript.transform.position, forceMultiplier: 0.5f, extraForce: 1f);
                    }
                    else
                    {
                        BounceOff(enemy.mainScript.transform.position, forceMultiplier: 0.5f);
                    }

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 10, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 4, playHitSFX: true);
                    }
                    else if (speed > 3)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 1, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as CentipedeAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 5);

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 8, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                    }
                    else if (speed > 3)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 1, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as BaboonBirdAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 8);

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 4, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as Scarecrow != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 5);

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 3, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 1, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as HoarderBugAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 5);

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 8, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                    }

                    return;
                }

                else if (enemy.mainScript as NutcrackerEnemyAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 10);
                }

                else if (enemy.mainScript as PufferAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position, extraForce: 5);

                    if (speed > 10)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 8, playHitSFX: true);
                    }
                    else if (speed > 6)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 2, playHitSFX: true);
                    }
                    else if (speed > 3)
                    {
                        enemy.mainScript.HitEnemyOnLocalClient(force: 1, playHitSFX: true);
                    }

                    return;
                }
            }

            //GRABBABLE OBJECT COLLISION
            else if (otherObject.GetComponent<GrabbableObject>() != null)
            {
                GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();
                Debug.Log("[TIRE]: Collided with grabbable object!");
                float speed = tireRigidbody.velocity.magnitude;

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

    private void CollideWithPlayer(Vector3 tireVelocity)
    {
        //DETERMINE VALUES BASED ON SPEED
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        Vector3 pushDirection = player.gameplayCamera.transform.position - transform.position;
        pushDirection.y = 0;
        pushDirection = Vector3.Normalize(pushDirection);
        float angleDifference = Vector3.Angle(Vector3.Normalize(tireVelocity), Vector3.Normalize(player.walkForce));
        if (player.walkForce.magnitude < 0.1 || tireVelocity.magnitude < 0.1)
        {
            angleDifference = 180;
        }

        float pushPlayerForce = 0f;
        float pushPlayerUpForce = 0f;
        float pushTireForce = 0.5f;
        int damage = 0;

        //SLOW MOVING TIRE
        if (tireVelocity.magnitude > 0.5)
        {
            pushPlayerForce = 3;
        }

        //MEDIUM MOVING TIRE
        if (tireVelocity.magnitude > 3)
        {
            damage = 10;
            pushPlayerForce = 10;
            pushPlayerUpForce = 20;
            if (angleDifference < 80)
            {
                damage = 0;
                pushPlayerForce = 3;
            }
        }

        //FAST MOVING TIRE
        if (tireVelocity.magnitude > 6)
        {
            damage = 30;
            pushPlayerForce = 20;
            if (angleDifference < 80)
            {
                damage = 10;
                pushPlayerForce = 5;
            }
        }

        //EXTREMELY FAST MOVING TIRE (LETHAL)
        if (tireVelocity.magnitude > 10)
        {
            damage = 100;
            pushPlayerForce = 35;
        }

        //PUSH TIRE FORCE
        if (player.walkForce.magnitude > 0.1 || player.externalForceAutoFade.magnitude > 0.1)
        {
            pushTireForce = 1;

            if (angleDifference > 80 && tireVelocity.magnitude > 0.5)
            {
                pushTireForce = 1.5f;

                if (player.isSprinting)
                {
                    pushTireForce += 3f;
                }
            }
        }

        //PUSH PLAYER
        PreventPlayerJump(player);
        player.externalForceAutoFade += pushDirection * pushPlayerForce;
        if (player.jumpCoroutine == null)
        {
            player.externalForceAutoFade += Vector3.up * pushPlayerUpForce;
        }

        //DAMAGE PLAYER
        if (damage > 0)
        {
            player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Bludgeoning, force: player.externalForceAutoFade);
        }

        Debug.Log("[TIRE]:");
        Debug.Log("[TIRE]: --- COLLIDED WITH PLAYER! ---");
        Debug.Log($"[TIRE]: Tire velocity:          {tireVelocity.magnitude}");
        Debug.Log($"[TIRE]: Player walk force:      {player.walkForce.magnitude}");
        Debug.Log($"[TIRE]: Angle difference:       {angleDifference}");
        Debug.Log($"[TIRE]: Push player force:      {pushPlayerForce}");
        Debug.Log($"[TIRE]: Push player up force:   {pushPlayerUpForce}");
        Debug.Log($"[TIRE]: Damage:                 {damage}");
        Debug.Log($"[TIRE]: Push tire force:        {pushTireForce}");
        Debug.Log("[TIRE]:");

        //BOUNCE TIRE
        if (IsOwner)
        {
            BounceOff(player.transform.position, extraForce: pushTireForce);
        }
        else
        {
            BounceOffNonOwnerPlayerServerRpc(player.transform.position, physicsTire.transform.position, pushTireForce);
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

        return false;
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

        if (isTouchingGround && movementForce.magnitude > 0.5f && (currentBehaviourStateIndex == 2 || currentBehaviourStateIndex == 1))
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

    private void HinderLookInput(float xSensMultiplier = 0.5f, bool blockLeft = false, bool blockRight = false)
    {
        Vector2 lookVector = playerHeldBy.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * IngamePlayerSettings.Instance.settings.lookSensitivity;
        lookVector.x *= xSensMultiplier;

        if (blockLeft && lookVector.x < 0)
        {
            lookVector.x = 0;
        }
        else if (blockRight && lookVector.x > 0)
        {
            lookVector.x = 0;
        }

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

    private void PlayerInputWhileRolling()
    {
        //GET GROUND INFO
        Vector3 tirePosition = playerHeldBy.transform.position + playerHeldBy.transform.forward * tireRadius + Vector3.up * tireRadius;
        RaycastHit groundInfo;
        RaycastHit hit2;
        bool touchingGround = false;

        if (Physics.Raycast(tirePosition, -Vector3.up, out groundInfo, tireRadius*2 + 0.1f, 1459624193, QueryTriggerInteraction.Ignore))
        {
            touchingGround = true;
        }

        else if (Physics.Raycast(tirePosition + playerHeldBy.transform.forward * 0.1f, -Vector3.up, out hit2, tireRadius*2 + 0.1f, 1459624193, QueryTriggerInteraction.Ignore))
        {
            touchingGround = true;
            groundInfo = hit2;
        }

        if (!touchingGround || playerHeldBy.isJumping || playerHeldBy.isCrouching || playerHeldBy.isPlayerDead || playerHeldBy.isExhausted || rollingTireCurrentSpeed > 2.65f)
        {
            ReleaseRollingTire();
            return;
        }

        Vector2 moveVector = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Move").ReadValue<Vector2>();
        float sprint = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").ReadValue<float>();

        //STORE VALUE FOR SLOPE
        Vector3 compareVector = -playerHeldBy.transform.forward;
        if (moveVector.y < 0)
        {
            compareVector *= -1;
        }
        float slope = 90 - Vector3.Angle(groundInfo.normal, compareVector);

        //CALCULATE TARGET ROLLING SPEED BASED ON CURRENT CONTEXT
            float targetSpeed;

            //CHECK IF MOVING
            if (Math.Abs(moveVector.y) > 0.1f)
            {
                targetSpeed = 1f;

                //CARRY WEIGHT
                targetSpeed *= Remap(playerHeldBy.carryWeight, 1f, 3f, 1f, 0.3f);

                //SPRINTING
                if (sprint > 0f)
                {
                    targetSpeed += 0.3f;
                    playerHeldBy.sprintMeter = Mathf.Clamp(playerHeldBy.sprintMeter - Time.deltaTime / playerHeldBy.sprintTime * playerHeldBy.carryWeight * 1.5f, 0f, 1f);
                }

                //INJURED
                if (playerHeldBy.criticallyInjured)
                {
                    targetSpeed -= 0.5f;
                }

                //TERRAIN
                switch (groundInfo.collider.gameObject.tag)
                {
                    case "Metal":
                        targetSpeed += 0.1f;
                        break;

                    case "Concrete":
                        targetSpeed += 0.1f;
                        break;

                    case "Puddle":
                        targetSpeed -= 0.25f;
                        break;

                    case "Gravel":
                        targetSpeed -= 0.15f;
                        break;

                    case "Aluminum":
                        targetSpeed += 0.1f;
                        break;

                    case "Tiles":
                        targetSpeed += 0.1f;
                        break;

                    case "Wood":
                        targetSpeed += 0.1f;
                        break;

                    case "Rock":
                        targetSpeed += 0.1f;
                        break;

                    case "Snow":
                        targetSpeed -= 0.15f;
                        break;
                }

                //SLOPE
                if (slope > 0)
                {
                    targetSpeed += Remap(slope, 0, 30, 0, -1f);
                }
                else
                {
                    targetSpeed += Remap(slope, 0, -30, 0, 2);
                }

                //DIRECTION
                if (moveVector.y < 0)
                {
                    targetSpeed *= -1;
                    targetSpeed = Math.Clamp(targetSpeed, -3, 0);
                }
                else
                {
                    targetSpeed = Math.Clamp(targetSpeed, 0, 3);
                }
            }

            //IF NOT MOVING
            else
            {
                //SET TARGET SPEED TO 0
                targetSpeed = 0f;
            }

        //LERP MOVEMENT SPEED TO TARGET SPEED

            if (Math.Abs(targetSpeed - rollingTireCurrentSpeed) > 0.1f)
            {
                //INCREASING SPEED
                if (rollingTireCurrentSpeed < targetSpeed)
                {
                    rollingTireCurrentSpeed += rollingTireAcceleration * Time.deltaTime;
                }

                //DECREASING SPEED
                else
                {
                    rollingTireCurrentSpeed -= rollingTireAcceleration * Time.deltaTime * 0.5f;
                }
            }

            else
            {
                rollingTireCurrentSpeed = targetSpeed;
            }

        //MOVEMENT SPEED OVERRIDES

            //CHECK IF MOVING FAST ENOUGH TO BUMP
            bool bump = false;
            if (Math.Abs(rollingTireCurrentSpeed) > 1.1f)
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
                    if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null && Math.Abs(rollingTireCurrentSpeed) > 0.3f)
                    {
                        PlayerControllerB otherPlayer = otherObject.GetComponent<PlayerControllerB>();

                        int damageOtherPlayer = 0;
                        if (bump)
                        {
                            damageOtherPlayer = 3;
                        }

                        //BUMP OTHER PLAYER
                        BumpPlayerServerRpc((int)otherPlayer.playerClientId, playerPushForce, damageOtherPlayer);
                    }

                    //IF TIRE
                    if (otherObject.GetComponent<TireReferenceScript>() != null)
                    {
                        Tire otherTire = otherObject.GetComponent<TireReferenceScript>().mainScript;
                        float bounceOffForce = 1f;

                        if (bump)
                        {
                            bounceOffForce = 5f;
                        }

                        otherTire.BounceOff(transform.position, extraForce: bounceOffForce);
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
                                playerPushForce += 1;

                                //ADD FORCE AND RELEASE TIRE
                                rollingTireCurrentSpeed = 0f;
                                playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * playerPushForce;
                                PlayHitSoundServerRpc(rollingTireCurrentForce);
                                rollingTireBumpSoundTimer = rollingTireBumpSoundCooldown;
                                ReleaseRollingTire();
                            }
                        }
                    }

                    //OVERRIDE ROLL SPEED AND BUMP PLAYERHELDBY
                    rollingTireCurrentSpeed = 0f;
                    playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * playerPushForce;
                    if (rollingTireBumpSoundTimer <= 0)
                    {
                        PlayHitSoundServerRpc(rollingTireCurrentForce);
                        rollingTireBumpSoundTimer = rollingTireBumpSoundCooldown;
                    }
                }

                //CHECK FOR TERRAIN
                else if (Physics.Raycast(tireObject.transform.position + Vector3.up * 0.1f, playerHeldBy.transform.forward, out bumpInfo, tireRadius, CoronaMod.Masks.DefaultRoomCollidersRailingVehicleInteractableObjects, QueryTriggerInteraction.Ignore))
                {
                    float playerPushForce = 1f;

                    if (bump)
                    {
                        playerPushForce = 2f;
                    }

                    //OVERRIDE ROLL SPEED AND BUMP PLAYERHELDBY
                    rollingTireCurrentSpeed = 0f;
                    playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * playerPushForce;
                    if (rollingTireBumpSoundTimer <= 0)
                    {
                        PlayHitSoundServerRpc(rollingTireCurrentForce);
                        rollingTireBumpSoundTimer = rollingTireBumpSoundCooldown;
                    }
                }

                //CHECK FOR ITEMS
                else if (Physics.Raycast(tireObject.transform.position - Vector3.up * 0.2f, playerHeldBy.transform.forward, out bumpInfo, tireRadius, 64, QueryTriggerInteraction.Collide))
                {
                    Debug.Log("[TIRE]: Item raycast found collider!");
                    if (bumpInfo.collider.gameObject.GetComponent<GrabbableObject>() != null)
                    {
                        GrabbableObject gObject = bumpInfo.collider.gameObject.GetComponent<GrabbableObject>();
                        Debug.Log($"[TIRE]: Item raycast found item {gObject.name}!");

                        float playerPushForce = 1f;

                        void resetRollSpeedAndBump()
                        {
                            rollingTireCurrentSpeed = 0f;
                            playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * playerPushForce;
                            if (rollingTireBumpSoundTimer <= 0)
                            {
                                PlayHitSoundServerRpc(rollingTireCurrentForce);
                                rollingTireBumpSoundTimer = rollingTireBumpSoundCooldown;
                            }
                        }

                        if (bump)
                        {
                            playerPushForce = 2f;
                        }

                        switch (gObject)
                        {
                            case ArtilleryShellItem artilleryShell:
                                if (bump)
                                {
                                    float c = UnityEngine.Random.Range(0,100);
                                    if (c < artilleryShell.explodeOnHitChance)
                                    {
                                        artilleryShell.ArmShellAndSync();
                                    }
                                }
                                resetRollSpeedAndBump();
                                return;

                            case HydraulicStabilizer hydraulic:
                                if (bump)
                                {
                                    hydraulic.GoPsychoAndSync();
                                }
                                resetRollSpeedAndBump();
                                return;

                            case Radiator radiator:
                                if (bump)
                                {
                                    radiator.FallOverAndSync(playerHeldBy.transform.forward);
                                }
                                resetRollSpeedAndBump();
                                return;
                            
                            case Toaster toaster:
                                if (bump)
                                {
                                    if (toaster.inserted)
                                    {
                                        toaster.EjectAndSync();
                                    }
                                    resetRollSpeedAndBump();
                                }
                                return;

                            case Vase vase:
                                int wobbleType = 1;
                                if (bump)
                                {
                                    wobbleType = 2;
                                }
                                vase.WobbleAndSync(wobbleType);
                                resetRollSpeedAndBump();
                                return;
                        }
                    }
                }

            //PLAYERHELDBY COLLISION

                //CHECK FOR PLAYERS OR ENEMIES
                if (Physics.Raycast(playerHeldBy.playerEye.transform.position, -playerHeldBy.transform.forward, out bumpInfo, 0.5f, CoronaMod.Masks.PlayerEnemies, QueryTriggerInteraction.Collide))
                {
                    GameObject otherObject = bumpInfo.collider.gameObject;
                    Debug.Log($"[TIRE]: Player bumped against {otherObject}!");

                    if (otherObject != playerHeldBy.gameObject)
                    {
                        if (bump)
                        {
                            ReleaseRollingTire();
                            if (rollingTireBumpSoundTimer <= 0)
                            {
                                PlayHitSoundServerRpc(rollingTireCurrentForce);
                                rollingTireBumpSoundTimer = rollingTireBumpSoundCooldown;
                            }
                            playerHeldBy.DamagePlayer(5, causeOfDeath: CauseOfDeath.Crushing);
                            Debug.Log($"[TIRE]: Player squished against {otherObject}!");
                        }

                        //OVERRIDE ROLL SPEED
                        if (moveVector.y < 0.1)
                        {
                            rollingTireCurrentSpeed = 0f;
                        }
                    }
                }

                //CHECK FOR TERRAIN
                else if (Physics.Raycast(playerHeldBy.playerEye.transform.position, -playerHeldBy.transform.forward, out bumpInfo, 0.5f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicleInteractableObjects, QueryTriggerInteraction.Ignore))
                {
                    GameObject otherObject = bumpInfo.collider.gameObject;
                    Debug.Log($"[TIRE]: Player bumped against {otherObject}!");

                    if (bump)
                    {
                        ReleaseRollingTire();
                        if (rollingTireBumpSoundTimer <= 0)
                        {
                            PlayHitSoundServerRpc(rollingTireCurrentForce);
                            rollingTireBumpSoundTimer = rollingTireBumpSoundCooldown;
                        }
                        playerHeldBy.DamagePlayer(1, causeOfDeath: CauseOfDeath.Crushing);
                        Debug.Log($"[TIRE]: Player squished against {otherObject}!");
                    }

                    //OVERRIDE ROLL SPEED
                    if (moveVector.y < 0.1)
                    {
                        rollingTireCurrentSpeed = 0f;
                    }
                }

        //BUMP SOUND COOLDOWN
        if (rollingTireBumpSoundTimer > 0)
        {
            rollingTireBumpSoundTimer -= Time.deltaTime;
        }

        //IF BUMPED BY STRONG ENOUGH EXTERNAL FORCE
        if (playerHeldBy.externalForceAutoFade.magnitude > 1f)
        {
            rollingTireCurrentSpeed = 0;
        }

        //RELEASE TIRE IF PULLING UPHILL
        if (moveVector.y < 0 && slope > 20f && rollingTireCurrentSpeed > -0.1)
        {
            ReleaseRollingTire();
            return;
        }

        //MOVE PLAYER WITH ROLL SPEED
        rollingTireCurrentForce = playerHeldBy.transform.forward * rollingTireCurrentSpeed * rollingTireSpeed;
        playerHeldBy.thisController.Move(rollingTireCurrentForce);

        //SET PUSH AWAY FORCE
        pushedWhileWalking = false;
        pushedWhileSprinting = false;
        if (moveVector.y > 0.1f)
        {
            pushedWhileWalking = true;
            pushedWhileSprinting = sprint > 0;
        }

        //ROTATE PLAYER LEFT AND RIGHT
        if (Math.Abs(moveVector.x) > 0.1f)
        {
            if (Math.Abs(rollingTireLeftRightSpeedCurrent) < rollingTireLeftRightSpeed)
            {
                if (moveVector.x < 0)
                {
                    rollingTireLeftRightSpeedCurrent -= rollingTireLeftRightAcceleration * Time.deltaTime;
                }
                else if (moveVector.x > 0)
                {
                    rollingTireLeftRightSpeedCurrent += rollingTireLeftRightAcceleration * Time.deltaTime;
                }
            }
            else
            {
                if (moveVector.x < 0)
                {
                    rollingTireLeftRightSpeedCurrent = -rollingTireLeftRightSpeed;
                }
                else
                {
                    rollingTireLeftRightSpeedCurrent = rollingTireLeftRightSpeed;
                }
            }
        }

        else
        {
            if (Math.Abs(rollingTireLeftRightSpeedCurrent) > rollingTireAcceleration * Time.deltaTime * 1.5f)
            {
                if (rollingTireLeftRightSpeedCurrent > 0)
                {
                    rollingTireLeftRightSpeedCurrent -= rollingTireLeftRightAcceleration * Time.deltaTime;
                }
                else
                {
                    rollingTireLeftRightSpeedCurrent += rollingTireLeftRightAcceleration * Time.deltaTime;
                }
            }
            else
            {
                rollingTireLeftRightSpeedCurrent = 0f;
            }
        }

        //PREVENT ROTATION IF COLLIDERS BLOCKING
        bool blockedByColliderLeft = false;
        bool blockedByColliderRight = false;
        if (Physics.Raycast(transform.position + playerHeldBy.transform.forward*tireRadius, playerHeldBy.transform.right, out bumpInfo, tireRadius, CoronaMod.Masks.DefaultRoomCollidersRailingVehicleInteractableObjects, QueryTriggerInteraction.Ignore))
        {
            blockedByColliderRight = true;
        }
        else if (Physics.Raycast(transform.position + playerHeldBy.transform.forward*tireRadius, -playerHeldBy.transform.right, out bumpInfo, tireRadius, CoronaMod.Masks.DefaultRoomCollidersRailingVehicleInteractableObjects, QueryTriggerInteraction.Ignore))
        {
            blockedByColliderLeft = true;
        }

        if (blockedByColliderLeft && rollingTireLeftRightSpeedCurrent < 0f)
        {
            rollingTireLeftRightSpeedCurrent = 0f;
        }
        else if (blockedByColliderRight && rollingTireLeftRightSpeedCurrent > 0f)
        {
            rollingTireLeftRightSpeedCurrent = 0f;
        }

        //SET ANIMATION VALUES
        if (rollingTireAnimator != null)
        {
            rollingTireAnimator.SetFloat("rollingTireCurrentSpeed", rollingTireCurrentSpeed);
            rollingTireAnimator.SetFloat("rollingTireLeftRightSpeed", rollingTireLeftRightSpeedCurrent);
            rollingTireAnimator.SetFloat("moveInputX", moveVector.x);
            rollingTireAnimator.SetFloat("moveInputY", moveVector.y);
            rollingTireAnimator.SetBool("sprinting", sprint > 0);
        }

        //HINDER LEFT/RIGHT MOUSE
        HinderLookInput(0.1f, blockedByColliderLeft, blockedByColliderRight);

        //ROTATE PLAYER
        playerHeldBy.transform.eulerAngles += new Vector3(0f, rollingTireLeftRightSpeedCurrent, 0f);

        //SYNC PLAYER ROTATION
        SyncPlayerRotationServerRpc(playerHeldBy.transform.eulerAngles, (int)playerHeldBy.playerClientId);
        lastPlayerRotation = playerHeldBy.transform.eulerAngles.y;

        //ROTATE TIRE VISUALLY
        RotateTireObjectServerRpc(rollingTireCurrentSpeed * 2f);
        tireObject.transform.eulerAngles += new Vector3(0,0,rollingTireCurrentSpeed * 2f);

        //TIRE AUDIO
        SetRollAudio(true, rollingTireCurrentForce * 30f, groundInfo.collider.gameObject.tag);

        //TIRE AUDIBLE NOISE
        if (Math.Abs(rollingTireCurrentSpeed) > 0.1f)
        {
            if (rollingAudibleNoiseTimer <= 0)
            {
                rollingAudibleNoiseTimer = rollingAudibleNoiseCooldown;
                PlayAudibleNoise(noiseRange, noiseLoudness);
            }
            else
            {
                rollingAudibleNoiseTimer -= Time.deltaTime;
            }
        }
    }

    private IEnumerator DisplayCursorTip(string cursorTip)
    {
        playerHeldBy.cursorTip.text = cursorTip;
        while(playerHeldBy.playerActions.Movement.ActivateItem.IsPressed())
        {
            // Debug.Log("[TIRE]: Player holding use!");
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