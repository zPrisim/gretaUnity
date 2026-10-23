using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;


namespace BlendShapes
{
    public static class ConfigFileReaders
    {
        // Ligne valide : non vide, ne commence pas par #, contient =
        private static bool IsValidLine(string line)
        {
            string t = line.Trim();
            return !string.IsNullOrWhiteSpace(t) && !t.StartsWith("#") && t.Contains("=");
        }

        // Lecture blendshapes : clé = val1:poids,val2:poids,...
        public static Dictionary<string, (string name, float weight)[]> FileReader(string path)
        {
            var dict = new Dictionary<string, (string name, float weight)[]>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (!IsValidLine(line)) continue;

                string stripped = Regex.Replace(line, @"\s", "");
                string[] split = stripped.Split('=');
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
            return dict;
        }

        // Lecture bone mapping : clé = valeur (1:1)
        public static Dictionary<string, string> BoneMappingReader(string path)
        {
            var mapping = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (!IsValidLine(line)) continue;

                string[] parts = line.Trim().Split('=');
                if (parts.Length == 2)
                    mapping[parts[0].Trim()] = parts[1].Trim();
            }
            return mapping;
        }
    }
}