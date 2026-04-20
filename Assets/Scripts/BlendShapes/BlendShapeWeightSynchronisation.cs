using UnityEngine;

public class BlendShapeWeightSynchronisation : MonoBehaviour
{
    public SkinnedMeshRenderer master; 
    public SkinnedMeshRenderer[] meshRenderers;     
    
    void LateUpdate()
    {
        if (master == null) return;

        Mesh masterMesh = master.sharedMesh;

        for (int i = 0; i < masterMesh.blendShapeCount; i++)
        {
            string blendShapeName = masterMesh.GetBlendShapeName(i);
            float weight = master.GetBlendShapeWeight(i);

            foreach (var meshRenderer in meshRenderers)
            {
                if (meshRenderer == null) continue;

                int targetIndex = meshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);
                if (targetIndex >= 0)
                {
                    meshRenderer.SetBlendShapeWeight(targetIndex, weight);
                }
            }
        }
    }
}