using UnityEngine;

public class HydraulicStabilizerBehaviour : StateMachineBehaviour
{
    public string boolOnEnter;

    public string boolOnExit;

    public bool trueOnEnter;

    public bool trueOnExit;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(boolOnEnter, trueOnEnter);
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(boolOnExit, trueOnExit);
    }
}