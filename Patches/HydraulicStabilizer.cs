using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

public class HydraulicStabilizer : AnimatedItem, IHittable, ITouchable
{
    [Header("Hydraulic Stabilizer Settings")]
    public float audibleNoiseCooldown = 2f;

    private bool noiseOnCooldown;

	public AnimationClip hydraulicOn;

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

    public override void GrabItem()
    {
        base.GrabItem();
		if (itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("HydraulicIdle"))
		{
			StartCoroutine(DelayAnimation("SteamOn", hydraulicOn.length));
		}
    }

	private IEnumerator DelayAnimation(string state, float delay)
	{
		yield return new WaitForSeconds(delay);
		itemAnimator.Play(state);
	}

    public override void DiscardItem()
    {
        base.DiscardItem();
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

		itemAnimator.Play("HydraulicPsycho", -1, 0f);

		if (itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("SteamIdleOn"))
		{
			return;
		}
		else if (itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("SteamOff"))
		{
			itemAnimator.Play("SteamIdleOn");
		}
		else
		{
			itemAnimator.Play("SteamOn");
		}
    }

	public void OnTouch(Collider other)
	{
    }

	public void OnExit(Collider other)
	{
	}
}