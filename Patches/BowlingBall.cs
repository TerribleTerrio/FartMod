using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class BowlingBall : GrabbableObject
{
    [Space(15f)]
    [Header("Bowling Ball Settings")]
    public float damageHeight;

    public int fallDamage;

    public int swingDamage;

    private bool reelingUp;

    private bool isHoldingButton;

    private bool collidedWhileFalling;

    private RaycastHit[] objectsHitByBall;

    private List<RaycastHit> objectsHitByBallList = new List<RaycastHit>();

    private Coroutine reelingUpCoroutine;

    private PlayerControllerB previousPlayerHeldBy;

    private int ballMask = 1084754248;

    [Space(10f)]
    [Header("Audio")]
    public AudioSource ballAudio;

    public AudioClip reelUpClip;

    public AudioClip[] hitSFX;

    private void CollideWhileFalling(Collider other)
    {
        if (!isHeld && !isHeldByEnemy && !hasHitGround && !collidedWhileFalling)
        {
            float fallHeight = startFallingPosition.y - transform.position.y;
            if (fallHeight < damageHeight)
            {
                return;
            }

            //FOR PLAYERS
            if (other.gameObject.layer == 3)
            {
                PlayerControllerB hitPlayer = other.gameObject.GetComponent<PlayerControllerB>();
                if (hitPlayer != null)
                {
                    hitPlayer.DamagePlayer(fallDamage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Bludgeoning);
                    collidedWhileFalling = true;
                    return;
                }
            }

            //FOR ENEMIES
            if (other.gameObject.layer == 19)
            {
                EnemyAICollisionDetect hitEnemy = other.gameObject.GetComponentInChildren<EnemyAICollisionDetect>();
                if (hitEnemy != null && hitEnemy.mainScript.IsOwner)
                {
                    hitEnemy.mainScript.HitEnemyOnLocalClient(fallDamage, transform.forward, playerHeldBy, playHitSFX: true);
                    collidedWhileFalling = true;
                    return;
                }
            }

            //FOR ITEMS
            if (other.gameObject.GetComponentInParent<Vase>() != null)
            {
                other.gameObject.GetComponentInParent<Vase>().ExplodeAndSync();
                return;
            }

            if (other.gameObject.GetComponentInParent<ArtilleryShellItem>() != null)
            {
                other.gameObject.GetComponentInParent<ArtilleryShellItem>().ArmShellAndSync();
                collidedWhileFalling = true;
                return;
            }

            if (other.gameObject.GetComponentInParent<HydraulicStabilizer>() != null)
            {
                other.gameObject.GetComponentInParent<HydraulicStabilizer>().GoPsycho();
                collidedWhileFalling = true;
            }

            if (other.gameObject.GetComponentInParent<Toaster>() != null)
            {
                other.gameObject.GetComponentInParent<Toaster>().Eject();
                other.gameObject.GetComponentInParent<Toaster>().EjectServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
            }

            //FOR HAZARDS
            if (other.gameObject.GetComponentInParent<Landmine>() != null)
            {
                Landmine landmine = other.gameObject.GetComponentInParent<Landmine>();
                landmine.SetOffMineAnimation();
                landmine.sendingExplosionRPC = true;
                landmine.ExplodeMineServerRpc();
            }

            if (other.gameObject.GetComponentInParent<Turret>() != null)
            {
                Turret turret = other.gameObject.GetComponentInParent<Turret>();
                if (turret.turretMode == TurretMode.Berserk || turret.turretMode == TurretMode.Firing)
                {
                    return;
                }
                turret.SwitchTurretMode(3);
                turret.EnterBerserkModeServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
            }
        }
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
            reelingUpCoroutine = StartCoroutine(reelUp());
        }
    }

    private IEnumerator reelUp()
    {
        playerHeldBy.activatingItem = true;
        playerHeldBy.twoHanded = true;
        playerHeldBy.playerBodyAnimator.ResetTrigger("shovelHit");
        playerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: true);
        ballAudio.PlayOneShot(reelUpClip);
        yield return new WaitForSeconds(1.3f);
        yield return new WaitUntil(() => !isHoldingButton || !isHeld);
        Swing(!isHeld);
        yield return new WaitForSeconds(0.13f);
        yield return new WaitForEndOfFrame();
        Hit(!isHeld);
        yield return new WaitForSeconds(0.3f);
        reelingUp = false;
        reelingUpCoroutine = null;
    }

    public void Swing(bool cancel = false)
    {
        previousPlayerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: false);
        if (!cancel)
        {
            ballAudio.PlayOneShot(reelUpClip);
            previousPlayerHeldBy.UpdateSpecialAnimationValue(specialAnimation: true, (short)previousPlayerHeldBy.transform.localEulerAngles.y, 0.4f);
        }
    }

    public void Hit(bool cancel = false)
    {
        if (previousPlayerHeldBy == null)
        {
            return;
        }
        previousPlayerHeldBy.activatingItem = false;
        bool isValidHitObject = false;
        bool flag2 = false;
        bool flag3 = false;
        int surfaceType = -1;
        if (!cancel)
        {
            previousPlayerHeldBy.twoHanded = false;
            objectsHitByBall = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f, 0.8f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, ballMask, QueryTriggerInteraction.Collide);
            objectsHitByBallList = objectsHitByBall.OrderBy((RaycastHit x) => x.distance).ToList();
            List<EnemyAI> enemiesHitByBall = new List<EnemyAI>();

            for (int i = 0; i < objectsHitByBallList.Count; i++)
            {
                RaycastHit hitObject = objectsHitByBallList[i];
                GameObject gObject = hitObject.transform.gameObject;

                if (gObject.layer == 8 || gObject.layer == 11)
                {
                    if (hitObject.collider.isTrigger)
                    {
                        continue;
                    }
                    isValidHitObject = true;
                    string tagString = hitObject.collider.gameObject.tag;

                    for (int j = 0; j < StartOfRound.Instance.footstepSurfaces.Length; j++)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[j].surfaceTag == tagString)
                        {
                            surfaceType = j;
                            break;
                        }
                    }
                }

                else
                {
                    if (!hitObject.transform.TryGetComponent<IHittable>(out var component) || hitObject.transform == previousPlayerHeldBy.transform || (!(hitObject.point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, hitObject.point, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
                    {
                        continue;
                    }
                    isValidHitObject = true;
                    Vector3 forward = previousPlayerHeldBy.gameplayCamera.transform.forward;
                    try
                    {
                        EnemyAICollisionDetect enemyCollision = hitObject.transform.GetComponent<EnemyAICollisionDetect>();
                        if (enemyCollision != null)
                        {
                            if (!(enemyCollision.mainScript == null) && enemiesHitByBall.Contains(enemyCollision.mainScript))
                            {
                                goto IL_02ff;
                            }
                            continue;
                        }
                        if (!(hitObject.transform.GetComponent<PlayerControllerB>() != null))
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
                        bool flag4 = component.Hit(swingDamage, forward, previousPlayerHeldBy, playHitSFX: true, 1);
                        if (flag4 && enemyCollision != null)
                        {
                            enemiesHitByBall.Add(enemyCollision.mainScript);
                        }
                        if (!flag2)
                        {
                            flag2 = flag4;
                        }
                        end_IL_0288:;
                    }
                    catch (Exception arg)
                    {
                        Debug.Log($"[BOWLING BALL]: Exception caught when hitting object from player {previousPlayerHeldBy.playerUsername}: {arg}");
                    }
                }
            }
        }

        if (isValidHitObject)
        {
            RoundManager.PlayRandomClip(ballAudio, hitSFX);
            UnityEngine.Object.FindObjectOfType<RoundManager>().PlayAudibleNoise(base.transform.position, 17f, 0.8f);
            if (!flag2 && surfaceType != -1)
            {
                ballAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[surfaceType].hitSurfaceSFX);
                WalkieTalkie.TransmitOneShotAudio(ballAudio, StartOfRound.Instance.footstepSurfaces[surfaceType].hitSurfaceSFX);
            }
            playerHeldBy.playerBodyAnimator.SetTrigger("shovelHit");
            HitBallServerRpc(surfaceType);
        }
    }

    [ServerRpc]
    public void HitBallServerRpc(int hitSurfaceID)
    {
        {
            HitBallClientRpc(hitSurfaceID);
        }
    }

    [ClientRpc]
    public void HitBallClientRpc(int hitSurfaceID)
    {
        if (!base.IsOwner)
        {
            RoundManager.PlayRandomClip(ballAudio, hitSFX);
            if (hitSurfaceID != -1)
            {
                HitSurfaceWithBall(hitSurfaceID);
            }
        }
    }

    private void HitSurfaceWithBall(int hitSurfaceID)
    {
        ballAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
        WalkieTalkie.TransmitOneShotAudio(ballAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
    }
}