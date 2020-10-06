﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

public class CarSpawnerSystem : SystemBase
{
    private Random random;

    protected override void OnCreate()
    {
        random = new Random(0x1234567);
    }

    protected override void OnUpdate()
    {
        Entities.WithStructuralChanges()
            .ForEach((Entity entity, in CarSpawner spawner, in LocalToWorld ltw, in Rotation rotation, in SpawnerFrequency frequency) =>
            {
                float r = random.NextFloat(0, 1);
                if (r > frequency.Value)
                {
                    return;
                }
                
                var instance = EntityManager.Instantiate(spawner.CarPrefab);
                EntityManager.SetComponentData(instance, new Translation
                {
                    Value = ltw.Position
                });

                float3 scale;
                int randInt = random.NextInt(0,3);
                switch (randInt){
                    case 0:
                        scale = spawner.carScaleV1;
                        break;
                    case 1:
                        scale = spawner.carScaleV2;
                        break;
                    case 2:
                        scale = spawner.carScaleV3;
                        break;
                    default:
                        scale = spawner.carScaleV1;
                        break;
                }

                EntityManager.SetComponentData(instance, new NonUniformScale
                {
                    Value = scale
                });

                EntityManager.SetComponentData(instance, new Rotation
                {
                    Value = rotation.Value
                });

                // Amanda temp
                float4 col;
                randInt = random.NextInt(0, 4);
                switch (randInt) {
                    case 0: // red
                        col = new float4(1,0,0,1);
                        break;
                    case 1: // green
                        col = new float4(0,1,0,1);
                        break;
                    case 2: // blue
                        col = new float4(0,0,1,1);
                        break;
                    case 3: // white
                        col = new float4(1,1,1,1);
                        break;
                    default:
                        col = new float4(0,0,0,1);
                        break;
                }

                EntityManager.SetComponentData(instance, new Color
                {
                    Value = col
                });

                // Add CarMovement component to spawned entity
                EntityManager.AddComponentData(instance, new CarMovement
                {
                    NextNode = new Translation{Value = new float3(0,0,20)},
                    Velocity = 0.01f,
                    Acceleration = 0.1f,
                    Deceleration = 0.1f,
                    MaxSpeed = 1 
                });

                // EntityManager.DestroyEntity(entity);

            }).Run();
    }
}