﻿
using LibGit2Sharp;
using GitRepositoryManager.CredentialManagers;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace GitRepositoryManager
{
	/// <summary>
	/// Note readonly once created. Create a new one to change values.
	/// </summary>
	public class Repository
	{
		private static List<Repository> _repos = new List<Repository>();
		public static Repository Get(ICredentialManager credentials, string url, string localDestination, string copyDestination, string subFolder, string branch = "master", string tag = "")
		{
			foreach(Repository repo in _repos)
			{
				if(repo._state.Url == url &&
					repo._state.CopyDestination == copyDestination &&
					repo._state.LocalDestination == localDestination &&
					repo._state.SubFolder == subFolder)
				{
					if (repo._state.Branch != branch || repo._state.Tag != tag)
					{
						throw new Exception("[Repository] A repository exists that points to a different branch or tag, but the copy destination is the same!");
					}

					return repo;
				}
			}

			Repository newRepo = new Repository(credentials, url, localDestination, copyDestination, branch, tag);
			_repos.Add(newRepo);
			return newRepo;
		}

		public static void Remove(string url, string localDestination, string copyDestination)
		{
			for(int i = _repos.Count-1; i >=0 ; i--)
			{
				Repository repo = _repos[i];
				if (repo._state.Url == url &&
					repo._state.CopyDestination == copyDestination &&
					repo._state.LocalDestination == localDestination)
				{

					_repos.RemoveAt(i);
				}
			}
		}

		public static int TotalInitialized
		{
			get
			{
				return _repos.Count;
			}
		}

		public class RepoState
		{
			public ICredentialManager CredentialManager;
			public string Url; //Remote repo base
			public string LocalDestination; //Where the repo will be cloned to.
			public string CopyDestination; //Where the required part of the repo will be coppied to.
			public string SubFolder; 
			public string Branch;
			public string Tag;
		}

		private RepoState _state;
		private volatile bool _inProgress;
		private volatile bool _cancellationPending;
		private volatile bool _lastOperationSuccess;

		public struct Progress
		{
			public Progress(float normalizedProgress, string message)
			{
				NormalizedProgress = normalizedProgress;
				Message = message;
			}

			public float NormalizedProgress;
			public string Message;
		}


		//TODO: for some reason repositories are not comparing on Repository.Get() causing lots of new ones to spawn.
		//TODO: using a get for the sub repo will not work as it does not use git authentication. Should rather check the path is valid after clone. If not remove path or find closest?
		//TODO: fix import not working properly
		//TODO: Allow us to push back to remote if in front. If merge conflicts open folder
		//TODO: thats it probably

		private ConcurrentQueue<Progress> _progressQueue = new ConcurrentQueue<Progress>();

		public Repository(ICredentialManager credentials, string url, string localDestination, string copyDestination, string subFolder, string branch = "master", string tag = "")
		{
			_state = new RepoState
			{
				CredentialManager = credentials,
				Url = url,
				LocalDestination = localDestination,
				CopyDestination = copyDestination,
				SubFolder = subFolder,
				Branch = branch,
				Tag = tag
			};

			TryUpdate();
		}

		public bool TryUpdate()
		{
			if(!_inProgress)
			{
				_inProgress = true;
				_lastOperationSuccess = true;
				ThreadPool.QueueUserWorkItem(UpdateTask, _state);
				return true;
			}
			else
			{
				return false;
			}
		}

		public bool TryRemoveCopy()
		{
			if (!Directory.Exists(_state.LocalDestination))
			{
				return false;
			}

			Directory.Delete(Path.Combine(_state.CopyDestination, _state.SubFolder));
			return true;
		}

		public List<string> Copy()
		{
			string localTarget = Path.Combine(_state.LocalDestination, _state.SubFolder);
			string localDestination = Path.Combine(_state.CopyDestination, _state.SubFolder);
			if (!Directory.Exists(localTarget))
			{
				return new List<string>();
			}

			return DirectoryCopy(localTarget, localDestination, true, true, ".git");

			// https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories?redirectedfrom=MSDN
			List<string> DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, bool overwrite, params string[] foldersToIgnore)
			{
				List<string> modifiedFiles = new List<string>();

				// Get the subdirectories for the specified directory.
				DirectoryInfo dir = new DirectoryInfo(sourceDirName);

				if (!dir.Exists)
				{
					throw new DirectoryNotFoundException(
						"Source directory does not exist or could not be found: "
						+ sourceDirName);
				}

				DirectoryInfo[] dirs = dir.GetDirectories();
				// If the destination directory doesn't exist, create it.
				if (!Directory.Exists(destDirName))
				{
					Directory.CreateDirectory(destDirName);
				}

				// Get the files in the directory and copy them to the new location.
				FileInfo[] files = dir.GetFiles();
				foreach (FileInfo file in files)
				{
					string temppath = Path.Combine(destDirName, file.Name);

					if (File.Exists(temppath))
					{
						File.SetAttributes(temppath, FileAttributes.Normal);
					}

					file.CopyTo(temppath, overwrite);
					modifiedFiles.Add(temppath);
				}

				// If copying subdirectories, copy them and their contents to new location.
				if (copySubDirs)
				{
					foreach (DirectoryInfo subdir in dirs)
					{
						if(!foldersToIgnore.Contains(subdir.Name))
						{
							string temppath = Path.Combine(destDirName, subdir.Name);
							modifiedFiles.AddRange(DirectoryCopy(subdir.FullName, temppath, copySubDirs, overwrite, foldersToIgnore));
						}
					}
				}

				return modifiedFiles;
			}
		}

		public void CancelUpdate()
		{
			if(_inProgress) _cancellationPending = true;
			else
			{
				_cancellationPending = false;
				_lastOperationSuccess = true;
			}
		}

		public bool InProgress
		{
			get
			{
				return _inProgress; 
			}
		}

		public bool LastOperationSuccess
		{
			get
			{
				return _lastOperationSuccess;
			}
		}


		public bool CancellationPending
		{
			get
			{
				return _cancellationPending;
			}
		}

		public Progress GetLastProgress()
		{
			Progress currentProgress;
			if(_progressQueue.Count > 0)
			{	
				if(_progressQueue.Count > 1)
				{
					_progressQueue.TryDequeue(out currentProgress);
				}
				else
				{
					_progressQueue.TryPeek(out currentProgress);
				}
			}
			else
			{
				currentProgress = new Progress(0, "Update Pending");
			}

			return currentProgress;
		}

		/// <summary>
		/// Runs in a thread pool. should clone then checkout the appropriate branch/commit. copy subdirectory into specified repo.
		/// </summary>
		/// <param name="stateInfo"></param>
		private void UpdateTask(object stateInfo)
		{
			//Do as much as possible outside of unity so we dont get constant rebuilds. Only when everything is ready 
			RepoState state = (RepoState)stateInfo;

			if(state == null)
			{
				_lastOperationSuccess = false;
				_progressQueue.Enqueue(new Progress(0, "Repository state info is null"));
				return;
			}

			if (state.CredentialManager == null)
			{
				_lastOperationSuccess = false;
				_progressQueue.Enqueue(new Progress(0, "Credentials manager is null"));
				return;
			}

			FetchOptions fetchOptions = new FetchOptions()
			{
				TagFetchMode = TagFetchMode.All,
				OnTransferProgress = new LibGit2Sharp.Handlers.TransferProgressHandler((progress) => 
				{
					_progressQueue.Enqueue(new Progress(progress.ReceivedObjects / progress.TotalObjects, "Fetching " + progress.ReceivedObjects + "/" + progress.TotalObjects + "(" + progress.ReceivedBytes + " bytes )"));
					
					return _cancellationPending;
				}),
				CredentialsProvider = (credsUrl, user, supportedCredentials) =>
				{
					state.CredentialManager.GetCredentials(credsUrl, user, supportedCredentials, out var credentials, out string message);
					return credentials;
				}
			};

			if (LibGit2Sharp.Repository.IsValid(state.LocalDestination))
			{
				_progressQueue.Enqueue(new Progress(0, "Found local repository."));

				//Repo exists we are doing a pull
				using (var repo = new LibGit2Sharp.Repository(state.LocalDestination))
				{
					_progressQueue.Enqueue(new Progress(0, "Nuking local changes. Checking out " + state.Branch));

					Branch branch = repo.Branches[state.Branch];
					Commands.Checkout(repo, branch, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force, CheckoutNotifyFlags = CheckoutNotifyFlags.None});

					// Credential information to fetch
					PullOptions options = new PullOptions
					{
						FetchOptions = fetchOptions
					};
					
					// User information to create a merge commit. Should not happen as we force checkout before pulling.
					var signature = new LibGit2Sharp.Signature(
						new Identity("MergeNotAllowed", "MergeNotAllowed@MergeMail.com"), DateTimeOffset.Now);

					try
					{
						_progressQueue.Enqueue(new Progress(0, "Pulling from " + state.Url));
						Commands.Pull(repo, signature, options);
						_progressQueue.Enqueue(new Progress(1, "Complete"));
						_lastOperationSuccess = true;
					}
					catch (Exception e)
					{
						_progressQueue.Enqueue(new Progress(0, "Pull failed: " + e.Message));
						_lastOperationSuccess = false;
					}
				}
			}
			else
			{
				_progressQueue.Enqueue(new Progress(0, "Initializing clone"));

				//Repo does not exist. Clone it.
				CloneOptions options = new CloneOptions()
				{
					CredentialsProvider = (credsUrl, user, supportedCredentials) =>
					{
						state.CredentialManager.GetCredentials(credsUrl, user, supportedCredentials, out var credentials, out string message);
						return credentials;
					},

					IsBare = false, // True will result in a bare clone, false a full clone.
					Checkout = true, // If true, the origin's HEAD will be checked out. This only applies to non-bare repositories.
					BranchName = state.Branch, // The name of the branch to checkout. When unspecified the remote's default branch will be used instead.
					RecurseSubmodules = false, // Recursively clone submodules.
					OnCheckoutProgress = new LibGit2Sharp.Handlers.CheckoutProgressHandler((message, value, total) =>
					{
						_progressQueue.Enqueue(new Progress(Math.Max(Math.Min(value / total, 1), 0), message));
					}), // Handler for checkout progress information.	
					FetchOptions = fetchOptions
				};

				try
				{
					_progressQueue.Enqueue(new Progress(0, "Cloning " + state.Url));
					LibGit2Sharp.Repository.Clone(state.Url, state.LocalDestination, options);
					_progressQueue.Enqueue(new Progress(1, "Complete"));
					_lastOperationSuccess = true;
				}
				catch(Exception e)
				{
					_progressQueue.Enqueue(new Progress(0, "Clone failed: " + e.Message));
					_lastOperationSuccess = false;
				}
			}

			//Once completed
			_inProgress = false;
			_cancellationPending = false;
		}
	}
}
