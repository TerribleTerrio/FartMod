using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Vase : AnimatedItem, IHittable, ITouchable
{
    [Space(15f)]
    [Header("Vase Settings")]
    public float breakHeight;

    public float safePlaceTime;

    public float physicsForce;

    private bool safePlaced;

    private bool hasBeenSeen;

    private bool shattered;

    [Space(10f)]
    [Header("Break When...")]
    public bool breakOnHit = true;

    public bool breakOnDrop = true;

    public bool breakOnDeath = true;

    public bool breakOnBlast = true;

    public bool breakInShip = true;

    public bool breakInOrbit = false;

    [Space(10f)]
    [Header("Shatter Prefabs")]
    public GameObject walkShatterPrefab;

    public GameObject sprintShatterPrefab;

    public GameObject explodeShatterPrefab;

    private GameObject shatterPrefab;

    public override void Start()
    {
        base.Start();
    }

    public override void Update()
    {
        base.Update();

        if (!hasBeenSeen)
        {
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(transform.position))
                {
                    hasBeenSeen = true;
                    Debug.Log($"[VASE]: Vase seen by {StartOfRound.Instance.allPlayerScripts[i]}.");
                }
            }
        }
    }

    public override void GrabItem()
    {
        base.GrabItem();
        Debug.Log("[VASE]: Vase grabbed.");

        CancelWobble();
        CancelWobbleServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    public override void PlayDropSFX()
    {
        Debug.Log($"[VASE]: Drop SFX found in item properties: {itemProperties.dropSFX}");
        AudioClip[] dropSFX = new AudioClip[1];
        dropSFX[0] = itemProperties.dropSFX;
        RoundManager.Instance.PlayAudibleNoise(this.transform.position, noiseRange/4, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, dropSFX, randomize: true, 1f, -1);

        hasHitGround = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CancelWobbleServerRpc(int clientWhoSentRpc)
    {
        CancelWobbleClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    public void CancelWobbleClientRpc(int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            CancelWobble();
        }
    }

    public void CancelWobble()
    {
        safePlaced = true;
        itemAnimator.SetTrigger("Stop");
    }

    public override void DiscardItem()
    {
        if (playerHeldBy.isPlayerDead && breakOnDeath)
        {
            ExplodeAndSync();
        }

        base.DiscardItem();
    }

    public override void FallWithCurve()
    {
        float num = startFallingPosition.y - targetFloorPosition.y;
        if (floorYRot == -1)
		{
			base.transform.rotation = Quaternion.Lerp(base.transform.rotation, Quaternion.Euler(itemProperties.restingRotation.x, base.transform.eulerAngles.y, itemProperties.restingRotation.z), Mathf.Clamp(30f * Time.deltaTime / num, 0f, 1f));
		}
		else
		{
			base.transform.rotation = Quaternion.Lerp(base.transform.rotation, Quaternion.Euler(itemProperties.restingRotation.x, (float)(floorYRot + itemProperties.floorYOffset) + 90f, itemProperties.restingRotation.z), Mathf.Clamp(30f * Time.deltaTime / num, 0f, 1f));
		}

        base.transform.localPosition = Vector3.Lerp(startFallingPosition, targetFloorPosition, StartOfRound.Instance.objectFallToGroundCurveNoBounce.Evaluate(fallTime));

        fallTime += Mathf.Abs(Time.deltaTime * 6f / num);
    }

    public override void OnHitGround()
    {
        base.OnHitGround();
        float fallHeight = startFallingPosition.y - targetFloorPosition.y;
		Debug.Log($"[VASE]: Vase dropped: {fallHeight}");

        if (fallHeight > breakHeight && breakOnDrop)
        {
            ExplodeAndSync();
        }
        else
        {
            Debug.Log("[VASE]: Vase placed safely, beginning cooldown time.");
            safePlaced = true;
            StartCoroutine(placeSafely(safePlaceTime));
        }
    }

    public IEnumerator placeSafely(float time)
    {
        yield return new WaitForSeconds(time);
        safePlaced = false;
    }

    public void Wobble(int type)
    {
        if (!CanWobble())
        {
            return;
        }

        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;
        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange/2, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

        switch (type)
        {
        case 0:
            itemAnimator.SetTrigger("WobbleCrouch");
            break;
        case 1:
            itemAnimator.SetTrigger("WobbleWalk");
            shatterPrefab = walkShatterPrefab;
            break;
        case 2:
            itemAnimator.SetTrigger("WobbleSprint");
            shatterPrefab = sprintShatterPrefab;
            break;
        }

        Debug.Log("[VASE]: Vase started wobbling.");
    }

    [ServerRpc(RequireOwnership = false)]
    public void WobbleServerRpc(int clientWhoSentRpc, int type)
    {
        WobbleWithTypeClientRpc(clientWhoSentRpc, type);
    }

    [ClientRpc]
    public void WobbleWithTypeClientRpc(int clientWhoSentRpc, int type)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            Wobble(type);
        }
    }

    public void ExplodeAndSync()
    {
        Shatter(explode: true);
        ShatterServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, explode: true);
    }

    public void ShatterAndSync()
    {
        Shatter();
        ShatterServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }


    public void WobbleAndSync(int type)
    {
        Wobble(type);
        WobbleServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, type);
    }

    public void SetGrabbable(bool setto)
    {
        grabbable = setto;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ShatterServerRpc(int clientWhoSentRpc, bool explode = false)
    {
        ShatterClientRpc(clientWhoSentRpc, explode);
		StartCoroutine(DespawnAfterFrame());
    }

	public IEnumerator DespawnAfterFrame()
	{
		yield return new WaitForEndOfFrame();
        if (base.gameObject.GetComponent<NetworkObject>().IsSpawned)
        {
            Debug.Log("[VASE]: Despawning!");
            base.gameObject.GetComponent<NetworkObject>().Despawn();
        }
	}

    [ClientRpc]
    public void ShatterClientRpc(int clientWhoSentRpc, bool explode = false)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            Shatter(explode);
        }
    }

    public void Shatter(bool explode = false)
    {
        Debug.Log("[VASE]: Vase shattered!");
        if (shattered)
        {
            return;
        }
        if (heldByPlayerOnServer)
        {
            Debug.Log("[VASE]: Discarding held object from player!");
            playerHeldBy.DiscardHeldObject();
        }
        else if (isHeldByEnemy)
        {
            Debug.Log("[VASE]: Discarding held object from enemy!");
            base.DiscardItemFromEnemy();
        }

        //SET FLAGS
        shattered = true;
        grabbable = false;
        grabbableToEnemies = false;
        scrapValue = 0;
        itemProperties.creditsWorth = 0;
        EnablePhysics(enable: false);
        if (isInShipRoom)
        {
            RoundManager.Instance.valueOfFoundScrapItems = RoundManager.Instance.valueOfFoundScrapItems - scrapValue;
        }

        //PLAY AUDIBLE NOISE
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;
        RoundManager.Instance.PlayAudibleNoise(this.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

        //SET POSITION FOR SHATTER PREFAB
        Vector3 shatterPosition;
        if (Physics.Raycast(base.transform.position + itemProperties.verticalOffset * Vector3.up, Vector3.down, out var hitInfo, 800f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
		{
			shatterPosition = hitInfo.point + itemProperties.verticalOffset * Vector3.up;
		}
		else
		{
			shatterPosition = base.transform.localPosition;
		}

        //CHECK IF EXPLODING
        if (explode)
        {
            shatterPrefab = explodeShatterPrefab;
        }

        //SPAWN SHATTER PREFAB AT SHATTER POSITION
        GameObject brokenVase;
        Vector3 shatterRotation = new Vector3(0, transform.eulerAngles.y, 0);

        brokenVase = UnityEngine.Object.Instantiate(shatterPrefab, shatterPosition, Quaternion.Euler(shatterRotation), RoundManager.Instance.mapPropsContainer.transform);

        //PARENT PREFAB PROPERLY
        bool droppedInElevator = false;
        bool droppedInPhysicsRegion = false;
        NetworkObject parentNetworkObject = null;

        if (isInElevator)
        {
            brokenVase.transform.SetParent(StartOfRound.Instance.elevatorTransform);
            droppedInElevator = true;
        }

        if (Physics.Raycast(brokenVase.transform.position, -Vector3.up, out RaycastHit physicsHit, 80f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
        {
            PlayerPhysicsRegion physicsRegion = hitInfo.collider.gameObject.transform.GetComponentInChildren<PlayerPhysicsRegion>();

            if (physicsRegion != null && physicsRegion.allowDroppingItems && physicsRegion.itemDropCollider.ClosestPoint(physicsHit.point) == physicsHit.point)
            {
                parentNetworkObject = physicsRegion.parentNetworkObject;
                if (parentNetworkObject != null)
                {
                    brokenVase.transform.SetParent(parentNetworkObject.transform, worldPositionStays: true);
                    droppedInPhysicsRegion = true;
                }
            }
        }

        if (parentNetworkObject != null)
        {
            PlayerPhysicsRegion regionInChildren = parentNetworkObject.GetComponentInChildren<PlayerPhysicsRegion>();
            if (regionInChildren != null && regionInChildren.allowDroppingItems)
            {
                brokenVase.transform.SetParent(regionInChildren.physicsTransform, worldPositionStays: true);
            }
        }

        if (!droppedInElevator && !droppedInPhysicsRegion)
        {
            brokenVase.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
        }
    }

    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        if (!CanWobble(other))
        {
            return;
        }

        //CHECK IF WALL BETWEEN VASE AND COLLIDER
        RaycastHit hitInfo;
        if (Physics.Linecast(transform.position, other.transform.position, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("[VASE]: Wobbling aborted, wall between vase and collider.");
            return;
        }

        //PLAYER COLLISION
        if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null)
        {
            Debug.Log("[VASE]: Vase bumped by player.");
            PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

            if (hasHitGround && !(isHeld || isHeldByEnemy))
            {
                if (safePlaced)
                {
                    Debug.Log("[VASE]: Vase was in safely placed state.");
                    WobbleAndSync(0);
                }
                else if (player.isSprinting)
                {
                    Debug.Log("[VASE]: Player was sprinting.");
                    WobbleAndSync(2);
                }
                else if (player.isCrouching)
                {
                    Debug.Log("[VASE]: Player was crouching.");
                    WobbleAndSync(0);
                }
                else
                {
                    Debug.Log("[VASE]: Player was walking.");
                    WobbleAndSync(1);
                }

                //PHYSICS FORCE
                PlayerControllerB playerControllerB = player;

                if (physicsForce > 0f && !Physics.Linecast(base.transform.position, playerControllerB.transform.position, out hitInfo, 256, QueryTriggerInteraction.Ignore))
                {
                    float dist = Vector3.Distance(playerControllerB.transform.position, base.transform.position);
                    Vector3 vector = Vector3.Normalize(playerControllerB.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
                    if (vector.magnitude > 2f)
                    {
                        if (vector.magnitude > 10f)
                        {
                            playerControllerB.CancelSpecialTriggerAnimations();
                        }
                        if (!playerControllerB.inVehicleAnimation || (playerControllerB.externalForceAutoFade + vector).magnitude > 50f)
                        {
                                playerControllerB.externalForceAutoFade += vector;
                        }
                    }
                }
            }

            float currentHeight = transform.position.y;
            if (base.transform.parent != null)
            {
                currentHeight = base.transform.parent.InverseTransformPoint(transform.position).y;
            }
            float fallHeight = startFallingPosition.y - currentHeight;
            Debug.Log($"[VASE]: Hit player after falling {fallHeight}.");

            if (!hasHitGround && !(isHeld || isHeldByEnemy) && fallHeight > breakHeight && breakOnDrop)
            {
                Debug.Log("[VASE]: Broke on player!");
                player.DamagePlayer(Mathf.RoundToInt(Mathf.Clamp(fallHeight*2, 10f, 30f)), hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning, 0, fallDamage:false);
                ExplodeAndSync();
            }
        }

        //TIRE COLLISION
        else if (otherObject.GetComponent<TireReferenceScript>() != null)
        {
            Debug.Log("[VASE]: Bumped by tire.");
            float speed = otherObject.GetComponent<Rigidbody>().velocity.magnitude;
            if (speed < 2f)
            {
                WobbleAndSync(0);
            }
            else if (speed < 4f)
            {
                WobbleAndSync(1);
            }
            else if (speed < 8f)
            {
                WobbleAndSync(2);
            }
            else if (speed >= 8f)
            {
                ExplodeAndSync();
            }
        }

        //ENEMY COLLISION
        else if (otherObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
        {
            EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
            Debug.Log($"[VASE]: Vase bumped by enemytype {enemy.mainScript.enemyType.enemyName}.");

            if (enemy.mainScript.enemyType.enemyName == "Barber")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Flowerman")
            {
                FlowermanAI flowerman = enemy.mainScript as FlowermanAI;
                if (flowerman.isInAngerMode)
                {
                    WobbleAndSync(1);
                }
                else
                {
                    return;
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Spring")
            {
                WobbleAndSync(2);
            }
            else if (enemy.mainScript.enemyType.enemyName == "Blob")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Jester")
            {
                JesterAI jester = enemy.mainScript as JesterAI;
                if (jester.creatureAnimator.GetBool("poppedOut"))
                {
                    WobbleAndSync(2);
                }
                else
                {
                    WobbleAndSync(0);
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Girl")
            {
                WobbleAndSync(0);
            }
            else if (enemy.mainScript.enemyType.enemyName == "Maneater")
            {
                CaveDwellerAI caveDweller = enemy.mainScript as CaveDwellerAI;
                if (caveDweller.hasPlayerFoundBaby)
                {
                    if (caveDweller.playerHolding)
                    {
                        return;
                    }
                    else if (caveDweller.babyRunning)
                    {
                        WobbleAndSync(1);
                    }
                    else
                    {
                        WobbleAndSync(0);
                    }
                }
                else if (caveDweller.adultContainer.activeSelf)
                {
                    WobbleAndSync(2);
                }
                else
                {
                    return;
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Masked")
            {
                MaskedPlayerEnemy masked = enemy.mainScript as MaskedPlayerEnemy;
                if (masked.sprinting)
                {
                    WobbleAndSync(2);
                }
                else
                {
                    WobbleAndSync(1);
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Crawler")
            {
                CrawlerAI crawler = enemy.mainScript as CrawlerAI;
                if (crawler.hasEnteredChaseMode)
                {
                    WobbleAndSync(2);
                }
                else
                {
                    WobbleAndSync(1);
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Red Locust Bees")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
            {
                ExplodeAndSync();
            }
            else if (enemy.mainScript.enemyType.enemyName == "MouthDog")
            {
                MouthDogAI mouthDog = enemy.mainScript as MouthDogAI;
                if (mouthDog.inLunge)
                {
                    ExplodeAndSync();
                }
                else if (mouthDog.hasEnteredChaseModeFully)
                {
                    WobbleAndSync(2);
                }
                else
                {
                    WobbleAndSync(1);
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "ForestGiant")
            {
                ForestGiantAI forestGiant = enemy.mainScript as ForestGiantAI;
                if (forestGiant.chasingPlayer)
                {
                    WobbleAndSync(2);
                }
                else
                {
                    WobbleAndSync(1);
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Manticoil")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "RadMech")
            {
                RadMechAI radMech = enemy.mainScript as RadMechAI;
                if (radMech.chargingForward)
                {
                    ExplodeAndSync();
                }
                else if (radMech.isAlerted)
                {
                    WobbleAndSync(2);
                }
                else
                {
                    WobbleAndSync(1);
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Docile Locust Bees")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Tulip Snake")
            {
                FlowerSnakeEnemy flowerSnake = enemy.mainScript as FlowerSnakeEnemy;
                if (flowerSnake.clingingToPlayer)
                {
                    return;
                }
                else if (flowerSnake.leaping)
                {
                    WobbleAndSync(1);
                }
                else
                {
                    WobbleAndSync(0);
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Centipede")
            {
                WobbleAndSync(0);
            }
            else
            {
                WobbleAndSync(1);
            }
        }

        //VEHICLE COLLISION
        else if (otherObject.transform.parent != null && otherObject.transform.parent.gameObject.layer == 30 && otherObject.GetComponentInParent<VehicleController>() != null)
		{
			VehicleController vehicle = otherObject.GetComponentInParent<VehicleController>();
			if (vehicle.averageVelocity.magnitude < 2)
			{
				WobbleAndSync(2);
			}
            else
            {
                ExplodeAndSync();
            }
			return;
		}

        //ITEM COLLISION
        else if (otherObject.layer == 6 && otherObject.GetComponent<GrabbableObject>() != null)
        {
            Debug.Log("[VASE]: Bumped by prop.");
            GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();
            if (gObject.itemProperties.itemName == "Soccer ball")
            {
                SoccerBallProp ball = otherObject.GetComponent<SoccerBallProp>();
                if (!ball.hasHitGround && !ball.isHeld && !ball.isHeldByEnemy)
                {
                    ExplodeAndSync();
                }
            }
        }
    }

    public void OnExit(Collider other)
    {
    }

    public bool CanWobble(Collider other = null)
    {
        if (itemAnimator.GetBool("Wobbling"))
        {
            Debug.Log("[VASE]: Wobbling aborted, vase is already wobbling.");
            return false;
        }
        if (other != null && other.gameObject.layer != 3 && other.gameObject.layer != 19 && other.gameObject.layer != 30 && other.gameObject.layer != 6)
        {
            //Debug.Log($"[VASE]: Detected collider object {otherObject} is not of type that interacts with vase.");
            return false;
        }
        else if (isHeld || isHeldByEnemy)
        {
            Debug.Log("[VASE]: Wobbling aborted, vase is being held.");
            return false;
        }
        else if (!grabbable)
        {
            Debug.Log("[VASE]: Wobbling aborted, vase is not grabbable.");
            return false;
        }
        else if (base.isInShipRoom && !breakInShip)
        {
            Debug.Log("[VASE]: Wobbling aborted, break in ship disabled.");
            return false;
        }
        else if (!breakInOrbit)
        {
            if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
            {
                Debug.Log("[VASE]: Wobbling aborted, break in orbit disabled.");
                return false;
            }
        }
        else if (!hasHitGround)
        {
            Debug.Log("[VASE]: Wobbling aborted, vase is midair.");
            return false;
        }
        else if (itemAnimator.GetBool("Shattered"))
        {
            Debug.Log("[VASE]: Wobbling aborted, vase is shattered.");
            return false;
        }
        else if (!hasBeenSeen)
        {
            Debug.Log("[VASE]: Wobbling aborted, vase has not been seen yet.");
            return false;
        }
        return true;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (breakOnHit)
        {
            ExplodeAndSync();
        }
        return false;
	}

    public void DamagePlayerAndSync(PlayerControllerB player, int damage)
    {
        int playerClientId = (int)player.playerClientId;
        DamagePlayerServerRpc(playerClientId, damage);
    }

    [ServerRpc]
    private void DamagePlayerServerRpc(int playerClientId, int damage)
    {
        DamagePlayerClientRpc(playerClientId, damage);
    }

    [ClientRpc]
    private void DamagePlayerClientRpc(int playerClientId, int damage)
    {
        if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId == playerClientId)
        {
            GameNetworkManager.Instance.localPlayerController.DamagePlayer(damage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning, 0, fallDamage:false);
        }
    }

}