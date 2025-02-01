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

        onEnter = itemType.GetMethod(callOnTriggerEnter);
        onEnterParameters = onEnter.GetParameters();

        onExit = itemType.GetMethod(callOnTriggerExit);
        onExitParameters = onExit.GetParameters();

        onHit = itemType.GetMethod(callOnHit);
        onHitParameters = onHit.GetParameters();
    }
    
    public void OnTriggerEnter(Collider other)
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

    public void OnTriggerExit(Collider other)
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

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        onHit.Invoke(objectScript, null);
        return false;
    }
}