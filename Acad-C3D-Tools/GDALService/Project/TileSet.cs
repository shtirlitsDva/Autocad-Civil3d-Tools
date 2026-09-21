namespace GDALService.Project;

// One tile as it is on disk now. Two listings with equal tiles describe the same
// terrain, which is what lets SET_PROJECT reuse an open raster.
internal sealed record TileFile(string Path, long Length, DateTime LastWriteUtc);

// A project's tiles as found: from which folder, and exactly which files. Never
// empty - a project without tiles is a NotFound, not an empty set.
internal sealed record TileSet(string BasePath, string ElevationsDir, IReadOnlyList<TileFile> Tiles);
