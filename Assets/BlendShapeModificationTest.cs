using System.Collections;
using UnityEngine;

public class BlendShapeModificationTest : MonoBehaviour
{
    SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private float transitionSpeed = 1f;

    void Start()
    {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();


        int blendShapeCount = skinnedMeshRenderer.sharedMesh.blendShapeCount;
        for (int j = 1; j < blendShapeCount; j++)
        {
            string name = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(j);
            if(
                   name == "Eye_Cheek_Raise_L" 
                || name == "Eye_Cheek_Raise_R" 
                || name == "Mouth_Corner_Pull_L"
                || name == "Mouth_Corner_Pull_R"
                )
            {
                skinnedMeshRenderer.SetBlendShapeWeight(j, 70);
            }


        }
        //StartCoroutine(AnimateBlendShapes());
    }
    IEnumerator AnimateBlendShapes()
    {
        int blendShapeCount = skinnedMeshRenderer.sharedMesh.blendShapeCount;
        skinnedMeshRenderer.SetBlendShapeWeight(0, 100);

        for (int j = 1; j < blendShapeCount; j++)
        {
            float elapsed = 0f;

            while (elapsed < transitionSpeed)
            {
                elapsed += Time.deltaTime * 5;
                float t = Mathf.Clamp01(elapsed / transitionSpeed);
                skinnedMeshRenderer.SetBlendShapeWeight(j - 1, Mathf.SmoothStep(100, 0, t));
                skinnedMeshRenderer.SetBlendShapeWeight(j, Mathf.SmoothStep(0, 100, t));
                yield return null;
            }
        }
    }
}