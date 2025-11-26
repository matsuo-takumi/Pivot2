using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Pivot;
using Pivot.CodeModule.Models;
using Pivot.CodeModule.ViewModels;

namespace Pivot.CodeModule.Services
{
    /// <summary>
    /// Hybrid snippet synchronization service that handles:
    /// 1. Immediate in-memory UI updates (real-time)
    /// 2. Throttled asynchronous SQLite persistence (background)
    /// </summary>
    public class SnippetSyncService
    {
        private readonly ICodeRepository? _repository;
        private readonly CodeViewModel? _viewModel;
        private readonly Dictionary<Guid, CancellationTokenSource> _pendingSaves = new();
        private readonly object _saveLock = new object();
        private const int SaveDebounceMs = 1000; // Wait 1 second after last edit before saving

        public SnippetSyncService(ICodeRepository? repository, CodeViewModel? viewModel)
        {
            _repository = repository;
            _viewModel = viewModel;
        }

        /// <summary>
        /// Updates snippet content in memory immediately (UI updates instantly).
        /// Schedules a debounced save to SQLite.
        /// </summary>
        public void UpdateContentImmediate(CodeFile snippet, string newContent)
        {
            if (snippet == null) return;

            // Update SelectedSnippet immediately
            snippet.Content = newContent;
            if (_viewModel != null)
            {
                _viewModel.IsDirty = true;

                // Update corresponding instance in Snippets collection for real-time card updates
                var snippetInCollection = _viewModel.Snippets.FirstOrDefault(s => s.Id == snippet.Id);
                if (snippetInCollection != null && !ReferenceEquals(snippetInCollection, snippet))
                {
                    snippetInCollection.Content = newContent;
                }
            }

            // Schedule debounced SQLite save
            ScheduleDebouncedSave(snippet);
        }

        /// <summary>
        /// Updates snippet title in memory immediately (UI updates instantly).
        /// Schedules a debounced save to SQLite.
        /// </summary>
        public void UpdateTitleImmediate(CodeFile snippet, string newTitle)
        {
            if (snippet == null) return;

            // Update SelectedSnippet immediately
            snippet.Title = newTitle;
            if (_viewModel != null)
            {
                _viewModel.IsDirty = true;

                // Update corresponding instance in Snippets collection for real-time card updates
                var snippetInCollection = _viewModel.Snippets.FirstOrDefault(s => s.Id == snippet.Id);
                if (snippetInCollection != null && !ReferenceEquals(snippetInCollection, snippet))
                {
                    snippetInCollection.Title = newTitle;
                }
            }

            // Schedule debounced SQLite save
            ScheduleDebouncedSave(snippet);
        }

        /// <summary>
        /// Schedules a debounced save to SQLite. If a save is already pending for this snippet,
        /// cancels it and schedules a new one.
        /// </summary>
        private void ScheduleDebouncedSave(CodeFile snippet)
        {
            if (_repository == null) return;

            lock (_saveLock)
            {
                // Cancel existing pending save for this snippet
                if (_pendingSaves.TryGetValue(snippet.Id, out var existingCts))
                {
                    try
                    {
                        existingCts.Cancel();
                        existingCts.Dispose();
                    }
                    catch { }
                    _pendingSaves.Remove(snippet.Id);
                }

                // Create new cancellation token source for debounced save
                var cts = new CancellationTokenSource();
                _pendingSaves[snippet.Id] = cts;

                // Schedule save after debounce delay
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(SaveDebounceMs, cts.Token);
                        
                        // Check if cancellation was requested
                        if (cts.Token.IsCancellationRequested) return;

                        // Perform SQLite save
                        lock (_saveLock)
                        {
                            if (_pendingSaves.ContainsKey(snippet.Id) && _pendingSaves[snippet.Id] == cts)
                            {
                                _pendingSaves.Remove(snippet.Id);
                            }
                        }

                        // Save to SQLite (synchronous operation)
                        _repository.Save(snippet);

                        // Update _allSnippets in ViewModel if needed
                        if (_viewModel != null)
                        {
                            App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                            {
                                try
                                {
                                    // Refresh to sync with database
                                    _viewModel.Refresh();
                                }
                                catch { }
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Save was cancelled, ignore
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SnippetSyncService: Error in debounced save: {ex.Message}");
                    }
                    finally
                    {
                        try { cts.Dispose(); } catch { }
                    }
                });
            }
        }

        /// <summary>
        /// Forces immediate save to SQLite (used when closing editor or explicit save).
        /// Cancels any pending debounced saves for this snippet.
        /// </summary>
        public void SaveImmediate(CodeFile snippet)
        {
            if (snippet == null || _repository == null) return;

            lock (_saveLock)
            {
                // Cancel pending debounced save
                if (_pendingSaves.TryGetValue(snippet.Id, out var cts))
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch { }
                    _pendingSaves.Remove(snippet.Id);
                }
            }

            // Save immediately
            try
            {
                _repository.Save(snippet);
                
                // Refresh ViewModel
                if (_viewModel != null)
                {
                    App.Current.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        try
                        {
                            _viewModel.Refresh();
                            _viewModel.IsDirty = false;
                        }
                        catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SnippetSyncService: Error in immediate save: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancels all pending saves (used during cleanup).
        /// </summary>
        public void CancelAllPendingSaves()
        {
            lock (_saveLock)
            {
                foreach (var cts in _pendingSaves.Values)
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch { }
                }
                _pendingSaves.Clear();
            }
        }
    }
}

