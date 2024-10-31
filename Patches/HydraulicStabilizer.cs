using System.Collections;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Events;

public class HydraulicStabilizer : AnimatedItem, IHittable, ITouchable, ZappableObject
{
    [Header("Hydraulic Stabilizer Settings")]
    public float audibleNoiseCooldown = 2f;

	public bool physicsForceOnSteam;

    public float physicsForce;

    public float physicsForceRange;
	
	public UnityEvent onAnimationEventCalled;

	public AudioSource audioSource1;

	public AudioSource audioSource1Far;

	public AudioSource audioSource2;

	public AudioSource audioSource2Far;

	[Header("On Clips")]
	public AudioClip HydraulicOnClip;
	public AudioClip HydraulicOnClipFar;

	[Header("Steaming Clips")]
	public AudioClip HydraulicSteamingClip;
	public AudioClip HydraulicSteamingClipFar;

	[Header("Loop Clips")]
	public AudioClip HydraulicLoopClip;
	public AudioClip HydraulicLoopClipFar;

	[Header("Off Clips")]
	public AudioClip HydraulicOffClip;
	public AudioClip HydraulicOffClipFar;

	[Header("Psycho Clips")]
	public AudioClip[] HydraulicPsychoClips;
	public AudioClip[] HydraulicPsychoClipsFar;

    private bool noiseOnCooldown;

    private bool startedSteaming;

	private bool ZapPsycho;

	private int PsychoStage;

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
        GoPsycho();
        return true;
    }

    public void GoPsycho()
    {
		if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
		{
			timesPlayedInOneSpot = 0;
		}
		timesPlayedInOneSpot++;
		lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange*1.5f, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

		if (PsychoStage == 0)
        {
			itemAnimator.Play("HydraulicPsycho", -1, 0f);
			PsychoStage++;
		}
		else if (PsychoStage == 1)
		{
			itemAnimator.Play("HydraulicPsycho2", -1, 0f);
			PsychoStage++;
		}
		else if (PsychoStage == 2)
		{
			itemAnimator.Play("HydraulicPsycho3", -1, 0f);
			PsychoStage++;
		}
		else if (PsychoStage == 3)
		{
			itemAnimator.Play("HydraulicPsycho4", -1, 0f);
			PsychoStage++;
		}
		else if (PsychoStage == 4)
		{
			itemAnimator.Play("HydraulicThunderPsycho", -1, 0f);
			PsychoStage = 0;
		}
		if (ZapPsycho)
		{
			itemAnimator.Play("HydraulicThunderPsycho", -1, 0f);
			ZapPsycho = false;
			PsychoStage = 0;
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
		ZapPsycho = true;
		GoPsycho();
	}

	public void OnAnimationEvent()
	{
		onAnimationEventCalled.Invoke();
	}
}