using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;

public class FridgeMagnet() : GrabbableObject
{

    [Space(15f)]
    [Header("Fridge Magnet Settings")]
    public int minExtraMagnets = 3;

    public int maxExtraMagnets = 6;

    public Mesh[] meshVariants;

    [Space(5f)]
    [Header("Fridge Magnet Save Data")]
    public int spawnExtras = 1;

    public int meshVariant = -1;

    public char letter;

    public String meshVariantLetters = "AEIOUBCDFGHJKLMNPQRSTVWXYZ";

    public int fridgeID = 0;

    public override void Start()
    {
        if (IsOwner)
        {
            if (spawnExtras == 1)
            {
                StartCoroutine(SpawnExtraMagnetsAfterFrame());
            }

            if (meshVariant < 0)
            {
                SetMeshVariantServerRpc();
            }
        }

        base.Start();

        if (meshVariant != -1)
        {
            letter = meshVariantLetters[meshVariant];
        }

        // itemProperties.isScrap = false;
    }

    public override int GetItemDataToSave()
    {
        String saveData = spawnExtras.ToString();
        if (meshVariant.ToString().Length == 1)
        {
            saveData += "0";
        }
        saveData += meshVariant.ToString();
        saveData += fridgeID.ToString();

        // itemProperties.isScrap = true;

        return int.Parse(saveData);
    }

    public override void LoadItemSaveData(int saveData)
    {
        spawnExtras = int.Parse(saveData.ToString().Substring(0,1));
        meshVariant = int.Parse(saveData.ToString().Substring(1,2));
        gameObject.GetComponent<MeshFilter>().mesh = meshVariants[meshVariant];
        letter = meshVariantLetters[meshVariant];
        fridgeID = int.Parse(saveData.ToString().Substring(3));
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

        Debug.Log($"[FRIDGEMAGNET]: Mesh variant set to {letter}.");
        GetItemDataToSave();
    }

    private IEnumerator SpawnExtraMagnetsAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        SpawnExtraMagnetsServerRpc();
        spawnExtras = 2;
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

        newMagnet.spawnExtras = 2;
        newMagnet.SetScrapValue(value);
        RoundManager.Instance.totalScrapValueInLevel += newMagnet.scrapValue;

        nObject.gameObject.transform.position = position;
        nObject.gameObject.transform.eulerAngles = new Vector3(0f,rotation,0f);
    }
}