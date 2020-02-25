using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine.Experimental.PlayerLoop;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

[CustomEditor(typeof(TM_SpriteAnimator))]

public class TM_CustomInspector_SpriteAnimator : Editor
{
	/*
	 *	[System.Serializable]
		public struct TM_AnimFrame
		{
			public Sprite sprite;
			public float frametime;
			public string eventToFire;
			public AudioClip clip;
		}
		
		[System.Serializable]
		public class TM_SpriteAnim
		{
			public string Name;
			public EShepAnimBehavior actionOnEnd;
			public List<TM_AnimFrame> Frames;
		
		}
	 *
	 *
	 *
	 *
	 * 
	 */

	/// <summary>
	/// Which anim we're looking at.
	/// </summary>
	private int selectedAnimIndex;

	/// <summary>
	/// The anim we were looking at last time.
	/// </summary>
	private int oldAnimIndex = -1;

	/// <summary>
	/// Index of the frame we're showing off in the live preview
	/// </summary>
	private int liveAnimFrameIndex;

	/// <summary>
	/// The anim frame index from the previous editor tick.
	/// </summary>
	private int oldFrameIndex = -1;
	
	/// <summary>
	/// The timer for showing the anim in the live preview
	/// </summary>
	private float liveAnimFrameTimer;

	/// <summary>
	/// Used to track time in the inspector.
	/// </summary>
	private float lastFrameRealtime;


	/// <summary>
	/// To stop the live renderer from boppin' around
	/// </summary>
	private float mostRecentHighestYValueForLiveAnim;

	/// <summary>
	/// We can change the frame time all at once.
	/// </summary>
	private string changeAllFrameTimeString;
	private float changeAllFrameTimeValue;

	/// <summary>
	/// We need to copy our sprite into this texture to get it to draw pixel perfect.
	/// </summary>
	private Texture2D texActiveFrameImage;

	/// <summary>
	/// We want our main image to be beeegah
	/// </summary>
	private int mainFrameImageSizeMultiplier = 4;

	private bool animIsPaused = false;

	private bool ctrlIsHeldDown;

	/// <summary>
	/// There's an add socket button and a text field to give it a name.
	/// </summary>
	private string newSocketName;

	/// <summary>
	/// Whatever socket we've been working with is the active one.
	/// </summary>
	private int activeSocketIndex = -1;

	private static Color[] socketColors;

	private List<List<(String, Vector2Int)>> socketsForAnimation;

	private void OnEnable()
	{
		socketColors = new Color[6];
		socketColors[0] = Color.cyan;
		socketColors[1] = Color.red;
		socketColors[2] = Color.green;
		socketColors[3] = Color.yellow;
		socketColors[4] = Color.blue;
		socketColors[5] = Color.magenta;
	}

	public override void OnInspectorGUI()
	{
		var myAnimator = (TM_SpriteAnimator)target;

		if (myAnimator == null) return;

		if (lastFrameRealtime <= 0f)
		{
			lastFrameRealtime = (float) EditorApplication.timeSinceStartup;
		}

		Event currentInputEvent = Event.current;
		Vector2Int socketMotionFromKeys = Vector2Int.zero;

		bool doSerializeSocketsThisFrame = false;

		// Keyboard commands for this window require CTRL to be held.
		// but we should also check for ctrl up and down
		switch (currentInputEvent.type)
		{
			case EventType.KeyDown:
				switch (currentInputEvent.keyCode)
				{
					case KeyCode.LeftControl:
					case KeyCode.RightControl:
						ctrlIsHeldDown = true;
						break;
					case KeyCode.A:
						if( ctrlIsHeldDown)
							liveAnimFrameIndex--;
						break;
					case KeyCode.D:
						if( ctrlIsHeldDown)
							liveAnimFrameIndex++;
						break;
					case KeyCode.LeftArrow:
						if (ctrlIsHeldDown)
							socketMotionFromKeys.x--;
						break;
					case KeyCode.RightArrow:
						if (ctrlIsHeldDown)
							socketMotionFromKeys.x++;
						break;
					case KeyCode.UpArrow:
						if (ctrlIsHeldDown)
							socketMotionFromKeys.y++;
						break;
					case KeyCode.DownArrow:
						if (ctrlIsHeldDown)
							socketMotionFromKeys.y--;
						break;
				}

				break;
			case EventType.KeyUp:
				switch (currentInputEvent.keyCode)
				{
					case KeyCode.LeftControl:
					case KeyCode.RightControl:
						ctrlIsHeldDown = false;
						break;
				}
				break;
		}

		//allow us to create a new anim
		EditorGUILayout.LabelField("Drag Texture Here To Create Anim",EditorStyles.boldLabel);
		var draggedSprite = EditorGUILayout.ObjectField("", null, typeof(Object),
			false) as Texture2D;

		EditorGUILayout.TextArea("",GUI.skin.horizontalSlider);

		if (draggedSprite != null)
		{
			var trynaLoad = "Assets/Resources/Sprites/" + draggedSprite.name + ".png";
			var objects = AssetDatabase.LoadAllAssetsAtPath(trynaLoad);
			var sprites = objects.Where(q => q is Sprite).Cast<Sprite>();
			
			var newAnim = new TM_SpriteAnim();
			foreach (var s in sprites)
			{
				var newFrame = new TM_AnimFrame();
				newFrame.sprite = s;
				newFrame.frametime = 0.1f;
				newAnim.Frames.Add(newFrame);
			}

			newAnim.Name = draggedSprite.name;

			//now take this new thing we created, and hack it in.
			Undo.RecordObject(myAnimator, "Added Animation");
			if (myAnimator.animationList == null)
			{
				myAnimator.animationList = new List<TM_SpriteAnim>();
			}
			
			myAnimator.animationList.Add(newAnim);
		}
		
		
		serializedObject.Update();
		
		//Store the list we want to update 
		var animationList = serializedObject.FindProperty("animationList");
		
		
		if (myAnimator.animationList == null || myAnimator.animationList.Count == 0)
		{
			DrawDefaultInspector();
			return;
		}
		
		int numAnimsInList = animationList.arraySize;
		
		//Here are the contents of that list.
		var spriteAnims = new SerializedProperty[numAnimsInList];
		var animNamesForDropdown = new string[numAnimsInList];
		for (int t = 0; t < numAnimsInList; t++)
		{
			spriteAnims[t] = animationList.GetArrayElementAtIndex(t);
			animNamesForDropdown[t] = spriteAnims[t].FindPropertyRelative("Name").stringValue;
		}
		
		//we need a dropdown for anims
		selectedAnimIndex = EditorGUILayout.Popup(selectedAnimIndex, animNamesForDropdown);

		//pick the currently selected item from the list
		var selectedSpriteAnim = spriteAnims[selectedAnimIndex];

		//grab the frame list, name, and action on end.
		var selectedSpriteAnim_Name = selectedSpriteAnim.FindPropertyRelative("Name");
		var selectedSpriteAnim_Frames = selectedSpriteAnim.FindPropertyRelative("Frames");
		var selectedSpriteAnim_actionOnEnd = selectedSpriteAnim.FindPropertyRelative("actionOnEnd");

		if (selectedSpriteAnim_Frames == null)
		{
			DrawDefaultInspector();
			return;
		}
		
		//build up the frame list
		int numFrames = selectedSpriteAnim_Frames.arraySize;
		var frames = new SerializedProperty[numFrames];
		for (int t = 0; t < numFrames; t++)
		{
			frames[t] = selectedSpriteAnim_Frames.GetArrayElementAtIndex(t);
		}

		//if we changed animations, build a new list of sockets
		if (oldAnimIndex != selectedAnimIndex || socketsForAnimation == null)
		{
			SetArrayOfSocketBullshitBecauseSerializedObjects(frames);
		}

		oldAnimIndex = selectedAnimIndex;
		
		//Animation Name ================
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Edit Name:",EditorStyles.boldLabel, GUILayout.Width(96));
		EditorGUILayout.PropertyField(selectedSpriteAnim_Name, GUIContent.none, EditorStyles.boldFont, 
			GUILayout.Height(18), GUILayout.Width(320));
		
		
		//Remove Anim button ============
		//blank label to r-justify the button
		EditorGUILayout.LabelField("", GUILayout.MinWidth(0f));
		if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(32)))
		{
			if (EditorUtility.DisplayDialog("Whoa", "Really remove animation '" + selectedSpriteAnim_Name.stringValue + 
			                                        "'? Undo probably doesn't work.", "Yes", "No"))
			{
				Undo.RecordObject(target, "Removing an anim");
				animationList.DeleteArrayElementAtIndex(selectedAnimIndex);
				serializedObject.ApplyModifiedProperties();
				DrawDefaultInspector();
				return;
			}
		}

		EditorGUILayout.EndHorizontal();
		GUILayout.Space(8.0f);
		
		//Running anim ==================
		if (numFrames > 0)
		{

			if (liveAnimFrameIndex >= frames.Length)
			{
				liveAnimFrameIndex = 0;
				liveAnimFrameTimer = 0f;
			}

			if (liveAnimFrameIndex < 0)
			{
				liveAnimFrameIndex = frames.Length - 1;
			}

			var runningAnimFrame = frames[liveAnimFrameIndex];
			var frame_spriteref = runningAnimFrame.FindPropertyRelative("sprite");
			var frame_sprite = frame_spriteref.objectReferenceValue as Sprite;
			var frame_frametime = runningAnimFrame.FindPropertyRelative("frametime");

			//we need to be at least this tall:
			mostRecentHighestYValueForLiveAnim = frame_sprite.rect.height * mainFrameImageSizeMultiplier;

			//copy this sprite into the texture if we need to...
			var spriteWide = (int) frame_sprite.rect.width * mainFrameImageSizeMultiplier;
			var spriteHeight = (int) frame_sprite.rect.height * mainFrameImageSizeMultiplier;

			//if the texture isn't the right size, make a new one.
			if (texActiveFrameImage == null ||
			    texActiveFrameImage.width * mainFrameImageSizeMultiplier != spriteWide ||
			    texActiveFrameImage.height * mainFrameImageSizeMultiplier != spriteHeight)
			{
				DestroyImmediate(texActiveFrameImage);
				texActiveFrameImage = new Texture2D(spriteWide, spriteHeight, TextureFormat.RGBA32, false);
				texActiveFrameImage.filterMode = FilterMode.Point;
				texActiveFrameImage.wrapMode = TextureWrapMode.Clamp;
				texActiveFrameImage.alphaIsTransparency = true;
				texActiveFrameImage.Apply();

				oldFrameIndex = -1;
			}

			if (oldFrameIndex != liveAnimFrameIndex)
			{
				Color32[] originalPixels = frame_sprite.texture.GetPixels32();
				Color32[] destPixels = texActiveFrameImage.GetPixels32();

				//Debug.Log("I'mma copy a block this big: " +
				//          (frame_sprite.rect.width * frame_sprite.rect.height * mainFrameImageSizeMultiplier *
				//           mainFrameImageSizeMultiplier));
				
				int destIdx = 0;
				for (int originalIdxY = (int) frame_sprite.rect.y;
					originalIdxY < (int) frame_sprite.rect.height + frame_sprite.rect.y;
					originalIdxY++)
				{
					for (int originalIdxX = (int) frame_sprite.rect.x;
						originalIdxX < (int) frame_sprite.rect.width + frame_sprite.rect.x;
						originalIdxX++)
					{
						//take the X,Y value and flatten it into an index in the source array.
						int pixelIdxFromOriginal = originalIdxX + originalIdxY * frame_sprite.texture.width;
						for (int copyX = 0; copyX < mainFrameImageSizeMultiplier; copyX++)
						{
							for (int copyY = 0; copyY < mainFrameImageSizeMultiplier; copyY++)
							{
								int drawHere = destIdx + copyY * spriteWide;
								if (drawHere < destPixels.Length)
								{
									var originalPixel = originalPixels[pixelIdxFromOriginal];
									destPixels[drawHere] = originalPixel; //originalPixels[pixelIdxFromOriginal];
								}
								else
								{
									Debug.LogWarning("Out of bounds at source " + originalIdxX + ", " + originalIdxY);
								}
							}

							destIdx++;
						}
					}

					destIdx += spriteWide * (mainFrameImageSizeMultiplier - 1);
				}

				//Debug.Log("I copied this many pixels: " + destIdx + " and I needed to copy " + destPixels.Length);
				
				int socketIdx = 0;
				foreach (var socketInfo in socketsForAnimation[liveAnimFrameIndex])
				{
					var colorIdx =  socketIdx % socketColors.Length;
					var drawColor = socketColors[colorIdx];
					if (activeSocketIndex == socketIdx)
					{
						drawColor = Color.Lerp(socketColors[colorIdx], Color.white,
							Mathf.PingPong((float)EditorApplication.timeSinceStartup, 0.25f) / 0.25f);
					}
					var loc = socketInfo.Item2;
					DrawPixelInMainFrameTexture(loc.x, loc.y, drawColor, destPixels);
					socketIdx++;
				}

				texActiveFrameImage.SetPixels32(destPixels);
				texActiveFrameImage.Apply();
				
				Repaint();
			}


			//advance our timer and maybe tick our frame.
			//OnGUI is called multiple times per editor frame, because reasons, so incrementing a timer
			//via deltaTime is not going to work. 
			var timeSinceStart = (float) EditorApplication.timeSinceStartup;
			if (animIsPaused)
			{
				lastFrameRealtime = timeSinceStart;
			}
			else
			{
				liveAnimFrameTimer += timeSinceStart - lastFrameRealtime;
				lastFrameRealtime = timeSinceStart;

				oldFrameIndex = liveAnimFrameIndex;

				//we may have jumped multiple frames
				while (liveAnimFrameTimer > frame_frametime.floatValue)
				{
					//tick down
					liveAnimFrameTimer -= frame_frametime.floatValue;

					//next frame
					liveAnimFrameIndex++;
					if (liveAnimFrameIndex >= frames.Count())
					{
						liveAnimFrameIndex = 0;
					}

					//read this frame and get new values
					runningAnimFrame = frames[liveAnimFrameIndex];
					frame_frametime = runningAnimFrame.FindPropertyRelative("frametime");

					//if we set our timer to < 0 somehow, get out.
					if (frame_frametime.floatValue <= 0f)
					{
						liveAnimFrameTimer = 0.1f;
						break;
					}
				}
			}


			//draw homie
			//EditorGUILayout.ObjectField(frame_sprite, typeof(Sprite), false,
			//	GUILayout.Width(frame_sprite.rect.width * 6), 
			//	GUILayout.Height(frame_sprite.rect.height * 6),
			//	GUILayout.MinHeight(mostRecentHighestYValueForLiveAnim));

			//Image on the left, list of sockets on the right
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("►", GUILayout.Width(28)))
			{
				animIsPaused = false;
			}

			if (GUILayout.Button("❚❚", GUILayout.Width(28)))
			{
				animIsPaused = true;
			}

			if (GUILayout.Button("<-", GUILayout.Width(28)))
			{
				liveAnimFrameIndex--;
				if (liveAnimFrameIndex < 0)
				{
					liveAnimFrameIndex = numFrames - 1;
				}
			}

			if (GUILayout.Button("->", GUILayout.Width(28)))
			{
				liveAnimFrameIndex++;
				if (liveAnimFrameIndex >= numFrames)
				{
					liveAnimFrameIndex = 0;
				}

			}
			
			GUILayout.Label("CTRL and: A/D move frames, Arrows move socket");

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			EditorGUILayout.ObjectField(texActiveFrameImage, typeof(Sprite), false,
				GUILayout.Width(spriteWide),
				GUILayout.Height(spriteHeight),
				GUILayout.MinHeight(mostRecentHighestYValueForLiveAnim));

			//frame number
			GUILayout.BeginVertical();
			EditorGUILayout.LabelField("Frame " + liveAnimFrameIndex, EditorStyles.boldLabel, GUILayout.Width(128));
			EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

			//list of sockets in this frame
			GUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Socket Name", EditorStyles.boldLabel, GUILayout.Width(148));
			EditorGUILayout.LabelField("Location", EditorStyles.boldLabel, GUILayout.Width(128));
			GUILayout.EndHorizontal();
			
			//track the socket we're messing with.
			activeSocketIndex = Math.Max(0, Math.Min(socketsForAnimation.Count - 1, activeSocketIndex));

			//OnGUI is called multiple times per editor frame, because reasons. If one call
			//is on frame x, and the next on frame x+1, and there are a different number 
			//of sockets, the editor doesn't like that.
			
			for( int checkSocketIndex = 0; checkSocketIndex < socketsForAnimation[liveAnimFrameIndex].Count; checkSocketIndex++)
			{
				var colorIdx = checkSocketIndex % socketColors.Length;
				
				var s = socketsForAnimation[liveAnimFrameIndex][checkSocketIndex];
				
				Vector2Int old = s.Item2;
				//socket name, socket position
				GUILayout.BeginHorizontal();
				
				//are we messing with this socket?
				Color oldColor = GUI.backgroundColor;
				GUI.backgroundColor = socketColors[colorIdx];
				bool thisSocketIsActive = EditorGUILayout.ToggleLeft("", checkSocketIndex == activeSocketIndex,
											EditorStyles.label, GUILayout.Width(20));
				GUI.backgroundColor = oldColor;
											
				if (thisSocketIsActive)
				{
					activeSocketIndex = checkSocketIndex;
				}
				
				//name of socket, bold if selected
				GUILayout.Label(s.Item1, thisSocketIsActive ? EditorStyles.whiteLabel : EditorStyles.label,
				 GUILayout.Width(128));
				 
				//location
				var newX = GUILayout.TextField(old.x.ToString(), GUILayout.Width(40));
				GUILayout.Space(8);
				var newY = GUILayout.TextField(old.y.ToString(), GUILayout.Width(40));
				
				//movement buttons
				Vector2Int deltaThisFrame = Vector2Int.zero;
				if (GUILayout.Button("←", GUILayout.Width(28)))
				{
					deltaThisFrame.x--;
				}
				if (GUILayout.Button("→", GUILayout.Width(28)))
				{
					deltaThisFrame.x++;
				}
				if (GUILayout.Button("↑", GUILayout.Width(28)))
				{
					deltaThisFrame.y++;
				}
				if (GUILayout.Button("↓", GUILayout.Width(28)))
				{
					deltaThisFrame.y--;
				}

				//track any changes from arrow keys if we used those
				//and this socket is active
				if (thisSocketIsActive)
				{
					deltaThisFrame += socketMotionFromKeys;
				}
				
				//copy pastas
				GUILayout.Space(8);
				bool bApplyToNext = GUILayout.Button("Copy to Next", GUILayout.Width(100));

				bool bAssignToAll = GUILayout.Button("Set For All", GUILayout.Width(100));

				GUILayout.EndHorizontal();

				if (newX != old.x.ToString() ||
				    newY != old.y.ToString() ||
				    deltaThisFrame != Vector2Int.zero ||
				    bApplyToNext ||
				    bAssignToAll)
				{
					//having clicked buttons above, this is now the active socket.
					activeSocketIndex = checkSocketIndex;
					
					Int32.TryParse(newX, out int xLoc);
						
					Int32.TryParse(newY, out int yLoc);

					xLoc += deltaThisFrame.x;
					yLoc += deltaThisFrame.y;

					if (xLoc < 0)
						xLoc = 0;

					if (yLoc < 0)
						yLoc = 0;
						
					UpdateSocketPositionForCurrentAnim(s.Item1, liveAnimFrameIndex,xLoc, yLoc, bAssignToAll);
					if (bApplyToNext)
					{
						var nextFrame = (liveAnimFrameIndex + 1) % numFrames;
						UpdateSocketPositionForCurrentAnim(s.Item1, nextFrame,xLoc, yLoc, bAssignToAll);
					}

					doSerializeSocketsThisFrame = true;
				}
			}
			
			//a little space before the add button
			GUILayout.Space(8);

			GUILayout.BeginHorizontal();
			bool addSocketThisFrame = GUILayout.Button("Add Socket Named: ", GUILayout.Width(160));
			newSocketName = GUILayout.TextField(newSocketName, GUILayout.Width(300));
			
			if (addSocketThisFrame && !string.IsNullOrEmpty(newSocketName))
			{
				AddNewSocketToCurrentAnim(newSocketName);
				newSocketName = "";
				animIsPaused = true;

				doSerializeSocketsThisFrame = true;
			}

			GUILayout.EndHorizontal();

			//end list of sockets
			GUILayout.EndVertical();				
			
			//end row with image and socket info.
			GUILayout.EndHorizontal();
		}
		
		//List of Frames ================
		EditorGUILayout.BeginHorizontal();
		
		//we need a button to change all timing
		bool changeTimes = GUILayout.Button("Set All Frame Times To:", EditorStyles.miniButton, GUILayout.Width(128));
		changeAllFrameTimeString = GUILayout.TextField(changeAllFrameTimeString, GUILayout.Width(64));
			
		float.TryParse(changeAllFrameTimeString, out changeAllFrameTimeValue);
		if (changeAllFrameTimeValue < 0)
		{
			changeAllFrameTimeValue = 0;
		}

		if (changeTimes && changeAllFrameTimeValue < 0.016)
		{
			changeAllFrameTimeValue = 0.016f;
			changeAllFrameTimeString = "0.016";
		}
		
		EditorGUILayout.EndHorizontal();
		GUILayout.Space(8.0f);

		EditorGUILayout.BeginHorizontal();
		
		//make sure our live window is as big as the biggest tallest frame.
		var checkForTallest = 0f;
		foreach (var frame in frames)
		{
			EditorGUILayout.BeginVertical();

			var frame_spriteref = frame.FindPropertyRelative("sprite");
			var frame_sprite = frame.FindPropertyRelative("sprite").objectReferenceValue as Sprite;

			if (frame_sprite.rect.height * 2 > checkForTallest)
			{
				checkForTallest = frame_sprite.rect.height * 2;
			}
			
			//draw the sprite
			frame_sprite = EditorGUILayout.ObjectField(frame_sprite, typeof(Sprite),false, 
				GUILayout.Height(64), GUILayout.Width(64)
				) as Sprite;

			//assign it too
			frame_spriteref.objectReferenceValue = frame_sprite;

			
			//how many MS to keep it going
			var frame_frametime = frame.FindPropertyRelative("frametime");
			
			//we may have changed it with a button press.
			if (changeTimes)
			{
				frame_frametime.floatValue = changeAllFrameTimeValue;
			}
			EditorGUILayout.PropertyField(frame_frametime, GUIContent.none, GUILayout.Height(20), GUILayout.Width(64));

			var frame_event = frame.FindPropertyRelative("eventToFire");
			EditorGUILayout.PropertyField(frame_event, GUIContent.none, EditorStyles.miniFont, GUILayout.Height(20), GUILayout.Width(64));

			EditorGUILayout.EndVertical();
		}
		
		//store this
		mostRecentHighestYValueForLiveAnim = checkForTallest;
		
		EditorGUILayout.EndHorizontal();
		
		//Action on end ================
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Action On End",EditorStyles.boldLabel, GUILayout.Width(96));
		var enumNames = new string[(int) EShepAnimBehavior.MAX];

		for (EShepAnimBehavior eb = 0; eb < EShepAnimBehavior.MAX; eb++)
		{
			enumNames[(int) eb] = eb.ToString();
		}
		
		selectedSpriteAnim_actionOnEnd.enumValueIndex = EditorGUILayout.Popup(selectedSpriteAnim_actionOnEnd.enumValueIndex,
			enumNames,GUILayout.Width(200));
		
		EditorGUILayout.EndHorizontal();
		
		var anim_next = selectedSpriteAnim.FindPropertyRelative("nextAnim");
		
		//Specific anim behavior drawing ===============================================================================
		//
		// play_then_random_branch
		//
		if (selectedSpriteAnim_actionOnEnd.enumValueIndex == (int)EShepAnimBehavior.play_then_random_branch)
		{
			var branchChances = selectedSpriteAnim.FindPropertyRelative("listBranchAnims_Chance");
			var branchNames = selectedSpriteAnim.FindPropertyRelative("listBranchAnims_Name");

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Branch Anim Name",EditorStyles.boldLabel, GUILayout.Width(256));
			EditorGUILayout.LabelField("% Chance",EditorStyles.boldLabel, GUILayout.Width(96));
			EditorGUILayout.EndHorizontal();

			var numBranches = branchChances.arraySize;
			int removeThisOne = -1;
			
			//show every branch
			for (int t = 0; t < numBranches; t++)
			{
				var branch = branchNames.GetArrayElementAtIndex(t);
				var chance = branchChances.GetArrayElementAtIndex(t);

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PropertyField(branch, GUIContent.none, GUILayout.Height(20), GUILayout.Width(256));
				EditorGUILayout.PropertyField(chance, GUIContent.none, GUILayout.Height(20), GUILayout.Width(96));
				if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(32)))
				{
					removeThisOne = t;
				}
				EditorGUILayout.EndHorizontal();
			}

			//was X pressed?
			if (removeThisOne > -1)
			{
				Undo.RecordObject(myAnimator, "Killed a branch");
				branchChances.DeleteArrayElementAtIndex(removeThisOne);
				branchNames.DeleteArrayElementAtIndex(removeThisOne);
			}
			
			//add button, why not
			if (GUILayout.Button("Add Branch", EditorStyles.miniButton, GUILayout.Width(96)))
			{
				Undo.RecordObject(myAnimator, "Added Branch");
				branchChances.arraySize++;
				branchNames.arraySize++;
			}

		}
		//
		// loop_count_and_then_next
		//
		else if (selectedSpriteAnim_actionOnEnd.enumValueIndex == (int) EShepAnimBehavior.loop_count_and_then_next)
		{
			var anim_loopcount = selectedSpriteAnim.FindPropertyRelative("loopThisManyTimes");

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Next Anim Name",EditorStyles.boldLabel, GUILayout.Width(256));
			EditorGUILayout.LabelField("Loop Count",EditorStyles.boldLabel, GUILayout.Width(96));
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(anim_next, GUIContent.none, GUILayout.Height(20), GUILayout.Width(256));
			EditorGUILayout.PropertyField(anim_loopcount, GUIContent.none, GUILayout.Height(20), GUILayout.Width(96));
			EditorGUILayout.EndHorizontal();
			
		}
		//
		// play_once_then_next
		//
		else if (selectedSpriteAnim_actionOnEnd.enumValueIndex == (int) EShepAnimBehavior.play_once_then_next)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Next Anim Name",EditorStyles.boldLabel, GUILayout.Width(256));
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(anim_next, GUIContent.none, GUILayout.Height(20), GUILayout.Width(256));
			EditorGUILayout.EndHorizontal();
		}

		//write out the sockets if we changed any.
		if (doSerializeSocketsThisFrame)
		{
			SerializeSocketInformation(frames);
		}

		//all done
		serializedObject.ApplyModifiedProperties();
		EditorGUILayout.TextArea("",GUI.skin.horizontalSlider);
		
		GUILayout.Space(32.0f);

		DrawDefaultInspector();
	}

	/// <summary>
	/// Ensure that we update this window every frame so that our live anim can run.
	/// </summary>
	/// <returns></returns>
	public override bool RequiresConstantRepaint()
	{
		return false;
	}

	void DrawPixelInMainFrameTexture(int x, int y, Color32 drawColor, Color32[] destPixels)
	{
		int spriteWide = texActiveFrameImage.width;
		int destDrawIdx = x * mainFrameImageSizeMultiplier + y * mainFrameImageSizeMultiplier * spriteWide;
		
		for (int copyX = 0; copyX < mainFrameImageSizeMultiplier; copyX++)
		{
			for (int copyY = 0; copyY < mainFrameImageSizeMultiplier; copyY++)
			{
				int drawHere = destDrawIdx + copyY * spriteWide;
				if (drawHere < destPixels.Length)
				{
					destPixels[drawHere] = drawColor;
				}
				else
				{
					Debug.LogWarning("Out of bounds at source " + x + ", " + y);
				}
			}
			destDrawIdx++;
		}
	}

	/// <summary>
	/// Best
	/// </summary>
	/// <param name="animation"></param>
	void SetArrayOfSocketBullshitBecauseSerializedObjects(SerializedProperty[] frames)
	{
		//clear out the old list, here's the new list.
		socketsForAnimation = new List<List<(string, Vector2Int)>>();

		//in every frame
		int frameIdx = 0;
		foreach (var frameObj in frames)
		{
			socketsForAnimation.Add( new List<(string, Vector2Int)>());
			var listOfSockets = frameObj.FindPropertyRelative("sockets");

			for (int idx = 0; idx < listOfSockets.arraySize; idx++)
			{
				var socket = listOfSockets.GetArrayElementAtIndex(idx);
				var socketName = socket.FindPropertyRelative("name");
				var loc = socket.FindPropertyRelative("location");

				Debug.Log("There is a socket called " + socketName.stringValue + " at " + loc.vector2IntValue);
				
				socketsForAnimation[frameIdx].Add( (socketName.stringValue, loc.vector2IntValue));
			}
			frameIdx++;
		}
		
		//get a list of sockets
		
		//in every socket list
		
		//store x,y values
	}

	/// <summary>
	/// We clicked a button, and a brand new socket is added.
	/// </summary>
	/// <param name="socketName"></param>
	void AddNewSocketToCurrentAnim(string socketName)
	{
		for (int idxFrame = 0; idxFrame < socketsForAnimation.Count; idxFrame++)
		{
			socketsForAnimation[idxFrame].Add( (socketName, new Vector2Int(0,0)));
		}
	}

	/// <summary>
	/// Update the position of a socket for a given frame.
	/// </summary>
	/// <param name="socketName"></param>
	/// <param name="idxFrame"></param>
	/// <param name="newX"></param>
	/// <param name="newY"></param>
	void UpdateSocketPositionForCurrentAnim(string socketName, int idxFrame, int newX, int newY,
	 bool assignToAllFrames)
	{
		for (int t = 0; t < socketsForAnimation.Count; t++)
		{
			var listForFrame = socketsForAnimation[t];
			if (listForFrame == null || listForFrame.Count == 0)
			{
				continue;
			}
			
			//try to find the socket we're looking for
			for(int listIdx = 0; listIdx < listForFrame.Count; listIdx++)
			{
				var maybeSocket = listForFrame[listIdx];
				if (maybeSocket.Item1 != socketName)
				{
					continue;
				}
				
				//this is our boy
				if (idxFrame == t ||
				    assignToAllFrames)
				{
					listForFrame.RemoveAt(listIdx);
					listForFrame.Insert(listIdx, (socketName, new Vector2Int(newX, newY)));
				}
			}
		}
	}


	void SerializeSocketInformation(SerializedProperty[] frames)
	{
		// here is every socket: socketsForAnimation	
		
		//in every frame
		int frameIdx = 0;
		foreach (var frameObj in frames)
		{
			//reset the socket list
			var listOfSockets = frameObj.FindPropertyRelative("sockets");
			var numOfSockets = socketsForAnimation[frameIdx].Count;
			listOfSockets.ClearArray();

			//set the new information for each socket.
			for (int idx = 0; idx < numOfSockets; idx++)
			{
				listOfSockets.InsertArrayElementAtIndex(idx);
				var modifiedSocket = socketsForAnimation[frameIdx][idx];
				var destSocket = listOfSockets.GetArrayElementAtIndex(idx);
				destSocket.FindPropertyRelative("name").stringValue = modifiedSocket.Item1;
				destSocket.FindPropertyRelative("location").vector2IntValue = modifiedSocket.Item2;
			}
			
			frameIdx++;
		}
		
	}
}
