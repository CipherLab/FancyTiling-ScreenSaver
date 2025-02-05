﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameTest.Sprites;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.Tiled;
using ScreenSaverHelper;
using ScreenSaverHelper.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using SharedKernel.Interfaces;
using Color = Microsoft.Xna.Framework.Color;
using Graphics = Nez.Graphics;
using Random = Nez.Random;
using Rectangle = System.Drawing.Rectangle;

namespace MonoGameTest.Scenes
{
    [WindowScene("Rigid Bodies", 100, "")]
    public class RigidBodyScene : SubSceneHelper
    {

        //public RigidBodyScene() : base(true, true)
        //{ }
        private PixelGlitchPostProcessor pixelGlitchPostProcessor { get; set; }
        private int RigidBodiesRenderLayer = 5;
        private int BackgroundRenderLayer = 15;
        internal Phase CurrentPhase { get; set; }
        private List<PhaseSteps> PhaseStepsList { get; set; }
        private SpriteRenderer BgComponent { get; set; }
        private IEnumerable<ICroppedImagePart> DetectedObjectImages { get; set; }
        private int Friction = 0;
        private int Elasticity = 1;
        private bool AllowUpdate { get; set; }
        private List<Rectangle> FoundObjectsBoxes { get; set; }
        public override void Initialize()
        {


            int originalWidth = 1280;
            int originalHeight = 720;

            //originalWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            //originalHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

            this.SimpleFont = Content.Load<SpriteFont>("Shared\\SimpleFont");
            SetDesignResolution(originalWidth, originalHeight, Scene.SceneResolutionPolicy.ShowAllPixelPerfect);
            Screen.SetSize(originalWidth, originalHeight);

            //Screen.IsFullscreen = true;
            Screen.ApplyChanges();

            AddRigidBorders(Friction, Elasticity);

            CurrentPhase = Phase.ShowBackground;
            PhaseStepsList = new List<PhaseSteps>();
            CurrentPhaseStep = new PhaseSteps(0, Phase.ShowBackground, Phase.BlurBackground, 5000);
            PhaseStepsList.Add(CurrentPhaseStep);
            PhaseStepsList.Add(new PhaseSteps(0, Phase.BlurBackground, Phase.TwitchAndLoad1));
            PhaseStepsList.Add(new PhaseSteps(0, Phase.TwitchAndLoad1, Phase.TwitchAndLoad2));
            PhaseStepsList.Add(new PhaseSteps(0, Phase.TwitchAndLoad2, Phase.RigidBodyMode));
            PhaseStepsList.Add(new PhaseSteps(0, Phase.RigidBodyMode, Phase.None));
            PhaseStepsList.Add(new PhaseSteps(0, Phase.None, Phase.None));
            base.Initialize();

            // create a Renderer that renders all but the light layer and screen space layer
            //AddRenderer(new RenderLayerRenderer(0, 3, ScreenSpaceRenderLayer));
            //AddRenderer(new RenderLayerExcludeRenderer(0, ScreenSpaceRenderLayer, RigidBodiesRenderLayer));



            // add a PostProcessor that renders the light render target and blurs it
            //AddPostProcessor(new PolyLightPostProcessor(0, lightRenderer.RenderTexture))
            //    .SetEnableBlur(true)
            //    .SetBlurAmount(0.5f);

            // renderer for the background image and the light map (created by StencilLightRenderer)
            var bg = AddRenderer(new RenderLayerRenderer(0, BackgroundRenderLayer));
            var rb = AddRenderer(new RenderLayerRenderer(1, RigidBodiesRenderLayer));
            bg.WantsToRenderAfterPostProcessors = false;
            rb.WantsToRenderAfterPostProcessors = true;

            this.AddRenderer(bg);
            this.AddRenderer(rb);
            //AddPostProcessor(new PolyLightPostProcessor(0, lightRenderer.RenderTexture))
            //    .SetEnableBlur(true)
            //    .SetBlurAmount(0.5f);

            pixelGlitchPostProcessor = AddPostProcessor(new PixelGlitchPostProcessor(1));
            pixelGlitchPostProcessor.HorizontalOffset = 0;
            // AddPostProcessor(new ScanlinesPostProcessor(2));
            //AddPostProcessor(new VignettePostProcessor(2));
            sw.Start();

            AllowUpdate = true;
        }


        Stopwatch sw = new Stopwatch();
        private PhaseSteps CurrentPhaseStep { get; set; }
        public override void Update()
        {
            if (CurrentPhaseStep == null)
                return;

            if (CurrentPhaseStep.Phase != CurrentPhase)
                CurrentPhaseStep = PhaseStepsList.Single(x => x.Phase == CurrentPhase);

            switch (CurrentPhase)
            {
                case Phase.ShowBackground:
                    if (sw.Elapsed.TotalMilliseconds > 1000)
                    {
                        pixelGlitchPostProcessor.HorizontalOffset = Random.Range(0, 2);
                        sw.Restart();
                    }
                    break;
                case Phase.BlurBackground:
                case Phase.TwitchAndLoad1:
                case Phase.TwitchAndLoad2:
                    if (sw.Elapsed.TotalMilliseconds > 500)
                    {
                        pixelGlitchPostProcessor.HorizontalOffset = Random.Range(1, 2);
                        sw.Restart();
                    }
                    break;
                case Phase.RigidBodyMode:
                    if (sw.Elapsed.TotalMilliseconds > 500)
                    {
                        pixelGlitchPostProcessor.HorizontalOffset = Random.Range(1, 3);
                        sw.Restart();
                    }
                    break;
                case Phase.None:
                    break;
            }
            switch (CurrentPhase)
            {
                case Phase.ShowBackground:
                    if (!CurrentPhaseStep.ActionStarted)
                    {
                        CurrentPhaseStep.ActionStarted = true;
                        var originalImageTex =
                            Texture2D.FromStream(Graphics.Instance.Batcher.GraphicsDevice,
                                new MemoryStream(Globals.ScreenCapturedImage));


                        var spriteR = new SpriteRenderer(originalImageTex);
                        spriteR.SetRenderLayer(BackgroundRenderLayer);

                        var bgEnt = CreateEntity("bg")
                            .SetPosition(Screen.Center);

                        BgComponent = bgEnt.AddComponent(spriteR);
                        BgComponent.SetRenderLayer(BackgroundRenderLayer);

                        CurrentPhaseStep.ActionCompleted = true;
                    }

                    if (CurrentPhaseStep.DoNextStep)
                        CurrentPhase = CurrentPhaseStep.NextPhase;
                    break;
                case Phase.BlurBackground:
                    //eh, lets see how it looks skipped.
                    CurrentPhase = CurrentPhaseStep.NextPhase;
                    break;
                    if (!CurrentPhaseStep.ActionStarted)
                    {
                        CurrentPhaseStep.ActionStarted = true;

                        using (var origImageHelper = new ImageHelper(new Rectangle(0, 0,
                            GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                            GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height
                        ), Globals.ScreenCapturedImage))
                        {

                            var dark = origImageHelper.ChangeBrightness(-30);
                            var blur = origImageHelper.GetBlurImageByteArrayFromData(3);
                            var result = origImageHelper.FlattenImages(dark, blur);
                            var blurBg = Texture2D.FromStream(Graphics.Instance.Batcher.GraphicsDevice, new MemoryStream(result));
                            BgComponent.Sprite = new Sprite(blurBg);
                        }
                        CurrentPhaseStep.ActionCompleted = true;
                    }
                    if (CurrentPhaseStep.DoNextStep)
                        CurrentPhase = CurrentPhaseStep.NextPhase;
                    break;
                case Phase.TwitchAndLoad1:
                    if (!CurrentPhaseStep.ActionStarted)
                    {
                        CurrentPhaseStep.ActionStarted = true;

                        using (var origImageHelper = new ImageHelper(new Rectangle(0, 0,
                            GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                            GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height
                        ), Globals.ScreenCapturedImage))
                        {

                            byte[] maskSolid = origImageHelper.EdgeDetectedFilledBlack(ImageFormat.Jpeg);
                            using (ISimpleImageHelper maskImageHelper = new ImageHelper())
                            {
                                //box locations for found stuff
                                new Thread(
                                    () =>
                                    {
                                        FoundObjectsBoxes = maskImageHelper.GetSpriteBoundingBoxesInImage(maskSolid, 4, 1);
                                    }).Start();

                            }
                        }

                    }
                    if (FoundObjectsBoxes != null && FoundObjectsBoxes.Any())
                        CurrentPhaseStep.ActionCompleted = true;

                    if (CurrentPhaseStep.DoNextStep)
                        CurrentPhase = CurrentPhaseStep.NextPhase;

                    break;
                case Phase.TwitchAndLoad2:
                    if (!CurrentPhaseStep.ActionStarted)
                    {
                        CurrentPhaseStep.ActionStarted = true;

                        using (ISimpleImageHelper origImageHelper = new ImageHelper())
                        {
                            if (FoundObjectsBoxes.Any())
                            {
                                FoundObjectsBoxes = FoundObjectsBoxes
                                    .OrderByDescending(x => x.Height * x.Width)
                                    .Take(200).ToList();

                                new Thread(
                                        () =>
                                        {
                                            DetectedObjectImages = origImageHelper.GetSpritesFromImage(Globals.ScreenCapturedImage, FoundObjectsBoxes, true);
                                        })
                                    .Start();
                            }
                        }

                    }
                    if (DetectedObjectImages != null && DetectedObjectImages.Any())
                        CurrentPhaseStep.ActionCompleted = true;

                    if (CurrentPhaseStep.DoNextStep)
                        CurrentPhase = CurrentPhaseStep.NextPhase;

                    break;
                case Phase.RigidBodyMode:
                    if (!CurrentPhaseStep.ActionStarted && DetectedObjectImages != null && DetectedObjectImages.Count() > 0)
                    {
                        CurrentPhaseStep.ActionStarted = true;
                    }
                    else
                    {
                        AddRigidBodyEntities(Friction, Elasticity);
                        if (AddedRigidBodyIdx >= DetectedObjectImages.Count())
                            CurrentPhaseStep.ActionCompleted = true;
                    }

                    if (CurrentPhaseStep.DoNextStep)
                    {
                        CurrentPhase = CurrentPhaseStep.NextPhase;

                        AddPlayer();

                    }

                    break;

                case Phase.None:
                    if (sw.Elapsed.TotalMilliseconds > 500)
                    {
                        pixelGlitchPostProcessor.HorizontalOffset = Random.Range(1, 3);
                        sw.Restart();
                    }
                    break;
            }

            base.Update();
        }

        private void AddPlayer()
        {
            var rowCount = Screen.Height / 16;
            var columnCount = Screen.Width / 16;
            var playerEntity = CreateEntity("player", new Vector2(100, 100));
            playerEntity.AddComponent(new Caveman());

            var rigidbody = new ArcadeRigidbody()
                .SetMass(.1f)
                .SetFriction(1)
                .SetElasticity(0.1f)
                .SetVelocity(Vector2.Zero);
            // playerEntity.AddComponent(new BoxCollider(-8, -16, 16, 32));
            playerEntity.AddComponent<BoxCollider>();

            rigidbody.ShouldUseGravity = true;
            playerEntity.AddComponent(rigidbody);
            var mover = new TiledMapMover(new TmxLayer
            {
                Map = new TmxMap
                {
                    TmxDirectory = null,
                    Version = null,
                    TiledVersion = null,
                    Width = columnCount,
                    Height = rowCount,
                    TileWidth = 16,
                    TileHeight = 16,
                    HexSideLength = null,
                    Orientation = OrientationType.Orthogonal,
                    StaggerAxis = StaggerAxisType.X,
                    StaggerIndex = StaggerIndexType.Odd,
                    RenderOrder = RenderOrderType.RightDown,
                    BackgroundColor = default,
                    NextObjectID = null,
                    Layers = null,
                    Tilesets = null,
                    TileLayers = null,
                    ObjectGroups = null,
                    ImageLayers = null,
                    Groups = null,
                    Properties = null,
                    MaxTileWidth = 16,
                    MaxTileHeight = 16
                },
                Name = null,
                Opacity = 0,
                Visible = false,
                OffsetX = 0,
                OffsetY = 0,
                Properties = null,
                Width = columnCount,
                Height = rowCount,
                Tiles = new TmxLayerTile[columnCount * rowCount]
            })
            {
                Entity = playerEntity,
                Enabled = true,
                UpdateOrder = 1,
                ColliderHorizontalInset = 0,
                ColliderVerticalInset = 0,
            };

            playerEntity.AddComponent(mover);
        }

        private int AddedRigidBodyIdx = 0;

        private void AddRigidBorders(int friction, int elasticity)
        {
            Texture2D xBorderTex;
            Texture2D yBorderTex;
            using (ISimpleImageHelper imgHelper = new ImageHelper())
            {
                byte[] xBorder = imgHelper.BlankImage(new Rectangle(0, 0, Screen.Width, 4), System.Drawing.Color.White);
                byte[] yBorder = imgHelper.BlankImage(new Rectangle(0, 0, 4, Screen.Height), System.Drawing.Color.White);

                yBorderTex = Texture2D.FromStream(Graphics.Instance.Batcher.GraphicsDevice, new MemoryStream(yBorder));
                xBorderTex = Texture2D.FromStream(Graphics.Instance.Batcher.GraphicsDevice, new MemoryStream(xBorder));
            }
            //bottom
            var rb = CreateEntity(
                new PointF(Screen.Width / 2f, Screen.Height + 4),
                0, friction, elasticity,
                new Vector2(0, 0), xBorderTex, false, Screen.Width, 4, false);
            rb.Entity.GetComponent<SpriteRenderer>().Color = Color.Transparent;

            //top
            rb = CreateEntity(
                new PointF(Screen.Width / 2f, -4),
                0, friction, elasticity,
                new Vector2(0, 0), xBorderTex, false, Screen.Width, 4, false);
            rb.Entity.GetComponent<SpriteRenderer>().Color = Color.Transparent;

            //left
            rb = CreateEntity(
                new PointF(-4, Screen.Height / 2f),
                0, friction, elasticity,
                new Vector2(0, 0), yBorderTex, false, 4, Screen.Height, false);
            rb.Entity.GetComponent<SpriteRenderer>().Color = Color.Transparent;

            //right
            rb = CreateEntity(
                new PointF(Screen.Width + 4, Screen.Height / 2f),
                0, friction, elasticity,
                new Vector2(0, 0), yBorderTex, false, 4, Screen.Height, false);
            rb.Entity.GetComponent<SpriteRenderer>().Color = Color.Transparent;
        }
        private void AddRigidBodyEntities(int friction, int elasticity)
        {
            if (AddedRigidBodyIdx > DetectedObjectImages.Count())
                return;

            var b = DetectedObjectImages.Skip(AddedRigidBodyIdx++).First();
            float mass = (b.ImageProperties.Width * b.ImageProperties.Height) / 100f;
            var v = new Vector2(Random.Range(3, 13), Random.Range(3, 13));
            var impulse = new Vector2(1f, 1f);

            int max = 200;
            //if ((b.ImageProperties.Width > 10 && b.ImageProperties.Height > 10) &&
            //    (b.ImageProperties.Width < max && b.ImageProperties.Height < max))
            //{
            //    v = new Vector2(Random.NextAngle(), Random.NextAngle());
            //    mass = 1f;
            //}
            //else if (b.ImageProperties.Height > max && b.ImageProperties.Width > max)
            //{
            //    v = new Vector2(0, 0);
            //    mass = 0;
            //}
            //else
            //{
            //    continue;
            //}
            if (b.ImageProperties.Height > max && b.ImageProperties.Width > max)
            {
                v = new Vector2(.001f, .0001f);
                impulse = new Vector2(0, 0);
            }
            var tex = Texture2D.FromStream(Graphics.Instance.Batcher.GraphicsDevice,
                new MemoryStream(b.ImageData));


            CreateEntity(b.Position, mass, friction, elasticity,
                    v, tex,
                    false, b.ImageProperties.Width, b.ImageProperties.Height, false)
                .AddImpulse(impulse);

        }


        ArcadeRigidbody CreateEntity(PointF position, float mass, float friction, float elasticity, Vector2 velocity,
                                     Texture2D texture, bool shouldUseGravity, int width, int height, bool circle = true)
        {
            var rigidbody = new ArcadeRigidbody()
                .SetMass(mass)
                .SetFriction(friction)
                .SetElasticity(elasticity)
                .SetVelocity(velocity);
            rigidbody.ShouldUseGravity = shouldUseGravity;
            var r = new SpriteRenderer(texture);
            r.SetRenderLayer(RigidBodiesRenderLayer);

            var entity = CreateEntity(Utils.RandomString(3))
                .SetPosition(new Vector2(position.X,position.Y))
                .AddComponent(rigidbody)
                .AddComponent(r)
                .SetRenderLayer(RigidBodiesRenderLayer);

            if (circle)
                entity.AddComponent<CircleCollider>();
            else
                entity.AddComponent(new BoxCollider(width, height));
            //entity.AddComponent<BoxCollider>();

            //entity.UpdateOrder = RigidBodiesRenderLayer;
            return rigidbody;
        }

        internal class PhaseSteps
        {
            private Stopwatch sw = new Stopwatch();
            private bool _actionStarted;
            private int Order { get; }
            public PhaseSteps(int order, Phase phase, Phase nextPhase, int durationLimit)
            {
                this.Order = order;
                this.Phase = phase;
                this.NextPhase = nextPhase;
                this.DurationLimit = durationLimit;
            }
            public PhaseSteps(int order, Phase phase, Phase nextPhase)
            {
                //no runtime limit.
                this.Order = order;
                this.Phase = phase;
                DurationLimit = Int32.MaxValue;
                this.NextPhase = nextPhase;
            }
            public Phase Phase { get; }
            public Phase NextPhase { get; }

            public bool ActionStarted
            {
                get => _actionStarted;
                set
                {
                    _actionStarted = value;
                    if (_actionStarted)
                        sw.Start();
                }
            }

            public bool ActionCompleted { get; set; }
            public int DurationLimit { get; }

            public bool DoNextStep =>
                (DurationLimit == Int32.MaxValue && ActionCompleted) ||
                (sw.ElapsedMilliseconds > DurationLimit && ActionCompleted);
        }

        public enum Phase
        {
            ShowBackground,
            BlurBackground,
            TwitchAndLoad1,
            TwitchAndLoad2,
            RigidBodyMode,
            None

        }
    }
}