using UnityEngine;

public class BoidActionNode : BoidNode
{
    [SerializeField] BoidAction _action;

    public override void Execute(BoidAgent2D boid)
    {
        boid.PerformAction(_action);
    }
}
