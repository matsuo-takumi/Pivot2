using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.CodeModule.Models;
using Pivot.Services;

namespace Pivot.CodeModule.Services
{
    public class CodeRepository : ICodeRepository
    {
        private readonly SQLiteDbContext _context;
        private readonly SettingsService _settingsService;

        public CodeRepository(SQLiteDbContext context, SettingsService settingsService)
        {
            _context = context;
            _settingsService = settingsService;
        }

        public IEnumerable<CodeFile> GetAll() => _context.CodeFiles.OrderByDescending(c => c.Updated);

        public IEnumerable<CodeFile> Search(string query)
            => _context.CodeFiles
               .Where(c => c.Title.Contains(query) || c.Content.Contains(query) || c.Tags.Contains(query));

        public IEnumerable<CodeFile> Filter(string language, string tool, string tag)
            => _context.CodeFiles.Where(c =>
                (string.IsNullOrEmpty(language) || c.Language == language) &&
                (string.IsNullOrEmpty(tool) || c.Tool == tool) &&
                (string.IsNullOrEmpty(tag) || c.Tags.Contains(tag)));

        public void Save(CodeFile file)
        {
            if (_context.CodeFiles.Any(f => f.Id == file.Id))
                _context.CodeFiles.Update(file);
            else
                _context.CodeFiles.Add(file);

            _context.SaveChanges();
        }

        public void Delete(Guid id)
        {
            var item = _context.CodeFiles.Find(id);
            if (item != null)
            {
                _context.CodeFiles.Remove(item);
                _context.SaveChanges();
            }
        }

        public IEnumerable<Pivot.CodeModule.Models.CodeTag> GetAllTags()
        {
            return _context.Tags.OrderBy(t => t.Name).ToList();
        }

        public void AddTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var trimmed = name.Trim();
            if (_context.Tags.Any(t => t.Name == trimmed)) return;
            _context.Tags.Add(new Pivot.CodeModule.Models.CodeTag { Name = trimmed });
            _context.SaveChanges();
        }

        public string GetFilterNameById(Guid filterId)
        {
            return _settingsService.GetFilterNameById(filterId);
        }
    }
}


