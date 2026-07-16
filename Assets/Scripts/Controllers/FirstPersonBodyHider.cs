using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Permanently slices out the player's head, neck, and upper collar/chest mesh at startup.
/// This prevents camera clipping when looking down while keeping the original character model,
/// belly/torso, arms, shoulders, and legs/feet 100% intact and visible.
/// </summary>
[DisallowMultipleComponent]
public class FirstPersonBodyHider : MonoBehaviour
{
    private void Start()
    {
        var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null || smr.sharedMesh == null)
        {
            Debug.LogWarning("[FirstPersonBodyHider] SkinnedMeshRenderer or sharedMesh not found on Player!");
            return;
        }

        // Clone the mesh so we don't modify the source asset on disk
        Mesh originalMesh = smr.sharedMesh;
        Mesh slicedMesh = Instantiate(originalMesh);
        slicedMesh.name = originalMesh.name + "_sliced";

        // Find bone indices in SkinnedMeshRenderer.bones list that we want to hide
        var bones = smr.bones;
        var hideIndices = new HashSet<int>();
        for (int i = 0; i < bones.Length; i++)
        {
            string bName = bones[i].name.ToLower();
            
            // Hide only the head, neck, and the upper chest/collar bone ("Spine").
            // We keep the lower torso ("Spine01", "Spine02"), shoulders, arms, legs, and feet visible.
            bool hide = bName.Contains("head") || bName == "neck" || bName == "spine";
                        
            if (hide)
            {
                hideIndices.Add(i);
            }
        }

        // Get weights, triangles, and vertices
        var weights = slicedMesh.boneWeights;
        var triangles = slicedMesh.triangles;
        var vertices = slicedMesh.vertices;
        bool[] hideVert = new bool[vertices.Length];

        // Identify vertices weighted primarily to hidden bones
        for (int i = 0; i < vertices.Length; i++)
        {
            var w = weights[i];
            int bestBone = w.boneIndex0;
            float bestWeight = w.weight0;
            if (w.weight1 > bestWeight) { bestBone = w.boneIndex1; bestWeight = w.weight1; }
            if (w.weight2 > bestWeight) { bestBone = w.boneIndex2; bestWeight = w.weight2; }
            if (w.weight3 > bestWeight) { bestBone = w.boneIndex3; bestWeight = w.weight3; }

            if (hideIndices.Contains(bestBone))
            {
                hideVert[i] = true;
            }
        }

        // Rebuild triangles: discard any triangle that contains a hidden vertex
        var newTriangles = new List<int>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];

            if (hideVert[v0] || hideVert[v1] || hideVert[v2])
            {
                continue;
            }
            newTriangles.Add(v0);
            newTriangles.Add(v1);
            newTriangles.Add(v2);
        }

        // Assign the modified triangles back to the cloned mesh
        slicedMesh.triangles = newTriangles.ToArray();
        slicedMesh.RecalculateBounds();

        // Assign the sliced mesh to the SkinnedMeshRenderer
        smr.sharedMesh = slicedMesh;

        Debug.Log($"[FirstPersonBodyHider] Successfully sliced mesh. Kept {newTriangles.Count / 3} of {triangles.Length / 3} triangles.");
    }
}
