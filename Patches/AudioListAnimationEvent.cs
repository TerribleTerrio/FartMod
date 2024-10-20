using UnityEngine;
using UnityEngine.Events;
using System;

public class AudioListAnimationEvent : MonoBehaviour
{
	public AudioSource audioSource;

	public AudioClip[] audioClips;


	public UnityEvent onAnimationEventCalled;

    private Vector3 lastPosition;

    private int timesPlayedInOneSpot;

	public void OnAnimationEvent()
	{
		onAnimationEventCalled.Invoke();
	}

	public void PlayAudio(int clipNumber)
	{
        audioSource.PlayOneShot(audioClips[clipNumber]);
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
	}

	public void PlayAudioAudible(int clipNumber)
	{
        audioSource.PlayOneShot(audioClips[clipNumber]);
        WalkieTalkie.TransmitOneShotAudio(audioSource, audioClips[clipNumber]);
        if (Vector3.Distance(lastPosition, audioSource.gameObject.transform.position) > 2f)
        {
            timesPlayedInOneSpot = 0;
        }
        timesPlayedInOneSpot++;
        bool isInsideClosedShip = StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(audioSource.gameObject.transform.position) && StartOfRound.Instance.hangarDoorsClosed;
        lastPosition = audioSource.gameObject.transform.position;
        RoundManager.Instance.PlayAudibleNoise(audioSource.gameObject.transform.position, 10f, 0.55f, timesPlayedInOneSpot, isInsideClosedShip);
	}

	public void StopAudio()
	{
		audioSource.Stop();
	}

}
