using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Pivot.Engine.Models;
using Pivot.Models;

namespace Pivot.ViewModels
{
	public partial class FilterViewModel : ObservableObject
	{
		public Guid Id { get; }
		[ObservableProperty]
		private string name = string.Empty;
		[ObservableProperty]
		private List<string> allowedExtensions = new List<string>();
		[ObservableProperty]
		private bool isBuiltIn = false;
		[ObservableProperty]
		private bool isAll = false;
		[ObservableProperty]
		private bool isSelected = false;

		public string DisplayString => AllowedExtensions != null && AllowedExtensions.Count > 0 ? string.Join(", ", AllowedExtensions) : "(any)";

		public FilterViewModel(CustomFilter model)
		{
			if (model == null) throw new ArgumentNullException(nameof(model));
			Id = model.Id;
			Name = model.Name ?? string.Empty;
			AllowedExtensions = model.AllowedExtensions ?? new List<string>();
			IsBuiltIn = model.IsBuiltIn;
			IsAll = model.IsAll;
		}
	}
}
