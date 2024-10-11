using System;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CanOpener : GrabbableObject
{
    public string grabItemBoolString;

	public string dropItemTriggerString;

    public string useItemBoolString;

	public bool makeAnimationWhenDropping;

	public Animator itemAnimator;

	public AudioSource itemAudio;

	public AudioClip grabAudio;

	public AudioClip dropAudio;

    public AudioClip flipAudio;

    public AudioClip unflipAudio;

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

	public AudioLowPassFilter itemAudioLowPassFilter;

    private bool isFlipped;

	private bool wasInPocket;

    private AnimatorStateInfo currentStateInfo;

    private float currentAnimationTime;

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
        isFlipped = false;
		if (itemAnimator != null)
		{
            itemAnimator.SetBool(useItemBoolString, value: false);
			itemAnimator.SetBool(grabItemBoolString, value: false);
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
		}
	}

	public override void PocketItem()
	{
		base.PocketItem();
		wasInPocket = true;
        if (itemAnimator != null)
		{
            itemAnimator.SetBool(useItemBoolString, value: false);
            itemAnimator.SetBool(grabItemBoolString, value: false);
		}
	}

    public override void ItemActivate(bool used, bool buttonDown = true)
	{
        base.ItemActivate(used, buttonDown);
        if (!(GameNetworkManager.Instance.localPlayerController == null))
		{
            if (!isFlipped)
		    {
			itemAnimator.SetBool(useItemBoolString, value: true);
			itemAudio.clip = flipAudio;
			itemAudio.Play();
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
            isFlipped = true;
            }
            else if (isFlipped)
            {
			itemAnimator.SetBool(useItemBoolString, value: false);
			itemAudio.clip = unflipAudio;
			itemAudio.Play();
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
            isFlipped = false;
            }
        }
    }

}