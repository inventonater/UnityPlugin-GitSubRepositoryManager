using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Callbacks;
using UnityEditor.Graphs;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace GitRepositoryManager
{
	public class GUIPushPanel
	{
		private string _branch;
		private string _commitMessage;
		private Action<string, string> _onPush;
		private GUIStyle _labelStyle;
		private GUIStyle _buttonStyle;

		public GUIPushPanel(string branch, Action<string, string> onPush)
		{
			_branch = branch;
			_onPush = onPush;
			_commitMessage = Repository.DEFAULT_MESSAGE;
			_labelStyle = new GUIStyle(EditorStyles.label);
			_labelStyle.richText = true;
			_buttonStyle=  new GUIStyle(EditorStyles.miniButton);
			_buttonStyle.richText = true;
		}
		
		public void OnDrawGUI()
		{
			EditorGUI.indentLevel+=2;
			_branch = EditorGUILayout.TextField("Branch", _branch);
			_commitMessage = EditorGUILayout.TextArea(_commitMessage);

			Space();
			Rect buttonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false));
			
			if (GUI.Button(buttonRect,new GUIContent("<b>Commit + Push</b>", "Commit all changes and push to the specified branch."), _buttonStyle))
			{
				if (_commitMessage == Repository.DEFAULT_MESSAGE || _commitMessage.Trim() == string.Empty)
				{
					if (EditorUtility.DisplayDialog("Empty commit message",
						"Are you sure you want to commit and push without a meaningful message?",
						"Commit and Push Anyway", "Cancel"))
					{
						Push();
					}
				}
				else
				{
					Push();
				}
				
				void Push()
				{
					_onPush?.Invoke(_branch, _commitMessage);
				}
			}
			EditorGUI.indentLevel-=2;
			Space();
			
			void Space()
			{
				EditorGUI.LabelField(EditorGUILayout.GetControlRect(false, 7), "");
			}
			
		}
	}
}