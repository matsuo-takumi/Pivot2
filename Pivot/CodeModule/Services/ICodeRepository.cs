using System;
using System.Collections.Generic;
using Pivot.CodeModule.Models;

namespace Pivot.CodeModule.Services
{
    public interface ICodeRepository
    {
        IEnumerable<CodeFile> GetAll();

        IEnumerable<CodeFile> Search(string query);

        IEnumerable<CodeFile> Filter(string language, string tool, string tag);

        void Save(CodeFile file);

        void Delete(Guid id);
        IEnumerable<CodeFile> GetAllDeleted();
        void Restore(Guid id);

        IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags();
        void AddTag(string name);

        string GetFilterNameById(Guid filterId);
        
        /// <summary>
        /// Updates a tag name in all code snippets that have this tag.
        /// </summary>
        void UpdateTagInAllSnippets(string oldTagName, string newTagName);
        
        /// <summary>
        /// Removes a tag from all code snippets that have this tag.
        /// </summary>
        void RemoveTagFromAllSnippets(string tagName);
    }
}


