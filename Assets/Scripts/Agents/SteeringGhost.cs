using UnityEngine;

// Mantiene la referencia del motor en los prefabs de fantasmas.
public class SteeringGhost : Steering
{
    internal void ClearShallowWallOverlap(Rect arena, LayerMask walls, float radius)
    {
        Vector2 position = Position;
        Collider2D wall = Physics2D.OverlapCircle(position, radius, walls);
        if (wall == null) return;

        Vector2 offset = position - wall.ClosestPoint(position);
        float distance = offset.magnitude;
        // Si el centro está dentro de la pared, no hay un lado seguro que inferir.
        if (distance <= 0.0001f) return;

        Vector2 direction = offset / distance;
        float separation = Mathf.Max(0f, radius - distance) + 0.01f;
        Vector2 clearPosition = position + direction * separation;
        if (!AStarPathfinding2D.IsWalkable(clearPosition, arena, walls, radius)) return;

        foreach (RaycastHit2D hit in Physics2D.CircleCastAll(position, radius, direction, separation, walls))
        {
            if (hit.collider != wall || hit.distance > 0.0001f) return;
        }

        transform.position = new Vector3(clearPosition.x, clearPosition.y, transform.position.z);
        Stop();
    }
}
