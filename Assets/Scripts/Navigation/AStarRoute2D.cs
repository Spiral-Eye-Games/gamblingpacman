using System.Collections.Generic;
using UnityEngine;

// Cada agente guarda su propia ruta, pero todos usan el mismo grafo de la escena.
internal sealed class AStarRoute2D
{
    const float ReplanInterval = 0.25f;
    const float ArrivalDistanceSqr = 0.0001f;
    internal const float PatrolArrivalSqr = 0.0225f;

    readonly IAgentMotor _motor;
    readonly LayerMask _walls;
    readonly float _radius;
    List<Vector2> _path = new List<Vector2>();
    List<Vector2> _candidate = new List<Vector2>();
    int _next;
    float _replanAt;

    public bool CanReplan => Time.time >= _replanAt;

    public AStarRoute2D(IAgentMotor motor, LayerMask walls, float radius)
    {
        _motor = motor;
        _walls = walls;
        _radius = radius;
    }

    public void Clear()
    {
        _path.Clear();
        _next = 0;
        _replanAt = 0f;
    }

    public void BeginPlan()
    {
        Clear();
        _replanAt = Time.time + ReplanInterval;
    }

    public bool TryPlan(AIGameManager2D manager, Vector2 destination)
    {
        if (!AStarPathfinding2D.TryFindPath(manager.ArenaBounds, manager.NavigationNodes,
            _motor.Position, destination, _walls, _radius, _candidate))
            return false;

        List<Vector2> oldPath = _path;
        _path = _candidate;
        _candidate = oldPath;
        _next = 0;
        return true;
    }

    public void PlanPatrolNodes(AIGameManager2D manager, ref int index)
    {
        IReadOnlyList<Vector2> nodes = manager.NavigationNodes;
        for (int attempt = 0; attempt < nodes.Count; attempt++)
        {
            Vector2 point = nodes[index];
            if ((point - _motor.Position).sqrMagnitude > PatrolArrivalSqr &&
                TryPlan(manager, point))
                return;

            index = (index + 1) % nodes.Count;
        }
    }

    public void Move()
    {
        while (_next < _path.Count &&
               (_path[_next] - _motor.Position).sqrMagnitude <= ArrivalDistanceSqr)
            _next++;

        if (_next == _path.Count)
        {
            _motor.Stop();
            return;
        }

        if (!_motor.MoveToWaypoint(_path[_next]))
            Clear();
    }

    public void Draw(Color color)
    {
        Gizmos.color = color;
        Vector2 previous = _motor.Position;
        for (int i = _next; i < _path.Count; i++)
        {
            Gizmos.DrawLine(previous, _path[i]);
            previous = _path[i];
        }
    }
}
