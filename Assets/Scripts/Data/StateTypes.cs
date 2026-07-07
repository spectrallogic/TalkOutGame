using System;
using UnityEngine;

namespace TalkOut.Data
{
    public enum Comparator
    {
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
        Equal,
        NotEqual
    }

    /// A single testable condition against scene state (stat threshold or flag value).
    [Serializable]
    public struct StateCondition
    {
        public enum Kind { Stat, Flag }

        public Kind kind;
        public string key;
        public Comparator comparator;
        public float value; // Stat: compared value. Flag: nonzero means true.

        public bool Evaluate(Core.SceneStateModel state)
        {
            if (kind == Kind.Flag)
            {
                bool expected = value != 0f;
                return state.GetFlag(key) == expected;
            }

            float stat = state.GetStat(key);
            switch (comparator)
            {
                case Comparator.Greater: return stat > value;
                case Comparator.GreaterOrEqual: return stat >= value;
                case Comparator.Less: return stat < value;
                case Comparator.LessOrEqual: return stat <= value;
                case Comparator.Equal: return Mathf.Approximately(stat, value);
                case Comparator.NotEqual: return !Mathf.Approximately(stat, value);
                default: return false;
            }
        }

        public static StateCondition Stat(string key, Comparator cmp, float value) =>
            new StateCondition { kind = Kind.Stat, key = key, comparator = cmp, value = value };

        public static StateCondition Flag(string key, bool expected) =>
            new StateCondition { kind = Kind.Flag, key = key, value = expected ? 1f : 0f };
    }

    /// An engine-side state mutation executed when an action runs.
    /// This is the ONLY path by which flags and locations change — never from LLM output.
    [Serializable]
    public struct StatEffect
    {
        public enum Kind { StatDelta, SetFlag, SetLocation }

        public Kind kind;
        public string key;         // stat id, flag id, or actor id (SetLocation)
        public float amount;       // StatDelta only
        public bool boolValue;     // SetFlag only
        public string stringValue; // SetLocation target location id

        public static StatEffect Delta(string stat, float amount) =>
            new StatEffect { kind = Kind.StatDelta, key = stat, amount = amount };

        public static StatEffect Flag(string flag, bool value) =>
            new StatEffect { kind = Kind.SetFlag, key = flag, boolValue = value };

        public static StatEffect Location(string actorId, string locationId) =>
            new StatEffect { kind = Kind.SetLocation, key = actorId, stringValue = locationId };
    }

    /// A stat tracked by a scenario (e.g. suspicion), with clamp range and initial value.
    [Serializable]
    public struct StatDefinition
    {
        public string id;
        public float initial;
        public float min;
        public float max;
        [Tooltip("Used by the prompt builder to verbalize state, e.g. 'suspicious'.")]
        public string adjective;
    }

    [Serializable]
    public struct FlagDefinition
    {
        public string id;
        public bool initial;
    }

    [Serializable]
    public struct ActorLocation
    {
        public string actorId;
        public string locationId;
    }
}
