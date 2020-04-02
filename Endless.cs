using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Endless : MonoBehaviour {

    const float scale = 5f;

    const float pointOfViewMovement = 25f;
    const float sqrPointOfViewMovement = pointOfViewMovement * pointOfViewMovement;

    public LevelDetailStatus[] levelDetails;
    public static float maxViewDistance;

    public Transform viewer;
    public Material worldMaterial;

    public static Vector2 pointOfViewPosition;
    Vector2 previousPointOfViewPosition;
    static WorldGenerator worldGenerator;

    int chunkSize;
    int chunkVisibleInViewDst;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    void Start()
    {
        worldGenerator = FindObjectOfType<WorldGenerator>();

        maxViewDistance = levelDetails [levelDetails.Length-1].visibleTreshold;
        chunkSize = WorldGenerator.worldPartSize - 1;
        chunkVisibleInViewDst = Mathf.RoundToInt(maxViewDistance / chunkSize);

        UpdateVisibleChunks();
    }

    void Update()
    {
        pointOfViewPosition = new Vector2(viewer.position.x, viewer.position.z) /scale;
        if ((previousPointOfViewPosition - pointOfViewPosition).sqrMagnitude > sqrPointOfViewMovement)
        {
            previousPointOfViewPosition = pointOfViewPosition;
            UpdateVisibleChunks();
        }   
    }

    void UpdateVisibleChunks(){

        for(int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(pointOfViewPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(pointOfViewPosition.y / chunkSize);

        for (int yOffSet = -chunkVisibleInViewDst; yOffSet <= chunkVisibleInViewDst; yOffSet++)
        {
            for (int xOffSet = -chunkVisibleInViewDst; xOffSet <= chunkVisibleInViewDst; xOffSet++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffSet, currentChunkCoordY + yOffSet);
                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                }
                else
                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, levelDetails, transform, worldMaterial));
                }
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;

        LevelDetailStatus[] levelDetails;
        LevelDetailMesh[] levelDetailMeshes;

        WorldStats worldStats;
        bool WorldInfoRecieved;
        int previousIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LevelDetailStatus[] levelDetails, Transform parent, Material material)
        {
            this.levelDetails = levelDetails;

            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;

            meshObject.transform.position = positionV3 * scale;
            //meshObject.transform.localScale = Vector3.one * size / 10f;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisible(true);

            levelDetailMeshes = new LevelDetailMesh[levelDetails.Length];
            for(int i = 0; i < levelDetails.Length; i++)
            {
                levelDetailMeshes[i] = new LevelDetailMesh(levelDetails[i].levelDetail, UpdateTerrainChunk);
            }

            worldGenerator.EnteringInput(position, OnWorldInfoReceived);
        }

        void OnWorldInfoReceived(WorldStats worldStats)
        {
            this.worldStats = worldStats;
            WorldInfoRecieved = true;

            Texture2D texture2D = Texturing.TextureFromColourMap(worldStats.colourWorld, WorldGenerator.worldPartSize, WorldGenerator.worldPartSize);
            meshRenderer.material.mainTexture = texture2D; 

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (WorldInfoRecieved)
            {
                float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(pointOfViewPosition));
                bool visible = viewerDstFromNearestEdge <= maxViewDistance;

                if (visible)
                {
                    int LevelIndex = 0;
                    for (int i = 0; i < levelDetails.Length - 1; i++)
                    {
                        if (viewerDstFromNearestEdge > levelDetails[i].visibleTreshold)
                        {
                            LevelIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (LevelIndex != previousIndex)
                    {
                        LevelDetailMesh levelDetailMesh = levelDetailMeshes[LevelIndex];
                        if (levelDetailMesh.getMesh)
                        {
                            meshFilter.mesh = levelDetailMesh.mesh;
                        }
                        else if (!levelDetailMesh.selectedPart)
                        {
                            levelDetailMesh.Meshing(worldStats);
                        }
                    }

                    terrainChunksVisibleLastUpdate.Add(this);

                }
                SetVisible(visible);
            }
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LevelDetailMesh
    {
        public Mesh mesh;
        public bool selectedPart;
        public bool getMesh;
        int levelDetail;
        System.Action autoUpdating;

        public LevelDetailMesh(int levelDetail, System.Action autoUpdating)
        {
            this.levelDetail = levelDetail;
            this.autoUpdating = autoUpdating;
        }

        void ReceivingMesh(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            getMesh = true;

            autoUpdating();
        }

        public void Meshing(WorldStats worldStats)
        {
            selectedPart = true;
            worldGenerator.EnteringMeshInput(worldStats, levelDetail, ReceivingMesh);
        }
    }

    [System.Serializable]
    public struct LevelDetailStatus
    {
        public int levelDetail;
        public float visibleTreshold;
    }
}
