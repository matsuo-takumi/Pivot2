using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.Services;
using Pivot.CodeModule.Models;
using Pivot.Models;

namespace Pivot.Services
{
    public class CodeNavigationService
    {
        private readonly FilterSettingsService _filterSettings;
        
        public CodeNavigationService(FilterSettingsService filterSettings)
        {
            _filterSettings = filterSettings;
        }

        public List<NavigationItem> GetNavigationItems()
        {
            var items = new List<NavigationItem>();
            try
            {
                // Add "All Snippets" item
                items.Add(NavigationItem.CreateAllSnippets());

                // Get filters from settings
                var filters = _filterSettings.GetCodeFilters();

                foreach (var f in filters.OrderBy(f => f.SortOrder).ThenBy(f => f.Name))
                {
                    try
                    {
                        items.Add(NavigationItem.CreateFilter(f.Name, f.Id));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CodeNavigationService.GetNavigationItems: Error adding filter '{f.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodeNavigationService.GetNavigationItems: Error: {ex.Message}");
            }
            return items;
        }
    }
}
