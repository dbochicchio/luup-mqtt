using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Newtonsoft.Json.Linq;
using Openluup.MqttBridge.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Openluup.MqttBridge.Services.Mqtt
{
	public class MQTTServer : BaseService
	{
		private IMqttServer mqttServer;
		private readonly ICollection<MqttDevice> devices = new HashSet<MqttDevice>();
		private readonly IConfiguration configuration;
		private readonly IServiceProvider serviceProvider;
		private readonly LuupWrapper luupWrapper;
		private readonly string username;
		private readonly string password;
		private readonly int port;

		public MQTTServer(IConfiguration configuration, LuupWrapper luaWrapper, IServiceProvider serviceProvider)
		{
			this.configuration = configuration;
			this.serviceProvider = serviceProvider;
			this.luupWrapper = luaWrapper;

			this.username = configuration["mqtt:username"];
			this.password = configuration["mqtt:password"];
			this.port = Convert.ToInt32(configuration["mqtt:port"]);
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			this.CancellationToken = cancellationToken;

			await LoadConfigurationAsync();

			Log.Information("[{where}] Broker is starting on port {port}...", nameof(MQTTServer), port);

			mqttServer = new MqttFactory().CreateMqttServer();

			var optionsBuilder = new MqttServerOptionsBuilder()
				.WithConnectionValidator(c =>
				{
					if (c.Username == username && c.Password == password)
						c.ReasonCode = MqttConnectReasonCode.Success;
					else
						c.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
				})
				.WithConnectionBacklog(100)
				.WithDefaultEndpointPort(port)
				.WithApplicationMessageInterceptor(async context =>
				{
					Log.Verbose("[{where}] Message Received...\r\n[{client}] - {topic} - {msg}", nameof(MQTTServer), context.ClientId ?? "n/a", context.ApplicationMessage.Topic, context.ApplicationMessage.ConvertPayloadToString());

					if (context?.ClientId != null)
					{
						// handle multiple devices mapped to the same payload
						var clients = devices.Where(x => x.ClientId == context.ClientId);

						Parallel.ForEach(clients, async (device) =>
						{
							if (device.TopicName.Equals(context.ApplicationMessage.Topic, StringComparison.InvariantCultureIgnoreCase))
								await ProcessMessageAsync(device, context.ApplicationMessage);
						});
					}

					CancelIfNeeded();
				});

			var options = optionsBuilder.Build() as MqttServerOptions;

			try
			{
				await mqttServer.StartAsync(options);
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"[{nameof(MqttServer)}].{nameof(StartAsync)}");
			}
		}

		private async Task ProcessMessageAsync(MqttDevice device, MqttApplicationMessage applicationMessage)
		{
			string value;
			bool matched;

			// check for exact match
			if (string.IsNullOrEmpty(device.TopicPath))
			{
				var messageValue = applicationMessage.ConvertPayloadToString();
				matched = messageValue.Equals(device.TopicValue, StringComparison.InvariantCultureIgnoreCase);
				value = device.Value;

				Log.Verbose("[{where}] Processed message: got {messageValue} - expected {value} - matched: {matched}", nameof(MQTTServer), messageValue, value, matched);
			}
			else
			{
				// read the value from the payload, using the path
				var jsonObject = JObject.Parse(applicationMessage.ConvertPayloadToString());
				value = (string)jsonObject.SelectToken(device.TopicPath);
				matched = true;

				Log.Verbose("[{where}] Processed message: got {value}", nameof(MQTTServer), value);
			}

			if (matched)
			{
				Log.Verbose("Matched value");
				await luupWrapper.UpdateVariablesAsync(device.DeviceID, device.Service, device.Variable, value);
			}
		}

		private async Task LoadConfigurationAsync()
		{
			var deviceSection = configuration.GetSection("devices").GetChildren();
			foreach (var item in deviceSection)
			{
				try
				{
					var node = item.GetChildren();

					var device = new MqttDevice
					{
						// generic
						ClientId = GetSettingsParam(node, "clientID"),

						// mqtt
						TopicName = GetSettingsParam(node, "TopicName"),
						TopicValue = GetSettingsParam(node, "TopicValue"),
						TopicPath = GetSettingsParam(node, "TopicPath"),

						// vera/openluup
						DeviceID = Convert.ToInt32(GetSettingsParam(node, "deviceID")),
						Service = GetSettingsParam(node, "Service"),
						Variable = GetSettingsParam(node, "Variable"),
						Value = GetSettingsParam(node, "Value")
					};

					// TODO: enforce config check at startups
					this.devices.Add(device);

					Log.Verbose("[{where}] MQTT Client Configuration loaded: {a}", nameof(MQTTServer), device.ClientId);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "[{where}] {what}.LoadConfiguration MQTT Server...", nameof(MQTTServer), nameof(StartAsync));
				}
			}

			await Task.CompletedTask;
		}

		private string GetSettingsParam(IEnumerable<IConfigurationSection> node, string nodeName) => node.FirstOrDefault(x => x.Key.Equals(nodeName, StringComparison.InvariantCultureIgnoreCase))?.Value;

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await mqttServer?.StopAsync();
			CancelIfNeeded();
		}
	}
}
