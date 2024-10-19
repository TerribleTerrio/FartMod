using System;
using System.Reflection;
using UnityEngine;

public class ItemTrigger : MonoBehaviour
{
    public GrabbableObject objectScript;

    public String callOnTriggerEnter;

    public String callOnTriggerExit;

    private MethodInfo onEnter;

    private ParameterInfo[] onEnterParameters;

    private MethodInfo onExit;

    private ParameterInfo[] onExitParameters;

    public void Start()
    {
        // itemComponent = parentObject.GetComponent<ITouchable>();
        Type itemType = objectScript.GetType();

        onEnter = itemType.GetMethod(callOnTriggerEnter);
        onEnterParameters = onEnter.GetParameters();

        onExit = itemType.GetMethod(callOnTriggerExit);
        onExitParameters = onExit.GetParameters();
    }
    
    public void OnTriggerEnter(Collider other)
    {
        // itemComponent.OnTouch(other);
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
        // itemComponent.OnExit(other);
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