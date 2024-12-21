# EspMon3

EspMon is a PC hardware monitor that displays CPU/GPU temps and usages.

It is a platform IO project which contains a Visual Studio companion app under EspMon

You need supported hardware flashed with the firmware, and then you need to run the companion app, select your Esp32's COM Ports(s) (It can display to multiple devices simultaneously) and click "Started"

You may click "Persistent" to install as a system service. This can take several seconds to install/uninstall. If you do this your settings will be persisted past your login and past the life of the application.

Clicking "Flash Device >" gives you the option to flash the firmware to any connected supported device.

Current device support (other ESP32 based devices can easily be added by editing lcd_config.h and platformio.ini):

- Lilygo TTGO T1 Display
- Lilygo TTGO T-Display S3
- M5 Stack Core 2
- M5 Stack Fire
- Espressif ESP_WROVER_KIT 4.1
- Makerfabs ESP Display Parellel 3.5inch
- Makerfabs ESP Display 4inch**
- Makerfabs ESP Display 4.3inch**
- Makerfabs ESP Display 7inch (1024x600)
- Waveshare ESP32S3 4.3inch

** presently display artifacts due to lcd_config.h settings issues

### Devices with multiple USB inputs

Select the UART Bridge/TTL port, not the USB native port. This project only works over a UART bridge.

### Graph axis explanation

The displayed graph is vertically 1 horizontal line for every 10%, and horizontally one vertical line for every 5 seconds.

### Instructions for adding more devices

1. Edit `include/lcd_config.h` to add an entry with your display's specific settings and wiring. Use one of the existing devices as a template, and then change the `#define`name from the original (like `M5STACK_CORE2` to your own name)

2. Edit the `platformio.ini` to add an entry for your device. Make sure to add your new `#define` name for the lcd settings to your `build_flags` entry. Use one of the existing entries as a template.

3. Build the project

4. Optionally edit the `copyfw.cmd` batch file to add an entry for copying firmware bin out to a friendly name in the root project directory. Use existing lines as a template.

5. Take the `firmware.bin`, renamed to a friendly name, and add it to `EspMon/firmware.zip`

This will add the code to support your display as well as add it to the list of flashable devices in the application.

### Serial protocol

The serial protocol is very simple. Ten times a second, the device sends 1 byte across the wire with a value of `0x01`. When it's received the host sends 7 bytes, starting with 0x01, and then values as indicated in `include/serial.hpp`'s `response_t` structure. Those values are used to update the display. If the device does not receive a response from the host for 1 second, it displays [ DISCONNECTED ] until it gets a signal again.

