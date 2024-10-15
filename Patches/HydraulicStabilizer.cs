using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

public class HydraulicStabilizer : AnimatedItem, IHittable
{
    [Header("Hydraulic Stabilizer Settings")]
    public float audibleNoiseCooldown = 2f;

	public bool physicsForceOnSteam;

    public float physicsForce;

    public float physicsForceRange;

    private bool noiseOnCooldown;

    private bool isSteaming;

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

        if (itemAnimator.GetCurrentAnimatorStateInfo(1).IsName("SteamOn") && !isSteaming)
        {
            isSteaming = true;
            if (physicsForceOnSteam)
            {
                PushNearbyPlayers();
            }
        }

        if (itemAnimator.GetCurrentAnimatorStateInfo(1).IsName("SteamOffIdle") && isSteaming)
        {
            isSteaming = false;
        }
    }

    private void PushNearbyPlayers()
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
        PushNearbyPlayers();
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

		itemAnimator.Play("HydraulicPsycho", -1, 0f);

		itemAnimator.SetBool("steaming", true);
    }
}