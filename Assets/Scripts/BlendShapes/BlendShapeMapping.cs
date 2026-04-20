using animationparameters;
using System.Linq;
using UnityEngine;

namespace BlendShapes
{


    public class BlendShapeMapping
    {
        public readonly ARKitBlendShape ARKITBlendShape;
        public readonly FAPSource[] fapSources;
        public readonly BlendShape[] modelBlendShape;

        public BlendShapeMapping(
            ARKitBlendShape ARKITBlendShape,
            FAPSource[] fapSources,
            BlendShape[] modelBlendShape)
        {
            this.ARKITBlendShape = ARKITBlendShape;
            this.fapSources = fapSources;
            this.modelBlendShape = modelBlendShape;
        }

        public float GetBlendShapeValue(AnimationParametersFrame frame)
        {
            float sum = 0f;
            int activeCount = 0;
            foreach (var fs in fapSources)
            {
                if (!frame.getMask(fs.fap)) continue;
                float val = fs.GetFapValue(frame.getValue(fs.fap));
                sum += val;
                activeCount++;
            }
            if (activeCount == 0) return 0f;
            return Mathf.Clamp(sum / activeCount, 0f, 100f);
        }
    }

    public class DirectBlendShapeMapping
    {
        public readonly string blendShapeName;
        public readonly FAPSource[] fapSources;

        public DirectBlendShapeMapping(string blendShapeName, FAPSource[] fapSources)
        {
            this.blendShapeName = blendShapeName;
            this.fapSources = fapSources;
        }

        public float GetBlendShapeValue(AnimationParametersFrame frame)
        {
            float sum = 0f;
            int activeCount = 0;

            foreach (var fs in fapSources)
            {
                if (!frame.getMask(fs.fap)) continue;   
                float val = fs.GetFapValue(frame.getValue(fs.fap));
                if (val == 0f) continue;                
                sum += val;
                activeCount++;
            }

            if (activeCount == 0) return 0f;
            return Mathf.Clamp(sum / activeCount, 0f, 100f);
        }
    }
    
}