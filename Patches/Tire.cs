using GameNetcodeStuff;
using UnityEngine;

public class Tire : AnimatedItem, IHittable, ITouchable
{
    [Header("Tire Settings")]
    public float tireRadius;

    private bool rolling;

    private bool rollingUp;

    private float speed;

    private float rollRotation;

    private GameObject rollObject;

    public AudioSource tireSource;

    public AudioClip[] switchToRollingSFX;

    public AudioClip[] switchToHoldingSFX;

    public AudioClip[] rollingSFX;

    public AudioClip[] bounceSFX;

    public AudioClip[] hitSFX;

    public override void Start()
    {
        base.Start();
        rollObject = new GameObject();
        rollObject.transform.position = transform.position;
    }

    public override void Update()
    {
        base.Update();

        if (playerHeldBy != null)
        {
            rollObject.transform.position = playerHeldBy.transform.position + Vector3.up + playerHeldBy.transform.forward;
            rollObject.transform.rotation = playerHeldBy.transform.rotation;
            rollObject.transform.Rotate(90, 90, 0);
            rollObject.transform.Rotate(Vector3.down * rollRotation);

            //MOVE ROLLOBJECT UP WITH FLOOR
            if (Physics.Raycast(rollObject.transform.position - Vector3.down * 0.5f, Vector3.down, out var hitInfo, 3f, 1073742080, QueryTriggerInteraction.Ignore))
            {
                //IF NEW POSITION IS HIGHER THAN LAST
                if (rollObject.transform.position.y < hitInfo.point.y)
                {
                    rollingUp = true;
                }
                //IF NEW POSITION IS LOWER THAN LAST
                else
                {
                    rollingUp = false;
                }
            }
            else
            {
                playerHeldBy.DiscardHeldObject();
            }

            if (rolling)
            {
                //CHECK AHEAD FOR WALL TO PREVENT MOVEMENT
                if (Physics.Raycast(rollObject.transform.position, playerHeldBy.transform.forward, out var hitPoint, 3f, 1073742080, QueryTriggerInteraction.Ignore))
                {
                    if (Vector3.Distance(rollObject.transform.position, hitPoint.transform.position) < tireRadius)
                    {
                        Debug.Log("Tire hit wall!");
                    }
                }

                //ROTATE WHEN PLAYER WALKS
                float rollSpeed = 1f;
                if (playerHeldBy.isSprinting)
                {
                    rollSpeed *= 1.75f;
                }
                if (playerHeldBy.movingForward)
                {
                    rollRotation -= playerHeldBy.averageVelocity * rollSpeed;
                }
                else
                {
                    rollRotation += playerHeldBy.averageVelocity * rollSpeed;
                }

                //ROLL AWAY WHEN JUMPING
                if (playerHeldBy.isJumping)
                {
                    Debug.Log("Player jumped, discarding.");
                    playerHeldBy.DiscardHeldObject();
                }

                //ROLL AWAY WHEN NO FLOOR
                // if (!Physics.Raycast(transform.position, Vector3.down, out var hitInfo, 10f, 1073742080, QueryTriggerInteraction.Ignore))
                // {
                //     Debug.Log("No floor detected under tire, rolling away from player.");
                //     RollAwayFromPlayer();
                // }
            }
        }
    }

    public override void LateUpdate()
    {
        if (parentObject != null)
        {
            base.transform.rotation = parentObject.rotation;
            base.transform.position = parentObject.position;

            if (rolling)
            {
                //SET POSITION TO HEIGHT OF FLOOR
                if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var hitInfo, 3f, 1073742080, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hitInfo.point + Vector3.up * tireRadius;
                }
            }
            else if (playerHeldBy != null)
            {
                base.transform.Rotate(itemProperties.rotationOffset);
                Vector3 positionOffset = itemProperties.positionOffset;
                positionOffset = parentObject.rotation * positionOffset;
                base.transform.position += positionOffset;
            }
        }

        if (radarIcon != null)
		{
			radarIcon.position = base.transform.position;
		}
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (rolling)
        {
            SwitchToHolding();
        }

        else
        {
            SwitchToRolling();
        }
    }

    public void SwitchToRolling()
    {
        Debug.Log("Tire switched to rolling.");
        rolling = true;

        //CHANGE PLAYER ANIMATION

        //LIMIT MOUSE LOOK OF PLAYER
        playerHeldBy.horizontalClamp = 90;
        playerHeldBy.clampLooking = true;

        //CHANGE TIRE LOCATION & PARENT
        parentObject = rollObject.transform;

        //ROTATE TIRE WHEN PLAYER WALKS
        itemAnimator.Play("roll");

        //REMOVE TIRE WEIGHT FROM PLAYER
        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight - (itemProperties.weight - 1f), 1f, 10f);

        //PLAY SOUND
        RoundManager.PlayRandomClip(itemAudio, switchToRollingSFX, randomize: true, 1f, -1);
    }

    public void SwitchToHolding()
    {
        Debug.Log("Tire switched to holding.");
        rolling = false;

        //CHANGE PLAYER ANIMATION

        //REMOVE MOUSE LOOK LIMIT FROM PLAYER
        playerHeldBy.clampLooking = false;

        //RESET TIRE INTO HANDS
        transform.SetParent(null);
        parentObject = playerHeldBy.localItemHolder;

        //STOP ROTATION OF TIRE
        itemAnimator.speed = 1;
        itemAnimator.Play("stop");

        //ADD TIRE WEIGHT TO PLAYER
        playerHeldBy.carryWeight = Mathf.Clamp(playerHeldBy.carryWeight + (itemProperties.weight - 1f), 1f, 10f);

        //PLAY SOUND
        RoundManager.PlayRandomClip(itemAudio, switchToHoldingSFX, randomize: true, 1f, -1);
    }

    public override void GrabItem()
    {
        if (rolling)
        {
            base.GrabItem();
            SwitchToRolling();
        }

        else
        {
            base.GrabItem();
        }
    }

    public override void DiscardItem()
    {
        if (rolling)
        {
            RollAwayFromPlayer();
            base.DiscardItem();
        }
        
        else
        {
            base.DiscardItem();
        }
    }

    public void RollAwayFromPlayer()
    {
        Debug.Log("Tire rolled away from player.");

        //CHANGE PLAYER ANIMATION

        //SET TIRE TO USE RIGIDBODY
    }

    public void OnTouch(Collider other)
    {
        GameObject otherObject = other.gameObject;

        if (rolling)
        {
            //PHYSICS FORCE TO OTHER PLAYERS

            //WOBBLE VASES

            if (speed > 10)
            {
                //DEAL DAMAGE TO PLAYERS

                //DEAL DAMAGE TO ENEMIES

                //BREAK VASES
            }
        }
    }

    public void OnExit(Collider other)
    {
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, hitSFX, randomize: true, 1f, -1);
        return true;
	}
}