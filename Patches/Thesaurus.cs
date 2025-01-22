using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using TMPro;
using System.Threading.Tasks;
using System;

public class Thesaurus : AnimatedItem
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

    [Header("Thesaurus Settings")]

    public BookContent thesaurusContent;

    public TMP_Text[] displayPages;

    public TMP_Text[] displayPagination;

	public int currentPageIndex = 20;

    public int maxPageIndex = 50;

    public Camera textCamera;

    public RenderTexture[] renderTextures;

    public GameObject lineOfSightBlocker;

    private bool inFlipPageAnimation;

    private Coroutine flipPageCoroutine;

    private Coroutine inspectBookCoroutine;

    private bool savedPage;

    /* --- CURRENT PAGE INDEX ---

    The "Current Page Index" is an integer representing 6 pages, incremented by 2 every time you flip left or right to account for each page being double-sided.

    If the minimum is 0 and the maximum is 200, index "0" will display the pages 0 through 5, and index "200" will display the pages 200 through 205.

    Of the 6 pages, the first and last 2 pages are for seamless transitions in animation and are displayed on the body of the book. Only the middle 2 pages are ever readable to the player, as the other 4 pages are obscured in motion.

    If "currentPageIndex" is, for example, 134, the displayed pages on the books surface will be 134, 135, 136, 137, 138, and 139. The pages in the middle of the book splayed out that the player can actually see will be pages 136 and 137, with 135 being on the back side of page 136, and 138 being on the back side of page 137.

    In general, the maxPageIndex should be 6 less than the total amount of actual pages in the book contents!
    
    */

	public override void LoadItemSaveData(int saveData)
	{
		base.LoadItemSaveData(saveData);
		currentPageIndex = saveData;
        if (saveData != 0)
        {
            savedPage = true;
        }
	}

	public override int GetItemDataToSave()
	{
		return currentPageIndex;
	}

    public override void Start()
    {
        base.Start();
        ChangePages(currentPageIndex);
        StartCoroutine(DelaySetup());
    }

    public IEnumerator DelaySetup()
    {
        yield return new WaitForSeconds(1);
        Debug.Log("Thesaurus doing first-time setup!");
        textCamera.enabled = false;
        for (int i = 0; i < displayPages.Length; i++)
        {
            displayPages[i].gameObject.SetActive(false);
        }
		System.Random random = new System.Random((int)targetFloorPosition.x + (int)targetFloorPosition.y);
        float randomstart = random.Next(maxPageIndex / 4, maxPageIndex / 2);
        if (!savedPage)
        {
            currentPageIndex = 2 * Mathf.RoundToInt(randomstart / 2);
        }
        ChangePages(currentPageIndex);
        ChangeSizeAndSync(true);
    }

    public void ChangeSizeAndSync(bool grow = false)
    {
        StartCoroutine(ChangeSize(grow));
        ChangeSizeServerRpc(grow);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeSizeServerRpc(bool grow = false)
    {
        ChangeSizeClientRpc(grow);
    }

     [ClientRpc]
    public void ChangeSizeClientRpc(bool grow = false)
    {
        if (!base.IsOwner)
        {
            StartCoroutine(ChangeSize(grow));
        }
    }

    public IEnumerator ChangeSize(bool grow = false)
    {
        Vector3 growsize = new Vector3(120, 120, 120);
        Vector3 shrinksize = new Vector3(100, 100, 100);
        float timespent = 0f;
        float duration = 0.3f;
        if (grow)
        {
            yield return null;
            while (timespent < duration)
            {
                itemAnimator.gameObject.transform.localScale = Vector3.Lerp(itemAnimator.gameObject.transform.localScale, growsize, timespent / duration);
                timespent += Time.deltaTime;
                yield return null;
            }
            itemAnimator.gameObject.transform.localScale = growsize;
        }
        else
        {
            yield return new WaitForSeconds(0.1f);
            while (timespent < duration)
            {
                itemAnimator.gameObject.transform.localScale = Vector3.Lerp(itemAnimator.gameObject.transform.localScale, shrinksize, timespent / duration);
                timespent += Time.deltaTime;
                yield return null;
            }
            itemAnimator.gameObject.transform.localScale = shrinksize;
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
                CancelLookCloser();
                SetAnimator(setOverride: false);
            }
        }
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
    }
    
    public override void EquipItem()
	{
		base.EquipItem();
        playerHeldBy.equippedUsableItemQE = true;
		previousPlayerHeldBy = playerHeldBy;
        SetAnimator(setOverride: true);
        playerHeldBy.playerBodyAnimator.Play("GrabThesaurus");
        playerHeldBy.playerBodyAnimator.SetTrigger("OpenThesaurus");
        ChangeSizeAndSync(false);
	}

	public override void DiscardItem()
	{
		if (playerHeldBy != null)
		{
			playerHeldBy.equippedUsableItemQE = false;
		}
        if (flipPageCoroutine != null)
        {
            StopCoroutine(flipPageCoroutine);
            flipPageCoroutine = null;
        }
		isBeingUsed = false;
		base.DiscardItem();
        previousPlayerHeldBy.activatingItem = false;
        inFlipPageAnimation = false;
        CancelLookCloser();
        SetAnimator(setOverride: false);
        ChangeSizeAndSync(true);
        lineOfSightBlocker.GetComponent<Collider>().enabled = false;
	}

	public override void PocketItem()
	{
		if (base.IsOwner && playerHeldBy != null)
		{
			playerHeldBy.equippedUsableItemQE = false;
            playerHeldBy.activatingItem = false;
			isBeingUsed = false;
            inFlipPageAnimation = false;
		}
        if (flipPageCoroutine != null)
        {
            StopCoroutine(flipPageCoroutine);
            flipPageCoroutine = null;
        }
		base.PocketItem();
        CancelLookCloser(); 
		if (itemAnimator != null)
		{
			itemAnimator.SetBool(grabItemBoolString, value: false);
		}
        lineOfSightBlocker.GetComponent<Collider>().enabled = false;
	}

	public override void ItemInteractLeftRight(bool right)
	{
        if (!inFlipPageAnimation)
        {
            int prevPageIndex = currentPageIndex;
            if (right)
            {
                currentPageIndex = Mathf.Clamp(currentPageIndex + 2, 0, maxPageIndex);
            }
            else
            {
                currentPageIndex = Mathf.Clamp(currentPageIndex - 2, 0, maxPageIndex);
            }
            if (flipPageCoroutine != null)
            {
                StopCoroutine(flipPageCoroutine);
                flipPageCoroutine = null;
            }
            if (prevPageIndex < currentPageIndex)
            {
                flipPageCoroutine = StartCoroutine(FlipPageAnimation(true));
            }
            else if (prevPageIndex > currentPageIndex)
            {
                flipPageCoroutine = StartCoroutine(FlipPageAnimation(false));
            }
        }
	}

    public override void ItemActivate(bool used, bool buttonDown = true)
	{
		base.ItemActivate(used, buttonDown);
		if (!(playerHeldBy == null) && base.IsOwner)
		{
			playerHeldBy.playerBodyAnimator.SetBool("HoldThesaurus", buttonDown);
			playerHeldBy.activatingItem = buttonDown;
            HUDManager.Instance.SetNearDepthOfFieldEnabled(!buttonDown);
            AudioListAnimationEvent lookEvent = itemAnimator.gameObject.GetComponent<AudioListAnimationEvent>();
            if (buttonDown)
            {
                lookEvent.PlayListAudio(4);
            }
            else
            {
                lookEvent.StopListAudio();
            }
            if (inspectBookCoroutine != null)
            {
                StopCoroutine(inspectBookCoroutine);
                inspectBookCoroutine = null;
            }
            inspectBookCoroutine = StartCoroutine(InspectBookAnimation(buttonDown));
        }
        
	}
    
    private IEnumerator InspectBookAnimation(bool blocking = true)
    {
        if (!blocking)
        {
            lineOfSightBlocker.GetComponent<Collider>().enabled = false;
            yield break;
        }
        else
        {
            yield return new WaitForSeconds(0.6f);
            lineOfSightBlocker.GetComponent<Collider>().enabled = true;
        }
    }

    public void ChangePagesIndex()
    {
        ChangePages(currentPageIndex);
    }

    public void ChangePagesAndSync()
    {
        ChangePages(currentPageIndex);
        ChangePagesServerRpc(currentPageIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangePagesServerRpc(int serverindex = 0)
    {
        ChangePagesClientRpc(serverindex);
    }

     [ClientRpc]
    public void ChangePagesClientRpc(int serverindex = 0)
    {
        if (!base.IsOwner)
        {
            ChangePages(serverindex);
        }
    }

    public void ChangePages(int index = 0)
    {
        for (int i = 0; i < displayPages.Length; i++)
        {
            displayPages[i].text = thesaurusContent.bookPages[i + index].pageContent;
        }
        for (int i = 0; i < displayPagination.Length; i++)
        {
            int fakeNumber = thesaurusContent.bookPages[i + index].pageNumber;
            fakeNumber += 255;
            displayPagination[i].text = fakeNumber.ToString();
        }
        RenderPages();
    }

    public void RenderPages()
    {
        for (int i = 0; i < renderTextures.Length; i++)
        {
            textCamera.gameObject.SetActive(true);
            displayPages[i].gameObject.SetActive(true);
            displayPagination[i].gameObject.SetActive(true);
            textCamera.targetTexture = renderTextures[i];
            textCamera.Render();
            displayPages[i].gameObject.SetActive(false);
            displayPagination[i].gameObject.SetActive(false);
            textCamera.gameObject.SetActive(false);
        }
    }

   private IEnumerator FlipPageAnimation(bool right = true)
   {
        inFlipPageAnimation = true;
		playerHeldBy.activatingItem = true;
        if (right)
        {
            itemAnimator.SetTrigger("FlipRight");
        }
        else
        {
            itemAnimator.SetTrigger("FlipLeft");
        }
        yield return new WaitForSeconds(1f);
        playerHeldBy.activatingItem = false;
        inFlipPageAnimation = false;
   }

    private void CancelLookCloser()
    {
		if (previousPlayerHeldBy != null)
		{
			previousPlayerHeldBy.playerBodyAnimator.SetBool("HoldThesaurus", value: false);
            previousPlayerHeldBy.activatingItem = false;
		}
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