using UnityEngine;
using UnityEngine.SceneManagement;

// Conserva el dinero entre rondas; una apuesta acertada gana su mismo monto.
public class BettingManager : MonoBehaviour
{
    public static BettingManager Instance { get; private set; }

    [SerializeField, Min(1)] int _startingBalance = 500;
    [SerializeField, Min(1)] int _goalBalance = 1000;

    AIGameManager2D _match;
    CoinFlipBooster _coin;

    public int Balance { get; private set; }
    public int GoalBalance => _goalBalance;
    public int Stake { get; private set; }
    public MatchSide BetSide { get; private set; }
    public MatchSide LastWinner { get; private set; }
    public bool HasBet { get; private set; }
    public bool IsRunning { get; private set; }
    public bool MatchFinished { get; private set; }
    public bool GoalReached => MatchFinished && Balance >= _goalBalance;
    public bool Bankrupt => MatchFinished && Balance == 0;
    public bool CanBuyCoin => HasBet && !IsRunning && !MatchFinished &&
        _coin != null && !_coin.HasFlipped && Balance - Stake >= _coin.Cost;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Balance = _startingBalance;
        _coin = GetComponent<CoinFlipBooster>();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (_match != null) _match.MatchFinished -= OnMatchFinished;
        Instance = null;
    }

    public void BindMatch(AIGameManager2D match)
    {
        if (_match != null) _match.MatchFinished -= OnMatchFinished;
        _match = match;
        if (_match != null) _match.MatchFinished += OnMatchFinished;
    }

    public bool TryPlaceBet(MatchSide side, int amount, out string message)
    {
        if (HasBet || IsRunning || MatchFinished || _match == null)
        {
            message = "La apuesta de esta ronda ya está cerrada.";
            return false;
        }
        if (amount < 1 || amount > Balance)
        {
            message = "Apostá entre $1 y tu saldo disponible.";
            return false;
        }
        BetSide = side;
        Stake = amount;
        HasBet = true;
        message = "Apuesta de $" + amount + " a " + SideName(side) + ".";
        return true;
    }

    public bool TryChargeCoin(int cost)
    {
        if (!CanBuyCoin || cost != _coin.Cost) return false;
        Balance -= cost;
        return true;
    }

    public bool TryStartRound(out string message)
    {
        if (!HasBet || IsRunning || MatchFinished || _match == null)
        {
            message = "Primero confirmá una apuesta.";
            return false;
        }
        if (!_match.BeginMatch())
        {
            message = "Faltan agentes o RoleSwapManager en la escena.";
            return false;
        }
        IsRunning = true;
        if (_coin != null) _coin.ApplyBoost(_match);
        message = "Ronda iniciada.";
        return true;
    }

    void OnMatchFinished(MatchSide winner)
    {
        if (!IsRunning || MatchFinished) return;
        IsRunning = false;
        MatchFinished = true;
        LastWinner = winner;
        Balance += winner == BetSide ? Stake : -Stake;
        Debug.Log("Ganó " + SideName(winner) + ". Saldo: $" + Balance, this);
    }

    public bool NextRound()
    {
        if (!MatchFinished || GoalReached || Bankrupt) return false;
        if (_match != null) _match.MatchFinished -= OnMatchFinished;
        _match = null;
        HasBet = false;
        IsRunning = false;
        MatchFinished = false;
        Stake = 0;
        if (_coin != null) _coin.ResetForNewRound();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        return true;
    }

    public static string SideName(MatchSide side)
    {
        return side == MatchSide.Pacman ? "Pacman" : "Fantasmas";
    }
}
