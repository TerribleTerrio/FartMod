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

    public override void Start()
    {
        path = new NavMeshPath();
        lastActionTime = Time.realtimeSinceStartup;
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        StopMoving(silent: true, clearMemory: true);
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
        StopMoving(silent: true, clearMemory: true);
        base.EquipItem();
    }

    public override void DiscardItem()
    {
        lastActionTime = Time.realtimeSinceStartup;
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        StopMoving(silent: true, clearMemory: true);
        base.DiscardItem();
    }

    public void OnTouch(Collider other)
    {
        if (isHeld || isHeldByEnemy || !hasHitGround || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            return;
        }
        int chosenSpeed = 1;
        float chosenDistance = walkDistance;
        if (other.gameObject.layer == 3 || other.gameObject.layer == 6 || other.gameObject.layer == 19)
        {
            if (other.gameObject.TryGetComponent<GrabbableObject>(out var gObject) && !Physics.Linecast(other.gameObject.transform.position + Vector3.up * 0.5f, base.transform.position + Vector3.up, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
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
                StopMoving(silent: true, clearMemory: true);
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
                        1 => 1.25f,
                        _ => 1f
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
        if (isHeld || isHeldByEnemy || !hasHitGround || base.isInShipRoom || !base.isInFactory || !base.IsOwner || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            return;
        }
        playersInside = [.. StartOfRound.Instance.allPlayerScripts.Where(player => player.isInsideFactory)];
        if (playersInside.Count <= 0)
        {
            return;
        }
        bool losFlag = false;
        bool movable = false;
        for (int i = 0; i < playersInside.Count; i++)
        {
            if (playersInside[i].HasLineOfSightToPosition(base.transform.position + Vector3.up))
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
                MoveTowardsAndSync(path.corners[1], silent: true, roaming: true);
                path.ClearCorners();
            }
            else
            {
                Debug.Log("[RADIATOR]: I can't move on my own!");
                lastActionTime = Time.realtimeSinceStartup;
            }
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
        if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            yield break;
        }
        StopMoving(silent, clearMemory: false);
        float timeBumped = Time.realtimeSinceStartup;
        lastActionTime = Time.realtimeSinceStartup;
        Vector3 startPosition = base.transform.position;
        float chosenSpeed;
        if (speedIndex == -1 && roaming)
        {
            chosenSpeed = Vector3.Distance(base.transform.position, ClosestPlayerInside().position) switch {> 16f => sprintSpeed, > 10f => walkSpeed, _ => crouchSpeed};
        }
        else if (speedIndex == -1)
        {
            chosenSpeed = Vector3.Distance(base.transform.position, position) switch {> 8f => sprintSpeed, > 4f => walkSpeed, _ => crouchSpeed};
        }
        else
        {
            chosenSpeed = speedIndex switch {0 => sprintSpeed, 1 => walkSpeed, _ => crouchSpeed};
        }
        Vector3 direction = Vector3.Normalize(position - startPosition);
        float noWallDistance = Mathf.Clamp(Vector3.Distance(startPosition, position), 0f, chosenSpeed);
		Ray ray = new(base.transform.position + Vector3.up * 0.4f, direction);
		Vector3 wallPos = (!Physics.Raycast(ray, out RaycastHit rayHit, noWallDistance, CoronaMod.Masks.RadiatorMask)) ? ray.GetPoint(noWallDistance) : ray.GetPoint(Vector3.Distance(ray.origin, rayHit.point) - 0.8f);
        Vector3 vertPos = Physics.Raycast(position, Vector3.down, out var vertHitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore) ? vertHitInfo.point + itemProperties.verticalOffset * Vector3.up : position;
        Vector3 endPosition = new(wallPos.x, vertPos.y, wallPos.z);
        float finalDistance = (!Physics.Raycast(ray, out _, noWallDistance, CoronaMod.Masks.RadiatorMask)) ? noWallDistance : Vector3.Distance(ray.origin, rayHit.point) - 0.8f;
        if (finalDistance < 1.2f && !roaming)
        {
            yield break;
        }
        float startY = base.transform.eulerAngles.y;
        float endY = Random.Range(-45f, 45f) + startY;
        Vector3 endEuler = new(base.transform.eulerAngles.x, endY, base.transform.eulerAngles.z);
        Debug.Log($"[RADIATOR]: Moving towards {endPosition}, starting distance: {finalDistance}");
        while (Vector3.Distance(base.transform.position, new(endPosition.x, base.transform.position.y, endPosition.z)) > 0.1f)
        {
            //CHECK FUTURE POSITION
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
                    StopMoving(silent: false, clearMemory: true);
                    yield break;
                }
            }
            yield return null;
            //BUMP INTO THINGS
            if (Time.realtimeSinceStartup - timeBumped > 0.2f)
            {
                RaycastHit[] results = Physics.SphereCastAll(base.transform.position + Vector3.up, 0.85f, Vector3.down, 0.85f, CoronaMod.Masks.PropsMapHazards);
                for (int i = 0; i < results.Count(); i++)
                {
                    if (CollidersHitByRadiator.Contains(results[i].collider))
                    {
                        continue;
                    }
                    else
                    {
                        BumpInto(results[i].collider);
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
        StopMoving(silent: true, clearMemory: true);
    }

    private void StopMoving(bool silent, bool clearMemory)
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

    private void BumpInto(Collider other)
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
            vase.WobbleAndSync(1);
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
            float sqrMagnitude = (playersInside[i].transform.position - agent.transform.position).sqrMagnitude;
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
                Vector3 pos = base.transform.position - (direction * crouchDistance);
                MoveTowardsAndSync(pos, silent: false, 1);
            }
            else
            {
                Vector3 pos = base.transform.position - (hitDirection * crouchDistance);
                MoveTowardsAndSync(pos, silent: false, 1);
            }
        }
        return false;
    }
}