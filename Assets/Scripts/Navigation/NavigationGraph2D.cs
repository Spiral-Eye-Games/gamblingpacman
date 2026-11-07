using System.Collections.Generic;
using UnityEngine;

// Datos compartidos del mapa. Cada agente conserva su propia búsqueda y recorrido.
internal sealed class NavigationGraph2D
{
    public IReadOnlyList<Vector2> Nodes { get; }
    public IReadOnlyList<int>[] Neighbors { get; }

    public NavigationGraph2D(Vector2 origin, IReadOnlyList<Vector2> localNodes,
        IReadOnlyList<Vector2Int> links)
    {
        var worldNodes = new Vector2[localNodes.Count];
        var neighbors = new List<int>[localNodes.Count];
        for (int i = 0; i < localNodes.Count; i++)
        {
            worldNodes[i] = origin + localNodes[i];
            neighbors[i] = new List<int>();
        }

        foreach (Vector2Int link in links)
        {
            int from = link.x;
            int to = link.y;
            if ((uint)from >= localNodes.Count || (uint)to >= localNodes.Count ||
                from == to || neighbors[from].Contains(to))
                continue;

            neighbors[from].Add(to);
            neighbors[to].Add(from);
        }

        Nodes = worldNodes;
        Neighbors = neighbors;
    }
}
