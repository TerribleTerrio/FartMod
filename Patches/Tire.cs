using System;
using GameNetcodeStuff;
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

    private float currentWeight;

    private float fallHeightPeak;

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

    public override void Start()
    {
        base.Start();
        tireObjectCollider = tireObject.GetComponent<Collider>();
        physicsTirePrefab.transform.localScale = base.originalScale;
        currentWeight = itemProperties.weight;
    }

    public override void Update()
    {
        base.Update();

        if (heldByPlayerOnServer && previousPlayerHeldBy != playerHeldBy)
        {
            previousPlayerHeldBy = playerHeldBy;
        }

        if (playerPushCollisionTimer > 0)
        {
            playerPushCollisionTimer -= Time.deltaTime;
        }

        switch (currentBehaviourStateIndex)
        {

        //ITEM TIRE
        case 0:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[TIRE]: Entered held item state.");

                tireRollAudio.Pause();

                //CHECK IF COMING FROM ROLLING STATE
                if (previousBehaviourStateIndex == 1)
                {
                    itemAudio.PlayOneShot(switchToHoldingClip);

                    if (heldByPlayerOnServer)
                    {
                        //RE-ADD WEIGHT
                        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (itemProperties.weight - 1f), 1f, 10f);
                    }
                }

                //CHECK IF COMING FROM PHYSICS TIRE STATE
                if (previousBehaviourStateIndex == 2)
                {
                    //CHECK THAT PHYSICS TIRE EXISTS AND NOT HELD BY PLAYER
                    if (physicsTire != null && !heldByPlayerOnServer)
                    {
                        //SET DROP POSITION BASED ON PHYSICS TIRE
                        startFallingPosition = base.transform.position;
                        if (base.transform.parent != null)
                        {
                            startFallingPosition = base.transform.parent.InverseTransformPoint(startFallingPosition);
                        }
                        // FallToGround();

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
                    }
                }

                //SET OR CLEAR PARENT OBJECT
                if (heldByPlayerOnServer)
                {
                    if (IsOwner)
                    {
                        //SET TOOLTIPS
                        itemProperties.toolTips = itemToolTips;
                        SetControlTipsForItem();
                        parentObject = playerHeldBy.localItemHolder;
                    }
                    else
                    {
                        parentObject = playerHeldBy.serverItemHolder;
                    }
                }
                else
                {
                    parentObject = null;
                }

                EnableTireObjectMeshes(true);
                SpawnPhysicsTire(false);

                //SET TIRE MESH TO GRABBABLEOBJECT
                tireObject.transform.position = transform.position;
                tireObject.transform.rotation = transform.rotation;
                tireObject.transform.SetParent(transform);

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            break;

        //ROLLING TIRE
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[TIRE]: Entered rolling state.");

                itemAudio.PlayOneShot(switchToRollingCip);

                //SET TIRE MESH IN FRONT OF PLAYER
                tireObject.transform.position = playerHeldBy.transform.position + playerHeldBy.transform.forward * tireRadius + Vector3.up * tireRadius;
                tireObject.transform.rotation = playerHeldBy.transform.rotation;
                tireObject.transform.SetParent(playerHeldBy.transform);
                tireObject.transform.localEulerAngles = new Vector3(0f,90f,0f);

                //SET TOOLTIPS
                itemProperties.toolTips = rollToolTips;
                SetControlTipsForItem();

                //SET WEIGHT
                playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (itemProperties.weight - 1f), 1f, 10f);

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //CATCH IF NOT HELD BY PLAYER
            if (!heldByPlayerOnServer)
            {
                Debug.Log("[TIRE]: Tire not held by player, returning to item state.");
                currentBehaviourStateIndex = 0;
                break;
            }

            //PUSH BACK IF PUSHED AGAINST WALL
            if (Physics.Raycast(tireObject.transform.position, playerHeldBy.transform.forward, tireRadius, 268438273, QueryTriggerInteraction.Ignore))
            {
                playerHeldBy.externalForceAutoFade += -playerHeldBy.transform.forward * 0.1f;
            }

            float steepness;

            //GET GROUND INFO
            RaycastHit groundInfo;
            if (Physics.Raycast(tireObject.transform.position, -Vector3.up, out groundInfo, 1.5f, 268438273, QueryTriggerInteraction.Ignore))
            {
                //SET Y POSITION OF TIRE
                tireObject.transform.position = groundInfo.point + Vector3.up * tireRadius;

                //FIND STEEPNESS OF SURFACE UNDER TIRE
                Vector3 compareVector = -playerHeldBy.transform.forward;
                if (!playerHeldBy.movingForward)
                {
                    compareVector *= -1;
                }
                steepness = 90 - Vector3.Angle(groundInfo.normal, compareVector);
                // Debug.Log($"[TIRE]: Steepness of surface: {steepness}");

                //SLOW DOWN OR SPEED UP BASED ON STEEPNESS
                if (steepness > 0)
                {
                    playerHeldBy.externalForceAutoFade += Vector3.Normalize(playerHeldBy.walkForce) * Remap(steepness, 0f, 30f, 0f, -0.085f);
                }
                else if (steepness < 0)
                {
                    playerHeldBy.externalForceAutoFade += Vector3.Normalize(playerHeldBy.walkForce) * Remap(steepness, 0f, -30f, 0f, 0.03f);
                }
            }
            else
            {
                if (IsInCollider())
                {
                    Debug.Log("[TIRE]: Released while in collider!");
                    tireObject.transform.position = playerHeldBy.transform.position;
                }
                currentBehaviourStateIndex = 2;
                break;
            }

            //ROTATE TIRE WITH WALK
            if (playerHeldBy.isWalking)
            {
                float rotateSpeed = 2.2f;
                if (playerHeldBy.isSprinting)
                {
                    rotateSpeed = 2.9f;
                }
                if (!playerHeldBy.movingForward)
                {
                    rotateSpeed *= -1;
                }
                tireObject.transform.eulerAngles += new Vector3(0,0,rotateSpeed);
            }

            Vector2 playerMove = playerHeldBy.playerActions.FindAction("Move").ReadValue<Vector2>();

            //PLAY ROLLING SOUND IF MOVING
            if (playerMove.magnitude > 0 && !tireRollAudio.isPlaying)
            {
                tireRollAudio.Play();
            }

            if (playerMove.magnitude < 0 && tireRollAudio.isPlaying)
            {
                tireRollAudio.Pause();
            }

            //LOWER MOUSE X SENSITIVITY
            
            
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

            //INSTANCES WHERE TIRE ROLLS AWAY
            if (playerHeldBy.isJumping || playerHeldBy.isCrouching || playerHeldBy.isPlayerDead || (playerHeldBy.externalForces + playerHeldBy.externalForceAutoFade).magnitude > 10f)
            {
                if (IsInCollider())
                {
                    Debug.Log("[TIRE]: Released while in collider!");
                    tireObject.transform.position = playerHeldBy.transform.position;
                }
                currentBehaviourStateIndex = 2;
                break;
            }

            break;

        //PHYSICS TIRE
        case 2:

            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[TIRE]: Entered physics state.");

                playerPushCollisionTimer = playerPushCollisionCooldown;

                fallHeightPeak = physicsTire.transform.position.y;

                //DISABLE TIRE ITEM MESH
                EnableTireObjectMeshes(false);

                //SPAWN PHYSICS TIRE
                SpawnPhysicsTire(true);
                tireRigidbody = physicsTire.GetComponent<Rigidbody>();

                if (heldByPlayerOnServer)
                {
                    Rigidbody playerRigidbody = playerHeldBy.playerRigidbody;

                    //GIVE TIRE MOVE SPEED OF PLAYER
                    tireRigidbody.AddForce(playerRigidbody.velocity * 2f, ForceMode.Impulse);

                    //GIVE EXTRA BOOST
                    if (boost)
                    {
                        Vector3 force = previousPlayerHeldBy.transform.forward * boostForce;

                        if (previousPlayerHeldBy.movingForward)
                        {
                            if (previousPlayerHeldBy.isSprinting)
                            {
                                itemAudio.PlayOneShot(sprintPush);
                                force *= 2f;
                            }
                            else
                            {
                                itemAudio.PlayOneShot(walkPush);
                            }
                        }
                        else
                        {
                            itemAudio.PlayOneShot(push);
                            force /= 2f;
                        }

                        force += Vector3.up;
                        tireRigidbody.AddForce(force, ForceMode.Impulse);
                    }

                    boost = false;

                    playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (itemProperties.weight - 1f), 1f, 10f);
                    playerHeldBy.DiscardHeldObject();
                }

                //SET PARENT OBJECT OF ITEM TO PHYSICS TIRE
                parentObject = physicsTire.transform;

                //SET TIRE MESH TO GRABBABLEOBJECT
                tireObject.transform.position = transform.position;
                tireObject.transform.rotation = transform.rotation;
                tireObject.transform.SetParent(transform);

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //CHECK IF TOUCHING GROUND
            if (Physics.Raycast(physicsTire.transform.position, -Vector3.up, out groundInfo, tireRadius*1.1f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                //PLAY ROLLING SOUND IF MOVING ON GROUND
                if (tireRigidbody.velocity.magnitude > 0.2f && !tireRollAudio.isPlaying)
                {
                    tireRollAudio.Play();
                }

                else if (tireRigidbody.velocity.magnitude < 0.2f && tireRollAudio.isPlaying)
                {
                    tireRollAudio.Pause();
                }

                float tireFallHeight = fallHeightPeak - groundInfo.point.y;

                //BOUNCE IF FALLEN FAR ENOUGH
                // if (tireFallHeight > 2f)
                // {
                //     Debug.Log($"[TIRE]: Fell from height of {tireFallHeight}, bouncing!");
                //     BounceOff(groundInfo.point);
                // }
            }

            else
            {
                if (fallHeightPeak < physicsTire.transform.position.y)
                {
                    fallHeightPeak = physicsTire.transform.position.y;
                    Debug.Log($"[TIRE]: Fall height peak set to {fallHeightPeak}.");
                }
            }

            //CHECK IF TIRE HAS FALLEN OVER
            if ((Vector3.Angle(physicsTire.transform.forward, Vector3.up) < 30f || Vector3.Angle(-physicsTire.transform.forward, Vector3.up) < 30f) && physicsTire.GetComponent<Rigidbody>().velocity.magnitude < 1f)
            {
                if (Vector3.Angle(-physicsTire.transform.forward, Vector3.up) < 30f)
                {
                    itemProperties.restingRotation = new Vector3(90f,0f,0f);
                }
                else
                {
                    itemProperties.restingRotation = new Vector3(-90f,0f,0f);
                }
                tireRollAudio.Pause();
                currentBehaviourStateIndex = 0;
                break;
            }

            break;
        }
    }



    //USING ITEM
    public override void ItemActivate(bool used, bool buttonDown = true)
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
                currentBehaviourStateIndex = 1;
                return;
            }
            else if (currentBehaviourStateIndex == 1)
            {
                boost = true;
                currentBehaviourStateIndex = 2;
                return;
            }
        }
    }

    public override void ItemInteractLeftRight(bool right)
    {
        base.ItemInteractLeftRight(right);

        if (currentBehaviourStateIndex == 1)
        {
            if (!right)
            {
                currentBehaviourStateIndex = 0;
            }
        }
    }

    public override void GrabItem()
    {
        base.GrabItem();
        base.playerHeldBy.equippedUsableItemQE = true;

        if (currentBehaviourStateIndex == 0)
        {
            
        }

        if (currentBehaviourStateIndex == 2)
        {
            currentBehaviourStateIndex = 0;
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

    private void SpawnPhysicsTire(bool enable)
    {
        if (enable && physicsTire == null)
        {
            Quaternion tireRotation = Quaternion.Euler(tireObject.transform.eulerAngles);
            physicsTire = Instantiate(physicsTirePrefab, tireObject.transform.position, tireRotation);
            Rigidbody rigidbody = physicsTire.GetComponent<Rigidbody>();
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
                Rigidbody rigidbody = physicsTire.GetComponent<Rigidbody>();
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

                if (player == previousPlayerHeldBy && playerPushCollisionTimer > 0)
                {
                    Debug.Log("[TIRE]: Collided with previous player during cooldown.");
                    return;
                }

                float speed = rigidbody.velocity.magnitude;
                float dirDifference = Mathf.Clamp((Vector3.Normalize(rigidbody.velocity) - Vector3.Normalize(player.walkForce)).magnitude, 0f, 1f);
                int damage = Mathf.RoundToInt(Mathf.Clamp(speed - 5, 0f, 100f) * 5f * dirDifference);

                Debug.Log($"[TIRE]: Collided with player! Damage set to {damage}!");

                Vector3 pushDirection = Vector3.Normalize(player.gameplayCamera.transform.position - transform.position);
                Vector3 pushForce = pushDirection * speed * 2f;
                player.externalForceAutoFade += pushForce;

                if (damage >= 1)
                {
                    player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Bludgeoning, force: pushForce);
                }

                BounceOff(player.transform.position, forceMultiplier: dirDifference);
                return;
            }

            //ENEMY COLLISION
            else if (otherObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
            {
                float speed = physicsTire.GetComponent<Rigidbody>().velocity.magnitude;
                EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();

                if (enemy.mainScript as FlowermanAI != null)
                {
                    FlowermanAI flowerman = enemy.mainScript as FlowermanAI;
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
                    BounceOff(enemy.mainScript.transform.position);
                    return;
                }

                else if (enemy.mainScript as BaboonBirdAI != null)
                {
                    BounceOff(enemy.mainScript.transform.position);
                    return;
                }
            }

            //ITEM COLLISION
            else if (otherObject.layer == 6 && otherObject.GetComponent<GrabbableObject>() != null)
            {
                GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();

                switch (gObject)
                {
                    case SoccerBallProp ball:
                        if (!ball.hasHitGround && !ball.isHeld && !ball.isHeldByEnemy)
                        {
                            BounceOff(ball.transform.position, forceMultiplier: 0.5f);
                            return;
                        }
                        return;
                    
                    case HydraulicStabilizer hydraulic:
                        BounceOff(hydraulic.transform.position, forceMultiplier: 0.2f);
                        return;
                }
            }

            //TIRE COLLISION
            else if (otherObject.layer == 3 && otherObject.name.Contains("PhysicsTire"))
            {
                if (otherObject != physicsTire)
                {
                    BounceOff(otherObject.transform.position);
                }
            }
        }
    }

    public void OnExit(Collider other)
    {

    }

    public void BounceOff(Vector3 otherPos, float forceMultiplier = 1f, float extraForce = 0, bool bounceUp = true)
    {
        Rigidbody rigidbody = physicsTire.GetComponent<Rigidbody>();

        Vector3 direction = Vector3.Normalize(physicsTire.transform.position - otherPos);
        Vector3 directionForce = direction * (Mathf.Clamp(rigidbody.velocity.magnitude * 1.5f, 2f, 20f) + extraForce);
        Vector3 upForce = new Vector3(0,0,0);

        if (bounceUp)
        {
            upForce = Vector3.up * Mathf.Clamp(rigidbody.velocity.magnitude * 0.75f, 1f, 10f);
        }

        Vector3 bounceForce = (directionForce + upForce) * forceMultiplier * 7f;
        rigidbody.AddForce(bounceForce, ForceMode.Impulse);

        if (bounceForce.magnitude > 35f)
        {
            tireBumpAudio.PlayOneShot(tireHitSFX[UnityEngine.Random.Range(0,tireHitSFX.Length)]);
            PlayAudibleNoise(noiseRange, noiseLoudness);
        }
        else
        {
            tireBumpAudio.PlayOneShot(tireBumpSFX[UnityEngine.Random.Range(0,tireHitSFX.Length)]);
            PlayAudibleNoise(noiseRange, noiseLoudness);
        }

        Debug.Log($"[TIRE]: Bounced with force {bounceForce}.");
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
        return true;
	}



    private float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }
}