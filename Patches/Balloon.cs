using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Balloon : GrabbableObject
{

    [Space(15f)]
    [Header("Balloon Settings")]

    public float disableCatchingCooldown;

    [Space(5f)]
    [Header("Game Object References")]
    public GameObject balloon;

    public GameObject balloonCollider;

    public GameObject[] balloonStrings;

    public GameObject grabString;

    public GameObject scanNode;

    public GameObject popPrefab;

    [Space(5f)]
    [Header("Position Syncing")]
    public float syncPositionInterval = 0.2f;

    public float syncPositionThreshold = 0.5f;

    private float syncTimer;

    private Vector3 balloonServerPosition;

    [Space(5f)]
    [Header("Force Settings")]
    public ConstantForce constantForce;
    
    public float upwardForce;

    public String[] windyMoons;

    private String lastMoon;

    private bool windy;

    public float windForce;

    private Vector3 windDirection;

    private float windTimer;

    public float windTimeMin;

    public float windTimeMax;

    private float noiseAmount;

    private float noiseTimer;

    private float pushTimer;

    public float pushCooldown;

    private float drag;

    public float dragMin = 1f;

    public float dragMax = 2f;

    public float dragLerpTime = 1f;

    private Coroutine lerpDrag;

    [Space(5f)]
    [Header("Sounds")]
    public AudioSource balloonAudio;

    public AudioClip caughtClip;

    public AudioClip[] bumpClips;

    public AudioClip tugClip;

    private LineRenderer lineRenderer;

    private SphereCollider[] itemColliders;

    public override void Start()
    {
        //BALLOON START
        balloon.transform.SetParent(null, true);
        balloonCollider.transform.SetParent(null, true);

        lastMoon = StartOfRound.Instance.currentLevel.PlanetName;
        Debug.Log($"[BALLOON]: lastMoon set to {lastMoon}.");

        //CHECK FOR WIND
        Debug.Log("[BALLOON]: Checking if current weather is stormy.");
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy)
        {
            windy = true;
        }
        else
        {
            Debug.Log("[BALLOON]: Current weather is not stormy.");
            Debug.Log("[BALLOON]: Checking if current level is a windy moon.");
            for (int i = 0; i < windyMoons.Length; i++)
            {
                Debug.Log($"[BALLOON]: Checking if current level is {windyMoons[i]}...");
                if (StartOfRound.Instance.currentLevel.PlanetName.Contains(windyMoons[i]))
                {
                    Debug.Log($"[BALLOON]: Current level is {windyMoons[i]}! Setting balloon to windy.");
                    windy = true;
                    break;
                }
            }
        }

        for (int i = 0; i < balloonStrings.Length; i++)
        {
            balloonStrings[i].transform.SetParent(null, true);
        }

        lineRenderer = base.gameObject.GetComponent<LineRenderer>();
        lineRenderer.positionCount = balloonStrings.Length;

        //CREATE COLLIDERS FOR EACH STRING SECTION
        itemColliders = new SphereCollider[balloonStrings.Length];
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            SphereCollider sphereCollider = base.gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 1f;
            itemColliders[i] = sphereCollider;
        }

        if (base.IsOwner)
        {
            EnablePhysics(true);
        }
        else
        {
            EnablePhysics(false);
        }

        balloonServerPosition = balloon.transform.position;
        DampFloating();



        //BASE START FOR GRABBABLE OBJECT WITHOUT FALLING
        propColliders = base.gameObject.GetComponentsInChildren<Collider>();
        for (int i = 0; i < propColliders.Length; i++)
        {
            if (!propColliders[i].CompareTag("InteractTrigger"))
            {
                propColliders[i].excludeLayers = -2621449;
            }
        }

        originalScale = base.transform.localScale;

        if (itemProperties.isScrap && RoundManager.Instance.mapPropsContainer != null)
        {
            radarIcon = UnityEngine.Object.Instantiate(StartOfRound.Instance.itemRadarIconPrefab, RoundManager.Instance.mapPropsContainer.transform).transform;
        }

        if (!itemProperties.isScrap)
        {
            HoarderBugAI.grabbableObjectsInMap.Add(base.gameObject);
        }

        MeshRenderer[] meshRenderers = base.gameObject.GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshRenderers[i].renderingLayerMask = 1u;
        }
        SkinnedMeshRenderer[] skinnedMeshRenderers = base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            skinnedMeshRenderers[i].renderingLayerMask = 1u;
        }
    }

    public override void Update()
    {
        if (balloon == null)
        {
            Debug.Log("[BALLOON]: Balloon prefab destroyed, skipping update!");
            return;
        }

        //BASE UPDATE FOR SCRAP WITHOUT FALLING
        if (currentUseCooldown >= 0f)
        {
            currentUseCooldown -= Time.deltaTime;
        }

        if (base.IsOwner)
        {
            if (!wasOwnerLastFrame)
            {
                EnablePhysics(true);
                wasOwnerLastFrame = true;
            }
        }
        else if (wasOwnerLastFrame)
        {
            EnablePhysics(false);
            wasOwnerLastFrame = false;
        }

        //BALLOON UPDATE
        if (StartOfRound.Instance.currentLevel.PlanetName != lastMoon)
        {
            Debug.Log("[BALLOON]: Landed on new moon, checking for wind...");
            lastMoon = StartOfRound.Instance.currentLevel.PlanetName;

            if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy)
            {
                windy = true;
            }
            else
            {
                for (int i = 0; i < windyMoons.Length; i++)
                {
                    if (StartOfRound.Instance.currentLevel.PlanetName.Contains(windyMoons[i]))
                    {
                        windy = true;
                        break;
                    }
                }
            }
        }

        if (pushTimer > 0)
        {
            pushTimer -= Time.deltaTime;
        }

        for (int i = 0; i < balloonStrings.Length; i++)
        {
            Rigidbody rigidbody = balloonStrings[i].GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.drag = drag;
            }
        }

        if (base.IsOwner)
        {
            if (disableCatchingCooldown > 0)
            {
                disableCatchingCooldown -= Time.deltaTime;
                
                if (balloon.GetComponent<Collider>().enabled = false)
                {
                    balloon.GetComponent<Collider>().enabled = true;
                }
            }

            if (playerHeldBy != null)
            {
                if (playerHeldBy.teleportingThisFrame)
                {
                    Debug.Log("[BALLOON]: Player teleporting while holding balloon!");
                    balloon.GetComponent<Collider>().enabled = false;
                    disableCatchingCooldown = 1f;
                }

                if (playerHeldBy.teleportedLastFrame)
                {
                    balloon.transform.position = playerHeldBy.localItemHolder.transform.position;
                }

                //DISCARD IF STRETCHED TOO FAR FROM OWNER
                if (disableCatchingCooldown <= 0 && Vector3.Distance(balloon.transform.position, grabString.transform.position) > 8f)
                {
                    Debug.Log("[BALLOON]: Too far from player hand, discarding!");

                    //PLAY SOUND
                    PlayCaughtClipAndSync();

                    //DISCARD BALLOON
                    playerHeldBy.DiscardHeldObject();
                }
            }

            //POP IF BALLOON FLOATS TOO HIGH
            if (balloon.transform.position.y > 70f)
            {
                Pop();
            }

            //SYNC POSITION FROM OWNER TO CLIENTS ON INTERVAL
            if (syncTimer < syncPositionInterval)
            {
                syncTimer += Time.deltaTime;
            }
            else
            {
                syncTimer = 0f;
                SyncBalloonPositionServerRpc(balloon.transform.position);
            }

            Vector3 baseForce = new Vector3(0f, upwardForce, 0f);

            //WIND
            if (windy)
            {
                if (noiseTimer > 0)
                {
                    noiseTimer -= Time.deltaTime;
                }
                else
                {
                    noiseTimer = UnityEngine.Random.Range(0.05f,0.1f);
                    noiseAmount = UnityEngine.Random.Range(-2f, 2f);
                }

                if (windTimer > 0)
                {
                    windTimer -= Time.deltaTime;
                }
                else
                {
                    windTimer = UnityEngine.Random.Range(windTimeMin, windTimeMax);
                    RandomizeWindDirection();
                }

                if (!isInFactory && !isInShipRoom)
                {
                    constantForce.force = new Vector3(0f, upwardForce, 0f) + windDirection*windForce + windDirection*noiseAmount;
                }
                else
                {
                    constantForce.force = baseForce;
                }
            }
            else
            {
                if (constantForce.force != baseForce)
                {
                    constantForce.force = baseForce;
                }
            }
        }
        else
        {
            balloon.transform.position = Vector3.Lerp(balloon.transform.position, balloonServerPosition, Time.deltaTime*3f);
        }
    }

    public void PlayCaughtClipAndSync(bool makeNoise = true)
    {
        balloonAudio.PlayOneShot(caughtClip);
        if (makeNoise)
        {
            RoundManager.Instance.PlayAudibleNoise(balloon.transform.position);
        }

        PlayCaughtServerRpc();
    }

    [ServerRpc]
    public void PlayCaughtServerRpc(bool makeNoise = true)
    {
        PlayCaughtClientRpc();
    }

    [ClientRpc]
    public void PlayCaughtClientRpc(bool makeNoise = true)
    {
        if (!base.IsOwner)
        {
            balloonAudio.PlayOneShot(caughtClip);
            if (makeNoise)
            {
                RoundManager.Instance.PlayAudibleNoise(balloon.transform.position);
            }
        }
    }

    public override void LateUpdate()
    {
        if (radarIcon != null)
        {
            radarIcon.position = base.transform.position;
        }

        //SET STRING TO HAND IF OBJECT IS HELD BY PLAYER
        if (isHeld)
        {
            if (IsOwner)
            {
                grabString.transform.position = playerHeldBy.localItemHolder.transform.position;
            }
            else
            {
                grabString.transform.position = playerHeldBy.serverItemHolder.transform.position;
            }
        }

        //SET POSITIONS OF BALLOON PARTS
        if (balloon == null || grabString == null)
        {
            Debug.Log("[BALLOON]: Floating balloon prefab was not found, skipping setting postions!");
            return;
        }

        base.transform.position = grabString.transform.position;
        scanNode.transform.position = balloon.transform.position;
        balloonCollider.transform.position = balloon.transform.position;

        //SET POSITIONS OF GRAB COLLIDERS
        for (int i = 0; i < itemColliders.Length; i++)
        {
            itemColliders[i].center = base.transform.InverseTransformPoint(balloonStrings[i].transform.position);
        }

        //SET LINE RENDERER POSITIONS
        Vector3[] stringPositions = new Vector3[balloonStrings.Length];
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            stringPositions[i] = balloonStrings[i].transform.position;
        }
        lineRenderer.SetPositions(stringPositions);
    }

    public void EnablePhysics(bool enable = true)
    {
        if (enable)
        {
            balloon.GetComponent<Rigidbody>().isKinematic = false;
            balloon.GetComponent<Collider>().enabled = true;
        }
        else
        {
            balloon.GetComponent<Rigidbody>().isKinematic = true;
            balloon.GetComponent<Collider>().enabled = false;
        }
    }

    public void RandomizeWindDirection()
    {
        Vector2 dir2D = UnityEngine.Random.insideUnitCircle;
        Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y);
        dir.Normalize();
        windDirection = dir;
        Debug.Log($"[BALLOON]: Wind direction set to {windDirection}.");
    }



    //GRABBING AND DISCARDING
    public override void GrabItem()
    {
        base.GrabItem();
        for (int i = 0; i < itemColliders.Length; i++)
        {
            itemColliders[i].enabled = false;
        }
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        base.GrabItemFromEnemy(enemy);
    }

    public override void DiscardItem()
    {
        base.DiscardItem();
        DampFloating();
        for (int i = 0; i < itemColliders.Length; i++)
        {
            itemColliders[i].enabled = true;
        }
    }

    public override void DiscardItemFromEnemy()
    {

    }

    public void DampFloating()
    {
        Debug.Log("[BALLOON]: Damping physics!");

        for (int i = 0; i < balloonStrings.Length; i++)
        {
            Rigidbody rigidbody = balloonStrings[i].GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.velocity = new Vector3(0f,0f,0f);
            }
        }

        if (lerpDrag != null)
        {
            StopCoroutine(lerpDrag);
        }

        lerpDrag = StartCoroutine(LerpDrag());
    }

    public IEnumerator LerpDrag()
    {
        drag = dragMax;
        float timeElapsed = 0f;
        while (timeElapsed < dragLerpTime)
        {
            drag = Mathf.Lerp(dragMax, dragMin, timeElapsed / dragLerpTime);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        drag = dragMin;
    }



    //USING ITEM
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        Debug.Log("[BALLOON]: Used item!");
        base.ItemActivate(used, buttonDown);

        //ANIMATE HAND DOWN TO TUG ON BALLOON A BIT

        //PLAY SOUND
        balloonAudio.PlayOneShot(tugClip);

        //PLAY AUDIBLE NOISE
        // if (Vector3.Distance(lastPosition, balloon.transform.position) > 2f)
        // {
        //     timesPlayedInOneSpot = 0;
        // }
        // timesPlayedInOneSpot++;
        // lastPosition = balloon.transform.position;
        // RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
    }



    //COLLISIONS
    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        Debug.Log($"[BALLOON]: Balloon collided with {gameObject}.");

        RaycastHit hitInfo;
        if (Physics.Linecast(transform.position, other.transform.position, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        //PLAYER COLLISION
        if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null)
        {
            Debug.Log("[BALLOON]: Bumped by player.");
            PushBalloon(otherObject.transform.position, 10f);
        }

        //ENEMY COLLISION
        else if (otherObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
        {
            EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
            Debug.Log($"[BALLOON]: Bumped by enemytype {enemy.mainScript.enemyType.enemyName}.");

            if (enemy.mainScript.enemyType.enemyName == "Flowerman")
            {
                PushBalloon(otherObject.transform.position, 8f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Spring")
            {
                PushBalloon(otherObject.transform.position, 20f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Jester")
            {
                JesterAI jester = enemy.mainScript as JesterAI;
                if (jester.creatureAnimator.GetBool("poppedOut"))
                {
                    Pop();
                }
                else
                {
                    PushBalloon(otherObject.transform.position, 8f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Maneater")
            {
                CaveDwellerAI caveDweller = enemy.mainScript as CaveDwellerAI;
                if (caveDweller.adultContainer.activeSelf)
                {
                    PushBalloon(otherObject.transform.position, 20f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Masked")
            {
                PushBalloon(otherObject.transform.position, 10f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Crawler")
            {
                CrawlerAI crawler = enemy.mainScript as CrawlerAI;
                if (crawler.hasEnteredChaseMode)
                {
                    Pop();
                }
                else
                {
                    PushBalloon(otherObject.transform.position, 20f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Red Locust Bees")
            {
                Pop();
            }

            else if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
            {
                Pop();
            }

            else if (enemy.mainScript.enemyType.enemyName == "MouthDog")
            {
                MouthDogAI mouthDog = enemy.mainScript as MouthDogAI;
                if (mouthDog.hasEnteredChaseModeFully)
                {
                    Pop();
                }
                else
                {
                    PushBalloon(otherObject.transform.position, 20f);
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "ForestGiant")
            {
                PushBalloon(otherObject.transform.position, 30f);
            }

            else if (enemy.mainScript.enemyType.enemyName == "Manticoil")
            {
                DoublewingAI doublewing = enemy.mainScript as DoublewingAI;
                if (doublewing.creatureAnimator.GetBool("flying"))
                {
                    Pop();
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Docile Locust Bees")
            {
                Pop();
            }

            else if (enemy.mainScript.enemyType.enemyName == "Tulip Snake")
            {
                FlowerSnakeEnemy flowerSnake = enemy.mainScript as FlowerSnakeEnemy;
                if (flowerSnake.leaping)
                {
                    Pop();
                }
            }

            else if (enemy.mainScript.enemyType.enemyName == "Centipede")
            {
                CentipedeAI centipede = enemy.mainScript as CentipedeAI;
                if (centipede.triggeredFall)
                {
                    Pop();
                }
            }

            else if (enemy.mainScript.enemyType.enemyName != "RadMech")
            {
                RadMechAI radMech = enemy.mainScript as RadMechAI;
                if (radMech.chargingForward)
                {
                    Pop();
                }
                else if (radMech.inFlyingMode)
                {
                    Pop();
                }
                else
                {
                    PushBalloon(otherObject.transform.position, 30f);
                }

            }
        }

        //VEHICLE COLLISION
        else if (otherObject.GetComponentInParent<VehicleController>() != null)
        {
            VehicleController vehicle = otherObject.GetComponentInParent<VehicleController>();
            if (vehicle.averageVelocity.magnitude < 5)
            {
                PushBalloon(otherObject.transform.position, 5*vehicle.averageVelocity.magnitude);
            }
            else
            {
                Pop();
            }
        }

        //OTHER BALLOON COLLISION
        else if (otherObject.GetComponent<BalloonCollisionDetection>() != null)
        {
            PushBalloon(otherObject.transform.position, 5);
        }

        //ITEM COLLISION
        else if (otherObject.layer == 6 && otherObject.GetComponent<GrabbableObject>() != null)
        {
            GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();

            if (gObject.itemProperties.itemName == "Soccer ball")
            {
                SoccerBallProp ball = otherObject.GetComponent<SoccerBallProp>();
                if (!ball.hasHitGround && !ball.isHeld && !ball.isHeldByEnemy)
                {
                    Pop();
                }
            }
        }
    }

    public void OnExit(Collider other)
    {

    }

    public void PushBalloon(Vector3 otherPos, float pushForce)
    {
        if (pushTimer <= 0)
        {
            pushTimer = pushCooldown;

            //ADD FORCE
            Vector3 vector = transform.position - otherPos;
            vector.y = 0;
            Vector3 dir = Vector3.Normalize(vector);
            Vector3 force = dir*pushForce;
            Debug.Log($"[BALLOON]: Pushing with force {force}!");
            balloon.GetComponent<Rigidbody>().AddForce(force, ForceMode.Impulse);

            //PLAY SOUND
            balloonAudio.PlayOneShot(bumpClips[UnityEngine.Random.Range(0,bumpClips.Length)]);

            //PLAY AUDIBLE NOISE
            // if (Vector3.Distance(lastPosition, balloon.transform.position) > 2f)
            // {
            //     timesPlayedInOneSpot = 0;
            // }
            // timesPlayedInOneSpot++;
            // lastPosition = balloon.transform.position;
            // RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        }
    }



    //POPPING
    public void Pop()
    {
        PopServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PopServerRpc()
    {
        //RELEASE FROM PLAYER IF HELD
        if (playerHeldBy != null)
        {
            playerHeldBy.DiscardHeldObject();
        }

        PopClientRpc();
    }

    [ClientRpc]
    public void PopClientRpc()
    {
        Debug.Log("[BALLOON]: Popped!");

        //DESTROY FLOATING PREFAB
        DestroyBalloon();

        //SPAWN POP PREFAB
        Instantiate(popPrefab, balloon.transform.position, Quaternion.identity);

        //PLAY AUDIBLE NOISE
        RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange: 15f, noiseLoudness: 1f);

        //DESPAWN BALLOON ITEM
        if (IsOwner)
        {
            StartCoroutine(DespawnAfterFrame());
        }
    }

    public IEnumerator DespawnAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        if (base.gameObject.GetComponent<NetworkObject>().IsSpawned)
        {
            Debug.Log("[BALLOON]: Despawning!");
            base.gameObject.GetComponent<NetworkObject>().Despawn();
        }
    }

    public void DestroyBalloon()
    {
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            Destroy(balloonStrings[i]);
        }

        Destroy(balloonCollider);
    }



    //SYNCING POSITIONS
    [ServerRpc]
    private void SyncBalloonPositionServerRpc(Vector3 balloonPos)
    {
        SyncBalloonPositionClientRpc(balloonPos);
    }

    [ClientRpc]
    private void SyncBalloonPositionClientRpc(Vector3 balloonPos)
    {
        if (!base.IsOwner)
        {
            if (Vector3.Distance(balloon.transform.position, balloonPos) > syncPositionThreshold)
            {
                balloonServerPosition = balloonPos;
            }
        }
    }

}