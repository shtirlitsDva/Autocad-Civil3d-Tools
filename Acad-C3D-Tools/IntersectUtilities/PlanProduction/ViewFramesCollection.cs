using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Collections;

namespace IntersectUtilities.PlanProduction
{
    internal class ViewFramesCollection
    {
        public static ViewFramesCollection Load(string folderPath)
        {
            var path = Path.Combine(folderPath, "ViewFramesCollection.json");
            if (!File.Exists(path)) throw new Exception("ViewFramesCollection.json not found!");
            var json = File.ReadAllText(path);
            var vfc = JsonSerializer.Deserialize<ViewFramesCollection>(json);
            if (vfc is null) throw new Exception("Failed to deserialize ViewFramesCollection!");
            vfc.Validate();
            return vfc;
        }

        [JsonInclude]
        public string FolderPath { get; }

        [JsonInclude]
        public List<ViewFrameDrawing> ViewFrames { get; }

        public ViewFramesCollection(string folderPath)
        {
            ViewFrames = new();
            FolderPath = folderPath;
        }

        internal void Save()
        {
            if (string.IsNullOrEmpty(FolderPath)) throw new Exception("FolderPath is not set!");
            var json = JsonSerializer.Serialize(this);
            var path = Path.Combine(FolderPath, "ViewFramesCollection.json");
            File.WriteAllText(path, json);
        }

        internal void Validate()
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                var item = this[i];
                if (!File.Exists(Path.Combine(FolderPath, item.FileName))) RemoveAt(i);
            }
        }

        public int Count => ViewFrames.Count;

        public bool IsReadOnly => ((ICollection<ViewFrameDrawing>)ViewFrames).IsReadOnly;

        public ViewFrameDrawing this[int index]
        {
            get => ViewFrames[index];
            set => ViewFrames[index] = value;
        }

        public int IndexOf(ViewFrameDrawing item)
        {
            return ViewFrames.IndexOf(item);
        }

        public void Insert(int index, ViewFrameDrawing item)
        {
            ViewFrames.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ViewFrames.RemoveAt(index);
        }

        public void Add(ViewFrameDrawing item)
        {
            ViewFrames.Add(item);
        }

        public void Clear()
        {
            ViewFrames.Clear();
        }

        public bool Contains(ViewFrameDrawing item)
        {
            return ViewFrames.Contains(item);
        }

        public void CopyTo(ViewFrameDrawing[] array, int arrayIndex)
        {
            ViewFrames.CopyTo(array, arrayIndex);
        }

        public bool Remove(ViewFrameDrawing item)
        {
            return ViewFrames.Remove(item);
        }

        public IEnumerator<ViewFrameDrawing> GetEnumerator()
        {
            return ViewFrames.GetEnumerator();
        }
    }
}
