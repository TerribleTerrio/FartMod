using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class Blowtorch : AnimatedItem
{

    [Header("Blowtorch Settings")]
    public int damage;

    public GameObject rangeStart;

    public GameObject rangeEnd;

    private Animator animator;

    private bool isHoldingButton;

    private bool isOn;

    private PlayerControllerB previousPlayerHeldBy;

    public GameObject blowtorchLight;

    [Space(5f)]
    public AudioSource torchAudio;

    public AudioClip[] on;

    public AudioClip[] off;

    public AudioClip[] fireLoop;

    public override void Start()
    {
        base.Start();
        animator = base.gameObject.GetComponent<Animator>();
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        Debug.Log("ItemActivate called for blowtorch.");
        base.ItemActivate(used, buttonDown);
        isHoldingButton = buttonDown;
        if (buttonDown)
        {
            Debug.Log($"Blowtorch isBeingUsed: {isBeingUsed}, buttonDown: {buttonDown}");
            previousPlayerHeldBy = playerHeldBy;
            if (!isOn)
            {
                turnOn();
                return;
            }
            if (isOn)
            {
                turnOff();
                return;
            }
        }
    }

    public IEnumerator delayTorchOn(float delay)
    {
        yield return new WaitForSeconds(delay);

        //CHECK FOR COLLIDERS
        Collider[] colliders = checkColliders();
        for (int i = 0; i < colliders.Length; i++)
        {
            //FOR PLAYERS
            if (colliders[i].gameObject.layer == 3)
            {
                PlayerControllerB playerControllerB = colliders[i].gameObject.GetComponent<PlayerControllerB>();
                Debug.Log($"Blowtorch detected {playerControllerB}.");
                if (playerControllerB != null && playerControllerB.IsOwner)
                {
                    Vector3 bodyVelocity = Vector3.Normalize(playerControllerB.gameplayCamera.transform.position - base.transform.position) * 80f / Vector3.Distance(playerControllerB.gameplayCamera.transform.position, base.transform.position);
                    playerControllerB.DamagePlayer(damage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Burning, 0, fallDamage: false, bodyVelocity);
                    Debug.Log($"Blowtorch damaged {playerControllerB}.");
                }
            }

            //FOR ENEMIES
            else if (colliders[i].gameObject.layer == 19)
            {
                EnemyAICollisionDetect enemy = colliders[i].gameObject.GetComponentInChildren<EnemyAICollisionDetect>();
                Debug.Log($"Blowtorch detected {enemy}.");
                if (enemy != null && enemy.mainScript.IsOwner)
                {
                    enemy.mainScript.HitEnemyOnLocalClient(damage);
                    Debug.Log($"Blowtorch damaged {enemy}.");
                }
            }
        }

        blowtorchLight.SetActive(true);

        //FIGURE OUT HOW TO MAKE THIS LOOP
        torchAudio.loop = true;
        RoundManager.PlayRandomClip(torchAudio, fireLoop, randomize: true, 1f, -1);
    }

    public IEnumerator delayTorchOff(float delay)
    {
        yield return new WaitForSeconds(delay);
        blowtorchLight.SetActive(false);
        isOn = false;
    }

    public void turnOn()
    {
        Debug.Log("turnOn called for blowtorch.");
        isOn = true;
        animator.Play("turnOn");

        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(torchAudio, on, randomize: true, 1f, -1);

        StartCoroutine(delayTorchOn(on[0].length));
    }

    public void turnOff()
    {
        Debug.Log("turnOff called for blowtorch.");
        animator.Play("turnOff");
        
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        torchAudio.loop = false;
        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(torchAudio, off, randomize: true, 1f, -1);

        StartCoroutine(delayTorchOff(off[0].length));
    }

    private Collider[] checkColliders()
    {
        Collider[] colliders = Physics.OverlapCapsule(rangeStart.transform.position, rangeEnd.transform.position, 0.2f, 2621448, QueryTriggerInteraction.Collide);
        return colliders;
    }

}