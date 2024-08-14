using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntersectUtilities.PlanProduction
{
    internal class ViewFramesCollection : List<ViewFrameDrawing>
    {
        public static ViewFramesCollection Load(string folderPath)
        {
            var path = System.IO.Path.Combine(folderPath, "ViewFramesCollection.json");
            if (!System.IO.File.Exists(path)) throw new Exception("ViewFramesCollection.json not found!");
            var json = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<ViewFramesCollection>(json);
        }
        public string FolderPath { get; }
        public ViewFramesCollection(string folderPath) : base(new List<ViewFrameDrawing>()) { FolderPath = folderPath; }
        internal void Save()
        {
            if (FolderPath.IsNoE()) throw new Exception("FolderPath is not set!");
            var json = JsonSerializer.Serialize(this);
            var path = System.IO.Path.Combine(FolderPath, "ViewFramesCollection.json");
            System.IO.File.WriteAllText(path, json);
        }
    }
}
