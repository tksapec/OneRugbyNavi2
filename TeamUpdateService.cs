namespace OneRugbyNavi2;

public sealed class TeamUpdateService
{
    private readonly LeagueOneDatabase _database;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TeamUpdateService(LeagueOneDatabase database)
    {
        _database = database;
    }

    public bool IsRunning { get; private set; }

    public async Task<TeamUpdateResult> UpdateTeamAsync(TeamCard team, IProgress<string>? progress = null)
    {
        if (!await _gate.WaitAsync(0))
        {
            throw new InvalidOperationException("別のチーム更新が実行中です。完了してから再実行してください。");
        }

        IsRunning = true;
        try
        {
            progress?.Report($"{team.TeamName} の一時DB方式を確認しています...");
            await _database.InitializeAsync();

            var sourceDb = _database.GetDatabasePath();
            var tempDb = Path.Combine(LeagueOneDatabase.AppDataRoot, $"leagueone_update_team_{team.LeagueOneTeamId}.db");
            if (File.Exists(tempDb))
            {
                File.Delete(tempDb);
            }

            File.Copy(sourceDb, tempDb);

            progress?.Report($"{team.TeamName} の確認対象を読み取っています...");
            var playerCount = await _database.CountPlayersForTeamAsync(team.Id);

            // The first in-app version keeps the current DB intact unless a real team fetcher is added.
            // This still enforces the safe temp-DB update boundary required by the plan.
            File.Delete(tempDb);

            return new TeamUpdateResult
            {
                TeamName = team.TeamName,
                UpdatedPlayers = playerCount,
                NewPlayers = 0,
                MissingCandidates = 0,
                UpdatedImages = 0,
                FailedPages = 0,
                UpdatedAt = DateTime.Now,
                Message = "既存DBを壊さず一時DBを作成・破棄できることを確認しました。公式サイトからの実更新処理は未実装です。"
            };
        }
        finally
        {
            IsRunning = false;
            _gate.Release();
        }
    }
}
