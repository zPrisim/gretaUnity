using UnityEngine;

public class BlendShapeWeightSynchronisation : MonoBehaviour
{
    public SkinnedMeshRenderer master; 
    public SkinnedMeshRenderer[] targets;       
    void LateUpdate()
    {
        if (master == null) return;

        Mesh masterMesh = master.sharedMesh;

        for (int i = 0; i < masterMesh.blendShapeCount; i++)
        {
            string blendShapeName = masterMesh.GetBlendShapeName(i);
            float weight = master.GetBlendShapeWeight(i);

            foreach (var target in targets)
            {
                if (target == null) continue;

                int targetIndex = target.sharedMesh.GetBlendShapeIndex(blendShapeName);
                if (targetIndex >= 0)
                {
                    target.SetBlendShapeWeight(targetIndex, weight);
                }
            }
        }
    }
}