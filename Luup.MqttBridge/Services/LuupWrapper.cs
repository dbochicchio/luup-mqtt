using Microsoft.Extensions.Configuration;
using Serilog;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace Luup.MqttBridge.Services
{
	public class LuupWrapper
	{
		private readonly string luupIP;
		private readonly string port;
		private readonly IHttpClientFactory httpClientFactory;
		private readonly CultureInfo enCulture = new CultureInfo("en-us");

		public LuupWrapper(IConfiguration configuration, IHttpClientFactory httpClientFactory)
		{
			luupIP = configuration["luup:ipAddress"];
			port = configuration["luup:port"];
			this.httpClientFactory = httpClientFactory;
		}

		public async Task UpdateVariablesAsync(int deviceID, string serviceID, string variableName, string value) => await RunCommandAsync(deviceID, "variableset", "Variable", "Value", variableName, serviceID, value);

		public async Task RunCommandAsync(int deviceID, string action, string actionCommand, string commandParameter, string command, string serviceID, string value)
		{
			var uri = $"http://{luupIP}:{port}/data_request?id={action}&output_format=json&DeviceNum={deviceID}&serviceId={serviceID}&{actionCommand}={HttpUtility.UrlEncode(command)}&{commandParameter}={HttpUtility.UrlEncode(value)}&RunAsync=1";

			var httpClient = httpClientFactory.CreateClient(NamedHttpClients.LuupClient);
			var result = await httpClient.GetStringAsync(uri).ConfigureAwait(false);

			Log.Verbose("[{where}] Calling Uri: {uri} {l}", nameof(LuupWrapper), uri, result.Length);
		}

	}
}
