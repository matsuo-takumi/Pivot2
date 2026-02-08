using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Pivot.Engine.Models;

namespace Pivot.Utilities
{
    /// <summary>
    /// Utility class for building directory tree structures from file paths.
    /// </summary>
    public static class DirectoryTreeBuilder
    {
        /// <summary>
        /// Build a folder tree from root directories.
        /// </summary>
        public static void BuildTree(ObservableCollection<FolderNode> targetCollection, IEnumerable<string> rootDirectories)
        {
            targetCollection.Clear();
            foreach (var dir in rootDirectories)
            {
                if (Directory.Exists(dir))
                {
                    var node = new FolderNode(new DirectoryInfo(dir).Name, dir);
                    BuildRecursive(node);
                    targetCollection.Add(node);
                }
            }
        }

        /// <summary>
        /// Recursively build child nodes for a folder.
        /// </summary>
        private static void BuildRecursive(FolderNode node)
        {
            try
            {
                var subDirs = Directory.GetDirectories(node.FullPath);
                foreach (var dir in subDirs)
                {
                    var subNode = new FolderNode(new DirectoryInfo(dir).Name, dir);
                    BuildRecursive(subNode);
                    node.Children.Add(subNode);
                }
            }
            catch { }
        }
    }
}
