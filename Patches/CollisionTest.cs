using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class CollisionTest : MonoBehaviour
{

    public GameObject gameObject;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"{gameObject} touched by player.");
        }

        else if (other.CompareTag("Enemy"))
        {
            Debug.Log($"{gameObject} touched by enemy.");
        }
    }
}