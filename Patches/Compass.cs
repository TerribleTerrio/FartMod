using System;
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

    private bool broken;

    public Item[] detectableItems;

    public List<GrabbableObject> detectedItems;

    [Space(5f)]
    public AudioClip[] alert;

    public AudioClip[] breakSFX;

    public override void Start()
    {
        base.Start();
        LoadDetectedItems();

        if (broken)
        {
            itemAnimator.Play("broken");
        }
    }

    public override void Update()
    {
        base.Update();

        if (broken)
        {
            return;
        }

        if (detectedItems.Count > 0)
        {
            for (int i = 0; i < detectedItems.Count; i++)
            {
                if (detectedItems[i].gameObject == null)
                {
                    detectedItems.Remove(detectedItems[i]);
                }
            }

            if (ClosestItem() != null)
            {
                float distance = Vector3.Distance(ClosestItem().transform.position, base.transform.position);
                itemAnimator.SetFloat("spinSpeed", Remap(distance, 0, detectRange, 5, 0));

                if (Vector3.Distance(ClosestItem().transform.position, base.transform.position) < alertRange)
                {
                    if (!alertOnCooldown)
                    {
                        Alert();
                    }
                }
            }
        }

        else
        {
            itemAnimator.SetFloat("spinSpeed", 0);
        }
    }

    public void OnEnterTrigger(Collider other = null)
    {
        if (other == null || other.gameObject.GetComponent<PlayerControllerB>() != null)
        {
            LoadDetectedItems();
        }
    }

    public override void GrabItem()
    {
        base.GrabItem();
        LoadDetectedItems();
    }

    public void LoadDetectedItems()
    {
        GrabbableObject[] allItems = FindObjectsOfType<GrabbableObject>();
        detectedItems = allItems.ToList();

        for (int i = 0; i < detectedItems.Count; i++)
        {
            bool detected = false;
            for (int j = 0; j < detectableItems.Length; j++)
            {
                if (detectedItems[i].itemProperties.itemName != detectableItems[j].itemName)
                {
                    continue;
                }
                else
                {
                    detected = true;
                }
            }
            if (!detected)
            {
                detectedItems.Remove(detectedItems[i]);
            }
        }
    }

    public GameObject ClosestItem()
    {
        if (detectedItems.Count > 0 && detectedItems[0] != null)
        {
            GameObject closestItem = detectedItems[0].gameObject;
            for (int i = 0; i < detectedItems.Count; i++)
            {
                if (detectedItems[i] != null)
                {
                    if (Vector3.Distance(detectedItems[i].transform.position, base.transform.position) < Vector3.Distance(closestItem.transform.position, base.transform.position))
                    {
                        closestItem = detectedItems[i].gameObject;
                    }
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

        itemAnimator.Play("alert");
    }

    public IEnumerator AlertTimer(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        alertOnCooldown = false;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (!broken)
        {
            broken = true;
            itemAnimator.SetBool("broken", true);

            if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
            {
                timesPlayedInOneSpot = 0;
            }
            timesPlayedInOneSpot++;
            lastPosition = base.transform.position;

            RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
            RoundManager.PlayRandomClip(itemAudio, breakSFX, randomize: true, 1f, -1);
        }
        
        return true;
	}

    public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }

}