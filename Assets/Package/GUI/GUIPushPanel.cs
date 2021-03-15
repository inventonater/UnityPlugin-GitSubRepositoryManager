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
		private string _targetUrl;
		private string _targetBranch;
		private string _commitMessage;
		private string _localName;
		private Action<string, string> _onPush;
		private Action _onRemoveChanges;
		private GUIStyle _labelStyle;
		private GUIStyle _buttonStyle;
		private Repository _repo;

		public GUIPushPanel(string targetURL, string targetBranch, string localName, Repository repo, Action<string, string> onPush, Action onRemoveChanges)
		{
			_targetUrl = targetURL;
			_targetBranch = targetBranch;
			_localName = localName;
			_repo = repo;
			_onPush = onPush;
			_onRemoveChanges = onRemoveChanges;
			_commitMessage = Repository.DEFAULT_MESSAGE;
			_labelStyle = new GUIStyle(EditorStyles.label);
			_labelStyle.richText = true;
			_buttonStyle=  new GUIStyle(EditorStyles.miniButton);
			_buttonStyle.richText = true;
		}
		
		public void OnDrawGUI()
		{
			EditorGUI.indentLevel+=2;
			_targetBranch = EditorGUILayout.TextField("Remote Branch", _targetBranch);
			_commitMessage = EditorGUILayout.TextArea(_commitMessage);

			Space();
			
			GUILayout.BeginHorizontal();
			Rect pushRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false));
			pushRect.width /= 2;
			Rect clearRect = new Rect(pushRect);
			pushRect.x += pushRect.width;
			
			if (GUI.Button(pushRect,new GUIContent("<b>Commit + Push</b>", "Commit all changes and push to the specified branch."), _buttonStyle))
			{
				if (_commitMessage == Repository.DEFAULT_MESSAGE || _commitMessage.Trim() == string.Empty)
				{
					if (EditorUtility.DisplayDialog("Empty commit message",
						"Are you sure you want to commit and push without a meaningful message?",
						"Yes, Commit without a message", "Cancel"))
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
					if (EditorUtility.DisplayDialog("Commit and Push",
						$"Commit and push changes from {_localName}:{_repo.Branch} to {_targetUrl}:{_targetBranch}?\n\nChanges:\n{_repo.PrintableStatus}",
						"Commit and Push", "Cancel"))
					{
						_onPush?.Invoke(_targetBranch, _commitMessage);
					}
				
				}
			}
			if (GUI.Button(clearRect,new GUIContent("<b>Clear Changes</b>", "Remove all local changes."), _buttonStyle))
			{
				if (EditorUtility.DisplayDialog("Clear Local Changes",
					$"This will permanently remove all local changes, staged and un-staged. Are you sure?\n\nChanges:\n{_repo.PrintableStatus}",
					"Yes, Remove all local changes", "Cancel"))
				{
					_onRemoveChanges?.Invoke();
				}
			}
			GUILayout.EndHorizontal();
			EditorGUI.indentLevel-=2;
			Space();
			
			void Space()
			{
				EditorGUI.LabelField(EditorGUILayout.GetControlRect(false, 7), "");
			}
			
		}
	}
}