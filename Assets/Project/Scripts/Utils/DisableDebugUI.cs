using UnityEngine;
using UnityEngine.Rendering;

namespace Antigravity.Utils
{
    /// <summary>
    /// Automatically disables the specific DebugManager runtime UI that tends to conflict
    /// with controller inputs (specifically L3/R3 on some setups).
    /// </summary>
    public static class DisableDebugUI
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void DisableRuntimeUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debug = DebugManager.instance;
            if (debug != null)
            {
                debug.enableRuntimeUI = false;
                // Debug.Log("[DisableDebugUI] Utility: Disabled DebugManager Runtime UI.");
            }
#endif
        }
    }
}
