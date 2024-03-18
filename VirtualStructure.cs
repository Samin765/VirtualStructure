using System.Linq;
using Imported.StandardAssets.Vehicles.Car.Scripts;
using Scripts.Game;
using Scripts.Map;
using UnityEngine;

using System.Collections;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using TMPro;
using UnityEngine.Analytics;
using PostProcessing;
using UnityEngine.UIElements;
public class VirtualStructure
{
    private List<Vector3> path;
    private List<Vector3> PredictedCarPos;

    private List<GameObject> startPos;
    private List<List<Vector3>> gates = new List<List<Vector3>>();
    public float radius = 1f;
    public int segments = 5;


    private int thickness = 1;
    public List<Vector3> VSPoints;


    private ObstacleMap obstacleMap;
    public VirtualStructure(List<Vector3> _path, ObstacleMapManager obstacleMapManager)
    {
        this.path = GlobalVariables.paths[0];
        this.gates = GlobalVariables.gates;
        //this.startPos = GameObject.FindGameObjectsWithTag("Start").ToList();
        this.obstacleMap = obstacleMapManager.ObstacleMap;


    }

    public void offSetGateGoals()
    {
        foreach (var gateGroup in gates)
        {
            int gateGroupIndex = 0;

            foreach (var gate in gateGroup)
            {
                var gateDirection = gateGroup[0] - gateGroup[4];

                var gateOffset = gateDirection;

                DrawCircle(gate + (0.06f * gateOffset), radius, segments, thickness, Color.cyan);

            }
        }
    }

    public void checkLeader()
    {

         
        Vector3 leaderCurrentPosition = GlobalVariables.CurrentCarPos[0];
        Vector3 leaderTargetPosition = GlobalVariables.VSPoints[0];

        float reachThreshold = 0.5f; // meters

        if (Vector3.Distance(leaderCurrentPosition, leaderTargetPosition) <= reachThreshold)
        {
            Debug.Log("Leader has reached the desired point.");
            GlobalVariables.createVS = false;


        }
        else
        {
            VSFit(); 
        }
    
    }

    public void predictNextPoint(float v, float phi, float theta, float deltaTime)
    {
        float L = 2.0f; // wheelbase

        // Calculate the change in position and orientation
        float deltaX = v * Mathf.Cos(theta) * 2;
        float deltaY = v * Mathf.Sin(theta) * 2;
        float deltaTheta = (v / L) * Mathf.Tan(phi) * 2;

        var point = new Vector3(deltaX, 0, deltaY);
        Debug.Log(point);
        DrawCircle(point, radius, thickness, segments, Color.cyan);

    }



        public void VSFit()
        {
            float epsilon = 1e-8f;

            List<Vector3> gradientSquares = GlobalVariables.VSPoints.Select(p => Vector3.zero).ToList();
            float beta = 0.9f;



            List<Vector3> velocity = GlobalVariables.VSPoints.Select(p => Vector3.zero).ToList();
            int maxIterations = 100;
            float initialAlpha = 0.1f;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                List<Vector3> gradients = new List<Vector3>();

                for (int i = 0; i < GlobalVariables.CurrentCarPos.Count; i++)
                {

                    Vector3 gradient = CalculateGradient(GlobalVariables.CurrentCarPos[i], GlobalVariables.VSPoints[i], GlobalVariables.CurrentCarPos , GlobalVariables.VSPoints);
                    gradients.Add(gradient);

                }

                for (int i = 0; i < GlobalVariables.VSPoints.Count; i++)
                {
                    gradientSquares[i] = beta * gradientSquares[i] + (1 - beta) * Vector3.Scale(gradients[i], gradients[i]);


                Vector3 adaptiveLearningRate = new Vector3(
                    initialAlpha / (Mathf.Sqrt(gradientSquares[i].x) + epsilon),
                    initialAlpha / (Mathf.Sqrt(gradientSquares[i].y) + epsilon),
                    initialAlpha / (Mathf.Sqrt(gradientSquares[i].z) + epsilon)
                );
                velocity[i] = beta * velocity[i] + (1 - beta) * Vector3.Scale(adaptiveLearningRate, gradients[i]);
                    GlobalVariables.VSPoints[i] -= velocity[i];
                }
            }

            GlobalVariables.VSPoints = GlobalVariables.VSPoints;
        }

        private float CalculateTotalErrorForPosition2(List<Vector3> currentPositions, List<Vector3> desiredPositions)
        {
            float totalError = 0;


            for (int i = 0; i < currentPositions.Count; i++)
            {
                float error = (currentPositions[i] - desiredPositions[i]).magnitude;
                totalError += error;
            }
            return totalError;
        }

        private Vector3 CalculateGradient(Vector3 currentPosition, Vector3 desiredPosition, List<Vector3> currentPositions, List<Vector3> desiredVSPositions, float h = 0.001f)
        {


            Vector3 gradient = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                Vector3 stepForward = Vector3.zero;
                Vector3 stepBackward = Vector3.zero;
                stepForward[i] = h;
                stepBackward[i] = -h;

                float errorForward = CalculateTotalErrorForPosition(currentPosition + stepForward, desiredPosition, currentPositions, desiredVSPositions);
                float errorBackward = CalculateTotalErrorForPosition(currentPosition + stepBackward, desiredPosition, currentPositions, desiredVSPositions);

                gradient[i] = (errorForward - errorBackward) / (2 * h);
            }

            return gradient;
        }

            private float CalculateTotalErrorForPosition(Vector3 testPosition, Vector3 desiredPosition, List<Vector3> currentPositions, List<Vector3> desiredVSPositions)
    {
        // Assuming this method calculates the error for one position,
        // but it could be adapted to calculate total error across all positions if needed
        return (testPosition - desiredPosition).magnitude;
    }




        public void createVirtualStructure(int gateGroupIndex)   // TO DO: Check if virtual Structure goes through walls/obstacles , find the closest points that does not do this 
                                                                 // or use A* for indivudal car Until all cars can create a virtual Structure (i.e at the end)
        {
            if (path.Count == 0 || !GlobalVariables.createVS)
            {
                return;
            }
            gates = GlobalVariables.gates;

            this.VSPoints = new List<Vector3>(new Vector3[5]);
            var LocalMiddlePoint = path[0];
            var WorldMiddlePoint = obstacleMap.mapGrid.LocalToWorld(LocalMiddlePoint);
            VSPoints[2] = WorldMiddlePoint;


            Vector3 offset = VSPoints[2] - gates[gateGroupIndex][2];

            for (int i = 0; i < VSPoints.Count; i++)
            {

                if (i == 2)
                {
                    continue;
                }

                VSPoints[i] = obstacleMap.mapGrid.WorldToLocal(gates[gateGroupIndex][i] + offset);

            }

            VSPoints[2] = obstacleMap.mapGrid.WorldToLocal(WorldMiddlePoint);



            GlobalVariables.VSPoints = VSPoints;  // All cars use the same VS
            GlobalVariables.createVS = false; // Don't create more virtualStructures until we reach the gates 

        }

        public void drawVirtualStructure()
        {
            foreach (var point in GlobalVariables.VSPoints)
            {
                DrawCircle(obstacleMap.mapGrid.LocalToWorld(point), radius, segments, thickness, Color.blue);
            }
        }


        public void testStructure()
        {
            var Localpoint = path[0];
            var Worldpoint = obstacleMap.mapGrid.LocalToWorld(Localpoint);

            var WorldgatePoint = gates[8][2];
            var LocalgatePoint = obstacleMap.mapGrid.WorldToLocal(WorldgatePoint);

            var LocalOffset = Worldpoint - WorldgatePoint;

            var newPoint = gates[8][1] + LocalOffset;



            DrawCircle(Worldpoint, radius, segments, thickness, Color.red);
            DrawCircle(WorldgatePoint, radius, segments, thickness, Color.green);
            DrawCircle(newPoint, radius, segments, thickness, Color.cyan);

        }



        public void DrawGates()
        {

            if (gates == null)
            {
                Debug.LogError("gatesArray NULL"); return;
            }

            foreach (List<Vector3> gateGroup in gates)
            {
                foreach (Vector3 gatePosition in gateGroup)
                {
                    DrawCircle(gatePosition, radius, segments, thickness, Color.blue);
                }
            }
        }

        private void DrawCircle(Vector3 center, float radius, int segments, float simulatedThickness, Color color)
        {
            // Draw multiple concentric circles to simulate thickness
            int layers = Mathf.CeilToInt(simulatedThickness * 5);
            float layerOffset = simulatedThickness / layers;

            for (int layer = 0; layer < layers; layer++)
            {
                float currentRadius = radius + (layer * layerOffset);
                Vector3 previousPoint = center + new Vector3(currentRadius, 0, 0);
                Vector3 firstPoint = previousPoint;

                for (int i = 1; i <= segments; i++)
                {
                    float angle = 2 * Mathf.PI * i / segments;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * currentRadius, 0, Mathf.Sin(angle) * currentRadius);
                    Vector3 currentPoint = center + offset;
                    Debug.DrawLine(previousPoint, currentPoint, color);
                    previousPoint = currentPoint;
                }


                Debug.DrawLine(previousPoint, firstPoint, color);
            }
        }
}
    
