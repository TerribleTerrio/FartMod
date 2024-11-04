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

    private bool active;

    private GameObject closestObject;

    private float closestObjectDistance;

    private Coroutine loadItemsCoroutine;

    private Coroutine changeOffsetCoroutine;

    public Item[] detectableItems;

    private List<GrabbableObject> detectedObjects = new List<GrabbableObject>();

    public GameObject needleBone;

    private float rotationCurrent;

    private float offset;

    private float targetOffset;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource noiseAudio;

    public override void Start()
    {
        base.Start();
        rotationCurrent = transform.eulerAngles.y;
    }

    public override void Update()
    {
        base.Update();

        // SetDirection();

        if (active && loadItemsCoroutine == null && base.IsOwner)
        {
            LoadDetectedObjectsServerRpc();
            loadItemsCoroutine = StartCoroutine(Cooldown(Random.Range(2f, 5f), loadItemsCoroutine));
        }

        if (active && changeOffsetCoroutine == null && base.IsOwner)
        {
            ChangeOffsetServerRpc();
            changeOffsetCoroutine = StartCoroutine(Cooldown(Random.Range(30f,120f), changeOffsetCoroutine));
        }

        float noiseAmount = itemAnimator.GetFloat("NoiseAmount");
        float targetNoiseAmount;

        if (closestObject != null)
        {
            targetNoiseAmount = Remap(closestObjectDistance, 0f, detectRange, 1f, 0f);
        }
        else
        {
            targetNoiseAmount = 0f;
        }

        noiseAmount = Mathf.Lerp(noiseAmount, targetNoiseAmount, Time.deltaTime);
        itemAnimator.SetLayerWeight(1, noiseAmount);
        noiseAudio.volume = noiseAmount;

        offset = Mathf.Lerp(offset, targetOffset, Time.deltaTime);

        needleBone.transform.localEulerAngles = new Vector3(0f, -transform.eulerAngles.y + offset, 0f);
    }

    [ServerRpc]
    public void LoadDetectedObjectsServerRpc()
    {
        detectedObjects.Clear();
        GrabbableObject[] objectsInMap = Object.FindObjectsOfType<GrabbableObject>();
        for (int i = 0; i < objectsInMap.Length; i++)
        {
            GrabbableObject gObject = objectsInMap[i];
            for (int j = 0; j < detectableItems.Length; j++)
            {
                if (gObject.itemProperties.itemName == detectableItems[j].itemName)
                {
                    detectedObjects.Add(gObject);
                    Debug.Log($"Compass found {gObject}!");
                }
            }
        }

        ClosestObject().GetComponent<NetworkObject>();
    }

    [ClientRpc]
    public void LoadDetectedObjectsClientRpc()
    {
        detectedObjects.Clear();
        GrabbableObject[] objectsInMap = Object.FindObjectsOfType<GrabbableObject>();
        for (int i = 0; i < objectsInMap.Length; i++)
        {
            GrabbableObject gObject = objectsInMap[i];
            for (int j = 0; j < detectableItems.Length; j++)
            {
                if (gObject.itemProperties.itemName == detectableItems[j].itemName)
                {
                    detectedObjects.Add(gObject);
                    Debug.Log($"Compass found {gObject}!");
                }
            }
        }

        closestObject = ClosestObject();
    }

    [ServerRpc]
    public void ChangeOffsetServerRpc()
    {
        float offset = Random.Range(0f,360f);
        ChangeOffsetClientRpc(offset);
    }

    [ClientRpc]
    public void ChangeOffsetClientRpc(float offset)
    {
        itemAnimator.SetTrigger("Spin");
        targetOffset = offset;
        changeOffsetCoroutine = StartCoroutine(Cooldown(Random.Range(30f,120f), changeOffsetCoroutine));
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
            GameObject closestObjectSoFar = detectedObjects[0].gameObject;
            float checkObjectDistance = Vector3.Distance(detectedObjects[0].transform.position, base.transform.position);
            for (int i = 1; i < detectedObjects.Count; i++)
            {
                if (Vector3.Distance(detectedObjects[i].transform.position, transform.position) < checkObjectDistance)
                {
                    closestObjectSoFar = detectedObjects[i].gameObject;
                    checkObjectDistance = Vector3.Distance(detectedObjects[i].transform.position, transform.position);
                }
            }

            Debug.Log($"Compass closest object: {closestObject}");
            return closestObjectSoFar;
        }
        else
        {
            return null;
        }
    }

    private IEnumerator Cooldown(float time, Coroutine cooldown)
    {
        yield return new WaitForSeconds(time);
        cooldown = null;
    }

    public void OnTriggerStay(Collider other)
    {
        PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();

        if (player != null)
        {
            active = true;
        }
    }

    public override void GrabItem()
    {
        base.GrabItem();
        if (base.IsOwner)
        {
            LoadDetectedObjectsServerRpc();
        }
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        itemAnimator.SetTrigger("Spin");

        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        
        return true;
	}

    public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }

}