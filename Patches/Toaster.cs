using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

public class Toaster : AnimatedItem, IHittable, ITouchable
{

    [Header("Toaster Settings")]
    public float ejectTimeMin;

    public float ejectTimeMax;

    public float popRange;

    public float damageRange;

    public bool damagePlayersOnPop = true;

    public int playerDamage = 1;

    public bool physicsForceOnPop = true;

    public float physicsForce = 1;

    public bool jumpOnPop = true;

    private bool inserted;

    private Coroutine waitToEject;

    [Space(5f)]
    public AudioSource popSource;

    public AudioClip[] insertSFX;

    public AudioClip[] ejectSFX;

    public AudioClip[] hitSFX;

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (!inserted)
        {
            inserted = true;
            Insert();
        }
    }

    public override void UseUpBatteries()
    {
        base.UseUpBatteries();
        if (inserted)
        {
            inserted = false;
            StopCoroutine(waitToEject);
            Eject();
        }
    }

    public void Insert()
    {
        Debug.Log("Toaster inserted.");
        isBeingUsed = true;
        itemAnimator.Play("insert");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 2, noiseLoudness/1.5f, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(popSource, insertSFX, randomize: true, 1f, -1);

        float ejectTime = UnityEngine.Random.Range(ejectTimeMin, ejectTimeMax);
        Debug.Log($"Eject time set to {ejectTime}s.");
        waitToEject = StartCoroutine(WaitToEject(ejectTime));
    }

    public IEnumerator WaitToEject(float delay)
    {
        Debug.Log("Wait to eject started.");
        yield return new WaitForSeconds(delay);
        if (inserted)
        {
            inserted = false;
            Eject();
        }
    }

    public void Eject()
    {
        Debug.Log("Toaster ejected.");
        isBeingUsed = false;
        itemAnimator.Play("eject");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, ejectSFX, randomize: true, 1f, -1);

        if (base.playerHeldBy != null)
        {
            Debug.Log("Toaster held by player, attempting to deal damage.");
            base.playerHeldBy.DamagePlayer(playerDamage);
        }

        Collider[] colliders = Physics.OverlapSphere(base.transform.position, popRange, 2621448, QueryTriggerInteraction.Collide);

        for (int i = 0; i < colliders.Length; i++)
        {
            GameObject otherObject = colliders[i].gameObject;

            Debug.Log($"Toaster eject found {colliders[i]}.");

            //PLAYERS
            if (otherObject.layer == 3)
            {
                Debug.Log("Toaster found player in pop range.");
                PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

                if (jumpOnPop)
                {
                    if (((player.IsOwner && player.isPlayerControlled && (!player.IsServer || player.isHostPlayerObject)) || player.isTestingPlayer) && !player.inSpecialInteractAnimation && (player.isMovementHindered <= 0 || player.isUnderwater) && (player.thisController.isGrounded || (!player.isJumping && player.IsPlayerNearGround())) && !player.isJumping && (!player.isPlayerSliding || player.playerSlidingTimer > 2.5f) && !player.isCrouching)
                    {
                        player.playerSlidingTimer = 0f;
                        player.isJumping = true;
                        StartOfRound.Instance.PlayerJumpEvent.Invoke(player);
                        player.PlayJumpAudio();
                        if (player.jumpCoroutine != null)
                        {
                            StopCoroutine(player.jumpCoroutine);
                        }
                        player.jumpCoroutine = StartCoroutine(player.PlayerJump());
                        if (StartOfRound.Instance.connectedPlayersAmount!= 0)
                        {
                            player.PlayerJumpedServerRpc();
                        }
                    }
                }

                if (physicsForceOnPop)
                {
                    RaycastHit hitInfo;
                    if (physicsForce > 0f && !Physics.Linecast(base.transform.position, player.transform.position, out hitInfo, 256, QueryTriggerInteraction.Ignore))
                    {
                        float dist = Vector3.Distance(player.transform.position, base.transform.position);
                        Vector3 vector = Vector3.Normalize(player.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
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

                if (damagePlayersOnPop && Vector3.Distance(player.transform.position, base.transform.position) <= damageRange && base.playerHeldBy != player)
                {
                    player.DamagePlayer(playerDamage);
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

        if (inserted)
        {
            inserted = false;
            StopCoroutine(waitToEject);
            Eject();
        }
        else
        {
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 7, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
            RoundManager.PlayRandomClip(popSource, hitSFX, randomize: true, 1f, -1);
        }

        return true;
	}

}