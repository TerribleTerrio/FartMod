using UnityEngine;

public class BalloonCollisionDetection : MonoBehaviour
{
    public Balloon mainScript;

    private void OnCollisionEnter(Collision collision)
    {
        Collider other = collision.collider;
        if (other.gameObject.layer == 11 && other.gameObject.tag == "Aluminum")
        {
            if (other.gameObject.transform.parent != null)
            {
                if (other.gameObject.transform.parent.parent != null)
                {
                    if (other.gameObject.transform.parent.parent.gameObject.GetComponentInChildren<SpikeRoofTrap>() != null)
                    {
                        mainScript.Pop();
                    }
                }
            }
        }
    }
}