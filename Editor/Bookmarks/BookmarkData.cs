using System.Collections.Generic;
using UnityEngine;

namespace Sanat.CodeGenerator.Editor.Bookmarks
{
    [CreateAssetMenu(fileName = "New Bookmark", menuName = "Code Generator/Bookmark", order = 1)]
    public class BookmarkData : ScriptableObject
    {
        public string bookmarkName;
        public List<string> selectedClassNames = new List<string>();
        public string task;
        public int category;
        
        public void Initialize(string name, List<string> classNames, int category, string task)
        {
            bookmarkName = name;
            selectedClassNames = new List<string>(classNames);
            this.category = category;
            this.task = task;
        }
    }
}