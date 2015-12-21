﻿using System;
using UnityEngine;
using Lockstep.Data;
namespace Lockstep
{
    public class MovementGroupHandler : BehaviourHelper
    {
        public override InputCode ListenInput
        {
            get
            {
                return AbilityInterfacer.FindInterfacer(typeof (Move)).ListenInput;
            }
        }



        public static MovementGroup LastCreatedGroup {get; private set;}
        static readonly FastBucket<MovementGroup> activeGroups = new FastBucket<MovementGroup>();
        static readonly FastStack<MovementGroup> pooledGroups = new FastStack<MovementGroup>();

        protected override void OnInitialize()
        {
            activeGroups.FastClear();
        }
        
        protected override void OnSimulate()
        {
            for (int i = 0; i < activeGroups.PeakCount; i++)
            {
                if (activeGroups.arrayAllocation[i])
                {
                    MovementGroup moveGroup = activeGroups [i];
                    moveGroup.LocalSimulate();
                }
            }
        }

        protected override void OnLateSimulate()
        {
            for (int i = 0; i < activeGroups.PeakCount; i++) {
                if (MovementGroupHandler.activeGroups.arrayAllocation[i]) {
                    MovementGroup moveGroup = activeGroups[i];
                    moveGroup.LateSimulate();
                }
            }
        }

        protected override void OnExecute(Command com)
        {
            Execute(com);
        }

        public static void Execute(Command com)
        {
            if (com.HasPosition)
            {
                CreateGroup(com);
            }
        }
        
        public static MovementGroup CreateGroup(Command com)
        {
            MovementGroup moveGroup = pooledGroups.Count > 0 ? pooledGroups.Pop() : new MovementGroup();
            
            moveGroup.indexID = activeGroups.Add(moveGroup);
            LastCreatedGroup = moveGroup;
            moveGroup.Initialize(com);
            return moveGroup;
        }

        public static void Pool(MovementGroup group)
        {
            int indexID = group.indexID;
            activeGroups.RemoveAt(indexID);
            pooledGroups.Add(group);
        }
        protected override void OnDeactivate()
        {
            LastCreatedGroup = null;
        }
    }

    public class MovementGroup
    {       
        const int MinGroupSize = 3;

        public Vector2d Destination { get; private set; }

        FastList<Move> movers;
        Vector2d groupDirection;
        public Vector2d groupPosition;

        public int indexID { get; set; }

        int moversCount;
        long radius;
        long averageCollisionSize;
        bool calculatedBehaviors;
            
        public void Initialize(Command com)
        {
            Destination = com.Position;
            calculatedBehaviors = false;
            movers = new FastList<Move>(com.Select.selectedAgentLocalIDs.Count);
        }
            
        public void Add(Move mover)
        {
            if (mover.MyMovementGroup .IsNotNull())
            {
                mover.MyMovementGroup.movers.Remove(mover);
            }
            mover.MyMovementGroup = this;
            mover.MyMovementGroupID = indexID;
            movers.Add(mover);
            moversCount++;
        }
            
        public void Remove(Move mover)
        {
            moversCount--;
        }
            
        public void LocalSimulate()
        {

        }
        public void LateSimulate () {
            if (!calculatedBehaviors)
            {
                CalculateAndExecuteBehaviors();
                calculatedBehaviors = true;
            }
            if (movers.Count == 0)
            {
                Deactivate();
            }
        }

        public MovementType movementType { get; private set; }
            
        public void CalculateAndExecuteBehaviors()
        {
                
            Move mover;
                
            if (movers.Count >= MinGroupSize)
            {
                averageCollisionSize = 0;
                groupPosition = Vector2d.zero;
                for (int i = 0; i < movers.Count; i++)
                {
                    mover = movers [i];
                    groupPosition += mover.Position;
                    averageCollisionSize += mover.CollisionSize;
                }
                    
                groupPosition /= movers.Count;
                averageCollisionSize /= movers.Count;
                    
                long biggestSqrDistance = 0;
                for (int i = 0; i < movers.Count; i++)
                {
                    long currentSqrDistance = movers [i].Position.SqrDistance(groupPosition.x, groupPosition.y);
                    if (currentSqrDistance > biggestSqrDistance)
                    {
                        long currentDistance = FixedMath.Sqrt(currentSqrDistance);
                        /*
                    DistDif = currentDistance - Radius;
                    if (DistDif > MaximumDistDif * MoversCount / 128) {
                        ExecuteGroupIndividualMove ();
                        return;
                    }*/
                        biggestSqrDistance = currentSqrDistance;
                        radius = currentDistance;
                    }
                }
                if (radius == 0)
                {
                    ExecuteGroupIndividualMove();
                    return;
                }
                long expectedSize = averageCollisionSize.Mul(averageCollisionSize).Mul(FixedMath.One * 2).Mul(movers.Count);
                long groupSize = radius.Mul(radius);

                if (groupSize > expectedSize || groupPosition.FastDistance(Destination.x, Destination.y) < (radius * radius))
                {
                    ExecuteGroupIndividualMove();
                    return;
                }
                ExecuteGroupMove ();

            } else
            {
                ExecuteIndividualMove();
            }
        }
            
        public void Deactivate()
        {
            Move mover;
            for (int i = 0; i < movers.Count; i++)
            {
                mover = movers [i];
                mover.MyMovementGroup = null;
                mover.MyMovementGroupID = -1;
            }
            movers.FastClear();
            MovementGroupHandler.Pool(this);
            calculatedBehaviors = false;
            indexID = -1;
        }
        void ExecuteGroupMove () {
            movementType = MovementType.Group;
            groupDirection = Destination - groupPosition;
            
            for (int i = 0; i < movers.Count; i++)
            {
                Move mover = movers [i];
                mover.IsFormationMoving = true;
                mover.CollisionStopMultiplier = Move.FormationStop;
                mover.OnGroupProcessed(mover.Position + groupDirection);
            }
        }
        void ExecuteIndividualMove () {
            movementType = MovementType.Individual;
            for (int i = 0; i < movers.Count; i++)
            {
                Move mover = movers [i];
                mover.IsFormationMoving = false;
                mover.CollisionStopMultiplier = Move.DirectStop;
                mover.OnGroupProcessed(Destination);
            }
        }
        void ExecuteGroupIndividualMove()
        {
            movementType = MovementType.GroupIndividual;
            for (int i = 0; i < movers.Count; i++)
            {
                Move mover = movers [i];
                mover.IsFormationMoving = false;
                mover.CollisionStopMultiplier = Move.GroupDirectStop;
                mover.OnGroupProcessed(Destination);
            }
        }
    }

    public enum MovementType : long
    {
        Group,
        GroupIndividual,
        Individual
    }

}