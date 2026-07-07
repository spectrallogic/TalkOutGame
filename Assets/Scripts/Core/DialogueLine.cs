namespace TalkOut.Core
{
    public enum LineKind
    {
        Player,
        Npc,
        Beat,   // italic action narration, e.g. "The officer taps his ticket pad."
        System  // intro text, outcome text
    }

    public struct DialogueLine
    {
        public LineKind kind;
        public string speaker;
        public string text;

        public DialogueLine(LineKind kind, string speaker, string text)
        {
            this.kind = kind;
            this.speaker = speaker;
            this.text = text;
        }
    }
}
