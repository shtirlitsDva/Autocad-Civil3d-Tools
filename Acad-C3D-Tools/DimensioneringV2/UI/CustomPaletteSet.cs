﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using utils = IntersectUtilities.UtilsCommon.Utils;

namespace DimensioneringV2.UI
{
    internal class CustomPaletteSet : PaletteSet
    {
        public bool WasVisible { get; set; } = false;

        public CustomPaletteSet() : base("DimV2", "DIM2MAP", new Guid("75B45F74-C728-432B-AC50-9A5F345A3877"))
        {
            Style =
                PaletteSetStyles.ShowAutoHideButton |
                PaletteSetStyles.ShowCloseButton |
                PaletteSetStyles.ShowPropertiesMenu;
            MinimumSize = new System.Drawing.Size(250, 150);
            Location = new System.Drawing.Point(0, 0);

            AddVisual("MAP", new MainWindow());
            AddVisual("SETTINGS", new SettingsTab());
            Activate(0);

            //// automatically hide the palette while none document is active (no document state)
            //var docs = Application.DocumentManager;
            //docs.DocumentBecameCurrent += (s, e) => { Visible = e.Document == null ? false : WasVisible; };//utils.prdDbg("DocumentBecameCurrent fired!"); };
            //docs.DocumentCreated += (s, e) => { Visible = WasVisible; };//utils.prdDbg("DocumentCreated fired!"); };
            //docs.DocumentToBeDeactivated += (s, e) => { WasVisible = Visible; };//utils.prdDbg("DocumentToBeDeactivated fired!"); };
            //docs.DocumentToBeDestroyed += (s, e) =>
            //{
            //    utils.prdDbg("DocumentToBeDestroyed fired!");
            //    WasVisible = Visible;
            //    if (docs.Count == 1)
            //    {
            //        Visible = false;
            //        this.Close();
            //        this.Dispose();
            //        Services.PaletteSetCache.paletteSet = null;
            //    }
            //};
            //this.StateChanged += (s, e) =>
            //{
            //    //utils.prdDbg("this.StateChanged fired!");
            //    //utils.prdDbg($"State changed! V: {Visible}, wV: {WasVisible}");
            //    if (WasVisible)
            //    {
            //        utils.prdDbg("Saving settings!");
            //        HydraulicSettingsSerializer.Save(Application.DocumentManager.MdiActiveDocument,
            //            HydraulicSettingsService.Instance.Settings);
            //    }
            //    WasVisible = false;
            //};
        }
    }
}
