using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Compass : AnimatedItem, IHittable
{
    [Space(15f)]
    [Header("Compass Settings")]
    public float detectRange;

    private GameObject closestObject;

    public Item[] detectableItems;

    private List<GrabbableObject> detectedObjects = new List<GrabbableObject>();

    public GameObject needleBone;

    public float offsetCoolDownMin;

    public float offsetCoolDownMax;

    private float rotationCurrent;

    private float offset;

    private bool changeOffset = true;

    private float targetOffset;

    private int previousNumSyncedObjects;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource noiseAudio;

    public override void Start()
    {
        base.Start();
        rotationCurrent = transform.eulerAngles.y;
        LoadDetectedObjectsServerRpc();
        previousNumSyncedObjects = RoundManager.Instance.spawnedSyncedObjects.Count;
    }

    public override void Update()
    {
        base.Update();

        //CATCH ANY TIME AN OBJECT IS ADDED OR REMOVED FROM SPAWNED SYNCED OBJECTS
        if (previousNumSyncedObjects != RoundManager.Instance.spawnedSyncedObjects.Count)
        {
            LoadDetectedObjectsServerRpc();
            previousNumSyncedObjects = RoundManager.Instance.spawnedSyncedObjects.Count;
        }

        GameObject prevClosestObject = closestObject;

        //SET CLOSEST OBJECT
        closestObject = ClosestObject();

        if (prevClosestObject != closestObject)
        {
            Debug.Log($"[COMPASS]: Closest object set to {closestObject}.");
        }

        // SetDirection();

        //IF NOT ON COOLDOWN, SET RANDOM OFFSET
        if (base.IsOwner)
        {
            if (changeOffset == true)
            {
                ChangeOffsetServerRpc();
            }
        }

        //SET NOISE AMOUNT ACCORDING TO CLOSEST OBJECT PROXIMITY
        float noiseAmount = itemAnimator.GetFloat("NoiseAmount");

        if (closestObject != null)
        {
            float closestObjectDistance = Vector3.Distance(closestObject.transform.position, transform.position);

            if (closestObjectDistance < detectRange)
            {
                if (closestObjectDistance < detectRange)
                {
                    Debug.Log("[COMPASS]: Closest object in range!");
                }
                noiseAmount = Remap(closestObjectDistance, 0f, detectRange, 1f, 0f);
            }
        }
        else
        {
            noiseAmount = 0f;
        }

        Debug.Log($"[COMPASS]: Layer weight set to {noiseAmount}");

        // noiseAmount = Mathf.Lerp(noiseAmount, targetNoiseAmount, Time.deltaTime);
        itemAnimator.SetLayerWeight(1, noiseAmount);
        noiseAudio.volume = noiseAmount;

        //LERP TO TARGET OFFSET
        offset = Mathf.Lerp(offset, targetOffset, Time.deltaTime);

        //SET NEEDLE TO DIRECTION + OFFSET
        needleBone.transform.localEulerAngles = new Vector3(0f, -transform.eulerAngles.y + offset, 0f);
    }

    [ServerRpc]
    public void LoadDetectedObjectsServerRpc()
    {
        detectedObjects.Clear();
        for (int i = 0; i < RoundManager.Instance.spawnedSyncedObjects.Count; i++)
        {
            GrabbableObject gObject = RoundManager.Instance.spawnedSyncedObjects[i].GetComponent<GrabbableObject>();
            if (gObject != null)
            {
                for (int j = 0; j < detectableItems.Length; j++)
                {
                    if (gObject.itemProperties.itemName == detectableItems[j].itemName)
                    {
                        Debug.Log($"[COMPASS]: Server found {gObject}.");
                        NetworkObjectReference networkObjectReference = RoundManager.Instance.spawnedSyncedObjects[i].GetComponent<NetworkObject>();
                        AddDetectedObjectClientRpc(networkObjectReference);
                    }
                }
            }
        }
    }

    [ClientRpc]
    public void AddDetectedObjectClientRpc(NetworkObjectReference networkObjectReference)
    {
        GameObject detectedObject = networkObjectReference;
        GrabbableObject gObject = detectedObject.GetComponent<GrabbableObject>();
        if (gObject != null)
        {
            detectedObjects.Add(gObject);
            Debug.Log($"[COMPASS]: Added {gObject} to detected items.");
        }
    }

    [ServerRpc]
    public void ChangeOffsetServerRpc()
    {
        changeOffset = false;
        float coolDownTime = Random.Range(offsetCoolDownMin, offsetCoolDownMax);
        StartCoroutine(ChangeOffsetCooldown(coolDownTime));
        float newOffset = Random.Range(0f,360f);
        ChangeOffsetClientRpc(newOffset);
    }

    [ClientRpc]
    public void ChangeOffsetClientRpc(float newOffset)
    {
        itemAnimator.SetTrigger("Spin");
        targetOffset = newOffset;
        Debug.Log($"[COMPASS]: Offset set to {newOffset}");
    }

    // public void SetDirection()
    // {
    //     if (rotationPrevious > 270f && transform.eulerAngles.y < 90f)
    //     {
    //         rotations++;
    //     }

    //     if (rotationPrevious < 90f && transform.eulerAngles.y > 270f)
    //     {
    //         rotations--;
    //     }

    //     rotationPrevious = rotationCurrent;

    //     float rotationTarget = rotations*360f + transform.eulerAngles.y;

    //     rotationCurrent = Mathf.Lerp(rotationCurrent, rotationTarget, Time.deltaTime);
    //     needleBone.transform.localEulerAngles = new Vector3(0f, rotationCurrent, 0f);
    // }

    public GameObject ClosestObject()
    {
        if (detectedObjects.Count > 0)
        {
            GameObject closestObjectInCheck = detectedObjects[0].gameObject;
            float checkObjectDistance = Vector3.Distance(detectedObjects[0].transform.position, base.transform.position);
            for (int i = 1; i < detectedObjects.Count; i++)
            {
                if (Vector3.Distance(detectedObjects[i].transform.position, transform.position) < checkObjectDistance)
                {
                    closestObjectInCheck = detectedObjects[i].gameObject;
                    checkObjectDistance = Vector3.Distance(detectedObjects[i].transform.position, transform.position);
                }
            }
            return closestObjectInCheck;
        }
        else
        {
            return null;
        }
    }

    private IEnumerator ChangeOffsetCooldown(float time)
    {
        yield return new WaitForSeconds(time);
        changeOffset = false;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        ChangeOffsetServerRpc();
        return true;
	}

    public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }

}