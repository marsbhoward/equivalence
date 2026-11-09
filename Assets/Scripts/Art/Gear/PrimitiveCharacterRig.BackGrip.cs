using UnityEngine;

namespace Convergence.Art.Gear
{
    // A HAND HOLDING SOMETHING LOW AT ITS SIDE IS SEEN FROM BEHIND (the user's call, 2026-11-09, after the disc
    // pair's Wings: "the back winged stance doesn't show they are being held"). Turned away, the cape covers the
    // forearms and fists, so whatever hangs from a fist at the side floats beside the body with no hand on it.
    // The bow at rest is the other case (the disc pair lifts its own, in the DiscStance partial): its forearm
    // and fist go up to the CAPE's order, each a hair nearer the camera than the last (a z tie-break), so the fist
    // is on top of the cape while whatever ranks over the cape - the head, the helm, the pauldrons - still covers
    // them. The BOW stays under the cape: lifted with the fist, its string ran across the whole back. The
    // greatsword needs nothing: its carrying fist is promoted already, and in the combat grip its hands are in
    // front of the chest, where from behind they should be hidden.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>The bow arm's forearm and fist, in the order they stack over the bow.</summary>
        static readonly RigLayer[] BowGripLayers =
            { RigLayer.ArmBackLower, RigLayer.GlovesBackLower, RigLayer.ArmBackHand, RigLayer.GlovesBackHand };

        /// <summary>Each lifted piece this much nearer the camera than the one under it, units.</summary>
        const float BackGripZStep = 0.001f;

        bool _bowGripLifted;

        void LiftBowGrip(bool swinging)
        {
            bool lift = HeldBow && _facingAway && !swinging && _sinceSwing >= CarryReturnDelay && _present <= 0f
                        && _layers.TryGetValue(RigLayer.Back, out var cape) && cape != null && cape.enabled;
            if (lift)
            {
                int order = _layers[RigLayer.Back].sortingOrder;
                _bowGripLifted = true;
                for (int i = 0; i < BowGripLayers.Length; i++)
                    if (_layers.TryGetValue(BowGripLayers[i], out var g) && g != null)
                    {
                        g.sortingOrder = order;
                        var p = g.transform.localPosition;
                        g.transform.localPosition = new Vector3(p.x, p.y, -BackGripZStep * (i + 1));
                    }
            }
            else if (_bowGripLifted)
            {
                _bowGripLifted = false;
                foreach (var layer in BowGripLayers)
                    if (_layers.TryGetValue(layer, out var g) && g != null)
                    {
                        var p = g.transform.localPosition;
                        g.transform.localPosition = new Vector3(p.x, p.y, 0f);
                    }
                ApplyLayerOrder();
            }
        }
    }
}
