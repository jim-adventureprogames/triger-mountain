using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TMAnimationCollection : ScriptableObject
{
	[SerializeField]
	[Tooltip("A list of animations.")]
	public TM_SpriteAnim[] animations;
}
