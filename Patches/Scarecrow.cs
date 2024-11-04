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

    private NetworkObjectReference dropObjectRef;

    public Transform dropItemTransform;

    public Transform meshContainer;

    private List<PlayerControllerB> playersInRange;

    public float scareRange;

    public string[] invalidTerrainTags;

    private bool targetPlayerEnteredDetectWhileWatching;

    private bool invisible;

    private Coroutine changePositionCoroutine;

    private bool scarePrimed;

    private List<GameObject> nodes;

    private GameObject[] spawnDenialPoints;

    private List<PlayerControllerB> playersWithLineOfSight;

    public Transform[] lineOfSightTriggers;

    public Transform scareTriggerTransform;

    [Space(10f)]
    [Header("Wind Levels On Moons")]
    public string[] noWindMoons;

    public string[] lightWindMoons;

    public string[] heavyWindMoons;

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
    public float detectSoundChance = 20;

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

    public AudioClip[] warningSoundsLow;

    public AudioClip[] warningSoundsMedium;

    public AudioClip[] warningSoundsHigh;

    [Space(5f)]
    public AudioSource decoyAudio;

    public AudioClip[] decoySounds;

    public override void Start()
    {
        if (StartOfRound.Instance.gameStats.daysSpent < daysBeforeScarecrowSpawns)
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

        if (IsOwner)
        {
            SetDangerLevelsAndSync();
            GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
        }

        Debug.Log("---Scarecrow Spawn Values---");
        Debug.Log($"Danger value: {dangerValue}");
        Debug.Log($"Minimum enemy spawn increase: {enemySpawnIncrease}");
        Debug.Log($"Max enemy power increase: {enemyPowerIncrease}");
        Debug.Log($"Start value: {startValue}");
        Debug.Log($"End value: {endValue}");
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

    public void AddDetectedPlayer(Collider other)
    {
        if (IsOwner)
        {
            if (other.gameObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
                playersInRange.Add(player);
                Debug.Log($"Scarecrow added {player.playerUsername} to detected players.");
                SetStateBasedOnPlayers(playersInRange.Count);
            }
        }
    }

    public void RemoveDetectedPlayer(Collider other)
    {
        if (IsOwner)
        {
            if (other.gameObject.GetComponent<PlayerControllerB>() != null)
            {
                PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
                playersInRange.Remove(player);
                Debug.Log($"Scarecrow removed {player.playerUsername} from detected players.");
                SetStateBasedOnPlayers(playersInRange.Count);
            }
        }
    }

    public void SetStateBasedOnPlayers(int numPlayers)
    {
        if (RoundManager.Instance.timeScript.normalizedTimeOfDay < normalizedTimeInDayToBecomeActive || isEnemyDead || invisible)
        {
            Debug.Log("Scarecrow inactive or invisible, remaining in inactive state.");
            return;
        }

        if (numPlayers == 0)
        {
            Debug.Log("No players in range, switching to state 1");
            SwitchToBehaviourState(1);
        }

        else if (numPlayers == 1)
        {
            Debug.Log("One player in range, switching to state 2");
            SwitchToBehaviourState(2);
        }

        else if (numPlayers > 1)
        {
            Debug.Log("Players in range, switching to state 4");
            SwitchToBehaviourState(4);
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (isEnemyDead)
        {
            return;
        }

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

            if (invisible && changePositionCoroutine == null && playersWithLineOfSight.Count == 0)
            {
                Debug.Log("Scarecrow out of view of all players, re-enabling meshes.");
                SetInvisibleServerRpc(false);
            }

            if (RoundManager.Instance.timeScript.normalizedTimeOfDay > normalizedTimeInDayToBecomeActive && !isEnemyDead && !invisible)
            {
                SetStateBasedOnPlayers(playersInRange.Count);
            }

            break;

        //NO PLAYERS NEARBY
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                scarePrimed = false;
                moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            if (moveTimer <= 0)
            {
                if (playersWithLineOfSight.Count == 0)
                {
                    moveTimer = Random.Range(minMoveCooldown, maxMoveCooldown);
                    if (Random.Range(0f,100f) < moveChance)
                    {
                        TryMoveToNewPosition(GetRandomNavMeshPositionNearAINode());
                    }
                }
            }

            break;

        //1 PLAYER IN RANGE
        case 2:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                decoySoundTimer = decoySoundCooldown;
                scarePrimed = false;

                //IF PLAYER IS IN SCARE RANGE
                if (Vector3.Distance(playersInRange[0].transform.position, transform.position) < scareRange)
                {
                    Debug.Log("Player already in scare range, switching to state 3.");
                    currentBehaviourStateIndex = 3;
                }

                //IF COMING FROM NO PLAYERS STATE
                if (previousBehaviourStateIndex == 1)
                {
                    if (detectSoundTimer <= 0)
                    {
                        detectSoundTimer = detectSoundCooldown;
                        if (Random.Range(0f,100f) < detectSoundChance && playersWithLineOfSight.Count == 0)
                        {
                            PlayDetectSoundServerRpc();
                        }
                    }
                }

                //IF PLAYER ENTERED RANGE WHILE LOOKING AT SCARECROW
                if (CheckLineOfSightForScarecrow(playersInRange[0]))
                {
                    targetPlayerEnteredDetectWhileWatching = true;
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //IF PLAYER BREAKS LOS
            if (!CheckLineOfSightForScarecrow(playersInRange[0]))
            {
                decoySoundTimer = decoySoundCooldown;

                //AND NO ONE IS WATCHING
                if (playersWithLineOfSight.Count == 0)
                {
                    if (facePlayerTimer <= 0)
                    {
                        facePlayerTimer = facePlayerCooldown;
                        if (Random.Range(0f,100f) < facePlayerChance)
                        {
                            FacePosition(playersInRange[0].transform.position);
                            GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);

                            if (tweakOutTimer <= 0)
                            {
                                tweakOutTimer = tweakOutCooldown;
                                if (Random.Range(0f,100f) < tweakOutChance)
                                {
                                    TweakOut(playersInRange[0]);
                                }
                            }
                        }
                    }
                }
            }

            //IF PLAYER IS LOOKING AT SCARECROW
            else
            {
                if (decoySoundTimer <= 0)
                {
                    decoySoundTimer = decoySoundCooldown;
                    if (Random.Range(0f,100f) < decoySoundChance && playersWithLineOfSight.Count == 1)
                    {
                        PlayDecoySoundServerRpc();
                    }
                }
            }

            //IF PLAYER ENTERS SCARE RANGE
            if (Vector3.Distance(playersInRange[0].transform.position, transform.position) < scareRange)
            {
                Debug.Log("1 player in range entered scare range, switching to state 3.");
                currentBehaviourStateIndex = 3;
            }

            break;

        //1 PLAYER IN SCARE RANGE (WHILE NO ONE ELSE IS IN RANGE)
        case 3:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                //IF PLAYER ENTERED SCARE RANGE WHILE LOOKING
                if (CheckLineOfSightForScarecrow(playersInRange[0]))
                {
                    if (scarePlayerTimer <= 0)
                    {
                        scarePlayerTimer = scarePlayerCooldown;
                        if (Random.Range(0f,100f) < 5f && playersWithLineOfSight.Count == 1)
                        {
                            Debug.Log("Calling ScarePlayerServerRpc!");
                            ScarePlayerServerRpc((int)playersInRange[0].playerClientId);
                        }
                    }
                }

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //ANY TIME PLAYER BREAKS LOS
            if (!CheckLineOfSightForScarecrow(playersInRange[0]))
            {
                if (facePlayerTimer <= 0 && playersWithLineOfSight.Count == 0)
                {
                    facePlayerTimer = facePlayerCooldown;
                    if (Random.Range(0f,100f) < facePlayerChance)
                    {
                        FacePosition(playersInRange[0].transform.position);
                        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                    }
                }

                if (scarePlayerTimer <= 0 && playersWithLineOfSight.Count == 0)
                {
                    scarePlayerTimer = scarePlayerCooldown;
                    if (Random.Range(0f,100f) < scarePlayerChance)
                    {
                        FacePosition(playersInRange[0].transform.position);
                        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
                        scarePrimed = true;
                    }
                }
            }

            //ANY TIME PLAYER HAS LOS
            else
            {
                //AND SCARE IS PRIMED
                if (scarePrimed && playersInRange[0].HasLineOfSightToPosition(scareTriggerTransform.position))
                {
                    scarePrimed = false;
                    if (playersWithLineOfSight.Count == 1)
                    {
                        Debug.Log("Calling ScarePlayerServerRpc!");
                        ScarePlayerServerRpc((int)playersInRange[0].playerClientId);
                    }
                }
            }

            if (Vector3.Distance(playersInRange[0].transform.position, transform.position) > scareRange)
            {
                Debug.Log("1 player in range exited scare range, switching to state 2.");
                currentBehaviourStateIndex = 2;
            }

            break;

        //MULTIPLE PLAYERS IN RANGE
        case 4:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                scarePrimed = false;
                decoySoundTimer = decoySoundCooldown;

                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }

            //UPDATE LIST OF PLAYERS IN RANGE WITH LOS
            List<PlayerControllerB> playersInRangeWithLineOfSight = new List<PlayerControllerB>(playersInRange);
            for (int i = 0; i < playersInRangeWithLineOfSight.Count; i++)
            {
                PlayerControllerB player = playersInRangeWithLineOfSight[i];
                if (!CheckLineOfSightForScarecrow(player))
                {
                    playersInRangeWithLineOfSight.Remove(player);
                }
            }

            //IF ONLY 1 PLAYER IN RANGE IS LOOKING
            if (playersInRangeWithLineOfSight.Count == 1)
            {
                //AND NO ONE ELSE IS LOOKING
                if (playersWithLineOfSight.Count == 1)
                {
                    if (tweakOutTimer <= 0f)
                    {
                        tweakOutTimer = tweakOutCooldown;
                        if (Random.Range(0f,100f) < tweakOutChance)
                        {
                            TweakOut(playersInRangeWithLineOfSight[0]);
                        }
                    }
                }
            }

            //IF MORE THAN 1 PLAYER IN RANGE IS LOOKING
            if (playersInRangeWithLineOfSight.Count > 1)
            {
                //AND ALL PLAYERS IN RANGE ARE LOOKING AND NO ONE ELSE IS LOOKING
                if (playersInRangeWithLineOfSight.Count == playersWithLineOfSight.Count)
                {
                    if (decoySoundTimer <= 0f)
                    {
                        decoySoundTimer = decoySoundCooldown;
                        if (Random.Range(0f,100f) < decoySoundChance)
                        {
                            PlayDecoySoundServerRpc();
                        }
                    }
                }
            }

            break;
        }
    }

    public void TryMoveToNewPosition(Vector3 newPosition)
    {
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        float c;

        Debug.Log($"Trying new position: {newPosition}");

        bool inViewOfPlayer = false;
        for (int i = 0; i < players.Length; i++)
        {
            //PREVENT FROM MOVING WHILE IN VIEW OF PLAYER
            if (CheckLineOfSightForScarecrow(players[i]))
            {
                Debug.Log($"Current position in view of {players[i].playerUsername}, scarecrow did not move.");
                return;
            }

            //PREVENT FROM MOVING TO NEW POSITION IN VIEW OF PLAYER
            if (lineOfSightTriggers.Length > 0)
            {
                for (int j = 0; j < lineOfSightTriggers.Length; j++)
                {
                    Debug.Log($"Line of sight trigger: {newPosition + lineOfSightTriggers[j].localPosition}");
                    if (players[i].HasLineOfSightToPosition(newPosition + lineOfSightTriggers[j].localPosition, range: 100))
                    {
                        Debug.Log($"LOS trigger visible to {players[i].playerUsername} in new position, scarecrow did not move.");
                        inViewOfPlayer = true;
                        break;
                    }
                }
            }
        }

        if (inViewOfPlayer)
        {
            c = Random.Range(0f,100f);
            {
                if (c > 50f)
                {
                    return;
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
                Debug.Log($"New position on {invalidTerrainTags[i]}.");
                onInvalidTerrain = true;
            }
        }

        if (onInvalidTerrain)
        {
            Debug.Log("Scarecrow attempting to move to invalid terrain.");
            c = Random.Range(0f,100f);
            if (c > 90f)
            {
                Debug.Log("Scarecrow did not move.");
                return;
            }
        }

        if (Vector3.Angle(Vector3.up, hitInfo.normal) > 35f)
        {
            Debug.Log("New position on too steep of ground, scarecrow did not move.");
            return;
        }

        Collider[] headCollisions = Physics.OverlapSphere(newPosition + scareTriggerTransform.localPosition, 0.1f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);
        if (headCollisions.Length > 0)
        {
            Debug.Log("New position obscures head, did not move.");
            return;
        }

        if (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Flooded)
        {
            if (newPosition.y < TimeOfDay.Instance.currentWeatherVariable)
            {
                Debug.Log("New position is under flood level, did not move.");
                return;
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
                    Debug.Log("New position too close to spawn denial point.");
                    Debug.Log("Scarecrow did not move.");
                    return;
                }
            }
        }

        changePositionCoroutine = StartCoroutine(ChangePositionWhileInvisible(newPosition, 1.5f));
        // SetPositionAndSync(newPosition);
        // transform.position = newPosition;

        GiveRandomTiltAndSync((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    private IEnumerator ChangePositionWhileInvisible(Vector3 position, float time)
    {
        SetInvisibleServerRpc(true);
        invisible = true;
        transform.position = position;
        Debug.Log("Scarecrow moved.");
        currentBehaviourStateIndex = 0;
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
            Debug.Log("Scarecrow set invisible.");
            invisible = true;
            EnableEnemyMesh(false);
        }
        else
        {
            Debug.Log("Scarecrow set visible.");
            invisible = false;
            EnableEnemyMesh(true);
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

    public void TweakOut(PlayerControllerB player)
    {
        Debug.Log("Scarecrow tweaked out!");
        AudioClip clip = tweakOutSounds[Random.Range(0, tweakOutSounds.Length)];
        tweakOutAudio.PlayOneShot(clip);
        // player.insanityLevel += player.maxInsanityLevel * 0.1f;
        creatureAnimator.SetTrigger("TweakOut");
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

    //MIGHT BE SYNCED ALREADY
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
            dropItemPrefab = zapItemPrefab;
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
                dropItemPrefab = zapItemPrefab;
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

    public void DropItem()
    {
        if (base.IsOwner)
        {
            DropItemServerRpc();
        }
    }

    [ServerRpc]
    public void DropItemServerRpc()
    {
        Debug.Log("Called DropItemServerRpc!");
        GameObject dropObject = Instantiate(dropItemPrefab, dropItemTransform.position, dropItemTransform.rotation, RoundManager.Instance.spawnedScrapContainer);
        dropObject.GetComponent<NetworkObject>().Spawn();

        DropItemClientRpc(dropObjectRef, currentValue, rotAmount);
    }

    [ClientRpc]
    public void DropItemClientRpc(NetworkObjectReference dropObjectRef, int value = 0, float rot = 0f)
    {
        GameObject dropObject = dropObjectRef;
        GrabbableObject gObject = dropObject.GetComponent<GrabbableObject>();

        Pumpkin pumpkin = dropObject.GetComponent<Pumpkin>();
        if (pumpkin != null)
        {
            pumpkin.SetScrapValue(value);
            pumpkin.itemAnimator.SetFloat("rot", rot);
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