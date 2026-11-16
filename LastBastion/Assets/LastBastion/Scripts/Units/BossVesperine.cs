using UnityEngine;

namespace LastBastion
{
    /// <summary>
    /// Vesperine, the Lich Queen (Weeping Marsh, wave 30). Two unique mechanics:
    ///  1. Fade: after 6s in the open she winds up for 1.6s (violet ring), then turns invisible for
    ///     5s. While hidden only towers with sight can hit her (Sir Aldric and Dragonfire reveal her).
    ///     Interruptible: stun her during the wind-up and she stays visible.
    ///  2. Bone Wall: at 75%, 50% and 25% health she raises five Skeletons around her.
    /// </summary>
    public class BossVesperine : Enemy
    {
        const float VisibleTime = 6f;
        const float WindUp = 1.6f;
        const float HiddenTime = 5f;
        const int WallSize = 5;

        float visibleTimer = VisibleTime, wind, hiddenTimer;
        int walls;
        bool wallTipShown;
        SpriteRenderer ring;

        public override bool WindingUp => wind > 0f;
        public override string WindUpName => wind > 0f ? "FADE" : "";
        public override float WindUpProgress => wind > 0f ? 1f - wind / WindUp : 0f;
        protected override bool CanMove => wind <= 0f;

        protected override void Setup(EnemyType type, PathRoute route, int wave)
        {
            base.Setup(type, route, wave);
            ring = Art.Make("fadeRing", transform, Art.Ring, new Color(0.7f, 0.5f, 1f, 0.9f), Vector2.zero, Vector2.one * 2f, 32);
            ring.enabled = false;
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            CheckBoneWall();
            if (Stunned) return;

            if (hiddenTimer > 0f)
            {
                hiddenTimer -= dt;
                if (hiddenTimer <= 0f)
                {
                    forceHidden = false;
                    visibleTimer = VisibleTime;
                    Fx.Ring(Position, new Color(0.7f, 0.5f, 1f, 0.8f), 0.3f, 2f, 0.4f);
                }
                return;
            }

            if (wind > 0f)
            {
                wind -= dt;
                float k = 1f - wind / WindUp;
                ring.transform.localScale = Vector3.one * Mathf.Lerp(4f, 1.6f, k);
                ring.color = new Color(0.7f, 0.5f, 1f, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * (6f + 12f * k))));
                if (wind <= 0f)
                {
                    ring.enabled = false;
                    hiddenTimer = HiddenTime;
                    forceHidden = true;
                    AudioManager.Play(Sfx.Wail, 0.6f);
                    Fx.Burst(Position, new Color(0.6f, 0.4f, 1f, 0.7f), 1f, 3f, 0.5f);
                    if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 1.3f, "Vesperine fades from sight!", Palette.Magic);
                }
                return;
            }

            visibleTimer -= dt;
            if (visibleTimer <= 0f)
            {
                wind = WindUp;
                ring.enabled = true;
                AudioManager.Play(Sfx.Warn);
            }
        }

        void CheckBoneWall()
        {
            if (walls >= 3 || Hp > MaxHp * (1f - 0.25f * (walls + 1))) return;
            walls++;
            var lm = LevelManager.I;
            if (lm == null) return;
            AudioManager.Play(Sfx.Raise);
            Fx.Ring(Position, new Color(0.9f, 0.88f, 0.8f, 0.9f), 0.5f, 3f, 0.5f);
            for (int i = 0; i < WallSize; i++)
            {
                var e = lm.SpawnAt(EnemyType.Skeleton, Distance - 1.2f + i * 0.6f, SpawnWave, true);
                if (e != null) Fx.Burst(e.Position, new Color(0.9f, 0.88f, 0.8f, 0.8f), 0.4f, 1.3f, 0.4f);
            }
            lm.AddPopup(Position + Vector2.up * 1.3f, "BONE WALL!", Palette.Parchment);
            if (!wallTipShown)
            {
                wallTipShown = true;
                lm.Announce("Bone Wall!", "Each time Vesperine loses a quarter of her health she raises five Skeletons. Splash damage and Dragonfire clear them fast.", "Captain Brand", Palette.Magic, 6f);
            }
        }

        protected override void OnInterrupted()
        {
            wind = 0f;
            ring.enabled = false;
            visibleTimer = VisibleTime;
            if (LevelManager.I != null) LevelManager.I.AddPopup(Position + Vector2.up * 1.3f, "FADE INTERRUPTED!", Palette.Interrupt);
        }
    }
}
