using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Coordina las rondas de Unity con el modelo de apuestas.
public class BettingManager : MonoBehaviour
{
    public static BettingManager Instance { get; private set; }

    [SerializeField, Min(1)] int _startingBalance = 500;
    [SerializeField, Min(1)] int _goalBalance = 1000;

    AIGameManager2D _match;
    CoinFlipBooster _coin;
    readonly BettingModel _model = new BettingModel(0);

    internal event Action StateChanged;

    public int Balance => _model.Balance;
    public int GoalBalance => _goalBalance;
    public int Stake => _model.Stake;
    public MatchSide BetSide => _model.BetSide;
    public MatchSide LastWinner => _model.LastWinner;
    public bool HasBet => _model.HasBet;
    public bool IsRunning => _model.IsRunning;
    public bool MatchFinished => _model.IsFinished;
    public bool GoalReached => _model.HasReachedGoal(_goalBalance);
    public bool Bankrupt => _model.IsBankrupt;
    public bool CanBuyCoin => _coin != null && !_coin.HasFlipped &&
        _model.CanAffordCoin(_coin.Cost);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _model.RestoreBalance(_startingBalance);
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
        BettingModel.BetResult result = _match == null
            ? BettingModel.BetResult.Closed : _model.PlaceBet(side, amount);
        if (result == BettingModel.BetResult.Closed)
        {
            message = "La apuesta de esta ronda ya está cerrada.";
            return false;
        }
        if (result == BettingModel.BetResult.InvalidAmount)
        {
            message = "Apostá entre $1 y tu saldo disponible.";
            return false;
        }

        message = "Apuesta de $" + amount + " a " + side.DisplayName() + ".";
        StateChanged?.Invoke();
        return true;
    }

    public bool TryChargeCoin(int cost)
    {
        if (!CanBuyCoin || cost != _coin.Cost) return false;
        _model.ChargeCoin(cost);
        StateChanged?.Invoke();
        return true;
    }

    public bool TryStartRound(out string message)
    {
        if (!_model.CanStart || _match == null)
        {
            message = "Primero confirmá una apuesta.";
            return false;
        }
        if (!_match.BeginMatch())
        {
            message = "Faltan agentes o RoleSwapManager en la escena.";
            return false;
        }
        _model.BeginRound();
        if (_coin != null) _coin.ApplyBoost(_match);
        message = "Ronda iniciada.";
        StateChanged?.Invoke();
        return true;
    }

    void OnMatchFinished(MatchSide winner)
    {
        if (!_model.TryFinishRound(winner)) return;
        Debug.Log("Ganó " + winner.DisplayName() + ". Saldo: $" + Balance, this);
        StateChanged?.Invoke();
    }

    public bool NextRound()
    {
        if (!MatchFinished || GoalReached || Bankrupt) return false;
        if (_match != null) _match.MatchFinished -= OnMatchFinished;
        _match = null;
        _model.ResetRound();
        if (_coin != null) _coin.ResetForNewRound();
        StateChanged?.Invoke();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        return true;
    }

    //Al ganar (meta) o perder (sin dinero), vuelve a empezar desde el saldo inicial
    public bool Retry()
    {
        if (!GoalReached && !Bankrupt) return false;
        _model.RestoreBalance(_startingBalance);
        if (NextRound()) return true;
        StateChanged?.Invoke();
        return false;
    }

    public static string SideName(MatchSide side) => side.DisplayName();
}
