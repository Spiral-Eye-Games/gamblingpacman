using UnityEngine;

// Operaciones que necesita el seguidor de rutas.
public interface IAgentMotor
{
    Vector2 Position { get; }
    bool MoveToWaypoint(Vector2 waypoint);
    void Stop();
}
