using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class ArtilleryShellItem : AnimatedItem, IHittable, IShockableWithGun, ITouchable
{
    [Header("Artillery Shell Settings")]
    public float explodeHeight;

	private float fallHeight;

    public float killRange;

    public float damageRange;

    public float pushRange;

	[SerializeField] public LayerMask layerMask;

	private int layers;

	private int mask;

    public int nonLethalDamage;

    public float physicsForce;

	public float delayedDetonationTime;

	public bool hasExploded;

	private Vector3 startPosition;

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
		int mask = 1 << 3 | 1 << 6 | 1 << 19 | 1 << 21 | 1 << 30;
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

	// private void OnTriggerEnter(Collider other)
	// {
	// 	if (other.gameObject.name == "ItemDropRegion")
	// 	{
	// 		base.isInElevator == true;
	// 	}
	// }

	// private void OnTriggerExit(Collider other)
	// {
	// 	if (other.gameObject.name == "ItemDropRegion")
	// 	{
	// 		base.isInElevator == false;
	// 	}
	// }

	public override void DiscardItem()
	{
		startPosition = base.transform.position;
		if (base.transform.parent != null)
		{
			startPosition = base.transform.parent.InverseTransformPoint(startPosition);
		}

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

		Vector3 fallPosition = base.transform.position;
		if (base.transform.parent != null)
		{
			fallPosition = base.transform.parent.InverseTransformPoint(fallPosition);
		}

		fallHeight = Vector3.Distance(fallPosition,startPosition);
		Debug.Log($"Shell fell: {fallHeight}");

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
		// else if (otherObject.transform.parent.gameObject.layer == 30) //REWRITE THIS!!
		// {
		// 	VehicleController vehicle = otherObject.GetComponentInParent<VehicleController>();
		// 	if (vehicle.averageVelocity.magnitude > 17)
		// 	{
		// 		Detonate();
		// 	}
		// 	return;
		// }

		//PROPS COLLISION
		else if (otherObject.layer == 6)
		{
			Debug.Log("Detected collider on prop layer.");
			if (otherObject.name.StartsWith("explosionColliderDamage"))
			{
				Debug.Log("Detected explosion collider.");
				Detonate();
			}
		}
	}

	public void OnExit(Collider other)
	{

	}

	//SHOCKABLE PARAMS
	bool IShockableWithGun.CanBeShocked()
	{
		return explodeOnShockWithGun;
	}
	float IShockableWithGun.GetDifficultyMultiplier()
	{
		return 1;
	}
	NetworkObject IShockableWithGun.GetNetworkObject()
	{
		return base.NetworkObject;
	}
	Vector3 IShockableWithGun.GetShockablePosition()
	{
		return base.transform.position;
	}
	Transform IShockableWithGun.GetShockableTransform()
	{
		return base.transform;
	}
	void IShockableWithGun.ShockWithGun(PlayerControllerB shockedByPlayer)
	{
		if (explodeOnShockWithGun == true)
		{
			Detonate();
		}
	}
	void IShockableWithGun.StopShockingWithGun()
	{
	}

}