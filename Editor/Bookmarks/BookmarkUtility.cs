// Assets/Sanat/CodeGenerator/Editor/Bookmarks/BookmarkUtility.cs
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Sanat.CodeGenerator.Editor.Bookmarks
{
    public static class BookmarkUtility
    {
        private const string BOOKMARK_FOLDER = "Assets/Sanat/CodeGenerator/Bookmarks";
        
        [MenuItem("Tools/Sanat/CodeGenerator/Bookmarks/Create Bookmark Database")]
        public static void CreateBookmarkDatabase()
        {
            EnsureBookmarkFolderExists();
            BookmarkDatabase.GetOrCreateDatabase();
            EditorUtility.DisplayDialog("Bookmark Database", "Bookmark database created successfully.", "OK");
        }
        
        [MenuItem("Tools/Sanat/CodeGenerator/Bookmarks/Export Bookmarks")]
        public static void ExportBookmarks()
        {
            var database = BookmarkDatabase.GetOrCreateDatabase();
            if (database.bookmarks.Count == 0)
            {
                EditorUtility.DisplayDialog("Export Bookmarks", "No bookmarks to export.", "OK");
                return;
            }
            
            string path = EditorUtility.SaveFilePanel("Export Bookmarks", "", "CodeGeneratorBookmarks.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            
            // Create serializable data
            SerializableBookmarkExport export = new SerializableBookmarkExport();
            foreach (var bookmark in database.bookmarks)
            {
                export.bookmarks.Add(new SerializableBookmark
                {
                    name = bookmark.bookmarkName,
                    selectedClassNames = bookmark.selectedClassNames,
                    task = bookmark.task,
                    category = bookmark.category
                });
            }
            
            string json = JsonUtility.ToJson(export, true);
            File.WriteAllText(path, json);
            
            EditorUtility.DisplayDialog("Export Complete", 
                $"Successfully exported {database.bookmarks.Count} bookmarks.", "OK");
        }
        
        [MenuItem("Tools/Sanat/CodeGenerator/Bookmarks/Import Bookmarks")]
        public static void ImportBookmarks()
        {
            string path = EditorUtility.OpenFilePanel("Import Bookmarks", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            
            string json = File.ReadAllText(path);
            SerializableBookmarkExport import;
            
            try
            {
                import = JsonUtility.FromJson<SerializableBookmarkExport>(json);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Import Failed", 
                    $"Failed to parse bookmark file: {e.Message}", "OK");
                return;
            }
            
            var database = BookmarkDatabase.GetOrCreateDatabase();
            int importedCount = 0;
            
            foreach (var bookmarkData in import.bookmarks)
            {
                if (database.FindBookmarkByName(bookmarkData.name) == null)
                {
                    database.AddBookmark(
                        bookmarkData.name,
                        bookmarkData.selectedClassNames,
                        bookmarkData.category,
                        bookmarkData.task
                    );
                    importedCount++;
                }
            }
            
            EditorUtility.DisplayDialog("Import Complete", 
                $"Successfully imported {importedCount} bookmarks.", "OK");
        }
        
        private static void EnsureBookmarkFolderExists()
        {
            if (!Directory.Exists(BOOKMARK_FOLDER))
            {
                Directory.CreateDirectory(BOOKMARK_FOLDER);
                AssetDatabase.Refresh();
            }
        }
        
        [System.Serializable]
        private class SerializableBookmarkExport
        {
            public System.Collections.Generic.List<SerializableBookmark> bookmarks = 
                new System.Collections.Generic.List<SerializableBookmark>();
        }
        
        [System.Serializable]
        private class SerializableBookmark
        {
            public string name;
            public System.Collections.Generic.List<string> selectedClassNames;
            public string task;
            public int category;
        }
    }
}