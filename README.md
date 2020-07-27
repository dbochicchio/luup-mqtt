# MQTT Bridge for Luup
This is a MQTT Bridge for Openluup and Mios' Vera Platform (Luup engine).
This could help integration with other ecosystems (Tasmota, Shelly, Home Assistant) using an external process, running indendpently from your Vera/Openluup installation. It's fast, lightweight and it's not crippling your Luup engine stability.
Right now, it's only receiving messages. Sending will be implemented soon.

***Vera early beta. Use at your own risk.***

# Installation and minimal requirements
The broker runs on .NET Core 3.1. Linux, Windows or macOs are OK. See [Microsoft docs](https://docs.microsoft.com/en-us/dotnet/core/install/linux) for more information on running it on Linux. Raspberry are perfectly supported, as well as Docker.
You can build it directly from source. Just download the files, install .NET Core SDK and then run, inside the directory:

```
dotnet build
dotnet publish -c Release -r linux-x64
```

Remove `-r linux-x64` if you want to build a portable app. It's completely fine to build it on Windows and publish on Linux, or macOs, or whatever combination you like.

The application binary will be published under `bin\Release\netcoreapp3.1\linux-x64\publish\`.
All the files here can be copied to your machine. The executable name is `Luup.MqttBridge` (.exe on Windows) and needs permissions to execute.
You can run the application directly (ie from another process).
Refer to your own preferred operating system on how to transform and run this at startup or as service. Systemd under Linux is supported.

# First configuration
Here's an example of configuration:

```
{
	"MQTT": {
		"Username": "luup",
		"Password": "openluup",
		"Port": 1883
	},

	"Luup": {
		"ipAddress": "127.0.0.1",
		"port": 3480
	},

	"Devices": [
		{
			"ClientID": "sonoff-pool",

			"TopicName": "tele/tasmota/SENSOR",
			"TopicPath": "DS18B20.Temperature",

			"DeviceID": 501,
			"Service": "urn:upnp-org:serviceId:TemperatureSensor1",
			"Variable": "CurrentTemperature"
		},

		{
			"ClientID": "tasmota-dehum",

			"TopicName": "stat/sonoff/POWER",
			"TopicValue": "ON",

			"DeviceID": 486,
			"Service": "urn:upnp-org:serviceId:HVAC_UserOperatingMode1",
			"Variable": "ModeTarget",
			"Value": "HeatOn"
		},

		{
			"ClientID": "tasmota-dehum",

			"TopicName": "stat/sonoff/POWER",
			"TopicValue": "OFF",

			"DeviceID": 486,
			"Service": "urn:upnp-org:serviceId:HVAC_UserOperatingMode1",
			"Variable": "ModeTarget",
			"Value": "Off"
		},

		{
			"ClientID": "tasmota-dehum",

			"TopicName": "tele/tasmota/SENSOR",
			"TopicPath": "DS18B20.Temperature",

			"DeviceID": 486,
			"Service": "urn:upnp-org:serviceId:TemperatureSensor1",
			"Variable": "CurrentTemperature"
		},

		{
			"ClientID": "tasmota-dehum",

			"TopicName": "stat/sonoff/POWER",
			"TopicValue": "OFF",

			"DeviceID": 499,
			"Service": "urn:upnp-org:serviceId:SwitchPower1",
			"Variable": "Status",
			"Value": "0"
		},

		{
			"ClientID": "tasmota-dehum",

			"TopicName": "stat/sonoff/POWER",
			"TopicValue": "On",

			"DeviceID": 499,
			"Service": "urn:upnp-org:serviceId:SwitchPower1",
			"Variable": "Status",
			"Value": "1"
		}
	]
}
```

It's self explanatory. Go to `MQTT` section and put your credentials. These will be used by your client devices as well.
In `Luup` section specify your Vera/Openluup IP and port.

`Devices` section has your mappings. You can map a message to different devices, use a part of the payload, or mix and match.

Standard configuration must include:
- *ClientID*: the broker filters the messages by this value. Be sure to set the device client ID, as set in your device.
- *TopicName*: the MQTT topic (case insensitive).
- *DeviceID*: Vera/Openluup deviceID.
- *Service*: Luup service ID.
- *Variable*: The variable to update with the computed value.

If your device send a **fixed value**, you should specify:
- *TopicValue*: if your device send a fixed value (ie `on`), specify the value. It's case insensitive.
- *Value*: The value to be sent as the computed value.

If your device send a **dynamic value**, you should specify:
- TopicPath: specify the path,in JSONPath format. i.e. "DS18B20.Temperature" means to search for a `DS18B20` node, then get the value from `Temperature` node. Values are preserved and sent as received.

Use one or another. If both are specified, the first (fixed value) wins.

# Virtual HTTP Devices plug-in for Vera
If you need to represent virtual devices that performs HTTP calls, you need my other plug-in.
This is 100% compatible with Vera UI/Altui, mobile apps and act as a standard control in that sense.

[More info](https://github.com/dbochicchio/vera/tree/master/VirtualDevices/)

# Logging
By default, verbose logging is enabled. Logs are rotated daily in `_logs`. If you want to disable verbose logging, just uncomment the latest lines from the default config file.
