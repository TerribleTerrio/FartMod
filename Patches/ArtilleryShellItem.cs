using System.Collections;
using GameNetcodeStuff;
using UnityEngine;
using Unity.Netcode;

public class ArtilleryShellItem : AnimatedItem, IHittable, ITouchable, ZappableObject
{
    [Header("Artillery Shell Settings")]
    public float explodeHeight;

	private float fallHeight;

    public float killRange;

    public float damageRange;

    public float pushRange;

	[SerializeField] public LayerMask layerMask;

	private int layers;

    public int nonLethalDamage;

    public float physicsForce;

	public float delayedDetonationTime;

	public bool hasExploded;

	[Space(5f)]
	public GameObject explosionPrefab;

	[Space(5f)]
    public bool explodeOnHit;

    public float explodeOnHitChance;

	public bool explodeOnDrop;

	public float explodeOnDropChance;

    public bool explodeOnBlast;

    public bool explodeOnMauling;

    public bool explodeOnCrushing;

    public bool explodeOnFalling;

	public bool explodeOnGunshot;

	public bool explodeOnShockWithGun;

	public bool explodeInOrbit;

    [Space(5f)]
	public AudioSource shellExplodeSource;

	public AudioSource shellExplodeSourceMedium;

	public AudioSource shellExplodeSourceFar;

    public AudioClip[] shellHit;

	public AudioClip[] shellDud;

    public AudioClip[] shellExplode;

	public AudioClip[] shellExplodeMedium;

    public AudioClip[] shellExplodeFar;

	public AudioClip[] shellArmed;

	public override void Start()
	{
		base.Start();
		Debug.Log($"Shell explosion layer mask: {layerMask}");
		layers = (1 << layerMask);
		Debug.Log($"Shell explosion layer mask hash: {layers}");
		int mask = 1 << 2 | 1 << 3 | 1 << 4| 1 << 5 | 1 << 6 | 1 << 7 | 1 << 9 | 1 << 10 | 1 << 13 | 1 << 14 | 1 << 15 | 1 << 16 | 1 << 17 | 1 << 18 | 1 << 19 | 1 << 20 | 1 << 21 | 1 << 22 | 1 << 23 | 1 << 27 | 1 << 28 | 1 << 29;
		Debug.Log($"mask: {mask}");

		float f1 = 55f*0.4f;
		float t1 = 340f*0.4f;
		float f2 = 0.5f;
		float t2 = 1.5f;
		float m = (base.scrapValue - f1) / (t1 - f1) * (t2 - f2) + f2;
		Debug.Log($"Shell price: {base.scrapValue}");
		Debug.Log($"Shell danger: {m}");
		killRange = killRange*m;
		damageRange = damageRange*m;
		pushRange = pushRange*m;
		hasExploded = false;
	}

	public override void DiscardItem()
	{
		if (playerHeldBy.isPlayerDead == true)
		{
			//DIED BY BLAST
			if (explodeOnBlast && playerHeldBy.causeOfDeath == CauseOfDeath.Blast)
			{
				Detonate();
			}

			//DIED BY MAULING
			else if (explodeOnMauling && playerHeldBy.causeOfDeath == CauseOfDeath.Mauling)
			{
				Detonate();
			}

			//DIED BY CRUSHING
			else if (explodeOnCrushing && playerHeldBy.causeOfDeath == CauseOfDeath.Crushing)
			{
				Detonate();
			}

			//DIED BY FALLING
			else if (explodeOnFalling && playerHeldBy.causeOfDeath == CauseOfDeath.Gravity)
			{
				Detonate();
			}

			//DIED BY GUNSHOT
			else if (explodeOnGunshot && playerHeldBy.causeOfDeath == CauseOfDeath.Gunshots)
			{
				Detonate();
			}
		}

		base.DiscardItem();
	}

	public override void OnHitGround()
	{
		base.OnHitGround();

		Debug.Log("Shell dropped.");

		fallHeight = startFallingPosition.y - targetFloorPosition.y;

		float c = UnityEngine.Random.Range(0,100);

		if (fallHeight > explodeHeight && explodeOnDrop == true && !base.isInShipRoom && !base.isInElevator)
		{
			if (c < explodeOnDropChance)
			{
				Detonate();
			}
			else
			{
				RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange*5, noiseLoudness*2, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

        		RoundManager.PlayRandomClip(shellExplodeSource, shellDud, randomize: false, 1f, -1);
				RoundManager.PlayRandomClip(shellExplodeSourceMedium, shellDud, randomize: false, 1f, -1);
				RoundManager.PlayRandomClip(shellExplodeSourceFar, shellDud, randomize: false, 1f, -1);
			}
		}
	}

    public void Detonate()
    {
		if (!explodeInOrbit)
		{
			if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
			{
				return;
			}
		}
		if (hasExploded)
		{
			return;
		}
		Debug.Log("Artillery shell detonated!");
		Explode();
    }

	public void ArmShell()
	{
		if (hasExploded)
		{
			return;
		}
		Debug.Log("Shell armed.");
		RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, shellArmed, randomize: false, 1f, -1);
		StartCoroutine(DelayDetonate(delayedDetonationTime));
	}

	public IEnumerator DelayDetonate(float delay)
	{
		yield return new WaitForSeconds(delay);
		Detonate();
	}

	public IEnumerator DelayDetonateOtherShell(float delay, ArtilleryShellItem shell)
	{
		if (!shell.hasExploded)
		{
			yield return new WaitForSeconds(delay);
			shell.Detonate();
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public void Explode()
	{
		hasExploded = true;
		Landmine.SpawnExplosion(this.gameObject.transform.position + Vector3.up, true, killRange, damageRange, nonLethalDamage, physicsForce, explosionPrefab, true);

		Collider[] colliders = Physics.OverlapSphere(this.gameObject.transform.position, pushRange, 3, QueryTriggerInteraction.Collide);
		for (int i = 0; i < colliders.Length; i++)
		{
			GameObject otherObject = colliders[i].gameObject;
			if (otherObject.GetComponent<PlayerControllerB>() != null)
			{
				RaycastHit hitInfo;
				PlayerControllerB playerControllerB = otherObject.GetComponent<PlayerControllerB>();
				if (physicsForce > 0f && !Physics.Linecast(base.transform.position, playerControllerB.transform.position, out hitInfo, 256, QueryTriggerInteraction.Ignore))
				{
					float dist = Vector3.Distance(playerControllerB.transform.position, base.transform.position);
					Vector3 vector = Vector3.Normalize(playerControllerB.transform.position + Vector3.up * dist - base.transform.position) / (dist * 0.35f) * physicsForce;
					if (vector.magnitude > 2f)
					{
						if (vector.magnitude > 10f)
						{
							playerControllerB.CancelSpecialTriggerAnimations();
						}
						if (!playerControllerB.inVehicleAnimation || (playerControllerB.externalForceAutoFade + vector).magnitude > 50f)
						{
								playerControllerB.externalForceAutoFade += vector;
						}
					}
				}
			}
		}

		EnableItemMeshes(enable: false);
        grabbable = false;
        grabbableToEnemies = false;
        deactivated = true;
		if (heldByPlayerOnServer)
		{
			playerHeldBy.DropItemAheadOfPlayer();
		}
		StartCoroutine(DelayDestroySelf(delayedDetonationTime));
	}

	public IEnumerator DelayDestroySelf(float delay)
	{
		yield return new WaitForSeconds(delay);
		base.gameObject.SetActive(false);
		base.targetFloorPosition.x = 3000f;
		// UnityEngine.Object.Destroy(base.gameObject);
	}

    //HITTABLE PARAMS
    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{

        Debug.Log("Shell hit.");

		RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, shellHit, randomize: true, 1f, -1);

        if (explodeOnHit == true)
        {
            float c = UnityEngine.Random.Range(0,100);
            if (c < explodeOnHitChance)
			{
				ArmShell();
			}
			else
			{
				Debug.Log("Shell not armed.");
			}
        }

        return true;
	}

	//TOUCHABLE PARAMS
	public void OnTouch(Collider other)
	{
		if (hasExploded)
		{
			return;
		}

		GameObject otherObject = other.gameObject;

		//PLAYER COLLISION
		if (otherObject.layer == 3)
		{
			PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();
			return;
		}

		//ENEMY COLLISION
		if (otherObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
		{
			EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
			if (enemy.mainScript.enemyType.enemyName == "RadMech")
			{
				Detonate();
				enemy.mainScript.SetEnemyStunned(true, 2f);
			}
			if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
			{
				Detonate();
			}
			return;
		}

		//TRIGGERS COLLISION
		else if (otherObject.layer == 13)
        {
			if (otherObject.GetComponentInParent<SpikeRoofTrap>() != null)
			{
				Detonate();
			}
			return;
        }

		//VEHICLE COLLISION
		else if (otherObject.transform.parent != null && otherObject.transform.parent.gameObject.layer == 30)
		{
			VehicleController vehicle = otherObject.GetComponentInParent<VehicleController>();
			if (vehicle.averageVelocity.magnitude > 17)
			{
				Detonate();
			}
			return;
		}
	}

	public void OnExit(Collider other)
	{

	}

	public float GetZapDifficulty()
	{
		return 1;
	}

	public void StopShockingWithGun()
	{

	}

	public void ShockWithGun(PlayerControllerB playerControllerB)
	{
		ArmShell();
	}

}