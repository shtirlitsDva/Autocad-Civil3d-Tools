using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.Graphs
{
    public class Con
    {
        public static Regex ConRegex = new(
            @"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*);");
        public EndType OwnEndType { get; }
        public EndType ConEndType { get; }
        public Handle ConHandle { get; }
        public Handle OwnHandle { get; set; }
        public Con(string ownEndType, string conEndType, string handle)
        {
            int ownEndTypeInt = Convert.ToInt32(ownEndType);
            OwnEndType = (EndType)ownEndTypeInt;
            int conEndTypeInt = Convert.ToInt32(conEndType);
            ConEndType = (EndType)conEndTypeInt;
            ConHandle = new Handle(Convert.ToInt64(handle, 16));
        }
        internal static Con[] ParseConString(string conString)
        {
            Con[] cons;
            if (ConRegex.IsMatch(conString))
            {
                var matches = ConRegex.Matches(conString);
                cons = new Con[matches.Count];
                int i = 0;
                foreach (Match match in matches)
                {
                    string ownEndTypeString = match.Groups["OwnEndType"].Value;
                    string conEndTypeString = match.Groups["ConEndType"].Value;
                    string handleString = match.Groups["Handle"].Value;
                    cons[i] = new Con(ownEndTypeString, conEndTypeString, handleString);
                    i++;
                }
            }
            else
            {
                throw new System.Exception($"Malforfmed string: {conString}!");
            }

            return cons;
        }
    }
}
