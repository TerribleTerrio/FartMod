using System.Collections.Generic;
using System.Collections;
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

    public GameObject dropItemPrefab;

    public GameObject zapItemPrefab;

    public Transform dropItemTransform;

    public Transform meshContainer;

    private List<PlayerControllerB> playersInRange;

    private PlayerControllerB targetPlayer;

    public float detectRange;

    public float scareRange;

    public string[] invalidTerrainTags;

    private bool invisible;

    private Coroutine changePositionCoroutine;

    private bool scarePrimed;

    private List<GameObject> nodes;

    private GameObject[] spawnDenialPoints;

    private List<PlayerControllerB> playersWithLineOfSight;

    public Transform[] lineOfSightTriggers;

    public Transform scareTriggerTransform;

    private FloodWeather floodWeather;

    private Collider enemyCollider;

    [Space(10f)]
    [Header("Wind Levels On Moons")]
    public string[] noWindMoons;

    public string[] lightWindMoons;

    public string[] heavyWindMoons;

    [Space(10f)]
    [Header("Chances & Cooldowns")]

    [Space(5f)]
    public float minSearchTime = 100f;
    
    public float maxSearchTime = 300f;

    private float searchTimer;

    [Space(5f)]
    public float minChaseTime = 100f;

    public float maxChaseTime = 300f;

    private float chaseTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float tweakOutStartingChance = 20;

    private float tweakOutChance;

    public float tweakOutChanceIncrement;

    public float tweakOutCooldown = 10f;

    private float tweakOutTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float facePlayerStartingChance = 20;

    private float facePlayerChance;

    public float facePlayerChanceIncrement;

    public float facePlayerCooldown = 10f;

    private float facePlayerTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float detectSoundStartingChance = 20;

    private float detectSoundChance;

    public float detectSoundChanceIncrement;

    public float detectSoundCooldown = 10f;

    private float detectSoundTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float moveStartingChance = 20;

    private float moveChance;

    public float moveChanceIncrement;

    public float minMoveCooldown = 60f;

    public float maxMoveCooldown = 360f;

    private float moveTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float scarePlayerStartingChance = 20;

    private float scarePlayerChance;

    public float scarePlayerChanceIncrement;

    [Space(5f)]
    [Range(0f, 100f)]
    public float instantScareStartingChance = 5;

    private float instantScareChance;

    public float instantScareChanceIncrement;

    public float scarePlayerCooldown = 5f;

    private float scarePlayerTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float decoySoundStartingChance = 20;

    private float decoySoundChance;

    public float decoySoundChanceIncrement;

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

    public AudioClip[] warningSoundsLow;

    public AudioClip[] warningSoundsMedium;

    public AudioClip[] warningSoundsHigh;

    [Space(5f)]
    public AudioSource decoyAudio;

    public AudioClip[] decoySounds;

    [Space(5f)]
    public bool useScanNode;

    public ScanNodeProperties scanNode;

    public TerminalNode newScarecrowNode;

	public Terminal currentTerminal;

    public override void Start()
    {
        if (IsOwner)
        {
            //IF SCARECROW SPAWNS BEFORE X DAYS HAVE PASSED:
            if (StartOfRound.Instance.gameStats.daysSpent < daysBeforeScarecrowSpawns)
            {
                //DESPAWN SCARECROW
                Debug.Log($"[SCARECROW]: Tried to spawn before {daysBeforeScarecrowSpawns} days!");
                RoundManager.Instance.DespawnEnemyOnServer(base.NetworkObject);

                //TRY SPAWN RANDOM DAYTIME ENEMY INSTEAD
                GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("OutsideAINode");
                float num = RoundManager.Instance.timeScript.lengthOfHours * (float)RoundManager.Instance.currentHour;
                RoundManager.Instance.SpawnRandomDaytimeEnemy(spawnPoints, num);
                return;
            }

            SetDangerLevelsAndSync();
            GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);

            //MOVE TO VALID POSITION
            if (!CheckPositionIsValid(transform.position))
            {
                // MoveToRandomPosition();
            }
        }

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            floodWeather = Object.FindObjectOfType<FloodWeather>();
        }

        if (!useScanNode)
        {
            scanNode.creatureScanID = -1;
        }

        base.Start();

        SetWindLevel();

        playersInRange = new List<PlayerControllerB>();
        playersWithLineOfSight = new List<PlayerControllerB>();

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

        enemyCollider = base.gameObject.GetComponentInChildren<EnemyAICollisionDetect>().gameObject.GetComponent<Collider>();

        tweakOutChance = tweakOutStartingChance;
        facePlayerChance = facePlayerStartingChance;
        detectSoundChance = detectSoundStartingChance;
        moveChance = moveStartingChance;
        scarePlayerChance = scarePlayerStartingChance;
        decoySoundChance = decoySoundStartingChance;

        Debug.Log("[SCARECROW]: Spawned!");
        Debug.Log($"[SCARECROW]: Danger value: {dangerValue}");
        Debug.Log($"[SCARECROW]: Minimum enemy spawn increase: {enemySpawnIncrease}");
        Debug.Log($"[SCARECROW]: Max enemy power increase: {enemyPowerIncrease}");
        Debug.Log($"[SCARECROW]: Start value: {startValue}");
        Debug.Log($"[SCARECROW]: End value: {endValue}");
    }

    public void SetDangerLevelsAndSync()
    {
        normalizedTimeInDayToBecomeActive += 1 / RoundManager.Instance.timeScript.numberOfHours * Random.Range(-1, 1);

        dangerValue = UnityEngine.Random.Range(0f,100f);
        enemySpawnIncrease = RemapInt(dangerValue, 0, 100, minEnemySpawnIncrease, maxEnemySpawnIncrease);
        enemyPowerIncrease = RemapInt(dangerValue, 0, 100, minEnemyPowerIncrease, maxEnemyPowerIncrease);

        startValue = UnityEngine.Random.Range(minStartValue, maxStartValue);
        endValue = UnityEngine.Random.Range(minEndValue, maxEndValue);

        SetDangerLevelsServerRpc(normalizedTimeInDayToBecomeActive, dangerValue, enemySpawnIncrease, enemyPowerIncrease, startValue, endValue);
    }

    [ServerRpc]
    public void SetDangerLevelsServerRpc(float activeTime, float danger, int spawnIncrease, int powerIncrease, int startPrice, int endPrice)
    {
        SetDangerLevelsClientRpc(activeTime, danger, spawnIncrease, powerIncrease, startPrice, endPrice);
    }

    [ClientRpc]
    public void SetDangerLevelsClientRpc(float activeTime, float danger, int spawnIncrease, int powerIncrease, int startPrice, int endPrice)
    {
        normalizedTimeInDayToBecomeActive += activeTime;

        dangerValue = danger;
        enemySpawnIncrease = spawnIncrease;
        enemyPowerIncrease = powerIncrease;

        startValue = startPrice;
        endValue = endPrice;
    }

    public void SetWindLevel(int level = -1)
    {
        if (level == -1)
        {
            for (int i = 0; i < noWindMoons.Length; i++)
            {
                if (StartOfRound.Instance.currentLevel.PlanetName == noWindMoons[i])
                {
                    creatureAnimator.SetInteger("WindLevel", 0);
                }
            }

            for (int i = 0; i < lightWindMoons.Length; i++)
            {
                if (StartOfRound.Instance.currentLevel.PlanetName == lightWindMoons[i])
                {
                    creatureAnimator.SetInteger("WindLevel", 1);
                }
            }

            for (int i = 0; i < heavyWindMoons.Length; i++)
            {
                if (StartOfRound.Instance.currentLevel.PlanetName == heavyWindMoons[i])
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

    public void GiveRandomTiltAndSync(int clientWhoSentRpc)
    {
        Vector3 randomTilt = new Vector3(Random.Range(-5f,5f), 0f, Random.Range(-12f,12f));
        meshContainer.localEulerAngles = randomTilt;
        GiveRandomTiltServerRpc(randomTilt, clientWhoSentRpc);
    }

    [ServerRpc(RequireOwnership = false)]
    public void GiveRandomTiltServerRpc(Vector3 randomTilt, int clientWhoSentRpc)
    {
        GiveRandomTiltClientRpc(randomTilt, clientWhoSentRpc);
    }

    [ClientRpc]
    public void GiveRandomTiltClientRpc(Vector3 randomTilt, int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            meshContainer.localEulerAngles = randomTilt;
        }
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
        float dayProgress = RoundManager.Instance.timeScript.normalizedTimeOfDay;
        currentValue = RemapInt(dayProgress, 0f, 1f, startValue, endValue);
        rotAmount = dayProgress;
        creatureAnimator.SetFloat("rot", rotAmount);

        if (stunnedByPlayer)
        {
            if (stunnedIndefinitely < 1)
            {
                creatureAnimator.SetBool("Electrocuting", false);
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

    public void AddDetectedPlayer(PlayerControllerB player)
    {
        if (IsOwner)
        {
            playersInRange.Add(player);
            Debug.Log($"[SCARECROW]: Added {player.playerUsername} to detected players.");
        }
    }

    public void RemoveDetectedPlayer(PlayerControllerB player)
    {
        if (IsOwner)
        {
            playersInRange.Remove(player);
            Debug.Log($"[SCARECROW]: Removed {player.playerUsername} from detected players.");
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (isEnemyDead)
        {
            return;
        }

        if (searchTimer > 0)
        {
            searchTimer--;
        }
        if (moveTimer > 0)
        {
            moveTimer--;
        }
        if (chaseTimer > 0)
        {
            chaseTimer--;
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
                Debug.Log($"[SCARECROW]: Player {StartOfRound.Instance.allPlayerScripts[i].playerUsername} has line of sight.");
                playersWithLineOfSight.Add(player);
            }
            if (!CheckLineOfSightForScarecrow(player) && playersWithLineOfSight.Contains(player))
            {
                Debug.Log($"[SCARECROW]: Player {StartOfRound.Instance.allPlayerScripts[i].playerUsername} lost line of sight.");
                playersWithLineOfSight.Remove(player);
            }
        }

        if (invisible && changePositionCoroutine == null && playersWithLineOfSight.Count < 1)
        {
            invisible = false;
            PlayerControllerB nearestPlayer = NearestPlayer();
            if (nearestPlayer != null && Vector3.Distance(nearestPlayer.transform.position, transform.position) < detectRange * 1.5f)
            {
                FacePosition(NearestPlayer().transform.position);
            }
            SetInvisibleServerRpc(false);
        }

        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
            if (!playersInRange.Contains(player))
            {
                if (Vector3.Distance(player.transform.position, transform.position) <= detectRange)
                {
                    AddDetectedPlayer(player);
                }
            }
            else
            {
                if (Vector3.Distance(player.transform.position, transform.position) > detectRange)
                {
                    RemoveDetectedPlayer(player);
                }
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

            if (RoundManager.Instance.timeScript.normalizedTimeOfDay > normalizedTimeInDayToBecomeActive && !isEnemyDead && !invisible)
            {
                currentBehaviourStateIndex = 1;
            }

            break;



        //SEARCHING
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[SCARECROW]: Searching.");
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                searchTimer = Random.Range(minSearchTime, maxSearchTime);

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //IF SEARCH EXCEEDS SEARCH TIME
            if (searchTimer <= 0)
            {
                Debug.Log("[SCARECROW]: Search exceeded search time.");
                for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                {
                    PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
                    if (targetPlayer == null)
                    {
                        if (!player.isInsideFactory && !player.isPlayerDead && player.isPlayerAlone)
                        {
                            targetPlayer = player;
                            continue;
                        }
                    }
                    else if (!player.isInsideFactory && !player.isPlayerDead && player.isPlayerAlone)
                    {
                        if (Vector3.Distance(player.transform.position, transform.position) < Vector3.Distance(targetPlayer.transform.position, transform.position))
                        {
                            targetPlayer = player;
                        }
                    }
                }

                if (targetPlayer != null)
                {
                    Debug.Log("[SCARECROW]: Closest valid player selected as target.");
                    currentBehaviourStateIndex = 2;
                }
                else
                {
                    Debug.Log("[SCARECROW]: No valid players, restarting search.");
                    searchTimer = Random.Range(minSearchTime, maxSearchTime);
                }
            }

            //IF NO PLAYERS WITHIN RANGE
            if (playersInRange.Count < 1)
            {
                if (moveTimer <= 0 && playersWithLineOfSight.Count == 0)
                {
                    moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                    if (Random.Range(0f,100f) < moveChance)
                    {
                        MoveToRandomPosition();
                        moveChance = moveStartingChance;
                    }
                    else
                    {
                        moveChance += moveChanceIncrement;
                    }
                }
            }

            //IF ONE PLAYER WITHIN RANGE
            else if (playersInRange.Count == 1)
            {
                targetPlayer = playersInRange[0];
                currentBehaviourStateIndex = 2;
            }

            //IF MULTIPLE PLAYERS WITHIN RANGE
            else if (playersInRange.Count > 1)
            {
                currentBehaviourStateIndex = 3;
            }

            break;



        //CHASING
        case 2:

            bool instantScare = false;

            //IF TARGET PLAYER IS NULL
            if (targetPlayer == null)
            {
                Debug.LogError("[SCARECROW]: Entered chase while target was null! Returning to search.");
                currentBehaviourStateIndex = 1;
                break;
            }

            //IF PLAYER IS NOT ACCESSIBLE
            if (targetPlayer.isInsideFactory || targetPlayer.isPlayerDead)
            {
                Debug.Log("[SCARECROW]: Target player dead or inaccessible, returning to search.");
                targetPlayer = null;
                currentBehaviourStateIndex = 1;
                break;
            }

            //WHEN FIRST ENTERING CHASE STATE
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log($"[SCARECROW]: Chasing {targetPlayer.playerUsername}.");
                chaseTimer = Random.Range(minChaseTime, maxChaseTime);
                scarePrimed = false;
                
                //DETERMINE INSTANT SCARE
                if (Random.Range(0f,100f) < instantScareChance)
                {
                    instantScare = true;
                    instantScareChance = instantScareStartingChance;
                }
                else
                {
                    instantScareChance += instantScareChanceIncrement;
                }

                //CHANCE TO PLAY DETECT SOUND
                if (detectSoundTimer <= 0)
                {
                    tweakOutTimer = tweakOutCooldown;
                    detectSoundTimer = detectSoundCooldown;
                    if (Random.Range(0f,100f) < detectSoundChance)
                    {
                        PlayDetectSoundServerRpc();
                        detectSoundChance = detectSoundStartingChance;
                    }
                    else
                    {
                        detectSoundChance += detectSoundChanceIncrement;
                    }
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //IF CHASE EXCEEDS CHASE TIME
            if (chaseTimer <= 0)
            {

                //AND NO ONE IS LOOKING
                if (playersWithLineOfSight.Count < 1)
                {
                    Debug.Log("[SCARECROW]: Chase exceeded chase time, returning to search.");
                    targetPlayer = null;
                    MoveToRandomPosition(escaping: true);
                    currentBehaviourStateIndex = 1;
                    break;
                }
            }

            //IF NO PLAYERS WITHIN RANGE
            if (playersInRange.Count < 1)
            {
                if (moveTimer <= 0 && playersWithLineOfSight.Count == 0)
                {
                    moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                    if (Random.Range(0f,100f) < moveChance + 25)
                    {
                        MoveToTargetPlayer();
                        moveChance = moveStartingChance;
                    }
                    else
                    {
                        moveChance += moveChanceIncrement;
                    }
                }
            }

            //IF MULTIPLE PLAYERS WITHIN RANGE
            else if (playersInRange.Count > 1)
            {
                targetPlayer = null;
                currentBehaviourStateIndex = 3;
            }

            //IF ONE PLAYER WITHIN RANGE
            else if (playersInRange.Count == 1)
            {

                //IF PLAYER IS NOT TARGET PLAYER
                if (targetPlayer != playersInRange[0])
                {
                    targetPlayer = playersInRange[0];
                    Debug.Log($"[SCARECROW]: Chasing {targetPlayer.playerUsername}.");
                    chaseTimer = Random.Range(minChaseTime, maxChaseTime);

                    //CHANCE TO PLAY DETECT SOUND
                    if (detectSoundTimer <= 0)
                    {
                        tweakOutTimer = tweakOutCooldown;
                        detectSoundTimer = detectSoundCooldown;
                        if (Random.Range(0f,100f) < detectSoundChance)
                        {
                            PlayDetectSoundServerRpc();
                            detectSoundChance = detectSoundStartingChance;
                        }
                        else
                        {
                            detectSoundChance += detectSoundChanceIncrement;
                        }
                    }
                }

                //IF PLAYER IS HOLDING WEAPON
                if (playersInRange[0].currentlyHeldObjectServer != null)
                {
                    if (playersInRange[0].currentlyHeldObjectServer.itemProperties.isDefensiveWeapon)
                    {
                        targetPlayer = null;
                        currentBehaviourStateIndex = 3;
                    }
                }

                //ONCE PLAYER IS SET TO TARGET PLAYER
                if (targetPlayer == playersInRange[0] && !invisible)
                {

                    //IF PLAYER IS NOT WITHIN SCARE RANGE
                    if (Vector3.Distance(targetPlayer.transform.position, transform.position) > scareRange)
                    {

                        //IF TARGET PLAYER HAS LOS
                        if (CheckLineOfSightForScarecrow(targetPlayer))
                        {

                            //AND NO ONE ELSE IS LOOKING
                            if (playersWithLineOfSight.Count == 1)
                            {

                                //CHANCE TO TWEAK OUT
                                if (tweakOutTimer <= 0)
                                {
                                    tweakOutTimer = tweakOutCooldown;
                                    if (Random.Range(0f,100f) < tweakOutChance)
                                    {
                                        TweakOutServerRpc((int)targetPlayer.playerClientId);
                                    }
                                    else
                                    {
                                        tweakOutChance += tweakOutChanceIncrement;
                                    }
                                }
                            }
                        }

                        //IF TARGET PLAYER BREAKS LOS
                        else
                        {

                            //AND NO ONE ELSE IS LOOKING
                            if (playersWithLineOfSight.Count == 0)
                            {

                                //CHANCE TO FACE PLAYER
                                if (facePlayerTimer <= 0)
                                {
                                    facePlayerTimer = facePlayerCooldown;
                                    if (Random.Range(0f,100f) < facePlayerChance)
                                    {
                                        FacePosition(targetPlayer.transform.position);
                                        facePlayerChance = facePlayerStartingChance;
                                        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                                    }
                                    else
                                    {
                                        facePlayerChance += facePlayerChanceIncrement;
                                    }
                                }
                            }
                        }
                    }

                    //IF TARGET PLAYER IS WITHIN SCARE RANGE
                    else
                    {

                        //IF TARGET PLAYER HAS LOS
                        if (CheckLineOfSightForScarecrow(targetPlayer))
                        {

                            //AND NO ONE ELSE IS LOOKING
                            if (playersWithLineOfSight.Count == 1)
                            {

                                //AND INSTANT SCARE IS TRUE
                                if (instantScare)
                                {
                                    scarePlayerChance = scarePlayerStartingChance;
                                    scarePlayerTimer = scarePlayerCooldown;
                                    scarePrimed = false;
                                    FacePosition(targetPlayer.transform.position);
                                    ScarePlayerServerRpc((int)targetPlayer.playerClientId);
                                }

                                //AND SCARE HAS BEEN PRIMED (+ PLAYER HAS LOS TO SCARE TRIGGER)
                                if (scarePrimed && targetPlayer.HasLineOfSightToPosition(scareTriggerTransform.position))
                                {
                                    scarePlayerChance = scarePlayerStartingChance;
                                    scarePlayerTimer = scarePlayerCooldown;
                                    FacePosition(targetPlayer.transform.position);
                                    ScarePlayerServerRpc((int)targetPlayer.playerClientId);
                                }

                                //CHANCE TO PLAY DECOY SOUNDS
                                if (decoySoundTimer <= 0)
                                {
                                    decoySoundTimer = decoySoundCooldown;
                                    if (Random.Range(0f,100f) < decoySoundChance)
                                    {
                                        PlayDecoySoundServerRpc();
                                        decoySoundChance = decoySoundStartingChance;
                                    }
                                    else
                                    {
                                        decoySoundChance += decoySoundChanceIncrement;
                                    }
                                }
                            }
                        }

                        //IF TARGET PLAYER BREAKS LOS
                        else
                        {

                            //AND NO ONE ELSE IS LOOKING
                            if (playersWithLineOfSight.Count == 0)
                            {

                                //CHANCE TO TWEAK OUT
                                if (tweakOutTimer <= 0)
                                {
                                    tweakOutTimer = tweakOutCooldown;
                                    if (Random.Range(0f,100f) < tweakOutChance)
                                    {
                                        TweakOutServerRpc((int)targetPlayer.playerClientId);
                                    }
                                    else
                                    {
                                        tweakOutChance += tweakOutChanceIncrement;
                                    }
                                }

                                //CHANCE TO PRIME SCARE
                                if (scarePlayerTimer <= 0)
                                {
                                    scarePlayerTimer = scarePlayerCooldown;
                                    if (Random.Range(0f,100f) < scarePlayerChance)
                                    {
                                        scarePrimed = true;
                                        FacePosition(targetPlayer.transform.position);
                                        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                                    }
                                    else
                                    {
                                        scarePlayerChance += scarePlayerChanceIncrement;
                                    }
                                }

                                //CHANCE TO FACE PLAYER
                                if (facePlayerTimer <= 0)
                                {
                                    facePlayerTimer = facePlayerCooldown;
                                    if (Random.Range(0f,100f) < facePlayerChance)
                                    {
                                        FacePosition(targetPlayer.transform.position);
                                        facePlayerChance = facePlayerStartingChance;
                                        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                                    }
                                    else
                                    {
                                        facePlayerChance += facePlayerChanceIncrement;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            break;



        //ESCAPING
        case 3:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                Debug.Log("[SCARECROW]: Escaping.");
                decoySoundTimer = decoySoundCooldown;

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //IF NO PLAYERS HAVE LOS
            if (playersWithLineOfSight.Count < 1)
            {

                //ESCAPE
                MoveToRandomPosition(escaping: true);
                currentBehaviourStateIndex = 1;
            }

            //IF MORE THAN ONE PLAYER HAS LOS
            if (playersWithLineOfSight.Count > 1)
            {

                //CHANCE TO PLAY DECOY SOUND
                if (decoySoundTimer <= 0)
                {
                    decoySoundTimer = decoySoundCooldown;
                    if (Random.Range(0f,100f) < decoySoundChance)
                    {
                        PlayDecoySoundServerRpc();
                        decoySoundChance = decoySoundStartingChance;
                    }
                    else
                    {
                        decoySoundChance += decoySoundChanceIncrement;
                    }
                }
            }

            break;

        }
    }

    public void MoveToRandomPosition(bool escaping = false)
    {
        int moveAttempts = 0;
        Vector3 newPosition = GetRandomNavMeshPositionNearAINode();
        while(!CheckPositionIsValid(newPosition, escaping))
        {
            moveAttempts++;

            if (moveAttempts < 20)
            {
                newPosition = GetRandomNavMeshPositionNearAINode();
            }
            else
            {
                Debug.Log("[SCARECROW]: Failed to find valid position near AI node, restarting move cooldown.");
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                return;
            }
        }
        changePositionCoroutine = StartCoroutine(ChangePositionWhileInvisible(newPosition, 1.5f));
        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    public void MoveToTargetPlayer()
    {
        int chaseAttempts = 0;
        Vector3 newPosition = GetRandomNavMeshPositionNearPlayer(targetPlayer);
        while (!CheckPositionIsValid(newPosition))
        {
            chaseAttempts++;

            if (chaseAttempts < 5)
            {
                newPosition = GetRandomNavMeshPositionNearPlayer(targetPlayer);
            }
            else
            {
                Debug.Log($"[SCARECROW]: Failed to find valid position near player after {chaseAttempts + 1} tries, restarting move cooldown.");
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                return;
            }
        }
        changePositionCoroutine = StartCoroutine(ChangePositionWhileInvisible(newPosition, 1.5f));
        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    public bool CheckPositionIsValid(Vector3 newPosition, bool escaping = false)
    {
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        float c;

        Debug.Log($"[SCARECROW]: Trying new position: {newPosition}");

        bool inViewOfPlayer = false;
        for (int i = 0; i < players.Length; i++)
        {
            //PREVENT FROM MOVING WHILE IN VIEW OF PLAYER
            if (CheckLineOfSightForScarecrow(players[i]))
            {
                Debug.Log($"[SCARECROW]: Current position in view of {players[i].playerUsername}, did not move.");
                return false;
            }

            //PREVENT FROM MOVING TO NEW POSITION IN VIEW OF PLAYER
            if (lineOfSightTriggers.Length > 0)
            {
                for (int j = 0; j < lineOfSightTriggers.Length; j++)
                {
                    if (players[i].HasLineOfSightToPosition(newPosition + lineOfSightTriggers[j].localPosition, range: 100))
                    {
                        Debug.Log($"[SCARECROW]: LOS trigger visible to {players[i].playerUsername} in new position, did not move.");
                        inViewOfPlayer = true;
                        break;
                    }
                }
            }

            //PREVENT FROM MOVING NEAR PLAYERS WHEN ESCAPING
            if (escaping && Vector3.Distance(newPosition, players[i].transform.position) < 10f)
            {
                return false;
            }
        }

        if (inViewOfPlayer)
        {
            c = Random.Range(0f,100f);
            {
                if (c > 50f)
                {
                    return false;
                }
            }
        }

        bool onInvalidTerrain = false;
        RaycastHit hitInfo;
        Physics.Raycast(newPosition, Vector3.down, out hitInfo, StartOfRound.Instance.collidersAndRoomMaskAndDefault);
        for (int i = 0; i < invalidTerrainTags.Length; i++)
        {
            if (hitInfo.collider.gameObject.tag == invalidTerrainTags[i])
            {
                Debug.Log($"[SCARECROW]: New position on {invalidTerrainTags[i]}.");
                onInvalidTerrain = true;
            }
        }

        if (onInvalidTerrain)
        {
            Debug.Log("[SCARECROW]: Attempting to move to invalid terrain.");
            c = Random.Range(0f,100f);
            if (c > 90f)
            {
                Debug.Log("[SCARECROW]: Did not move.");
                return false;
            }
        }

        if (Vector3.Angle(Vector3.up, hitInfo.normal) > 35f)
        {
            Debug.Log("[SCARECROW]: New position on too steep of ground, did not move.");
            return false;
        }

        Collider[] headCollisions = Physics.OverlapSphere(newPosition + scareTriggerTransform.localPosition, 0.1f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);
        if (headCollisions.Length > 0)
        {
            Debug.Log("[SCARECROW]: New position obscures head, did not move.");
            return false;
        }

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            if (newPosition.y < floodWeather.gameObject.transform.position.y)
            {
                Debug.Log($"[SCARECROW]: New position y ({newPosition.y}) is under current flood level ({floodWeather.gameObject.transform.position.y}), did not move.");
                return false;
            }
        }

        GameObject[] spawnDenialPoints = GameObject.FindGameObjectsWithTag("SpawnDenialPoint");
        c = Random.Range(0f,100f);
        for (int i = 0; i < spawnDenialPoints.Length; i++)
        {
            if (Vector3.Distance(newPosition, spawnDenialPoints[i].transform.position) < 30)
            {
                if (c < 80f)
                {
                    Debug.Log("[SCARECROW]: New position too close to spawn denial point, did not move.");
                    return false;
                }
            }
        }

        return true;
    }

    public PlayerControllerB NearestPlayer()
    {
        PlayerControllerB nearestPlayer = null;
        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
            if (nearestPlayer == null)
            {
                nearestPlayer = player;
            }
            else if (Vector3.Distance(player.transform.position, transform.position) < Vector3.Distance(nearestPlayer.transform.position, transform.position))
            {
                nearestPlayer = player;
            }
        }
        if (nearestPlayer != null)
        {
            return nearestPlayer;
        }
        else
        {
            return null;
        }
    }

    private IEnumerator ChangePositionWhileInvisible(Vector3 position, float time)
    {
        SetInvisibleServerRpc(true);
        invisible = true;
        transform.position = position;
        Debug.Log("Scarecrow moved.");
        // currentBehaviourStateIndex = 0;
        yield return new WaitForSeconds(time);
        changePositionCoroutine = null;
        // SetInvisibleServerRpc(false);

    }

    [ServerRpc(RequireOwnership = false)]
    private void SetInvisibleServerRpc(bool enabled)
    {
        SetInvisibleClientRpc(enabled);
    }

    [ClientRpc]
    private void SetInvisibleClientRpc(bool enabled)
    {
        if (enabled == true)
        {
            Debug.Log("Scarecrow set invisible.");
            invisible = true;
            EnableEnemyMesh(false);
            enemyCollider.enabled = false;
            scanNode.gameObject.SetActive(false);
        }
        else
        {
            Debug.Log("Scarecrow set visible.");
            invisible = false;
            EnableEnemyMesh(true);
            enemyCollider.enabled = true;
            scanNode.gameObject.SetActive(true);
        }
    }

    public Vector3 GetRandomNavMeshPositionNearAINode(float radius = 20f)
    {
        int nodeSelected = Random.Range(0, nodes.Count);
        Vector3 nodePosition = nodes[nodeSelected].transform.position;
        Vector3 newPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(nodePosition, radius);
        // float furthestRotation = RoundManager.Instance.YRotationThatFacesTheFarthestFromPosition(newPosition, 2f);
        // Transform tempTransform = transform;
        // tempTransform.position = newPosition;
        // tempTransform.eulerAngles = new Vector3(0f, furthestRotation, 0f);
        // newPosition += tempTransform.forward * Random.Range(1f, 2f);
        return newPosition;
    }

    public Vector3 GetRandomNavMeshPositionNearPlayer(PlayerControllerB player, float radius = 10f)
    {
        Vector3 newPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius);
        return newPosition;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ScarePlayerServerRpc(int playerId)
    {
        int scareSound = Random.Range(0, scareSounds.Length);
        ScarePlayerClientRpc(playerId, scareSound);
    }

    [ClientRpc]
    public void ScarePlayerClientRpc(int playerId, int scareSound)
    {
        ScarePlayer(playerId, scareSound);
    }

    public void ScarePlayer(int playerId, int scareSound)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];

        FacePosition(player.transform.position);
        AudioClip clip = scareSounds[scareSound];
        scareAudio.PlayOneShot(clip);
        RoundManager.Instance.PlayAudibleNoise(transform.position, noiseRange, 1, 0, false, -1);
        scarePrimed = false;
        creatureAnimator.SetTrigger("ScarePlayer");

        if (GameNetworkManager.Instance.localPlayerController == player)
		{
            player.insanityLevel += player.maxInsanityLevel * 0.2f;
            player.JumpToFearLevel(0.5f);
        }

        Debug.Log($"Scarecrow scared player {player.playerUsername}.");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TweakOutServerRpc(int playerId)
    {
        int tweakSound = Random.Range(0, tweakOutSounds.Length);
        TweakOutClientRpc(playerId, tweakSound);
    }

    [ClientRpc]
    public void TweakOutClientRpc(int playerId, int tweakSound)
    {
        TweakOut(playerId, tweakSound);
    }

    public void TweakOut(int playerId, int tweakSound)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];

        Debug.Log("Scarecrow tweaked out!");
        AudioClip clip = tweakOutSounds[tweakSound];
        tweakOutAudio.PlayOneShot(clip);
        creatureAnimator.SetTrigger("TweakOut");

        if (GameNetworkManager.Instance.localPlayerController == player)
        {
            player.insanityLevel += player.maxInsanityLevel * 0.1f;
        }
    }

    public void FacePosition(Vector3 lookPosition)
    {
        Transform tempTransform = base.transform;
        tempTransform.LookAt(lookPosition);
        tempTransform.eulerAngles = new Vector3(0f, tempTransform.eulerAngles.y, 0f);
        base.transform.eulerAngles = tempTransform.eulerAngles;
    }

    [ServerRpc]
    public void PlayDetectSoundServerRpc()
    {
        int detectSound = Random.Range(0, detectSounds.Length);
        PlayDetectSoundClientRpc(detectSound);
    }

    [ClientRpc]
    public void PlayDetectSoundClientRpc(int detectSound)
    {
        detectAudio.PlayOneShot(detectSounds[detectSound]);
    }

    [ServerRpc]
    public void PlayDecoySoundServerRpc()
    {
        int decoySound = Random.Range(0, decoySounds.Length);

        Vector3 meanVector = Vector3.zero;
        for (int i = 0; i < playersInRange.Count; i++)
        {
            meanVector += playersInRange[i].transform.position;
        }
        meanVector /= playersInRange.Count;

        Vector3 direction = Vector3.Normalize(meanVector - transform.position);
        Vector3 decoyPosition = meanVector + direction * 10f;
        decoyPosition = RoundManager.Instance.GetRandomPositionInRadius(decoyPosition, 0f, 8f);

        PlayDecoySoundClientRpc(decoySound, decoyPosition);
    }

    [ClientRpc]
    public void PlayDecoySoundClientRpc(int decoySound, Vector3 soundPosition)
    {
        decoyAudio.transform.position = soundPosition;
        decoyAudio.PlayOneShot(decoySounds[decoySound]);
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
            Debug.Log($"Target player set to {targetPlayer.playerUsername}.");
        }
        enemyHP -= force;
        if (enemyHP <= 0 && !isEnemyDead)
        {
            creatureAnimator.SetTrigger("Die");
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
            creatureAnimator.SetTrigger("Explode");
        }
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
                creatureAnimator.SetTrigger("Electrocute");
            }
            else
            {
                //BEHAVIOUR WHEN STUNNED BY FLASH
            }
        }
    }

    public override void KillEnemy(bool destroy = false)
    {
        Debug.Log("Called KillEnemy!");
        IncreaseEnemySpawnRate();
        base.KillEnemy(destroy);
    }

    public void DropItem(bool zapped = false)
    {
        if (base.IsOwner)
        {
            DropItemServerRpc(zapped);
        }
    }

    [ServerRpc]
    public void DropItemServerRpc(bool zapped = false)
    {
        Debug.Log("Called DropItemServerRpc!");
        GameObject prefab;
        if (zapped)
        {
            prefab = zapItemPrefab;
        }
        else
        {
            prefab = dropItemPrefab;
        }
        GameObject dropObject = Instantiate(prefab, dropItemTransform.position, dropItemTransform.rotation, RoundManager.Instance.spawnedScrapContainer);
        dropObject.GetComponent<NetworkObject>().Spawn();
        NetworkObjectReference dropObjectRef = dropObject.GetComponent<NetworkObject>();

        DropItemClientRpc(dropObjectRef, currentValue, rotAmount);
    }

    [ClientRpc]
    public void DropItemClientRpc(NetworkObjectReference dropObjectRef, int value = 0, float rot = 0f)
    {
        NetworkObject dropObjectNetworkObject = dropObjectRef;
        GameObject dropObject = dropObjectNetworkObject.gameObject;
        GrabbableObject gObject = dropObject.GetComponent<GrabbableObject>();
        AnimatedItem aObject = dropObject.GetComponent<AnimatedItem>();

        if (aObject != null)
        {
            if (aObject.itemProperties.itemName == "Rotten Pumpkin")
            {
                aObject.SetScrapValue(value);
                aObject.itemAnimator.SetFloat("rot", rot);
            }
        }
        else if (gObject != null)
        {
            gObject.SetScrapValue(5);
        }
    }

    private void IncreaseEnemySpawnRate()
    {
        Debug.Log($"Original minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        Debug.Log($"Original max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");
        RoundManager.Instance.minOutsideEnemiesToSpawn += enemySpawnIncrease;
        RoundManager.Instance.currentMaxOutsidePower += enemyPowerIncrease;
        Debug.Log($"Increased minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        Debug.Log($"Increased max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");

        PlayWarningSoundServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayWarningSoundServerRpc()
    {
        Vector2 soundOffset = Random.insideUnitCircle.normalized * 500f;
        Vector3 soundPosition = transform.position + new Vector3(soundOffset.x, 0f, soundOffset.y);
        int clip;

        if (dangerValue < 33)
        {
            clip = Random.Range(0,warningSoundsLow.Length);
        }
        else if (dangerValue < 66)
        {
            clip = Random.Range(0,warningSoundsMedium.Length);
        }
        else
        {
            clip = Random.Range(0,warningSoundsHigh.Length);
        }
        
        PlayWarningSoundClientRpc(soundPosition, clip);
    }

    [ClientRpc]
    private void PlayWarningSoundClientRpc(Vector3 soundPosition, int clip)
    {
        warningAudio.transform.position = soundPosition;
        
        if (dangerValue < 33)
        {
            warningAudio.PlayOneShot(warningSoundsLow[clip]);
        }
        else if (dangerValue < 66)
        {
            warningAudio.PlayOneShot(warningSoundsMedium[clip]);
        }
        else
        {
            warningAudio.PlayOneShot(warningSoundsHigh[clip]);
        }
    }

    public int RemapInt(float value, float min1, float max1, float min2, float max2)
    {
        float m = (value - min1) / (max1 - min1) * (max2 - min2) + min2;
        return Mathf.RoundToInt(m);
    }
}