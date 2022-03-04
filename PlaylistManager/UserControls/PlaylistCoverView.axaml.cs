using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Aura.UI.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using BeatSaberPlaylistsLib.Types;
using PlaylistManager.Clipboard;
using PlaylistManager.Models;
using PlaylistManager.Utilities;
using PlaylistManager.Views;
using PlaylistManager.Windows;
using ReactiveUI;
using Splat;

namespace PlaylistManager.UserControls
{
    public class PlaylistCoverView : UserControl
    {
        private PlaylistsListView? playlistsListView;
        public PlaylistsListView PlaylistsListView => playlistsListView ??= Locator.Current.GetService<PlaylistsListView>()!;
        
        private TextBox? renameBox;
        public TextBox RenameBox => renameBox ??= this.Find<TextBox>("RenameBox");

        public PlaylistCoverView()
        {
            AvaloniaXamlLoader.Load(this);
            AddHandler(DragDrop.DragOverEvent, DragOver!);
            AddHandler(DragDrop.DropEvent, Drop!);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is PlaylistCoverViewModel viewModel)
            {
                viewModel.SetParentControl(this);
            }
        }

        #region Drag and Drop
        
        public const string kPlaylistData = "application/com.rithik-b.PlaylistManager.Playlist";
        private bool pointerHeld;
        
        private async void DoDrag(object sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            pointerHeld = true;

            await Task.Delay(Utils.kHoldDelay);
            if (!pointerHeld)
            {
                return;
            }
            
            if (DataContext is PlaylistCoverViewModel {isPlaylist: true, playlist:{}} coverViewModel
                && PlaylistsListView.viewModel.CurrentManager != null)
            {
                var playlistPath = coverViewModel.playlist.GetPlaylistPath(PlaylistsListView.viewModel.CurrentManager);
                var dragData = new DataObject();
                dragData.Set(kPlaylistData, coverViewModel.playlist);
                dragData.Set(DataFormats.FileNames, new string[1]
                {
                    playlistPath
                });
                
                // Need to keep file name as it will change when moving
                var oldFileName = coverViewModel.playlist.Filename;

                var result = await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Link |
                                                                    DragDropEffects.Move | 
                                                                    DragDropEffects.Copy |
                                                                    DragDropEffects.None);
                if (result == DragDropEffects.Move)
                {
                    var newFileName = coverViewModel.playlist.Filename;
                    coverViewModel.playlist.Filename = oldFileName;
                    coverViewModel.Delete(false);
                    coverViewModel.playlist.Filename = newFileName;
                }
            }
        }
        
        // Tracks if pointer is released to prevent a drag operation
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => pointerHeld = false;

        private void DragOver(object sender, DragEventArgs e)
        {
            // If playlist is dragged (from within app) only move to other subfolders
            if (DataContext is PlaylistCoverViewModel current && !current.isPlaylist &&
                current is {playlistManager: { }} && e.Data.Contains(kPlaylistData))
            {
                e.DragEffects = DragDropEffects.Move;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }
        
        private void Drop(object sender, DragEventArgs e)
        {
            if (DataContext is PlaylistCoverViewModel {isPlaylist: false, playlistManager: {}} current
                && e.Data.Get(kPlaylistData) is IPlaylist drag)
            {
                PlaylistLibUtils.OnPlaylistMove(drag, current.playlistManager);
                _ = current.LoadPlaylistsAsync();
                e.DragEffects = DragDropEffects.Move;
            }
        }
        #endregion

        #region Context Menu

        private void OpenClick(object? sender, RoutedEventArgs e) => PlaylistsListView.OpenSelectedPlaylistOrManager();

        private async void CutClick(object? sender, RoutedEventArgs? e)
        {
            if (DataContext is PlaylistCoverViewModel viewModel)
            {
                await viewModel.Cut();
            }
        }

        private async void CopyClick(object? sender, RoutedEventArgs? e)
        {
            if (DataContext is PlaylistCoverViewModel viewModel)
            {
                await viewModel.Copy();
            }
        }
        
        private void RenameClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is PlaylistCoverViewModel viewModel)
            {
                viewModel.IsRenaming = true;
            }
        }

        private void RenameKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is PlaylistCoverViewModel viewModel)
            {
                viewModel.IsRenaming = false;
            }
        }

        private void DeleteClick(object? sender, RoutedEventArgs? e)
        {
            if (DataContext is PlaylistCoverViewModel viewModel)
            {
                viewModel.Delete();
            }
        }

        #endregion
    }
    
    /*
     * I really really wanted to use abstraction and inheritance for this but because of the ViewLocator (I Hardly Know
     * Her!) I can't. Maybe someday I'll figure this stuff out 😔
     */
    public class PlaylistCoverViewModel : ViewModelBase
    {
        public readonly IPlaylist? playlist;
        public readonly BeatSaberPlaylistsLib.PlaylistManager? playlistManager;
        public readonly bool isPlaylist;
        private bool popupShowing;
        private PlaylistCoverView? control;
        private CoverImageLoader? coverImageLoader;
        private PlaylistLibUtils? playlistLibUtils;
        private int? numPlaylists;
        private Bitmap? coverImage;
        
        private PlaylistsListView? playlistsListView;
        private PlaylistsListView PlaylistsListView =>
            playlistsListView ??= Locator.Current.GetService<PlaylistsListView>()!;

        private IClipboardHandler? clipboardHandler;
        private IClipboardHandler ClipboardHandler =>
            clipboardHandler ??= Locator.Current.GetService<IClipboardHandler>()!;
        
        private MainWindow? mainWindow;
        private MainWindow MainWindow => mainWindow ??= Locator.Current.GetService<MainWindow>()!;

        public PlaylistCoverViewModel(IPlaylist playlist)
        {
            this.playlist = playlist;
            isPlaylist = true;
        }
        
        public PlaylistCoverViewModel(BeatSaberPlaylistsLib.PlaylistManager playlistManager)
        {
            this.playlistManager = playlistManager;
            isPlaylist = false;
        }
        
        private bool AllowDrop => !isPlaylist;
        private bool IsPlaylist => isPlaylist;
        
        public string Title
        {
            get
            {
                if (isPlaylist)
                {
                    return playlist?.Title ?? "";
                }
                return Path.GetFileName(playlistManager?.PlaylistPath) ?? "";
            }
            set
            {
                if (isPlaylist && playlist != null && PlaylistsListView.viewModel.CurrentManager != null)
                {
                    File.Delete(playlist.GetPlaylistPath(PlaylistsListView.viewModel.CurrentManager));
                    playlist.Filename = "";
                    playlist.Title = value;
                    PlaylistsListView.viewModel.CurrentManager.StorePlaylist(playlist);
                }
                else if (!isPlaylist && playlistManager != null)
                {
                    var input = value;
                    foreach (var c in Path.GetInvalidFileNameChars())
                    {
                        input = input.Replace($"{c}", "");
                    }
                    if (Path.GetFileName(playlistManager.PlaylistPath) != input)
                    {
                        var oldName = Path.GetFileName(playlistManager.PlaylistPath);
                        try
                        {
                            playlistManager.RenameManager(input);
                        }
                        catch (Exception e)
                        {
                            MainWindow.NewMessageDialog("Rename Error", e.Message, null, null);
                            RenameTitle = oldName;
                        }
                    }
                }
                NotifyPropertyChanged();
            }
        }

        public string Author
        {
            get
            {
                if (isPlaylist)
                {
                    return playlist?.Author ?? "";
                }
                
                if (numPlaylists == null)
                {
                    _ = LoadPlaylistsAsync();
                    return "";
                }
                
                return $"{numPlaylists} playlists";
            }
        }

        public Bitmap? CoverImage
        {
            get
            {
                if (coverImage != null)
                {
                    return coverImage;
                }
                coverImageLoader ??= Locator.Current.GetService<CoverImageLoader>()!;
                if (isPlaylist)
                {
                    _ = LoadCoverAsync();
                    return coverImageLoader.LoadingImage;
                }
                return coverImageLoader.FolderImage;
            }
            set
            {
                coverImage = value;
                NotifyPropertyChanged();
            }
        }
        
        private async Task LoadCoverAsync()
        {
            await using var imageStream = playlist?.GetCoverStream();
            var bitmap = await Task.Run(() => Bitmap.DecodeToWidth(imageStream, 400));
            if (bitmap != null)
            {
                RxApp.MainThreadScheduler.Schedule(() => CoverImage = bitmap);
            }
        }
        
        public async Task LoadPlaylistsAsync()
        {
            playlistLibUtils ??= Locator.Current.GetService<PlaylistLibUtils>()!;
            if (playlistManager != null)
            {
                numPlaylists = (await playlistLibUtils.GetPlaylistsAsync(playlistManager)).Length;
                NotifyPropertyChanged(nameof(Author));
            }
        }

        public void SetParentControl(PlaylistCoverView control)
        {
            this.control = control;
            if (IsRenaming)
            {
                _ = StartRenaming();
            }
        }

        #region Context Menu

        public async Task Cut()
        {
            if (PlaylistsListView.viewModel.CurrentManager != null)
            {
                await ClipboardHandler.Cut(PlaylistsListView.viewModel.SelectedPlaylistsOrManagers, PlaylistsListView.viewModel.CurrentManager);
            }
        }
        
        public async Task Copy()
        {
            if (PlaylistsListView.viewModel.CurrentManager != null)
            {
                await ClipboardHandler.Copy(PlaylistsListView.viewModel.SelectedPlaylistsOrManagers, PlaylistsListView.viewModel.CurrentManager);
            }
        }

        #region Rename

        private bool isRenaming;
        public bool IsRenaming
        {
            get => isRenaming;
            set
            {
                isRenaming = value;
                if (value)
                {
                    RenameTitle = Title;
                    _ = StartRenaming();
                }
                else
                {
                    if (Title != RenameTitle && !string.IsNullOrWhiteSpace(RenameTitle))
                    {
                        Title = RenameTitle;
                    }
                }
                NotifyPropertyChanged();
            }
        }
        
        private string renameTitle = "";
        public string RenameTitle
        {
            get => renameTitle;
            set
            {
                renameTitle = value;
                NotifyPropertyChanged();
            }
        }

        private async Task StartRenaming()
        {
            if (control != null)
            {
                // I am sorry but I gotta wait a tick
                await Task.Delay(1);
                control.RenameBox.Focus();
                control.RenameBox.SelectionStart = 0;
                control.RenameBox.SelectionEnd = Int32.MaxValue;
            }
        }

        #endregion

        public void Delete(bool showPopup = true)
        {
            if (showPopup)
            {
                var deleteMessage = GetDeleteMessage();
                if (deleteMessage != null && !popupShowing)
                {
                    MainWindow.NewContentDialog(deleteMessage, (sender, e) =>
                    {
                        CoreDelete();
                        popupShowing = false;
                    }, (sender, e) => popupShowing = false, "Yes", "No");
                    popupShowing = true;
                }   
            }
            else
            {
                CoreDelete();
            }
        }

        // TODO: Make this async
        private void CoreDelete()
        {
            if (PlaylistsListView.viewModel.CurrentManager != null)
            {
                var selectedItems = PlaylistsListView.viewModel.SelectedPlaylistsOrManagers.ToArray();
                foreach (var playlistsOrManager in selectedItems)
                {
                    if (playlistsOrManager.isPlaylist && playlistsOrManager.playlist != null)
                    {
                        var playlistPath = playlistsOrManager.playlist.GetPlaylistPath(PlaylistsListView.viewModel.CurrentManager);
                        if (File.Exists(playlistPath))
                        {
                            PlaylistsListView.viewModel.CurrentManager.DeletePlaylist(playlistsOrManager.playlist);
                        }
                    }
                    else if (playlistsOrManager.playlistManager != null)
                    {
                        if (Directory.Exists(playlistsOrManager.playlistManager.PlaylistPath))
                        {
                            PlaylistsListView.viewModel.CurrentManager.DeleteChildManager(playlistsOrManager.playlistManager);
                        }
                    }
                    PlaylistsListView.viewModel.SearchResults.Remove(playlistsOrManager);
                }   
            }
        }

        private string? GetDeleteMessage()
        {
            var numPlaylists = 0;
            var numManagers = 0;

            foreach (var playlistsOrManager in PlaylistsListView.viewModel.SelectedPlaylistsOrManagers)
            {
                if (playlistsOrManager.isPlaylist)
                {
                    numPlaylists++;
                }
                else
                {
                    numManagers++;
                }
            }

            if (numPlaylists > 0 && numManagers > 0)
            {
                return $"Are you sure you want to delete {numPlaylists} playlist{(numPlaylists != 1 ? "s" : "")} and " +
                       $"{numManagers} folder{(numManagers != 1 ? "s" : "")}?";
            }
            
            if (numPlaylists > 0)
            {
                return $"Are you sure you want to delete {numPlaylists} playlist{(numPlaylists != 1 ? "s" : "")}?";
            }

            if (numManagers > 0)
            {
                return $"Are you sure you want to delete {numManagers} folder{(numManagers != 1 ? "s" : "")}?";
            }

            return null;
        }

        #endregion
    }
}