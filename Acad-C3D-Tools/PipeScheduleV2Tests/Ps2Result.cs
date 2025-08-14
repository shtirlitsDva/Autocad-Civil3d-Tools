using System;

namespace PipeScheduleV2Tests
{
	public sealed class Ps2Result
	{
		public string Name { get; set; } = string.Empty;
		public Ps2Status Status { get; set; }
		public string Message { get; set; } = string.Empty;
		public string StackTrace { get; set; } = string.Empty;
		public TimeSpan Duration { get; set; }
	}
}