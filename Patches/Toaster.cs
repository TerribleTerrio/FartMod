using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;
using System.Linq;

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

    [HideInInspector]
    public AudioSource zapSource;

    public AudioClip[] zapSFX;

    public AudioClip zapSteam;

    public GameObject zapPrefab;

    public AudioSource rainSource;

    public ParticleSystem rainDropParticle;

    public AudioClip[] raindropClips;
   
    public ParticleSystem rainSizzleParticle;

    public AudioClip[] sizzleClips;

    [HideInInspector]
    public Collider? underwaterCollider;

    private Coroutine ejectCoroutine;

    private bool isUnderwater;

    private bool zapping;

    private float lastRainZapCheckTime;

    private float rainZapCheckInterval = 1f;

    private float rainVisAmount;

    private float sizzleInterval = 3f;

    private float lastSizzleTime;

    private float raindropInterval = 0.1f;

    private float lastRaindropTime;

    private float rainInsertZapMeter;

    private float rainInsertZapTime = 4f;

    private float sizzleChance = 60f;

    private float raindropChance = 25f;

    private bool inRain;

    private float lastHauntCheckTime;

    private static bool haunted;

    private float hauntInterval = 28f;

    private float hauntChance = 22f;

    private float lastWaterCheck;

    private float waterInterval = 1f;

    private List<Collider> waterColliders = [];

    private bool gotColliderOnDrop;

    public override void Start()
    {
        base.Start();
        playersInPopRange = new List<PlayerControllerB>();
        SetupToaster();
        StartOfRound.Instance.StartNewRoundEvent.AddListener(SetupToaster);
        CoronaMod.Patches.NetworkPatches.StartOfRoundPatch.EndRoundEvent.AddListener(ClearToaster);
    }

    public void SetupToaster()
    {
        haunted = false;
        waterColliders.Clear();
        QuicksandTrigger[] array = FindObjectsOfType<QuicksandTrigger>().Where(x => x.isWater || x.isInsideWater).ToArray();
        for (int i = 0; i < array.Length; i++)
        {
            waterColliders.Add(array[i].gameObject.GetComponent<Collider>());
        }
    }

    public void ClearToaster()
    {
        haunted = false;
        waterColliders.Clear();
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
        WaterUpdate();
        base.Update();
    }

    public void WaterUpdate()
    {
        if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            return;
        }
        if (Time.realtimeSinceStartup - lastWaterCheck > waterInterval)
        {
            lastWaterCheck = Time.realtimeSinceStartup;
            if (!gotColliderOnDrop && waterColliders.Count > 0)
            {
                underwaterCollider = waterColliders.OrderBy(collider => (collider.transform.position - base.transform.position).sqrMagnitude).First();
            }
        }
        if (underwaterCollider != null && underwaterCollider.bounds.Contains(base.transform.position))
        {
            isUnderwater = true;
        }
        else
        {
            isUnderwater = false;
        }
        if (isUnderwater && inserted)
        {
            ZapAndSync(1.9f, submergedEffect: true);
            EjectAndSync();
        }

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Rainy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            inRain = !Physics.Raycast(base.transform.position, Vector3.up, out _, 100f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore);
            rainVisAmount = Mathf.Lerp(rainVisAmount, inRain ? 1f : 0f, Time.deltaTime * (inRain ? 0.4f : 8f));
            rainSource.volume = Mathf.Lerp(rainSource.volume, inRain ? 1f : 0f, Time.deltaTime * (inRain ? 1.5f : 8f));;
            rainInsertZapMeter = Mathf.Clamp(inRain && inserted ? rainInsertZapMeter + Time.deltaTime : rainInsertZapMeter - Time.deltaTime, 0f, rainInsertZapTime);

            if (rainVisAmount > 0.1f)
            {
                if (Time.realtimeSinceStartup - lastRaindropTime > raindropInterval)
                {
                    raindropInterval = Random.Range(0.015f, 0.075f);
                    lastRaindropTime = Time.realtimeSinceStartup;
                    if (Random.Range(0f, 100f) < raindropChance * Mathf.Clamp(rainInsertZapMeter / 2, 0.5f, rainInsertZapMeter))
                    {
                        rainDropParticle.Play();
                        rainSource.PlayOneShot(raindropClips[Random.Range(0, raindropClips.Length)]);
                    }
                }
            }

            if (rainVisAmount > 0.5f)
            {
                if (Time.realtimeSinceStartup - lastSizzleTime > sizzleInterval)
                {
                    sizzleInterval = Random.Range(inserted ? 0.3f : 1.5f, inserted ? 1.4f : 4f);
                    lastSizzleTime = Time.realtimeSinceStartup;
                    if (Random.Range(0f, 100f) < sizzleChance * Mathf.Clamp(rainInsertZapMeter / 2, 0.5f, rainInsertZapMeter))
                    {
                        rainSizzleParticle.Play();
                        rainSource.PlayOneShot(sizzleClips[Random.Range(0, sizzleClips.Length)]);
                    }
                }
            }

            if (Time.realtimeSinceStartup - lastRainZapCheckTime > rainZapCheckInterval)
            {
                lastRainZapCheckTime = Time.realtimeSinceStartup;
                if (inserted && !isInFactory && !isInShipRoom && rainVisAmount > 0.75f && !Physics.Raycast(base.transform.position, Vector3.up, out _, 100f, CoronaMod.Masks.DefaultRoomCollidersRailingVehicle, QueryTriggerInteraction.Ignore) && rainInsertZapMeter >= rainInsertZapTime)
                {
                    EjectAndSync();
                }
            }
        }
    }

    public void ZapAndSync(float length, bool submergedEffect, bool fryOwner = false)
    {
        StartCoroutine(ZapEffect(length, submergedEffect, fryOwner));
        ZapServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, length, submergedEffect, fryOwner);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ZapServerRpc(int clientWhoSentRpc, float length, bool submergedEffect, bool fryOwner = false)
    {
        ZapClientRpc(clientWhoSentRpc, length, submergedEffect, fryOwner);
    }

    [ClientRpc]
    public void ZapClientRpc(int clientWhoSentRpc, float length, bool submergedEffect, bool fryOwner = false)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            StartCoroutine(ZapEffect(length, submergedEffect, fryOwner));
        }
    }

    public IEnumerator ZapEffect(float length, bool submergedEffect, bool fryOwner = false)
    {
        if (zapping)
        {
            yield break;
        }
        zapping = true;
        float timeElapsed = 0f;
        float duration = length;
        float damageInterval = submergedEffect ? 0.15f : 0.4f;
        float damageTime = 0f;
        float[] zapIntervals = submergedEffect ? [0.25f, 0.35f, 0.5f, 0.65f, 0.75f, 0.9f] : [0.2f, 0.4f, 0.6f, 0.8f];
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
        zapLight.lightDimmer = submergedEffect ? 0.7f : 1f;
        zapLight.range = submergedEffect ? 9f : 1f;
        if (!submergedEffect)
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
                        zapEffect.SetFloat(arcNoiseMult, submergedEffect ? 0.1f : 0.4f);
                        break;

                    case 1:
                        zapEffect.SetFloat(amount, 32f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 2.4f);
                        zapEffect.SetFloat(directionMult, 3f);
                        zapEffect.SetFloat(arcNoiseMult, submergedEffect ? 0.15f : 0.5f);
                        break;

                    case 2:
                        zapEffect.SetFloat(amount, 48f);
                        zapEffect.SetFloat(lifetime, 0.3f);
                        zapEffect.SetFloat(size, 5f);
                        zapEffect.SetFloat(directionMult, 5f);
                        zapEffect.SetFloat(arcNoiseMult, submergedEffect ? 0.45f : 0.6f);
                        break;

                    case 3:
                        zapEffect.SetFloat(amount, 32f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 3.4f);
                        zapEffect.SetFloat(directionMult, 3f);
                        zapEffect.SetFloat(arcNoiseMult, submergedEffect ? 0.25f : 0.4f);
                        break;

                    case 4:
                        zapEffect.SetFloat(amount, 24f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 2.4f);
                        zapEffect.SetFloat(directionMult, 3f);
                        zapEffect.SetFloat(arcNoiseMult, submergedEffect ? 0.15f : 0.3f);
                        break;

                    case 5:
                        zapEffect.SetFloat(amount, 16f);
                        zapEffect.SetFloat(lifetime, 0.2f);
                        zapEffect.SetFloat(size, 1.4f);
                        zapEffect.SetFloat(directionMult, 2f);
                        zapEffect.SetFloat(arcNoiseMult, submergedEffect ? 0.15f : 0.2f);
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
                    if (fryOwner && playerHeldBy == player && !player.isPlayerDead)
                    {
                        StartCoroutine(ReturnDropPosition(player.gameplayCamera.transform.position));
                        StartCoroutine(FryBody(player, submerged: false));
                        player.KillPlayer(player.transform.forward * 8f, spawnBody: true, CauseOfDeath.Burning, 6);
                    }
                    if (!fryOwner && submergedEffect && !player.isPlayerDead && underwaterCollider != null && underwaterCollider.bounds.Contains(player.transform.position) && Vector3.Distance(player.transform.position, base.transform.position) < 20f)
                    {
                        StartCoroutine(FryBody(player));
                        player.KillPlayer(Vector3.up * 6f, spawnBody: true, CauseOfDeath.Burning, 6);
                    }
                    else if (!player.isPlayerDead && Vector3.Distance(player.transform.position, base.transform.position) < damageRange * 4f)
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

    public IEnumerator ReturnDropPosition(Vector3 prevPosition)
    {
        yield return null;
        base.transform.position = prevPosition;
        startFallingPosition = base.transform.parent.InverseTransformPoint(base.transform.position);
        FallToGround(randomizePosition: true);
        fallTime = Random.Range(-0.3f, 0.05f);
    }

    public IEnumerator FryBody(PlayerControllerB killedPlayer, bool submerged = true)
    {
		float timeAtStart = Time.realtimeSinceStartup;
		yield return new WaitUntil(() => killedPlayer.deadBody != null || Time.realtimeSinceStartup - timeAtStart > 3f);
		if (!(killedPlayer.deadBody == null))
		{
            yield return null;
            killedPlayer.deadBody.bodyAudio.PlayOneShot(zapSteam, 0.65f);
            if (submerged)
            {
                killedPlayer.deadBody.causeOfDeath = CauseOfDeath.Drowning;
            }
		}
    }

    public override void DiscardItem()
    {
        base.DiscardItem();
        SetParticleAndSync(true);
        if (Physics.Raycast(base.transform.position, Vector3.down, out RaycastHit downHit, 200f, CoronaMod.Masks.DefaultTriggers, QueryTriggerInteraction.Collide))
        {
            if (downHit.collider.gameObject.TryGetComponent<QuicksandTrigger>(out var quicksand))
            {
                if ((!isInFactory && quicksand.isWater) || (isInFactory && quicksand.isInsideWater))
                {
                    underwaterCollider = downHit.collider;
                    gotColliderOnDrop = true;
                    return;
                }
            }
        }
        gotColliderOnDrop = false;
    }

    public override void EquipItem()
    {
        base.EquipItem();
        SetParticleAndSync(true);
    }

    public override void PocketItem()
    {
        base.PocketItem();
        SetParticleAndSync(false);
    }

    public void SetParticleAndSync(bool setto)
    {
        SetParticle(setto);
        SetParticleServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, setto);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SetParticleServerRpc(int clientWhoSentRpc, bool setto)
    {
        SetParticleClientRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, setto);
    }

    [ClientRpc]
    public void SetParticleClientRpc(int clientWhoSentRpc, bool setto)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            SetParticle(setto);
        }
    }

    public void SetParticle(bool setto)
    {
        rainDropParticle.gameObject.SetActive(setto);
        rainSizzleParticle.gameObject.SetActive(setto);
        rainSource.gameObject.SetActive(setto);
    }

    public override void OnHitGround()
    {
        base.OnHitGround();
        if (underwaterCollider != null && underwaterCollider.bounds.Contains(base.transform.position))
        {
            isUnderwater = true;
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
        rainInsertZapMeter = 0f;
        rainInsertZapTime = Random.Range(3f, 5.5f);

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
        lastRainZapCheckTime = Time.realtimeSinceStartup;
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
        if (inRain)
        {
            ZapAndSync(0.5f, submergedEffect: false);
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
            EjectAndSync();
        }
    }

	public override void ChargeBatteries()
	{
        if (inserted && playerHeldBy != null && insertedBattery.charge == 1f)
        {
            ZapAndSync(1.5f, submergedEffect: true, fryOwner: true);
            EjectAndSync();
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