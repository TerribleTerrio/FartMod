using UnityEngine;

public class Pumpkin : AnimatedItem
{
    [Header("Pumpkin Settings")]
    public float rotAmount;

    public override void Start()
    {
        base.Start();
        itemAnimator.SetFloat("rot", rotAmount);
    }

}