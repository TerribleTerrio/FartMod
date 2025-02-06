using System;
using System.Reflection;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class TriggerScript : MonoBehaviour, IHittable
{
    public NetworkBehaviour objectScript;

    public string? callOnTriggerEnter;

    public string? callOnTriggerExit;

    public string? callOnHit;

    private MethodInfo? onEnter;

    private ParameterInfo[]? onEnterParameters;

    private MethodInfo? onExit;

    private ParameterInfo[]? onExitParameters;

    private MethodInfo? onHit;

    private ParameterInfo[]? onHitParameters;

    public void Start()
    {
        Type itemType = objectScript.GetType();

        if (!string.IsNullOrEmpty(callOnTriggerEnter))
        {
            onEnter = itemType.GetMethod(callOnTriggerEnter);
            if (onEnter != null)
            {
                onEnterParameters = onEnter.GetParameters();
            }
        }

        if (!string.IsNullOrEmpty(callOnTriggerExit))
        {
            onExit = itemType.GetMethod(callOnTriggerExit);
            if (onExit != null)
            {
                onExitParameters = onExit.GetParameters();
            }
        }

        if (!string.IsNullOrEmpty(callOnHit))
        {
            onHit = itemType.GetMethod(callOnHit);
            if (onHit != null)
            {
                onHitParameters = onHit.GetParameters();
            }
        }
    }
    
    public void OnTriggerEnter(Collider other)
    {
        if (onEnter != null && !string.IsNullOrEmpty(callOnTriggerEnter))
        {
            if (onEnterParameters is null || onEnterParameters.Length == 0)
            {
                onEnter.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = [other];
                onEnter.Invoke(objectScript, parametersArray);
            }
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (onExit != null && !string.IsNullOrEmpty(callOnTriggerExit))
        {
            if (onExitParameters is null || onExitParameters.Length == 0)
            {
                onExit.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = [other];
                onExit.Invoke(objectScript, parametersArray);
            }
        }
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (onHit != null && !string.IsNullOrEmpty(callOnHit))
        {
            if (onHitParameters is null || onHitParameters.Length == 0)
            {
                onHit.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = [force, hitDirection, playerWhoHit = null, playHitSFX = false, hitID = -1];
                onHit.Invoke(objectScript, parametersArray);
            }
        }
        return false;
    }
}