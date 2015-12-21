﻿//=======================================================================
// Copyright (c) 2015 John Pan
// Distributed under the MIT License.
// (See accompanying file LICENSE or copy at
// http://opensource.org/licenses/MIT)
//=======================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Timers;
using System;

namespace Lockstep
{
    public static class PhysicsManager
    {
        #region User-defined Variables

        public const bool SimulatePhysics = true;
        public const int SimulationSpread = 1;

        static	int VisualSetSpread
        {
            get
            {
                return 2;
            }
        }

        public const int FrameRate = LockstepManager.FrameRate / SimulationSpread;

        static long FixedDeltaTicks
        {
            get
            {
                return (long)((10000000L * VisualSetSpread) / FrameRate);
            }
        }

        #endregion

        #region Counters

        #endregion

        #region Simulation Variables

        public const int MaxSimObjects = 5000;
        public static LSBody[] SimObjects = new LSBody[MaxSimObjects];
        private static Dictionary<int,CollisionPair> CollisionPairs = new Dictionary<int,CollisionPair>(MaxSimObjects);
        public static FastList<CollisionPair> FastCollisionPairs = new FastList<CollisionPair>(MaxSimObjects);
        private static int simulationCount;
        private static int visualSetCount;

        #endregion

        #region Assignment Variables

        public static bool[] SimObjectExists = new bool[MaxSimObjects];
        public static int PeakCount = 0;
        private static FastStack<int> CachedIDs = new FastStack<int>(MaxSimObjects / 8);
        public static int AssimilatedCount = 0;

        #endregion

        #region Visualization

        #endregion

        public static void Setup()
        {
            Partition.Setup();
        }


        public static void Initialize()
        {
            PeakCount = 0;

            CachedIDs.FastClear();
            SimObjectExists.Clear();
            //CollisionPairs.Clear ();
            //SimObjects.Clear ();
            CollisionPair.CurrentCollisionPair = null;

            PeakCount = 0;
            AssimilatedCount = 0;
            simulationCount = SimulationSpread;

            FastCollisionPairs.FastClear();

            Partition.Initialize();
        }


        public static void Simulate()
        {
            simulationCount--;
            if (simulationCount <= 0)
            {
                simulationCount = SimulationSpread;
            } else
            {
                return;
            }


            for (int i = 0; i < PeakCount; i++)
            {
                if (SimObjectExists [i])
                {
                    LSBody b1 = SimObjects [i];
                    b1.EarlySimulate();
                }
            }
            Partition.CheckAndDistributeCollisions();
            for (int i = 0; i < PeakCount; i++)
            {
                if (SimObjectExists [i])
                {
                    LSBody b1 = SimObjects [i];
                    b1.Simulate();
                }
            }

        }

        public static void LateSimulate()
        {
            visualSetCount--;
			
            SetVisuals = visualSetCount <= 0;
			
            if (SetVisuals)
            {
                long curTicks = LockstepManager.Ticks;
                LastDeltaTicks = curTicks - LastSimulateTicks;
                LastSimulateTicks = curTicks;
                visualSetCount = VisualSetSpread;
            }
            for (int i = 0; i < PeakCount; i++)
            {
                if (SimObjectExists [i])
                {
                    SimObjects [i].LateSimulate();
                }
            }

        }

        public static bool SetVisuals {get; private set;}
        public static float LerpTime {get ;private set;}
        public static float LerpDamping {get; private set;}
        private static float LerpDampScaler;
        static long LastSimulateTicks;
        static long LastDeltaTicks;

        public static void Visualize()
        {
            float smoothDeltaTime = Mathf.Max(Time.unscaledDeltaTime, 1f / 256);
            LerpDampScaler = .3f / smoothDeltaTime;
            LerpDamping = Time.unscaledDeltaTime * LerpDampScaler;
            LerpDamping *= Time.timeScale;
            long curTicks = LockstepManager.Ticks;
            LerpTime = (float)((curTicks - LastSimulateTicks) / (double)FixedDeltaTicks);
            LerpTime *= Time.timeScale;
            if (LerpTime <= 1f)
            {
                for (int i = 0; i < PeakCount; i++)
                {
                    if (SimObjectExists [i])
                    {
                        LSBody b1 = SimObjects [i];
                        b1.Visualize();
                    }
                }
            } else
            {
                for (int i = 0; i < PeakCount; i++)
                {
                    if (SimObjectExists [i])
                    {
                        SimObjects [i].LerpOverReset();
                    }
                }
            }
        }

        public static float ElapsedTime;


        static int id;
        static LSBody other;

        internal static int Assimilate(LSBody body)
        {
            if (CachedIDs.Count > 0)
            {
                id = CachedIDs.Pop();
            } else
            {
                id = PeakCount;
                PeakCount++;
            }
            SimObjectExists [id] = true;
            SimObjects [id] = body;

			
            AssimilatedCount++;
            return id;
        }

        private static CollisionPair CreatePair(LSBody body1, LSBody body2)
        {
            int pairIndex = GetCollisionPairIndex (body1.ID,body2.ID);
            CollisionPair pair = new CollisionPair();
            CollisionPairs [pairIndex] = pair;
            FastCollisionPairs.Add(pair);
            pair.Initialize(body1, body2);
            return pair;

        }

        internal static void Dessimilate(LSBody body)
        {
            if (!SimObjectExists [body.ID])
            {
                Debug.LogWarning("Object with ID" + body.ID.ToString() + "cannot be dessimilated because it it not assimilated");
                return;
            }

            SimObjectExists [body.ID] = false;
            CachedIDs.Add(body.ID);

            for (int i = 0; i < FastCollisionPairs.Count; i++)
            {
                CollisionPair pair = FastCollisionPairs.innerArray [i];
                if (pair.Body1 == body || pair.Body2 == body)
                {
                    pair.Deactivate();
                }
            }

            AssimilatedCount--;
            body.Deactivate();
        }

        public static CollisionPair GetCollisionPair(int ID1, int ID2)
        {
            CollisionPair pair;
            int pairIndex = GetCollisionPairIndex(ID1, ID2);
            if (CollisionPairs.TryGetValue(pairIndex, out pair) == false)
            {
                pair = CreatePair(SimObjects [ID1], SimObjects [ID2]);
            }
            return pair;
        }

        public static int GetCollisionPairIndex(int ID1, int ID2)
        {
            if (ID1 < ID2)
            {
                return ID1 * MaxSimObjects + ID2;
            } else
            {				
                return ID2 * MaxSimObjects + ID1;
            }
        }



        public static bool RequireCollisionPair(LSBody body1, LSBody body2)
        {
            if (
                Physics2D.GetIgnoreLayerCollision(body1.Layer, body2.Layer) == false &&
                (!body1.Immovable || !body2.Immovable) &&
                (!body1.IsTrigger || !body2.IsTrigger) &&
                (body1.Shape != ColliderType.None && body2.Shape != ColliderType.None))
            {
                return true;
            }
            return false;
        }

    }
}