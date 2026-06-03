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
        private MatchItem? _nextMatch;
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

            list.ItemsSource = _vm.FilteredItems;
            periodPicker.ItemsSource = new[] { "\u3059\u3079\u3066", "\u4ECA\u5F8C\u306E\u8A66\u5408", "\u904E\u53BB\u306E\u8A66\u5408" };
            periodPicker.SelectedIndex = 0;
            LoadFavoriteTeam();
            UpdateFavoriteUi();
            UpdateTabVisual(1);
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
                    UpdateEmptyState();
                    UpdateNextMatchCard();
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
                    SetMessage(FetchedOfficialScheduleMessage, true, autoHide: true);
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
                        UpdateNextMatchCard();
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

                UpdateLastUpdatedLabel();

                UpdateEmptyState();
                UpdateNextMatchCard();
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
                UpdateNextMatchCard();
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
            UpdateEmptyState();
            UpdateNextMatchCard();
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
            button.BackgroundColor = active ? Color.FromArgb("#0057B8") : Colors.Transparent;
            button.TextColor = active ? Colors.White : Color.FromArgb("#26364F");
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
            if (_seasonOptions.Count == 0)
            {
                _suppressPickerEvents = true;
                seasonPicker.ItemsSource = Array.Empty<string>();
                seasonPicker.SelectedIndex = -1;
                _suppressPickerEvents = false;
            }
        }

        private void RefreshCategoryTabs()
        {
            btnReplacement.IsVisible = _vm.GetCategoryItemCount(ScheduleViewModel.CategoryReplacement) > 0;
            btnOther.IsVisible = _vm.GetCategoryItemCount(ScheduleViewModel.CategoryOther) > 0;
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
            UpdateEmptyState();
            UpdateNextMatchCard();
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
            UpdateEmptyState();
            UpdateNextMatchCard();
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
            UpdateEmptyState();
            UpdateNextMatchCard();
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
            UpdateEmptyState();
            UpdateNextMatchCard();
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

        private async void OnNextMatchTapped(object sender, TappedEventArgs e)
        {
            if (_nextMatch == null)
            {
                return;
            }

            try
            {
                await NavigateToMatchAsync(_nextMatch);
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u8A66\u5408\u8A73\u7D30\u3092\u958B\u3051\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
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
                UpdateNextMatchCard();
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
            UpdateNextMatchCard();
            UpdateFilterSummaryUi();
            await DisplayAlert("\u5B8C\u4E86", $"\u304A\u6C17\u306B\u5165\u308A\u306B\u767B\u9332\u3057\u307E\u3057\u305F\u3002\n{team}", "OK");
        }

        private async void OnStandingsClicked(object sender, EventArgs e)
        {
            await OpenWebAsync("https://league-one.jp/standings/");
        }

        private async void OnInfoClicked(object sender, EventArgs e)
        {
            var lastUpdated = _databaseBuildTimestampText;
            var cacheState = _isShowingCache ? "\u524D\u56DE\u53D6\u5F97\u30C7\u30FC\u30BF\u3092\u8868\u793A\u4E2D" : "\u6700\u65B0\u53D6\u5F97\u30C7\u30FC\u30BF\u3092\u8868\u793A\u4E2D";
            string body =
"One Rugby Navi2 \u306F JAPAN RUGBY LEAGUE ONE \u516C\u5F0F\u30B5\u30A4\u30C8\u306E\u516C\u958B\u60C5\u5831\u3092\u3082\u3068\u306B\u3001\u65E5\u7A0B\u3068\u7D50\u679C\u3092\u8868\u793A\u3057\u307E\u3059\u3002\n\n" +
"\u672C\u30A2\u30D7\u30EA\u306F\u516C\u5F0F\u30A2\u30D7\u30EA\u3067\u306F\u3042\u308A\u307E\u305B\u3093\u3002\n" +
"\u004A\u0041\u0050\u0041\u004E \u0052\u0055\u0047\u0042\u0059 \u004C\u0045\u0041\u0047\u0055\u0045 \u004F\u004E\u0045 \u304A\u3088\u3073\u5404\u30C1\u30FC\u30E0\u3068\u306F\u95A2\u4FC2\u3042\u308A\u307E\u305B\u3093\u3002\n\n" +
"\u901A\u4FE1\u306B\u5931\u6557\u3057\u305F\u5834\u5408\u306F\u3001\u4FDD\u5B58\u6E08\u307F\u306E\u524D\u56DE\u53D6\u5F97\u30C7\u30FC\u30BF\u3092\u8868\u793A\u3059\u308B\u3053\u3068\u304C\u3042\u308A\u307E\u3059\u3002\n" +
$"\u6700\u7D42\u66F4\u65B0: {lastUpdated}\n" +
$"\u8868\u793A\u72B6\u614B: {cacheState}\n" +
$"D1: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv1)}\u4EF6 / D2: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv2)}\u4EF6 / D3: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryDiv3)}\u4EF6 / 入替戦: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryReplacement)}件 / その他: {_vm.GetCategoryItemCount(ScheduleViewModel.CategoryOther)}件\n\n" +
$"\u5BFE\u8C61\u30B7\u30FC\u30BA\u30F3: {_selectedSeasonLabel}\n" +
"\u53D6\u5F97\u5143: JAPAN RUGBY LEAGUE ONE \u516C\u5F0F\u30B5\u30A4\u30C8\n\n" +
"\u4F7F\u7528\u30E9\u30A4\u30D6\u30E9\u30EA\u3068\u30E9\u30A4\u30BB\u30F3\u30B9\u306E\u8A73\u7D30\u306F\u914D\u5E03\u7269\u5185\u306E LICENSES.txt \u3092\u53C2\u7167\u3057\u3066\u304F\u3060\u3055\u3044\u3002";

            await DisplayAlert("\u60C5\u5831", body, "OK");
            var action = await DisplayActionSheet("\u64CD\u4F5C", "\u9589\u3058\u308B", null, "\u9806\u4F4D\u8868", "\u500B\u4EBA\u30E9\u30F3\u30AD\u30F3\u30B0", "\u30AD\u30E3\u30C3\u30B7\u30E5\u524A\u9664");
            if (action == "\u9806\u4F4D\u8868")
            {
                await OpenWebAsync("https://league-one.jp/standings/");
            }
            else if (action == "\u500B\u4EBA\u30E9\u30F3\u30AD\u30F3\u30B0")
            {
                await OpenWebAsync("https://league-one.jp/ranking/");
            }
            else if (action == "\u30AD\u30E3\u30C3\u30B7\u30E5\u524A\u9664")
            {
                await DeleteCacheAsync();
            }
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

        private void UpdateNextMatchCard()
        {
            _nextMatch = _vm.GetNextMatch(_favoriteTeam);
            if (_nextMatch == null)
            {
                nextMetaLabel.Text = "";
                nextHomeLabel.Text = "\u4ECA\u5F8C\u306E\u8A66\u5408\u306F\u3042\u308A\u307E\u305B\u3093";
                nextAwayLabel.Text = "";
                nextScoreLabel.Text = "";
                nextKickoffLabel.Text = "";
                nextDateLabel.Text = "\u6761\u4EF6\u3092\u5909\u3048\u308B\u3068\u8868\u793A\u3067\u304D\u308B\u5834\u5408\u304C\u3042\u308A\u307E\u3059";
                nextVenueLabel.Text = "";
                return;
            }

            nextMetaLabel.Text = $"{_nextMatch.Division}  {_nextMatch.Section}";
            nextHomeLabel.Text = _nextMatch.HomeTeam;
            nextAwayLabel.Text = _nextMatch.AwayTeam;
            nextScoreLabel.Text = "vs";
            nextKickoffLabel.Text = "";
            nextDateLabel.Text = $"{_nextMatch.MatchDate}  {_nextMatch.KickoffTime}".Trim();
            nextVenueLabel.Text = _nextMatch.VenueCompact;
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

            favoriteTeamLabel.Text = $"\u304A\u6C17\u306B\u5165\u308A: {_favoriteTeam}";
            favoriteButton.Text = "\u304A\u6C17\u306B\u5165\u308A\u89E3\u9664";
            favoriteChipLabel.Text = $"\u2605 {_favoriteTeam}";
        }

        private void ApplyTeamFilterForCurrentDivision(string? preferredTeam, bool allowFavoriteFallback)
        {
            var teams = _vm.GetTeamsForPicker();
            if (!string.IsNullOrWhiteSpace(preferredTeam) && teams.Contains(preferredTeam))
            {
                _vm.TeamFilter = preferredTeam;
                _vm.ApplyFilters();
                return;
            }

            if (allowFavoriteFallback &&
                !string.IsNullOrWhiteSpace(_favoriteTeam) &&
                teams.Contains(_favoriteTeam))
            {
                _vm.TeamFilter = _favoriteTeam;
                _vm.ApplyFilters();
                return;
            }

            _vm.TeamFilter = null;
            _vm.ApplyFilters();
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

