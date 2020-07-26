using System;
using System.Collections.Generic;
using System.Text;

namespace Openluup.MqttBridge.Model
{
	internal class MqttDevice
	{
		public string ClientId { get; set; }
		public int DeviceID { get; set; }
		public string TopicName { get; set; }
		public string TopicValue { get; set; }
		public string TopicPath { get; set; }
		public string Service { get; set; }
		public string Variable { get; set; }
		public string Value { get; set; }
	}
}
