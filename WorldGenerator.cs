using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

public class WorldGenerator : MonoBehaviour {

    public enum DrawMode {NoiseWorld, ColourWorld, Mesh, FallMap};
    public DrawMode drawMode;

    public Noise.NormalMode normal;

    public const int worldPartSize = 241;
    [Range(0,6)]
    public int ValueLevelOfDetail;
    //public int mapChunkSize;
    //public int mapChunkSize;
    public float noiseScale;

    public int octaves = 5;
    [Range(0,1)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;

    public bool fallUsed;

    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    public bool autoUpdate;

    public TerrainType[] regions;

    float[,] fallMap;

    void Awake()
    {
        fallMap = FallGenerator.GenerateFall(worldPartSize);
    }

    Queue<WorldThreadingStats<WorldStats>> worldStatsThreadQueue = new Queue<WorldThreadingStats<WorldStats>>();
    Queue<WorldThreadingStats<MeshData>> meshStatsThreadQueue = new Queue<WorldThreadingStats<MeshData>>();
    public void RendWorld()
    {
        WorldStats worldStats = GenerateWorld(Vector2.zero);
        WorldDisplay display = FindObjectOfType<WorldDisplay>();
        if (drawMode == DrawMode.NoiseWorld)
        {
            display.DrawTexture(Texturing.TextureFromHeightMap(worldStats.heightWorld));
        }
        else if (drawMode == DrawMode.ColourWorld)
        {
            display.DrawTexture(Texturing.TextureFromColourMap(worldStats.colourWorld, worldPartSize, worldPartSize));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(worldStats.heightWorld, meshHeightMultiplier, meshHeightCurve, ValueLevelOfDetail), Texturing.TextureFromColourMap(worldStats.colourWorld, worldPartSize, worldPartSize));
        }
        else if (drawMode == DrawMode.FallMap)
        {
            display.DrawTexture(Texturing.TextureFromHeightMap(FallGenerator.GenerateFall(worldPartSize)));
        }
    }

    public void EnteringInput(Vector2 centre ,Action<WorldStats> callback)
    {
        ThreadStart threadStart = delegate
        {
            ThreadOfWorld(centre, callback);
        };

        new Thread(threadStart).Start();
    }

    void ThreadOfWorld(Vector2 centre, Action<WorldStats> callback)
    {
        WorldStats worldStats = GenerateWorld(centre);
        lock (worldStatsThreadQueue)
        {
            worldStatsThreadQueue.Enqueue(new WorldThreadingStats<WorldStats>(callback, worldStats));
        }
    }

    public void EnteringMeshInput(WorldStats worldStats, int LevelOfDetails, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            ThreadOfMesh(worldStats, LevelOfDetails, callback);
        };

        new Thread(threadStart).Start();
    }

    void ThreadOfMesh(WorldStats worldStats, int LevelOfDetails, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(worldStats.heightWorld, meshHeightMultiplier, meshHeightCurve, LevelOfDetails);
        lock (meshStatsThreadQueue)
        {
            meshStatsThreadQueue.Enqueue(new WorldThreadingStats<MeshData>(callback, meshData));
        }
    }

    void Update()
    {
        if(worldStatsThreadQueue.Count > 0)
        {
            for(int i = 0; i < worldStatsThreadQueue.Count; i++)
            {
                WorldThreadingStats<WorldStats> threadStats = worldStatsThreadQueue.Dequeue();
                threadStats.callback(threadStats.value);
            }
        }    

        if(meshStatsThreadQueue.Count > 0)
        {
            for (int i = 0; i < meshStatsThreadQueue.Count; i++)
            {
                WorldThreadingStats<MeshData> threadStats = meshStatsThreadQueue.Dequeue();
                threadStats.callback(threadStats.value);
            }
        }
    }

    WorldStats GenerateWorld(Vector2 centre)
    {
        float[,] noiseWorld = Noise.GenerateNoiseMap (worldPartSize + 2, worldPartSize + 2, seed, noiseScale, octaves, persistance, lacunarity, centre + offset, normal);

        Color[] colourWorld = new Color[worldPartSize * worldPartSize];
        for (int y = 0; y < worldPartSize; y++)
        { 
            for(int x = 0; x < worldPartSize; x++)
            {
                if (fallUsed)
                {
                    noiseWorld[x, y] = Mathf.Clamp01(noiseWorld[x, y] - fallMap[x, y]);
                }
                float currentHeight = noiseWorld[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if(currentHeight >= regions[i].height)
                    {
                        colourWorld [y * worldPartSize + x] = regions[i].colour;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        return new WorldStats(noiseWorld, colourWorld);
    }

    void OnValidate()
    {
        if (lacunarity < 1)
        {
            lacunarity = 1;
        }
        if (octaves < 0)
        {
            octaves = 0;
        }

        fallMap = FallGenerator.GenerateFall(worldPartSize);  
    }

    struct WorldThreadingStats<T>
    {
        public readonly Action<T> callback;
        public readonly T value;

        public WorldThreadingStats(Action<T> callback, T value)
        {
            this.callback = callback;
            this.value = value;
        }
    }
}


[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color colour;
}

public struct WorldStats
{
    public float[,] heightWorld;
    public Color[] colourWorld;

    public WorldStats(float[,] heightWorld, Color[] colourWorld)
    {
        this.heightWorld = heightWorld;
        this.colourWorld = colourWorld;
    }
}