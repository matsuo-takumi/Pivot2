using CommunityToolkit.Mvvm.ComponentModel;

namespace Pivot.CodeModule.Models
{
    public class TagItem : ObservableObject
    {
        private int _id;
        private string _name = string.Empty;
        private bool _isSelected;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TagItem()
        {
        }

        public TagItem(string name)
        {
            Name = name;
        }
    }
}
