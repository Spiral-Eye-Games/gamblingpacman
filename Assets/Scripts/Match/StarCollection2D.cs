using System;
using System.Collections.Generic;

// Estado del objetivo. La salida observa su finalización para actualizar la vista.
internal sealed class StarCollection2D
{
    readonly List<StarPickup2D> _remaining = new List<StarPickup2D>();

    public event Action Completed;
    public bool IsComplete => _remaining.Count == 0;

    public void Begin(IEnumerable<StarPickup2D> stars)
    {
        _remaining.Clear();
        _remaining.AddRange(stars);
        if (IsComplete) Completed?.Invoke();
    }

    public void CollectNearby(IReadOnlyList<BoidAgent2D> players)
    {
        if (IsComplete) return;

        for (int i = _remaining.Count - 1; i >= 0; i--)
        {
            StarPickup2D star = _remaining[i];
            if (star == null) continue;

            foreach (BoidAgent2D player in players)
            {
                if (player == null || !player.IsAlive || !star.TryCollect(player))
                    continue;

                _remaining.RemoveAt(i);
                break;
            }
        }

        if (IsComplete) Completed?.Invoke();
    }
}
