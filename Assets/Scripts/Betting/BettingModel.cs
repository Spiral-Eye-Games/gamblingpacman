// Estado y reglas de apuestas, independientes de la escena y de la interfaz.
internal sealed class BettingModel
{
    public enum BetResult { Accepted, Closed, InvalidAmount }

    enum RoundPhase { ChoosingBet, Ready, Running, Finished }

    RoundPhase _phase;

    public int Balance { get; private set; }
    public int Stake { get; private set; }
    public MatchSide BetSide { get; private set; }
    public MatchSide LastWinner { get; private set; }
    public bool HasBet => _phase != RoundPhase.ChoosingBet;
    public bool CanStart => _phase == RoundPhase.Ready;
    public bool IsRunning => _phase == RoundPhase.Running;
    public bool IsFinished => _phase == RoundPhase.Finished;
    public bool IsBankrupt => IsFinished && Balance == 0;

    public BettingModel(int startingBalance)
    {
        Balance = startingBalance;
    }

    public bool HasReachedGoal(int goal) => IsFinished && Balance >= goal;

    public BetResult PlaceBet(MatchSide side, int amount)
    {
        if (HasBet) return BetResult.Closed;
        if (amount < 1 || amount > Balance) return BetResult.InvalidAmount;

        BetSide = side;
        Stake = amount;
        _phase = RoundPhase.Ready;
        return BetResult.Accepted;
    }

    public bool CanAffordCoin(int cost) => CanStart && Balance - Stake >= cost;

    public void ChargeCoin(int cost)
    {
        Balance -= cost;
    }

    public void BeginRound()
    {
        _phase = RoundPhase.Running;
    }

    public bool TryFinishRound(MatchSide winner)
    {
        if (!IsRunning) return false;

        _phase = RoundPhase.Finished;
        LastWinner = winner;
        Balance += winner == BetSide ? Stake : -Stake;
        return true;
    }

    public void ResetRound()
    {
        _phase = RoundPhase.ChoosingBet;
        Stake = 0;
    }

    public void RestoreBalance(int startingBalance)
    {
        Balance = startingBalance;
    }
}
