using System;

namespace PipeScheduleV2Tests
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class Ps2TestAttribute : Attribute
	{
		public Ps2TestAttribute() { }
	}
}