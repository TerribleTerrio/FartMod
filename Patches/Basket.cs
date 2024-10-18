using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BasketSimplified : AnimatedItem
{

    [Header("Basket Settings")]

    private GrabbableObject basketObject = null;

    public Transform itemHolder;

    private DepositItemsDesk desk;

    [Space(5f)]
    public AudioClip[] holdItem;

    public AudioClip[] dropItem;

    [Space(5f)]
    public AnimationClip grabObjectAnimation;

    public override void Update()
    {
        base.Update();
        if (basketObject != null)
        {
            if (!basketObject.scrapPersistedThroughRounds && playerHeldBy != null)
            {
                if (playerHeldBy.isInHangarShipRoom)
                {
                    CollectHeldItem();
                }
            }

            if (basketObject.heldByPlayerOnServer)
            {
                basketObject = null;
            }
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        if (basketObject != null)
        {
            basketObject.transform.position = itemHolder.position;
            basketObject.transform.rotation = itemHolder.rotation;
        }
    }

    public override void OnBroughtToShip()
    {
        base.OnBroughtToShip();
        if (basketObject != null)
        {
            CollectHeldItem();
        }
    }

    public void CollectHeldItem()
    {
        if (!basketObject.scrapPersistedThroughRounds)
        {
            if (playerHeldBy.isInHangarShipRoom)
            {
                RoundManager.Instance.scrapCollectedInLevel += basketObject.scrapValue;
                StartOfRound.Instance.gameStats.allPlayerStats[playerHeldBy.playerClientId].profitable += basketObject.scrapValue;
                RoundManager.Instance.CollectNewScrapForThisRound(basketObject);
                basketObject.OnBroughtToShip();

                if (basketObject.itemProperties.isScrap && Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, basketObject.transform.position) < 12f)
                {
                    HUDManager.Instance.DisplayTip("Got scrap!", "To sell, use the terminal to route the ship to the company building.", isWarning: false, useSave: true, "LCTip_SellScrap");
                }
            }
            else
            {
                if (!basketObject.scrapPersistedThroughRounds)
                {
                    RoundManager.Instance.scrapCollectedInLevel -= basketObject.scrapValue;
                    StartOfRound.Instance.gameStats.allPlayerStats[playerHeldBy.playerClientId].profitable -= basketObject.scrapValue;
                }
                HUDManager.Instance.SetQuota(RoundManager.Instance.scrapCollectedInLevel);
            }
            if (playerHeldBy.isInHangarShipRoom)
            {
                StartOfRound.Instance.currentShipItemCount++;
            }
            else
            {
                StartOfRound.Instance.currentShipItemCount--;
            }
        }
    }

    public IEnumerator DetectDroppedItemHitGround(GrabbableObject gObject)
    {
        yield return new WaitUntil(() => gObject.hasHitGround);

        gObject.PlayDropSFX();
    }

    public override void GrabItem()
    {
        base.GrabItem();
        base.playerHeldBy.equippedUsableItemQE = true;
        if (basketObject != null)
        {
            basketObject.EnablePhysics(enable: true);
            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (basketObject.itemProperties.weight - 1f), 1f, 10f);
            if (basketObject.gameObject.GetComponent<AnimatedItem>() != null)
            {
                StartAnimatedItem();
            }

            if (basketObject.gameObject.GetComponent<InteractTrigger>() != null)
            {
                InteractTrigger trigger = basketObject.gameObject.GetComponent<InteractTrigger>();
                trigger.interactable = false;
            }
        }
    }

    public override void DiscardItem()
    {
        base.playerHeldBy.equippedUsableItemQE = false;
        if (basketObject != null)
        {
            basketObject.EnablePhysics(enable: true);
            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (basketObject.itemProperties.weight - 1f), 1f, 10f);
            if (basketObject.gameObject.GetComponent<AnimatedItem>() != null)
            {
                StopAnimatedItem();
            }

            if (basketObject.gameObject.GetComponent<InteractTrigger>() != null)
            {
                InteractTrigger trigger = basketObject.gameObject.GetComponent<InteractTrigger>();
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
        if (basketObject != null)
        {
            basketObject.grabbable = false;
            basketObject.grabbableToEnemies = false;
            basketObject.EnablePhysics(enable: true);
            if (basketObject.gameObject.GetComponent<AnimatedItem>() != null)
            {
                StopAnimatedItem();
            }
            Debug.Log("Basket placed while holding item.");

            if (desk != null && desk.itemsOnCounter.Contains(this) && !desk.itemsOnCounter.Contains(basketObject))
            {
                desk.itemsOnCounter.Add(basketObject);
                desk.itemsOnCounterNetworkObjects.Add(basketObject.gameObject.GetComponent<NetworkObject>());

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
                if (basketObject == null)
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
        basketObject = gObject;
        basketObject.EnablePhysics(enable: false);
        if (basketObject.parentObject != null)
        {
            basketObject.parentObject = null;
        }

        //PLACE OBJECT INTO BASKET
        Debug.Log($"Basket position: {transform.position}");
        Debug.Log($"itemHolder position: {itemHolder.transform.position}");
        Debug.Log($"Basket object position: {basketObject.transform.position}");

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
        if (basketObject is AnimatedItem)
        {
            StartAnimatedItem();
        }

        //ADD WEIGHT TO PLAYER
        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (basketObject.itemProperties.weight - 1f), 1f, 10f);
        Debug.Log($"Added {basketObject.itemProperties.weight} to player weight.");
    }

    private void RemoveObjectFromBasket()
    {
        //CHECK IF HOLDING OBJECT
        if (basketObject == null)
        {
            return;
        }

        //PLAY SOUND FOR REMOVING FROM BASKET
        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, dropItem, randomize: true, 1f, -1);

        //PLAY REMOVING OBJECT ANIMATION
        itemAnimator.Play("removeObject");

        //FOR ANIMATED ITEMS
        if (basketObject is AnimatedItem)
        {
            StopAnimatedItem();
        }

        //DROP BASKET OBJECT
        DiscardBasketObject();

        //REMOVE WEIGHT OF OBJECT
        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (basketObject.itemProperties.weight - 1f), 1f, 10f);

        //PLAY SOUND FOR DROPPED OBJECT
        GrabbableObject droppedObject = basketObject;
        StartCoroutine(DetectDroppedItemHitGround(droppedObject));

        basketObject = null;
    }

    public void DiscardBasketObject()
    {
        bool droppedInElevator = false;
        bool droppedInShipRoom = false;
        bool droppedInPhysicsRegion = false;
        Vector3 targetFloorPosition = GetItemFloorPosition();

        //SET FLOOR Y ROTATION
        int floorYRot = (int)basketObject.transform.localEulerAngles.y;

        //CHECK WHETHER OBJECT ALLOWS DROPPING AHEAD OF PLAYER
        if (basketObject.itemProperties.allowDroppingAheadOfPlayer && !playerHeldBy.isInElevator)
        {
            targetFloorPosition = playerHeldBy.DropItemAheadOfPlayer();
        }

        //CHECK IF PLAYER IS IN ELEVATOR (ON SHIP)
        if (playerHeldBy.isInElevator)
        {
            droppedInElevator = true;
            targetFloorPosition = StartOfRound.Instance.elevatorTransform.InverseTransformPoint(targetFloorPosition);
            basketObject.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
            
            //CHECK IF ITEM IS DROPPED IN SHIP ROOM
            if (playerHeldBy.isInHangarShipRoom)
            {
                droppedInShipRoom = true;
            }
        }

        //CHECK IF ITEM IS DROPPED IN PHYSICSREGION
        Vector3 hitPoint;
        NetworkObject physicsRegionOfDroppedObject = basketObject.GetPhysicsRegionOfDroppedObject(playerHeldBy, out hitPoint);
        if (physicsRegionOfDroppedObject != null)
        {
            droppedInPhysicsRegion = true;
            targetFloorPosition = hitPoint;
            basketObject.transform.SetParent(physicsRegionOfDroppedObject.transform, worldPositionStays: true);

            PlayerPhysicsRegion componentInChildren = physicsRegionOfDroppedObject.GetComponentInChildren<PlayerPhysicsRegion>();
            if (componentInChildren != null && componentInChildren.allowDroppingItems)
            {
                // targetFloorPosition = componentInChildren.transform.InverseTransformPoint(targetFloorPosition);
                basketObject.transform.SetParent(componentInChildren.physicsTransform, worldPositionStays: true);
            }
        }

        if (!droppedInElevator && !droppedInPhysicsRegion)
        {
            targetFloorPosition = StartOfRound.Instance.propsContainer.InverseTransformPoint(targetFloorPosition);
            basketObject.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
        }

        basketObject.heldByPlayerOnServer = false;
        basketObject.parentObject = null;
        basketObject.isInElevator = droppedInElevator;

        CollectHeldItem();

        basketObject.EnablePhysics(enable: true);
        basketObject.EnableItemMeshes(enable: true);
        basketObject.isHeld = false;
        basketObject.hasHitGround = false;
        basketObject.fallTime = 0f;
        basketObject.startFallingPosition = basketObject.transform.parent.InverseTransformPoint(basketObject.transform.position);
        basketObject.targetFloorPosition = targetFloorPosition + Vector3.up * basketObject.itemProperties.verticalOffset - Vector3.up * itemProperties.verticalOffset;
        basketObject.floorYRot = floorYRot;

        Debug.Log($"Basket object start falling position: {basketObject.startFallingPosition}");
        Debug.Log($"Basket object target floor position: {basketObject.targetFloorPosition}");
        Debug.Log($"Basket object parentObject: {basketObject.parentObject}");
        Debug.Log($"Basket object parent: {basketObject.transform.parent}");
        Debug.Log($"Dropped in ship room: {droppedInShipRoom}");
        Debug.Log($"Dropped in elevator: {droppedInElevator}");
    }

    public void StartAnimatedItem()
    {
        AnimatedItem aObject = basketObject as AnimatedItem;
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
        AnimatedItem aObject = basketObject as AnimatedItem;
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