using System.Collections;
using System.Collections.Generic;
using CoronaMod;
using GameNetcodeStuff;
using Unity.Netcode;
using Unity.Netcode.Samples;
using UnityEngine;

public class Toaster : AnimatedItem, IHittable
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

    private List<PlayerControllerB> playersInPopRange;

    [Space(5f)]
    public AudioSource popSource;

    public AudioClip[] insertSFX;

    public AudioClip[] ejectSFX;

    public AudioClip[] hitSFX;

    public override void Start()
    {
        base.Start();
        playersInPopRange = new List<PlayerControllerB>();
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (!inserted)
        {
            inserted = true;
            Insert();
        }
    }

    public void Insert()
    {
        isBeingUsed = true;
        itemAnimator.Play("insert");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 2, noiseLoudness/1.5f, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(popSource, insertSFX, randomize: true, 1f, -1);

        if (base.IsOwner)
        {
            float ejectTime = Random.Range(ejectTimeMin, ejectTimeMax);
            StartCoroutine(WaitToEject(ejectTime));
        }
    }

    public IEnumerator WaitToEject(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (inserted)
        {
            Eject();
            EjectServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        }
    }

    [ServerRpc]
    public void EjectServerRpc(int clientWhoSentRpc)
    {
        EjectClientRpc(clientWhoSentRpc);
    }

    [ClientRpc]
    public void EjectClientRpc(int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            Eject();
        }
    }

    public void Eject()
    {
        isBeingUsed = false;
        itemAnimator.Play("eject");
        inserted = false;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, ejectSFX, randomize: true, 1f, -1);

        if (base.playerHeldBy != null)
        {
            base.playerHeldBy.DamagePlayer(playerDamage);
        }

        playersInPopRange.Clear();
        Collider[] colliders = Physics.OverlapSphere(base.transform.position, popRange, 2621448, QueryTriggerInteraction.Collide);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].gameObject.GetComponent<PlayerControllerB>() != null && !playersInPopRange.Contains(colliders[i].gameObject.GetComponent<PlayerControllerB>()))
            {
                playersInPopRange.Add(colliders[i].gameObject.GetComponent<PlayerControllerB>());
            }
        }

        for (int i = 0; i < playersInPopRange.Count; i++)
        {
            PlayerControllerB player = playersInPopRange[i];

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
    }

    public override void UseUpBatteries()
    {
        base.UseUpBatteries();
        if (inserted)
        {
            inserted = false;
            Eject();
            EjectServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        }
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
            Eject();
            EjectServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        }
        else
        {
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 7, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
            RoundManager.PlayRandomClip(popSource, hitSFX, randomize: true, 1f, -1);
        }

        return true;
	}
}