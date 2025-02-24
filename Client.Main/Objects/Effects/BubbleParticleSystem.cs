using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Objects.Effects
{
    public class BubbleParticleSystem : WorldObject
    {
        // Each bubble info
        private class Bubble
        {
            public Vector3 BasePosition;  // Original spawn position (used for oscillation)
            public Vector3 Position;      // Current position
            public float Speed;           // Vertical speed
            public float Size;            // Diameter of the bubble
            public float Life;            // How long the bubble has existed
            public float MaxLife;         // Max lifetime before reset

            // New fields for horizontal oscillation
            public float SwayAmplitude;   // Maximum offset in the horizontal axis
            public float SwayFrequency;   // Frequency of oscillation
            public float SwayPhase;       // Phase offset for oscillation
        }

        private List<Bubble> _bubbles = new List<Bubble>();
        private Random _rnd = new Random();

        // Texture for the bubble (PNG with transparency)
        private Texture2D _bubbleTexture;

        // Adjusted parameters
        private const int MAX_BUBBLES = 20; // Fewer bubbles for a cleaner effect
        private const float MIN_SPEED = 10f;
        private const float MAX_SPEED = 50f;
        private const float MIN_SIZE = 5f;  // Larger bubbles
        private const float MAX_SIZE = 10f;
        private const float MIN_LIFE = 15f;   // Longer lifetime
        private const float MAX_LIFE = 25f;

        public override async Task Load()
        {
            // Load bubble texture (ensure that Bubbles.png has proper transparency settings)
            _bubbleTexture = MuGame.Instance.Content.Load<Texture2D>("Bubbles");

            // Generate initial bubbles
            for (int i = 0; i < MAX_BUBBLES; i++)
                _bubbles.Add(CreateBubble());

            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update each bubble
            for (int i = 0; i < _bubbles.Count; i++)
            {
                var b = _bubbles[i];
                b.Life += dt;

                // Update vertical position (rising effect)
                b.Position.Z = b.BasePosition.Z + b.Speed * b.Life;

                // Add horizontal oscillation on X axis using sine function
                b.Position.X = b.BasePosition.X + b.SwayAmplitude * (float)Math.Sin(b.Life * b.SwayFrequency + b.SwayPhase);

                // Optionally, you can add oscillation on Y as well:
                // b.Position.Y = b.BasePosition.Y + b.SwayAmplitude * (float)Math.Cos(b.Life * b.SwayFrequency + b.SwayPhase);

                // If bubble's lifetime is over, reset it
                if (b.Life >= b.MaxLife)
                {
                    _bubbles[i] = CreateBubble();
                }
                else
                {
                    _bubbles[i] = b;
                }
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_bubbleTexture == null)
                return;

            var spriteBatch = GraphicsManager.Instance.Sprite;
            spriteBatch.Begin(
                SpriteSortMode.BackToFront,    // Sprites sorted from back (depth=1) to front (depth=0)
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.DepthRead,   // Enable depth reading
                RasterizerState.CullNone
            );

            // Draw each bubble as a billboard with proper layerDepth
            foreach (var b in _bubbles)
            {
                DrawBubbleBillboard(b);
            }

            spriteBatch.End();

            // Optionally reset render states if needed
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            base.Draw(gameTime);
        }

        private void DrawBubbleBillboard(Bubble bubble)
        {
            // Project the bubble's 3D position to 2D screen coordinates
            Vector3 screenPos = GraphicsDevice.Viewport.Project(
                bubble.Position,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity
            );

            // Skip if behind camera
            if (screenPos.Z < 0f || screenPos.Z > 1f)
                return;

            float scale = bubble.Size;
            var spriteBatch = GraphicsManager.Instance.Sprite;

            // Pass screenPos.Z as layerDepth. (0 = front, 1 = back)
            spriteBatch.Draw(
                _bubbleTexture,
                new Vector2(screenPos.X, screenPos.Y),
                null,
                Color.White,
                0f,
                new Vector2(_bubbleTexture.Width / 2f, _bubbleTexture.Height / 2f),
                scale / _bubbleTexture.Width,
                SpriteEffects.None,
                screenPos.Z
            );
        }

        private Bubble CreateBubble()
        {
            Bubble b = new Bubble();
            float range = 100f; // Area in which bubbles can spawn
            float x = (float)(_rnd.NextDouble() * range - range / 2) + Position.X;
            float y = (float)(_rnd.NextDouble() * range - range / 2) + Position.Y;
            float z = Position.Z; // Start at base Z level

            // Set base position and initial position
            b.BasePosition = new Vector3(x, y, z);
            b.Position = b.BasePosition;

            b.Speed = MathHelper.Lerp(MIN_SPEED, MAX_SPEED, (float)_rnd.NextDouble());
            b.Size = MathHelper.Lerp(MIN_SIZE, MAX_SIZE, (float)_rnd.NextDouble());
            b.Life = 0f;
            b.MaxLife = MathHelper.Lerp(MIN_LIFE, MAX_LIFE, (float)_rnd.NextDouble());

            // Set random oscillation parameters
            b.SwayAmplitude = MathHelper.Lerp(5f, 20f, (float)_rnd.NextDouble());
            b.SwayFrequency = MathHelper.Lerp(1f, 2f, (float)_rnd.NextDouble());
            b.SwayPhase = (float)_rnd.NextDouble() * MathHelper.TwoPi;

            return b;
        }
    }
}
