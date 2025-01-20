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

    private bool collidedWhileFalling;

	public int shovelHitForce = 1;

	public bool reelingUp;

	public bool isHoldingButton;

	private Coroutine reelingUpCoroutine;

	private RaycastHit[] objectsHitByShovel;

	private List<RaycastHit> objectsHitByShovelList = new List<RaycastHit>();

	public AudioClip reelUp;

	public AudioClip swing;

	public AudioClip[] hitSFX;

	public AudioSource shovelAudio;

	private int shovelMask = 1084754248;

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
        playerHeldBy.playerBodyAnimator.SetTrigger("GrabBowlingBall");
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
			reelingUpCoroutine = StartCoroutine(ReelUpShovel());
		}
	}

	private IEnumerator ReelUpShovel()
	{
		playerHeldBy.activatingItem = true;
		playerHeldBy.playerBodyAnimator.ResetTrigger("BowlingBallHit");
		playerHeldBy.playerBodyAnimator.SetBool("BowlingBallReelingUp", value: true);
		shovelAudio.PlayOneShot(reelUp);
		ReelUpSFXServerRpc();
		yield return new WaitForSeconds(0.85f);
		yield return new WaitUntil(() => !isHoldingButton || !isHeld);
		SwingShovel(!isHeld);
		yield return new WaitForSeconds(0.13f);
		yield return new WaitForEndOfFrame();
		HitShovel(!isHeld);
		yield return new WaitForSeconds(0.7f);
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
            shovelAudio.PlayOneShot(reelUp);
        }
    }

    public void SwingShovel(bool cancel = false)
	{
		previousPlayerHeldBy.playerBodyAnimator.SetBool("BowlingBallReelingUp", value: false);
		if (!cancel)
		{
			shovelAudio.PlayOneShot(swing);
			previousPlayerHeldBy.UpdateSpecialAnimationValue(specialAnimation: true, (short)previousPlayerHeldBy.transform.localEulerAngles.y, 0.4f);
		}
	}

	public void HitShovel(bool cancel = false)
	{
		if (previousPlayerHeldBy == null)
		{
			Debug.LogError("Previousplayerheldby is null on this client when HitShovel is called.");
			return;
		}
		previousPlayerHeldBy.activatingItem = false;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		int num = -1;
		if (!cancel)
		{
			objectsHitByShovel = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f, 0.8f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, shovelMask, QueryTriggerInteraction.Collide);
			objectsHitByShovelList = objectsHitByShovel.OrderBy((RaycastHit x) => x.distance).ToList();
			List<EnemyAI> list = new List<EnemyAI>();
			for (int i = 0; i < objectsHitByShovelList.Count; i++)
			{
				if (objectsHitByShovelList[i].transform.gameObject.layer == 8 || objectsHitByShovelList[i].transform.gameObject.layer == 11)
				{
					if (objectsHitByShovelList[i].collider.isTrigger)
					{
						continue;
					}
					flag = true;
					string text = objectsHitByShovelList[i].collider.gameObject.tag;
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
					if (!objectsHitByShovelList[i].transform.TryGetComponent<IHittable>(out var component) || objectsHitByShovelList[i].transform == previousPlayerHeldBy.transform || (!(objectsHitByShovelList[i].point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, objectsHitByShovelList[i].point, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
					{
						continue;
					}
					flag = true;
					Vector3 forward = previousPlayerHeldBy.gameplayCamera.transform.forward;
					try
					{
						EnemyAICollisionDetect component2 = objectsHitByShovelList[i].transform.GetComponent<EnemyAICollisionDetect>();
						if (component2 != null)
						{
							if (!(component2.mainScript == null) && !list.Contains(component2.mainScript))
							{
								goto IL_02ff;
							}
							continue;
						}
						if (!(objectsHitByShovelList[i].transform.GetComponent<PlayerControllerB>() != null))
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
						bool flag4 = component.Hit(shovelHitForce, forward, previousPlayerHeldBy, playHitSFX: true, 1);
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
						Debug.Log($"Exception caught when hitting object with shovel from player #{previousPlayerHeldBy.playerClientId}: {arg}");
					}
				}
			}
		}
		if (flag)
		{
			RoundManager.PlayRandomClip(shovelAudio, hitSFX);
			UnityEngine.Object.FindObjectOfType<RoundManager>().PlayAudibleNoise(base.transform.position, 17f, 0.8f);
			if (!flag2 && num != -1)
			{
				shovelAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
				WalkieTalkie.TransmitOneShotAudio(shovelAudio, StartOfRound.Instance.footstepSurfaces[num].hitSurfaceSFX);
			}
			playerHeldBy.playerBodyAnimator.SetTrigger("BowlingBallHit");
			HitShovelServerRpc(num);
		}
	}

	[ServerRpc]
	public void HitShovelServerRpc(int hitSurfaceID)
    {
        HitShovelClientRpc(hitSurfaceID);
    }
	[ClientRpc]
	public void HitShovelClientRpc(int hitSurfaceID)
    {
        if (!base.IsOwner)
        {
			RoundManager.PlayRandomClip(shovelAudio, hitSFX);
			if (hitSurfaceID != -1)
			{
				HitSurfaceWithShovel(hitSurfaceID);
			}
		}
    }

	private void HitSurfaceWithShovel(int hitSurfaceID)
	{
		shovelAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
		WalkieTalkie.TransmitOneShotAudio(shovelAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
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