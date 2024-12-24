using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	public struct EspStub
	{
		public string Name { get; }
		public uint EntryPoint { get; }
		public byte[] Text { get; }
		public uint TextStart { get; }
		public byte[] Data { get; }
		public uint DataStart { get; }
		public EspStub(string name, uint entryPoint, byte[] text, uint textStart, byte[] data, uint dataStart)
		{
			Name = name;
			EntryPoint = entryPoint;
			Text = text;
			TextStart = textStart;
			Data = data;
			DataStart = dataStart;
		}
	}
	partial class EspLink
	{
		public bool IsStub { get; private set; }

		async Task<EspStub> GetStubAsync()
		{
			if (Device == null)
			{
				throw new InvalidOperationException("Not connected");
			}
			var chipName = Device.CHIP_NAME;
			var resName = chipName.Replace("(", "").Replace(")", "").Replace("-", "").ToLowerInvariant();
			var names = GetType().Assembly.GetManifestResourceNames();
			// since VS puts them under the root namespace and we don't necessarily know what that is, we look through everything
			var searchIdx = $".Stubs.{resName}.idx";
			string idxPath = null;
			string pathRoot = null;
			for (int i = 0; i < names.Length; ++i)
			{
				var name = names[i];
				if (name.EndsWith(searchIdx, StringComparison.Ordinal))
				{
					idxPath = name;
					pathRoot = idxPath.Substring(0, idxPath.Length - 4);
					break;
				}
			}
			if (idxPath == null)
			{
				throw new NotSupportedException($"The chip \"{chipName}\" is not supported");
			}
			uint entryPoint, textStart, dataStart;
			using (var stm = GetType().Assembly.GetManifestResourceStream(idxPath))
			{
				var ba = new byte[4];
				await stm.ReadAsync(ba, 0, 4);
				entryPoint = BitConverter.ToUInt32(ba, 0);
				await stm.ReadAsync(ba, 0, 4);
				textStart = BitConverter.ToUInt32(ba, 0);
				await stm.ReadAsync(ba, 0, 4);
				dataStart = BitConverter.ToUInt32(ba, 0);
				if (!BitConverter.IsLittleEndian)
				{
					entryPoint = SwapBytes(entryPoint);
					textStart = SwapBytes(textStart);
					dataStart = SwapBytes(dataStart);
				}
			}
			byte[] text = null;
			using (var stm = GetType().Assembly.GetManifestResourceStream($"{pathRoot}.text"))
			{
				text = new byte[stm.Length];
				await stm.ReadAsync(text, 0, text.Length);
			}
			byte[] data = null;
			using (var stm = GetType().Assembly.GetManifestResourceStream($"{pathRoot}.data"))
			{
				data = new byte[stm.Length];
				await stm.ReadAsync(data, 0, data.Length);
			}
			return new EspStub(resName, entryPoint, text, textStart, data, dataStart);
		}
		async Task WriteStubEntryAsync(CancellationToken cancellationToken, uint offset, byte[] data, int timeout = -1,IProgress<int> progress=null)
		{
			var len = (uint)data.Length;
			var blocks = (len + Device.ESP_RAM_BLOCK - 1)/Device.ESP_RAM_BLOCK;
			progress?.Report(0);
			await BeginWriteMemoryAsync(len, blocks, Device.ESP_RAM_BLOCK, offset,cancellationToken, timeout);
			for (uint seq = 0; seq < blocks; ++seq)
			{
				progress?.Report((int)((seq*100)/blocks));
				var fromOffs = seq * Device.ESP_RAM_BLOCK;
				var toWrite = len-fromOffs;
				if(toWrite>Device.ESP_RAM_BLOCK)
				{
					toWrite = Device.ESP_RAM_BLOCK;
				}
				await WriteMemoryBlockAsync(data, (int)fromOffs, (int)toWrite, seq,cancellationToken, timeout);
			}
			progress?.Report(100);
		}
		/// <summary>
		/// Loads the stub for the device into device memory and executes it
		/// </summary>
		/// <param name="timeout">The timeout for the suboperations</param>
		/// <param name="progress">A <see cref="IProgress{Int32}"/> which reports progress</param>
		public void RunStub(int timeout=-1,IProgress<int> progress=null)
		{
			RunStubAsync(CancellationToken.None, timeout, progress).Wait();
		}
		/// <summary>
		/// Asynchronously loads the stub for the device into device memory and executes it
		/// </summary>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation</param>
		/// <param name="timeout">The timeout for each suboperation</param>
		/// <param name="progress">A <see cref="IProgress{Int32}"/> that will report the progress</param>
		/// <returns>An awaitable <see cref="Task"/></returns>
		/// <exception cref="InvalidOperationException">The device isn't connected or the stub is already running</exception>
		/// <exception cref="IOException">The stub did not send an acknowledgment</exception>
		public async Task RunStubAsync(CancellationToken cancellationToken, int timeout = -1, IProgress<int> progress=null)
		{
			if(Device==null)
			{
				throw new InvalidOperationException("Not connected");
			}
			if(IsStub)
			{
				throw new InvalidOperationException("Stub already running");
			}
			
			var stub = await GetStubAsync();
			if(stub.Text!=null)
			{
				await WriteStubEntryAsync(cancellationToken, stub.TextStart, stub.Text, timeout,progress);
			}
			if (stub.Data!= null)
			{
				await WriteStubEntryAsync(cancellationToken, stub.DataStart, stub.Data, timeout);
			}
			await FinishWriteMemoryAsync(cancellationToken, stub.EntryPoint);
			// we're expecting something from the stub
			// waiting for a special SLIP frame from the stub: 0xC0 0x4F 0x48 0x41 0x49 0xC0
			// it's not a response packet so we can't use the normal code with it
			var frame = ReadFrame(cancellationToken, timeout);
			if (frame.Length == 4 && frame[0] == 0x4f && frame[1] == 0x48 && frame[2] == 0x41 && frame[3] == 0x49)
			{
				IsStub = true;
			}
			if (!IsStub)
			{
				throw new IOException("The stub was not successfully executed");
			}
		}
	}
}
