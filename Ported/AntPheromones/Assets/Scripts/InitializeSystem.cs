﻿using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Rendering;
using Unity.Transforms;
using Random = UnityEngine.Random;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(ConvertToEntitySystem))]
public class InitializeSystem : JobComponentSystem
{
    bool init;

    protected override void OnCreate()
    {
        init = false; 
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (init)
            return inputDeps;

        inputDeps.Complete();

        AntSettings settings = GetSingleton<AntSettings>();
        SpawnAnts(ref settings);
        SpawnObstacles(ref settings);
        SpawnTargets(ref settings);
 
        init = true;

        return new JobHandle();
    }
    
    protected void SpawnAnts(ref AntSettings settings)
    {
        Translation colonyPosition = new Translation();
        colonyPosition.Value = new Vector3(0.5f, 0.5f, 0);

        NonUniformScale antSize = new NonUniformScale();
        antSize.Value = settings.antSize;

        const float perimeter = 0.03f;

        for (int i = 0; i < settings.antCount; ++i)
        {
            Translation antPosition = new Translation();
            antPosition.Value = new Vector3(Random.Range(-perimeter, perimeter) + colonyPosition.Value.x, Random.Range(-perimeter, perimeter) + colonyPosition.Value.y, 0);

            float angleInDegrees = Random.Range(0.0f, 360.0f);
            Rotation antRotation = new Rotation();
            antRotation.Value = Quaternion.Euler(0f, 0f, angleInDegrees);

			AntComponent ant = new AntComponent()
			{
				acceleration = settings.antAccel,
				facingAngle = math.radians(angleInDegrees),
				index = i
			};

            Entity newEntity = EntityManager.Instantiate(settings.antPrefab);
			EntityManager.AddComponentData(newEntity, new AntComponent() { index = i });
            EntityManager.SetComponentData(newEntity, antPosition);
            EntityManager.AddComponentData(newEntity, antSize);
            EntityManager.SetComponentData(newEntity, antRotation);
            EntityManager.SetComponentData(newEntity, ant);
        }
    }

	protected void SpawnObstacles(ref AntSettings settings)
	{
        NonUniformScale prefabScale = new NonUniformScale();
        prefabScale.Value = Vector3.one * 2 * settings.obstacleRadius/(float)settings.mapSize;

		List<ObstacleComponent> obstacleComponents = new List<ObstacleComponent>();
		List<Translation> obstacleTranslations = new List<Translation>();
		int numObstacles = 0;
		
		for (int i = 1; i <= settings.obstacleRingCount; i++)
		{
			float ringRadius = (i / (settings.obstacleRingCount + 1f)) * (settings.mapSize * 0.5f);
			float circumference = ringRadius * 2f * Mathf.PI;
			int maxCount = Mathf.CeilToInt(circumference / (2f * settings.obstacleRadius) * 2f);
			int offset = Random.Range(0, maxCount);
			int holeCount = Random.Range(1, 3);
			for (int j = 0; j < maxCount; j++)
			{
				float t = (float)j / maxCount;
				if ((t * holeCount) % 1f < settings.obstaclesPerRing)
				{
					float angle = (j + offset) / (float)maxCount * (2f * Mathf.PI);
                    float center = settings.mapSize * 0.5f;
                    float x = Mathf.Cos(angle) * ringRadius + center;
                    float y = Mathf.Sin(angle) * ringRadius + center;
                    ObstacleComponent obstacleComponent = new ObstacleComponent() {radius = settings.obstacleRadius};
                    Translation obstacleTranslation = new Translation() {Value = new float3(x / settings.mapSize, y / settings.mapSize, 0.0f)};

                    obstacleComponents.Add(obstacleComponent);
					obstacleTranslations.Add(obstacleTranslation);
					numObstacles++;

                    var prefabEntity = EntityManager.Instantiate(settings.obstaclePrefab);
                    EntityManager.SetComponentData(prefabEntity, obstacleComponent);
                    EntityManager.SetComponentData(prefabEntity, obstacleTranslation);
                    EntityManager.AddComponentData(prefabEntity, prefabScale);
				}
			}
		}
		List<ObstacleComponent>[,] tempObstacleBuckets = new List<ObstacleComponent>[settings.bucketResolution, settings.bucketResolution];

		for (int x = 0; x < settings.bucketResolution; x++)
		{
			for (int y = 0; y < settings.bucketResolution; y++)
			{
				tempObstacleBuckets[x, y] = new List<ObstacleComponent>();
			}
		}

		var res = settings.bucketResolution;
		RuntimeManager.instance.obstacleBuckets = new NativeArray<int2>(res * res, Allocator.Persistent);

		int numBucketedObstacles = 0;
		for(int i = 0; i < numObstacles; i++)
		{
			ObstacleComponent obstacle = obstacleComponents[i];
			Translation translation = obstacleTranslations[i];
			float3 pos = translation.Value;
			float radius = obstacle.radius;

			var startX = math.clamp((int)((pos.x - radius) / settings.mapSize * res), 0, res - 1);
			var endX = math.clamp((int)((pos.x + radius) / settings.mapSize * res), 0, res - 1);

			var startY = math.clamp((int)((pos.y - radius) / settings.mapSize * res), 0, res - 1);
			var endY = math.clamp((int)((pos.y + radius) / settings.mapSize * res), 0, res - 1);

			for (int x = startX; x <= endX; x++)
			{
				for (int y = startY; y <= endY; y++)
				{
					tempObstacleBuckets[x, y].Add(obstacle);
					numBucketedObstacles++;
				}
			}
		}

		var obstacleArchetype = new ComponentType[] { typeof(ObstacleComponent) };

		// sort obstacles and fill buckets
		int2 range = new int2(0, 0);

		for (int x = 0; x < res; x++)
		{
			for (int y = 0; y < res; y++)
			{
				int index = x + y * res;
				foreach (var o in tempObstacleBuckets[x,y])
				{
					var obstacle = o;
					obstacle.bucketIndex = index;
					
                    var entity = EntityManager.CreateEntity(obstacleArchetype);
					EntityManager.SetComponentData(entity, obstacle);
                    
                    range.y++;
				}
				
				RuntimeManager.instance.obstacleBuckets[index] = range;

				range.x += range.y;
				range.y = 0;
			}
		}

	}

    protected void SpawnTargets(ref AntSettings settings)
    {
        NonUniformScale scale = new NonUniformScale();
        scale.Value = Vector3.one * 2 * settings.obstacleRadius / (float)settings.mapSize;

        Translation colonyTranslation = new Translation();
        colonyTranslation.Value = new Vector3(0.5f, 0.5f, 0);
		RuntimeManager.instance.colonyPosition = colonyTranslation.Value;

        var colonyEntity = EntityManager.Instantiate(settings.colonyPrefab);
        EntityManager.SetComponentData(colonyEntity, colonyTranslation);
        EntityManager.AddComponentData(colonyEntity, scale);
        
        float resourceAngle = Random.value * 2f * Mathf.PI;
        Translation resourceTranslation = new Translation();
        resourceTranslation.Value = new Vector3(Mathf.Cos(resourceAngle) * .475f + 0.5f, Mathf.Sin(resourceAngle) * .475f + 0.5f, 0.0f);
		RuntimeManager.instance.resourcePosition = resourceTranslation.Value;

        var resourceEntity = EntityManager.Instantiate(settings.resourcePrefab);
        EntityManager.SetComponentData(resourceEntity, resourceTranslation);
        EntityManager.AddComponentData(resourceEntity, scale);
    }

}