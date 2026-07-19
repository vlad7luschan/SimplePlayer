using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;

namespace Simple_Player
{
    public partial class MainPage : ContentPage
    {
        private string _lastCurrentTime = string.Empty;
        private string _lastTotalTime = string.Empty;

        private double _previousVolume = 1.0;
        private bool _isMuted = false;
        private bool _isUpdatingVolumeFromCode = false;

        private List<string> _filePaths = new List<string>();
        public ObservableCollection<string> PlaylistItems { get; set; } = new ObservableCollection<string>();

        // Таймер для автоматичного приховання HUD
        private IDispatcherTimer _hudTimer;

        public MainPage()
        {
            InitializeComponent();

            playlistView.ItemsSource = PlaylistItems;
            ResetLabels();
            player.PositionChanged += OnPlayerPositionChanged;

            // Ініціалізуємо таймер (спрацьовує через 3 секунди нерухомості миші)
            _hudTimer = Dispatcher.CreateTimer();
            _hudTimer.Interval = TimeSpan.FromSeconds(3);
            _hudTimer.Tick += (s, e) => HideHud();

            // Динамічно стежимо за кількістю елементів для кнопки
            PlaylistItems.CollectionChanged += (s, e) => EvaluateToggleVisibility();

            // Запускаємо відлік таймера відразу при старті програми
            _hudTimer.Start();
        }

        // Обробник руху миші — викликається при найменшому русі по екрану
        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            ShowHud();
        }

        // Показує інтерфейс та скидає таймер
        // Показує інтерфейс МИТТЄВО при першому ж русі миші
        private void ShowHud()
        {
            // Скидаємо таймер приховання у будь-якому випадку, щоб зафіксувати показ
            _hudTimer.Stop();

            // Якщо HUD уже повністю видимий, просто перезапускаємо таймер і виходимо
            if (HudOverlay.Opacity >= 1 && !HudOverlay.InputTransparent)
            {
                _hudTimer.Start();
                return;
            }

            // Видаляємо старі незавершені анімації, щоб вони не гальмували процес
            HudOverlay.AbortAnimation("FadeIn");
            HudOverlay.AbortAnimation("FadeOut");

            // Робимо елементи клікабельними негайно
            HudOverlay.InputTransparent = false;

            // Швидка анімація появи (замість 250мс ставимо 100мс для блискавичного відгуку)
            HudOverlay.Animate("FadeIn",
                v => HudOverlay.Opacity = v,
                HudOverlay.Opacity, 1,
                length: 100,
                finished: (k, d) =>
                {
                    _hudTimer.Start();
                });
        }

        // Плавно ховає інтерфейс, коли миша спокійна
        private void HideHud()
        {
            _hudTimer.Stop();

            if (HudOverlay.Opacity <= 0) return;

            HudOverlay.AbortAnimation("FadeIn");
            HudOverlay.AbortAnimation("FadeOut");

            // Робимо HUD прозорим для кліків відразу, як тільки він почав зникати
            HudOverlay.InputTransparent = true;

            // Ховаємо за стандартні 250-300мс, щоб це виглядало приємно для ока
            HudOverlay.Animate("FadeOut", v => HudOverlay.Opacity = v, HudOverlay.Opacity, 0, 16, 250);
        }

        // Кнопка просто перемикає видимість самого плейліста вручну
        private void OnTogglePlaylistClicked(object sender, EventArgs e)
        {
            if (playlistView.IsVisible)
            {
                playlistView.IsVisible = false;
                btnTogglePlaylist.Text = "👁️ Show Playlist";
            }
            else
            {
                playlistView.IsVisible = true;
                btnTogglePlaylist.Text = "❌ Hide Playlist";
            }

            // Будь-яка дія користувача вважається активністю — оновлюємо таймер
            ShowHud();
        }

        private void EvaluateToggleVisibility()
        {
            if (PlaylistItems.Count > 0)
            {
                btnTogglePlaylist.IsVisible = true;
            }
            else
            {
                btnTogglePlaylist.IsVisible = false;
                playlistView.IsVisible = true;
                btnTogglePlaylist.Text = "❌ Hide Playlist";
            }
            ShowHud();
        }

        private void OnPlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = e.CurrentSelection.FirstOrDefault() as string;
            if (!string.IsNullOrEmpty(selectedItem))
            {
                string fullPath = _filePaths.FirstOrDefault(p => p.EndsWith(selectedItem));
                if (!string.IsNullOrEmpty(fullPath))
                {
                    player.Stop(); ResetLabels(); timelineSlider.Value = 0;
                    player.Source = MediaSource.FromFile(fullPath); player.Play();

                    // Якщо це відео, для зручності відразу ховаємо плашку плейліста
                    string extension = Path.GetExtension(fullPath).ToLower();
                    if (extension == ".mp4" || extension == ".avi")
                    {
                        playlistView.IsVisible = false;
                        btnTogglePlaylist.Text = "👁️ Show Playlist";
                    }
                    else
                    {
                        playlistView.IsVisible = true;
                        btnTogglePlaylist.Text = "❌ Hide Playlist";
                    }

                    // Активуємо показ HUD при старті нового треку/відео
                    ShowHud();
                }
            }
        }

        private void OnRemoveFileClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            var fileName = button?.CommandParameter as string;

            if (!string.IsNullOrEmpty(fileName))
            {
                // 1. Шукаємо повний шлях до файлу у нашому списку
                string fullPath = _filePaths.FirstOrDefault(p => p.EndsWith(fileName));

                // 2. Видаляємо з внутрішнього списку шляхів та колекції для UI
                if (!string.IsNullOrEmpty(fullPath)) _filePaths.Remove(fullPath);
                PlaylistItems.Remove(fileName);

                // 3. НАДІЙНА ПЕРЕВІРКА: Отримуємо шлях через властивість .Path замість .File
                if (player.Source is FileMediaSource fileSource && !string.IsNullOrEmpty(fileSource.Path))
                {
                    string currentlyPlayingName = Path.GetFileName(fileSource.Path);

                    // Якщо імена збігаються — повністю зупиняємо відтворення та скидаємо джерело
                    if (currentlyPlayingName == fileName)
                    {
                        player.Stop();
                        player.Source = null; // Повністю очищуємо джерело
                        ResetLabels();
                        timelineSlider.Value = 0;
                    }
                }
            }
            ShowHud();
        }

        private async void OnOpenFileClicked(object sender, EventArgs e)
        {
            ShowHud();
            try
            {
                var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                    { DevicePlatform.WinUI, new[] { ".mp3", ".mp4", ".m4a", ".avi", ".wav" } },
                    { DevicePlatform.Android, new[] { "audio/*", "video/*" } }
                });
                var results = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "Select media files", FileTypes = customFileType });
                if (results != null && results.Any())
                {
                    foreach (var file in results) { _filePaths.Add(file.FullPath); PlaylistItems.Add(file.FileName); }
                }
            }
            catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        }

        private void UpdateLabel(Grid container, string text)
        {
            container.Children.Clear();
            var newLabel = new Label { Text = text, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center };
            container.Children.Add(newLabel);
        }

        private void ResetLabels()
        {
            _lastCurrentTime = "00:00"; _lastTotalTime = "00:00";
            UpdateLabel(CurrentTimeContainer, _lastCurrentTime); UpdateLabel(TotalTimeContainer, _lastTotalTime);
        }

        private void OnPlayerPositionChanged(object sender, MediaPositionChangedEventArgs e)
        {
            string newCurrentTime = e.Position.ToString(@"mm\:ss");
            if (_lastCurrentTime != newCurrentTime) { _lastCurrentTime = newCurrentTime; UpdateLabel(CurrentTimeContainer, _lastCurrentTime); }

            if (player.Duration != TimeSpan.Zero)
            {
                string newTotalTime = player.Duration.ToString(@"mm\:ss");
                if (_lastTotalTime != newTotalTime) { _lastTotalTime = newTotalTime; UpdateLabel(TotalTimeContainer, _lastTotalTime); }
                timelineSlider.Maximum = player.Duration.TotalSeconds;
                timelineSlider.Value = e.Position.TotalSeconds;
            }
        }

        private void OnTimelineSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (Math.Abs(player.Position.TotalSeconds - e.NewValue) > 1.5) player.SeekTo(TimeSpan.FromSeconds(e.NewValue));
            ShowHud();
        }

        private void OnVolumeSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (_isUpdatingVolumeFromCode) return;
            player.Volume = e.NewValue;
            btnMute.Text = e.NewValue > 0 ? "🔊" : "🔇";
            _isMuted = e.NewValue <= 0;
            ShowHud();
        }

        private void OnMuteClicked(object sender, EventArgs e)
        {
            _isUpdatingVolumeFromCode = true;
            if (!_isMuted)
            {
                _previousVolume = volumeSlider.Value;
                if (_previousVolume < 0.05) _previousVolume = 0.5;
                volumeSlider.Value = 0; player.Volume = 0; btnMute.Text = "🔇"; _isMuted = true;
            }
            else
            {
                volumeSlider.Value = _previousVolume; player.Volume = _previousVolume; btnMute.Text = "🔊"; _isMuted = false;
            }
            _isUpdatingVolumeFromCode = false;
            ShowHud();
        }

        private void OnPlayClicked(object sender, EventArgs e) { player.Play(); ShowHud(); }
        private void OnPauseClicked(object sender, EventArgs e) { player.Pause(); ShowHud(); }
        private void OnStopClicked(object sender, EventArgs e) { player.Stop(); ShowHud(); }
    }
}