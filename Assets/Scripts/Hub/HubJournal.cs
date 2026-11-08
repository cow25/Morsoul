using System;
using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Scene-scoped clue store for hub scenes. Lives on the HubContext
    /// GameObject (no static singleton — access via HubContext). Registering a
    /// clue is idempotent: duplicates are dropped and the event only fires for
    /// genuinely new clues.
    /// </summary>
    public class HubJournal : MonoBehaviour
    {
        readonly List<ClueData> _collected = new List<ClueData>();
        IEventBus _bus;

        /// <summary>Clues collected so far in this hub, in registration order.</summary>
        public IReadOnlyList<ClueData> Collected => _collected;

        /// <summary>Fires once per newly registered clue (never for duplicates).</summary>
        public event Action<ClueData> ClueRegistered;

        void OnEnable()
        {
            // Rehydrate from the persistent session after a scene load; clue
            // assets cached this run repopulate the journal view. Snapshot the
            // dictionary and hydrate silently - Register would mutate the same
            // session dictionary mid-enumeration and re-notify listeners of
            // clues that were already announced.
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session == null) return;
            foreach (var kv in new List<KeyValuePair<string, ClueData>>(session.ClueAssets))
                if (kv.Value != null && !_collected.Contains(kv.Value))
                    _collected.Add(kv.Value);
            _bus = CoreRuntime.Instance.Bus;
            _bus.Subscribe<InvestigationStateChangedEvent>(OnInvestigationChanged);
        }

        void OnDisable()
        {
            _bus?.Unsubscribe<InvestigationStateChangedEvent>(OnInvestigationChanged);
            _bus = null;
        }

        void OnInvestigationChanged(InvestigationStateChangedEvent change)
        {
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session == null) return;
            foreach (var clue in new List<ClueData>(session.ClueAssets.Values)) Register(clue);
        }

        /// <summary>Registers the clue if not already present. Returns true when added.</summary>
        public bool Register(ClueData clue)
        {
            if (clue == null || _collected.Contains(clue)) return false;
            _collected.Add(clue);
            CoreRuntime.Instance?.Session.RegisterClue(clue);
            ClueRegistered?.Invoke(clue);
            return true;
        }

        /// <summary>True when the clue was already registered.</summary>
        public bool Contains(ClueData clue) => clue != null && _collected.Contains(clue);
    }
}
