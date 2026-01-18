using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace Pivot.CodeModule.Controls
{
    public sealed partial class VariableInputDialog : ContentDialog
    {
        public List<TemplateVariable> Variables { get; }

        public VariableInputDialog(List<TemplateVariable> variables)
        {
            this.InitializeComponent();
            Variables = variables;
            VariablesList.ItemsSource = Variables;
        }
    }

    public class TemplateVariable
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
