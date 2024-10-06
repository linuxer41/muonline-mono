﻿using Client.Data;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Login;
using Client.Main.Objects.Lorencia;
using Client.Main.Objects.Particles;
using Client.Main.Objects.Particles.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class TestScene : BaseScene
    {
        private ParticleSystem Particles;
        public WorldControl World { get; private set; }

        public TestScene()
        {
            Controls.Add(World = new WorldControl(-1));
            Camera.Instance.Position = new Vector3(10, 10, 10);
            Camera.Instance.Target = new Vector3(1, 1, 1);
        }


        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            await base.Load(graphicsDevice);
            await World.AddObject(new StatueTorchObject() { World = World });
        }

        public override void Update(GameTime time)
        {
            Camera.Instance.Position = new Vector3(300, 300, 300);
            Camera.Instance.Target = new Vector3(150, 150, 150);

            base.Update(time);
        }
    }
}
