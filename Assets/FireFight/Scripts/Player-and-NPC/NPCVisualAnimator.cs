using UnityEngine;
using UnityEngine.AI;

/// <summary>Animates the character model using its NPC's actual movement speed.</summary>
[DisallowMultipleComponent]
public sealed class NPCVisualAnimator : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        if (characterAnimator == null)
            characterAnimator = GetComponentInChildren<Animator>();

        if (characterAnimator != null)
            characterAnimator.applyRootMotion = false;
    }

    private void Update()
    {
        if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null)
            return;

        float speed = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh
            ? agent.velocity.magnitude
            : 0f;
        characterAnimator.SetFloat(Speed, speed);
    }
}
