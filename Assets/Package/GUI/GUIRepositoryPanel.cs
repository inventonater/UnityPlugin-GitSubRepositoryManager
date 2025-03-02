using System;
using System.IO;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

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
			Rect bottomRect = new Rect();

			Rect fullRect = headerRect;

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
				GUI.color = Color.blue;
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
				
				// Enhanced error display with more specific information
				string errorMsg = "<b><color=red>Failure</color></b>";
				string tooltip = lastProgress.Message;
				bool isExpectedError = false;
				
				// Provide more helpful error messages
				if (lastProgress.Message.Contains("Directory does not exist"))
				{
					// This is expected for new repositories, show as info instead of error
					errorMsg = "<b><color=blue>Ready to clone</color></b>";
					tooltip = $"Click the pull button to clone {DependencyInfo.Name} from {DependencyInfo.Url}";
					isExpectedError = true;
				}
				else if (lastProgress.Message.Contains("not found"))
				{
					errorMsg = "<b><color=red>Repository not found</color></b>";
					tooltip = $"The repository could not be found. Check that the URL is correct: {DependencyInfo.Url}\n\nOriginal error: {lastProgress.Message}";
				}
				else if (lastProgress.Message.Contains("Permission denied"))
				{
					errorMsg = "<b><color=red>Permission denied</color></b>";
					tooltip = $"You don't have access to this repository. Check your credentials or SSH keys.\n\nOriginal error: {lastProgress.Message}";
				}
				else if (lastProgress.Message.Contains("network") || lastProgress.Message.Contains("timeout"))
				{
					errorMsg = "<b><color=red>Network error</color></b>";
					tooltip = $"A network error occurred. Check your internet connection.\n\nOriginal error: {lastProgress.Message}";
				}
				
				GUI.Label(labelRect, new GUIContent(errorMsg, tooltip), failureStyle);
				
				// Log the error to the console for easier debugging
				if (!isExpectedError)
				{
					Debug.LogError($"[GUIRepositoryPanel] Repository operation failed: {lastProgress.Message}");
				}
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
			}
			
			if (!_repo.InProgress && GUI.Button(editButtonRect, new GUIContent(_editIcon, "Edit this repository"), iconButtonStyle))
			{
				OnEditRequested(DependencyInfo, RelativeRepositoryPath());
			}
			
			if (!updateNeeded)
			{
				GUI.enabled = false;
				ClosePushWindow();
			}

			GUIStyle togglePushButtonStyle = _expandableAreaAnimBool.value?toggledIconButtonStyle:iconButtonStyle;
			if (!_repo.InProgress && GUI.Button(pushButtonRect, new GUIContent(_pushIcon, "Push changes"), togglePushButtonStyle))
			{
				TogglePushWindow();
			}

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
						ClosePushWindow();
						_repo.PushChanges(branch, message);
						PollDirty();
					}, () =>
					{
						_repo.ClearLocalChanges();
						//PollDirty();
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

			string tooltipText = updateNeeded 
				? "Pull or clone into project. WARNING: This will discard local changes!" 
				: "Pull or clone into project.";

			if (GUI.Button(rect, new GUIContent(_pullIcon, tooltipText), iconButtonStyle))
			{
				try
				{
					if (updateNeeded)
					{
						if (EditorUtility.DisplayDialog("Local Changes Detected", 
							$"{DependencyInfo.Name} has local changes. Updating will permanently delete them.\n\nStatus:\n{_repo.PrintableStatus}\n\nContinue?", 
							"Yes", "No"))
						{
							UpdateRepository();
							Debug.Log($"[GUIRepositoryPanel] Update initiated for {DependencyInfo.Name} (with local changes)");
						}
						else
						{
							Debug.Log($"[GUIRepositoryPanel] Update cancelled for {DependencyInfo.Name} due to local changes");
						}
					}
					else
					{
						Debug.Log($"[GUIRepositoryPanel] Update initiated for {DependencyInfo.Name}");
						UpdateRepository();
					}
				}
				catch (Exception ex)
				{
					// Log exception and show an error dialog
					Debug.LogError($"[GUIRepositoryPanel] Error updating repository {DependencyInfo.Name}: {ex.Message}\n{ex.StackTrace}");
					EditorUtility.DisplayDialog("Repository Update Failed", 
						$"Failed to update {DependencyInfo.Name}. Check console for details.\n\nError: {ex.Message}", 
						"OK");
				}
			}
		}

		public bool Busy()
		{
			return _repo.InProgress || !_repo.LastOperationSuccess;
		}

		public void UpdateRepository()
		{
			try
			{
				// Create the repositories directory if it doesn't exist
				string repoDirectory = Path.Combine(RootFolder(), "Assets", "Repositories");
				if (!Directory.Exists(repoDirectory))
				{
					Directory.CreateDirectory(repoDirectory);
					Debug.Log($"[GUIRepositoryPanel] Created repositories directory: {repoDirectory}");
				}
				
				// Ensure repository folder exists
				string fullRepoPath = Path.Combine(RootFolder(), RelativeRepositoryPath());
				if (!Directory.Exists(Path.GetDirectoryName(fullRepoPath)))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(fullRepoPath));
					Debug.Log($"[GUIRepositoryPanel] Created repository parent directory: {Path.GetDirectoryName(fullRepoPath)}");
				}
				
				// Start the update process
				bool updateStarted = _repo.TryUpdate();
				
				if (updateStarted)
				{
					Debug.Log($"[GUIRepositoryPanel] Repository update initiated for {DependencyInfo.Name}");
					PollDirty();
				}
				else
				{
					Debug.LogWarning($"[GUIRepositoryPanel] Repository update could not be started for {DependencyInfo.Name} - operation already in progress");
					EditorUtility.DisplayDialog("Operation In Progress", 
						$"An operation is already in progress for {DependencyInfo.Name}. Please wait for it to complete.", 
						"OK");
				}
			}
			catch (Exception ex)
			{
				string errorMsg = $"[GUIRepositoryPanel] Error starting repository update: {ex.Message}\n{ex.StackTrace}";
				Debug.LogError(errorMsg);
				
				EditorUtility.DisplayDialog("Update Failed", 
					$"Failed to start update for {DependencyInfo.Name}. Check the console for details.\n\nError: {ex.Message}", 
					"OK");
			}
		}

		public void TogglePushWindow()
		{
			_expandableAreaAnimBool.target = !_expandableAreaAnimBool.target;
			EditorPrefs.SetBool(ExpandableAreaIdentifier, _expandableAreaAnimBool.target);
		}
		
		public void ClosePushWindow()
		{
			_expandableAreaAnimBool.target = false;
			EditorPrefs.SetBool(ExpandableAreaIdentifier, _expandableAreaAnimBool.target);
		}

		private void PollDirty()
		{
			EditorApplication.update += Poll;
			void Poll()
			{
				if (_repo.Dirty)
				{
					OnRepaintRequested();
					_repo.Dirty = false;
					EditorApplication.update -= Poll;
				}
			}
			
		}
	}
}