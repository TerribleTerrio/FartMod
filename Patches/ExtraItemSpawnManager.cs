using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

[CreateAssetMenu(menuName = "Extra Item Spawn Manager", order = 1)]

public class ExtraItemSpawnManager : ScriptableObject
{
    public int minSpawnAmount;

    public int maxSpawnAmount;

    public float extraItemRange;

    private int spawnAmount;

    public GameObject spawnObject;

    public NavMeshHit navHit;

    private System.Random RandomSeed;

    public void SpawnExtraItems(Vector3 position)
    {
        
        navHit = default(NavMeshHit);
        spawnAmount = UnityEngine.Random.Range(minSpawnAmount, maxSpawnAmount + 1);
        int extraValue = 0;

        for (int i = 0; i < spawnAmount; i++)
        {
            Vector3 spawnPosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(position, extraItemRange, navHit, RandomSeed);

            if (spawnObject.GetComponent<GrabbableObject>() != null)
            {
                GrabbableObject component = spawnObject.GetComponent<GrabbableObject>();
                spawnPosition += Vector3.up * component.itemProperties.verticalOffset;
            }

            GameObject obj = UnityEngine.Object.Instantiate(spawnObject, spawnPosition, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);

            if (obj.gameObject.GetComponent<GrabbableObject> != null)
            {
                GrabbableObject component = obj.GetComponent<GrabbableObject>();
                component.transform.rotation = Quaternion.Euler(component.itemProperties.restingRotation);
                component.scrapValue = UnityEngine.Random.Range(component.itemProperties.minValue, component.itemProperties.maxValue + 1);
                extraValue += component.scrapValue;
            }
        }

        RoundManager.Instance.totalScrapValueInLevel += extraValue;
    }
}