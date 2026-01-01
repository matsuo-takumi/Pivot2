using System;
using CommunityToolkit.Mvvm.Messaging;
using Pivot.Messages;
using Pivot.Models;

namespace Pivot.Services
{
    /// <summary>
    /// INavigationServiceの実装。
    /// MainWindowからナビゲーションハンドラを受け取り、メッセージ経由でのナビゲーションを仲介する。
    /// </summary>
    public class NavigationService : INavigationService, IRecipient<NavigationRequestMessage>
    {
        private readonly IMessenger _messenger;
        private Action<NavigationRegion>? _navigationHandler;

        public NavigationService(IMessenger messenger)
        {
            _messenger = messenger;
            _messenger.Register(this);
        }

        public void RegisterNavigationHandler(Action<NavigationRegion> handler)
        {
            _navigationHandler = handler;
        }

        public void NavigateTo(NavigationRegion region)
        {
            _navigationHandler?.Invoke(region);
        }

        public void Receive(NavigationRequestMessage message)
        {
            NavigateTo(message.Value);
        }
    }
}

