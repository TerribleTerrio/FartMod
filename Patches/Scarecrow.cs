using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Scarecrow : EnemyAI
{
    [Space(15f)]
    [Header("Scarecrow Settings")]

    [Range(0f, 1f)]
    public float normalizedTimeInDayToBecomeActive;

    public Item dropItem;

    public Transform dropItemTransform;

    private List<PlayerControllerB> playersInRange;

    private bool active;

    [Space(5f)]
    [Header("Debug Controls")]
    public bool triggerNewPosition;

    [Space(5f)]
    [Header("Danger Values")]
    public float dangerValue;

    public int minEnemyPowerIncrease = 5;

    public int maxEnemyPowerIncrease = 20;

    private int enemyPowerIncrease;

    public int minEnemySpawnIncrease = 0;

    public int maxEnemySpawnIncrease = 2;

    private int enemySpawnIncrease;

    [Space(5f)]
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

    [Space(5f)]
    [Header("Animations")]
    public float rotateSpeed;

    [Space(5f)]
    [Header("Audio")]
    public float noiseRange;

    [Space(5f)]
    public AudioSource scareAudio;

    public AudioClip[] scareSounds;

    [Space(5f)]
    public AudioSource warningAudio;

    public AudioClip[] warningSounds;

    [Space(5f)]
    public AudioSource decoyAudio;

    public AudioClip[] decoySounds;

    public override void Start()
    {
        base.Start();
        playersInRange = new List<PlayerControllerB>();

        dangerValue = UnityEngine.Random.Range(0f,100f);
        enemySpawnIncrease = RemapInt(dangerValue, 0, 100, minEnemySpawnIncrease, maxEnemySpawnIncrease);
        enemyPowerIncrease = RemapInt(dangerValue, 0, 100, minEnemyPowerIncrease, maxEnemyPowerIncrease);

        startValue = UnityEngine.Random.Range(minStartValue, maxStartValue);
        endValue = UnityEngine.Random.Range(minEndValue, maxEndValue);

        Debug.Log("---Scarecrow Spawn Values---");
        Debug.Log($"Danger value: {dangerValue}");
        Debug.Log($"Minimum enemy spawn increase: {enemySpawnIncrease}");
        Debug.Log($"Max enemy power increase: {enemyPowerIncrease}");
        Debug.Log($"Start value: {startValue}");
        Debug.Log($"End value: {endValue}");
    }

    public override void Update()
    {
        base.Update();

        if (triggerNewPosition)
        {
            triggerNewPosition = false;
            TryMoveToPosition(GetRandomNavMeshPositionNearAINode());
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
        else
        {
            float dayProgress = RoundManager.Instance.timeScript.normalizedTimeOfDay;
            currentValue = RemapInt(dayProgress, 0f, 1f, startValue, endValue);
        }

        switch (currentBehaviourStateIndex)
        {
        
        //INACTIVE
        case 0:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }
            break;

        //NO PLAYERS NEARBY (ACTIVE)
        case 1:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }
            break;

        //ONE PLAYER NEARBY
        case 2:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }
            break;

        //PLAYERS NEARBY
        case 3:
            if (previousBehaviourStateIndex != currentBehaviourStateIndex)
            {
                previousBehaviourStateIndex = currentBehaviourStateIndex;
            }
            break;
        }

    }

    public void TryMoveToPosition(Vector3 newPosition)
    {
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].HasLineOfSightToPosition(transform.position, range: 100))
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

        transform.position = newPosition;
    }

    public Vector3 GetRandomNavMeshPositionNearAINode(float radius = 20f)
    {
        GameObject[] nodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
        int numNodes = nodes.Length;

        for (int i = 0; i < numNodes; i++)
        {
            Debug.Log($"Node [i]: {nodes[i].transform.position}");
        }

        int nodeSelected = Random.Range(0, numNodes + 1);
        Vector3 nodePosition = nodes[nodeSelected].transform.position;
        Debug.Log($"Selected node {nodeSelected} at {nodePosition}.");
        return RoundManager.Instance.GetRandomNavMeshPositionInRadius(nodePosition, radius);
    }

    public void ScarePlayer(PlayerControllerB player)
    {
        RoundManager.Instance.PlayAudibleNoise(transform.position, noiseRange, 1, 0, false, -1);

        creatureAnimator.SetTrigger("ScarePlayer");

        StartCoroutine(TurnToFacePosition(player.transform.position));
    }

    public IEnumerator TurnToFacePosition(Vector3 lookPosition)
    {
        Quaternion targetRotation = Quaternion.LookRotation(lookPosition, transform.position);
        float rotationSpeed = Mathf.Min(rotateSpeed * Time.deltaTime, 1);
        while (transform.rotation != targetRotation)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed);
            yield return null;
        }
    }

    public void PlayDecoySound(AudioClip clip, float volume)
    {
        Vector3 decoyPosition = RoundManager.Instance.GetRandomPositionInRadius(transform.position, 6f, 11f);
        decoyAudio.transform.position = decoyPosition;
        decoyAudio.volume = volume;
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
        if (distance > 4)
        {
            HitEnemyOnLocalClient();
        }
        else
        {
            KillEnemyOnOwnerClient();
        }
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        creatureAnimator.SetBool("IsDead", value: true);
        IncreaseEnemySpawnRate();
        DropItem();
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