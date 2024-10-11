using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

public class Vase : AnimatedItem, IHittable, ITouchable
{
    [Header("Vase Settings")]
    public float breakHeight;

    private Vector3 startPosition;

    public float safePlaceTime;

    public float physicsForce;

    private bool safePlaced;

    public bool wobbling;

    public bool shattered;

    private bool hasBeenSeen;

    public GameObject sprintShatterPrefab;

    public GameObject walkShatterPrefab;

    public GameObject explodePrefab;

    [Space(5f)]
    public bool breakOnHit = true;

    public bool breakOnCrouch = false;

    public bool breakOnDrop = true;

    public bool breakOnDeath = true;

    public bool breakOnBlast = true;

    public bool breakInShip = true;

    public bool breakInOrbit = false;

    [Space(5f)]
    public AnimationClip crouchWobbleAnimation;

    public AnimationClip walkWobbleAnimation;

    public AnimationClip sprintWobbleAnimation;

    [Space(5f)]
    public AudioSource vaseAudio;

    public AudioClip[] wobbleWalk;

    public AudioClip[] wobbleSprint;

    public AudioClip[] wobbleCrouch;

    public AudioClip[] vaseBreak;

    private Coroutine CancellableShatter;

    public override void Start()
    {
        base.Start();
        safePlaced = false;
        wobbling = false;
    }

    public override void Update()
    {
        base.Update();

        if (!hasBeenSeen)
        {
            for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount; i++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(base.transform.position))
                {
                    hasBeenSeen = true;
                    Debug.Log($"Vase seen by {StartOfRound.Instance.allPlayerScripts[i]}.");
                }
            }
        }
    }

    public override void GrabItem()
    {
        Debug.Log("Vase grabbed.");
        wobbling = false;
        safePlaced = true;
        itemAnimator.Play("stop");
        itemAudio.Stop();
        if (CancellableShatter != null)
        {
            StopCoroutine(CancellableShatter);
        }

        base.GrabItem();
    }

    public override void DiscardItem()
    {
        startPosition = base.gameObject.transform.position;
		if (base.gameObject.transform.parent != null)
		{
			startPosition = base.gameObject.transform.parent.InverseTransformPoint(startPosition);
		}

        if (playerHeldBy.isPlayerDead && breakOnDeath)
        {
            Shatter(explodePrefab);
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
		Debug.Log($"Vase dropped: {fallHeight}");

        if (fallHeight > breakHeight && breakOnDrop)
        {
            Shatter(explodePrefab);
        }
        else
        {
            Debug.Log("Vase placed safely, beginning cooldown time.");
            StartCoroutine(placeSafely(safePlaceTime));
        }
    }

    public override void PlayDropSFX()
    {
        Debug.Log($"Drop SFX found in item properties: {itemProperties.dropSFX}");
        AudioClip[] dropSFX = new AudioClip[1];
        dropSFX[0] = itemProperties.dropSFX;
        RoundManager.Instance.PlayAudibleNoise(this.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(vaseAudio, dropSFX, randomize: true, 1f, -1);

        hasHitGround = true;
    }

    public IEnumerator placeSafely(float time)
    {
        yield return new WaitForSeconds(time);
        safePlaced = false;
    }

    public void Shatter(GameObject prefab)
    {
        if (shattered || heldByPlayerOnServer)
        {
            return;
        }

        Debug.Log("Vase shattered!");
        shattered = true;
        grabbable = false;
        grabbableToEnemies = false;
        scrapValue = 0;
        if (isInShipRoom)
        {
            RoundManager.Instance.valueOfFoundScrapItems = RoundManager.Instance.valueOfFoundScrapItems - scrapValue;
        }

        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(this.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(vaseAudio, vaseBreak, randomize: true, 1f, -1);

        Vector3 shatterPosition;
        if (Physics.Raycast(base.transform.position + itemProperties.verticalOffset * Vector3.up, Vector3.down, out var hitInfo, 80f, -2130704000, QueryTriggerInteraction.Ignore))
		{
			shatterPosition = hitInfo.point + itemProperties.verticalOffset * Vector3.up;
			if (base.transform.parent != null)
			{
				shatterPosition = base.transform.parent.InverseTransformPoint(targetFloorPosition);
			}
            Debug.Log($"Raycast set shatter position to: {shatterPosition}");
		}
		else
		{
			Debug.Log("dropping item did not get raycast : " + base.gameObject.name);
			shatterPosition = base.transform.localPosition;
		}

        GameObject brokenVase = UnityEngine.Object.Instantiate(prefab, shatterPosition, this.transform.rotation, RoundManager.Instance.mapPropsContainer.transform);
        if(isInShipRoom)
        {
            brokenVase.transform.SetParent(base.gameObject.transform.parent);
        }
        if (isInElevator)
        {
            brokenVase.transform.SetParent(base.gameObject.transform.parent);
        }

        if (isHeld)
        {
            DiscardItem();
        }

        UnityEngine.Object.Destroy(this.gameObject);
    }

    public void Wobble(AnimationClip animation, AudioClip[] audioClip, GameObject shatterPrefab, bool shatter = true)
    {
        wobbling = true;

        Debug.Log("Vase started wobbling.");
        
        if (animation == crouchWobbleAnimation)
        {
            itemAnimator.Play("wobbleCrouch");
        }
        else if (animation == walkWobbleAnimation)
        {
            itemAnimator.Play("wobbleWalk");
        }
        else if (animation == sprintWobbleAnimation)
        {
            itemAnimator.Play("wobbleSprint");
        }

        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange/2, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(vaseAudio, audioClip, randomize: false, 1f, -1);

        CancellableShatter = StartCoroutine(ShatterOnDelay(animation.length, shatterPrefab, shatter));
    }

    public IEnumerator ShatterOnDelay(float time, GameObject shatterPrefab, bool shatter)
    {
        yield return new WaitForSeconds(time);
        if (shatter)
        {
            Shatter(shatterPrefab);
        }
        else
        {
            wobbling = false;
            itemAnimator.Play("stop");
        }
    }

    public void CrouchWobble()
    {
        Wobble(crouchWobbleAnimation, wobbleCrouch, explodePrefab, shatter: breakOnCrouch);
    }

    public void WalkWobble()
    {
        Wobble(walkWobbleAnimation, wobbleWalk, walkShatterPrefab);
    }

    public void SprintWobble()
    {
        Wobble(sprintWobbleAnimation, wobbleSprint, sprintShatterPrefab);
    }

    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        if (wobbling)
        {
            Debug.Log("Wobbling aborted, vase is already wobbling.");
            return;
        }

        if (otherObject.layer != 3 && otherObject.layer != 19 && otherObject.layer != 30 && otherObject.layer != 6)
        {
            //Debug.Log($"Detected collider object {otherObject} is not of type that interacts with vase.");
            return;
        }
        else if (isHeld || isHeldByEnemy)
        {
            Debug.Log("Wobbling aborted, vase is being held.");
            return;
        }
        else if (!grabbable)
        {
            Debug.Log("Wobbling aborted, vase is not grabbable.");
            return;
        }
        else if (base.isInShipRoom && !breakInShip)
        {
            Debug.Log("Wobbling aborted, break in ship disabled.");
            return;
        }
        else if (!breakInOrbit)
        {
            if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
            {
                Debug.Log("Wobbling aborted, break in orbit disabled.");
                return;
            }
        }
        else if (!hasHitGround)
        {
            Debug.Log("Wobbling aborted, vase is midair.");
            return;
        }
        else if (safePlaced)
        {
            Debug.Log("Wobbling aborted, vase placed safely and is on cooldown.");
            return;
        }
        else if (shattered)
        {
            Debug.Log("Wobbling aborted, vase is shattered.");
            return;
        }
        else if (!hasBeenSeen)
        {
            Debug.Log("Wobbling aborted, vase has not been seen yet.");
            return;
        }

        //PLAYER COLLISION
        if (otherObject.layer == 3)
        {
            Debug.Log("Vase bumped by player.");
            PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

            if (player.isSprinting)
            {
                Debug.Log("Player was sprinting.");
                SprintWobble();
            }
            else if (player.isCrouching)
            {
                Debug.Log("Player was crouching.");
                CrouchWobble();
            }
            else if (player.isWalking && !player.isSprinting && !player.isCrouching)
            {
                Debug.Log("Player was walking.");
                WalkWobble();
            }

            //PHYSICS FORCE
            RaycastHit hitInfo;
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

        //ENEMY COLLISION
        else if (otherObject.layer == 19)
        {
            Debug.Log("Vase bumped by enemy.");
            Debug.Log($"Collider object: {otherObject}");
            if (otherObject.GetComponent<EnemyAICollisionDetect>() == null)
            {
                return;
            }

            EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();

            if (enemy.mainScript.enemyType.enemyName == "Barber")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Flowerman")
            {
                FlowermanAI flowerman = enemy.mainScript as FlowermanAI;
                if (flowerman.isInAngerMode)
                {
                    WalkWobble();
                }
                else
                {
                    return;
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Spring")
            {
                SprintWobble();
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
                    SprintWobble();
                }
                else
                {
                    CrouchWobble();
                }
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
                        WalkWobble();
                    }
                    else
                    {
                        CrouchWobble();
                    }
                }
                else if (caveDweller.adultContainer.activeSelf)
                {
                    SprintWobble();
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
                    SprintWobble();
                }
                else
                {
                    WalkWobble();
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Crawler")
            {
                CrawlerAI crawler = enemy.mainScript as CrawlerAI;
                if (crawler.hasEnteredChaseMode)
                {
                    SprintWobble();
                }
                else
                {
                    WalkWobble();
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Red Locust Bees")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
            {
                Shatter(explodePrefab);
            }
            else if (enemy.mainScript.enemyType.enemyName == "MouthDog")
            {
                MouthDogAI mouthDog = enemy.mainScript as MouthDogAI;
                if (mouthDog.inLunge)
                {
                    Shatter(explodePrefab);
                }
                else if (mouthDog.hasEnteredChaseModeFully)
                {
                    SprintWobble();
                }
                else
                {
                    WalkWobble();
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "ForestGiant")
            {
                ForestGiantAI forestGiant = enemy.mainScript as ForestGiantAI;
                if (forestGiant.chasingPlayer)
                {
                    SprintWobble();
                }
                else
                {
                    WalkWobble();
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
                    Shatter(explodePrefab);
                }
                else if (radMech.isAlerted)
                {
                    SprintWobble();
                }
                else
                {
                    WalkWobble();
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
                    WalkWobble();
                }
                else
                {
                    CrouchWobble();
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Centipede")
            {
                CrouchWobble();
            }
            else
            {
                WalkWobble();
            }
        }

        //VEHICLE COLLISION
        else if (otherObject.layer == 30)
        {
            Debug.Log("Bumped by vehicle.");
        }

        //ITEM COLLISION
        else if (otherObject.layer == 6)
        {
            Debug.Log("Bumped by prop.");
            if (otherObject.GetComponent<SoccerBallProp>() != null)
            {
                GrabbableObject ball = otherObject.GetComponent<SoccerBallProp>();
                if (!ball.hasHitGround && !ball.isHeld && !ball.isHeldByEnemy)
                {
                    Shatter(explodePrefab);
                }
            }
            else if (otherObject.name.StartsWith("explosionCollider"))
            {
                Shatter(explodePrefab);
            }
        }
    }

    public void OnExit(Collider other)
    {
    }

    public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        float m = (value - min1) / (max1 - min1) * (max2 - min2) + min2;
        return m;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (breakOnHit)
        {
            Shatter(explodePrefab);
        }
        return true;
	}

}