using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        /// Updates snippet content in memory immediately (UI updates when editor is closed).
        /// Schedules a debounced save to SQLite.
        /// </summary>
        public void UpdateContentImmediate(CodeFile snippet, string newContent)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[DEBUG] SnippetSyncService.UpdateContentImmediate: called, snippetId={snippet?.Id}, contentLength={newContent?.Length ?? 0}");
#endif
            if (snippet == null) return;

            // Update SelectedSnippet immediately
            snippet.Content = newContent ?? string.Empty;
            if (_viewModel != null)
            {
                _viewModel.IsDirty = true;

                // Update corresponding instance in Snippets collection for real-time card updates
                var snippetInCollection = _viewModel.Snippets.FirstOrDefault(s => s.Id == snippet.Id);
                if (snippetInCollection != null && !ReferenceEquals(snippetInCollection, snippet))
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SnippetSyncService.UpdateContentImmediate: updating snippetInCollection, sameInstance={ReferenceEquals(snippetInCollection, snippet)}");
#endif
                    snippetInCollection.Content = newContent ?? string.Empty;
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SnippetSyncService.UpdateContentImmediate: snippetInCollection not found or same instance");
#endif
                }
            }

            // Schedule debounced SQLite save
            ScheduleDebouncedSave(snippet);
        }

        /// <summary>
        /// Updates snippet title in memory immediately (UI updates when editor is closed).
        /// Schedules a debounced save to SQLite.
        /// </summary>
        public void UpdateTitleImmediate(CodeFile snippet, string newTitle)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[DEBUG] SnippetSyncService.UpdateTitleImmediate: called, snippetId={snippet?.Id}, newTitle='{newTitle}'");
#endif
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
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SnippetSyncService.UpdateTitleImmediate: updating snippetInCollection, sameInstance={ReferenceEquals(snippetInCollection, snippet)}");
#endif
                    snippetInCollection.Title = newTitle;
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] SnippetSyncService.UpdateTitleImmediate: snippetInCollection not found or same instance");
#endif
                }
            }

            // Schedule debounced SQLite save
            ScheduleDebouncedSave(snippet);
        }

        /// <summary>
        /// Triggers UI update for a snippet in the ListView (called when editor is closed).
        /// Updates silently by disabling animations temporarily, then refreshing ItemsSource.
        /// </summary>
        public void TriggerUiUpdate(CodeFile snippet)
        {
            if (snippet == null || _viewModel == null) return;

            var dispatcher = App.Current.MainWindow?.DispatcherQueue;
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        // Update the snippet in collection to ensure properties are synced
                        var snippetInCollection = _viewModel.Snippets.FirstOrDefault(s => s.Id == snippet.Id);
                        if (snippetInCollection != null && !ReferenceEquals(snippetInCollection, snippet))
                        {
                            // Copy updated values to collection instance
                            snippetInCollection.Title = snippet.Title;
                            snippetInCollection.Content = snippet.Content;
                            snippetInCollection.Updated = snippet.Updated;
                            snippetInCollection.Tags = snippet.Tags;
                        }
                        
                        // Force UI refresh by temporarily clearing and restoring ItemsSource
                        // Disable animations to make update less noticeable
                        var listView = FindListView();
                        if (listView != null)
                        {
                            // Temporarily disable transitions to prevent animation
                            var originalTransitions = listView.ItemContainerTransitions;
                            listView.ItemContainerTransitions = null;
                            
                            try
                            {
                                var currentSource = listView.ItemsSource;
                                listView.ItemsSource = null;
                                listView.ItemsSource = currentSource;
                            }
                            finally
                            {
                                // Restore transitions after update
                                listView.ItemContainerTransitions = originalTransitions;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SnippetSyncService.TriggerUiUpdate: Error triggering UI update: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Finds the ListView control in the UI tree.
        /// </summary>
        private ListView? FindListView()
        {
            try
            {
                var mainWindow = App.Current.MainWindow;
                if (mainWindow == null) return null;

                // Try to find ListView by name
                var content = mainWindow.Content as FrameworkElement;
                if (content == null) return null;

                return FindListViewRecursive(content, "SnippetListView");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recursively searches for a ListView with the specified name.
        /// </summary>
        private ListView? FindListViewRecursive(DependencyObject parent, string name)
        {
            try
            {
                if (parent is ListView listView && listView.Name == name)
                {
                    return listView;
                }

                var count = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is DependencyObject depObj)
                    {
                        var result = FindListViewRecursive(depObj, name);
                        if (result != null) return result;
                    }
                }
            }
            catch { }
            return null;
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

