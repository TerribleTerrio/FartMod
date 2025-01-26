using UnityEngine;
using GameNetcodeStuff;
using System.Collections;

public class Lighter : AnimatedItem
{
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

    [Space(10f)]
    [Header("Lighter Settings")]
    public float tankTime = 70f;

    private float lighterTank = 1f;

    private bool inToggleLighterAnimation;

    private Coroutine toggleLighterCoroutine;

    private Coroutine waitForTankCoroutine;

    private bool isOn;

    private bool tankEmpty;

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

    public override void LateUpdate()
    {
        base.LateUpdate();
        if (isOn && isHeld)
		{
            lighterTank = Mathf.Max(lighterTank - Time.deltaTime / tankTime, 0f);
            if (lighterTank <= 0f)
            {
                tankEmpty = true;
            }
        }
    }

	public override void LoadItemSaveData(int saveData)
	{
		base.LoadItemSaveData(saveData);
		lighterTank = (float)saveData / 100f;
	}

	public override int GetItemDataToSave()
	{
		return (int)(lighterTank * 100f);
	}


    public override void EquipItem()
	{
		base.EquipItem();
		previousPlayerHeldBy = playerHeldBy;
        SetAnimator(setOverride: true);
	}

	public override void DiscardItem()
	{
		base.DiscardItem();
		previousPlayerHeldBy.activatingItem = false;
        SetAnimator(setOverride: false);
        if (toggleLighterCoroutine != null)
        {
            StopCoroutine(toggleLighterCoroutine);
            toggleLighterCoroutine = null;
        }
        if (waitForTankCoroutine != null)
        {
            StopCoroutine(waitForTankCoroutine);
            waitForTankCoroutine = null;
        }
        itemAnimator.Play("off");
        inToggleLighterAnimation = false;
        isOn = false;
	}

	public override void PocketItem()
	{
		base.PocketItem();
		playerHeldBy.activatingItem = false;
        SetAnimator(setOverride: false);     
        if (toggleLighterCoroutine != null)
        {
            StopCoroutine(toggleLighterCoroutine);
            toggleLighterCoroutine = null;
        }
        if (waitForTankCoroutine != null)
        {
            StopCoroutine(waitForTankCoroutine);
            waitForTankCoroutine = null;
        }
        itemAnimator.Play("off");
        inToggleLighterAnimation = false;
        isOn = false;
	}

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        if (!inToggleLighterAnimation)
        {
            if (toggleLighterCoroutine != null)
            {
                StopCoroutine(toggleLighterCoroutine);
                toggleLighterCoroutine = null;
            }
            if (waitForTankCoroutine != null)
            {
                StopCoroutine(waitForTankCoroutine);
                waitForTankCoroutine = null;
                Debug.Log("Stopping wait for tank coroutine.");
            }
            if (!isOn)
            {
                playerHeldBy.activatingItem = true;
                toggleLighterCoroutine = StartCoroutine(ToggleLighterAnimation(true, tankEmpty));
            }
            else
            {
                playerHeldBy.activatingItem = true;
                toggleLighterCoroutine = StartCoroutine(ToggleLighterAnimation(false, tankEmpty));
            }
        }
    }

    private IEnumerator ToggleLighterAnimation(bool turningon = true, bool empty = false)
    {
        Debug.Log($"Lighter tank: {lighterTank}.");
        Debug.Log($"Toggling lighter animation, turning on: {turningon}, empty: {empty}.");
        inToggleLighterAnimation = true;
		playerHeldBy.activatingItem = true;
		playerHeldBy.doingUpperBodyEmote = 1.38f;
        if (turningon)
        {
            playerHeldBy.playerBodyAnimator.SetTrigger("TurnLighterOn");
        }
        else
        {
            playerHeldBy.playerBodyAnimator.SetTrigger("TurnLighterOff");
        }
        if (!empty)
        {
            itemAnimator.SetTrigger("Used");
        }
        else
        {
            itemAnimator.SetTrigger("UsedEmpty");
        }
        yield return new WaitForSeconds(1.38f);
        playerHeldBy.activatingItem = false;
        inToggleLighterAnimation = false;
        isOn = turningon;
        Debug.Log("Toggling lighter animation coroutine complete.");
        if (turningon && !empty)
        {
            waitForTankCoroutine = StartCoroutine(WaitForTank());
        }
    }

    private IEnumerator WaitForTank()
    {
        Debug.Log("Starting wait for tank coroutine.");
        yield return new WaitUntil(() => tankEmpty);
        Debug.Log($"Lighter tank: {lighterTank}, turning off.");
        itemAnimator.Play("emptying");
        playerHeldBy.activatingItem = false;
        inToggleLighterAnimation = false;
        isOn = false;
        Debug.Log("Wait for tank coroutine complete.");
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
