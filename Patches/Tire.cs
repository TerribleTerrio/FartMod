using System;
using GameNetcodeStuff;
using UnityEngine;

public class Tire : AnimatedItem, IHittable, ITouchable
{
    [Space(15f)]
    [Header("Tire Settings")]
    public GameObject rollingTirePrefab;

    private GameObject rollingTire;

    public GameObject physicsTirePrefab;

    private GameObject physicsTire;

    public int currentBehaviourStateIndex = 0;

    public int previousBehaviourStateIndex;

    public float tireRadius;

    public float physicsForce = 5;

    private float baseWeight;

    private bool boost;

    private PlayerControllerB previousPlayerHeldBy;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource tireSource;

    public AudioClip[] switchToRollingSFX;

    public AudioClip[] switchToHoldingSFX;

    public AudioClip[] rollingSFX;

    public AudioClip[] hitSFX;

    public override void Start()
    {
        base.Start();

        physicsTirePrefab.gameObject.GetComponentInChildren<Rigidbody>().maxAngularVelocity = 20f;
    }

    public override void Update()
    {
        base.Update();

        if (playerHeldBy != null && previousPlayerHeldBy != playerHeldBy)
        {
            previousPlayerHeldBy = playerHeldBy;
        }

        switch (currentBehaviourStateIndex)
        {

        //ITEM TIRE
        case 0:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                if (playerHeldBy != null)
                {
                    playerHeldBy.carryWeight = Mathf.Clamp(baseWeight + (itemProperties.weight - 1f), 1f, 10f);

                    if (previousBehaviourStateIndex == 1)
                    {
                        parentObject = playerHeldBy.localItemHolder;
                    }
                }
                else
                {
                    previousPlayerHeldBy.carryWeight = baseWeight;
                }

                Debug.Log("Tire entered holding state.");
                EnableItemTire(meshEnabled: true, grabEnabled: true);
                EnableRollingTire(enabled: false);
                EnablePhysicsTire(enabled: false);

                if (radarIcon != null)
                {
                    radarIcon.position = base.transform.position;
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            break;

        //ROLLING TIRE
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("Tire entered rolling state.");
                EnableItemTire(meshEnabled: false, grabEnabled: false);
                EnableRollingTire(enabled: true);
                parentObject = rollingTire.transform;

                playerHeldBy.carryWeight = baseWeight;

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            if (playerHeldBy == null)
            {
                currentBehaviourStateIndex = 0;
                break;
            }

            //GET GROUND INFO
            RaycastHit rollGroundInfo;
            Physics.Raycast(rollingTire.transform.position, Vector3.down, out rollGroundInfo, 1f, 268438273, QueryTriggerInteraction.Ignore);

            //SET POSITION OF ROLLING TIRE
            Vector3 rollingPosition = playerHeldBy.transform.position + playerHeldBy.transform.forward * 1.2f;
            rollingPosition.y = rollGroundInfo.point.y + tireRadius;
            rollingTire.transform.position = rollingPosition;

            //SET PLAYER SPEED BASED ON GROUND ANGLE USING WEIGHT
            float groundAngle = Vector3.Angle(playerHeldBy.transform.forward, rollGroundInfo.normal) - 90f;
            float groundAngleBackward = Vector3.Angle(playerHeldBy.transform.forward * -1, rollGroundInfo.normal) - 90f;
            float slopeWeight;
            if (playerHeldBy.moveInputVector.y >= 0)
            {
                slopeWeight = Remap(groundAngle, -15f, 15f, -2f, 2f);
            }
            else
            {
                slopeWeight = Remap(groundAngleBackward, -15f, 15f, -2f, 2f);
            }
            playerHeldBy.carryWeight = Mathf.Clamp(baseWeight + slopeWeight, 1f, 10f);

            //SET ROLL SPEED BASED ON MOVEMENT SPEED
            float rollSpeed = playerHeldBy.walkForce.magnitude / playerHeldBy.carryWeight * 0.85f;
            if (!playerHeldBy.movingForward)
            {
                rollSpeed *= -1f;
            }
            if (playerHeldBy.isSprinting)
            {
                rollSpeed *= 1.25f;
            }
            rollingTire.GetComponent<Animator>().SetFloat("RollSpeed", rollSpeed);

            //LET GO OF TIRE IF PULLING UP HILL
            if (groundAngle < -20f && playerHeldBy.moveInputVector.y < 0)
            {
                currentBehaviourStateIndex = 2;
                break;
            }

            //CHECK IF EXTERNAL FORCE IS ENOUGH TO LET GO OF TIRE
            if ((playerHeldBy.externalForces + playerHeldBy.externalForceAutoFade).magnitude > 10f)
            {
                currentBehaviourStateIndex = 2;
                break;
            }

            if (radarIcon != null)
            {
                radarIcon.position = rollingTire.transform.position;
            }

            //INSTANCES WHERE THE TIRE ROLLS AWAY (jumping, no floor, dropping)
            if (playerHeldBy.isJumping || playerHeldBy.isCrouching || !Physics.Raycast(rollingTire.transform.position, Vector3.down, 1f, 268438273, QueryTriggerInteraction.Ignore) || playerHeldBy.isPlayerDead || groundAngle < -30f && playerHeldBy.moveInputVector.y < 0)
            {
                currentBehaviourStateIndex = 2;
                break;
            }

            break;

        //PHYSICS TIRE
        case 2:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("Tire entered physics state.");

                if (playerHeldBy != null)
                {
                    playerHeldBy.DiscardHeldObject();
                }

                previousPlayerHeldBy.carryWeight = baseWeight;

                previousBehaviourStateIndex = currentBehaviourStateIndex;

                EnableItemTire(meshEnabled: false, grabEnabled: true);
                EnableRollingTire(enabled: false);
                EnablePhysicsTire(enabled: true);
                parentObject = physicsTire.GetComponentInChildren<Collider>().gameObject.transform;

                //GIVE TIRE MOVE SPEED OF PLAYER
                Rigidbody rigidbody = physicsTire.GetComponentInChildren<Rigidbody>();
                Rigidbody playerRigidbody = previousPlayerHeldBy.playerRigidbody;
                rigidbody.AddForce(playerRigidbody.velocity * 2f, ForceMode.Impulse);

                //GIVE TIRE SLIGHT BOOST FORWARD
                if (boost)
                {
                    Vector3 forward = previousPlayerHeldBy.transform.forward;
                    Vector3 boostForce = forward;
                    if (previousPlayerHeldBy.isSprinting && previousPlayerHeldBy.movingForward)
                    {
                        boostForce = forward * 12;
                    }
                    else if (previousPlayerHeldBy.isWalking && previousPlayerHeldBy.movingForward)
                    {
                        boostForce = forward * 6;
                    }
                    boostForce += Vector3.up * 1;

                    rigidbody.AddForce(boostForce, ForceMode.Impulse);
                }
            }

            break;
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();

        switch (currentBehaviourStateIndex)
        {

        //ITEM TIRE
        case 0:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                
            }
            break;
            
        //ROLLING TIRE
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {

            }
            break;

        //PHYSICS TIRE
        case 2:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {

            }
            break;

        }
    }

    public void EnableItemTire(bool meshEnabled, bool grabEnabled)
    {
        if (grabEnabled)
        {
            grabbable = true;
            grabbableToEnemies = true;
        }
        else
        {
            grabbable = false;
            grabbableToEnemies = false;
        }

        if (meshEnabled)
        {
            EnableItemMeshes(enable: true);
        }
        else
        {
            EnableItemMeshes(enable: false);
        }
    }

    public void EnableRollingTire(bool enabled)
    {
        if (enabled && rollingTire == null)
        {
            rollingTire = Instantiate(rollingTirePrefab, playerHeldBy.transform);
        }
        else if (!enabled && rollingTire != null)
        {
            Destroy(rollingTire);
        }
    }

    public void EnablePhysicsTire(bool enabled)
    {
        if (enabled && physicsTire == null)
        {
            physicsTire = Instantiate(physicsTirePrefab);
            physicsTire.transform.position = rollingTire.transform.position;
            physicsTire.transform.eulerAngles = rollingTire.gameObject.transform.GetChild(0).transform.rotation.eulerAngles + new Vector3(90f, 0f, 90f);
            Rigidbody rigidbody = physicsTire.GetComponentInChildren<Rigidbody>();
            rigidbody.maxAngularVelocity = 20f;
            rigidbody.maxLinearVelocity = 20f;
        }
        else if (!enabled && physicsTire != null)
        {
            Destroy(physicsTire);
        }
    }

    public void EnableClampLook(bool enabled)
    {
        if (enabled)
        {
            playerHeldBy.clampLooking = true;
            playerHeldBy.minVerticalClamp = 25f;
            playerHeldBy.maxVerticalClamp = -60f;
            playerHeldBy.horizontalClamp = 60f;
        }
        else
        {
            playerHeldBy.clampLooking = false;
        }
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (currentBehaviourStateIndex == 0)
        {
            Debug.Log("Tire activated in state 0.");
            currentBehaviourStateIndex = 1;
            return;
        }
        if (currentBehaviourStateIndex == 1)
        {
            Debug.Log("Tire activated in state 1.");
            boost = true;
            currentBehaviourStateIndex = 2;
            return;
        }
    }

    public override void GrabItem()
    {
        baseWeight = Mathf.Clamp(playerHeldBy.carryWeight - (itemProperties.weight - 1f), 1f, 10f);

        if (currentBehaviourStateIndex == 0)
        {
            base.GrabItem();
        }
        if (currentBehaviourStateIndex == 2)
        {
            base.GrabItem();
            currentBehaviourStateIndex = 0;
        }
    }

    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;
        Rigidbody rigidbody = physicsTire.GetComponentInChildren<Rigidbody>();
        PlayerControllerB player = null;

        if (otherObject.GetComponent<PlayerControllerB>() != null)
        {
            player = otherObject.GetComponent<PlayerControllerB>();
        }

        if (currentBehaviourStateIndex == 0)
        {
            return;
        }

        if (currentBehaviourStateIndex == 1)
        {
            if (player != null)
            {    
                if (!Physics.Linecast(base.transform.position, player.transform.position, 256, QueryTriggerInteraction.Ignore))
                {
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
        }

        if (currentBehaviourStateIndex == 2)
        {
            if (player != null)
            {    
                if (!Physics.Linecast(base.transform.position, player.transform.position, 256, QueryTriggerInteraction.Ignore))
                {
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

                    if (rigidbody.velocity.magnitude > 5f)
                    {
                        Vector3 hitVector = Vector3.Normalize(player.transform.position - rigidbody.transform.position);
                        player.playerRigidbody.AddForce(hitVector * 5f, ForceMode.Impulse);
                        player.DamagePlayer(2, hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning, 0, fallDamage:false);
                    }
                }
            }

            if (otherObject.GetComponent<EnemyAICollisionDetect>() != null)
            {
                EnemyAI enemy = otherObject.GetComponentInParent<EnemyAI>();

                //DEAL DAMAGE BASED ON SPEED OF PHYSICS TIRE
            }
        }
    }

    public void OnExit(Collider other)
    {
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
        RoundManager.PlayRandomClip(itemAudio, hitSFX, randomize: true, 1f, -1);
        return true;
	}

    public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }
}