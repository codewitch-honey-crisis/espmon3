# EspMon3

EspMon is a PC hardware monitor that displays CPU/GPU temps and usages.

It is a platform IO project which contains a Visual Studio companion app under EspMon

You need supported hardware flashed with the firmware, and then you need to run the companion app, select your Esp32's COM Ports(s) (It can display to multiple devices simultaneously) and click "Started"

You may click Install to install as a system service. This can take several seconds to install/uninstall. If you do this your settings will be persisted past your login and past the life of the application.

Disclaimer: The UI is still a little buggy. If after checking Installed the screen doesn't connect when checking a COM port (even with Started checked) then close the application, reopen it and try again.

