using System.Collections.Generic;
using System.Collections;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using UnityEngine.AI;
using UnityEngine.Audio;

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

    public Collider enemyCollider;

    [Space(10f)]
    [Header("Wind Levels On Moons")]
    public string[] noWindMoons;

    public string[] lightWindMoons;

    public string[] heavyWindMoons;

    [Space(10f)]
    [Header("Nodes On Moons")]
    public bool useScarecrowNodes;

    public bool addScarecrowNodesToOutsideNodes;

    public GameObject[] nodesList;

    [Space(10f)]
    [Header("Chances & Cooldowns")]

    [Space(5f)]
    public float minSearchTime = 100f;
    
    public float maxSearchTime = 300f;

    private float searchTimer;

    public float startOfDayCooldownMultiplier = 1f;

    public float endOfDayCooldownMultiplier = 2f;

    public float startOfDayChanceMultiplier = 1f;

    public float endOfDayChanceMultiplier = 2f;

    public bool multiplyChances = false;

    public bool multiplyCooldowns = true;

    private float cooldownMultiplier;

    private float chanceMultiplier;

    private List<float> timers;

    private List<float> chances;

    [Space(5f)]
    [Range(0f, 100f)]
    public float chaseStartingChance = 65f;

    private float chaseChance;

    public float chaseChanceIncrement = 5f;

    [Space(5f)]
    public int minChaseMoves = 3;

    public int maxChaseMoves = 10;

    private int chaseMoves;

    public float chaseMoveAddedChance = 20f;

    public float chaseMoveCooldownMultiplier = 0.5f;

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
    public AudioSource spottedAudio;

    public AudioClip spottedLow;

    public AudioClip spottedMed;

    public AudioClip spottedHigh;

    public float initialSpottedTimer = 1f;

    public int spottedDistance = 45;

    public float spottedAngle = 8f;

    public bool useSaveFileForMusic = false;

    public string saveFileString = "PlayedScarecrowMusic";

    public AudioMixerSnapshot spottedSnapshot;

    public AudioMixer scarecrowMixer;

    private float spottedTimer;

    private bool spottedLocally;

    private bool spottedOnce;

    private Coroutine PlayMusicLocallyCoroutine;

    [Space(5f)]
    public AudioSource rumbleAudio;

    [Space(5f)]
    public AudioSource decoyAudio;

    public AudioClip[] decoySounds;

    public AudioClip[] decoySoundsDesperate;

    private bool decoyAudioPlaying;
    
    private bool desperate;

    [Space(5f)]
    public Volume screenShakeVolume;

    public GameObject screenShakeParticles;

    public Transform screenShakeTransform;

    [Space(5f)]
    public bool useScanNode;

    public ScanNodeProperties scanNode;

    public TerminalNode newScarecrowNode;

	public Terminal currentTerminal;

    private bool scanNodeActive;

    public List<GameObject> debugNodes;

    private bool hardMode = false;

    private bool mediumMode = false;

    private float hardModeTimer;

    private float escapeRange = 25f;

    private PlayerControllerB? attackerPlayer;

    public class IterateJob(string name, float value, Coroutine coroutine, bool done)
    {
        public string Name = name;

        public float Value = value;

        public Coroutine Coroutine = coroutine;

        public bool Done = done;
    }

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
        }

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            floodWeather = FindObjectOfType<FloodWeather>();
        }

        currentTerminal = FindObjectOfType<Terminal>();
        currentTerminal.SyncTerminalValuesServerRpc();

        base.Start();

        SetWindLevel();
        SetSpawnPoints();
        SetScanNode();

        playersInRange = new List<PlayerControllerB>();
        playersWithLineOfSight = new List<PlayerControllerB>();

        tweakOutChance = tweakOutStartingChance;
        facePlayerChance = facePlayerStartingChance;
        detectSoundChance = detectSoundStartingChance;
        moveChance = moveStartingChance;
        scarePlayerChance = scarePlayerStartingChance;
        decoySoundChance = decoySoundStartingChance;
        chaseChance = chaseStartingChance;

        if (IsOwner)
        {
            MoveToRandomPosition();
        }

        Debug.Log("[SCARECROW]: Spawned!");
        Debug.Log($"[SCARECROW]: Danger value: {dangerValue}");
        Debug.Log($"[SCARECROW]: Minimum enemy spawn increase: {enemySpawnIncrease}");
        Debug.Log($"[SCARECROW]: Max enemy power increase: {enemyPowerIncrease}");
        Debug.Log($"[SCARECROW]: Start value: {startValue}");
        Debug.Log($"[SCARECROW]: End value: {endValue}");
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

    public void SetSpawnPoints()
    {
        spawnDenialPoints = GameObject.FindGameObjectsWithTag("SpawnDenialPoint");

        if (useScarecrowNodes)
        {
            for (int i = 0; i < nodesList.Length; i++)
            {
                if (StartOfRound.Instance.currentLevel.levelID != i)
                {
                    continue;
                }
                else
                {
                    GameObject nodesParent = GameObject.Find("/NodesAndPoints");
                    GameObject nodesTransform = Instantiate(nodesList[i], nodesParent.transform.position, Quaternion.identity, nodesParent.transform);
                    nodesTransform.transform.localEulerAngles = new Vector3(0, 0, 0);
                    List<GameObject> nodesForPlanet = new List<GameObject>();
                    foreach (Transform child in nodesTransform.transform)
                    {
                        nodesForPlanet.Add(child.gameObject);
                    }
                    nodes = nodesForPlanet;
                    Debug.Log($"[SCARECROW]: Current level {StartOfRound.Instance.currentLevel.PlanetName} has an ID of {StartOfRound.Instance.currentLevel.levelID}, which is in nodes list. Using nodes list for scarecrow spawn points.");
                    if (addScarecrowNodesToOutsideNodes)
                    {
                        List<GameObject> outsideNodes = GameObject.FindGameObjectsWithTag("OutsideAINode").ToList();
                        nodes.AddRange(outsideNodes);
                        Debug.Log($"[SCARECROW]: Adding outside AI nodes to scarecrow spawn points.");
                    }
                    return;
                }
            }
        }
        List<GameObject> outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode").ToList<GameObject>();
        nodes = outsideAINodes;
        Debug.Log($"[SCARECROW]: Current level {StartOfRound.Instance.currentLevel.PlanetName} has an ID of {StartOfRound.Instance.currentLevel.levelID}, which is not in nodes list, using outside AI nodes instead.");
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
        StartWaitForMusicServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
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

    public void SetScanNode(bool use = false)
    {
        if (!use)
        {
            scanNode.creatureScanID = -1;
            scanNodeActive = false;
        }
        else
        {
            for (int i = 0; i < currentTerminal.enemyFiles.Count; i++)
            {
                if (currentTerminal.enemyFiles[i].creatureName != "Scarecrow")
                {
                    continue;
                }
                else
                {
                    scanNode.creatureScanID = currentTerminal.enemyFiles[i].creatureFileID;
                    scanNodeActive = true;
                    break;
                }
            }
        }
    }

    public void GiveRandomTiltAndSync(int clientWhoSentRpc)
    {
        Vector3 randomTilt = new Vector3(Random.Range(-3f,3f), 0f, Random.Range(-6f,6f));
        StartCoroutine(GiveRandomTiltLerp(randomTilt, 0.1f));
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
            StartCoroutine(GiveRandomTiltLerp(randomTilt, 0.1f));
        }
    }

    public IEnumerator GiveRandomTiltLerp(Vector3 randomTilt, float length)
    {
        float timeElapsed = 0f;
        float duration = length;
        float tiltX;
        float tiltZ;
        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            tiltX = Mathf.LerpAngle(meshContainer.localEulerAngles.x, randomTilt.x, timeElapsed / duration);
            tiltZ = Mathf.LerpAngle(meshContainer.localEulerAngles.z, randomTilt.z, timeElapsed / duration);
            meshContainer.localEulerAngles = new Vector3(tiltX, 0f, tiltZ);
            yield return null;
        }
        meshContainer.localEulerAngles = randomTilt;
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

    public void CheckForHardMode()
    {
        if (!hardMode)
        {
            GrabbableObject[] itemsList = FindObjectsOfType<GrabbableObject>();
            for (int i = 0; i < itemsList.Length; i++)
            {
                if (itemsList[i].itemProperties.itemName != "Jetpack")
                {
                    continue;
                }
                else
                {
                    hardMode = true;
                    moveStartingChance *= 1.4f;
                    scarePlayerStartingChance *= 1.1f;
                    decoySoundStartingChance *= 1.2f;
                    minMoveCooldown *= 0.75f;
                    maxMoveCooldown *= 0.75f;
                    escapeRange = 55f;
                    return;
                }
            }
            if (!mediumMode)
            {
                if (FindObjectOfType<VehicleController>() != null)
                {
                    mediumMode = true;
                    moveStartingChance *= 1.2f;
                    decoySoundStartingChance *= 1.1f;
                    minMoveCooldown *= 0.85f;
                    maxMoveCooldown *= 0.85f;
                    escapeRange = 40f;
                    return;
                }
            }
        }
        return;
    }

    public void UpdateTimers()
    {
        timers = [searchTimer, moveTimer, facePlayerTimer, scarePlayerTimer, detectSoundTimer, tweakOutTimer, decoySoundTimer, hardModeTimer];
        for (int i = 0; i < timers.Count; i++)
        {
            if (timers[i] > 0)
            {
                timers[i] = multiplyCooldowns ? timers[i] -= Time.deltaTime * cooldownMultiplier : timers[i] -= Time.deltaTime;
            }
        }
        searchTimer = timers[0];
        moveTimer = timers[1];
        facePlayerTimer = timers[2];
        scarePlayerTimer = timers[3];
        detectSoundTimer = timers[4];
        tweakOutTimer = timers[5];
        decoySoundTimer = timers[6];
        hardModeTimer = timers[7];

        if (multiplyChances)
        {
            chances = [tweakOutStartingChance, facePlayerStartingChance, detectSoundStartingChance, moveStartingChance, scarePlayerStartingChance, decoySoundStartingChance, chaseStartingChance];
            for (int i = 0; i < chances.Count; i++)
            {
                chances[i] = chances[i] * chanceMultiplier;
            }
            tweakOutStartingChance = chances[0];
            facePlayerStartingChance = chances[1];
            detectSoundStartingChance = chances[2];
            moveStartingChance = chances[3];
            scarePlayerStartingChance = chances[4];
            decoySoundStartingChance = chances[5];
            chaseStartingChance = chances[6];
        }

        if (hardModeTimer <= 0)
        {
            CheckForHardMode();
            hardModeTimer = 30f;
        }
    }

    public override void Update()
    {
        float dayProgress = RoundManager.Instance.timeScript.normalizedTimeOfDay;
        currentValue = RemapInt(dayProgress, 0f, 1f, startValue, endValue);
        rotAmount = dayProgress;
        creatureAnimator.SetFloat("rot", rotAmount);
        cooldownMultiplier = RemapFloat(dayProgress, 0f, 1f, startOfDayCooldownMultiplier, endOfDayCooldownMultiplier);
        chanceMultiplier = RemapFloat(dayProgress, 0f, 1f, startOfDayChanceMultiplier, endOfDayChanceMultiplier);

        UpdateTimers();

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
                    Debug.Log("[SCARECROW]: Played audible sound!");
                }
            }
        }

        if (!base.IsOwner)
        {
            SetClientCalculatingAI(enable: false);
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

    public void AddPlayerToLineOfSightAndSync(int playerId, bool adding)
    {
        AddPlayerToLineOfSight(playerId, adding);
        AddPlayerToLineOfSightServerRpc(playerId, adding);
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddPlayerToLineOfSightServerRpc(int playerId, bool adding)
    {
        AddPlayerToLineOfSightClientRpc(playerId, adding);
    }

    [ClientRpc]
    public void AddPlayerToLineOfSightClientRpc(int playerId, bool adding)
    {
        if (!IsOwner)
        {
            AddPlayerToLineOfSight(playerId, adding);
        }
    }

    public void AddPlayerToLineOfSight(int playerId, bool adding)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];
        if (adding)
        {
            Debug.Log($"[SCARECROW]: Player {StartOfRound.Instance.allPlayerScripts[playerId].playerUsername} has line of sight.");
            playersWithLineOfSight.Add(player);
        }
        else
        {
            Debug.Log($"[SCARECROW]: Player {StartOfRound.Instance.allPlayerScripts[playerId].playerUsername} lost line of sight.");
            playersWithLineOfSight.Remove(player);
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (isEnemyDead)
        {
            return;
        }

        //CHECK PLAYERS FOR LOS
        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
            if (CheckLineOfSightForScarecrow(player) && !playersWithLineOfSight.Contains(player))
            {
                AddPlayerToLineOfSightAndSync((int)player.playerClientId, true);
            }
            if (!CheckLineOfSightForScarecrow(player) && playersWithLineOfSight.Contains(player))
            {
                AddPlayerToLineOfSightAndSync((int)player.playerClientId, false);
            }
        }

        //PAUSE BEHAVIOURS WHILE INVISIBLE
        if (invisible)
        {
            if (changePositionCoroutine == null && playersWithLineOfSight.Count < 1)
            {
                invisible = false;
                PlayerControllerB nearestPlayer = NearestPlayer();
                if (nearestPlayer != null)
                {
                    FacePosition(NearestPlayer().transform.position);
                }
                SetInvisibleServerRpc(false);
            }
            else
            {
                return;
            }
        }

        //DETECT PLAYERS WITHIN RANGE
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
                Debug.Log("[SCARECROW]: Inactive.");
                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            if (RoundManager.Instance.timeScript.normalizedTimeOfDay > normalizedTimeInDayToBecomeActive && !isEnemyDead && !invisible)
            {
                SwitchToBehaviourState(1);
            }

            break;



        //SEARCHING
        case 1:

            desperate = false;

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

                if (Random.Range(0f,100f) < chaseChance)
                {
                    chaseChance = chaseStartingChance;

                    Debug.Log("[SCARECROW]: Checking for valid players to target.");
                    for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                    {
                        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
                        if (targetPlayer == null)
                        {
                            if (player.isPlayerControlled && !player.isInsideFactory && !player.isPlayerDead && player.isPlayerAlone && (player != attackerPlayer))
                            {
                                targetPlayer = player;
                                continue;
                            }
                        }
                        else if (player.isPlayerControlled && !player.isInsideFactory && !player.isPlayerDead && player.isPlayerAlone && (player != attackerPlayer))
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
                        SwitchToBehaviourState(2);
                    }
                    else
                    {
                        Debug.Log("[SCARECROW]: No valid players, restarting search.");
                        searchTimer = Random.Range(minSearchTime, maxSearchTime);
                    }
                }

                else
                {
                    Debug.Log("[SCARECROW]: Restarting search.");
                    chaseChance += chaseChanceIncrement;
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
                if (attackerPlayer != null && playersInRange[0] == attackerPlayer)
                {
                    SwitchToBehaviourState(3);
                }
                targetPlayer = playersInRange[0];
                SwitchOwnershipAndSyncStatesServerRpc(2, targetPlayer.actualClientId);
            }

            //IF MULTIPLE PLAYERS WITHIN RANGE
            else if (playersInRange.Count > 1)
            {
                SwitchToBehaviourState(3);
            }

            break;



        //CHASING
        case 2:

            bool instantScare = false;

            //IF TARGET PLAYER IS NULL
            if (targetPlayer == null)
            {
                Debug.LogError("[SCARECROW]: Target player is null, Returning to search.");
                SwitchToBehaviourState(1);
                break;
            }

            //IF TARGET PLAYER IS DEAD OR INACCESSIBLE
            if (!targetPlayer.isPlayerControlled || targetPlayer.isPlayerDead || targetPlayer.isInsideFactory)
            {
                Debug.Log("[SCARECROW]: Target player dead or inaccessible, escaping.");
                targetPlayer = null;
                SwitchToBehaviourState(3);
                break;
            }

            //WHEN FIRST ENTERING CHASE STATE
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                chaseMoves = Random.Range(minChaseMoves, maxChaseMoves);
                Debug.Log($"[SCARECROW]: Chasing {targetPlayer.playerUsername} with {chaseMoves} moves.");
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

            //IF NO PLAYERS WITHIN RANGE
            if (playersInRange.Count < 1)
            {
                if (moveTimer <= 0 && playersWithLineOfSight.Count == 0)
                {
                    moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown) * chaseMoveCooldownMultiplier;
                    if (Random.Range(0f,100f) < moveChance + chaseMoveAddedChance)
                    {

                        //IF OUT OF CHASE MOVES
                        if (chaseMoves <= 0)
                        {
                            Debug.Log("[SCARECROW]: Out of chase moves, returning to search.");
                            targetPlayer = null;
                            MoveToRandomPosition(escaping: true);
                            SwitchToBehaviourState(1);
                            break;
                        }

                        MoveToTargetPlayer();
                        moveChance = moveStartingChance;
                        chaseMoves--;
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
                SwitchToBehaviourState(3);
            }

            //IF ONE PLAYER WITHIN RANGE
            else if (playersInRange.Count == 1)
            {

                //IF PLAYER IS NOT TARGET PLAYER
                if (targetPlayer != playersInRange[0])
                {
                    targetPlayer = playersInRange[0];
                    chaseMoves = Random.Range(minChaseMoves, maxChaseMoves);
                    Debug.Log($"[SCARECROW]: Chasing {targetPlayer.playerUsername} with {chaseMoves} moves.");
                    scarePrimed = false;
                    SwitchOwnershipAndSyncStatesServerRpc(2, targetPlayer.actualClientId);

                    //CHANCE TO PLAY DETECT SOUND (WHILE PLAYER IS OR ISNT LOOKING)
                    if (detectSoundTimer <= 0 && playersWithLineOfSight.Count <= 1)
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
                    if (playersInRange[0].currentlyHeldObjectServer.itemProperties.isDefensiveWeapon || playersInRange[0].currentlyHeldObjectServer.itemProperties.itemName == "Easter egg")
                    {
                        targetPlayer = null;
                        SwitchToBehaviourState(3);
                    }
                }

                //ONCE PLAYER IS SET TO TARGET PLAYER
                if (targetPlayer == playersInRange[0] && !invisible)
                {

                    //IF PLAYER IS NOT WITHIN SCARE RANGE
                    if (Vector3.Distance(targetPlayer.transform.position, transform.position) > scareRange)
                    {

                        //SETTING SCAN NODE TERMINAL ENTRY INACCESSIBLE
                        if (scanNodeActive)
                        {
                            SetScanNode(false);
                        }

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
                                        StartCoroutine(FacePositionLerp(targetPlayer.transform.position, 0.1f));
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

                        //SETTING SCAN NODE TERMINAL ENTRY ACCESSIBLE
                        if (!scanNodeActive && playersWithLineOfSight.Count == 1)
                        {
                            SetScanNode(true);
                        }
                        else if ((scanNodeActive && playersWithLineOfSight.Count > 1) || (scanNodeActive && playersWithLineOfSight.Count == 0))
                        {
                            SetScanNode(false);
                        }

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
                                    ScarePlayerServerRpc((int)targetPlayer.playerClientId);
                                    decoySoundTimer = decoySoundCooldown;
                                }

                                //AND SCARE HAS BEEN PRIMED (+ PLAYER HAS LOS TO SCARE TRIGGER)
                                if (scarePrimed && targetPlayer.HasLineOfSightToPosition(scareTriggerTransform.position))
                                {
                                    scarePlayerChance = scarePlayerStartingChance;
                                    scarePlayerTimer = scarePlayerCooldown;
                                    ScarePlayerServerRpc((int)targetPlayer.playerClientId);
                                    decoySoundTimer = decoySoundCooldown;
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
                                        StartCoroutine(FacePositionLerp(targetPlayer.transform.position, 0.1f));
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
                                        StartCoroutine(FacePositionLerp(targetPlayer.transform.position, 0.1f));
                                        facePlayerChance = facePlayerStartingChance;
                                        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                                    }
                                    else
                                    {
                                        facePlayerChance += facePlayerChanceIncrement;
                                    }
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

                //SETTING SCAN NODE TERMINAL ENTRY INACCESSIBLE
                if (scanNodeActive)
                {
                    SetScanNode(false);
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //IF NO PLAYERS HAVE LOS
            if (playersWithLineOfSight.Count < 1)
            {

                //ESCAPE
                MoveToRandomPosition(escaping: true);
                SwitchToBehaviourState(1);
            }

            //IF ANYONE HAS A WEAPON
            for (int i = 0; i < playersInRange.Count; i++)
            {
                if (playersInRange[i].currentlyHeldObjectServer != null)
                {
                    if (!desperate && (playersInRange[i].currentlyHeldObjectServer.itemProperties.isDefensiveWeapon || playersInRange[i].currentlyHeldObjectServer.itemProperties.itemName == "Easter egg"))
                    {
                        decoySoundTimer = 0f;
                        desperate = true;
                        Debug.Log("[SCARECROW]: Player with weapon in range, desperate set to true.");
                        break;
                    }
                }
            }

            //IF ONE PLAYER HAS LOS
            if (playersWithLineOfSight.Count == 1 && !desperate)
            {

                //CHANCE TO TWEAK OUT
                if (tweakOutTimer <= 0)
                {
                    tweakOutTimer = tweakOutCooldown;
                    if (Random.Range(0f,100f) < tweakOutChance)
                    {
                        TweakOutServerRpc((int)playersWithLineOfSight[0].playerClientId);
                    }
                    else
                    {
                        tweakOutChance += tweakOutChanceIncrement;
                    }
                }
            }

            //IF ONE OR MORE PLAYERS HAVE LOS
            if (playersWithLineOfSight.Count >= 1)
            {

                //CHANCE TO PLAY DECOY SOUND
                if (decoySoundTimer <= 0)
                {
                    decoySoundTimer = decoySoundCooldown * 0.8f;
                    if (Random.Range(0f,100f) < decoySoundChance + (desperate ? 30 : 15))
                    {
                        PlayDecoySoundServerRpc(desperate);
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
            if (moveAttempts < 20)
            {
                newPosition = GetRandomNavMeshPositionNearAINode();
                moveAttempts++;
            }
            else
            {
                Debug.Log($"[SCARECROW]: Failed to find valid position near AI node after {moveAttempts} tries, restarting move cooldown.");
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                return;
            }
        }
        changePositionCoroutine = StartCoroutine(ChangePositionWhileInvisible(newPosition, 1.5f));
        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    public async void MoveToTargetPlayer()
    {
        int chaseAttempts = 0;
        Vector3 newPosition = GetRandomNavMeshPositionNearPlayer(targetPlayer);
        while (!CheckPositionIsValid(newPosition))
        {
            if (chaseAttempts < 5)
            {
                newPosition = GetRandomNavMeshPositionNearPlayer(targetPlayer);
                chaseAttempts++;
            }
            else
            {
                MoveToRandomPosition(escaping: true);

                await Task.Yield();

                Debug.Log($"[SCARECROW]: Failed to find valid position near player after {chaseAttempts} tries, restarting move cooldown.");
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown) * 0.5f;
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

            //PREVENT FROM MOVING TO NEW POSITION IN VIEW OF PLAYER
            if (lineOfSightTriggers.Length > 0)
            {
                for (int j = 0; j < lineOfSightTriggers.Length; j++)
                {
                    if (players[i].HasLineOfSightToPosition(newPosition + lineOfSightTriggers[j].localPosition, range: 100))
                    {
                        Debug.Log($"[SCARECROW]: LOS trigger visible to {players[i].playerUsername} in new position.");
                        inViewOfPlayer = true;
                        break;
                    }
                }
            }

            //PREVENT FROM MOVING NEAR PLAYERS WHEN ESCAPING
            if (escaping && Vector3.Distance(newPosition, players[i].transform.position) < escapeRange)
            {
                Debug.Log($"[SCARECROW]: Position too close to players while escaping, Did not move.");
                return false;
            }
        }

        if (inViewOfPlayer)
        {
            c = Random.Range(0f,100f);
            {
                if (c > 50f)
                {
                    Debug.Log($"[SCARECROW]: Did not move.");
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

        c = Random.Range(0f,100f);
        for (int i = 0; i < spawnDenialPoints.Length; i++)
        {
            if (Vector3.Distance(newPosition, spawnDenialPoints[i].transform.position) < 30)
            {
                Debug.Log($"[SCARECROW]: New position too close to spawn denial point.");
                if (c < 80f)
                {
                    Debug.Log("[SCARECROW]: Did not move.");
                    return false;
                }
            }
        }

        Collider[] bodyCollisions = Physics.OverlapCapsule(newPosition + Vector3.up * 1f, newPosition + Vector3.up * 2.5f, 0.1f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);
        if (bodyCollisions.Length > 0)
        {
            Debug.Log("[SCARECROW]: New position obscures body, did not move.");
            return false;
        }

        // This is for when he is chasing and ends up somewhere unwanted like half inside the wall of a building or half inside the floor of a building or on the roof of a building because an inaccessible automatically baked navmesh island was chosen for the new position
        if (!escaping && targetPlayer != null)
        {
            bool pathValid = NavMesh.CalculatePath(newPosition, targetPlayer.transform.position, agent.areaMask, path1);
            if (!pathValid)
            {
                Debug.Log("[SCARECROW]: Path from position to player invalid, did not move.");
                CreateDebugNode(newPosition, "INVALID newPosition", 1);
                return false;
            }
            if (pathValid && path1.status == NavMeshPathStatus.PathPartial)
            {
                Debug.Log("[SCARECROW]: Path from position to player partial, did not move.");
                CreateDebugNode(newPosition, "PARTIAL newPosition", 0);
                return false;
            }
        }

        if (attackerPlayer != null && Vector3.Distance(newPosition, attackerPlayer.transform.position) < escapeRange)
        {
            Debug.Log($"[SCARECROW]: Position too close to previous attacker, Did not move.");
            return false;
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
        agent.Warp(position);
        Debug.Log("[SCARECROW]: Moved.");
        yield return new WaitForSeconds(time);
        changePositionCoroutine = null;
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
            Debug.Log("[SCARECROW]: Set invisible.");
            invisible = true;
            EnableEnemyMesh(false);
            enemyCollider.enabled = false;
            scanNode.gameObject.SetActive(false);
        }
        else
        {
            Debug.Log("[SCARECROW]: Set visible.");
            invisible = false;
            EnableEnemyMesh(true);
            enemyCollider.enabled = true;
            scanNode.gameObject.SetActive(true);
        }
    }

    public Vector3 GetRandomNavMeshPositionNearAINode(float radius = 16f)
    {
        int nodeSelected = Random.Range(0, nodes.Count);
        Vector3 nodePosition = nodes[nodeSelected].transform.position;
        Vector3 newPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(nodePosition, radius);
        return PositionAwayFromWall(newPosition);
    }

    public Vector3 GetRandomNavMeshPositionNearPlayer(PlayerControllerB player, float radius = 8f)
    {
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy)
        {
            radius *= 2.5f;
        }
        Vector3 newPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius);
        return PositionAwayFromWall(newPosition);
    }

    public Vector3 PositionAwayFromWall(Vector3 pos, float maxDistance = 3f, int resolution = 6)
    {
        Vector3 newPosition = pos;
        Transform tempTransform = base.transform;
        float shortestDistance = maxDistance;
        float yRotation = -1;

        //CAST RAYS AROUND POSITION TO FIND NEAREST WALL
        for (int i = 0; i < 360; i += 360/resolution)
        {
            tempTransform.eulerAngles = new Vector3(0f, i, 0f);
            if (Physics.Raycast(pos, tempTransform.forward, out var hitInfo, maxDistance, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                if (hitInfo.distance < shortestDistance)
                {
                    shortestDistance = hitInfo.distance;
                    yRotation = i;
                }
            }
        }

        //IF WALL WAS FOUND
        if (yRotation != -1)
        {
            //MOVE POSITION AWAY FROM NEAREST WALL
            tempTransform.eulerAngles = new Vector3(0f, yRotation, 0f);
            newPosition = pos + tempTransform.forward * (maxDistance - shortestDistance);

            //ENSURE NEW POSITION IS ON GROUND
            Vector3 checkFromPosition = newPosition + base.transform.up * 2f;
            if (Physics.Raycast(checkFromPosition, Vector3.down, out var hitInfo, 4f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                newPosition = hitInfo.point;
            }
            else
            {
                newPosition = pos;
            }
        }

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

        AudioClip clip = scareSounds[scareSound];
        scareAudio.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(scareAudio, clip);
        StartCoroutine(FaceTransformForTime(player.transform, clip.length/2));
        RoundManager.Instance.PlayAudibleNoise(transform.position, noiseRange, 1, 0, false, -1);
        scarePrimed = false;
        creatureAnimator.SetTrigger("ScarePlayer");

        if (GameNetworkManager.Instance.localPlayerController == player)
		{
            player.insanityLevel += player.maxInsanityLevel * 0.2f;
            player.JumpToFearLevel(0.5f);
            spottedLocally = true;
        }

        StopInterruptibleAudio();

        Debug.Log($"[SCARECROW]: Scarecrow scared player {player.playerUsername}.");
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

        Debug.Log("[SCARECROW]: Scarecrow tweaked out!");
        AudioClip clip = tweakOutSounds[tweakSound];
        tweakOutAudio.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(tweakOutAudio, clip);
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

    public IEnumerator FacePositionLerp(Vector3 lookPosition, float length)
    {
        RoundManager.Instance.tempTransform.position = base.transform.position;
        RoundManager.Instance.tempTransform.LookAt(lookPosition);

        float timeElapsed = 0f;
        float duration = length;

        float startRotation = base.transform.eulerAngles.y;
        float rotation = startRotation;
        float newRotation = RoundManager.Instance.tempTransform.eulerAngles.y;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            rotation = Mathf.LerpAngle(rotation, newRotation, timeElapsed / duration);
            base.transform.eulerAngles = new Vector3(0f, rotation, 0f);

            yield return null;
        }

        base.transform.eulerAngles = new Vector3(0f, newRotation, 0f);
    }

    public IEnumerator FaceTransformForTime(Transform transform, float length)
    {
        yield return StartCoroutine(FacePositionLerp(transform.position, 0.15f));

        RoundManager.Instance.tempTransform.position = base.transform.position;

        float timeElapsed = 0f;
        float duration = length;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            RoundManager.Instance.tempTransform.LookAt(transform.position);
            base.transform.rotation = Quaternion.Lerp(base.transform.rotation, RoundManager.Instance.tempTransform.rotation, 12f * Time.deltaTime);
            base.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);

            yield return null;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayDetectSoundServerRpc()
    {
        int detectSound = Random.Range(0, detectSounds.Length);
        PlayDetectSoundClientRpc(detectSound);
    }

    [ClientRpc]
    public void PlayDetectSoundClientRpc(int detectSound)
    {
        RoundManager.Instance.PlayAudibleNoise(transform.position, noiseRange, 0.5f, 0, false, -1);
        detectAudio.PlayOneShot(detectSounds[detectSound]);
        WalkieTalkie.TransmitOneShotAudio(detectAudio, detectSounds[detectSound]);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayDecoySoundServerRpc(bool desperateRpc = false)
    {
        int decoySound = Random.Range(0, decoySounds.Length);
        if (desperateRpc)
        {
            decoySound = Random.Range(0, decoySoundsDesperate.Length);
        }

        Vector3 meanVector = Vector3.zero;
        for (int i = 0; i < playersInRange.Count; i++)
        {
            meanVector += playersInRange[i].transform.position;
        }
        meanVector /= playersInRange.Count;

        Vector3 direction = Vector3.Normalize(meanVector - transform.position);
        Vector3 decoyPosition = meanVector + direction * 10f;
        decoyPosition = RoundManager.Instance.GetRandomPositionInRadius(decoyPosition, 0f, 8f);

        PlayDecoySoundClientRpc(decoySound, decoyPosition, desperateRpc);
    }

    [ClientRpc]
    public void PlayDecoySoundClientRpc(int decoySound, Vector3 soundPosition, bool desperateRpc = false)
    {
        decoyAudio.transform.position = soundPosition;
        desperate = desperateRpc;

        if (desperate)
        {
            decoyAudio.volume = 0.93f;
            decoyAudio.clip = decoySoundsDesperate[decoySound];
        }
        else
        {
            decoyAudio.volume = 0.96f;
            decoyAudio.clip = decoySounds[decoySound];
        }

        decoyAudio.Play();
        WalkieTalkie.TransmitOneShotAudio(decoyAudio, decoyAudio.clip);
        KeepDecoyPosition();
    }

    public async void KeepDecoyPosition()
    {
        decoyAudioPlaying = true;

        Vector3 oldpos = decoyAudio.transform.position;
        float timeElapsed = 0f;
        float duration = decoyAudio.clip.length;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            decoyAudio.transform.position = oldpos;
            await Task.Yield();
        }

        decoyAudioPlaying = false;
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (isEnemyDead)
        {
            return;
        }
        StopInterruptibleAudio();
        creatureAnimator.SetTrigger("TakeDamage");
        enemyHP -= force;
        if (playerWhoHit != null)
        {
            RememberAttackerServerRpc((int)playerWhoHit.playerClientId);
        }
        if (enemyHP <= 0 && !isEnemyDead)
        {
            creatureAnimator.SetTrigger("Die");
            isEnemyDead = true;
            SwitchToBehaviourState(0);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RememberAttackerServerRpc(int playerId)
    {
        RememberAttackerClientRpc(playerId);
    }

    [ClientRpc]
    public void RememberAttackerClientRpc(int playerId)
    {
        attackerPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
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
            StopInterruptibleAudio();
            creatureAnimator.SetTrigger("Explode");
            isEnemyDead = true;
            SwitchToBehaviourState(0);
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
        Debug.Log("[SCARECROW]: Called KillEnemy!");
        StopInterruptibleAudio();
        IncreaseEnemySpawnRate();
        enemyCollider.enabled = false;
        base.KillEnemy(destroy);
    }

    public void DropItem(bool zapped = false)
    {
        if (base.IsServer)
        {
            DropItemServerRpc(zapped);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropItemServerRpc(bool zapped = false)
    {
        Debug.Log("[SCARECROW]: Called DropItemServerRpc!");
        GameObject prefab;
        if (zapped)
        {
            prefab = zapItemPrefab;
        }
        else
        {
            prefab = dropItemPrefab;
        }
        GameObject dropObject = Instantiate(prefab, dropItemTransform.position, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
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
                RoundManager.Instance.totalScrapValueInLevel += aObject.scrapValue;
            }
        }
        else if (gObject != null)
        {
            gObject.SetScrapValue(RemapInt(value, startValue, endValue, startValue*0.45f, endValue*1.85f));
            RoundManager.Instance.totalScrapValueInLevel += gObject.scrapValue;
        }
        DropItemStupidly(dropObject);
    }

    public async void DropItemStupidly(GameObject dropitem)
    {
        await Task.Yield();
        dropitem.transform.position = dropItemTransform.position;
        dropitem.GetComponent<GrabbableObject>().FallToGround();
        dropitem.GetComponent<GrabbableObject>().hasHitGround = false;
    }

    private void IncreaseEnemySpawnRate()
    {
        Debug.Log($"[SCARECROW]: Original minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        Debug.Log($"[SCARECROW]: Original max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");
        RoundManager.Instance.minOutsideEnemiesToSpawn += enemySpawnIncrease;
        RoundManager.Instance.currentMaxOutsidePower += enemyPowerIncrease;
        Debug.Log($"[SCARECROW]: Increased minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        Debug.Log($"[SCARECROW]: Increased max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");

        if (IsServer)
        {
            PlayWarningSoundServerRpc();
        }
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
        SoundManager.Instance.playingOutsideMusic = false;

        //PLAY WARNING SOUND
        warningAudio.transform.position = soundPosition;
        AudioClip warningSoundClip;
        if (dangerValue < 33)
        {
            warningSoundClip = warningSoundsLow[clip];
        }
        else if (dangerValue < 66)
        {
            warningSoundClip = warningSoundsMedium[clip];
        }
        else
        {
            warningSoundClip = warningSoundsHigh[clip];
        }
        warningAudio.PlayOneShot(warningSoundClip);

        //SHAKE PLAYER SCREEN
        StartCoroutine(ShakeScreen(rumbleAudio.clip.length - 3f));

        //COLOR PLAYER SCREEN
        StartCoroutine(ColorScreen(rumbleAudio.clip.length - 3f));
    }

    public IEnumerator ShakeScreen(float duration)
    {
        yield return new WaitForSeconds(6f);
        rumbleAudio.Play();
        float timeElapsed = 0f;
        float layerWeight = 0f;

        Vector3 originalPosition = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.localPosition;

        bool particlesEnabled = false;
        Vector3 originalParticlePosition = screenShakeParticles.transform.position;

        //LERP LAYER WEIGHT ON
        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            if (timeElapsed < duration/2)
            {
                layerWeight = Mathf.Lerp(0f, 1f, timeElapsed / (duration/2f));
            }

            if (timeElapsed > duration/2)
            {
                layerWeight = Mathf.Lerp(1f, 0f, (timeElapsed-(duration/2f)) / (duration/2f));
            }

            screenShakeParticles.transform.position = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position;

            if (GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                if (timeElapsed > duration*0.2 && !particlesEnabled)
                {
                    screenShakeParticles.SetActive(true);
                    screenShakeParticles.GetComponent<ParticleSystem>().Play();
                    particlesEnabled = true;
                }
            }
            else
            {
                screenShakeParticles.SetActive(false);
                particlesEnabled = false;
            }

            creatureAnimator.SetLayerWeight(2, layerWeight);
            GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.localPosition = screenShakeTransform.localPosition;
            yield return null;
        }

        creatureAnimator.SetLayerWeight(2, 0);
        GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.localPosition = originalPosition;

        screenShakeParticles.SetActive(false);
        screenShakeParticles.transform.position = originalParticlePosition;
    }

    public IEnumerator ColorScreen(float duration)
    {
        yield return new WaitForSeconds(4.5f);
        float timeElapsed = 0f;
        float volumeWeight = 0f;
        screenShakeVolume.enabled = true;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            if (timeElapsed < duration/2)
            {
                volumeWeight = Mathf.Lerp(0f, 0.4f, timeElapsed / (duration/2f));
            }
            if (timeElapsed > duration/2)
            {
                volumeWeight = Mathf.Lerp(0.4f, 0f, (timeElapsed-(duration/2f)) / (duration/2f));
            }

            screenShakeVolume.weight = volumeWeight;
            yield return null;
        }

        screenShakeVolume.weight = 0;
        screenShakeVolume.enabled = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartWaitForMusicServerRpc()
    {
        StartWaitForMusicClientRpc();
    }

    [ClientRpc]
    public void StartWaitForMusicClientRpc()
    {
        spottedLocally = false;
        spottedTimer = initialSpottedTimer;

        //SHORTEN SPOTTED DISTANCE FOR MUSIC IN OBSCURED VISION
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Foggy || StartOfRound.Instance.currentLevel.PlanetName == "85 Rend" || StartOfRound.Instance.currentLevel.PlanetName == "7 Dine" || StartOfRound.Instance.currentLevel.PlanetName == "8 Titan" || StartOfRound.Instance.currentLevel.PlanetName == "91 Bellow")
        {
            spottedDistance = Mathf.RoundToInt(spottedDistance * 0.4f);
        }

        StartCoroutine(WaitToBeSpotted());
    }

    public IEnumerator WaitToBeSpotted()
    {
        while (!spottedLocally)
        {
            if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(scareTriggerTransform.position, spottedAngle, spottedDistance) && !spottedLocally && !invisible)
            {
                spottedOnce = true;
                spottedTimer -= Time.deltaTime;

                SoundManager.Instance.musicSource.volume = Mathf.Lerp(SoundManager.Instance.musicSource.volume, 0f, 3f*Time.deltaTime);
                
                if (spottedTimer <= 0f)
                {
                    spottedLocally = true;
                    if (PlayMusicLocallyCoroutine != null)
                    {
                        StopCoroutine(PlayMusicLocallyCoroutine);
                        PlayMusicLocallyCoroutine = null;
                    }
                    PlayMusicLocallyCoroutine = StartCoroutine(PlaySpottedMusicLocally());
                }
            }
            else if (spottedOnce)
            {
                SoundManager.Instance.musicSource.volume = Mathf.Lerp(SoundManager.Instance.musicSource.volume, 0.85f, 3f*Time.deltaTime);

                if (spottedTimer <= initialSpottedTimer)
                {
                    spottedTimer += Time.deltaTime;
                }
                else
                {
                    spottedTimer = initialSpottedTimer;
                    spottedOnce = false;
                }
            }

            yield return null;
        }
    }

    public IEnumerator PlaySpottedMusicLocally()
    {
        if (useSaveFileForMusic)
        {
            if (ES3.Load(saveFileString, "LCGeneralSaveData", defaultValue: false))
            {
                Debug.Log("[SCARECROW]: Not playing music: Player has already heard scarecrow music for the first time.");
                yield break;
            }
        }

        float maxMusicVolume;
        float minDiageticVolume;

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Rainy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded || StartOfRound.Instance.currentLevel.PlanetName == "85 Rend" || StartOfRound.Instance.currentLevel.PlanetName == "8 Titan" || StartOfRound.Instance.currentLevel.PlanetName == "91 Bellow")
        {
            maxMusicVolume = 1f;
            minDiageticVolume = -10f;
        }
        else
        {
            maxMusicVolume = 0.85f;
            minDiageticVolume = -7f;
        }

        if (dangerValue < 33)
        {
            spottedAudio.clip = spottedLow;
        }
        else if (dangerValue < 66)
        {
            spottedAudio.clip = spottedMed;
        }
        else
        {
            spottedAudio.clip = spottedHigh;
        }

        spottedAudio.Play();
        SoundManager.Instance.playingOutsideMusic = false;
        float timeElapsed = 0f;
        float duration = spottedAudio.clip.length;
        bool watching = false;
        IterateJob increaseDiagetic = new("increase diagetic volume", 0f, null, false);
        IterateJob decreaseDiagetic = new("decrease diagetic volume", 0f, null, false);
        IterateJob increaseMusic = new("increase music volume", 0f, null, false);
        IterateJob decreaseMusic = new("decrease music volume", 0f, null, false);

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            if (GameNetworkManager.Instance.localPlayerController.isPlayerDead || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Eclipsed)
            {
                goto END;
            }
            else if (StartOfRound.Instance.audioListener == null || playersWithLineOfSight.Count > 1)
            {
                goto END_EARLY;
            }

            if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(scareTriggerTransform.position, spottedAngle*3.5f, spottedDistance) && !invisible)
            {
                if (!watching)
                {
                    watching = true;
                    RestartCoroutine(increaseDiagetic.Coroutine, false);
                    RestartCoroutine(decreaseMusic.Coroutine, false);
                    RestartCoroutine(decreaseDiagetic.Coroutine, true, LerpIncrement(decreaseDiagetic, increaseDiagetic.Value, minDiageticVolume, 3.5f, 0.25f));
                    RestartCoroutine(increaseMusic.Coroutine, true, LerpIncrement(increaseMusic, decreaseMusic.Value, maxMusicVolume, 3f, 0f));
                }
                SoundManager.Instance.SetDiageticMasterVolume(decreaseDiagetic.Value);
                spottedAudio.volume = increaseMusic.Value;
            }
            else
            {
                if (watching)
                {
                    watching = false;
                    RestartCoroutine(decreaseDiagetic.Coroutine, false);
                    RestartCoroutine(increaseMusic.Coroutine, false);
                    RestartCoroutine(increaseDiagetic.Coroutine, true, LerpIncrement(increaseDiagetic, decreaseDiagetic.Value, 0f, 2f, 0.25f));
                    RestartCoroutine(decreaseMusic.Coroutine, true, LerpIncrement(decreaseMusic, increaseMusic.Value, 0f, 2f, 0f));
                }
                SoundManager.Instance.SetDiageticMasterVolume(increaseDiagetic.Value);
                spottedAudio.volume = decreaseMusic.Value;

                if (timeElapsed < duration*0.5f && decreaseMusic.Done)
                {
                    goto END_EARLY;
                }
            }

            yield return null;
        }

        if (useSaveFileForMusic)
        {
            ES3.Save(saveFileString, value: true, "LCGeneralSaveData");
            Debug.Log("[SCARECROW]: Saved: Player has heard scarecrow music for the first time.");
        }

        END:
        SoundManager.Instance.SetDiageticMasterVolume(0f);
        spottedAudio.volume = 0f;
        PlayMusicLocallyCoroutine = null;
        yield break;

        END_EARLY:
        SoundManager.Instance.SetDiageticMasterVolume(0f);
        spottedAudio.volume = 0f;
        spottedLocally = false;
        spottedOnce = false;
        spottedTimer = initialSpottedTimer;
        StartCoroutine(WaitToBeSpotted());
        PlayMusicLocallyCoroutine = null;
        yield break;
    }

    //FOR TESTING
    public void DeleteSpottedMusicSaveData()
    {
        if (ES3.KeyExists(saveFileString, "LCGeneralSaveData"))
        {
            ES3.DeleteKey(saveFileString, "LCGeneralSaveData");
            Debug.LogWarning($"[SCARECROW]: Deleted key {saveFileString} in LCGeneralSaveData.");
        }
        else
        {
            Debug.LogError($"[SCARECROW]: key {saveFileString} in LCGeneralSaveData does not exist.");
        }
    }

    public void RestartCoroutine(Coroutine coroutine, bool restart, IEnumerator ienumerator = null)
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
            coroutine = null;
        }
        if (restart && ienumerator != null)
        {
            coroutine = StartCoroutine(ienumerator);
        }
    }

    public IEnumerator LerpIncrement(IterateJob iterateJob, float lerpStartValue, float lerpEndValue, float lerpDuration, float lerpIncrement)
    {
        float lerpTimeElapsed = 0f;
        float lerpTimer = 0f;
        while (lerpTimeElapsed < lerpDuration)
        {
            lerpTimeElapsed += Time.deltaTime;
            lerpTimer += Time.deltaTime;
            if (lerpTimer > lerpIncrement)
            {
                iterateJob.Value = Mathf.Lerp(lerpStartValue, lerpEndValue, lerpTimeElapsed / lerpDuration);
                lerpTimer = 0f;
            }
            yield return null;
        }
        iterateJob.Done = true;
        iterateJob.Coroutine = null;
    }

	[ServerRpc(RequireOwnership = false)]
	public void SwitchOwnershipAndSyncStatesServerRpc(int state, ulong newOwner)
    {
        Debug.Log($"SwitchOwnershipAndSyncStatesServerRpc called!");
        if (thisNetworkObject.OwnerClientId != newOwner)
        {
            thisNetworkObject.ChangeOwnership(newOwner);
        }
        if (StartOfRound.Instance.ClientPlayerList.TryGetValue(newOwner, out var playerId))
        {
            targetPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
            SwitchOwnershipAndSyncStatesClientRpc(playerId, desperate, state);
        }
    }

	[ClientRpc]
	public void SwitchOwnershipAndSyncStatesClientRpc(int playerId, bool desperateRpc, int state)
    {
        Debug.Log($"SwitchOwnershipAndSyncStatesClientRpc called!");
        currentOwnershipOnThisClient = playerId;
        base.transform.position = serverPosition;
        SwitchToBehaviourStateOnLocalClient(state);
        desperate = desperateRpc;
        targetPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
    }

    public void StopInterruptibleAudio()
    {
        if (decoyAudioPlaying)
        {
            Debug.Log("[SCARECROW]: Stopping decoy audio.");
            decoyAudio.Stop();
        }
        if (PlayMusicLocallyCoroutine != null)
        {
            Debug.Log("[SCARECROW]: Stopping local music audio.");
            StopCoroutine(PlayMusicLocallyCoroutine);
            SoundManager.Instance.SetDiageticMasterVolume(0f);
            spottedAudio.volume = 0f;
        }
    }

    public void CreateDebugNode(Vector3 position, string name, int type)
    {
        if (DebugEnemy)
        {
            GameObject debugNode = Instantiate(scanNode.gameObject, position, Quaternion.identity);
            debugNodes.Add(debugNode);
            debugNode.GetComponent<ScanNodeProperties>().minRange = 1;
            debugNode.GetComponent<ScanNodeProperties>().maxRange = 300;
            debugNode.GetComponent<ScanNodeProperties>().headerText = name + " " + debugNodes.Count.ToString();
            debugNode.GetComponent<ScanNodeProperties>().subText = $"{position}";
            debugNode.GetComponent<ScanNodeProperties>().nodeType = type;
            debugNode.GetComponent<ScanNodeProperties>().creatureScanID = -1;
            debugNode.GetComponent<ScanNodeProperties>().requiresLineOfSight = false;
            Debug.LogWarning($"[SCARECROW]: Placed debug node at {position}.");
        }
    }

    public int RemapInt(float value, float min1, float max1, float min2, float max2)
    {
        float m = (value - min1) / (max1 - min1) * (max2 - min2) + min2;
        return Mathf.RoundToInt(m);
    }

    public float RemapFloat(float value, float min1, float max1, float min2, float max2)
    {
        float m = (value - min1) / (max1 - min1) * (max2 - min2) + min2;
        return m;
    }
}