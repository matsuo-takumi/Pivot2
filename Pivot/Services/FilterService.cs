using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Pivot.ViewModels;
using Pivot.Models;

namespace Pivot.Services
{
	public class FilterService : ObservableObject
	{
		private readonly SettingsService _settings;
		public ObservableCollection<FilterViewModel> Filters { get; } = new ObservableCollection<FilterViewModel>();

		// multi-select: selected filter IDs (per-tab selection persisted via SettingsService)
		private HashSet<Guid> _selectedFilterIds = new HashSet<Guid>();
		public IReadOnlyCollection<Guid> SelectedFilterIds => _selectedFilterIds;

		public FilterService(SettingsService settings)
		{
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));
			LoadFilters();
			// restore selected filters for Asset tab if available
            try
            {
                // Intentionally do NOT restore previously selected filters on startup.
                // Start with an empty selection so no filters are active until the user chooses.
                _selectedFilterIds = new HashSet<Guid>();
                OnPropertyChanged(nameof(SelectedFilterIds));
                UpdateFilterSelections();
            }
            catch { }
		}

		public void LoadFilters()
		{
			Filters.Clear();
			try
			{
				var user = _settings.GetUserSettings();
				if (user?.AssetFilters != null)
				{
					foreach (var f in user.AssetFilters)
					{
						Filters.Add(new FilterViewModel(f));
					}
				}
				// keep existing SelectedFilterIds unchanged here; consumer will ApplyFilterFromService after construction
				UpdateFilterSelections();
			}
			catch { }
		}

        private static string NormalizeExtension(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            try
            {
                // if it's a URI, strip query and fragment
                if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
                {
                    var path = uri.AbsolutePath; // /path/to/file.ext
                    var ext = System.IO.Path.GetExtension(path);
                    return string.IsNullOrEmpty(ext) ? string.Empty : ext.ToLowerInvariant();
                }
                // otherwise, remove query if present
                var qIdx = input.IndexOf('?');
                var clean = qIdx >= 0 ? input.Substring(0, qIdx) : input;
                // take last segment after '/'
                var lastSlash = clean.LastIndexOf('/');
                var last = lastSlash >= 0 ? clean.Substring(lastSlash + 1) : clean;
                var ext2 = System.IO.Path.GetExtension(last);
                return string.IsNullOrEmpty(ext2) ? string.Empty : ext2.ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

		public IEnumerable<TemplateItem> ApplyFilter(IEnumerable<TemplateItem> source)
		{
			if (source == null) return Enumerable.Empty<TemplateItem>();
			if (_selectedFilterIds == null || _selectedFilterIds.Count == 0) return source;

			var selected = Filters.Where(f => _selectedFilterIds.Contains(f.Id)).ToList();
			if (selected.Count == 0) return source;

			// if any selected filter is an 'All' filter, return unfiltered source
			if (selected.Any(s => s.IsAll)) return source;

			var known = Filters.SelectMany(f => f.AllowedExtensions ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.ToLowerInvariant()).Distinct().ToHashSet();
			var allowedSet = selected.SelectMany(f => f.AllowedExtensions ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.ToLowerInvariant()).Distinct().ToHashSet();
			var includeOther = selected.Any(f => f.AllowedExtensions == null || f.AllowedExtensions.Count == 0);

			return source.Where(a =>
			{
				var ext = NormalizeExtension(a.Path ?? a.ThumbnailPath ?? a.Name);
				if (string.IsNullOrEmpty(ext)) return includeOther;
				if (allowedSet.Contains(ext)) return true;
				if (includeOther && !known.Contains(ext)) return true;
				return false;
			});
		}

		// selection manipulation
		public void SetSelectedFilters(IEnumerable<Guid> ids)
		{
			_selectedFilterIds = ids != null ? new HashSet<Guid>(ids) : new HashSet<Guid>();
			OnPropertyChanged(nameof(SelectedFilterIds));
			UpdateFilterSelections();
		}

		public void AddSelectedFilter(Guid id)
		{
			if (!_selectedFilterIds.Contains(id)) { _selectedFilterIds.Add(id); OnPropertyChanged(nameof(SelectedFilterIds)); }
			UpdateFilterSelections();
		}

		public void RemoveSelectedFilter(Guid id)
		{
			if (_selectedFilterIds.Contains(id)) { _selectedFilterIds.Remove(id); OnPropertyChanged(nameof(SelectedFilterIds)); }
			UpdateFilterSelections();
		}

		public void ClearSelectedFilters()
		{
			_selectedFilterIds.Clear();
			OnPropertyChanged(nameof(SelectedFilterIds));
			UpdateFilterSelections();
		}

		private void UpdateFilterSelections()
		{
			try
			{
				foreach (var f in Filters)
				{
					f.IsSelected = _selectedFilterIds.Contains(f.Id);
				}
			}
			catch { }
		}
	}
}
