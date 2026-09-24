using GDALService.Common;

namespace GDALService.Hosting;

// The three streams the service talks on: requests in, replies out, and the
// READY/PROGRESS/diagnostic lines on the error stream. The console in the
// executable, strings in the tests.
internal sealed record ServiceStreams(TextReader Input, TextWriter Output, TextWriter Error);

// How GDAL's start-up went, for the READY line: its release name, or why it
// could not load.
internal sealed record GdalStatus(Result<string> Release);
