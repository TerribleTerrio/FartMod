using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
    
public class HandMirror : GrabbableObject
{
    [HideInInspector]
    public RuntimeAnimatorController playerDefaultAnimatorController;

    [Header("Override animator to replace current player animator")]
    public AnimatorOverrideController playerOverrideAnimator;

    [Header("Reflection")]
    public Camera reflectionCamera;
    
    public RenderTexture reflectionTexture;

    public Material reflectionMaterial;

    public Material reflectionMaterialOff;

    public GameObject ReflectionObject;

	private PlayerControllerB previousPlayerHeldBy;

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
                // CancelLookCloser();
                // SetAnimatorAsOverrideServerRpc(setOverride: false);
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

    // public override void ItemActivate(bool used, bool buttonDown = true)
	// {
	// 	base.ItemActivate(used, buttonDown);
	// 	if (!(playerHeldBy == null) && base.IsOwner)
	// 	{
	// 		playerHeldBy.playerBodyAnimator.SetBool("HoldMask", buttonDown);
	// 		playerHeldBy.activatingItem = buttonDown;
    //     }
	// }

	public override void EquipItem()
	{
		base.EquipItem();
		previousPlayerHeldBy = playerHeldBy;
        // SetAnimatorAsOverrideServerRpc(setOverride: true);
        CreateCameraTexture();
        reflectionCamera.enabled = true;
	}

	public override void DiscardItem()
	{
		base.DiscardItem();
		previousPlayerHeldBy.activatingItem = false;
		// CancelLookCloser();
        // SetAnimatorAsOverrideServerRpc(setOverride: false);
        RemoveCameraTexture();
        reflectionCamera.enabled = false;
	}

	public override void PocketItem()
	{
		base.PocketItem();
		playerHeldBy.activatingItem = false;
		// CancelLookCloser();
        // SetAnimatorAsOverrideServerRpc(setOverride: false);
        RemoveCameraTexture();
        reflectionCamera.enabled = false;
	}

    [ServerRpc(RequireOwnership = false)]
    private void SetAnimatorAsOverrideServerRpc(bool setOverride)
    {
        SetAnimatorAsOverrideClientRpc(setOverride);
    }

    [ClientRpc]
    private void SetAnimatorAsOverrideClientRpc(bool setOverride)
    {
        if (setOverride == true)
        {
            if (playerHeldBy != null)
            {
                if (playerDefaultAnimatorController != playerOverrideAnimator)
                {
                    playerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
                }
                playerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerOverrideAnimator;
            }
        }

        else
        {
            if (previousPlayerHeldBy != null)
            {
                previousPlayerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerDefaultAnimatorController;
            }
        }
    }

    // private void SetAnimatorAsOverride()
    // {
	// 	if (previousPlayerHeldBy != null)
	// 	{
    //         if (playerDefaultAnimatorController != playerOverrideAnimator)
    //         {
    //             playerDefaultAnimatorController = playerHeldBy.playerBodyAnimator.runtimeAnimatorController;
    //         }
    //         playerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerOverrideAnimator;
    //     }
    // }

    // private void SetAnimatorAsDefault()
    // {
	// 	if (previousPlayerHeldBy != null)
	// 	{
    //         previousPlayerHeldBy.playerBodyAnimator.runtimeAnimatorController = playerDefaultAnimatorController;
    //     }
    // }

    // private void CancelLookCloser()
    // {
	// 	if (previousPlayerHeldBy != null)
	// 	{
	// 		previousPlayerHeldBy.activatingItem = false;
	// 		previousPlayerHeldBy.playerBodyAnimator.SetBool("HoldMask", value: false);
	// 	}
    // }

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