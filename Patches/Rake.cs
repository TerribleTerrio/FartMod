using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Rake : GrabbableObject, ITouchable
{
    [Space(15f)]
    [Header("Rake Settings")]
    public int damageDealtPlayer;

    public int damageDealtEnemy;

    public float damageRange;

    public float physicsForce;

    public float cooldownTime;

    private float cooldownTimer;

    private bool onCooldown;

    private int previousColliderCount;

    private List<Collider> collidersTouching;

    private Vector3 lastPositionAtFlip;

    [Space(10f)]
    [Header("Animations")]
    public GameObject animContainer;

    private PlayerControllerB lastHeld;

    public GameObject itemCollider;

    public Animator animator;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource rakeAudio;

    public AudioClip reelUp;

    public AudioClip rakeSwing;

    public AudioClip[] hitSFX;

    private int timesPlayingInOneSpot = 0;

    [Space(10f)]
    [Header("Weapon Properties")]
    public int rakeHitForce = 1;

    public bool reelingUp;

    public bool isHoldingButton;

    private Coroutine reelingUpCoroutine;

    private RaycastHit[] objectsHitByRake;

    private List<RaycastHit> objectsHitByRakeList = new List<RaycastHit>();

    private PlayerControllerB previousPlayerHeldBy;

    private int rakeMask = 1084754248;

    private bool dropAnimationComplete;

    public override void Start()
    {
        collidersTouching = new List<Collider>();
        base.Start();
    }

    public override void Update()
    {
        base.Update();
        if (cooldownTimer >= 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (animator.GetCurrentAnimatorStateInfo(0).IsName("sit") && cooldownTimer <= 0f)
        {
            onCooldown = false;
        }
        else
        {
            onCooldown = true;
        }

        if (base.transform.localPosition != targetFloorPosition)
        {
            dropAnimationComplete = false;
        }
        else
        {
            dropAnimationComplete = true;
        }
    }

    public override void OnHitGround()
    {
        StartCoroutine(SetDropRotation());
    }

    private IEnumerator SetDropRotation()
    {
        yield return new WaitUntil(() => dropAnimationComplete);
        var sendingRotation = base.transform.rotation;
        SetDropRotationServerRpc(sendingRotation);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetDropRotationServerRpc(Quaternion getRotation)
    {
        SetDropRotationClientRpc(getRotation);
    }

    [ClientRpc]
    public void SetDropRotationClientRpc(Quaternion getRotation)
    {
        base.transform.rotation = getRotation;
    }

    public void OnTouch(Collider other)
    {
        previousColliderCount = collidersTouching.Count();
        GameObject otherObject = other.gameObject;
        
        if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            return;
        }

        if (Physics.Linecast(base.transform.position + Vector3.up * 0.5f, otherObject.transform.position, 1073742080, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        //PLAYER COLLISION
        if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null && otherObject.GetComponent<PlayerControllerB>() && !collidersTouching.Contains(other))
        {
            collidersTouching.Add(other);

            if (previousColliderCount < 1 && !onCooldown && !isHeld && !isHeldByEnemy && hasHitGround && !otherObject.GetComponent<PlayerControllerB>().isCrouching)
            {
                FlipAndSync();
                return;
            }
        }

        //ENEMY COLLISION
        else if (other.gameObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
        {
            EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();

            if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Red Locust Bees")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Butler Bees")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Docile Locust Bees")
            {
                return;
            }
            else if (enemy.mainScript.enemyType.enemyName == "Flowerman")
            {
                FlowermanAI flowerman = enemy.mainScript as FlowermanAI;
                if (flowerman.isInAngerMode)
                {
                    collidersTouching.Add(other);
                    if (previousColliderCount < 1 && !onCooldown && !isHeld && !isHeldByEnemy && hasHitGround)
                    {
                        FlipAndSync();
                        return;
                    }
                }
            }
            else
            {
                collidersTouching.Add(other);
                if (previousColliderCount < 1 && !onCooldown && !isHeld && !isHeldByEnemy && hasHitGround)
                {
                    FlipAndSync();
                    return;
                }
            }
        }

        previousColliderCount = collidersTouching.Count();
    }

    public void OnExit(Collider other)
    {
        if (collidersTouching.Contains(other))
        {
            collidersTouching.Remove(other);
        }

        if (previousColliderCount > 0 && collidersTouching.Count < 1 && !isHeld && !isHeldByEnemy && hasHitGround)
        {
            FallAndSync();
        }

        previousColliderCount = collidersTouching.Count();
    }

    public void FlipAndSync()
    {
        Flip();
        FlipServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void FlipServerRpc(int clientWhoSentRpc)
    {
        FlipClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    public void FlipClientRpc(int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            Flip();
        }
    }

    public void Flip()
    {
        //CHECK FOR COLLIDERS IN RANGE
        Collider[] colliders = Physics.OverlapSphere(base.transform.position, damageRange, 2621448, QueryTriggerInteraction.Collide);
        List<EnemyAI> hitEnemies = new List<EnemyAI>();
        for (int i =0; i < colliders.Length; i++)
        {
            //FLIP RAKE UP
            animator.SetTrigger("flip");

            //PLAY AUDIBLE NOISE
            if (Vector3.Distance(lastPositionAtFlip, base.transform.position) > 2f)
            {
                timesPlayingInOneSpot = 0;
            }
            timesPlayingInOneSpot = timesPlayingInOneSpot + 5;
            lastPositionAtFlip = base.transform.position;
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 10f, 1f, timesPlayingInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

            GameObject otherObject = colliders[i].gameObject;

            //FOR PLAYERS
            if (otherObject.layer == 3 && colliders[i].gameObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = colliders[i].gameObject.GetComponent<PlayerControllerB>();

                if (player.isCrouching)
                {
                    continue;
                }

                //DAMAGE ALL CLOSE PLAYERS
                Vector3 bodyVelocity = Vector3.Normalize(player.transform.position - base.transform.position) * 80f / Vector3.Distance(player.transform.position, base.transform.position);
                player.DamagePlayer(damageDealtPlayer, hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning, 0, fallDamage:false, bodyVelocity);

                //PUSH ALL CLOSE PLAYERS
                float dist = Vector3.Distance(player.transform.position, base.transform.position);
                Vector3 vector = Vector3.Normalize(player.transform.position - base.transform.position) * physicsForce + -player.walkForce * 2f + Vector3.up * 2.5f;
                if (vector.magnitude > 2f)
                {
                    if (vector.magnitude > 10f)
                    {
                        player.CancelSpecialTriggerAnimations();
                    }
                    if (!player.inVehicleAnimation || (player.externalForceAutoFade + vector).magnitude > 50f)
                    {
                        player.externalForceAutoFade += vector;
                    }
                }

                //CAMERA SHAKE
                if ((int)player.playerClientId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
                {
                    if (dist < damageRange)
                    {
                        HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
                    }
                }

                //DROP HELD ITEM OF ALL CLOSE PLAYERS
                if (player.isHoldingObject)
                {
                    player.DiscardHeldObject();
                }
            }

            //FOR ALL NEARBY ENEMIES
            if (otherObject.layer == 19)
            {
                //DAMAGE ALL CLOSE ENEMIES
                EnemyAICollisionDetect enemy = colliders[i].gameObject.GetComponentInChildren<EnemyAICollisionDetect>();
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
            }
        }
    }

    public void FallAndSync()
    {
        Fall();
        FallServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void FallServerRpc(int clientWhoSentRpc)
    {
        FallClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    public void FallClientRpc(int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            Fall();
        }
    }

    public void Fall()
    {
        animator.SetTrigger("fall");
    }

    public override void GrabItem()
    {
        SetRakeSitServerRpc();
        base.GrabItem();
        lastHeld = playerHeldBy;
        collidersTouching.Clear();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetRakeSitServerRpc()
    {
        SetRakeSitClientRpc();
    }

    [ClientRpc]
    public void SetRakeSitClientRpc()
    {
        animator.Play("sit");
        cooldownTimer = cooldownTime;
    }


    public override void DiscardItem()
    {
        base.DiscardItem();
        if (playerHeldBy != null)
        {
            playerHeldBy.activatingItem = false;
        }
        SetRakeSitServerRpc();
    }

    public override void DiscardItemFromEnemy()
    {
        base.DiscardItemFromEnemy();
        SetRakeSitServerRpc();
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        base.GrabItemFromEnemy(enemy);
        SetRakeSitServerRpc();
        collidersTouching.Clear();
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