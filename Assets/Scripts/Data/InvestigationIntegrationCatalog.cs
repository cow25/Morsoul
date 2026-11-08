using System;
using UnityEngine;

namespace Morsoul.Data
{
    public enum InvestigationIntegrationStatus { NotIntegrated, Integrated, NotApplicable }
    public enum InvestigationEntryKind { Blocked, Load, Simulate }

    [Serializable]
    public class InvestigationArea
    {
        public int caseId;
        public string routeKey;
        public InvestigationIntegrationStatus status;
        public bool allowDevelopmentFallback;
        public string requiredEntryFlag;
    }

    [CreateAssetMenu(menuName = "Morsoul/Investigation Integration Catalog")]
    public class InvestigationIntegrationCatalog : ScriptableObject
    {
        public bool enableDevelopmentFallback;
        public InvestigationArea[] areas = Array.Empty<InvestigationArea>();

        public InvestigationEntryKind Decide(string routeKey, bool developmentBuild,
            bool sceneLoadable, out InvestigationArea area, bool? fallbackEnabled = null)
        {
            area = null;
            if (string.IsNullOrEmpty(routeKey) || areas == null) return InvestigationEntryKind.Blocked;
            foreach (var candidate in areas)
            {
                if (candidate == null || candidate.routeKey != routeKey) continue;
                if (area != null) { area = null; return InvestigationEntryKind.Blocked; }
                area = candidate;
            }
            if (area == null || area.caseId < 0 || area.caseId > 4)
                return InvestigationEntryKind.Blocked;
            if (area.status == InvestigationIntegrationStatus.Integrated)
                return sceneLoadable ? InvestigationEntryKind.Load : InvestigationEntryKind.Blocked;
            return area.status == InvestigationIntegrationStatus.NotIntegrated &&
                   developmentBuild && (fallbackEnabled ?? enableDevelopmentFallback) && area.allowDevelopmentFallback
                ? InvestigationEntryKind.Simulate : InvestigationEntryKind.Blocked;
        }
    }
}
