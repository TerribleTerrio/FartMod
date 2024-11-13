using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
    
public class HandMirror : GrabbableObject
{
    [HideInInspector]
    public RuntimeAnimatorController playerDefaultAnimatorController;

    [HideInInspector]
    public RuntimeAnimatorController otherPlayerDefaultAnimatorController;

    [Header("Animators to replace default player animators")]
    public RuntimeAnimatorController playerMirrorAnimatorController;

    public RuntimeAnimatorController otherPlayerMirrorAnimatorController;

    [Header("Reflection")]
    public Camera reflectionCamera;
    
    public RenderTexture reflectionTexture;

    public Material reflectionMaterial;

    public Material reflectionMaterialOff;

    public GameObject ReflectionObject;

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
                CancelLookCloser();
                SetAnimator(setOverride: false);
                RemoveCameraTexture();
                reflectionCamera.enabled = false;
            }
        }
    }

    public override void Start()
    {
        base.Start();
        reflectionCamera.enabled = false;
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
	{
		base.ItemActivate(used, buttonDown);
		if (!(playerHeldBy == null) && base.IsOwner)
		{
			playerHeldBy.playerBodyAnimator.SetBool("HoldMirror", buttonDown);
			playerHeldBy.activatingItem = buttonDown;
        }
	}

	public override void EquipItem()
	{
		base.EquipItem();
		previousPlayerHeldBy = playerHeldBy;
        SetAnimator(setOverride: true);
        CreateCameraTexture();
        reflectionCamera.enabled = true;
	}

	public override void DiscardItem()
	{
		base.DiscardItem();
		previousPlayerHeldBy.activatingItem = false;
		CancelLookCloser();
        SetAnimator(setOverride: false);
        RemoveCameraTexture();
        reflectionCamera.enabled = false;
	}

	public override void PocketItem()
	{
		base.PocketItem();
		playerHeldBy.activatingItem = false;
		CancelLookCloser();
        SetAnimator(setOverride: false);
        RemoveCameraTexture();
        reflectionCamera.enabled = false;
	}

    private void SetAnimator(bool setOverride)
    {
        if (setOverride == true)
        {
            if (playerHeldBy != null)
            {
                if (playerHeldBy == StartOfRound.Instance.localPlayerController)
                {
                    SaveAnimatorStates(playerHeldBy.playerBodyAnimator);
                    if (playerDefaultAnimatorController != playerMirrorAnimatorController)
                    {
                        playerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
                    }
                    playerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerMirrorAnimatorController;
                    SetAnimatorStates(playerHeldBy.playerBodyAnimator);
                }
                else
                {
                    SaveAnimatorStates(playerHeldBy.playerBodyAnimator);
                    if (otherPlayerDefaultAnimatorController != otherPlayerMirrorAnimatorController)
                    {
                        otherPlayerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
                    }
                    playerHeldBy.playerBodyAnimator.runtimeAnimatorController = otherPlayerMirrorAnimatorController;
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

    private void CancelLookCloser()
    {
		if (previousPlayerHeldBy != null)
		{
			previousPlayerHeldBy.activatingItem = false;
			previousPlayerHeldBy.playerBodyAnimator.SetBool("HoldMirror", value: false);
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

    private void CreateCameraTexture()
    {
        RenderTexture thisReflectionTexture = new RenderTexture (reflectionTexture);
        Material thisReflectionMaterial = new Material (reflectionMaterial);
        ReflectionObject.GetComponent<Renderer>().SetMaterial(thisReflectionMaterial);
        ReflectionObject.GetComponent<Renderer>().material.mainTexture = thisReflectionTexture;
        reflectionCamera.targetTexture = thisReflectionTexture;
        thisReflectionTexture.hideFlags = HideFlags.HideAndDontSave;
        thisReflectionMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    private void RemoveCameraTexture()
    {
        reflectionCamera.targetTexture = reflectionTexture;
        ReflectionObject.GetComponent<Renderer>().SetMaterial(reflectionMaterialOff);
    }
}