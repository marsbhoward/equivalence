using UnityEngine;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// The GameObject the jslib calls back into. Its NAME IS THE ADDRESS - SendMessage resolves
    /// by name, so "EquivalenceChainBridge" is duplicated in EquivalenceChain.jslib and must not
    /// be renamed on one side alone. A rename does not error; the reply simply never arrives and
    /// every request times out.
    ///
    /// DontDestroyOnLoad because a reply may land after a scene change - the run-end checkpoint
    /// is awaited on the way back to the hub.
    /// </summary>
    public class ChainBridgeReceiver : MonoBehaviour
    {
        public const string ObjectName = "EquivalenceChainBridge";

        static ChainBridgeReceiver _instance;

        public static void Ensure()
        {
            if (_instance != null) return;

            // Find before create: a domain reload drops the static while the GameObject survives,
            // and a second receiver would leave SendMessage picking between them by name.
            var existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                _instance = existing.GetComponent<ChainBridgeReceiver>()
                            ?? existing.AddComponent<ChainBridgeReceiver>();
                return;
            }

            var go = new GameObject(ObjectName);
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<ChainBridgeReceiver>();
        }

        /// <summary>Called by name from the jslib. Do not rename.</summary>
        public void OnChainResult(string json) => WebChainBridge.Deliver(json);

        void Update() => WebChainBridge.SweepTimeouts();
    }
}
