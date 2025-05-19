using System.Collections;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class HydraulicStabilizer : AnimatedItem, IHittable, ITouchable, ZappableObject
{
    [Header("Hydraulic Stabilizer Settings")]
    public float audibleNoiseCooldown = 2f;

    public float physicsForce;

    public float physicsForceRange;
	
	public UnityEvent onAnimationEventCalled;

    private bool noiseOnCooldown;

	private int psychoStage;

    private bool disableCanvasFlag;

    private Canvas? _elevatorPanelScreen;

    public Canvas ElevatorPanelScreen
    {
        get
        {
            _elevatorPanelScreen ??= FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(canvas => canvas.gameObject.name.Contains("ElevatorPanelScreen"));
            return _elevatorPanelScreen;
        }
    }

    public override void Start()
    {
        base.Start();
		psychoStage = 1;
    }

    public override void Update()
    {
        base.Update();
        if (itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hydraulic Idle"))
        {
            return;
        }
        if (itemAnimator.GetBool("steaming"))
        {
            if (!noiseOnCooldown)
            {
                StartCoroutine(LoopNoiseOnCooldown(audibleNoiseCooldown));
            }
            if (StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(base.transform.position) && !disableCanvasFlag)
            {
                disableCanvasFlag = true;
                Debug.Log("[HYDRAULIC]: Disabling canvases!");
                StartOfRound.Instance.upperMonitorsCanvas.transform.GetChild(0).gameObject.SetActive(false);
                ElevatorPanelScreen.gameObject.SetActive(false);
            }
        }
        else if (StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(base.transform.position) && disableCanvasFlag)
        {
            disableCanvasFlag = false;
            Debug.Log("[HYDRAULIC]: Enabling canvases!");
            StartOfRound.Instance.upperMonitorsCanvas.transform.GetChild(0).gameObject.SetActive(true);
            ElevatorPanelScreen.gameObject.SetActive(true);
        }
    }

    public void SetAnimatorBoolAndSync(string name, bool state)
    {
        Debug.Log($"Called SetAnimatorBoolAndSync({name}, {state})");
        SetAnimatorBool(name, state);
        SetAnimatorBoolServerRpc(name, state, (int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetAnimatorBoolServerRpc(string name, bool state, int clientWhoSentRpc)
    {
        Debug.Log($"Called SetAnimatorBoolServerRpc({name}, {state})");
        SetAnimatorBoolClientRpc(name, state, clientWhoSentRpc);
    }

    [ClientRpc]
    public void SetAnimatorBoolClientRpc(string name, bool state, int clientWhoSentRpc)
    {
        Debug.Log($"Called SetAnimatorBoolClientRpc({name}, {state})");
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            SetAnimatorBool(name, state);
        }
    }

    public void SetAnimatorBool(string name, bool state)
    {
        Debug.Log($"Called SetAnimatorBool({name}, {state})");
        itemAnimator.SetBool(name, state);
    }

    public override void GrabItem()
    {
        base.GrabItem();
		SetAnimatorBoolAndSync("HoldHydraulic", true);
    }

    public override void DiscardItem()
    {
        base.DiscardItem();
		SetAnimatorBoolAndSync("HoldHydraulic", false);
    }

    public void PushNearbyPlayers()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, physicsForceRange, CoronaMod.Masks.PlayerPropsEnemiesMapHazardsVehicle, QueryTriggerInteraction.Collide);
        RaycastHit hitInfo;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].gameObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB playerControllerB = colliders[i].gameObject.GetComponent<PlayerControllerB>();

                if (physicsForce > 0f && !Physics.Linecast(base.transform.position, playerControllerB.transform.position, out hitInfo, 256, QueryTriggerInteraction.Ignore))
                {
                    float dist = Vector3.Distance(playerControllerB.transform.position, base.transform.position);
                    Vector3 vector = Vector3.Normalize(playerControllerB.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
                    if (vector.magnitude > 2f)
                    {
                        if (vector.magnitude > 10f)
                        {
                            playerControllerB.CancelSpecialTriggerAnimations();
                        }
                        if (!playerControllerB.inVehicleAnimation || (playerControllerB.externalForceAutoFade + vector).magnitude > 50f)
                        {
                                playerControllerB.externalForceAutoFade += vector;
                        }
                    }
                }
            }
        }
    }

    private IEnumerator LoopNoiseOnCooldown(float delay)
    {
        noiseOnCooldown = true;

        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

        yield return new WaitForSeconds(delay);

        noiseOnCooldown = false;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
        GoPsychoAndSync();
        return false;
    }

    public void GoPsychoAndSync(bool zap = false)
    {
        GoPsycho(zap);
        GoPsychoServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, zap);
    }

    [ServerRpc(RequireOwnership = false)]
    public void GoPsychoServerRpc(int clientWhoSentRpc, bool zap = false)
    {
        GoPsychoClientRpc(clientWhoSentRpc, zap);
    }

    [ClientRpc]
    public void GoPsychoClientRpc(int clientWhoSentRpc, bool zap = false)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            GoPsycho(zap);
        }
    }

    public void GoPsycho(bool zap = false)
    {
		if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
		{
			timesPlayedInOneSpot = 0;
		}
		timesPlayedInOneSpot++;
		lastPosition = base.transform.position;

  		RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange*1.5f, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
		if (zap)
		{
			psychoStage = 5;
		}
		if (psychoStage > 5)
		{
			psychoStage = 1;
		}
		itemAnimator.SetInteger("Psycho Stage", psychoStage);
		itemAnimator.Play("Go Psycho");
		psychoStage++;
	}

	public void SetSteamingBoolTrue()
	{
		Debug.Log("Hydraulic: Setting steaming to true");
		itemAnimator.SetBool("steaming", true);
	}

	public void SetSteamingBoolFalse()
	{
		Debug.Log("Hydraulic: Setting steaming to false");
		itemAnimator.SetBool("steaming", false);
	}

	public void OnTouch(Collider other)
	{
        GameObject otherObject = other.gameObject;

        //TIRE COLLISION
        if (otherObject.TryGetComponent<TireReferenceScript>(out var tireReferenceScript))
		{
            float bounceForce = 0f;
			if (tireReferenceScript.mainScript.tireRigidbody.velocity.magnitude >= 3f)
            {
                GoPsychoAndSync();
                bounceForce = 5f;
            }
            tireReferenceScript.mainScript.BounceOff(base.transform.position, extraForce: bounceForce);
            return;
        }
	}

	public void OnExit(Collider other)
	{

	}

	public float GetZapDifficulty()
	{
		return 2;
	}

	public void StopShockingWithGun()
	{

	}

	public void ShockWithGun(PlayerControllerB shockingPlayer)
	{
		GoPsychoAndSync(zap: true);
	}

	public void OnAnimationEvent()
	{
		onAnimationEventCalled.Invoke();
	}
}