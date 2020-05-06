﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameTest.Sprites;
using Nez;
using Nez.Tiled;

namespace MonoGameTest.Scenes
{
	[SampleScene("Platformer", 120, "Work in progress...\nArrows, d-pad or left stick to move, z key or a button to jump")]
	public class PlatformerScene : SubSceneHelper
	{
		public PlatformerScene() : base(true, true)
		{}


		public override void Initialize()
		{
            this.SimpleFont = Content.Load<SpriteFont>("Shared\\SimpleFont");

			// setup a pixel perfect screen that fits our map
			SetDesignResolution(640, 480, Scene.SceneResolutionPolicy.ShowAllPixelPerfect);
			Screen.SetSize(640 * 2, 480 * 2);

			// load up our TiledMap
			var map = Content.LoadTiledMap("Content/Platformer/tiledMap.tmx");
			var spawnObject = map.GetObjectGroup("objects").Objects["spawn"];

			var tiledEntity = CreateEntity("tiled-map-entity");
			tiledEntity.AddComponent(new TiledMapRenderer(map, "main"));


			// create our Player and add a TiledMapMover to handle collisions with the tilemap
			var playerEntity = CreateEntity("player", new Vector2(spawnObject.X, spawnObject.Y));
			playerEntity.AddComponent(new Caveman());
			playerEntity.AddComponent(new BoxCollider(-8, -16, 16, 32));
			playerEntity.AddComponent(new TiledMapMover(map.GetLayer<TmxLayer>("main")));

			AddPostProcessor(new VignettePostProcessor(1));
		}
	}
}