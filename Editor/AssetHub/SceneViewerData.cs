using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SceneViewer
{
    public enum ColorCode
    {
        Slate = 0,
        Indigo = 1,
        Teal = 2,
        Emerald = 3,
        Amber = 4,
        Rose = 5
    }

    [System.Serializable]
    public class ColorMapping
    {
        public string guid;
        public ColorCode color = ColorCode.Slate;
    }

    public class SceneViewerData : ScriptableObject
    {
        public string customFilter = "l:scene";
        public List<ColorMapping> colorMappings = new List<ColorMapping>();

        public void SetItemColor(string guid, ColorCode color)
        {
            var mapping = colorMappings.Find(m => m.guid == guid);
            if (mapping == null)
            {
                mapping = new ColorMapping { guid = guid };
                colorMappings.Add(mapping);
            }
            mapping.color = color;
            SaveData();
        }

        public ColorCode GetItemColor(string guid)
        {
            var mapping = colorMappings.Find(m => m.guid == guid);
            return mapping != null ? mapping.color : ColorCode.Slate;
        }

        public void SaveData()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
