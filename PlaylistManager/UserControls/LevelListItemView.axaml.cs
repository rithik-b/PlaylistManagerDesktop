using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using PlaylistManager.Models;
using PlaylistManager.Utilities;
using PlaylistManager.Windows;
using ReactiveUI;
using Splat;

namespace PlaylistManager.UserControls
{
    public class LevelListItemView : UserControl
    {
        private const string kRabbitPreviewerIHardlyKnowHer = "https://skystudioapps.com/bs-viewer/?id=";
        
        public LevelListItemView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void PreviewClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is LevelListItemViewModel {Key: { }} viewModel)
            {
                Utils.OpenBrowser(kRabbitPreviewerIHardlyKnowHer + viewModel.Key);
            }
        }
    }

    public class LevelListItemViewModel : ViewModelBase
    {
        private readonly PlaylistSongWrapper playlistSong;
        private Bitmap? coverImage;
        private CoverImageLoader? coverImageLoader;
        private string? selectedCharacteristic;

        public LevelListItemViewModel(PlaylistSongWrapper playlistSong)
        {
            this.playlistSong = playlistSong;

            foreach (var characteristic in playlistSong.customLevelData.Difficulties.Keys)
            {
                Characteristics.Add(characteristic);
            }

            selectedCharacteristic = Characteristics.FirstOrDefault();
        }

        public string SongName => $"{playlistSong.customLevelData.SongName} {playlistSong.customLevelData.SongSubName}";
        public string AuthorName => $"{playlistSong.customLevelData.SongAuthorName} [{playlistSong.customLevelData.LevelAuthorName}]";
        public string? Key => playlistSong.customLevelData.Key;
        public List<string> Characteristics { get; } = new();
        public Bitmap? CoverImage
        {
            get
            {
                if (coverImage != null)
                {
                    return coverImage;
                }
                coverImageLoader ??= Locator.Current.GetService<CoverImageLoader>();
                _ = LoadCoverAsync();
                return coverImageLoader?.LoadingImage;
            }
            private set
            {
                coverImage = value;
                NotifyPropertyChanged();
            }
        }

        public string? SelectedCharacteristic
        {
            get => selectedCharacteristic;
            set
            {
                selectedCharacteristic = value;
                NotifyPropertyChanged();
            }
        }

        private async Task LoadCoverAsync()
        {
            var bitmap = await playlistSong.customLevelData.GetCoverImageAsync();
            if (bitmap != null)
            {
                RxApp.MainThreadScheduler.Schedule(() => CoverImage = bitmap);
            }
        }
    }
}