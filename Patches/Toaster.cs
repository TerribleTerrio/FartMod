using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;

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

    public AudioSource zapSource;

    public AudioClip[] zapSFX;

    public AudioClip zapSteam;

    public GameObject zapPrefab;

    [HideInInspector]
    public Collider? underwaterCollider;

    private Coroutine ejectCoroutine;

    private bool isUnderwater;

    private bool zapping;

    private float lastRainZapTime;

    private float rainZapInterval = 1f;

    private float lastHauntCheckTime;

    private static bool haunted;

    private float hauntInterval = 28f;

    private float hauntChance = 22f;

    public override void Start()
    {
        base.Start();
        playersInPopRange = new List<PlayerControllerB>();
    }

    public override void Update()
    {
        if (base.IsOwner)
        {
            if (!haunted && Time.realtimeSinceStartup - lastHauntCheckTime > (hauntInterval / 2))
            {
                lastHauntCheckTime = Time.realtimeSinceStartup;
                if (FindObjectOfType<DressGirlAI>(includeInactive: true) != null)
                {
                    haunted = true;
                }
            }
            if (haunted && Time.realtimeSinceStartup - lastHauntCheckTime > hauntInterval)
            {
                lastHauntCheckTime = Time.realtimeSinceStartup;
                if (!isHeld && !isHeldByEnemy && !StartOfRound.Instance.inShipPhase && StartOfRound.Instance.timeSinceRoundStarted > 2f && !inserted && Random.Range(0f, 100f) < hauntChance)
                {
                    InsertServerRpc();
                }
            }
        }
        if (isHeld && playerHeldBy != null && playerHeldBy.isUnderwater && playerHeldBy.underwaterCollider != null && playerHeldBy.underwaterCollider.bounds.Contains(base.transform.position + Vector3.up * 0.5f))
        {
            if (underwaterCollider == null)
            {
                underwaterCollider = playerHeldBy.underwaterCollider;
            }
            isUnderwater = true;
        }
        else if (isHeld && playerHeldBy != null && playerHeldBy.isUnderwater && playerHeldBy.underwaterCollider != null && !playerHeldBy.underwaterCollider.bounds.Contains(base.transform.position + Vector3.up * 0.5f))
        {
            if (underwaterCollider == null)
            {
                underwaterCollider = playerHeldBy.underwaterCollider;
            }
            isUnderwater = false;
        }
        else if (isHeld && playerHeldBy != null && !playerHeldBy.isUnderwater)
        {
            if (underwaterCollider != null)
            {
                underwaterCollider = null;
            }
            isUnderwater = false;
        }
        if (isUnderwater && inserted)
        {
            ZapAndSync(1.9f, underwater: true);
            EjectAndSync();
        }
        if ((StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Rainy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded) && Time.realtimeSinceStartup - lastRainZapTime > rainZapInterval)
        {
            lastRainZapTime = Time.realtimeSinceStartup;
            if (inserted && !isInFactory && !isInShipRoom && !Physics.Raycast(base.transform.position, Vector3.up, out _, 100f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore))
            {
                ZapAndSync(0.5f, underwater: false);
                EjectAndSync();
            }
        }
        base.Update();
    }

    public void ZapAndSync(float length, bool underwater)
    {
        StartCoroutine(ZapEffect(length, underwater));
        ZapServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, length, underwater);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ZapServerRpc(int clientWhoSentRpc, float length, bool underwater)
    {
        ZapClientRpc(clientWhoSentRpc, length, underwater);
    }

    [ClientRpc]
    public void ZapClientRpc(int clientWhoSentRpc, float length, bool underwater)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            StartCoroutine(ZapEffect(length, underwater));
        }
    }

    public IEnumerator ZapEffect(float length, bool submerged)
    {
        if (zapping)
        {
            yield break;
        }
        zapping = true;
        float timeElapsed = 0f;
        float duration = length;
        float damageInterval = submerged ? 0.15f : 0.4f;
        float damageTime = 0f;
        float[] zapIntervals = submerged ? [0.25f, 0.35f, 0.5f, 0.65f, 0.75f, 0.9f] : [0.2f, 0.4f, 0.6f, 0.8f];
        int zapInterval = 0;
        GameObject zapPrefabInstance = Instantiate(zapPrefab, base.transform.position, Quaternion.identity);
        VisualEffect zapEffect = zapPrefabInstance.GetComponent<VisualEffect>();
        ParticleSystem zapParticle = zapPrefabInstance.GetComponent<ParticleSystem>();
        HDAdditionalLightData zapLight = zapPrefabInstance.GetComponentInChildren<Light>().GetComponent<HDAdditionalLightData>();
        zapSource = zapPrefabInstance.GetComponent<AudioSource>();
        const string amount = "Amount";
        const string directionMult = "DirectionMult";
        const string lifetime = "Lifetime";
        const string arcNoiseMult = "ArcNoiseMult";
        const string size = "Size";
        const string zap = "Zap";
        const string compSize = "CompSize";
        const string compDirectionMult = "CompDirectionMult";
        const string MinBounds = "MinBounds";
        const string MaxBounds = "MaxBounds";
        zapEffect.SetFloat(compSize, 1.8f);
        zapEffect.SetFloat(compDirectionMult, 2.35f);
        zapLight.lightDimmer = submerged ? 0.7f : 1f;
        zapLight.range = submerged ? 9f : 1f;
        if (!submerged)
        {
            zapEffect.Stop();
            zapEffect.SetVector3(MinBounds, new(-18f, -18f, -18f));
            zapEffect.SetVector3(MaxBounds, new(18f, 18f, 18f));
            zapEffect.SetFloat(compSize, 1.65f);
            zapEffect.SetFloat(compDirectionMult, 1.75f);
            zapEffect.Play();
        }
        while (timeElapsed < duration)
        {
            if (zapInterval < zapIntervals.Length && timeElapsed > zapIntervals[zapInterval])
            {
                switch (zapInterval)
                {
                    case 0:
                        zapEffect.SetFloat(amount, 20f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 1.4f);
                        zapEffect.SetFloat(directionMult, 1.2f);
                        zapEffect.SetFloat(arcNoiseMult, submerged ? 0.1f : 0.4f);
                        break;

                    case 1:
                        zapEffect.SetFloat(amount, 32f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 2.4f);
                        zapEffect.SetFloat(directionMult, 3f);
                        zapEffect.SetFloat(arcNoiseMult, submerged ? 0.15f : 0.5f);
                        break;

                    case 2:
                        zapEffect.SetFloat(amount, 48f);
                        zapEffect.SetFloat(lifetime, 0.3f);
                        zapEffect.SetFloat(size, 5f);
                        zapEffect.SetFloat(directionMult, 5f);
                        zapEffect.SetFloat(arcNoiseMult, submerged ? 0.45f : 0.6f);
                        break;

                    case 3:
                        zapEffect.SetFloat(amount, 32f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 3.4f);
                        zapEffect.SetFloat(directionMult, 3f);
                        zapEffect.SetFloat(arcNoiseMult, submerged ? 0.25f : 0.4f);
                        break;

                    case 4:
                        zapEffect.SetFloat(amount, 24f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 2.4f);
                        zapEffect.SetFloat(directionMult, 3f);
                        zapEffect.SetFloat(arcNoiseMult, submerged ? 0.15f : 0.3f);
                        break;

                    case 5:
                        zapEffect.SetFloat(amount, 16f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 1.4f);
                        zapEffect.SetFloat(directionMult, 2f);
                        zapEffect.SetFloat(arcNoiseMult, submerged ? 0.15f : 0.2f);
                        break;
                }
                zapParticle.Stop();
                zapParticle.Play();
                zapEffect.SendEvent(zap);
                zapSource.clip = zapSFX[Random.Range(0, zapSFX.Length)];
                zapSource.pitch = Random.Range(0.9f, 1.2f);
                zapSource.Play();
                zapInterval++;
            }
            if (timeElapsed > damageTime)
            {
                for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                {
                    PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
                    if (submerged && !player.isPlayerDead && underwaterCollider != null && underwaterCollider.bounds.Contains(player.transform.position) && Vector3.Distance(player.transform.position, base.transform.position) < 20f)
                    {
                        StartCoroutine(FloatBodyToSurface(StartOfRound.Instance.allPlayerScripts[i]));
                        StartOfRound.Instance.allPlayerScripts[i].KillPlayer(Vector3.up * 6f, spawnBody: true, CauseOfDeath.Burning, 6);
                    }
                    else if (!submerged && !player.isPlayerDead && Vector3.Distance(player.transform.position, base.transform.position) < damageRange * 4f)
                    {
                        player.DamagePlayer(6, hasDamageSFX: true, causeOfDeath: CauseOfDeath.Electrocution);
                    }
                }
                damageTime += damageInterval;
            }
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(2f);
        Destroy(zapPrefabInstance);
        zapping = false;
    }

    public IEnumerator FloatBodyToSurface(PlayerControllerB killedPlayer)
    {
		float timeAtStart = Time.realtimeSinceStartup;
		yield return new WaitUntil(() => killedPlayer.deadBody != null || Time.realtimeSinceStartup - timeAtStart > 3f);
		if (!(killedPlayer.deadBody == null))
		{
            yield return null;
            killedPlayer.deadBody.bodyAudio.PlayOneShot(zapSteam, 0.65f);
            killedPlayer.deadBody.causeOfDeath = CauseOfDeath.Drowning;
		}
    }

    public override void DiscardItem()
    {
        base.DiscardItem();
        if (underwaterCollider == null)
        {
            if (Physics.Raycast(base.transform.position, Vector3.down, out RaycastHit downHit, 200f, CoronaMod.Masks.DefaultTriggers, QueryTriggerInteraction.Collide))
            {
                if (downHit.collider.gameObject.TryGetComponent<QuicksandTrigger>(out var quicksand))
                {
                    if (quicksand.isWater || quicksand.isInsideWater)
                    {
                        underwaterCollider = downHit.collider;
                    }
                }
            }
        }
    }

    public override void EquipItem()
    {
        base.EquipItem();
    }

    public override void OnHitGround()
    {
        base.OnHitGround();
        if (inserted && underwaterCollider != null && underwaterCollider.bounds.Contains(base.transform.position))
        {
            isUnderwater = true;
            ZapAndSync(1.9f, underwater: true);
            EjectAndSync();
        }
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (!inserted && !zapping)
        {
            Insert();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void InsertServerRpc()
    {
        InsertClientRpc();
    }

    [ClientRpc]
    public void InsertClientRpc()
    {
        Insert();
    }

    public void Insert()
    {
        inserted = true;
        isBeingUsed = true;
        itemAnimator.SetTrigger("Insert");

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, 2, noiseLoudness/1.5f, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(popSource, insertSFX, randomize: true, 1f, -1);

        if (base.IsOwner)
        {
            float ejectTime = Random.Range(ejectTimeMin, ejectTimeMax);
            if (ejectCoroutine != null)
            {
                StopCoroutine(ejectCoroutine);
                ejectCoroutine = null;
            }
            ejectCoroutine = StartCoroutine(WaitToEject(ejectTime));
        }
        lastRainZapTime = Time.realtimeSinceStartup;
    }

    public IEnumerator WaitToEject(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (inserted)
        {
            EjectAndSync();
        }
    }

    public void EjectAndSync()
    {
        Eject();
        EjectServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
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
        if (!inserted)
        {
            return;
        }
        isBeingUsed = false;
        itemAnimator.SetTrigger("Eject");
        inserted = false;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, ejectSFX, randomize: true, 1f, -1);

        if (base.playerHeldBy != null)
        {
            base.playerHeldBy.DamagePlayer(playerDamage, callRPC: true, force: Vector3.up * 3f);
        }

        playersInPopRange.Clear();
        Collider[] colliders = Physics.OverlapSphere(base.transform.position, popRange, CoronaMod.Masks.PlayerEnemiesMapHazards, QueryTriggerInteraction.Collide);

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
                if (physicsForce > 0f && !Physics.Linecast(base.transform.position, player.transform.position, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
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
                player.DamagePlayer(playerDamage, callRPC: true, force: Vector3.up * 3f);
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
            EjectAndSync();
        }
        else
        {
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 7, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
            RoundManager.PlayRandomClip(popSource, hitSFX, randomize: true, 1f, -1);
        }

        return false;
	}
}