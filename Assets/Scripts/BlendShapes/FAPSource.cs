using animationparameters;
using System.Collections.Generic;
using UnityEngine;
namespace BlendShapes
{
    public class FAPSource
    {
        public readonly FAPType fap;
        public readonly FAPSign sign;
        public readonly float weight;

        public static readonly HashSet<FAPType> EYE_BALL_FAPS_SET = new HashSet<FAPType>
        {
            FAPType.pitch_l_eyeball,
            FAPType.pitch_r_eyeball,
            FAPType.yaw_l_eyeball,
            FAPType.yaw_r_eyeball
        };

        private const float EYE_BALL_AMPLITUDE = (1.0f / 100000.0f) * Mathf.Rad2Deg;
        private const float MAX_EYE_BALL_PITCH = 45f;
        private const float MAX_EYE_BALL_YAW = 60f;

        public FAPSource(FAPType fap, FAPSign sign = FAPSign.Positive, float FAPWeight = 1f)
        {
            this.fap = fap;
            this.sign = sign;
            this.weight = FAPWeight;
        }

        public float GetFapValue(float rawValue)
        {
            float boundary = FAPRanges.FAPBoundary(fap);
            float adjustedValue = rawValue;

            if (EYE_BALL_FAPS_SET.Contains(fap))
            {
                adjustedValue *= EYE_BALL_AMPLITUDE * 10f;
            }

            float normalized = adjustedValue / boundary * 100f;
            normalized *= this.weight;
            return sign == FAPSign.Negative ? -normalized : normalized;
        }

    }
}