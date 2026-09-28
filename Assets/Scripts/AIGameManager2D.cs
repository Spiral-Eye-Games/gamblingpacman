using System;
using System.Collections.Generic;
using UnityEngine;

// Mantiene los participantes y decide el ganador de cada ronda.
public class AIGameManager2D : MonoBehaviour
{
    public static AIGameManager2D Instance { get; private set; }

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

    [SerializeField] HunterFSM2D _hunter;
    [Header("Zonas de wraparound")]
    [SerializeField] WrapArea[] _wrapAreas = new WrapArea[0];
    [Header("Guía visual de la arena")]
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
    public HunterFSM2D Hunter => _hunter;
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

    void LateUpdate()
    {
        if (!_running) return;

        // Los agentes se mueven en Update; luego se corrige el solapamiento.
        // Tres pasadas alcanzan para los pequeños grupos de esta escena.
        for (int pass = 0; pass < 3; pass++)
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

        ResolveCaptures();
        for (int i = 0; i < _boids.Count; i++)
            if (_boids[i] != null && _boids[i].IsAlive)
                _boids[i].Motor.MarkFrameEnd();
        for (int i = 0; i < _hunters.Count; i++)
            if (_hunters[i] != null && _hunters[i].IsAlive)
                _hunters[i].Motor.MarkFrameEnd();
    }

    // Se comprueba al final del frame para incluir movimiento y separación entre compañeros.
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

    // Distancia mínima entre las trayectorias de los dos agentes durante este frame.
    // Así se detecta también un cruce rápido que no termina en solapamiento.
    static bool PathsTouch(Steering first, Steering second, float radius)
    {
        Vector2 firstEnd = first.Position;
        Vector2 secondEnd = second.Position;
        float radiusSqr = radius * radius;
        if ((firstEnd - secondEnd).sqrMagnitude <= radiusSqr) return true;

        Vector2 firstStart = first.FrameStartPosition;
        Vector2 secondStart = second.FrameStartPosition;
        float maxFirstStep = first.MaxSpeed * Time.deltaTime + 0.5f;
        float maxSecondStep = second.MaxSpeed * Time.deltaTime + 0.5f;
        if ((firstEnd - firstStart).sqrMagnitude > maxFirstStep * maxFirstStep)
            firstStart = firstEnd; // wraparound: el teletransporte no es una captura
        if ((secondEnd - secondStart).sqrMagnitude > maxSecondStep * maxSecondStep)
            secondStart = secondEnd;

        Vector2 relativeStart = firstStart - secondStart;
        Vector2 relativeMove = (firstEnd - firstStart) - (secondEnd - secondStart);
        float lengthSqr = relativeMove.sqrMagnitude;
        float t = lengthSqr > 0.000001f
            ? Mathf.Clamp01(-Vector2.Dot(relativeStart, relativeMove) / lengthSqr)
            : 0f;
        return (relativeStart + relativeMove * t).sqrMagnitude <= radiusSqr;
    }

    static void SeparateTeammates(Steering first, float firstRadius,
        Steering second, float secondRadius)
    {
        Vector2 difference = first.Position - second.Position;
        float distance = difference.magnitude;
        float minimumDistance = firstRadius + secondRadius;
        if (distance >= minimumDistance) return;

        Vector2 direction = distance > 0.0001f
            ? difference / distance : Vector2.right;
        Vector2 correction = direction * ((minimumDistance - distance) * 0.5f);
        first.PushOut(correction);
        second.PushOut(-correction);
    }

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
        if (hunter == null || _hunters.Contains(hunter)) return;
        _hunters.Add(hunter);
        if (_hunter == null) _hunter = hunter;
    }

    public void RegisterFood(FoodPickup2D item)
    {
        if (item != null && !_food.Contains(item)) _food.Add(item);
    }

    public void UnregisterFood(FoodPickup2D item)
    {
        _food.Remove(item);
    }

    public FoodPickup2D FindNearestFood(Vector2 position, float radius)
    {
        FoodPickup2D nearest = null;
        float bestDistance = radius;
        for (int i = 0; i < _food.Count; i++)
        {
            FoodPickup2D item = _food[i];
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
        for (int i = 0; i < _hunters.Count; i++)
        {
            HunterFSM2D hunter = _hunters[i];
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
        for (int i = 0; i < _boids.Count; i++)
        {
            BoidAgent2D boid = _boids[i];
            if (boid == null || !boid.IsAlive) continue;
            float distance = Vector2.Distance(boid.Motor.Position, position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = boid;
        }
        return nearest;
    }

    public bool IsInsideArena(Vector2 position, float margin)
    {
        Vector2 offset = position - (Vector2)transform.position;
        Vector2 half = _arenaSize * 0.5f;
        return Mathf.Abs(offset.x) + margin <= half.x &&
               Mathf.Abs(offset.y) + margin <= half.y;
    }

    // Solo se teletransporta si el paso entra o cruza una zona definida.
    public Vector2 ApplyWrapAreas(Vector2 previousPosition, Vector2 position)
    {
        if (_wrapAreas == null) return position;

        for (int i = 0; i < _wrapAreas.Length; i++)
        {
            WrapArea area = _wrapAreas[i];
            if (area == null) continue;

            Vector2 entryCenter = (Vector2)transform.position + area.EntryLocalCenter;
            Bounds entry = new Bounds(entryCenter,
                new Vector3(area.EntrySize.x, area.EntrySize.y, 1f));
            Vector2 crossingPosition = position;

            if (!entry.Contains(position))
            {
                Vector2 movement = position - previousPosition;
                float distance = movement.magnitude;
                if (distance < 0.0001f) continue;

                Vector2 direction = movement / distance;
                Ray ray = new Ray(previousPosition, direction);
                float hitDistance;
                if (!entry.IntersectRay(ray, out hitDistance) ||
                    hitDistance > distance)
                    continue;
                crossingPosition = previousPosition + direction * hitDistance;
            }

            Vector2 offset = area.ExitOffset(crossingPosition - entryCenter);
            return (Vector2)transform.position + area.ExitLocalPosition + offset;
        }

        return position;
    }

    void OnValidate()
    {
        if (_wrapAreas == null) return;
        for (int i = 0; i < _wrapAreas.Length; i++)
            if (_wrapAreas[i] != null) _wrapAreas[i].ClampSize();
    }

    public void FoodConsumed(FoodPickup2D item)
    {
        if (!_running || _finished || item == null || _roles == null) return;
        _roles.AddFood(item.FoodUnits);
    }

    public void BoidCaught()
    {
        if (!_running || _finished) return;
        for (int i = 0; i < _boids.Count; i++)
            if (_boids[i] != null && _boids[i].IsAlive) return;
        Finish(MatchSide.Ghosts);
    }

    public void GhostCaught()
    {
        if (!_running || _finished) return;
        for (int i = 0; i < _hunters.Count; i++)
            if (_hunters[i] != null && _hunters[i].IsAlive) return;
        Finish(MatchSide.Pacman);
    }

    void Finish(MatchSide winner)
    {
        _running = false;
        _finished = true;
        _winner = winner;
        Debug.Log("Ganó " + (winner == MatchSide.Pacman ? "Pacman" : "Fantasmas"), this);
        MatchFinished?.Invoke(winner);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, _arenaSize);

        if (_wrapAreas == null) return;
        for (int i = 0; i < _wrapAreas.Length; i++)
        {
            WrapArea area = _wrapAreas[i];
            if (area == null) continue;
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
