using System;

namespace PipeScheduleV2Tests
{
	public sealed class Ps2SkipException : Exception
	{
		public Ps2SkipException(string message) : base(message) { }
	}
}