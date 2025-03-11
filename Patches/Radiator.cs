using UnityEngine;
using GameNetcodeStuff;
using System.Collections;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using System;
using Random = UnityEngine.Random;
using UnityEngine.Events;

public class Radiator : GrabbableObject, IHittable, ITouchable
{
    [Space(15f)]
    [Header("Radiator Settings")]
    public Animator itemAnimator;
    
    public AudioSource bumpSource;

    public AudioSource wheelSource;

    public AudioClip[] lightBumpClips;

    public AudioClip[] heavyBumpClips;

    public AudioClip[] lightWheelClips;

    public AudioClip[] heavyWheelClips;

    public NavMeshAgent agent;

    [HideInInspector]
    public NavMeshPath path;

    public float crouchSpeed = 0.75f;

    public float crouchDistance = 1.5f;

    public float walkSpeed = 1.2f;

    public float walkDistance = 2.5f;

    public float sprintSpeed = 2.4f;

    public float sprintDistance = 4.5f;

    public float updateInterval = 0.25f;

    public float lastSeenDuration = 18f;

    private List<PlayerControllerB> playersInside = new List<PlayerControllerB>();

    private List<Collider> collidersHitByRadiator = new List<Collider>();

    private Coroutine moveCoroutine;

    private Coroutine fallOverCoroutine;

    private float lastActionTime;

    private float lastHauntCheckTime;

    private float updateTimer;

    private GameObject debugPos;

    private static bool haunted;

    public bool fallen;

    private float moveTimer;

    private float moveCooldown = 0.3f;

    private float wallRadius = 1f;

    private float rayHeightMult = 0.4f;

    public override void Start()
    {
        path = new NavMeshPath();
        lastActionTime = Time.realtimeSinceStartup;
        ResetStatesAndSync();
        ResetAnimationsAndSync(silent: true, clearMemory: true);
        base.Start();
        ResetHaunt();
        StartOfRound.Instance.StartNewRoundEvent.AddListener(ResetHaunt);
        CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.AddListener(ResetHaunt);
    }

    public void ResetHaunt()
    {
        haunted = false;
    }

    public override void Update()
    {
        if (updateTimer > 0f)
        {
            updateTimer -= Time.deltaTime;
        }
        else
        {
            IntervalUpdate();
            updateTimer = updateInterval;
        }
        if (moveTimer > 0f)
        {
            moveTimer -= Time.deltaTime;
        }
		if (currentUseCooldown >= 0f)
		{
			currentUseCooldown -= Time.deltaTime;
		}
		if (base.IsOwner)
		{
			if (isBeingUsed && itemProperties.requiresBattery)
			{
				if (insertedBattery.charge > 0f)
				{
					if (!itemProperties.itemIsTrigger)
					{
						insertedBattery.charge -= Time.deltaTime / itemProperties.batteryUsage;
					}
				}
				else if (!insertedBattery.empty)
				{
					insertedBattery.empty = true;
					if (isBeingUsed)
					{
						Debug.Log("Use up batteries local");
						isBeingUsed = false;
						UseUpBatteries();
						isSendingItemRPC++;
						UseUpItemBatteriesServerRpc();
					}
				}
			}
			if (!wasOwnerLastFrame)
			{
				wasOwnerLastFrame = true;
			}
		}
		else if (wasOwnerLastFrame)
		{
			wasOwnerLastFrame = false;
		}
		if (!isHeld && parentObject == null)
		{
			if (fallTime < 1f)
			{
				reachedFloorTarget = false;
				FallWithCurve();
				if (base.transform.localPosition.y - targetFloorPosition.y < 0.05f && !hasHitGround)
				{
					PlayDropSFX();
					OnHitGround();
				}
				return;
			}
			if (!reachedFloorTarget)
			{
				if (!hasHitGround)
				{
					PlayDropSFX();
					OnHitGround();
				}
				reachedFloorTarget = true;
				if (floorYRot == -1)
				{
					base.transform.rotation = Quaternion.Euler(itemProperties.restingRotation.x, base.transform.eulerAngles.y, itemProperties.restingRotation.z);
				}
				else
				{
					base.transform.rotation = Quaternion.Euler(itemProperties.restingRotation.x, (float)(floorYRot + itemProperties.floorYOffset) + 90f, itemProperties.restingRotation.z);
				}
			}
		}
		else if (isHeld || isHeldByEnemy)
		{
			reachedFloorTarget = false;
		}
    }

    public override void EquipItem()
    {
        lastActionTime = Time.realtimeSinceStartup;
        ResetStatesAndSync();
        ResetAnimationsAndSync(silent: true, clearMemory: true);
        base.EquipItem();
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        lastActionTime = Time.realtimeSinceStartup;
        ResetStatesAndSync();
        ResetAnimationsAndSync(silent: true, clearMemory: true);
        base.GrabItemFromEnemy(enemy);
    }

    public override void DiscardItem()
    {
        lastActionTime = Time.realtimeSinceStartup;
        ResetStatesAndSync();
        ResetAnimationsAndSync(silent: true, clearMemory: true);
        base.DiscardItem();
    }

    public void OnTouch(Collider other)
    {
        if (fallen || isHeld || isHeldByEnemy || !hasHitGround || moveTimer > 0f || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f || base.transform.parent?.GetComponentInChildren<PlayerPhysicsRegion>() != null)
        {
            return;
        }
        if (other.gameObject.layer != 3 && other.gameObject.layer != 6 && other.gameObject.layer != 19)
        {
            return;
        }
        int chosenSpeed = 1;
        ulong playerId = 0;
        if (other.gameObject.layer == 19)
        {
            if (!other.TryGetComponent<EnemyAICollisionDetect>(out var enemy))
            {
                return;
            }
            else
            {
                if (enemy.mainScript.TryGetComponent<BaboonBirdAI>(out _))
                {
                    return;
                }
                chosenSpeed = 0;
            }
        }
        if (other.gameObject.layer == 6)
        {
            if (other.TryGetComponent<GrabbableObject>(out var gObject) && !Physics.Linecast(other.gameObject.transform.position + Vector3.up * 0.5f, base.transform.position + Vector3.up, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                if (gObject.TryGetComponent<SoccerBallProp>(out var ball))
                {
                    if (ball.hasHitGround || ball.isHeld || ball.isHeldByEnemy)
                    {
                        return;
                    }
                    else
                    {
                        chosenSpeed = 0;
                        ball.BeginKickBall(base.transform.position + Vector3.up, hitByEnemy: false);
                    }
                }
                else
                {
                    return;
                }
            }
        }
        else if (other.TryGetComponent<PlayerControllerB>(out var player))
        {
            if (player.isJumping || player.isFallingFromJump || player.isFallingNoJump)
            {
                return;
            }
            playerId = player.playerClientId;
            ResetAnimationsAndSync(silent: true, clearMemory: true);
            if (player.isSprinting)
            {
                chosenSpeed = 0;
            }
            if (!Physics.Linecast(base.transform.position, player.transform.position, out var _, 256, QueryTriggerInteraction.Ignore))
            {
                float physicsForce = chosenSpeed switch
                {
                    0 => 1f,
                    _ => 0.7f
                };
                float dist = Mathf.Clamp(Vector3.Distance(player.transform.position, base.transform.position), 0.3f, 0.6f);
                Vector3 vector = Vector3.Normalize(player.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
                vector = new Vector3(vector.x, 0, vector.z);
                if (vector.magnitude > 2f)
                {
                    if (vector.magnitude > 10f)
                    {
                        player.CancelSpecialTriggerAnimations();
                    }
                    if (!player.inVehicleAnimation || (player.externalForceAutoFade + vector).magnitude > 50f)
                    {
                        player.externalForceAutoFade += vector;
                    }
                }
            }
            if (player.isCrouching)
            {
                ResetAnimationsAndSync(silent: false, clearMemory: true);
                return;
            }
        }
        else if (other.TryGetComponent<TireReferenceScript>(out var tireReferenceScript))
        {
            float speed = tireReferenceScript.mainScript.tireRigidbody.velocity.magnitude;
            if (speed < 5f)
            {
                if (!fallen && !isHeld && !isHeldByEnemy && hasHitGround && !StartOfRound.Instance.inShipPhase && StartOfRound.Instance.timeSinceRoundStarted > 2f)
                {
                    moveTimer = 0f;
                    Vector3 moveDirection = Vector3.Normalize(new Vector3(other.transform.position.x, base.transform.position.y, other.transform.position.z) - base.transform.position);
                    Vector3 movePos = base.transform.position - moveDirection * speed;
                    MoveTowardsAndSync(movePos, silent: false, speedIndex: 0);
                }
                tireReferenceScript.mainScript.BounceOff(base.transform.position, extraForce: 3f);
            }
            else if (speed >= 5f)
            {
                FallOverAndSync(-(new Vector3(other.transform.position.x, 0f, other.transform.position.z) - new Vector3(transform.position.x, 0f, transform.position.z)));
                tireReferenceScript.mainScript.BounceOff(base.transform.position, extraForce: 10f);
            }
        }
        float chosenDistance = chosenSpeed switch {0 => sprintDistance, 1 => walkDistance, _ => crouchDistance};
        Vector3 direction = Vector3.Normalize(new Vector3(other.transform.position.x, base.transform.position.y, other.transform.position.z) - base.transform.position);
        Vector3 pos = base.transform.position - (direction * chosenDistance);
        MoveTowardsAndSync(pos, silent: false, speedIndex: chosenSpeed, clientWhoPushed: playerId);
    }

    public void OnExit(Collider other)
    {
    }

    private void IntervalUpdate()
    {
        if (fallen || isHeld || isHeldByEnemy || !hasHitGround || base.isInShipRoom || !base.isInFactory || !base.IsOwner || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f || base.transform.parent?.GetComponentInChildren<PlayerPhysicsRegion>() != null)
        {
            return;
        }
        playersInside = [.. StartOfRound.Instance.allPlayerScripts.Where(player => player.isInsideFactory)];
        if (playersInside.Count <= 0)
        {
            return;
        }
        if (!haunted && Time.realtimeSinceStartup - lastHauntCheckTime > 30f)
        {
            lastHauntCheckTime = Time.realtimeSinceStartup;
            if (RoundManager.Instance.SpawnedEnemies.Any(enemy => enemy is DressGirlAI))
            {
                haunted = true;
                lastSeenDuration *= 0.8f;
            }
        }
        bool losFlag = false;
        bool movable = false;
        for (int i = 0; i < playersInside.Count; i++)
        {
            if (playersInside[i].HasLineOfSightToPosition(base.transform.position + Vector3.up * rayHeightMult))
            {
                losFlag = true;
            }
        }
        if (NavMesh.SamplePosition(base.transform.position, out var hit, 2f, -1) && (Time.realtimeSinceStartup - lastActionTime > lastSeenDuration))
        {
            if ((haunted || Random.Range(0f, 100f) < 10f) && (!losFlag || Random.Range(0f, 100f) < 5f))
            {
                movable = true;
            }
            else if (Time.realtimeSinceStartup - lastActionTime > lastSeenDuration)
            {
                lastActionTime = Time.realtimeSinceStartup;
            }
        }
        else
        {
            movable = false;
        }
        if (movable)
        {
            if (NavMesh.CalculatePath(hit.position, ClosestPlayerInside().position, -1, path))
            {
                RoamMovement(hit.position, ClosestPlayerInside().position);
                lastActionTime = Time.realtimeSinceStartup;
            }
            else
            {
                lastActionTime = Time.realtimeSinceStartup;
            }
        }
    }

    public void RoamMovement(Vector3 pathStart, Vector3 pathEnd)
    {
        if (NavMesh.CalculatePath(pathStart, pathEnd, -1, path))
        {
            Vector3 currentPosition = base.transform.position;
            float maxRange = 200f;
            int corner = 0;
            for (int i = 0; i < path.corners.Length; i++)
            {
                float sqrMagnitude = (path.corners[i] - base.transform.position).sqrMagnitude;
                if (sqrMagnitude < maxRange)
                {
                    maxRange = sqrMagnitude;
                    corner = (i + 1 > path.corners.Length) ? i : i + 1;
                }
            }
            Vector3 newPosition = path.corners[corner];
            float maxWallDistance = wallRadius * 0.85f;
            float wallDistance = maxWallDistance;
            float yRotation = -1;
            for (int i = 0; i < 360; i += 360/6)
            {
                RoundManager.Instance.tempTransform.eulerAngles = new Vector3(base.transform.eulerAngles.x, i, base.transform.eulerAngles.z);
                if (Physics.Raycast(currentPosition + Vector3.up * rayHeightMult, RoundManager.Instance.tempTransform.forward, out var hitInfo, maxWallDistance, CoronaMod.Masks.RadiatorMask, QueryTriggerInteraction.Ignore))
                {
                    if (hitInfo.distance < wallDistance)
                    {
                        wallDistance = hitInfo.distance;
                        yRotation = i;
                    }
                }
            }
            if (yRotation != -1)
            {
                RoundManager.Instance.tempTransform.eulerAngles = new Vector3(base.transform.eulerAngles.x, yRotation, base.transform.eulerAngles.z);
                currentPosition = path.corners[corner] - RoundManager.Instance.tempTransform.forward * (maxWallDistance - wallDistance);
                Vector3 checkFromPosition = currentPosition + Vector3.up * rayHeightMult;
                if (Physics.Raycast(checkFromPosition, Vector3.down, out var hitInfo, 8f, CoronaMod.Masks.RadiatorMask, QueryTriggerInteraction.Ignore))
                {
                    newPosition = hitInfo.point;
                }
                else
                {
                    newPosition = path.corners[corner];
                }
            }
            MoveTowardsAndSync(newPosition, silent: true, roaming: true);
        }
    }

    public void MoveTowardsAndSync(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false, int timesBounced = 0, bool affectRotation = true, ulong clientWhoPushed = 0)
    {
        MoveTowards(position, base.transform.position, base.transform.rotation, silent, speedIndex, roaming, timesBounced, affectRotation);
        MoveTowardsServerRpc(position, base.transform.position, base.transform.rotation, silent, speedIndex, (int)GameNetworkManager.Instance.localPlayerController.playerClientId, roaming, timesBounced, affectRotation, clientWhoPushed);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveTowardsServerRpc(Vector3 position, Vector3 ownerPos, Quaternion ownerRot, bool silent, int speedIndex = -1, int clientWhoSentRpc = -1, bool roaming = false, int timesBounced = 0, bool affectRotation = true, ulong clientWhoPushed = 0)
    {
        if (clientWhoPushed != 0 && base.OwnerClientId != clientWhoPushed)
        {
            try
            {
                base.gameObject.GetComponent<NetworkObject>().ChangeOwnership(clientWhoPushed);
            }
            catch (Exception arg)
            {
                Debug.Log($"Failed to transfer ownership of radiator to client: {arg}");
            }
        }
        MoveTowardsClientRpc(position, ownerPos, ownerRot, silent, speedIndex, clientWhoSentRpc, roaming, timesBounced, affectRotation);
    }

    [ClientRpc]
    public void MoveTowardsClientRpc(Vector3 position, Vector3 ownerPos, Quaternion ownerRot, bool silent, int speedIndex = -1, int clientWhoSentRpc = -1, bool roaming = false, int timesBounced = 0, bool affectRotation = true)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            MoveTowards(position, ownerPos, ownerRot, silent, speedIndex, roaming, timesBounced, affectRotation);
        }
    }

    //TODO: What is this (ownerPos, ownerRot)? Did I just never implement this?
    public void MoveTowards(Vector3 position, Vector3 ownerPos, Quaternion ownerRot, bool silent, int speedIndex = -1, bool roaming = false, int timesBounced = 0, bool affectRotation = true)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        moveCoroutine = StartCoroutine(DecelerateTowards(position, silent, speedIndex, roaming, timesBounced, affectRotation));
    }

    public IEnumerator DecelerateTowards(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false, int timesBounced = 0, bool affectRotation = true)
    {
        if ((fallen && affectRotation) || !grabbable || isHeld || isHeldByEnemy || moveTimer > 0f || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            ResetAnimationsAndSync(silent: true, clearMemory: true);
            yield break;
        }
        bool heavy = speedIndex == 0;
        ResetAnimationsAndSync(silent, clearMemory: false, heavy: heavy);
        moveTimer = moveCooldown;
        lastActionTime = Time.realtimeSinceStartup;
        float timeBumped = Time.realtimeSinceStartup;
        Vector3 startPosition = base.transform.position;
        float chosenSpeed;
        float chosenDistance;
        if (speedIndex == -1 && roaming && playersInside.Count > 0)
        {
            chosenSpeed = Vector3.Distance(base.transform.position, ClosestPlayerInside().position) switch {> 14f => sprintSpeed, > 6f => walkSpeed, _ => crouchSpeed};
            chosenDistance = Vector3.Distance(base.transform.position, ClosestPlayerInside().position) switch {> 14f => sprintDistance, > 6f => walkDistance, _ => crouchDistance};
        }
        else if (speedIndex == -1)
        {
            chosenSpeed = Vector3.Distance(base.transform.position, position) switch {> 8f => sprintSpeed, > 4f => walkSpeed, _ => crouchSpeed};
            chosenDistance = Vector3.Distance(base.transform.position, position) switch {> 8f => sprintDistance, > 4f => walkDistance, _ => crouchDistance};
        }
        else
        {
            chosenSpeed = speedIndex switch {0 => sprintSpeed, 1 => walkSpeed, _ => crouchSpeed};
            chosenDistance = speedIndex switch {0 => sprintDistance, 1 => walkDistance, _ => crouchDistance};
        }
        chosenDistance = timesBounced switch {0 => chosenDistance, 1 => chosenDistance / 2, 2 => chosenDistance / 3, _ => 0f};
        Vector3 direction = Vector3.Normalize(position - startPosition);
        float noWallDistance = Mathf.Clamp(Vector3.Distance(startPosition, position), 0f, chosenDistance);
		Ray ray = new Ray(base.transform.position + Vector3.up * rayHeightMult, direction);
		Vector3 wallPos = (!Physics.Raycast(ray, out RaycastHit wallHitInfo, noWallDistance, CoronaMod.Masks.RadiatorMask)) ? ray.GetPoint(noWallDistance) : ray.GetPoint(Vector3.Distance(ray.origin, wallHitInfo.point));
        Vector3 vertPos = Physics.Raycast(position + Vector3.up * rayHeightMult, Vector3.down, out RaycastHit vertHitInfo, 10f, CoronaMod.Masks.RadiatorMask, QueryTriggerInteraction.Ignore) ? vertHitInfo.point + itemProperties.verticalOffset * Vector3.up : position + itemProperties.verticalOffset * Vector3.up;
        Vector3 endPosition = new Vector3(wallPos.x, vertPos.y, wallPos.z);
        if (Vector3.Distance(base.transform.position, new Vector3(endPosition.x, base.transform.position.y, endPosition.z)) < 0.05f && !roaming)
        {
            yield break;
        }
        float endY = base.transform.eulerAngles.y + Random.Range(-50f, 50f);
        wheelSource.clip = heavy ? heavyWheelClips[Random.Range(0, heavyWheelClips.Length)] : lightWheelClips[Random.Range(0, lightWheelClips.Length)];
        wheelSource.pitch = Random.Range(0.85f, 1.2f);
        wheelSource.Play();
        bool scaredPlayer = false;
        while (Vector3.Distance(base.transform.position, new Vector3(endPosition.x, base.transform.position.y, endPosition.z)) > 0.05f)
        {
            //CHECK FUTURE POSITION FOR CLIFFS OR WALLS
            Vector3 futurePosition = base.transform.position;
            futurePosition = Vector3.Lerp(futurePosition, endPosition, chosenSpeed * Time.deltaTime);
            bool futureCast = Physics.Raycast(futurePosition + Vector3.up * rayHeightMult, Vector3.down, out RaycastHit futureHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            futurePosition = new Vector3(futurePosition.x, futureHitInfo.point.y, futurePosition.z);
            Vector3 futureLocalPosition = base.transform.localPosition;
            if (futureCast)
            {
                Vector3 futureTargetFloorPosition = futureHitInfo.point + itemProperties.verticalOffset * Vector3.up;
                if (base.transform.parent != null)
                {
                    futureTargetFloorPosition = base.transform.parent.InverseTransformPoint(futureTargetFloorPosition);
                }
                if (Mathf.Abs(futureLocalPosition.y - futureTargetFloorPosition.y) > 0.12f || Mathf.Abs(futurePosition.y - futureHitInfo.point.y) > 0.12f || Vector3.Angle(base.transform.up, futureHitInfo.normal) > 48f || (Vector3.Angle(Vector3.up, futureHitInfo.normal) > 45f && !isInFactory))
                {
                    Debug.Log($"{Mathf.Abs(futureLocalPosition.y - futureTargetFloorPosition.y)}, {Mathf.Abs(futurePosition.y - futureHitInfo.point.y)}, {Vector3.Angle(base.transform.up, futureHitInfo.normal)}, {Vector3.Angle(Vector3.up, futureHitInfo.normal)}");
                    if (!fallen)
                    {
                    ResetAnimationsAndSync(silent: !roaming, clearMemory: true);
                    FallOverAndSync(direction);
                    yield break;
                    }
                    else
                    {
                        ResetAnimationsAndSync(silent: !roaming, clearMemory: true);
                        yield break;
                    }
                }
            }
            else
            {
                ResetAnimationsAndSync(silent: !roaming, clearMemory: true);
                yield break;
            }
            if (!roaming)
            {
                float maxWallDistance = wallRadius * 0.65f;
                float wallDistance = maxWallDistance;
                Vector3 wallNormal = Vector3.zero;
                for (int i = 0; i < 360; i += 360 / 6)
                {
                    RoundManager.Instance.tempTransform.eulerAngles = new Vector3(base.transform.eulerAngles.x, i, base.transform.eulerAngles.z);
                    if (Physics.Raycast(futurePosition + Vector3.up * rayHeightMult, RoundManager.Instance.tempTransform.forward, out RaycastHit collideInfo, maxWallDistance, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
                    {
                        if (collideInfo.distance < wallDistance)
                        {
                            wallDistance = collideInfo.distance;
                            wallNormal = collideInfo.normal;
                        }
                    }
                }
                if (wallNormal != Vector3.zero && Vector3.Angle(base.transform.up, wallNormal) > 50f)
                {
                    Vector3 newDirection = Vector3.Reflect(new Vector3(direction.x, 0f, direction.z), new Vector3(wallNormal.x, 0f, wallNormal.z));
                    Vector3 newPos = base.transform.position + (newDirection * chosenDistance);
                    moveTimer = 0f;
                    MoveTowardsAndSync(newPos, silent: false, speedIndex: speedIndex, timesBounced: timesBounced + 1);
                    yield break;
                }
            }

            yield return null;

            //INTERACT WITH OBJECTS
            if (Time.realtimeSinceStartup - timeBumped > moveCooldown)
            {
                RaycastHit[] results = Physics.SphereCastAll(base.transform.position + Vector3.up, 0.75f, Vector3.down, 0.75f, CoronaMod.Masks.PropsMapHazards);
                for (int i = 0; i < results.Count(); i++)
                {
                    if (collidersHitByRadiator.Contains(results[i].collider))
                    {
                        continue;
                    }
                    else
                    {
                        BumpInto(results[i].collider, speedIndex);
                        break;
                    }
                }
            }
            if (roaming && !scaredPlayer)
            {
                for (int i = 0; i < playersInside.Count; i++)
                {
                    if (playersInside[i].HasLineOfSightToPosition(base.transform.position + Vector3.up * rayHeightMult))
                    {
                        playersInside[i].insanityLevel += playersInside[i].maxInsanityLevel * 0.1f;
                        scaredPlayer = true;
                    }
                }
            }

            //MOVE FORWARD
            base.transform.position = Vector3.Lerp(base.transform.position, endPosition, chosenSpeed * Time.deltaTime);
            base.transform.position = new Vector3(base.transform.position.x, futureHitInfo.point.y + itemProperties.verticalOffset, base.transform.position.z);
            if (Physics.Raycast(base.transform.position, Vector3.down, out RaycastHit hitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                Vector3 newTargetFloorPosition = hitInfo.point + itemProperties.verticalOffset * (Vector3.up * (!affectRotation ? 1.5f : 1.18f));
                if (base.transform.parent != null)
                {
                    newTargetFloorPosition = base.transform.parent.InverseTransformPoint(newTargetFloorPosition);
                }
                targetFloorPosition = newTargetFloorPosition;
                base.transform.localPosition = targetFloorPosition;
            }
            Physics.Raycast(base.transform.position + (base.transform.forward * 0.4f) + (Vector3.up  * rayHeightMult), Vector3.down, out RaycastHit frontHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            Physics.Raycast(base.transform.position - (base.transform.forward * 0.4f) + (Vector3.up  * rayHeightMult), Vector3.down, out RaycastHit backHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            Vector3 avgUp = frontHitInfo.normal + backHitInfo.normal;
            if (affectRotation)
            {
                base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.FromToRotation(base.transform.up, avgUp), chosenSpeed * Time.deltaTime * 2f);
                base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, endY, chosenSpeed * Time.deltaTime * 3f), base.transform.eulerAngles.z);
            }
            itemAnimator.SetLayerWeight(1, Mathf.Clamp(Vector3.Distance(base.transform.position, new Vector3(endPosition.x, base.transform.position.y, endPosition.z)), 0f, 1f));
        }
        ResetAnimationsAndSync(silent: true, clearMemory: true);
    }

    public void FallOverAndSync(Vector3 direction)
    {
        StartFallOver(direction);
        FallOverServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, direction);
    }

    [ServerRpc(RequireOwnership = false)]
    private void FallOverServerRpc(int clientWhoSentRpc, Vector3 direction)
    {
        FallOverClientRpc(clientWhoSentRpc, direction);
    }

    [ClientRpc]
    private void FallOverClientRpc(int clientWhoSentRpc, Vector3 direction)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            StartFallOver(direction);
        }
    }

    private void StartFallOver(Vector3 direction)
    {
            if (moveCoroutine != null && !fallen)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }
            if (fallOverCoroutine != null)
            {
                StopCoroutine(fallOverCoroutine);
                fallOverCoroutine = null;
            }
            fallOverCoroutine = StartCoroutine(FallOver(direction));
    }

    private IEnumerator FallOver(Vector3 direction)
    {
        if (fallen)
        {
            yield break;
        }
        fallen = true;
        Quaternion startRot = base.transform.rotation;
        Physics.Raycast(base.transform.position + direction + Vector3.up, Vector3.down, out RaycastHit floorHit, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
        Quaternion axisRot = Quaternion.identity * Quaternion.AngleAxis(-90f, Vector3.Cross(direction, Vector3.up));
        bool crossProductsMatch = (Vector3.Cross(base.transform.forward, direction).y > 0f) == (Vector3.Cross(direction, Vector3.forward).y > 0f);
        Quaternion endRot = Quaternion.Euler(crossProductsMatch ? 180f : 0f, axisRot.eulerAngles.y, axisRot.eulerAngles.z);
        Vector3 startPos = base.transform.position;
        Vector3 dropPos = floorHit.point;
        float dist = startPos.y - dropPos.y;
        float timeElapsed = 0f;
        float duration = 0.95f;
        float timeElapsedWhenFell = timeElapsed;
        bool soundFlag1 = false;
        bool soundFlag2 = false;
        bool dropFlag = false;
        bool firstCheckFlag = false;
        bool firstMoveFlag = false;
        while (timeElapsed < duration)
        {
            Physics.Raycast(base.transform.position + Vector3.up, Vector3.down, out RaycastHit currentHit, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            Physics.Raycast(base.transform.position + direction + Vector3.up, Vector3.down, out RaycastHit futureHit, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            Ray wallRay = new Ray(base.transform.position + Vector3.up, direction);
            if (Physics.Raycast(wallRay, out RaycastHit wallCheck, 1.1f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                Physics.Raycast(wallRay.GetPoint(wallCheck.distance * 0.5f) + Vector3.up, Vector3.down, out futureHit, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            }
            //TODO: Get correct dropPos on moving physicsregion objects (elevator going up/down)
            if (!firstCheckFlag)
            {
                firstCheckFlag = true;
                NetworkObject physicsRegionOfDroppedObject = null;
                Transform transform = futureHit.collider.gameObject.transform;
                if (transform != null)
                {
                    PlayerPhysicsRegion componentInChildren = transform.GetComponentInChildren<PlayerPhysicsRegion>();
                    if (componentInChildren != null && componentInChildren.allowDroppingItems && componentInChildren.itemDropCollider.ClosestPoint(futureHit.point) == futureHit.point)
                    {
                        NetworkObject parentNetworkObject = componentInChildren.parentNetworkObject;
                        if (parentNetworkObject != null)
                        {
                            physicsRegionOfDroppedObject = parentNetworkObject;
                        }
                    }
                }
                if (physicsRegionOfDroppedObject != null)
                {
                    base.transform.SetParent(physicsRegionOfDroppedObject.transform, worldPositionStays: true);
                    PlayerPhysicsRegion componentInChildren = physicsRegionOfDroppedObject.GetComponentInChildren<PlayerPhysicsRegion>();
                    if (componentInChildren != null && componentInChildren.allowDroppingItems)
                    {
                        base.transform.SetParent(componentInChildren.physicsTransform, worldPositionStays: true);
                    }
                }
            }
            //TODO: Get correct dropPos on moving physicsregion objects (elevator going up/down)
            if ((Mathf.Abs(base.transform.position.y - futureHit.point.y) > 0.25f) && !dropFlag)
            {
                dropFlag = true;
                dropPos = futureHit.point + itemProperties.verticalOffset * Vector3.up;
                startPos = base.transform.position;
                dist = startPos.y - dropPos.y;
                timeElapsedWhenFell = timeElapsed;
                if (moveCoroutine != null)
                {
                    StopCoroutine(moveCoroutine);
                    moveCoroutine = null;
                }
                ResetAnimationsAndSync(silent: false, clearMemory: false);
                bool droppedInElevator = false;
                bool droppedInPhysicsRegion = false;
                Vector3 hitPoint = dropPos;
                NetworkObject physicsRegionOfDroppedObject = null;
                Transform transform = futureHit.collider.gameObject.transform;
                if (transform != null)
                {
                    PlayerPhysicsRegion componentInChildren = transform.GetComponentInChildren<PlayerPhysicsRegion>();
                    if (componentInChildren != null && componentInChildren.allowDroppingItems && componentInChildren.itemDropCollider.ClosestPoint(futureHit.point) == futureHit.point)
                    {
                        NetworkObject parentNetworkObject = componentInChildren.parentNetworkObject;
                        if (parentNetworkObject != null)
                        {
                            Vector3 addPositionOffsetToItems = componentInChildren.addPositionOffsetToItems;
                            hitPoint = futureHit.point + Vector3.up * 0.04f + itemProperties.verticalOffset * Vector3.up + addPositionOffsetToItems;
                            physicsRegionOfDroppedObject = parentNetworkObject;
                        }
                    }
                }
                if (physicsRegionOfDroppedObject != null)
                {
                    droppedInPhysicsRegion = true;
                    dropPos = hitPoint;
                    base.transform.SetParent(physicsRegionOfDroppedObject.transform, worldPositionStays: true);
                    PlayerPhysicsRegion componentInChildren = physicsRegionOfDroppedObject.GetComponentInChildren<PlayerPhysicsRegion>();
                    if (componentInChildren != null && componentInChildren.allowDroppingItems)
                    {
                        base.transform.SetParent(componentInChildren.physicsTransform, worldPositionStays: true);
                    }
                }
                if (!droppedInElevator && !droppedInPhysicsRegion)
                {
                    dropPos = futureHit.point + itemProperties.verticalOffset * Vector3.up;
                }
            }
            if (timeElapsed > duration * 0.3f && !soundFlag1 && (!dropFlag || (Mathf.Abs(base.transform.position.y - currentHit.point.y) < 0.5f)))
            {
                soundFlag1 = true;
                bumpSource.clip = heavyBumpClips[Random.Range(0, heavyBumpClips.Length)];
                bumpSource.pitch = Random.Range(1f, 1.35f);
                bumpSource.Play();
                RoundManager.Instance.PlayAudibleNoise(base.transform.position);
            }
            if (timeElapsed > duration * 0.7f && !soundFlag2 && (!dropFlag || (Mathf.Abs(base.transform.position.y - currentHit.point.y) < 0.5f)))
            {
                soundFlag2 = true;
                bumpSource.clip = lightBumpClips[Random.Range(0, lightBumpClips.Length)];
                bumpSource.pitch = Random.Range(1f, 1.35f);
                bumpSource.Play();
                RoundManager.Instance.PlayAudibleNoise(base.transform.position);
            }
            if (!firstMoveFlag)
            {
                firstMoveFlag = true;
                MoveTowardsAndSync(base.transform.position + direction, silent: true, speedIndex: 0, timesBounced: 2, affectRotation: false);
            }
            timeElapsed += !dropFlag ? Time.deltaTime : (Time.deltaTime * 6f / Mathf.Clamp(dist, 4f, 16f));
            base.transform.rotation = Quaternion.Slerp(startRot, endRot, StartOfRound.Instance.objectFallToGroundCurve.Evaluate(timeElapsed / duration));
            if (dropFlag)
            {
                base.transform.position = Vector3.Lerp(startPos, dropPos, StartOfRound.Instance.objectFallToGroundCurve.Evaluate((timeElapsed - timeElapsedWhenFell - 0.2f) / duration));
            }
            yield return null;
        }
        base.transform.rotation = endRot;
        if (dropFlag)
        {
            base.transform.position = dropPos;
        }
    }

    private void ResetStatesAndSync()
    {
        ResetStates();
        ResetStatesServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetStatesServerRpc(int clientWhoSentRpc)
    {
        ResetStatesClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    private void ResetStatesClientRpc(int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            ResetStates();
        }
    }

    private void ResetStates()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        if (fallOverCoroutine != null)
        {
            StopCoroutine(fallOverCoroutine);
            fallOverCoroutine = null;
        }
        fallen = false;
    }

    private void ResetAnimationsAndSync(bool silent, bool clearMemory, bool heavy = false)
    {
        ResetAnimations(silent, clearMemory, heavy);
        ResetAnimationsServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, silent, clearMemory, heavy);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetAnimationsServerRpc(int clientWhoSentRpc, bool silent, bool clearMemory, bool heavy = false)
    {
        ResetAnimationsClientRpc(clientWhoSentRpc, silent, clearMemory, heavy);
    }

    [ClientRpc]
    private void ResetAnimationsClientRpc(int clientWhoSentRpc, bool silent, bool clearMemory, bool heavy = false)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            ResetAnimations(silent, clearMemory, heavy);
        }
    }

    private void ResetAnimations(bool silent, bool clearMemory, bool heavy = false)
    {
        if (!silent)
        {
            itemAnimator.SetTrigger("Bump");
            bumpSource.clip = heavy ? heavyBumpClips[Random.Range(0, heavyBumpClips.Length)] : lightBumpClips[Random.Range(0, lightBumpClips.Length)];
            bumpSource.pitch = Random.Range(0.85f, 1.2f);
            bumpSource.Play();
            RoundManager.Instance.PlayAudibleNoise(base.transform.position);
        }
        if (clearMemory)
        {
            collidersHitByRadiator.Clear();
        }
        wheelSource.Stop();
        itemAnimator.SetLayerWeight(1, 0f);
        if (debugPos != null)
        {
            Destroy(debugPos);
            debugPos = null;
        }
    }

    private void BumpInto(Collider other, int speedIndex)
    {
        if (collidersHitByRadiator.Contains(other))
        {
            return;
        }
        else
        {
            collidersHitByRadiator.Add(other);
        }
        bool bumped = false;
        if (other.gameObject.TryGetComponent<Vase>(out var vase))
        {
            bumped = true;
            vase.WobbleAndSync(speedIndex == 0 ? 1 : 0);
        }
        else if (other.gameObject.TryGetComponent<WhoopieCushionItem>(out var whoopie))
        {
            whoopie.Fart();
        }
        else if (other.gameObject.TryGetComponent<Rake>(out var rake))
        {
            bumped = true;
            speedIndex = 0;
            rake.FlipAndSync();
            StartCoroutine(LeaveRake(rake));
            collidersHitByRadiator.Clear();
            collidersHitByRadiator.Add(other);
        }
        else if (other.gameObject.TryGetComponent<Radiator>(out var otherRadiator) && otherRadiator != this)
        {
            bumped = true;
            Vector3 direction = Vector3.Normalize(new Vector3(base.transform.position.x, other.transform.position.y, base.transform.position.z) - other.transform.position);
            Vector3 pos = other.transform.position - (direction * crouchDistance);
            otherRadiator.MoveTowardsAndSync(pos, silent: false, 2);
        }
        else if (other.gameObject.TryGetComponent<Turret>(out var turret))
        {
            bumped = true;
            turret.GetComponent<IHittable>().Hit(1, Vector3.zero);
        }
        if (bumped)
        {
            Vector3 direction = Vector3.Normalize(new Vector3(other.transform.position.x, base.transform.position.y, other.transform.position.z) - base.transform.position);
            Vector3 pos = base.transform.position - (direction * crouchDistance);
            MoveTowardsAndSync(pos, silent: false, speedIndex);
        }
    }

    private IEnumerator LeaveRake(Rake rake)
    {
        yield return new WaitForSeconds(0.5f);
        rake.FallAndSync();
    }

    public Transform ClosestPlayerInside()
    {
		float maxRange = 200f;
		int closestPlayer = 0;
        for (int i = 0; i < playersInside.Count; i++)
        {
            float sqrMagnitude = (playersInside[i].transform.position - base.transform.position).sqrMagnitude;
            if (sqrMagnitude < maxRange)
            {
                maxRange = sqrMagnitude;
                closestPlayer = i;
            }
        }
        return playersInside[closestPlayer].transform;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
    {
        if (!fallen && !isHeld && !isHeldByEnemy && hasHitGround && !StartOfRound.Instance.inShipPhase && StartOfRound.Instance.timeSinceRoundStarted > 2f && playerWhoHit != null)
        {
            moveTimer = 0f;
            Vector3 pos = base.transform.position + (new Vector3(hitDirection.x, 0f, hitDirection.z) * sprintDistance);
            MoveTowardsAndSync(pos, silent: false, speedIndex: 0, clientWhoPushed: playerWhoHit.playerClientId);
        }
        return false;
    }
}