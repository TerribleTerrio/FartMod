using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Basket : AnimatedItem
{

    [Space(15f)]
    [Header("Basket Settings")]
    private GrabbableObject basketObject = null;

    public Transform itemHolder;

    public Item[] excludedItems;

    private DepositItemsDesk desk;

    private float itemActivateTimer;

    private float itemActivateTimerMin = 300f;

    private float itemActivateTimerMax = 1000f;

    private float itemActivateChance = 20f;

    [Space(10f)]
    [Header("Item Offsets")]
    public Transform[] itemOffsets;

    public Item[] offsetItemTypes;

    private Transform defaultOffset;

    public override void Start()
    {
        base.Start();
        defaultOffset.position = itemHolder.position;
        defaultOffset.rotation = itemHolder.rotation;
    }

    public override void Update()
    {
        base.Update();

        if (basketObject != null)
        {
            if (!basketObject.scrapPersistedThroughRounds && playerHeldBy != null)
            {
                if (playerHeldBy.isInHangarShipRoom)
                {
                    RoundManager.Instance.CollectNewScrapForThisRound(basketObject);
                }
            }

            if (basketObject.playerHeldBy != null)
            {
                ClearBasketObjectServerRpc();
            }
        }

        if (playerHeldBy != null && basketObject != null)
        {
            if (playerHeldBy.isWalking)
            {
                itemActivateTimer--;
                
                if (playerHeldBy.isSprinting)
                {
                    itemActivateTimer--;
                }

                if (itemActivateTimer <= 0)
                {
                    Debug.Log("[BASKET]: Item activate timer reached 0.");
                    itemActivateTimer = Random.Range(itemActivateTimerMin, itemActivateTimerMax);
                    float c = Random.Range(0f,100f);
                    if (c < itemActivateChance)
                    {
                        Debug.Log("[BASKET]: Item activated!");
                        GrabbableObject gObject = basketObject.GetComponent<GrabbableObject>();
                        if (gObject != null)
                        {
                            if (gObject.itemProperties.syncUseFunction)
                            {
                                isSendingItemRPC++;
                                gObject.ActivateItemServerRpc(gObject.isBeingUsed, true);
                            }
                            gObject.ItemActivate(gObject.isBeingUsed, true);
                        }
                    }
                }
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearBasketObjectServerRpc()
    {
        ClearBasketObjectClientRpc();
    }

    [ClientRpc]
    public void ClearBasketObjectClientRpc()
    {
        ClearBasketObject();
    }

    public void ClearBasketObject()
    {
        itemAnimator.SetTrigger("removeObject");
        basketObject.parentObject = basketObject.playerHeldBy.localItemHolder;

        if (isHeld)
        {
            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (basketObject.itemProperties.weight - 1f), 1f, 10f);
        }

        basketObject = null;
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
            RoundManager.Instance.CollectNewScrapForThisRound(basketObject);
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
            basketObject.EnablePhysics(enable: false);
            playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (basketObject.itemProperties.weight - 1f), 1f, 10f);
            if (basketObject.gameObject.GetComponent<AnimatedItem>() != null)
            {
                StartAnimatedItem();
            }
        }

        itemActivateTimer = Random.Range(itemActivateTimerMin, itemActivateTimerMax);
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
        }

        base.DiscardItem();
    }

    public override void OnPlaceObject()
    {
        Debug.Log("[BASKET]: Basket placed.");
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
            Debug.Log("[BASKET]: Basket placed while holding item.");

            if (desk != null && desk.itemsOnCounter.Contains(this) && !desk.itemsOnCounter.Contains(basketObject))
            {
                desk.itemsOnCounter.Add(basketObject);
                desk.itemsOnCounterNetworkObjects.Add(basketObject.gameObject.GetComponent<NetworkObject>());

                for (int i = 0; i < desk.itemsOnCounter.Count; i++)
                {
                    Debug.Log($"[BASKET]: Desk item {i}: {desk.itemsOnCounter[i]}");
                    Debug.Log($"[BASKET]: Desk network object {i}: {desk.itemsOnCounterNetworkObjects[i]}");
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
        Debug.Log("[BASKET]: Basket used.");
        base.ItemInteractLeftRight(right);

        if (right)
        {
            if ((int)playerHeldBy.playerClientId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
            {
                TryGetBasketObject();
            }
        }
        else
        {
            if ((int)playerHeldBy.playerClientId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
            {
                TryDropBasketObject();
            }
        }
    }

    public void TryGetBasketObject()
    {
        if (!RequireInteractCooldown())
        {
            if (basketObject == null)
            {
                if (playerHeldBy == null)
                {
                    return;
                }

                if (Physics.Raycast(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward, out var hitInfo, 4f, 1073742144, QueryTriggerInteraction.Ignore))
                {
                    GrabbableObject gObject = hitInfo.collider.gameObject.GetComponent<GrabbableObject>();
                    if (!(gObject == null) && !(gObject == this) && gObject.itemProperties.isScrap && !gObject.isHeld && !gObject.isHeldByEnemy)
                    {
                        for (int i = 0; i < excludedItems.Length; i++)
                        {
                            if (gObject.itemProperties.itemName == excludedItems[i].itemName)
                            {
                                Debug.Log($"[BASKET]: Item {gObject} is in excluded items list.");
                                return;
                            }
                        }
                        if (gObject.itemProperties.twoHanded)
                        {
                            return;
                        }
                        else
                        {
                            Debug.Log($"[BASKET]: Found object {gObject}.");
                            PutObjectInBasketAndSync(gObject);
                        }
                    }
                    else
                    {
                        Debug.Log("[BASKET]: No item found.");
                        return;
                    }
                }
            }
        }
    }

    public void TryDropBasketObject()
    {
        if (!RequireInteractCooldown())
        {
            if (basketObject != null)
            {
                Debug.Log("[BASKET]: Item in basket, attempting to drop.");
                if (playerHeldBy == null)
                {
                    Debug.Log("[BASKET]: Aborting item item drop for basket, player dropped basket.");
                    return;
                }
                else
                {
                    RemoveObjectFromBasketAndSync();
                }
            }
        }
    }

    public void PutObjectInBasketAndSync(GrabbableObject gObject)
    {
        PutObjectInBasket(gObject);
        gObject.EnablePhysics(enable: false);

        PutObjectInBasketServerRpc(gObject.NetworkObjectId, (int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PutObjectInBasketServerRpc(ulong id, int clientWhoSentRpc)
    {
        PutObjectInBasketClientRpc(id, clientWhoSentRpc);
    }

    [ClientRpc]
    public void PutObjectInBasketClientRpc(ulong id, int clientWhoSentRpc)
    {
        GrabbableObject gObject = NetworkManager.SpawnManager.SpawnedObjects[id].gameObject.GetComponent<GrabbableObject>();

        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            PutObjectInBasket(gObject);
        }
    }

    public void PutObjectInBasket(GrabbableObject gObject)
    {
        basketObject = gObject;

        for (int i = 0; i < offsetItemTypes.Length; i++)
        {
            if (basketObject.itemProperties.itemName == offsetItemTypes[i].itemName)
            {
                itemHolder.position = itemOffsets[i].position;
                itemHolder.rotation = itemOffsets[i].rotation;
            }
            else
            {
                itemHolder.position = defaultOffset.position;
                itemHolder.rotation = defaultOffset.rotation;
            }
        }

        //SET PARAMETERS TO INDICATE OBJECT IS BEING HELD
        if (basketObject.parentObject != null)
        {
            basketObject.parentObject = null;
        }

        //PLAY BASKET AUDIO
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

        //PLAY GRAB AUDIO FOR ITEM
        AudioClip[] gObjectSFX = new AudioClip[1];
        gObjectSFX[0] = (basketObject.itemProperties.grabSFX);
        RoundManager.PlayRandomClip(itemAudio, gObjectSFX, randomize: true, 1f, -1);

        //PLAY GRAB OBJECT ANIMATION
        itemAnimator.SetTrigger("grabObject");

        //FOR ANIMATED ITEMS
        if (basketObject is AnimatedItem)
        {
            StartAnimatedItem();
        }

        //ADD WEIGHT TO PLAYER
        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (basketObject.itemProperties.weight - 1f), 1f, 10f);
        Debug.Log($"[BASKET]: Added {basketObject.itemProperties.weight} to player weight.");
    }

    public void RemoveObjectFromBasketAndSync()
    {
        RemoveObjectFromBasket();
        RemoveObjectFromBasketServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveObjectFromBasketServerRpc(int clientWhoSentRpc)
    {
        RemoveObjectFromBasketClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    public void RemoveObjectFromBasketClientRpc(int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            RemoveObjectFromBasket();
        }
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

        //PLAY REMOVING OBJECT ANIMATION
        itemAnimator.SetTrigger("removeObject");

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

        basketObject.EnablePhysics(enable: true);
        basketObject.EnableItemMeshes(enable: true);
        basketObject.isHeld = false;
        basketObject.hasHitGround = false;
        basketObject.fallTime = 0f;
        basketObject.startFallingPosition = basketObject.transform.parent.InverseTransformPoint(basketObject.transform.position);
        basketObject.targetFloorPosition = targetFloorPosition + Vector3.up * basketObject.itemProperties.verticalOffset - Vector3.up * itemProperties.verticalOffset;
        basketObject.floorYRot = floorYRot;
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