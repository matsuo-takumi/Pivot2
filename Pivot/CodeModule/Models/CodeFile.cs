using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Pivot.CodeModule.Models
{
    public class CodeFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private Guid _id = Guid.NewGuid();
        private string _title = string.Empty;
        private string _language = string.Empty;
        private string _tool = string.Empty;
        private string _tags = string.Empty;
        private string _content = string.Empty;
        private DateTime _updated = DateTime.Now;
        private bool _isDeleted = false;
        private DateTime? _deletedAt = null;

        [Key]
        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Language
        {
            get => _language;
            set => SetProperty(ref _language, value);
        }

        public string Tool
        {
            get => _tool;
            set => SetProperty(ref _tool, value);
        }

        public string Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public DateTime Updated
        {
            get => _updated;
            set => SetProperty(ref _updated, value);
        }

        // Soft-delete support: when true the snippet is considered in Trash and can be restored within 30 days.
        public bool IsDeleted
        {
            get => _isDeleted;
            set => SetProperty(ref _isDeleted, value);
        }

        // Timestamp when item was moved to Trash (UTC). Null when not deleted.
        public DateTime? DeletedAt
        {
            get => _deletedAt;
            set => SetProperty(ref _deletedAt, value);
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName!));
        }
    }
}


