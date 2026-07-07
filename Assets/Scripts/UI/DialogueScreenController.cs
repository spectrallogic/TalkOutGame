using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TalkOut.Core;
using TalkOut.Data;
using TalkOut.Player;

namespace TalkOut.UI
{
    /// FPS HUD: crosshair + interact hint, streaming chat log fed by the
    /// EventLog, Enter-to-type chat mode (frees the cursor), mic status,
    /// thinking indicator, outcome overlay.
    [RequireComponent(typeof(UIDocument))]
    public class DialogueScreenController : MonoBehaviour
    {
        public TurnController turnController;
        public FirstPersonRig firstPersonRig;
        public InteractionRaycaster raycaster;
        public VoiceInput voiceInput;

        [Tooltip("Scene-specific control hint; empty keeps the UXML default")]
        public string micHintText = "";

        private ScrollView historyView;
        private VisualElement inputRow;
        private TextField inputField;
        private Label thinkingLabel;
        private Label interactHint;
        private Label micStatus;
        private Label goalBanner;
        private Label timerLabel;
        private VisualElement outcomeOverlay;
        private Label outcomeVerdict;
        private Label outcomeTitle;
        private Label outcomeScore;
        private Label outcomeTime;
        private Label outcomeText;

        private Label liveNpcLabel;
        private float thinkingTime;
        private bool chatMode;
        private bool attachedToLog;
        private string defaultMicText;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            historyView = root.Q<ScrollView>("history");
            inputRow = root.Q<VisualElement>("input-row");
            inputField = root.Q<TextField>("input-field");
            thinkingLabel = root.Q<Label>("thinking");
            interactHint = root.Q<Label>("interact-hint");
            micStatus = root.Q<Label>("mic-status");
            goalBanner = root.Q<Label>("goal-banner");
            timerLabel = root.Q<Label>("timer");
            outcomeOverlay = root.Q<VisualElement>("outcome-overlay");
            outcomeVerdict = root.Q<Label>("outcome-verdict");
            outcomeTitle = root.Q<Label>("outcome-title");
            outcomeScore = root.Q<Label>("outcome-score");
            outcomeTime = root.Q<Label>("outcome-time");
            outcomeText = root.Q<Label>("outcome-text");
            if (!string.IsNullOrEmpty(micHintText)) micStatus.text = micHintText;
            defaultMicText = micStatus.text;

            root.Q<Button>("submit-button").clicked += SubmitTyped;
            inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    SubmitTyped();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    SetChatMode(false);
                    evt.StopPropagation();
                }
            });

            root.Q<Button>("retry-button").clicked += () =>
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            var menuButton = root.Q<Button>("menu-button");
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                menuButton.clicked += () => SceneManager.LoadScene("MainMenu");
            }
            else
            {
                menuButton.style.display = DisplayStyle.None;
            }

            if (turnController != null)
            {
                turnController.ThinkingChanged += OnThinkingChanged;
                turnController.PartialReply += OnPartialReply;
                turnController.SceneEnded += OnSceneEnded;
            }
            if (raycaster != null) raycaster.HintChanged += OnHintChanged;
            if (voiceInput != null)
            {
                voiceInput.ListeningChanged += OnListening;
                voiceInput.TranscribingChanged += OnTranscribing;
            }
        }

        private void OnDisable()
        {
            if (turnController != null)
            {
                turnController.ThinkingChanged -= OnThinkingChanged;
                turnController.PartialReply -= OnPartialReply;
                turnController.SceneEnded -= OnSceneEnded;
                if (attachedToLog && turnController.Log != null)
                {
                    turnController.Log.EventAdded -= OnEvent;
                }
            }
            if (raycaster != null) raycaster.HintChanged -= OnHintChanged;
            if (voiceInput != null)
            {
                voiceInput.ListeningChanged -= OnListening;
                voiceInput.TranscribingChanged -= OnTranscribing;
            }
        }

        private void Update()
        {
            // The EventLog exists only after GameManager initializes — attach lazily.
            if (!attachedToLog && turnController != null && turnController.Log != null)
            {
                turnController.Log.EventAdded += OnEvent;
                attachedToLog = true;
                foreach (var e in turnController.Log.Events) OnEvent(e);
                goalBanner.text = $"<color=#F5C518>GOAL:</color>  {turnController.Scenario.playerGoal}";
            }

            if (turnController != null && timerLabel != null && turnController.Scenario != null)
            {
                float limit = turnController.Scenario.timeLimitSeconds;
                if (limit > 0)
                {
                    float remaining = Mathf.Max(0f, limit - turnController.ElapsedSeconds);
                    timerLabel.text = FormatTime(remaining);
                    timerLabel.EnableInClassList("timer-low", remaining <= 60f);
                }
                else
                {
                    timerLabel.text = FormatTime(turnController.ElapsedSeconds);
                }
            }

            if (!chatMode && Input.GetKeyDown(KeyCode.Return))
            {
                SetChatMode(true);
            }

            if (thinkingLabel.style.display == DisplayStyle.Flex)
            {
                thinkingTime += Time.deltaTime;
                int dots = 1 + (int)(thinkingTime * 2f) % 3;
                thinkingLabel.text = "the officer is thinking" + new string('.', dots);
            }
        }

        private void SetChatMode(bool on)
        {
            chatMode = on;
            inputRow.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (firstPersonRig != null)
            {
                firstPersonRig.LookEnabled = !on;
                firstPersonRig.LockCursor(!on);
            }
            if (on)
            {
                inputField.value = "";
                inputField.schedule.Execute(() => inputField.Focus()).ExecuteLater(30);
            }
        }

        private void SubmitTyped()
        {
            string text = inputField.value;
            SetChatMode(false);
            if (!string.IsNullOrWhiteSpace(text) && turnController != null)
            {
                turnController.SubmitPlayerUtterance(text);
            }
        }

        private void OnEvent(GameEvent e)
        {
            string text;
            string kindClass;
            switch (e.kind)
            {
                case EventKind.PlayerSaid:
                    text = $"You: {e.text}";
                    kindClass = "line-player";
                    break;
                case EventKind.NpcSaid:
                    text = $"{e.actor}: {e.text}";
                    kindClass = "line-npc";
                    if (liveNpcLabel != null)
                    {
                        liveNpcLabel.text = text;
                        liveNpcLabel = null;
                        ScrollToBottom();
                        return;
                    }
                    break;
                case EventKind.PlayerAction:
                case EventKind.SceneBeat:
                    text = e.text;
                    kindClass = "line-beat";
                    break;
                default:
                    text = e.text;
                    kindClass = "line-system";
                    break;
            }
            AppendLabel(text, kindClass);
            ScrollToBottom();
        }

        private void OnThinkingChanged(bool thinking)
        {
            thinkingLabel.style.display = thinking ? DisplayStyle.Flex : DisplayStyle.None;
            thinkingTime = 0f;
            if (thinking)
            {
                liveNpcLabel = AppendLabel("…", "line-npc");
                ScrollToBottom();
            }
        }

        private void OnPartialReply(string partial)
        {
            if (liveNpcLabel != null && !string.IsNullOrEmpty(partial))
            {
                liveNpcLabel.text = partial;
                ScrollToBottom();
            }
        }

        private void OnHintChanged(string hint)
        {
            bool show = !string.IsNullOrEmpty(hint);
            interactHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) interactHint.text = $"[Click]  {hint}";
        }

        private void OnListening(bool listening)
        {
            micStatus.text = listening ? "● Listening… (release V to send)" : defaultMicText;
            micStatus.EnableInClassList("listening", listening);
        }

        private void OnTranscribing(bool transcribing)
        {
            if (transcribing) micStatus.text = "…transcribing…";
            else if (!micStatus.ClassListContains("listening")) micStatus.text = defaultMicText;
            micStatus.EnableInClassList("transcribing", transcribing);
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }

        private void OnSceneEnded(OutcomeRule outcome)
        {
            outcomeVerdict.text = outcome.isWin ? "YOU PASSED" : "YOU FAILED";
            outcomeVerdict.EnableInClassList("failed", !outcome.isWin);
            outcomeTitle.text = (outcome.isWin ? "🎉 " : "🚨 ") + outcome.title;
            if (outcome.isWin)
            {
                outcomeScore.text = $"{turnController.LastRunScore:N0}" +
                                    (turnController.LastRunIsNewBest ? "  ★ NEW BEST" : "");
            }
            else
            {
                outcomeScore.text = "";
            }
            outcomeTime.text = $"⏱ {FormatTime(turnController.ElapsedSeconds)}  ·  {turnController.PlayerTurnsTaken} lines";
            outcomeText.text = outcome.resultText;
            outcomeOverlay.style.display = DisplayStyle.Flex;
            if (firstPersonRig != null)
            {
                firstPersonRig.LookEnabled = false;
                firstPersonRig.LockCursor(false);
            }
        }

        private Label AppendLabel(string text, string kindClass)
        {
            var label = new Label(text);
            label.AddToClassList("line");
            label.AddToClassList(kindClass);
            historyView.Add(label);
            // Keep the on-screen log tidy: cap at 40 lines.
            while (historyView.childCount > 40) historyView.RemoveAt(0);
            return label;
        }

        private void ScrollToBottom()
        {
            historyView.schedule.Execute(() =>
            {
                historyView.scrollOffset = new Vector2(0, historyView.contentContainer.layout.height);
            }).ExecuteLater(30);
        }
    }
}
