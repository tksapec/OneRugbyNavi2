using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace OneRugbyNavi2
{
    public partial class MainPage : ContentPage
    {
        private readonly ScheduleViewModel _vm = new();

        private const string FetchFailedMessage = "試合日程を取得できませんでした。通信状態または公式サイトの構造を確認してください。";
        private const string FetchedOfficialScheduleMessage = "Webから最新の日程を取得しました。";
        private const string ShowingScheduleCacheMessage = "通信に失敗したため、前回取得した試合日程を表示しています。";
        private const string PartialFailureMessage = "\u4E00\u90E8\u306EDivision\u306E\u53D6\u5F97\u307E\u305F\u306F\u7D50\u679C\u88DC\u5B8C\u306B\u5931\u6557\u3057\u307E\u3057\u305F\u3002\u8868\u793A\u3067\u304D\u308B\u30C7\u30FC\u30BF\u3092\u8868\u793A\u3057\u3066\u3044\u307E\u3059\u3002";
        private const string RefreshSuccessMessage = "\u6700\u65B0\u30C7\u30FC\u30BF\u3092\u53D6\u5F97\u3057\u307E\u3057\u305F\u3002";
        private const string ShowingCacheMessage = "\u524D\u56DE\u53D6\u5F97\u30C7\u30FC\u30BF\u3092\u8868\u793A\u3057\u3066\u3044\u307E\u3059";
        private const string FavoriteTeamsKey = "FavoriteTeams.v2";
        private const string LegacyFavoriteTeamKey = "FavoriteTeam";
        private const string NoFavoriteMatchMessage = "このシーズンの日程にお気に入りチームが見つかりません。";

        private bool _hasInitialized;
        private bool _isRefreshing;
        private bool _suppressPickerEvents;
        private bool _isShowingCache;
        private bool _isFilterExpanded;
        private bool _teamFilterManuallySelected;
        private bool _isFavoriteFilterActive;
        private bool _isDivisionSwipeAnimating;
        private string? _lastMessage;
        private System.Collections.Generic.IReadOnlyList<string> _favoriteTeams = Array.Empty<string>();
        private string _databaseBuildTimestampText = "-";
        private string _selectedSeasonKey = "";
        private string _selectedSeasonLabel = "";
        private string? _dataConsistencyWarning;
        private readonly System.Collections.Generic.List<ScheduleFetcher.SeasonOption> _seasonOptions = new();
        private CancellationTokenSource? _messageHideCts;

        public MainPage()
        {
            InitializeComponent();

            UpdateSeasonTitle();

            list.ItemsSource = _vm.FilteredItems;
            periodPicker.ItemsSource = new[] { "\u3059\u3079\u3066", "\u4ECA\u5F8C\u306E\u8A66\u5408", "\u904E\u53BB\u306E\u8A66\u5408" };
            periodPicker.SelectedIndex = 0;
            LoadFavoriteTeams();
            UpdateFavoriteUi();
            UpdateTabVisual(1);
            RefreshCategoryTabs();
            UpdateEmptyState();
            UpdateFilterPanelUi();
            UpdateFilterSummaryUi();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_hasInitialized)
            {
                var previousFavorites = _favoriteTeams.ToArray();
                LoadFavoriteTeams();
                UpdateFavoriteUi();
                if (!previousFavorites.SequenceEqual(_favoriteTeams, StringComparer.Ordinal) && _isFavoriteFilterActive)
                {
                    var matchingFavorites = FindMatchingFavoritesForCurrentDivision();
                    if (matchingFavorites.Count == 0)
                    {
                        ExitFavoriteFilterMode();
                    }
                    else
                    {
                        _vm.FavoriteTeamFilters = matchingFavorites;
                    }
                    _vm.ApplyFilters();
                    RefreshPickers(preserveSelection: true);
                    ScrollScheduleToStart();
                    UpdateEmptyState();
                    UpdateFilterSummaryUi();
                }

                return;
            }

            _hasInitialized = true;
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            await RefreshDataAsync(showSuccessMessage: false, chooseFavoriteDivision: true);
        }

        private async Task RefreshDataAsync(bool showSuccessMessage, bool chooseFavoriteDivision = false)
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            SetLoading(true);
            SetMessage("", false);
            _dataConsistencyWarning = null;
            cachePanel.IsVisible = false;

            try
            {
                var currentCategory = _vm.CurrentCategory;
                var preserveFavoriteFilter = _isFavoriteFilterActive && !chooseFavoriteDivision;
                var selectedTeam = chooseFavoriteDivision ? null : _vm.TeamFilter;
                var selectedVenue = _vm.VenueFilter;
                var selectedPeriod = _vm.PeriodFilter;
                ScheduleFetcher.FetchAllResult? fetchResult = null;
                try
                {
                    fetchResult = await ScheduleFetcher.FetchSeasonAsync(string.IsNullOrWhiteSpace(_selectedSeasonKey) ? null : _selectedSeasonKey);
                }
                catch
                {
                    fetchResult = null;
                }

                IReadOnlyCollection<ScheduleFetcher.Item> div1 = fetchResult == null ? Array.Empty<ScheduleFetcher.Item>() : fetchResult.Div1;
                IReadOnlyCollection<ScheduleFetcher.Item> div2 = fetchResult == null ? Array.Empty<ScheduleFetcher.Item>() : fetchResult.Div2;
                IReadOnlyCollection<ScheduleFetcher.Item> div3 = fetchResult == null ? Array.Empty<ScheduleFetcher.Item>() : fetchResult.Div3;
                IReadOnlyCollection<ScheduleFetcher.Item> replacement = fetchResult == null ? Array.Empty<ScheduleFetcher.Item>() : fetchResult.Replacement;
                IReadOnlyCollection<ScheduleFetcher.Item> other = fetchResult == null ? Array.Empty<ScheduleFetcher.Item>() : fetchResult.Other;
                _isShowingCache = false;

                if (fetchResult != null && fetchResult.Seasons.Count > 0)
                {
                    UpdateSeasonOptions(fetchResult.Seasons, fetchResult.SeasonKey, fetchResult.SeasonLabel);
                }

                if (HasScheduleItems(div1, div2, div3, replacement, other))
                {
                    var fetchedAt = fetchResult?.FetchedAt ?? DateTimeOffset.Now;
                    _selectedSeasonKey = fetchResult?.SeasonKey ?? _selectedSeasonKey;
                    _selectedSeasonLabel = fetchResult?.SeasonLabel ?? _selectedSeasonLabel;
                    _dataConsistencyWarning = fetchResult?.DataConsistencyWarning;
                    await ScheduleCacheStore.SaveAsync(_selectedSeasonKey, _selectedSeasonLabel, div1, div2, div3, replacement, other, fetchedAt);
                    _databaseBuildTimestampText = fetchedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
                    var statusMessage = fetchResult?.DataConsistencyWarning is { Length: > 0 } warning
                        ? $"{FetchedOfficialScheduleMessage}\n{warning}"
                        : FetchedOfficialScheduleMessage;
                    SetMessage(statusMessage, true, autoHide: true);
                }
                else
                {
                    EnsureDefaultSeasonSelection();
                    var cache = await ScheduleCacheStore.LoadAsync(_selectedSeasonKey, _selectedSeasonLabel);
                    if (cache == null || !HasScheduleItems(cache.Div1, cache.Div2, cache.Div3, cache.Replacement, cache.Other))
                    {
                        _vm.SetItems(
                            Array.Empty<ScheduleFetcher.Item>(),
                            Array.Empty<ScheduleFetcher.Item>(),
                            Array.Empty<ScheduleFetcher.Item>(),
                            Array.Empty<ScheduleFetcher.Item>(),
                            Array.Empty<ScheduleFetcher.Item>());
                        _vm.SetSource(currentCategory);
                        _vm.TeamFilter = null;
                        ExitFavoriteFilterMode();
                        _vm.VenueFilter = null;
                        RefreshPickers(preserveSelection: false);
                        _databaseBuildTimestampText = "-";
                        UpdateLastUpdatedLabel();
                        SetMessage(FetchFailedMessage, true, autoHide: true);
                        UpdateEmptyState();
                        UpdateFavoriteUi();
                        UpdateFilterSummaryUi();
                        return;
                    }

                    div1 = cache.Div1;
                    div2 = cache.Div2;
                    div3 = cache.Div3;
                    replacement = cache.Replacement;
                    other = cache.Other;
                    _isShowingCache = true;
                    _selectedSeasonKey = cache.SeasonKey;
                    _selectedSeasonLabel = cache.SeasonLabel;
                    UpdateSeasonTitle();
                    _databaseBuildTimestampText = cache.LastUpdated.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
                    SetMessage(ShowingScheduleCacheMessage, true, autoHide: true);
                }

                _vm.SetItems(div1, div2, div3, replacement, other);
                var normalizedFavorites = FavoriteTeamLogic.NormalizeByDivision(
                    _favoriteTeams,
                    GetSelectedSeasonStartYear(),
                    _vm.ItemsDiv1.ToList(),
                    _vm.ItemsDiv2.ToList(),
                    _vm.ItemsDiv3.ToList());
                if (!_favoriteTeams.SequenceEqual(normalizedFavorites, StringComparer.Ordinal))
                {
                    _favoriteTeams = normalizedFavorites;
                    Preferences.Set(FavoriteTeamsKey, FavoriteTeamLogic.Serialize(_favoriteTeams));
                    Preferences.Remove(LegacyFavoriteTeamKey);
                }

                _vm.SetSource(currentCategory);
                if (_vm.GetCurrentDivisionItemCount() == 0)
                {
                    _vm.SetSource(ScheduleViewModel.CategoryDiv1);
                }

                if (chooseFavoriteDivision)
                {
                    var favoriteDivision = FavoriteTeamLogic.FindHighestFavoriteDivision(
                        _vm.ItemsDiv1.ToList(), _vm.ItemsDiv2.ToList(), _vm.ItemsDiv3.ToList(), _favoriteTeams);
                    if (favoriteDivision.HasValue)
                    {
                        _vm.SetSource(DivisionToCategory(favoriteDivision.Value));
                    }
                }

                bool canPreserveVenue = !string.IsNullOrWhiteSpace(selectedVenue) &&
                    _vm.GetVenuesForPicker().Contains(selectedVenue);

                NormalizeManualTeamSelection(selectedTeam);
                _vm.TeamFilter = _teamFilterManuallySelected ? FindCurrentTeamAlias(selectedTeam) : null;
                _vm.VenueFilter = canPreserveVenue ? selectedVenue : null;
                _vm.PeriodFilter = selectedPeriod;
                if (preserveFavoriteFilter)
                {
                    var matchingFavorites = FindMatchingFavoritesForCurrentDivision();
                    if (matchingFavorites.Count == 0)
                    {
                        ExitFavoriteFilterMode();
                        SetMessage(NoFavoriteMatchMessage, true, autoHide: true);
                    }
                    else
                    {
                        _vm.FavoriteTeamFilters = matchingFavorites;
                    }
                }
                else
                {
                    ExitFavoriteFilterMode();
                }
                _vm.ApplyFilters();
                RefreshCategoryTabs();
                RefreshPickers(preserveSelection: true);
                ScrollScheduleToStart();

                UpdateLastUpdatedLabel();

                UpdateEmptyState();
                UpdateFavoriteUi();
                UpdateFilterSummaryUi();
            }
            catch
            {
                ExitFavoriteFilterMode();
                _vm.SetItems(
                    Array.Empty<ScheduleFetcher.Item>(),
                    Array.Empty<ScheduleFetcher.Item>(),
                    Array.Empty<ScheduleFetcher.Item>(),
                    Array.Empty<ScheduleFetcher.Item>(),
                    Array.Empty<ScheduleFetcher.Item>());
                _vm.SetSource(_vm.CurrentCategory);
                RefreshPickers(preserveSelection: false);
                _databaseBuildTimestampText = "-";
                UpdateLastUpdatedLabel();
                SetMessage(FetchFailedMessage, true, autoHide: true);
                UpdateEmptyState();
                UpdateFavoriteUi();
                UpdateFilterSummaryUi();
            }
            finally
            {
                SetLoading(false);
                _isRefreshing = false;
            }
        }

        private void OnDiv1Clicked(object sender, EventArgs e)
        {
            SelectCategory(ScheduleViewModel.CategoryDiv1);
        }

        private void OnDiv2Clicked(object sender, EventArgs e)
        {
            SelectCategory(ScheduleViewModel.CategoryDiv2);
        }

        private void OnDiv3Clicked(object sender, EventArgs e)
        {
            SelectCategory(ScheduleViewModel.CategoryDiv3);
        }

        private void OnReplacementClicked(object sender, EventArgs e)
        {
            SelectCategory(ScheduleViewModel.CategoryReplacement);
        }

        private void OnOtherClicked(object sender, EventArgs e)
        {
            SelectCategory(ScheduleViewModel.CategoryOther);
        }

        private void SelectCategory(string category)
        {
            ExitFavoriteFilterMode();
            _vm.SetSource(category);
            NormalizeManualTeamSelection(_vm.TeamFilter);
            _vm.TeamFilter = _teamFilterManuallySelected ? FindCurrentTeamAlias(_vm.TeamFilter) : null;
            _vm.ApplyFilters();
            UpdateTabVisual(_vm.CurrentCategory);
            RefreshPickers(preserveSelection: true);
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFavoriteUi();
            UpdateFilterSummaryUi();
        }

        private async void OnDivisionTabsSwiped(object sender, SwipedEventArgs e)
        {
            if (e.Direction is not (SwipeDirection.Left or SwipeDirection.Right))
            {
                return;
            }

            await TurnSchedulePageAsync(e.Direction);
        }

        private async Task TurnSchedulePageAsync(SwipeDirection direction)
        {
            if (_isDivisionSwipeAnimating)
            {
                return;
            }

            var currentDivision = _vm.CurrentDivision;
            if (currentDivision is < 1 or > 3)
            {
                return;
            }

            var swipeDirection = direction switch
            {
                SwipeDirection.Left => DivisionSwipeDirection.Left,
                SwipeDirection.Right => DivisionSwipeDirection.Right,
                _ => (DivisionSwipeDirection?)null
            };
            if (swipeDirection is null)
            {
                return;
            }

            var targetDivision = DivisionSwipeNavigation.GetAdjacentDivision(currentDivision, swipeDirection.Value);
            if (targetDivision is null)
            {
                return;
            }

            _isDivisionSwipeAnimating = true;
            var isForwardTurn = direction == SwipeDirection.Left;
            var departingPage = CaptureSchedulePage();
            var curl = new PageCurlDrawable(isForwardTurn, departingPage);
            pageCurlOverlay.Drawable = curl;
            try
            {
                pageCurlOverlay.IsVisible = true;
                SelectCategory(DivisionToCategory(targetDivision.Value));
                await AnimatePageCurlAsync(curl, 0, 1, 520, Easing.CubicInOut);
            }
            finally
            {
                curl.SetProgress(0);
                pageCurlOverlay.Invalidate();
                pageCurlOverlay.IsVisible = false;
                _isDivisionSwipeAnimating = false;
            }
        }

#if ANDROID
        private Microsoft.Maui.Graphics.IImage? CaptureSchedulePage()
        {
            if (schedulePageContent.Handler?.PlatformView is not Android.Views.View nativePage ||
                nativePage.Width <= 0 || nativePage.Height <= 0)
                return null;

            using var bitmap = Android.Graphics.Bitmap.CreateBitmap(
                nativePage.Width, nativePage.Height, Android.Graphics.Bitmap.Config.Argb8888!);
            using (var nativeCanvas = new Android.Graphics.Canvas(bitmap))
                nativePage.Draw(nativeCanvas);

            using var stream = new System.IO.MemoryStream();
            bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream);
            stream.Position = 0;
            var imageLoader = schedulePageContent.Handler.MauiContext?.Services
                .GetService<Microsoft.Maui.Graphics.IImageLoadingService>();
            return imageLoader?.FromStream(stream, Microsoft.Maui.Graphics.ImageFormat.Png);
        }

#endif

        private async Task AnimatePageCurlAsync(PageCurlDrawable curl, double from, double to, uint duration, Easing easing)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var animation = new Animation(value =>
            {
                curl.SetProgress(value);
                pageCurlOverlay.Invalidate();
            }, from, to, easing);
            animation.Commit(this, "DivisionPageCurl", length: duration, finished: (_, wasCancelled) =>
            {
                if (wasCancelled)
                    completion.TrySetCanceled();
                else
                    completion.TrySetResult();
            });

            await completion.Task;
        }

        private void UpdateTabVisual(int active)
        {
            UpdateTabVisual(active switch
            {
                1 => ScheduleViewModel.CategoryDiv1,
                2 => ScheduleViewModel.CategoryDiv2,
                3 => ScheduleViewModel.CategoryDiv3,
                4 => ScheduleViewModel.CategoryReplacement,
                _ => ScheduleViewModel.CategoryOther
            });
        }

        private void UpdateTabVisual(string active)
        {
            var normalized = ScheduleViewModel.NormalizeCategoryCode(active);
            ApplyTabVisual(btnDiv1, normalized == ScheduleViewModel.CategoryDiv1);
            ApplyTabVisual(btnDiv2, normalized == ScheduleViewModel.CategoryDiv2);
            ApplyTabVisual(btnDiv3, normalized == ScheduleViewModel.CategoryDiv3);
            ApplyTabVisual(btnReplacement, normalized == ScheduleViewModel.CategoryReplacement);
            ApplyTabVisual(btnOther, normalized == ScheduleViewModel.CategoryOther);
        }

        private static void ApplyTabVisual(Button button, bool active)
        {
            button.BackgroundColor = active ? PageStyles.Blue : Colors.Transparent;
            button.TextColor = active ? Colors.White : PageStyles.Navy;
            button.Opacity = active ? 1.0 : 0.85;
        }

        private async void OnSeasonChanged(object sender, EventArgs e)
        {
            if (_suppressPickerEvents || seasonPicker.SelectedIndex < 0 || seasonPicker.SelectedIndex >= _seasonOptions.Count)
            {
                return;
            }

            var selected = _seasonOptions[seasonPicker.SelectedIndex];
            if (string.Equals(_selectedSeasonKey, selected.SeasonKey, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSeasonKey = selected.SeasonKey;
            _selectedSeasonLabel = selected.SeasonLabel;
            UpdateSeasonTitle();
            _teamFilterManuallySelected = false;
            ExitFavoriteFilterMode();
            await RefreshDataAsync(showSuccessMessage: true, chooseFavoriteDivision: true);
        }

        private void UpdateSeasonOptions(
            System.Collections.Generic.IReadOnlyList<ScheduleFetcher.SeasonOption> seasons,
            string selectedSeasonKey,
            string selectedSeasonLabel)
        {
            _seasonOptions.Clear();
            _seasonOptions.AddRange(seasons);
            _selectedSeasonKey = selectedSeasonKey;
            _selectedSeasonLabel = selectedSeasonLabel;
            UpdateSeasonTitle();

            _suppressPickerEvents = true;
            seasonPicker.ItemsSource = _seasonOptions.Select(season => season.SeasonLabel).ToArray();
            seasonPicker.SelectedIndex = Math.Max(0, _seasonOptions.FindIndex(season => string.Equals(season.SeasonKey, selectedSeasonKey, StringComparison.Ordinal)));
            _suppressPickerEvents = false;
        }

        private void EnsureDefaultSeasonSelection()
        {
            if (!string.IsNullOrWhiteSpace(_selectedSeasonKey))
            {
                return;
            }

            var firstSeason = _seasonOptions.FirstOrDefault();
            _selectedSeasonKey = firstSeason?.SeasonKey ?? "";
            _selectedSeasonLabel = firstSeason?.SeasonLabel ?? "";
            UpdateSeasonTitle();
            if (_seasonOptions.Count == 0)
            {
                _suppressPickerEvents = true;
                seasonPicker.ItemsSource = Array.Empty<string>();
                seasonPicker.SelectedIndex = -1;
                _suppressPickerEvents = false;
            }
        }

        private void UpdateSeasonTitle()
        {
            var key = string.IsNullOrWhiteSpace(_selectedSeasonKey)
                ? SeasonCatalog.CurrentSeasonKey
                : _selectedSeasonKey;
            var label = SeasonCatalog.ToSeasonUiLabel(key);
            seasonTitleLabel.Text = string.IsNullOrWhiteSpace(label) ? "Season" : label;
        }

        private void RefreshCategoryTabs()
        {
            btnReplacement.IsVisible = _vm.GetCategoryItemCount(ScheduleViewModel.CategoryReplacement) > 0;
            btnOther.IsVisible = _vm.GetCategoryItemCount(ScheduleViewModel.CategoryOther) > 0;
            var visibleButtons = new[] { btnDiv1, btnDiv2, btnDiv3, btnReplacement, btnOther }
                .Where(button => button.IsVisible)
                .ToArray();
            divisionTabsGrid.ColumnDefinitions.Clear();
            for (var index = 0; index < visibleButtons.Length; index++)
            {
                divisionTabsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                Grid.SetColumn(visibleButtons[index], index);
            }

            if (!btnReplacement.IsVisible && _vm.CurrentCategory == ScheduleViewModel.CategoryReplacement)
            {
                _vm.SetSource(ScheduleViewModel.CategoryDiv1);
            }

            if (!btnOther.IsVisible && _vm.CurrentCategory == ScheduleViewModel.CategoryOther)
            {
                _vm.SetSource(ScheduleViewModel.CategoryDiv1);
            }

            UpdateTabVisual(_vm.CurrentCategory);
        }

        private void RefreshPickers(bool preserveSelection)
        {
            if (!preserveSelection)
            {
                _vm.TeamFilter = null;
                _vm.VenueFilter = null;
                _vm.PeriodFilter = ScheduleViewModel.DateRangeFilter.All;
                _vm.ApplyFilters();
            }

            var previousTeam = preserveSelection ? _vm.TeamFilter : null;
            var previousVenue = preserveSelection ? _vm.VenueFilter : null;
            var previousPeriod = preserveSelection ? _vm.PeriodFilter : ScheduleViewModel.DateRangeFilter.All;

            var teams = _vm.GetTeamsForPicker();
            var venues = _vm.GetVenuesForPicker();

            _suppressPickerEvents = true;
            teamPicker.ItemsSource = teams;
            venuePicker.ItemsSource = venues;
            teamPicker.SelectedIndex = GetSelectedIndex(teams, previousTeam);
            venuePicker.SelectedIndex = GetSelectedIndex(venues, previousVenue);
            periodPicker.SelectedIndex = (int)previousPeriod;
            _suppressPickerEvents = false;
        }

        private void OnTeamFilterChanged(object sender, EventArgs e)
        {
            if (_suppressPickerEvents || teamPicker.SelectedIndex < 0)
            {
                return;
            }

            var value = teamPicker.SelectedItem as string;
            ExitFavoriteFilterMode();
            _vm.TeamFilter = string.IsNullOrWhiteSpace(value) ? null : value;
            _teamFilterManuallySelected = !string.IsNullOrWhiteSpace(_vm.TeamFilter);
            _vm.ApplyFilters();
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFavoriteUi();
            UpdateFilterSummaryUi();
        }

        private void OnVenueFilterChanged(object sender, EventArgs e)
        {
            if (_suppressPickerEvents || venuePicker.SelectedIndex < 0)
            {
                return;
            }

            var value = venuePicker.SelectedItem as string;
            _vm.VenueFilter = string.IsNullOrWhiteSpace(value) ? null : value;
            _vm.ApplyFilters();
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFavoriteUi();
            UpdateFilterSummaryUi();
        }

        private void OnPeriodFilterChanged(object sender, EventArgs e)
        {
            if (_suppressPickerEvents || periodPicker.SelectedIndex < 0)
            {
                return;
            }

            _vm.PeriodFilter = (ScheduleViewModel.DateRangeFilter)periodPicker.SelectedIndex;
            _vm.ApplyFilters();
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFilterSummaryUi();
        }

        private void OnClearFilters(object sender, EventArgs e)
        {
            ExitFavoriteFilterMode();
            _vm.TeamFilter = null;
            _vm.VenueFilter = null;
            _vm.PeriodFilter = ScheduleViewModel.DateRangeFilter.All;
            _teamFilterManuallySelected = false;
            _vm.ApplyFilters();
            RefreshPickers(preserveSelection: true);
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFavoriteUi();
            UpdateFilterSummaryUi();
        }

        private void OnFavoriteFilterLabelTapped(object sender, TappedEventArgs e)
        {
            if (_favoriteTeams.Count == 0)
            {
                return;
            }

            if (_isFavoriteFilterActive)
            {
                ExitFavoriteFilterMode();
                _vm.ApplyFilters();
                RefreshPickers(preserveSelection: true);
                ScrollScheduleToStart();
                UpdateEmptyState();
                UpdateFavoriteUi();
                UpdateFilterSummaryUi();
                return;
            }

            var resolution = FavoriteTeamLogic.ResolveQuickFilter(
                _vm.CurrentDivision,
                _vm.ItemsDiv1.ToList(),
                _vm.ItemsDiv2.ToList(),
                _vm.ItemsDiv3.ToList(),
                _favoriteTeams);
            if (resolution is null)
            {
                SetMessage(NoFavoriteMatchMessage, true, autoHide: true);
                return;
            }

            ExitFavoriteFilterMode();
            _vm.SetSource(DivisionToCategory(resolution.Division));
            _vm.TeamFilter = null;
            _teamFilterManuallySelected = false;
            _vm.FavoriteTeamFilters = resolution.Favorites;
            _isFavoriteFilterActive = true;
            _vm.ApplyFilters();
            UpdateTabVisual(_vm.CurrentCategory);
            RefreshPickers(preserveSelection: true);
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFavoriteUi();
            UpdateFilterSummaryUi();
        }

        private void OnToggleFiltersClicked(object sender, EventArgs e)
        {
            _isFilterExpanded = !_isFilterExpanded;
            UpdateFilterPanelUi();
        }

        private async void OnMatchTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not BindableObject { BindingContext: MatchItem match })
            {
                return;
            }

            try
            {
                await NavigateToMatchAsync(match);
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u8A66\u5408\u8A73\u7D30\u3092\u958B\u3051\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private async void OnHomeTeamLogoClicked(object sender, EventArgs e)
        {
            if (sender is BindableObject { BindingContext: MatchItem match })
            {
                await TeamOfficialPageNavigator.OpenAsync(this, match.HomeTeam);
            }
        }

        private async void OnAwayTeamLogoClicked(object sender, EventArgs e)
        {
            if (sender is BindableObject { BindingContext: MatchItem match })
            {
                await TeamOfficialPageNavigator.OpenAsync(this, match.AwayTeam);
            }
        }

        private async Task NavigateToMatchAsync(MatchItem match)
        {
            await Navigation.PushAsync(new MatchDetailPage(match));
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await RefreshDataAsync(showSuccessMessage: true);
        }

        private void OnFavoriteClicked(object sender, EventArgs e)
        {
            var team = _vm.TeamFilter;
            if (string.IsNullOrWhiteSpace(team))
            {
                SetMessage("お気に入りを管理するチームを選択してください。", true, autoHide: true);
                return;
            }

            var seasonYear = GetSelectedSeasonStartYear();
            var wasFavorite = IsFavoriteTeam(team, seasonYear);
            _favoriteTeams = FavoriteTeamLogic.ToggleInDivision(
                _favoriteTeams,
                team,
                _vm.CurrentDivision,
                seasonYear,
                _vm.ItemsDiv1.ToList(),
                _vm.ItemsDiv2.ToList(),
                _vm.ItemsDiv3.ToList());
            Preferences.Set(FavoriteTeamsKey, FavoriteTeamLogic.Serialize(_favoriteTeams));
            Preferences.Remove(LegacyFavoriteTeamKey);
            ExitFavoriteFilterMode();
            _vm.TeamFilter = null;
            _teamFilterManuallySelected = false;
            _vm.ApplyFilters();
            RefreshPickers(preserveSelection: true);
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFavoriteUi();
            UpdateFilterSummaryUi();
            SetMessage(wasFavorite ? $"お気に入りから解除しました: {team}" : $"お気に入りに登録しました: {team}", true, autoHide: true);
        }
        private void OnInfoClicked(object sender, EventArgs e)
        {
            menuOverlay.IsVisible = true;
        }

        private void CloseMenuOverlay()
        {
            if (!menuOverlay.IsVisible)
            {
                return;
            }

            menuOverlay.IsVisible = false;
        }

        private void OnMenuBackdropTapped(object sender, TappedEventArgs e) => CloseMenuOverlay();

        private void OnMenuCloseClicked(object sender, EventArgs e) => CloseMenuOverlay();

        protected override bool OnBackButtonPressed()
        {
            if (menuOverlay.IsVisible)
            {
                CloseMenuOverlay();
                return true;
            }

            return base.OnBackButtonPressed();
        }

        private async void OnMenuStatusClicked(object sender, EventArgs e)
        {
            CloseMenuOverlay();
            var cacheState = _isShowingCache ? "前回取得データを表示中" : "最新取得データを表示中";
            var body =
                $"対象シーズン: {_selectedSeasonLabel}\n" +
                $"表示状態: {cacheState}\n" +
                $"最終更新: {_databaseBuildTimestampText}\n\n" +
                (string.IsNullOrWhiteSpace(_dataConsistencyWarning) ? "" : $"データ確認: {_dataConsistencyWarning}\n\n") +
                $"D1: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv1)}件 / " +
                $"D2: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv2)}件 / " +
                $"D3: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv3)}件 / " +
                $"入替戦: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryReplacement)}件 / " +
                $"その他: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryOther)}件\n\n" +
                "取得元: JAPAN RUGBY LEAGUE ONE公式サイト\n" +
                "使用ライブラリとライセンスの詳細は、配布物内のLICENSES.txtを参照してください。";
            await DisplayAlert("更新状況", body, "OK");
        }

        private async void OnMenuScheduleClicked(object sender, EventArgs e)
        {
            CloseMenuOverlay();
            await Shell.Current.GoToAsync("//SchedulePage");
        }

        private async void OnMenuTeamsClicked(object sender, EventArgs e)
        {
            await NavigateToMenuPageAsync(nameof(TeamListPage));
        }

        private async void OnMenuStandingsClicked(object sender, EventArgs e)
        {
            CloseMenuOverlay();
            await OpenWebAsync("https://league-one.jp/standings/");
        }

        private async void OnMenuRankingClicked(object sender, EventArgs e)
        {
            await NavigateToMenuPageAsync(nameof(RankingPage));
        }

        private async void OnMenuAboutClicked(object sender, EventArgs e)
        {
            await NavigateToMenuPageAsync(nameof(InfoPage));
        }

        private async Task NavigateToMenuPageAsync(string route)
        {
            CloseMenuOverlay();
            await Shell.Current.GoToAsync("//SchedulePage");
            await Shell.Current.GoToAsync(route);
        }

        private async void OnMenuClearCacheClicked(object sender, EventArgs e)
        {
            CloseMenuOverlay();
            await DeleteCacheAsync();
        }

        private void SetLoading(bool isLoading)
        {
            loadingPanel.IsVisible = isLoading;
        }

        private void SetMessage(string message, bool visible, bool autoHide = true)
        {
            _messageHideCts?.Cancel();
            _lastMessage = visible ? message : null;
            messageLabel.Text = message;
            messagePanel.IsVisible = visible && !string.IsNullOrWhiteSpace(message);

            if (autoHide && messagePanel.IsVisible)
            {
                _messageHideCts = new CancellationTokenSource();
                _ = HideMessageAfterDelayAsync(_messageHideCts.Token);
            }
        }

        private async Task HideMessageAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4), token);
                if (!token.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _lastMessage = null;
                        messageLabel.Text = "";
                        messagePanel.IsVisible = false;
                        UpdateEmptyState();
                    });
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void UpdateLastUpdatedLabel()
        {
            var season = string.IsNullOrWhiteSpace(_selectedSeasonLabel) ? "" : $" / {_selectedSeasonLabel}";
            lastUpdatedLabel.Text = $"最終更新: {_databaseBuildTimestampText}{season}";
        }

        private void UpdateFilterPanelUi()
        {
            filterDetailsPanel.IsVisible = _isFilterExpanded;
            filterToggleButton.Text = _isFilterExpanded
                ? "\u30D5\u30A3\u30EB\u30BF\u3092\u9589\u3058\u308B"
                : "\u30D5\u30A3\u30EB\u30BF\u3092\u958B\u304F";
        }

        private void UpdateFilterSummaryUi()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(_vm.TeamFilter))
            {
                parts.Add($"\u30C1\u30FC\u30E0: {_vm.TeamFilter}");
            }

            if (!string.IsNullOrWhiteSpace(_vm.VenueFilter))
            {
                parts.Add($"\u4F1A\u5834: {_vm.VenueFilter}");
            }

            if (_vm.PeriodFilter != ScheduleViewModel.DateRangeFilter.All)
            {
                parts.Add($"\u671F\u9593: {GetPeriodFilterText(_vm.PeriodFilter)}");
            }

            filterSummaryLabel.IsVisible = parts.Count > 0 || !_isFavoriteFilterActive;
            filterSummaryLabel.Text = parts.Count == 0
                ? "\u30D5\u30A3\u30EB\u30BF\u306A\u3057"
                : string.Join(" / ", parts);
        }

        private static string GetPeriodFilterText(ScheduleViewModel.DateRangeFilter filter)
        {
            return filter switch
            {
                ScheduleViewModel.DateRangeFilter.Upcoming => "\u4ECA\u5F8C\u306E\u8A66\u5408",
                ScheduleViewModel.DateRangeFilter.Past => "\u904E\u53BB\u306E\u8A66\u5408",
                _ => "\u3059\u3079\u3066"
            };
        }

        private static bool HasScheduleItems(
            System.Collections.Generic.IReadOnlyCollection<ScheduleFetcher.Item> div1,
            System.Collections.Generic.IReadOnlyCollection<ScheduleFetcher.Item> div2,
            System.Collections.Generic.IReadOnlyCollection<ScheduleFetcher.Item> div3,
            System.Collections.Generic.IReadOnlyCollection<ScheduleFetcher.Item> replacement,
            System.Collections.Generic.IReadOnlyCollection<ScheduleFetcher.Item> other)
        {
            return div1.Count > 0 || div2.Count > 0 || div3.Count > 0 || replacement.Count > 0 || other.Count > 0;
        }

        private void UpdateEmptyState()
        {
            if (_vm.FilteredItems.Count > 0)
            {
                emptyStatePanel.IsVisible = false;
                return;
            }

            if (_vm.GetCurrentDivisionItemCount() == 0)
            {
                emptyStateLabel.Text = string.IsNullOrWhiteSpace(_lastMessage)
                    ? "\u8868\u793A\u3067\u304D\u308B\u8A66\u5408\u30C7\u30FC\u30BF\u306F\u3042\u308A\u307E\u305B\u3093"
                    : "\u65E5\u7A0B\u30C7\u30FC\u30BF\u3092\u8868\u793A\u3067\u304D\u307E\u305B\u3093\u3067\u3057\u305F";
                emptyStatePanel.IsVisible = true;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_vm.TeamFilter) ||
                _vm.FavoriteTeamFilters.Count > 0 ||
                !string.IsNullOrWhiteSpace(_vm.VenueFilter) ||
                _vm.PeriodFilter != ScheduleViewModel.DateRangeFilter.All)
            {
                emptyStateLabel.Text = "\u8A72\u5F53\u3059\u308B\u8A66\u5408\u306F\u3042\u308A\u307E\u305B\u3093";
                emptyStatePanel.IsVisible = true;
                return;
            }

            emptyStateLabel.Text = "\u8868\u793A\u3067\u304D\u308B\u8A66\u5408\u30C7\u30FC\u30BF\u306F\u3042\u308A\u307E\u305B\u3093";
            emptyStatePanel.IsVisible = true;
        }

        private void ScrollScheduleToStart()
        {
            if (_vm.FilteredItems.FirstOrDefault() is { } first)
            {
                list.ScrollTo(first, position: ScrollToPosition.Start, animate: false);
            }
        }

        private void LoadFavoriteTeams()
        {
            var hasV2 = Preferences.ContainsKey(FavoriteTeamsKey);
            var loaded = FavoriteTeamLogic.Load(
                hasV2 ? Preferences.Get(FavoriteTeamsKey, "") : null,
                Preferences.Get(LegacyFavoriteTeamKey, ""));
            _favoriteTeams = loaded.Favorites;
            if (loaded.RequiresWriteBack)
            {
                Preferences.Set(FavoriteTeamsKey, FavoriteTeamLogic.Serialize(_favoriteTeams));
                Preferences.Remove(LegacyFavoriteTeamKey);
            }
        }

        private void UpdateFavoriteUi()
        {
            var selectedTeam = _vm.TeamFilter;
            var hasSelection = !string.IsNullOrWhiteSpace(selectedTeam);
            var isSelectedFavorite = hasSelection && IsFavoriteTeam(selectedTeam!, GetSelectedSeasonStartYear());
            var favoriteSummary = FavoriteTeamLogic.FormatFavoriteSummary(
                _favoriteTeams, selectedTeam, isSelectedFavorite);
            favoriteTeamLabel.Text = $"{(_isFavoriteFilterActive ? "★" : "☆")} {favoriteSummary}";
            favoriteTeamLabel.IsEnabled = _favoriteTeams.Count > 0;
            favoriteTeamLabel.Opacity = _favoriteTeams.Count > 0 ? 1 : 0.65;

            var quickFilterFavorites = _isFavoriteFilterActive && _vm.FavoriteTeamFilters.Count > 0
                ? _vm.FavoriteTeamFilters
                : _favoriteTeams;
            var quickFilterNames = quickFilterFavorites
                .Select(favorite => FindCurrentTeamAlias(favorite) ?? favorite)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            favoriteQuickFilterLabel.Text = $"{(_isFavoriteFilterActive ? "★" : "☆")} {string.Join("、", quickFilterNames)}";
            favoriteQuickFilterBorder.IsVisible = _favoriteTeams.Count > 0;
            favoriteQuickFilterBorder.IsEnabled = _favoriteTeams.Count > 0;
            favoriteQuickFilterBorder.Opacity = _favoriteTeams.Count > 0 ? 1 : 0.65;

            favoriteButton.Text = isSelectedFavorite ? "お気に入り解除" : "お気に入り登録";
            favoriteButton.IsEnabled = hasSelection;
        }

        private void ExitFavoriteFilterMode()
        {
            _isFavoriteFilterActive = false;
            _vm.FavoriteTeamFilters = Array.Empty<string>();
        }

        private string? FindCurrentTeamAlias(string? name, System.Collections.Generic.IReadOnlyList<string>? teams = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            teams ??= _vm.GetTeamsForPicker();
            var seasonYear = GetSelectedSeasonStartYear();
            return teams.FirstOrDefault(team => SeasonCatalog.AreSameTeamName(team, name, seasonYear));
        }

        private int GetSelectedSeasonStartYear()
        {
            var seasonYear = SeasonCatalog.ParseSeasonStartYear(_selectedSeasonKey);
            return seasonYear > 0 ? seasonYear : SeasonCatalog.CurrentSeasonStartYear;
        }

        private bool IsFavoriteTeam(string team, int seasonStartYear)
            => _favoriteTeams.Any(favorite => SeasonCatalog.AreSameTeamName(favorite, team, seasonStartYear));

        private System.Collections.Generic.IReadOnlyList<string> FindMatchingFavoritesForCurrentDivision()
        {
            var matches = _vm.CurrentDivision switch
            {
                1 => _vm.ItemsDiv1.ToList(),
                2 => _vm.ItemsDiv2.ToList(),
                3 => _vm.ItemsDiv3.ToList(),
                _ => new System.Collections.Generic.List<MatchItem>()
            };
            return FavoriteTeamLogic.FindMatchingFavorites(matches, _favoriteTeams);
        }

        private static string DivisionToCategory(int division) => division switch
        {
            1 => ScheduleViewModel.CategoryDiv1,
            2 => ScheduleViewModel.CategoryDiv2,
            3 => ScheduleViewModel.CategoryDiv3,
            _ => ScheduleViewModel.CategoryDiv1
        };

        private void NormalizeManualTeamSelection(string? preferredTeam)
        {
            if (!_teamFilterManuallySelected)
            {
                _vm.TeamFilter = null;
                return;
            }

            var matchingTeam = FindCurrentTeamAlias(preferredTeam);
            if (matchingTeam is null)
            {
                _teamFilterManuallySelected = false;
                _vm.TeamFilter = null;
                return;
            }

            _vm.TeamFilter = matchingTeam;
        }
        private async Task OpenWebAsync(string url)
        {
            if (!TryCreateHttpUri(url, out var uri))
            {
                await DisplayAlert("\u78BA\u8A8D", "\u3053\u306E\u30EA\u30F3\u30AF\u306F\u958B\u3051\u307E\u305B\u3093\u3002\u5B89\u5168\u306A http / https URL \u306E\u307F\u5BFE\u5FDC\u3057\u3066\u3044\u307E\u3059\u3002", "OK");
                return;
            }

            try
            {
                await Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(
                    uri,
                    Microsoft.Maui.ApplicationModel.BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u30EA\u30F3\u30AF\u3092\u958B\u3051\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private static bool TryCreateHttpUri(string? url, out Uri uri)
        {
            uri = null!;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                return false;
            }

            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            uri = parsed;
            return true;
        }

        private async Task DeleteCacheAsync()
        {
            try
            {
                var confirmed = await DisplayAlert(
                    "\u78BA\u8A8D",
                    "\u4FDD\u5B58\u6E08\u307F\u306E\u30AD\u30E3\u30C3\u30B7\u30E5\u3092\u524A\u9664\u3057\u307E\u3059\u3002\u3088\u308D\u3057\u3044\u3067\u3059\u304B\uFF1F",
                    "\u524A\u9664",
                    "\u30AD\u30E3\u30F3\u30BB\u30EB");
                if (!confirmed)
                {
                    return;
                }

                if (!await ScheduleCacheStore.DeleteAsync())
                {
                    await DisplayAlert("\u78BA\u8A8D", "\u30AD\u30E3\u30C3\u30B7\u30E5\u3092\u524A\u9664\u3067\u304D\u307E\u305B\u3093\u3067\u3057\u305F\u3002", "OK");
                    return;
                }

                _isShowingCache = false;
                cachePanel.IsVisible = false;
                await DisplayAlert("\u5B8C\u4E86", "\u30AD\u30E3\u30C3\u30B7\u30E5\u3092\u524A\u9664\u3057\u307E\u3057\u305F\u3002", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u30AD\u30E3\u30C3\u30B7\u30E5\u3092\u524A\u9664\u3067\u304D\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private static int GetSelectedIndex(System.Collections.Generic.IReadOnlyList<string> items, string? selectedValue)
        {
            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                return 0;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == selectedValue)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
