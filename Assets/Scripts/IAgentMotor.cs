using UnityEngine;

//lo minimo que el manager y los comportamientos necesitan de cualquier motor
public interface IAgentMotor
{
    Vector2 Position {get;}
    Vector2 FrameStartPosition {get;}
    Vector2 Velocity { get; }
    float MaxSpeed {get;}
    bool MoveToWaypoint(Vector2 waypoint);
    void Stop();
    void MarkFrameEnd();
}
