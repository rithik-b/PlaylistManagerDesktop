using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using PlaylistManager.Models;
using PlaylistManager.Utilities;
using Splat;

namespace PlaylistManager.Views
{
    public class SettingsView : UserControl
    {
        private readonly ViewModel viewModel;
        
        public SettingsView()
        {
            AvaloniaXamlLoader.Load(this);
            viewModel = new ViewModel();
            DataContext = viewModel;
        }
        
        private void LoseFocus(object? sender, PointerPressedEventArgs e) => Focus();

        public class ViewModel : ViewModelBase
        {
            private readonly ConfigModel configModel;

            private LevelLoader? levelLoader;
            private LevelLoader LevelLoader => levelLoader ??= Locator.Current.GetService<LevelLoader>()!;

            private PlaylistLibUtils? playlistLibUtils;
            private PlaylistLibUtils PlaylistLibUtils => playlistLibUtils ??= Locator.Current.GetService<PlaylistLibUtils>()!;
            
            private PlaylistsListView? playlistsListView;
            private PlaylistsListView PlaylistsListView => playlistsListView ??= Locator.Current.GetService<PlaylistsListView>()!;
            
            public ViewModel()
            {
                configModel = Locator.Current.GetService<ConfigModel>()!;
            }

            private string BeatSaberDir
            {
                get => configModel.BeatSaberDir;
                set
                {
                    configModel.BeatSaberDir = value;
                    NotifyPropertyChanged();
                }
            }

            private Bitmap? CoverImage
            {
                get => configModel.coverImage;
                set
                {
                    configModel.coverImage = value;
                    NotifyPropertyChanged();
                }
            }

            private string AuthorName
            {
                get => configModel.AuthorName;
                set
                {
                    configModel.AuthorName = value;
                    NotifyPropertyChanged();
                }
            }

            private bool refreshingLevels;

            private bool RefreshingLevels
            {
                get => refreshingLevels;
                set
                {
                    refreshingLevels = value;
                    NotifyPropertyChanged();
                }
            }

            private async void RefreshLevels()
            {
                if (!RefreshingLevels)
                {
                    RefreshingLevels = true;
                    await LevelLoader.GetCustomLevelsAsync(true);
                    await PlaylistLibUtils.RefreshPlaylistsAsync(PlaylistLibUtils.PlaylistManager, true);
                    PlaylistsListView.viewModel.CurrentManager = PlaylistLibUtils.PlaylistManager;
                    RefreshingLevels = false;
                }
            }
        }
    }
}