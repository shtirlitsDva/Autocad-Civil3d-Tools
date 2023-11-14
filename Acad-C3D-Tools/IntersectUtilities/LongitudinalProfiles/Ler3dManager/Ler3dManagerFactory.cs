using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IntersectUtilities.LongitudinalProfiles
{
    public static class Ler3dManagerFactory
    {
        public static ILer3dManager LoadLer3d(string path)
        {
            if (File.Exists(path))
            {
                UtilsCommon.Utils.prdDbg("Loading Ler 3d from single file!");
                if (Path.GetExtension(path).ToLower() == ".dwg")
                {
                    var obj = new Ler3dManagerFile();
                    obj.Load(path);
                    return obj;
                }
                    
                else throw new Exception("Ler3d has wrong extension: " + path);
            }
            else if (Directory.Exists(path))
            {
                UtilsCommon.Utils.prdDbg("Loading Ler 3d from a collection of files!");
                var obj = new Ler3dManagerFolder();
                obj.Load(path);
                return obj;
            }
            else
            {
                throw new Exception("Ler3d info not found: " + path);
            }            
        }
    }
}
