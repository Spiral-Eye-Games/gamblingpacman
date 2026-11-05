using UnityEngine;

// Operaciones que necesita el seguidor de rutas.
public interface IAgentMotor
{
    Vector2 Position { get; }
    bool MoveToWaypoint(Vector2 waypoint);
    void Stop();
}

// Lectura del desplazamiento de un agente para resolver capturas.
internal interface IMovementTrace2D
{
    Vector2 Position { get; }
    Vector2 FrameStartPosition { get; }
    float MaxSpeed { get; }
}
