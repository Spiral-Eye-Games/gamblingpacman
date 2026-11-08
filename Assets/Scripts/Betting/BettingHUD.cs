using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Vista del Canvas. Traduce los controles a acciones del sistema de apuestas.
public class BettingHUD : MonoBehaviour
{
    [SerializeField, Tooltip("Canvas con los grupos Safe Area/Partida y Safe Area/Apuestas.")]
    Canvas _hudCanvas;

    AIGameManager2D _match;
    BettingManager _betting;
    CoinFlipBooster _coin;

    TMP_Text _matchStateText;
    TMP_Text _phaseText;
    TMP_Text _countsText;
    TMP_Text _balanceText;
    TMP_Text _betSummaryText;
    TMP_Text _coinPriceText;
    TMP_Text _coinResultText;
    TMP_Text _runningText;
    TMP_Text _outcomeText;
    TMP_Text _messageText;
    TMP_InputField _amountInput;

    Button _headsButton;
    Button _tailsButton;
    Button _nextRoundButton;
    Button _retryButton; // aparece al ganar o perder el juego

    GameObject _bettingPanel;
    GameObject _betEntryGroup;
    GameObject _beforeRoundGroup;
    GameObject _coinChoiceGroup;
    GameObject _runningGroup;
    GameObject _finishedGroup;

    string _message = "Elegí un monto y un bando.";
    bool _initialized;

    void Start()
    {
        _match = AIGameManager2D.Instance;
        _betting = BettingManager.Instance;
        if (_betting != null)
            _coin = _betting.GetComponent<CoinFlipBooster>();

        if (_match == null || _betting == null || _hudCanvas == null)
        {
            Debug.LogError("BettingHUD necesita un HUD Canvas, AIGameManager2D y BettingManager en la escena.", this);
            enabled = false;
            return;
        }

        try
        {
            BindUI();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(exception.Message, this);
            enabled = false;
            return;
        }

        _amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _amountInput.characterLimit = 8;
        if (string.IsNullOrEmpty(_amountInput.text)) _amountInput.text = "100";
        _initialized = true;
        ConnectStateEvents();
    }

    // Transform.Find también encuentra hijos de grupos desactivados. Solo se busca al iniciar.
    void BindUI()
    {
        Transform matchPanel = FindUI<RectTransform>(_hudCanvas.transform, "Safe Area/Partida");
        Transform bettingPanel = FindUI<RectTransform>(_hudCanvas.transform, "Safe Area/Apuestas");
        Transform betEntry = FindUI<RectTransform>(bettingPanel, "Elegir apuesta");
        Transform beforeRound = FindUI<RectTransform>(bettingPanel, "Antes de iniciar");
        Transform coinChoice = FindUI<RectTransform>(beforeRound, "Elegir moneda");
        Transform running = FindUI<RectTransform>(bettingPanel, "Ronda en curso");
        Transform finished = FindUI<RectTransform>(bettingPanel, "Ronda terminada");

        _matchStateText = FindUI<TMP_Text>(matchPanel, "Estado");
        _phaseText = FindUI<TMP_Text>(matchPanel, "Cazador actual");
        _countsText = FindUI<TMP_Text>(matchPanel, "Contadores");
        _balanceText = FindUI<TMP_Text>(bettingPanel, "Saldo y meta");
        _betSummaryText = FindUI<TMP_Text>(beforeRound, "Apuesta confirmada");
        _coinPriceText = FindUI<TMP_Text>(beforeRound, "Precio moneda");
        _coinResultText = FindUI<TMP_Text>(beforeRound, "Resultado moneda");
        _runningText = FindUI<TMP_Text>(running, "Texto ronda");
        _outcomeText = FindUI<TMP_Text>(finished, "Resultado");
        _messageText = FindUI<TMP_Text>(bettingPanel, "Mensaje");
        _amountInput = FindUI<TMP_InputField>(betEntry, "Monto");

        _headsButton = FindUI<Button>(coinChoice, "Cara");
        _tailsButton = FindUI<Button>(coinChoice, "Cruz");
        _nextRoundButton = FindUI<Button>(finished, "Siguiente ronda");
        _retryButton = FindUI<Button>(finished, "RETRY");

        _bettingPanel = bettingPanel.gameObject;
        _betEntryGroup = betEntry.gameObject;
        _beforeRoundGroup = beforeRound.gameObject;
        _coinChoiceGroup = coinChoice.gameObject;
        _runningGroup = running.gameObject;
        _finishedGroup = finished.gameObject;
    }

    static T FindUI<T>(Transform root, string path) where T : Component
    {
        Transform child = root.Find(path);
        if (child != null && child.TryGetComponent(out T component)) return component;
        throw new InvalidOperationException(
            $"BettingHUD: falta {typeof(T).Name} en '{root.name}/{path}'. Revisá la jerarquía del HUD Canvas.");
    }

    void OnEnable()
    {
        if (_initialized) ConnectStateEvents();
    }

    void ConnectStateEvents()
    {
        if (_betting != null) _betting.StateChanged += Refresh;
        if (_coin != null) _coin.Changed += RefreshBetting;
        Refresh();
    }

    void OnDisable()
    {
        if (!_initialized) return;

        if (_betting != null) _betting.StateChanged -= Refresh;
        if (_coin != null) _coin.Changed -= RefreshBetting;
    }

    void Update()
    {
        // Los contadores y el rol temporizado pueden cambiar durante la simulación.
        RefreshMatch();
    }

    void Refresh()
    {
        RefreshMatch();
        RefreshBetting();
        if (_betting != null)
            SetActive(_bettingPanel, !_betting.IsRunning);
    }

    void RefreshMatch()
    {
        if (_match == null || _betting == null) return;

        string state = _match.IsFinished
            ? "Ganó " + _match.Winner.DisplayName()
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
            "    Pac-Man: " + boids + "    Fantasmas: " + ghosts);
    }

    void RefreshBetting()
    {
        if (_match == null || _betting == null) return;

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
        if (_retryButton != null)
            SetActive(_retryButton.gameObject, _betting.GoalReached || _betting.Bankrupt);

        if (beforeRound)
        {
            SetText(_betSummaryText, "Apuesta: $" + _betting.Stake + " a " +
                _betting.BetSide.DisplayName());
            SetText(_coinPriceText, _coin != null
                ? "Moneda opcional: $" + _coin.Cost : "Moneda no disponible");
            SetActive(_coinChoiceGroup, _coin != null && !_coin.HasFlipped);
            _headsButton.interactable = _betting.CanBuyCoin;
            _tailsButton.interactable = _betting.CanBuyCoin;
            SetText(_coinResultText, _coin != null && _coin.HasFlipped
                ? "Salió " + (_coin.WasHeads ? "cara" : "cruz") +
                  ". Boost para " + _coin.BoostSide.DisplayName()
                : "");
        }

        if (_betting.IsRunning)
            SetText(_runningText, "Simulación en curso. Apostaste $" +
                _betting.Stake + " a " + _betting.BetSide.DisplayName() + ".");

        if (_betting.MatchFinished)
        {
            string result = "Ganó " + _betting.LastWinner.DisplayName() + ".";
            if (_betting.GoalReached)
                result += "\n¡Llegaste a la meta! Ganaste.";
            else if (_betting.Bankrupt)
                result += "\nTe quedaste sin dinero. Fin del juego.";
            SetText(_outcomeText, result);
            SetActive(_nextRoundButton.gameObject,
                !_betting.GoalReached && !_betting.Bankrupt);
        }
    }

    static void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value) target.SetActive(value);
    }

    // Acciones públicas asignadas en On Click() de cada Button en la escena.
    public void Retry() { _betting.Retry(); }
    public void BetPacman() { PlaceBet(MatchSide.Pacman); }
    public void BetGhosts() { PlaceBet(MatchSide.Ghosts); }
    public void ChooseHeads() { Flip(true); }
    public void ChooseTails() { Flip(false); }

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

    public void StartRound()
    {
        _betting.TryStartRound(out _message);
        Refresh();
    }

    public void NextRound()
    {
        if (!_betting.NextRound())
        {
            _message = "No se puede iniciar otra ronda.";
            Refresh();
        }
    }
}
