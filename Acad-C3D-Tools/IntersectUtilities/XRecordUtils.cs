using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices
{
    public static class Extension
    {
        public static T GetObject<T>(
            this ObjectId id,
            OpenMode mode = OpenMode.ForRead,
            bool openErased = false,
            bool forceOpenOnLockedLayer = false)
            where T : DBObject
        {
            if (id.IsNull)
                throw new Runtime.Exception(ErrorStatus.NullObjectId);
            Transaction tr = id.Database.TransactionManager.TopTransaction;
            if (tr == null)
                throw new Runtime.Exception(ErrorStatus.NoActiveTransactions);
            return (T)tr.GetObject(id, mode, openErased, forceOpenOnLockedLayer);
        }

        public static DBDictionary TryGetExtensionDictionary(this DBObject source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            ObjectId dictId = source.ExtensionDictionary;
            if (dictId == ObjectId.Null)
            {
                return null;
            }
            return dictId.GetObject<DBDictionary>();
        }

        public static DBDictionary GetOrCreateExtensionDictionary(this DBObject source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.ExtensionDictionary == ObjectId.Null)
            {
                source.UpgradeOpen();
                source.CreateExtensionDictionary();
            }
            return source.ExtensionDictionary.GetObject<DBDictionary>();
        }

        public static ResultBuffer GetXDictionaryXrecordData(this DBObject source, string key)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));
            DBDictionary xdict = source.TryGetExtensionDictionary();
            if (xdict == null)
            {
                return null;
            }
            return xdict.GetXrecordData(key);
        }

        public static void SetXDictionaryXrecordData(this DBObject target, string key, params TypedValue[] values)
        {
            target.SetXDictionaryXrecordData(key, new ResultBuffer(values));
        }

        public static void SetXDictionaryXrecordData(this DBObject target, string key, ResultBuffer data)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));
            target.GetOrCreateExtensionDictionary().SetXrecordData(key, data);
        }

        public static ResultBuffer GetXrecordData(this DBDictionary dict, string key)
        {
            if (dict == null)
                throw new ArgumentNullException(nameof(dict));
            if (!dict.Contains(key))
            {
                return null;
            }
            ObjectId id = (ObjectId)dict[key];
            if (id.ObjectClass != RXObject.GetClass(typeof(Xrecord)))
                return null;
            return id.GetObject<Xrecord>().Data;
        }

        public static void SetXrecordData(this DBDictionary dict, string key, ResultBuffer data)
        {
            if (dict == null)
                throw new ArgumentNullException(nameof(dict));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));
            Transaction tr = dict.Database.TransactionManager.TopTransaction;
            if (tr == null)
                throw new Runtime.Exception(ErrorStatus.NoActiveTransactions);
            Xrecord xrec;
            if (dict.Contains(key))
            {
                xrec = ((ObjectId)dict[key]).GetObject<Xrecord>(OpenMode.ForWrite);
            }
            else
            {
                dict.UpgradeOpen();
                xrec = new Xrecord();
                dict.SetAt(key, xrec);
                tr.AddNewlyCreatedDBObject(xrec, true);
            }
            xrec.Data = data;
        }
    }
}

