using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// Plays the Maelstrom (<see cref="MaelstromArt"/>) off its own EnemyController: the turning loop
    /// while it stands, draining into the floor step by step as it dives, gone while it is beneath the
    /// floor (MaelstromWater draws its ripple there), and the drain played back as it pours up in the
    /// whirlpool. The figure is symmetrical and never turns.
    /// </summary>
    public class MaelstromVisual : TexelVisual
    {
        const float TurnSecondsPerFrame = 0.16f;

        public static MaelstromVisual Attach(GameObject visual, EnemyController enemy, EnemyLook look, bool elite)
            => Attach<MaelstromVisual>(visual, enemy, look, elite, (int)MaelstromFrame.Turn0);

        protected override Sprite[] Build(EnemyLook look, bool elite) => MaelstromArt.For(look, elite);
        protected override Sprite[] Build(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome) => MaelstromArt.For(look, elite, biome);
        protected override Color[] Reserved => MaelstromArt.Reserved;

        void LateUpdate()
        {
            if (_enemy == null || _sr == null) return;
            var phase = _enemy.MaelPhase;
            if (phase is MaelstromPhase.Under or MaelstromPhase.Tell) { _sr.enabled = false; return; }
            _sr.enabled = true;

            float p = _enemy.MaelProgress01;
            int sink = Mathf.Clamp(Mathf.FloorToInt(p * 4f), 0, 3);
            int frame = phase switch
            {
                MaelstromPhase.Dive => (int)MaelstromFrame.Sink0 + sink,
                MaelstromPhase.Rise => (int)MaelstromFrame.Sink3 - sink,
                // surfaced after the whirlpool it turns at half speed - spent, open to hits
                _ => (int)MaelstromFrame.Turn0 + Mathf.FloorToInt(Time.time / (TurnSecondsPerFrame * (_enemy.Recovering ? 2f : 1f))) % 8,
            };
            Show(frame);
        }
    }
}
