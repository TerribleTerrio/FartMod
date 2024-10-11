using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Analytics;

public class Rake : GrabbableObject, ITouchable
{

    [Header("Rake Settings")]
    public int damageDealtPlayer;

    public int damageDealtEnemy;

    public float damageRange;

    public float physicsForce;

    public float upForce;

    public float coolDownTime;

    private bool onCoolDown;

    public GameObject animContainer;

    private PlayerControllerB lastHeld;

    public GameObject itemCollider;

    private Animator animator;

    private List<GameObject> objectsTouching;

    private Vector3 lastPositionAtFlip;

    [Space(5f)]
    public AudioSource rakeAudio;

    public AudioClip[] rakeFlip;

    public AudioClip[] rakeFall;

    public AudioClip reelUp;

    public AudioClip rakeSwing;

    public AudioClip[] hitSFX;

    private int timesPlayingInOneSpot = 0;

    [Space(5f)]
    [Header("Weapon Variables")]
    public int rakeHitForce = 1;

    public bool reelingUp;

    public bool isHoldingButton;

    private RaycastHit rayHit;

    private Coroutine reelingUpCoroutine;

    private RaycastHit[] objectsHitByRake;

    private List<RaycastHit> objectsHitByRakeList = new List<RaycastHit>();

    private PlayerControllerB previousPlayerHeldBy;

    private int rakeMask = 1084754248;

    public override void Start()
    {
        animator = animContainer.gameObject.GetComponent<Animator>();

        objectsTouching = new List<GameObject>();

        base.Start();
    }

    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        if (isHeld || !hasHitGround)
        {
            return;
        }

        //PLAYER COLLISION
        if (otherObject.layer == 3 && objectsTouching.Count() < 1 && lastHeld != otherObject.GetComponent<PlayerControllerB>())
        {
            if (objectsTouching.Count() < 1 && !onCoolDown && animator.GetCurrentAnimatorStateInfo(0).IsName("sit"))
            {
                Flip();
            }
            objectsTouching.Add(otherObject);
        }

        //ENEMY COLLISION
        else if (other.gameObject.layer == 19)
        {
            EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
            if (objectsTouching.Count() <1 && !onCoolDown && animator.GetCurrentAnimatorStateInfo(0).IsName("sit"))
            {
                if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
                {
                }
                else if (enemy.mainScript.enemyType.enemyName == "Red Locust Bees")
                {
                }
                else if (enemy.mainScript.enemyType.enemyName == "Butler Bees")
                {
                }
                else if (enemy.mainScript.enemyType.enemyName == "Docile Locust Bees")
                {
                }
                else if (enemy.mainScript.enemyType.enemyName == "Flowerman")
                {
                    FlowermanAI flowerman = enemy.mainScript as FlowermanAI;
                    if (flowerman.isInAngerMode)
                    {
                        Flip();
                    }
                    else
                    {
                    }
                }
                else
                {
                    Flip();
                }
            }
            objectsTouching.Add(otherObject);
        }

        else
        {
        }

    }

    public void OnExit(Collider other)
    {
        GameObject otherObject = other.gameObject;

        if (objectsTouching.Contains(otherObject))
        {
            objectsTouching.Remove(otherObject);
        }

        if (lastHeld == otherObject.GetComponent<PlayerControllerB>())
        {
            lastHeld = null;
        }

        if (objectsTouching.Count < 1 && !animator.GetCurrentAnimatorStateInfo(0).IsName("fall") && !animator.GetCurrentAnimatorStateInfo(0).IsName("sit"))
        {
            Fall();
        }

    }

    private void Flip()
    {
        if (Vector3.Distance(lastPositionAtFlip, base.transform.position) > 2f)
        {
            timesPlayingInOneSpot = 0;
        }
        timesPlayingInOneSpot = timesPlayingInOneSpot + 5;
        lastPositionAtFlip = base.transform.position;

        onCoolDown = true;
        animator.Play("flip");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 10f, 1f, timesPlayingInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(rakeAudio, rakeFlip, randomize: true, 1f, -1);

        Collider[] array = Physics.OverlapSphere(base.transform.position, damageRange, 2621448, QueryTriggerInteraction.Collide);
        List<EnemyAI> hitEnemies = new List<EnemyAI>();
        PlayerControllerB playerControllerB = null;
        RaycastHit hitInfo;

        for (int i =0; i < array.Length; i++)
        {
            float dist = Vector3.Distance(array[i].transform.position, base.transform.position);

            //FOR ALL NEARBY PLAYERS
            if (array[i].gameObject.layer == 3)
            {
                PlayerControllerB player = array[i].gameObject.GetComponent<PlayerControllerB>();

                //DAMAGE ALL CLOSE PLAYERS
                Vector3 bodyVelocity = Vector3.Normalize(player.transform.position - base.transform.position) * 80f / Vector3.Distance(player.transform.position, base.transform.position);
                player.DamagePlayer(damageDealtPlayer, hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning, 0, fallDamage:false, bodyVelocity);

                //DROP HELD ITEM OF ALL CLOSE PLAYERS
                if (player.isHoldingObject)
                {
                    DisarmPlayer(player);
                }
            }

            //FOR ALL NEARBY ENEMIES
            if (array[i].gameObject.layer == 19)
            {
                //DAMAGE ALL CLOSE ENEMIES
                EnemyAICollisionDetect enemy = array[i].gameObject.GetComponentInChildren<EnemyAICollisionDetect>();
                if (enemy != null && enemy.mainScript.IsOwner && !hitEnemies.Contains(enemy.mainScript))
                {
                    if (enemy.mainScript.enemyType.enemyName == "Crawler")
                    {
                        CrawlerAI crawler = enemy.mainScript as CrawlerAI;
                        if (crawler.hasEnteredChaseMode)
                        {
                            hitEnemies.Add(enemy.mainScript);
                            enemy.mainScript.HitEnemyOnLocalClient(damageDealtEnemy, playerWhoHit: lastHeld, playHitSFX: true, hitID: 1);
                        }
                    }
                    else if (enemy.mainScript.enemyType.enemyName == "Bunker Spider")
                    {
                        SandSpiderAI spider = enemy.mainScript as SandSpiderAI;
                        if (spider.movingTowardsTargetPlayer)
                        {
                            hitEnemies.Add(enemy.mainScript);
                            enemy.mainScript.HitEnemyOnLocalClient(damageDealtEnemy, playerWhoHit: lastHeld, playHitSFX: true, hitID: 1);
                        }
                    }
                    else if (enemy.mainScript.enemyType.enemyName == "Flowerman")
                    {
                    }
                    else
                    {
                        hitEnemies.Add(enemy.mainScript);
                        enemy.mainScript.HitEnemyOnLocalClient(damageDealtEnemy, playerWhoHit: lastHeld, playHitSFX: true, hitID: 1);
                    }
                }
                else if (enemy != null)
                {
                }
            }
        }

        playerControllerB = GameNetworkManager.Instance.localPlayerController;

        //PHYSICS FORCE
        if (physicsForce > 0f && !Physics.Linecast(base.transform.position, playerControllerB.transform.position + Vector3.up * upForce, out hitInfo, 256, QueryTriggerInteraction.Ignore))
        {
            float dist = Vector3.Distance(playerControllerB.transform.position, base.transform.position);
            Vector3 vector = Vector3.Normalize(playerControllerB.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
            if (vector.magnitude > 2f)
            {
                if (vector.magnitude > 10f)
                {
                    playerControllerB.CancelSpecialTriggerAnimations();
                }
                if (!playerControllerB.inVehicleAnimation || (playerControllerB.externalForceAutoFade + vector).magnitude > 50f)
                {
                    playerControllerB.externalForceAutoFade += vector;
                }
            }
        }
    }

    private void Fall()
    {
        StartCoroutine(CoolDown(coolDownTime));
        if (Vector3.Distance(lastPositionAtFlip, base.transform.position) > 2f)
        {
            timesPlayingInOneSpot = 0;
        }
        timesPlayingInOneSpot = timesPlayingInOneSpot + 5;
        lastPositionAtFlip = base.transform.position;
        
        animator.Play("fall");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 10f, 1f, timesPlayingInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(rakeAudio, rakeFall, randomize: true, 1f, -1);
    }

    public void DisarmPlayer(PlayerControllerB player, bool itemsFall = true, bool disconnecting = false)
    {
        GrabbableObject gObject = player.currentlyHeldObjectServer;
        int itemSlot = player.currentItemSlot;
        if (!player.isHoldingObject)
        {
            return;
        }
        if (itemsFall)
        {
            gObject.parentObject = null;
            gObject.heldByPlayerOnServer = false;
            if (isInElevator)
            {
                gObject.transform.SetParent(player.playersManager.elevatorTransform, worldPositionStays: true);
            }
            else
            {
                gObject.transform.SetParent(player.playersManager.propsContainer, worldPositionStays: true);
            }
            player.SetItemInElevator(player.isInHangarShipRoom, player.isInElevator, gObject);
            gObject.EnablePhysics(enable: true);
            gObject.transform.localScale = gObject.originalScale;
            gObject.isHeld = false;
            gObject.isPocketed = false;
            gObject.startFallingPosition = gObject.transform.parent.InverseTransformPoint(gObject.transform.position);
            gObject.FallToGround(randomizePosition: true);
            gObject.fallTime = UnityEngine.Random.Range(-0.3f, 0.05f);
            if (player.IsOwner)
            {
                gObject.DiscardItemOnClient();
            }
            else if (!gObject.itemProperties.syncDiscardFunction)
            {
                gObject.playerHeldBy = null;
            }
        }
        if (player.IsOwner && !disconnecting)
        {
            HUDManager.Instance.holdingTwoHandedItem.enabled = false;
            HUDManager.Instance.itemSlotIcons[itemSlot].enabled = false;
            HUDManager.Instance.ClearControlTips();
            player.activatingItem = false;
        }

        player.ItemSlots[itemSlot] = null;

        if (player.isHoldingObject)
        {
            player.isHoldingObject = false;
            if (player.currentlyHeldObjectServer != null)
            {
                player.SetSpecialGrabAnimationBool(setTrue: false, player.currentlyHeldObjectServer);
            }
            player.playerBodyAnimator.SetBool("cancelHolding", value: true);
            player.playerBodyAnimator.SetTrigger("Throw");
        }
        player.activatingItem = false;
        player.twoHanded = false;
        player.carryWeight = 1f;
        player.currentlyHeldObjectServer = null;
    }

    public IEnumerator CoolDown(float duration)
    {
        yield return new WaitForSeconds(duration);
        onCoolDown = false;
    }

    public override void GrabItem()
    {
        animator.Play("sit");
        onCoolDown = false;
        base.GrabItem();
        lastHeld = playerHeldBy;
        objectsTouching.Clear();
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (playerHeldBy == null)
        {
            return;
        }
        isHoldingButton = buttonDown;
        if (!reelingUp && buttonDown)
        {
            reelingUp = true;
            previousPlayerHeldBy = playerHeldBy;
            if (reelingUpCoroutine != null)
            {
                StopCoroutine(reelingUpCoroutine);
            }
            reelingUpCoroutine = StartCoroutine(reelUpRake());
        }
    }

    private IEnumerator reelUpRake()
    {
        playerHeldBy.activatingItem = true;
        playerHeldBy.twoHanded = true;
        playerHeldBy.playerBodyAnimator.ResetTrigger("shovelHit");
        playerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: true);
        rakeAudio.PlayOneShot(reelUp);
        ReelUpSFXServerRpc();
        yield return new WaitForSeconds(0.35f);
        yield return new WaitUntil(() => !isHoldingButton || !isHeld);
        SwingRake(!isHeld);
        yield return new WaitForSeconds(0.13f);
        yield return new WaitForEndOfFrame();
        HitRake(!isHeld);
        yield return new WaitForSeconds(0.3f);
        reelingUp = false;
        reelingUpCoroutine = null;
    }

    [ServerRpc]
    public void ReelUpSFXServerRpc()
    {
        {
            ReelUpSFXClientRpc();
        }
    }

    [ClientRpc]
    public void ReelUpSFXClientRpc()
    {
        if(!base.IsOwner)
        {
            rakeAudio.pitch = 1;
            rakeAudio.PlayOneShot(reelUp);
        }
    }

    public override void DiscardItem()
    {
        base.DiscardItem();
        if (playerHeldBy != null)
        {
            playerHeldBy.activatingItem = false;
        }
    }

    public void SwingRake(bool cancel = false)
    {
        previousPlayerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: false);
        if (!cancel)
        {
            rakeAudio.pitch = 1;
            rakeAudio.PlayOneShot(rakeSwing);
        }
    }

    public void HitRake(bool cancel = false)
    {
        if (previousPlayerHeldBy == null)
        {
            return;
        }
        previousPlayerHeldBy.activatingItem = false;
        bool flag = false;
        bool flag2 = false;
        bool flag3 = false;
        int num = -1;
        if (!cancel)
        {
            previousPlayerHeldBy.twoHanded = false;
            objectsHitByRake = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f, 0.8f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, rakeMask, QueryTriggerInteraction.Collide);
            objectsHitByRakeList = objectsHitByRake.OrderBy((RaycastHit x) => x.distance).ToList();
            List<EnemyAI> list = new List<EnemyAI>();

            for (int i = 0; i < objectsHitByRakeList.Count; i++)
            {
                if (objectsHitByRakeList[i].transform.gameObject.layer == 8 || objectsHitByRakeList[i].transform.gameObject.layer == 11)
                {
                    if (objectsHitByRakeList[i].collider.isTrigger)
                    {
                        continue;
                    }
                    flag = true;
                    string text = objectsHitByRake[i].collider.gameObject.tag;
                    for (int j = 0; j < StartOfRound.Instance.footstepSurfaces.Length; j++)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[j].surfaceTag == text)
                        {
                            num = j;
                            break;
                        }
                    }
                }
                else
                {
                    if (!objectsHitByRakeList[i].transform.TryGetComponent<IHittable>(out var component) || objectsHitByRakeList[i].transform == previousPlayerHeldBy.transform || (!(objectsHitByRakeList[i].point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, objectsHitByRakeList[i].point, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
                    {
                        continue;
                    }
                    flag = true;
                    Vector3 forward = previousPlayerHeldBy.gameplayCamera.transform.forward;
                    try
                    {
                        EnemyAICollisionDetect component2 = objectsHitByRakeList[i].transform.GetComponent<EnemyAICollisionDetect>();
                        if (component2 != null)
                        {
                            if (!(component2.mainScript == null) && !list.Contains(component2.mainScript))
                            {
                                goto IL_02ff;
                            }
                            continue;
                        }
                        if (!(objectsHitByRakeList[i].transform.GetComponent<PlayerControllerB>() != null))
                        {
                            goto IL_02ff;
                        }
                        if (!flag3)
                        {
                            flag3 = true;
                            goto IL_02ff;
                        }
                        goto end_IL_0288;
                        IL_02ff:
                        bool flag4 = component.Hit(rakeHitForce, forward, previousPlayerHeldBy, playHitSFX: true, 1);
                        if (flag4 && component2 != null)
                        {
                            list.Add(component2.mainScript);
                        }
                        if (!flag2)
                        {
                            flag2 = flag4;
                        }
                        end_IL_0288:;
                    }
                    catch (Exception arg)
                    {
                        Debug.Log($"Exception caught when hitting object with rake from player # {previousPlayerHeldBy.playerClientId}: {arg}");
                    }
                }
            }
        }
        if (flag)
        {
            RoundManager.PlayRandomClip(rakeAudio, hitSFX);
            UnityEngine.Object.FindObjectOfType<RoundManager>().PlayAudibleNoise(base.transform.position, 17f, 0.8f);
            if (!flag2 && num != -1)
            {
                rakeAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
                WalkieTalkie.TransmitOneShotAudio(rakeAudio, StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
            }
            playerHeldBy.playerBodyAnimator.SetTrigger("shovelHit");
            HitRakeServerRpc(num);
        }
    }

    [ServerRpc]
    public void HitRakeServerRpc(int hitSurfaceID)
    {
        {
            HitRakeClientRpc(hitSurfaceID);
        }
    }

    [ClientRpc]
    public void HitRakeClientRpc(int hitSurfaceID)
    {
        if(!base.IsOwner)
        {
            RoundManager.PlayRandomClip(rakeAudio, hitSFX);
            if (hitSurfaceID != -1)
            {
                HitSurfaceWithRake(hitSurfaceID);
            }
        }
    }

    private void HitSurfaceWithRake(int hitSurfaceID)
    {
        rakeAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
        WalkieTalkie.TransmitOneShotAudio(rakeAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
    }
}