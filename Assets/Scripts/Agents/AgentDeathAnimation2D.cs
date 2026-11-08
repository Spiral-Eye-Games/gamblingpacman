using System.Collections;
using UnityEngine;

// Espera al estado de muerte real del Animator, respetando la duración del clip.
internal static class AgentDeathAnimation2D
{
    static readonly int DieParameter = Animator.StringToHash("Die");

    public static IEnumerator Play(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            yield break;

        animator.speed = 1f;
        animator.SetTrigger(DieParameter);
        while (true)
        {
            yield return null;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsTag("Death") && state.normalizedTime >= 1f && !animator.IsInTransition(0))
                yield break;
        }
    }
}
