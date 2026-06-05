using animationparameters;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlendShapes
{
    public static class BlendShapeMappingProfile
    { 
        // def FAP -> ARKit (commune à tous les presets)
        private static readonly (ARKitBlendShape arkit, FAPSource[] faps)[] FAPToARKit =
         {
            (ARKitBlendShape.browInnerUp,         new[]{ 
				new FAPSource(FAPType.raise_l_i_eyebrow), 
				new FAPSource(FAPType.raise_r_i_eyebrow) }),
            (ARKitBlendShape.browOuterUpLeft,     new[]{ 
				new FAPSource(FAPType.raise_l_o_eyebrow), 
				new FAPSource(FAPType.raise_l_m_eyebrow) }),
            (ARKitBlendShape.browOuterUpRight,    new[]{ 
				new FAPSource(FAPType.raise_r_o_eyebrow), 
				new FAPSource(FAPType.raise_r_m_eyebrow) }),
            (ARKitBlendShape.browDownLeft,        new[]{ 
				new FAPSource(FAPType.squeeze_l_eyebrow), 
				new FAPSource(FAPType.raise_l_o_eyebrow, FAPSign.Negative), 
				new FAPSource(FAPType.raise_l_m_eyebrow, FAPSign.Negative) }),
            (ARKitBlendShape.browDownRight,       new[]{ 
				new FAPSource(FAPType.squeeze_r_eyebrow), 
				new FAPSource(FAPType.raise_r_o_eyebrow, FAPSign.Negative), 
				new FAPSource(FAPType.raise_r_m_eyebrow, FAPSign.Negative) }),
            (ARKitBlendShape.eyeBlinkLeft,        new[]{ 
				new FAPSource(FAPType.close_t_l_eyelid)}),
            (ARKitBlendShape.eyeBlinkRight,       new[]{ 
				new FAPSource(FAPType.close_t_r_eyelid)}),
            (ARKitBlendShape.eyeWideLeft,         new[]{ 
				new FAPSource(FAPType.close_t_l_eyelid, FAPSign.Negative),  }),
            (ARKitBlendShape.eyeWideRight,        new[]{ 
				new FAPSource(FAPType.close_t_r_eyelid, FAPSign.Negative),  }),
            (ARKitBlendShape.eyeSquintLeft,       new[]{ 
				new FAPSource(FAPType.close_b_l_eyelid) }),
            (ARKitBlendShape.eyeSquintRight,      new[]{ 
				new FAPSource(FAPType.close_b_r_eyelid) }),
            (ARKitBlendShape.eyeLookUpLeft,       new[]{ 
				new FAPSource(FAPType.pitch_l_eyeball, FAPSign.Negative) }),
            (ARKitBlendShape.eyeLookUpRight,      new[]{ 
				new FAPSource(FAPType.pitch_r_eyeball, FAPSign.Negative) }),
            (ARKitBlendShape.eyeLookDownLeft,     new[]{ 
				new FAPSource(FAPType.pitch_l_eyeball) }),
            (ARKitBlendShape.eyeLookDownRight,    new[]{ 
				new FAPSource(FAPType.pitch_r_eyeball) }),
            (ARKitBlendShape.eyeLookInLeft,       new[]{ 
				new FAPSource(FAPType.yaw_l_eyeball, FAPSign.Negative) }),
            (ARKitBlendShape.eyeLookOutLeft,      new[]{ 
				new FAPSource(FAPType.yaw_l_eyeball) }),
            (ARKitBlendShape.eyeLookInRight,      new[]{ 
				new FAPSource(FAPType.yaw_r_eyeball) }),
            (ARKitBlendShape.eyeLookOutRight,     new[]{ 
				new FAPSource(FAPType.yaw_r_eyeball, FAPSign.Negative) }),
            (ARKitBlendShape.cheekSquintLeft,     new[]{ 
				new FAPSource(FAPType.lift_l_cheek) }),
            (ARKitBlendShape.cheekSquintRight,    new[]{ 
				new FAPSource(FAPType.lift_r_cheek) }),
            (ARKitBlendShape.cheekPuff,           new[]{ 
				new FAPSource(FAPType.puff_l_cheek), 
				new FAPSource(FAPType.puff_r_cheek) }),
            (ARKitBlendShape.noseSneerLeft,       new[]{ 
				new FAPSource(FAPType.raise_nose), 
				new FAPSource(FAPType.bend_nose, FAPSign.Negative) }),
            (ARKitBlendShape.noseSneerRight,      new[]{ 
				new FAPSource(FAPType.raise_nose), 
				new FAPSource(FAPType.bend_nose) }),
            (ARKitBlendShape.jawOpen,             new[]{ 
				new FAPSource(FAPType.open_jaw) }),
            (ARKitBlendShape.jawForward,          new[]{ 
				new FAPSource(FAPType.thrust_jaw) }),
            (ARKitBlendShape.jawLeft,             new[]{ 
				new FAPSource(FAPType.shift_jaw, FAPSign.Negative) }),
            (ARKitBlendShape.jawRight,            new[]{ 
				new FAPSource(FAPType.shift_jaw) }),
            (ARKitBlendShape.mouthClose,          new[]{
				new FAPSource(FAPType.push_t_lip, FAPSign.Negative),
				new FAPSource(FAPType.push_b_lip, FAPSign.Negative) }),
            (ARKitBlendShape.mouthFunnel,         new[]{
				new FAPSource(FAPType.stretch_r_cornerlip, FAPSign.Negative),
				new FAPSource(FAPType.stretch_l_cornerlip, FAPSign.Negative),                
				new FAPSource(FAPType.raise_b_lip_lm, FAPSign.Negative),
				new FAPSource(FAPType.raise_b_lip_rm, FAPSign.Negative)}),
            (ARKitBlendShape.mouthPucker,         new[]{ 
				new FAPSource(FAPType.stretch_l_cornerlip_o, FAPSign.Negative), 
				new FAPSource(FAPType.stretch_r_cornerlip_o, FAPSign.Negative), }),
            (ARKitBlendShape.mouthLeft,           new[]{ 
				new FAPSource(FAPType.stretch_l_cornerlip, FAPSign.Negative) }),
            (ARKitBlendShape.mouthRight,          new[]{ 
				new FAPSource(FAPType.stretch_r_cornerlip, FAPSign.Negative) }),
            (ARKitBlendShape.mouthSmileLeft,      new[]{ 
				new FAPSource(FAPType.raise_l_cornerlip_o) }),
            (ARKitBlendShape.mouthSmileRight,     new[]{ 
				new FAPSource(FAPType.raise_r_cornerlip_o) }),
            (ARKitBlendShape.mouthFrownLeft,      new[]{ 
				new FAPSource(FAPType.raise_l_cornerlip_o, FAPSign.Negative) }),
            (ARKitBlendShape.mouthFrownRight,     new[]{ 
				new FAPSource(FAPType.raise_r_cornerlip_o, FAPSign.Negative) }),
            (ARKitBlendShape.mouthDimpleLeft,     new[]{ 
				new FAPSource(FAPType.stretch_l_cornerlip) }),
            (ARKitBlendShape.mouthDimpleRight,    new[]{ 
				new FAPSource(FAPType.stretch_r_cornerlip) }),
            (ARKitBlendShape.mouthStretchLeft,    new[]{ 
				new FAPSource(FAPType.stretch_l_cornerlip_o) }),
            (ARKitBlendShape.mouthStretchRight,   new[]{ 
				new FAPSource(FAPType.stretch_r_cornerlip_o) }),
            (ARKitBlendShape.mouthRollUpper,      new[]{ 
				new FAPSource(FAPType.push_t_lip) }),
            (ARKitBlendShape.mouthRollLower,      new[]{ 
				new FAPSource(FAPType.push_b_lip) }),
            (ARKitBlendShape.mouthShrugUpper,     new[]{ 
				new FAPSource(FAPType.lower_t_midlip_o, FAPSign.Negative) }),
            (ARKitBlendShape.mouthShrugLower,     new[]{ 
				new FAPSource(FAPType.raise_b_lip_rm), 
				new FAPSource(FAPType.raise_b_lip_lm) }),
            (ARKitBlendShape.mouthPressLeft,      new[]{ 
				new FAPSource(FAPType.lower_t_lip_lm), 
				new FAPSource(FAPType.lower_t_lip_lm_o) }),
            (ARKitBlendShape.mouthPressRight,     new[]{ 
				new FAPSource(FAPType.lower_t_lip_rm), 
				new FAPSource(FAPType.lower_t_lip_rm_o) }),
            (ARKitBlendShape.mouthUpperUpLeft,    new[]{ 
				new FAPSource(FAPType.lower_t_lip_lm_o, FAPSign.Negative) }),
            (ARKitBlendShape.mouthUpperUpRight,   new[]{ 
				new FAPSource(FAPType.lower_t_lip_rm_o, FAPSign.Negative) }),
            (ARKitBlendShape.mouthLowerDownLeft,  new[]{ 
				new FAPSource(FAPType.raise_b_lip_lm, FAPSign.Negative), 
				new FAPSource(FAPType.raise_b_lip_lm_o, FAPSign.Negative) }),
            (ARKitBlendShape.mouthLowerDownRight, new[]{ 
				new FAPSource(FAPType.raise_b_lip_rm, FAPSign.Negative), 
				new FAPSource(FAPType.raise_b_lip_rm_o, FAPSign.Negative) }),
            (ARKitBlendShape.tongueOut,           new[]{ 
				new FAPSource(FAPType.thrust_tongue_tip), 
				new FAPSource(FAPType.raise_tongue_tip) }),
        };


        public static List<BlendShapeMapping> BlendShapeMappingBuild(Dictionary<ARKitBlendShape, List<BlendShape>> blendshapes)
        {
            var list = new List<BlendShapeMapping>();
            foreach (var (arkit, faps) in FAPToARKit)
            {
                if (!blendshapes.TryGetValue(arkit, out var bsNames)) continue;
                var modelsBlendShape = new BlendShape[bsNames.Count];
                for (int i = 0; i < bsNames.Count; i++)
                    modelsBlendShape[i] = bsNames[i];
                list.Add(new BlendShapeMapping(arkit, faps, modelsBlendShape));
            }
            return list;
        }
    }
}