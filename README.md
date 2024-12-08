# EspMon3

EspMon is a PC hardware monitor that displays CPU/GPU temps and usages.

It is a platform IO project which contains a Visual Studio companion app under EspMon

You need supported hardware flashed with the firmware, and then you need to run the companion app, select your Esp32's COM Ports(s) (It can display to multiple devices simultaneously) and click "Started"

You may click Install to install as a system service. This can take several seconds to install/uninstall. If you do this your settings will be persisted past your login and past the life of the application.

Current device support (other ESP32 based devices can easily be added by editing lcd_config.h and platformio.ini):

- Lilygo TTGO T1 Display
- Lilygo TTGO T-Display S3
- M5 Stack Core 2
- M5 Stack Fire
- Espressif ESP_WROVER_KIT 4.1
- Makerfabs ESP Display Parellel 3.5inch
- Makerfabs ESP Display 4inch*
- Makerfabs ESP Display 4.3inch*
- Makerfabs ESP Display 7inch (1024x600)
- Waveshare ESP32S3 4.3inch
