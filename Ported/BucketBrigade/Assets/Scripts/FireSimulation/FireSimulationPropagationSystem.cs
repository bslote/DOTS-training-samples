﻿using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FireSimulationUpdateSystem))]
public class FireSimulationPropagationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var simulationEntity = GetSingletonEntity<FireSimulation>();
        Timer timer = GetComponent<Timer>(simulationEntity);

        if (timer.TimerIsUp())
        {
            FireSimulation simulation = GetComponent<FireSimulation>(simulationEntity);
            var simulationTemperatures = GetBuffer<SimulationTemperature>(simulationEntity).AsNativeArray();
            int heatRadius = simulation.heatRadius;
            float heatTransferRate = simulation.heatTransferRate;
            float flashPoint = simulation.flashpoint;
            int rowCount = simulation.rows;
            int columnCount = simulation.columns;

            Entities.
                ForEach((Entity fireCellEntity, ref Temperature temperature, in CellIndex cellIndex) =>
                {
                    FireUtils.ArrayToGridCoord(cellIndex.Value, simulation.rows, out int row, out int column);

                    float tempChange = 0.0f;

                    for (int rowIndex = -heatRadius; rowIndex <= heatRadius; rowIndex++)
                    {
                        int currentRow = row - rowIndex;
                        if (currentRow >= 0 && currentRow < rowCount)
                        {
                            for (int columnIndex = -heatRadius; columnIndex <= heatRadius; columnIndex++)
                            {
                                int currentColumn = column + columnIndex;
                                if (currentColumn >= 0 && currentColumn < columnCount)
                                {
                                    var neighbourTemperature = simulationTemperatures[FireUtils.GridToArrayCoord(currentRow, currentColumn, rowCount)];
                                    if (neighbourTemperature >= flashPoint) // OnFire
                                    {
                                        tempChange += neighbourTemperature * heatTransferRate;
                                    }
                                }
                            }
                        }
                    }

                    temperature.Value = UnityEngine.Mathf.Clamp(temperature.Value + tempChange, -1f, 1f);
                }).Run();//.ScheduleParallel();
        }
    }
}