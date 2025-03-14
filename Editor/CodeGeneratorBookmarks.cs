// Assets/Sanat/CodeGenerator/Editor/Bookmarks/CodeGeneratorBookmarks.cs
using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;

namespace Sanat.CodeGenerator.Editor.Bookmarks
{
    public class CodeGeneratorBookmarks
    {
        private BookmarkDatabase database;
        private string newBookmarkName = "";
        private string bookmarkSearchQuery = "";
        private Vector2 bookmarkScrollPosition;
        private bool showBookmarks = false;
        private ReorderableList bookmarkList;
        private Texture2D bookmarkIcon;
        private CodeGenerator codeGenerator;
        private string _className;
        
        public string ClassName {
            get {
                if (string.IsNullOrEmpty(_className)) _className = $"<color=#56dd12>CodeGeneratorBookmarks</color>";
                return _className;
            }
        }
        
        public event Action OnBookmarkSaved;
        public event System.Action<BookmarkData> OnBookmarkLoaded;
        
        public CodeGeneratorBookmarks()
        {
            InitializeDatabase();
        }
        
        private void InitializeDatabase()
        {
            database = BookmarkDatabase.GetOrCreateDatabase();
            if (database == null)
            {
                Debug.LogError($"{ClassName} Failed to initialize bookmark database!");
            }
        }
        
        public List<BookmarkData> GetBookmarks()
        {
            if (database == null) InitializeDatabase();
            return database.bookmarks;
        }
        
        public void DrawBookmarksUI(CodeGenerator codeGeneratorEditorWindow)
        {
            codeGenerator = codeGeneratorEditorWindow;
            if (!codeGenerator.IsSettingsLoaded)
                return;
                
            if (database == null) InitializeDatabase();
                
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showBookmarks = EditorGUILayout.Foldout(showBookmarks, "Project Bookmarks", true);
            if (showBookmarks)
            {
                EditorGUILayout.Space();
                DrawBookmarkCreation();
                DrawBookmarkSearch();
                DrawBookmarkList();
                
                // Migration button for legacy bookmarks
                if (GUILayout.Button("Migrate Legacy Bookmarks"))
                {
                    MigrateLegacyBookmarks();
                }
            }
            EditorGUILayout.EndVertical();
        }
        
        private void DrawBookmarkCreation()
        {
            EditorGUILayout.BeginHorizontal();
            newBookmarkName = EditorGUILayout.TextField("New Bookmark Name", newBookmarkName);
            string[] categories = { "General", "UI", "Gameplay", "Audio" };
            int selectedCategory = EditorGUILayout.Popup("Category", 0, categories);
            if (GUILayout.Button("Save Bookmark", GUILayout.Width(120)))
            {
                SaveBookmark(newBookmarkName, codeGenerator.selectedClassNames, selectedCategory, codeGenerator.taskInput);
                newBookmarkName = "";
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawBookmarkSearch()
        {
            bookmarkSearchQuery = EditorGUILayout.TextField("Search Bookmarks", bookmarkSearchQuery);
        }
        
        private void DrawBookmarkList()
        {
            if (database == null || database.bookmarks == null || database.bookmarks.Count == 0)
            {
                EditorGUILayout.LabelField("No bookmarks saved in this project.");
                return;
            }
            
            bookmarkScrollPosition = EditorGUILayout.BeginScrollView(bookmarkScrollPosition, GUILayout.Height(200));
            if (bookmarkList != null)
            {
                try
                {
                    bookmarkList.DoLayoutList();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{ClassName} Error in bookmark list: {e.Message}. Reinitializing...");
                    InitializeReorderableList();
                }
            }
            EditorGUILayout.EndScrollView();
        }
        
        public void InitializeReorderableList()
        {
            if (database == null) InitializeDatabase();
            
            bookmarkList = new ReorderableList(database.bookmarks, typeof(BookmarkData), true, true, false, false);
            bookmarkList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Project Bookmarks");
            bookmarkList.drawElementCallback = DrawBookmarkElement;
            bookmarkList.onReorderCallback = (ReorderableList list) => {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            };
        }
        
        private void DrawBookmarkElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= database.bookmarks.Count)
            {
                Debug.LogWarning($"Invalid bookmark index: {index}. Total bookmarks: {database.bookmarks.Count}");
                return;
            }
            
            var bookmark = database.bookmarks[index];
            if (bookmark == null)
            {
                Debug.LogWarning($"Null bookmark at index {index}");
                return;
            }
            
            if (!string.IsNullOrEmpty(bookmarkSearchQuery) &&
                !bookmark.bookmarkName.ToLower().Contains(bookmarkSearchQuery.ToLower()))
            {
                return;
            }
            
            float iconWidth = 20;
            float buttonWidth = 60;
            float spacing = 5;
            
            string tooltipSeparator = bookmark.selectedClassNames.Count > 10 ? ", " : "\n";
            string tooltip = $"[{bookmark.selectedClassNames.Count}]: ";
            tooltip += string.Join(tooltipSeparator, bookmark.selectedClassNames);
            
            EditorGUI.LabelField(
                new Rect(rect.x + iconWidth + spacing, rect.y, rect.width - iconWidth - buttonWidth * 3 - spacing * 4, rect.height),
                new GUIContent(bookmark.bookmarkName, tooltip)
            );
            
            if (GUI.Button(new Rect(rect.xMax - buttonWidth * 3 - spacing * 2, rect.y, buttonWidth, rect.height), "Add"))
            {
                AddBookmark(bookmark);
            }
            
            if (GUI.Button(new Rect(rect.xMax - buttonWidth * 2 - spacing, rect.y, buttonWidth, rect.height), "Load"))
            {
                LoadBookmark(bookmark);
            }
            
            if (GUI.Button(new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height), "Delete"))
            {
                if (EditorUtility.DisplayDialog("Confirm Deletion",
                    $"Are you sure you want to delete the bookmark '{bookmark.bookmarkName}'?", "Yes", "No"))
                {
                    DeleteBookmark(bookmark);
                    InitializeReorderableList(); // Reinitialize the list after deletion
                }
            }
        }
        
        public void SaveBookmark(string name, List<string> selectedClassNames, int category, string task)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("Bookmark name cannot be empty.");
                return;
            }
            
            var bookmark = database.AddBookmark(name, selectedClassNames, category, task);
            
            Debug.Log($"{ClassName} New bookmark saved: {bookmark.bookmarkName}. "+
                      $"Selected Classes ({bookmark.selectedClassNames.Count}): "+
                      $"{string.Join(", ", bookmark.selectedClassNames)}; "+
                      $"Task: {bookmark.task}; Category: {bookmark.category}");
                      
            OnBookmarkSaved?.Invoke();
            InitializeReorderableList();
            
            if (codeGenerator != null)
            {
                codeGenerator.Repaint();
            }
        }
        
        public void LoadBookmark(BookmarkData bookmark)
        {
            if (bookmark != null)
            {
                OnBookmarkLoaded?.Invoke(bookmark);
            }
        }
        
        public void AddBookmark(BookmarkData bookmark)
        {
            if (bookmark != null)
            {
                var selectedClasses = codeGenerator.selectedClassNames;
                var oldAmount = selectedClasses.Count;
                var oldClasses = selectedClasses;
                selectedClasses.AddRange(bookmark.selectedClassNames);
                codeGenerator.selectedClassNames = new List<string>(selectedClasses.Distinct());
                selectedClasses = codeGenerator.selectedClassNames;
                var newAmount = selectedClasses.Count;
                var addedClasses = selectedClasses.Except(oldClasses).ToList();
                Debug.Log($"{ClassName} Added {newAmount-oldAmount} classes to selection: {string.Join(", ", addedClasses)}");
            }
        }
        
        public void DeleteBookmark(BookmarkData bookmark)
        {
            database.DeleteBookmark(bookmark);
        }
        
        // Migration functionality
        private void MigrateLegacyBookmarks()
        {
            string json = EditorPrefs.GetString("CodeGeneratorBookmarks", "");
            if (string.IsNullOrEmpty(json))
            {
                EditorUtility.DisplayDialog("Migration", "No legacy bookmarks found to migrate.", "OK");
                return;
            }
            
            try
            {
                SerializableBookmarkList legacyBookmarks = JsonUtility.FromJson<SerializableBookmarkList>(json);
                int migratedCount = 0;
                
                foreach (var legacyBookmark in legacyBookmarks.bookmarks)
                {
                    // Check if bookmark with same name already exists
                    if (database.FindBookmarkByName(legacyBookmark.Name) == null)
                    {
                        database.AddBookmark(
                            legacyBookmark.Name,
                            legacyBookmark.SelectedClassNames,
                            legacyBookmark.Category,
                            legacyBookmark.Task
                        );
                        migratedCount++;
                    }
                }
                
                EditorUtility.DisplayDialog("Migration Complete", 
                    $"Successfully migrated {migratedCount} bookmarks to project-specific storage.", "OK");
                
                // Clear the EditorPrefs after successful migration
                if (migratedCount > 0 && 
                    EditorUtility.DisplayDialog("Clear Legacy Bookmarks", 
                        "Would you like to clear the legacy bookmarks from EditorPrefs?", "Yes", "No"))
                {
                    EditorPrefs.DeleteKey("CodeGeneratorBookmarks");
                }
                
                InitializeReorderableList();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to migrate legacy bookmarks: {e.Message}");
                EditorUtility.DisplayDialog("Migration Failed", 
                    $"Failed to migrate legacy bookmarks: {e.Message}", "OK");
            }
        }
        
        // For backward compatibility with legacy system
        [System.Serializable]
        private class SerializableBookmarkList
        {
            public List<Bookmark> bookmarks;
        }
        
        [System.Serializable]
        public class Bookmark
        {
            public string Name;
            public List<string> SelectedClassNames;
            public string Task;
            public int Category;
        }
    }
}