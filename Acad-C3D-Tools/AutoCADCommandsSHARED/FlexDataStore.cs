using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Dreambuild.AutoCAD
{
    /// <summary>
    /// Flexible data store. FDS is our v3 data store mechanism. Old ways FXD (v1) and CD (v2) should be deprecated.
    /// </summary>
    public class FlexDataStore
    {
        private ObjectId DictionaryId { get; }

        internal FlexDataStore(ObjectId dictionaryId)
        {
            this.DictionaryId = dictionaryId;
        }
        
        /// <summary>
        /// Checks if a key exists.
        /// </summary>
        /// <param name="key">Name of the key.</param>
        /// <returns>True if exists, otherwise false.</returns>
        public bool Has(string key)
        {
            var dictionary = this.DictionaryId.QOpenForRead<DBDictionary>();
            return dictionary.Contains(key);
        }

        /// <summary>
        /// Gets a string value (existing functionality).
        /// </summary>
        public string GetValue(string key)
        {
            var dictionary = this.DictionaryId.QOpenForRead<DBDictionary>();
            if (dictionary.Contains(key))
            {
                var record = dictionary.GetAt(key).QOpenForRead<Xrecord>();
                var data = record.Data.AsArray();

                // If this is old style string data:
                if (data.Length == 1 && data[0].TypeCode == (int)DxfCode.ExtendedDataAsciiString)
                {
                    return data[0].Value.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Removes an entry.
        /// </summary>
        public void RemoveEntry(string key)
        {
            using (var trans = this.DictionaryId.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var dictionary = trans.GetObject(this.DictionaryId, OpenMode.ForWrite) as DBDictionary;
                if (dictionary.Contains(key))
                {
                    trans.GetObject(dictionary.GetAt(key), OpenMode.ForWrite).Erase();
                    dictionary.Remove(key);
                }
                else
                {
                    trans.Abort();
                    return;
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// Sets a string value (existing functionality).
        /// </summary>
        public FlexDataStore SetValue(string key, string value)
        {
            using (var trans = this.DictionaryId.Database.TransactionManager.StartTransaction())
            {
                var dictionary = trans.GetObject(this.DictionaryId, OpenMode.ForWrite) as DBDictionary;
                if (dictionary.Contains(key))
                {
                    trans.GetObject(dictionary.GetAt(key), OpenMode.ForWrite).Erase();
                }

                var record = new Xrecord
                {
                    Data = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataAsciiString, value))
                };

                dictionary.SetAt(key, record);
                trans.AddNewlyCreatedDBObject(record, true);
                trans.Commit();
            }

            return this;
        }

        /// <summary>
        /// Stores an arbitrary object by serializing to JSON, compressing, and splitting into 255-byte chunks.
        /// </summary>
        public FlexDataStore SetObject(string key, object obj)
        {
            // Serialize the object to JSON
            string jsonString = JsonSerializer.Serialize(obj);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);

            // Compress the data
            byte[] compressedData;
            using (var msDest = new MemoryStream())
            {
                using (var gz = new GZipStream(msDest, CompressionMode.Compress, true))
                {
                    gz.Write(jsonBytes, 0, jsonBytes.Length);
                }
                compressedData = msDest.ToArray();
            }

            // Split into chunks of 255 bytes
            const int chunkSize = 255;
            var buffer = new ResultBuffer();
            int position = 0;
            int remaining = compressedData.Length;

            while (remaining > 0)
            {
                int size = Math.Min(chunkSize, remaining);
                byte[] chunk = new byte[size];
                Buffer.BlockCopy(compressedData, position, chunk, 0, size);
                buffer.Add(new TypedValue((int)DxfCode.BinaryChunk, chunk));
                position += size;
                remaining -= size;
            }

            // Store in the dictionary
            using (var trans = this.DictionaryId.Database.TransactionManager.StartTransaction())
            {
                var dictionary = trans.GetObject(this.DictionaryId, OpenMode.ForWrite) as DBDictionary;
                if (dictionary.Contains(key))
                {
                    trans.GetObject(dictionary.GetAt(key), OpenMode.ForWrite).Erase();
                }

                using (var record = new Xrecord())
                {
                    record.Data = buffer;
                    dictionary.SetAt(key, record);
                    trans.AddNewlyCreatedDBObject(record, true);
                }

                trans.Commit();
            }

            return this;
        }

        /// <summary>
        /// Retrieves an object by combining binary chunks, decompressing, and deserializing from JSON.
        /// </summary>
        public T GetObject<T>(string key) where T : class
        {
            var dictionary = this.DictionaryId.QOpenForRead<DBDictionary>();
            if (!dictionary.Contains(key))
                return null;

            var record = dictionary.GetAt(key).QOpenForRead<Xrecord>();
            var data = record.Data.AsArray();

            // Combine all binary chunks
            using (var ms = new MemoryStream())
            {
                foreach (var tv in data)
                {
                    if (tv.TypeCode != (int)DxfCode.BinaryChunk)
                    {
                        // Non-binary data encountered; handle as needed (e.g., ignore or throw)
                        continue;
                    }
                    byte[] chunk = tv.Value as byte[];
                    ms.Write(chunk, 0, chunk.Length);
                }

                // Decompress
                ms.Position = 0;
                using (var decompressedMs = new MemoryStream())
                {
                    using (var gz = new GZipStream(ms, CompressionMode.Decompress, true))
                    {
                        gz.CopyTo(decompressedMs);
                    }

                    decompressedMs.Position = 0;
                    byte[] decompressedData = decompressedMs.ToArray();
                    string jsonString = Encoding.UTF8.GetString(decompressedData);

                    // Deserialize the JSON to an object of type T
                    return JsonSerializer.Deserialize<T>(jsonString);
                }
            }
        }
    }

    public static class FlexDataStoreExtensions
    {
        internal const string DwgGlobalStoreName = "FlexDataStore";

        public static FlexDataStore FlexDataStore(this Database db)
        {
            using (var trans = db.TransactionManager.StartTransaction())
            {
                var namedObjectsDict = trans.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead) as DBDictionary;
                if (!namedObjectsDict.Contains(FlexDataStoreExtensions.DwgGlobalStoreName))
                {
                    namedObjectsDict.UpgradeOpen();
                    var dwgGlobalStore = new DBDictionary();
                    var storeId = namedObjectsDict.SetAt(FlexDataStoreExtensions.DwgGlobalStoreName, dwgGlobalStore);
                    trans.AddNewlyCreatedDBObject(dwgGlobalStore, true);
                    trans.Commit();
                    return new FlexDataStore(storeId);
                }

                trans.Abort();
                return new FlexDataStore(namedObjectsDict.GetAt(FlexDataStoreExtensions.DwgGlobalStoreName));
            }
        }

        public static FlexDataStore FlexDataStore(this ObjectId id, bool createNew = false)
        {
            using (var trans = id.Database.TransactionManager.StartTransaction())
            {
                var dbo = trans.GetObject(id, OpenMode.ForRead);
                if (dbo.ExtensionDictionary == ObjectId.Null && createNew)
                {
                    dbo.UpgradeOpen();
                    dbo.CreateExtensionDictionary();
                    trans.Commit();
                    return new FlexDataStore(dbo.ExtensionDictionary);
                }
                else if (dbo.ExtensionDictionary == ObjectId.Null && !createNew)
                {
                    trans.Abort();
                    return null;
                }

                trans.Abort();
                return new FlexDataStore(dbo.ExtensionDictionary);
            }
        }
    }

}
