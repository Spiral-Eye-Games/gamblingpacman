using System;
using System.Collections.Generic;
using UnityEngine;

public static class AStarPathfinding2D
{
    const float CornerClearance = 0.025f;

    internal static bool TryFindLinkedPath(Rect arena, IReadOnlyList<Vector2> nodes,
        IReadOnlyList<int>[] neighbors, int start, int destination, LayerMask walls, float radius,
        List<int> path) =>
        TryFindLinkedPath(arena, nodes, neighbors, start, node => node == destination,
            nodes[destination], walls, radius, path);

    internal static bool TryFindLinkedPath(Rect arena, IReadOnlyList<Vector2> nodes,
        IReadOnlyList<int>[] neighbors, int start, Predicate<int> reachedGoal, Vector2? goal,
        LayerMask walls, float radius,
        List<int> path)
    {
        path.Clear();
        if (!IsWalkable(nodes[start], arena, walls, radius))
            return false;

        var frontier = new NodePriorityQueue(nodes.Count);
        var costSoFar = new float[nodes.Count];
        var cameFrom = new int[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
            costSoFar[i] = float.PositiveInfinity;

        costSoFar[start] = 0f;
        cameFrom[start] = -1;
        frontier.EnqueueOrDecrease(start, Estimate(start));

        while (frontier.TryDequeue(out int current))
        {
            if (reachedGoal(current))
            {
                for (int node = current; node >= 0; node = cameFrom[node])
                    path.Add(node);
                path.Reverse();
                return true;
            }

            Vector2 position = nodes[current];
            foreach (int next in neighbors[current])
            {
                Vector2 point = nodes[next];
                float cost = costSoFar[current] + Vector2.Distance(position, point);
                if (cost >= costSoFar[next] || !IsWalkable(point, arena, walls, radius) ||
                    !SegmentIsClear(position, point, radius, walls))
                    continue;

                costSoFar[next] = cost;
                cameFrom[next] = current;
                frontier.EnqueueOrDecrease(next, cost + Estimate(next));
            }
        }

        return false;

        float Estimate(int node) => goal.HasValue ? Vector2.Distance(nodes[node], goal.Value) : 0f;
    }

    // El inicio y el destino se conectan a los nodos visibles para esta búsqueda.
    public static bool TryFindPath(Rect arena, IReadOnlyList<Vector2> nodes,
        Vector2 start, Vector2 destination, LayerMask walls, float radius, List<Vector2> path)
    {
        path.Clear();
        if (!IsWalkable(start, arena, walls, radius) ||
            !IsWalkable(destination, arena, walls, radius))
            return false;

        if (SegmentIsClear(start, destination, radius, walls))
        {
            path.Add(destination);
            return true;
        }

        if (TrySearch(arena, nodes, start, destination, walls, radius, path))
            return true;

        // Completa el grafo solo para esta búsqueda. La patrulla conserva sus nodos.
        var detourNodes = new List<Vector2>(nodes);
        foreach (Collider2D wall in Physics2D.OverlapBoxAll(arena.center, arena.size, 0f, walls))
        {
            Bounds bounds = wall.bounds;
            float clearance = radius + CornerClearance;
            float left = bounds.min.x - clearance;
            float right = bounds.max.x + clearance;
            float bottom = bounds.min.y - clearance;
            float top = bounds.max.y + clearance;
            AddCorner(new Vector2(left, bottom));
            AddCorner(new Vector2(left, top));
            AddCorner(new Vector2(right, bottom));
            AddCorner(new Vector2(right, top));
        }

        return detourNodes.Count > nodes.Count &&
            TrySearch(arena, detourNodes, start, destination, walls, radius, path);

        void AddCorner(Vector2 corner)
        {
            if (IsWalkable(corner, arena, walls, radius))
                detourNodes.Add(corner);
        }
    }

    static bool TrySearch(Rect arena, IReadOnlyList<Vector2> nodes, Vector2 start,
        Vector2 destination, LayerMask walls, float radius, List<Vector2> path)
    {
        var frontier = new NodePriorityQueue(nodes.Count);
        var costSoFar = new float[nodes.Count];
        var cameFrom = new int[nodes.Count];
        var walkable = new bool[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            costSoFar[i] = float.PositiveInfinity;
            walkable[i] = IsWalkable(nodes[i], arena, walls, radius);
            Visit(i, -1, start, 0f);
        }

        while (frontier.TryDequeue(out int current))
        {
            Vector2 position = nodes[current];

            // Si el destino es visible, la heurística es el costo real del último tramo.
            if (SegmentIsClear(position, destination, radius, walls))
            {
                path.Add(destination);
                for (int node = current; node >= 0; node = cameFrom[node])
                {
                    Vector2 point = nodes[node];
                    if (!point.Equals(path[path.Count - 1]) && !point.Equals(start))
                        path.Add(point);
                }
                path.Reverse();
                return true;
            }

            for (int next = 0; next < nodes.Count; next++)
                Visit(next, current, position, costSoFar[current]);
        }

        return false;

        void Visit(int next, int parent, Vector2 from, float parentCost)
        {
            if (!walkable[next]) return;
            Vector2 position = nodes[next];
            float newCost = parentCost + Vector2.Distance(from, position);
            if (newCost >= costSoFar[next] || !SegmentIsClear(from, position, radius, walls))
                return;

            costSoFar[next] = newCost;
            cameFrom[next] = parent;
            frontier.EnqueueOrDecrease(next, newCost + Vector2.Distance(position, destination));
        }
    }

    internal static bool IsWalkable(Vector2 point, Rect arena, LayerMask walls, float radius)
    {
        return point.x >= arena.xMin + radius && point.x <= arena.xMax - radius &&
            point.y >= arena.yMin + radius && point.y <= arena.yMax - radius &&
            Physics2D.OverlapCircle(point, radius, walls) == null;
    }

    internal static bool SegmentIsClear(Vector2 from, Vector2 to, float radius, LayerMask walls)
    {
        Vector2 offset = to - from;
        float distance = offset.magnitude;
        return distance == 0f || Physics2D.CircleCast(from, radius, offset / distance,
            distance, walls).collider == null;
    }
}
