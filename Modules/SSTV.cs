using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using HamBot.Helper;

namespace HamBot.Modules
{
	internal class SSTVPayload
	{
		public string mode { get; set; } = "";
		public int freq { get; set; }
		public string sstvMode { get; set; } = "";
		public string file { get; set; } = "";
		public int width { get; set; }
		public int height { get; set; }
		public int line { get; set; }
	}
	internal class SSTV
	{
		private DiscordSocketClient _discordClient;
		private IConfiguration _configuration;
		private List<SocketGuildChannel> _channels;

		private IMqttClient? _mqttClient;

		private string[] ignoredModes = 
		[
			"PD 50",
			"PD 290",
			"PD 120",
			"PD 180",
			"PD 240",
			"PD 160",
			"PD 90",
		];

		public SSTV(DiscordSocketClient discordClient, IConfiguration configuration)
		{
			_configuration = configuration;
			_discordClient = discordClient;
			_channels = new List<SocketGuildChannel>();

			_discordClient.Ready += BeginListen;
			Console.WriteLine("SSTV service created");
		}

		private async Task RecievedImage(string jsonPayload)
		{
			SSTVPayload? sstvPayload = JsonSerializer.Deserialize<SSTVPayload>(jsonPayload);
			if (sstvPayload is not null && !ignoredModes.Contains(sstvPayload.sstvMode))
			{
				//download file from owrx server
				HttpClient httpClient = new HttpClient();
				try
				{
					var imageBytes = await httpClient.GetByteArrayAsync($"{_configuration["owrx"]}/files/{sstvPayload.file}");

					double entropy = NoiseDetector.GetEntropy(imageBytes);
					bool noisy = entropy < 7.05;

					if (!noisy)
					{
						using (var stream = new MemoryStream(imageBytes))
						{
							foreach (var channel in _channels)
							{
								if (channel is SocketTextChannel textChannel)
								{
									var embed = new EmbedBuilder()
										.WithImageUrl($"attachment://{sstvPayload.file}")
										.WithDescription($"**Mode**: {sstvPayload.sstvMode}\n**Frequency**: {sstvPayload.freq / 1000000.0:F3} MHz\n**Dimensions**: {sstvPayload.width}x{sstvPayload.height}\n**Entropy**: {entropy:F4} bits\n**Noisy**: {noisy}")
										.WithColor(Color.Blue)
										.WithTimestamp(DateTimeOffset.Now)
										.Build();
									await textChannel.SendFileAsync(stream, sstvPayload.file, embed: embed);
								}

							}
						}
					}
					else
					{
						Console.WriteLine($"ignored noisy image with entropy of {entropy:F3} bits..");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("error on image handling:");
					Console.WriteLine(ex.ToString());
				}
			}
		}

		private async Task StartMQTT()
		{
			var mqttFactory = new MqttClientFactory();

			_mqttClient = mqttFactory.CreateMqttClient();
			var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer(_configuration["mqttBroker"]).Build();

			_mqttClient.ApplicationMessageReceivedAsync += async e =>
			{
				var message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
				//Console.WriteLine($"Received application message: {message}");
				if (message.Contains("file"))
				{
					await RecievedImage(message);
				}
				//return Task.CompletedTask;
			};

			await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

			var topicFilter = mqttFactory.CreateTopicFilterBuilder().WithTopic(_configuration["sstvTopic"]).WithAtLeastOnceQoS();

			var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build();

			var response = await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

			Console.WriteLine("MQTT client subscribed to topic.");

		}

		public async Task BeginListen()
		{
			Console.WriteLine($"getting all channels...");
			foreach (var guild in _discordClient.Guilds)
			{
				Console.WriteLine($"guild {guild.Id}");
				foreach (var channel in guild.Channels)
				{
					if (channel.ChannelType == Discord.ChannelType.Text && channel.Name == "sstv")
					{
						_channels.Add(channel);
						Console.WriteLine($"channel id {channel.Id}: {channel.Name}");
					}
				}
			}

			Console.WriteLine($"setting up MQTT client");

			await StartMQTT();
		}
	}
}
