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
                Debug.Log("[COMPASS]: Offset timer reached 0, setting new offset!");
                offsetTimer = Random.Range(offsetTimerMin, offsetTimerMax);
                ChangeOffsetServerRpc();
            }

            SetClosestItemServerRpc();
            SetNoiseAmountServerRpc();
        }

        offset = Mathf.Lerp(offset, targetOffset, Time.deltaTime);
        needleBone.transform.localEulerAngles = new Vector3(0f, -transform.eulerAngles.y + offset, 0f);
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

    public void AddDetectedObject(Collider other)
    {
        GrabbableObject gObject = other.gameObject.GetComponent<GrabbableObject>();
        if (gObject != null)
        {
            for (int i = 0; i < detectableItems.Length; i++)
            {
                if (gObject.itemProperties.itemName == detectableItems[i].itemName)
                {
                    Debug.Log($"[COMPASS]: Added {gObject} to detected objects!");
                    detectedObjects.Add(gObject);
                }
            }
        }
    }

    public void RemoveDetectedObject(Collider other)
    {
        GrabbableObject gObject = other.gameObject.GetComponent<GrabbableObject>();
        if (gObject != null)
        {
            if (detectedObjects.Contains(gObject))
            {
                detectedObjects.Remove(gObject);
            }
        }
    }

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
        Debug.Log($"[COMPASS]: Set closest object to {closestObject}.");
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
        if (detectedObjects.Count > 0)
        {
            closestObjectInCheck = detectedObjects[0].gameObject;
            if (detectedObjects.Count == 1)
            {
                return closestObjectInCheck;
            }

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