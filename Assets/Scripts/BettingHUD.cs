using UnityEngine;
using UnityEngine.UI;

// Actualiza y conecta los controles del Canvas. El diseño vive en la escena.
public class BettingHUD : MonoBehaviour
{
    [Header("Sistemas")]
    [SerializeField] AIGameManager2D _match;
    [SerializeField] BettingManager _betting;
    [SerializeField] CoinFlipBooster _coin;

    [Header("Estado de la partida")]
    [SerializeField] Text _matchStateText;
    [SerializeField] Text _phaseText;
    [SerializeField] Text _countsText;

    [Header("Apuestas")]
    [SerializeField] Text _balanceText;
    [SerializeField] Text _betSummaryText;
    [SerializeField] Text _coinPriceText;
    [SerializeField] Text _coinResultText;
    [SerializeField] Text _runningText;
    [SerializeField] Text _outcomeText;
    [SerializeField] Text _messageText;
    [SerializeField] InputField _amountInput;

    [Header("Botones")]
    [SerializeField] Button _betPacmanButton;
    [SerializeField] Button _betGhostsButton;
    [SerializeField] Button _headsButton;
    [SerializeField] Button _tailsButton;
    [SerializeField] Button _startButton;
    [SerializeField] Button _nextRoundButton;

    [Header("Grupos de la interfaz")]
    [SerializeField] GameObject _betEntryGroup;
    [SerializeField] GameObject _beforeRoundGroup;
    [SerializeField] GameObject _coinChoiceGroup;
    [SerializeField] GameObject _runningGroup;
    [SerializeField] GameObject _finishedGroup;

    string _message = "Elegí un monto y un bando.";

    void Start()
    {
        if (_match == null) _match = AIGameManager2D.Instance;
        if (_betting == null) _betting = BettingManager.Instance;
        if (_coin == null && _betting != null)
            _coin = _betting.GetComponent<CoinFlipBooster>();

        if (_match == null || _betting == null || _amountInput == null ||
            _betPacmanButton == null || _betGhostsButton == null ||
            _headsButton == null || _tailsButton == null ||
            _startButton == null || _nextRoundButton == null)
        {
            Debug.LogError("BettingHUD necesita las referencias del HUD Canvas.", this);
            enabled = false;
            return;
        }

        _amountInput.contentType = InputField.ContentType.IntegerNumber;
        _amountInput.characterLimit = 8;
        if (string.IsNullOrEmpty(_amountInput.text)) _amountInput.text = "100";
        _betPacmanButton.onClick.AddListener(BetPacman);
        _betGhostsButton.onClick.AddListener(BetGhosts);
        _headsButton.onClick.AddListener(ChooseHeads);
        _tailsButton.onClick.AddListener(ChooseTails);
        _startButton.onClick.AddListener(StartRound);
        _nextRoundButton.onClick.AddListener(NextRound);
        Refresh();
    }

    void OnDestroy()
    {
        if (_betPacmanButton != null) _betPacmanButton.onClick.RemoveListener(BetPacman);
        if (_betGhostsButton != null) _betGhostsButton.onClick.RemoveListener(BetGhosts);
        if (_headsButton != null) _headsButton.onClick.RemoveListener(ChooseHeads);
        if (_tailsButton != null) _tailsButton.onClick.RemoveListener(ChooseTails);
        if (_startButton != null) _startButton.onClick.RemoveListener(StartRound);
        if (_nextRoundButton != null) _nextRoundButton.onClick.RemoveListener(NextRound);
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        if (_match == null || _betting == null) return;

        string state = _match.IsFinished
            ? "Ganó " + BettingManager.SideName(_match.Winner)
            : (_match.IsRunning ? "Ronda en curso" : "Esperando apuesta");
        SetText(_matchStateText, state);
        SetText(_phaseText, _match.Roles != null && _match.Roles.PacmanIsHunter
            ? "Pacman caza" : "Fantasmas cazan");

        int boids = 0;
        int ghosts = 0;
        for (int i = 0; i < _match.Boids.Count; i++)
            if (_match.Boids[i] != null && _match.Boids[i].IsAlive) boids++;
        for (int i = 0; i < _match.Hunters.Count; i++)
            if (_match.Hunters[i] != null && _match.Hunters[i].IsAlive) ghosts++;
        int food = _match.Roles != null ? _match.Roles.FoodEaten : 0;
        int needed = _match.Roles != null ? _match.Roles.FoodNeeded : 0;
        SetText(_countsText, "Comida: " + food + "/" + needed +
            "    Boids: " + boids + "    Fantasmas: " + ghosts);

        SetText(_balanceText, "Saldo: $" + _betting.Balance +
            " / Meta: $" + _betting.GoalBalance);
        SetText(_messageText, _message);

        bool placingBet = !_betting.HasBet;
        bool beforeRound = _betting.HasBet && !_betting.IsRunning &&
            !_betting.MatchFinished;
        SetActive(_betEntryGroup, placingBet);
        SetActive(_beforeRoundGroup, beforeRound);
        SetActive(_runningGroup, _betting.IsRunning);
        SetActive(_finishedGroup, _betting.MatchFinished);

        if (beforeRound)
        {
            SetText(_betSummaryText, "Apuesta: $" + _betting.Stake + " a " +
                BettingManager.SideName(_betting.BetSide));
            SetText(_coinPriceText, _coin != null
                ? "Moneda opcional: $" + _coin.Cost : "Moneda no disponible");
            SetActive(_coinChoiceGroup, _coin != null && !_coin.HasFlipped);
            _headsButton.interactable = _betting.CanBuyCoin;
            _tailsButton.interactable = _betting.CanBuyCoin;
            SetText(_coinResultText, _coin != null && _coin.HasFlipped
                ? "Salió " + (_coin.WasHeads ? "cara" : "cruz") +
                  ". Boost para " + BettingManager.SideName(_coin.BoostSide)
                : "");
        }

        if (_betting.IsRunning)
            SetText(_runningText, "Simulación en curso. Apostaste $" +
                _betting.Stake + " a " + BettingManager.SideName(_betting.BetSide) + ".");

        if (_betting.MatchFinished)
        {
            string result = "Ganó " + BettingManager.SideName(_betting.LastWinner) + ".";
            if (_betting.GoalReached)
                result += "\n¡Llegaste a la meta! Ganaste.";
            else if (_betting.Bankrupt)
                result += "\nTe quedaste sin dinero. Fin del juego.";
            SetText(_outcomeText, result);
            SetActive(_nextRoundButton.gameObject,
                !_betting.GoalReached && !_betting.Bankrupt);
        }
    }

    static void SetText(Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value) target.SetActive(value);
    }

    void BetPacman() { PlaceBet(MatchSide.Pacman); }
    void BetGhosts() { PlaceBet(MatchSide.Ghosts); }
    void ChooseHeads() { Flip(true); }
    void ChooseTails() { Flip(false); }

    void PlaceBet(MatchSide side)
    {
        if (!int.TryParse(_amountInput.text, out int amount))
        {
            _message = "Escribí un monto entero.";
            Refresh();
            return;
        }
        _betting.TryPlaceBet(side, amount, out _message);
        Refresh();
    }

    void Flip(bool chooseHeads)
    {
        if (_coin != null) _coin.TryBuyAndFlip(chooseHeads, out _message);
        Refresh();
    }

    void StartRound()
    {
        _betting.TryStartRound(out _message);
        Refresh();
    }

    void NextRound()
    {
        if (!_betting.NextRound())
        {
            _message = "No se puede iniciar otra ronda.";
            Refresh();
        }
    }
}
