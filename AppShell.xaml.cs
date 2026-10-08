namespace OneRugbyNavi2;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(TeamListPage), typeof(TeamListPage));
		Routing.RegisterRoute(nameof(RankingPage), typeof(RankingPage));
		Routing.RegisterRoute(nameof(InfoPage), typeof(InfoPage));
		Routing.RegisterRoute(nameof(PlayersPage), typeof(PlayersPage));
	}
}

