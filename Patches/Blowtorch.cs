using GameNetcodeStuff;
using UnityEngine;

public class Blowtorch : AnimatedItem
{

    [Header("Blowtorch Settings")]
    public int damage;

    public GameObject rangeStart;

    public GameObject rangeEnd;

    public override void Start()
    {
        base.Start();
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        Debug.Log("ItemActivate called for blowtorch.");
        base.ItemActivate(used, buttonDown);
        if (buttonDown)
        {
            AnimatorStateInfo state = itemAnimator.GetCurrentAnimatorStateInfo(0);
            
            itemAnimator.SetTrigger("Used");
        }
    }

    public void DamageWithFlame(int force)
    {
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
    }

    public void MakeNoise()
    {
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
    }

    private Collider[] checkColliders()
    {
        Collider[] colliders = Physics.OverlapCapsule(rangeStart.transform.position, rangeEnd.transform.position, 0.2f, 2621448, QueryTriggerInteraction.Collide);
        return colliders;
    }

}