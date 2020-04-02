using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour {

    public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail)
    {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);

        int meshSimplificationIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;

        int borderSize = heightMap.GetLength(0);
        int meshSize = borderSize - 2 * meshSimplificationIncrement;
        int meshSizeUnsimple = borderSize - 2; 

        float topLeftX = (meshSize - 1) / -2f;
        float topLeftZ = (meshSize - 1) / 2f;

        int verticesPerLine = (meshSize - 1) / meshSimplificationIncrement + 1;

        MeshData meshData = new MeshData(verticesPerLine);

        int[,] vertexIndicesMap = new int[borderSize, borderSize];
        int meshVertexIndex = 0;
        int borderVertexIndex = -1;

        for (int y = 0; y < borderSize; y += meshSimplificationIncrement)
        {
            for (int x = 0; x < borderSize; x += meshSimplificationIncrement)
            {
                bool isBorderVertex = y == 0 || y == borderSize - 1 || x == 0 || x == borderSize - 1;

                if (isBorderVertex)
                {
                    vertexIndicesMap[x, y] = borderVertexIndex;
                    borderVertexIndex--;
                }
                else
                {
                    vertexIndicesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        for (int y = 0; y < borderSize; y+= meshSimplificationIncrement)
        {
            for (int x = 0; x < borderSize; x+= meshSimplificationIncrement)
            {
                int vertexIndex = vertexIndicesMap[x, y];
                Vector2 percent = new Vector2((x - meshSimplificationIncrement) / (float)meshSize, (y - meshSimplificationIncrement) / (float)meshSize);
                float height = heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
                Vector3 vertexPosition = new Vector3(topLeftX + percent.x * meshSizeUnsimple, height, topLeftZ - percent.y * meshSizeUnsimple);

                meshData.AddVertex(vertexPosition, percent, vertexIndex);

                if (x < borderSize - 1 && y < borderSize - 1)
                {
                    int a = vertexIndicesMap[x, y];
                    int b = vertexIndicesMap[x + meshSimplificationIncrement, y];
                    int c = vertexIndicesMap[x, y + meshSimplificationIncrement];
                    int d = vertexIndicesMap[x + meshSimplificationIncrement, y + meshSimplificationIncrement];
                    meshData.AddTriangle(a,d,c);
                    meshData.AddTriangle(d,a,b);
                }
                vertexIndex++;
            }
        }
        return meshData;
    }
}

public class MeshData
{
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uvs;

    Vector3[] borderVertices;
    int[] borderTriangles;

    int triangleIndex;
    int borderTriangleIndex;

    public MeshData(int verticesPerLine)
    {
        vertices = new Vector3[verticesPerLine * verticesPerLine];
        uvs = new Vector2[verticesPerLine * verticesPerLine];
        triangles = new int[(verticesPerLine - 1) * (verticesPerLine - 1) * 6];

        borderVertices = new Vector3[verticesPerLine * 4 + 4];
        borderTriangles = new int[24 * verticesPerLine];
    }

    public void AddVertex (Vector3 vertexPosition, Vector2 uv, int vertexIndex)
    {
        if(vertexIndex < 0)
        {
            borderVertices[-vertexIndex - 1] = vertexPosition;
        }
        else
        {
            vertices[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle (int a, int b, int c)
    {
        if (a < 0 || b < 0 || c < 0)
        {
            borderTriangles[borderTriangleIndex] = a;
            borderTriangles[borderTriangleIndex + 1] = b;
            borderTriangles[borderTriangleIndex + 2] = c;
            borderTriangleIndex += 3;
        }
        else
        {
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    Vector3[] Calculate()
    {
        Vector3[] vertexes = new Vector3[vertices.Length];
        int triangleCont = triangles.Length / 3;
        for(int i = 0; i < triangleCont; i ++)
        {
            int triangleIndexes = i * 3;
            int vertexIndexA = triangles[triangleIndexes];
            int vertexIndexB = triangles[triangleIndexes + 1];
            int vertexIndexC = triangles[triangleIndexes + 2];

            Vector3 triangleNormal = SurfaceNormal(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexes[vertexIndexA] += triangleNormal;
            vertexes[vertexIndexB] += triangleNormal;
            vertexes[vertexIndexC] += triangleNormal;
        }

        int borderTriangleCont = borderTriangles.Length / 3;
        for (int i = 0; i < borderTriangleCont; i++)
        {
            int triangleIndexes = i * 3;
            int vertexIndexA = borderTriangles[triangleIndexes];
            int vertexIndexB = borderTriangles[triangleIndexes + 1];
            int vertexIndexC = borderTriangles[triangleIndexes + 2];

            Vector3 triangleNormal = SurfaceNormal(vertexIndexA, vertexIndexB, vertexIndexC);
            if (vertexIndexA >= 0)
            {
                vertexes[vertexIndexA] += triangleNormal;
            }
            if (vertexIndexB >= 0)
            {
                vertexes[vertexIndexB] += triangleNormal;
            }
            if (vertexIndexC >= 0)
            {
                vertexes[vertexIndexC] += triangleNormal;
            }
        }

        for (int i = 0; i < vertexes.Length; i++)
        {
            vertexes[i].Normalize();
        }

        return vertexes;
    }

    Vector3 SurfaceNormal(int indexA, int indexB, int indexC)
    {
        Vector3 pointA = (indexA < 0) ? borderVertices[-indexA - 1] : vertices[indexA];
        Vector3 pointB = (indexB < 0) ? borderVertices[-indexB - 1] : vertices[indexB];
        Vector3 pointC = (indexC < 0) ? borderVertices[-indexC - 1] : vertices[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    } 

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = Calculate();
        return mesh;
    }
}