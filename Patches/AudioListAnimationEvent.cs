using UnityEngine;
using UnityEngine.Events;
using System;

public class AudioListAnimationEvent : MonoBehaviour
{
	public AudioSource audioSource;

	public AudioClip[] audioClips;

    public AudioClip[] randomAudioClips;


	public UnityEvent onAnimationEventCalled;

    private Vector3 lastPosition;

    private int timesPlayedInOneSpot;

    private bool noiseOnCooldown;

  	public void OnAnimationEvent()
	{
		onAnimationEventCalled.Invoke();
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
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
	}

	public void PlayListAudioNoLoop(int clipNumber)
	{
        audioSource.clip = audioClips[clipNumber];
        audioSource.loop = false;
        audioSource.Play();
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
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
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
        StartCoroutine(MakeAudibleNoiseLoop(2f));
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
        Debug.Log("PlayListAudioRandom called, choosing a sound.");
        int num = UnityEngine.Random.Range(0, randomAudioClips.Length);
        if (!(randomAudioClips[num] == null))
		{
            Debug.Log("Chose a sound, playing clip: " + num + " from audio source: " + audioSource.name);
            audioSource.PlayOneShot(randomAudioClips[num]);
            WalkieTalkie.TransmitOneShotAudio(audioSource, randomAudioClips[num]);
        }
	}

	public void PlayListAudioRandomChance(int chance)
	{
        Debug.Log("PlayListAudioRandomChance called, choosing a sound.");
        int num = UnityEngine.Random.Range(0, randomAudioClips.Length);
        if (!(randomAudioClips[num] == null))
		{
            Debug.Log("Chose a sound, trying chance.");
            if (UnityEngine.Random.Range(0, 100) < chance)
            {
                Debug.Log("Chance succeeded, playing clip: " + num + " from audio source: " + audioSource.name);
                audioSource.PlayOneShot(randomAudioClips[num]);
                WalkieTalkie.TransmitOneShotAudio(audioSource, randomAudioClips[num]);
            }
        }
	}

	public void StopListAudio()
	{
		audioSource.Stop();
        StopCoroutine(MakeAudibleNoiseLoop(2f));
	}

	public void StopListAudioAndStopLoop()
	{
		audioSource.Stop();
        audioSource.loop = false;
        StopCoroutine(MakeAudibleNoiseLoop(2f));
	}

    public void StopListAudioLoopOnly()
    {
        StopCoroutine(MakeAudibleNoiseLoop(2f));
    }

    private System.Collections.IEnumerator MakeAudibleNoiseLoop(float delay)
    {
        noiseOnCooldown = true;

        if (Vector3.Distance(lastPosition, audioSource.gameObject.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        bool isInsideClosedShip = StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(audioSource.gameObject.transform.position) && StartOfRound.Instance.hangarDoorsClosed;
        lastPosition = audioSource.gameObject.transform.position;
        RoundManager.Instance.PlayAudibleNoise(audioSource.gameObject.transform.position, 20f, 0.55f, timesPlayedInOneSpot, isInsideClosedShip);

        yield return new WaitForSeconds(delay);

        noiseOnCooldown = false;
    }

    private void MakeAudibleNoise()
    {
        if (Vector3.Distance(lastPosition, audioSource.gameObject.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        bool isInsideClosedShip = StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(audioSource.gameObject.transform.position) && StartOfRound.Instance.hangarDoorsClosed;
        lastPosition = audioSource.gameObject.transform.position;
        RoundManager.Instance.PlayAudibleNoise(audioSource.gameObject.transform.position, 20f, 0.55f, timesPlayedInOneSpot, isInsideClosedShip);
    }
}
