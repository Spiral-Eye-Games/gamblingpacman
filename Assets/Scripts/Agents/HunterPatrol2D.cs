using System;
using System.Collections.Generic;

// Orden de patrulla y reincorporación de un fantasma, separado de su percepción.
internal sealed class HunterPatrol2D
{
    readonly List<int> _nodes = new List<int>();
    readonly int _entryPatrolIndex;
    int _nextPatrolIndex;
    int _returnNodeIndex = -1;
    Action<AIGameManager2D, AStarRoute2D> _plan;
    Action<AIGameManager2D, AStarRoute2D> _resume = delegate { };
    readonly Action<AIGameManager2D, AStarRoute2D> _returnPlan;

    public bool IsIndividual { get; }
    public bool IsReturning { get; private set; }

    public HunterPatrol2D(IReadOnlyList<int> configuredNodes, int nodeCount,
        int entryNode, Action<string> warn)
    {
        IsIndividual = configuredNodes != null && configuredNodes.Count > 0;
        _plan = PlanPatrol;
        _returnPlan = IsIndividual ? PlanIndividualReturn : PlanGlobalReturn;
        if (!IsIndividual)
        {
            for (int i = 0; i < nodeCount; i++)
                _nodes.Add(i);
            return;
        }

        foreach (int node in configuredNodes)
        {
            if ((uint)node < nodeCount)
                _nodes.Add(node);
            else
                warn($"El nodo {node} de la patrulla no existe en AIManager.");
        }

        if (entryNode < 0) return;
        int entry = _nodes.IndexOf(entryNode);
        if (entry >= 0)
            _entryPatrolIndex = entry;
        else
            warn("El nodo de reincorporación debe pertenecer a la patrulla. Se usará el primero.");
    }

    public void Return()
    {
        IsReturning = _nodes.Count > 0;
        _returnNodeIndex = -1;
        _resume = IsReturning ? ResumeReturning : delegate { };
        _plan = IsReturning ? _returnPlan : PlanPatrol;
    }

    public void TryResume(AIGameManager2D manager, AStarRoute2D route) => _resume(manager, route);

    void ResumeReturning(AIGameManager2D manager, AStarRoute2D route)
    {
        bool reachedEntry = IsIndividual ? route.IsComplete :
            _returnNodeIndex >= 0 && route.IsAtPatrolNode(manager.NavigationNodes[_returnNodeIndex]);
        if (!reachedEntry) return;

        IsReturning = false;
        _resume = delegate { };
        _plan = PlanPatrol;
        int entry = IsIndividual ? _entryPatrolIndex : _returnNodeIndex;
        _nextPatrolIndex = (entry + 1) % _nodes.Count;
        route.Clear();
    }

    public void Plan(AIGameManager2D manager, AStarRoute2D route) => _plan(manager, route);

    void PlanPatrol(AIGameManager2D manager, AStarRoute2D route) =>
        route.PlanPatrolNodes(manager, _nodes, ref _nextPatrolIndex, IsIndividual);

    void PlanIndividualReturn(AIGameManager2D manager, AStarRoute2D route) =>
        route.PlanPatrolReturn(manager, _nodes[_entryPatrolIndex]);

    void PlanGlobalReturn(AIGameManager2D manager, AStarRoute2D route)
    {
        if (_returnNodeIndex < 0 ||
            !route.TryPlan(manager, manager.NavigationNodes[_returnNodeIndex]))
            _returnNodeIndex = route.PlanClosestNode(manager);
    }
}
