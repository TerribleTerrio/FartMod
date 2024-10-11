using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

public class Toaster : AnimatedItem, IHittable, ITouchable
{

    [Header("Toaster Settings")]
    public float ejectTimeMin;

    public float ejectTimeMax;

    public float popRange;

    public bool damagePlayersOnPop = true;

    public int playerDamage = 1;

    public bool physicsForceOnPop = true;

    public float physicsForce = 1;

    public float physicsForceUp = 1;

    [Space(5f)]
    public AudioClip[] insertSFX;

    public AudioClip[] ejectSFX;

    public AudioClip[] hitSFX;

    public void Insert()
    {
        itemAnimator.Play("insert");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 2, noiseLoudness/1.5f, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, insertSFX, randomize: true, 1f, -1);

        float ejectTime = UnityEngine.Random.Range(ejectTimeMin, ejectTimeMax);
        StartCoroutine(WaitToEject(ejectTime));
    }

    public IEnumerator WaitToEject(float delay)
    {
        yield return new WaitForSeconds(delay);
        Eject();
    }

    public void Eject()
    {
        itemAnimator.Play("eject");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, insertSFX, randomize: true, 1f, -1);

        Collider[] colliders = Physics.OverlapSphere(base.transform.position, popRange, 603987972, QueryTriggerInteraction.Collide);
        for (int i = 0; i < colliders.Length; i++)
        {
            GameObject otherObject = colliders[i].gameObject;

            //PLAYERS
            if (otherObject.layer == 3)
            {
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

                if (damagePlayersOnPop)
                {
                    player.DamagePlayer(playerDamage);
                }

                if (physicsForceOnPop)
                {
                    RaycastHit hitInfo;
                    if (physicsForce > 0f && !Physics.Linecast(base.transform.position, player.transform.position, out hitInfo, 256, QueryTriggerInteraction.Ignore))
                    {
                        float dist = Vector3.Distance(player.transform.position, base.transform.position);
                        Vector3 vector = Vector3.Normalize(player.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
                        if (player.isGroundedOnServer)
                        {
                            vector += Vector3.up * physicsForceUp;
                        }
                        if (vector.magnitude > 2f)
                        {
                            if (vector.magnitude > 10f)
                            {
                                player.CancelSpecialTriggerAnimations();
                            }
                            if (!player.inVehicleAnimation || (player.externalForceAutoFade + vector).magnitude > 50f)
                            {
                                    player.externalForceAutoFade += vector;
                            }
                        }
                    }
                }
            }

            //ENEMIES
            else if (otherObject.layer == 19)
            {

            }

            //ITEMS
            else if (otherObject.layer == 6)
            {

            }

            //VEHICLES
            else if (otherObject.layer == 30)
            {

            }
        }
    }

    public void OnTouch(Collider other)
    {

    }

    public void OnExit(Collider other)
    {

    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, hitSFX, randomize: true, 1f, -1);
        return true;
	}

}