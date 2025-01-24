using UnityEngine;

public class ScarecrowHallucinationMissile : MonoBehaviour
{
	private float currentMissileSpeed = 0.35f;

	public ScarecrowHallucination ghostScript;

	private bool hitWall = true;

	private float despawnTimer;

	private System.Random missileFlyRandom;

	private float forwardDistance;

	private float lastRotationDistance;

	private void Start()
	{
		missileFlyRandom = new System.Random((int)(base.transform.position.x + base.transform.position.y) + ghostScript.missilesFired);
		hitWall = false;
	}

	private void FixedUpdate()
	{
		if (hitWall)
		{
			return;
		}
		if (despawnTimer < 5f && ghostScript != null)
		{
			despawnTimer += Time.deltaTime;
			CheckCollision();
			base.transform.position += base.transform.forward * ghostScript.missileSpeed * currentMissileSpeed;
			forwardDistance += ghostScript.missileSpeed * currentMissileSpeed;
			if (forwardDistance - lastRotationDistance > 2f)
			{
				lastRotationDistance = forwardDistance;
				base.transform.rotation *= Quaternion.Euler(new Vector3(15f * ghostScript.missileWarbleLevel * (float)(missileFlyRandom.NextDouble() * 2.0 - 1.0), 7f * ghostScript.missileWarbleLevel * (float)(missileFlyRandom.NextDouble() * 2.0 - 1.0), 15f * ghostScript.missileWarbleLevel * (float)(missileFlyRandom.NextDouble() * 2.0 - 1.0)));
			}
			currentMissileSpeed += 0.05f;
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void CheckCollision()
	{
		if (!Physics.Raycast(base.transform.position, base.transform.forward, out var hitInfo, 0.6f * currentMissileSpeed, 526592, QueryTriggerInteraction.Ignore) && !Physics.Raycast(base.transform.position, base.transform.forward, out hitInfo, 0.6f * currentMissileSpeed, 8, QueryTriggerInteraction.Collide))
		{
			return;
		}
		if (hitInfo.collider.gameObject.layer == 19)
		{
			EnemyAICollisionDetect component = hitInfo.collider.GetComponent<EnemyAICollisionDetect>();
			if (component != null && component.mainScript == ghostScript)
			{
				return;
			}
		}
		hitWall = true;
		ghostScript.StartExplosion(base.transform.position - base.transform.forward * 0.5f, base.transform.forward);
		UnityEngine.Object.Destroy(base.gameObject);
	}
}