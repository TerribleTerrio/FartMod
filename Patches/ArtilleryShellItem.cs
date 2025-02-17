using System.Collections;
using GameNetcodeStuff;
using UnityEngine;
using Unity.Netcode;

public class ArtilleryShellItem : AnimatedItem, IHittable, ITouchable, ZappableObject
{
	[Space(15f)]
    [Header("Artillery Shell Settings")]
    public float explodeHeight;

	private float fallHeight;

    public float killRange;

    public float damageRange;

    public float pushRange;

    public int nonLethalDamage;

    public float physicsForce;

	public float delayedDetonationTime;

	public GameObject explosionPrefab;

	public bool exploded;

	[Space(10f)]
	[Header("Toggles")]
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

    [Space(10f)]
	[Header("Audio")]
	public AudioSource shellSource;

	public AudioSource shellMediumSource;

	public AudioSource shellFarSource;

    public AudioClip[] shellHit;

	public AudioClip[] shellDud;

	public AudioClip[] shellArmed;

	public AudioClip shellZapped;

	private Coroutine waitToBeEatenCoroutine;
	
	private bool hasBeenSeen;

	public override void Start()
	{
		base.Start();

		Debug.Log($"Shell spawned with scrap value {scrapValue}.");
		float dangerLevel = Remap(scrapValue, itemProperties.minValue * 0.4f, itemProperties.maxValue * 0.4f, 0f, 100f);
		Debug.Log($"Shell spawned with danger level {dangerLevel}");
		float dangerMultiplier = Remap(dangerLevel, 0f, 100f, 0.85f, 1.25f);
		killRange *= dangerMultiplier;
		damageRange *= dangerMultiplier;
		pushRange *= dangerMultiplier;
	}

    public override void Update()
    {
        base.Update();

        if (!hasBeenSeen)
        {
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(transform.position))
                {
                    hasBeenSeen = true;
                    Debug.Log($"[ARTILLERY SHELL]: Has been seen by {StartOfRound.Instance.allPlayerScripts[i]}.");
                }
            }
        }
    }

    public override void GrabItem()
	{
		base.GrabItem();
		RestartWaitToBeEaten();
	}

	public void RestartWaitToBeEaten(bool restart = true)
	{
		if (waitToBeEatenCoroutine != null)
		{
			StopCoroutine(waitToBeEatenCoroutine);
			waitToBeEatenCoroutine = null;
		}
		if (restart)
		{
			waitToBeEatenCoroutine = StartCoroutine(WaitToBeEaten());
		}
	}

	private IEnumerator WaitToBeEaten()
	{
		yield return new WaitUntil(() => (bool)playerHeldBy.inAnimationWithEnemy);
		if (playerHeldBy.inAnimationWithEnemy.enemyType.enemyName == "ForestGiant")
		{
			Debug.Log("FOREST GIANT EATING A BOMB!!!");
			yield return new WaitForSeconds(4.4f);
			ExplodeAndSync();
		}
		else
		{
			Debug.Log("In special animation with enemy while holding a bomb.");
			RestartWaitToBeEaten();
		}
	}

	public override void DiscardItem()
	{
		RestartWaitToBeEaten(false);
		if (playerHeldBy.isPlayerDead == true)
		{
			//DIED BY BLAST
			if (explodeOnBlast && playerHeldBy.causeOfDeath == CauseOfDeath.Blast)
			{
				ExplodeAndSync();
			}

			//DIED BY MAULING
			else if (explodeOnMauling && playerHeldBy.causeOfDeath == CauseOfDeath.Mauling)
			{
				ExplodeAndSync();
			}

			//DIED BY CRUSHING
			else if (explodeOnCrushing && playerHeldBy.causeOfDeath == CauseOfDeath.Crushing)
			{
				ExplodeAndSync();
			}

			//DIED BY FALLING
			else if (explodeOnFalling && playerHeldBy.causeOfDeath == CauseOfDeath.Gravity)
			{
				ExplodeAndSync();
			}

			//DIED BY GUNSHOT
			else if (explodeOnGunshot && playerHeldBy.causeOfDeath == CauseOfDeath.Gunshots)
			{
				ExplodeAndSync();
			}
		}

		base.DiscardItem();
	}

	public override void OnHitGround()
	{
		base.OnHitGround();
		fallHeight = startFallingPosition.y - targetFloorPosition.y;

		if (fallHeight > explodeHeight && explodeOnDrop == true && !base.isInShipRoom && !base.isInElevator)
		{
			if (base.IsOwner)
			{
				float c = UnityEngine.Random.Range(0,100);
				if (c < explodeOnDropChance)
				{
					ExplodeAndSync();
				}
				else
				{
					RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange*5, noiseLoudness*2, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

					RoundManager.PlayRandomClip(shellSource, shellDud, randomize: false, 1f, -1);
					RoundManager.PlayRandomClip(shellMediumSource, shellDud, randomize: false, 1f, -1);
					RoundManager.PlayRandomClip(shellFarSource, shellDud, randomize: false, 1f, -1);
				}
			}
		}
	}

	public void ArmShellAndSync()
	{
		if (!explodeInOrbit)
		{
			if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
			{
				return;
			}
		}
		if (!hasBeenSeen)
		{
			return;
		}
		ArmShell();
		ArmShellServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
	}

	[ServerRpc(RequireOwnership = false)]
	public void ArmShellServerRpc(int clientWhoSentRpc)
	{
		ArmShellClientRpc(clientWhoSentRpc);
		StartCoroutine(DelayDetonate(delayedDetonationTime));
	}

	[ClientRpc]
	public void ArmShellClientRpc(int clientWhoSentRpc)
	{
		if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
		{
			ArmShell();
		}
	}

	public void ArmShell()
	{
		if (exploded)
		{
			return;
		}
		RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, shellArmed, randomize: false, 1f, -1);
	}

	public IEnumerator DelayDetonate(float delay)
	{
		yield return new WaitForSeconds(delay);
		ExplodeAndSync();
	}

	public void ExplodeAndSync()
	{
		Explode();
		ExplodeServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
	}

	[ServerRpc(RequireOwnership = false)]
	public void ExplodeServerRpc(int clientWhoSentRpc)
	{
		ExplodeClientRpc(clientWhoSentRpc);
		StartCoroutine(DespawnAfterFrame());
	}

	public IEnumerator DespawnAfterFrame()
	{
		yield return new WaitForEndOfFrame();
        if (base.gameObject.GetComponent<NetworkObject>().IsSpawned)
        {
			Debug.Log("[ARTILLERY SHELL]: Despawning!");
            base.gameObject.GetComponent<NetworkObject>().Despawn();
        }
	}

	[ClientRpc]
	public void ExplodeClientRpc(int clientWhoSentRpc)
	{
        if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            Explode();
        }
	}

	public void Explode()
	{
		//CHECK IF SHELL CAN EXPLODE
		if (exploded || !hasBeenSeen)
		{
			return;
		}
		if (!explodeInOrbit)
		{
			if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
			{
				return;
			}
		}
        if (heldByPlayerOnServer)
        {
			Debug.Log("[ARTILLERY SHELL]: Discarding held object from player!");
            playerHeldBy.DiscardHeldObject();
        }
        else if (isHeldByEnemy)
        {
			Debug.Log("[ARTILLERY SHELL]: Discarding held object from enemy!");
            base.DiscardItemFromEnemy();
        }

		//SET FLAGS
		exploded = true;

		//SPAWN EXPLOSION EFFECT (DETECTS OBJECTS EFFECTED BY EXPLOSIONS)
		Landmine.SpawnExplosion(this.gameObject.transform.position + Vector3.up, true, killRange, damageRange, nonLethalDamage, physicsForce, explosionPrefab, true);

		//CHECK FOR COLLIDERS IN RANGE
		Collider[] colliders = Physics.OverlapSphere(this.gameObject.transform.position, pushRange, CoronaMod.Masks.PlayerEnemies, QueryTriggerInteraction.Collide);
		for (int i = 0; i < colliders.Length; i++)
		{
			//CHECK THAT COLLIDERS ARE NOT BEHIND WALLS
			if (!Physics.Linecast(base.transform.position, colliders[i].transform.position, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				GameObject otherObject = colliders[i].gameObject;
				float dist = Vector3.Distance(otherObject.transform.position, base.transform.position);

				//FOR PLAYERS
				if (otherObject.GetComponent<PlayerControllerB>() != null)
				{
					PlayerControllerB player = otherObject.GetComponent<PlayerControllerB>();

					//APPLY PHYSICS FORCE BASED ON DISTANCE OF PLAYER FROM EXPLOSION
					if (physicsForce > 0f && dist > killRange && dist < pushRange)
					{
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

					//CAMERA SHAKE
					if ((int)player.playerClientId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
					{
						if (dist < pushRange/4)
						{
							HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
						}
						else if (dist < pushRange/2)
						{
							HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
						}
						else if (dist < pushRange)
						{
							HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
						}
					}
				}

				//FOR ENEMIES
				if (otherObject.TryGetComponent<EnemyAICollisionDetect>(out var enemy) && enemy.TryGetComponent<IHittable>(out var hittable))
				{
					if (enemy.mainScript != null && enemy.mainScript.IsOwner)
					{
						hittable.Hit(6, Vector3.Normalize(base.transform.position - enemy.transform.position));
						enemy.mainScript.HitFromExplosion(dist);
					}
				}
			}
		}
	}

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
	{
		RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        RoundManager.PlayRandomClip(itemAudio, shellHit, randomize: true, 1f, -1);

		if (explodeOnHit == true)
		{
			float c;
			if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
			{
				if (explodeInOrbit)
				{
					c = UnityEngine.Random.Range(0,100);
					if (c < explodeOnHitChance)
					{
						ArmShellAndSync();
					}
				}
			}
			else
			{
				c = UnityEngine.Random.Range(0,100);
				if (c < explodeOnHitChance)
				{
					ArmShellAndSync();
				}
			}
		}
		
        return false;
	}

	public void OnTouch(Collider other)
	{
		GameObject otherObject = other.gameObject;

		//TIRE COLLISION
		if (otherObject.layer == 3 && otherObject.name.Contains("PhysicsTire"))
		{
			float speed = otherObject.GetComponent<Rigidbody>().velocity.magnitude;
			if (speed >= 8f)
			{
				ArmShellAndSync();
			}
		}

		//ENEMY COLLISION
		if (otherObject.layer == 19 && otherObject.GetComponent<EnemyAICollisionDetect>() != null)
		{
			EnemyAICollisionDetect enemy = otherObject.GetComponent<EnemyAICollisionDetect>();
			if (enemy.mainScript.enemyType.enemyName == "RadMech")
			{
				ExplodeAndSync();
				enemy.mainScript.SetEnemyStunned(true, 2f);
			}
			if (enemy.mainScript.enemyType.enemyName == "Earth Leviathan")
			{
				ExplodeAndSync();
			}
			return;
		}

		//VEHICLE COLLISION
		else if (otherObject.transform.parent != null && otherObject.transform.parent.gameObject.layer == 30)
		{
			VehicleController vehicle = otherObject.GetComponentInParent<VehicleController>();
			if (vehicle.averageVelocity.magnitude > 17)
			{
				ExplodeAndSync();
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
		ZapShellAndSync();
	}

	public void ZapShellAndSync()
	{
		if (exploded || !hasBeenSeen)
		{
			return;
		}
		if (!explodeInOrbit)
		{
			if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.timeSinceRoundStarted < 2f)
			{
				return;
			}
		}
		ZapShell();
		ZapShellServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
	}

	[ServerRpc(RequireOwnership = false)]
	public void ZapShellServerRpc(int clientWhoSentRpc)
	{
		ZapShellClientRpc(clientWhoSentRpc);
		StartCoroutine(DelayDetonate(shellZapped.length));
	}

	[ClientRpc]
	public void ZapShellClientRpc(int clientWhoSentRpc)
	{
		if (clientWhoSentRpc != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
		{
			ZapShell();
		}
	}

	public void ZapShell()
	{
		if (exploded)
		{
			return;
		}
		RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
		itemAudio.PlayOneShot(shellZapped);
	}

	public float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return (value - min1) / (max1 - min1) * (max2 - min2) + min2;
    }

}