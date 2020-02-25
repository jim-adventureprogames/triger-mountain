using UnityEngine;
using System.Collections;
using System.Security.Policy;
using UnityEditor;

public class TMCreateAnimationCollection
{
	[MenuItem("Assets/Create/TMAnimationCollection")]
	public static void CreateMyAsset()
	{
		TMAnimationCollection asset = ScriptableObject.CreateInstance<TMAnimationCollection>();
		string strName = "New TMAnimationCollection";
		AssetDatabase.CreateAsset(asset, "Assets/Resources/Animation/" + strName + ".asset");
		AssetDatabase.SaveAssets();
		EditorUtility.FocusProjectWindow();
	
		Selection.activeObject = asset;
	}
}
