using System;
using System.Reflection;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class TriggerScript : MonoBehaviour, IHittable
{
    public NetworkBehaviour objectScript;

    public String callOnTriggerEnter;

    public String callOnTriggerExit;

    public String callOnHit;

    private MethodInfo onEnter;

    private ParameterInfo[] onEnterParameters;

    private MethodInfo onExit;

    private ParameterInfo[] onExitParameters;

    private MethodInfo onHit;

    private ParameterInfo[] onHitParameters;

    public void Start()
    {
        Type itemType = objectScript.GetType();

        if (!string.IsNullOrEmpty(callOnTriggerEnter))
        {
            onEnter = itemType.GetMethod(callOnTriggerEnter);
            onEnterParameters = onEnter.GetParameters();
        }

        if (!string.IsNullOrEmpty(callOnTriggerExit))
        {
            onExit = itemType.GetMethod(callOnTriggerExit);
            onExitParameters = onExit.GetParameters();
        }

        if (!string.IsNullOrEmpty(callOnHit))
        {
            onHit = itemType.GetMethod(callOnHit);
            onHitParameters = onHit.GetParameters();
        }
    }
    
    public void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(callOnTriggerEnter))
        {
            if (onEnterParameters.Length == 0)
            {
                onEnter.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = new object[1];
                parametersArray[0] = other;
                onEnter.Invoke(objectScript, parametersArray);
            }
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (!string.IsNullOrEmpty(callOnTriggerExit))
        {
            if (onExitParameters.Length == 0)
            {
                onExit.Invoke(objectScript, null);
            }
            else
            {
                object[] parametersArray = new object[1];
                parametersArray[0] = other;
                onExit.Invoke(objectScript, parametersArray);
            }
        }
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (!string.IsNullOrEmpty(callOnHit))
        {
            onHit.Invoke(objectScript, null);
        }
        return false;
    }
}