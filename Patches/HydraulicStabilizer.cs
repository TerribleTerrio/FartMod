using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Mathematics;
using UnityEngine;

public class HydraulicStabilizer : GrabbableObject, IHittable, ITouchable
{

	public string grabItemBoolString;

	public string dropItemTriggerString;

	public bool makeAnimationWhenDropping;

	public Animator itemAnimator;

	public AudioSource itemAudio;

	public AudioClip grabAudio;

	public AudioClip dropAudio;

	public bool loopGrabAudio;

	public bool loopDropAudio;

	[Range(0f, 100f)]
	public int chanceToTriggerAnimation = 100;

	public int chanceToTriggerAlternateMesh;

	public Mesh alternateMesh;

	private Mesh normalMesh;

	private System.Random itemRandomChance;

	public float noiseRange;

	public float noiseLoudness;

	private int timesPlayedInOneSpot;

	private float makeNoiseInterval;

	private Vector3 lastPosition;

	public AudioLowPassFilter itemAudioLowPassFilter;

	private bool wasInPocket;

    [Header("Hydraulic Stabilizer Settings")]

    public float audibleNoiseCooldown = 2f;

	public string HitItemBoolString;

    private bool noiseOnCooldown;

    private bool hitOnCooldown;


	public override void Start()
	{
		base.Start();
		itemRandomChance = new System.Random(StartOfRound.Instance.randomMapSeed + StartOfRound.Instance.currentLevelID + itemProperties.itemId);
		if (chanceToTriggerAlternateMesh > 0)
		{
			normalMesh = base.gameObject.GetComponent<MeshFilter>().mesh;
		}
	}

	public override void EquipItem()
	{
		base.EquipItem();
		if (itemAudioLowPassFilter != null)
		{
			itemAudioLowPassFilter.cutoffFrequency = 20000f;
		}
		itemAudio.volume = 1f;
		if (chanceToTriggerAlternateMesh > 0)
		{
			if (itemRandomChance.Next(0, 100) < chanceToTriggerAlternateMesh)
			{
				base.gameObject.GetComponent<MeshFilter>().mesh = alternateMesh;
				itemAudio.Stop();
				return;
			}
			base.gameObject.GetComponent<MeshFilter>().mesh = normalMesh;
		}
		if (!wasInPocket)
		{
			if (itemRandomChance.Next(0, 100) > chanceToTriggerAnimation)
			{
				itemAudio.Stop();
				return;
			}
		}
		else
		{
			wasInPocket = false;
		}
		if (itemAnimator != null)
		{
			itemAnimator.SetBool(grabItemBoolString, value: true);
            // Debug.Log("GrabItemBoolString set to true!");
		}
		if (itemAudio != null)
		{
			itemAudio.clip = grabAudio;
			itemAudio.loop = loopGrabAudio;
			itemAudio.Play();
		}
	}

	public override void DiscardItem()
	{
		base.DiscardItem();
		if (itemAnimator != null)
		{
			itemAnimator.SetBool(grabItemBoolString, value: false);
            // Debug.Log("GrabItemBoolString set to false!");
		}
		if (chanceToTriggerAlternateMesh > 0)
		{
			base.gameObject.GetComponent<MeshFilter>().mesh = normalMesh;
		}
		if (!makeAnimationWhenDropping)
		{
			itemAudio.Stop();
			return;
		}
		if (itemRandomChance.Next(0, 100) < chanceToTriggerAnimation)
		{
			itemAudio.Stop();
			return;
		}
		if (itemAnimator != null)
		{
			itemAnimator.SetTrigger(dropItemTriggerString);
		}
		if (itemAudio != null)
		{
			itemAudio.loop = loopDropAudio;
			itemAudio.clip = dropAudio;
			itemAudio.Play();
			if (itemAudioLowPassFilter != null)
			{
				itemAudioLowPassFilter.cutoffFrequency = 20000f;
			}
			itemAudio.volume = 1f;
		}
	}

	public override void PocketItem()
	{
		base.PocketItem();
		wasInPocket = true;
		if (itemAudio != null)
		{
			if (itemAudioLowPassFilter != null)
			{
				itemAudioLowPassFilter.cutoffFrequency = 1700f;
			}
			itemAudio.volume = 0.5f;
		}
	}

    public override void Update()
    {
        base.Update();
        if (itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("HydraulicSteaming") || itemAnimator.GetCurrentAnimatorStateInfo(0).IsName("HydraulicLoop"))
        {
            if (!noiseOnCooldown)
            {
                StartCoroutine(LoopNoiseOnCooldown(audibleNoiseCooldown));
            }
        }
    }

    private IEnumerator LoopNoiseOnCooldown(float delay)
    {
        noiseOnCooldown = true;

        if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        lastPosition = base.transform.position;

        RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);

        yield return new WaitForSeconds(delay);

        noiseOnCooldown = false;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
        GoPsycho();
        return true;
    }

    public void GoPsycho()
    {
		if (heldByPlayerOnServer)
		{
			playerHeldBy.DropItemAheadOfPlayer();
		}
        if (!hitOnCooldown)
        {
            hitOnCooldown = true;
            itemAnimator.SetBool(HitItemBoolString, value: true);
            // Debug.Log("HitItemBoolString set to true!");

            // StartCoroutine(HitCooldown(itemAnimator.GetCurrentAnimatorStateInfo(0).length));
            StartCoroutine(HitCooldown(4.83f));

            if (Vector3.Distance(lastPosition, base.transform.position) > 2f)
            {
                timesPlayedInOneSpot = 0;
            }
            timesPlayedInOneSpot++;
            lastPosition = base.transform.position;
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
        }
    }

    private IEnumerator HitCooldown(float delay)
    {
        yield return new WaitForSeconds(delay);
        hitOnCooldown = false;
        itemAnimator.SetBool(HitItemBoolString, value: false);
        // Debug.Log("HitItemBoolString set to false!");
    }

	public void OnTouch(Collider other)
	{
        GameObject otherObject = other.gameObject;

		if (otherObject.layer == 6)
		{
			Debug.Log("Detected collider on prop layer.");
            Debug.Log("Other object: " + otherObject.name);
			if (otherObject.name.StartsWith("explosionColliderDamage"))
			{
				Debug.Log("Detected explosion collider.");
                GoPsycho();
			}
		}
    }

	public void OnExit(Collider other)
	{
	}
}