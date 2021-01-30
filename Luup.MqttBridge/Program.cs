using Luup.MqttBridge.Services;
using Luup.MqttBridge.Services.Mqtt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Luup.MqttBridge
{
	internal class Program
	{
		public static CancellationTokenSource CancellationToken { get; } = new CancellationTokenSource();
		public static Version Version => new Version(0, 31, 210130);

		public static async Task Main(string[] args)
		{
			try
			{
				var host = CreateHostBuilder(args).Build();
				await host.RunAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[{DateTime.Now:HH:mm:ss} CON] v {Version} FATAL: {ex}");
			}
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.UseSystemd()
				.ConfigureServices((hostContext, services) =>
				{
					services.AddOptions();
					services.AddSingleton<LuupWrapper>();
					services.AddHostedService<MQTTServer>();

					// HTTP CLIENTS
					services.AddHttpClient(NamedHttpClients.LuupClient, client =>
						{
							//client.DefaultRequestVersion = new Version(2, 0); // HTTP 2
							client.Timeout = TimeSpan.FromMilliseconds(double.Parse(hostContext.Configuration["luup:timeout"] ?? "10000"));
							client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4204.0 Safari/537.36 Edg/86.0.587.0");
							client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
						})
						.AddPolicyHandler(Policy.BulkheadAsync<HttpResponseMessage>(1, 500))
						.AddPolicyHandler(HttpPolicyExtensions
											.HandleTransientHttpError()
											.OrResult(msg => (int)msg.StatusCode >= 500)
											.WaitAndRetryAsync(int.Parse(hostContext.Configuration["luup:retries"] ?? "5"), retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * double.Parse(hostContext.Configuration["luup:retryAttemptTimeout"] ?? "500"))));

				})
				.ConfigureAppConfiguration((hostingContext, config) =>
				{
					var env = hostingContext.HostingEnvironment;

					var path = GetCurrentPath();

					config.SetBasePath(path)
							.AddJsonFile("appsettings.json", optional: true)
							.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
							.AddEnvironmentVariables();
				})
				.UseSerilog((hostingContext, loggerConfiguration) =>
				{
					var path = GetCurrentPath();

					// Initialize serilog logger
					var logPath = path + "/_logs/log-.txt";

					loggerConfiguration
						.Enrich.FromLogContext()
						.Enrich.WithDemystifiedStackTraces()
						.WriteTo.File(logPath,
							buffered: true,
							rollOnFileSizeLimit: true,
							rollingInterval: RollingInterval.Day,
							retainedFileCountLimit: 15,
							flushToDiskInterval: TimeSpan.FromSeconds(10),
							fileSizeLimitBytes: 1_000_000) // 1 MB
						.WriteTo.Console(theme: AnsiConsoleTheme.Code)
#if DEBUG
						.MinimumLevel.Verbose()
						.MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Error)
#else
						.MinimumLevel.Information()
						.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Error)
						.MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Error)
#endif
				;
				});

		private static string GetCurrentPath() => Path.GetDirectoryName(AppContext.BaseDirectory);
	}

	internal class NamedHttpClients
	{
		public static string LuupClient => "Luup";
	}
}
