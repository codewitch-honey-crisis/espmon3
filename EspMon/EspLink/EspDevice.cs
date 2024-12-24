using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    class EspDeviceAttribute : Attribute
    {
        public EspDeviceAttribute(string name, uint magic, uint id = 0)
        {
            Name = name;
            Magic = magic;
			Id = id;

		}
		public string Name { get; set; }
        public uint Magic { get; set; }
		public uint Id { get; set; }

	}
    struct EspPartitionEntry {
        public uint Offset { get; } 
        public uint Size { get;  } 
		public string Name { get; }
        public EspPartitionEntry(uint offset,uint size, string name)
        {
            Offset = offset;
            Size = size;
            Name = name;
        }
    }
    /// <summary>
    /// Represents the base class for an Espressif MCU device
    /// </summary>
    public abstract class EspDevice
    {
        private WeakReference<EspLink> _parent;
        protected EspDevice(EspLink parent)
        {
            _parent = new WeakReference<EspLink>(parent);
        }
        protected EspLink Parent {
            get {
                EspLink result;
                if (_parent.TryGetTarget(out result))
                {
                    return result;
                }
                throw new InvalidOperationException("The parent has been disposed");
            }
        }
        public virtual int FlashSize { get =>0; } 
        public abstract uint FLASH_ID
        {
            get;
        }
         
        protected bool SyncStubDetected { get; set; } = false;
        public virtual string CHIP_NAME { get { return GetType().GetCustomAttribute<EspDeviceAttribute>().Name; } }
        public virtual uint CHIP_DETECT_MAGIC_VALUE { get { return GetType().GetCustomAttribute<EspDeviceAttribute>().Magic; } }
        public virtual bool IS_STUB { get; set; } = false;

        // Commands supported by ESP8266 ROM bootloader
        public virtual byte ESP_FLASH_BEGIN { get; } = 0x02;
        public virtual byte ESP_FLASH_DATA { get; } = 0x03;
        public virtual byte ESP_FLASH_END { get; } = 0x04;
        public virtual byte ESP_MEM_BEGIN { get; } = 0x05;
        public virtual byte ESP_MEM_END { get; } = 0x06;
        public virtual byte ESP_MEM_DATA { get; } = 0x07;
        public virtual byte ESP_SYNC { get; } = 0x08;
        public virtual byte ESP_WRITE_REG { get; } = 0x09;
        public virtual byte ESP_READ_REG { get; } = 0x0A;

        // Some commands supported by ESP32 and later chips ROM bootloader (or -8266 w/ stub)
        public virtual byte ESP_SPI_SET_PARAMS { get; } = 0x0B;
        public virtual byte ESP_SPI_ATTACH { get; } = 0x0D;
        public virtual byte ESP_READ_FLASH_SLOW { get; } = 0x0E;  // ROM only, much slower than the stub flash read
        public virtual byte ESP_CHANGE_BAUDRATE { get; } = 0x0F;
        public virtual byte ESP_FLASH_DEFL_BEGIN { get; } = 0x10;
        public virtual byte ESP_FLASH_DEFL_DATA { get; } = 0x11;
        public virtual byte ESP_FLASH_DEFL_END { get; } = 0x12;
        public virtual byte ESP_SPI_FLASH_MD5 { get; } = 0x13;

        // Commands supported by ESP32-S2 and later chips ROM bootloader only
        public virtual byte ESP_GET_SECURITY_INFO { get; } = 0x14;
		public virtual uint EFUSE_RD_REG_BASE { get; } = 0;

		public virtual uint EFUSE_BLK0_RDATA3_REG_OFFS { get => 0; }
		public virtual uint EFUSE_BLK0_RDATA5_REG_OFFS { get => 0; }
		// Some commands supported by stub only
		public virtual byte ESP_ERASE_FLASH { get; } = 0xD0;
        public virtual byte ESP_ERASE_REGION { get; } = 0xD1;
        public virtual byte ESP_READ_FLASH { get; } = 0xD2;
        public virtual byte ESP_RUN_USER_CODE { get; } = 0xD3;

        // Flash encryption encrypted data command
        public virtual byte ESP_FLASH_ENCRYPT_DATA { get; } = 0xD4;

        // Response code(s) sent by ROM
        public virtual byte ROM_INVALID_RECV_MSG { get; } = 0x05;  // response if an invalid message is received

        // Maximum block sized for RAM and Flash writes, respectively.
        public virtual uint ESP_RAM_BLOCK { get; protected set; } = 0x1800;

        public virtual uint FLASH_WRITE_SIZE { get; } = 0x400;

        // Default baudrate. The ROM auto-bauds, so we can use more or less whatever we want.
        public virtual uint ESP_ROM_BAUD { get; } = 115200;

        // First byte of the application image
        public virtual uint ESP_IMAGE_MAGIC { get; } = 0xE9;

        // Initial state for the checksum routine
        public virtual uint ESP_CHECKSUM_MAGIC { get; } = 0xEF;

        // Flash sector size, minimum unit of erase.
        public virtual uint FLASH_SECTOR_SIZE { get; } = 0x1000;

        public virtual uint UART_DATE_REG_ADDR { get; } = 0x60000078;

        // Whether the SPI peripheral sends from MSB of 32-bit register, or the MSB of valid LSB bits.
        public virtual bool SPI_ADDR_REG_MSB { get; } = true;

        // This ROM address has a different value on each chip model
        public virtual uint CHIP_DETECT_MAGIC_REG_ADDR { get; } = 0x40001000;

        public virtual uint UART_CLKDIV_MASK { get; } = 0xFFFFF;

        //  Memory addresses
        public virtual uint IROM_MAP_START { get; } = 0x40200000;
        public virtual uint IROM_MAP_END { get; } = 0x40300000;

        // The number of bytes in the UART response that signify command status
        public virtual ushort STATUS_BYTES_LENGTH { get; } = 2;

        // Bootloader flashing offset
        public virtual uint BOOTLOADER_FLASH_OFFSET { get; } = 0x0;

        // ROM supports an encrypted flashing mode
        public virtual bool SUPPORTS_ENCRYPTED_FLASH { get; } = false;

        // # Device PIDs
        public virtual uint USB_JTAG_SERIAL_PID { get; } = 0x1001;

        //  Chip IDs that are no longer supported by esptool
        public (int Id, string Name)[] UNSUPPORTED_CHIPS { get; } = { (6, "ESP32-S3(beta 3)") };

        // Whether the SPI peripheral sends from MSB of 32-bit register, or the MSB of valid LSB bits.
        
		public virtual uint SPI_REG_BASE { get; } = 0;
		public virtual byte SPI_USR_OFFS { get; } = 0;
		public virtual byte SPI_USR1_OFFS { get; } = 0;
		public virtual byte SPI_USR2_OFFS { get; } = 0;
		public virtual short SPI_MOSI_DLEN_OFFS { get; } = -1;
		public virtual short SPI_MISO_DLEN_OFFS { get; } = -1;
        public virtual byte SPI_W0_OFFS { get; } = 0;
        protected virtual Task OnConnectedAsync(CancellationToken cancellationToken, int timeout)
        {
            return Task.CompletedTask;
        }
        public async Task ConnectAsync(CancellationToken cancellationToken, int timeout = -1)
        {
            await OnConnectedAsync(cancellationToken, timeout);
        }
	}
    
}