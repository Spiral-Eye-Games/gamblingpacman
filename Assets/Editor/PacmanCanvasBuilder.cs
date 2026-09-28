using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Crea una vez la jerarquía editable del HUD; no reemplaza un Canvas personalizado.
public static class PacmanCanvasBuilder
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    static readonly Color PanelColor = new Color(0.04f, 0.07f, 0.12f, 0.91f);
    static readonly Color ButtonColor = new Color(0.10f, 0.34f, 0.53f, 1f);
    static readonly Color AccentColor = new Color(1f, 0.82f, 0.23f, 1f);
    static readonly Color MutedColor = new Color(0.76f, 0.84f, 0.91f, 1f);
    static Font _font;

    [MenuItem("Tools/Pacman/Crear Canvas HUD en esta escena")]
    public static void BuildOpenScene()
    {
        Build(SceneManager.GetActiveScene());
    }

    // Punto de entrada para generar y guardar la escena desde Unity en batchmode.
    public static void BuildSampleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Build(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ValidateSampleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find("HUD Canvas");
        BettingHUD hud = Object.FindFirstObjectByType<BettingHUD>();
        CanvasSafeAreaLayout layout = Object.FindFirstObjectByType<CanvasSafeAreaLayout>();
        if (root == null || hud == null || layout == null ||
            root.GetComponent<Canvas>() == null ||
            root.GetComponent<CanvasScaler>() == null ||
            root.GetComponent<GraphicRaycaster>() == null ||
            Object.FindFirstObjectByType<EventSystem>() == null)
            throw new System.InvalidOperationException("Falta un componente del HUD Canvas.");

        string[] fields = {
            "_match", "_matchStateText", "_phaseText", "_countsText", "_balanceText",
            "_betSummaryText", "_coinPriceText", "_coinResultText", "_runningText",
            "_outcomeText", "_messageText", "_amountInput", "_betPacmanButton",
            "_betGhostsButton", "_headsButton", "_tailsButton", "_startButton",
            "_nextRoundButton", "_betEntryGroup", "_beforeRoundGroup",
            "_coinChoiceGroup", "_runningGroup", "_finishedGroup"
        };
        SerializedObject hudData = new SerializedObject(hud);
        for (int i = 0; i < fields.Length; i++)
        {
            SerializedProperty property = hudData.FindProperty(fields[i]);
            if (property == null || property.objectReferenceValue == null)
                throw new System.InvalidOperationException("HUD sin referencia: " + fields[i]);
        }

        SerializedObject layoutData = new SerializedObject(layout);
        if (layoutData.FindProperty("_matchPanel").objectReferenceValue == null ||
            layoutData.FindProperty("_bettingPanel").objectReferenceValue == null)
            throw new System.InvalidOperationException("Paneles sin diseño adaptable.");

        EditorSceneManager.SaveScene(scene);
        Debug.Log("HUD_CANVAS_VALIDATED: Canvas y todas las referencias correctas.");
    }

    static void Build(Scene scene)
    {
        if (!scene.IsValid()) throw new System.InvalidOperationException("No hay escena activa.");
        BettingHUD hud = Object.FindFirstObjectByType<BettingHUD>();
        AIGameManager2D match = Object.FindFirstObjectByType<AIGameManager2D>();
        if (hud == null || match == null)
            throw new System.InvalidOperationException("Faltan BettingHUD o AIGameManager2D.");

        if (GameObject.Find("HUD Canvas") != null)
        {
            Debug.Log("HUD Canvas ya existe; se conservó su diseño.");
            return;
        }

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null)
            throw new System.InvalidOperationException("No se encontró la fuente integrada de Unity.");

        GameObject canvasObject = new GameObject("HUD Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform safeArea = CreateRect("Safe Area", canvasObject.transform);
        Stretch(safeArea, 0f);

        RectTransform matchPanel = CreatePanel("Partida", safeArea);
        Text matchTitle = CreateText("Titulo", matchPanel, "IA 1 · Pacman autónomo",
            30, 38f, AccentColor, true);
        Text matchState = CreateText("Estado", matchPanel, "Esperando apuesta",
            26, 32f, Color.white);
        Text phase = CreateText("Cazador actual", matchPanel, "Fantasmas cazan",
            24, 30f, AccentColor);
        Text counts = CreateText("Contadores", matchPanel,
            "Comida: 0/1    Boids: 0    Fantasmas: 0", 22, 32f, Color.white);
        Text assignment = CreateText("Consigna del parcial", matchPanel,
            "Consigna: boids con separación, alineación y cohesión; árbol de " +
            "decisión para comida/Arrive, cazador/Evade, grupo y Wander; " +
            "cazador con FSM Rest, Patrol y Hunting/Pursuit.",
            18, 78f, MutedColor);
        assignment.verticalOverflow = VerticalWrapMode.Truncate;

        RectTransform bettingPanel = CreatePanel("Apuestas", safeArea);
        CreateText("Titulo", bettingPanel, "APUESTA", 30, 38f, AccentColor, true);
        Text balance = CreateText("Saldo y meta", bettingPanel,
            "Saldo: $500 / Meta: $1000", 25, 34f, Color.white);

        RectTransform betEntry = CreateGroup("Elegir apuesta", bettingPanel, 240f);
        CreateText("Monto etiqueta", betEntry, "Monto a apostar:",
            22, 30f, MutedColor);
        InputField amount = CreateInput("Monto", betEntry);
        Button betPacman = CreateButton("Apostar a Pacman", betEntry,
            "Apostar a Pacman");
        Button betGhosts = CreateButton("Apostar a Fantasmas", betEntry,
            "Apostar a Fantasmas");

        RectTransform beforeRound = CreateGroup("Antes de iniciar", bettingPanel, 330f);
        Text betSummary = CreateText("Apuesta confirmada", beforeRound,
            "Apuesta confirmada", 23, 40f, Color.white);
        Text coinPrice = CreateText("Precio moneda", beforeRound,
            "Moneda opcional: $100", 21, 30f, MutedColor);
        RectTransform coinChoice = CreateGroup("Elegir moneda", beforeRound, 110f);
        Button heads = CreateButton("Cara", coinChoice, "Elegir cara");
        Button tails = CreateButton("Cruz", coinChoice, "Elegir cruz");
        Text coinResult = CreateText("Resultado moneda", beforeRound,
            "", 20, 42f, AccentColor);
        Button start = CreateButton("Iniciar ronda", beforeRound, "Iniciar ronda");

        RectTransform running = CreateGroup("Ronda en curso", bettingPanel, 80f);
        Text runningText = CreateText("Texto ronda", running,
            "La simulación está en curso...", 22, 72f, Color.white);

        RectTransform finished = CreateGroup("Ronda terminada", bettingPanel, 170f);
        Text outcome = CreateText("Resultado", finished, "Ganó ...",
            24, 100f, Color.white);
        Button next = CreateButton("Siguiente ronda", finished, "Siguiente ronda");

        Text message = CreateText("Mensaje", bettingPanel,
            "Elegí un monto y un bando.", 19, 70f, MutedColor);

        CanvasSafeAreaLayout responsive = safeArea.gameObject.AddComponent<CanvasSafeAreaLayout>();
        SerializedObject layoutData = new SerializedObject(responsive);
        Assign(layoutData, "_matchPanel", matchPanel);
        Assign(layoutData, "_bettingPanel", bettingPanel);
        layoutData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject hudData = new SerializedObject(hud);
        Assign(hudData, "_match", match);
        Assign(hudData, "_matchStateText", matchState);
        Assign(hudData, "_phaseText", phase);
        Assign(hudData, "_countsText", counts);
        Assign(hudData, "_balanceText", balance);
        Assign(hudData, "_betSummaryText", betSummary);
        Assign(hudData, "_coinPriceText", coinPrice);
        Assign(hudData, "_coinResultText", coinResult);
        Assign(hudData, "_runningText", runningText);
        Assign(hudData, "_outcomeText", outcome);
        Assign(hudData, "_messageText", message);
        Assign(hudData, "_amountInput", amount);
        Assign(hudData, "_betPacmanButton", betPacman);
        Assign(hudData, "_betGhostsButton", betGhosts);
        Assign(hudData, "_headsButton", heads);
        Assign(hudData, "_tailsButton", tails);
        Assign(hudData, "_startButton", start);
        Assign(hudData, "_nextRoundButton", next);
        Assign(hudData, "_betEntryGroup", betEntry.gameObject);
        Assign(hudData, "_beforeRoundGroup", beforeRound.gameObject);
        Assign(hudData, "_coinChoiceGroup", coinChoice.gameObject);
        Assign(hudData, "_runningGroup", running.gameObject);
        Assign(hudData, "_finishedGroup", finished.gameObject);
        hudData.ApplyModifiedPropertiesWithoutUndo();

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("HUD_CANVAS_CREATED: paneles, controles y referencias conectados.");
    }

    static void Assign(SerializedObject data, string field, Object value)
    {
        SerializedProperty property = data.FindProperty(field);
        if (property == null) throw new System.InvalidOperationException("Falta campo " + field);
        property.objectReferenceValue = value;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    static RectTransform CreatePanel(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = false;
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    static RectTransform CreateGroup(string name, Transform parent, float height)
    {
        RectTransform rect = CreateRect(name, parent);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    static Text CreateText(string name, Transform parent, string value,
        int size, float height, Color color, bool bold = false)
    {
        RectTransform rect = CreateRect(name, parent);
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = _font;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.color = color;
        label.text = value;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return label;
    }

    static Button CreateButton(string name, Transform parent, string caption)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
        Text label = CreateText("Texto", rect, caption, 22, 50f, Color.white, true);
        RectTransform textRect = label.rectTransform;
        Stretch(textRect, 8f);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    static InputField CreateInput(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        InputField input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.contentType = InputField.ContentType.IntegerNumber;
        input.characterLimit = 8;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
        Text value = CreateText("Valor", rect, "", 23, 50f, Color.black);
        Stretch(value.rectTransform, 10f);
        Text placeholder = CreateText("Ejemplo", rect, "100", 23, 50f,
            new Color(0.45f, 0.45f, 0.45f, 1f));
        Stretch(placeholder.rectTransform, 10f);
        input.textComponent = value;
        input.placeholder = placeholder;
        input.text = "100";
        return input;
    }
}
