using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Models; 
using Pivot.Services;
using Pivot.Messages;
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

        public CodeEditorViewModel(CodeService codeService, IMessenger messenger)
        {
            _codeService = codeService;
            _messenger = messenger;
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
            }
        }

        private async Task LoadContentAsync(AssetEntity snippet)
        {
            try
            {
                if (File.Exists(snippet.FilePath))
                {
                    TextContent = await File.ReadAllTextAsync(snippet.FilePath);
                }
                else if (!string.IsNullOrEmpty(snippet.ContentIndex))
                {
                    TextContent = snippet.ContentIndex;
                }
                else
                {
                    TextContent = string.Empty;
                }
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
                // Parse Hashtags from content
                if (!string.IsNullOrEmpty(TextContent))
                {
                    // Regex for hashtags: #tag (alphanumeric+underscore)
                    // Simplified regex. 
                    var regex = new Regex(@"(?<!\w)#[a-zA-Z0-9_]+");
                    var matches = regex.Matches(TextContent);
                    foreach(Match match in matches)
                    {
                        var tag = match.Value.TrimStart('#');
                        if (!string.IsNullOrEmpty(tag) && !Tags.Contains(tag))
                        {
                            Tags.Add(tag);
                        }
                    }
                }

                // Sync Tags
                CurrentSnippet.UserTagsJson = JsonSerializer.Serialize(Tags);

                // Write to file
                await File.WriteAllTextAsync(CurrentSnippet.FilePath, TextContent);
                
                // Update Asset metadata
                CurrentSnippet.ContentIndex = TextContent.Length > 500 ? TextContent.Substring(0, 500) : TextContent;
                CurrentSnippet.FileSize = new FileInfo(CurrentSnippet.FilePath).Length;
                CurrentSnippet.UpdatedAt = DateTime.UtcNow;
                
                await _codeService.SaveSnippetAsync(CurrentSnippet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving content: {ex.Message}");
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
                ? SanitizeFileName(title) 
                : $"snippet_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            var extension = GetExtensionForLanguage(language);
            fileName = $"{fileName}{extension}";

            var snippet = new AssetEntity
            {
                FilePath = Path.Combine(GetCodeDirectory(), fileName),
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
                var dir = Path.GetDirectoryName(snippet.FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                await File.WriteAllTextAsync(snippet.FilePath, code);
            }
            catch {}

            await _codeService.SaveSnippetAsync(snippet);

            return snippet;
        }

        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Length > 50 ? name.Substring(0, 50) : name;
        }

        private string GetExtensionForLanguage(string language)
        {
            return language?.ToLowerInvariant() switch
            {
                "python" => ".py",
                "javascript" or "js" => ".js",
                "typescript" or "ts" => ".ts",
                "c#" or "csharp" => ".cs",
                "c++" or "cpp" => ".cpp",
                "c" => ".c",
                "go" => ".go",
                "rust" => ".rs",
                "sql" => ".sql",
                "shell" or "bash" => ".sh",
                "powershell" or "ps1" => ".ps1",
                "vex" => ".vex",
                "hlsl" => ".hlsl",
                "glsl" => ".glsl",
                "html" => ".html",
                "css" => ".css",
                "json" => ".json",
                "xml" => ".xml",
                "yaml" => ".yaml",
                "markdown" or "md" => ".md",
                "java" => ".java",
                "kotlin" => ".kt",
                "swift" => ".swift",
                "ruby" => ".rb",
                "php" => ".php",
                _ => ".txt"
            };
        }

        private string GetCodeDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Pivot", "Code");
        }
    }
}
