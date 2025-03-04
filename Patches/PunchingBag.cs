using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class PunchingBag : NetworkBehaviour, IHittable, ITouchable
{
    public Animator punchingBagAnimator;

    public Collider interactCollider;

    public Collider playerCollider;

    public InteractTrigger interactTrigger;

    public int ripState;

    public int damageState;

    public int bumpState;

    public bool canBeBroken;

    public bool isBroken;

    public bool damaging;

    private float physicsForce;
    
    private bool hitMaskOnce;

    public void OnEnable()
    {
        damageState = 0;
        ripState = 0;
        bumpState = 0;
        isBroken = false;
        hitMaskOnce = false;
        interactCollider.enabled = true;
        playerCollider.enabled = true;
    }

    public void PunchDefault()
    {
        PunchAndSync(true);
    }

    public void Punch(bool damaging = false, string punchSource = null)
    {
        if (isBroken)
        {
            return;
        }

        if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
        {
            canBeBroken = false;
        }
        else
        {
            canBeBroken = true;
        }
        
        if (!damaging)
        {
            if (bumpState != 4)
            {
                bumpState++;
            }
            else
            {
                bumpState = 0;
            }
            punchingBagAnimator.SetInteger("bumpstate", bumpState);
            punchingBagAnimator.SetTrigger("bump");
            return;
        }

        if (punchSource == "Shotgun" || punchSource == "Explosion" || punchSource == "Lunging dog" || punchSource == "Chasing dog")
        {
            if (canBeBroken)
            {
                ripState = 4;
                punchingBagAnimator.SetInteger("ripstate", ripState);
                punchingBagAnimator.Play("Rip Layer.Rip 4");
                punchingBagAnimator.SetTrigger("break");
                isBroken = true;
                interactCollider.enabled = false;
                playerCollider.enabled = false;
                return;
            }
            ripState = 4;
            punchingBagAnimator.SetInteger("ripstate", ripState);
        }
        else if (punchSource == "Kitchen knife")
        {
            if (ripState != 4)
            {
                ripState++;
                punchingBagAnimator.SetInteger("ripstate", ripState);
            }
        }
        else if (punchSource == "Blowtorch")
        {
            if (ripState != 4)
            {
                ripState++;
                punchingBagAnimator.SetInteger("ripstate", ripState);
            }
        }

        if (damageState != 4)
        {
            damageState++;
        }
        else
        {
            damageState = 0;
        }
        punchingBagAnimator.SetInteger("damagestate", damageState);
        punchingBagAnimator.SetTrigger("damage");
        return;
    }
    
    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (playerWhoHit.currentlyHeldObjectServer.itemProperties.itemName == "Kitchen knife" || playerWhoHit.currentlyHeldObjectServer.itemProperties.itemName == "Rake")
        {
            PunchAndSync(true, "Kitchen knife");
            return false;
        }
        else
        {
            PunchAndSync(true);
        }
        return true;
	}

    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        if (otherObject.layer != 3 && otherObject.layer != 19 && otherObject.layer != 30 && otherObject.layer != 6)
        {
            return;
        }

        RaycastHit hitInfo;
        if (Physics.Linecast(transform.position, other.transform.position, out hitInfo, CoronaMod.Masks.RoomVehicle, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (otherObject.layer == 3 && otherObject.GetComponent<PlayerControllerB>() != null)
        {
            PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

            if (player.isSprinting)
            {
                PunchAndSync(true);
                physicsForce = 3f;
            }
            else
            {
                PunchAndSync(false);
                physicsForce = 2f;
            }
            
            if (!isBroken)
            {
                if (!Physics.Linecast(base.transform.position, player.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
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
        }

        else if (otherObject.TryGetComponent<TireReferenceScript>(out var tireReferenceScript))
        {
            float speed = otherObject.GetComponent<Rigidbody>().velocity.magnitude;
            if (speed > 2f && speed < 4f)
            {
                PunchAndSync(false);
                tireReferenceScript.mainScript.BounceOff(base.transform.position, extraForce: 5f);
            }
            else if (speed >= 4f)
            {
                PunchAndSync(true);
                tireReferenceScript.mainScript.BounceOff(base.transform.position, extraForce: 10f);
            }
        }

        else if (otherObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
        {
            EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();

            if (enemy.mainScript.enemyType.enemyName == "MouthDog")
            {
                MouthDogAI mouthDog = enemy.mainScript as MouthDogAI;
                if (mouthDog.inLunge)
                {
                    PunchAndSync(true, "Lunging dog");
                }
                else if (mouthDog.hasEnteredChaseModeFully)
                {
                    PunchAndSync(true, "Chasing dog");
                }
            }
            else if (enemy.mainScript.enemyType.enemyName == "Masked")
            {
                MaskedPlayerEnemy masked = enemy.mainScript as MaskedPlayerEnemy;
                if (masked.sprinting)
                {
                    if (!hitMaskOnce)
                    {
                        masked.HitEnemyOnLocalClient();
                        hitMaskOnce = true;
                    }
                    PunchAndSync(true);
                }
                else
                {
                    PunchAndSync(false);
                }
            }
            else
            {
                PunchAndSync(true);
            }
        }

        else if (otherObject.layer == 6 && otherObject.GetComponent<GrabbableObject>() != null)
        {
            GrabbableObject gObject = otherObject.GetComponent<GrabbableObject>();
            if (gObject.itemProperties.itemName == "Soccer ball")
            {
                SoccerBallProp ball = otherObject.GetComponent<SoccerBallProp>();
                if (!ball.hasHitGround && !ball.isHeld && !ball.isHeldByEnemy)
                {
                    PunchAndSync(true);
                    ball.BeginKickBall(transform.position + Vector3.up, false);
                }
            }
        }
    }

    public void OnExit(Collider other)
    {
    }

    public void PunchAndSync(bool damaging = false, string punchSource = null)
    {
        Punch(damaging, punchSource);
        PunchServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, damaging, punchSource);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void PunchServerRpc(int clientWhoSentRpc, bool damaging = false, string punchSource = null)
    {
        PunchClientRpc(clientWhoSentRpc, damaging, punchSource);
    }

    [ClientRpc]
    public void PunchClientRpc(int clientWhoSentRpc, bool damaging = false, string punchSource = null)
    {
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            Punch(damaging, punchSource);
        }
    }

}