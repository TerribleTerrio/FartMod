using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using GameNetcodeStuff;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Unity.Netcode;

public class ScarecrowHallucination : NetworkBehaviour, INoiseListener
{
    [HideInInspector]
    public Scarecrow mainScarecrow;

    public EnemyType enemyType;

    [HideInInspector]
	public PlayerControllerB closestPlayer;

    public AudioListAnimationEvent audioEvent;

    public GameObject[] ghostParts;

    public AudioSource[] ghostAudioSources;

    public NavMeshAgent agent;

    public Animator hallucinationAnimator;

    public GameObject[] LOSTriggers;

    public float walkSpeed;

    public float runSpeed;

    public bool fading = false;

    public enum Ghosts
    {
        MouthDog,

        ForestGiant,

        RadMech,
        
        MaskedPlayerEnemy
    }

    public Ghosts chosenEnemy;

    public int chosenBehaviour;
    
    private Coroutine faceTowardsPlayerCoroutine;

    private Coroutine changeAgentSpeedCoroutine;

    private bool inSpecialAnimation;

    private Vector3 lastSeenPosition;

    private float seenTimer;

    private float seenUpdateCooldown = 0.8f;

    [Space(10f)]
    [Header("Mouth Dog")]
    public float dogHearNoiseCooldown = 0.03f;

    private int dogSuspicionLevel;

    private bool dogEnraged;

    private bool dogAlerted;

    private Vector3 dogHeardNoisePosition;

    private Vector3 dogPrevPosition;

    [Space(10f)]
    [Header("Forest Giant")]
    private Vector3 giantVelocity;

    private Vector3 giantPrevPosition;

    private float giantVelX;

    private float giantVelZ;

    private bool giantDanger;

    [Space(10f)]
    [Header("Rad Mech")]
    public GameObject spotlight;

    public Material spotlightMat;

    public Material defaultMat;

    public AudioClip spotlightOff;

    public Transform leftFootPoint;

    public ParticleSystem leftFootParticle;

    public Transform rightFootPoint;

    public ParticleSystem rightFootParticle;

    public GameObject startChargingEffectContainer;

	public ParticleSystem chargeParticle;

    public Transform torsoContainer;

    public Transform torsoDefaultRotation;

    public Transform gunArm;

    public Transform defaultArmRotation;

    public Transform gunPoint;

    public ParticleSystem gunArmParticle;

    public GameObject missilePrefab;

    public GameObject explosionPrefab;

    public AudioClip[] shootGunSFX;

    public AudioClip[] largeExplosionSFX;

    private static List<AudioSource> explosionSources = [];

    private bool takingStep;

    private bool leftFoot;

    private bool disableWalking;

    private bool aimingGun;

    private bool doTurnTorso;

    private float walkStepTimer;

    private float timeBetweenSteps = 0.7f;

    private float timeToTakeStep = 0.23f;

    private float stepMovementSpeed = 11f;

    private bool chargingForward;

    private bool startedChargeEffect;

    private float beginChargingTimer;

    private float chargeForwardSpeed = 35f;
    
    private float shootCooldown;

    private float shootUptime = 1.25f;

    private float shootDowntime = 0.92f;

    private float shootTimer;

    private float fireRate = 0.4f;

    private float fireRateVariance = 0.3f;

    private float forwardAngleCompensation = 0.25f;

    private float gunArmSpeed = 40;

    [HideInInspector]
    public float missileSpeed = 0.3f;

    [HideInInspector]
    public float currentMissileSpeed;

    [HideInInspector]
    public float missileWarbleLevel = 0.73f;

    [HideInInspector]
    public int missilesFired;

    [Space(10f)]
    [Header("Masked Player Enemy")]
	public AudioSource movementAudio;

    public RandomPeriodicAudioPlayer maskedAudioPlayer;

	public Transform maskedHeadTiltTarget;

	private Ray enemyRay;

	private RaycastHit enemyRayHit;

	private int currentFootstepSurfaceIndex;

	private int previousFootstepClip;

	private bool maskedSprinting;

    private bool maskedHandsOut;

    private bool maskedAllowSprinting;

    private bool maskedAllowStopSprinting;

    private bool maskedLaughWhenStopped;

    private bool maskedContinueWhenStopped;

    public void Update()
    {
        if (!mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            return;
        }
        switch (chosenEnemy)
        {
            case Ghosts.MouthDog:
                if (!fading && PlayerHasLineOfSightToGhost(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.2f, 0.4f);
                }
                if (chosenBehaviour >= 2)
                {
                    dogHearNoiseCooldown -= Time.deltaTime;
                    hallucinationAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(base.transform.position - dogPrevPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
                    dogPrevPosition = base.transform.position;
                }
                break;

            case Ghosts.ForestGiant:
                if (!fading && giantDanger)
                {
                    GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.2f, 0.9f);
                }
                else if (!fading)
                {
                    GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.2f, 0.6f);
                }
                if (!fading && PlayerHasLineOfSightToGhost(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(1f);
                }
                giantVelocity = hallucinationAnimator.transform.InverseTransformDirection(Vector3.ClampMagnitude(base.transform.position - giantPrevPosition, 1f) / (Time.deltaTime * 4f));
                giantVelX = Mathf.Lerp(giantVelX, giantVelocity.x, 5f * Time.deltaTime);
                hallucinationAnimator.SetFloat("VelocityX", Mathf.Clamp(giantVelX, 0f - 1f, 1f));
                giantVelZ = Mathf.Lerp(giantVelZ, giantVelocity.z, 5f * Time.deltaTime);
                hallucinationAnimator.SetFloat("VelocityZ", Mathf.Clamp(giantVelZ, 0f - 1f, 1f));
                giantPrevPosition = base.transform.position;
                break;

            case Ghosts.RadMech:
                beginChargingTimer += Time.deltaTime;
                if (seenTimer > 0f)
                {
                    seenTimer -= Time.deltaTime;
                }
                else
                {
                    seenTimer = seenUpdateCooldown;
                    lastSeenPosition = closestPlayer.transform.position;
                }
                if (aimingGun)
                {
                    shootCooldown = Mathf.Min(shootCooldown + Time.deltaTime * shootUptime, 10f);
                    AimAndShootCycle();
                }
                else
                {
                    shootCooldown = Mathf.Max(shootCooldown - Time.deltaTime * shootDowntime, 0f);
                }
                if (!inSpecialAnimation)
                {
                    DoFootstepCycle();
                    AimGunArmTowardsTarget();
                    TurnTorsoToTarget();
                }
                break;

            case Ghosts.MaskedPlayerEnemy:
                float dist = Vector3.Distance(transform.position, closestPlayer.transform.position);
                if (dist > 4f)
                {
                    if (!maskedSprinting && maskedAllowSprinting)
                    {
                        maskedSprinting = true;
                        hallucinationAnimator.SetBool("Running", value: true);
                        agent.speed = runSpeed;
                    }
                    if (dist > 8f)
                    {
                        if (maskedHandsOut)
                        {
                            maskedHandsOut = false;
                            hallucinationAnimator.SetBool("HandsOut", value: false);
                        }
                    }
                }
                else if (dist < 6f)
                {
                    if (!maskedHandsOut)
                    {
                        maskedHandsOut = true;
                        hallucinationAnimator.SetBool("HandsOut", value: true);
                    }
                    if (dist < 3f && maskedSprinting && maskedAllowStopSprinting)
                    {
                        maskedSprinting = false;
                        hallucinationAnimator.SetBool("Running", value: false);
                        agent.speed = walkSpeed;
                    }
                }
                maskedHeadTiltTarget.LookAt(closestPlayer.gameplayCamera.transform);
                maskedHeadTiltTarget.localEulerAngles = new Vector3(maskedHeadTiltTarget.localEulerAngles.x, 0f, 0f);
                break;
        }
    }

	public PlayerControllerB ClosestPlayerToGhost(List<PlayerControllerB> players)
	{
        PlayerControllerB resultPlayer = null;
        foreach(PlayerControllerB currentPlayer in players)
        {
            if (resultPlayer == null)
            {
                resultPlayer = currentPlayer;
            }
            else
            {
                if (Vector3.Distance(transform.position, currentPlayer.transform.position) < Vector3.Distance(transform.position, resultPlayer.transform.position))
                {
                    resultPlayer = currentPlayer;
                }
            }
        }
        return resultPlayer;
	}

    public bool PlayerHasLineOfSightToGhost(PlayerControllerB player, float setWidth = 45)
    {
        float setProx = (int)chosenEnemy switch
        {
            0 => 1.5f,
            1 => 4.5f,
            2 => 4f,
            3 => 1f,
            _ => 2f
        };
        int setRange = 45;
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Foggy || StartOfRound.Instance.currentLevel.PlanetName == "85 Rend" || StartOfRound.Instance.currentLevel.PlanetName == "8 Titan" || StartOfRound.Instance.currentLevel.PlanetName == "91 Bellow")
        {
            setRange = Mathf.RoundToInt(setRange * 0.4f);
        }
        foreach (GameObject obj in LOSTriggers)
        {
            if (player.HasLineOfSightToPosition(obj.transform.position, setWidth, setRange, setProx))
            {
                return true;
            }
        }
        return false;
    }

    public void DoPossibleBehaviours()
    {
        switch (chosenEnemy)
        {
            case Ghosts.MouthDog:
                DoMouthDogBehaviours();
                break;
            case Ghosts.ForestGiant:
                DoForestGiantBehaviours();
                break;
            case Ghosts.RadMech:
                DoRadMechBehaviours();
                break;
            case Ghosts.MaskedPlayerEnemy:
                DoMaskedPlayerEnemyBehaviours();
                break;
        }
    }

    public void DoMouthDogBehaviours()
    {
        switch (chosenBehaviour)
        {
            case 0:
                if (mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.5f);
                }
                hallucinationAnimator.Play("DogSuspicious");
                break;

            case 1:
                if (mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.7f);
                    if (Vector3.Distance(transform.position, GameNetworkManager.Instance.localPlayerController.transform.position) < 16f)
                    {
                        HUDManager.Instance.ShakeCamera(ScreenShakeType.Long);
                        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.5f);
                    }
                }
                hallucinationAnimator.Play("DogScream");
                break;

            case 2:
                StartCoroutine(DogReactToNoise(right: true));
                break;

            case 3:
                StartCoroutine(DogReactToNoise(right: false));
                break;

            case 4:
                StartCoroutine(DogRunAndLunge());
                break;
        }
    }

    public void DoForestGiantBehaviours()
    {
        switch (chosenBehaviour)
        {
            case 0:
                if (mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.3f);
                }
                StartCoroutine(GiantStare());
                break;

            case 1:
                if (mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.6f);
                }
                StartCoroutine(GiantRun());
                break;

            case 2:
                if (mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.3f);
                }
                StartCoroutine(GiantStareAndRun(delayed: false));
                break;

            case 3:
                if (mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
                {
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.3f);
                }
                StartCoroutine(GiantStareAndRun(delayed: true));
                break;

            case 4:
                StartCoroutine(GiantWander());
                break;
        }
    }

    public void DoRadMechBehaviours()
    {
        switch (chosenBehaviour)
        {
            case 0:
                StartCoroutine(MechInvestigate());
                break;

            case 1:
                StartCoroutine(MechAggro());
                break;

            case 2:
                StartCoroutine(MechCharge());
                break;

            case 3:
                StartCoroutine(MechRockets());
                break;

            case 4:
                StartCoroutine(MechAggro(delayStart: true));
                break;
        }
    }

    public void DoMaskedPlayerEnemyBehaviours()
    {
        switch (chosenBehaviour)
        {
            case 0:
                StartCoroutine(MaskedChase());
                break;

            case 1:
                StartCoroutine(MaskedChase(stopSprinting: false));
                break;

            case 2:
                StartCoroutine(MaskedFollow());
                break;

            case 3:
                StartCoroutine(MaskedFollow(forceLaugh: true));
                break;

            case 4:
                StartCoroutine(MaskedFollowAndStare());
                break;
        }
    }

    public void StartFaceTowardsPlayer(float time)
    {
        agent.speed = 0f;
        if (faceTowardsPlayerCoroutine != null)
        {
            StopCoroutine(faceTowardsPlayerCoroutine);
            faceTowardsPlayerCoroutine = null;
        }
        faceTowardsPlayerCoroutine = StartCoroutine(FaceTowardsTransform(closestPlayer.transform, time));
    }

    public void StopFaceTowardsPlayer()
    {
        if (faceTowardsPlayerCoroutine != null)
        {
            StopCoroutine(faceTowardsPlayerCoroutine);
            faceTowardsPlayerCoroutine = null;
        }
    }

    public void WalkTowardsNearestPlayer(float time)
    {
        agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        if (changeAgentSpeedCoroutine != null)
        {
            StopCoroutine(changeAgentSpeedCoroutine);
            changeAgentSpeedCoroutine = null;
        }
        changeAgentSpeedCoroutine = StartCoroutine(ChangeAgentSpeed(agent.speed, walkSpeed, time));
    }

    public void RunTowardsNearestPlayer(float time)
    {
        agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        if (changeAgentSpeedCoroutine != null)
        {
            StopCoroutine(changeAgentSpeedCoroutine);
            changeAgentSpeedCoroutine = null;
        }
        changeAgentSpeedCoroutine = StartCoroutine(ChangeAgentSpeed(agent.speed, runSpeed, time));
    }

    public void KeepMovingInDirection(float distance = 4f)
    {
		Ray ray = new(transform.position + Vector3.up * 2f, transform.forward);
		Vector3 pos = (!Physics.Raycast(ray, out RaycastHit rayHit, distance, StartOfRound.Instance.collidersAndRoomMask)) ? ray.GetPoint(distance) : rayHit.point;
		pos = RoundManager.Instance.GetNavMeshPosition(pos);
        agent.SetDestination(pos);
    }

    public void SetDirectionAsDestination(Vector3 direction, float distance = 4f, float rayHeight = 1.5f)
    {
		Ray ray = new(transform.position + Vector3.up * rayHeight, direction);
		Vector3 pos = (!Physics.Raycast(ray, out RaycastHit rayHit, distance, StartOfRound.Instance.collidersAndRoomMask)) ? ray.GetPoint(distance) : rayHit.point;
		pos = RoundManager.Instance.GetNavMeshPosition(pos);
        agent.SetDestination(pos);
    }

    public void SlowlyStopMoving(float time)
    {
        if (changeAgentSpeedCoroutine != null)
        {
            StopCoroutine(changeAgentSpeedCoroutine);
            changeAgentSpeedCoroutine = null;
        }
        changeAgentSpeedCoroutine = StartCoroutine(ChangeAgentSpeed(agent.speed, 0f, time));
    }

	public void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesNoisePlayedInOneSpot = 0, int noiseID = 0)
	{
		if (noiseID == 7 || noiseID == 546 || dogHearNoiseCooldown >= 0f || timesNoisePlayedInOneSpot > 15)
		{
			return;
		}
		dogHearNoiseCooldown = 0.05f;
		float num = Vector3.Distance(base.transform.position, noisePosition);
		float num2 = 10f * noiseLoudness;
		if (Physics.Linecast(base.transform.position, noisePosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
		{
			noiseLoudness /= 2f;
			num2 /= 2f;
		}
		if (noiseLoudness < 0.25f)
		{
			return;
		}
		if (num < num2)
		{
			dogSuspicionLevel = 9;
		}
		else
		{
			dogSuspicionLevel++;
		}
		bool fullyEnrage = false;
		if (dogSuspicionLevel >= 9)
		{
            fullyEnrage = true;
		}
        dogHeardNoisePosition = noisePosition;
        dogEnraged = fullyEnrage;
        dogAlerted = true;
        HearNoiseServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, noisePosition, fullyEnrage, true);
	}

    [ServerRpc(RequireOwnership = false)]
    private void HearNoiseServerRpc(int clientWhoSentRpc, Vector3 noisePosition, bool enraged, bool alerted)
    {
        HearNoiseClientRpc(clientWhoSentRpc, noisePosition, enraged, alerted);
    }

    [ClientRpc]
    private void HearNoiseClientRpc(int clientWhoSentRpc, Vector3 noisePosition, bool enraged, bool alerted)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            dogHeardNoisePosition = noisePosition;
            dogEnraged = enraged;
            dogAlerted = true;
        }
    }

	private void DoFootstepCycle()
	{
		if (chargingForward && !disableWalking && !aimingGun)
		{
			if (beginChargingTimer < 0.6f)
			{
				agent.speed = 4f;
				return;
			}
			agent.speed = chargeForwardSpeed;
			if (startedChargeEffect)
			{
				return;
			}
			startedChargeEffect = true;
			float num = Vector3.Distance(base.transform.position - base.transform.forward * 5f, GameNetworkManager.Instance.localPlayerController.transform.position);
			if (num < 25f)
			{
				HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
			}
		}
		if (takingStep)
		{
			if (walkStepTimer <= 0f)
			{
				walkStepTimer = timeBetweenSteps;
				takingStep = false;
				agent.speed = 0f;
			}
			else
			{
				agent.speed = stepMovementSpeed;
				walkStepTimer -= Time.deltaTime;
			}
		}
		else if (!disableWalking && !aimingGun)
		{
			if (walkStepTimer <= 0f)
			{
				walkStepTimer = timeToTakeStep;
				leftFoot = !leftFoot;
				takingStep = true;
				TakeStepForwardAnimation(leftFoot);
			}
			else
			{
				walkStepTimer -= Time.deltaTime;
			}
		}
	}

	private void TakeStepForwardAnimation(bool leftFootForward)
	{
        hallucinationAnimator.SetBool("leftFootForward", leftFootForward);
	}

	public void EnableSpotlight(bool audio = true)
	{
		spotlight.SetActive(value: true);
		ghostParts[0].GetComponent<SkinnedMeshRenderer>().sharedMaterial = spotlightMat;
        if (audio)
        {
            ghostAudioSources[8].Play();
        }
	}

	public void DisableSpotlight(bool audio = true)
	{
		spotlight.SetActive(value: false);
		ghostParts[0].GetComponent<SkinnedMeshRenderer>().sharedMaterial = defaultMat;
        if (audio)
        {
		    ghostAudioSources[5].PlayOneShot(spotlightOff);
        }
	}

	public void StompLeftFoot()
	{
		Stomp(leftFootPoint, leftFootParticle);
	}

	public void StompRightFoot()
	{
		Stomp(rightFootPoint, rightFootParticle);
	}

	public void StompBothFeet()
	{
		Stomp(base.transform, leftFootParticle, rightFootParticle);
	}

	private void Stomp(Transform stompTransform, ParticleSystem particle, ParticleSystem? particle2 = null)
	{
        if (!mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            return;
        }
		PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
		float num = Vector3.Distance(localPlayerController.transform.position, stompTransform.position);
		RoundManager.PlayRandomClip(ghostAudioSources[5], enemyType.audioClips, randomize: true, 1f, 1115, 4);
        if (!fading)
        {
            particle.Play();
            particle2?.Play();
            if (num < 12f)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }
            else if (num < 24f)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            }
        }
	}

	public void SetChargingForwardOnLocalClient(bool charging)
	{
        if (!mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            return;
        }
		if (charging != chargingForward)
		{
			hallucinationAnimator.SetBool("charging", charging);
			startChargingEffectContainer.SetActive(charging);
			beginChargingTimer = 0f;
			startedChargeEffect = false;
			if (charging)
			{
				StompBothFeet();
				chargeParticle.Play();
				agent.angularSpeed = 120f;
				agent.acceleration = 25f;
                ghostAudioSources[6].Stop();
                ghostAudioSources[1].Play();
			}
			else
			{
				agent.speed = 0f;
				chargeParticle.Stop();
				agent.angularSpeed = 220f;
				agent.acceleration = 60f;
                ghostAudioSources[1].Stop();
                ghostAudioSources[6].Play();
			}
			chargingForward = charging;
		}
	}

	public void SetAimingGun(bool setAiming)
	{
        if (setAiming)
        {
            shootCooldown = 0f;
        }
        aimingGun = setAiming;
        SetChargingForwardOnLocalClient(charging: false);
        if (!aimingGun)
        {
            hallucinationAnimator.SetBool("AimGun", value: false);
        }
	}

	public void AimAndShootCycle()
	{
		if (takingStep)
		{
			return;
		}
		hallucinationAnimator.SetBool("AimGun", value: true);
		if (shootTimer > fireRate)
		{
			if (shootCooldown > 4.75f || chargingForward)
			{
				SetAimingGun(setAiming: false);
				return;
			}
			shootTimer = 0f + Random.Range((0f - fireRateVariance) * 0.5f, fireRateVariance * 0.5f);
			ShootGun(gunPoint.position, gunPoint.rotation.eulerAngles);
		}
		else
		{
			shootTimer += Time.deltaTime;
		}
	}

	public void TurnTorsoToTarget()
	{
		if (doTurnTorso && Vector3.Distance(base.transform.position, lastSeenPosition) > 3f)
		{
			RoundManager.Instance.tempTransform.position = torsoContainer.position + Vector3.up * 1f;
			RoundManager.Instance.tempTransform.LookAt(lastSeenPosition);
			RoundManager.Instance.tempTransform.rotation *= Quaternion.Euler(0f, 90f, 0f);
			float num = 0f;
			if (aimingGun)
			{
				num = Vector3.Angle(lastSeenPosition - base.transform.position, base.transform.forward) * forwardAngleCompensation;
			}
			RoundManager.Instance.tempTransform.localEulerAngles = new Vector3(torsoContainer.eulerAngles.x, torsoContainer.eulerAngles.y, RoundManager.Instance.tempTransform.localEulerAngles.z + num);
			torsoContainer.rotation = Quaternion.RotateTowards(torsoContainer.rotation, RoundManager.Instance.tempTransform.rotation, 80f * Time.deltaTime);
		}
		else
		{
			torsoContainer.rotation = Quaternion.Lerp(torsoContainer.rotation, torsoDefaultRotation.rotation, 3f * Time.deltaTime);
		}
	}

	public void AimGunArmTowardsTarget()
	{
		if (!aimingGun || inSpecialAnimation)
		{
			gunArm.rotation = Quaternion.Lerp(gunArm.rotation, defaultArmRotation.rotation, 3f * Time.deltaTime);
			return;
		}
		RoundManager.Instance.tempTransform.position = gunArm.position;
		RoundManager.Instance.tempTransform.LookAt(lastSeenPosition);
		RoundManager.Instance.tempTransform.rotation *= Quaternion.Euler(90f, 0f, 0f);
		RoundManager.Instance.tempTransform.localEulerAngles = new Vector3(gunArm.eulerAngles.x, RoundManager.Instance.tempTransform.localEulerAngles.y, RoundManager.Instance.tempTransform.localEulerAngles.z);
		gunArm.rotation = Quaternion.RotateTowards(gunArm.rotation, RoundManager.Instance.tempTransform.rotation, gunArmSpeed * Time.deltaTime);
		gunArm.localEulerAngles = new Vector3(0f, 0f, gunArm.localEulerAngles.z);
	}

	public void ShootGun(Vector3 startPos, Vector3 startRot)
	{
		if (hallucinationAnimator.GetBool("AimGun"))
		{
			hallucinationAnimator.SetTrigger("ShootGun");
		}
		currentMissileSpeed = 0.2f;
		GameObject obj = Instantiate(missilePrefab, startPos, Quaternion.Euler(startRot), RoundManager.Instance.mapPropsContainer.transform);
		missilesFired++;
		obj.GetComponent<ScarecrowHallucinationMissile>().ghostScript = this;
		gunArmParticle.Play();
		RoundManager.PlayRandomClip(ghostAudioSources[5], shootGunSFX);
		if (Vector3.Distance(base.transform.position, GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position) < 16f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
		}
	}

	public void StartExplosion(Vector3 explosionPosition, Vector3 forwardRotation)
	{
        SetExplosion(explosionPosition, forwardRotation);
	}

	public void SetExplosion(Vector3 explosionPosition, Vector3 forwardRotation)
	{
        if (!mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            return;
        }
        Landmine.SpawnExplosion(explosionPosition - forwardRotation * 0.1f, spawnExplosionEffect: true, 0f, 0f, 0, 0f, explosionPrefab);
        AudioSource explosionSource = Instantiate(ghostAudioSources[2], explosionPosition + Vector3.up * 0.5f, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
        explosionSources.Add(explosionSource);
		RoundManager.PlayRandomClip(explosionSource, largeExplosionSFX);
	}

    public override void OnDestroy()
    {
        if (explosionSources.Count > 0)
        {
            for (int i = 0; i < explosionSources.Count; i++)
            {
                Destroy(explosionSources[i].gameObject);
                explosionSources.Remove(explosionSources[i]);
            }
        }
    }

	public void GetMaterialStandingOn()
	{
		enemyRay = new Ray(base.transform.position + Vector3.up, -Vector3.up);
		if (Physics.Raycast(enemyRay, out enemyRayHit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore))
		{
			if (enemyRayHit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].surfaceTag))
			{
				return;
			}
			for (int i = 0; i < StartOfRound.Instance.footstepSurfaces.Length; i++)
			{
				if (enemyRayHit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[i].surfaceTag))
				{
					currentFootstepSurfaceIndex = i;
					break;
				}
			}
		}
		else
		{
			Debug.DrawRay(enemyRay.origin, enemyRay.direction, Color.white, 0.3f);
		}
	}

	public void PlayFootstepSound()
	{
		GetMaterialStandingOn();
		int num = UnityEngine.Random.Range(0, StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].clips.Length);
		if (num == previousFootstepClip)
		{
			num = (num + 1) % StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].clips.Length;
		}
		movementAudio.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
		float num2 = maskedSprinting ? 0.95f : 0.8f;
		movementAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].clips[num], num2);
		previousFootstepClip = num;
		WalkieTalkie.TransmitOneShotAudio(movementAudio, StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].clips[num], num2);
	}

    private IEnumerator DogReactToNoise(bool right = true)
    {
        dogSuspicionLevel = 4;
        StartCoroutine(FadeAudioSources(Random.Range(0.65f, 0.85f), true));
        StartCoroutine(ChangeAgentSpeed(0f, walkSpeed, 0.2f));
        SetDirectionAsDestination((right ? transform.right : -transform.right) * 0.3f + transform.forward, 7f, 3f);
        hallucinationAnimator.Play("Idle1");
        yield return new WaitForSeconds(Random.Range(0.7f, 1.05f));

        agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        float timeElapsed = 0f;
        float duration = 5f;
        while (timeElapsed < duration && !dogAlerted)
        {
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        if (!dogAlerted)
        {
            SetDirectionAsDestination(-transform.forward + (right ? transform.right : -transform.right), 20f, 2f);
            timeElapsed = 0f;
            duration = 5f;
            while (timeElapsed < duration && !dogAlerted)
            {
                timeElapsed += Time.deltaTime;
                yield return null;
            }
        }
        hallucinationAnimator.SetTrigger("GetSuspicious");
        audioEvent.PlayListAudio(0);
        if (!fading && mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.4f);
        }
        agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        timeElapsed = 0f;
        duration = 5.5f;
        while (timeElapsed < duration && !dogEnraged)
        {
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        if (!fading && dogEnraged)
        {
            audioEvent.PlayListAudio(1);
            hallucinationAnimator.SetTrigger("ChaseHowl");
            if (mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
            {
                if (Vector3.Distance(transform.position, GameNetworkManager.Instance.localPlayerController.transform.position) < 16f)
                {
                    HUDManager.Instance.ShakeCamera(ScreenShakeType.Long);
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.5f);
                }
            }
            yield return new WaitForSeconds(0.5f);
            hallucinationAnimator.SetBool("StartedChase", value: true);
            StartCoroutine(ChangeAgentSpeed(0f, runSpeed, 0.6f));
            agent.SetDestination(dogHeardNoisePosition);
            while (Vector3.Distance(transform.position, dogHeardNoisePosition) > 4f)
            {
                yield return null;
            }
            audioEvent.PlayListAudio(2);
            hallucinationAnimator.SetTrigger("Lunge");
            KeepMovingInDirection(25f);
            SlowlyStopMoving(2.5f);
        }
        else
        {
            SetDirectionAsDestination(-transform.forward + -transform.right, 20f, 2f);
        }
    }

    private IEnumerator DogRunAndLunge()
    {
        hallucinationAnimator.Play("ChaseHowl 0");
        StartCoroutine(FadeAudioSources(Random.Range(0.4f, 0.7f), true));
        StartCoroutine(ChangeAgentSpeed(0f, runSpeed, 0.2f));
        agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        while (Vector3.Distance(transform.position, agent.destination) > 2f)
        {
            yield return null;
        }
        audioEvent.PlayListAudio(2);
        hallucinationAnimator.SetTrigger("Lunge");
        KeepMovingInDirection(25f);
        SlowlyStopMoving(2.5f);
    }

    private IEnumerator GiantStare()
    {
        giantDanger = false;
        StartCoroutine(FadeAudioSources(Random.Range(0.6f, 1.2f), true));
        StartCoroutine(FaceTowardsTransform(closestPlayer.transform, 15f));
        hallucinationAnimator.SetBool("stare", true);
        hallucinationAnimator.SetBool("Chasing", false);
        yield return new WaitForSeconds(0.4f);
        StartCoroutine(ChangeAgentSpeed(0f, walkSpeed, 0.2f));
        switch (Random.Range(0, 3))
        {
            case 0:
                SetDirectionAsDestination(-transform.right + -transform.forward, 3f, 6f);
                yield return new WaitForSeconds(0.35f);
                SetDirectionAsDestination(transform.forward, 4.5f, 6f);
                break;
            case 1:
                SetDirectionAsDestination(transform.right + -transform.forward, 1.5f, 6f);
                yield return new WaitForSeconds(0.25f);
                SetDirectionAsDestination(transform.forward + -transform.right, 2f, 6f);
                break;
            case 2:
                SetDirectionAsDestination(transform.right + transform.forward, 2.5f, 6f);
                yield return new WaitForSeconds(0.45f);
                SetDirectionAsDestination(-transform.forward + -transform.right, 1.5f, 6f);
                break;
        }
    }

    private IEnumerator GiantWander()
    {
        giantDanger = false;
        StartCoroutine(FadeAudioSources(Random.Range(0.6f, 1.2f), true));
        yield return new WaitForSeconds(0.4f);
        StartCoroutine(ChangeAgentSpeed(0f, walkSpeed, 0.2f));
        switch (Random.Range(0, 3))
        {
            case 0:
                SetDirectionAsDestination(-transform.right + -transform.forward, 5.5f, 6f);
                yield return new WaitForSeconds(0.9f);
                SetDirectionAsDestination(-transform.forward, 12f, 6f);
                yield return new WaitForSeconds(3.3f);
                SetDirectionAsDestination(transform.right + -transform.forward, 13f, 6f);
                yield return new WaitForSeconds(4f);
                SetDirectionAsDestination(transform.right + transform.forward, 7f, 6f);
                break;
            case 1:
                SetDirectionAsDestination(transform.right + -transform.forward, 11f, 6f);
                yield return new WaitForSeconds(3.9f);
                SetDirectionAsDestination(transform.forward + -transform.right, 5f, 6f);
                yield return new WaitForSeconds(4f);
                SetDirectionAsDestination(-transform.right + -transform.forward, 11f, 6f);
                break;
            case 2:
                SetDirectionAsDestination(transform.right + transform.forward, 3f, 6f);
                yield return new WaitForSeconds(2.5f);
                SetDirectionAsDestination(-transform.forward + -transform.right, 12f, 6f);
                yield return new WaitForSeconds(4.6f);
                SetDirectionAsDestination(-transform.right + transform.forward, 15f, 6f);
                break;
        }
    }

    private IEnumerator GiantRun()
    {
        giantDanger = true;
        StartCoroutine(FadeAudioSources(Random.Range(0.6f, 1.2f), true));
        yield return new WaitForSeconds(0.25f);
        hallucinationAnimator.SetBool("Chasing", true);
        StartCoroutine(FaceTowardsTransform(closestPlayer.transform, 0.3f));
        StartCoroutine(ChangeAgentSpeed(0f, walkSpeed, 0.1f));
        SetDirectionAsDestination(transform.right + -transform.forward * 0.5f, 3.5f, 6f);
        yield return new WaitForSeconds(0.1f);
        SetDirectionAsDestination(-transform.right * 0.5f + transform.forward, 4f, 6f);
        StartCoroutine(ChangeAgentSpeed(0f, runSpeed + 1, 1f));
        yield return new WaitForSeconds(0.2f);
        float chasePlayerTimer = 5.7f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 3f)
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            base.transform.LookAt(closestPlayer.transform.position);
            base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
            yield return null;
        }
        SetDirectionAsDestination(transform.forward, 40f, 6f);
    }

    private IEnumerator GiantStareAndRun(bool delayed = false)
    {
        giantDanger = false;
        StartCoroutine(FadeAudioSources(Random.Range(0.6f, 1.2f), true));
        if (delayed)
        {
            SetDirectionAsDestination(transform.right * 2f + -transform.forward * 0.5f, 6f, 6f);
            yield return new WaitForSeconds(2.2f);
        }
        else
        {
            yield return new WaitForSeconds(0.35f);
        }
        hallucinationAnimator.SetBool("stare", true);
        hallucinationAnimator.SetBool("Chasing", true);
        StartCoroutine(FaceTowardsTransform(closestPlayer.transform, 1.35f));
        StartCoroutine(ChangeAgentSpeed(0f, walkSpeed, 0.1f));
        SetDirectionAsDestination(transform.right + -transform.forward * 0.5f, 3.5f, 6f);
        yield return new WaitForSeconds(0.2f);
        SetDirectionAsDestination(-transform.right * 0.5f + transform.forward, 2f, 6f);
        StartCoroutine(ChangeAgentSpeed(walkSpeed, runSpeed, 1f));
        yield return new WaitForSeconds(delayed ? Random.Range(1.45f, 2.15f) : Random.Range(0.85f, 1.65f));
        if (!fading && mainScarecrow.playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.9f);
        }
        StartCoroutine(ChangeAgentSpeed(runSpeed, runSpeed + 1, 2f));
        giantDanger = true;
        float chasePlayerTimer = 5.7f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 3f)
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            base.transform.LookAt(closestPlayer.transform.position);
            base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
            yield return null;
        }
        SetDirectionAsDestination(transform.forward, 40f, 6f);
    }

    private IEnumerator MechInvestigate()
    {
        doTurnTorso = false;
        timeBetweenSteps = 1.1f;
        StartCoroutine(FadeAudioSources(0.6f, true));
        yield return new WaitForSeconds(1.85f);
        EnableSpotlight();
        SetDirectionAsDestination(transform.forward, 8f, 6f);
        yield return new WaitForSeconds(1.6f);
        timeBetweenSteps = 0.7f;
        doTurnTorso = true;
        float chasePlayerTimer = 12f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 3f)
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            yield return null;
        }
        disableWalking = true;
    }

    private IEnumerator MechAggro(bool delayStart = false)
    {
        doTurnTorso = false;
        timeBetweenSteps = 1.1f;
        StartCoroutine(FadeAudioSources(0.4f, true));
        yield return new WaitForSeconds(!delayStart ? 1.2f : 2.3f);
        EnableSpotlight();
        agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        yield return new WaitForSeconds(2.4f);
        if (!fading)
        {
            if (base.IsServer)
            {
                int num = Random.Range(4, enemyType.audioClips.Length);
                ghostAudioSources[4].clip = enemyType.audioClips[num];
                ghostAudioSources[4].Play();
                ChangeBroadcastClipClientRpc(num);
            }
            timeBetweenSteps = 0.2f;
        }
        doTurnTorso = true;
        float chasePlayerTimer = 12f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 3f)
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            yield return null;
        }
        KeepMovingInDirection(12f);
    }

    private IEnumerator MechCharge()
    {
        doTurnTorso = false;
        StartCoroutine(FadeAudioSources(0.3f, true));
        timeBetweenSteps = 1.1f;
        yield return new WaitForSeconds(0.3f);
        SetChargingForwardOnLocalClient(true);
        if (base.IsServer)
        {
            int num = Random.Range(4, enemyType.audioClips.Length);
            ghostAudioSources[4].clip = enemyType.audioClips[num];
            ghostAudioSources[4].Play();
            ChangeBroadcastClipClientRpc(num);
        }
        EnableSpotlight(audio: false);
        agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        float chasePlayerTimer = 5f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 16f && !fading)
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            yield return null;
        }
        Vector3 direction = Vector3.Normalize(closestPlayer.transform.position - base.transform.position);
        SetDirectionAsDestination(direction, 100f, 10f);
    }

    private IEnumerator MechRockets()
    {
        if (!Physics.Linecast(transform.position + Vector3.up * 6f, closestPlayer.transform.position + Vector3.up * 2f, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
        {
            doTurnTorso = true;
            SetAimingGun(setAiming: true);
            hallucinationAnimator.SetBool("GrabUnsuccessful", value: true);
            hallucinationAnimator.SetBool("AttemptingGrab", value: false);
            StartCoroutine(FadeAudioSources(0.8f, true));
            timeBetweenSteps = 0.2f;
            while (shootCooldown < 4.75f && !fading)
            {
                yield return null;
            }
            EnableSpotlight(audio: false);
            yield return new WaitForSeconds(0.65f);
            hallucinationAnimator.Play("LeftFootForward");
            hallucinationAnimator.SetBool("leftFootForward", false);
            SetAimingGun(setAiming: false);
            agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
        }
        else
        {
            StartCoroutine(MechCharge());
        }
    }

	[ClientRpc]
	public void ChangeBroadcastClipClientRpc(int clipIndex)
    {
        if (!base.IsServer)
        {
            ghostAudioSources[4].clip = enemyType.audioClips[clipIndex];
            ghostAudioSources[4].Play();
        }
    }

    private IEnumerator MaskedChase(bool stopSprinting = true)
    {
        maskedAllowSprinting = true;
        maskedAllowStopSprinting = stopSprinting;
        hallucinationAnimator.SetBool("IsMoving", true);
        StartCoroutine(MaskedLerpAnimatorVelocity(0.15f));
        StartCoroutine(FadeAudioSources(0.6f, fadeIn: true));
        float chasePlayerTimer = 6f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 2f)
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            base.transform.LookAt(closestPlayer.transform.position);
            base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
            yield return null;
        }
        KeepMovingInDirection(30f);
    }

    private IEnumerator MaskedFollow(bool forceLaugh = false)
    {
        maskedAllowSprinting = false;
        hallucinationAnimator.SetBool("IsMoving", true);
        StartCoroutine(MaskedLerpAnimatorVelocity(0.15f));
        if (forceLaugh)
        {
            maskedAudioPlayer.thisAudio.volume = 1f;
            maskedAudioPlayer.audioChancePercent = 0f;
            if (base.IsServer)
            {
                maskedAudioPlayer.PlayRandomAudioClientRpc(Random.Range(0, maskedAudioPlayer.randomClips.Length));
            }
        }
        else
        {
            maskedAudioPlayer.thisAudio.volume = 0f;
        }
        StartCoroutine(FadeAudioSources(0.6f, fadeIn: true));
        float chasePlayerTimer = 6f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 2f)
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            base.transform.LookAt(closestPlayer.transform.position);
            base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
            yield return null;
        }
        KeepMovingInDirection(30f);
    }

    private IEnumerator MaskedFollowAndStare()
    {
        maskedAudioPlayer.thisAudio.volume = 0f;
        maskedAudioPlayer.audioChancePercent = 0f;
        maskedLaughWhenStopped = Random.Range(0, 2) == 1;
        maskedContinueWhenStopped = Random.Range(0, 2) == 1;
        if (base.IsServer)
        {
            SyncMaskedBoolsClientRpc(maskedLaughWhenStopped, maskedContinueWhenStopped);
        }
        maskedAllowSprinting = false;
        hallucinationAnimator.SetBool("IsMoving", true);
        StartCoroutine(FadeAudioSources(0.6f, fadeIn: true));
        StartCoroutine(MaskedLerpAnimatorVelocity(0.15f));
        float chasePlayerTimer = 2.2f;
        float timeElapsed = 0f;
        float updateInterval = 0.2f;
        float updateTimeElapsed = 0f;
        while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > Random.Range(2f, 6f))
        {
            timeElapsed += Time.deltaTime;
            updateTimeElapsed += Time.deltaTime;
            if (updateTimeElapsed > updateInterval)
            {
                updateTimeElapsed = 0f;
                agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
            }
            base.transform.LookAt(closestPlayer.transform.position);
            base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
            yield return null;
        }
        if (maskedLaughWhenStopped)
        {
            maskedAudioPlayer.thisAudio.volume = 1f;
            if (base.IsServer)
            {
                maskedAudioPlayer.PlayAudio(Random.Range(0, maskedAudioPlayer.randomClips.Length));
            }
        }
        if (!maskedContinueWhenStopped)
        {
            StartFaceTowardsPlayer(12f);
            KeepMovingInDirection(0.01f);
            hallucinationAnimator.SetBool("IsMoving", false);
            StartCoroutine(MaskedLerpAnimatorVelocity(0.15f, stop: true));
        }
        else
        {
            chasePlayerTimer = 12f;
            timeElapsed = 0f;
            updateInterval = 0.2f;
            updateTimeElapsed = 0f;
            while (timeElapsed < chasePlayerTimer && Vector3.Distance(base.transform.position, closestPlayer.transform.position) > 2f)
            {
                timeElapsed += Time.deltaTime;
                updateTimeElapsed += Time.deltaTime;
                if (updateTimeElapsed > updateInterval)
                {
                    updateTimeElapsed = 0f;
                    agent.SetDestination(RoundManager.Instance.GetNavMeshPosition(closestPlayer.transform.position));
                }
                base.transform.LookAt(closestPlayer.transform.position);
                base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
                yield return null;
            }
            KeepMovingInDirection(30f);
        }
    }

    [ClientRpc]
    private void SyncMaskedBoolsClientRpc(bool set1, bool set2)
    {
        if (!base.IsServer)
        {
            maskedLaughWhenStopped = set1;
            maskedContinueWhenStopped = set2;
        }
    }

    private IEnumerator MaskedLerpAnimatorVelocity(float duration, bool stop = false)
    {
        float timeElapsed = 0f;
        float endValue = stop ? 0f : 1f;
        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            hallucinationAnimator.SetFloat("VelocityZ", Mathf.Lerp(hallucinationAnimator.GetFloat("VelocityZ"), endValue, timeElapsed / duration));
            yield return null;
        }
    }

    private IEnumerator FadeAudioSources(float time, bool fadeIn = true)
    {
        float timeElapsed = 0;
        while (timeElapsed < time)
        {
            timeElapsed += Time.deltaTime;
            foreach (AudioSource source in ghostAudioSources)
            {
                source.volume = Mathf.Lerp(fadeIn ? 0f : source.volume, fadeIn ? 1f : 0f, timeElapsed / time);
            }
            yield return null;
        }
    }

    private IEnumerator FaceTowardsTransform(Transform transform, float time)
    {
        float timer = 0f;
        while (timer < time)
        {
            timer += Time.deltaTime;
            base.transform.LookAt(transform.position);
            base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
            yield return null;
        }
    }

    private IEnumerator ChangeAgentSpeed(float startSpeed, float endSpeed, float time)
    {
        float timer = 0f;
        float speed;
        while (timer < time)
        {
            timer += Time.deltaTime;
            speed = Mathf.Lerp(startSpeed, endSpeed, timer/time);
            agent.speed = speed;
            yield return null;
        }
    }
}