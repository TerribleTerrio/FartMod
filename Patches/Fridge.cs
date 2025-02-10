using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Fridge : NetworkBehaviour
{
    [Header("Opening & Closing")]
    public Animator animator;

    [Space(5f)]
    [Header("Interactable Trigger Toggling")]
    private bool magnetTriggersEnabled;

    private float grabMagnetCooldown;

    public Collider[] magnetSurfaces;

    public Collider[] openTriggers;

    [Space(5f)]
    [Header("Magnet Placement")]
    private bool initialized;

    public List<FridgeMagnet> placedMagnets;

    private int assignedID = 1;

    [Space(5f)]
    [Header("Fridge Save Data")]
    public List<int> magnetIDs;

    public List<Vector3> magnetPositions;

    public List<int> magnetParents;

    public static String magnetIDsKey = "magnetIDs";

    public static String magnetPositionsKey = "magnetPositions";

    public static String magnetParentsKey = "magnetParents";

    public static String saveFile = "FridgeSave";

    private Vector3 lastPosition;

    private FridgeMagnet[] magnetsInSceneOnStart;

    [Space(5f)]
    [Header("Haunted Words")]

    [Range(0f,100f)]
    public float hauntChance;

    public String[] hauntedWords;

    public GameObject hauntedWordPosition;

    public float fridgeWidth = 2f;

    private float beginHauntTime;

    private bool hasTriedHauntingToday;

    private System.Random FridgeRandom;

    

    public void Start()
    {
        //SET ENABLED BOOL TO WHATEVER THE SURFACES ARE SET TO BY DEFAULT IN UNITY
        magnetTriggersEnabled = magnetSurfaces[0].enabled;

        //INSTANTIATE LISTS
        placedMagnets = new List<FridgeMagnet>();

        magnetIDs = new List<int>();
        magnetPositions = new List<Vector3>();
        magnetParents = new List<int>();

        //FIND ALL MAGNETS IN SCENE ON START
        magnetsInSceneOnStart = FindObjectsOfType<FridgeMagnet>();

        for (int i = 0; i < magnetsInSceneOnStart.Length; i++)
        {
            magnetsInSceneOnStart[i].LoadItemSaveData(magnetsInSceneOnStart[i].GetItemDataToSave());
        }

        if (IsHost)
        {
            //CHECK FOR AND SET SAVE DATA IF HOST
            StartCoroutine(CheckForSaveDataAfterFrame());
        }
    }

    public void Update()
    {
        //UPDATE TASKS ONLY CARRIED OUT BY OWNER
        if (IsOwner)
        {
            if (initialized)
            {
                //DETECT CHANGES IN POSITION
                if (lastPosition != base.transform.position)
                {
                    Debug.Log("[FRIDGE]: Placement changed, saving new position data!");
                    lastPosition = base.transform.position;
                    for (int i = 0; i < placedMagnets.Count; i++)
                    {
                        magnetPositions[i] = placedMagnets[i].transform.position;
                    }
                    SaveData();
                }
            }

            //INITIALIZE RANDOM FROM LEVEL SEED
            if (FridgeRandom == null && RoundManager.Instance.hasInitializedLevelRandomSeed)
            {
                // FridgeRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            }

            //SET RANDOM HAUNTING TIME ON LAND
            if (StartOfRound.Instance.shipHasLanded && beginHauntTime == 0f && FridgeRandom != null)
            {
                beginHauntTime = UnityEngine.Random.Range(0.3f, 0.7f);
            }

            //RESET HAUNTING TIME ON TAKEOFF
            if (StartOfRound.Instance.shipIsLeaving && beginHauntTime != 0f)
            {
                hasTriedHauntingToday = false;
                beginHauntTime = 0f;
                // FridgeRandom = null;
            }

            //IF HAUNTING TIME REACHED
            if (StartOfRound.Instance.shipHasLanded && TimeOfDay.Instance.normalizedTimeOfDay > beginHauntTime && !hasTriedHauntingToday)
            {
                //IF HAUNT CHANCE SUCCEEDS
                if (UnityEngine.Random.Range(0f,100f) < hauntChance)
                {
                    SpellHauntedWord();
                }
            }
        }

        if (initialized)
        {
            //TOGGLE INTERACT TRIGGERS
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            if (player.currentlyHeldObjectServer != null)
            {
                if (player.currentlyHeldObjectServer.gameObject.GetComponent<FridgeMagnet>() != null)
                {
                    EnableMagnetSurfaces();
                }
                else
                {
                    EnableMagnetSurfaces(false);
                }
            }
            else
            {
                EnableMagnetSurfaces(false);
            }

            //IF FRIDGE HAS ANY PLACED MAGNETS
            if (placedMagnets.Count > 0)
            {
                //FOR EACH MAGNET EVERY FRAME
                for (int i = 0; i < placedMagnets.Count; i++)
                {
                    //IF MAGNET REMOVED FROM FRIDGE
                    if (placedMagnets[i].playerHeldBy != null)
                    {
                        Debug.Log("[FRIDGE]: Magnet removed from fridge, setting cooldown!");
                        grabMagnetCooldown = 0.05f;
                    }
                }
            }
        }

        else
        {
            Debug.Log("[FRIDGE]: Not initialized!");

            if (!IsOwner)
            {
                if (GameNetworkManager.Instance.localPlayerController == null)
                {
                    Debug.Log("[FRIDGE]: Waiting...");
                }
                else
                {
                    Debug.Log("[FRIDGE]: Attempting to sync magnets from owner.");
                    SyncPlacedMagnetsFromOwnerServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                    initialized = true;
                }
            }
        }

        //TICK INTERACTION COOLDOWN
        if (grabMagnetCooldown > 0)
        {
            grabMagnetCooldown -= Time.deltaTime;
        }
    
    }

    public void LateUpdate()
    {
        if (IsOwner)
        {
            //IF FRIDGE HAS ANY PLACED MAGNETS
            if (placedMagnets.Count > 0)
            {
                //FOR EACH MAGNET EVERY FRAME
                for (int i = 0; i < placedMagnets.Count; i++)
                {
                    //IF MAGNET REMOVED FROM FRIDGE
                    if (placedMagnets[i].playerHeldBy != null)
                    {
                        NetworkObjectReference magnetRef = placedMagnets[i].gameObject.GetComponent<NetworkObject>();
                        RemoveMagnetServerRpc(magnetRef);
                    }
                }
            }
        }
    }



    //CHECKING AND LOADING SAVE DATA
    private IEnumerator CheckForSaveDataAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        CheckForSaveData();
    }

    private void CheckForSaveData()
    {
        //REFERENCE CORRECT SAVE FILE
        saveFile = GameNetworkManager.Instance.saveFileNum switch
        {
            0 => "FartmodSave1",
            1 => "FartmodSave2",
            2 => "FartmodSave3",
            _ => "FartmodSave1"
        };

        //CHECK IF SAVE DATA EXISTS
        if (ES3.KeyExists(magnetIDsKey, saveFile) && ES3.KeyExists(magnetPositionsKey, saveFile) && ES3.KeyExists(magnetParentsKey, saveFile))
        {
            //LOAD LISTS FROM SAVE DATA
            Debug.Log("[FRIDGE]: Magnet save data found!");
            LoadData();

            //CHECK IF ENOUGH MAGNETS EXIST
            if (magnetsInSceneOnStart.Length < magnetIDs.Count)
            {
                Debug.Log("[FRIDGE]: Mismatch between magnets in scene and save data detected! Starting with empty data.");
                return;
            }

            //UPDATE ASSIGNED ID
            int largestID = 0;
            for (int i = 0; i < magnetIDs.Count; i++)
            {
                if (magnetIDs[i] > largestID)
                {
                    largestID = magnetIDs[i];
                }
            }
            assignedID = largestID + 1;

            //PLACE MAGNETS ON FRIDGE BASED ON SAVE DATA
            for (int i = 0; i < magnetIDs.Count; i++)
            {
                for (int j = 0; j < magnetsInSceneOnStart.Length; j++)
                {
                    if (magnetsInSceneOnStart[j].fridgeID == magnetIDs[i])
                    {
                        Debug.Log($"[FRIDGE]: Found matching id for magnet {i}! Placing on fridge.");

                        NetworkObjectReference magnetRef = magnetsInSceneOnStart[j].gameObject.GetComponent<NetworkObject>();
                        PlaceMagnetClientRpc(magnetRef, magnetPositions[i], magnetParents[i]);
                    }
                }
            }
        }

        else
        {
            //CONTINUE WITH NEW LISTS FOR SAVE DATA
            Debug.Log("[FRIDGE]: Magnet save data missing or incomplete, beginning with empty lists!");
        }

        initialized = true;
    }

    private void LoadData()
    {
        Debug.Log("[FRIDGE]: --- Loading data! ---");

        magnetIDs = ES3.Load<List<int>>(magnetIDsKey, saveFile);
        magnetPositions = ES3.Load<List<Vector3>>(magnetPositionsKey, saveFile);
        magnetParents = ES3.Load<List<int>>(magnetParentsKey, saveFile);

        for (int i = 0; i < magnetIDs.Count; i++)
        {
            Debug.Log($"[FRIDGE]: Magnet ID {i}: {magnetIDs[i]}");
        }

        Debug.Log("[FRIDGE]: --- Data loaded! ---");
    }

    private void SaveData()
    {
        Debug.Log("[FRIDGE]: --- Saving data! ---");
        for (int i = 0; i < magnetIDs.Count; i++)
        {
            Debug.Log($"[FRIDGE]: Magnet ID {i}: {magnetIDs[i]}");
        }

        ES3.Save(magnetIDsKey, magnetIDs, saveFile);
        ES3.Save(magnetPositionsKey, magnetPositions, saveFile);
        ES3.Save(magnetParentsKey, magnetParents, saveFile);

        Debug.Log("[FRIDGE]: --- Data saved! ---");
    }

    // private IEnumerator SyncPlacedMagnetsFromOwnerAfterFrame()
    // {
    //     yield return new WaitForEndOfFrame();
    //     SyncPlacedMagnetsFromOwnerServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    // }

    [ServerRpc(RequireOwnership = false)]
    private void SyncPlacedMagnetsFromOwnerServerRpc(int playerId)
    {
        if (IsOwner)
        {
            Debug.Log($"[FRIDGE]: Owner syncing placed magnets to player {StartOfRound.Instance.allPlayerScripts[playerId].playerUsername}.");
            for (int i = 0; i < placedMagnets.Count; i++)
            {
                FridgeMagnet magnet = placedMagnets[i];
                Debug.Log($"[FRIDGE]: Syncing magnet {i} with letter {magnet.letter} and ID {magnet.fridgeID}.");

                //GET CORRECT PARENT
                int parent;
                int index = 0;
                for (int j = 0; j < magnetIDs.Count; j++)
                {
                    if (magnet.fridgeID == magnetIDs[j])
                    {
                        index = j;
                        break;
                    }
                }

                parent = magnetParents[index];

                NetworkObjectReference magnetRef = magnet.gameObject.GetComponent<NetworkObject>();
                SyncPlacedMagnetClientRpc(magnetRef, magnet.transform.position, parent, playerId);
            }
        }
    }

    [ClientRpc]
    private void SyncPlacedMagnetClientRpc(NetworkObjectReference magnetRef, Vector3 pos, int parent, int playerId)
    {
        if (playerId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            NetworkObject magnetNetworkObject = magnetRef;
            FridgeMagnet magnet = magnetNetworkObject.gameObject.GetComponent<FridgeMagnet>();
            Debug.Log($"[FRIDGE]: Syncing magnet for client with letter {magnet.letter} and ID {magnet.fridgeID}!");
            PlaceMagnetClientRpc(magnetRef, pos, parent);
        }
    }



    //TOGGLING INTERACT TRIGGERS
    public void EnableMagnetSurfaces(bool enable = true)
    {
        if (enable && !magnetTriggersEnabled)
        {
            // Debug.Log("[FRIDGE]: Enabling magnet triggers!");
            for (int i = 0; i < magnetSurfaces.Length; i++)
            {
                magnetSurfaces[i].enabled = true;
            }
            for (int i = 0; i < openTriggers.Length; i++)
            {
                openTriggers[i].enabled = false;
            }
            magnetTriggersEnabled = true;

            StartCoroutine(EnableMagnetCollidersAfterFrame(false));
        }

        if (!enable && magnetTriggersEnabled)
        {
            // Debug.Log("[FRIDGE]: Disabling magnet triggers!");
            for (int i = 0; i < magnetSurfaces.Length; i++)
            {
                magnetSurfaces[i].enabled = false;
            }
            for (int i = 0; i < openTriggers.Length; i++)
            {
                openTriggers[i].enabled = true;
            }
            magnetTriggersEnabled = false;

            StartCoroutine(EnableMagnetCollidersAfterFrame());
        }
    }

    private IEnumerator EnableMagnetCollidersAfterFrame(bool enable = true)
    {
        yield return new WaitForEndOfFrame();
        EnableMagnetColliders(enable);
    }

    private void EnableMagnetColliders(bool enable = true)
    {
        for (int i = 0; i < placedMagnets.Count; i++)
        {
            placedMagnets[i].gameObject.GetComponent<Collider>().enabled = enable;
        }
    }



    //PLACING & REMOVING MAGNETS
    public void PlaceHeldMagnet()
    {
        //ABORT IF ON COOLDOWN
        // if (grabMagnetCooldown > 0)
        // {
        //     return;
        // }

        Debug.Log("[FRIDGE]: Calling PlaceHeldMagnet!");

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
        RaycastHit hit;
        if (Physics.Raycast(player.gameplayCamera.transform.position, player.gameplayCamera.transform.forward, out hit, 4f, player.interactableObjectsMask, QueryTriggerInteraction.Collide))
        {
            //SET PLACEMENT POSITION AND ROTATION
            Vector3 setPosition = hit.point + hit.collider.gameObject.transform.forward * fridgeMagnet.itemProperties.verticalOffset;

            //GET INDEX OF COLLIDER
            int parent = 0;
            for (int i = 0; i < magnetSurfaces.Length; i++)
            {
                if (magnetSurfaces[i] == hit.collider)
                {
                    parent = i;
                    break;
                }
            }

            NetworkObjectReference magnetRef = fridgeMagnet.gameObject.GetComponent<NetworkObject>();
            PlaceMagnetServerRpc(magnetRef, setPosition, parent);
        }
        else
        {
            Debug.LogError("[FRIDGE]: PlaceHeldMagnet called but could not find placementTrigger!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlaceMagnetServerRpc(NetworkObjectReference magnetRef, Vector3 pos, int parent)
    {
        PlaceMagnetClientRpc(magnetRef, pos, parent);
    }

    [ClientRpc]
    public void PlaceMagnetClientRpc(NetworkObjectReference magnetRef, Vector3 pos, int parent)
    {
        NetworkObject magnetNetworkObject = magnetRef;
        FridgeMagnet magnet = magnetNetworkObject.gameObject.GetComponent<FridgeMagnet>();

        PlaceMagnet(magnet, pos, parent);
    }

    public void PlaceMagnet(FridgeMagnet magnet, Vector3 pos, int parent)
    {
        Debug.Log("[FRIDGE]: Placing magnet!");

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

        StartCoroutine(SetMagnetTransformsAfterFrame(magnet, pos, parent));

        magnet.OnPlaceObject();

        placedMagnets.Add(magnet);
        // magnet.itemProperties.isScrap = true;

        if (IsHost)
        {
            //CHECK IF MAGNET IS NOT ALREADY IN SAVE DATA
            if (!magnetIDs.Contains(magnet.fridgeID))
            {
                Debug.Log("[FRIDGE]: Host updating save data!");

                //ASSIGN ID AND INCREMENT
                magnet.fridgeID = assignedID;
                magnet.GetItemDataToSave();
                assignedID++;

                //ADD MAGNET TO SAVE DATA
                magnetIDs.Add(magnet.fridgeID);
                magnetPositions.Add(pos);
                magnetParents.Add(parent);
            }
            else
            {
                Debug.Log("[FRIDGE]: Synced magnet!");
            }

            //UPDATE SAVE DATA
            SaveData();
        }
    }

    private IEnumerator SetMagnetTransformsAfterFrame(FridgeMagnet magnet, Vector3 pos, int parent)
    {
        yield return new WaitForEndOfFrame();
        magnet.parentObject = null;
        magnet.transform.SetParent(magnetSurfaces[parent].transform, worldPositionStays: true);
        magnet.transform.localPosition = magnetSurfaces[parent].transform.InverseTransformPoint(pos);
        magnet.targetFloorPosition = magnetSurfaces[parent].transform.InverseTransformPoint(pos);
        magnet.fallTime = 1.1f;

        // yield return new WaitForEndOfFrame();
        magnet.transform.localEulerAngles = new Vector3(90,0,0);
    }

    [ServerRpc]
    private void RemoveMagnetServerRpc(NetworkObjectReference magnetRef)
    {
        RemoveMagnetClientRpc(magnetRef);
    }

    [ClientRpc]
    private void RemoveMagnetClientRpc(NetworkObjectReference magnetRef)
    {
        NetworkObject magnetNetworkObject = magnetRef;
        FridgeMagnet magnet = magnetNetworkObject.gameObject.GetComponent<FridgeMagnet>();
        Debug.Log($"[FRIDGE]: Removing magnet with letter {magnet.letter}!");

        if (IsHost)
        {
            for (int i = 0; i < magnetIDs.Count; i++)
            {
                if (magnetIDs[i] == magnet.fridgeID)
                {
                    //UPDATE SAVE DATA
                    magnetIDs.Remove(magnetIDs[i]);
                    magnetPositions.Remove(magnetPositions[i]);
                    magnetParents.Remove(magnetParents[i]);
                    SaveData();
                }
            }
        }

        magnet.fridgeID = 0;
        placedMagnets.Remove(magnet);

        if (!magnet.heldByPlayerOnServer)
        {
            //DROP MAGNET TO GROUND
        }

        // magnet.itemProperties.isScrap = true;
    }



    //OPENING AND CLOSING
    public void OpenFridgeDoor()
    {
        if (animator.GetBool("fridgeOpen"))
        {
            SetAnimatorBoolServerRpc("fridgeOpen", false);
        }
        else
        {
            SetAnimatorBoolServerRpc("fridgeOpen", true);
        }
    }

    public void OpenFreezerDoor()
    {
        if (animator.GetBool("freezerOpen"))
        {
            SetAnimatorBoolServerRpc("freezerOpen", false);
        }
        else
        {
            SetAnimatorBoolServerRpc("freezerOpen", true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetAnimatorBoolServerRpc(String boolName, bool boolValue)
    {
        SetAnimatorBoolClientRpc(boolName, boolValue);
    }

    [ClientRpc]
    public void SetAnimatorBoolClientRpc(String boolName, bool boolValue)
    {
        animator.SetBool(boolName, boolValue);
    }



    //HAUNTED WORDS
    public String GetValidHauntedWord(char[] chars)
    {
        Debug.Log($"[FRIDGE]: Finding valid haunted word for chars [{chars}].");

        String hauntedWord = "_";
        List<String> validWords = new List<String>();

        //DECIDE RANDOM WORD BASED ON CHARS IN SHIP

        //FOR EACH HAUNTED WORD
        for (int i = 0; i < hauntedWords.Length; i++)
        {
            char[] availableChars = new char[chars.Length];
            System.Array.Copy(chars, availableChars, chars.Length);

            Debug.Log($"[FRIDGE]: Checking if haunted word {hauntedWords[i]} is valid.");
            bool wordValid = false;

            char[] currentWordChars = hauntedWords[i].ToCharArray();

            int charsFound = 0;

            for (int j = 0; j < currentWordChars.Length; j++)
            {
                char currentChar = currentWordChars[j];
                Debug.Log($"[FRIDGE]: Searching for char {currentChar} in available chars.");

                for (int k = 0; k < availableChars.Length; k++)
                {
                    Debug.Log($"[FRIDGE]: {currentChar} : {availableChars[k]}");
                    if (availableChars[k] == currentChar)
                    {
                        Debug.Log("[FRIDGE]: Char found! Moving to next.");
                        availableChars[k] = (char)0;
                        charsFound++;
                        break;
                    }
                }

                if (charsFound == currentWordChars.Length)
                {
                    wordValid = true;
                }
            }

            if (wordValid)
            {
                validWords.Add(hauntedWords[i]);
            }
            else
            {
                Debug.Log($"[FRIDGE]: Word invalid!");
            }
        }

        Debug.Log($"[FRIDGE]: {validWords.Count} valid words found!");

        if (validWords.Count > 0)
        {
            if (validWords.Count == 1)
            {
                hauntedWord = validWords[0];
            }
            else
            {
                Debug.Log("[FRIDGE]: Selecting random valid word.");
                int wordNum = UnityEngine.Random.Range(0, validWords.Count);
                hauntedWord = validWords[wordNum];
                Debug.Log($"[FRIDGE]: Word {wordNum} selected.");
            }
        }

        Debug.Log($"[FRIDGE]: Haunted word set to {hauntedWord}.");

        return hauntedWord;
    }

    public void SpellHauntedWord()
    {
        List<FridgeMagnet> magnetsInShip = new List<FridgeMagnet>();
        char[] charsInShip = new char[1];

        //GET MAGNETS CURRENTLY IN SHIP
        FridgeMagnet[] allMagnets = FindObjectsOfType<FridgeMagnet>();

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
            Debug.LogError("[FRIDGE]: No magnets found in scene!");
            return;
        }

        //GET CHARS CURRENTLY IN SHIP
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
            Debug.LogError("[FRIDGE]: Not enough magnets in ship to spell word!");
            return;
        }

        Debug.Log($"[FRIDGE]: Chars in ship set to [{charsInShip}].");

        String hauntedWord = GetValidHauntedWord(charsInShip);
        char[] wordChars = hauntedWord.ToCharArray();

        //CHECK IF HAUNTED WORD WAS NOT FOUND
        if (hauntedWord == "_")
        {
            Debug.LogError("[FRIDGE]: No valid haunted words were found for magnets in ship!");
            return;
        }

        //CHECK IF HAUNTED WORD IS TOO LONG
        if (wordChars.Length > 5)
        {
            Debug.LogError("[FRIDGE]: Haunted word is too long, positions have not been set!");
            return;
        }

        //REMOVE PLACED MAGNETS
        for (int i = 0; i < placedMagnets.Count; i++)
        {
            NetworkObjectReference magnetRef = placedMagnets[i].gameObject.GetComponent<NetworkObject>();
            RemoveMagnetServerRpc(magnetRef);
        }

        //CREATE POSITIONS FOR HAUNTED WORD MAGNETS
        Vector3[] letterPositions = new Vector3[wordChars.Length];
        float dist = fridgeWidth/wordChars.Length;

        //SET X POSITIONS LOCAL TO HAUNTEDWORDPOSITION OBJECT
        letterPositions[0] = hauntedWordPosition.transform.position - (hauntedWordPosition.transform.forward * fridgeWidth/2) + (hauntedWordPosition.transform.forward * dist/2);
        for (int i = 1; i < letterPositions.Length; i++)
        {
            letterPositions[i] = letterPositions[i-1] + hauntedWordPosition.transform.forward * dist;
        }

        //SET RANDOM Y POSITIONS LOCAL TO HAUNTEDWORDPOSITION OBJECT
        for (int i = 0; i < letterPositions.Length; i++)
        {
            letterPositions[i] += hauntedWordPosition.transform.up * UnityEngine.Random.Range(-0.15f, 0.15f);
        }

        //PLACE MAGNETS AT GENERATED POSITIONS
        List<FridgeMagnet> availableMagnets = magnetsInShip;
        for (int i = 0; i < wordChars.Length; i++)
        {
            for (int j = 0; j < availableMagnets.Count; j++)
            {
                if (wordChars[i] == availableMagnets[j].letter)
                {
                    PlaceMagnet(availableMagnets[j], letterPositions[i], 0);
                    availableMagnets.Remove(availableMagnets[j]);
                    break;
                }
            }
        }
    }

    private float RemapFloat(float value, float min1, float max1, float min2, float max2)
    {
        float m = (value - min1) / (max1 - min1) * (max2 - min2) + min2;
        return m;
    }
}