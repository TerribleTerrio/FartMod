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

    public float disablePhysicsCooldown;

    [Space(5f)]
    [Header("Game Object References")]
    public GameObject balloon;

    public GameObject balloonCollider;

    public GameObject stringCollider;

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

    private SphereCollider[] itemColliders = [];

    private bool tugging;

    private float popHeight = 55f;

    private float stringGrabRadius = 1f;

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

    private EnemyAI? focusedByEnemy;

    private Vector3 lastDroppedPosition;

    private const int originalItemId = 0;

    private const int baboonHawkUngrabbableId = 1531; //Beehive ID --- Remember to only set this when necessary!

    private float enemyGrabHeight = 8f;

    private bool poppedThisFrame = false;

    private Color[] balloonColors =
    {
        new(0.9f, 0.2f, 0.2f),      //RED
        new(0.4f, 0.9f, 0.3f),      //GREEN
        new(0.95f, 0.95f, 0.4f),    //YELLOW
        new(0.4f, 0.5f, 0.95f),     //BLUE
        new(0.4f, 0.95f, 0.85f),    //CYAN
        new(1f, 0.5f, 0.1f),        //ORANGE
        new(1f, 0.5f, 0.7f),        //PINK
        new(0.6f, 0.1f, 0.8f)       //PURPLE
    };

    private Color balloonColor;

    private int balloonColorIndex = 0;

    private Rigidbody[] balloonStringsPhys = [];

    private Coroutine? shipLandingCoroutine;

    private Coroutine? shipLeavingCoroutine;

    private bool stopSyncingPosition;

    private float syncPositionLerpSpeed = 3f;

    public override void Start()
    {
        //BALLOON START
        balloon.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        balloonCollider.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        stringCollider.transform.SetParent(StartOfRound.Instance.propsContainer, true);

        //INITIALIZE ARRAYS AND ARRAY VALUES
        itemColliders = new SphereCollider[balloonStrings.Length];
        balloonStringsPhys = new Rigidbody[balloonStrings.Length];
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            balloonStrings[i].transform.SetParent(StartOfRound.Instance.propsContainer, true);
            SphereCollider sphereCollider = base.gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = stringGrabRadius;
            itemColliders[i] = sphereCollider;
            balloonStringsPhys[i] = balloonStrings[i].GetComponent<Rigidbody>();
        }
        lineRenderer = base.gameObject.GetComponent<LineRenderer>();
        lineRenderer.positionCount = balloonStrings.Length;
        if (base.IsOwner)
        {
            SetBalloonColor(RPC: true);
        }

        //CHECK FOR WIND AND POP HEIGHT
        lastMoon = StartOfRound.Instance.currentLevel.PlanetName;
        Debug.Log($"[BALLOON]: lastMoon set to {lastMoon}.");
        MoonConditionsCheck();

        //INITIAL POSITION SYNC
        if (base.IsOwner)
        {
            EnableStringPhysics(true);
            EnableBalloonPhysics(true);
            disablePhysicsCooldown = 0.05f;
            balloon.transform.position = base.transform.position + Vector3.up * 0.3f;
            balloonStrings[1].transform.position = base.transform.position + Vector3.up * 0.2f;
            balloonStrings[2].transform.position = base.transform.position + Vector3.up * 0.1f;
            grabString.transform.position = base.transform.position;
            SyncPositionInstantlyServerRpc(base.transform.position);
        }
        else
        {
            EnableStringPhysics(true);
            EnableBalloonPhysics(false);
        }

        balloonServerPosition = balloon.transform.position;
        lastDroppedPosition = base.transform.position;
        itemProperties.itemId = originalItemId;
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

	public void OnEnable()
	{
		StartOfRound.Instance.StartNewRoundEvent.AddListener(StartHandlingShipLanding);
        CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.AddListener(StartHandlingShipLeaving);
	}

	public void OnDisable()
	{
		StartOfRound.Instance.StartNewRoundEvent.RemoveListener(StartHandlingShipLanding);
        CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.AddListener(StartHandlingShipLeaving);
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
            lastMoon = StartOfRound.Instance.currentLevel.PlanetName;
            Debug.Log($"[BALLOON]: Landed on new moon, setting LastMoon to {lastMoon} and checking for wind and height...");
            MoonConditionsCheck();
        }

        if (pushTimer > 0)
        {
            pushTimer -= Time.deltaTime;
        }

        for (int i = 0; i < balloonStringsPhys.Length; i++)
        {
            balloonStringsPhys[i].drag = drag;
        }

        if (!base.IsOwner)
        {
            if (disablePhysicsCooldown > 0f)
            {
                disablePhysicsCooldown -= Time.deltaTime;
            }
            else if (frozen)
            {
                EnableStringPhysics(true);
            }
            if (!stopSyncingPosition)
            {
                balloon.transform.position = Vector3.Lerp(balloon.transform.position, balloonServerPosition, Time.deltaTime * syncPositionLerpSpeed);
            }
        }
        else
        {
            if (disablePhysicsCooldown > 0f)
            {
                disablePhysicsCooldown -= Time.deltaTime;
            }
            else if (frozen)
            {
                EnableStringPhysics(true);
                EnableBalloonPhysics(true);
            }

            //HANDLING TELEPORTING
            if (playerHeldBy != null)
            {
                if (playerHeldBy.teleportingThisFrame)
                {
                    EnableStringPhysics(false);
                    EnableBalloonPhysics(false);
                    disablePhysicsCooldown = 0.05f;
                }

                if (playerHeldBy.teleportedLastFrame)
                {
                    balloon.transform.position = playerHeldBy.localItemHolder.transform.position + Vector3.up * 1f;
                    balloonStrings[1].transform.position = playerHeldBy.localItemHolder.transform.position + Vector3.up * 0.75f;
                    balloonStrings[2].transform.position = playerHeldBy.localItemHolder.transform.position + Vector3.up * 0.25f;
                    grabString.transform.position = playerHeldBy.localItemHolder.transform.position;
                    SyncPositionInstantlyServerRpc(playerHeldBy.localItemHolder.transform.position);
                }
            }

            //HANDLING HEIGHTS
            if (disablePhysicsCooldown <= 0 && !isInShipRoom && !isInElevator && Vector3.Distance(lastDroppedPosition, balloon.transform.position) > popHeight)
            {
                Pop();
            }
            else if (!isInShipRoom && grabbableToEnemies && Vector3.Distance(lastDroppedPosition, balloon.transform.position) > enemyGrabHeight)
            {
                grabbableToEnemies = false;
                itemProperties.itemId = baboonHawkUngrabbableId;
                Debug.Log($"[BALLOON]: Setting grabbableToEnemies: {grabbableToEnemies}, {itemProperties.itemId}");
            }
            else if (!grabbableToEnemies && Vector3.Distance(lastDroppedPosition, balloon.transform.position) < enemyGrabHeight)
            {
                grabbableToEnemies = true;
                itemProperties.itemId = originalItemId;
                Debug.Log($"[BALLOON]: Setting grabbableToEnemies: {grabbableToEnemies}, {itemProperties.itemId}");
            }

            //HANDLING BEING HELD
            if (isHeld || isHeldByEnemy)
            {
                lastDroppedPosition = base.transform.position;

                if (Physics.Linecast(balloon.transform.position, grabString.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault) && Vector3.Distance(balloon.transform.position, grabString.transform.position) > 5f)
                {
                    caughtAudio.volume = Mathf.Lerp(caughtAudio.volume, 1f, Time.deltaTime * 10f);
                }
                else
                {
                    caughtAudio.volume = Mathf.Lerp(caughtAudio.volume, 0f, Time.deltaTime * 10f);
                }

                //DISCARD IF STRETCHED TOO FAR FROM OWNER
                if (disablePhysicsCooldown <= 0 && Vector3.Distance(balloon.transform.position, grabString.transform.position) > 7f)
                {
                    Debug.Log("[BALLOON]: Too far from holder, discarding!");

                    //PLAY SOUND
                    PlaySnapClipAndSync();

                    //DISCARD BALLOON
                    if (playerHeldBy != null)
                    {
                        Debug.Log("[BALLOON]: Too far from player holding, discarding!");
                        playerHeldBy.DiscardHeldObject();
                    }
                    else if (focusedByEnemy != null)
                    {
                        Debug.Log("[BALLOON]: Too far from enemy holding, discarding!");
                        (focusedByEnemy as BaboonBirdAI)?.DropHeldItemAndSync();
                        (focusedByEnemy as HoarderBugAI)?.DropItemAndCallDropRPC((focusedByEnemy as HoarderBugAI)?.heldItem.itemGrabbableObject.GetComponent<NetworkObject>(), false);
                    }
                }
            }
            else
            {
                caughtAudio.volume = Mathf.Lerp(caughtAudio.volume, 0f, Time.deltaTime * 10f);
            }

            //SYNC POSITION FROM OWNER TO CLIENTS ON INTERVAL
            if (syncTimer < syncPositionInterval)
            {
                syncTimer += Time.deltaTime;
            }
            else
            {
                syncTimer = 0f;
                if (!stopSyncingPosition && Vector3.Distance(balloonServerPosition, balloon.transform.position) > syncPositionThreshold)
                {
                    SyncBalloonPositionServerRpc(balloon.transform.position);
                    balloonServerPosition = balloon.transform.position;
                }
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

    private void StartHandlingShipLanding()
    {
        if (shipLandingCoroutine != null)
        {
            StopCoroutine(shipLandingCoroutine);
            shipLandingCoroutine = null;
        }
        shipLandingCoroutine = StartCoroutine(HandleShipLanding());
    }

    private void StartHandlingShipLeaving()
    {
        if (shipLeavingCoroutine != null)
        {
            StopCoroutine(shipLeavingCoroutine);
            shipLeavingCoroutine = null;
        }
        shipLeavingCoroutine = StartCoroutine(HandleShipLeaving());
    }

    private IEnumerator HandleShipLanding()
    {
        yield return null;
        Debug.Log("[BALLOON]: Handling ship landing!");
        if (isInShipRoom)
        {
            yield return new WaitUntil(() => !StartOfRound.Instance.inShipPhase);
            Debug.Log("[BALLOON]: Ship is landing!");
            EnableStringPhysics(false);
            EnableBalloonPhysics(false);
            disablePhysicsCooldown = 15f;
            stopSyncingPosition = true;
            syncPositionLerpSpeed = 6f;
            float timeStart = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsAnimator.GetBool("Closed"));
            yield return new WaitUntil(() => isHeld || isHeldByEnemy || Time.realtimeSinceStartup - timeStart > 15f);
            EnableStringPhysics(true);
            EnableBalloonPhysics(base.IsOwner);
            disablePhysicsCooldown = 0f;
            stopSyncingPosition = false;
            syncPositionLerpSpeed = 3f;
        }
    }

    private IEnumerator HandleShipLeaving()
    {
        yield return null;
        Debug.Log("[BALLOON]: Handling ship leaving!");
        if (isInShipRoom)
        {
            yield return new WaitUntil(() => RoundManager.Instance.playersManager.shipDoorsAnimator.GetBool("Closed"));
            yield return new WaitForSeconds(1f);
            Debug.Log("[BALLOON]: Ship is leaving!");
            EnableStringPhysics(false);
            EnableBalloonPhysics(false);
            disablePhysicsCooldown = 15f;
            stopSyncingPosition = true;
            float timeStart = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => !StartOfRound.Instance.shipDoorsEnabled);
            yield return new WaitUntil(() => isHeld || isHeldByEnemy || Time.realtimeSinceStartup - timeStart > 15f);
            EnableStringPhysics(true);
            EnableBalloonPhysics(base.IsOwner);
            disablePhysicsCooldown = 0f;
            stopSyncingPosition = false;
            syncPositionLerpSpeed = 3f;
        }
    }

    public override int GetItemDataToSave()
    {
        return balloonColorIndex;
    }

    public override void LoadItemSaveData(int saveData)
    {
        StartCoroutine(WaitToLoadSaveData(saveData));
    }

    private IEnumerator WaitToLoadSaveData(int saveData)
    {
        yield return new WaitForSeconds(3f);
        {
            SetBalloonColor(saveData);
            ParentBalloonToShip(true);
        }
    }

    public void SetBalloonColor(int index = -1, bool RPC = false)
    {
        if (index != -1)
        {
            balloonColorIndex = index;
        }
        else
        {
            int common = UnityEngine.Random.Range(0, 4);
            int uncommon = UnityEngine.Random.Range(4, 6);
            int rare = UnityEngine.Random.Range(6, balloonColors.Length);
            float rareChance = 2f;
            float uncommonChance = 20f;
            float random = UnityEngine.Random.Range(0f, 100f);
            balloonColorIndex = (random < rareChance) ? rare : (random < uncommonChance) ? uncommon : common;
        }
        balloonColor = balloonColors[balloonColorIndex];
        balloon.GetComponent<Renderer>().material.color = balloonColor;
        if (RPC)
        {
            SetBalloonColorServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, balloonColorIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetBalloonColorServerRpc(int clientWhoSentRpc, int index = -1)
    {
        SetBalloonColorClientRpc(clientWhoSentRpc, index);
    }

    [ClientRpc]
    public void SetBalloonColorClientRpc(int clientWhoSentRpc, int index = -1)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            SetBalloonColor(index);
        }
    }

    public void MoonConditionsCheck()
    {
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy)
        {
            Debug.Log("[BALLOON]: Current weather is stormy.");
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
        popHeight = StartOfRound.Instance.currentLevel.levelID switch
        {
            0 => 60f,   //Experimentation
            1 => 60f,   //Assurance
            2 => 40f,   //Vow
            8 => 60f,   //Offense
            4 => 40f,   //March
            5 => 60f,   //Adamance
            6 => 40f,   //Rend
            7 => 40f,   //Dine
            9 => 70f,   //Titan
            10 => 50f,  //Artifice
            12 => 50f,  //Embrion
            3 => 40f,   //The Company Building
            _ => 55f    //Default
        };
        Debug.Log($"[BALLOON]: Pop height for this moon is {popHeight}!");
    }

    public void OnStringTouch(Collider other)
    {
        if (base.IsOwner && !isHeld && !isHeldByEnemy && other.gameObject.layer == 19 && other.gameObject.TryGetComponent<EnemyAICollisionDetect>(out var enemycollider) && enemycollider.mainScript != null)
        {
            switch (enemycollider.mainScript)
            {
                case BaboonBirdAI baboonBird:
                    if (baboonBird.focusedScrap == this && Vector3.Distance(base.transform.position, enemycollider.mainScript.transform.position) < 8f)
                    {
                        focusedByEnemy = baboonBird;
                    }
                    break;

                case HoarderBugAI hoarderBug:
                    if (hoarderBug.targetItem == this && Vector3.Distance(base.transform.position, enemycollider.mainScript.transform.position) < 8f)
                    {
                        focusedByEnemy = hoarderBug;
                    }
                    break;
            }
        }
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

        //PARENT TO SHIP
        if (base.IsOwner && disablePhysicsCooldown <= 0f)
        {
            if (isInElevator || playerHeldBy != null && playerHeldBy.isInElevator || StartOfRound.Instance.shipBounds.bounds.Contains(base.transform.position) || StartOfRound.Instance.shipBounds.bounds.Contains(grabString.transform.position))
            {
                if (base.transform.parent != StartOfRound.Instance.elevatorTransform)
                {
                    ParentBalloonToShip(true);
                    Debug.Log("[BALLOON]: Owner sending ParentBalloonToShipServerRpc() (Parenting to ship!)");
                    ParentBalloonToShipServerRpc(true);
                }
            }
            else
            {
                if (base.transform.parent == StartOfRound.Instance.elevatorTransform)
                {
                    ParentBalloonToShip(false);
                    Debug.Log("[BALLOON]: Owner sending ParentBalloonToShipServerRpc() (Parenting to propsContainer!)");
                    ParentBalloonToShipServerRpc(false);
                }
            }
        }

        //SET STRING TO HAND IF OBJECT IS HELD BY PLAYER
        if (parentObject != null)
        {
			Vector3 positionOffset = itemProperties.positionOffset;
            grabString.transform.position = parentObject.transform.position;
            positionOffset = parentObject.transform.rotation * positionOffset;
			grabString.transform.position += positionOffset;
        }

        //SET BASE ACCESSIBLE TO GRABBING ENEMIES
        if (focusedByEnemy == null)
        {
            base.transform.position = grabString.transform.position;
        }
        else if (!isHeldByEnemy)
        {
            base.transform.position = focusedByEnemy.transform.position;
        }
        else
        {
            base.transform.position = focusedByEnemy switch
            {
                BaboonBirdAI bird => bird.grabTarget.transform.position,
                HoarderBugAI bug => bug.grabTarget.transform.position,
                _ => focusedByEnemy.transform.position
            };
        }
        scanNode.transform.position = balloon.transform.position;
        balloonCollider.transform.position = balloon.transform.position;
        stringCollider.transform.position = grabString.transform.position;

        //SET POSITIONS OF GRAB COLLIDERS
        for (int i = 0; i < itemColliders.Length; i++)
        {
            itemColliders[i].center = base.transform.InverseTransformPoint(balloonStrings[i].transform.position);
        }

        //SET LINE RENDERER POSITIONS
        SubdivideStrings(subdivisions: 3);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ParentBalloonToShipServerRpc(bool parentToShip)
    {
        ParentBalloonToShipClientRpc(parentToShip);
    }

    [ClientRpc]
    public void ParentBalloonToShipClientRpc(bool parentToShip)
    {
        if (!base.IsOwner)
        {
            Debug.Log($"[BALLOON]: Non-owner receiving ParentBalloonToShipClientRpc() (Parenting: {parentToShip})");
            ParentBalloonToShip(parentToShip);
        }
    }

    public void ParentBalloonToShip(bool parentToShip)
    {
        if (parentToShip && base.transform.parent != StartOfRound.Instance.elevatorTransform)
        {
            base.transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
        }
        else if (!parentToShip)
        {
            base.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        }
        for (int i = 0; i < balloonStrings.Length; i++)
        {
            if (parentToShip && balloonStrings[i].transform.parent != StartOfRound.Instance.elevatorTransform)
            {
                balloonStrings[i].transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
            }
            else if (!parentToShip)
            {
                balloonStrings[i].transform.SetParent(StartOfRound.Instance.propsContainer, true);
            }
        }
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

    public void EnableStringPhysics(bool enable)
    {
        Debug.Log($"[BALLOON]: {(enable ? "Enabling" : "Disabling")} physics on strings!");
        for (int i = 1; i < balloonStringsPhys.Length; i++)
        {
            balloonStringsPhys[i].isKinematic = !enable;
        }
        frozen = !enable;
    }

    public void EnableBalloonPhysics(bool enable)
    {
        Debug.Log($"[BALLOON]: {(enable ? "Enabling" : "Disabling")} physics on balloon!");
        balloon.GetComponent<Rigidbody>().isKinematic = !enable;
        balloon.GetComponent<Collider>().enabled = enable;
        balloon.GetComponent<Rigidbody>().isKinematic = !enable;
        balloon.GetComponent<Collider>().enabled = enable;
    }

    public new void EnablePhysics(bool enable)
    {
        EnableBalloonPhysics(enable);
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

	public override void PocketItem()
	{
		if (base.IsOwner && playerHeldBy != null)
		{
			playerHeldBy.IsInspectingItem = false;
		}
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
        lastDroppedPosition = base.transform.position;
    }

    public override void DiscardItemFromEnemy()
    {
        focusedByEnemy = null;
        lastDroppedPosition = base.transform.position;
    }

    public override void OnDestroy()
    {
        if (itemProperties.itemId != originalItemId)
        {
            itemProperties.itemId = originalItemId;
        }
        DestroyBalloon();
        base.OnDestroy();
    }

    public void DampFloating()
    {
        Debug.Log("[BALLOON]: Damping physics!");

        for (int i = 0; i < balloonStringsPhys.Length; i++)
        {
            balloonStringsPhys[i].velocity = new Vector3(0f,0f,0f);
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

        if (frozen || Physics.Linecast(transform.position, other.transform.position, out _, CoronaMod.Masks.RoomVehicle, QueryTriggerInteraction.Ignore))
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

            switch (enemy.mainScript)
            {
                case FlowermanAI:
                    PushBalloon(otherObject.transform.position, 8f);
                    break;

                case SpringManAI:
                    PushBalloon(otherObject.transform.position, 20f);
                    break;

                case JesterAI jester:
                    if (jester.creatureAnimator.GetBool("poppedOut"))
                    {
                        Pop();
                    }
                    else
                    {
                        PushBalloon(otherObject.transform.position, 8f);
                    }
                    break;

                case CaveDwellerAI caveDweller:
                    if (caveDweller.adultContainer.activeSelf)
                    {
                        PushBalloon(otherObject.transform.position, 20f);
                    }
                    break;

                case MaskedPlayerEnemy:
                    PushBalloon(otherObject.transform.position, 10f);
                    break;

                case CrawlerAI crawler:
                    if (crawler.hasEnteredChaseMode)
                    {
                        Pop();
                    }
                    else
                    {
                        PushBalloon(otherObject.transform.position, 20f);
                    }
                    break;

                case RedLocustBees:
                    Pop();
                    break;

                case SandWormAI:
                    Pop();
                    break;

                case MouthDogAI mouthDog:
                    if (mouthDog.hasEnteredChaseModeFully)
                    {
                        Pop();
                    }
                    else
                    {
                        PushBalloon(otherObject.transform.position, 20f);
                    }
                    break;

                case ForestGiantAI:
                    PushBalloon(otherObject.transform.position, 30f);
                    break;

                case DoublewingAI doublewing:
                    if (doublewing.creatureAnimator.GetBool("flying"))
                    {
                        Pop();
                    }
                    break;

                case DocileLocustBeesAI:
                    Pop();
                    break;

                case FlowerSnakeEnemy flowerSnake:
                    if (flowerSnake.leaping)
                    {
                        Pop();
                    }
                    break;

                case CentipedeAI centipede:
                    if (centipede.triggeredFall)
                    {
                        Pop();
                    }
                    break;

                case RadMechAI radMech:
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
                    break;
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
        else if (otherObject.TryGetComponent<BalloonCollisionDetection>(out var balloonCollision) && balloonCollision.mainScript != this)
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
        if (poppedThisFrame)
        {
            return;
        }
        poppedThisFrame = true;
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
        GameObject popParticleObj = Instantiate(popPrefab, balloon.transform.position, Quaternion.identity).transform.Find("MainBlast/ColorParticles").gameObject;
        var popParticleModule = popParticleObj.GetComponent<ParticleSystem>().main;
        var popRendererModule = popParticleObj.GetComponent<ParticleSystemRenderer>();
        popParticleModule.startColor = balloonColor;
        popRendererModule.material.color = balloonColor;

        //PLAY AUDIBLE NOISE
        RoundManager.Instance.PlayAudibleNoise(balloon.transform.position, noiseRange: 17f, noiseLoudness: 1f);
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
        Destroy(stringCollider);
    }



    //SYNCING POSITIONS
    [ServerRpc(RequireOwnership = false)]
    private void SyncPositionInstantlyServerRpc(Vector3 balloonPos)
    {
        SyncPositionInstantlyClientRpc(balloonPos);
    }

    [ClientRpc]
    private void SyncPositionInstantlyClientRpc(Vector3 balloonPos)
    {
        if (!base.IsOwner)
        {
            EnableStringPhysics(false);
            EnableBalloonPhysics(false);
            balloonServerPosition = balloonPos + Vector3.up * 0.3f;
            balloon.transform.position = balloonPos + Vector3.up * 0.3f;
            balloonStrings[1].transform.position = balloonPos + Vector3.up * 0.2f;
            balloonStrings[2].transform.position = balloonPos + Vector3.up * 0.1f;
            grabString.transform.position = balloonPos;
            base.transform.position = balloonPos;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncBalloonPositionServerRpc(Vector3 balloonPos)
    {
        SyncBalloonPositionClientRpc(balloonPos);
    }

    [ClientRpc]
    private void SyncBalloonPositionClientRpc(Vector3 balloonPos)
    {
        if (!base.IsOwner)
        {
            balloonServerPosition = balloonPos;
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