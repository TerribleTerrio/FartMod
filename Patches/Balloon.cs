using System;
using System.Collections;
using System.Collections.Generic;
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
    public AudioSource itemAudio;

    public AudioSource physicsAudio;

    public AudioSource caughtAudio;

    public AudioClip[] snapClips;

    public AudioClip[] bumpClips;

    public AudioClip tugClip;

    public AudioClip[] tugStringClips;

    private LineRenderer lineRenderer;

    private SphereCollider[] itemColliders;

    private bool tugging;

    private float popHeight = 55f;

    private float stringColliderRadius = 1f;

    private bool frozen;

    [HideInInspector]
    public RuntimeAnimatorController playerDefaultAnimatorController;

    [HideInInspector]
    public RuntimeAnimatorController otherPlayerDefaultAnimatorController;

    [Header("Animators to replace default player animators")]
    public RuntimeAnimatorController playerCustomAnimatorController;

    public RuntimeAnimatorController otherPlayerCustomAnimatorController;

	private PlayerControllerB previousPlayerHeldBy;

    private bool isCrouching;

    private bool isJumping;

    private bool isWalking;

    private bool isSprinting;

    private AnimatorStateInfo currentStateInfo;

    private float currentAnimationTime;

	public float noiseRange = 15f;

	public float noiseLoudness = 0.55f;

	private int timesPlayedInOneSpot;

	private Vector3 lastPosition;

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
            sphereCollider.radius = stringColliderRadius;
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
            }
            else if (frozen)
            {
                FreezePhysics(false);
            }

            if (playerHeldBy != null)
            {
                if (playerHeldBy.teleportingThisFrame)
                {
                    Debug.Log("[BALLOON]: Player teleporting while holding balloon!");
                    FreezePhysics(true);
                    disableCatchingCooldown = 0.05f;
                }

                //BRING BALLOON IN VIEW OF PLAYER AFTER TELEPORTING
                if (playerHeldBy.teleportedLastFrame)
                {
                    balloon.transform.position = playerHeldBy.localItemHolder.transform.position + Vector3.up * 1f;
                    balloonStrings[1].transform.position = playerHeldBy.localItemHolder.transform.position + Vector3.up * 0.75f;
                    balloonStrings[2].transform.position = playerHeldBy.localItemHolder.transform.position + Vector3.up * 0.25f;
                    grabString.transform.position = playerHeldBy.localItemHolder.transform.position;
                }

                if (Physics.Linecast(balloon.transform.position, grabString.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault) && Vector3.Distance(balloon.transform.position, grabString.transform.position) > 5f)
                {
                    caughtAudio.volume = Mathf.Lerp(caughtAudio.volume, 1f, Time.deltaTime * 10f);
                }
                else
                {
                    caughtAudio.volume = Mathf.Lerp(caughtAudio.volume, 0f, Time.deltaTime * 10f);
                }

                //DISCARD IF STRETCHED TOO FAR FROM OWNER
                if (disableCatchingCooldown <= 0 && Vector3.Distance(balloon.transform.position, grabString.transform.position) > 7f)
                {
                    Debug.Log("[BALLOON]: Too far from player hand, discarding!");

                    //PLAY SOUND
                    PlaySnapClipAndSync();

                    //DISCARD BALLOON
                    playerHeldBy.DiscardHeldObject();

                    caughtAudio.volume = 0f;
                }
            }

            //POP IF BALLOON FLOATS TOO HIGH
            if (balloon.transform.position.y > popHeight)
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

        //PLAYER ANIMATOR UPDATE
        if (previousPlayerHeldBy == null || !base.IsOwner)
        {
            return;
        }
        else 
        {
            if (previousPlayerHeldBy.isPlayerDead)
            {
                SetAnimator(setOverride: false);
            }
        }
    }

    public void FreezePhysics(bool freeze)
    {
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            balloonStrings[i].GetComponent<Rigidbody>().isKinematic = freeze;
        }
        frozen = freeze;
    }

    public void PlaySnapClipAndSync()
    {
        physicsAudio.clip = snapClips[UnityEngine.Random.Range(0, snapClips.Length)];
        physicsAudio.pitch = UnityEngine.Random.Range(0.9f, 1.15f);
        physicsAudio.Play();
        RoundManager.Instance.PlayAudibleNoise(balloon.transform.position);

        PlaySnapClipServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlaySnapClipServerRpc()
    {
        PlaySnapClipClientRpc();
    }

    [ClientRpc]
    public void PlaySnapClipClientRpc()
    {
        if (!base.IsOwner)
        {
            physicsAudio.clip = snapClips[UnityEngine.Random.Range(0, snapClips.Length)];
            physicsAudio.pitch = UnityEngine.Random.Range(0.9f, 1.15f);
            physicsAudio.Play();
            RoundManager.Instance.PlayAudibleNoise(balloon.transform.position);
        }
    }

    public override void LateUpdate()
    {
        if (radarIcon != null)
        {
            radarIcon.position = base.transform.position;
        }

        //SET POSITIONS OF BALLOON PARTS
        if (balloon == null || grabString == null)
        {
            Debug.Log("[BALLOON]: Floating balloon prefab was not found, skipping setting postions!");
            return;
        }

        //SET STRING TO HAND IF OBJECT IS HELD BY PLAYER
        if (parentObject != null)
        {
			Vector3 positionOffset = itemProperties.positionOffset;
            grabString.transform.position = parentObject.transform.position;
            positionOffset = parentObject.transform.rotation * positionOffset;
			grabString.transform.position += positionOffset;
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
        SubdivideStrings(subdivisions: 3);
    }

    public void SubdivideStrings(int subdivisions)
    {
        static Vector3 SplinePosition(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, float k)
        {
            Vector3 a = 2f * p1;
            Vector3 b = (p2 - p0) * k;
            Vector3 c = (2f * p0 - 5f * p1 + 4f * p2 - p3) * k;
            Vector3 d = (-p0 + 3f * p1 - 3f * p2 + p3) * k;
            
            return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
        }
        List<Vector3> origStringPoints = [];
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            origStringPoints.Add(balloonStrings[i].transform.position);
        }
        if (isHeld)
        {
            //ADD UNMOVING STRING SEGMENT IN PLAYER'S HAND
            origStringPoints.Add(balloonStrings[^1].transform.position - playerHeldBy.localItemHolder.transform.right * 0.1f);
        }
        List<Vector3> subdivPoints = [];
        for (int i = 0; i < origStringPoints.Count - 1; i++)
        {
            float curvature = i < 2 ? 1f : i == 2 ? 0.75f : 0.25f;
            Vector3 p0 = origStringPoints[i - 1 < 0 ? 0 : i - 1];
            Vector3 p1 = origStringPoints[i];
            Vector3 p2 = origStringPoints[i + 1];
            Vector3 p3 = origStringPoints[i + 2 >= origStringPoints.Count ? origStringPoints.Count - 1 : 1 + 2];

            subdivPoints.Add(p1);
            for (int j = 1; j <= subdivisions; j++)
            {
                float t = j / (float)(subdivisions + 1);
                subdivPoints.Add(SplinePosition(p0, p1, p2, p3, t, curvature));
            }
        }
        subdivPoints.Add(origStringPoints[^1]);
        lineRenderer.positionCount = subdivPoints.Count;
        lineRenderer.SetPositions(subdivPoints.ToArray());
    }

    public new void EnablePhysics(bool enable = true)
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

    public override void EquipItem()
    {
        base.EquipItem();
        previousPlayerHeldBy = playerHeldBy;
        SetAnimator(setOverride: true);
        playerHeldBy.playerBodyAnimator.Play("HoldBalloon");
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
        SetAnimator(setOverride: false);
    }

    public override void DiscardItemFromEnemy()
    {

    }

    public override void OnDestroy()
    {
        DestroyBalloon();
        base.OnDestroy();
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
        if (tugging)
        {
            return;
        }
        base.ItemActivate(used, buttonDown);

        //ANIMATE HAND DOWN TO TUG ON BALLOON A BIT
        StartCoroutine(TugBalloon());

        //PLAY AUDIBLE NOISE
        if (Vector3.Distance(lastPosition, balloon.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = balloon.transform.position;
        RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
    }

    public IEnumerator TugBalloon()
    {
        tugging = true;
        if (IsOwner)
        {
            previousPlayerHeldBy.playerBodyAnimator.SetTrigger("TugBalloon");
        }
        itemAudio.clip = tugClip;
        itemAudio.pitch = UnityEngine.Random.Range(0.95f, 1.1f);
        itemAudio.Play();
        yield return new WaitForSeconds(0.2f);
        if (IsOwner)
        {
            Vector3 direction = Vector3.Lerp(Vector3.down, (balloonCollider.transform.position - grabString.transform.position).normalized * -1f, 0.5f);
            float force = Mathf.Clamp(Vector3.Distance(balloonCollider.transform.position, grabString.transform.position) * 6f, 5f, 25f);
            balloon.GetComponent<Rigidbody>().AddForce(direction * force, ForceMode.Impulse);
            DampFloating();
        }
        physicsAudio.clip = tugStringClips[UnityEngine.Random.Range(0, tugStringClips.Length)];
        physicsAudio.pitch = UnityEngine.Random.Range(0.9f, 1.15f);
        physicsAudio.Play();
        yield return new WaitForSeconds(0.75f);
        if (IsOwner)
        {
            previousPlayerHeldBy.playerBodyAnimator.ResetTrigger("TugBalloon");
        }
        tugging = false;
    }

    //COLLISIONS
    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        Debug.Log($"[BALLOON]: Balloon collided with {gameObject}.");

        RaycastHit hitInfo;
        if (frozen || Physics.Linecast(transform.position, other.transform.position, out hitInfo, 1073742080, QueryTriggerInteraction.Ignore))
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
            physicsAudio.clip = bumpClips[UnityEngine.Random.Range(0,bumpClips.Length)];
            physicsAudio.pitch = UnityEngine.Random.Range(0.9f, 1.15f);
            physicsAudio.Play();

            //PLAY AUDIBLE NOISE
            if (Vector3.Distance(lastPosition, balloon.transform.position) > 2f)
            {
                timesPlayedInOneSpot = 0;
            }
            timesPlayedInOneSpot++;
            lastPosition = balloon.transform.position;
            RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
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
        PopClientRpc();
    }

    [ClientRpc]
    public void PopClientRpc()
    {
        Debug.Log("[BALLOON]: Popped!");

        //RELEASE FROM PLAYER IF HELD AND DESPAWN BALLOON
        if (playerHeldBy != null)
        {
            if (playerHeldBy == GameNetworkManager.Instance.localPlayerController)
            {
                playerHeldBy.DiscardHeldObject();
                DespawnBalloonServerRpc();
            }
        }
        else
        {
            if (base.IsServer)
            {
                StartCoroutine(DespawnAfterFrame());
            }
        }

        //DESTROY FLOATING PREFAB
        DestroyBalloon();

        //SPAWN POP PREFAB
        Instantiate(popPrefab, balloon.transform.position, Quaternion.identity);

        //PLAY AUDIBLE NOISE
        RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange: 15f, noiseLoudness: 1f);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DespawnBalloonServerRpc()
    {
        StartCoroutine(DespawnAfterFrame());
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

    // --- CHANGE ANIMATOR - RIPPED FROM HAND MIRROR! ---
    private void SetAnimator(bool setOverride)
    {
        if (setOverride == true)
        {
            if (playerHeldBy != null)
            {
                if (playerHeldBy == StartOfRound.Instance.localPlayerController)
                {
                    SaveAnimatorStates(playerHeldBy.playerBodyAnimator);
                    if (playerDefaultAnimatorController != playerCustomAnimatorController)
                    {
                        playerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
                    }
                    playerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerCustomAnimatorController;
                    SetAnimatorStates(playerHeldBy.playerBodyAnimator);
                }
                else
                {
                    SaveAnimatorStates(playerHeldBy.playerBodyAnimator);
                    if (otherPlayerDefaultAnimatorController != otherPlayerCustomAnimatorController)
                    {
                        otherPlayerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
                    }
                    playerHeldBy.playerBodyAnimator.runtimeAnimatorController = otherPlayerCustomAnimatorController;
                    SetAnimatorStates(playerHeldBy.playerBodyAnimator);
                }
            }
        }
        else
        {
            if (previousPlayerHeldBy != null)
            {
                if (previousPlayerHeldBy == StartOfRound.Instance.localPlayerController)
                {
                    SaveAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                    previousPlayerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerDefaultAnimatorController;
                    SetAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                }
                else
                {
                    SaveAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                    previousPlayerHeldBy.playerBodyAnimator.runtimeAnimatorController = otherPlayerDefaultAnimatorController;
                    SetAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                }
            }
        }
    }

    public void SaveAnimatorStates(Animator animator)
    {
        isCrouching = animator.GetBool("crouching");
        isJumping = animator.GetBool("Jumping");
        isWalking = animator.GetBool("Walking");
        isSprinting = animator.GetBool("Sprinting");
        currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        currentAnimationTime = currentStateInfo.normalizedTime;
    }

    public void SetAnimatorStates(Animator animator)
    {
        animator.Play(currentStateInfo.fullPathHash, 0, currentAnimationTime);
        animator.SetBool("crouching", isCrouching);
        animator.SetBool("Jumping", isJumping);
        animator.SetBool("Walking", isWalking);
        animator.SetBool("Sprinting", isSprinting);
    }
}