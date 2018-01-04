﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sdl.Community.StudioCleanupTool.Annotations;
using Sdl.Community.StudioCleanupTool.Model;

namespace Sdl.Community.StudioCleanupTool.ViewModel
{
	public class MainWindowViewModel:INotifyPropertyChanged
	{
		private ObservableCollection<StudioVersion> _studioVersionsCollection;
		public event PropertyChangedEventHandler PropertyChanged;

		public MainWindowViewModel()
		{
			FillStudioVersionList();
		}

		private void FillStudioVersionList()
		{
			_studioVersionsCollection = new ObservableCollection<StudioVersion>
			{
				new StudioVersion
				{
					DisplayName = "Studio 2014",
					IsSelected = false,
					//FullVersionNumber = "11.0.0.0",
					//VersionNumber = "11" //need to check version number
				},
				new StudioVersion
				{
					DisplayName = "Studio 2015",
					IsSelected = false,
					//FullVersionNumber = "12.0.0.0",
				},
				new StudioVersion
				{
					DisplayName = "Studio 2017",
					IsSelected = false
				}
			};
		}

		public ObservableCollection<StudioVersion> StudioVersionsCollection
		{
			get
			{
				return _studioVersionsCollection;
			}

			set
			{
				if (Equals(value, _studioVersionsCollection))
				{
					return;
				}
				OnPropertyChanged(nameof(StudioVersionsCollection));
			}
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}