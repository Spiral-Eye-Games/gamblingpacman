using UnityEngine;

// La moneda se compra antes de comenzar la ronda y da un boost temporal.
public class CoinFlipBooster : MonoBehaviour
{
    [SerializeField, Min(1)] int _cost = 100;
    [SerializeField, Min(1f)] float _speedMultiplier = 1.5f;
    [SerializeField, Min(0.1f)] float _boostSeconds = 6f;

    BettingManager _betting;
    MatchSide _boostSide;

    public int Cost => _cost;
    public bool HasFlipped { get; private set; }
    public bool WasHeads { get; private set; }
    public bool GuessedCorrectly { get; private set; }
    public MatchSide BoostSide => _boostSide;

    void Awake()
    {
        _betting = GetComponent<BettingManager>();
    }

    public bool TryBuyAndFlip(bool chooseHeads, out string message)
    {
        if (_betting == null || HasFlipped || !_betting.TryChargeCoin(_cost))
        {
            message = "No se puede comprar la moneda en esta ronda.";
            return false;
        }

        WasHeads = Random.value < 0.5f;
        GuessedCorrectly = WasHeads == chooseHeads;
        _boostSide = GuessedCorrectly ? _betting.BetSide :
            (_betting.BetSide == MatchSide.Pacman ? MatchSide.Ghosts : MatchSide.Pacman);
        HasFlipped = true;
        message = "Salió " + (WasHeads ? "cara" : "cruz") +
            ". Boost para " + BettingManager.SideName(_boostSide) + ".";
        return true;
    }

    public void ApplyBoost(AIGameManager2D match)
    {
        if (!HasFlipped || match == null) return;
        if (_boostSide == MatchSide.Pacman)
        {
            for (int i = 0; i < match.Boids.Count; i++)
            {
                BoidAgent2D boid = match.Boids[i];
                if (boid != null && boid.IsAlive)
                    boid.Motor.BoostSpeed(_speedMultiplier, _boostSeconds);
            }
        }
        else
        {
            for (int i = 0; i < match.Hunters.Count; i++)
            {
                HunterFSM2D hunter = match.Hunters[i];
                if (hunter != null && hunter.IsAlive)
                    hunter.Motor.BoostSpeed(_speedMultiplier, _boostSeconds);
            }
        }
    }

    public void ResetForNewRound()
    {
        HasFlipped = false;
        WasHeads = false;
        GuessedCorrectly = false;
    }
}
