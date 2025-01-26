using UnityEngine;
using GameNetcodeStuff;
using System.Collections;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;

public class Radiator : GrabbableObject, ITouchable
{
    [Space(15f)]
    [Header("Radiator Settings")]
    public NavMeshAgent agent;

    [HideInInspector]
    public NavMeshPath path;

    public float crouchSpeed = 1f;

    public float walkSpeed = 3f;

    public float sprintSpeed = 5f;

    public float updateInterval = 0.25f;

    public float lastSeenDuration = 17f;

    private List<PlayerControllerB> playersInside;

    private Coroutine moveCoroutine;

    private float lastActionTime;

    private float updateTimer;

    public override void Start()
    {
        path = new NavMeshPath();
        lastActionTime = Time.realtimeSinceStartup;
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
            RadiatorUpdate();
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
        base.EquipItem();
    }

    public override void DiscardItem()
    {
        lastActionTime = Time.realtimeSinceStartup;
        base.DiscardItem();
    }

    public void OnTouch(Collider other)
    {
        if (isHeld || isHeldByEnemy || !hasHitGround)
        {
            return;
        }
        if (other.gameObject.layer == 3 || other.gameObject.layer == 19)
        {
            int chosenSpeed = 1;
            float chosenDistance = 3.5f;
            if (other.TryGetComponent<PlayerControllerB>(out var player))
            {
                if (player.isSprinting)
                {
                    chosenSpeed = 0;
                    chosenDistance = 5f;
                }
                else if (player.isCrouching)
                {
                    chosenSpeed = 2;
                    chosenDistance = 1.5f;
                }
                else
                {
                    chosenSpeed = 1;
                    chosenDistance = 3.5f;
                }

                if (!Physics.Linecast(base.transform.position, player.transform.position, out var _, 256, QueryTriggerInteraction.Ignore))
                {
                    float physicsForce = 1.5f;
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
            Vector3 direction = Vector3.Normalize(other.transform.position - base.transform.position);
            MoveTowardsAndSync(direction * chosenDistance, chosenSpeed);
        }
    }

    public void OnExit(Collider other)
    {
    }

    public void RadiatorUpdate()
    {
        if (isHeld || isHeldByEnemy || !hasHitGround || base.isInShipRoom || !base.isInFactory || !base.IsOwner)
        {
            return;
        }
        playersInside = [.. StartOfRound.Instance.allPlayerScripts.Where(player => player.isInsideFactory)];
        bool losFlag = false;
        bool aloneFlag = false;
        bool movable = false;
        for (int i = 0; i < playersInside.Count; i++)
        {
            if (playersInside[i].HasLineOfSightToPosition(base.transform.position + Vector3.up * 0.5f))
            {
                losFlag = true;
            }
        }
        if (losFlag)
        {
            lastActionTime = Time.realtimeSinceStartup;
        }
        else if (Vector3.Distance(ClosestPlayerInside().position, base.transform.position) > 30f)
        {
            aloneFlag = true;
        }
        float finalDuration = aloneFlag ? lastSeenDuration / 2f : lastSeenDuration;
        if (NavMesh.SamplePosition(base.transform.position, out var _, 1f, -1) && !losFlag && (Time.realtimeSinceStartup - lastActionTime > finalDuration))
        {
            movable = true;
        }
        else
        {
            movable = false;
        }
        if (movable)
        {
            if (NavMesh.CalculatePath(base.transform.position, ClosestPlayerInside().position, agent.areaMask, path) && path.status != NavMeshPathStatus.PathPartial)
            {
                Debug.Log("[RADIATOR]: Moving on my own!");
                MoveTowardsAndSync(path.m_Corners[0]);
            }
        }
    }

    public void MoveTowardsAndSync(Vector3 position, int speedIndex = -1)
    {
        MoveTowards(position, speedIndex);
        MoveTowardsServerRpc(position, speedIndex, (int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveTowardsServerRpc(Vector3 position, int speedIndex = -1, int clientWhoSentRpc = -1)
    {
        MoveTowardsClientRpc(position, speedIndex, clientWhoSentRpc);
    }

    [ClientRpc]
    public void MoveTowardsClientRpc(Vector3 position, int speedIndex = -1, int clientWhoSentRpc = -1)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            MoveTowards(position, speedIndex);
        }
    }

    public void MoveTowards(Vector3 position, int speedIndex = -1)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        moveCoroutine = StartCoroutine(DecelerateTowards(position, speedIndex));
    }

    public IEnumerator DecelerateTowards(Vector3 position, int speedIndex = -1)
    {
        lastActionTime = Time.realtimeSinceStartup;
        Vector3 startPosition = base.transform.position;
        Vector3 direction = Vector3.Normalize(position - startPosition);
        float distance = (startPosition - position).sqrMagnitude;
		Ray ray = new(transform.position + Vector3.up * 2f, direction);
		Vector3 pos = (!Physics.Raycast(ray, out RaycastHit rayHit, distance, StartOfRound.Instance.collidersAndRoomMask)) ? ray.GetPoint(distance) : rayHit.point;
        position = new(pos.x, position.y, pos.z);
        Debug.Log($"[RADIATOR]: Moving towards {position}! Distance: {(startPosition - position).sqrMagnitude}");
        float chosenSpeed;
        if (speedIndex == -1)
        {
            chosenSpeed = (startPosition - position).sqrMagnitude switch
            {
                > 8f => sprintSpeed,
                > 5f => walkSpeed,
                _ => crouchSpeed,
            };
        }
        else
        {
            chosenSpeed = speedIndex switch
            {
                0 => sprintSpeed,
                1 => walkSpeed,
                _ => crouchSpeed
            };
        }
        while ((startPosition - position).sqrMagnitude > 0.1f)
        {
            base.transform.position = Vector3.Lerp(base.transform.position, position, chosenSpeed * Time.deltaTime);
            if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                targetFloorPosition = hitInfo.point + itemProperties.verticalOffset * Vector3.up;
                if (base.transform.parent != null)
                {
                    targetFloorPosition = base.transform.parent.InverseTransformPoint(targetFloorPosition);
                }
                base.transform.position = new(base.transform.position.x, targetFloorPosition.y, base.transform.position.z);
            }
            yield return null;
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
}