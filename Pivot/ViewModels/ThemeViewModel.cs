using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Pivot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging; // IMessengerを使用するために追加
using Pivot.Messages; // BackdropTypeChangedMessageを使用するために追加
using Pivot.Messages; // ThemeChangedMessageを使用するために追加

namespace Pivot.ViewModels
{
    public partial class ThemeViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ElementTheme _currentTheme;

        [ObservableProperty]
        private MainWindow.BackdropType _selectedBackdropType;

        public ThemeViewModel(SettingsService settingsService, IMessenger messenger)
        {
            _settingsService = settingsService;
            _messenger = messenger;

            CurrentTheme = _settingsService.GetTheme();
            SelectedBackdropType = _settingsService.GetBackdropType();

            // テーマが変更されたときに保存
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CurrentTheme))
                {
                    _settingsService.SetTheme(CurrentTheme);
                    _messenger.Send(new ThemeChangedMessage(CurrentTheme));
                }
                if (e.PropertyName == nameof(SelectedBackdropType))
                {
                    _settingsService.SetBackdropType(SelectedBackdropType);
                    _messenger.Send(new BackdropTypeChangedMessage(SelectedBackdropType));
                }
            };
        }
    }
}
