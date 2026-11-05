using System.Collections.Generic;
using UnityEngine;

// Busca posiciones con las mismas reglas de arena y paredes para comida y agentes.
internal readonly struct SpawnPlacement2D
{
    readonly AIGameManager2D _arena;
    readonly LayerMask _walls;
    readonly float _clearance;
    readonly int _attempts;

    public SpawnPlacement2D(AIGameManager2D arena, LayerMask walls, float clearance, int attempts)
    {
        _arena = arena;
        _walls = walls;
        _clearance = clearance;
        _attempts = attempts;
    }

    public bool TryFindPosition(Vector2 center, Vector2 size, out Vector2 position,
        IReadOnlyList<Vector2> occupied = null)
    {
        for (int attempt = 0; attempt < _attempts; attempt++)
        {
            Vector2 candidate = center + new Vector2(
                Random.Range(-size.x * 0.5f, size.x * 0.5f),
                Random.Range(-size.y * 0.5f, size.y * 0.5f));
            if (_arena != null && !_arena.IsInsideArena(candidate, _clearance)) continue;
            if (_walls.value != 0 &&
                Physics2D.OverlapCircle(candidate, _clearance, _walls) != null)
                continue;
            if (occupied != null && !IsFarFromOthers(candidate, occupied)) continue;

            position = candidate;
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    bool IsFarFromOthers(Vector2 candidate, IReadOnlyList<Vector2> occupied)
    {
        for (int i = 0; i < occupied.Count; i++)
        {
            if (Vector2.Distance(candidate, occupied[i]) >= _clearance * 2f) continue;
            return false;
        }
        return true;
    }
}
