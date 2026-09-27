using UnityEngine;

// Controles sencillos para apostar antes de iniciar cada ronda.
public class BettingHUD : MonoBehaviour
{
    [SerializeField] BettingManager _betting;
    [SerializeField] CoinFlipBooster _coin;
    string _amountText = "100";
    string _message = "Elegí un monto y un bando.";

    void Start()
    {
        if (_betting == null) _betting = BettingManager.Instance;
        if (_coin == null && _betting != null)
            _coin = _betting.GetComponent<CoinFlipBooster>();
    }

    void OnGUI()
    {
        if (_betting == null) return;
        bool narrow = Screen.width < 870;
        float x = narrow ? 10f : Screen.width - 350f;
        float y = narrow ? 185f : 10f;
        GUILayout.BeginArea(new Rect(x, y, 340, 260), GUI.skin.box);
        GUILayout.Label("APUESTA | Saldo: $" + _betting.Balance +
            " / Meta: $" + _betting.GoalBalance);
        if (_coin != null && _coin.HasFlipped)
            GUILayout.Label("Moneda: " + (_coin.WasHeads ? "cara" : "cruz") +
                " | boost para " + BettingManager.SideName(_coin.BoostSide));

        if (!_betting.HasBet)
        {
            GUILayout.Label("Monto a apostar:");
            _amountText = GUILayout.TextField(_amountText, 8);
            if (GUILayout.Button("Apostar a Pacman")) PlaceBet(MatchSide.Pacman);
            if (GUILayout.Button("Apostar a Fantasmas")) PlaceBet(MatchSide.Ghosts);
        }
        else if (!_betting.IsRunning && !_betting.MatchFinished)
        {
            GUILayout.Label("Apuesta: $" + _betting.Stake + " a " +
                BettingManager.SideName(_betting.BetSide));
            if (_coin != null && !_coin.HasFlipped)
            {
                GUILayout.Label("Moneda opcional: $" + _coin.Cost);
                GUI.enabled = _betting.CanBuyCoin;
                if (GUILayout.Button("Elegir cara")) Flip(true);
                if (GUILayout.Button("Elegir cruz")) Flip(false);
                GUI.enabled = true;
            }
            if (GUILayout.Button("Iniciar ronda"))
                _betting.TryStartRound(out _message);
        }
        else if (_betting.IsRunning)
        {
            GUILayout.Label("La simulación está en curso...");
        }
        else
        {
            GUILayout.Label("Ganó " + BettingManager.SideName(_betting.LastWinner));
            if (_betting.GoalReached)
                GUILayout.Label("¡Llegaste a $" + _betting.GoalBalance + "! Ganaste.");
            else if (_betting.Bankrupt)
                GUILayout.Label("Te quedaste sin dinero. Fin del juego.");
            else if (GUILayout.Button("Siguiente ronda"))
                _betting.NextRound();
        }

        GUILayout.Space(8f);
        GUILayout.Label(_message);
        GUILayout.EndArea();
    }

    void PlaceBet(MatchSide side)
    {
        if (!int.TryParse(_amountText, out int amount))
        {
            _message = "Escribí un monto entero.";
            return;
        }
        _betting.TryPlaceBet(side, amount, out _message);
    }

    void Flip(bool chooseHeads)
    {
        if (_coin != null) _coin.TryBuyAndFlip(chooseHeads, out _message);
    }
}
