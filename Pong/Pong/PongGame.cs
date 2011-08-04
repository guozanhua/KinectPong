using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using Microsoft.Research.Kinect.Nui;
using Coding4Fun.Kinect.WinForm;

namespace Pong
{
	public class PongGame : Microsoft.Xna.Framework.Game
	{
		const int kLRMargin = 20, kPaddleWidth = 26, kPaddleHeight = 120;
		const int kBallWidth = 24, kBallHeight = 24;
		const int kMaxAIPaddleVelocity = 7;
		const int kGameWidth = 1360, kGameHeight = 800;
		
		bool passedCenter = false;
		
		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;
		
		Texture2D dotTexture = null, ballTexture = null;
		
		Rectangle ourPaddleRect = new Rectangle(kLRMargin, 0, kPaddleWidth, kPaddleHeight);
		Rectangle aiPaddleRect;
		
		Vector2 ballVelocity;
		Rectangle ballRect;
		
		float predictedBallHeight = 0.0f;
		
		Runtime runtime = new Runtime();
		
		public PongGame()
		{
			graphics = new GraphicsDeviceManager(this);
			graphics.PreferredBackBufferWidth = kGameWidth;
			graphics.PreferredBackBufferHeight = kGameHeight;
			
			Content.RootDirectory = "Content";
		}
		
		private void RestartGame()
		{
			aiPaddleRect = new Rectangle(GraphicsDevice.Viewport.Width - kLRMargin - kPaddleWidth, 20, kPaddleWidth, kPaddleHeight);
			ballVelocity = new Vector2(6.0f, 6.0f);
			ballRect = new Rectangle(500, 600, kBallWidth, kBallHeight);
		}
		
		protected override void Initialize()
		{
			RestartGame();
			
			runtime.Initialize(RuntimeOptions.UseSkeletalTracking);
			runtime.SkeletonEngine.TransformSmooth = true;
			
			runtime.SkeletonEngine.SmoothParameters = new TransformSmoothParameters() {
				Smoothing = 0.4f,
				Correction = 0.3f,
				Prediction = 0.6f,
				JitterRadius = 1.0f,
				MaxDeviationRadius = 0.5f
			};
			
			runtime.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(runtime_SkeletonFrameReady);
			
			base.Initialize();
		}
		
		void runtime_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
		{
			foreach (SkeletonData data in e.SkeletonFrame.Skeletons)
			{
				if (data.TrackingState == SkeletonTrackingState.Tracked)
				{
					int handY = (int) data.Joints[JointID.HandLeft].ScaleTo(kGameWidth, kGameHeight - kPaddleHeight, 0.6f, 0.4f).Position.Y;
					handY = Math.Max(handY, 0);
					handY = Math.Min(handY, kGameHeight - kPaddleHeight);
					ourPaddleRect.Y = handY;
					
					break;
				}
			}
		}
		
		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(GraphicsDevice);
			
			dotTexture = Content.Load<Texture2D>("Dot");
			ballTexture = Content.Load<Texture2D>("Ball");
		}
		
		protected override void UnloadContent()
		{
			runtime.Uninitialize();
		}
		
		private void SimulateRestOfTurn()
		{
			Rectangle currentBallRect = ballRect;
			Vector2 currentBallVelocity = ballVelocity;
			
			bool done = false;
			
			while (!done)
			{
				BallCollision result = AdjustBallPositionWithScreenBounds(ref currentBallRect, ref currentBallVelocity);
				done = (result == BallCollision.RightMiss || result == BallCollision.RightPaddle);
			}
			
			predictedBallHeight = currentBallRect.Y;
		}
		
		enum BallCollision
		{
			None,
			RightPaddle,
			LeftPaddle,
			RightMiss,
			LeftMiss
		}
		
		private BallCollision AdjustBallPositionWithScreenBounds(ref Rectangle enclosingRect, ref Vector2 velocity)
		{
			BallCollision collision = BallCollision.None;
			
			enclosingRect.X += (int)velocity.X;
			enclosingRect.Y += (int)velocity.Y;
			
			if (enclosingRect.Y >= GraphicsDevice.Viewport.Height - kBallHeight)
			{
				velocity.Y *= -1;
			}
			else if (enclosingRect.Y <= 0)
			{
				velocity.Y *= -1;
			}
			
			if (aiPaddleRect.Intersects(enclosingRect))
			{
				velocity.X *= -1;
				collision = BallCollision.RightPaddle;
			}
			else if (ourPaddleRect.Intersects(enclosingRect))
			{
				velocity.X *= -1;
				collision = BallCollision.LeftPaddle;
			}
			else if (enclosingRect.X >= GraphicsDevice.Viewport.Width - kLRMargin)
			{
				collision = BallCollision.RightMiss;
			}
			else if (enclosingRect.X <= 0)
			{
				collision = BallCollision.LeftMiss;
			}
			
			return collision;
		}
		
		protected override void Update(GameTime gameTime)
		{
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
				this.Exit();
			
			BallCollision collision = AdjustBallPositionWithScreenBounds(ref ballRect, ref ballVelocity);
			
			if (collision > 0)
			{
				passedCenter = false;
				
				float newY = (new Random().Next(80) + 1) / 10.0f;
				ballVelocity.Y = ballVelocity.Y > 0 ? newY : -newY;
			}
			
			if (collision == BallCollision.RightMiss || collision == BallCollision.LeftMiss)
			{
				RestartGame();
			}
			
			if (passedCenter == false && ballVelocity.X > 0 && (ballRect.X + kBallWidth >= GraphicsDevice.Viewport.Bounds.Center.X))
			{
				SimulateRestOfTurn();
				passedCenter = true;
			}
			
			int ballCenter = (int)predictedBallHeight + (kBallHeight / 2);
			int aiPaddleCenter = aiPaddleRect.Center.Y;
			
			if (predictedBallHeight > 0 && ballCenter != aiPaddleCenter)
			{
				if (ballCenter < aiPaddleCenter)
				{
					aiPaddleRect.Y -= kMaxAIPaddleVelocity;
				}
				else if (ballCenter > aiPaddleCenter)
				{
					aiPaddleRect.Y += kMaxAIPaddleVelocity;
				}
				
				if (Math.Abs(ballCenter - aiPaddleCenter) < kMaxAIPaddleVelocity)
				{
					aiPaddleRect.Y = ballCenter - (kPaddleHeight / 2);
				}
			}
			
			base.Update(gameTime);
		}
		
		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.Black);
			
			spriteBatch.Begin();
			
			spriteBatch.Draw(dotTexture, ourPaddleRect, Color.White);
			spriteBatch.Draw(dotTexture, aiPaddleRect, Color.White);
			spriteBatch.Draw(ballTexture, ballRect, Color.White);
			
			spriteBatch.End();
			
			base.Draw(gameTime);
		}
	}
}
