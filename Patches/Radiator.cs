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

    private float lastActionTime;

    private float updateTimer;

    private GameObject testPos;

    private bool haunted;

    private float moveTimer;

    private float moveCooldown = 0.25f;

    public override void Start()
    {
        path = new NavMeshPath();
        lastActionTime = Time.realtimeSinceStartup;
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        ResetMovement(silent: true, clearMemory: true);
        base.Start();
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
        ResetMovement(silent: true, clearMemory: true);
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
        ResetMovement(silent: true, clearMemory: true);
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
        ResetMovement(silent: true, clearMemory: true);
        base.DiscardItem();
    }

    public void OnTouch(Collider other)
    {
        if (isHeld || isHeldByEnemy || !hasHitGround || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f || base.transform.parent.GetComponentInChildren<PlayerPhysicsRegion>() != null)
        {
            return;
        }
        int chosenSpeed = 1;
        float chosenDistance = walkDistance;
        if (other.gameObject.layer == 3 || other.gameObject.layer == 6 || other.gameObject.layer == 19)
        {
            if (other.TryGetComponent<EnemyAICollisionDetect>(out var enemy))
            {
                if (enemy.mainScript.TryGetComponent<HoarderBugAI>(out _))
                {
                    return;
                }
                if (enemy.mainScript.TryGetComponent<BaboonBirdAI>(out _))
                {
                    return;
                }
            }
            if (other.TryGetComponent<GrabbableObject>(out var gObject) && !Physics.Linecast(other.gameObject.transform.position + Vector3.up * 0.5f, base.transform.position + Vector3.up, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
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
                        chosenDistance = sprintDistance;
                        ball.BeginKickBall(base.transform.position + Vector3.up, hitByEnemy: false);
                    }
                }
                else
                {
                    return;
                }
            }
            if (other.TryGetComponent<PlayerControllerB>(out var player))
            {
                if (player.isJumping || player.isFallingFromJump || player.isFallingNoJump)
                {
                    return;
                }
                ResetMovement(silent: true, clearMemory: true);
                if (player.isSprinting)
                {
                    chosenSpeed = 0;
                    chosenDistance = sprintDistance;
                }
                else if (player.isCrouching)
                {
                    chosenSpeed = 2;
                    chosenDistance = crouchDistance;
                }
                if (!Physics.Linecast(base.transform.position, player.transform.position, out var _, 256, QueryTriggerInteraction.Ignore))
                {
                    float physicsForce = chosenSpeed switch
                    {
                        0 => 2f,
                        1 => 1.3f,
                        _ => 1.2f
                    };
                    float dist = Vector3.Distance(player.transform.position, base.transform.position);
                    Vector3 vector = Vector3.Normalize(player.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
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
            }
            Vector3 direction = Vector3.Normalize(new Vector3(other.transform.position.x, base.transform.position.y, other.transform.position.z) - base.transform.position);
            Vector3 pos = base.transform.position - (direction * chosenDistance);
            MoveTowardsAndSync(pos, silent: false, chosenSpeed);
        }
    }

    public void OnExit(Collider other)
    {
    }

    private void IntervalUpdate()
    {
        if (isHeld || isHeldByEnemy || !hasHitGround || base.isInShipRoom || !base.isInFactory || !base.IsOwner || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f || base.transform.parent.GetComponentInChildren<PlayerPhysicsRegion>() != null)
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
            if (playersInside[i].HasLineOfSightToPosition(base.transform.position + Vector3.up) && !haunted)
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
                Debug.Log("[RADIATOR]: Moving on my own!");
                RoamMovement(hit.position);
                lastActionTime = Time.realtimeSinceStartup;
            }
            else
            {
                Debug.Log("[RADIATOR]: I can't move on my own!");
                lastActionTime = Time.realtimeSinceStartup;
            }
        }
    }

    public void RoamMovement(Vector3 pathStart)
    {
        if (NavMesh.CalculatePath(pathStart, ClosestPlayerInside().position, -1, path))
        {
            float maxRange = 200f;
            int corner = 0;
            for (int i = 0; i < path.corners.Length; i++)
            {
                float sqrMagnitude = (path.corners[i] - base.transform.position).sqrMagnitude;
                if (sqrMagnitude < maxRange)
                {
                    maxRange = sqrMagnitude;
                    corner = i + 1;
                }
            }
            Vector3 newPosition = path.corners[corner];
            Vector3 currentPosition = base.transform.position;
            float maxWallDistance = 1.3f;
            float wallDistance = maxWallDistance;
            float yRotation = -1;
            for (int i = 0; i < 360; i += 360/6)
            {
                RoundManager.Instance.tempTransform.eulerAngles = new Vector3(0f, i, 0f);
                if (Physics.Raycast(currentPosition + Vector3.up * 2f, RoundManager.Instance.tempTransform.forward, out var hitInfo, maxWallDistance, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
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
                RoundManager.Instance.tempTransform.eulerAngles = new Vector3(0f, yRotation, 0f);
                currentPosition = path.corners[corner] - RoundManager.Instance.tempTransform.forward * (maxWallDistance - wallDistance);
                Vector3 checkFromPosition = currentPosition + Vector3.up * 2f;
                if (Physics.Raycast(checkFromPosition, Vector3.down, out var hitInfo, 8f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    newPosition = RoundManager.Instance.GetNavMeshPosition(hitInfo.point);
                }
                else
                {
                    newPosition = path.corners[corner];
                }
            }
            MoveTowardsAndSync(newPosition, silent: true, roaming: true);
        }
    }

    public void MoveTowardsAndSync(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false)
    {
        MoveTowards(position, silent, speedIndex, roaming);
        MoveTowardsServerRpc(position, silent, speedIndex, (int)GameNetworkManager.Instance.localPlayerController.playerClientId, roaming);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveTowardsServerRpc(Vector3 position, bool silent, int speedIndex = -1, int clientWhoSentRpc = -1, bool roaming = false)
    {
        MoveTowardsClientRpc(position, silent, speedIndex, clientWhoSentRpc, roaming);
    }

    [ClientRpc]
    public void MoveTowardsClientRpc(Vector3 position, bool silent, int speedIndex = -1, int clientWhoSentRpc = -1, bool roaming = false)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            MoveTowards(position, silent, speedIndex, roaming);
        }
    }

    public void MoveTowards(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        moveCoroutine = StartCoroutine(DecelerateTowards(position, silent, speedIndex, roaming));
    }

    public IEnumerator DecelerateTowards(Vector3 position, bool silent, int speedIndex = -1, bool roaming = false)
    {
        if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f || moveTimer > 0f)
        {
            yield break;
        }
        ResetMovement(silent, clearMemory: false);
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
        Vector3 direction = Vector3.Normalize(position - startPosition);
        float noWallDistance = Mathf.Clamp(Vector3.Distance(startPosition, position), 0f, chosenDistance);
        Debug.Log($"[RADIATOR]: Starting move!");
		Ray ray = new(base.transform.position + Vector3.up * 0.4f, direction);
		Vector3 wallPos = (!Physics.Raycast(ray, out RaycastHit rayHit, noWallDistance, CoronaMod.Masks.RadiatorMask)) ? ray.GetPoint(noWallDistance) : ray.GetPoint(Vector3.Distance(ray.origin, rayHit.point) - 0.8f);
        Vector3 vertPos = Physics.Raycast(position, Vector3.down, out var vertHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore) ? vertHitInfo.point + itemProperties.verticalOffset * Vector3.up : position;
        Vector3 endPosition = new(wallPos.x, vertPos.y, wallPos.z);
        float finalDistance = (!Physics.Raycast(ray, out _, noWallDistance, CoronaMod.Masks.RadiatorMask)) ? noWallDistance : Vector3.Distance(ray.origin, rayHit.point) - 0.8f;
        if (finalDistance < 1f && !roaming)
        {
            Debug.Log($"[RADIATOR]: Too close to a wall to begin with, can't move!");
            yield break;
        }
        float startY = base.transform.eulerAngles.y;
        float endY = Random.Range(-65f, 65f) + startY;
        Vector3 endEuler = new(base.transform.eulerAngles.x, endY, base.transform.eulerAngles.z);
        while (Vector3.Distance(base.transform.position, new(endPosition.x, base.transform.position.y, endPosition.z)) > 0.1f)
        {
            //CHECK FUTURE POSITION FOR CLIFFS OR WALLS
            Vector3 futurePosition = base.transform.position;
            futurePosition = Vector3.Lerp(futurePosition, endPosition, chosenSpeed * Time.deltaTime);
            Vector3 futureLocalPosition = base.transform.localPosition;
            if (Physics.Raycast(futurePosition, Vector3.down, out var futureHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                Vector3 futureTargetFloorPosition = futureHitInfo.point + itemProperties.verticalOffset * Vector3.up;
                if (base.transform.parent != null)
                {
                    futureTargetFloorPosition = base.transform.parent.InverseTransformPoint(futureTargetFloorPosition);
                }
                if (Vector3.Distance(futureLocalPosition, futureTargetFloorPosition) > 0.5f || Vector3.Angle(Vector3.up, futureHitInfo.normal) > 45f)
                {
                    Debug.Log($"[RADIATOR]: Elevation change, can't move!");
                    ResetMovement(silent: false, clearMemory: true);
                    yield break;
                }
            }
            if (!roaming)
            {
                float maxWallDistance = 0.8f;
                float wallDistance = maxWallDistance;
                float yRotation = -1;
                for (int i = 0; i < 360; i += 360/6)
                {
                    RoundManager.Instance.tempTransform.eulerAngles = new Vector3(0f, i, 0f);
                    if (Physics.Raycast(futurePosition + Vector3.up * 2f, RoundManager.Instance.tempTransform.forward, out var collideInfo, maxWallDistance, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                    {
                        if (collideInfo.distance < wallDistance)
                        {
                            wallDistance = collideInfo.distance;
                            yRotation = i;
                        }
                    }
                }
                if (yRotation != -1)
                {
                    Debug.Log($"[RADIATOR]: Hit a wall, can't move!");
                    ResetMovement(silent: true, clearMemory: true);
                    yield break;
                }
            }
            yield return null;
            //BUMP INTO THINGS
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
            if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                Vector3 newTargetFloorPosition = hitInfo.point + itemProperties.verticalOffset * Vector3.up;
                if (base.transform.parent != null)
                {
                    newTargetFloorPosition = base.transform.parent.InverseTransformPoint(newTargetFloorPosition);
                }
                targetFloorPosition = newTargetFloorPosition;
                base.transform.localPosition = targetFloorPosition;
            }
            base.transform.rotation = Quaternion.Euler(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, endEuler.y, chosenSpeed * Time.deltaTime), base.transform.eulerAngles.z);
            itemAnimator.SetLayerWeight(1, Mathf.Clamp(Vector3.Distance(base.transform.position, new(endPosition.x, base.transform.position.y, endPosition.z)), 0f, 1f));
        }
        Debug.Log($"[RADIATOR]: Movement finished!");
        ResetMovement(silent: true, clearMemory: true);
    }

    private void ResetMovement(bool silent, bool clearMemory)
    {
        if (!silent)
        {
            itemAnimator.SetTrigger("Bump");
        }
        if (clearMemory)
        {
            CollidersHitByRadiator.Clear();
        }
        itemAnimator.SetLayerWeight(1, 0f);
        if (testPos != null)
        {
            Destroy(testPos);
            testPos = null;
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
            vase.WobbleAndSync(speedIndex switch {0 => 2, 1 => 1, _ => 0});
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
                Vector3 pos = base.transform.position - (direction * walkDistance);
                MoveTowardsAndSync(pos, silent: false, 1);
            }
            else
            {
                Vector3 pos = base.transform.position - (hitDirection * walkDistance);
                MoveTowardsAndSync(pos, silent: false, 1);
            }
        }
        return false;
    }
}