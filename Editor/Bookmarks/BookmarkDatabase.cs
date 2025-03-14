using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

namespace Sanat.CodeGenerator.Editor.Bookmarks
{
    [CreateAssetMenu(fileName = "BookmarkDatabase", menuName = "Code Generator/Bookmark Database", order = 2)]
    public class BookmarkDatabase : ScriptableObject
    {
        public List<BookmarkData> bookmarks = new ();
        
        private const string DATABASE_PATH = "Assets/Sanat/CodeGenerator/Bookmarks";
        private const string DATABASE_NAME = "BookmarkDatabase.asset";
        
        public static BookmarkDatabase GetOrCreateDatabase()
        {
            // Ensure directory exists
            if (!Directory.Exists(DATABASE_PATH))
            {
                Directory.CreateDirectory(DATABASE_PATH);
            }
            
            // Try to load existing database
            string fullPath = Path.Combine(DATABASE_PATH, DATABASE_NAME);
            BookmarkDatabase database = AssetDatabase.LoadAssetAtPath<BookmarkDatabase>(fullPath);
            
            // Create new database if it doesn't exist
            if (database == null)
            {
                database = CreateInstance<BookmarkDatabase>();
                AssetDatabase.CreateAsset(database, fullPath);
                AssetDatabase.SaveAssets();
            }
            
            return database;
        }
        
        public BookmarkData AddBookmark(string name, List<string> selectedClassNames, int category, string task)
        {
            // Create bookmark asset
            BookmarkData newBookmark = CreateInstance<BookmarkData>();
            newBookmark.Initialize(name, selectedClassNames, category, task);
            
            // Save as asset
            string bookmarkPath = Path.Combine(DATABASE_PATH, $"{name}.asset");
            AssetDatabase.CreateAsset(newBookmark, AssetDatabase.GenerateUniqueAssetPath(bookmarkPath));
            
            // Add to database
            bookmarks.Add(newBookmark);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            
            return newBookmark;
        }
        
        public void DeleteBookmark(BookmarkData bookmark)
        {
            if (bookmarks.Contains(bookmark))
            {
                bookmarks.Remove(bookmark);
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                
                // Delete the asset
                string assetPath = AssetDatabase.GetAssetPath(bookmark);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }
        
        public BookmarkData FindBookmarkByName(string name)
        {
            return bookmarks.FirstOrDefault(b => b.bookmarkName == name);
        }
    }
}