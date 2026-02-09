using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pivot.Engine.Data;
using Pivot.Engine.Models;
using Pivot.Models;

namespace Pivot.Services
{
    public class SmartFolderService
    {
        private readonly IDbContextFactory<PivotDbContext> _dbFactory;
        private readonly ILogger<SmartFolderService> _logger;

        public SmartFolderService(
            IDbContextFactory<PivotDbContext> dbFactory,
            ILogger<SmartFolderService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<List<SmartFolder>> GetSmartFoldersAsync()
        {
            try
            {
                using var db = await _dbFactory.CreateDbContextAsync();
                return await db.SmartFolders
                    .OrderBy(f => f.SortOrder)
                    .ThenBy(f => f.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load smart folders");
                return new List<SmartFolder>();
            }
        }

        public async Task<SmartFolder> SaveSmartFolderAsync(SmartFolder folder)
        {
            using var db = await _dbFactory.CreateDbContextAsync();
            
            if (folder.Id == 0)
            {
                // Create
                folder.CreatedAt = DateTime.UtcNow;
                folder.UpdatedAt = DateTime.UtcNow;
                
                // Set sort order to end
                var maxSort = await db.SmartFolders.MaxAsync(f => (int?)f.SortOrder) ?? 0;
                folder.SortOrder = maxSort + 1;
                
                db.SmartFolders.Add(folder);
            }
            else
            {
                // Update
                var existing = await db.SmartFolders.FindAsync(folder.Id);
                if (existing == null) throw new KeyNotFoundException($"SmartFolder {folder.Id} not found");
                
                existing.Name = folder.Name;
                existing.IconGlyph = folder.IconGlyph;
                existing.CriteriaJson = folder.CriteriaJson;
                existing.SortOrder = folder.SortOrder;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            
            await db.SaveChangesAsync();
            return folder;
        }

        public async Task DeleteSmartFolderAsync(int id)
        {
            using var db = await _dbFactory.CreateDbContextAsync();
            var folder = await db.SmartFolders.FindAsync(id);
            if (folder != null)
            {
                db.SmartFolders.Remove(folder);
                await db.SaveChangesAsync();
            }
        }
    }
}
