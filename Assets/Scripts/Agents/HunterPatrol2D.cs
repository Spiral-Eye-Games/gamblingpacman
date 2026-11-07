using System;
using System.Collections.Generic;

// Orden de patrulla y reincorporación de un fantasma, separado de su percepción.
internal sealed class HunterPatrol2D
{
    readonly List<int> _nodes = new List<int>();
    readonly int _entryPatrolIndex;
    int _nextPatrolIndex;
    int _returnNodeIndex = -1;

    public bool IsIndividual { get; }
    public bool IsReturning { get; private set; }

    public HunterPatrol2D(IReadOnlyList<int> configuredNodes, int nodeCount,
        int entryNode, Action<string> warn)
    {
        IsIndividual = configuredNodes != null && configuredNodes.Count > 0;
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
    }

    public void TryResume(AIGameManager2D manager, AStarRoute2D route)
    {
        if (!IsReturning) return;

        bool reachedEntry = IsIndividual ? route.IsComplete :
            _returnNodeIndex >= 0 && route.IsAtPatrolNode(manager.NavigationNodes[_returnNodeIndex]);
        if (!reachedEntry) return;

        IsReturning = false;
        int entry = IsIndividual ? _entryPatrolIndex : _returnNodeIndex;
        _nextPatrolIndex = (entry + 1) % _nodes.Count;
        route.Clear();
    }

    public void Plan(AIGameManager2D manager, AStarRoute2D route)
    {
        if (!IsReturning)
        {
            route.PlanPatrolNodes(manager, _nodes, ref _nextPatrolIndex, IsIndividual);
            return;
        }

        if (IsIndividual)
            route.PlanPatrolReturn(manager, _nodes[_entryPatrolIndex]);
        else if (_returnNodeIndex < 0 ||
            !route.TryPlan(manager, manager.NavigationNodes[_returnNodeIndex]))
            _returnNodeIndex = route.PlanClosestNode(manager);
    }
}
