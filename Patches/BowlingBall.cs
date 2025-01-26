using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System;
using System.Linq;

public class BowlingBall : GrabbableObject
{
    [Space(15f)]
    [Header("Bowling Ball Settings")]
    public float damageHeight;

    public int fallDamage;

    private List<Collider> CollidersHitByFallingBowlingBall = new List<Collider>();

    private Vector3 dropPosition;

	public int bowlingBallHitForce = 1;

	public bool reelingUp;

	public bool isHoldingButton;

	private Coroutine reelingUpCoroutine;

	private RaycastHit[] objectsHitByBowlingBall;

	private List<RaycastHit> objectsHitByBowlingBallList = new List<RaycastHit>();

	public AudioClip reelUp;

	public AudioClip swing;

	public AudioClip[] hitSFX;

	public AudioSource bowlingBallAudio;

    [HideInInspector]
    public RuntimeAnimatorController playerDefaultAnimatorController;

    [HideInInspector]
    public RuntimeAnimatorController otherPlayerDefaultAnimatorController;

    [Header("Animators to replace default player animators")]
    public RuntimeAnimatorController playerCustomAnimatorController;

    public RuntimeAnimatorController otherPlayerCustomAnimatorController;

	private PlayerControllerB previousPlayerHeldBy;

    private bool isCrouching;

    private bool isJumping;

    private bool isWalking;

    private bool isSprinting;

    private AnimatorStateInfo currentStateInfo;

    private float currentAnimationTime;

    public override void Update()
    {
        base.Update();
        if (previousPlayerHeldBy == null || !base.IsOwner)
        {
            return;
        }
        else 
        {
            if (previousPlayerHeldBy.isPlayerDead)
            {
                SetAnimator(setOverride: false);
            }
        }
    }

    public override void EquipItem()
    {
        base.EquipItem();
        previousPlayerHeldBy = playerHeldBy;
        SetAnimator(setOverride: true);
        playerHeldBy.playerBodyAnimator.Play("HoldBowlingBall");
    }

	public override void DiscardItem()
	{
		if (playerHeldBy != null)
		{
			playerHeldBy.activatingItem = false;
		}
		base.DiscardItem();
        previousPlayerHeldBy.activatingItem = false;
        SetAnimator(setOverride: false);
        dropPosition = base.transform.position;
        CollidersHitByFallingBowlingBall.Clear();
        StartCoroutine(DropBowlingBall());
	}

    private IEnumerator DropBowlingBall()
    {
        while (!isHeld && !isHeldByEnemy && !hasHitGround)
        {
            yield return null;
            RaycastHit[] results = Physics.SphereCastAll(base.transform.position, 0.1f, Vector3.down, 0.1f, CoronaMod.Masks.PlayerPropsEnemiesMapHazards);
            for (int i = 0; i < results.Count(); i++)
            {
                if (CollidersHitByFallingBowlingBall.Contains(results[i].collider))
                {
                    continue;
                }
                else
                {
                    CollideWhileFalling(results[i].collider);
                }
            }
        }
    }

    private void CollideWhileFalling(Collider other)
    {
        if (CollidersHitByFallingBowlingBall.Contains(other))
        {
            return;
        }
        else
        {
            CollidersHitByFallingBowlingBall.Add(other);
        }
        float fallHeight = Vector3.Distance(dropPosition, base.transform.position);
        if (fallHeight < damageHeight)
        {
            return;
        }

        //FOR PLAYERS
        if (other.gameObject.layer == 3)
        {
            if (other.gameObject.TryGetComponent<IHittable>(out var hittable))
            {
                hittable.Hit((fallHeight > damageHeight * 2) ? 10 : bowlingBallHitForce * 2, (fallHeight > damageHeight * 2) ? Vector3.down * 6f : Vector3.down * 3f, previousPlayerHeldBy, playHitSFX: true, 1);
                return;
            }
        }

        //FOR ENEMIES
        if (other.gameObject.layer == 19)
        {
            EnemyAICollisionDetect hitEnemy = other.gameObject.GetComponentInChildren<EnemyAICollisionDetect>();
            if (hitEnemy != null && hitEnemy.mainScript.IsOwner)
            {
                hitEnemy.mainScript.HitEnemyOnLocalClient(bowlingBallHitForce, transform.forward, playerHeldBy, playHitSFX: true);
            }
        }

        //FOR ITEMS
        if (other.gameObject.layer == 6)
        {
            other.gameObject.GetComponent<Vase>()?.ExplodeAndSync();
            other.gameObject.GetComponent<ArtilleryShellItem>()?.ArmShellAndSync();
            other.gameObject.GetComponent<HydraulicStabilizer>()?.GoPsychoAndSync();
            other.gameObject.GetComponent<Toaster>()?.EjectAndSync();
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
            if (!(turret.turretMode == TurretMode.Berserk || turret.turretMode == TurretMode.Firing))
            {
                turret.SwitchTurretMode(3);
                turret.EnterBerserkModeServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
            }
        }
    }

    public override void OnHitGround()
    {
        base.OnHitGround();
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
			reelingUpCoroutine = StartCoroutine(ReelUpBowlingBall());
		}
	}

	private IEnumerator ReelUpBowlingBall()
	{
		playerHeldBy.activatingItem = true;
		playerHeldBy.playerBodyAnimator.ResetTrigger("BowlingBallHit");
		playerHeldBy.playerBodyAnimator.SetBool("BowlingBallReelingUp", value: true);
		bowlingBallAudio.PlayOneShot(reelUp);
		ReelUpSFXServerRpc();
        yield return new WaitForSeconds(0.93f);
		yield return new WaitUntil(() => !isHoldingButton || !isHeld);
		SwingBowlingBall(!isHeld);
		yield return new WaitForSeconds(0.13f);
		yield return new WaitForEndOfFrame();
		HitBowlingBall(!isHeld);
		yield return new WaitForSeconds(0.5f);
		reelingUp = false;
		reelingUpCoroutine = null;
	}

	[ServerRpc]
	public void ReelUpSFXServerRpc()
    {
        ReelUpSFXClientRpc();
    }

	[ClientRpc]
	public void ReelUpSFXClientRpc()
    {   
        if (!base.IsOwner)
        {
            bowlingBallAudio.PlayOneShot(reelUp);
        }
    }

    public void SwingBowlingBall(bool cancel = false)
	{
		previousPlayerHeldBy.playerBodyAnimator.SetBool("BowlingBallReelingUp", value: false);
		if (!cancel)
		{
			bowlingBallAudio.PlayOneShot(swing);
            SwingBowlingBallSFXServerRpc();
			previousPlayerHeldBy.UpdateSpecialAnimationValue(specialAnimation: true, (short)previousPlayerHeldBy.transform.localEulerAngles.y, 0.4f);
		}
	}

	[ServerRpc]
	public void SwingBowlingBallSFXServerRpc()
    {
        SwingBowlingBallSFXClientRpc();
    }

	[ClientRpc]
	public void SwingBowlingBallSFXClientRpc()
    {   
        if (!base.IsOwner)
        {
            bowlingBallAudio.PlayOneShot(swing);
        }
    }

	public void HitBowlingBall(bool cancel = false)
	{
		if (previousPlayerHeldBy == null)
		{
			Debug.LogError("Previousplayerheldby is null on this client when HitBowlingBall is called.");
			return;
		}
		previousPlayerHeldBy.activatingItem = false;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		int num = -1;
		if (!cancel)
		{
			objectsHitByBowlingBall = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f, 0.8f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, CoronaMod.Masks.WeaponMask, QueryTriggerInteraction.Collide);
			objectsHitByBowlingBallList = objectsHitByBowlingBall.OrderBy((RaycastHit x) => x.distance).ToList();
			List<EnemyAI> list = new List<EnemyAI>();
			for (int i = 0; i < objectsHitByBowlingBallList.Count; i++)
			{
				if (objectsHitByBowlingBallList[i].transform.gameObject.layer == 8 || objectsHitByBowlingBallList[i].transform.gameObject.layer == 11)
				{
					if (objectsHitByBowlingBallList[i].collider.isTrigger)
					{
						continue;
					}
					flag = true;
					string text = objectsHitByBowlingBallList[i].collider.gameObject.tag;
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
					if (!objectsHitByBowlingBallList[i].transform.TryGetComponent<IHittable>(out var component) || objectsHitByBowlingBallList[i].transform == previousPlayerHeldBy.transform || (!(objectsHitByBowlingBallList[i].point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, objectsHitByBowlingBallList[i].point, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
					{
						continue;
					}
					flag = true;
                    int finalDmg = bowlingBallHitForce;
					Vector3 forward = previousPlayerHeldBy.gameplayCamera.transform.forward;
					try
					{
						EnemyAICollisionDetect component2 = objectsHitByBowlingBallList[i].transform.GetComponent<EnemyAICollisionDetect>();
						if (component2 != null)
						{
							if (component2.mainScript != null && !list.Contains(component2.mainScript))
							{
								goto HIT;
							}
							continue;
						}
						if (objectsHitByBowlingBallList[i].transform.GetComponent<PlayerControllerB>() != null)
						{
                            finalDmg *= 2;
							goto HIT;
						}
						if (!flag3)
						{
							flag3 = true;
							goto HIT;
						}
						goto QUIT;
						HIT:
						bool flag4 = component.Hit(finalDmg, forward * 2, previousPlayerHeldBy, playHitSFX: true, 1);
						if (flag4 && component2 != null)
						{
							list.Add(component2.mainScript);
						}
						if (!flag2)
						{
							flag2 = flag4;
						}
						QUIT:;
					}
					catch (Exception arg)
					{
						Debug.Log($"Exception caught when hitting object with bowling ball from player #{previousPlayerHeldBy.playerClientId}: {arg}");
					}
				}
			}
		}
		if (flag)
		{
			RoundManager.PlayRandomClip(bowlingBallAudio, hitSFX);
			UnityEngine.Object.FindObjectOfType<RoundManager>().PlayAudibleNoise(base.transform.position, 17f, 0.8f);
			if (!flag2 && num != -1)
			{
				bowlingBallAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
				WalkieTalkie.TransmitOneShotAudio(bowlingBallAudio, StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
			}
			playerHeldBy.playerBodyAnimator.SetTrigger("BowlingBallHit");
			HitBowlingBallServerRpc(num);
		}
	}

	[ServerRpc]
	public void HitBowlingBallServerRpc(int hitSurfaceID)
    {
        HitBowlingBallClientRpc(hitSurfaceID);
    }
	[ClientRpc]
	public void HitBowlingBallClientRpc(int hitSurfaceID)
    {
        if (!base.IsOwner)
        {
			RoundManager.PlayRandomClip(bowlingBallAudio, hitSFX);
			if (hitSurfaceID != -1)
			{
				HitSurfaceWithBowlingBall(hitSurfaceID);
			}
		}
    }

	private void HitSurfaceWithBowlingBall(int hitSurfaceID)
	{
		bowlingBallAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
		WalkieTalkie.TransmitOneShotAudio(bowlingBallAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
	}

    // --- CHANGE ANIMATOR - RIPPED FROM HAND MIRROR! ---
    private void SetAnimator(bool setOverride)
    {
        if (setOverride == true)
        {
            if (playerHeldBy != null)
            {
                if (playerHeldBy == StartOfRound.Instance.localPlayerController)
                {
                    SaveAnimatorStates(playerHeldBy.playerBodyAnimator);
                    if (playerDefaultAnimatorController != playerCustomAnimatorController)
                    {
                        playerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
                    }
                    playerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerCustomAnimatorController;
                    SetAnimatorStates(playerHeldBy.playerBodyAnimator);
                }
                else
                {
                    SaveAnimatorStates(playerHeldBy.playerBodyAnimator);
                    if (otherPlayerDefaultAnimatorController != otherPlayerCustomAnimatorController)
                    {
                        otherPlayerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
                    }
                    playerHeldBy.playerBodyAnimator.runtimeAnimatorController = otherPlayerCustomAnimatorController;
                    SetAnimatorStates(playerHeldBy.playerBodyAnimator);
                }
            }
        }
        else
        {
            if (previousPlayerHeldBy != null)
            {
                if (previousPlayerHeldBy == StartOfRound.Instance.localPlayerController)
                {
                    SaveAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                    previousPlayerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerDefaultAnimatorController;
                    SetAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                }
                else
                {
                    SaveAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                    previousPlayerHeldBy.playerBodyAnimator.runtimeAnimatorController = otherPlayerDefaultAnimatorController;
                    SetAnimatorStates(previousPlayerHeldBy.playerBodyAnimator);
                }
            }
        }
    }

    public void SaveAnimatorStates(Animator animator)
    {
        isCrouching = animator.GetBool("crouching");
        isJumping = animator.GetBool("Jumping");
        isWalking = animator.GetBool("Walking");
        isSprinting = animator.GetBool("Sprinting");
        currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        currentAnimationTime = currentStateInfo.normalizedTime;
    }

    public void SetAnimatorStates(Animator animator)
    {
        animator.Play(currentStateInfo.fullPathHash, 0, currentAnimationTime);
        animator.SetBool("crouching", isCrouching);
        animator.SetBool("Jumping", isJumping);
        animator.SetBool("Walking", isWalking);
        animator.SetBool("Sprinting", isSprinting);
    }
}