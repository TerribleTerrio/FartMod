using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class StaticGenerator : NetworkBehaviour
{
	public bool isPowerOn;

	public RoundManager roundManager;

	public OnSwitchPowerEvent powerSwitchEvent;

	public OnSwitchPowerEvent permanentPowerSwitchEvent;

	public void Start()
	{
		roundManager = Object.FindObjectOfType<RoundManager>();
		roundManager.onPowerSwitch.AddListener(OnPowerSwitch);
	}

	public void OnPowerSwitch(bool switchedOn)
	{
		// bool powerOffPermanently = roundManager.powerOffPermanently;
		// if (powerOffPermanently)
		// {
		// 	permanentPowerSwitchEvent.Invoke(switchedOn);
		// 	Debug.Log("Generator: Permanent Power Switch event invoked!");
		// }
		// else
		// {
		// 	powerSwitchEvent.Invoke(switchedOn);
		// 	Debug.Log("Generator: Power Switch event invoked!");
		// }
		StartCoroutine(WaitForPermanentCheck());
        isPowerOn = switchedOn;
	}

	private IEnumerator WaitForPermanentCheck()
	{
		yield return new WaitForSeconds(1f);
		bool powerOffPermanently = roundManager.powerOffPermanently;
		if (powerOffPermanently)
		{
			permanentPowerSwitchEvent.Invoke(isPowerOn);
			Debug.Log("Generator: Permanent Power Switch event invoked!");
			yield break;
		}
		else
		{
			powerSwitchEvent.Invoke(isPowerOn);
			Debug.Log("Generator: Power Switch event invoked!");
			yield break;
		}
	}
	// public void OnEnable()
	// {
	// 	roundManager.onPowerSwitch.AddListener(OnPowerSwitch);
	// }

	// public void OnDisable()
	// {
	// 	roundManager.onPowerSwitch.RemoveListener(OnPowerSwitch);
	// }

    public void SwitchPower(bool isPowerOn)
	{
		if (roundManager == null || roundManager.powerOffPermanently)
		{
			return;
		}
		if (base.IsServer)
		{
			if (!isPowerOn)
			{
				roundManager.SwitchPower(on: true);
			}
			else if (isPowerOn)
			{
				roundManager.SwitchPower(on: false);
			}
		}
	}
}
