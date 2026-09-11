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
        private ScrollRect scrollRect;
        private Transform actionPanel;
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
        private int humanRaiseAmount = 50;
        private string playerName = "Player";

        private Button foldBtn, callCheckBtn, raiseBtn, bluffBtn, strongBtn, allianceBtn, tellBtn, newHandBtn;
        private Text callCheckText;

        private CloudBallerTracker cloudManager;

        private async void Start()
        {
            llmBrain = GetComponent<LLMOpponentBrain>();

            // 1. Check if manager exists on this GameObject or ANY object in the scene
            var cloudTracker = FindObjectOfType<CloudBallerTracker>();

            // 2. Only add it dynamically if it doesn't exist anywhere in the scene
            if (cloudTracker == null)
            {
                cloudTracker = gameObject.AddComponent<CloudBallerTracker>();
            }

            // 3. Load chips after ensuring a single instance is used
            startingChips = await cloudTracker.LoadChipsAsync(1000);

            BuildUI();
        }

        // Call whenever a hand finishes
        private async void OnHandComplete(int newChipTotal)
        {
            await cloudManager.SaveChipsAsync(newChipTotal);
        }

        private void BuildUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            var canvasGO = new GameObject("PokerCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvasGO.AddComponent<GraphicRaycaster>();

            // --- TABLE UI (Hidden initially) ---
            tableFelt = CreatePanel(canvasGO.transform, Vector2.zero, new Vector2(1100, 550), new Color(0.1f, 0.4f, 0.2f, 1f));
            deckAnchor = CreateAnchor(tableFelt, new Vector2(-250, 0));
            communityAnchor = CreateAnchor(tableFelt, new Vector2(0, 0));

            potText = CreateText(tableFelt, new Vector2(0, -120), new Vector2(300, 40), 28, "Pot: 0");
            repText = CreateText(canvasGO.transform, new Vector2(0, -420), new Vector2(800, 30), 18, "Table reputation: 1.00  |  Stakes cap: 1000");

            actionPanel = CreatePanel(canvasGO.transform, new Vector2(600, 0), new Vector2(300, 420), new Color(0.15f, 0.15f, 0.18f, 0.95f));

            buttonPlacementIndex = 0;
            foldBtn = CreateButton(actionPanel, "Fold", OnFoldClicked);
            callCheckBtn = CreateButton(actionPanel, "Check", OnCallCheckClicked);
            callCheckText = callCheckBtn.GetComponentInChildren<Text>();
            raiseBtn = CreateButton(actionPanel, "Raise (+50)", OnRaiseClicked);
            bluffBtn = CreateButton(actionPanel, "Call Bluff (Seat 2)", OnBluffClicked);
            strongBtn = CreateButton(actionPanel, "Call Strong (Seat 2)", OnStrongClicked);
            allianceBtn = CreateButton(actionPanel, "Alliance (Seat 3)", OnAllianceClicked);
            tellBtn = CreateButton(actionPanel, "Broadcast Fake Tell", OnTellClicked);
            newHandBtn = CreateButton(actionPanel, "Next Hand", OnNewHandClicked);

            // Log Panel Setup
            var logPanel = CreatePanel(canvasGO.transform, new Vector2(-600, 0), new Vector2(320, 480), new Color(0.15f, 0.15f, 0.18f, 0.95f));
            var scrollGO = new GameObject("ScrollRect");
            scrollGO.transform.SetParent(logPanel, false);
            var srRect = scrollGO.AddComponent<RectTransform>();
            srRect.anchorMin = Vector2.zero;
            srRect.anchorMax = Vector2.one;
            srRect.sizeDelta = new Vector2(-20, -20);

            scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.scrollSensitivity = 45f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollGO.AddComponent<RectMask2D>();

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(scrollGO.transform, false);
            var cRect = contentGO.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0, 1);
            cRect.anchorMax = new Vector2(1, 1);
            cRect.pivot = new Vector2(0, 1);
            cRect.sizeDelta = new Vector2(0, 0);

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            logText = CreateText(contentGO.transform, Vector2.zero, Vector2.zero, 14, "");
            logText.alignment = TextAnchor.UpperLeft;

            var textLayout = logText.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;

            scrollRect.content = cRect;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            // Hide gameplay panels initially
            tableFelt.gameObject.SetActive(false);
            actionPanel.gameObject.SetActive(false);
            logPanel.gameObject.SetActive(false);
            repText.gameObject.SetActive(false);

            // --- START MENU OVERLAY ---
            BuildStartMenu(canvasGO.transform, logPanel.gameObject);
        }

        private void BuildStartMenu(Transform parent, GameObject logPanel)
        {
            var menuPanelRect = CreatePanel(parent, Vector2.zero, new Vector2(500, 300), new Color(0.12f, 0.12f, 0.15f, 0.98f));
            startMenuPanel = menuPanelRect.gameObject;

            CreateText(startMenuPanel.transform, new Vector2(0, -30), new Vector2(400, 40), 24, "WELCOME TO POKER SOCIAL");
            CreateText(startMenuPanel.transform, new Vector2(0, -80), new Vector2(400, 30), 16, "Enter Your Name:");

            // Input Field
            var inputGO = new GameObject("NameInputField");
            inputGO.transform.SetParent(startMenuPanel.transform, false);
            var inputImg = inputGO.AddComponent<Image>();
            inputImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var inputRect = inputGO.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.5f, 1f);
            inputRect.anchorMax = new Vector2(0.5f, 1f);
            inputRect.anchoredPosition = new Vector2(0, -120);
            inputRect.sizeDelta = new Vector2(300, 40);

            nameInputField = inputGO.AddComponent<InputField>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(inputGO.transform, false);
            var inputText = textGO.AddComponent<Text>();
            inputText.font = GetDefaultFont(18);
            inputText.fontSize = 18;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleCenter;

            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            nameInputField.textComponent = inputText;

            // Join Game Button
            buttonPlacementIndex = 0;
            var joinBtn = CreateButton(startMenuPanel.transform, "Join Table", () => OnJoinGameClicked(logPanel));
            var joinBtnRect = joinBtn.GetComponent<RectTransform>();
            joinBtnRect.anchoredPosition = new Vector2(0, -200);
        }

        private void OnJoinGameClicked(GameObject logPanel)
        {
            if (!string.IsNullOrEmpty(nameInputField.text.Trim()))
            {
                playerName = nameInputField.text.Trim();
            }

            startMenuPanel.SetActive(false);

            // Show table elements
            tableFelt.gameObject.SetActive(true);
            actionPanel.gameObject.SetActive(true);
            logPanel.SetActive(true);
            repText.gameObject.SetActive(true);

            // Initialize game with player's custom name
            SetupPlayers();
            table = new TableState { tableId = "table-1", tableName = "Table 1" };
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
            text.alignment = TextAnchor.UpperCenter;
            text.text = content;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
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

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.55f, 0.55f, 0.6f, 1f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(260, 34);
            rect.anchoredPosition = new Vector2(0, -10 - buttonPlacementIndex * 40);
            buttonPlacementIndex++;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = GetDefaultFont(15);
            text.fontSize = 15;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            return btn;
        }

        private void SetupPlayers()
        {
            string[] defaultBotNames = { "Bob", "Carla", "Dev", "Eli", "Fay" };
            players.Clear();

            // Seat 0 is the human player with custom name
            players.Add(new PlayerData("p0", playerName, startingChips));

            for (int i = 1; i < playerCount; i++)
            {
                players.Add(new PlayerData($"p{i}", defaultBotNames[(i - 1) % defaultBotNames.Length], startingChips));
            }

            float radius = 380f;
            for (int i = 0; i < players.Count; i++)
            {
                float angle = Mathf.PI * (0.12f + 0.76f * i / Mathf.Max(1, players.Count - 1));
                Vector2 pos = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 60);
                var anchor = CreateAnchor(tableFelt, pos);
                seatAnchors[players[i].playerId] = anchor;

                var nameText = CreateText(anchor, new Vector2(0, 70), new Vector2(180, 24), 15,
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

            // 1. PRE-FLOP
            yield return StartCoroutine(DealHoleCards(deck));
            foreach (var cv in seatCards[players[0].playerId])
                yield return StartCoroutine(cv.FlipRoutine());

            yield return StartCoroutine(PlayerActionRound("Pre-Flop"));
            if (CountActive() <= 1) { yield return StartCoroutine(Showdown()); yield break; }

            // 2. FLOP
            ResetRoundBets();
            yield return StartCoroutine(DealCommunityCards(deck, 3));
            yield return StartCoroutine(PlayerActionRound("Flop"));
            if (CountActive() <= 1) { yield return StartCoroutine(Showdown()); yield break; }

            // 3. TURN
            ResetRoundBets();
            yield return StartCoroutine(DealCommunityCards(deck, 1));
            yield return StartCoroutine(PlayerActionRound("Turn"));
            if (CountActive() <= 1) { yield return StartCoroutine(Showdown()); yield break; }

            // 4. RIVER
            ResetRoundBets();
            yield return StartCoroutine(DealCommunityCards(deck, 1));
            yield return StartCoroutine(PlayerActionRound("River"));

            // 5. SHOWDOWN
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

            yield return StartCoroutine(OpponentDecisions());
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
                    var cv = CardView.Create(tableFelt, new Vector2(70, 100));
                    cv.Setup(card);
                    cv.Rect.anchoredPosition = deckAnchor.anchoredPosition;

                    var seat = seatAnchors[p.playerId];
                    int idx = seatCards[p.playerId].Count;
                    Vector2 offset = new Vector2((idx - 0.5f) * 45, -10);
                    Vector2 target = seat.anchoredPosition + offset;

                    seatCards[p.playerId].Add(cv);
                    yield return StartCoroutine(cv.MoveRoutine(target, 0.12f));
                }
            }
        }

        private IEnumerator DealCommunityCards(Deck deck, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var card = deck.Draw();
                communityCards.Add(card);

                var cv = CardView.Create(tableFelt, new Vector2(70, 100));
                cv.Setup(card);
                cv.Rect.anchoredPosition = deckAnchor.anchoredPosition;

                int idx = communityCardViews.Count;
                Vector2 target = communityAnchor.anchoredPosition + new Vector2((idx - 2) * 80, 0);

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
                }
                else
                {
                    Log($"{p.displayName} checks.");
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

        private void OnRaiseClicked()
        {
            var human = players[0];
            int toCall = highestBet - currentBets[human.playerId];
            int totalRaise = toCall + humanRaiseAmount;
            int paid = Mathf.Min(totalRaise, human.chipStack);

            human.chipStack -= paid;
            currentBets[human.playerId] += paid;
            highestBet = currentBets[human.playerId];
            pot += paid;

            Log($"You raise to {highestBet} (+{humanRaiseAmount}).");
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
            raiseBtn.interactable = value;
            bluffBtn.interactable = value;
            strongBtn.interactable = value;
            allianceBtn.interactable = value;
            tellBtn.interactable = value;
        }

        private void UpdateSeatLabel(PlayerData p)
        {
            var labelTransform = seatAnchors[p.playerId].Find("SeatLabel_" + p.playerId);
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label != null) label.text = $"{p.displayName} ({p.chipStack})";
        }

        private void UpdatePotText() => potText.text = $"Pot: {pot}";

        private void Log(string line)
        {
            logText.text += line + "\n";
            StartCoroutine(ScrollToBottomNextFrame());
            Debug.Log(line);
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return new WaitForEndOfFrame();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}