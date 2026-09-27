using UnityEngine;

public class BoidQuestionNode : BoidNode
{
    public enum Question {FoodNearby, HunterNearby, NeighborsNearby}

    [SerializeField] Question _question;
    [SerializeField] BoidNode _trueNode;
    [SerializeField] BoidNode _falseNode;

    public override void Execute(BoidAgent2D boid)
    {
        bool answer = false;
        switch (_question)
        {
            case Question.FoodNearby:
                answer = boid.FoodNearby;
                break;
            case Question.HunterNearby:
                answer = boid.HunterNearby;
                break;
            case Question.NeighborsNearby:
                answer = boid.NeighborsNearby;
                break;
        }

        BoidNode next = answer ? _trueNode : _falseNode;
        if (next != null)
        {
            next.Execute(boid);
            return;
        }

        Debug.LogError("Falta conectar una rama del árbol de decisión.", this);
    }
}
