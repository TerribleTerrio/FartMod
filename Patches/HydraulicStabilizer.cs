using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements.UIR;

public class HydraulicStabilizer : AnimatedItem, IHittable, ITouchable, ZappableObject
{
    [Header("Hydraulic Stabilizer Settings")]
    public float audibleNoiseCooldown = 2f;

    public float physicsForce;

    public float physicsForceRange;
	
	public UnityEvent onAnimationEventCalled;

    private bool noiseOnCooldown;

	private int psychoStage;

    public override void Start()
    {
        base.Start();
		psychoStage = 0;
    }

    public override void Update()
    {
        base.Update();
        if (itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("HydraulicSteaming") || itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("HydraulicLoop"))
        {
            if (!noiseOnCooldown)
            {
                StartCoroutine(LoopNoiseOnCooldown(audibleNoiseCooldown));
            }
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
        Collider[] colliders = Physics.OverlapSphere(transform.position, physicsForceRange, 1076363336, QueryTriggerInteraction.Collide);
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
        return true;
    }

    public void GoPsychoAndSync()
    {
        GoPsycho();
        GoPsychoServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void GoPsychoServerRpc(int clientWhoSentRpc)
    {
        GoPsychoClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    public void GoPsychoClientRpc(int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            GoPsycho();
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

		itemAnimator.SetInteger("Psycho Stage", psychoStage);
		if (zap)
		{
			psychoStage = 5;
		}
		itemAnimator.Play("Go Psycho");
		psychoStage++;
		if (psychoStage > 5)
		{
			psychoStage = 1;
		}

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

	public void ShockWithGun(PlayerControllerB playerControllerB)
	{
		GoPsycho(zap: true);
	}

	public void OnAnimationEvent()
	{
		onAnimationEventCalled.Invoke();
	}
}