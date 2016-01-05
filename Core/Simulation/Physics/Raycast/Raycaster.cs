﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PanLineAlgorithm;

namespace Lockstep
{

    public static class Raycaster
    {
        public static IEnumerable<LSBody> RaycastAll(Vector2d start, Vector2d end)
        {
            LSBody.PrepareAxisCheck(start, end);
            foreach (FractionalLineAlgorithm.Coordinate coor in
                GetRelevantNodeCoordinates (start,end))
            {
                int indexX = coor.X;
                int indexY = coor.Y;
                if (!Partition.CheckValid(coor.X, coor.Y))
                {
                    break;
                }
                PartitionNode node = Partition.GetNode(indexX, indexY);
                for (int i = node.ContainedObjects.Count - 1; i >= 0; i--)
                {
                    LSBody body = PhysicsManager.SimObjects [node.ContainedObjects [i]];
                    if (body.Overlaps())
                        yield return body;
                    
                }
            }
            yield break;
        }

        public static IEnumerable<LSBody> RaycastAll(Vector2d start, Vector2d end, long startHeight, long heightSlope)
        {
            foreach (LSBody body in RaycastAll(start,end))
            {
                long dist = body.GetClosestDist(start);
                long heightAtBodyPosition = startHeight + (dist.Mul(heightSlope));
                if (body.HeightOverlaps(heightAtBodyPosition))
                {
                    yield return body;
                }
            }
        }

        public static IEnumerable<FractionalLineAlgorithm.Coordinate> GetRelevantNodeCoordinates(Vector2d start, Vector2d end)
        {
            //Note: 99% sure this is deterministic enough for use in simulation.
            foreach (FractionalLineAlgorithm.Coordinate coor in FractionalLineAlgorithm.Trace(
                Partition.GetRelativeX(start.x).ToDouble(),
                Partition.GetRelativeX(start.y).ToDouble(),
                Partition.GetRelativeY(end.x).ToDouble(),
                Partition.GetRelativeY(end.y).ToDouble()))
            {
                int indexX = coor.X;
                int indexY = coor.Y;
                yield return coor;
            }
        }
    }
}