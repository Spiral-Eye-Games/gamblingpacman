using UnityEngine;

// Lectura del desplazamiento de un agente para resolver capturas.
internal interface IMovementTrace2D
{
    Vector2 Position { get; }
    Vector2 FrameStartPosition { get; }
}
