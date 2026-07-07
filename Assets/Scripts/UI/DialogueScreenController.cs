using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.UI
{
    /// Binds the UI Toolkit dialogue screen to the TurnController.
    /// Handles history rendering, live reply streaming, thinking state,
    /// input submission, and the outcome overlay.
    [RequireComponent(typeof(UIDocument))]
    public class DialogueScreenController : MonoBehaviour
    {
        public TurnController turnController;

        private ScrollView historyView;
        private TextField inputField;
        private Button submitButton;
        private Label speakerName;
        private Label thinkingLabel;
        private VisualElement portrait;
        private VisualElement outcomeOverlay;
        private Label outcomeTitle;
        private Label outcomeText;

        private Label liveNpcLabel; // label being streamed into while the model talks
        private float thinkingTime;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            historyView = root.Q<ScrollView>("history");
            inputField = root.Q<TextField>("input-field");
            submitButton = root.Q<Button>("submit-button");
            speakerName = root.Q<Label>("speaker-name");
            thinkingLabel = root.Q<Label>("thinking");
            portrait = root.Q<VisualElement>("portrait");
            outcomeOverlay = root.Q<VisualElement>("outcome-overlay");
            outcomeTitle = root.Q<Label>("outcome-title");
            outcomeText = root.Q<Label>("outcome-text");

            var micButton = root.Q<Button>("mic-button");
            micButton.SetEnabled(false); // voice input is post-MVP

            submitButton.clicked += Submit;
            inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Submit();
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
                turnController.LineAdded += OnLineAdded;
                turnController.ThinkingChanged += OnThinkingChanged;
                turnController.PartialReply += OnPartialReply;
                turnController.SceneEnded += OnSceneEnded;
            }
        }

        private void OnDisable()
        {
            if (turnController != null)
            {
                turnController.LineAdded -= OnLineAdded;
                turnController.ThinkingChanged -= OnThinkingChanged;
                turnController.PartialReply -= OnPartialReply;
                turnController.SceneEnded -= OnSceneEnded;
            }
        }

        private void Start()
        {
            var npc = turnController != null && turnController.Scenario != null
                ? turnController.Scenario.GetNpc(turnController.Scenario.respondingNpcId)
                : null;
            if (npc != null)
            {
                speakerName.text = npc.displayName;
                if (npc.faceSet != null && npc.faceSet.defaultFace != null)
                {
                    portrait.style.backgroundImage = new StyleBackground(npc.faceSet.defaultFace);
                }
            }
            inputField.Focus();
        }

        private void Update()
        {
            if (thinkingLabel.style.display == DisplayStyle.Flex)
            {
                thinkingTime += Time.deltaTime;
                int dots = 1 + (int)(thinkingTime * 2f) % 3;
                thinkingLabel.text = "thinking" + new string('.', dots);
            }
        }

        private void Submit()
        {
            if (turnController == null || turnController.Phase != TurnPhase.AwaitingInput) return;
            string text = inputField.value;
            if (string.IsNullOrWhiteSpace(text)) return;
            inputField.value = "";
            turnController.SubmitPlayerInput(text);
            inputField.Focus();
        }

        private void OnLineAdded(DialogueLine line)
        {
            if (line.kind == LineKind.Npc && liveNpcLabel != null)
            {
                // Finalize the streamed label instead of adding a duplicate.
                liveNpcLabel.text = FormatLine(line);
                liveNpcLabel = null;
            }
            else
            {
                AppendLabel(FormatLine(line), ClassFor(line.kind));
            }
            ScrollToBottom();
        }

        private void OnThinkingChanged(bool thinking)
        {
            thinkingLabel.style.display = thinking ? DisplayStyle.Flex : DisplayStyle.None;
            thinkingTime = 0f;
            inputField.SetEnabled(!thinking);
            submitButton.SetEnabled(!thinking);
            if (thinking)
            {
                liveNpcLabel = AppendLabel("…", "line-npc");
            }
            else if (!thinking && liveNpcLabel != null && liveNpcLabel.text == "…")
            {
                // Nothing streamed (mock fallback etc.) — the final line will fill it.
            }
        }

        private void OnPartialReply(string partial)
        {
            if (liveNpcLabel != null && !string.IsNullOrEmpty(partial))
            {
                liveNpcLabel.text = FormatNpcText(partial);
                ScrollToBottom();
            }
        }

        private void OnSceneEnded(OutcomeRule outcome)
        {
            outcomeTitle.text = (outcome.isWin ? "🎉 " : "🚨 ") + outcome.title;
            outcomeText.text = outcome.resultText;
            outcomeOverlay.RemoveFromClassList("hidden");
            outcomeOverlay.style.display = DisplayStyle.Flex;
        }

        private string FormatLine(DialogueLine line)
        {
            switch (line.kind)
            {
                case LineKind.Player: return line.text;
                case LineKind.Npc: return FormatNpcText(line.text);
                default: return line.text;
            }
        }

        private string FormatNpcText(string text) => text;

        private static string ClassFor(LineKind kind)
        {
            switch (kind)
            {
                case LineKind.Player: return "line-player";
                case LineKind.Npc: return "line-npc";
                case LineKind.Beat: return "line-beat";
                default: return "line-system";
            }
        }

        private Label AppendLabel(string text, string kindClass)
        {
            var label = new Label(text);
            label.AddToClassList("line");
            label.AddToClassList(kindClass);
            historyView.Add(label);
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
