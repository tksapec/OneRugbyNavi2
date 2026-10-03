using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;

namespace OneRugbyNavi2
{
    public sealed class MatchDetailPage : ContentPage
    {
        private const string FavoriteTeamKey = "FavoriteTeam";

        private readonly MatchItem _match;

        public MatchDetailPage(MatchItem match)
        {
            _match = match;
            BackgroundColor = PageStyles.Background;
            Shell.SetNavBarIsVisible(this, false);

            var root = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star }
                }
            };

            root.Children.Add(BuildHeader());
            root.Children.Add(new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16, 8, 16, 20),
                    Spacing = 14,
                    Children =
                    {
                        BuildHero(),
                        BuildInfoSection(),
                        BuildActionSection()
                    }
                }
            }.Row(1));

            Content = root;
        }

        private View BuildHeader()
        {
            return new Border
            {
                BackgroundColor = PageStyles.Surface,
                StrokeThickness = 0,
                Padding = new Thickness(12, 14, 16, 12),
                Content = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    ColumnSpacing = 10,
                    Children =
                    {
                        new Button
                        {
                            Text = "\u2190",
                            FontSize = 22,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = PageStyles.Navy,
                            BackgroundColor = Colors.Transparent,
                            Padding = new Thickness(8, 0),
                            WidthRequest = 44,
                            HeightRequest = 44
                        }.Apply(button => button.Clicked += async (_, _) => await Navigation.PopAsync()),
                        new VerticalStackLayout
                        {
                            Spacing = 0,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new Label
                                {
                                    Text = "\u8A66\u5408\u8A73\u7D30",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = PageStyles.Navy
                                },
                                new Label
                                {
                                    Text = $"{_match.Division}  {_match.Section}".Trim(),
                                    FontSize = 12,
                                    TextColor = PageStyles.Muted
                                }
                            }
                        }.Column(1)
                    }
                }
            };
        }

        private View BuildHero()
        {
            return Card(
                new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    },
                    RowSpacing = 12,
                    Children =
                    {
                        new HorizontalStackLayout
                        {
                            Spacing = 8,
                            Children =
                            {
                                Pill(_match.Division),
                                Pill(_match.Section)
                            }
                        },
                        new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = GridLength.Star },
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = GridLength.Star }
                            },
                            ColumnSpacing = 12,
                            Children =
                            {
                                BuildTeamBlock(_match.HomeTeam, _match.HomeLogoPath, _match.HasHomeLogo, _match.HomeBadgeText, TextAlignment.Start),
                                new Label
                                {
                                    Text = _match.ScoreText,
                                    FontSize = 26,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = PageStyles.Blue,
                                    HorizontalTextAlignment = TextAlignment.Center,
                                    VerticalTextAlignment = TextAlignment.Center
                                }.Column(1),
                                BuildTeamBlock(_match.AwayTeam, _match.AwayLogoPath, _match.HasAwayLogo, _match.AwayBadgeText, TextAlignment.End).Column(2)
                            }
                        }.Row(1),
                        new VerticalStackLayout
                        {
                            Spacing = 3,
                            Children =
                            {
                                new Label
                                {
                                    Text = $"{_match.MatchDate}  {_match.KickoffTime}".Trim(),
                                    FontSize = 16,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = PageStyles.Navy,
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                new Label
                                {
                                    Text = _match.VenueCompact,
                                    FontSize = 13,
                                    TextColor = PageStyles.Muted,
                                    HorizontalTextAlignment = TextAlignment.Center,
                                    LineBreakMode = LineBreakMode.WordWrap
                                }
                            }
                        }.Row(2)
                    }
                });
        }

        private View BuildInfoSection()
        {
            var rows = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    SectionTitle("\u8A66\u5408\u60C5\u5831"),
                    Row("Division", _match.Division),
                    Row("シーズン", _match.SeasonLabel),
                    Row("カテゴリ", _match.CategoryLabel),
                    Row("\u7BC0", _match.Section),
                    Row("試合コード", _match.MatchCode),
                    Row("\u30AB\u30F3\u30D5\u30A1\u30EC\u30F3\u30B9", _match.Conference),
                    Row("\u8A66\u5408\u65E5", _match.MatchDate),
                    Row("\u30AD\u30C3\u30AF\u30AA\u30D5\u6642\u523B", _match.KickoffTime),
                    Row("\u30DB\u30FC\u30E0\u30C1\u30FC\u30E0", _match.HomeTeam),
                    Row("\u30D3\u30B8\u30BF\u30FC\u30C1\u30FC\u30E0", _match.AwayTeam),
                    Row("\u30B9\u30B3\u30A2", _match.ScoreText),
                    Row("\u8A66\u5408\u72B6\u614B", _match.StatusText),
                    Row("\u90FD\u9053\u5E9C\u770C", _match.Prefecture),
                    Row("\u4F1A\u5834", _match.VenueDisplayName),
                    Row("放送・配信", _match.BroadcastText)
                }
            };

            return Card(rows);
        }

        private View BuildActionSection()
        {
            var content = new VerticalStackLayout { Spacing = 14 };
            var officialLinks = new VerticalStackLayout { Spacing = 8 };
            var hasOfficialLinks = false;

            if (!string.IsNullOrWhiteSpace(_match.MatchInfoUrl))
            {
                officialLinks.Children.Add(ActionButton("試合情報を開く", async () => await OpenWebAsync(_match.MatchInfoUrl)));
                hasOfficialLinks = true;
            }

            if (!string.IsNullOrWhiteSpace(_match.PreviewUrl))
            {
                officialLinks.Children.Add(ActionButton("公式プレビューを開く", async () => await OpenWebAsync(_match.PreviewUrl)));
                hasOfficialLinks = true;
            }

            if (!string.IsNullOrWhiteSpace(_match.ReportUrl))
            {
                officialLinks.Children.Add(ActionButton("公式レポートを開く", async () => await OpenWebAsync(_match.ReportUrl)));
                hasOfficialLinks = true;
            }

            if (hasOfficialLinks)
            {
                content.Children.Add(SectionTitle("公式リンク"));
                content.Children.Add(officialLinks);
            }

            var quickActions = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 8,
                RowSpacing = 8
            };
            void AddQuickAction(string text, Func<System.Threading.Tasks.Task> action)
            {
                var index = quickActions.Children.Count;
                var row = index / 2;
                if (row >= quickActions.RowDefinitions.Count)
                {
                    quickActions.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                }

                var button = ActionButton(text, action);
                button.MinimumHeightRequest = 52;
                button.FontSize = 12;
                Grid.SetColumn(button, index % 2);
                Grid.SetRow(button, row);
                quickActions.Children.Add(button);
            }

            if (!string.IsNullOrWhiteSpace(_match.VenueDisplayName))
            {
                AddQuickAction("地図", OpenMapAsync);
            }

            if (ScheduleViewModel.TryGetMatchStart(_match, out _))
            {
                AddQuickAction("カレンダー", AddToCalendarAsync);
            }

            if (!string.IsNullOrWhiteSpace(_match.HomeTeam))
            {
                AddQuickAction("ホームをお気に入り", async () => await SaveFavoriteTeamAsync(_match.HomeTeam));
            }

            if (!string.IsNullOrWhiteSpace(_match.AwayTeam))
            {
                AddQuickAction("ビジターをお気に入り", async () => await SaveFavoriteTeamAsync(_match.AwayTeam));
            }

            AddQuickAction("共有", ShareAsync);
            AddQuickAction("順位表", async () => await OpenWebAsync("https://league-one.jp/standings/"));
            AddQuickAction("公式ランキング", async () => await Shell.Current.GoToAsync(nameof(RankingPage)));

            content.Children.Add(SectionTitle("その他の操作"));
            content.Children.Add(quickActions);
            return Card(content);
        }

        private static Border Card(View content)
        {
            return new Border
            {
                Padding = 16,
                BackgroundColor = PageStyles.Surface,
                Stroke = PageStyles.Stroke,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Content = content
            };
        }

        private static Label SectionTitle(string text)
        {
            return new Label
            {
                Text = text,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = PageStyles.Navy
            };
        }

        private static Border Pill(string text)
        {
            return new Border
            {
                Padding = new Thickness(10, 4),
                BackgroundColor = PageStyles.InformationBackground,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = new Label
                {
                    Text = string.IsNullOrWhiteSpace(text) ? "-" : text,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = PageStyles.Blue
                }
            };
        }

        private static View Row(string label, string value)
        {
            return new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(118) },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                Children =
                {
                    new Label
                    {
                        Text = label,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = PageStyles.Muted,
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
                        TextColor = PageStyles.Navy,
                        LineBreakMode = LineBreakMode.WordWrap
                    }.Column(1)
                }
            };
        }

        private static Button ActionButton(string text, Func<System.Threading.Tasks.Task> action)
        {
            var button = PageStyles.SecondaryButton(text);
            button.MinimumHeightRequest = 48;
            button.Clicked += async (_, _) => await action();
            return button;
        }

        private View BuildTeamBlock(string teamName, string? logoPath, bool hasLogo, string badgeText, TextAlignment textAlignment)
        {
            var layout = new VerticalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = textAlignment == TextAlignment.End ? LayoutOptions.End : LayoutOptions.Start
            };

            var mark = BuildTeamMark(logoPath, hasLogo, badgeText);
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await TeamOfficialPageNavigator.OpenAsync(this, teamName);
            mark.GestureRecognizers.Add(tap);
            SemanticProperties.SetHint(mark, "タップすると公式チームページを開きます");
            layout.Children.Add(mark);
            layout.Children.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(teamName) ? "-" : teamName,
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = PageStyles.Navy,
                HorizontalTextAlignment = textAlignment,
                LineBreakMode = LineBreakMode.WordWrap
            });

            return layout;
        }

        private static View BuildTeamMark(string? logoPath, bool hasLogo, string badgeText)
        {
            return new Border
            {
                WidthRequest = 60,
                HeightRequest = 60,
                Padding = 8,
                BackgroundColor = PageStyles.Background,
                Stroke = PageStyles.Stroke,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 30 },
                HorizontalOptions = LayoutOptions.Center,
                Content = hasLogo
                    ? new Image
                    {
                        Source = logoPath,
                        Aspect = Aspect.AspectFit,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                    : new Label
                    {
                        Text = string.IsNullOrWhiteSpace(badgeText) ? "?" : badgeText,
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = PageStyles.Blue,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    }
            };
        }

        private async System.Threading.Tasks.Task OpenWebAsync(string url)
        {
            if (!TryCreateHttpUri(url, out var uri))
            {
                await DisplayAlert("\u78BA\u8A8D", "\u3053\u306E\u30EA\u30F3\u30AF\u306F\u958B\u3051\u307E\u305B\u3093\u3002\u5B89\u5168\u306A http / https URL \u306E\u307F\u5BFE\u5FDC\u3057\u3066\u3044\u307E\u3059\u3002", "OK");
                return;
            }

            try
            {
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u30EA\u30F3\u30AF\u3092\u958B\u3051\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private async System.Threading.Tasks.Task OpenMapAsync()
        {
            var query = Uri.EscapeDataString($"{_match.Prefecture} {_match.VenueDisplayName}".Trim());
            var geoUri = $"geo:0,0?q={query}";

            try
            {
                if (await Launcher.CanOpenAsync(geoUri) && await Launcher.OpenAsync(geoUri))
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                var mapsUrl = $"https://www.google.com/maps/search/?api=1&query={query}";
                var opened = await Launcher.OpenAsync(mapsUrl);
                if (!opened)
                {
                    await DisplayAlert("\u78BA\u8A8D", "\u5730\u56F3\u691C\u7D22\u3092\u958B\u3051\u307E\u305B\u3093\u3067\u3057\u305F\u3002", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u5730\u56F3\u691C\u7D22\u3092\u958B\u3051\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private async System.Threading.Tasks.Task AddToCalendarAsync()
        {
            try
            {
                if (!ScheduleViewModel.TryGetMatchStart(_match, out var start))
                {
                    await DisplayAlert("\u78BA\u8A8D", "\u65E5\u4ED8\u307E\u305F\u306F\u6642\u523B\u304C\u672A\u5B9A\u306E\u305F\u3081\u3001\u30AB\u30EC\u30F3\u30C0\u30FC\u306B\u8FFD\u52A0\u3067\u304D\u307E\u305B\u3093\u3002", "OK");
                    return;
                }

#if ANDROID
                var begin = new DateTimeOffset(start);
                var end = begin.AddHours(2);
                var intent = new Android.Content.Intent(Android.Content.Intent.ActionInsert)
                    .SetData(Android.Provider.CalendarContract.Events.ContentUri)
                    .PutExtra(Android.Provider.CalendarContract.ExtraEventBeginTime, begin.ToUnixTimeMilliseconds())
                    .PutExtra(Android.Provider.CalendarContract.ExtraEventEndTime, end.ToUnixTimeMilliseconds())
                    .PutExtra(Android.Provider.CalendarContract.Events.InterfaceConsts.Title, _match.HomeVsAway)
                    .PutExtra(Android.Provider.CalendarContract.Events.InterfaceConsts.EventLocation, _match.VenueCompact)
                    .PutExtra(Android.Provider.CalendarContract.Events.InterfaceConsts.Description, BuildCalendarNote());

                var activity = Platform.CurrentActivity;
                if (activity != null)
                {
                    activity.StartActivity(intent);
                }
                else
                {
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    Android.App.Application.Context.StartActivity(intent);
                }
#else
                await DisplayAlert("\u78BA\u8A8D", "\u3053\u306E\u74B0\u5883\u3067\u306F\u30AB\u30EC\u30F3\u30C0\u30FC\u8FFD\u52A0\u3092\u5229\u7528\u3067\u304D\u307E\u305B\u3093\u3002", "OK");
#endif
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u30AB\u30EC\u30F3\u30C0\u30FC\u3092\u958B\u3051\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private async System.Threading.Tasks.Task ShareAsync()
        {
            try
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = _match.HomeVsAway,
                    Text = BuildShareText()
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u5171\u6709\u3092\u958B\u59CB\u3067\u304D\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private async System.Threading.Tasks.Task SaveFavoriteTeamAsync(string team)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(team))
                {
                    await DisplayAlert("\u78BA\u8A8D", "\u304A\u6C17\u306B\u5165\u308A\u306B\u767B\u9332\u3067\u304D\u308B\u30C1\u30FC\u30E0\u540D\u304C\u3042\u308A\u307E\u305B\u3093\u3002", "OK");
                    return;
                }

                Preferences.Set(FavoriteTeamKey, team);
                await DisplayAlert("\u5B8C\u4E86", $"\u304A\u6C17\u306B\u5165\u308A\u306B\u767B\u9332\u3057\u307E\u3057\u305F\u3002\n{team}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("\u78BA\u8A8D", $"\u304A\u6C17\u306B\u5165\u308A\u3092\u767B\u9332\u3067\u304D\u307E\u305B\u3093\u3067\u3057\u305F\u3002\n{ex.Message}", "OK");
            }
        }

        private string BuildCalendarNote()
        {
            var officialUrl = string.IsNullOrWhiteSpace(_match.MatchInfoUrl) ? _match.ReportUrl : _match.MatchInfoUrl;
            return string.Join(Environment.NewLine, new[]
            {
                _match.Division,
                _match.Section,
                _match.StatusText,
                _match.BroadcastText,
                officialUrl
            }.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private string BuildShareText()
        {
            var lines = new List<string>
            {
                _match.Section,
                _match.HomeVsAway,
                $"{_match.MatchDate} {_match.KickoffTime}".Trim(),
                $"\u4F1A\u5834: {_match.VenueCompact}"
            };

            if (_match.HasResult)
            {
                lines.Add($"\u8A66\u5408\u7D50\u679C: {_match.ScoreText}");
            }

            var officialUrl = string.IsNullOrWhiteSpace(_match.MatchInfoUrl) ? _match.ReportUrl : _match.MatchInfoUrl;
            if (!string.IsNullOrWhiteSpace(officialUrl))
            {
                lines.Add($"\u516C\u5F0F\u60C5\u5831: {officialUrl}");
            }

            if (!string.IsNullOrWhiteSpace(_match.BroadcastText))
            {
                lines.Add($"放送・配信: {_match.BroadcastText}");
            }

            return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
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
    }
}
