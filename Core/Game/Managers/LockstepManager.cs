﻿//=======================================================================
// Copyright (c) 2015 John Pan
// Distributed under the MIT License.
// (See accompanying file LICENSE or copy at
// http://opensource.org/licenses/MIT)
//=======================================================================

#if UNITY_EDITOR
#pragma warning disable 0168 // variable declared but not used.
#pragma warning disable 0219 // variable assigned but not used.
#pragma warning disable 0414 // private field assigned but not used.
#endif

/*
 * Call Pattern
 * ------------
 * Setup: Called once per run for setting up any values
 * Initialize: Called once per instance. On managers, called in new game. On agents, called when unpooled.
 * Simulate: Called once every simulation frame. 
 * Visualize: Called once every rendering/player interfacing frame
 * Deactivate: Called upon deactivation. On managers, called when game is ended. On agents, called when pooled.
 */

using Lockstep.UI;
using UnityEngine;

//using Lockstep.Integration;
using Lockstep.Data;
using System;

namespace Lockstep
{
	//TODO: Set up default functions to implement LSManager
	public static class LockstepManager
	{
		public static readonly System.Diagnostics.Stopwatch SimulationTimer = new System.Diagnostics.Stopwatch ();

		/// <summary>
		/// Seconds since start if the last session.
		/// </summary>
		/// <value>The seconds.</value>
		public static double Seconds { get { return SimulationTimer.ElapsedTicks / (double)System.TimeSpan.TicksPerSecond; } }

		public static MonoBehaviour UnityInstance { get; private set; }

		public const int FrameRate = 32;
		public const int InfluenceResolution = 2;
		public const float BaseDeltaTime = (float)(1d / FrameRate);

		private static int InfluenceCount;

		public static int InfluenceFrameCount { get; private set; }

		public static int FrameCount { get; private set; }

		public static bool GameStarted { get; private set; }

		public static bool Loaded { get; private set; }

		private static GameManager _mainGameManager;

		public static event Action onSetup;
		public static event Action onInitialize;

		public static GameManager MainGameManager {
			get {
				if (_mainGameManager == null)
					throw new System.Exception ("MainGameManager has exploded!");
				return _mainGameManager;
			}
			private set {
				_mainGameManager = value;
			}
		}

		public static int PauseCount { get; private set; }

		public static bool IsPaused { get { return PauseCount > 0; } }

		public static void Pause ()
		{
			PauseCount++;
		}

		public static void Unpause ()
		{
			PauseCount--;
		}

		public static void Reset ()
		{
			LockstepManager.Deactivate ();
			GameObject copy = GameObject.Instantiate (MainGameManager.gameObject);

		}

		internal static void Setup ()
		{
			DefaultMessageRaiser.EarlySetup ();

			LSDatabaseManager.Setup ();
			Command.Setup ();

			UnityInstance = GameObject.CreatePrimitive (PrimitiveType.Quad).AddComponent<MonoBehaviour> ();
			GameObject.Destroy (UnityInstance.GetComponent<Collider> ());
			UnityInstance.GetComponent<Renderer> ().enabled = false;
			GameObject.DontDestroyOnLoad (UnityInstance.gameObject);

			GridManager.Setup ();
			AbilityDataItem.Setup ();
         
			AgentController.Setup ();
			TeamManager.Setup ();

			ProjectileManager.Setup ();
			EffectManager.Setup ();

			PhysicsManager.Setup ();
			ClientManager.Setup ();

			Time.fixedDeltaTime = BaseDeltaTime;
			Time.maximumDeltaTime = Time.fixedDeltaTime * 2;
			InputCodeManager.Setup ();


			DefaultMessageRaiser.LateSetup ();
			if (onSetup != null)
				onSetup ();


		}

		private static long _playRate = FixedMath.One;
		public static long PlayRate
		{
			get
			{
				return _playRate;
			}
			set
			{
				if (value != _playRate)
				{
					_playRate = value;
					Time.timeScale = PlayRate.ToFloat();
					//Time.fixedDeltaTime = BaseDeltaTime / _playRate.ToFloat();
				}
			}
		}

		public static float FloatPlayRate
		{
			get { return _playRate.ToFloat(); }
			set
			{
				PlayRate = FixedMath.Create(value);
			}
		}

		internal static void Initialize (GameManager gameManager)
		{
			PlayRate = FixedMath.One;
			//PauseCount = 0;
			MainGameManager = gameManager;

			if (!Loaded) {
				Setup ();
				Loaded = true;
			}



			DefaultMessageRaiser.EarlyInitialize ();
			SimulationTimer.Stop ();
			SimulationTimer.Reset ();
			SimulationTimer.Start ();
			LSDatabaseManager.Initialize ();
			LSUtility.Initialize (1);
			InfluenceCount = 0;
			Time.timeScale = 1f;

			Stalled = true;

			FrameCount = 0;
			InfluenceFrameCount = 0;

			ClientManager.Initialize (MainGameManager.MainNetworkHelper);

			TriggerManager.Initialize ();

			GridManager.Initialize ();

			TeamManager.Initialize ();

			CoroutineManager.Initialize ();
			FrameManager.Initialize ();

			CommandManager.Initialize ();

			AgentController.Initialize ();
			TeamManager.LateInitialize ();

			PhysicsManager.Initialize ();
			PlayerManager.Initialize ();
			SelectionManager.Initialize ();
			InfluenceManager.Initialize ();
			ProjectileManager.Initialize ();

			DefaultMessageRaiser.LateInitialize ();
			InitializeHelpers ();
			BehaviourHelperManager.LateInitialize ();
			if (onInitialize != null)
				onInitialize ();
		}

		static void InitializeHelpers ()
		{
			FastList<BehaviourHelper> helpers = new FastList<BehaviourHelper> ();
			MainGameManager.GetBehaviourHelpers (helpers);
			BehaviourHelperManager.Initialize (helpers.ToArray ());
		}

		static bool Stalled;

		internal static void Simulate ()
		{
			MainGameManager.MainNetworkHelper.Simulate ();
			DefaultMessageRaiser.EarlySimulate ();
			if (InfluenceCount == 0) {
				InfluenceSimulate ();
				InfluenceCount = InfluenceResolution - 1;
				if (FrameManager.CanAdvanceFrame == false) {
					Stalled = true;
					return;
				}
				Stalled = false;
				if (InfluenceFrameCount == 0) {
					GameStart ();
				}
				FrameManager.Simulate ();
				InfluenceFrameCount++;
			} else {
				InfluenceCount--;
			}
			if (Stalled || IsPaused) {
				return;
			}


			BehaviourHelperManager.Simulate ();
			AgentController.Simulate ();
			PhysicsManager.Simulate ();
			CoroutineManager.Simulate ();
			InfluenceManager.Simulate ();
			ProjectileManager.Simulate ();
			TeamManager.Simulate ();

			TriggerManager.Simulate ();

			LateSimulate ();
			FrameCount++;

		}

		private static void GameStart ()
		{
			BehaviourHelperManager.GameStart ();
			GameStarted = true;

		}

		private static void LateSimulate ()
		{
			BehaviourHelperManager.LateSimulate ();
			AgentController.LateSimulate ();
			PhysicsManager.LateSimulate ();
			DefaultMessageRaiser.LateSimulate ();
		}

		internal static void InfluenceSimulate ()
		{
			PlayerManager.Simulate ();
			CommandManager.Simulate ();
			ClientManager.Simulate ();
		}

		internal static void Execute (Command com)
		{
			if (!GameStarted) {
				Debug.LogError ("BOOM");
				return;
			}
			if (com.ControllerID != byte.MaxValue) {
				AgentController cont = AgentController.InstanceManagers [com.ControllerID];
				cont.Execute (com);
			} else {
				BehaviourHelperManager.Execute (com);
			}

			DefaultMessageRaiser.Execute (com);

		}

		internal static void Visualize ()
		{
			if (!GameStarted)
				return;
			DefaultMessageRaiser.EarlyVisualize ();
			PlayerManager.Visualize ();
			BehaviourHelperManager.Visualize ();
			PhysicsManager.Visualize ();
			AgentController.Visualize ();
			ProjectileManager.Visualize ();
			EffectManager.Visualize ();

			TeamManager.Visualize ();
		}

		internal static void LateVisualize ()
		{
			DefaultMessageRaiser.LateVisualize ();

		}

		internal static void DrawGUI ()
		{

		}

		internal static void Deactivate ()
		{
			DefaultMessageRaiser.EarlyDeactivate ();

			if (GameStarted == false)
				return;
			Selector.Clear ();
			AgentController.Deactivate ();
			BehaviourHelperManager.Deactivate ();
			ProjectileManager.Deactivate ();
			EffectManager.Deactivate ();
			ClientManager.Deactivate ();

			TeamManager.Deactivate ();
			ClientManager.Quit ();
			PhysicsManager.Deactivate ();
			GameStarted = false;
			LSServer.Deactivate ();
			DefaultMessageRaiser.LateDeactivate ();

			if (MainGameManager.gameObject != null)
				GameObject.Destroy (MainGameManager.gameObject);
		}

		public static void Quit ()
		{
			ClientManager.Quit ();
		}

		public static int GetStateHash ()
		{
			int hash = LSUtility.PeekRandom (int.MaxValue);
			hash += 1;
			hash ^= AgentController.GetStateHash ();
			hash += 1;
			hash ^= ProjectileManager.GetStateHash ();
			hash += 1;
			return hash;
		}
	}
}