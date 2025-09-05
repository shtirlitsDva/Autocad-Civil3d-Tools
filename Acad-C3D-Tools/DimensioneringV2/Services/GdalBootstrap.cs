using MaxRev.Gdal.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal static class GdalBootstrap
    {
        private static bool _init;
        public static void Init()
        {
            if (_init) return;
            GdalBase.ConfigureAll();   // sets PATH/GDAL_DATA/PROJ, loads drivers
            //Gdal.AllRegister();
            _init = true;
        }
    }
}
