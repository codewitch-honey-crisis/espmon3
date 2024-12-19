using System;

namespace EL
{
	partial class EspLink : IDisposable
    {
		void Cleanup()
		{
			Device = null;
			_isSpiFlashAttached = false;
		}
		void IDisposable.Dispose()
		{
			Close();
			GC.SuppressFinalize(this);
		}
		~EspLink()
		{
			Close();
		}
	}
}
