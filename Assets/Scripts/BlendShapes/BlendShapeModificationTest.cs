using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlendShapeModificationTest : MonoBehaviour
{
    private SkinnedMeshRenderer[] skinnedMeshRenderers;
    [SerializeField] private float transitionSpeed = 1f;

    void Start()
    {
        skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Transform[] childArray = GetComponentsInChildren<Transform>();


        String boneNames = "";
        foreach (Transform child in childArray)
        {
            boneNames += child.gameObject.name + " ";
        }

        //Debug.Log(boneNames);


        var blendshapeNames = new LinkedList<string>();
        var seen = new HashSet<string>();

        foreach (var smr in skinnedMeshRenderers)
        {
            if (smr.sharedMesh == null) continue;
            int count = smr.sharedMesh.blendShapeCount;
            for (int j = 0; j < count; j++)
            {
                string name = smr.sharedMesh.GetBlendShapeName(j);
                if (seen.Add(name))
                    blendshapeNames.AddLast(name);
            }
        }

        Debug.Log(string.Join(" \n", blendshapeNames));
    }
}