using System;
using System.Collections; 
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PokerSocial.Data;
using PokerSocial.Systems;

namespace PokerSocial.Core
{
    public class PokerTableView : MonoBehaviour
    {
        [SerializeField] private int playerCount = 4;
        [SerializeField] private int startingChips = 1000;
        [SerializeField] private int ante = 10;

        private GameObject startMenuPanel;
        private InputField nameInputField;

        private RectTransform tableFelt;
        private RectTransform deckAnchor;
        private RectTransform communityAnchor;
        private Text potText;
        private Text repText;
        private Text logText;
        private Transform actionPanel;
        private GameObject logPanelGO;
        private int buttonPlacementIndex = 0;

        private readonly List<PlayerData> players = new();
        private readonly Dictionary<string, List<CardView>> seatCards = new();
        private readonly List<CardView> communityCardViews = new();
        private readonly List<Card> communityCards = new();
        private readonly Dictionary<string, RectTransform> seatAnchors = new();
        private readonly Dictionary<string, bool> stillIn = new();
        private readonly Dictionary<string, int> currentBets = new();

        private TableState table;
        private readonly ReadSystem readSystem = new();
        private readonly AllianceSystem allianceSystem = new();
        private readonly TellSystem tellSystem = new();
        private readonly ReputationManager reputationManager = new();
        private LLMOpponentBrain llmBrain;

        private int pot;
        private int highestBet;
        private bool waitingForHumanDecision;
        private bool humanFolded;
        private string playerName = "Player";

        private Button foldBtn, callCheckBtn, halfPotBtn, potBtn, allInBtn, bluffBtn, strongBtn, allianceBtn, tellBtn, newHandBtn;
        private Text callCheckText;

        private CloudBallerTracker cloudManager;

        private Sprite customCardBack;
        private Sprite customTableFelt;
        private Sprite customBackground;

        private void LoadCustomTextures()
        {
            customCardBack = Resources.Load<Sprite>("CardBack");
            customTableFelt = Resources.Load<Sprite>("TableFelt");
            customBackground = Resources.Load<Sprite>("Background");
        }

        private async void Start()
        {
            llmBrain = GetComponent<LLMOpponentBrain>();

            var cloudTracker = FindAnyObjectByType<CloudBallerTracker>();
            if (cloudTracker == null)
            {
                cloudTracker = gameObject.AddComponent<CloudBallerTracker>();
            }

            cloudManager = cloudTracker;
            startingChips = await cloudManager.LoadChipsAsync(1000);

            BuildUI();
        }

        private async void OnHandComplete(int newChipTotal)
        {
            await cloudManager.SaveChipsAsync(newChipTotal);
        }

        private void BuildUI()
        {
            // 1. Canvas Setup
            var canvasGO = new GameObject("PokerCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvasGO.AddComponent<GraphicRaycaster>();

            LoadCustomTextures();

            // 2. Background
            if (customBackground != null)
            {
                var bgGO = new GameObject("Background");
                bgGO.transform.SetParent(canvasGO.transform, false);
                bgGO.transform.SetAsFirstSibling();

                var bgImg = bgGO.AddComponent<Image>();
                bgImg.sprite = customBackground;

                var bgRect = bgGO.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
            }

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // 3. Table Felt Panel
            tableFelt = CreatePanel(canvasGO.transform, Vector2.zero, new Vector2(880, 480), new Color(0, 0, 0, 0));

            if (customTableFelt != null)
            {
                var feltImg = tableFelt.GetComponent<Image>();
                feltImg.sprite = customTableFelt;
                feltImg.color = Color.white;
            }

            // 4. Anchors & Dedicated Layout Slots
            deckAnchor = CreateAnchor(tableFelt, new Vector2(-360, 0));
            communityAnchor = CreateAnchor(tableFelt, new Vector2(0, 0));

            // Fixed, non-overlapping 4-player seat positions
            seatAnchors["player_bottom"] = CreateAnchor(tableFelt, new Vector2(0, -155));
            seatAnchors["player_top_left"] = CreateAnchor(tableFelt, new Vector2(-190, 140));
            seatAnchors["player_top_right"] = CreateAnchor(tableFelt, new Vector2(190, 140));
            seatAnchors["player_left"] = CreateAnchor(tableFelt, new Vector2(-310, 0));

            // Header UI: Centered Pot & Isolated Reputation Bar
            potText = CreateText(tableFelt, PokerTableLayoutConfig.PotTextPos, PokerTableLayoutConfig.PotTextSize, 24, "Pot: 0");

            // Dedicated Info Bar positioned cleanly above community space
            var infoBar = CreatePanel(tableFelt, new Vector2(0, 175), new Vector2(480, 24), new Color(0, 0, 0, 0.4f));
            repText = CreateText(infoBar, Vector2.zero, new Vector2(460, 20), 13, "Table reputation: 1.00  |  Stakes cap: 1000");

            // 5. Side Panels: Control Buttons (Snapped to Right Edge of Table Felt)
            var actionPanelGO = new GameObject("ActionPanel");
            actionPanelGO.transform.SetParent(canvasGO.transform, false);
            var actionImg = actionPanelGO.AddComponent<Image>();
            actionImg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

            var actionRect = actionPanelGO.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(0.5f, 0.5f); // Center anchor to match table
            actionRect.anchorMax = new Vector2(0.5f, 0.5f);
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            actionRect.anchoredPosition = PokerTableLayoutConfig.ActionPanelPos;
            actionRect.sizeDelta = new Vector2(PokerTableLayoutConfig.ActionPanelWidth, 480f); // Matches table felt height

            actionPanel = actionRect;

            buttonPlacementIndex = 0;
            foldBtn = CreateButton(actionPanel, "Fold", OnFoldClicked);
            callCheckBtn = CreateButton(actionPanel, "Check", OnCallCheckClicked);
            callCheckText = callCheckBtn.GetComponentInChildren<Text>();

            halfPotBtn = CreateButton(actionPanel, "Raise 1/2 Pot", OnHalfPotClicked);
            potBtn = CreateButton(actionPanel, "Raise Pot", OnPotClicked);
            allInBtn = CreateButton(actionPanel, "ALL-IN!", OnAllInClicked);

            bluffBtn = CreateButton(actionPanel, "Call Bluff (Seat 2)", OnBluffClicked);
            strongBtn = CreateButton(actionPanel, "Call Strong (Seat 2)", OnStrongClicked);
            allianceBtn = CreateButton(actionPanel, "Alliance (Seat 3)", OnAllianceClicked);
            tellBtn = CreateButton(actionPanel, "Broadcast Fake Tell", OnTellClicked);

            newHandBtn = CreateButton(actionPanel, "Next Hand", OnNewHandClicked);

            // 6. Side Panels: Event Log (Snapped to Left Edge of Table Felt)
            var logPanelGO = new GameObject("LogPanel");
            logPanelGO.transform.SetParent(canvasGO.transform, false);
            var logImg = logPanelGO.AddComponent<Image>();
            logImg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

            var logPanelRect = logPanelGO.GetComponent<RectTransform>();
            logPanelRect.anchorMin = new Vector2(0.5f, 0.5f); // Center anchor to match table
            logPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            logPanelRect.pivot = new Vector2(0.5f, 0.5f);
            logPanelRect.anchoredPosition = PokerTableLayoutConfig.LogPanelPos;
            logPanelRect.sizeDelta = new Vector2(PokerTableLayoutConfig.LogPanelWidth, 480f); // Matches table felt height
            this.logPanelGO = logPanelGO;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(logPanelGO.transform, false);
            var vpRect = viewportGO.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = new Vector2(-20, -20);

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var cRect = contentGO.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 0);
            cRect.anchorMax = new Vector2(1, 0);
            cRect.pivot = new Vector2(0, 0);
            cRect.sizeDelta = Vector2.zero;

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            logText = CreateText(contentGO.transform, Vector2.zero, Vector2.zero, PokerTableLayoutConfig.LogFontSize, "");
            logText.alignment = TextAnchor.LowerLeft;

            var textLayout = logText.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;

            // 7. Initial State Setup
            tableFelt.gameObject.SetActive(false);
            actionPanel.gameObject.SetActive(false);
            logPanelGO.SetActive(false);
            infoBar.gameObject.SetActive(false);

            // 8. Start Menu Overlay
            BuildStartMenu(canvasGO.transform);
        }

        private void BuildStartMenu(Transform parent)
        {
            // 1. Center Panel
            var menuPanelRect = CreatePanel(parent, Vector2.zero, PokerTableLayoutConfig.StartMenuSize, new Color(0.12f, 0.12f, 0.15f, 0.98f));
            startMenuPanel = menuPanelRect.gameObject;

            menuPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            menuPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuPanelRect.pivot = new Vector2(0.5f, 0.5f);
            menuPanelRect.anchoredPosition = Vector2.zero;

            // 2. Title Text
            var headerText = CreateText(startMenuPanel.transform, new Vector2(0, 80), new Vector2(400, 40), 22, "WELCOME TO VIBING");
            headerText.alignment = TextAnchor.MiddleCenter;

            // 3. Subtitle
            var labelText = CreateText(startMenuPanel.transform, new Vector2(0, 35), new Vector2(400, 30), 15, "Enter Your Name:");
            labelText.alignment = TextAnchor.MiddleCenter;

            // 4. Input Field Container
            var inputGO = new GameObject("NameInputField");
            inputGO.transform.SetParent(startMenuPanel.transform, false);

            var inputImg = inputGO.AddComponent<Image>();
            inputImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var inputRect = inputGO.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.5f, 0.5f);
            inputRect.anchorMax = new Vector2(0.5f, 0.5f);
            inputRect.pivot = new Vector2(0.5f, 0.5f);
            inputRect.anchoredPosition = new Vector2(0, -10);
            inputRect.sizeDelta = new Vector2(280, 40);

            nameInputField = inputGO.AddComponent<InputField>();

            // Input Field Text Child
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(inputGO.transform, false);
            var inputText = textGO.AddComponent<Text>();
            inputText.font = GetDefaultFont(16);
            inputText.fontSize = 16;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleCenter;

            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(10, 0);
            tRect.offsetMax = new Vector2(-10, 0);

            nameInputField.textComponent = inputText;

            // 5. Join Button (Get RectTransform from the returned Button component)
            Button joinBtn = CreateButton(startMenuPanel.transform, "Join Table", OnJoinGameClicked);
            RectTransform joinBtnRect = joinBtn.GetComponent<RectTransform>();
            joinBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
            joinBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
            joinBtnRect.pivot = new Vector2(0.5f, 0.5f);
            joinBtnRect.anchoredPosition = new Vector2(0, -75);
            joinBtnRect.sizeDelta = new Vector2(200, 40);
        }

        private void OnJoinGameClicked()
        {
            if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text.Trim()))
            {
                playerName = nameInputField.text.Trim();
            }

            if (startMenuPanel != null) startMenuPanel.SetActive(false);
            if (tableFelt != null) tableFelt.gameObject.SetActive(true);
            if (actionPanel != null) actionPanel.gameObject.SetActive(true);
            if (logPanelGO != null) logPanelGO.SetActive(true);
            if (repText != null && repText.transform.parent != null) repText.transform.parent.gameObject.SetActive(true);

            SetupPlayers();
            table = new TableState
            {
                tableId = "table-1",
                tableName = "Table 1"
            };
            table.seatedPlayers = players;

            WireLogging();
            StartCoroutine(RunHand());
        }

        private void WireLogging()
        {
            readSystem.OnReadDeclared += r =>
            {
                var reader = players.First(p => p.playerId == r.readerId);
                var target = players.First(p => p.playerId == r.targetId);
                Log($"{reader.displayName} calls {(r.claimedBluff ? "BLUFF" : "STRONG")} on {target.displayName}");
            };
            allianceSystem.OnWhisperDetected += Log;
            tellSystem.OnTellBroadcast += (id, tell) =>
                Log($"{players.First(p => p.playerId == id).displayName} broadcasts a fake tell: {tell}");
            reputationManager.OnReputationChanged += (t, rep) =>
                repText.text = $"Table reputation: {rep:F2}  |  Stakes cap: {t.stakesCap}";
        }

        private RectTransform CreatePanel(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform CreateAnchor(Transform parent, Vector2 localPos)
        {
            var go = new GameObject("Anchor");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = localPos;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private Text CreateText(Transform parent, Vector2 pos, Vector2 size, int fontSize, string content)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = GetDefaultFont(fontSize);
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return text;
        }

        private Font GetDefaultFont(int fontSize)
        {
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            return defaultFont;
        }

        private Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var btnGO = new GameObject("Btn_" + text);
            btnGO.transform.SetParent(parent, false);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f, 1f);

            var btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            // Anchor to top-center of the parent panel
            var rect = btnGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            // Stack downwards starting from top padding
            float yOffset = -PokerTableLayoutConfig.ButtonTopPadding - (buttonPlacementIndex * PokerTableLayoutConfig.ButtonVerticalSpacing);
            rect.anchoredPosition = new Vector2(0, yOffset);
            rect.sizeDelta = PokerTableLayoutConfig.ButtonSize;

            buttonPlacementIndex++;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var txt = textGO.AddComponent<Text>();
            txt.font = GetDefaultFont(13);
            txt.fontSize = 13;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;

            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            return btn;
        }

        private void SetupPlayers()
        {
            string[] defaultBotNames = { "Bob", "Carla", "Dev" };
            players.Clear();

            players.Add(new PlayerData("p0", playerName, startingChips));

            for (int i = 1; i < playerCount; i++)
            {
                players.Add(new PlayerData($"p{i}", defaultBotNames[(i - 1) % defaultBotNames.Length], startingChips));
            }

            string[] seatKeys = { "player_bottom", "player_top_left", "player_top_right", "player_left" };

            for (int i = 0; i < players.Count; i++)
            {
                string key = seatKeys[i % seatKeys.Length];
                RectTransform anchor = seatAnchors[key];

                seatAnchors[players[i].playerId] = anchor;

                var oldLabel = anchor.Find("SeatLabel_" + players[i].playerId);
                if (oldLabel != null) Destroy(oldLabel.gameObject);

                Vector2 labelOffset = (key == "player_bottom") ? new Vector2(0, 52) : new Vector2(0, -52);
                var nameText = CreateText(anchor, labelOffset, new Vector2(180, 22), 14,
                    $"{players[i].displayName} ({players[i].chipStack})");
                nameText.gameObject.name = "SeatLabel_" + players[i].playerId;
            }
        }

        private IEnumerator RunHand()
        {
            Log("=== NEW HAND (Texas Hold 'em) ===");
            newHandBtn.gameObject.SetActive(false);
            ClearTable();

            pot = 0;
            highestBet = 0;

            foreach (var p in players)
            {
                stillIn[p.playerId] = true;
                currentBets[p.playerId] = 0;
                int a = Mathf.Min(ante, p.chipStack);
                p.chipStack -= a;
                pot += a;
                UpdateSeatLabel(p);
            }
            allianceSystem.ResetSlotsForNewHand();
            UpdatePotText();

            var deck = new Deck();

            yield return StartCoroutine(DealHoleCards(deck));
            foreach (var cv in seatCards[players[0].playerId])
                yield return StartCoroutine(cv.FlipRoutine());

            yield return StartCoroutine(PlayerActionRound("Pre-Flop"));
            if (CountActive() <= 1 || humanFolded) { yield return StartCoroutine(Showdown()); yield break; }

            ResetRoundBets();
            yield return StartCoroutine(DealCommunityCards(deck, 3));
            yield return StartCoroutine(PlayerActionRound("Flop"));
            if (CountActive() <= 1 || humanFolded) { yield return StartCoroutine(Showdown()); yield break; }

            ResetRoundBets();
            yield return StartCoroutine(DealCommunityCards(deck, 1));
            yield return StartCoroutine(PlayerActionRound("Turn"));
            if (CountActive() <= 1 || humanFolded) { yield return StartCoroutine(Showdown()); yield break; }

            ResetRoundBets();
            yield return StartCoroutine(DealCommunityCards(deck, 1));
            yield return StartCoroutine(PlayerActionRound("River"));

            yield return StartCoroutine(Showdown());
        }

        private void ResetRoundBets()
        {
            highestBet = 0;
            foreach (var p in players) currentBets[p.playerId] = 0;
        }

        private IEnumerator PlayerActionRound(string roundName)
        {
            Log($"-- Action Phase: {roundName} --");
            var human = players[0];

            if (stillIn[human.playerId])
            {
                int toCall = highestBet - currentBets[human.playerId];
                callCheckText.text = toCall > 0 ? $"Call ({toCall})" : "Check";

                SetActionButtonsInteractable(true);
                humanFolded = false;
                waitingForHumanDecision = true;

                yield return new WaitUntil(() => !waitingForHumanDecision);
                SetActionButtonsInteractable(false);
            }

            if (!humanFolded)
            {
                yield return StartCoroutine(OpponentDecisions());
            }
        }

        private IEnumerator DealHoleCards(Deck deck)
        {
            foreach (var p in players)
                seatCards[p.playerId] = new List<CardView>();

            for (int round = 0; round < 2; round++)
            {
                foreach (var p in players)
                {
                    var card = deck.Draw();
                    var cv = CardView.Create(tableFelt, new Vector2(55, 78));
                    cv.Setup(card);

                    cv.SetFaceDown(customCardBack);
                    cv.Rect.anchoredPosition = deckAnchor.anchoredPosition;

                    var seat = seatAnchors[p.playerId];
                    int idx = seatCards[p.playerId].Count;
                    Vector2 offset = new Vector2((idx - 0.5f) * 36f, 0);
                    Vector2 target = seat.anchoredPosition + offset;

                    seatCards[p.playerId].Add(cv);
                    yield return StartCoroutine(cv.MoveRoutine(target, 0.12f));
                }
            }
        }

        private IEnumerator DealCommunityCards(Deck deck, int count)
        {
            const float GAP_SPACING = 10f;
            const float CARD_WIDTH = 55f;

            for (int i = 0; i < count; i++)
            {
                var card = deck.Draw();
                communityCards.Add(card);

                var cv = CardView.Create(tableFelt, new Vector2(CARD_WIDTH, 78));
                cv.Setup(card);

                cv.SetFaceDown(customCardBack);
                cv.Rect.anchoredPosition = deckAnchor.anchoredPosition;

                int idx = communityCardViews.Count;
                float xPos = (idx - 2) * (CARD_WIDTH + GAP_SPACING);
                Vector2 target = communityAnchor.anchoredPosition + new Vector2(xPos, 0);

                communityCardViews.Add(cv);
                yield return StartCoroutine(cv.MoveRoutine(target, 0.15f));
                yield return StartCoroutine(cv.FlipRoutine());
            }
        }

        private IEnumerator OpponentDecisions()
        {
            foreach (var p in players)
            {
                if (p.playerId == players[0].playerId || !stillIn[p.playerId]) continue;

                var fullHand = seatCards[p.playerId].Select(cv => cv.Card).Concat(communityCards).ToList();

                string handDesc = fullHand.Count >= 5
                    ? HandEvaluator.Evaluate(fullHand).description
                    : "Pre-flop / Early round";

                int callAmount = highestBet - currentBets[p.playerId];

                if (llmBrain != null)
                {
                    bool done = false;
                    LLMOpponentBrain.Decision decision = null;
                    llmBrain.RequestDecision(p.displayName, handDesc, pot, p.chipStack, d => { decision = d; done = true; });
                    yield return new WaitUntil(() => done);

                    if (decision != null && decision.action == "fold" && callAmount > 0)
                    {
                        stillIn[p.playerId] = false;
                        Log($"{p.displayName} folds.");
                        continue;
                    }
                    else if (decision != null && !string.IsNullOrEmpty(decision.tableTalk))
                    {
                        Log($"{p.displayName}: \"{decision.tableTalk}\"");
                    }
                }

                if (callAmount > 0)
                {
                    int paid = Mathf.Min(callAmount, p.chipStack);
                    p.chipStack -= paid;
                    currentBets[p.playerId] += paid;
                    pot += paid;
                    Log($"{p.displayName} calls {paid}.");
                    StartCoroutine(ShowSpeechBubble(p.playerId, $"Call {paid}"));
                }
                else
                {
                    Log($"{p.displayName} checks.");
                    StartCoroutine(ShowSpeechBubble(p.playerId, "Check"));
                }

                UpdateSeatLabel(p);
                UpdatePotText();
            }
        }

        private IEnumerator Showdown()
        {
            Log("\n--- SHOWDOWN ---");
            foreach (var p in players)
            {
                if (!stillIn[p.playerId] || p.playerId == players[0].playerId) continue;
                foreach (var cv in seatCards[p.playerId])
                    yield return StartCoroutine(cv.FlipRoutine());
            }

            PlayerData winner = null;
            HandEvaluation bestEval = default;
            foreach (var p in players)
            {
                if (!stillIn[p.playerId]) continue;

                var fullHand = seatCards[p.playerId].Select(cv => cv.Card).Concat(communityCards).ToList();

                var eval = fullHand.Count >= 5
                    ? HandEvaluator.Evaluate(fullHand)
                    : new HandEvaluation { rank = HandRank.HighCard, description = "Incomplete Hand" };

                Log($"{p.displayName}: {eval.description}");
                if (winner == null || HandEvaluator.Compare(eval, bestEval) > 0)
                {
                    winner = p;
                    bestEval = eval;
                }
            }

            if (winner != null)
            {
                winner.chipStack += pot;
                Log($"\nWINNER: {winner.displayName} takes pot of {pot} with {bestEval.description}");
                OnHandComplete(winner.chipStack);
            }

            foreach (var p in players) UpdateSeatLabel(p);
            newHandBtn.gameObject.SetActive(true);
        }

        private void ClearTable()
        {
            foreach (var kvp in seatCards)
                foreach (var cv in kvp.Value)
                    if (cv != null) Destroy(cv.gameObject);
            seatCards.Clear();

            foreach (var cv in communityCardViews)
                if (cv != null) Destroy(cv.gameObject);
            communityCardViews.Clear();
            communityCards.Clear();
        }

        private int CountActive() => stillIn.Values.Count(v => v);

        private void OnFoldClicked()
        {
            stillIn[players[0].playerId] = false;
            humanFolded = true;
            Log("You folded.");
            waitingForHumanDecision = false;
        }

        private void OnCallCheckClicked()
        {
            var human = players[0];
            int toCall = highestBet - currentBets[human.playerId];
            if (toCall > 0)
            {
                int paid = Mathf.Min(toCall, human.chipStack);
                human.chipStack -= paid;
                currentBets[human.playerId] += paid;
                pot += paid;
                Log($"You call {paid}.");
            }
            else
            {
                Log("You check.");
            }
            UpdateSeatLabel(human);
            UpdatePotText();
            waitingForHumanDecision = false;
        }

        private void OnAllInClicked()
        {
            var human = players[0];
            if (human.chipStack <= 0) return;

            int allInAmount = human.chipStack;
            human.chipStack = 0;
            currentBets[human.playerId] += allInAmount;
            pot += allInAmount;

            if (currentBets[human.playerId] > highestBet)
            {
                highestBet = currentBets[human.playerId];
            }

            Log($"ALL-IN! You pushed your entire stack ({allInAmount} chips) into the pot!");
            UpdateSeatLabel(human);
            UpdatePotText();
            waitingForHumanDecision = false;
        }

        private void OnBluffClicked() => DeclareReadOnSeatTwo(claimingBluff: true);
        private void OnStrongClicked() => DeclareReadOnSeatTwo(claimingBluff: false);

        private void DeclareReadOnSeatTwo(bool claimingBluff)
        {
            if (players.Count < 2) return;
            try
            {
                var reader = players[0];
                var target = players[1];
                var read = readSystem.DeclareRead(reader, target, claimingBluff);
                var fullHand = seatCards[target.playerId].Select(cv => cv.Card).Concat(communityCards).ToList();

                bool actuallyBluffing = fullHand.Count >= 5 && HandEvaluator.Evaluate(fullHand).rank <= HandRank.Pair;
                readSystem.ResolveRead(read, reader, actuallyBluffing);
                Log($"Read was {(read.wasCorrect ? "CORRECT" : "WRONG")} — {reader.displayName}'s accuracy now {reader.ReadAccuracy:P0}");
                UpdateSeatLabel(reader);
            }
            catch (Exception e) { Log(e.Message); }
        }

        private void OnAllianceClicked()
        {
            if (players.Count < 3) return;
            try
            {
                allianceSystem.FormAlliance(players[0], players[2], 20, table);
                UpdateSeatLabel(players[0]);
                UpdateSeatLabel(players[2]);
            }
            catch (Exception e) { Log(e.Message); }
        }

        private void OnTellClicked()
        {
            try
            {
                var handRecord = new HandRecord { handId = "current" };
                tellSystem.BroadcastFakeTell(players[0], TellType.FoldAnimationWhileNotFolding, table, handRecord);
                UpdateSeatLabel(players[0]);
            }
            catch (Exception e) { Log(e.Message); }
        }

        private void OnNewHandClicked() => StartCoroutine(RunHand());

        private void SetActionButtonsInteractable(bool value)
        {
            foldBtn.interactable = value;
            callCheckBtn.interactable = value;
            halfPotBtn.interactable = value && players[0].chipStack > 0;
            potBtn.interactable = value && players[0].chipStack > 0;
            allInBtn.interactable = value && players[0].chipStack > 0;

            if (bluffBtn != null) bluffBtn.interactable = value;
            if (strongBtn != null) strongBtn.interactable = value;
            if (allianceBtn != null) allianceBtn.interactable = value;
            if (tellBtn != null) tellBtn.interactable = value;
        }

        private void UpdateSeatLabel(PlayerData p)
        {
            var labelTransform = seatAnchors[p.playerId].Find("SeatLabel_" + p.playerId);
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label != null) label.text = $"{p.displayName} ({p.chipStack})";
        }

        private void UpdatePotText() => potText.text = $"Pot: {pot}";

        private Queue<string> logHistory = new Queue<string>();
        private const int MAX_LOG_LINES = 25;

        private void Log(string message)
        {
            Debug.Log(message);

            logHistory.Enqueue(message);

            while (logHistory.Count > MAX_LOG_LINES)
            {
                logHistory.Dequeue();
            }

            if (logText != null)
            {
                logText.text = string.Join("\n", logHistory);
            }

            StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return new WaitForEndOfFrame();
            if (logText != null && logText.rectTransform.parent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(logText.rectTransform.parent as RectTransform);
            }
        }

        private void OnHalfPotClicked()
        {
            var human = players[0];
            int toCall = highestBet - currentBets[human.playerId];
            int currentPotWithCall = pot + toCall;

            int raiseSize = Mathf.Max(50, currentPotWithCall / 2);
            ExecuteBet(toCall + raiseSize);
        }

        private void OnPotClicked()
        {
            var human = players[0];
            int toCall = highestBet - currentBets[human.playerId];
            int currentPotWithCall = pot + toCall;

            int raiseSize = Mathf.Max(100, currentPotWithCall);
            ExecuteBet(toCall + raiseSize);
        }

        private void ExecuteBet(int totalBetAmount)
        {
            var human = players[0];
            int neededChips = totalBetAmount - currentBets[human.playerId];

            if (neededChips >= human.chipStack)
            {
                OnAllInClicked();
                return;
            }

            human.chipStack -= neededChips;
            currentBets[human.playerId] += neededChips;
            pot += neededChips;
            highestBet = currentBets[human.playerId];

            Log($"You raised to {highestBet} chips.");
            UpdateSeatLabel(human);
            UpdatePotText();
            waitingForHumanDecision = false;
        }

        private IEnumerator ShowSpeechBubble(string playerId, string text, float duration = 3.5f)
        {
            if (!seatAnchors.TryGetValue(playerId, out var anchor)) yield break;

            var existingBubble = anchor.Find("SpeechBubble_" + playerId);
            if (existingBubble != null) Destroy(existingBubble.gameObject);

            Vector2 bubbleOffset = (playerId == players[0].playerId) ? new Vector2(0, 80) : new Vector2(0, -80);
            var bubblePanel = CreatePanel(anchor, bubbleOffset, new Vector2(120, 32), new Color(0.05f, 0.05f, 0.08f, 0.9f));
            bubblePanel.gameObject.name = "SpeechBubble_" + playerId;

            var bubbleText = CreateText(bubblePanel, Vector2.zero, new Vector2(110, 24), 12, text);
            bubbleText.alignment = TextAnchor.MiddleCenter;

            yield return new WaitForSeconds(duration);

            if (bubblePanel != null)
            {
                Destroy(bubblePanel.gameObject);
            }
        }
    }
}