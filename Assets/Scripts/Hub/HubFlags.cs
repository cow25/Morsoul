using System.Collections.Generic;
using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Scene-scoped set of story/progress flags for a hub scene. Lives on the
    /// HubContext GameObject; wired into <see cref="Morsoul.Dialogue.DialogueController"/>
    /// via SetFlags so requiresFlag gating shares this exact set.
    /// </summary>
    public class HubFlags : MonoBehaviour
    {
        readonly HashSet<string> _flags = new HashSet<string>(System.StringComparer.Ordinal);
        IEventBus _bus;

        /// <summary>The backing flag set; hand to DialogueController.SetFlags.</summary>
        public ISet<string> Set => _flags;

        /// <summary>True when the flag has been set (exact, ordinal match).</summary>
        public bool Has(string flag) =>
            !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        void OnEnable()
        {
            // Rehydrate from the persistent session after a scene load.
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session == null) return;
            foreach (var kv in session.Flags)
                if (kv.Value) _flags.Add(kv.Key);
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
            _flags.Clear();
            foreach (var flag in session.Flags)
                if (flag.Value) _flags.Add(flag.Key);
        }

        /// <summary>Marks the flag as set; no-op for null/empty names.</summary>
        public void Add(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            _flags.Add(flag);
            CoreRuntime.Instance?.Session.SetFlag(flag);
        }
    }
}
