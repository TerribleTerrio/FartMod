using UnityEngine;

public class TireReferenceScript : MonoBehaviour
{
    public Tire mainScript;

    private void Update()
    {
        if (mainScript == null)
        {
            Destroy(base.transform.parent.gameObject);
        }
    }
}