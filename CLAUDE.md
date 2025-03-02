# Git Sub-Repository Manager for Unity

## Project Overview
Unity plugin for managing git sub-repositories. Provides GUI tools to add, update, push, and manage git repositories from within Unity.

## Build/Run Commands
- Open in Unity 2019.2+ (Windows supported, Mac/Linux untested)
- No specific build commands as this is a Unity plugin

## Code Style Guidelines
- **Naming**: PascalCase for classes, methods; camelCase with underscore for private fields (_variableName)
- **Namespace**: GitRepositoryManager
- **Error Handling**: Uses exception handling with specific error messages
- **Threading**: Uses C# Tasks for asynchronous git operations with proper locking
- **Unity Patterns**: Follows Unity editor window patterns with OnGUI method
- **Classes**:
  - Repository: Core data structure representing a repository
  - GitProcessHelper: Handles git commands execution
  - GUIRepositoryManagerWindow: Main editor window
  - GUIRepositoryPanel/GUIPushPanel: Sub-panels for repository management

## Project Structure
- `/Assets/Package/Core/`: Core functionality (Repository, GitProcessHelper)
- `/Assets/Package/GUI/`: Unity editor interface components
- `/Assets/Package/Resources/`: UI resources (icons)
- `/Assets/Repositories/`: Where managed repositories are stored