// BakeSmoothNormalsToVertexColor.cs
//
// Editor script rozwiązujący pękanie toon outline'a na szwach UV.
//
// PROBLEM: Import modelu duplikuje wierzchołki na UV seamach, żeby każda
// kopia mogła mieć inne UV. Każda kopia dostaje też swoją (poprawną do
// oświetlenia) normalną - ale to oznacza, że w tym samym miejscu w
// przestrzeni mamy 2+ wierzchołków z różnymi normalnymi. Outline shader
// wypychający wierzchołki wzdłuż normalOS w takim miejscu "pęka" -
// kopie wierzchołków rozjeżdżają się w różne strony.
//
// ROZWIĄZANIE: Ten skrypt liczy normalną UŚREDNIONĄ po wszystkich
// wierzchołkach o tej samej pozycji w object space (czyli "jak by
// wyglądała normalna, gdyby nie było seamu") i zapisuje ją w kanale
// vertex color (RGB, zmapowane z [-1,1] na [0,1]). Outline shader
// wypycha wierzchołki wzdłuż TEJ normalnej zamiast wzdłuż NORMAL -
// dzięki czemu wszystkie kopie wierzchołka w tym samym miejscu
// wypychają się identycznie i szew się nie rozjeżdża.
//
// UŻYCIE:
// 1. Wrzuć ten plik do folderu Editor/ w projekcie.
// 2. Zaznacz model (prefab/instancję w scenie) z komponentem MeshFilter
//    lub SkinnedMeshRenderer.
// 3. Tools > SteamSpirit > Bake Smooth Normals To Vertex Color
// 4. Zapisz wynikowy mesh jako osobny asset (skrypt tworzy kopię -
//    NIE nadpisuje oryginalnego mesha z importu, żeby reimport go
//    nie nadpisał z powrotem).

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class BakeSmoothNormalsToVertexColor
{
    [MenuItem("Tools/SteamSpirit/Bake Smooth Normals To Vertex Color")]
    private static void BakeSelected()
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("[BakeSmoothNormals] Zaznacz obiekt z MeshFilter lub SkinnedMeshRenderer.");
            return;
        }

        int processed = 0;

        foreach (var go in Selection.gameObjects)
        {
            Mesh sourceMesh = GetMesh(go, out bool isSkinned);
            if (sourceMesh == null)
            {
                Debug.LogWarning($"[BakeSmoothNormals] Pominięto '{go.name}' - brak mesha.", go);
                continue;
            }

            Mesh bakedMesh = BakeMesh(sourceMesh);

            string path = SaveMeshAsset(bakedMesh, sourceMesh.name);
            AssignMesh(go, bakedMesh, isSkinned);

            Debug.Log($"[BakeSmoothNormals] Zapisano '{path}' i podpięto do '{go.name}'.", go);
            processed++;
        }

        Debug.Log($"[BakeSmoothNormals] Gotowe. Przetworzono {processed} obiekt(ów).");
    }

    private static Mesh GetMesh(GameObject go, out bool isSkinned)
    {
        var skinned = go.GetComponent<SkinnedMeshRenderer>();
        if (skinned != null && skinned.sharedMesh != null)
        {
            isSkinned = true;
            return skinned.sharedMesh;
        }

        var filter = go.GetComponent<MeshFilter>();
        isSkinned = false;
        return filter != null ? filter.sharedMesh : null;
    }

    private static void AssignMesh(GameObject go, Mesh mesh, bool isSkinned)
    {
        if (isSkinned)
        {
            var skinned = go.GetComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
        }
        else
        {
            var filter = go.GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
        }
    }

    private static Mesh BakeMesh(Mesh source)
    {
        Mesh baked = Object.Instantiate(source);
        baked.name = source.name + "_SmoothOutlineNormals";

        Vector3[] vertices = baked.vertices;
        Vector3[] normals = baked.normals;

        if (normals == null || normals.Length != vertices.Length)
        {
            Debug.LogWarning($"[BakeSmoothNormals] Mesh '{source.name}' nie ma poprawnych normalnych - pomijam.");
            return baked;
        }

        // Grupowanie wierzchołków po pozycji (z tolerancją na błędy zmiennoprzecinkowe)
        var positionGroups = new Dictionary<Vector3Int, List<int>>();
        const float precision = 10000f; // ok. 0.0001 jednostki tolerancji

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertices[i].x * precision),
                Mathf.RoundToInt(vertices[i].y * precision),
                Mathf.RoundToInt(vertices[i].z * precision));

            if (!positionGroups.TryGetValue(key, out var list))
            {
                list = new List<int>();
                positionGroups[key] = list;
            }
            list.Add(i);
        }

        // Uśrednienie normalnej w każdej grupie i zapis do vertex color
        Color[] colors = new Color[vertices.Length];

        foreach (var kvp in positionGroups)
        {
            Vector3 avgNormal = Vector3.zero;
            foreach (int idx in kvp.Value)
                avgNormal += normals[idx];

            avgNormal.Normalize();

            // Mapowanie [-1,1] -> [0,1] do zapisu w vertex color (8-bit per channel)
            Color packed = new Color(
                avgNormal.x * 0.5f + 0.5f,
                avgNormal.y * 0.5f + 0.5f,
                avgNormal.z * 0.5f + 0.5f,
                1f);

            foreach (int idx in kvp.Value)
                colors[idx] = packed;
        }

        baked.colors = colors;
        return baked;
    }

    private static string SaveMeshAsset(Mesh mesh, string originalName)
    {
        string folder = "Assets/GeneratedMeshes";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");

        string path = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(folder, originalName + "_SmoothOutlineNormals.asset"));

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return path;
    }
}
