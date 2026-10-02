using Microsoft.Maui.ApplicationModel;

namespace OneRugbyNavi2;

public static class TeamOfficialPageNavigator
{
    private static readonly TeamCatalogFetcher CatalogFetcher = new();

    public static async Task OpenAsync(ContentPage page, string teamName)
    {
        try
        {
            var seasonYear = SeasonCatalog.CurrentSeasonStartYear;
            var catalog = await CatalogFetcher.GetAsync(seasonYear);
            if (!TeamOfficialPageLink.TryFindTeamUrl(catalog.Snapshot.Teams, teamName, seasonYear, out var teamUrl) ||
                !TeamOfficialPageLink.TryCreateUri(teamUrl, out var uri))
            {
                await page.DisplayAlert("確認", "チーム公式ページのURLを確認できません。", "OK");
                return;
            }

            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("確認", $"チーム公式ページを開けませんでした。\n{ex.Message}", "OK");
        }
    }
}
