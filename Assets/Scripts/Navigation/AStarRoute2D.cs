using System.Collections.Generic;
using UnityEngine;

// Cada agente guarda su propia ruta, pero todos usan el mismo grafo de la escena.
internal sealed class AStarRoute2D
{
    const float ReplanInterval = 0.25f;
    const float ArrivalDistanceSqr = 0.0001f;
    const float PatrolArrivalSqr = 0.0225f;
    const float ChaseWallClearance = 0.01f;

    readonly IAgentMotor _motor;
    readonly LayerMask _walls;
    readonly float _radius;
    List<Vector2> _path = new List<Vector2>();
    List<Vector2> _candidatePath = new List<Vector2>();
    readonly List<int> _linkedNodes = new List<int>();
    readonly List<int> _candidateLinkedNodes = new List<int>();
    int _next;
    int _firstRequiredWaypoint = -1;
    int _lastLinkedNode = -1;
    float _replanAt;

    public bool CanReplan => Time.time >= _replanAt;
    public bool IsComplete => _path.Count > 0 && _next == _path.Count;
    public bool NeedsRoute => _path.Count == 0 || IsComplete;

    public AStarRoute2D(IAgentMotor motor, LayerMask walls, float radius)
    {
        _motor = motor;
        _walls = walls;
        _radius = radius;
    }

    public void Clear()
    {
        ResetPath();
        _lastLinkedNode = -1;
        _replanAt = 0f;
    }

    void ResetPath()
    {
        _path.Clear();
        _linkedNodes.Clear();
        _next = 0;
        _firstRequiredWaypoint = -1;
    }

    public void BeginPlan()
    {
        ResetPath();
        _replanAt = Time.time + ReplanInterval;
    }

    public bool TryPlan(AIGameManager2D manager, Vector2 destination)
    {
        if (!AStarPathfinding2D.TryFindPath(manager.ArenaBounds, manager.NavigationNodes,
            _motor.Position, destination, _walls, _radius, _candidatePath))
            return false;

        UseCandidate();
        return true;
    }

    public void PlanChase(AIGameManager2D manager, Vector2 destination)
    {
        // El jugador puede caber más cerca de la pared que el Hunter.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Collider2D wall = Physics2D.OverlapCircle(destination, _radius, _walls);
            if (wall == null) break;

            Vector2 closest = wall.ClosestPoint(destination);
            Vector2 away = destination - closest;
            if (away.sqrMagnitude == 0f) return;
            destination = closest + away.normalized * (_radius + ChaseWallClearance);
        }

        TryPlan(manager, destination);
    }

    void UseCandidate()
    {
        (_path, _candidatePath) = (_candidatePath, _path);
        _next = 0;
    }

    public void PlanPatrolNodes(AIGameManager2D manager, IReadOnlyList<int> patrol,
        ref int index, bool individual)
    {
        for (int attempt = 0; attempt < patrol.Count; attempt++)
        {
            Vector2 point = manager.NavigationNodes[patrol[index]];
            bool arrived = individual ? point.Equals(_motor.Position) : IsAtPatrolNode(point);
            if (!arrived && TryPlan(manager, point))
            {
                if (individual) _firstRequiredWaypoint = _path.Count - 1;
                return;
            }

            if (!arrived && individual) return;
            index = (index + 1) % patrol.Count;
        }
    }

    public bool IsAtPatrolNode(Vector2 point)
    {
        return (point - _motor.Position).sqrMagnitude <= PatrolArrivalSqr;
    }

    public int PlanClosestNode(AIGameManager2D manager)
    {
        IReadOnlyList<Vector2> nodes = manager.NavigationNodes;
        Vector2 position = _motor.Position;
        float nearestDistanceSqr = float.PositiveInfinity;
        int nearest = -1;
        for (int i = 0; i < nodes.Count; i++)
        {
            float distanceSqr = (nodes[i] - position).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr || !TryPlan(manager, nodes[i])) continue;

            nearestDistanceSqr = distanceSqr;
            nearest = i;
        }
        return nearest;
    }

    public void PlanFlee(AIGameManager2D manager, Vector2 threat, float viewRadius)
    {
        int anchor = PlanClosestNode(manager);
        if (anchor < 0) return;

        IReadOnlyList<Vector2> nodes = manager.NavigationNodes;
        // Con heurística cero, la PriorityQueue encuentra el refugio de menor costo por los enlaces.
        if (AStarPathfinding2D.TryFindLinkedPath(manager.ArenaBounds, nodes,
            manager.NavigationNeighbors, anchor,
            node => manager.FindNearestBoid(nodes[node], viewRadius, _walls) == null,
            null, _walls, _radius, _candidateLinkedNodes))
        {
            AppendLinkedPath(nodes);
            return;
        }

        // Si todos los nodos siguen a la vista, avanzá hacia el vecino seguro que más se aleje.
        int next = anchor;
        float bestDistanceSqr = (nodes[anchor] - threat).sqrMagnitude;
        foreach (int neighbor in manager.NavigationNeighbors[anchor])
        {
            float distanceSqr = (nodes[neighbor] - threat).sqrMagnitude;
            if (distanceSqr <= bestDistanceSqr || !TryLinkedPath(manager, anchor, neighbor))
                continue;

            next = neighbor;
            bestDistanceSqr = distanceSqr;
        }

        if (TryLinkedPath(manager, anchor, next))
            AppendLinkedPath(nodes);
    }

    public void PlanPatrolReturn(AIGameManager2D manager, int destination)
    {
        IReadOnlyList<Vector2> nodes = manager.NavigationNodes;
        Vector2 position = _motor.Position;

        // Si se bloqueó un enlace durante el regreso, retomá desde el último nodo visitado.
        if (_lastLinkedNode >= 0)
        {
            Vector2 anchor = nodes[_lastLinkedNode];
            if (AStarPathfinding2D.IsWalkable(position, manager.ArenaBounds, _walls, _radius) &&
                AStarPathfinding2D.SegmentIsClear(position, anchor, _radius, _walls) &&
                TryLinkedPath(manager, _lastLinkedNode, destination))
            {
                _candidatePath.Clear();
                _candidatePath.Add(anchor);
                UseCandidate();
                AppendLinkedPath(nodes);
            }
            return;
        }

        float nearestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < nodes.Count; i++)
        {
            float distanceSqr = (nodes[i] - position).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr || !TryLinkedPath(manager, i, destination) ||
                !TryPlan(manager, nodes[i]))
                continue;

            nearestDistanceSqr = distanceSqr;
            AppendLinkedPath(nodes);
        }
    }

    bool TryLinkedPath(AIGameManager2D manager, int from, int destination) =>
        AStarPathfinding2D.TryFindLinkedPath(manager.ArenaBounds, manager.NavigationNodes,
            manager.NavigationNeighbors, from, destination, _walls, _radius, _candidateLinkedNodes);

    void AppendLinkedPath(IReadOnlyList<Vector2> nodes)
    {
        _firstRequiredWaypoint = _path.Count - 1; // El tramo de aproximación ya termina en el ancla.
        _linkedNodes.Clear();
        _linkedNodes.AddRange(_candidateLinkedNodes);
        for (int i = 1; i < _linkedNodes.Count; i++)
            _path.Add(nodes[_linkedNodes[i]]);
    }

    public void Move()
    {
        Vector2 position = _motor.Position;
        while (_next < _path.Count)
        {
            Vector2 waypoint = _path[_next];
            if (_firstRequiredWaypoint >= 0 && _next >= _firstRequiredWaypoint)
            {
                if (!position.Equals(waypoint)) break;
                if (_linkedNodes.Count > 0)
                    _lastLinkedNode = _linkedNodes[_next - _firstRequiredWaypoint];
            }
            else
            {
                if ((waypoint - position).sqrMagnitude > ArrivalDistanceSqr) break;
                // En una esquina, saltar un punto cercano puede cortar por la pared.
                if (_next + 1 < _path.Count &&
                    !AStarPathfinding2D.SegmentIsClear(position, _path[_next + 1], _radius, _walls))
                    break;
            }

            _next++;
        }

        if (_next == _path.Count)
        {
            _motor.Stop();
            return;
        }

        if (!_motor.MoveToWaypoint(_path[_next]))
            ResetPath();
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
