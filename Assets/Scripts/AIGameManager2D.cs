using System;
using System.Collections.Generic;
using UnityEngine;

// Mantiene los participantes, resuelve las capturas y decide el ganador de cada ronda.
public class AIGameManager2D : MonoBehaviour
{
    public static AIGameManager2D Instance { get; private set; }

    // Números técnicos con nombre, no son para ajustar el juego, por eso no van al inspector
    const float MinDistance = 0.0001f;          // "es prácticamente el mismo punto"
    const float MinRelativeMoveSqr = 0.000001f; // "los dos agentes se mueven igual"
    const float TeleportMargin = 0.5f;          // tolerancia para distinguir un paso normal de un teletransporte

    // Zona de entrada que teletransporta a una salida (los túneles del mapa).
    [Serializable]
    public class WrapArea
    {
        [SerializeField] Vector2 _entryLocalCenter;
        [SerializeField] Vector2 _entrySize = new Vector2(1.1f, 0.5f);
        [SerializeField] Vector2 _exitLocalPosition;
        [SerializeField] bool _keepX;
        [SerializeField] bool _keepY = true;

        public Vector2 EntryLocalCenter => _entryLocalCenter;
        public Vector2 EntrySize => _entrySize;
        public Vector2 ExitLocalPosition => _exitLocalPosition;

        // Conserva en la salida el desplazamiento en X y/o Y con el que se entró.
        public Vector2 ExitOffset(Vector2 entryOffset)
        {
            return new Vector2(_keepX ? entryOffset.x : 0f,
                _keepY ? entryOffset.y : 0f);
        }

        public void ClampSize()
        {
            _entrySize.x = Mathf.Max(0.01f, _entrySize.x);
            _entrySize.y = Mathf.Max(0.01f, _entrySize.y);
        }
    }

    [Header("Separación entre compañeros")]
    [SerializeField, Min(1)] int _separationPasses = 3;

    [Header("Zonas de wraparound")]
    [SerializeField] WrapArea[] _wrapAreas = new WrapArea[0];

    [Header("Arena")]
    [SerializeField] Vector2 _arenaSize = new Vector2(16f, 9f);

    readonly List<BoidAgent2D> _boids = new List<BoidAgent2D>();
    readonly List<HunterFSM2D> _hunters = new List<HunterFSM2D>();
    readonly List<FoodPickup2D> _food = new List<FoodPickup2D>();
    RoleSwapManager _roles;
    bool _running;
    bool _finished;
    MatchSide _winner;

    public event Action<MatchSide> MatchFinished;

    public List<BoidAgent2D> Boids => _boids;
    public List<HunterFSM2D> Hunters => _hunters;
    public RoleSwapManager Roles => _roles;
    public bool IsRunning => _running;
    public bool IsFinished => _finished;
    public MatchSide Winner => _winner;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Debe haber un solo AIGameManager2D en la escena.", this);
            enabled = false;
            return;
        }
        Instance = this;
        _roles = GetComponent<RoleSwapManager>();
    }

    void Start()
    {
        if (_roles == null)
            Debug.LogError("AIManager necesita RoleSwapManager.", this);
        if (BettingManager.Instance == null)
            Debug.LogError("Falta BettingManager en la escena.", this);
        else
            BettingManager.Instance.BindMatch(this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- Fin de cada frame: los agentes ya se movieron en su Update ---

    void LateUpdate()
    {
        if (!_running) return;

        for (int pass = 0; pass < _separationPasses; pass++)
        {
            SeparateBoids();
            SeparateHunters();
        }

        ResolveCaptures();
        MarkFrameEnd();
    }

    void SeparateBoids()
    {
        for (int i = 0; i < _boids.Count; i++)
        {
            BoidAgent2D first = _boids[i];
            if (first == null || !first.IsAlive) continue;
            for (int j = i + 1; j < _boids.Count; j++)
            {
                BoidAgent2D second = _boids[j];
                if (second == null || !second.IsAlive) continue;
                SeparateTeammates(first.Motor, first.TeamCollisionRadius,
                    second.Motor, second.TeamCollisionRadius);
            }
        }
    }

    void SeparateHunters()
    {
        for (int i = 0; i < _hunters.Count; i++)
        {
            HunterFSM2D first = _hunters[i];
            if (first == null || !first.IsAlive) continue;
            for (int j = i + 1; j < _hunters.Count; j++)
            {
                HunterFSM2D second = _hunters[j];
                if (second == null || !second.IsAlive) continue;
                SeparateTeammates(first.Motor, first.TeamCollisionRadius,
                    second.Motor, second.TeamCollisionRadius);
            }
        }
    }
    

    // Si dos compañeros se solapan, los aleja a mitad de camino cada uno.
    static void SeparateTeammates(IAgentMotor first, float firstRadius,
    IAgentMotor second, float secondRadius)
    {
        Vector2 difference = first.Position - second.Position;
        float distance = difference.magnitude;
        float minimumDistance = firstRadius + secondRadius;
        if (distance >= minimumDistance) return;

        Vector2 direction = distance > MinDistance
            ? difference / distance : Vector2.right;
        Vector2 correction = direction * ((minimumDistance - distance) * 0.5f);
        first.PushOut(correction);
        second.PushOut(-correction);
    }

    // La captura depende del bando cazador, aunque el fantasma esté descansando.
    void ResolveCaptures()
    {
        if (_roles == null || !_running) return;
        bool pacmanHunts = _roles.PacmanIsHunter;

        for (int i = 0; i < _boids.Count; i++)
        {
            BoidAgent2D boid = _boids[i];
            if (boid == null || !boid.IsAlive) continue;
            for (int j = 0; j < _hunters.Count; j++)
            {
                HunterFSM2D ghost = _hunters[j];
                if (ghost == null || !ghost.IsAlive) continue;
                float radius = pacmanHunts ? boid.CaptureRadius : ghost.CaptureRadius;
                if (!PathsTouch(boid.Motor, ghost.Motor, radius)) continue;

                if (pacmanHunts) ghost.CaughtByPacman();
                else boid.Caught();
                return; // una captura puede cambiar el resultado y las listas
            }
        }
    }

    // ¿Se tocaron los dos agentes en algún momento de este frame?
    // Además del punto final, se revisa la trayectoria: así también cuenta
    // un cruce rápido que no termina en solapamiento.
    static bool PathsTouch(IAgentMotor first, IAgentMotor second, float radius)
    {
        Vector2 firstEnd = first.Position;
        Vector2 secondEnd = second.Position;
        float radiusSqr = radius * radius;

        // Caso simple: al final del frame ya se están tocando.
        if ((firstEnd - secondEnd).sqrMagnitude <= radiusSqr) return true;

        // Caso del cruce rápido: se busca el momento del frame en que estuvieron más cerca.
        Vector2 firstStart = FrameStart(first);
        Vector2 secondStart = FrameStart(second);
        Vector2 relativeStart = firstStart - secondStart;
        Vector2 relativeMove = (firstEnd - firstStart) - (secondEnd - secondStart);
        float moveSqr = relativeMove.sqrMagnitude;

        // t va de 0 (inicio del frame) a 1 (final): cuándo estuvieron más cerca.
        float t = moveSqr > MinRelativeMoveSqr
            ? Mathf.Clamp01(-Vector2.Dot(relativeStart, relativeMove) / moveSqr)
            : 0f;
        return (relativeStart + relativeMove * t).sqrMagnitude <= radiusSqr;
    }

    // Punto donde el agente empezó el frame. Si se movió más de lo posible, fue un
    // teletransporte (wraparound) y no una captura: se usa el punto final como inicio.
    static Vector2 FrameStart(IAgentMotor agent)
    {
        Vector2 end = agent.Position;
        Vector2 start = agent.FrameStartPosition;
        float maxStep = agent.MaxSpeed * Time.deltaTime + TeleportMargin;
        return (end - start).sqrMagnitude > maxStep * maxStep ? end : start;
    }

    void MarkFrameEnd()
    {
        foreach (BoidAgent2D boid in _boids)
            if (boid != null && boid.IsAlive) boid.Motor.MarkFrameEnd();
        foreach (HunterFSM2D hunter in _hunters)
            if (hunter != null && hunter.IsAlive) hunter.Motor.MarkFrameEnd();
    }

    // --- Ronda ---

    public bool BeginMatch()
    {
        if (_running || _finished || _roles == null) return false;
        if (_boids.Count == 0 || _hunters.Count == 0)
        {
            Debug.LogError("La ronda necesita al menos un boid y un fantasma.", this);
            return false;
        }
        _running = true;
        return true;
    }

    public void FoodConsumed(FoodPickup2D item)
    {
        if (!_running || _finished || item == null || _roles == null) return;
        _roles.AddFood(item.FoodUnits);
    }

    // Ganan los fantasmas cuando no queda ningún boid vivo.
    public void BoidCaught()
    {
        if (!_running || _finished || AnyBoidAlive()) return;
        Finish(MatchSide.Ghosts);
    }

    // Gana Pacman cuando no queda ningún fantasma vivo.
    public void GhostCaught()
    {
        if (!_running || _finished || AnyHunterAlive()) return;
        Finish(MatchSide.Pacman);
    }

    bool AnyBoidAlive()
    {
        foreach (BoidAgent2D boid in _boids)
            if (boid != null && boid.IsAlive) return true;
        return false;
    }

    bool AnyHunterAlive()
    {
        foreach (HunterFSM2D hunter in _hunters)
            if (hunter != null && hunter.IsAlive) return true;
        return false;
    }

    void Finish(MatchSide winner)
    {
        _running = false;
        _finished = true;
        _winner = winner;
        Debug.Log("Ganó " + BettingManager.SideName(winner), this);
        MatchFinished?.Invoke(winner);
    }

    // --- Registro de participantes ---

    public void RegisterBoid(BoidAgent2D boid)
    {
        if (boid != null && !_boids.Contains(boid)) _boids.Add(boid);
    }

    public void UnregisterBoid(BoidAgent2D boid)
    {
        _boids.Remove(boid);
    }

    public void RegisterHunter(HunterFSM2D hunter)
    {
        if (hunter != null && !_hunters.Contains(hunter)) _hunters.Add(hunter);
    }

    public void RegisterFood(FoodPickup2D item)
    {
        if (item != null && !_food.Contains(item)) _food.Add(item);
    }

    public void UnregisterFood(FoodPickup2D item)
    {
        _food.Remove(item);
    }

    // --- Búsquedas: el más cercano dentro del radio, o null si no hay ---

    public FoodPickup2D FindNearestFood(Vector2 position, float radius)
    {
        FoodPickup2D nearest = null;
        float bestDistance = radius;
        foreach (FoodPickup2D item in _food)
        {
            if (item == null || !item.IsAvailable) continue;
            float distance = Vector2.Distance(item.transform.position, position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = item;
        }
        return nearest;
    }

    public HunterFSM2D FindNearestHunter(Vector2 position, float radius)
    {
        HunterFSM2D nearest = null;
        float bestDistance = radius;
        foreach (HunterFSM2D hunter in _hunters)
        {
            if (hunter == null || !hunter.IsAlive) continue;
            float distance = Vector2.Distance(hunter.Motor.Position, position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = hunter;
        }
        return nearest;
    }

    public BoidAgent2D FindNearestBoid(Vector2 position, float radius)
    {
        BoidAgent2D nearest = null;
        float bestDistance = radius;
        foreach (BoidAgent2D boid in _boids)
        {
            if (boid == null || !boid.IsAlive) continue;
            float distance = Vector2.Distance(boid.Motor.Position, position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = boid;
        }
        return nearest;
    }

    // --- Arena y túneles ---

    public bool IsInsideArena(Vector2 position, float margin)
    {
        Vector2 offset = position - (Vector2)transform.position;
        Vector2 half = _arenaSize * 0.5f;
        return Mathf.Abs(offset.x) + margin <= half.x &&
               Mathf.Abs(offset.y) + margin <= half.y;
    }

    // Solo teletransporta si el paso de este frame entra o cruza una zona de entrada.
    public Vector2 ApplyWrapAreas(Vector2 previousPosition, Vector2 position)
    {
        foreach (WrapArea area in _wrapAreas)
        {
            Vector2 entryCenter = (Vector2)transform.position + area.EntryLocalCenter;
            // Profundidad 1: el juego es 2D, Bounds solo necesita que no sea 0.
            Bounds entry = new Bounds(entryCenter,
                new Vector3(area.EntrySize.x, area.EntrySize.y, 1f));
            Vector2 crossingPosition = position;

            if (!entry.Contains(position))
            {
                // No terminó adentro: se revisa si el paso atravesó la zona.
                Vector2 movement = position - previousPosition;
                float distance = movement.magnitude;
                if (distance < MinDistance) continue;

                Vector2 direction = movement / distance;
                Ray ray = new Ray(previousPosition, direction);
                if (!entry.IntersectRay(ray, out float hitDistance) ||
                    hitDistance > distance)
                    continue;
                crossingPosition = previousPosition + direction * hitDistance;
            }

            Vector2 offset = area.ExitOffset(crossingPosition - entryCenter);
            return (Vector2)transform.position + area.ExitLocalPosition + offset;
        }

        return position;
    }

    // --- Editor ---

    void OnValidate()
    {
        foreach (WrapArea area in _wrapAreas) area.ClampSize();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, _arenaSize);

        foreach (WrapArea area in _wrapAreas)
        {
            Vector2 entry = (Vector2)transform.position + area.EntryLocalCenter;
            Vector2 exit = (Vector2)transform.position + area.ExitLocalPosition;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(entry, area.EntrySize);
            Gizmos.DrawLine(entry, exit);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(exit, 0.15f);
        }
    }
}