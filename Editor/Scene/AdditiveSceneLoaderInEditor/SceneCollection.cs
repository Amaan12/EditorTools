using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Scenes/Scene Collection")]
public class SceneCollection : ScriptableObject
{
    public List<SceneAsset> scenes = new();
}