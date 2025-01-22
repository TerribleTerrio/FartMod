using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class Blowtorch : AnimatedItem
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

    [Header("Blowtorch Settings")]
    public int damage;

    public float tankTime = 145f;

    public GameObject rangeStart;

    public GameObject rangeEnd;

    public GameObject particles;

    [Space(5f)]
    public AudioSource BurnAudio;

    public GameObject sprayPaintPrefab;

	public float sprayIntervalSpeed = 0.1f;

    public int maxSprayPaintDecals = 1000;

    public static List<GameObject> sprayPaintDecals = new List<GameObject>();

	public static int sprayPaintDecalsIndex;

    public static DecalProjector previousSprayDecal;

    public GameObject sparkParticles;

    public AudioSource sparkAudio;

    public AudioClip[] sparkAudioClips;

    private int currentSparkClip = 0;

    private float sparkInterval;

    private int addSprayPaintWithFrameDelay;

    private DecalProjector delayedSprayPaintDecal;

	private RaycastHit sprayHit;

    private int sprayPaintMask = 605030721;

	private Vector3 previousSprayPosition;

    private bool isBurning;

	private float sprayInterval;

    private bool isOn;

    private float burnRange = 0.68f;

    private bool inToggleTorchAnimation;

    private Coroutine toggleTorchCoroutine;

    private Coroutine waitForTankCoroutine;

    private float torchTank = 1f;

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

	public override void LoadItemSaveData(int saveData)
	{
		base.LoadItemSaveData(saveData);
		torchTank = (float)saveData / 100f;
	}

	public override int GetItemDataToSave()
	{
		return (int)(torchTank * 100f);
	}

    public override void LateUpdate()
    {
        base.LateUpdate();
        if (addSprayPaintWithFrameDelay > 1)
		{
			addSprayPaintWithFrameDelay--;
		}
		else if (addSprayPaintWithFrameDelay == 1)
		{
			addSprayPaintWithFrameDelay = 0;
			delayedSprayPaintDecal.enabled = true;
		}
        if (isBurning && isHeld)
		{
            torchTank = Mathf.Max(torchTank - Time.deltaTime / tankTime, 0f);
            if (torchTank <= 0f)
            {
                tankEmpty = true;
            }

            if (!base.IsOwner)
            {
                return;
            }
            if (sprayInterval <= 0f)
            {
                if (TryBurning())
                {
                    sprayInterval = sprayIntervalSpeed;
                }
                else
                {
                    sprayInterval = 0.037f;
                }
            }
            else
            {
                sprayInterval -= Time.deltaTime;
            }
            
            Ray ray = new Ray(particles.transform.position, particles.transform.forward);
            if (!Physics.Raycast(ray, out sprayHit, burnRange, sprayPaintMask, QueryTriggerInteraction.Collide))
            {
                BurnAudio.volume = 0f;
            }
            else
            {
                BurnAudio.volume = 1f;
            }

            if (sparkInterval <= 0f)
            {
                sparkInterval = UnityEngine.Random.Range(0.2f, 0.7f);
                if (isBurning && Physics.Raycast(ray, out sprayHit, burnRange, sprayPaintMask, QueryTriggerInteraction.Collide))
                {
                    // I wanted to do this but I'm not sure, I'll just leave it here
                    // && sprayHit.collider.gameObject.tag != "Gravel" && sprayHit.collider.gameObject.tag != "Snow" && sprayHit.collider.gameObject.tag != "Grass"
                    Spark();
                }
            }
            else
            {
                sparkInterval -= Time.deltaTime;
            }
        }
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
        if (toggleTorchCoroutine != null)
        {
            StopCoroutine(toggleTorchCoroutine);
            toggleTorchCoroutine = null;
        }
        if (waitForTankCoroutine != null)
        {
            StopCoroutine(waitForTankCoroutine);
            waitForTankCoroutine = null;
        }
        itemAnimator.Play("off");
        inToggleTorchAnimation = false;
        isOn = false;
	}

	public override void PocketItem()
	{
		base.PocketItem();
		playerHeldBy.activatingItem = false;
        SetAnimator(setOverride: false);     
        if (toggleTorchCoroutine != null)
        {
            StopCoroutine(toggleTorchCoroutine);
            toggleTorchCoroutine = null;
        }
        if (waitForTankCoroutine != null)
        {
            StopCoroutine(waitForTankCoroutine);
            waitForTankCoroutine = null;
        }
        itemAnimator.Play("off");
        inToggleTorchAnimation = false;
        isOn = false;
	}

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        if (!inToggleTorchAnimation)
        {
            if (toggleTorchCoroutine != null)
            {
                StopCoroutine(toggleTorchCoroutine);
                toggleTorchCoroutine = null;
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
                toggleTorchCoroutine = StartCoroutine(ToggleTorchAnimation(true, tankEmpty));
            }
            else
            {
                playerHeldBy.activatingItem = true;
                toggleTorchCoroutine = StartCoroutine(ToggleTorchAnimation(false, tankEmpty));
            }
        }
    }

    private IEnumerator ToggleTorchAnimation(bool turningon = true, bool empty = false)
    {
        Debug.Log($"Blowtorch tank: {torchTank}.");
        Debug.Log($"Toggling torch animation, turning on: {turningon}, empty: {empty}.");
        inToggleTorchAnimation = true;
		playerHeldBy.activatingItem = true;
		playerHeldBy.doingUpperBodyEmote = 1.38f;
        if (turningon)
        {
            playerHeldBy.playerBodyAnimator.SetTrigger("TurnBlowtorchOn");
        }
        else
        {
            playerHeldBy.playerBodyAnimator.SetTrigger("TurnBlowtorchOff");
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
        inToggleTorchAnimation = false;
        isOn = turningon;
        Debug.Log("Toggling torch animation coroutine complete.");
        if (turningon && !empty)
        {
            waitForTankCoroutine = StartCoroutine(WaitForTank());
        }
    }

    private IEnumerator WaitForTank()
    {
        Debug.Log("Starting wait for tank coroutine.");
        yield return new WaitUntil(() => tankEmpty);
        Debug.Log($"Blowtorch tank: {torchTank}, turning off.");
        isBurning = false;
        StopBurning();
        itemAnimator.Play("emptying");
        playerHeldBy.activatingItem = false;
        inToggleTorchAnimation = false;
        isOn = false;
        Debug.Log("Wait for tank coroutine complete.");
    }

    public void StartBurning()
    {
        isBurning = true;
        BurnAudio.Play();
    }

    public void StopBurning()
    {
        isBurning = false;
        BurnAudio.Stop();
    }

    public void Spark()
    {
        if (currentSparkClip != 6)
        {
            currentSparkClip++;
        }
        else
        {
            currentSparkClip = 0;
        }
        MakeNoise();
        sparkAudio.PlayOneShot(sparkAudioClips[currentSparkClip]);
        WalkieTalkie.TransmitOneShotAudio(sparkAudio, sparkAudioClips[currentSparkClip]);
        SparkServerRpc(currentSparkClip);
        GameObject newSpark = UnityEngine.Object.Instantiate(sparkParticles, sparkParticles.transform.position, Quaternion.identity, null);
        newSpark.SetActive(true);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SparkServerRpc(int clip = 0)
    {
        SparkClientRpc(clip);
    }

    [ClientRpc]
    public void SparkClientRpc(int clip = 0)
    {
        if (!base.IsOwner)
        {
            MakeNoise();
            sparkAudio.PlayOneShot(sparkAudioClips[clip]);
            WalkieTalkie.TransmitOneShotAudio(sparkAudio, sparkAudioClips[currentSparkClip]);
            GameObject newSpark = UnityEngine.Object.Instantiate(sparkParticles, sparkParticles.transform.position, Quaternion.identity, null);
            newSpark.SetActive(true);
        }
    }

    public void DamageWithFlame()
    {
        if (playerHeldBy.inSpecialInteractAnimation)
        {
            itemAnimator.Play("off");
            isOn = false;
            return;
        }
        //CHECK FOR COLLIDERS
        Collider[] colliders = checkColliders();
        for (int i = 0; i < colliders.Length; i++)
        {
            //FOR PLAYERS
            if (colliders[i].gameObject.layer == 3)
            {
                PlayerControllerB playerControllerB = colliders[i].gameObject.GetComponent<PlayerControllerB>();
                if (playerControllerB != null && playerControllerB != playerHeldBy)
                {
                    Vector3 bodyVelocity = Vector3.Normalize(playerControllerB.gameplayCamera.transform.position - base.transform.position) * 80f / Vector3.Distance(playerControllerB.gameplayCamera.transform.position, base.transform.position);
                    playerControllerB.DamagePlayer(5, hasDamageSFX: true, callRPC: true, CauseOfDeath.Burning);
                    Debug.Log($"Blowtorch damaged a player.");
                    Spark();
                    if (playerControllerB.isHoldingObject)
                    {
                        if (playerControllerB.currentlyHeldObjectServer.gameObject.GetComponent<Vase>())
                        {
                            playerControllerB.currentlyHeldObjectServer.gameObject.GetComponent<Vase>().ExplodeAndSync();
                        }
                    }
                }
            }

            //FOR ENEMIES
            else if (colliders[i].gameObject.layer == 19)
            {
                EnemyAICollisionDetect enemy = colliders[i].gameObject.GetComponentInChildren<EnemyAICollisionDetect>();
                if (enemy != null && enemy.mainScript.IsOwner)
                {
                    enemy.mainScript.HitEnemyOnLocalClient(damage, transform.forward, playerHeldBy, playHitSFX: true);
                    Debug.Log($"Blowtorch damaged an enemy.");
                    Spark();
                }
            }

            //FOR ITEMS
            else if (colliders[i].gameObject.GetComponentInParent<ArtilleryShellItem>() != null)
            {
                Debug.Log($"Blowtorch detected Artillery Shell.");
                Spark();
                colliders[i].gameObject.GetComponentInParent<ArtilleryShellItem>().ArmShellAndSync();
                continue;
            }

            else if (colliders[i].gameObject.GetComponentInParent<PunchingBag>() != null)
            {
                Debug.Log($"Blowtorch detected Punching Bag.");
                Spark();
                colliders[i].gameObject.GetComponentInParent<PunchingBag>().PunchAndSync(true, "Blowtorch");
            }

            else if (colliders[i].gameObject.GetComponentInParent<HydraulicStabilizer>() != null)
            {
                Debug.Log($"Blowtorch detected Hydraulic.");
                Spark();
                colliders[i].gameObject.GetComponentInParent<HydraulicStabilizer>().GoPsychoAndSync();
            }

            else if (colliders[i].gameObject.GetComponentInParent<Vase>() != null)
            {
                Debug.Log($"Blowtorch detected Vase.");
                Spark();
                colliders[i].gameObject.GetComponentInParent<Vase>().ExplodeAndSync();
            }

            else if (colliders[i].gameObject.GetComponentInParent<Toaster>() != null)
            {
                Debug.Log($"Blowtorch detected Toaster.");
                Spark();
                colliders[i].gameObject.GetComponentInParent<Toaster>().EjectAndSync();
            }

            //FOR HAZARDS
            else if (colliders[i].gameObject.GetComponentInParent<Landmine>() != null)
            {
                Debug.Log($"Blowtorch detected Landmine.");
                Spark();
                Landmine landmine = colliders[i].gameObject.GetComponentInParent<Landmine>();
                landmine.SetOffMineAnimation();
                landmine.sendingExplosionRPC = true;
                landmine.ExplodeMineServerRpc();
            }

            else if (colliders[i].gameObject.GetComponentInParent<Turret>() != null)
            {
                Debug.Log($"Blowtorch detected Turret.");
                Spark();
                Turret turret = colliders[i].gameObject.GetComponentInParent<Turret>();
                if (turret.turretMode == TurretMode.Berserk || turret.turretMode == TurretMode.Firing)
                {
                    return;
                }
                turret.SwitchTurretMode(3);
                turret.EnterBerserkModeServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
            }
        }
    }

    public void MakeNoise()
    {
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
    }

    private Collider[] checkColliders()
    {
        Collider[] colliders = Physics.OverlapCapsule(rangeStart.transform.position, rangeEnd.transform.position, 0.3f, 1076363336, QueryTriggerInteraction.Collide);
        return colliders;
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

    // --- BURN DECALS - RIPPED FROM SPRAY PAINT CAN! ---
    public bool TryBurning()
	{
		if (BurnDecalLocal(particles.transform.position, particles.transform.forward))
		{
			BurnDecalServerRpc(particles.transform.position, particles.transform.forward);
			return true;
		}
		return false;
	}

    [ServerRpc]
    public void BurnDecalServerRpc(Vector3 sprayPos, Vector3 sprayRot)
    {
        {
            BurnDecalClientRpc(sprayPos, sprayRot);
        }
    }

    [ClientRpc]
    public void BurnDecalClientRpc(Vector3 sprayPos, Vector3 sprayRot)
    {
        if (!base.IsOwner)
        {
            BurnDecalLocal(sprayPos, sprayRot);
        }
    }

	private void ToggleBurnDecalOnHolder(bool enable)
	{
		if (!enable)
		{
			for (int i = 0; i < playerHeldBy.bodyPartSpraypaintColliders.Length; i++)
			{
				playerHeldBy.bodyPartSpraypaintColliders[i].enabled = false;
				playerHeldBy.bodyPartSpraypaintColliders[i].gameObject.layer = 2;
			}
		}
		else
		{
			for (int j = 0; j < playerHeldBy.bodyPartSpraypaintColliders.Length; j++)
			{
				playerHeldBy.bodyPartSpraypaintColliders[j].enabled = false;
				playerHeldBy.bodyPartSpraypaintColliders[j].gameObject.layer = 29;
			}
		}
	}

	private bool BurnDecalLocal(Vector3 sprayPos, Vector3 sprayRot)
	{
		if (playerHeldBy == null)
		{
			return false;
		}
		ToggleBurnDecalOnHolder(enable: false);
		if (RoundManager.Instance.mapPropsContainer == null)
		{
			RoundManager.Instance.mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer");
		}
		Ray ray = new Ray(sprayPos, sprayRot);
		if (!Physics.Raycast(ray, out sprayHit, burnRange, sprayPaintMask, QueryTriggerInteraction.Collide))
		{
			ToggleBurnDecalOnHolder(enable: true);
			return false;
		}
		if (Vector3.Distance(sprayHit.point, previousSprayPosition) < 0.015f)
		{
			ToggleBurnDecalOnHolder(enable: true);
			return false;
		}
		int num = -1;
		Transform transform;
		if (sprayHit.collider.gameObject.layer == 11 || sprayHit.collider.gameObject.layer == 8 || sprayHit.collider.gameObject.layer == 0)
		{
			transform = (!playerHeldBy.isInElevator && !StartOfRound.Instance.inShipPhase && !(RoundManager.Instance.mapPropsContainer == null)) ? RoundManager.Instance.mapPropsContainer.transform : StartOfRound.Instance.elevatorTransform;
		}
		else
		{
			if (sprayHit.collider.tag.StartsWith("PlayerBody"))
			{
				switch (sprayHit.collider.tag)
				{
				case "PlayerBody":
					num = 0;
					break;
				case "PlayerBody1":
					num = 1;
					break;
				case "PlayerBody2":
					num = 2;
					break;
				case "PlayerBody3":
					num = 3;
					break;
				}
				if (num == (int)playerHeldBy.playerClientId)
				{
					ToggleBurnDecalOnHolder(enable: true);
					return false;
				}
			}
			else if (sprayHit.collider.tag.StartsWith("PlayerRagdoll"))
			{
				switch (sprayHit.collider.tag)
				{
				case "PlayerRagdoll":
					num = 0;
					break;
				case "PlayerRagdoll1":
					num = 1;
					break;
				case "PlayerRagdoll2":
					num = 2;
					break;
				case "PlayerRagdoll3":
					num = 3;
					break;
				}
			}
			transform = sprayHit.collider.transform;
		}
		sprayPaintDecalsIndex = (sprayPaintDecalsIndex + 1) % maxSprayPaintDecals;
		DecalProjector decalProjector = null;
		GameObject gameObject;
		if (sprayPaintDecals.Count <= sprayPaintDecalsIndex)
		{
			for (int i = 0; i < 200; i++)
			{
				if (sprayPaintDecals.Count >= maxSprayPaintDecals)
				{
					break;
				}
				gameObject = UnityEngine.Object.Instantiate(sprayPaintPrefab, transform);
				sprayPaintDecals.Add(gameObject);
				decalProjector = gameObject.GetComponent<DecalProjector>();
			}
		}
		if (sprayPaintDecals[sprayPaintDecalsIndex] == null)
		{
			gameObject = UnityEngine.Object.Instantiate(sprayPaintPrefab, transform);
			sprayPaintDecals[sprayPaintDecalsIndex] = gameObject;
		}
		else
		{
			if (!sprayPaintDecals[sprayPaintDecalsIndex].activeSelf)
			{
				sprayPaintDecals[sprayPaintDecalsIndex].SetActive(value: true);
			}
			gameObject = sprayPaintDecals[sprayPaintDecalsIndex];
		}
		decalProjector = gameObject.GetComponent<DecalProjector>();
		switch (num)
		{
            case 0:
                decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer4;
                break;
            case 1:
                decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer5;
                break;
            case 2:
                decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer6;
                break;
            case 3:
                decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer7;
                break;
            case -1:
                decalProjector.decalLayerMask = DecalLayerEnum.DecalLayerDefault;
                break;
		}
		gameObject.transform.position = ray.GetPoint(sprayHit.distance - 0.1f);
		gameObject.transform.forward = sprayRot;
		if (gameObject.transform.parent != transform)
		{
			gameObject.transform.SetParent(transform);
		}
		previousSprayPosition = sprayHit.point;
		addSprayPaintWithFrameDelay = 2;
		delayedSprayPaintDecal = decalProjector;
		ToggleBurnDecalOnHolder(enable: true);
		return true;
	}

}