using Json;
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

		EspStub GetStub()
		{
			if(Device==null)
			{
				throw new InvalidOperationException("Not connected");
			}
			var chipName = Device.CHIP_NAME;
			var jsonName = chipName.Replace("(", "").Replace(")", "").Replace("-", "").ToLowerInvariant();
			var names = GetType().Assembly.GetManifestResourceNames();
			// since VS puts them under the root namespace and we don't necessarily know what that is, we look through everything
			var search = $".Stubs.{jsonName}.json";
			string respath = null;
			for (int i = 0;i<names.Length;++i)
			{
				var name = names[i];
				if(name.EndsWith(search, StringComparison.Ordinal))
				{
					respath = name; break;
				}

			}
			if (respath == null)
			{
				throw new NotSupportedException($"The chip \"{chipName}\" is not supported");
			}
			using (var stm = GetType().Assembly.GetManifestResourceStream(respath))
			{
				var reader = new StreamReader(stm, Encoding.UTF8);
				dynamic json = JsonObject.Parse(reader);
				var entryPoint = (uint)json.entry;
				var text = Convert.FromBase64String(json.text);
				var textStart = (uint)json.text_start;
				var data = Convert.FromBase64String(json.data);
				var dataStart = (uint)json.data_start;
				return new EspStub(jsonName,entryPoint,text, textStart, data,dataStart);
			}
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
		public void RunStub(int timeout=-1,IProgress<int> progress=null)
		{
			RunStubAsync(CancellationToken.None, timeout, progress).Wait();
		}
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
			
			var stub = GetStub();
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
			if(frame.Length==4 && frame[0]==0x4f && frame[1]==0x48 && frame[2]==0x41 && frame[3]==0x49)
			{
				IsStub = true;
				return;
			}
			throw new IOException("The stub was not successfully executed");
		}
	}
}
