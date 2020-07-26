using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Openluup.MqttBridge.Services
{
	public abstract class BaseService : IService
	{
		protected internal CancellationToken CancellationToken { get; set; }

		public abstract Task StartAsync(CancellationToken cancellationToken);
		public abstract Task StopAsync(CancellationToken cancellationToken);

		protected void CancelIfNeeded()
		{
			if (CancellationToken != null && CancellationToken.IsCancellationRequested)
				CancellationToken.ThrowIfCancellationRequested();
		}
	}

	public interface IService : IHostedService
	{
	}
}
