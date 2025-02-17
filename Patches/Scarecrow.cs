using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Rendering.HighDefinition;
using Random = UnityEngine.Random;

public class Scarecrow : EnemyAI
{
    [Space(15f)]
    [Header("Scarecrow Settings")]

    public int daysBeforeScarecrowSpawns = 5;

    [Range(0f, 100f)]
    public float earlyDaysSpawnChance = 2f;

    [Range(0f, 1f)]
    public float normalizedTimeInDayToBecomeActive;

    public GameObject dropItemPrefab;

    public GameObject zapItemPrefab;

    public Transform dropItemTransform;

    public Transform meshContainer;

    [HideInInspector]
    public List<PlayerControllerB> playersInRange;

    [HideInInspector]
    public List<PlayerControllerB> playersWithLineOfSight;

    [HideInInspector]
    public List<PlayerControllerB> playersInDefenseRange;

    [HideInInspector]
    public List<PlayerControllerB> playersHallucinating;

    private PlayerControllerB attackerPlayer;
    
    public float detectRange;

    private float currentDetectRange;

    private float weaponDetectRange;

    public float scareRange;

    public string[] invalidTerrainTags;

    public float shipSafetyRadius;

    public float escapeRange = 30f;

    private Coroutine changePositionCoroutine;

    private bool scarePrimed;

    private List<GameObject> nodes;

    private GameObject[] spawnDenialPoints;

    public Transform[] lineOfSightTriggers;

    public Transform scareTriggerTransform;

    private FloodWeather floodWeather;

    private StormyWeather stormyWeather;

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

    private bool instantScare;

    public float scarePlayerCooldown = 5f;

    private float scarePlayerTimer;

    [Space(5f)]
    [Range(0f, 100f)]
    public float decoySoundStartingChance = 20;

    private float decoySoundChance;

    public float decoySoundChanceIncrement;

    public float decoySoundCooldown = 5f;

    private float decoySoundTimer;

    private enum Conditions : byte
    {
        DefaultState,

        ExperimentationState,

        FoggyState,

        EvasiveState,

        JetpackState
    }

    private Conditions currentCondition = Conditions.DefaultState;

    private Conditions prevCondition = Conditions.DefaultState;

    private float conditionsCheckTimer;

    [Serializable]
    private struct ConditionalValues()
    {
        
        [Range(0f, 100f)]
        public float tweakOutStartingChance;

        [Range(0f, 100f)]
        public float facePlayerStartingChance;
        
        [Range(0f, 100f)]
        public float detectSoundStartingChance;
        
        [Range(0f, 100f)]
        public float moveStartingChance;

        [Range(0f, 100f)]
        public float scarePlayerStartingChance;

        [Range(0f, 100f)]
        public float instantScareStartingChance;

        [Range(0f, 100f)]
        public float decoySoundStartingChance;

        [Range(0f, 100f)]
        public float chaseStartingChance;

        public float minMoveCooldown;

        public float maxMoveCooldown;

        public float shipSafetyRadius;

        public float hallucinationSpawnTime;

        public float detectRange;

        public float escapeRange;

        public float minSearchTime;

        public float maxSearchTime;
    }

    [SerializeField] private ConditionalValues[] conditionalValues;

    private ConditionalValues currentConditionalValues;

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

    private int detectSound;

    [Space(5f)]
    public AudioSource scareAudio;

    public AudioClip[] scareSounds;

    [Space(5f)]
    public AudioSource tweakOutAudio;

    public AudioClip[] tweakOutSounds;

    private int tweakSound;

    [Space(5f)]
    public AudioSource warningAudio;

    public AudioClip[] warningSoundsLow;

    public AudioClip[] warningSoundsMedium;

    public AudioClip[] warningSoundsHigh;

    [Space(5f)]
    public AudioSource decoyAudio;

    public AudioClip[] decoySounds;

    private int decoySound;

    private bool decoyAudioPlaying;

    [Space(5f)]
    public AudioSource spottedAudio;

    public AudioClip spottedLow;

    public AudioClip spottedMed;

    public AudioClip spottedHigh;

    public float initialSpottedTimer = 1f;

    public int spottedDistance = 45;

    public float spottedAngle = 8f;

    public AudioMixerSnapshot spottedSnapshot;

    public AudioMixer scarecrowMixer;

    private float spottedTimer;

    private bool fullySpotted;

    private bool initiallySpotted;

    private Coroutine playMusicLocallyCoroutine;

    [Space(5f)]
    public AudioSource rumbleAudio;

    public Volume screenShakeVolume;

    public GameObject screenShakeParticles;

    public Transform screenShakeTransform;

    [Space(10f)]
    [Header("Scan Node Settings")]
    public bool useScanNode;

    public ScanNodeProperties scanNode;

    public TerminalNode newScarecrowNode;

	public Terminal currentTerminal;

    private bool scanNodeActive;

    public static List<GameObject> debugNodes = new List<GameObject>();

    [Space(10f)]
    [Header("Teleport Animations")]
    public GameObject burrowContainer;

    public AudioClip[] burrowClips;

    public static List<GameObject> burrowPrefabs = [];
    
    public AudioSource tunnelSource;

    public AudioClip[] tunnelClips;

    public Transform rootBone;

    public AnimationClip teleportStartClip;

    public AnimationClip teleportEndClip;

    [Space(10f)]
    [Header("Transmissions")]
    public AudioClip[] talkClips;

    public AudioClip[] replyClips;

    public float transmissionDeadline;

    private Coroutine marcoCoroutine;
    
    private Coroutine responseCoroutine;

    private Coroutine poloCoroutine;

    private Coroutine waitForBeginningCoroutine;

    private Coroutine notBeingInterruptedCoroutine;

    private Coroutine waitToEndEarlyCoroutine;

    private Coroutine manageRadioVolumesCoroutine;

    private int talkClip;

    private int replyClip;

    private bool notBeingInterrupted;

    private bool quitEarly;

    private bool transmitting;

    private Dictionary<WalkieTalkie, AudioSource> transmissionSources = [];

    [Space(10f)]
    [Header("Hallucinations")]
    public GameObject[] hallucinations;

    [HideInInspector]
    public GameObject instanceHallucinationObject;

    [HideInInspector]
    public ScarecrowHallucination instanceHallucination;
    
    public Volume hallucinationVolume;

    public AudioClip[] ghostFadeClips;

    public AudioSource ghostFadeSource;

    private Coroutine hallucinatingCoroutine;

    private Coroutine fadeHallucinationCoroutine;

    private Coroutine spawnHallucinationCoroutine;

    public float hallucinationSpawnTime = 1.5f;

    public float hallucinateThreatIncrement = 0.2f;

    public float hallucinateChanceMin = 0.2f;

    public float hallucinateChanceMax = 1.4f;

    public float weaponDetectIncrement = 0.25f;

    public float weaponDetectRangeMin = 1f;

    public float weaponDetectRangeMax = 4f;

    private float hallucinateChanceMultiplier;

    private IterateJob controlVolumeOpacity = new("control volume opacity", 0f, null, false);

    private IterateJob controlFadeVolume = new("control fade volume", 0f, null, false);

    private IterateJob controlMeshOpacity = new("control mesh opacity", 0f, null, false);

    public SkinnedMeshRenderer bodyRenderer;

    public SkinnedMeshRenderer rotRenderer;

    public Material[] scarecrowMats;

    public Material[] scarecrowMatsRot;

    public Material[] scarecrowMatsDither;

    public Material[] scarecrowMatsRotDither;

    [Space(10f)]
    [Header("Save File Parameters")]
    public bool useSaveFileForMusic = false;

    public bool useSaveFileForTransmit = false;

    public bool useSaveFileForThreats = false;

    public bool useSaveFileForBehaviours = false;

    public static string currentSaveFile = CoronaMod.Info.SaveFileName1;

    public static string musicSaveFileKey = "PlayedScarecrowMusic";

    public static string talkSaveFileKey = "TalkClipsPlayed";

    public static string replySaveFileKey = "ReplyClipsPlayed";

    public static string behaviourSaveFileKey = "BehavioursUsed";

    public static string threatenedSaveFileKey = "TimesThreatened";

    public static int timesThreatenedInSaveFile;

    [Space(10f)]
    [Header("Unordered Fields")]
    public float audibleSoundCooldown = 10f;

    public class IterateJob(string name, float value, Coroutine coroutine, bool done)
    {
        public string Name = name;

        public float Value = value;

        public Coroutine Coroutine = coroutine;

        public bool Done = done;
    }

    //SO COMPILER CAN CATCH TYPOS AND CAPITALIZATION MISMATCH
    private static string stopDetecting = nameof(stopDetecting); 

    private static string invisibleForOwner = nameof(invisibleForOwner);

    private static string encountered = nameof(encountered);

    private static string hallucinationLive = nameof(hallucinationLive);

    private static string hallucinationSpotted = nameof(hallucinationSpotted);

    private static string hallucinationVisible = nameof(hallucinationVisible);

    private static string desperate = nameof(desperate);

    private static string syncPosition = nameof(syncPosition);

    public Dictionary<string, bool> syncedBool = new()
    {
        [stopDetecting] = false,

        [invisibleForOwner] = false,

        [encountered] = false,

        [hallucinationLive] = false,

        [hallucinationSpotted] = false,

        [hallucinationVisible] = false,

        [desperate] = false,

        [syncPosition] = false
    };

    private static List<GameObject> stupidObjects = [];

    private static List<GameObject> newStupidColliders = [];

    public bool useDebugNodes = false;

    public bool debugForceSpawnHallucination = false;

    public int debugForceSpawnHallucinationIndex = -1;

    public int debugForceSpawnHallucinationBehaviour = -1;

    public override void Start()
    {
        if (IsOwner)
        {
            DebugMsg($"[SCARECROW]: Days spent: {StartOfRound.Instance.gameStats.daysSpent}");

            //IF SCARECROW SPAWNS BEFORE X DAYS HAVE PASSED:
            if (!StartOfRound.Instance.isChallengeFile && StartOfRound.Instance.gameStats.daysSpent < daysBeforeScarecrowSpawns)
            {
                if (Random.Range(0f, 100f) > earlyDaysSpawnChance)
                {
                    //DESPAWN SCARECROW
                    DebugMsg($"[SCARECROW]: Tried to spawn before {daysBeforeScarecrowSpawns} days!");
                    RoundManager.Instance.DespawnEnemyOnServer(base.NetworkObject);

                    //TRY SPAWN RANDOM DAYTIME ENEMY INSTEAD
                    GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("OutsideAINode");
                    float num = RoundManager.Instance.timeScript.lengthOfHours * (float)RoundManager.Instance.currentHour;
                    RoundManager.Instance.SpawnRandomDaytimeEnemy(spawnPoints, num);
                    return;
                }
                else
                {
                    DebugMsg($"[SCARECROW]: Spawning before {daysBeforeScarecrowSpawns} days because of earlyDaysSpawnChance succeeding!");
                }
            }

            SetDangerLevelsAndSync();
            GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        }
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            floodWeather = FindObjectOfType<FloodWeather>();
        }
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy)
        {
            stormyWeather = FindObjectOfType<StormyWeather>();
        }
        if (StartOfRound.Instance.isChallengeFile)
        {
            useSaveFileForMusic = false;
            useSaveFileForTransmit = false;
            useSaveFileForThreats = false;
            useSaveFileForBehaviours = false;
        }

        StupidObjectCheck();

        currentTerminal = FindObjectOfType<Terminal>();
        currentTerminal.SyncTerminalValuesServerRpc();

        base.Start();

        SetWindLevel();
        SetSpawnPoints(spawn: true);
        SetScanNode();

        playersInRange = [];
        playersWithLineOfSight = [];
        playersInDefenseRange = [];
        playersHallucinating = [];

        //set defaultstate's conditional values as original input fields
        conditionalValues[0].tweakOutStartingChance = tweakOutStartingChance;
        conditionalValues[0].facePlayerStartingChance = facePlayerStartingChance;
        conditionalValues[0].detectSoundStartingChance = detectSoundStartingChance;
        conditionalValues[0].moveStartingChance = moveStartingChance;
        conditionalValues[0].scarePlayerStartingChance = scarePlayerStartingChance;
        conditionalValues[0].instantScareStartingChance = instantScareStartingChance;
        conditionalValues[0].decoySoundStartingChance = decoySoundStartingChance;
        conditionalValues[0].chaseStartingChance = chaseStartingChance;
        conditionalValues[0].minMoveCooldown = minMoveCooldown;
        conditionalValues[0].maxMoveCooldown = maxMoveCooldown;
        conditionalValues[0].shipSafetyRadius = shipSafetyRadius;
        conditionalValues[0].hallucinationSpawnTime = hallucinationSpawnTime;
        conditionalValues[0].detectRange = detectRange;
        conditionalValues[0].escapeRange = escapeRange;
        conditionalValues[0].minSearchTime = minSearchTime;
        conditionalValues[0].maxSearchTime = maxSearchTime;

        currentConditionalValues = conditionalValues[0];

        tweakOutChance = currentConditionalValues.tweakOutStartingChance;
        facePlayerChance = currentConditionalValues.facePlayerStartingChance;
        detectSoundChance = currentConditionalValues.detectSoundStartingChance;
        moveChance = currentConditionalValues.moveStartingChance;
        scarePlayerChance = currentConditionalValues.scarePlayerStartingChance;
        instantScareChance = currentConditionalValues.instantScareStartingChance;
        decoySoundChance = currentConditionalValues.decoySoundStartingChance;
        chaseChance = currentConditionalValues.chaseStartingChance;

        if (IsOwner)
        {
            SetThreatenedValueServerRpc(load: true);
            MoveToRandomPosition(spawn: true);
        }

        DebugMsg("[SCARECROW]: Spawned!");
        DebugMsg($"[SCARECROW]: Danger value: {dangerValue}");
        DebugMsg($"[SCARECROW]: Minimum enemy spawn increase: {enemySpawnIncrease}");
        DebugMsg($"[SCARECROW]: Max enemy power increase: {enemyPowerIncrease}");
        DebugMsg($"[SCARECROW]: Start value: {startValue}");
        DebugMsg($"[SCARECROW]: End value: {endValue}");
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

    public void SetSpawnPoints(bool spawn)
    {
        List<GameObject> outsideAINodes = [.. GameObject.FindGameObjectsWithTag("OutsideAINode")];
        if (spawn)
        {
            spawnDenialPoints = GameObject.FindGameObjectsWithTag("SpawnDenialPoint");
            nodes = outsideAINodes;
            return;
        }
        else
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
                    List<GameObject> nodesForPlanet = [];
                    foreach (Transform child in nodesTransform.transform)
                    {
                        nodesForPlanet.Add(child.gameObject);
                    }
                    nodes = nodesForPlanet;
                    if (addScarecrowNodesToOutsideNodes)
                    {
                        nodes.AddRange(outsideAINodes);
                    }
                    return;
                }
            }
        }
    }

    public void SetDangerLevelsAndSync()
    {
        normalizedTimeInDayToBecomeActive += 1 / RoundManager.Instance.timeScript.numberOfHours * Random.Range(-1, 1);

        dangerValue = Random.Range(0f,100f);
        enemySpawnIncrease = RemapInt(dangerValue, 0, 100, minEnemySpawnIncrease, maxEnemySpawnIncrease);
        enemyPowerIncrease = RemapInt(dangerValue, 0, 100, minEnemyPowerIncrease, maxEnemyPowerIncrease);

        startValue = Random.Range(minStartValue, maxStartValue);
        endValue = Random.Range(minEndValue, maxEndValue);

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
        StartCoroutine(GiveRandomTiltLerp(randomTilt, 0.17f));
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
            StartCoroutine(GiveRandomTiltLerp(randomTilt, 0.17f));
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
            if (player.HasLineOfSightToPosition(lineOfSightTriggers[i].position, width: 46, range: 200))
            {
                return true;
            }
        }
        return false;
    }

    public void ConditionsCheck()
    {
        if (isEnemyDead)
        {
            return;
        }
        if ((int)currentCondition < 4 && FindObjectOfType<JetpackItem>() != null)
        {
            currentCondition = Conditions.JetpackState;
        }
        if ((int)currentCondition < 3 && (FindObjectOfType<VehicleController>() != null || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy || enemyHP < 2))
        {
            currentCondition = Conditions.EvasiveState;
        }
        if ((int)currentCondition < 2 && (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Foggy))
        {
            currentCondition = Conditions.FoggyState;
        }
        if ((int)currentCondition < 1 && (StartOfRound.Instance.currentLevel.PlanetName == "41 Experimentation"))
        {
            currentCondition = Conditions.ExperimentationState;
        }
        if (currentCondition != prevCondition)
        {
            DebugMsg($"[SCARECROW]: Changing current condition to {currentCondition} | condition {(int)currentCondition}.");
            currentConditionalValues = conditionalValues[(int)currentCondition];

            tweakOutStartingChance = currentConditionalValues.tweakOutStartingChance;
            facePlayerStartingChance = currentConditionalValues.facePlayerStartingChance;
            detectSoundStartingChance = currentConditionalValues.detectSoundStartingChance;
            moveStartingChance = currentConditionalValues.moveStartingChance;
            scarePlayerStartingChance = currentConditionalValues.scarePlayerStartingChance;
            instantScareStartingChance = currentConditionalValues.instantScareStartingChance;
            decoySoundStartingChance = currentConditionalValues.decoySoundStartingChance;
            chaseStartingChance = currentConditionalValues.chaseStartingChance;
            minMoveCooldown = currentConditionalValues.minMoveCooldown;
            maxMoveCooldown = currentConditionalValues.maxMoveCooldown;
            shipSafetyRadius = currentConditionalValues.shipSafetyRadius;
            hallucinationSpawnTime = currentConditionalValues.hallucinationSpawnTime;
            detectRange = currentConditionalValues.detectRange;
            escapeRange = currentConditionalValues.escapeRange;
            minSearchTime = currentConditionalValues.minSearchTime;
            maxSearchTime = currentConditionalValues.maxSearchTime;

            prevCondition = currentCondition;
        }
    }

    public void UpdateTimers()
    {
        timers = [searchTimer, moveTimer, facePlayerTimer, scarePlayerTimer, detectSoundTimer, tweakOutTimer, decoySoundTimer, conditionsCheckTimer];
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
        conditionsCheckTimer = timers[7];

        if (multiplyChances)
        {
            tweakOutStartingChance = currentConditionalValues.tweakOutStartingChance * chanceMultiplier;
            facePlayerStartingChance = currentConditionalValues.facePlayerStartingChance * chanceMultiplier;
            detectSoundStartingChance = currentConditionalValues.detectSoundStartingChance * chanceMultiplier;
            moveStartingChance = currentConditionalValues.moveStartingChance * chanceMultiplier;
            scarePlayerStartingChance = currentConditionalValues.scarePlayerStartingChance * chanceMultiplier;
            decoySoundStartingChance = currentConditionalValues.decoySoundStartingChance * chanceMultiplier;
            chaseStartingChance = currentConditionalValues.chaseStartingChance * chanceMultiplier;
        }

        if (conditionsCheckTimer <= 0)
        {
            ConditionsCheck();
            conditionsCheckTimer = 15f;
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
                    DebugMsg("[SCARECROW]: Played audible sound!");
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
				if (syncedBool[syncPosition])
                {
                    base.transform.position = Vector3.SmoothDamp(base.transform.position, serverPosition, ref tempVelocity, syncMovementSpeed);
                    base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, targetYRotation, 15f * Time.deltaTime), base.transform.eulerAngles.z);
                }
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
            updateDestinationInterval = AIIntervalTime + Random.Range(-0.015f, 0.015f);
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

    public void AddDetectedPlayerAndSync(int playerId, bool adding, bool defense)
    {
        AddDetectedPlayer(playerId, adding, defense);
        AddDetectedPlayerServerRpc(playerId, adding, defense);
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddDetectedPlayerServerRpc(int playerId, bool adding, bool defense)
    {
        AddDetectedPlayerClientRpc(playerId, adding, defense);
    }

    [ClientRpc]
    public void AddDetectedPlayerClientRpc(int playerId, bool adding, bool defense)
    {
        if (!IsOwner)
        {
            AddDetectedPlayer(playerId, adding, defense);
        }
    }

    public void AddDetectedPlayer(int playerId, bool adding, bool defense)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];
        if (!defense)
        {
            if (adding)
            {
                playersInRange.Add(player);
                DebugMsg($"[SCARECROW]: Added {player.playerUsername} to detected players.");
            }
            else
            {
                playersInRange.Remove(player);
                DebugMsg($"[SCARECROW]: Removed {player.playerUsername} from detected players.");
            }
        }
        else
        {
            if (adding)
            {
                playersInDefenseRange.Add(player);
                DebugMsg($"[SCARECROW]: Added {player.playerUsername} to detected players in defense range.");
            }
            else
            {
                playersInDefenseRange.Remove(player);
                DebugMsg($"[SCARECROW]: Removed {player.playerUsername} from detected players in defense range.");
            }
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
            DebugMsg($"[SCARECROW]: Player {StartOfRound.Instance.allPlayerScripts[playerId].playerUsername} has line of sight.");
            playersWithLineOfSight.Add(player);
        }
        else
        {
            DebugMsg($"[SCARECROW]: Player {StartOfRound.Instance.allPlayerScripts[playerId].playerUsername} lost line of sight.");
            playersWithLineOfSight.Remove(player);
        }
    }

    public override void DoAIInterval()
    {
        if (syncedBool[syncPosition])
        {
            SyncPositionToClients();
        }

        if (isEnemyDead)
        {
            return;
        }

        currentDetectRange = syncedBool[stopDetecting] ? 0f : detectRange;

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
        if (syncedBool[invisibleForOwner])
        {
            return;
        }

        //DETECT PLAYERS WITHIN RANGE
        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
            if ((Vector3.Distance(player.transform.position, transform.position) <= currentDetectRange) && !playersInRange.Contains(player))
            {
                AddDetectedPlayerAndSync((int)player.playerClientId, true, false);
                if (CheckLineOfSightForScarecrow(player) && !syncedBool[encountered])
                {
                    SetSyncedBool(encountered, true);
                }
            }
            if ((Vector3.Distance(player.transform.position, transform.position) > currentDetectRange) && playersInRange.Contains(player))
            {
                AddDetectedPlayerAndSync((int)player.playerClientId, false, false);
            }

            //DETECT PLAYERS WITHIN DEFENSE RANGE
            if ((Vector3.Distance(player.transform.position, transform.position) <= weaponDetectRange) && !playersInDefenseRange.Contains(player))
            {
                AddDetectedPlayerAndSync((int)player.playerClientId, true, true);
            }
            if ((Vector3.Distance(player.transform.position, transform.position) > weaponDetectRange) && playersInDefenseRange.Contains(player))
            {
                AddDetectedPlayerAndSync((int)player.playerClientId, false, true);
            }
        }

        switch (currentBehaviourStateIndex)
        {

        //INACTIVE
        case 0:

            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                DebugMsg("[SCARECROW]: Inactive.");
                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //ACTIVATING FOR THE FIRST TIME
            if (RoundManager.Instance.timeScript.normalizedTimeOfDay > normalizedTimeInDayToBecomeActive && !isEnemyDead && !syncedBool[invisibleForOwner])
            {
                SetSpawnPoints(spawn: false);
                StartTransmission();
                SwitchToBehaviourState(1);
            }

            break;



        //SEARCHING
        case 1:

            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                DebugMsg("[SCARECROW]: Searching.");
                targetPlayer = null;
                SetSyncedBool(desperate, false);
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                searchTimer = Random.Range(minSearchTime, maxSearchTime);
                tweakOutTimer = tweakOutCooldown;
                decoySoundTimer = decoySoundCooldown;

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //CONDUCTIVE OBJECT BEING ELECTROCUTED IN RANGE OF SCARECROW
            if (stormyWeather != null && stormyWeather.setStaticToObject != null && Vector3.Distance(base.transform.position, stormyWeather.setStaticToObject.transform.position) < 8)
            {
                DebugMsg("[SCARECROW]: Too close to conductive object, escaping.");
                SwitchToBehaviourState(3);
            }

            //IF SEARCH EXCEEDS SEARCH TIME
            if (searchTimer <= 0)
            {
                DebugMsg("[SCARECROW]: Search exceeded search time.");

                if (Random.Range(0f,100f) < chaseChance)
                {
                    chaseChance = chaseStartingChance;

                    DebugMsg("[SCARECROW]: Checking for valid players to target.");
                    for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
                    {
                        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
                        if (targetPlayer == null)
                        {
                            if (player.isPlayerControlled && !player.isInsideFactory && !player.isPlayerDead && player.isPlayerAlone && (player != attackerPlayer) && !player.isInHangarShipRoom)
                            {
                                targetPlayer = player;
                                continue;
                            }
                        }
                        else if (player.isPlayerControlled && !player.isInsideFactory && !player.isPlayerDead && player.isPlayerAlone && (player != attackerPlayer) && !player.isInHangarShipRoom)
                        {
                            if (Vector3.Distance(player.transform.position, transform.position) < Vector3.Distance(targetPlayer.transform.position, transform.position))
                            {
                                targetPlayer = player;
                            }
                        }
                    }

                    if (targetPlayer != null)
                    {
                        DebugMsg("[SCARECROW]: Closest valid player selected as target.");
                        SwitchOwnershipAndSyncStatesServerRpc(2, targetPlayer.actualClientId);
                    }
                    else
                    {
                        DebugMsg("[SCARECROW]: No valid players, restarting search.");
                        searchTimer = Random.Range(minSearchTime, maxSearchTime);
                    }
                }

                else
                {
                    DebugMsg("[SCARECROW]: Restarting search.");
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
                targetPlayer = playersInRange[0];
                SwitchOwnershipAndSyncStatesServerRpc(2, targetPlayer.actualClientId);
            }

            //IF MULTIPLE PLAYERS WITHIN RANGE
            else if (playersInRange.Count > 1)
            {
                SwitchToBehaviourState(3);
            }

            //IF ARMED PLAYERS WITHIN DEFENSE RANGE
            if (playersInDefenseRange.Count >= 1)
            {
                for (int i = 0; i < playersInDefenseRange.Count; i++)
                {
                    if (playersInDefenseRange[i].currentlyHeldObjectServer != null)
                    {
                        if (playersInDefenseRange[i].currentlyHeldObjectServer.itemProperties.isDefensiveWeapon || playersInDefenseRange[i].currentlyHeldObjectServer.itemProperties.itemName == "Easter egg")
                        {
                            SwitchToBehaviourState(3);
                            break;
                        }
                    }

                    if (attackerPlayer != null && playersInDefenseRange[i] == attackerPlayer)
                    {
                        SwitchToBehaviourState(3);
                        break;
                    }
                }
            }

            break;



        //CHASING
        case 2:

            //IF TARGET PLAYER IS NULL
            if (targetPlayer == null)
            {
                DebugMsg("[SCARECROW]: Target player is null, Returning to search.");
                SwitchToBehaviourState(1);
                break;
            }

            //IF TARGET PLAYER IS DEAD OR INACCESSIBLE
            if (!targetPlayer.isPlayerControlled || targetPlayer.isPlayerDead || targetPlayer.isInsideFactory || (targetPlayer.isInHangarShipRoom && StartOfRound.Instance.hangarDoorsClosed))
            {
                DebugMsg("[SCARECROW]: Target player dead or inaccessible, escaping.");
                SwitchToBehaviourState(3);
                break;
            }

            //PRELIMINARY CHECKS TO NOT PLAY DETECT SOUND AND SKIP STRAIGHT TO ESCAPING
            for (int i = 0; i < playersInDefenseRange.Count; i++)
            {
                if (playersInDefenseRange[i].currentlyHeldObjectServer != null)
                {
                    if (playersInDefenseRange[i].currentlyHeldObjectServer.itemProperties.isDefensiveWeapon || playersInDefenseRange[i].currentlyHeldObjectServer.itemProperties.itemName == "Easter egg")
                    {
                        SwitchToBehaviourState(3);
                        break;
                    }
                }

                if (attackerPlayer != null && playersInDefenseRange[i] == attackerPlayer)
                {
                    SwitchToBehaviourState(3);
                    break;
                }
            }

            //WHEN FIRST ENTERING CHASE STATE
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                instantScare = false;
                int randomMoves = Random.Range(minChaseMoves, maxChaseMoves);
                chaseMoves = Mathf.RoundToInt((dangerValue < 33) ? randomMoves * 0.8f : (dangerValue < 66) ? randomMoves : randomMoves * 1.2f);
                string debug = (dangerValue < 33) ? "LOW" : (dangerValue < 66) ? "MEDIUM" : "HIGH";
                DebugMsg($"[SCARECROW]: Chasing {targetPlayer.playerUsername} with {chaseMoves} moves. (danger mult: {debug})");
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

            //CONDUCTIVE OBJECT BEING ELECTROCUTED IN RANGE OF SCARECROW
            if (stormyWeather != null && stormyWeather.setStaticToObject != null && Vector3.Distance(base.transform.position, stormyWeather.setStaticToObject.transform.position) < 8)
            {
                DebugMsg("[SCARECROW]: Too close to conductive object, escaping.");
                SwitchToBehaviourState(3);
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
                            DebugMsg("[SCARECROW]: Out of chase moves, returning to search.");
                            MoveToRandomPosition(escaping: true);
                            SwitchToBehaviourState(1);
                            break;
                        }

                        StartCoroutine(MoveToTargetPlayer());
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
            else if (playersInDefenseRange.Count > 1)
            {
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
                    DebugMsg($"[SCARECROW]: Chasing {targetPlayer.playerUsername} with {chaseMoves} moves.");
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

                //ONCE PLAYER IS SET TO TARGET PLAYER
                if (targetPlayer == playersInRange[0] && !syncedBool[invisibleForOwner])
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
                                        tweakOutChance = tweakOutStartingChance;
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

                        if (!syncedBool[encountered])
                        {
                            SetSyncedBool(encountered, true);
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
                                    detectSoundTimer = detectSoundCooldown;
                                    if (Random.Range(0f,100f) < detectSoundChance)
                                    {
                                        PlayDetectSoundServerRpc();
                                        tweakOutTimer = tweakOutCooldown;
                                        detectSoundChance = detectSoundStartingChance;
                                    }
                                    else
                                    {
                                        detectSoundChance += detectSoundChanceIncrement;
                                    }
                                }

                                //CHANCE TO TWEAK OUT
                                if (tweakOutTimer <= 0)
                                {
                                    tweakOutTimer = tweakOutCooldown;
                                    if (Random.Range(0f,100f) < tweakOutChance)
                                    {
                                        TweakOutServerRpc((int)targetPlayer.playerClientId);
                                        detectSoundTimer = detectSoundCooldown;
                                        tweakOutChance = tweakOutStartingChance;
                                    }
                                    else
                                    {
                                        tweakOutChance += tweakOutChanceIncrement;
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
                DebugMsg("[SCARECROW]: Escaping.");
                targetPlayer = null;
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
            for (int i = 0; i < playersInDefenseRange.Count; i++)
            {
                if (playersInDefenseRange[i].currentlyHeldObjectServer != null)
                {
                    if (!syncedBool[desperate] && (playersInDefenseRange[i].currentlyHeldObjectServer.itemProperties.isDefensiveWeapon || playersInDefenseRange[i].currentlyHeldObjectServer.itemProperties.itemName == "Easter egg"))
                    {
                        decoySoundTimer = 0f;
                        SetSyncedBool(desperate, true);
                        if (IsOwner)
                        {
                            SetThreatenedValueServerRpc();
                        }
                        DebugMsg("[SCARECROW]: Player with weapon in range, desperate set to true.");
                        break;
                    }
                }
            }

            //IF ONE PLAYER HAS LOS
            if (playersWithLineOfSight.Count == 1)
            {

                //CHANCE TO TWEAK OUT IF NOT DESPERATE
                if (tweakOutTimer <= 0)
                {
                    tweakOutTimer = tweakOutCooldown;
                    if (Random.Range(0f,100f) < tweakOutChance && !syncedBool[desperate])
                    {
                        tweakOutChance = tweakOutStartingChance;
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

                //CHANCE TO PLAY DECOY SOUND OR SPAWN HALLUCINATION IF DESPERATE
                if (decoySoundTimer <= 0 && !syncedBool[desperate])
                {
                    decoySoundTimer = decoySoundCooldown * 0.8f;
                    if (Random.Range(0f,100f) < decoySoundChance + 20)
                    {
                        PlayDecoySoundServerRpc();
                        decoySoundChance = decoySoundStartingChance;
                    }
                    else
                    {
                        decoySoundChance += decoySoundChanceIncrement;
                    }
                }
                else if (decoySoundTimer <= 0 && syncedBool[desperate])
                {
                    decoySoundTimer = decoySoundCooldown;
                    if (Random.Range(0f,100f) < (!debugForceSpawnHallucination ? decoySoundChance * hallucinateChanceMultiplier : 100f)) 
                    {
                        RestartCoroutine(ref spawnHallucinationCoroutine, true, StartSpawnHallucination(hallucinationSpawnTime, debugForceSpawnHallucinationIndex, debugForceSpawnHallucinationBehaviour));
                        decoySoundChance = decoySoundStartingChance;
                    }
                    else
                    {
                        if (Random.Range(0f,100f) < decoySoundChance + 25 + (20 * hallucinateChanceMultiplier))
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

            break;

        }
    }

    public void MoveToRandomPosition(bool escaping = false, bool instant = false, bool spawn = false)
    {
        int moveAttempts = 0;
        Vector3 newPosition = GetRandomNavMeshPositionNearAINode();
        while(!CheckPositionIsValid(newPosition, escaping, spawn))
        {
            if (moveAttempts < 20)
            {
                newPosition = GetRandomNavMeshPositionNearAINode();
                moveAttempts++;
            }
            else
            {
                DebugMsg($"[SCARECROW]: Failed to find valid position near AI node after {moveAttempts} tries, restarting move cooldown.");
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                return;
            }
        }
        RestartCoroutine(ref changePositionCoroutine, true, ChangePositionWhileInvisible(newPosition, instant ? teleportEndClip.length : teleportStartClip.length + teleportEndClip.length, instant));
        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    public IEnumerator MoveToTargetPlayer(bool instant = false)
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

                yield return null;

                DebugMsg($"[SCARECROW]: Failed to find valid position near player after {chaseAttempts} tries, restarting move cooldown.");
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown) * 0.5f;
                yield break;
            }
        }
        RestartCoroutine(ref changePositionCoroutine, true, ChangePositionWhileInvisible(newPosition, instant ? teleportEndClip.length : teleportStartClip.length + teleportEndClip.length, instant));
        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    public bool CheckPositionIsValid(Vector3 newPosition, bool escaping = false, bool spawn = false)
    {
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        float c;

        DebugMsg($"[SCARECROW]: Trying new position: {newPosition}");

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
                        DebugMsg($"[SCARECROW]: LOS trigger visible to {players[i].playerUsername} in new position.");
                        inViewOfPlayer = true;
                        break;
                    }
                }
            }

            //PREVENT FROM MOVING NEAR PLAYERS WHEN ESCAPING
            if (escaping && Vector3.Distance(newPosition, players[i].transform.position) < escapeRange)
            {
                DebugMsg($"[SCARECROW]: Position too close to players while escaping, Did not move.");
                return false;
            }
        }

        if (inViewOfPlayer)
        {
            c = Random.Range(0f,100f);
            {
                if (c > 50f)
                {
                    DebugMsg($"[SCARECROW]: Did not move.");
                    return false;
                }
            }
        }

        Vector3 shipPosition = new Vector3(0f, 1.5f, -14f);
        if (Vector3.Distance(shipPosition, newPosition) < shipSafetyRadius)
        {
            DebugMsg("[SCARECROW]: Position too close to ship, did not move.");
            return false;
        }

        bool onInvalidTerrain = false;
        RaycastHit hitInfo;
        Physics.Raycast(newPosition, Vector3.down, out hitInfo, StartOfRound.Instance.collidersAndRoomMaskAndDefault);
        for (int i = 0; i < invalidTerrainTags.Length; i++)
        {
            if (hitInfo.collider.gameObject.tag == invalidTerrainTags[i])
            {
                DebugMsg($"[SCARECROW]: New position on {invalidTerrainTags[i]}.");
                onInvalidTerrain = true;
            }
        }

        if (onInvalidTerrain)
        {
            DebugMsg("[SCARECROW]: Attempting to move to invalid terrain.");
            c = Random.Range(0f,100f);
            if (c > 90f)
            {
                DebugMsg("[SCARECROW]: Did not move.");
                return false;
            }
        }

        if (Vector3.Angle(Vector3.up, hitInfo.normal) > 35f)
        {
            DebugMsg("[SCARECROW]: New position on too steep of ground, did not move.");
            return false;
        }

        Collider[] headCollisions = Physics.OverlapSphere(newPosition + scareTriggerTransform.localPosition, 0.1f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);
        if (headCollisions.Length > 0)
        {
            DebugMsg("[SCARECROW]: New position obscures head, did not move.");
            return false;
        }

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            if (newPosition.y < floodWeather.gameObject.transform.position.y)
            {
                DebugMsg($"[SCARECROW]: New position y ({newPosition.y}) is under current flood level ({floodWeather.gameObject.transform.position.y}), did not move.");
                return false;
            }
        }

        if (stormyWeather != null && stormyWeather.setStaticToObject != null && Vector3.Distance(newPosition, stormyWeather.setStaticToObject.transform.position) < 8)
        {
            DebugMsg($"[SCARECROW]: New position too close to conductive object, did not move.");
            return false;
        }

        c = Random.Range(0f,100f);
        for (int i = 0; i < spawnDenialPoints.Length; i++)
        {
            if (Vector3.Distance(newPosition, spawnDenialPoints[i].transform.position) < 30)
            {
                DebugMsg($"[SCARECROW]: New position too close to spawn denial point.");
                if (c < 80f)
                {
                    DebugMsg("[SCARECROW]: Did not move.");
                    return false;
                }
            }
        }

        Collider[] bodyCollisions = Physics.OverlapCapsule(newPosition + Vector3.up * 1f, newPosition + Vector3.up * 2.5f, 0.1f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);
        if (bodyCollisions.Length > 0)
        {
            DebugMsg("[SCARECROW]: New position obscures body, did not move.");
            return false;
        }

        //PREVENT FROM MOVING TO INACCESSIBLE NAVMESH ISLANDS WHEN CHASING A PLAYER
        if (!escaping && targetPlayer != null)
        {
            bool pathValid = NavMesh.CalculatePath(newPosition, RoundManager.Instance.GetNavMeshPosition(targetPlayer.transform.position, sampleRadius: 8f), agent.areaMask, path1);
            if (!pathValid)
            {
                DebugMsg("[SCARECROW]: Path from position to player invalid, did not move.");
                CreateDebugNode(newPosition, "INVALID newPosition", 1);
                return false;
            }
            if (pathValid && path1.status == NavMeshPathStatus.PathPartial)
            {
                DebugMsg("[SCARECROW]: Path from position to player partial, did not move.");
                CreateDebugNode(newPosition, "PARTIAL newPosition", 0);
                return false;
            }
        }

        //PREVENT FROM SPAWNING ON INACCESSIBLE NAVMESH ISLANDS AT THE START OF THE DAY
        if (spawn)
        {
            List<GameObject> nodeDistList = [.. nodes.OrderBy(node => (node.transform.position - newPosition).sqrMagnitude)];
            Vector3 nodePos1 = RoundManager.Instance.GetNavMeshPosition(nodeDistList[0].transform.position);
            Vector3 nodePos2 = RoundManager.Instance.GetNavMeshPosition(nodeDistList[1].transform.position);
            DebugMsg($"NodePos1: {nodePos1}, NodePos2: {nodePos2}");
            bool pathValid1 = NavMesh.CalculatePath(newPosition, nodePos1, -1, path1);
            bool pathValid2 = NavMesh.CalculatePath(newPosition, nodePos2, -1, path1);
            if (!pathValid1 || !pathValid2)
            {
                DebugMsg("[SCARECROW]: Path from spawn position to nearest two nodes invalid, did not move.");
                CreateDebugNode(newPosition, "INVALID Spawn Position", 1);
                CreateDebugNode(nodePos1, "NodePosOne", 0);
                CreateDebugNode(nodePos2, "NodePosTwo", 0);
                return false;
            }
        }

        if (attackerPlayer != null && Vector3.Distance(newPosition, attackerPlayer.transform.position) < escapeRange)
        {
            DebugMsg($"[SCARECROW]: Position too close to previous attacker, Did not move.");
            return false;
        }

        return true;
    }

    public PlayerControllerB? NearestPlayer()
    {
        PlayerControllerB? nearestPlayer = null;
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

    private IEnumerator ChangePositionWhileInvisible(Vector3 position, float time, bool instant)
    {
        DebugMsg("[SCARECROW]: Move coroutine STARTED!");
        SetInvisible(true);
        StartCoroutine(MoveWithAnimation(position, instant));
        TeleportAnimationServerRpc(position, instant, (int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        yield return new WaitForSeconds(time);
        while (playersWithLineOfSight.Count != 0)
        {
            yield return null;
        }
        SetSyncedBool(stopDetecting, false);
        SetInvisible(false);
        DebugMsg("[SCARECROW]: Move coroutine FINISHED!");
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeleportAnimationServerRpc(Vector3 position, bool instant, int clientWhoSentRpc)
    {
        TeleportAnimationClientRpc(position, instant, clientWhoSentRpc);
    }

    [ClientRpc]
    private void TeleportAnimationClientRpc(Vector3 position, bool instant, int clientWhoSentRpc)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            if (GameNetworkManager.Instance.localPlayerController.isPlayerDead)
            {
                StartCoroutine(MoveWithAnimation(position, instant));
            }
            else
            {
                StartCoroutine(MoveWithAnimation(position, instant, fade: true));
            }
        }
    }

    private IEnumerator MoveWithAnimation(Vector3 position, bool instant, bool fade = false)
    {
        SetSyncedBool(syncPosition, false);
        if (!fade)
        {
            //PLAYS FOR OWNER (WHILE INVISIBLE) & DEAD NON-OWNERS (WHILE VISIBLE)
            if (!instant)
            {
                creatureAnimator.SetTrigger("TeleportStart");
                yield return new WaitForSeconds(teleportStartClip.length);
            }
            if (IsOwner)
            {
                agent.Warp(position);
            }
            else
            {
                agent.transform.position = position;
            }
            SetSyncedBool(syncPosition, true);
            while (syncedBool[invisibleForOwner])
            {
                yield return null;
            }
            if (IsOwner)
            {
                creatureAnimator.SetTrigger("TeleportEndInstant");
            }
            else
            {
                creatureAnimator.SetTrigger("TeleportEnd");
            }
        }
        else
        {
            //PLAYS FOR ALIVE NON-OWNERS
            if (!instant)
            {
                StartCoroutine(FadeScarecrow(0.15f));
                yield return new WaitForSeconds(0.15f);
                SetInvisible(true, syncBool: false);
            }
            else
            {

                yield return null;
                SetInvisible(true, syncBool: false);
            }
            agent.transform.position = position;
            while (syncedBool[invisibleForOwner])
            {
                yield return null;
            }
            SetInvisible(false, syncBool: false);
            if (instant)
            {
                creatureAnimator.SetTrigger("TeleportEndInstant");
            }
            StartCoroutine(FadeScarecrow(0.15f, fadeIn: true));
        }

        PlayerControllerB? nearestPlayer = NearestPlayer();
        if (nearestPlayer != null)
        {
            FacePosition(nearestPlayer.transform.position);
        }
    }

    private void SetInvisible(bool enabled, bool syncBool = true)
    {
        if (enabled == true)
        {
            DebugMsg("[SCARECROW]: Set invisible.");
            EnableEnemyMesh(false);
            enemyCollider.isTrigger = true;
            scanNode.gameObject.SetActive(false);
            if (syncBool)
            {
                SetSyncedBool(invisibleForOwner, true);
            }
        }
        else
        {
            DebugMsg("[SCARECROW]: Set visible.");
            EnableEnemyMesh(true);
            enemyCollider.isTrigger = false;
            scanNode.gameObject.SetActive(true);
            if (syncBool)
            {
                SetSyncedBool(invisibleForOwner, false);
            }
        }
    }

    private IEnumerator FadeScarecrow(float duration, bool fadeIn = false)
    {
        float timeElapsed = 0f;
        float meshOpacity;
        bodyRenderer.sharedMaterials = scarecrowMatsDither;
        rotRenderer.sharedMaterials = scarecrowMatsRotDither;
        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            meshOpacity = Mathf.Lerp(fadeIn ? 0f : 1f, fadeIn ? 1f : 0f, timeElapsed / duration);
            for (int i = 0; i < bodyRenderer.sharedMaterials.Length; i++)
            {
                var color = bodyRenderer.sharedMaterials[i].color;
                color.a = meshOpacity;
                bodyRenderer.sharedMaterials[i].color = color;
            }
            for (int i = 0; i < rotRenderer.sharedMaterials.Length; i++)
            {
                var color = rotRenderer.sharedMaterials[i].color;
                color.a = meshOpacity;
                rotRenderer.sharedMaterials[i].color = color;
            }
            yield return null;
        }
        yield return null;
        bodyRenderer.sharedMaterials = scarecrowMats;
        rotRenderer.sharedMaterials = scarecrowMatsRot;
    }

    public Vector3 GetRandomNavMeshPositionNearAINode(float radius = 16f)
    {
        int nodeSelected = Random.Range(0, nodes.Count);
        Vector3 nodePosition = nodes[nodeSelected].transform.position;
        Vector3 newPosition = RoundManager.Instance.GetRandomNavMeshPositionInRadius(nodePosition, radius);
        return PositionAwayFromWall(newPosition);
    }

    public Vector3 GetRandomNavMeshPositionNearPlayer(PlayerControllerB player, float radius = 10f)
    {
        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy)
        {
            radius *= 1.35f;
        }
        Vector3 newPosition = GetRandomNavMeshPositionMinAndMax(player.transform.position, radius/3f, radius);
        return PositionAwayFromWall(newPosition);
    }

    //TODO: remake lazy stallable while loop with simpler math method, such as in RoundManager.Instance.GetRandomPositionInRadius()
    public Vector3 GetRandomNavMeshPositionMinAndMax(Vector3 pos, float minRadius = 2f, float maxRadius = 10f)
    {
        int radiusAttempts = 0;
		Vector3 tryPos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, maxRadius);
        while (Vector3.Distance(tryPos, pos) < minRadius)
        {
            if (radiusAttempts < 20)
            {
                tryPos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, maxRadius);
                radiusAttempts++;
            }
            else
            {
                DebugMsg($"[SCARECROW]: Returned position under minimum radius after {radiusAttempts} attempts.");
                return pos;
            }
        }
        return tryPos;
    }

    public Vector3 PositionAwayFromWall(Vector3 pos, float maxDistance = 3f, int resolution = 12)
    {
        Vector3 newPosition = pos;
        float shortestDistance = maxDistance;
        float yRotation = -1;

        //CAST RAYS AROUND POSITION TO FIND NEAREST WALL
        for (int i = 0; i < 360; i += 360/resolution)
        {
            RoundManager.Instance.tempTransform.eulerAngles = new Vector3(0f, i, 0f);
            if (Physics.Raycast(pos + Vector3.up * 2f, RoundManager.Instance.tempTransform.forward, out var hitInfo, maxDistance, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
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
            RoundManager.Instance.tempTransform.eulerAngles = new Vector3(0f, yRotation, 0f);
            newPosition = pos - RoundManager.Instance.tempTransform.forward * (maxDistance - shortestDistance);

            //ENSURE NEW POSITION IS ON GROUND
            Vector3 checkFromPosition = newPosition + Vector3.up * 2f;
            if (Physics.Raycast(checkFromPosition, Vector3.down, out var hitInfo, 8f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                newPosition = RoundManager.Instance.GetNavMeshPosition(hitInfo.point);
            }
            else
            {
                newPosition = pos;
            }
        }

        return newPosition;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ScarePlayerServerRpc(int playerId, float lerpLength = 0.15f, bool animation = true)
    {
        int scareSound = Random.Range(0, scareSounds.Length);
        ScarePlayerClientRpc(playerId, scareSound, lerpLength, animation);
    }

    [ClientRpc]
    public void ScarePlayerClientRpc(int playerId, int scareSound, float lerpLength, bool animation = true)
    {
        ScarePlayer(playerId, scareSound, lerpLength, animation);
    }

    public void ScarePlayer(int playerId, int scareSound, float lerpLength, bool animation = true)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];

        scareAudio.clip = scareSounds[scareSound];
        scareAudio.Play();
        WalkieTalkie.TransmitOneShotAudio(scareAudio, scareAudio.clip);
        StartCoroutine(FaceTransformForTime(player.transform, scareAudio.clip.length/2, lerpLength));
        RoundManager.Instance.PlayAudibleNoise(transform.position, noiseRange, 1, 0, false, -1);
        scarePrimed = false;
        instantScare = false;
        if (animation)
        {
            creatureAnimator.SetTrigger("ScarePlayer");
        }

        if (GameNetworkManager.Instance.localPlayerController == player)
		{
            player.insanityLevel += player.maxInsanityLevel * 0.2f;
            player.JumpToFearLevel(0.5f);
            fullySpotted = true;
        }

        StopInterruptibles();

        DebugMsg($"[SCARECROW]: Scarecrow scared player {player.playerUsername}.");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TweakOutServerRpc(int playerId)
    {
        int newSound = Random.Range(0, tweakOutSounds.Length);
        while (newSound == tweakSound)
        {
            newSound = Random.Range(0, tweakOutSounds.Length);
        }
        tweakSound = newSound;
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

        DebugMsg("[SCARECROW]: Scarecrow tweaked out!");
        tweakOutAudio.clip = tweakOutSounds[tweakSound];
        tweakOutAudio.pitch = Random.Range(0.9f, 1.1f);
        tweakOutAudio.Play();
        WalkieTalkie.TransmitOneShotAudio(tweakOutAudio, tweakOutAudio.clip);
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

    public IEnumerator FaceTransformForTime(Transform transform, float length, float lerpLength = 0.15f)
    {
        yield return StartCoroutine(FacePositionLerp(transform.position, lerpLength));

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
        int newSound = Random.Range(0, detectSounds.Length);
        while (newSound == detectSound)
        {
            newSound = Random.Range(0, detectSounds.Length);
        }
        detectSound = newSound;
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
    public void PlayDecoySoundServerRpc()
    {
        int newSound = Random.Range(0, decoySounds.Length);
        while (newSound == decoySound)
        {
            newSound = Random.Range(0, detectSounds.Length);
        }
        decoySound = newSound;
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
        decoyAudio.volume = 0.96f;
        decoyAudio.clip = decoySounds[decoySound];
        decoyAudio.pitch = Random.Range(0.9f, 1.1f);
        decoyAudio.Play();
        WalkieTalkie.TransmitOneShotAudio(decoyAudio, decoyAudio.clip);
        StartCoroutine(KeepDecoyPosition());
    }

    public IEnumerator KeepDecoyPosition()
    {
        decoyAudioPlaying = true;

        Vector3 oldpos = decoyAudio.transform.position;
        float timeElapsed = 0f;
        float duration = decoyAudio.clip.length;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            decoyAudio.transform.position = oldpos;
            yield return null;
        }

        decoyAudioPlaying = false;
    }

    public IEnumerator KeepFadePosition(float duration, Transform target)
    {
        float timeElapsed = 0f;

        while (timeElapsed < duration && target != null)
        {
            timeElapsed += Time.deltaTime;

            ghostFadeSource.transform.position = target.transform.position + Vector3.up * 1.5f;

            yield return null;
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (isEnemyDead)
        {
            return;
        }
        StopInterruptibles();
        enemyHP -= force;
        if (playerWhoHit != null)
        {
            RememberAttackerServerRpc((int)playerWhoHit.playerClientId);
        }
        if (force >= 6)
        {
            StopInterruptibles();
            creatureAnimator.SetTrigger("Explode");
            isEnemyDead = true;
            SwitchToBehaviourState(0);
        }
        if (enemyHP <= 0 && !isEnemyDead)
        {
            creatureAnimator.SetTrigger("Die");
            isEnemyDead = true;
            SwitchToBehaviourState(0);
        }
        if (enemyHP > 0)
        {
            creatureAnimator.SetTrigger("TakeDamage");
            if (IsOwner)
            {
                SetThreatenedValueServerRpc(incrementAmount: 2);
            }
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

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1f, PlayerControllerB setStunnedByPlayer = null)
    {
        if (isEnemyDead || !enemyType.canBeStunned)
        {
            return;
        }

        if (setToStunned)
        {
            stunnedByPlayer = setStunnedByPlayer;
            if (setToStunTime == 0.25f)
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
        DebugMsg("[SCARECROW]: Called KillEnemy!");
        StopInterruptibles();
        IncreaseEnemySpawnRate();
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
        DebugMsg("[SCARECROW]: Called DropItemServerRpc!");
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
                RoundManager.Instance.totalScrapValueInLevel += aObject.scrapValue;
            }
        }
        else if (gObject != null)
        {
            gObject.SetScrapValue(RemapInt(value, startValue, endValue, startValue*0.45f, endValue*1.85f));
            RoundManager.Instance.totalScrapValueInLevel += gObject.scrapValue;
        }
        StartCoroutine(DropItemStupidly(dropObject));
    }

    public IEnumerator DropItemStupidly(GameObject dropitem)
    {
        yield return null;
        dropitem.transform.position = dropItemTransform.position;
        dropitem.GetComponent<GrabbableObject>().FallToGround();
        dropitem.GetComponent<GrabbableObject>().hasHitGround = false;
    }

    private void IncreaseEnemySpawnRate()
    {
        DebugMsg($"[SCARECROW]: Original minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        DebugMsg($"[SCARECROW]: Original max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");
        RoundManager.Instance.minOutsideEnemiesToSpawn += enemySpawnIncrease;
        RoundManager.Instance.currentMaxOutsidePower += enemyPowerIncrease;
        DebugMsg($"[SCARECROW]: Increased minimum outside enemies to spawn: {RoundManager.Instance.minOutsideEnemiesToSpawn}");
        DebugMsg($"[SCARECROW]: Increased max outside enemy power: {RoundManager.Instance.currentMaxOutsidePower}");

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
        fullySpotted = false;
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
        while (!fullySpotted)
        {
            bool nearDetectRange = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, transform.position) < detectRange*1.4;
            bool nearScareRange = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, transform.position) < scareRange*1.4;
            float firstSpottedAngle;
            firstSpottedAngle = nearScareRange ? spottedAngle * 5f : nearDetectRange ? spottedAngle * 3f : spottedAngle;
            if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(scareTriggerTransform.position, firstSpottedAngle, spottedDistance) && !fullySpotted && !syncedBool[invisibleForOwner])
            {
                initiallySpotted = true;
                spottedTimer -= Time.deltaTime;

                SoundManager.Instance.musicSource.volume = Mathf.Lerp(SoundManager.Instance.musicSource.volume, 0f, 3f*Time.deltaTime);
                
                if (spottedTimer <= 0f)
                {
                    fullySpotted = true;
                    DebugMsg("[SCARECROW]: Fully spotted, starting music.");
                    if (!syncedBool[encountered]) 
                    {
                        SetSyncedBool(encountered, true);
                    }
                    RestartCoroutine(ref playMusicLocallyCoroutine, true, PlaySpottedMusicLocally());
                }
            }
            else if (initiallySpotted)
            {
                SoundManager.Instance.musicSource.volume = Mathf.Lerp(SoundManager.Instance.musicSource.volume, 0.85f, 3f*Time.deltaTime);

                if (spottedTimer <= initialSpottedTimer)
                {
                    spottedTimer += Time.deltaTime;
                }
                else
                {
                    spottedTimer = initialSpottedTimer;
                    initiallySpotted = false;
                }
            }

            yield return null;
        }
    }

    public IEnumerator PlaySpottedMusicLocally()
    {
        if (useSaveFileForMusic)
        {
            if (ES3.Load(musicSaveFileKey, CoronaMod.Info.GeneralSaveFileName, defaultValue: false))
            {
                DebugMsg("[SCARECROW]: Not playing music: You have already heard scarecrow music for the first time.");
                yield break;
            }
        }

        float maxMusicVolume;
        float minDiageticVolume;

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Rainy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded || StartOfRound.Instance.currentLevel.PlanetName == "85 Rend" || StartOfRound.Instance.currentLevel.PlanetName == "8 Titan" || StartOfRound.Instance.currentLevel.PlanetName == "91 Bellow")
        {
            maxMusicVolume = 1f;
            minDiageticVolume = -9f;
        }
        else
        {
            maxMusicVolume = 0.85f;
            minDiageticVolume = -6.5f;
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
        float timeElapsed = 0f;
        float duration = spottedAudio.clip.length;
        bool watching = false;
        IterateJob increaseDiagetic = new("increase diagetic volume", 0f, null, false);
        IterateJob decreaseDiagetic = new("decrease diagetic volume", 0f, null, false);
        IterateJob increaseMusic = new("increase music volume", 0f, null, false);
        IterateJob decreaseMusic = new("decrease music volume", 0f, null, false);

        while (timeElapsed < duration * 0.85)
        {
            timeElapsed += Time.deltaTime;

            SoundManager.Instance.playingOutsideMusic = false;

            if (StartOfRound.Instance.audioListener == null || GameNetworkManager.Instance.localPlayerController.isPlayerDead || StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Eclipsed)
            {
                goto END;
            }

            if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(scareTriggerTransform.position, spottedAngle*4f, spottedDistance, scareRange) && !syncedBool[invisibleForOwner])
            {
                if (!watching)
                {
                    watching = true;
                    RestartCoroutine(ref increaseDiagetic.Coroutine, false);
                    RestartCoroutine(ref decreaseMusic.Coroutine, false);
                    RestartCoroutine(ref decreaseDiagetic.Coroutine, true, LerpIncrement(decreaseDiagetic, increaseDiagetic.Value, minDiageticVolume, 3.5f, 0.25f));
                    RestartCoroutine(ref increaseMusic.Coroutine, true, LerpIncrement(increaseMusic, decreaseMusic.Value, maxMusicVolume, 3f, 0f));
                }
                SoundManager.Instance.SetDiageticMasterVolume(decreaseDiagetic.Value);
                spottedAudio.volume = increaseMusic.Value;
            }
            else
            {
                if (watching)
                {
                    watching = false;
                    RestartCoroutine(ref decreaseDiagetic.Coroutine, false);
                    RestartCoroutine(ref increaseMusic.Coroutine, false);
                    RestartCoroutine(ref increaseDiagetic.Coroutine, true, LerpIncrement(increaseDiagetic, decreaseDiagetic.Value, 0f, 2f, 0.25f));
                    RestartCoroutine(ref decreaseMusic.Coroutine, true, LerpIncrement(decreaseMusic, increaseMusic.Value, 0f, 2f, 0f));
                }
                SoundManager.Instance.SetDiageticMasterVolume(increaseDiagetic.Value);
                spottedAudio.volume = decreaseMusic.Value;

                if (timeElapsed < duration*0.45f && decreaseMusic.Done)
                {
                    goto END_EARLY;
                }
            }

            yield return null;
        }

        if (useSaveFileForMusic)
        {
            ES3.Save(musicSaveFileKey, true, CoronaMod.Info.GeneralSaveFileName);
            DebugMsg("[SCARECROW]: Saved: You have heard scarecrow music for the first time.");
        }

        END:
        DebugMsg("[SCARECROW]: Spotted music ended.");
        SoundManager.Instance.SetDiageticMasterVolume(0f);
        spottedAudio.volume = 0f;
        playMusicLocallyCoroutine = null;
        yield break;

        END_EARLY:
        DebugMsg("[SCARECROW]: Spotted music ended early, resetting.");
        SoundManager.Instance.SetDiageticMasterVolume(0f);
        spottedAudio.volume = 0f;
        fullySpotted = false;
        initiallySpotted = false;
        spottedTimer = initialSpottedTimer;
        StartCoroutine(WaitToBeSpotted());
        playMusicLocallyCoroutine = null;
        yield break;
    }

    //FOR TESTING
    public void DeleteSpecifiedSaveData(string specifiedKey, string specifiedSave)
    {
        if (ES3.KeyExists(specifiedKey, specifiedSave))
        {
            ES3.DeleteKey(specifiedKey, specifiedSave);
            Debug.LogWarning($"[SCARECROW]: Deleted key {specifiedKey} in {specifiedSave}.");
        }
        else
        {
            Debug.LogError($"[SCARECROW]: key {specifiedKey} in {specifiedSave} does not exist.");
        }
    }

    public void RestartCoroutine(ref Coroutine coroutine, bool restart, IEnumerator ienumerator = null)
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
        DebugMsg($"SwitchOwnershipAndSyncStatesServerRpc called!");
        if (thisNetworkObject.OwnerClientId != newOwner)
        {
            thisNetworkObject.ChangeOwnership(newOwner);
        }
        if (StartOfRound.Instance.ClientPlayerList.TryGetValue(newOwner, out var playerId))
        {
            targetPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
            SwitchOwnershipAndSyncStatesClientRpc(playerId, state);
        }
    }

	[ClientRpc]
	public void SwitchOwnershipAndSyncStatesClientRpc(int playerId, int state)
    {
        DebugMsg($"SwitchOwnershipAndSyncStatesClientRpc called!");
        currentOwnershipOnThisClient = playerId;
        base.transform.position = serverPosition;
        SwitchToBehaviourStateOnLocalClient(state);
        targetPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
    }

    public void StopInterruptibles()
    {
        if (decoyAudioPlaying)
        {
            DebugMsg("[SCARECROW]: Stopping interruptible: Decoy audio.");
            decoyAudio.Stop();
        }
        if (playMusicLocallyCoroutine != null)
        {
            DebugMsg("[SCARECROW]: Stopping interruptible: Local music audio.");
            RestartCoroutine(ref playMusicLocallyCoroutine, false);
            SoundManager.Instance.SetDiageticMasterVolume(0f);
            spottedAudio.volume = 0f;
        }
        if (spawnHallucinationCoroutine != null)
        {
            DebugMsg("[SCARECROW]: Stopping interruptible: Spawning hallucination.");
            RestartCoroutine(ref spawnHallucinationCoroutine, false);
        }
        if (syncedBool[hallucinationLive])
        {
            SetSyncedBool(hallucinationLive, false);
            DebugMsg("[SCARECROW]: Stopping interruptible: Live hallucination.");
            StartCoroutine(DespawnHallucination());
        }
    }

    public void CreateDebugNode(Vector3 position, string name, int type)
    {
        if (debugEnemyAI && useDebugNodes)
        {
            GameObject debugNode = Instantiate(scanNode.gameObject, position, Quaternion.identity);
            debugNodes.Add(debugNode);
            debugNode.GetComponent<ScanNodeProperties>().minRange = 1;
            debugNode.GetComponent<ScanNodeProperties>().maxRange = 300;
            debugNode.GetComponent<ScanNodeProperties>().headerText = $"{name} (Node {debugNodes.Count})";
            debugNode.GetComponent<ScanNodeProperties>().subText = $"{position}";
            debugNode.GetComponent<ScanNodeProperties>().nodeType = type;
            debugNode.GetComponent<ScanNodeProperties>().creatureScanID = -1;
            debugNode.GetComponent<ScanNodeProperties>().requiresLineOfSight = false;
            DebugMsg($"[SCARECROW]: Placed debug node at {position}.", warning: true);
        }
    }

    private void SetSyncedBool(string set, bool setto)
    {
        if (syncedBool[set] == setto)
        {
            return;
        }
        DebugMsg($"[SCARECROW]: THIS client setting bool {set} to {setto}.");
        syncedBool[set] = setto;
        SetSyncedBoolServerRpc(set, setto);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetSyncedBoolServerRpc(string set, bool setto)
    {
        SetSyncedBoolClientRpc(set, setto);
    }

    [ClientRpc]
    private void SetSyncedBoolClientRpc(string set, bool setto)
    {
        if (syncedBool[set] == setto)
        {
            return;
        }
        DebugMsg($"[SCARECROW]: OTHER client setting bool {set} to {setto}.");
        syncedBool[set] = setto;
    }

    //FOR ANIMATION EVENT
    public void SetInSpecialAnimation(bool set = true)
    {
        inSpecialAnimation = set;
    }

    //FOR ANIMATION EVENT
    public void MoveAndLoseTarget()
    {
        if (IsOwner)
        {
            SetSyncedBool(stopDetecting, true);
            MoveToRandomPosition(escaping: true, instant: true);
            SwitchToBehaviourState(1);
        }
    }

    //FOR ANIMATION EVENT
    public void ScareAttackingPlayer()
    {
        if (IsOwner)
        {
            ScarePlayerServerRpc((int)attackerPlayer.playerClientId, 0.24f, animation: false);
        }
    }

    //FOR ANIMATION EVENT
    public void CreateTeleportParticles(bool sound = true)
    {
        GameObject burrowPrefab = Instantiate(burrowContainer, rootBone.position + Vector3.down * 0.5f, Quaternion.identity);
        burrowPrefab.SetActive(true);
        if (sound)
        {
            burrowPrefab.GetComponent<AudioSource>().clip = burrowClips[Random.Range(0, burrowClips.Length)];
            burrowPrefab.GetComponent<AudioSource>().Play();
            StartCoroutine(PlayTunnelingSound(burrowPrefab.transform.position));
        }
        burrowPrefab.GetComponentInChildren<DecalProjector>().enabled = true;
        burrowPrefabs.Add(burrowPrefab);
        if (burrowPrefabs.Count > 12)
        {
            DebugMsg("[SCARECROW]: Removing and destroying oldest burrow decal prefab.");
            Destroy(burrowPrefabs[0]);
            burrowPrefabs.RemoveAt(0);
        }
    }

    public IEnumerator PlayTunnelingSound(Vector3 startPosition)
    {
        tunnelSource.clip = tunnelClips[Random.Range(0, tunnelClips.Length)];
        tunnelSource.pitch = Random.Range(0.9f, 1.1f);
        tunnelSource.Play();
        float duration = 3.5f;
        float timer = 0f;
        tunnelSource.volume = 1f;
        if (Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, startPosition) < 12f)
        {
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
        }
        while (timer < duration)
        {
            timer += Time.deltaTime;
            Vector3 direction = Vector3.Normalize(startPosition - base.transform.position);
            Vector3 endPosition = startPosition + direction * 8f;
            tunnelSource.transform.position = Vector3.Lerp(startPosition + Vector3.down * 3, endPosition + Vector3.down * 3, timer / duration * 0.25f);
            tunnelSource.volume = Mathf.Lerp(1, 0, timer / duration);
            yield return null;
        }
        tunnelSource.volume = 0f;
    }

    public void StartTransmission()
    {
        if (syncedBool[encountered])
        {
            DebugMsg("[SCARECROW]: No need to send transmission");
            return;
        }
        if (dangerValue < Random.Range(20f, 45f))
        {
            DebugMsg($"[SCARECROW]: No luck for transmission");
            return;
        }
        int setTalkClip;
        int setReplyClip;
        int[] talkClipsPlayed = [];
        int[] replyClipsPlayed = [];
        bool allTalkClipsPlayed = true;
        bool allReplyClipsPlayed = true;
        if (!useSaveFileForTransmit)
        {
            DebugMsg("[SCARECROW]: Not using save data for transmission");
            setTalkClip = Random.Range(0, talkClips.Length);
            setReplyClip = Random.Range(0, replyClips.Length);
            talkClip = setTalkClip;
            replyClip = setReplyClip;
            SendTransmissionServerRpc(setTalkClip, setReplyClip);
            return;
        }
        if (!ES3.KeyExists(talkSaveFileKey, CoronaMod.Info.GeneralSaveFileName))
        {
            DebugMsg($"[SCARECROW]: {talkSaveFileKey} does not exist in {CoronaMod.Info.GeneralSaveFileName}, first time?");
            setTalkClip = Random.Range(0, talkClips.Length);
        }
        else
        {
            DebugMsg($"[SCARECROW]: {talkSaveFileKey} exists in {CoronaMod.Info.GeneralSaveFileName}");
            talkClipsPlayed = ES3.Load<int[]>(talkSaveFileKey, CoronaMod.Info.GeneralSaveFileName);
            for (int i = 0; i < talkClips.Length; i++)
            {
                if (!talkClipsPlayed.Contains(i))
                {
                    allTalkClipsPlayed = false;
                    break;
                }
                else
                {
                    continue;
                }
            }
            if (allTalkClipsPlayed)
            {
                DebugMsg($"[SCARECROW]: All talk clips played, resetting memory");
                talkClipsPlayed = [];
                allTalkClipsPlayed = false;
            }
            setTalkClip = Random.Range(0, talkClips.Length);
            while (!allTalkClipsPlayed && talkClipsPlayed.Contains(setTalkClip))
            {
                setTalkClip = Random.Range(0, talkClips.Length);
            }
        }
        if (!ES3.KeyExists(replySaveFileKey, CoronaMod.Info.GeneralSaveFileName))
        {
            DebugMsg($"[SCARECROW]: {replySaveFileKey} does not exist in {CoronaMod.Info.GeneralSaveFileName}, first time?");
            setReplyClip = Random.Range(0, replyClips.Length);
        }
        else
        {
            DebugMsg($"[SCARECROW]: {replySaveFileKey} exists in {CoronaMod.Info.GeneralSaveFileName}");
            replyClipsPlayed = ES3.Load<int[]>(replySaveFileKey, CoronaMod.Info.GeneralSaveFileName);
            for (int i = 0; i < replyClips.Length; i++)
            {
                if (!replyClipsPlayed.Contains(i))
                {
                    allReplyClipsPlayed = false;
                    break;
                }
                else
                {
                    continue;
                }
            }
            if (allReplyClipsPlayed)
            {
                DebugMsg($"[SCARECROW]: All reply clips played, resetting memory");
                replyClipsPlayed = [];
                allReplyClipsPlayed = false;
            }
            setReplyClip = Random.Range(0, replyClips.Length);
            while (!allReplyClipsPlayed && replyClipsPlayed.Contains(setReplyClip))
            {
                setReplyClip = Random.Range(0, replyClips.Length);
            }
        }
        talkClipsPlayed = [.. talkClipsPlayed, setTalkClip];
        replyClipsPlayed = [.. replyClipsPlayed, setReplyClip];
        ES3.Save(talkSaveFileKey, talkClipsPlayed, CoronaMod.Info.GeneralSaveFileName);
        ES3.Save(replySaveFileKey, replyClipsPlayed, CoronaMod.Info.GeneralSaveFileName);
        talkClip = setTalkClip;
        replyClip = setReplyClip;
        SendTransmissionServerRpc(setTalkClip, setReplyClip);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendTransmissionServerRpc(int set1, int set2)
    {
        DebugMsg("[SCARECROW]: Server sending transmission");
        SendTransmissionClientRpc(set1, set2);
    }

    [ClientRpc]
    public void SendTransmissionClientRpc(int set1, int set2)
    {
        DebugMsg("[SCARECROW]: Client receiving transmission");
        talkClip = set1;
        replyClip = set2;
        RestartCoroutine(ref waitForBeginningCoroutine, true, WaitForBeginning(16f));
        RestartCoroutine(ref waitToEndEarlyCoroutine, true, WaitToEndEarly());
        RestartCoroutine(ref manageRadioVolumesCoroutine, true, ManageRadioVolumes());
    }

    public IEnumerator WaitForBeginning(float timeToWait)
    {
        float silenceTimer = 0f;
        while (RoundManager.Instance.timeScript.normalizedTimeOfDay < transmissionDeadline)
        {
            silenceTimer += Time.deltaTime;
            if (silenceTimer > timeToWait)
            {
                RestartCoroutine(ref marcoCoroutine, true, SayMarcoToRadio());
                yield break;
            }
            if (!IsAnyoneUsingWalkieTo(listen: true))
            {
                silenceTimer = 0f;
            }
            if (IsAnyoneUsingWalkieTo(talk: true))
            {
                silenceTimer = 0f;
            }
            yield return null;
        }
        DebugMsg("[SCARECROW]: Can't wait all day");
        EndTransmission();
    }

    public IEnumerator SayMarcoToRadio()
    {
        DebugMsg("[SCARECROW]: Marco");
        PlayRadioSound(startTransmitSound: true);
        PlayRadioSound(talkClips[talkClip]);
        RestartCoroutine(ref responseCoroutine, true, WaitForResponse(end: false));
        transmitting = true;
        yield return null;
        yield return new WaitForSeconds(talkClips[talkClip].length);
        EndTransmission();
    }

    public IEnumerator WaitForResponse(bool end)
    {
        float responseTimer = 0f;
        float length = end ? replyClips[replyClip].length : talkClips[talkClip].length;
        yield return new WaitForSeconds(3f);
        while (responseTimer < length - 4.5f)
        {
            responseTimer += Time.deltaTime;
            if (IsAnyoneUsingWalkieTo(out var talker, talk: true))
            {
                DebugMsg("[SCARECROW]: Response received");
                yield return new WaitForSeconds(1f);
                PlayRadioSound(stopClip: true);
                if (end)
                {
                    EndTransmission();
                }
                else
                {
                    PlayRadioSound(endTransmitSound: true);
                }
                RestartCoroutine(ref marcoCoroutine, false);
                RestartCoroutine(ref poloCoroutine, false);
                if (!end)
                {
                    if (talker != null)
                    {
                        yield return new WaitWhile(() => talker.clientIsHoldingAndSpeakingIntoThis);
                    }
                    RestartCoroutine(ref poloCoroutine, true, SayPoloToRadio());
                }
                yield break;
            }
            yield return null;
        }
        DebugMsg("[SCARECROW]: No response");
    }

    public IEnumerator SayPoloToRadio()
    {
        DebugMsg("[SCARECROW]: Interruption check");
        RestartCoroutine(ref notBeingInterruptedCoroutine, true, NotBeingInterruptedCheck(10f, 24f));
        while (!notBeingInterrupted)
        {
            if (quitEarly)
            {
                EndTransmission();
                yield break;
            }
            yield return null;
        }
        DebugMsg("[SCARECROW]: Polo");
        PlayRadioSound(startTransmitSound: true);
        PlayRadioSound(clip: replyClips[replyClip]);
        RestartCoroutine(ref responseCoroutine, true, WaitForResponse(end: true));
        transmitting = true;
        yield return null;
        yield return new WaitForSeconds(replyClips[replyClip].length);
        EndTransmission();
    }

    public IEnumerator NotBeingInterruptedCheck(float timeToWait, float timeout)
    {
        notBeingInterrupted = false;
        quitEarly = false;
        float silenceTimer = 0f;
        float timeoutTimer = 0f;
        while (timeoutTimer < timeout)
        {
            timeoutTimer += Time.deltaTime;
            silenceTimer += Time.deltaTime;
            if (silenceTimer > timeToWait)
            {
                notBeingInterrupted = true;
                yield break;
            }
            if (IsAnyoneUsingWalkieTo(talk: true))
            {
                silenceTimer = 0f;
            }
            yield return null;
        }
        DebugMsg("[SCARECROW]: Too noisy");
        quitEarly = true;
    }

    public IEnumerator WaitToEndEarly()
    {
        while (RoundManager.Instance.timeScript.normalizedTimeOfDay < transmissionDeadline * 1.2f)
        {
            if (syncedBool[encountered])
            {
                DebugMsg("[SCARECROW]: No need to multitask");
                EndTransmission();
                yield break;
            }
            yield return null;
        }
    }

    public bool IsAnyoneUsingWalkieTo(bool talk = false, bool listen = false)
    {
        if (talk)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (walkie.isBeingUsed)
                {
                    if (walkie.clientIsHoldingAndSpeakingIntoThis || walkie.talkiesSendingToThis.Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        if (listen)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (walkie.isBeingUsed)
                {
                    return true;
                }
            }
            return false;
        }
        return false;
    }

    public bool IsAnyoneUsingWalkieTo(out WalkieTalkie? radio, bool talk = false, bool listen = false)
    {
        radio = null;
        if (talk)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (walkie.isBeingUsed)
                {
                    if (walkie.clientIsHoldingAndSpeakingIntoThis)
                    {
                        radio = walkie;
                        return true;
                    }
                }
            }
            return false;
        }
        if (listen)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (walkie.isBeingUsed)
                {
                    return true;
                }
            }
            return false;
        }
        return false;
    }

    public IEnumerator ManageRadioVolumes()
    {
        while (RoundManager.Instance.timeScript.normalizedTimeOfDay < transmissionDeadline * 1.2f)
        {
            if (transmitting)
            {
                foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
                {
                    if (transmissionSources.TryGetValue(walkie, out var transmissionSource))
                    {
                        transmissionSource.volume = walkie.isBeingUsed ? 1f : 0f;
                    }
                }
            }
            yield return null;
        }
    }

    public void PlayRadioSound(AudioClip? clip = null, bool stopClip = false, bool startTransmitSound = false, bool endTransmitSound = false)
    {
        if (startTransmitSound)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (walkie.isBeingUsed)
                {
                    RoundManager.PlayRandomClip(walkie.thisAudio, walkie.startTransmissionSFX);
                }
            }
            return;
        }
        if (endTransmitSound)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (walkie.isBeingUsed)
                {
                    RoundManager.PlayRandomClip(walkie.thisAudio, walkie.stopTransmissionSFX);
                }
            }
            return;
        }
        if (stopClip)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (transmissionSources.TryGetValue(walkie, out var transmissionSource))
                {
                    transmissionSource.Stop();
                    transmissionSources.Remove(walkie, out var oldSource);
                    Destroy(oldSource);
                }
            }
            return;
        }
        if (clip != null)
        {
            foreach (WalkieTalkie walkie in WalkieTalkie.allWalkieTalkies)
            {
                if (transmissionSources.TryGetValue(walkie, out var oldSource))
                {
                    oldSource.clip = clip;
                    oldSource.Play();
                }
                else
                {
                    AudioSource transmissionSource = walkie.target.gameObject.AddComponent<AudioSource>();
                    transmissionSources.Add(walkie, transmissionSource);
                    transmissionSource.spatialBlend = 1f;
                    transmissionSource.clip = clip;
                    transmissionSource.Play();
                }
            }
            return;
        }
    }

    public void EndTransmission()
    {
        RestartCoroutine(ref waitForBeginningCoroutine, false);
        RestartCoroutine(ref marcoCoroutine, false);
        RestartCoroutine(ref responseCoroutine, false);
        RestartCoroutine(ref poloCoroutine, false);
        RestartCoroutine(ref notBeingInterruptedCoroutine, false);
        RestartCoroutine(ref manageRadioVolumesCoroutine, false);
        if (transmitting)
        {
            PlayRadioSound(stopClip: true);
            PlayRadioSound(endTransmitSound: true);
            transmitting = false;
        }
    }

    public void StupidObjectCheck(bool undo = false) //TODO: figure out how to include telephone poles called "Cylinder (1)" in experimentation scene (global instance id?)
    {
        if (StartOfRound.Instance.currentLevel.PlanetName == "41 Experimentation")
        {
            if (!undo)
            {
                stupidObjects = FindObjectsOfType<GameObject>().Where(obj => obj.name.Contains("StraightRaiing")).ToList();
                foreach (GameObject obj in stupidObjects)
                {
                    obj.GetComponent<BoxCollider>().enabled = false;
                    GameObject newObj = Instantiate(obj, obj.transform.position, Quaternion.identity);
                    Destroy(newObj.GetComponent<MeshRenderer>());
                    Destroy(newObj.GetComponent<MeshFilter>());
                    newObj.GetComponent<BoxCollider>().enabled = true;
                    newObj.layer = 28;
                    newStupidColliders.Add(newObj);
                }
            }
            else
            {
                foreach (GameObject obj in stupidObjects)
                {
                    obj.GetComponent<BoxCollider>().enabled = true;
                    for (int i = 0; i < newStupidColliders.Count; i++)
                    {
                        Destroy(newStupidColliders[i].gameObject);
                        newStupidColliders.Remove(newStupidColliders[i]);
                    }
                }
            }
        }
        if (StartOfRound.Instance.currentLevel.PlanetName == "7 Dine")
        {
            if (!undo)
            {
                stupidObjects = FindObjectsOfType<GameObject>().Where(obj => obj.name.Contains("ChainlinkFence")).ToList();
                var a = stupidObjects.Where(obj => obj.name.Contains("Bend")).ToList();
                var b = stupidObjects.Where(obj => obj.name.Contains("HoleModifier")).ToList();
                a.AddRange(b);
                foreach (var item in a)
                {
                    stupidObjects.Remove(item);
                }
                foreach (GameObject obj in stupidObjects)
                {
                    obj.GetComponent<BoxCollider>().enabled = false;
                    GameObject newObj = Instantiate(obj, obj.transform.position, Quaternion.identity);
                    Destroy(newObj.GetComponent<MeshRenderer>());
                    Destroy(newObj.GetComponent<MeshFilter>());
                    newObj.GetComponent<BoxCollider>().enabled = true;
                    newObj.layer = 28;
                    newStupidColliders.Add(newObj);
                }
            }
            else
            {
                foreach (GameObject obj in stupidObjects)
                {
                    obj.GetComponent<BoxCollider>().enabled = true;
                    for (int i = 0; i < newStupidColliders.Count; i++)
                    {
                        Destroy(newStupidColliders[i].gameObject);
                        newStupidColliders.Remove(newStupidColliders[i]);
                    }
                }
            }
        }
    }

    public override void OnDestroy()
    {
        if (stupidObjects.Count > 0)
        {
            StupidObjectCheck(undo: true);
        }
        if (debugNodes.Count > 0)
        {
            for (int i = 0; i < debugNodes.Count; i++)
            {
                Destroy(debugNodes[i]);
            }
            debugNodes.Clear();
        }
        base.OnDestroy();
    }

    public IEnumerator StartSpawnHallucination(float time, int enemyChosenIndex = -1, int chosenBehaviour = -1)
    {
        DebugMsg("[SCARECROW]: Owner starting wait to spawn hallucination!");
        yield return new WaitForSeconds(time);
        DebugMsg("[SCARECROW]: Wait to spawn hallucination complete!");
        SpawnHallucinationServerRpc(enemyChosenIndex, chosenBehaviour);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnHallucinationServerRpc(int enemyChosenIndex = -1, int chosenBehaviour = -1)
    {
        if (playersInDefenseRange.Count == 0)
        {
            DebugMsg("[SCARECROW]: Server couldn't spawn hallucination! No players in defense range.");
            return;
        }
        const int DOG = 0;
        const int GIANT = 1;
        const int MECH = 2;
        const int MASKED = 3;
        if (enemyChosenIndex == -1)
        {
            //TODO: Use RandomWeightedIndex for SpawnableEnemyWithRarity in each level for better compatibility with custom moons
            (int[] common, int[] uncommon) enemies = StartOfRound.Instance.currentLevel.levelID switch
            {
                0 => ([DOG], [MECH, GIANT, MASKED]),    //Experimentation
                1 => ([DOG, GIANT], [MECH, MASKED]),    //Assurance
                2 => ([DOG, GIANT], [MECH, MASKED]),    //Vow
                8 => ([DOG, GIANT, MECH], [MASKED]),    //Offense
                4 => ([DOG, GIANT, MASKED], [MECH]),    //March
                5 => ([DOG, GIANT, MECH, MASKED], []),  //Adamance
                6 => ([DOG, GIANT, MASKED], [MECH]),    //Rend
                7 => ([DOG, GIANT, MECH, MASKED], []),  //Dine
                9 => ([DOG, GIANT, MECH, MASKED], []),  //Titan
                10 => ([DOG, GIANT, MECH, MASKED], []), //Artifice
                12 => ([MECH], [DOG, GIANT, MASKED]),   //Embrion
                3 => ([DOG, GIANT, MECH, MASKED], []),  //The Company Building
                _ => ([DOG, GIANT, MECH, MASKED], [])   //Default
            };
            int pick1 = enemies.common[Random.Range(0, enemies.common.Count())];
            int? pick2 = !enemies.uncommon.Any() ? null : enemies.uncommon[Random.Range(0, enemies.uncommon.Count())];
            enemyChosenIndex = (int)((pick2 == null) ? pick1 : (Random.Range(0, 100) < 10) ? pick2 : pick1);
        }
        if (chosenBehaviour == -1)
        {
            chosenBehaviour = SelectHallucinationChosenBehaviour(enemyChosenIndex);
        }
        DebugMsg("[SCARECROW]: Server spawning hallucination!");
        Vector3 spawnPos;
        Vector3 meanVector = Vector3.zero;
        for (int i = 0; i < playersInDefenseRange.Count; i++)
        {
            meanVector += playersInDefenseRange[i].transform.position;
        }
        meanVector /= playersInDefenseRange.Count;
        Vector3 direction = Vector3.Normalize(meanVector - transform.position);
        float idealDistance = GetIdealDistanceForHallucination(enemyChosenIndex, chosenBehaviour);
		Ray proximityRay = new(meanVector + Vector3.up * 10f, direction);
        if ((enemyChosenIndex == GIANT || enemyChosenIndex == MECH) && Physics.Raycast(proximityRay, out RaycastHit rayHit, idealDistance, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
        {
            if (rayHit.distance < idealDistance * 0.5f)
            {
                int[] smallEnemies = [DOG, MASKED];
                enemyChosenIndex = smallEnemies[Random.Range(0, 2)];
                DebugMsg($"[SCARECROW]: Hallucination spawn position too close to a wall to begin with, choosing different enemy! Enemy: {enemyChosenIndex}", warning: true);
                idealDistance = GetIdealDistanceForHallucination(enemyChosenIndex, chosenBehaviour);
            }
        }
        spawnPos = GetHallucinationSpawnPosition(meanVector, direction, idealDistance, GetClosestPlayer(), enemyChosenIndex);

        RoundManager.Instance.tempTransform.position = spawnPos;
        RoundManager.Instance.tempTransform.LookAt(transform.position);
        Quaternion spawnRot = Quaternion.Euler(new Vector3(0f, RoundManager.Instance.tempTransform.eulerAngles.y, 0f));
        GameObject instanceHallucinationPrefab = Instantiate(hallucinations[enemyChosenIndex], spawnPos, spawnRot);
        NetworkObject netObject = instanceHallucinationPrefab.GetComponentInChildren<NetworkObject>();
        netObject.Spawn(destroyWithScene: true);
        PlayerControllerB closestPlayer = netObject.GetComponent<ScarecrowHallucination>().ClosestPlayerToGhost(playersInDefenseRange);
        SpawnHallucinationClientRpc(netObject, chosenBehaviour, spawnPos, spawnRot, (int)closestPlayer.playerClientId);
    }

    public int SelectHallucinationChosenBehaviour(int enemyChosenIndex)
    {
        int chosenBehaviour = Random.Range(0, 5);
        int[][] behavioursUsed = [ [], [], [], [] ];
        bool prevBehavioursSavedForChosenEnemy = false;
        bool allBehavioursUsedForChosenEnemy = true;
        if (!useSaveFileForBehaviours)
        {
            DebugMsg("[SCARECROW]: Not using save data for hallucination behaviours!");
            return chosenBehaviour;
        }
        else
        {
            if (!ES3.KeyExists(behaviourSaveFileKey, CoronaMod.Info.GeneralSaveFileName))
            {
                chosenBehaviour = Random.Range(0, 5);
            }
            else
            {
                behavioursUsed = ES3.Load<int[][]>(behaviourSaveFileKey, CoronaMod.Info.GeneralSaveFileName);
                if (behavioursUsed[enemyChosenIndex].Count() > 0)
                {
                    prevBehavioursSavedForChosenEnemy = true;
                    for (int i = 0; i < 5; i++)
                    {
                        if (!behavioursUsed[enemyChosenIndex].ToList().Contains(i))
                        {
                            allBehavioursUsedForChosenEnemy = false;
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                if (prevBehavioursSavedForChosenEnemy && allBehavioursUsedForChosenEnemy)
                {
                    DebugMsg($"[SCARECROW]: All hallucination behaviours used for enemy {enemyChosenIndex}, resetting memory!");
                    behavioursUsed[enemyChosenIndex] = [];
                    allBehavioursUsedForChosenEnemy = false;
                    prevBehavioursSavedForChosenEnemy = false;
                }
                while (prevBehavioursSavedForChosenEnemy && !allBehavioursUsedForChosenEnemy && behavioursUsed[enemyChosenIndex].ToList().Contains(chosenBehaviour))
                {
                    chosenBehaviour = Random.Range(0, 5);
                }
            }
            DebugMsg($"[SCARECROW]: Saving behaviour {chosenBehaviour} to enemy {enemyChosenIndex}'s behaviours used in {CoronaMod.Info.GeneralSaveFileName}!");
            int[] newIndex = [.. behavioursUsed[enemyChosenIndex], chosenBehaviour];
            behavioursUsed[enemyChosenIndex] = newIndex;
            ES3.Save(behaviourSaveFileKey, behavioursUsed, CoronaMod.Info.GeneralSaveFileName);
            return chosenBehaviour;
        }
    }

    public float GetIdealDistanceForHallucination(int enemyChosenIndex, int chosenBehaviour)
    {
        const int DOG = 0;
        const int GIANT = 1;
        const int MECH = 2;
        const int MASKED = 3;
        float idealDistance = (enemyChosenIndex, chosenBehaviour) switch
        {
            (DOG, 0) => Random.Range(10f, 13f),
            (DOG, 1) => Random.Range(10f, 12f),
            (DOG, 2) => Random.Range(10f, 15f),
            (DOG, 3) => Random.Range(10f, 14f),
            (DOG, 4) => Random.Range(22f, 26f),
            (DOG, _) => 15f,
            (GIANT, 0) => Random.Range(19f, 25f),
            (GIANT, 1) => Random.Range(32f, 42f),
            (GIANT, 2) => Random.Range(24f, 40f),
            (GIANT, 3) => Random.Range(24f, 40f),
            (GIANT, 4) => Random.Range(18f, 26f),
            (GIANT, _) => 32f,
            (MECH, 0) => Random.Range(20f, 24f),
            (MECH, 1) => Random.Range(22f, 26f),
            (MECH, 2) => Random.Range(40f, 48f),
            (MECH, 3) => Random.Range(36f, 42f),
            (MECH, 4) => Random.Range(22f, 26f),
            (MECH, _) => 28f,
            (MASKED, 0) => Random.Range(10f, 13f),
            (MASKED, 1) => Random.Range(11f, 15f),
            (MASKED, 2) => Random.Range(9f, 12f),
            (MASKED, 3) => Random.Range(9f, 12f),
            (MASKED, 4) => Random.Range(9f, 11f),
            (MASKED, _) => 8f,
            (_, _) => 14f
        };
        return idealDistance;
    }

    public Vector3 GetHallucinationSpawnPosition(Vector3 meanVector, Vector3 direction, float idealDistance, PlayerControllerB closestPlayer, int enemyChosen)
    {
        (int, float, float, float) enemyValues = enemyChosen switch   //navMeshAreaMask, distanceFromWalls, distanceFromPlayers, randomRangeMax
        {
            0 => (863, 3f, 8f, 10f),
            1 => (799, 4f, 12f, 16f),
            2 => (927, 8f, 18f, 20f),
            3 => (-1, 2f, 5f, 8f),
            _ => (-1, 5f, 8f, 10f)
        };
		Ray wallRay = new(meanVector + Vector3.up * 6f, direction);
        float finalDistance = (!Physics.Raycast(wallRay, out RaycastHit rayHit, idealDistance, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore)) ? idealDistance : rayHit.distance - enemyValues.Item2;
        Vector3 directedPos = meanVector + direction * finalDistance;
        Vector3 newPos;
        int tryAttempts = 0;
        while (tryAttempts < 50)
        {
            tryAttempts++;
            newPos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(directedPos, radius: Mathf.Min(tryAttempts, enemyValues.Item4));
            bool notNearPlayers = true;
            for (int i = 0; i < playersInDefenseRange.Count; i++)
            {
                if (Vector3.Distance(newPos, playersInDefenseRange[i].transform.position) < enemyValues.Item3)
                {
                    DebugMsg("[SCARECROW]: Hallucination spawn position too close to players.");
                    notNearPlayers = false;
                }
            }

            bool navigationComplete;
            bool pathValid = NavMesh.CalculatePath(newPos, closestPlayer.transform.position, enemyValues.Item1, path1);
            if (!pathValid || (path1.status == NavMeshPathStatus.PathPartial))
            {
                DebugMsg("[SCARECROW]: Hallucination spawn position unable to reach players.");
                navigationComplete = false;
            }
            else
            {
                navigationComplete = true;
            }

            bool notInLOS = true;
            if (closestPlayer.HasLineOfSightToPosition(newPos + Vector3.up * 1.5f))
            {
                notInLOS = false;
            }

            if (notNearPlayers && navigationComplete && notInLOS)
            {
                DebugMsg("[SCARECROW]: Hallucination spawn position returned newPos!");
                return newPos;
            }
        }
        DebugMsg("[SCARECROW]: Hallucination spawn position returned directedPos.", warning: true);
        return directedPos;
    }

    [ClientRpc]
    public void SpawnHallucinationClientRpc(NetworkObjectReference netObjectRef, int chosenBehaviour, Vector3 spawnPos, Quaternion spawnRot, int playerId)
    {
        DebugMsg("[SCARECROW]: Client received reference to hallucination!");
        NetworkObject netObject = netObjectRef;
        netObject.GetComponent<NavMeshAgent>().enabled = false;
        netObject.transform.position = spawnPos;
        netObject.transform.rotation = spawnRot;
        netObject.GetComponent<NavMeshAgent>().enabled = true;
        RestartCoroutine(ref hallucinatingCoroutine, true, HallucinationLifespan(10f, netObject, chosenBehaviour, playerId));
    }

    public IEnumerator HallucinationLifespan(float lifetime, NetworkObject netObject, int chosenBehaviour, int playerId)
    {
        playersHallucinating = [.. playersInDefenseRange];
        foreach (PlayerControllerB player in playersHallucinating)
        {
            player.insanityLevel += player.maxInsanityLevel * 0.1f;
        }
        if (!playersHallucinating.Contains(GameNetworkManager.Instance.localPlayerController))
        {
            DebugMsg("[SCARECROW]: Client too far from hallucination!");
            hallucinationVolume.enabled = false;

            foreach (Transform transform in netObject.gameObject.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                transform.gameObject.layer = 23;
            }
            foreach (AudioSource source in netObject.gameObject.GetComponentsInChildren<AudioSource>(includeInactive: true))
            {
                source.enabled = false;
            }
            foreach (Collider collider in netObject.gameObject.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                collider.isTrigger = true;
                collider.enabled = false;
            }
            foreach (Light light in netObject.gameObject.GetComponentsInChildren<Light>(includeInactive: true))
            {
                light.intensity = 0f;
            }
        }
        else
        {
            DebugMsg("[SCARECROW]: Client within range of hallucination!");
            hallucinationVolume.enabled = true;
        }
        instanceHallucinationObject = netObject.gameObject;
        instanceHallucination = netObject.GetComponent<ScarecrowHallucination>();
        instanceHallucination.mainScarecrow = this;
        instanceHallucination.closestPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
        instanceHallucination.chosenBehaviour = chosenBehaviour;
        instanceHallucination.DoPossibleBehaviours();
        DebugMsg("[SCARECROW]: Hallucination live!");
        StartCoroutine(KeepFadePosition(lifetime, instanceHallucination.transform));
        RestartCoroutine(ref fadeHallucinationCoroutine, true, FadeHallucination(1f, instanceHallucination.ghostParts, instanceHallucination.ghostAudioSources, spawnFlag: true));
        SetSyncedBool(hallucinationLive, true);
        SetSyncedBool(hallucinationSpotted, false);
        SetSyncedBool(hallucinationVisible, true);
        float timeElapsed = 0f;
        bool otherClientSpottedFlag = false;
        bool thisClientSpottedFlag = false;
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

        yield return new WaitForSeconds(1f);

        while (timeElapsed < lifetime - 1 && syncedBool[hallucinationVisible] && syncedBool[hallucinationLive])
        {
            timeElapsed += Time.deltaTime;

            if (!playersHallucinating.Contains(localPlayer))
            {
                yield return null;
            }
            else
            {
                if (!syncedBool[hallucinationSpotted] && instanceHallucination.PlayerHasLineOfSightToGhost(localPlayer, spottedAngle*5f))
                {
                    DebugMsg("[SCARECROW]: Hallucination spotted by this client!");
                    fullySpotted = true;
                    ghostFadeSource.clip = ghostFadeClips[Random.Range(0, ghostFadeClips.Length)];
                    ghostFadeSource.pitch = Random.Range(0.9f, 1.1f);
                    ghostFadeSource.Play();
                    RestartCoroutine(ref fadeHallucinationCoroutine, true, FadeHallucination(6.7f, instanceHallucination.ghostParts, instanceHallucination.ghostAudioSources));
                    SetSyncedBool(hallucinationSpotted, true);
                    thisClientSpottedFlag = true;
                    localPlayer.insanityLevel += localPlayer.maxInsanityLevel * 0.1f;
                    timeElapsed = lifetime - 7f;
                }

                if (syncedBool[hallucinationSpotted] && !otherClientSpottedFlag && !thisClientSpottedFlag)
                {
                    DebugMsg("[SCARECROW]: Hallucination spotted by other client!");
                    ghostFadeSource.clip = ghostFadeClips[Random.Range(0, ghostFadeClips.Length)];
                    ghostFadeSource.pitch = Random.Range(0.9f, 1.1f);
                    ghostFadeSource.Play();
                    RestartCoroutine(ref fadeHallucinationCoroutine, true, FadeHallucination(6.7f, instanceHallucination.ghostParts, instanceHallucination.ghostAudioSources));
                    otherClientSpottedFlag = true;
                    timeElapsed = lifetime - 7f;
                }
            }
            yield return null;
        }
        StartCoroutine(DespawnHallucination());
    }

    public IEnumerator FadeHallucination(float duration, GameObject[] ghostParts, AudioSource[] ghostAudioSources, bool spawnFlag = false, bool interruption = false)
    {
        if (!spawnFlag)
        {
            instanceHallucination.fading = true;
        }
        float timeElapsed = 0f;
        List<Material> ghostMaterials = [];
        foreach (GameObject ghostPart in ghostParts)
        {
            ghostMaterials.Add(ghostPart.GetComponent<Renderer>().sharedMaterial);
        }
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        bool volumeFlag = false;
        bool volumeInterruptFlag = false;
        bool layerSwapFlag = false;
        bool fadeMeshFlag = false;
        float meshLerpStartTime = (!spawnFlag) ? 0.15f : 0f;
        float meshLerpDuration = (!spawnFlag) ? 0.35f : 1f;
        float meshLerpEndValue = (!spawnFlag) ? 0f : 1f;
        float fullVolumeOpacity = 0.6f;
        bool stopParticleFlag = false;
        bool mechFade = false;
        List<Light> ghostLights = [];
        if (instanceHallucination.chosenEnemy == ScarecrowHallucination.Ghosts.RadMech)
        {
            mechFade = true;
            foreach (Light light in instanceHallucination.GetComponentsInChildren<Light>(includeInactive: true))
            {
                ghostLights.Add(light);
            }
        }

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            //VOLUME FLAGS
            if (!interruption)
            {
                if (instanceHallucinationObject != null && instanceHallucination.PlayerHasLineOfSightToGhost(localPlayer, spottedAngle*5f) && timeElapsed < duration * 0.5f)
                {
                    if (!spawnFlag && !volumeFlag)
                    {
                        volumeFlag = true;
                        RestartCoroutine(ref controlVolumeOpacity.Coroutine, true, LerpIncrement(controlVolumeOpacity, controlVolumeOpacity.Value, fullVolumeOpacity, duration * 0.18f, 0));
                        RestartCoroutine(ref controlFadeVolume.Coroutine, true, LerpIncrement(controlFadeVolume, controlFadeVolume.Value, 1f, 1f, 0f));
                    }
                }
                else
                {
                    if (!spawnFlag && volumeFlag)
                    {
                        volumeFlag = false;
                        RestartCoroutine(ref controlVolumeOpacity.Coroutine, true, LerpIncrement(controlVolumeOpacity, controlVolumeOpacity.Value, 0f, duration * 0.5f, 0));
                        RestartCoroutine(ref controlFadeVolume.Coroutine, true, LerpIncrement(controlFadeVolume, controlFadeVolume.Value, 0f, 4f, 0f));
                    }
                }
            }
            else if (!volumeInterruptFlag)
            {
                volumeInterruptFlag = true;
                RestartCoroutine(ref controlVolumeOpacity.Coroutine, true, LerpIncrement(controlVolumeOpacity, controlVolumeOpacity.Value, 0f, duration * 0.5f, 0));
                RestartCoroutine(ref controlFadeVolume.Coroutine, true, LerpIncrement(controlFadeVolume, controlFadeVolume.Value, 0f, duration, 0f));
            }

            //MATERIAL FLAGS
            if (timeElapsed > duration * meshLerpStartTime && !fadeMeshFlag)
            {
                fadeMeshFlag = true;
                RestartCoroutine(ref controlMeshOpacity.Coroutine, true, LerpIncrement(controlMeshOpacity, controlMeshOpacity.Value, meshLerpEndValue, duration * meshLerpDuration, 0f));
            }
            if (!spawnFlag && instanceHallucinationObject != null && timeElapsed > duration * 0.7f && !layerSwapFlag)
            {
                layerSwapFlag = true;
                foreach (Transform transform in instanceHallucinationObject.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    transform.gameObject.layer = 23;
                }
            }
            if (!spawnFlag && mechFade && instanceHallucinationObject != null && timeElapsed > duration * 0.45f && !stopParticleFlag)
            {
                stopParticleFlag = true;
                instanceHallucination.chargeParticle.Stop();
                instanceHallucination.startChargingEffectContainer.SetActive(false);
            }

            //RUN EACH FRAME
            if (instanceHallucinationObject != null)
            {
                foreach(Material ghostMaterial in ghostMaterials)
                {
                    var color = ghostMaterial.color;
                    color.a = controlMeshOpacity.Value;
                    ghostMaterial.color = color;
                }
                if (!spawnFlag && timeElapsed > duration * 0.2f)
                {
                    foreach (AudioSource audio in ghostAudioSources)
                    {
                        audio.volume = Mathf.Lerp(interruption ? audio.volume : 1f, 0f, (timeElapsed-(duration * 0.2f)) / (duration * 0.35f));
                    }
                    if (mechFade)
                    {
                        foreach (Light light in ghostLights)
                        {
                            light.intensity =  Mathf.Lerp(light.intensity, 0f, (timeElapsed-(duration * 0.2f)) / (duration * 0.35f));
                        }
                    }
                }
            }
            hallucinationVolume.weight = controlVolumeOpacity.Value;
            ghostFadeSource.volume = controlFadeVolume.Value;

            yield return null;
        }
        hallucinationVolume.weight = 0;
        if (!spawnFlag)
        {
            SetSyncedBool(hallucinationVisible, false);
        }
    }

    public IEnumerator DespawnHallucination()
    {
        DebugMsg("[SCARECROW]: Client despawning hallucination!");
        if (syncedBool[hallucinationVisible]) 
        {
            RestartCoroutine(ref fadeHallucinationCoroutine, true, FadeHallucination(2f, instanceHallucination.ghostParts, instanceHallucination.ghostAudioSources, interruption: true));
            yield return new WaitUntil(() => !syncedBool[hallucinationVisible]);
        }
        playersHallucinating = [];
        RestartCoroutine(ref hallucinatingCoroutine, false);
        RestartCoroutine(ref fadeHallucinationCoroutine, false);
        DespawnHallucinationServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void DespawnHallucinationServerRpc()
    {
        SetSyncedBool(hallucinationSpotted, false);
        SetSyncedBool(hallucinationLive, false);
        if (instanceHallucination != null && instanceHallucination.TryGetComponent<NetworkObject>(out var netObject))
        {
            try //TODO: lazy, not a fix, debug and fix later
            {
                netObject.Despawn();
                DebugMsg("[SCARECROW]: Server despawned hallucination!");
            }
            catch
            {
                DebugMsg("[SCARECROW]: Server can't despawn hallucination! Already despawned?", warning: true);
            }
        }
        else
        {
            DebugMsg("[SCARECROW]: Hallucination finished!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetThreatenedValueServerRpc(bool load = false, int incrementAmount = 1)
    {
        currentSaveFile = GameNetworkManager.Instance.saveFileNum switch
        {
            0 => CoronaMod.Info.SaveFileName1,
            1 => CoronaMod.Info.SaveFileName2,
            2 => CoronaMod.Info.SaveFileName3,
            _ => CoronaMod.Info.SaveFileName1
        };
        if (load)
        {
            if (!useSaveFileForThreats)
            {
                DebugMsg("[SCARECROW]: Not using save file threatened value!");
                timesThreatenedInSaveFile = Random.Range(0, 8);
            }
            else if (!ES3.KeyExists(threatenedSaveFileKey, currentSaveFile))
            {
                DebugMsg("[SCARECROW]: No current save file threatened value, saving for the first time!");
                timesThreatenedInSaveFile = 0;
                ES3.Save(threatenedSaveFileKey, timesThreatenedInSaveFile, currentSaveFile);
            }
            else
            {
                DebugMsg("[SCARECROW]: Server loading threatened value from current save file!");
                timesThreatenedInSaveFile = ES3.Load(threatenedSaveFileKey, currentSaveFile, defaultValue: 0);
                timesThreatenedInSaveFile = (timesThreatenedInSaveFile > 0) ? timesThreatenedInSaveFile-- : 0;
            }
        }
        else
        {
            DebugMsg("[SCARECROW]: Server updating threatened value!");
            timesThreatenedInSaveFile += incrementAmount;
        }
        SetThreatenedValueClientRpc(timesThreatenedInSaveFile);
    }

    [ClientRpc]
    public void SetThreatenedValueClientRpc(int value)
    {
        DebugMsg("[SCARECROW]: This client getting threatened value from server!");
        DebugMsg($"[SCARECROW]: Threatened value: {value}");
        timesThreatenedInSaveFile = value;
        hallucinateChanceMultiplier = timesThreatenedInSaveFile switch
        {
            0 => 0f,
            1 => 0f,
            2 => 0.025f,
            _ => Mathf.Clamp(timesThreatenedInSaveFile * hallucinateThreatIncrement, min: hallucinateChanceMin, max: hallucinateChanceMax)
        };
        weaponDetectRange = timesThreatenedInSaveFile switch
        {
            0 => detectRange,
            1 => detectRange,
            2 => detectRange,
            _ => detectRange * Mathf.Clamp((timesThreatenedInSaveFile * weaponDetectIncrement) + 1f, min: weaponDetectRangeMin, max: weaponDetectRangeMax)
        };
    }

    private void DebugMsg(object message, bool error = false, bool warning = false)
    {
        if (debugEnemyAI)
        {
            if (error)
            {
                Debug.LogError(message);
            }
            else if (warning)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
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