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
        private const string FavoriteTeamKey = "FavoriteTeam";

        private bool _hasInitialized;
        private bool _isRefreshing;
        private bool _suppressPickerEvents;
        private bool _isShowingCache;
        private bool _isFilterExpanded;
        private bool _teamFilterManuallySelected;
        private string? _lastMessage;
        private string _favoriteTeam = "";
        private string _databaseBuildTimestampText = "-";
        private string _selectedSeasonKey = "";
        private string _selectedSeasonLabel = "";
        private readonly System.Collections.Generic.List<ScheduleFetcher.SeasonOption> _seasonOptions = new();
        private CancellationTokenSource? _messageHideCts;

        public MainPage()
        {
            InitializeComponent();

            UpdateSeasonTitle();

            list.ItemsSource = _vm.FilteredItems;
            periodPicker.ItemsSource = new[] { "\u3059\u3079\u3066", "\u4ECA\u5F8C\u306E\u8A66\u5408", "\u904E\u53BB\u306E\u8A66\u5408" };
            periodPicker.SelectedIndex = 0;
            LoadFavoriteTeam();
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
                var previousFavorite = _favoriteTeam;
                LoadFavoriteTeam();
                UpdateFavoriteUi();
                if (previousFavorite != _favoriteTeam && !_teamFilterManuallySelected)
                {
                    ApplyTeamFilterForCurrentDivision(null, allowFavoriteFallback: true);
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
            await RefreshDataAsync(showSuccessMessage: false);
        }

        private async Task RefreshDataAsync(bool showSuccessMessage)
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            SetLoading(true);
            SetMessage("", false);
            cachePanel.IsVisible = false;

            try
            {
                var currentCategory = _vm.CurrentCategory;
                var selectedTeam = _vm.TeamFilter;
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
                    await ScheduleCacheStore.SaveAsync(_selectedSeasonKey, _selectedSeasonLabel, div1, div2, div3, replacement, other, fetchedAt);
                    _databaseBuildTimestampText = fetchedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
                    var statusMessage = fetchResult?.DataConsistencyWarning is { Length: > 0 } warning
                        ? $"{FetchedOfficialScheduleMessage}\n{warning}"
                        : FetchedOfficialScheduleMessage;
                    SetMessage(statusMessage, true, autoHide: fetchResult?.DataConsistencyWarning is null);
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
                        _vm.VenueFilter = null;
                        RefreshPickers(preserveSelection: false);
                        _databaseBuildTimestampText = "-";
                        UpdateLastUpdatedLabel();
                        SetMessage(FetchFailedMessage, true);
                        UpdateEmptyState();
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
                _vm.SetSource(currentCategory);
                if (_vm.GetCurrentDivisionItemCount() == 0)
                {
                    _vm.SetSource(ScheduleViewModel.CategoryDiv1);
                }

                bool canPreserveVenue = !string.IsNullOrWhiteSpace(selectedVenue) &&
                    _vm.GetVenuesForPicker().Contains(selectedVenue);

                NormalizeManualTeamSelection(selectedTeam);
                ApplyTeamFilterForCurrentDivision(
                    selectedTeam,
                    allowFavoriteFallback: !_teamFilterManuallySelected || string.IsNullOrWhiteSpace(selectedTeam));
                _vm.VenueFilter = canPreserveVenue ? selectedVenue : null;
                _vm.PeriodFilter = selectedPeriod;
                _vm.ApplyFilters();
                RefreshCategoryTabs();
                RefreshPickers(preserveSelection: true);
                ScrollScheduleToStart();

                UpdateLastUpdatedLabel();

                UpdateEmptyState();
                UpdateFilterSummaryUi();
            }
            catch
            {
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
                SetMessage(FetchFailedMessage, true);
                UpdateEmptyState();
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
            _vm.SetSource(category);
            NormalizeManualTeamSelection(_vm.TeamFilter);
            ApplyTeamFilterForCurrentDivision(_teamFilterManuallySelected ? _vm.TeamFilter : null, allowFavoriteFallback: !_teamFilterManuallySelected);
            UpdateTabVisual(_vm.CurrentCategory);
            RefreshPickers(preserveSelection: true);
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFilterSummaryUi();
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
            await RefreshDataAsync(showSuccessMessage: true);
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
            _vm.TeamFilter = string.IsNullOrWhiteSpace(value) ? null : value;
            _teamFilterManuallySelected = !string.IsNullOrWhiteSpace(_vm.TeamFilter);
            _vm.ApplyFilters();
            ScrollScheduleToStart();
            UpdateEmptyState();
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
            _vm.TeamFilter = null;
            _vm.VenueFilter = null;
            _vm.PeriodFilter = ScheduleViewModel.DateRangeFilter.All;
            _teamFilterManuallySelected = false;
            _vm.ApplyFilters();
            RefreshPickers(preserveSelection: true);
            ScrollScheduleToStart();
            UpdateEmptyState();
            UpdateFilterSummaryUi();
        }

        private void OnToggleFiltersClicked(object sender, EventArgs e)
        {
            _isFilterExpanded = !_isFilterExpanded;
            UpdateFilterPanelUi();
        }

        private async void OnMatchSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not MatchItem match)
            {
                return;
            }

            list.SelectedItem = null;
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

        private async void OnFavoriteClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_favoriteTeam))
            {
                Preferences.Remove(FavoriteTeamKey);
                _favoriteTeam = "";
                UpdateFavoriteUi();
                UpdateFilterSummaryUi();
                await DisplayAlert("\u5B8C\u4E86", "\u304A\u6C17\u306B\u5165\u308A\u3092\u89E3\u9664\u3057\u307E\u3057\u305F\u3002", "OK");
                return;
            }

            var team = _vm.TeamFilter;
            if (string.IsNullOrWhiteSpace(team))
            {
                await DisplayAlert("\u78BA\u8A8D", "\u304A\u6C17\u306B\u5165\u308A\u306B\u767B\u9332\u3059\u308B\u30C1\u30FC\u30E0\u3092\u9078\u629E\u3057\u3066\u304F\u3060\u3055\u3044\u3002", "OK");
                return;
            }

            Preferences.Set(FavoriteTeamKey, team);
            _favoriteTeam = team;
            UpdateFavoriteUi();
            UpdateFilterSummaryUi();
            await DisplayAlert("\u5B8C\u4E86", $"\u304A\u6C17\u306B\u5165\u308A\u306B\u767B\u9332\u3057\u307E\u3057\u305F\u3002\n{team}", "OK");
        }

        private async void OnStandingsClicked(object sender, EventArgs e)
        {
            await OpenWebAsync("https://league-one.jp/standings/");
        }

        private async void OnInfoClicked(object sender, EventArgs e)
        {
            menuOverlay.IsVisible = true;
            Shell.SetTabBarIsVisible(this, false);
        }

        private void CloseMenuOverlay()
        {
            if (!menuOverlay.IsVisible)
            {
                return;
            }

            menuOverlay.IsVisible = false;
            Shell.SetTabBarIsVisible(this, true);
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
                $"D1: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv1)}件 / " +
                $"D2: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv2)}件 / " +
                $"D3: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv3)}件 / " +
                $"入替戦: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryReplacement)}件 / " +
                $"その他: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryOther)}件\n\n" +
                "取得元: JAPAN RUGBY LEAGUE ONE公式サイト\n" +
                "使用ライブラリとライセンスの詳細は、配布物内のLICENSES.txtを参照してください。";
            await DisplayAlert("更新状況", body, "OK");
        }

        private async void OnMenuStandingsClicked(object sender, EventArgs e)
        {
            CloseMenuOverlay();
            await OpenWebAsync("https://league-one.jp/standings/");
        }

        private async void OnMenuRankingClicked(object sender, EventArgs e)
        {
            CloseMenuOverlay();
            await Shell.Current.GoToAsync("//RankingPage");
        }

        private async void OnMenuAboutClicked(object sender, EventArgs e)
        {
            CloseMenuOverlay();
            await Shell.Current.GoToAsync("//InfoPage");
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

        private void SetMessage(string message, bool visible, bool autoHide = false)
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

        private void LoadFavoriteTeam()
        {
            _favoriteTeam = Preferences.Get(FavoriteTeamKey, "");
        }

        private void UpdateFavoriteUi()
        {
            if (string.IsNullOrWhiteSpace(_favoriteTeam))
            {
                favoriteTeamLabel.Text = "\u304A\u6C17\u306B\u5165\u308A: \u672A\u767B\u9332";
                favoriteButton.Text = "\u304A\u6C17\u306B\u5165\u308A\u767B\u9332";
                favoriteChipLabel.Text = "\u2605 \u672A\u767B\u9332";
                return;
            }

            var displayFavorite = FindCurrentTeamAlias(_favoriteTeam) ?? _favoriteTeam;
            favoriteTeamLabel.Text = $"\u304A\u6C17\u306B\u5165\u308A: {displayFavorite}";
            favoriteButton.Text = "\u304A\u6C17\u306B\u5165\u308A\u89E3\u9664";
            favoriteChipLabel.Text = $"\u2605 {displayFavorite}";
        }

        private void ApplyTeamFilterForCurrentDivision(string? preferredTeam, bool allowFavoriteFallback)
        {
            var teams = _vm.GetTeamsForPicker();
            var matchingPreferred = FindCurrentTeamAlias(preferredTeam, teams);
            if (!string.IsNullOrWhiteSpace(matchingPreferred))
            {
                _vm.TeamFilter = matchingPreferred;
                _vm.ApplyFilters();
                return;
            }

            var matchingFavorite = allowFavoriteFallback ? FindCurrentTeamAlias(_favoriteTeam, teams) : null;
            if (!string.IsNullOrWhiteSpace(matchingFavorite))
            {
                _vm.TeamFilter = matchingFavorite;
                _vm.ApplyFilters();
                return;
            }

            _vm.TeamFilter = null;
            _vm.ApplyFilters();
        }

        private string? FindCurrentTeamAlias(string? name, System.Collections.Generic.IReadOnlyList<string>? teams = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            teams ??= _vm.GetTeamsForPicker();
            var seasonYear = SeasonCatalog.ParseSeasonStartYear(_selectedSeasonKey);
            if (seasonYear <= 0) seasonYear = SeasonCatalog.CurrentSeasonStartYear;
            return teams.FirstOrDefault(team => SeasonCatalog.AreSameTeamName(team, name, seasonYear));
        }

        private void NormalizeManualTeamSelection(string? preferredTeam)
        {
            if (!_teamFilterManuallySelected || string.IsNullOrWhiteSpace(preferredTeam))
            {
                return;
            }

            if (!_vm.GetTeamsForPicker().Contains(preferredTeam))
            {
                _teamFilterManuallySelected = false;
            }
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
