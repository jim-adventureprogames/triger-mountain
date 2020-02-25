using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TMAnimationEditor : EditorWindow
{
	
	private Sprite testMe;

	private TMAnimationCollection selectedAnimCollection;

	private SerializedObject serializedAnims;

	/// <summary>
	/// The animations in the selected object.
	/// </summary>
	private SerializedProperty animList;

	private SerializedProperty currentAnimation;
	
	[MenuItem("Window/TM Animation Editor")]
	public static void ShowWindow()
	{
		EditorWindow.GetWindow<TMAnimationEditor>();
	}
	
	// Use this for initialization
	void Start () 
	{
		
	}
	
	// Update is called once per frame
	void Update () 
	{
		
	}

	private void OnGUI()
	{
		var checkAnimCollection = EditorGUILayout.ObjectField("Animation Collection", selectedAnimCollection, 
			typeof(TMAnimationCollection), true) as TMAnimationCollection;

		if (checkAnimCollection != selectedAnimCollection)
		{
			//change to whatever we put in the editor.
			selectedAnimCollection = checkAnimCollection;
			if (selectedAnimCollection == null)
			{
				return;
			}
			
			//if it is a thing, here we go.
			serializedAnims = new SerializedObject(selectedAnimCollection);
		}

		//Don't draw anything if there's nothing here.
		if (selectedAnimCollection == null)
		{
			return;
		}
		
		//TMAnimationCollection has a list of TM_SpriteAnims called animations
		//
		
		

		animList = serializedAnims.FindProperty("animations");
		currentAnimation = animList.GetArrayElementAtIndex(0);
		var currentAnimName = currentAnimation.FindPropertyRelative("Name");
		var currentAnimListOfFrames = currentAnimation.FindPropertyRelative("Frames");
		
		serializedAnims.Update();
		
		EditorGUILayout.PropertyField(animList, new GUIContent("Animation Collection"));
		//EditorGUILayout.PropertyField(testName, new GUIContent("Anim 0"));

		serializedAnims.ApplyModifiedProperties();
	}


	/// <summary>
	/// Draw one sprite for each cel in the animation
	/// </summary>
	/// <param name="topLeftCorner">The position on the window to start drawing</param>
	void DrawCurrentAnimationCels(Vector2 topLeftCorner)
	{
		
	}
	
}
