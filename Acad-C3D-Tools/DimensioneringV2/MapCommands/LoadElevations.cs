using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.MapCommands
{
    internal class LoadElevations
    {
        internal static async void Execute()
        {
            try
            {
                var docs = AcAp.DocumentManager;
                var ed = docs.MdiActiveDocument.Editor;

                await docs.ExecuteInCommandContextAsync(
                    async (obj) =>
                    {
                        await ed.CommandAsync("DIM2MAPLOADELEVATIONS");
                    }, null
                    );
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg($"Error in LoadElevations: {ex.Message}");
            }
        }
    }
}
