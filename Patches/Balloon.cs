using System.Collections;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class Balloon : AnimatedItem
{

    [Space(15f)]
    [Header("Balloon Settings")]
    public float gravityForce = -2;

    public GameObject grabString;

    private bool popped;

    public Rigidbody rigidbody;

    public override void Start()
    {
        //GRABBABLE OBJECT START
        propColliders = base.gameObject.GetComponentsInChildren<Collider>();
        for (int i = 0; i < propColliders.Length; i++)
        {
            if (!propColliders[i].CompareTag("InteractTrigger"))
            {
                propColliders[i].excludeLayers = -2621449;
            }
        }

        originalScale = base.transform.localScale;

        if (itemProperties.isScrap && RoundManager.Instance.mapPropsContainer != null)
        {
            radarIcon = UnityEngine.Object.Instantiate(StartOfRound.Instance.itemRadarIconPrefab, RoundManager.Instance.mapPropsContainer.transform).transform;
        }

        if (!itemProperties.isScrap)
        {
            HoarderBugAI.grabbableObjectsInMap.Add(base.gameObject);
        }

        MeshRenderer[] meshRenderers = base.gameObject.GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            meshRenderers[i].renderingLayerMask = 1u;
        }
        SkinnedMeshRenderer[] skinnedMeshRenderers = base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            skinnedMeshRenderers[i].renderingLayerMask = 1u;
        }

        //ANIMATED ITEM START
        itemRandomChance = new System.Random(StartOfRound.Instance.randomMapSeed + StartOfRound.Instance.currentLevelID + itemProperties.itemId);
        if (chanceToTriggerAlternateMesh > 0)
        {
            normalMesh = base.gameObject.GetComponent<MeshFilter>().mesh;
        }
    }

    public override void Update()
    {
        if (currentUseCooldown >= 0f)
        {
            currentUseCooldown -= Time.deltaTime;
        }

        if (base.IsOwner)
        {
            if (!wasOwnerLastFrame)
            {
                wasOwnerLastFrame = true;
            }
        }
        else if (wasOwnerLastFrame)
        {
            wasOwnerLastFrame = false;
        }

        if (!isHeld && parentObject == null && popped)
        {
            if (fallTime < 1f)
            {
                reachedFloorTarget = false;
                FallWithCurve();
                if (base.transform.localPosition.y - targetFloorPosition.y < 0.05f && !hasHitGround)
                {
                    PlayDropSFX();
                    OnHitGround();
                }
                return;
            }
            if (!reachedFloorTarget)
            {
                if (!hasHitGround)
                {
                    PlayDropSFX();
                    OnHitGround();
                }
                reachedFloorTarget = true;
            }
            base.transform.localPosition = targetFloorPosition;
        }
        else if (isHeld || isHeldByEnemy || !popped)
        {
            reachedFloorTarget = false;
        }

        //BALLOON UPDATE
        if (!isHeld && !isHeldByEnemy && !popped && parentObject == null)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.up, out hit, 1f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                ParentToCeiling(hit);
            }
            else
            {
                //UNPARENT IF CEILING ROTATES UPSIDE DOWN
            }
        }

        if (!popped && transform.position.y > 250f)
        {
            Pop();
        }
    }

    public override void LateUpdate()
    {
        if (radarIcon != null)
        {
            radarIcon.position = base.transform.position;
        }
    }



    //FLOATING UPWARD INSTEAD OF FALLING
    public void ParentToCeiling(RaycastHit hit)
    {
        //DISABLE RIGIDBODY

        base.transform.SetParent(hit.collider.gameObject.transform, worldPositionStays: true);
    }



    //GRABBING AND DISCARDING
    public override void GrabItem()
    {
        base.GrabItem();

        SetHolding();
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        base.GrabItemFromEnemy(enemy);
    }

    public override void DiscardItem()
    {
        base.DiscardItem();

        SetFloating();
    }

    public override void DiscardItemFromEnemy()
    {
        base.DiscardItemFromEnemy();
    }

    public void SetHolding()
    {
        grabString.GetComponent<Rigidbody>().isKinematic = true;
        grabString.transform.position = playerHeldBy.localItemHolder.position;
        grabString.transform.SetParent(playerHeldBy.localItemHolder);

        rigidbody.isKinematic = false;
    }

    public void SetFloating()
    {
        grabString.GetComponent<Rigidbody>().isKinematic = false;
        grabString.transform.SetParent(null);

        rigidbody.isKinematic = false;
    }

    public override void PocketItem()
    {
        if (base.IsOwner && playerHeldBy != null)
        {
            playerHeldBy.IsInspectingItem = false;
        }

        //CLEAR INVENTORY SLOT

        SetFloating();

        playerHeldBy = null;
    }



    //USING ITEM
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);

        //ANIMATE HAND DOWN TO TUG ON BALLOON A BIT
    }



    //COLLISIONS
    public override void ActivatePhysicsTrigger(Collider other)
    {
        base.ActivatePhysicsTrigger(other);

        GameObject otherObject = other.gameObject;

        if (otherObject.tag == "")
        {
            Pop();
        }
    }



    //POPPING
    public void Pop()
    {
        itemAnimator.SetTrigger("Pop");
        popped = true;

        //DISABLE FLOAT STRINGS

        //DISABLE HOLD STRINGS

        EnablePhysics(enable: false);

        fallTime = 0f;
        if (Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, 1000f, 268437760, QueryTriggerInteraction.Ignore))
        {
            targetFloorPosition = hitInfo.point;
            if (base.transform.parent != null)
            {
                targetFloorPosition = base.transform.parent.InverseTransformPoint(targetFloorPosition);
            }
        }
        else
        {
            Debug.Log("[BALLOON]: Popped balloon could not find ground.");
        }

        SetScrapValue(0);
    }

    public override void OnHitGround()
    {
        base.OnHitGround();

        itemAnimator.SetBool("HitGround", true);
    }
}