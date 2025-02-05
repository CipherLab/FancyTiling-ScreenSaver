﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using ScreenSaverEngine2.Scenes.SceneHelpers.Particles;
using ScreenSaverEngine2.Shared;
using ScreenSaverHelper.Util;
using SharedKernel.Enums;
using SharedKernel.Interfaces;
using Color = System.Drawing.Color;
using Debug = Nez.Debug;
using Random = Nez.Random;
using Rectangle = System.Drawing.Rectangle;

namespace ScreenSaverEngine2.Scenes.SceneHelpers
{
    public abstract class BaseScreenSaver : StartSceneSubSceneHelper
    {
        protected ISimpleImageHelper ImageHelper { get; set; }
        protected bool HasGlitchPostProcessor { get; set; }
        protected bool HasVignettePostProcessor { get; set; }

        protected bool IsTilingScreenSaver { get; set; }
        protected bool HasRigidBorders { get; set; }
        protected bool HasEdgeDetectRigidFloatingObjectsFromBackground { get; set; }
        protected bool ShowImageOnDetectedRigidFloatingObjects { get; set; }
        protected bool RenderRigidBodiesAfterPostProcessors { get; set; }
        protected bool HasParticleSystem { get; set; }
        protected int MaxFloatingRigidBodies { get; set; }
        protected Vector2 EdgeDetectedRigidFloatingObjectsVelocity { get; set; }

        //these are in StartSceneSubSceneHelper
        //protected bool IsFullScreen { get; set; }
        //protected bool HasGui { get; set; }
        //protected int Height { get; set; }
        //protected int Width { get; set; }
        protected byte[] BackgroundImage { get; set; }

        private SpriteRenderer BackgroundImageSpriteRenderer { get; set; }
        //must override
        //public abstract override string ToString();

        //Optional to override
        public abstract void InitProps(byte[] backgroundImage,
            ISimpleImageHelper imageHelper,
            bool isTilingScreenSaver,
            bool hasGlitchPostProcessor,
            bool hasVignettePostProcessor,
            bool hasRigidBorders,
            bool edgeDetectRigidFloatingObjectsFromBackground,
            bool showImageOnDetectedRigidFloatingObjects,
            bool renderRigidBodiesAfterPostProcessors,
            bool hasParticleSystem,
            int maxFloatingRigidBodies,
            Vector2 edgeDetectedRigidFloatingObjectsVelocity,
            bool isFullScreen,
            bool hasGui,
            int height,
            int width);

        //Here's the functionality, override it if you want (or as abstract override to force next class)
        public virtual string ToString3()
        {
            return string.Empty;
        }

        private bool StartUpdate { get; set; }

        private int GlitchOffsetMax = 0;
        private int GlitchOffsetMin = 0;
        private int BackgroundRenderLayer = 15;
        private readonly int RigidBodiesRenderLayer = 5;
        private bool _isFunctionDone = true;
        private PixelGlitchPostProcessor PixelGlitchPostProcessor { get; set; }
        private ConcurrentQueue<RunLoadingFunction> LoadingFunctions = new ConcurrentQueue<RunLoadingFunction>();
        private TilingScreenSaverComponent TilingScreenSaverComponent { get; set; }
        private SpriteRenderer TilingImageSpriteRenderer { get; set; }

        public override void OnStart()
        {
            this.ClearColor = Microsoft.Xna.Framework.Color.Black;
            if (BackgroundImage == null)
            {
                BackgroundImage = ImageHelper.BlankImage(new Rectangle(0, 0,
                        GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                        GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height),
                    Color.Black);
            }
            else
            {
                EnqueueLoadingFunction(SetBackgroundImage, 0, false);
            }
            if (HasParticleSystem)
            {
                Console.WriteLine("adding particle system");
                EnqueueLoadingFunction(AddParticleEffect, 0, false);
                //BackgroundRenderLayer = Int32.MaxValue;
            }
            else
            {
                var rb = AddRenderer(new RenderLayerRenderer(1, RigidBodiesRenderLayer));
                rb.WantsToRenderAfterPostProcessors = RenderRigidBodiesAfterPostProcessors;
                this.AddRenderer(rb);
                var bg = AddRenderer(new RenderLayerRenderer(0, BackgroundRenderLayer));
                bg.WantsToRenderAfterPostProcessors = false;
                this.AddRenderer(bg);
            }

            if (HasRigidBorders)
                EnqueueLoadingFunction(AddRigidBorders, 0, false);

            if (HasEdgeDetectRigidFloatingObjectsFromBackground)
                EnqueueLoadingFunction(GetEdgeDetectedObjectsFromBackground, 0, true);

            if (HasGlitchPostProcessor)
            {
                PixelGlitchPostProcessor = AddPostProcessor(new PixelGlitchPostProcessor(1));
                PixelGlitchPostProcessor.HorizontalOffset = 0;
                Core.StartCoroutine(GlitchBackground(1, 5));
            }

            if (HasVignettePostProcessor)
            {
                var vig = AddPostProcessor(new VignettePostProcessor(1));
                //float _power = 1f;
                //float _radius = 1.25f;
                vig.Power = .85f;
                vig.Radius = 1.50f;
            }

            if (IsTilingScreenSaver)
            {
                EnqueueLoadingFunction(SetupTilingScreenSaverComponent, 0, false);
            }

            Core.Instance.IsMouseVisible = HasGui;
            Core.StartCoroutine(RunAllFunctions(LoadingFunctions));

            base.OnStart();
        }

        public virtual bool SetBackgroundImage()
        {
            var bgEntity = CreateEntity("bg");
            var bgTexture = Texture2D.FromStream(
                Nez.Graphics.Instance.Batcher.GraphicsDevice,
                new MemoryStream(BackgroundImage));

            bgEntity.Position = Screen.Center;
            var sr = new SpriteRenderer(bgTexture);
            sr.SetRenderLayer(BackgroundRenderLayer);
            bgEntity.AddComponent(sr);
            return true;
        }

        private bool SetupTilingScreenSaverComponent()
        {
            TilingScreenSaverComponent =
                new TilingScreenSaverComponent(Graphics.Instance.Batcher.GraphicsDevice, this.ImageHelper);
            TilingScreenSaverComponent.PhaseChangedEvent += TilingScreenSaverComponent_PhaseChangedEvent;
            TilingScreenSaverComponent.InitFont(this.SimpleFont);
            // imageEntity.AddComponent(tilingScreenSaverComponent);

            var blank = ImageHelper.BlankImage(new Rectangle(0, 0,
                    GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                    GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height),
                Color.Black);

            var blankTexture2D =
                Texture2D.FromStream(Graphics.Instance.Batcher.GraphicsDevice,
                    new MemoryStream(blank));
            var sr1 = new SpriteRenderer(blankTexture2D);
            sr1.SetRenderLayer(BackgroundRenderLayer);
            CreateEntity("moon", new Vector2(Screen.Width / 2, Screen.Height / 2)).AddComponent(sr1);
            var sr2 = new SpriteRenderer(blankTexture2D);
            sr2.SetRenderLayer(BackgroundRenderLayer);
            CreateEntity("bg", new Vector2(Screen.Width / 2, Screen.Height / 2)).AddComponent(sr2);

            return true;
        }

        private Entity TilingImageEntity { get; set; }

        private void TilingScreenSaverComponent_PhaseChangedEvent(object sender, TilingSaverPhaseChangeEventArgs e)
        {
            if (TilingImageSpriteRenderer == null)
            {
                TilingImageEntity = this.FindEntity("moon");

                if (TilingImageEntity != null)
                    TilingImageSpriteRenderer = TilingImageEntity.GetComponent<SpriteRenderer>();
            }
            if (BackgroundImageSpriteRenderer == null)
            {
                var ent = this.FindEntity("bg");
                if (ent != null)
                    BackgroundImageSpriteRenderer = ent.GetComponent<SpriteRenderer>();
            }

            if (TilingImageSpriteRenderer == null)
                return;

            switch (e.CurrentPhase)
            {
                case Phase.GetImage:
                    //BackgroundImageSpriteRenderer.Color = new Microsoft.Xna.Framework.Color(0, 0, 0, 0);
                    break;

                case Phase.GotImage:
                    //if (moonTex != null)
                    TilingImageSpriteRenderer.Sprite = new Sprite(TilingScreenSaverComponent.CurrentImage);

                    break;

                case Phase.FadeIn:
                case Phase.FadeOut:
                    //addedComponent.Color = new Microsoft.Xna.Framework.Color(0, 0, 0,
                    //    MathHelper.Clamp(255 - TilingScreenSaverComponent.mAlphaValue, 0, 255));
                    BackgroundImageSpriteRenderer.Color = new Microsoft.Xna.Framework.Color(0, 0, 0,
                        MathHelper.Clamp(TilingScreenSaverComponent.mAlphaValue, 0, 255));
                    break;

                case Phase.ShowImage:
                    //BackgroundImageSpriteRenderer.Color = new Microsoft.Xna.Framework.Color(0, 0, 0, 0);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            //Console.WriteLine($"{e.CurrentPhase}-{MathHelper.Clamp(TilingScreenSaverComponent.mAlphaValue, 0, 255)} BG Alpha: {BackgroundImageSpriteRenderer.Color.A} ");
        }

        public override void Update()
        {
            if (!LoadingFunctions.Any() && StartUpdate == false)
                StartUpdate = true;

            if (!StartUpdate)
                return;

            if (GlitchOffsetMin + GlitchOffsetMax <= 0 && !LoadingFunctions.Any())
            {
                GlitchOffsetMin = 1;
                GlitchOffsetMax = 4;
            }

            if (IsTilingScreenSaver)
            {
                TilingScreenSaverComponent?.Update();
                //if (TilingScreenSaverComponent != null && TilingImageEntity != null)
                //{
                //    if (TilingScreenSaverComponent.ImagePosition != TilingImageEntity.LocalPosition)
                //        TilingImageEntity.LocalPosition = TilingScreenSaverComponent.ImagePosition;
                //}
            }

            base.Update();
        }

        public IEnumerator AddRigidBodyPartsToScene(IEnumerable<ICroppedImagePart> parts)
        {
            yield return null;

            var croppedImageParts = parts as ICroppedImagePart[] ?? parts.ToArray();
            int total = croppedImageParts.Count();
            int idx = 0;

            while (idx < total)
            {
                AddRigidBodies(croppedImageParts[idx++]);
                Console.WriteLine($"AddRigidBodies {idx}");
                yield return null;
            }
        }

        public IEnumerator AddRigidBodyPartsToScene(IEnumerable<Rectangle> parts)
        {
            yield return null;

            var rectangles = parts as Rectangle[] ?? parts.ToArray();
            int total = rectangles.Count();
            int idx = 0;

            while (idx < total)
            {
                AddRigidBodies(rectangles[idx++]);
                Console.WriteLine($"AddRigidBodies {idx}");
                yield return null;
            }
        }

        public void Dispose()
        {
            ImageHelper?.Dispose();
        }

        public IEnumerator GlitchBackground(int glitchDelayMin, int glitchDelayMax)
        {
            yield return null;

            var elapsed = 0f;
            int glitchDelay = Random.Range(glitchDelayMin, glitchDelayMax);
            while (elapsed < glitchDelay)
            {
                elapsed += Time.DeltaTime;
                if (elapsed > glitchDelay)
                {
                    PixelGlitchPostProcessor.HorizontalOffset = Random.Range(GlitchOffsetMin, GlitchOffsetMax);
                    glitchDelay = Random.Range(glitchDelayMin, glitchDelayMax);
                    elapsed = 0;
                }
                yield return null;
            }
        }

        private bool AddParticleEffect()
        {
            // add the ParticleSystemSelector which handles input for the scene and a SimpleMover to move it around with the keyboard
            var particlesEntity = CreateEntity("particles");
            particlesEntity.SetPosition(Screen.Center - new Vector2(0, 200));
            var pss = new ParticleSystemSelector(BackgroundRenderLayer * -1);
            pss.Enabled = false;
            particlesEntity.AddComponent(pss);
            particlesEntity.AddComponent(new SimpleMover());

            return true;
        }

        public IEnumerator RunAllFunctions(ConcurrentQueue<RunLoadingFunction> rf)
        {
            yield return null;

            // load up the new Scene
            while (LoadingFunctions.Any())
            {
                if (_isFunctionDone)
                {
                    _isFunctionDone = false;
                    if (rf.TryDequeue(out RunLoadingFunction result))
                    {
                        yield return Core.StartCoroutine(result.RunUntilDurationAndDone());
                    }
                }
                yield return null;
            }

            if (HasParticleSystem)
            {
                Console.WriteLine("enabled particles");
                this.FindEntity("particles").GetComponent<ParticleSystemSelector>().Enabled = true;
            }
        }

        private bool AddRigidBodies(Rectangle part)
        {
            CreateEntity(Guid.NewGuid().ToString()).AddComponent(
                new RigidBodies(
                    new CroppedImagePart { ImageData = null, ImageProperties = part },
                    RigidBodiesRenderLayer,
                    EdgeDetectedRigidFloatingObjectsVelocity,
                    ShowImageOnDetectedRigidFloatingObjects));
            return true;
        }

        private bool AddRigidBodies(ICroppedImagePart part)
        {
            CreateEntity(Guid.NewGuid().ToString()).AddComponent(
                new RigidBodies(part,
                    RigidBodiesRenderLayer,
                    EdgeDetectedRigidFloatingObjectsVelocity,
                    ShowImageOnDetectedRigidFloatingObjects));
            return true;
        }

        private bool AddRigidBorders()
        {
            CreateEntity("leftBorder").AddComponent(new RigidBorders(4, ScreenEdgeSide.Left, ImageHelper));
            CreateEntity("topBorder").AddComponent(new RigidBorders(4, ScreenEdgeSide.Top, ImageHelper));
            CreateEntity("rightBorder").AddComponent(new RigidBorders(4, ScreenEdgeSide.Right, ImageHelper));
            CreateEntity("bottomBorder").AddComponent(new RigidBorders(4, ScreenEdgeSide.Bottom, ImageHelper));
            Debug.DrawText("AddRigidBorders", 1000f);
            return true;
        }

        private void EnqueueLoadingBoundingBoxes(byte[] imageData)
        {
            ScreenSaverEngine2.SceneHelpers.DelegateDeclarationListRectangle d1 = ImageHelper.GetSpriteBoundingBoxesInImage;
            RunLoadingFunction rlf = new RunLoadingFunction(d1, 0, true, imageData, 4, 1);

            rlf.JobComplete += (sender, e) =>
            {
                Console.WriteLine("Got bounding boxes");
                //got bounding boxes
                _isFunctionDone = true;
                //get sprites
                if (e.BoxData.Any())
                {
                    EnqueueLoadingSpritesFromImageBoxData(e.BoxData);
                }
            };
            LoadingFunctions.Enqueue(rlf);
        }

        private void EnqueueLoadingFunction(ScreenSaverEngine2.SceneHelpers.DelegateDeclarationBool processToRun, int minDuration, bool useBackgroundThread)
        {
            RunLoadingFunction rlf1 = new RunLoadingFunction(processToRun, minDuration, useBackgroundThread);
            rlf1.JobComplete += (sender, args) =>
            {
                Console.WriteLine("Loaded a thing");
                _isFunctionDone = true;
            };
            LoadingFunctions.Enqueue(rlf1);
        }

        private void EnqueueLoadingFunction(ScreenSaverEngine2.SceneHelpers.DelegateDeclarationImageData processToRun, int minDuration, bool useBackgroundThread)
        {
            RunLoadingFunction rlf = new RunLoadingFunction(processToRun, minDuration, useBackgroundThread);
            rlf.JobComplete += (sender, e) =>
            {
                Console.WriteLine("Got mask image data");
                //Got mask image data
                _isFunctionDone = true;
                //get bounding boxes
                if (e.EdgeDetectedImageData != null && e.EdgeDetectedImageData.Any())
                    EnqueueLoadingBoundingBoxes(e.EdgeDetectedImageData);
            };
            LoadingFunctions.Enqueue(rlf);
        }

        private void EnqueueLoadingSpritesFromImageBoxData(List<Rectangle> boxData)
        {
            if (ShowImageOnDetectedRigidFloatingObjects)
            {
                ScreenSaverEngine2.SceneHelpers.DelegateDeclarationListCroppedImageParts d1 = ImageHelper.GetSpritesFromImage;

                RunLoadingFunction rlf = new RunLoadingFunction(d1, 0, true, this.BackgroundImage,
                    boxData.OrderByDescending(r => r.Width * r.Height).Take(MaxFloatingRigidBodies).ToList());

                rlf.JobComplete += (sender, e) =>
                {
                    Console.WriteLine("Got sprites from bounding boxes");
                    //got sprites
                    _isFunctionDone = true;
                    //put it on the screen
                    if (e.CroppedImageParts.Any())
                        Core.StartCoroutine(AddRigidBodyPartsToScene(e.CroppedImageParts.ToList()));
                };
                LoadingFunctions.Enqueue(rlf);
            }
            else
            {
                Core.StartCoroutine(AddRigidBodyPartsToScene(boxData.OrderByDescending(r => r.Width * r.Height).Take(MaxFloatingRigidBodies)));
            }
        }

        private byte[] GetEdgeDetectedObjectsFromBackground()
        {
            return ImageHelper.EdgeDetectedFilledBlack(this.BackgroundImage, ImageFormat.Png);
        }
    }
}