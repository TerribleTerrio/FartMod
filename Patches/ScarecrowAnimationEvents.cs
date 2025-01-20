using UnityEngine;
using UnityEngine.Events;

public class ScarecrowAnimationEvents : MonoBehaviour
{
    public UnityEvent[] ScarecrowAnimEvents;

    public void TriggerScarecrowAnimationEvent(int thisEvent)
	{
		ScarecrowAnimEvents[thisEvent].Invoke();
	}
}