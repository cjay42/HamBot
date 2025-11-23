using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HamBot
{
    internal class Program
    {
		private static IServiceProvider _services;

		private static IConfiguration _configuration = new ConfigurationBuilder()
				.AddEnvironmentVariables(prefix: "DC_")
				.AddJsonFile("appsettings.json", optional: true)
				.Build();

		private static readonly DiscordSocketConfig _socketConfig = new()
		{
			GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
			AlwaysDownloadUsers = true,
		};
		private static Task LogAsync(LogMessage msg)
		{
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
		}

		public static async Task Main()
		{
			_services = new ServiceCollection()
				.AddSingleton(_configuration)
				.AddSingleton(_socketConfig)
				.AddSingleton<DiscordSocketClient>()
				.AddSingleton<Modules.SSTV>()
				.BuildServiceProvider();

			_services.GetRequiredService<Modules.SSTV>();

			var client = _services.GetRequiredService<DiscordSocketClient>();

			client.Log += LogAsync;

			await client.LoginAsync(TokenType.Bot, _configuration["token"]);
			await client.StartAsync();

			await client.SetGameAsync($"Running on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}", null, ActivityType.CustomStatus);

			await Task.Delay(Timeout.Infinite);
		}
	}
}
