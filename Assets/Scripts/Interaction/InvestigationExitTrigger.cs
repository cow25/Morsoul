using Morsoul.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Interaction
{
    /// <summary>
    /// Exits a 3D investigation scene back to its 2D hub via GameStateManager,
    /// falling back to direct SceneManager load when launched standalone.
    /// </summary>
    public class InvestigationExitTrigger : MonoBehaviour
    {
        public int caseId = 1;
        public string fallbackHubScene = "Hub_LowerMarket";

        public void ExitToHub()
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.ReturnFromInvestigation(caseId))
                return;

            var core = CoreRuntime.Instance;
            if (core != null)
                core.Session.SetSceneContext(SceneMode.Mode2D, fallbackHubScene);

            if (!string.IsNullOrEmpty(fallbackHubScene))
                SceneManager.LoadScene(fallbackHubScene);
        }
    }
}

