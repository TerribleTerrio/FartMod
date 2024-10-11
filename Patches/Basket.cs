using Unity.Netcode;
using UnityEngine;

public class BasketSimplified : AnimatedItem
{

    [Header("Basket Settings")]

    private GrabbableObject heldObject = null;

    private bool holdingObject;

    public GameObject displayObject;

    private DepositItemsDesk desk;

    [Space(5f)]
    public AudioClip[] holdItem;

    public AudioClip[] dropItem;

    [Space(5f)]
    public AnimationClip grabObjectAnimation;

    public override void Update()
    {
        base.Update();
        if (holdingObject && heldObject.heldByPlayerOnServer)
        {
            holdingObject = false;
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        if (holdingObject && heldObject != null)
        {
            heldObject.transform.position = displayObject.transform.position;
            heldObject.transform.rotation = displayObject.transform.rotation;
        }
    }

    public override void GrabItem()
    {
        base.GrabItem();
        base.playerHeldBy.equippedUsableItemQE = true;
        if (holdingObject)
        {
            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (heldObject.itemProperties.weight - 1f), 1f, 10f);
            if (heldObject.gameObject.GetComponent<AnimatedItem>() != null)
            {
                StartAnimatedItem();
            }

            if (heldObject.gameObject.GetComponent<InteractTrigger>() != null)
            {
                InteractTrigger trigger = heldObject.gameObject.GetComponent<InteractTrigger>();
                trigger.interactable = false;
            }
        }
    }

    public override void DiscardItem()
    {
        base.playerHeldBy.equippedUsableItemQE = false;
        if (holdingObject)
        {
            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (heldObject.itemProperties.weight - 1f), 1f, 10f);
            if (heldObject.gameObject.GetComponent<AnimatedItem>() != null)
            {
                StopAnimatedItem();
            }

            if (heldObject.gameObject.GetComponent<InteractTrigger>() != null)
            {
                InteractTrigger trigger = heldObject.gameObject.GetComponent<InteractTrigger>();
                trigger.interactable = true;
            }
        }

        base.DiscardItem();
    }

    public override void OnPlaceObject()
    {
        Debug.Log("Basket placed.");
        desk = Object.FindObjectOfType<DepositItemsDesk>();
        base.OnPlaceObject();
        if (heldObject != null)
        {
            heldObject.grabbable = false;
            heldObject.grabbableToEnemies = false;
            heldObject.EnablePhysics(enable: false);
            if (heldObject.gameObject.GetComponent<AnimatedItem>() != null)
            {
                StopAnimatedItem();
            }
            Debug.Log("Basket placed while holding item.");

            if (desk != null && desk.itemsOnCounter.Contains(this) && !desk.itemsOnCounter.Contains(heldObject))
            {
                desk.itemsOnCounter.Add(heldObject);
                desk.itemsOnCounterNetworkObjects.Add(heldObject.gameObject.GetComponent<NetworkObject>());

                for (int i = 0; i < desk.itemsOnCounter.Count; i++)
                {
                    Debug.Log($"Desk item {i}: {desk.itemsOnCounter[i]}");
                    Debug.Log($"Desk network object {i}: {desk.itemsOnCounterNetworkObjects[i]}");
                }
            }
        }
    }

    public bool RequireInteractCooldown()
    {
        if (useCooldown > itemAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.length)
        {
            if (isBeingUsed)
            {
                return false;
            }
            if (currentUseCooldown <= 0f)
            {
                currentUseCooldown = useCooldown;
                return false;
            }
            return true;
        }
        return false;
    }

    public override void ItemInteractLeftRight(bool right)
    {
        Debug.Log("Basket used.");
        base.ItemInteractLeftRight(right);
        if (!right)
        {
            Debug.Log("Basket !right");
            if (!RequireInteractCooldown())
            {
                if (!holdingObject)
                {
                    Debug.Log("No item in basket, attempting detection for item to grab.");
                    if (playerHeldBy == null)
                    {
                        Debug.Log("Aborting item detection for basket, player dropped basket.");
                        return;
                    }

                    Debug.DrawRay(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward * 4f, Color.red, 2f);
                    if (Physics.Raycast(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward, out var hitInfo, 4f, 1073742144, QueryTriggerInteraction.Ignore))
                    {
                        GrabbableObject component = hitInfo.collider.gameObject.GetComponent<GrabbableObject>();
                        if (!(component == null) && !(component == this) && component.itemProperties.isScrap && !component.isHeld && !component.isHeldByEnemy && !component.itemProperties.twoHanded)
                        {
                            Debug.Log($"Found object {component}.");
                            PutObjectInBasket(component);
                        }
                        else
                        {
                            Debug.Log("No item found by basket.");
                        }
                    }
                }
                else
                {
                    Debug.Log("Basket contains item.");
                    RemoveObjectFromBasket();
                }
            }
            else
            {
                Debug.Log("Basket on cooldown.");
            }
        }
    }

    public void PutObjectInBasket(GrabbableObject gObject)
    {
        //SET PARAMETERS TO INDICATE OBJECT IS BEING HELD
        Debug.Log($"Placing {gObject} into basket.");
        heldObject = gObject;
        holdingObject = true;
        heldObject.isHeld = true;

        if (heldObject.gameObject.GetComponent<InteractTrigger>() != null)
        {
            InteractTrigger trigger = heldObject.gameObject.GetComponent<InteractTrigger>();
            trigger.interactable = false;
        }

        //PLAY BASKET AUDIO
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, holdItem, randomize: true, 1f, -1);

        //PLAY GRAB AUDIO FOR ITEM
        AudioClip[] gObjectSFX = new AudioClip[1];
        gObjectSFX[0] = (gObject.itemProperties.grabSFX);
        RoundManager.PlayRandomClip(itemAudio, gObjectSFX, randomize: true, 1f, -1);

        //PLAY GRAB OBJECT ANIMATION
        itemAnimator.Play("grabObject");

        //FOR ANIMATED ITEMS
        if (heldObject is AnimatedItem)
        {
            StartAnimatedItem();
        }

        //ADD WEIGHT TO PLAYER
        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (heldObject.itemProperties.weight - 1f), 1f, 10f);
        Debug.Log($"Added {heldObject.itemProperties.weight} to player weight.");
    }

    private void RemoveObjectFromBasket()
    {
        //CHECK IF HOLDING OBJECT
        if (!holdingObject)
        {
            Debug.Log("Not holding object, aborting remove function.");
            return;
        }

        //PLAY SOUND FOR REMOVING FROM BASKET
        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, dropItem, randomize: true, 1f, -1);

        //PLAY REMOVING OBJECT ANIMATION
        itemAnimator.Play("removeObject");

        //FOR ANIMATED ITEMS
        if (heldObject is AnimatedItem)
        {
            StopAnimatedItem();
        }

        //DROP HELD OBJECT
        if (isInElevator)
        {
            heldObject.gameObject.transform.SetParent(playerHeldBy.playersManager.elevatorTransform, worldPositionStays: true);
        }
        else
        {
            heldObject.gameObject.transform.SetParent(playerHeldBy.playersManager.propsContainer, worldPositionStays: true);
        }
        playerHeldBy.SetItemInElevator(playerHeldBy.isInHangarShipRoom, playerHeldBy.isInElevator, heldObject);
        heldObject.EnablePhysics(enable: true);
        heldObject.gameObject.transform.localScale = heldObject.originalScale;
        heldObject.startFallingPosition = heldObject.transform.parent.InverseTransformPoint(heldObject.transform.position);
        heldObject.FallToGround(randomizePosition: true);
        heldObject.fallTime = UnityEngine.Random.Range(-0.3f, 0.05f);

        //REMOVE WEIGHT OF OBJECT
        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (heldObject.itemProperties.weight - 1f), 1f, 10f);
        Debug.Log($"Removed {heldObject.itemProperties.weight} from player weight.");
        heldObject = null;

        //SET HELD OBJECT CHECKS
        if (heldObject.gameObject.GetComponent<InteractTrigger>() != null)
        {
            InteractTrigger trigger = heldObject.gameObject.GetComponent<InteractTrigger>();
            trigger.interactable = true;
        }
        
        holdingObject = false;
        heldObject.grabbable = true;
        heldObject.grabbableToEnemies = true;
        heldObject = null;
    }

    public void StartAnimatedItem()
    {
        AnimatedItem aObject = heldObject as AnimatedItem;
        if (aObject.chanceToTriggerAlternateMesh > 0)
        {
            if (aObject.itemRandomChance.Next(0, 100) < aObject.chanceToTriggerAlternateMesh)
            {
                aObject.gameObject.GetComponent<MeshFilter>().mesh = aObject.alternateMesh;
                aObject.itemAudio.Stop();
            }
        }
        if (aObject.itemRandomChance.Next(0, 100) > aObject.chanceToTriggerAnimation)
        {
            aObject.itemAudio.Stop();
        }
        if (aObject.itemAnimator != null)
        {
            aObject.itemAnimator.SetBool(aObject.grabItemBoolString, value: true);
        }
        if (aObject.itemAudio != null)
        {
            aObject.itemAudio.clip = aObject.grabAudio;
            aObject.itemAudio.loop = aObject.loopGrabAudio;
            aObject.itemAudio.Play();
        }
    }

    public void StopAnimatedItem()
    {
        AnimatedItem aObject = heldObject as AnimatedItem;
        if (aObject.itemAnimator != null)
        {
            aObject.itemAnimator.SetBool(aObject.grabItemBoolString, value: false);
        }
        if (aObject.chanceToTriggerAlternateMesh > 0)
        {
            aObject.gameObject.GetComponent<MeshFilter>().mesh = aObject.normalMesh;
        }
        if (!aObject.makeAnimationWhenDropping)
        {
            aObject.itemAudio.Stop();
        }
        if (aObject.itemRandomChance.Next(0, 100) < chanceToTriggerAnimation)
        {
            aObject.itemAudio.Stop();
        }
        if (aObject.itemAnimator != null)
        {
            itemAnimator.SetTrigger(dropItemTriggerString);
        }
        if (aObject.itemAudio != null)
        {
            aObject.itemAudio.loop = aObject.loopDropAudio;
            aObject.itemAudio.clip = aObject.dropAudio;
            aObject.itemAudio.Play();
            if (aObject.itemAudioLowPassFilter != null)
            {
                aObject.itemAudioLowPassFilter.cutoffFrequency = 20000f;
            }
            aObject.itemAudio.volume = 1f;
        }
    }
}