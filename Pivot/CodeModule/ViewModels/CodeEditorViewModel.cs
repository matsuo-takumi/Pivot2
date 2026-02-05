using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Models; 
using Pivot.Services;
using Pivot.Messages;
using Pivot.CodeModule.Helpers;
using Pivot.CodeModule.Services;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Pivot.CodeModule.ViewModels
{
    public partial class CodeEditorViewModel : ObservableObject
    {
        private readonly CodeService _codeService;
        private readonly IMessenger _messenger;
        private readonly CodeSettingsService _settingsService;

        [ObservableProperty]
        private AssetEntity? _currentSnippet;

        [ObservableProperty]
        private string _textContent = string.Empty;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private ObservableCollection<string> _tags = new();

        [ObservableProperty]
        private ObservableCollection<string> _availableTags = new();

        [ObservableProperty]
        private string _currentLanguage = "plaintext";

        // Color Settings
        [ObservableProperty] private Brush? _editorTextBrush;
        [ObservableProperty] private Brush? _editorBackgroundBrush;
        [ObservableProperty] private Brush? _titleBrush;
        [ObservableProperty] private Brush? _tagTextBrush;
        [ObservableProperty] private Brush? _tagBackgroundBrush;
        [ObservableProperty] private Brush? _tagBorderBrush;
        [ObservableProperty] private Brush? _lineNumberBrush;

        public CodeEditorViewModel(
            CodeService codeService, 
            CodeSettingsService settingsService,
            IMessenger messenger)
        {
            _codeService = codeService;
            _settingsService = settingsService;
            _messenger = messenger;

            // Initialize colors and listen to changes
            UpdateColors();
            _settingsService.SettingsChanged += (s, e) => UpdateColors();
        }

        private void UpdateColors()
        {
            EditorTextBrush = _settingsService.GetEditorTextBrush();
            EditorBackgroundBrush = _settingsService.GetEditorBackgroundBrush();
            TitleBrush = _settingsService.GetTitleBrush();
            TagTextBrush = _settingsService.GetTagTextBrush();
            TagBackgroundBrush = _settingsService.GetTagBackgroundBrush();
            TagBorderBrush = _settingsService.GetTagBorderBrush();
            LineNumberBrush = _settingsService.GetLineNumberBrush();
        }

        public void SetAvailableTags(IEnumerable<string> tags)
        {
            AvailableTags.Clear();
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    AvailableTags.Add(tag);
                }
            }
        }

        public async Task SetSnippetAsync(AssetEntity? snippet)
        {
            CurrentSnippet = snippet;
            IsEditing = snippet != null;
            Tags.Clear();
            
            if (snippet != null)
            {
                await LoadContentAsync(snippet);
                
                // Set language for syntax highlighting
                CurrentLanguage = CodeFileHelper.GetLanguageFromExtension(snippet.FilePath);
                
                // Load tags
                if (!string.IsNullOrEmpty(snippet.UserTagsJson))
                {
                    try 
                    {
                        var tags = JsonSerializer.Deserialize<string[]>(snippet.UserTagsJson);
                        if (tags != null)
                        {
                            foreach(var tag in tags) Tags.Add(tag);
                        }
                        System.Diagnostics.Debug.WriteLine($"[CodeEditor] Loaded {Tags.Count} tags from DB: {string.Join(", ", Tags)}");
                    }
                    catch 
                    {
                         // Ignore Json error
                    }
                }
            }
            else
            {
                TextContent = string.Empty;
                CurrentLanguage = "plaintext";
            }
        }

        private async Task LoadContentAsync(AssetEntity snippet)
        {
            try
            {
                TextContent = await _codeService.ReadContentAsync(snippet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading content: {ex.Message}");
                TextContent = "Error loading content.";
            }
        }

        public Task CloseEditorAsync() 
        {
            // Close without saving - user must explicitly save
            CloseEditor();
            return Task.CompletedTask;
        }

        public void CloseEditor()
        {
            CurrentSnippet = null;
            IsEditing = false;
            TextContent = string.Empty;
            Tags.Clear();
        }

        public async Task SaveContentAsync()
        {
            if (CurrentSnippet == null) return;

            try
            {
                // Tags are managed manually by the user through the UI
                // No automatic hashtag extraction to avoid overriding user's tag deletions

                // Sync Tags
                CurrentSnippet.UserTagsJson = JsonSerializer.Serialize(Tags);
                System.Diagnostics.Debug.WriteLine($"[CodeEditor] Saving {Tags.Count} tags to DB: {string.Join(", ", Tags)}");
                System.Diagnostics.Debug.WriteLine($"[CodeEditor] UserTagsJson: {CurrentSnippet.UserTagsJson}");

                // Write to file using CodeService
                await _codeService.WriteContentAsync(CurrentSnippet, TextContent);
                
                // Save metadata to database
                await _codeService.SaveSnippetAsync(CurrentSnippet, saveToDisk: false);
                
                // Notify changes (Updated)
                _messenger.Send(new AssetEntityChangedMessage(
                    CurrentSnippet, 
                    CurrentSnippet.FilePath, 
                    AssetEntityChangedMessage.ChangeType.Updated));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodeEditor] Error saving content: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (CurrentSnippet == null) return;

            try
            {
                // Delete from DB and file (CodeService handles both)
                await _codeService.DeleteSnippetAsync(CurrentSnippet);

                // Notify list to remove item
                _messenger.Send(new AssetEntityChangedMessage(CurrentSnippet, CurrentSnippet.FilePath, AssetEntityChangedMessage.ChangeType.Deleted));

                CloseEditor();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting snippet: {ex.Message}");
            }
        }

        public async Task<AssetEntity?> CreateSnippetAsync(string title, string language, string code, string[] tags)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            var fileName = !string.IsNullOrWhiteSpace(title) 
                ? CodeFileHelper.SanitizeFileName(title) 
                : $"snippet_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            var extension = CodeFileHelper.GetExtensionForLanguage(language);
            fileName = $"{fileName}{extension}";

            var snippet = new AssetEntity
            {
                FilePath = Path.Combine(CodeFileHelper.GetCodeDirectory(), fileName),
                FileName = fileName,
                Kind = AssetKind.Code,
                Tool = language,
                ContentIndex = code.Length > 500 ? code.Substring(0, 500) : code,
                UserTagsJson = JsonSerializer.Serialize(tags),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await _codeService.WriteContentAsync(snippet, code);
            }
            catch {}

            await _codeService.SaveSnippetAsync(snippet, saveToDisk: false);

            return snippet;
        }


    }
}
