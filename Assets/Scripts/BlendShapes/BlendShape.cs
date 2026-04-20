
namespace BlendShapes
{
    public class BlendShape
    {
        public readonly string blendShapeName;
        public readonly float weight;

        public BlendShape(string name, float weight = 1f)
        {
            this.blendShapeName = name;
            this.weight = weight;
        }
    }
}