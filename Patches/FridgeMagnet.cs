using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class FridgeMagnet : GrabbableObject
{
    [Space(15f)]
    [Header("Fridge Magnet Settings")]
    public int minExtraMagnets = 3;

    public int maxExtraMagnets = 6;

    public Mesh[] meshVariants;

    [Space(5f)]
    [Header("Fridge Magnet Save Data")]
    public int meshVariant = -1;

    public char letter;

    public string meshVariantLetters = "AEIOUBCDFGHJKLMNPQRSTVWXYZ";

    public int fridgeID = 0;

    public bool loadedData = false;

    public override void Start()
    {
        StartOfRound.Instance.StartNewRoundEvent.AddListener(SpawnExtraMagnets);
        if (base.IsHost)
        {
            if (meshVariant == -1)
            {
                SetMeshVariantServerRpc();
            }
        }
        else
        {
            StartCoroutine(WaitToLoadSaveData());
        }
        base.Start();
    }

    public override void EquipItem()
    {
        if (Fridge.Instance != null)
        {
            if (Fridge.Instance.placedMagnets.Contains(this))
            {
                Fridge.Instance.RemoveMagnet(this);
                Fridge.Instance.RemoveMagnetServerRpc(base.gameObject.GetComponent<NetworkObject>());
            }
        }
        base.EquipItem();
    }

    public IEnumerator WaitToLoadSaveData()
    {
        float timeStart = Time.realtimeSinceStartup;
        yield return new WaitUntil(() => GameNetworkManager.Instance.localPlayerController != null || Time.realtimeSinceStartupAsDouble - timeStart > 10f);
        RequestHostSaveDataServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestHostSaveDataServerRpc(int clientWhoSentRpc)
    {
        ReceiveHostSaveDataClientRpc(GetItemDataToSave(), clientWhoSentRpc);
    }

    [ClientRpc]
    public void ReceiveHostSaveDataClientRpc(int data, int clientWhoSentRpc)
    {
        if (clientWhoSentRpc == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            LoadItemSaveData(data);
        }
    }

    public override int GetItemDataToSave()
    {
        string saveData = "1";
        if (meshVariant < 10)
        {
            saveData += "0";
        }
        saveData += meshVariant.ToString();
        saveData += fridgeID.ToString();
        return int.Parse(saveData);
    }

    public override void LoadItemSaveData(int saveData)
    {
        meshVariant = int.Parse(saveData.ToString().Substring(1, 2));
        fridgeID = int.Parse(saveData.ToString()[3..]);
        gameObject.GetComponent<MeshFilter>().mesh = meshVariants[meshVariant];
        letter = meshVariantLetters[meshVariant];
        loadedData = true;
    }

    [ServerRpc(RequireOwnership = true)]
    private void SetMeshVariantServerRpc()
    {
        int variant;
        if (UnityEngine.Random.Range(0f,100f) < 25f)
        {
            variant = UnityEngine.Random.Range(0,5);
        }
        else
        {
            variant = UnityEngine.Random.Range(5,meshVariants.Length);
        }
        SetMeshVariantClientRpc(variant);
    }

    [ClientRpc]
    private void SetMeshVariantClientRpc(int variant)
    {
        gameObject.GetComponent<MeshFilter>().mesh = meshVariants[variant];
        meshVariant = variant;
        letter = meshVariantLetters[variant];
        Debug.Log($"[FRIDGEMAGNET]: Mesh variant set to {letter} | {meshVariant}.");
    }

    public void SpawnExtraMagnets()
    {
        if (!base.IsServer || scrapPersistedThroughRounds || isInShipRoom || isInElevator || hasBeenHeld || !isInFactory || StartOfRound.Instance.inShipPhase || !StartOfRound.Instance.currentLevel.spawnEnemiesAndScrap)
        {
            return;
        }
        else 
        {
            StartCoroutine(SpawnExtraMagnetsAfterFrame());
        }
    }

    private IEnumerator SpawnExtraMagnetsAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        SpawnExtraMagnetsServerRpc();
    }

    [ServerRpc]
    private void SpawnExtraMagnetsServerRpc()
    {
        int numMagnets = UnityEngine.Random.Range(minExtraMagnets, maxExtraMagnets+1);
        Debug.Log($"[FRIDGEMAGNET]: Spawning {numMagnets} extra magnets!");

        for (int i = 0; i < numMagnets; i++)
        {
            //INSTANTIATE PREFAB
            GameObject newObj = UnityEngine.Object.Instantiate(itemProperties.spawnPrefab, transform.position, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);

            //SPAWN NETWORK OBJECT
            newObj.GetComponent<NetworkObject>().Spawn();
            
            //GET RANDOM VALUES AND SYNC FOR CLIENTS
            Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(transform.position, 20);
            position += Vector3.up * itemProperties.verticalOffset;
            int value = (int)(UnityEngine.Random.Range(itemProperties.minValue, itemProperties.maxValue+1) * 0.4f);
            float rotation = UnityEngine.Random.Range(0f,360f);
            SyncValuesClientRpc(newObj.GetComponent<NetworkObject>(), value, position, rotation);
        }
    }

    [ClientRpc]
    private void SyncValuesClientRpc(NetworkObjectReference reference, int value, Vector3 position, float rotation)
    {
        NetworkObject nObject = reference;
        FridgeMagnet newMagnet = nObject.gameObject.GetComponent<FridgeMagnet>();

        newMagnet.SetScrapValue(value);
        RoundManager.Instance.totalScrapValueInLevel += newMagnet.scrapValue;

        nObject.gameObject.transform.position = position;
        nObject.gameObject.transform.eulerAngles = new Vector3(0f,rotation,0f);
    }
}