using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Scarecrow : EnemyAI
{
    [Space(15f)]
    [Header("Scarecrow Settings")]

    public int daysBeforeScarecrowSpawns = 5;

    [Range(0f, 1f)]
    public float normalizedTimeInDayToBecomeActive;

    public Item dropItem;

    public Item zapItem;

    public Transform dropItemTransform;

    public Transform meshContainer;

    private List<PlayerControllerB> playersInRange;

    public float scareRange;

    public string[] invalidTerrainTags;

    private bool targetPlayerWatching;

    private bool scarePrimed;

    private List<GameObject> nodes;

    private GameObject[] spawnDenialPoints;

    private List<PlayerControllerB> playersWithLineOfSight;

    public Transform[] lineOfSightTriggers;

    public Transform scareTriggerTransform;

    [Space(10f)]
    [Header("Wind Levels On Moons")]
    public SelectableLevel[] noWindMoons;

    public SelectableLevel[] lightWindMoons;

    public SelectableLevel[] heavyWindMoons;

    [Space(10f)]
    [Header("Chances & Cooldowns")]

    [Space(5f)]
    [Range(0f, 100f)]
    public float tweakOutChance = 20;

    public float tweakOutCooldown = 10f;

    private float tweakOutTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float facePlayerChance = 20;

    public float facePlayerCooldown = 10f;

    private float facePlayerTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float detectAudioChance = 20;

    public float detectSoundCooldown = 10f;

    private float detectSoundTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float moveChance = 20;

    public float minMoveCooldown = 60f;

    public float maxMoveCooldown = 360f;

    private float moveTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float scarePlayerChance = 20;

    public float scarePlayerCooldown = 5f;

    private float scarePlayerTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float decoySoundChance = 20;

    public float decoySoundCooldown = 5f;

    private float decoySoundTimer;

    private float audibleSoundCooldown = 10f;

    [Space(10f)]
    [Header("Danger Values")]
    public float dangerValue;

    public int minEnemyPowerIncrease = 5;

    public int maxEnemyPowerIncrease = 20;

    private int enemyPowerIncrease;

    public int minEnemySpawnIncrease = 0;

    public int maxEnemySpawnIncrease = 2;

    private int enemySpawnIncrease;

    [Space(10f)]
    [Header("Credit Values")]
    public int currentValue;

    public int minStartValue = 150;

    public int maxStartValue = 200;

    public int minEndValue = 10;

    public int maxEndValue = 40;

    public int startValue;

    public int endValue;

    private ScanNodeProperties scanNode;

    public float rotAmount;

    [Space(10f)]
    [Header("Audio")]
    public float noiseRange;

    [Space(5f)]
    public AudioSource detectAudio;

    public AudioClip[] detectSounds;

    [Space(5f)]
    public AudioSource scareAudio;

    public AudioClip[] scareSounds;

    [Space(5f)]
    public AudioSource tweakOutAudio;

    public AudioClip[] tweakOutSounds;

    [Space(5f)]
    public AudioSource warningAudio;

    public AudioClip[] warningSounds;

    [Space(5f)]
    public AudioSource decoyAudio;

    public AudioClip[] decoySounds;

    public override void Start()
    {
        if (StartOfRound.Instance.daysPlayersSurvivedInARow < daysBeforeScarecrowSpawns)
        {
            Debug.Log($"Tried to spawn scarecrow before {daysBeforeScarecrowSpawns} days!");
            RoundManager.Instance.currentDaytimeEnemyPower -= enemyType.PowerLevel;
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("OutsideAINode");
            float num = RoundManager.Instance.timeScript.lengthOfHours * (float)RoundManager.Instance.currentHour;
            RoundManager.Instance.SpawnRandomDaytimeEnemy(spawnPoints, num);
            RoundManager.Instance.DespawnEnemyOnServer(base.NetworkObject);
            return;
        }

        base.Start();

        SetWindLevel();

        normalizedTimeInDayToBecomeActive += 1 / RoundManager.Instance.timeScript.numberOfHours * Random.Range(-1, 1);

        playersInRange = new List<PlayerControllerB>();
        playersWithLineOfSight = new List<PlayerControllerB>();

        dangerValue = UnityEngine.Random.Range(0f,100f);
        enemySpawnIncrease = RemapInt(dangerValue, 0, 100, minEnemySpawnIncrease, maxEnemySpawnIncrease);
        enemyPowerIncrease = RemapInt(dangerValue, 0, 100, minEnemyPowerIncrease, maxEnemyPowerIncrease);

        startValue = UnityEngine.Random.Range(minStartValue, maxStartValue);
        endValue = UnityEngine.Random.Range(minEndValue, maxEndValue);

        List<GameObject> outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode").ToList<GameObject>();
        nodes = outsideAINodes;
        spawnDenialPoints = GameObject.FindGameObjectsWithTag("SpawnDenialPoint");
        for (int i = 0; i < outsideAINodes.Count; i++)
        {
            for (int j = 0; j < spawnDenialPoints.Length; j++)
            {
                if (Vector3.Distance(outsideAINodes[i].transform.position, spawnDenialPoints[j].transform.position) < 30)
                {
                    if (nodes.Contains(outsideAINodes[i]))
                    {
                        nodes.Remove(outsideAINodes[i]);
                    }
                }
            }
        }

        GiveRandomTilt();

        Debug.Log("---Scarecrow Spawn Values---");
        Debug.Log($"Danger value: {dangerValue}");
        Debug.Log($"Minimum enemy spawn increase: {enemySpawnIncrease}");
        Debug.Log($"Max enemy power increase: {enemyPowerIncrease}");
        Debug.Log($"Start value: {startValue}");
        Debug.Log($"End value: {endValue}");
    }

    public void SetWindLevel(int level = -1)
    {
        if (level == -1)
        {
            for (int i = 0; i < noWindMoons.Length; i++)
            {
                if (StartOfRound.Instance.currentLevel == noWindMoons[i])
                {
                    creatureAnimator.SetInteger("WindLevel", 0);
                }
            }

            for (int i = 0; i < lightWindMoons.Length; i++)
            {
                if (StartOfRound.Instance.currentLevel == lightWindMoons[i])
                {
                    creatureAnimator.SetInteger("WindLevel", 1);
                }
            }

            for (int i = 0; i < heavyWindMoons.Length; i++)
            {
                if (StartOfRound.Instance.currentLevel == heavyWindMoons[i])
                {
                    creatureAnimator.SetInteger("WindLevel", 2);
                    return;
                }
            }

            if (StartOfRound.Instance.currentLevel.PlanetName == "91 Bellow")
            {
                creatureAnimator.SetInteger("WindLevel", 2);
            }
        }

        else
        {
            creatureAnimator.SetInteger("WindLevel", level);
        }

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Rainy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            creatureAnimator.SetInteger("WindLevel", 1);
        }

        else if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy)
        {
            creatureAnimator.SetInteger("WindLevel", 2);
        }
    }

    public void GiveRandomTilt()
    {
        meshContainer.localEulerAngles = new Vector3(Random.Range(-5f,5f), 0f, Random.Range(-12f,12f));
        Debug.Log($"Mesh container rotation: {meshContainer.eulerAngles}");
    }

    public bool CheckLineOfSightForScarecrow(PlayerControllerB player)
    {
        for (int i = 0; i < lineOfSightTriggers.Length; i++)
        {
            if (player.HasLineOfSightToPosition(lineOfSightTriggers[i].position, range: 100))
            {
                return true;
            }
        }
        return false;
    }

    public override void Update()
    {
        if (stunnedByPlayer)
        {
            if (stunnedIndefinitely < 1)
            {
                KillEnemyOnOwnerClient();
            }
            else
            {
                audibleSoundCooldown--;
                if (audibleSoundCooldown <= 0)
                {
                    audibleSoundCooldown = 20;
                    RoundManager.Instance.PlayAudibleNoise(transform.position, noiseRange, 1, 0, false, -1);
                    Debug.Log("Played audible sound!");
                }
            }
        }

        if (!base.IsOwner)
        {
            SetClientCalculatingAI(enable: true);
            if (!inSpecialAnimation)
			{
				if (RoundManager.Instance.currentDungeonType == 4 && Vector3.Distance(base.transform.position, RoundManager.Instance.currentMineshaftElevator.elevatorInsidePoint.position) < 1f)
				{
					serverPosition += RoundManager.Instance.currentMineshaftElevator.elevatorInsidePoint.position - RoundManager.Instance.currentMineshaftElevator.previousElevatorPosition;
				}
				base.transform.position = Vector3.SmoothDamp(base.transform.position, serverPosition, ref tempVelocity, syncMovementSpeed);
				base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, targetYRotation, 15f * Time.deltaTime), base.transform.eulerAngles.z);
			}
			timeSinceSpawn += Time.deltaTime;
			return;
        }

        if (isEnemyDead)
        {
            SetClientCalculatingAI(enable: false);
            return;
        }

        if (!inSpecialAnimation)
        {
            SetClientCalculatingAI(enable: true);
        }
        else
        {
            return;
        }

        if (updateDestinationInterval >= 0f)
        {
            updateDestinationInterval -= Time.deltaTime;
        }
        else
        {
            DoAIInterval();
            updateDestinationInterval = AIIntervalTime + UnityEngine.Random.Range(-0.015f, 0.015f);
        }

        if (Mathf.Abs(previousYRotation - base.transform.eulerAngles.y) > 6f)
        {
            previousYRotation = base.transform.eulerAngles.y;
            targetYRotation = previousYRotation;
            if (base.IsServer)
            {
                UpdateEnemyRotationClientRpc((short)previousYRotation);
            }
            else
            {
                UpdateEnemyRotationServerRpc((short)previousYRotation);
            }
        }
    }

    public void AddDetectedPlayer(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerControllerB>() != null)
        {
            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            playersInRange.Add(player);
            Debug.Log($"Scarecrow added {player.playerUsername} to detected players.");
            SetStateBasedOnPlayers(playersInRange.Count);
        }
    }

    public void RemoveDetectedPlayer(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerControllerB>() != null)
        {
            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            playersInRange.Remove(player);
            Debug.Log($"Scarecrow removed {player.playerUsername} from detected players.");
            SetStateBasedOnPlayers(playersInRange.Count);
        }
    }

    public void SetStateBasedOnPlayers(int numPlayers)
    {
        if (RoundManager.Instance.timeScript.normalizedTimeOfDay < normalizedTimeInDayToBecomeActive || isEnemyDead)
        {
            Debug.Log("Scarecrow not active, remaining in inactive state.");
            return;
        }

        if (numPlayers == 0)
        {
            Debug.Log("No players in range, switching to state 1");
            targetPlayer = null;
            Debug.Log("Scarecrow target player set to null.");
            SwitchToBehaviourState(1);
        }

        else if (numPlayers == 1)
        {
            Debug.Log("One player in range, switching to state 2");
            SwitchToBehaviourState(2);
        }

        else if (numPlayers > 1)
        {
            Debug.Log("Players in range, switching to state 3");
            targetPlayer = null;
            Debug.Log("Scarecrow target player set to null.");
            SwitchToBehaviourState(3);
        }
    }

    public override void DoAIInterval()
    {

        base.DoAIInterval();

        if (isEnemyDead)
        {
            return;
        }

        float dayProgress = RoundManager.Instance.timeScript.normalizedTimeOfDay;
        currentValue = RemapInt(dayProgress, 0f, 1f, startValue, endValue);

        if (moveTimer > 0)
        {
            moveTimer--;
        }
        if (facePlayerTimer > 0)
        {
            facePlayerTimer--;
        }
        if (scarePlayerTimer > 0)
        {
            scarePlayerTimer--;
        }
        if (detectSoundTimer > 0)
        {
            detectSoundTimer--;
        }
        if (tweakOutTimer > 0)
        {
            tweakOutTimer--;
        }
        if (decoySoundTimer > 0)
        {
            decoySoundTimer--;
        }

        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
            if (CheckLineOfSightForScarecrow(player) && !playersWithLineOfSight.Contains(player))
            {
                Debug.Log($"Player {StartOfRound.Instance.allPlayerScripts[i].playerUsername} has line of sight to scarecrow.");
                playersWithLineOfSight.Add(player);
            }
            if (!CheckLineOfSightForScarecrow(player) && playersWithLineOfSight.Contains(player))
            {
                Debug.Log($"Player {StartOfRound.Instance.allPlayerScripts[i].playerUsername} lost line of sight to scarecrow.");
                playersWithLineOfSight.Remove(player);
            }
        }

        switch (currentBehaviourStateIndex)
        {

        //INACTIVE
        case 0:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            if (RoundManager.Instance.timeScript.normalizedTimeOfDay > normalizedTimeInDayToBecomeActive && !isEnemyDead)
            {
                SetStateBasedOnPlayers(playersInRange.Count);
            }

            break;

        //NO PLAYERS NEARBY (ACTIVE)
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                previousBehaviourStateIndex = currentBehaviourStateIndex;
                float moveCooldown = Random.Range(minMoveCooldown, maxMoveCooldown);
                moveTimer = moveCooldown;
                Debug.Log($"Scarecrow move cooldown set to {moveCooldown}s.");
            }

            if (moveTimer <= 0)
            {
                float moveCooldown = Random.Range(minMoveCooldown, maxMoveCooldown);
                moveTimer = moveCooldown;
                if (Random.Range(0f,100f) < moveChance)
                {
                    TryMoveToPosition(GetRandomNavMeshPositionNearAINode());
                }
                Debug.Log($"Scarecrow move cooldown set to {moveCooldown}s.");
            }

            break;

        //ONE PLAYER NEARBY
        case 2:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                if (previousBehaviourStateIndex != 3 && detectSoundTimer <= 0)
                {
                    detectSoundTimer = detectSoundCooldown;
                    if (Random.Range(0f,100f) < detectAudioChance)
                    {
                        AudioClip clip = detectSounds[Random.Range(0, detectSounds.Length)];
                        detectAudio.PlayOneShot(clip);
                    }
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;

                if (playersInRange[0] != null)
                {
                    targetPlayer = playersInRange[0];
                }

                if (CheckLineOfSightForScarecrow(targetPlayer))
                {
                    targetPlayerWatching = true;
                    Debug.Log("Target player entered range with scarecrow in view.");
                }
            }

            bool hasTurnedToFacePlayer = false;

            if (!CheckLineOfSightForScarecrow(targetPlayer) && facePlayerTimer <= 0)
            {
                facePlayerTimer = facePlayerCooldown;
                if (Random.Range(0f,100f) < facePlayerChance)
                {
                    FacePosition(targetPlayer.transform.position);
                    if (tweakOutTimer <= 0)
                    {
                        tweakOutTimer = tweakOutCooldown;
                        if (Random.Range(0f,100f) < tweakOutChance)
                        {
                            TweakOut(targetPlayer);
                        }
                    }
                    GiveRandomTilt();
                    hasTurnedToFacePlayer = true;
                }
            }

            if (Vector3.Distance(targetPlayer.transform.position, transform.position) < scareRange)
            {
                if (targetPlayerWatching)
                {
                    if (!CheckLineOfSightForScarecrow(targetPlayer))
                    {
                        targetPlayerWatching = false;

                        if (playersWithLineOfSight.Count == 0 && scarePlayerTimer <= 0)
                        {
                            scarePlayerTimer = scarePlayerCooldown;
                            if (Random.Range(0f,100f) < scarePlayerChance)
                            {
                                if (!hasTurnedToFacePlayer)
                                {
                                    FacePosition(targetPlayer.transform.position);
                                    GiveRandomTilt();
                                }
                                Debug.Log("Scarecrow scare primed!");
                                scarePrimed = true;
                            }
                        }
                    }
                }
                
                else
                {
                    if (CheckLineOfSightForScarecrow(targetPlayer))
                    {
                        if (playersWithLineOfSight.Count == 1 && scarePrimed && targetPlayer.HasLineOfSightToPosition(scareTriggerTransform.position))
                        {
                            ScarePlayer(targetPlayer);
                            scarePrimed = false;
                        }
                        targetPlayerWatching = true;
                    }
                }
            }

            break;

        //PLAYERS NEARBY
        case 3:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                previousBehaviourStateIndex = currentBehaviourStateIndex;
                decoySoundTimer = decoySoundCooldown;
            }

            List<PlayerControllerB> playersInRangeWithLineOfSight = playersInRange;
            for (int i = 0; i < playersInRangeWithLineOfSight.Count; i++)
            {
                PlayerControllerB player = playersInRangeWithLineOfSight[i];
                if (!CheckLineOfSightForScarecrow(player))
                {
                    playersInRangeWithLineOfSight.Remove(player);
                }
            }

            if (playersInRangeWithLineOfSight.Count == 1)
            {
                targetPlayer = playersInRangeWithLineOfSight[0];

                if (playersWithLineOfSight.Count == 1)
                {
                    if (tweakOutTimer <= 0f)
                    {
                        tweakOutTimer = tweakOutCooldown;
                        if (Random.Range(0f,100f) < tweakOutChance)
                        {
                            TweakOut(targetPlayer);
                        }
                    }
                }
            }

            if (decoySoundTimer <= 0)
            {
                decoySoundTimer = decoySoundCooldown;
                if (Random.Range(0f,100f) < decoySoundChance)
                {
                    PlayDecoySound();
                }
            }

            break;
        }

    }

    public void TryMoveToPosition(Vector3 newPosition)
    {
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;

        for (int i = 0; i < players.Length; i++)
        {
            if (CheckLineOfSightForScarecrow(players[i]))
            {
                Debug.Log($"Current position in view of {players[i].playerUsername}, scarecrow did not move.");
                return;
            }

            if (players[i].HasLineOfSightToPosition(newPosition, range: 100))
            {
                Debug.Log($"New position in view of {players[i].playerUsername}, scarecrow did not move.");
                return;
            }
        }

        bool onInvalidTerrain = false;
        RaycastHit hitInfo;
        Physics.Raycast(newPosition, Vector3.down, out hitInfo, StartOfRound.Instance.collidersAndRoomMaskAndDefault);
        for (int i = 0; i < invalidTerrainTags.Length; i++)
        {
            if (hitInfo.collider.gameObject.tag == invalidTerrainTags[i])
            {
                Debug.Log($"New position on {invalidTerrainTags[i]}.");
                onInvalidTerrain = true;
            }
        }

        if (onInvalidTerrain)
        {
            Debug.Log("Scarecrow attempting to move to invalid terrain.");
            float c = Random.Range(0f,100f);
            if (c > 5)
            {
                Debug.Log("Scarecrow did not move.");
                return;
            }
        }

        GameObject[] spawnDenialPoints = GameObject.FindGameObjectsWithTag("SpawnDenialPoint");
        for (int i = 0; i < spawnDenialPoints.Length; i++)
        {
            if (Vector3.Distance(newPosition, spawnDenialPoints[i].transform.position) < 30)
            {
                Debug.Log("New position too close to spawn denial point, scarecrow did not move.");
            }
        }

        transform.position = newPosition;
        GiveRandomTilt();
    }

    public Vector3 GetRandomNavMeshPositionNearAINode(float radius = 20f)
    {
        int nodeSelected = Random.Range(0, nodes.Count);
        Vector3 nodePosition = nodes[nodeSelected].transform.position;
        Debug.Log($"Selected node {nodeSelected} at {nodePosition}.");
        return RoundManager.Instance.GetRandomNavMeshPositionInRadius(nodePosition, radius);
    }

    public void ScarePlayer(PlayerControllerB player)
    {
        Debug.Log($"Scarecrow scared player {player.playerUsername}.");
        RoundManager.Instance.PlayAudibleNoise(transform.position, noiseRange, 1, 0, false, -1);
        AudioClip clip = scareSounds[Random.Range(0, scareSounds.Length)];
        scareAudio.PlayOneShot(clip);
        creatureAnimator.SetTrigger("ScarePlayer");
        player.insanityLevel += player.insanityLevel * 0.15f;
        player.JumpToFearLevel(0.5f);
        FacePosition(player.transform.position);
    }

    public void TweakOut(PlayerControllerB player)
    {
        Debug.Log("Scarecrow tweaked out!");
        AudioClip clip = tweakOutSounds[Random.Range(0, tweakOutSounds.Length)];
        tweakOutAudio.PlayOneShot(clip);
        player.insanityLevel += player.maxInsanityLevel * 0.1f;
        creatureAnimator.SetTrigger("TweakOut");
    }

    public void FacePosition(Vector3 lookPosition)
    {
        Transform tempTransform = base.transform;
        tempTransform.LookAt(lookPosition);
        tempTransform.eulerAngles = new Vector3(0f, tempTransform.eulerAngles.y, 0f);
        base.transform.eulerAngles = tempTransform.eulerAngles;
    }

    public void PlayDecoySound(AudioClip clip = null)
    {
        Vector3 meanVector = Vector3.zero;
        for (int i = 0; i < playersInRange.Count; i++)
        {
            meanVector += playersInRange[i].transform.position;
        }
        meanVector /= playersInRange.Count;

        Vector3 direction = Vector3.Normalize(meanVector - transform.position);
        Vector3 decoyPosition = meanVector + direction * 3f;
        decoyPosition = RoundManager.Instance.GetRandomPositionInRadius(decoyPosition, 0f, 3.5f);

        decoyAudio.transform.position = decoyPosition;
        if (clip == null)
        {
            clip = decoySounds[Random.Range(0, decoySounds.Length)];
        }
        decoyAudio.clip = clip;
        decoyAudio.Play();
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (isEnemyDead)
        {
            return;
        }
        creatureAnimator.SetTrigger("TakeDamage");
        if (playerWhoHit != null)
        {
            targetPlayer = playerWhoHit;
        }
        enemyHP -= force;
        if (base.IsOwner && enemyHP <= 0 && !isEnemyDead)
        {
            KillEnemyOnOwnerClient();
        }
    }

    public override void HitFromExplosion(float distance)
    {
        base.HitFromExplosion(distance);
        if (isEnemyDead)
        {
            return;
        }
        else
        {
            dropItem = zapItem;
            creatureAnimator.SetTrigger("Explode");
            KillEnemyOnOwnerClient();
        }
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        creatureAnimator.SetBool("IsDead", value: true);
        RoundManager.Instance.PlayAudibleNoise(transform.position, 5, 1, 0, false, -1);
        IncreaseEnemySpawnRate();
        DropItem();
        SubtractFromPowerLevel();
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1f, PlayerControllerB setStunnedByPlayer = null)
    {
        if (isEnemyDead || !enemyType.canBeStunned)
        {
            return;
        }

        if (setToStunned)
        {
            stunnedByPlayer = setStunnedByPlayer;
            if (stunnedByPlayer)
            {
                //BEHAVIOUR WHEN STUNNED BY GUN
                creatureAnimator.SetBool("Electrocuting", true);
                dropItem = zapItem;
            }
            else
            {
                //BEHAVIOUR WHEN STUNNED BY FLASH
            }
        }
    }

    public void DropItem()
    {
        if (dropItem == null)
        {
            Debug.Log("No drop item specified, scarecrow did not drop item.");
            return;
        }

        GameObject item = Instantiate(dropItem.spawnPrefab, dropItemTransform.position, dropItemTransform.rotation, RoundManager.Instance.mapPropsContainer.transform);
        item.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        RoundManager.Instance.spawnedSyncedObjects.Add(item);

        if (item.GetComponent<GrabbableObject>() != null)
        {
            GrabbableObject gObject = item.GetComponent<GrabbableObject>();
            gObject.SetScrapValue(currentValue);
        }

        if (item.GetComponent<Pumpkin>() != null)
        {
            item.GetComponent<Pumpkin>().rotAmount = rotAmount;
        }

        Debug.Log($"Scarecrow dropped {dropItem}.");
    }

    private void IncreaseEnemySpawnRate()
    {
        Debug.Log($"Original minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        Debug.Log($"Original max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");
        RoundManager.Instance.minOutsideEnemiesToSpawn += enemySpawnIncrease;
        RoundManager.Instance.currentMaxOutsidePower += enemyPowerIncrease;
        Debug.Log($"Increased minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        Debug.Log($"Increased max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");

        int c = UnityEngine.Random.Range(0, 3);
        if (dangerValue <= 33)
        {
            warningAudio.PlayOneShot(warningSounds[c]);
        }
        else if (dangerValue <= 66)
        {
            warningAudio.PlayOneShot(warningSounds[c+3]);
        }
        else
        {
            warningAudio.PlayOneShot(warningSounds[c+6]);
        }
    }

    public void OnTouch(Collider other)
    {

    }

    public void OnExit(Collider other)
    {

    }

    public int RemapInt(float value, float min1, float max1, float min2, float max2)
    {
        float m = (value - min1) / (max1 - min1) * (max2 - min2) + min2;
        return Mathf.RoundToInt(m);
    }
}