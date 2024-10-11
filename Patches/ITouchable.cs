using GameNetcodeStuff;
using UnityEngine;

public interface ITouchable
{
    void OnTouch(Collider other);
    void OnExit(Collider other);
}