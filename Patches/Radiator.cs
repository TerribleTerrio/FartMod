using UnityEngine;
using GameNetcodeStuff;
using System.Collections;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;

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

    private List<PlayerControllerB> playersInside;

    private List<Collider> CollidersHitByRadiator = new List<Collider>();

    private Coroutine moveCoroutine;

    private Coroutine fallOverCoroutine;

    private float lastActionTime;

    private float updateTimer;

    private GameObject debugPos;

    private bool haunted;

    private float moveTimer;

    private float moveCooldown = 0.3f;

    private float wallRadius = 1f;

    private float rayHeightMultiplier = 0.4f;

    public override void Start()
    {
        path = new NavMeshPath();
        lastActionTime = Time.realtimeSinceStartup;
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
        ResetAnimations(silent: true, clearMemory: true);
        base.Start();
        if (!base.IsOwner || isHeld)
        {
            return;
        }
        if (NavMesh.SamplePosition(base.transform.position, out NavMeshHit hit, 4f, -1))
        {
            base.transform.position = hit.position;
            if (NavMesh.CalculatePath(hit.position, RoundManager.Instance.GetClosestNode(base.transform.position, outside: base.transform.position.y > -80f).position, -1, path))
            {
                RoamMovement(hit.position, RoundManager.Instance.GetClosestNode(base.transform.position, outside: base.transform.position.y > -80f).position);
                lastActionTime = Time.realtimeSinceStartup;
            }
        }
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
        ResetAnimations(silent: true, clearMemory: true);
        base.EquipItem();
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        lastActionTime = Time.realtimeSinceStartup;
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
        ResetAnimations(silent: true, clearMemory: true);
        base.GrabItemFromEnemy(enemy);
    }

    public override void DiscardItem()
    {
        lastActionTime = Time.realtimeSinceStartup;
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
        ResetAnimations(silent: true, clearMemory: true);
        base.DiscardItem();
    }

    public void OnTouch(Collider other)
    {
        if (isHeld || isHeldByEnemy || !hasHitGround || moveTimer > 0f || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f || base.transform.parent?.GetComponentInChildren<PlayerPhysicsRegion>() != null)
        {
            return;
        }
        if (other.gameObject.layer != 3 && other.gameObject.layer != 6 && other.gameObject.layer != 19)
        {
            return;
        }
        int chosenSpeed = 1;
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
                if (enemy.mainScript.TryGetComponent<MouthDogAI>(out var dog))
                {
                    if (dog.inLunge || dog.hasEnteredChaseModeFully)
                    {
                        chosenSpeed = 0;
                    }
                }
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
        else if (other.gameObject.layer == 3)
        {
            if (!other.TryGetComponent<PlayerControllerB>(out var player))
            {
                return;
            }
            else
            {
                if (player.isJumping || player.isFallingFromJump || player.isFallingNoJump)
                {
                    return;
                }
                ResetAnimations(silent: true, clearMemory: true);
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
                    vector = new(vector.x, 0, vector.z);
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
                    ResetAnimations(silent: false, clearMemory: true);
                    // FallOverAndSync(Vector3.Normalize(new Vector3(other.transform.position.x, base.transform.position.y, other.transform.position.z) - base.transform.position));
                    return;
                }
            }
        }
        float chosenDistance = chosenSpeed switch {0 => sprintDistance, 1 => walkDistance, _ => crouchDistance};
        Vector3 direction = Vector3.Normalize(new Vector3(other.transform.position.x, base.transform.position.y, other.transform.position.z) - base.transform.position);
        Vector3 pos = base.transform.position - (direction * chosenDistance);
        MoveTowardsAndSync(pos, silent: false, chosenSpeed);
    }

    public void OnExit(Collider other)
    {
    }

    private void IntervalUpdate()
    {
        if (isHeld || isHeldByEnemy || !hasHitGround || base.isInShipRoom || !base.isInFactory || !base.IsOwner || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f || base.transform.parent?.GetComponentInChildren<PlayerPhysicsRegion>() != null)
        {
            return;
        }
        playersInside = [.. StartOfRound.Instance.allPlayerScripts.Where(player => player.isInsideFactory)];
        if (playersInside.Count <= 0)
        {
            return;
        }
        if (!haunted)
        {
            for (int i = 0; i < RoundManager.Instance.SpawnedEnemies.Count; i++)
            {
                if (RoundManager.Instance.SpawnedEnemies[i].enemyType.enemyName == "Girl")
                {
                    haunted = true;
                }
            }
        }
        bool losFlag = false;
        bool movable = false;
        for (int i = 0; i < playersInside.Count; i++)
        {
            if (playersInside[i].HasLineOfSightToPosition(base.transform.position + Vector3.up * rayHeightMultiplier) && !haunted)
            {
                losFlag = true;
            }
        }
        if (losFlag)
        {
            lastActionTime = Time.realtimeSinceStartup;
        }
        if (NavMesh.SamplePosition(base.transform.position, out var hit, 2f, -1) && !losFlag && (Time.realtimeSinceStartup - lastActionTime > lastSeenDuration))
        {
            movable = true;
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
                if (Physics.Raycast(currentPosition + Vector3.up * rayHeightMultiplier, RoundManager.Instance.tempTransform.forward, out var hitInfo, maxWallDistance, CoronaMod.Masks.RadiatorMask, QueryTriggerInteraction.Ignore))
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
                Vector3 checkFromPosition = currentPosition + Vector3.up * rayHeightMultiplier;
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

    public void MoveTowardsAndSync(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false, int timesBounced = 0)
    {
        MoveTowards(position, silent, speedIndex, roaming, timesBounced);
        MoveTowardsServerRpc(position, silent, speedIndex, (int)GameNetworkManager.Instance.localPlayerController.playerClientId, roaming, timesBounced);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveTowardsServerRpc(Vector3 position, bool silent, int speedIndex = -1, int clientWhoSentRpc = -1, bool roaming = false, int timesBounced = 0)
    {
        MoveTowardsClientRpc(position, silent, speedIndex, clientWhoSentRpc, roaming, timesBounced);
    }

    [ClientRpc]
    public void MoveTowardsClientRpc(Vector3 position, bool silent, int speedIndex = -1, int clientWhoSentRpc = -1, bool roaming = false, int timesBounced = 0)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            MoveTowards(position, silent, speedIndex, roaming, timesBounced);
        }
    }

    public void MoveTowards(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false, int timesBounced = 0)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        moveCoroutine = StartCoroutine(DecelerateTowards(position, silent, speedIndex, roaming, timesBounced));
    }

    public IEnumerator DecelerateTowards(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false, int timesBounced = 0)
    {
        if (!grabbable || isHeld || isHeldByEnemy || moveTimer > 0f || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            ResetAnimations(silent: true, clearMemory: true);
            yield break;
        }
        bool heavy = speedIndex switch {0 => true, _ => false};
        ResetAnimations(silent, clearMemory: false, heavy: heavy);
        moveTimer = moveCooldown;
        lastActionTime = Time.realtimeSinceStartup;
        float timeBumped = Time.realtimeSinceStartup;
        Vector3 startPosition = base.transform.position;
        float chosenSpeed;
        float chosenDistance;
        if (speedIndex == -1 && roaming)
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
        Debug.Log($"[RADIATOR]: Starting move! Times bounced: {timesBounced}");
		Ray ray = new(base.transform.position + Vector3.up * rayHeightMultiplier, direction);
		Vector3 wallPos = (!Physics.Raycast(ray, out RaycastHit wallHitInfo, noWallDistance, CoronaMod.Masks.RadiatorMask)) ? ray.GetPoint(noWallDistance) : ray.GetPoint(Vector3.Distance(ray.origin, wallHitInfo.point));
        Vector3 vertPos = Physics.Raycast(position + Vector3.up * rayHeightMultiplier, Vector3.down, out RaycastHit vertHitInfo, 10f, CoronaMod.Masks.RadiatorMask, QueryTriggerInteraction.Ignore) ? vertHitInfo.point + itemProperties.verticalOffset * Vector3.up : position + itemProperties.verticalOffset * Vector3.up;
        Vector3 endPosition = new(wallPos.x, vertPos.y, wallPos.z);
        if (Vector3.Distance(base.transform.position, new(endPosition.x, base.transform.position.y, endPosition.z)) < 0.25f)
        {
            Debug.Log($"[RADIATOR]: Insignificant movement, ignored! Distance: {Vector3.Distance(base.transform.position, new(endPosition.x, base.transform.position.y, endPosition.z))}");
            yield break;
        }
        float startY = base.transform.eulerAngles.y;
        float endY = Random.Range(-45f, 45f) + startY;
        Vector3 endEuler = new(base.transform.eulerAngles.x, endY, base.transform.eulerAngles.z);
        wheelSource.clip = heavy ? heavyWheelClips[Random.Range(0, heavyWheelClips.Length)] : lightWheelClips[Random.Range(0, lightWheelClips.Length)];
        wheelSource.pitch = Random.Range(0.9f, 1.15f);
        wheelSource.Play();
        while (Vector3.Distance(base.transform.position, new(endPosition.x, base.transform.position.y, endPosition.z)) > 0.05f)
        {
            //CHECK FUTURE POSITION FOR CLIFFS OR WALLS
            Vector3 futurePosition = base.transform.position;
            futurePosition = Vector3.Lerp(futurePosition, endPosition, chosenSpeed * Time.deltaTime);
            Vector3 futureLocalPosition = base.transform.localPosition;
            if (Physics.Raycast(futurePosition + Vector3.up * rayHeightMultiplier, Vector3.down, out RaycastHit futureHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                Vector3 futureTargetFloorPosition = futureHitInfo.point + itemProperties.verticalOffset * Vector3.up;
                if (base.transform.parent != null)
                {
                    futureTargetFloorPosition = base.transform.parent.InverseTransformPoint(futureTargetFloorPosition);
                }
                if (Vector3.Distance(futurePosition, futureHitInfo.point) > 0.5f || Vector3.Distance(futureLocalPosition, futureTargetFloorPosition) > 0.5f || Vector3.Angle(base.transform.up, futureHitInfo.normal) > 45f)
                {
                    Debug.Log($"[RADIATOR]: Elevation change at future position, can't move! Distance 1: {Vector3.Distance(futurePosition, futureHitInfo.point)}, Distance 2: {Vector3.Distance(futureLocalPosition, futureTargetFloorPosition)}, Angle: {Vector3.Angle(base.transform.up, futureHitInfo.normal)}");
                    ResetAnimations(silent: false, clearMemory: true);
                    // FallOverAndSync(direction);
                    yield break;
                }
            }
            else
            {
                Debug.Log($"[RADIATOR]: Couldn't find a floor at future position, can't move!");
                ResetAnimations(silent: false, clearMemory: true);
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
                    if (Physics.Raycast(futurePosition + Vector3.up * rayHeightMultiplier, RoundManager.Instance.tempTransform.forward, out RaycastHit collideInfo, maxWallDistance, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
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
                    Vector3 newDirection = Vector3.Reflect(new(direction.x, 0f, direction.z), new(wallNormal.x, 0f, wallNormal.z));
                    Vector3 newPos = base.transform.position + (newDirection * chosenDistance);
                    moveTimer = 0f;
                    MoveTowardsAndSync(newPos, silent: false, speedIndex: speedIndex, timesBounced: timesBounced + 1);
                    yield break;
                }
            }

            yield return null;

            //BUMP INTO INTERACTIVE OBJECTS
            if (Time.realtimeSinceStartup - timeBumped > moveCooldown)
            {
                RaycastHit[] results = Physics.SphereCastAll(base.transform.position + Vector3.up, 0.75f, Vector3.down, 0.75f, CoronaMod.Masks.PropsMapHazards);
                for (int i = 0; i < results.Count(); i++)
                {
                    if (CollidersHitByRadiator.Contains(results[i].collider))
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

            //MOVE FORWARD
            base.transform.position = Vector3.Lerp(base.transform.position, endPosition, chosenSpeed * Time.deltaTime);
            base.transform.position = new(base.transform.position.x, futureHitInfo.point.y + itemProperties.verticalOffset, base.transform.position.z);
            if (Physics.Raycast(base.transform.position, Vector3.down, out RaycastHit hitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                Vector3 newTargetFloorPosition = hitInfo.point + itemProperties.verticalOffset * Vector3.up;
                if (base.transform.parent != null)
                {
                    newTargetFloorPosition = base.transform.parent.InverseTransformPoint(newTargetFloorPosition);
                }
                targetFloorPosition = newTargetFloorPosition;
                base.transform.localPosition = targetFloorPosition;
            }
            Physics.Raycast(base.transform.position + base.transform.forward * 0.4f, Vector3.down, out RaycastHit frontHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            Physics.Raycast(base.transform.position - base.transform.forward * 0.4f, Vector3.down, out RaycastHit backHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            Vector3 avgUp = frontHitInfo.normal + backHitInfo.normal;
            base.transform.rotation = Quaternion.Euler(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, endEuler.y, chosenSpeed * Time.deltaTime * 0.5f), base.transform.eulerAngles.z);
            base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.FromToRotation(Vector3.up, avgUp), chosenSpeed * Time.deltaTime * 2f);
            itemAnimator.SetLayerWeight(1, Mathf.Clamp(Vector3.Distance(base.transform.position, new(endPosition.x, base.transform.position.y, endPosition.z)), 0f, 1f));
        }
        Debug.Log($"[RADIATOR]: Movement finished!");
        ResetAnimations(silent: true, clearMemory: true);
    }

    private void ResetAnimations(bool silent, bool clearMemory, bool heavy = false)
    {
        if (!silent)
        {
            itemAnimator.SetTrigger("Bump");
            bumpSource.clip = heavy ? heavyBumpClips[Random.Range(0, heavyBumpClips.Length)] : lightBumpClips[Random.Range(0, lightBumpClips.Length)];
            bumpSource.pitch = Random.Range(0.9f, 1.15f);
            bumpSource.Play();
            RoundManager.Instance.PlayAudibleNoise(base.transform.position);
        }
        if (clearMemory)
        {
            CollidersHitByRadiator.Clear();
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
        if (CollidersHitByRadiator.Contains(other))
        {
            return;
        }
        else
        {
            CollidersHitByRadiator.Add(other);
        }
        bool bumped = false;
        if (other.gameObject.TryGetComponent<Vase>(out var vase))
        {
            bumped = true;
            vase.WobbleAndSync(speedIndex switch {0 => 1, _ => 0});
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
            MoveTowardsAndSync(pos, silent: false, 1);
        }
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
        if (!isHeld || !isHeldByEnemy || hasHitGround || !StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted > 2f)
        {
            if (playerWhoHit != null)
            {
                Vector3 direction = Vector3.Normalize(new Vector3(playerWhoHit.transform.position.x, base.transform.position.y, playerWhoHit.transform.position.z) - base.transform.position);
                Vector3 pos = base.transform.position - (direction * sprintDistance);
                MoveTowardsAndSync(pos, silent: false, 0);
            }
            else
            {
                Vector3 pos = base.transform.position - (hitDirection * sprintDistance);
                MoveTowardsAndSync(pos, silent: false, 0);
            }
        }
        return false;
    }
}