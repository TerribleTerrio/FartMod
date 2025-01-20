using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Linq;

public class AudioListAnimationEvent : MonoBehaviour
{
	public AudioSource audioSource;

    public AudioSource audioSourceFar;

	public AudioClip[] audioClips;

	public AudioClip[] audioClipsFar;

    public AudioClip[] randomAudioClips;

	public UnityEvent onAnimationEventCalled;

	public UnityEvent onAnimationEvent2Called;

	public UnityEvent onAnimationEvent3Called;

    public UnityEvent TriggerAnimationEvent;

    private int timesChanced = 0;

    public float noiseRange = 35f;

    public float noiseLoudness = 0.8f;
  
    private Vector3 lastPosition;

    private int timesPlayedInOneSpot;

    private float makeNoiseInterval;

    private System.Random seedchance;

  	public void OnAnimationEvent()
	{
		onAnimationEventCalled.Invoke();
	}

  	public void OnAnimationEvent2()
	{
		onAnimationEvent2Called.Invoke();
	}

  	public void OnAnimationEvent3()
	{
		onAnimationEvent3Called.Invoke();
	}

  	public void TriggerAnimationEventChance(int chance)
	{
        seedchance = new System.Random(StartOfRound.Instance.randomMapSeed + timesChanced);
        timesChanced++;
        if (seedchance.Next(0, 100) < chance)
            {
                TriggerAnimationEvent.Invoke();
            }
	}

	public void PlayListAudio(int clipNumber)
	{
        audioSource.PlayOneShot(audioClips[clipNumber]);
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
	}

	public void PlayListAudioLoop(int clipNumber)
	{
        audioSource.clip = audioClips[clipNumber];
        audioSource.loop = true;
        audioSource.Play();
	}

	public void PlayListAudioLoopAndFar(int clipNumber)
	{
        audioSource.clip = audioClips[clipNumber];
        audioSourceFar.clip = audioClipsFar[clipNumber];
        audioSource.loop = true;
        audioSourceFar.loop = true;
        audioSource.Play();
        audioSourceFar.Play();
	}

	public void PlayListAudioNoLoop(int clipNumber)
	{
        audioSource.clip = audioClips[clipNumber];
        audioSource.loop = false;
        audioSource.Play();
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
	}
	public void PlayListAudioNoLoopAndFar(int clipNumber)
	{
        audioSource.clip = audioClips[clipNumber];
        audioSourceFar.clip = audioClipsFar[clipNumber];
        audioSource.loop = false;
        audioSourceFar.loop = false;
        audioSource.Play();
        audioSourceFar.Play();
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
        WalkieTalkie.TransmitOneShotAudio(audioSourceFar, audioClipsFar[clipNumber]);
	}

	public void PlayListAudioAudible(int clipNumber)
	{
        audioSource.PlayOneShot(audioClips[clipNumber]);
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
        MakeAudibleNoise();
	}

	public void PlayListAudioLoopAudible(int clipNumber)
	{
        audioSource.clip = audioClips[clipNumber];
        audioSource.loop = true;
        audioSource.Play();
        StartCoroutine(MakeAudibleNoiseLoop(2f, true));
	}

	public void PlayListAudioNoLoopAudible(int clipNumber)
	{
        audioSource.clip = audioClips[clipNumber];
        audioSource.loop = false;
        audioSource.Play();
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
        MakeAudibleNoise();
	}

	public void PlayListAudioRandom()
	{
        int num = UnityEngine.Random.Range(0, randomAudioClips.Length);
        if (!(randomAudioClips[num] == null))
		{
            audioSource.PlayOneShot(randomAudioClips[num]);
            WalkieTalkie.TransmitOneShotAudio(audioSource, randomAudioClips[num]);
        }
	}

	public void PlayListAudioRandomChance(int chance)
	{
        int num = UnityEngine.Random.Range(0, randomAudioClips.Length);
        if (!(randomAudioClips[num] == null))
		{
            seedchance = new System.Random(StartOfRound.Instance.randomMapSeed);
            if (seedchance.Next(0, 100) < chance)
            {
                audioSource.PlayOneShot(randomAudioClips[num]);
                WalkieTalkie.TransmitOneShotAudio(audioSource, randomAudioClips[num]);
            }
        }
	}

	public void StopListAudio()
	{
		audioSource.Stop();
        StopCoroutine(MakeAudibleNoiseLoop(2f, false));
	}

	public void StopListAudioAndStopLoop()
	{
		audioSource.Stop();
        audioSource.loop = false;
        StopCoroutine(MakeAudibleNoiseLoop(2f, false));
	}

    public void StopListAudioLoopOnly()
    {
        StopCoroutine(MakeAudibleNoiseLoop(2f, false));
    }

    public void SendVoidUpwards(string message)
    {
        SendMessageUpwards(message);
    }

    private System.Collections.IEnumerator MakeAudibleNoiseLoop(float delay, bool makingAudibleNoise)
    {
        if (!makingAudibleNoise)
        {
            yield break;
        }
        if (makeNoiseInterval <= 0f)
        {
            makeNoiseInterval = delay;
            if (Vector3.Distance(lastPosition, audioSource.gameObject.transform.position) > 4f)
            {
                timesPlayedInOneSpot = 0;
            }
            else
            {
                timesPlayedInOneSpot++;
            }
            bool isInsideClosedShip = StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(audioSource.gameObject.transform.position) && StartOfRound.Instance.hangarDoorsClosed;
            lastPosition = audioSource.gameObject.transform.position;
            RoundManager.Instance.PlayAudibleNoise(audioSource.gameObject.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInsideClosedShip);
        }
        else
        {
            makeNoiseInterval -= Time.deltaTime;
        }
    }

    private void MakeAudibleNoise()
    {
        if (Vector3.Distance(lastPosition, audioSource.gameObject.transform.position) > 4f)
        {
            timesPlayedInOneSpot = 0;
        }
        else
        {
            timesPlayedInOneSpot++;
        }
        bool isInsideClosedShip = StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(audioSource.gameObject.transform.position) && StartOfRound.Instance.hangarDoorsClosed;
        lastPosition = audioSource.gameObject.transform.position;
        RoundManager.Instance.PlayAudibleNoise(audioSource.gameObject.transform.position, noiseRange, noiseLoudness, timesPlayedInOneSpot, isInsideClosedShip);
    }
}
