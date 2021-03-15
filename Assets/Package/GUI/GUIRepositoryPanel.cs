using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Graphs;
using UnityEditor.PackageManager;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace GitRepositoryManager
{
	public class GUIRepositoryPanel
	{
		public Dependency DependencyInfo
		{
			get;
			private set;
		}

		private bool _repoWasInProgress;
		public event Action<string, string, string> OnRemovalRequested = delegate { };
		public event Action<Dependency, string> OnEditRequested = delegate { };

		public event Action<GUIRepositoryPanel[]> OnRefreshRequested = delegate { };

		public event Action OnRepaintRequested = delegate { };
		
		private Texture2D _editIcon;
		private Texture2D _removeIcon;
		private Texture2D _pushIcon;
		private Texture2D _pullIcon;

		public AnimBool _expandableAreaAnimBool;

		private GUIPushPanel _pushPanel;

		public string RootFolder()
		{
			string fullPath = Application.dataPath;
			return fullPath.Replace("/Assets", "");
		}

		public string RelativeRepositoryPath()
		{
			return $"Assets/Repositories/{DependencyInfo.Name}";
		}

		public string RelativeRepositoryFolderPath()
		{
			return $"{RelativeRepositoryPath()}/{DependencyInfo.SubFolder}";
		}

		private string ExpandableAreaIdentifier => $"GUIRepo_ExpandableArea_{DependencyInfo.Url}_{DependencyInfo.Branch}_{DependencyInfo.SubFolder}";
		
		private Repository _repo
		{
			get
			{
				//Link to repository task needs to survive editor reloading in case job is in progress. We do this by never storing references. always calling get. This will lazy init if the repo does not exist else reuse the repo.
				return Repository.Get(DependencyInfo.Url,  DependencyInfo.Branch, RootFolder(),RelativeRepositoryPath(), RelativeRepositoryFolderPath());
			}
		}

		public GUIRepositoryPanel(Dependency info, Texture2D editIcon, Texture2D removeIcon, Texture2D pushIcon, Texture2D pullIcon)
		{
			DependencyInfo = info;
			
			_editIcon = editIcon;
			_removeIcon = removeIcon;
			_pushIcon = pushIcon;
			_pullIcon = pullIcon;

			bool previouslyOpen = EditorPrefs.GetBool(ExpandableAreaIdentifier, false);
			_expandableAreaAnimBool = new AnimBool(previouslyOpen);
			_expandableAreaAnimBool.valueChanged.AddListener(() => { OnRepaintRequested(); });
		}

		public bool Update()
		{
			if (_repo.InProgress || !_repo.LastOperationSuccess)
			{
				_repoWasInProgress = true;
				return true;
			}

			//Clear changes on the frame after repo finished updating.
			if (_repoWasInProgress)
			{
				_repoWasInProgress = false;
				return true;
			}
			return false;
		}

		public bool HasLocalChanges()
		{
			return _repo.HasUncommittedChanges || _repo.AheadOfOrigin;
		}

		public void UpdateStatus()
		{
			_repo.BlockAndUpdateStatus();
		}

		public void OnDrawGUI(int index)
		{
			Rect headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(18));
			Rect fullRect = new Rect();
			Rect bottomRect = new Rect();

			fullRect = headerRect;

			//Overlay to darken every second item
			Rect boxRect = fullRect;
			boxRect.y -= 1.5f;
			boxRect.height += 3;
			boxRect.x -= 5;
			boxRect.width += 10;

			//Header rects

			Rect labelRect = headerRect;
			labelRect.width = headerRect.x + headerRect.width - (90 + 15);
			labelRect.height = 18;
			labelRect.x += 15;

			Rect pullButtonRect = headerRect;
			pullButtonRect.x = headerRect.width - 20;
			pullButtonRect.width = 20;

			Rect pushButtonRect = pullButtonRect;
			pushButtonRect.x = headerRect.width - 40;

			//Rect openTerminalRect = pushButtonRect;
			//openTerminalRect.x = headerRect.width - 60;

			Rect editButtonRect = pushButtonRect;
			editButtonRect.x = headerRect.width - 60;

			Rect removeButtonRect = editButtonRect;
			removeButtonRect.x = headerRect.width - 80;

			Rect localChangesRect = removeButtonRect;
			localChangesRect.width = 10;
			localChangesRect.x = headerRect.width - 90;
			//Expanded rect

			Rect gitBashRect = bottomRect;
			gitBashRect.x = bottomRect.width - 68;
			gitBashRect.width = 60;
			gitBashRect.y += 1;
			gitBashRect.height = 15;

			//Full Rect
			Repository.Progress lastProgress = _repo.GetLastProgress();

			Rect progressRectOuter = fullRect;
			Rect progressRectInner = fullRect;
			progressRectInner.width = progressRectOuter.width * lastProgress.NormalizedProgress;

			if (index % 2 == 1)
			{
				GUI.Box(boxRect, "");
			}

			//_repo.BlockAndUpdateStatus();
			
			bool updateNeeded = _repo.HasUncommittedChanges || _repo.AheadOfOrigin;
			bool remoteChanges = _repo.BehindOrigin;
				
			if(updateNeeded)
			{
				GUI.color = Color.yellow;
				GUI.Label(localChangesRect, new GUIContent("*", $"Local changes detected!\n\n{_repo.PrintableStatus}"), EditorStyles.miniBoldLabel);
				GUI.color = Color.white;
			}
			else if (remoteChanges)
			{
				GUI.color = Color.green;
				GUI.Label(localChangesRect, new GUIContent("*", $"Remote changes detected!\n\n{_repo.PrintableStatus}"), EditorStyles.miniBoldLabel);
				GUI.color = Color.white;
			}

			if (_repo.InProgress)
			{
				GUI.Box(progressRectOuter, "");

				GUI.color = lastProgress.Error ? Color.red : Color.green;

				GUI.Box(progressRectInner, "");
				GUI.color = Color.white;

				//GUI.Label(updatingLabelRect, ( ) + , EditorStyles.miniLabel);
				//GUI.Label(progressMessageRect, , EditorStyles.miniLabel);
			}
			else if (lastProgress.Error)
			{
				GUIStyle failureStyle = new GUIStyle(EditorStyles.label);
				failureStyle.richText = true;
				failureStyle.alignment = TextAnchor.MiddleRight;
				GUI.Label(labelRect, new GUIContent("<b><color=red>Failure</color></b>", lastProgress.Message), failureStyle);
			}

			if (!_repo.InProgress)
			{
				DrawUpdateButton(pullButtonRect, updateNeeded);
			}

			GUIStyle iconButtonStyle = new GUIStyle( EditorStyles.miniButton);
			iconButtonStyle.padding = new RectOffset(3,3,3,3);

			GUIStyle toggledIconButtonStyle = new GUIStyle( EditorStyles.miniButton);
			toggledIconButtonStyle.padding = new RectOffset(2,2,2,2);

			if (!_repo.InProgress && GUI.Button(removeButtonRect, new GUIContent(_removeIcon, "Remove the repository from this project."), iconButtonStyle))
			{
				if (EditorUtility.DisplayDialog("Remove " + DependencyInfo.Name + "?", "\nThis will remove the repository from the project.\n" +
					((updateNeeded)?"\nAll local changes will be discarded.\n":"") + "\nThis can not be undone.", "Yes", "Cancel"))
				{
					OnRemovalRequested(DependencyInfo.Name, DependencyInfo.Url, RelativeRepositoryPath());
				}
			};
			if (!_repo.InProgress && GUI.Button(editButtonRect, new GUIContent(_editIcon, "Edit this repository"), iconButtonStyle))
			{
				OnEditRequested(DependencyInfo, RelativeRepositoryPath());
			};
			if (!updateNeeded)
			{
				GUI.enabled = false;
			}

			GUIStyle togglePushButtonStyle = _expandableAreaAnimBool.value?toggledIconButtonStyle:iconButtonStyle;
			if (!_repo.InProgress && GUI.Button(pushButtonRect, new GUIContent(_pushIcon, "Push changes"), togglePushButtonStyle))
			{
				OpenPushWindow();
			};

			GUI.enabled = true;

			GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
			labelStyle.richText = true;

			string repoText = DependencyInfo.Name + "  <b><size=9>" + DependencyInfo.Branch + "</size></b>" +
			                  (_repo.InProgress
				                  ? $" <i><size=9>{lastProgress.Message}{GUIUtility.GetLoadingDots()}</size></i>"
				                  : "");
			GUI.Label(labelRect, new GUIContent(repoText, DependencyInfo.Url), labelStyle);

			if (_repo.RefreshPending)
			{
				OnRefreshRequested(new[]{this});
				_repo.RefreshPending = false;
			}


			if (EditorGUILayout.BeginFadeGroup(_expandableAreaAnimBool.faded))
			{
				if (_pushPanel == null)
				{
					_pushPanel = new GUIPushPanel(DependencyInfo.Url, _repo.Branch, DependencyInfo.Name, _repo,(branch, message) =>
					{
						_repo.PushChanges(branch, message);
					}, () =>
					{
						_repo.ClearLocalChanges();
					});
				}
				_pushPanel.OnDrawGUI();
			}
			EditorGUILayout.EndFadeGroup();

			//TODO: when we edit a repo we want to remove and re-download it.
			//TODO: or re-update it if the url and root folder has not changed
			//TODO: and update the gitignore as expected!
		}

		private void DrawUpdateButton(Rect rect, bool updateNeeded)
		{
			GUIStyle iconButtonStyle = new GUIStyle( EditorStyles.miniButton);
			iconButtonStyle.padding = new RectOffset(3,3,3,3);

			if (GUI.Button(rect, new GUIContent(_pullIcon, "Pull or clone into project."), iconButtonStyle))
			{
				if (updateNeeded)
				{
					if (EditorUtility.DisplayDialog("Local Changes Detected", DependencyInfo.Name + " has local changes. Updating will permanently delete them. Continue?", "Yes", "No"))
					{
						UpdateRepository();
					}
				}
				else
				{
					UpdateRepository();
				}
			};
		}

		public bool Busy()
		{
			return _repo.InProgress || !_repo.LastOperationSuccess;
		}

		public void UpdateRepository()
		{
			_repo.TryUpdate();
		}

		public void OpenPushWindow()
		{
			_expandableAreaAnimBool.target = !_expandableAreaAnimBool.target;
			EditorPrefs.SetBool(ExpandableAreaIdentifier, _expandableAreaAnimBool.target);
		}
	}
}