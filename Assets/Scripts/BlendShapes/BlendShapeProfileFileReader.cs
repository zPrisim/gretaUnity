using animationparameters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;


namespace BlendShapes
{
    public static class BlendShapeProfileFileReader
    {

        public static Dictionary<ARKitBlendShape, List<BlendShape>> FileReader(string file_path)
        {
            StreamReader sr = new StreamReader(file_path);
            Dictionary<ARKitBlendShape, List<BlendShape>> BlendShapeProfileDict = new Dictionary<ARKitBlendShape, List<BlendShape>>();
            while (!sr.EndOfStream)
            {
                string srLine = sr.ReadLine();
                if (srLine.StartsWith("//"))
                    continue;

                srLine = Regex.Replace(srLine, @"\s", "");
                string[] srLineSplit = srLine.Split('=');
                ARKitBlendShape ARKITBS = (ARKitBlendShape)Enum.Parse(typeof(ARKitBlendShape), srLineSplit[0]);
                string[] bs = srLineSplit[1].Split(',');


                List<BlendShape> blendShapes = new List<BlendShape>();
                foreach (var b in bs)
                {
                    if (string.IsNullOrEmpty(b)) continue;
                    string[] tmp = b.Split(':');
                    float weight = tmp.Length > 1 ? float.Parse(tmp[1], System.Globalization.CultureInfo.InvariantCulture) : 1f;
                    blendShapes.Add(new BlendShape(tmp[0], weight));
                }


                BlendShapeProfileDict.Add(ARKITBS, blendShapes);
            }

            //foreach(var bsp in BlendShapeProfileDict)
            //{
            //    Debug.Log($"ARKITBS: {bsp.ARKITBS}, Values: ");
            //    foreach(var s in bsp.Value)
            //    {
            //        Debug.Log(s);

            //    }
            //}

            sr.Close();
            return BlendShapeProfileDict;
        }

    }
    public static class VisemeProfileFileReader
    {
        public static Dictionary<string, (string name, float weight)[]> FileReader(string file_path)
        {
            var dict = new Dictionary<string, (string name, float weight)[]>();
            StreamReader sr = new StreamReader(file_path);
            while (!sr.EndOfStream)
            {
                string srLine = sr.ReadLine();
                if (string.IsNullOrWhiteSpace(srLine) || srLine.TrimStart().StartsWith("//")) continue;
                srLine = Regex.Replace(srLine, @"\s", "");
                string[] split = srLine.Split('=');

                if (split.Length < 2) continue;
                string key = split[0];
                string[] values = split[1].Split(',');

                var entries = new List<(string name, float weight)>();
                foreach (var v in values)
                {
                    if (string.IsNullOrEmpty(v)) continue;
                    string[] tmp = v.Split(':');
                    float w = tmp.Length > 1 ? float.Parse(tmp[1], System.Globalization.CultureInfo.InvariantCulture) : 1f;
                    entries.Add((tmp[0], w));
                }

                dict[key] = entries.ToArray();
            }
            sr.Close();
            return dict;
        }
    }

    public static class DirectFapProfileFilReader
    {
        public static Dictionary<string, FAPSource[]> FileReader(string file_path)
        {
            var dict = new Dictionary<string, FAPSource[]>();
            StreamReader sr = new StreamReader(file_path);
            while (!sr.EndOfStream)
            {
                string srLine = sr.ReadLine();
                if (string.IsNullOrWhiteSpace(srLine) || srLine.TrimStart().StartsWith("//")) continue;
                srLine = Regex.Replace(srLine, @"\s", "");
                string[] split = srLine.Split('=');
                if (split.Length < 2) continue;
                string blendShapeName = split[0];
                string[] fapEntries = split[1].Split(',');
                var sources = new List<FAPSource>();
                foreach (var entry in fapEntries)
                {
                    if (string.IsNullOrEmpty(entry)) continue;
                    string[] tmp = entry.Split(':');
                    FAPType fapType = (FAPType)Enum.Parse(typeof(FAPType), tmp[0]);

                    FAPSign sign = FAPSign.Positive;
                    float weight = 1f;

                    for (int i = 1; i < tmp.Length; i++)
                    {
                        if (tmp[i] == "n")
                            sign = FAPSign.Negative;
                        else
                            weight = float.Parse(tmp[i], System.Globalization.CultureInfo.InvariantCulture);
                    }

                    sources.Add(new FAPSource(fapType, sign, weight));
                }
                dict[blendShapeName] = sources.ToArray();
            }
            sr.Close();
            return dict;
        }
    }
}