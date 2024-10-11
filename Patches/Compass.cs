using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;

public class Compass : AnimatedItem, IHittable
{
    
    [Header("Compass Settings")]
    public float detectRange;

    public float alertRange;

    public float alertCooldown;

    private bool alertOnCooldown;

    public Item[] detectableItems;

    public List<GameObject> detectedItems;

    [Space(5f)]
    public AudioClip[] alert;

    public override void Start()
    {
        base.Start();
        Debug.Log("---COMPASS DETECTABLE ITEMS---");
        for (int i = 0; i < detectableItems.Length; i++)
        {
            Debug.Log($"{i}: {detectableItems[i].itemName}");
        }
    }

    public override void Update()
    {
        base.Update();

        DetectItems();

        if (detectedItems.Count > 0)
        {
            //SET SPIN SPEED TO DISTANCE FROM CLOSEST ITEM
            float distance = Vector3.Distance(ClosestItem().transform.position, base.transform.position);
            itemAnimator.speed = Remap(distance, 0, detectRange, 3, 0);

            if (Vector3.Distance(ClosestItem().transform.position, base.transform.position) < alertRange)
            {
                if (!alertOnCooldown)
                {
                    Alert();
                }
            }
        }
    }

    private void DetectItems()
    {
        Collider[] colliders = Physics.OverlapSphere(base.transform.position, detectRange, 67634176, QueryTriggerInteraction.Collide);
        
        for (int i = 0; i < colliders.Length; i++)
        {
            Debug.Log($"{i}: {colliders[i].gameObject}");
            if (colliders[i].gameObject.GetComponent<GrabbableObject>() != null)
            {
                GrabbableObject gObject = colliders[i].gameObject.GetComponent<GrabbableObject>();
                for (int j = 0; j < detectableItems.Length; j++)
                {
                    if (gObject.itemProperties.itemId == detectableItems[j].itemId && !detectedItems.Contains(gObject.gameObject))
                    {
                        detectedItems.Add(gObject.gameObject);
                        Debug.Log($"{gObject} added to detected items.");
                    }
                }
            }
        }

        for (int i = 0; i < detectedItems.Count; i++)
        {
            for (int j = 0; j < colliders.Length; j++)
            {
                if (detectedItems[i] == colliders[j].gameObject)
                {
                    return;
                }
            }
            Debug.Log($"{detectedItems[i]} removed from detected items.");
            detectedItems.Remove(detectedItems[i]);
        }
    }

    // public void OnTouch(Collider other)
    // {
    //     Debug.Log($"Collider {other} entered range of compass.");
    //     if (other.gameObject.GetComponent<GrabbableObject>() != null)
    //     {
    //         Debug.Log($"Item {other.gameObject.GetComponent<GrabbableObject>()} entered range of compass.");
    //         for (int i = 0; i < detectableItems.Length; i++)
    //         {
    //             Debug.Log($"Checking if item is of detectable type {detectableItems[i]}.");
    //             GrabbableObject gObject = other.gameObject.GetComponent<GrabbableObject>();
    //             if (gObject.itemProperties.itemName == detectableItems[i].itemName)
    //             {
    //                 Debug.Log($"Adding {other.gameObject} to detected items.");
    //                 detectedItems.Add(other.gameObject);
    //             }
    //         }
    //         if (detectedItems.Count > 0 && Vector3.Distance(ClosestItem().transform.position, base.transform.position) > alertRange)
    //         {
    //             itemAnimator.Play("loop");
    //         }
    //     }
    // }

    // public void OnExit(Collider other)
    // {
    //     if (detectedItems.Contains(other.gameObject))
    //     {
    //         Debug.Log($"Removing {other.gameObject} from detected items.");
    //         detectedItems.Remove(other.gameObject);
    //     }
    //     if (detectedItems.Count < 1)
    //     {
    //         itemAnimator.Play("idle");
    //     }
    // }

    public GameObject ClosestItem()
    {
        if (detectedItems.Count > 0)
        {
            GameObject closestItem = detectedItems[0];
            float distance = 10000f;
            for (int i = 0; i < detectedItems.Count; i++)
            {
                if (Vector3.Distance(detectedItems[i].transform.position, base.transform.position) < distance)
                {
                    closestItem = detectedItems[i];
                }
            }
            return closestItem;
        }
        else
        {
            return null;
        }
    }

    public void Alert()
    {
        Debug.Log($"Alert triggered by {ClosestItem()}.");
        alertOnCooldown = true;
        StartCoroutine(AlertTimer(alertCooldown));

        //ALERT SOUND
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, alert, randomize: true, 1f, -1);

        //ALERT ANIMATION
        itemAnimator.Play("alert");
    }

    public IEnumerator AlertTimer(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        alertOnCooldown = false;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, alert, randomize: true, 1f, -1);
        return true;
	}

    public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }

}