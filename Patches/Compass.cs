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

    private List<GrabbableObject> detectedItems = new List<GrabbableObject>();

    public GameObject needleBone;

    public float offsetTimerMin = 100;

    public float offsetTimerMax = 300;

    private float offset;

    private float targetOffset;

    private float offsetTimer;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource noiseAudio;

    public override void Start()
    {
        base.Start();
    }

    public override void Update()
    {
        base.Update();

        if (IsOwner)
        {
            offsetTimer--;
            if (offsetTimer <= 0f)
            {
                offsetTimer = Random.Range(offsetTimerMin, offsetTimerMax);
                ChangeOffsetServerRpc();
            }

            LoadDetectedItemsServerRpc();

            GameObject previousClosestObject = closestObject;
            SetClosestItemServerRpc();
            if (previousClosestObject != closestObject)
            {
                Debug.Log($"[COMPASS]: Closest object set to {closestObject}!");
                previousClosestObject = closestObject;
            }

            SetNoiseAmountServerRpc();
        }

        offset = Mathf.Lerp(offset, targetOffset, Time.deltaTime);
        needleBone.transform.localEulerAngles = new Vector3(0f, -transform.eulerAngles.y + offset, 0f);
    }

    [ServerRpc]
    public void LoadDetectedItemsServerRpc()
    {
        detectedItems.Clear();
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectRange, 1073742656, QueryTriggerInteraction.Collide);

        for (int i = 0; i < colliders.Length; i++)
        {
            GrabbableObject gObject = colliders[i].gameObject.GetComponent<GrabbableObject>();
            if (gObject != null)
            {
                for (int j = 0; j < detectableItems.Length; j++)
                {
                    if (gObject.itemProperties.itemName == detectableItems[j].itemName)
                    {
                        NetworkObjectReference networkObjectReference = gObject.NetworkObject;
                        AddDetectedItemClientRpc(networkObjectReference);
                    }
                }
            }
        }
    }

    [ClientRpc]
    public void AddDetectedItemClientRpc(NetworkObjectReference networkObjectReference)
    {
        NetworkObject nObject = networkObjectReference;
        GrabbableObject gObject = nObject.gameObject.GetComponent<GrabbableObject>();

        if (gObject != null)
        {
            detectedItems.Add(gObject);
        }
    }

    [ServerRpc]
    public void ChangeOffsetServerRpc()
    {
        float offsetNew = Random.Range(0f, 360f);
        ChangeOffsetClientRpc(offsetNew);
    }

    [ClientRpc]
    public void ChangeOffsetClientRpc(float offsetNew)
    {
        itemAnimator.SetTrigger("Spin");
        targetOffset = offsetNew;
    }

    [ServerRpc]
    public void SetNoiseAmountServerRpc()
    {
        float noiseAmount;

        if (closestObject != null)
        {
            float closestObjectDistance = Vector3.Distance(closestObject.transform.position, transform.position);

            if (closestObjectDistance < detectRange)
            {
                noiseAmount = Remap(closestObjectDistance, 0f, detectRange, 1f, 0f);
            }
            else
            {
                noiseAmount = 0f;
            }
        }
        else
        {
            noiseAmount = 0f;
        }

        SetNoiseAmountClientRpc(noiseAmount);
    }

    [ClientRpc]
    public void SetNoiseAmountClientRpc(float noiseAmount)
    {
        itemAnimator.SetLayerWeight(1, noiseAmount);
        noiseAudio.volume = noiseAmount;
    }

    // public void AddDetectedObject(Collider other)
    // {
    //     if (IsOwner)
    //     {
    //         Debug.Log($"[COMPASS]: Collider {other} entered detected range.");
    //         GrabbableObject gObject = other.gameObject.GetComponent<GrabbableObject>();
    //         if (gObject != null)
    //         {
    //             Debug.Log($"[COMPASS]: Collider had attached GrabbableObject {gObject}.");
    //             for (int i = 0; i < detectableItems.Length; i++)
    //             {
    //                 if (gObject.itemProperties.itemName == detectableItems[i].itemName)
    //                 {
    //                     Debug.Log($"[COMPASS]: Added {gObject} to detected objects!");
    //                     detectedObjects.Add(gObject);
    //                 }
    //             }
    //         }
    //     }
    // }

    // public void RemoveDetectedObject(Collider other)
    // {
    //     if (IsOwner)
    //     {
    //         GrabbableObject gObject = other.gameObject.GetComponent<GrabbableObject>();
    //         if (gObject != null)
    //         {
    //             if (detectedObjects.Contains(gObject))
    //             {
    //                 Debug.Log($"[COMPASS]: Removed {gObject} from detected objects!");
    //                 detectedObjects.Remove(gObject);
    //             }
    //         }
    //     }
    // }

    [ServerRpc]
    public void SetClosestItemServerRpc()
    {
        GameObject setClosestObject = ClosestObject();

        if (setClosestObject != null)
        {
            NetworkObjectReference closestObjectReference = setClosestObject.GetComponent<NetworkObject>();
            SetClosestObjectClientRpc(closestObjectReference);
        }
        else
        {
            SetClosestObjectNullClientRpc();
        }
    }

    [ClientRpc]
    public void SetClosestObjectClientRpc(NetworkObjectReference setClosestObject)
    {
        closestObject = setClosestObject;
    }

    [ClientRpc]
    public void SetClosestObjectNullClientRpc()
    {
        closestObject = null;
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
        GameObject closestObjectInCheck;
        if (detectedItems.Count > 0)
        {
            closestObjectInCheck = detectedItems[0].gameObject;
            if (detectedItems.Count == 1)
            {
                return closestObjectInCheck;
            }

            float checkObjectDistance = Vector3.Distance(detectedItems[0].transform.position, base.transform.position);
            for (int i = 1; i < detectedItems.Count; i++)
            {
                if (Vector3.Distance(detectedItems[i].transform.position, transform.position) < checkObjectDistance)
                {
                    closestObjectInCheck = detectedItems[i].gameObject;
                    checkObjectDistance = Vector3.Distance(detectedItems[i].transform.position, transform.position);
                }
            }
            return closestObjectInCheck;
        }
        else
        {
            return null;
        }
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        ChangeOffsetServerRpc();
        return false;
	}

    public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }

}