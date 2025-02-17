using System;
using System.Reflection;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class TriggerScript : MonoBehaviour, IHittable
{
    public NetworkBehaviour objectScript;

    public string? callOnTriggerEnter;

    public string? callOnTriggerStay;

    public string? callOnTriggerExit;

    public string? callOnCollisionEnter;

    public string? callOnCollisionExit;

    public string? callOnHit;

    private MethodInfo? onEnter;

    private ParameterInfo[]? onEnterParameters;

    private MethodInfo? onStay;

    private ParameterInfo[]? onStayParameters;

    private MethodInfo? onExit;

    private ParameterInfo[]? onExitParameters;

    private MethodInfo? onColEnter;

    private ParameterInfo[]? onColEnterParameters;

    private MethodInfo? onColExit;

    private ParameterInfo[]? onColExitParameters;

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

        if (!string.IsNullOrEmpty(callOnTriggerStay))
        {
            onStay = itemType.GetMethod(callOnTriggerStay);
            if (onStay != null)
            {
                onStayParameters = onStay.GetParameters();
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

        if (!string.IsNullOrEmpty(callOnCollisionEnter))
        {
            onColEnter = itemType.GetMethod(callOnCollisionEnter);
            if (onColEnter != null)
            {
                onColEnterParameters = onColEnter.GetParameters();
            }
        }

        if (!string.IsNullOrEmpty(callOnCollisionExit))
        {
            onColExit = itemType.GetMethod(callOnCollisionExit);
            if (onColExit != null)
            {
                onColExitParameters = onColExit.GetParameters();
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
    
    public void OnTriggerStay(Collider other)
    {
        if (onStay != null && !string.IsNullOrEmpty(callOnTriggerStay))
        {
            if (onStayParameters is null || onStayParameters.Length == 0)
            {
                onStay.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = [other];
                onStay.Invoke(objectScript, parametersArray);
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

    public void OnCollisionEnter(Collision other)
    {
        if (onColEnter != null && !string.IsNullOrEmpty(callOnCollisionEnter))
        {
            if (onColEnterParameters is null || onColEnterParameters.Length == 0)
            {
                onColEnter.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = [other];
                onColEnter.Invoke(objectScript, parametersArray);
            }
        }
    }

    public void OnCollisionExit(Collision other)
    {
        if (onColExit != null && !string.IsNullOrEmpty(callOnCollisionExit))
        {
            if (onColExitParameters is null || onColExitParameters.Length == 0)
            {
                onColExit.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = [other];
                onColExit.Invoke(objectScript, parametersArray);
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