using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Pivot.Models;

namespace Pivot.Messages
{
    public class CodeFiltersUpdatedMessage : ValueChangedMessage<List<CustomFilter>>
    {
        public CodeFiltersUpdatedMessage(List<CustomFilter> filters)
            : base(filters ?? new List<CustomFilter>())
        {
        }
    }
}

