using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Fridge : NetworkBehaviour
{
    [Header("Opening & Closing")]
    public Animator fridgeAnimator;

    public Collider[] magnetSurfaces;

    public Collider[] openTriggers;

    public Collider insideBounds;

    public TriggerScript collisionScript;

    public NavMeshObstacle navMeshObstacle;

    public NavMeshSurface navMeshSurface;

    public OffMeshLink navMeshLink;

    public AudioSource fridgeAudio;

    public AudioClip[] slamDoorClips;

    [Space(5f)]
    [Header("Magnet Placement")]
    private bool initialized;

    public List<FridgeMagnet> placedMagnets = [];

    private int assignedID = 1;

    public struct MagnetList(List<int> MagnetIDs, List<int> MagnetParents, List<Vector3> MagnetPositions)
    {
        public List<int> IDs = MagnetIDs;

        public List<int> Parents = MagnetParents;

        public List<Vector3> Positions = MagnetPositions;

        public readonly void AddMagnet(int ID, int parent, Vector3 pos)
        {
            IDs.Add(ID);
            Parents.Add(parent);
            Positions.Add(pos);
        }

        public readonly void RemoveMagnet(int ID, int parent, Vector3 pos)
        {
            IDs.Remove(ID);
            Parents.Remove(parent);
            Positions.Remove(pos);
        }

        public readonly void RemoveAt(int ID)
        {
            IDs.RemoveAt(ID);
            Parents.RemoveAt(ID);
            Positions.RemoveAt(ID);
        }
    }

    public static MagnetList savedMagnets = new([], [], []);

    public static string magnetsKey = "magnets";

    public static string currentSaveFile = "FridgeSave";

    [Space(5f)]
    [Header("Haunted Words")]

    [Range(0f,100f)]
    public float hauntChance;

    public float hauntInterval = 20f;

    public string[] hauntedWords;

    public GameObject hauntedWordPosition;

    public float fridgeWidth = 2f;

    private float beginHauntTime;

    private bool haunted;

    private float lastHauntCheckTime;

    private bool fridgeLightsOn;

    private bool hideOutsideMeshes;

    private bool hideInsideMeshes;

    private float forceOpenDoorTimer;

    private bool enemyOpeningDoor;

    private float forceOpenDoorTimeLimit;

    public (string Tip, Sprite Icon) previousHoverUI;

    private Transform? _itemContainer;

    public Transform ItemContainer
    {
        get
        {
            _itemContainer ??= insideBounds.GetComponent<PlayerPhysicsRegion>().physicsTransform;
            return _itemContainer;
        }
    }

    public bool DoorOpen => fridgeAnimator.GetBool("fridgeOpen");

    public bool DoorClosed => !fridgeAnimator.GetBool("fridgeOpen");

    public struct EnemyInsideFridge (EnemyAI AI, EnemyAICollisionDetect collision, Vector3 colliderSize)
    {
        public EnemyAI enemyAI = AI;

        public EnemyAICollisionDetect enemyCollision = collision;

        public Vector3 originalSize = colliderSize;

        public readonly float ExitFridgeTime => StartOfRound.Instance.allPlayerScripts.Count(player => player.isInHangarShipRoom) switch { 1 => 3.8f, 2 => 6f, 3 => 9f, _ => 11f };

        public bool Hiding { get; private set; }

        public void HideInFridge(bool hide)
        {
            Hiding = hide;
            enemyAI.EnableEnemyMesh(!hide);
            enemyCollision.GetComponent<BoxCollider>().size = hide ? new Vector3(0.01f, 0.01f, 0.01f) : originalSize;
        }
    }

    public EnemyInsideFridge? enemyInsideFridge;

    public bool LocalPlayerHidingInPridge => DoorClosed && insideBounds.bounds.Contains(GameNetworkManager.Instance.localPlayerController.transform.position);

    public bool PlayersSeeFridge => StartOfRound.Instance.allPlayerScripts.Any(player => player.isPlayerControlled && !player.isPlayerDead && player.HasLineOfSightToPosition(hauntedWordPosition.transform.position));

    public bool PlayersInsideShip => StartOfRound.Instance.allPlayerScripts.Any(player => player.isInHangarShipRoom);

    public static Fridge? Instance { get; private set; }

	public void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Destroy(Instance.gameObject);
		}
	}

    public void Start()
    {
        OnEnable();
    }

    public void OnEnable()
    {
        EnableMagnetSurfaces(false);
        if (base.IsHost)
        {
            FridgeMagnet[] magnetsInScene = FindObjectsOfType<FridgeMagnet>();
            StartCoroutine(WaitToLoadSaveData(magnetsInScene));
            StartOfRound.Instance.StartNewRoundEvent.AddListener(ResetHaunt);
            CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.AddListener(ResetHaunt);
        }
    }

    public void OnDisable()
    {
        if (!GameNetworkManager.Instance.isDisconnecting)
        {
            RemoveAllPlacedMagnets(andChildren: true);
        }
        enemyInsideFridge?.HideInFridge(false);
        if (base.IsHost)
        {
            StartOfRound.Instance.StartNewRoundEvent.RemoveListener(ResetHaunt);
            CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.RemoveListener(ResetHaunt);
        }
    }

    public void ResetHaunt()
    {
        haunted = false;
    }

    public void Update()
    {
        if (base.IsHost)
        {

            if (StartOfRound.Instance.shipHasLanded && beginHauntTime == 0f)
            {
                beginHauntTime = UnityEngine.Random.Range(0.3f, 0.7f);
            }

            if (StartOfRound.Instance.shipIsLeaving && beginHauntTime != 0f)
            {
                beginHauntTime = 0f;
            }

            if (!haunted && Time.realtimeSinceStartup - lastHauntCheckTime > hauntInterval)
            {
                hauntInterval = UnityEngine.Random.Range(15f, 40f);
                lastHauntCheckTime = Time.realtimeSinceStartup;
                if (RoundManager.Instance.SpawnedEnemies.Any(enemy => enemy is DressGirlAI))
                {
                    haunted = true;
                }
            }

            if (StartOfRound.Instance.shipHasLanded && TimeOfDay.Instance.normalizedTimeOfDay > beginHauntTime && Time.realtimeSinceStartup - lastHauntCheckTime > hauntInterval)
            {
                hauntInterval = UnityEngine.Random.Range(15f, 40f);
                lastHauntCheckTime = Time.realtimeSinceStartup;
                if (UnityEngine.Random.Range(0f, 100f) < (haunted ? hauntChance * 4f : hauntChance))
                {
                    if (UnityEngine.Random.Range(0, 2) == 0 && !PlayersSeeFridge)
                    {
                        SpellHauntedWord();
                    }
                    else
                    {
                        OpenRandomDoor();
                    }
                }
            }
        }

        if (!base.IsHost && !initialized)
        {
            if (GameNetworkManager.Instance.localPlayerController != null)
            {
                Debug.Log("[FRIDGE]: Attempting to sync magnets from host.");
                SyncPlacedMagnetsFromHostServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                initialized = true;
            }
        }

        if (StartOfRound.Instance.shipRoomLights.areLightsOn == fridgeLightsOn)
        {
            fridgeLightsOn = !StartOfRound.Instance.shipRoomLights.areLightsOn;
            fridgeAnimator.SetBool("lightsOn", fridgeLightsOn);
        }

		if (GameNetworkManager.Instance.localPlayerController != null)
        {
            if (GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer is FridgeMagnet && !magnetSurfaces[0].enabled)
            {
                EnableMagnetSurfaces(true);
            }
            else if (GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer is not FridgeMagnet && magnetSurfaces[0].enabled)
            {
                EnableMagnetSurfaces(false);
            }

            if (!hideInsideMeshes && DoorClosed)
            {
                hideInsideMeshes = true;
                HideMeshes(outsideMeshes: false, hide: true, delay: 0.3f);
            }
            else if (hideInsideMeshes && DoorOpen)
            {
                hideInsideMeshes = false;
                HideMeshes(outsideMeshes: false, hide: false);
            }

            if (!hideOutsideMeshes && LocalPlayerHidingInPridge)
            {
                hideOutsideMeshes = true;
                HideMeshes(outsideMeshes: true, hide: true, delay: 0.3f);
            }
            else if (hideOutsideMeshes && !LocalPlayerHidingInPridge)
            {
                hideOutsideMeshes = false;
                HideMeshes(outsideMeshes: true, hide: false);
            }
        }

        if ((StartOfRound.Instance.inShipPhase || StartOfRound.Instance.shipIsLeaving || !StartOfRound.Instance.shipHasLanded) && collisionScript.enabled)
        {
            collisionScript.enabled = false;
        }
        else if (!StartOfRound.Instance.inShipPhase && !StartOfRound.Instance.shipIsLeaving && StartOfRound.Instance.shipHasLanded && !collisionScript.enabled)
        {
            collisionScript.enabled = true;
        }

        if (!enemyOpeningDoor && forceOpenDoorTimer > 0f)
        {
            forceOpenDoorTimer -= Time.deltaTime;
        }
    }

    public void HideMeshes(bool outsideMeshes, bool hide, float delay = 0f)
    {
        StartCoroutine(HideMeshesWithDelay(outsideMeshes, hide, delay));
    }

    public IEnumerator HideMeshesWithDelay(bool outsideMeshes, bool hide, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (outsideMeshes)
        {
            if (RoundManager.Instance.numberOfEnemiesInScene > 0)
            {
                foreach(EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
                {
                    enemy.EnableEnemyMesh(!hide);
                }
                EnableNavMeshObstacleServerRpc(hide);
                navMeshSurface.enabled = !hide;
                navMeshLink.enabled = !hide;
            }
            if (hide)
            {
                previousHoverUI = (openTriggers[1].GetComponent<InteractTrigger>().hoverTip, openTriggers[1].GetComponent<InteractTrigger>().hoverIcon);
            }
            openTriggers[1].GetComponent<InteractTrigger>().hoverTip = hide ? "" : previousHoverUI.Tip;
            openTriggers[1].GetComponent<InteractTrigger>().hoverIcon = hide ? openTriggers[1].GetComponent<InteractTrigger>().disabledHoverIcon : previousHoverUI.Icon;
            openTriggers[1].GetComponent<InteractTrigger>().timeToHold = hide ? UnityEngine.Random.Range(1f, 5f) : 0.5f;
            HUDManager.Instance.holdInteractionCanvasGroup.gameObject.SetActive(!hide);
        }
        else
        {
            foreach (Transform child in ItemContainer)
            {
                if (child.TryGetComponent(out PlayerControllerB player) && player != GameNetworkManager.Instance.localPlayerController)
                {
                    player.DisablePlayerModel(player.gameObject, enable: !hide, disableLocalArms: true);
                }
                else if (child.TryGetComponent(out GrabbableObject gObject))
                {
                    gObject.EnableItemMeshes(enable: !hide);
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void EnableNavMeshObstacleServerRpc(bool enable, float delay = 0f)
    {
        EnableNavMeshObstacleClientRpc(enable, delay);
    }

    [ClientRpc]
    public void EnableNavMeshObstacleClientRpc(bool enable, float delay = 0f)
    {
        StartCoroutine(EnableNavMeshObstacleWithDelay(enable, delay));
    }

    public IEnumerator EnableNavMeshObstacleWithDelay(bool enable, float delay = 0f)
    {
        yield return new WaitForSeconds(delay);
        foreach (Transform child in navMeshObstacle.transform)
        {
            child.GetComponent<NavMeshObstacle>().enabled = enable;
        }
        navMeshObstacle.enabled = enable;
        Debug.Log($"[FRIDGE]: {(enable ? "Enabled" : "Disabled")} nav mesh obstacle!");
    }

    public bool PlayerInsideFridge(out PlayerControllerB player)
    {
        PlayerControllerB? firstPlayer = StartOfRound.Instance.allPlayerScripts.FirstOrDefault(player => insideBounds.bounds.Contains(player.transform.position));
        player = firstPlayer;
        return player != null;
    }



    //CHECKING AND LOADING SAVE DATA
    private IEnumerator WaitToLoadSaveData(FridgeMagnet[] magnetsInScene)
    {
        yield return new WaitForEndOfFrame();
        bool fullyLoaded = false;
        while (!fullyLoaded)
        {
            fullyLoaded = true;
            for (int i = 0; i < magnetsInScene.Length; i++)
            {
                if (!magnetsInScene[i].loadedData)
                {
                    fullyLoaded = false;
                    break;
                }
            }
        }
        LoadSaveData(magnetsInScene);
    }

    private void LoadSaveData(FridgeMagnet[] magnetsInScene)
    {
        currentSaveFile = GameNetworkManager.Instance.saveFileNum switch
        {
            0 => CoronaMod.Info.SaveFileName1,
            1 => CoronaMod.Info.SaveFileName2,
            2 => CoronaMod.Info.SaveFileName3,
            _ => CoronaMod.Info.SaveFileName1
        };
        if (ES3.KeyExists(magnetsKey, currentSaveFile))
        {
            Debug.Log("[FRIDGE]: Magnet save data found!");
            savedMagnets = ES3.Load<MagnetList>(magnetsKey, currentSaveFile);
            int largestID = 0;
            for (int i = 0; i < savedMagnets.IDs.Count; i++)
            {
                if (savedMagnets.IDs[i] > largestID)
                {
                    largestID = savedMagnets.IDs[i];
                }
            }
            assignedID = largestID + 1;
            HashSet<int> savedMagnetIds = [.. savedMagnets.IDs];
            for (int i = 0; i < magnetsInScene.Length; i++)
            {
                if (savedMagnetIds.Contains(magnetsInScene[i].fridgeID))
                {
                    int index = savedMagnets.IDs.IndexOf(magnetsInScene[i].fridgeID);
                    Debug.Log($"[FRIDGE]: Found matching id for scene magnet {i}! Placing on fridge.");
                    PlaceMagnet(magnetsInScene[i], savedMagnets.Positions[index], savedMagnets.Parents[index]);
                }
            }
        }
        initialized = true;
    }

    public void SaveData()
    {
        RefreshPlacedMagnets();
        HashSet<int> placedMagnetIDs = [.. placedMagnets.Select(magnet => magnet.fridgeID)];
        for (int i = savedMagnets.IDs.Count - 1; i >= 0; i--)
        {
            if (!placedMagnetIDs.Contains(savedMagnets.IDs[i]))
            {
                savedMagnets.RemoveAt(i);
            }
        }
        ES3.Save(magnetsKey, savedMagnets, currentSaveFile);
        Debug.Log($"[FRIDGE]: Saved new data! ({currentSaveFile})");
        for (int i = 0; i < savedMagnets.IDs.Count; i++)
        {
            Debug.Log($"[FRIDGE]: ID: {savedMagnets.IDs[i]}");
            Debug.Log($"[FRIDGE]: POS: {savedMagnets.Positions[i]}");
            Debug.Log($"[FRIDGE]: PARENT: {savedMagnets.Parents[i]}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncPlacedMagnetsFromHostServerRpc(int playerId)
    {
        Debug.Log($"[FRIDGE]: Host syncing placed magnets to player {StartOfRound.Instance.allPlayerScripts[playerId].playerUsername}.");
        for (int i = 0; i < placedMagnets.Count; i++)
        {
            FridgeMagnet magnet = placedMagnets[i];
            Debug.Log($"[FRIDGE]: placeMagnets: {i}, letter: {magnet.letter}, ID: {magnet.fridgeID}, meshVariant: {magnet.meshVariant}");

            //GET MATCHING PARENT
            int index = 0;
            for (int j = 0; j < savedMagnets.IDs.Count; j++)
            {
                if (magnet.fridgeID == savedMagnets.IDs[j])
                {
                    index = j;
                    break;
                }
            }

            NetworkObjectReference magnetRef = magnet.gameObject.GetComponent<NetworkObject>();
            SyncPlacedMagnetClientRpc(magnetRef, magnet.transform.localPosition, savedMagnets.Parents[index], playerId);
        }
    }

    [ClientRpc]
    private void SyncPlacedMagnetClientRpc(NetworkObjectReference magnetRef, Vector3 localPos, int parent, int playerId)
    {
        if (playerId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            FridgeMagnet magnet = ((NetworkObject)magnetRef).gameObject.GetComponent<FridgeMagnet>();
            Debug.Log($"[FRIDGE]: Client receiving magnet, letter: {magnet.letter}, ID: {magnet.fridgeID}");
            PlaceMagnet(magnet, localPos, parent);
        }
    }



    //TOGGLING INTERACT TRIGGERS
    public void EnableMagnetSurfaces(bool enable)
    {
        for (int i = 0; i < magnetSurfaces.Length; i++)
        {
            magnetSurfaces[i].enabled = enable;
        }
        for (int i = 0; i < openTriggers.Length; i++)
        {
            openTriggers[i].enabled = !enable;
        }
        StartCoroutine(EnableMagnetCollidersAfterFrame(!enable));
    }

    private IEnumerator EnableMagnetCollidersAfterFrame(bool enable)
    {
        yield return new WaitForEndOfFrame();
        RefreshPlacedMagnets();
        for (int i = 0; i < placedMagnets.Count; i++)
        {
            placedMagnets[i].gameObject.GetComponent<Collider>().enabled = enable;
        }
    }



    //PLACING & REMOVING MAGNETS
    public void PlaceHeldMagnet()
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        FridgeMagnet fridgeMagnet;

        //GET HELD FRIDGEMAGNET
        if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.gameObject.GetComponent<FridgeMagnet>() != null)
        {
            fridgeMagnet = player.currentlyHeldObjectServer.gameObject.GetComponent<FridgeMagnet>();
        }
        else
        {
            Debug.LogError("[FRIDGE]: PlaceHeldMagnet called while not holding magnet!");
            return;
        }

        //SEARCH FOR COLLIDER
        if (Physics.Raycast(player.gameplayCamera.transform.position, player.gameplayCamera.transform.forward, out RaycastHit hit, 4f, player.interactableObjectsMask, QueryTriggerInteraction.Collide))
        {
            for (int i = 0; i < magnetSurfaces.Length; i++)
            {
                if (magnetSurfaces[i] == hit.collider)
                {
                    //SET PARENT INDEX OF COLLIDER
                    Vector3 setPosition = magnetSurfaces[i].transform.InverseTransformPoint(hit.point + hit.collider.gameObject.transform.forward * fridgeMagnet.itemProperties.verticalOffset);
                    NetworkObjectReference magnetRef = fridgeMagnet.gameObject.GetComponent<NetworkObject>();
                    PlaceMagnet(fridgeMagnet, setPosition, i);
                    PlaceMagnetServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, magnetRef, setPosition, i);
                    break;
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlaceMagnetServerRpc(int clientWhoSentRpc, NetworkObjectReference magnetRef, Vector3 localPos, int parent, bool anim = false)
    {
        PlaceMagnetClientRpc(clientWhoSentRpc, magnetRef, localPos, parent, anim);
    }

    [ClientRpc]
    public void PlaceMagnetClientRpc(int clientWhoSentRpc, NetworkObjectReference magnetRef, Vector3 localPos, int parent, bool anim = false)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            PlaceMagnet(((NetworkObject)magnetRef).gameObject.GetComponent<FridgeMagnet>(), localPos, parent, anim);
        }
    }

    public void PlaceMagnet(FridgeMagnet magnet, Vector3 localPos, int parent, bool anim = false)
    {
        if (placedMagnets.Contains(magnet))
        {
            return;
        }
        Debug.Log($"[FRIDGE]: Placing magnet with letter {magnet.letter}!");

        placedMagnets.Add(magnet);

        if (magnet.playerHeldBy != null)
        {
            PlayerControllerB player = magnet.playerHeldBy;
            
            //DISCARD MAGNET BEFORE PLACING ON FRIDGE
            magnet.isHeld = false;
            magnet.isPocketed = false;
            magnet.heldByPlayerOnServer = false;
            magnet.playerHeldBy = null;

            player.SetSpecialGrabAnimationBool(setTrue: false, player.currentlyHeldObjectServer);
            player.playerBodyAnimator.SetBool("cancelHolding", value: true);
            player.playerBodyAnimator.SetTrigger("Throw");
            HUDManager.Instance.itemSlotIcons[player.currentItemSlot].enabled = false;
            HUDManager.Instance.holdingTwoHandedItem.enabled = false;

            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                if (player.ItemSlots[i] == magnet)
                {
                    player.ItemSlots[i] = null;
                }
            }
            player.twoHanded = false;
            player.twoHandedAnimation = false;
            player.carryWeight = Mathf.Clamp(player.carryWeight - (magnet.itemProperties.weight - 1f), 1f, 10f);
            player.isHoldingObject = false;
            player.hasThrownObject = true;
            player.currentlyHeldObject = null;
            player.currentlyHeldObjectServer = null;

            magnet.DiscardItemOnClient();
        }

        magnet.EnablePhysics(enable: true);
        magnet.EnableItemMeshes(enable: true);
        magnet.parentObject = null;
        magnet.transform.localScale = magnet.originalScale;

        StartCoroutine(SetMagnetTransformsAfterFrame(magnet, localPos, parent, anim));

        if (base.IsHost)
        {
            if (!savedMagnets.IDs.Contains(magnet.fridgeID))
            {
                //ASSIGN ID AND INCREMENT
                magnet.fridgeID = assignedID;
                assignedID++;

                //ADD MAGNET TO SAVE DATA
                savedMagnets.AddMagnet(magnet.fridgeID, parent, localPos);
            }
        }
    }

    public IEnumerator SetMagnetTransformsAfterFrame(FridgeMagnet magnet, Vector3 localPos, int parent, bool anim = false)
    {
        if (!anim)
        {
            magnet.parentObject = null;
            magnet.transform.SetParent(magnetSurfaces[parent].transform, worldPositionStays: false);
            magnet.transform.localPosition = localPos;
            magnet.targetFloorPosition = localPos;
            magnet.fallTime = 1.1f;
            yield return new WaitForEndOfFrame();
            magnet.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        }
        else
        {
            magnet.parentObject = null;
            magnet.transform.SetParent(magnetSurfaces[parent].transform, worldPositionStays: true);
            magnet.targetFloorPosition = magnet.transform.localPosition;
            magnet.fallTime = 1.1f;
            float timeElapsed = 0f;
            float duration = 1.5f;
            Vector3 startRot = magnet.transform.localEulerAngles;
            Vector3 endRot = new(90f, 0f, 0f);
            Vector3 startPos = magnet.transform.localPosition;
            Vector3 endPos = localPos;
            while (timeElapsed < duration)
            {
                timeElapsed += Time.deltaTime;
                magnet.transform.localPosition = Vector3.Lerp(startPos, endPos, StartOfRound.Instance.objectFallToGroundCurveNoBounce.Evaluate(timeElapsed / duration));
                magnet.transform.localEulerAngles = Vector3.Lerp(startRot, endRot, StartOfRound.Instance.objectFallToGroundCurveNoBounce.Evaluate(timeElapsed / duration));
                yield return null;
            }
            magnet.transform.localPosition = localPos;
            magnet.targetFloorPosition = localPos;
            magnet.fallTime = 1.1f;
            yield return new WaitForEndOfFrame();
            magnet.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveMagnetServerRpc(NetworkObjectReference magnetRef)
    {
        RemoveMagnetClientRpc(magnetRef);
    }

    [ClientRpc]
    public void RemoveMagnetClientRpc(NetworkObjectReference magnetRef)
    {
        RemoveMagnet(((NetworkObject)magnetRef).gameObject.GetComponent<FridgeMagnet>());
    }

    public void RemoveMagnet(FridgeMagnet magnet)
    {
        if (!placedMagnets.Contains(magnet))
        {
            return;
        }
        Debug.Log($"[FRIDGE]: Removing magnet with letter {magnet.letter}!");

        placedMagnets.Remove(magnet);

        if (!magnet.heldByPlayerOnServer)
        {
            magnet.transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
            magnet.startFallingPosition = magnet.transform.parent.InverseTransformPoint(magnet.transform.position);
            magnet.FallToGround();
            magnet.targetFloorPosition += new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0f, UnityEngine.Random.Range(-0.2f, 0.2f));
            magnet.fallTime = UnityEngine.Random.Range(-0.4f, 0.05f);
        }

        if (base.IsHost)
        {
            for (int i = 0; i < savedMagnets.IDs.Count; i++)
            {
                if (savedMagnets.IDs[i] == magnet.fridgeID)
                {
                    magnet.fridgeID = 0;

                    //UPDATE SAVE DATA
                    savedMagnets.RemoveMagnet(savedMagnets.IDs[i], savedMagnets.Parents[i], savedMagnets.Positions[i]);
                }
            }
        }
    }

    private void RefreshPlacedMagnets()
    {
        for (int i = placedMagnets.Count - 1; i >= 0; i--)
        {
            if (placedMagnets[i] == null)
            {
                placedMagnets.RemoveAt(i);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveAllPlacedMagnetsServerRpc(int clientWhoSentRpc)
    {
        RemoveAllPlacedMagnetsClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    public void RemoveAllPlacedMagnetsClientRpc(int clientWhoSentRpc) 
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            RemoveAllPlacedMagnets();
        }
    }

    public void RemoveAllPlacedMagnets(bool andChildren = false)
    {
        RefreshPlacedMagnets();
        for (int i = placedMagnets.Count - 1; i >= 0; i--)
        {
            RemoveMagnet(placedMagnets[i]);
        }
        if (!andChildren)
        {
            return;
        }
        foreach (Transform child in ItemContainer)
        {
            if (child.TryGetComponent(out NetworkObject netObj))
            {
                if (netObj.AutoObjectParentSync)
                {
                    if (base.IsHost)
                    {
                        netObj.TryRemoveParent(true);
                    }
                }
                else
                {
                    netObj.TryRemoveParent(true);
                }
            }
            else
            {
                child.SetParent(null, true);
            }

            if (child.TryGetComponent(out PlayerControllerB player) && player != GameNetworkManager.Instance.localPlayerController)
            {
                player.DisablePlayerModel(player.gameObject, enable: true, disableLocalArms: true);
            }
            else if (child.TryGetComponent(out GrabbableObject gObject))
            {
                gObject.EnableItemMeshes(enable: true);
            }
        }
    }



    //OPENING AND CLOSING
    public void ToggleFridgeDoor()
    {
        SetAnimatorBoolServerRpc("fridgeOpen", !fridgeAnimator.GetBool("fridgeOpen"));
    }

    public void ToggleFreezerDoor()
    {
        SetAnimatorBoolServerRpc("freezerOpen", !fridgeAnimator.GetBool("freezerOpen"));
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetAnimatorBoolServerRpc(string boolName, bool boolValue)
    {
        SetAnimatorBoolClientRpc(boolName, boolValue);
    }

    [ClientRpc]
    public void SetAnimatorBoolClientRpc(string boolName, bool boolValue)
    {
        fridgeAnimator.SetBool(boolName, boolValue);
        switch (boolName)
        {
            case "fridgeOpen":
                openTriggers[1].GetComponent<InteractTrigger>().hoverTip = boolValue ? "Close fridge : [E]" : "Open fridge : [E]";
                break;
            case "freezerOpen":
                openTriggers[0].GetComponent<InteractTrigger>().hoverTip = boolValue ? "Close freezer : [E]" : "Open freezer : [E]";
                break;
        }
    }



    //HAUNTED WORDS
    public bool SpellHauntedWord()
    {
        List<FridgeMagnet> magnetsInShip = [];
        FridgeMagnet[] allMagnets = FindObjectsByType<FridgeMagnet>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (allMagnets != null)
        {
            for (int i = 0; i < allMagnets.Length; i ++)
            {
                if (allMagnets[i].isInShipRoom)
                {
                    magnetsInShip.Add(allMagnets[i]);
                }
            }
        }
        else
        {
            return false;
        }
        char[] charsInShip;
        if (magnetsInShip.Count > 3)
        {
            charsInShip = new char[magnetsInShip.Count];
            for (int i = 0; i < magnetsInShip.Count; i++)
            {
                charsInShip[i] = magnetsInShip[i].letter;
            }
        }
        else
        {
            return false;
        }
        Debug.Log($"[FRIDGE]: Magnets in ship: [{new string(charsInShip)}].");

        //Get frequency map of available characters, then compare each word to the frequency map
        Dictionary<char, int> characterFrequencies = charsInShip.GroupBy(letter => letter).ToDictionary(letters => letters.Key, letters => letters.Count());
        List<string> validWords = [.. hauntedWords.Where(word => word.GroupBy(letter => letter).All(letters => characterFrequencies.ContainsKey(letters.Key) && letters.Count() <= characterFrequencies[letters.Key]))];
        string hauntedWord =  validWords.Count > 0 ? validWords[UnityEngine.Random.Range(0, validWords.Count)] : "_";

        if (hauntedWord == "_")
        {
            Debug.Log("[FRIDGE]: No valid haunted words were found for magnets in ship!");
            return false;
        }
        else
        {
            RemoveAllPlacedMagnets();
            RemoveAllPlacedMagnetsServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
            StartCoroutine(PlaceHauntedWord(hauntedWord, magnetsInShip));
            return true;
        }
    }

    public IEnumerator PlaceHauntedWord(string hauntedWord, List<FridgeMagnet> magnetsInShip)
    {
        yield return new WaitForEndOfFrame();
        Debug.Log($"[FRIDGE]: Spelling the word: [{hauntedWord}].");
        char[] wordChars = hauntedWord.ToCharArray();
        Vector3[] letterPositions = new Vector3[wordChars.Length];
        float dist = fridgeWidth/wordChars.Length;
        letterPositions[0] = hauntedWordPosition.transform.position - (hauntedWordPosition.transform.forward * fridgeWidth/2) + (hauntedWordPosition.transform.forward * dist/2);
        for (int i = 1; i < letterPositions.Length; i++)
        {
            letterPositions[i] = letterPositions[i-1] + hauntedWordPosition.transform.forward * dist;
            letterPositions[i] += hauntedWordPosition.transform.up * UnityEngine.Random.Range(-0.15f, 0.15f);
        }
        for (int i = 0; i < wordChars.Length; i++)
        {
            for (int j = magnetsInShip.Count - 1; j >= 0; j--)
            {
                if (wordChars[i] == magnetsInShip[j].letter)
                {
                    PlaceMagnet(magnetsInShip[j],  magnetSurfaces[0].transform.InverseTransformPoint(letterPositions[i]), 0, true);
                    PlaceMagnetServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, magnetsInShip[j].GetComponent<NetworkObject>(), magnetSurfaces[0].transform.InverseTransformPoint(letterPositions[i]), 0, true);
                    magnetsInShip.RemoveAt(j);
                    break;
                }
            }
        }
    }

    public void OpenRandomDoor()
    {
        switch (UnityEngine.Random.Range(0, 2))
        {
            case 0:
                ToggleFridgeDoor();
                break;
            case 1:
                ToggleFreezerDoor();
                break;
        }
    }



    //COLLISIONS
    public void OnTouch(Collider other)
    {
        if (other.gameObject.layer == 19 && other.gameObject.TryGetComponent(out EnemyAICollisionDetect enemyCollision))
        {
            switch (enemyCollision.mainScript)
            {
                case MaskedPlayerEnemy masked:
                    if (masked.currentBehaviourStateIndex == 2 && !PlayerInsideFridge(out _) && enemyInsideFridge == null)
                    {
                        enemyInsideFridge = new(enemyCollision.mainScript, enemyCollision, enemyCollision.GetComponent<BoxCollider>().size);
                        Debug.Log($"[FRIDGE]: enemyInsideFridge set to masked!");
                        StartCoroutine(CoronaMod.Patches.MaskedPlayerEnemyPatch.LetMaskedIntoFridge(masked));
                    }
                    break;
                case MouthDogAI dog:
                    if (dog.inLunge || dog.hasEnteredChaseModeFully)
                    {
                        ForceOpenDoor(loud: 0);
                    }
                    break;
                case DressGirlAI:
                    ForceOpenDoor(loud: 1);
                    break;
                case CaveDwellerAI caveDweller:
                    if (caveDweller.leaping)
                    {
                        ForceOpenDoor(loud: 0);
                    }
                    break;
            }
        }
    }

    public void OnStay(Collider other)
    {
        if (other.gameObject.layer == 19 && other.gameObject.TryGetComponent(out EnemyAICollisionDetect enemyCollision) && enemyCollision.mainScript is MaskedPlayerEnemy masked)
        {
            if (PlayerInsideFridge(out _))
            {
                enemyOpeningDoor = true;
                forceOpenDoorTimer += Time.deltaTime;
                if (forceOpenDoorTimer > forceOpenDoorTimeLimit * 1.5f)
                {
                    ForceOpenDoor();
                }
            }
            else if (enemyInsideFridge?.enemyAI == masked && masked.crouching)
            {
                if (DoorClosed)
                {
                    enemyInsideFridge?.HideInFridge(true);
                }
                else if (DoorOpen && (bool)enemyInsideFridge?.Hiding!)
                {
                    enemyInsideFridge?.HideInFridge(false);
                }

                if (!PlayersInsideShip && DoorOpen)
                {
                    enemyOpeningDoor = false;
                    ToggleFridgeDoor();
                }
                else if (PlayersInsideShip && DoorClosed)
                {
                    enemyOpeningDoor = true;
                    forceOpenDoorTimer += Time.deltaTime;
                    if (forceOpenDoorTimer > enemyInsideFridge?.ExitFridgeTime)
                    {
                        ForceOpenDoor(loud: 1);
                    }
                }
            }
            else if (enemyInsideFridge?.enemyAI == masked && !masked.crouching && DoorClosed)
            {
                ForceOpenDoor();
            }
        }
    }

    public void OnExit(Collider other)
    {
        if (other.gameObject.layer == 19 && other.gameObject.TryGetComponent(out EnemyAICollisionDetect enemyCollision) && enemyCollision.mainScript.currentBehaviourStateIndex != 2 && enemyCollision.mainScript == enemyInsideFridge?.enemyAI)
        {
            if (DoorClosed)
            {
                ForceOpenDoor();
                enemyInsideFridge?.HideInFridge(false);
            }
            enemyInsideFridge = null;
            Debug.Log($"[FRIDGE]: enemyInsideFridge set to null!");
        }
    }

    public void ForceOpenDoor(int loud = 0, float delay = 0f)
    {
        forceOpenDoorTimer = 0f;
        enemyOpeningDoor = false;
        StartCoroutine(ForceOpenDoorWithDelay(loud, delay));
    }

    public IEnumerator ForceOpenDoorWithDelay(int loud, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fridgeAnimator.GetBool("fridgeOpen"))
        {
            yield break;
        }
        forceOpenDoorTimeLimit = UnityEngine.Random.Range(1f, 3f);
        SetAnimatorBoolServerRpc("fridgeOpen", true);
        EnableNavMeshObstacleServerRpc(false);
        switch (loud)
        {
            case 0:
                fridgeAudio.PlayOneShot(slamDoorClips[UnityEngine.Random.Range(0, slamDoorClips.Length)]);
                break;
            case 1:
                if (UnityEngine.Random.Range(0f, 100f) > 50f)
                {
                    fridgeAudio.PlayOneShot(slamDoorClips[UnityEngine.Random.Range(0, slamDoorClips.Length)]);
                }
                break;
        }
    }
}